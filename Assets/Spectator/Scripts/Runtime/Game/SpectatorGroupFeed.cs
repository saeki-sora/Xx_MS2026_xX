using System;
using System.Collections.Generic;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Spectator
{
    /// <summary>
    /// いま画面に出す観客の群れ（メインスレッドだけ）。Host は自分で入れ、Client は Host から届いた分を入れる。
    /// 名前は番号から各 PC が作る（<see cref="SpectatorNames"/>）ので、文字は送らない。
    /// </summary>
    internal static class SpectatorGroupFeed
    {
        /// <summary>Host → 全員: byte 数, 群れごとに ushort 番号, byte 色, byte 印(1=ボス), ushort 残り, float x, float y, float 広がり。</summary>
        public const string MessageName = "MS2026.Spectator.Groups";

        /// <summary>1 回に送る群れの数の上限（1 つの包みに収まる数）。</summary>
        public const int MaxGroups = 24;

        private const int BytesPerGroup = 2 + 1 + 1 + 2 + 4 * 3;

        public static readonly List<SpectatorGroupState> Latest = new List<SpectatorGroupState>();

        /// <summary>最後に届いた時刻（Time.unscaledTime）。古ければ出さない。</summary>
        public static float ReceivedAt = -100f;

        private static NetworkManager _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Latest.Clear();
            ReceivedAt = -100f;
            _registered = null;
        }

        public static void Publish(List<SpectatorGroupState> groups)
        {
            Latest.Clear();
            Latest.AddRange(groups);
            ReceivedAt = Time.unscaledTime;
        }

        /// <summary>Host: 全員に配る（通信していなければ何もしない）。</summary>
        public static void Send(List<SpectatorGroupState> groups)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer || manager.CustomMessagingManager == null || manager.ConnectedClientsIds.Count <= 1)
            {
                return;
            }

            var count = Mathf.Min(groups.Count, MaxGroups);
            using var writer = new FastBufferWriter(1 + count * BytesPerGroup, Allocator.Temp);
            writer.WriteValueSafe((byte)count);
            for (var i = 0; i < count; i++)
            {
                var g = groups[i];
                writer.WriteValueSafe((ushort)Mathf.Clamp(g.Number, 0, ushort.MaxValue));
                writer.WriteValueSafe((byte)Mathf.Clamp(g.ColorIndex, 0, 255));
                writer.WriteValueSafe((byte)(g.Boss ? 1 : 0));
                writer.WriteValueSafe((ushort)Mathf.Clamp(g.Alive, 0, ushort.MaxValue));
                writer.WriteValueSafe(g.X);
                writer.WriteValueSafe(g.Y);
                writer.WriteValueSafe(g.Radius);
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.UnreliableSequenced);
        }

        /// <summary>Client: 受け取りの登録を、通信の状態に合わせて付け外しする（毎フレーム呼んでよい）。</summary>
        public static void EnsureMessaging()
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

        public static void Unregister()
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

        private static readonly List<SpectatorGroupState> Incoming = new List<SpectatorGroupState>();

        private static void OnReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority)
            {
                return;
            }

            Incoming.Clear();
            reader.ReadValueSafe(out byte count);
            for (var i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ushort number);
                reader.ReadValueSafe(out byte color);
                reader.ReadValueSafe(out byte flags);
                reader.ReadValueSafe(out ushort alive);
                reader.ReadValueSafe(out float x);
                reader.ReadValueSafe(out float y);
                reader.ReadValueSafe(out float radius);
                Incoming.Add(new SpectatorGroupState
                {
                    Number = number,
                    ColorIndex = color,
                    Boss = (flags & 1) != 0,
                    Alive = alive,
                    X = x,
                    Y = y,
                    Radius = radius
                });
            }

            Publish(Incoming);
        }
    }
}
