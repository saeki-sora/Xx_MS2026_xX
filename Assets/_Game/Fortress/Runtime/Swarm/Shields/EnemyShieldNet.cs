using System;
using System.Collections.Generic;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 盾の向きの通信（名前つきメッセージ <see cref="MessageName"/>）。盾の向きと「にらむ相手」は Host が決め、1秒に15回配る。
    /// 1枚あたり5バイト（敵の番号2・向き2・相手1）。盾持ちの位置は群衆の写真で届くので送らない。
    /// Client は届いた向きへ回す（<see cref="EnemyShieldDirector"/>）。
    /// </summary>
    internal sealed class EnemyShieldNet
    {
        public const string MessageName = "MS2026.Fortress.Shields";
        private const float SendInterval = 1f / 15f;
        private const int BytesPerShield = 5;

        public struct State
        {
            public float angleDegrees;
            public int focus;
        }

        private readonly Dictionary<int, State> _received = new Dictionary<int, State>();
        private NetworkManager _registered;
        private float _nextSend;

        public bool TryGet(int netId, out State state)
        {
            return _received.TryGetValue(netId, out state);
        }

        public void Forget(int netId)
        {
            _received.Remove(netId);
        }

        public void Tick(IReadOnlyList<EnemyShieldView> views)
        {
            EnsureMessaging();
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1 || views.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextSend)
            {
                return;
            }

            _nextSend = now + SendInterval;
            var max = (NetMessageLimits.UnreliableMaxBytes - NetMessageLimits.HeaderAllowanceBytes - sizeof(ushort)) / BytesPerShield;
            var count = Mathf.Min(views.Count, max);
            using var writer = new FastBufferWriter(sizeof(ushort) + count * BytesPerShield, Allocator.Temp);
            writer.WriteValueSafe((ushort)count);
            for (var i = 0; i < count; i++)
            {
                var view = views[i];
                writer.WriteValueSafe((ushort)view.NetId);
                writer.WriteValueSafe((ushort)Mathf.RoundToInt(Mathf.Repeat(view.AngleDegrees, 360f) / 360f * 65535f));
                writer.WriteValueSafe((sbyte)Mathf.Clamp(view.Focus, -1, 3));
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.UnreliableSequenced);
        }

        public void Dispose()
        {
            Unregister();
            _received.Clear();
        }

        private void EnsureMessaging()
        {
            var manager = NetworkManager.Singleton;
            var listening = manager != null && manager.IsListening && manager.CustomMessagingManager != null;
            if (!listening)
            {
                Unregister();
                return;
            }

            if (ReferenceEquals(_registered, manager))
            {
                return;
            }

            Unregister();
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnReceived);
            _registered = manager;
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                _registered.CustomMessagingManager?.UnregisterNamedMessageHandler(MessageName);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        private void OnReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority)
            {
                return;
            }

            reader.ReadValueSafe(out ushort count);
            for (var i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ushort id);
                reader.ReadValueSafe(out ushort angle);
                reader.ReadValueSafe(out sbyte focus);
                _received[id] = new State { angleDegrees = angle / 65535f * 360f, focus = focus };
            }
        }
    }
}
