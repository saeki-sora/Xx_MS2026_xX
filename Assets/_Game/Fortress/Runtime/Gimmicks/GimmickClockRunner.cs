using System;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// <see cref="GimmickClock"/> の係（起動時に自動で作られる、見えないオブジェクト。シーンに置く物は無い）。
    /// ・新しい試合（群衆の計算が新しくなった）で時計を 0 に戻す
    /// ・Host（オフラインでは自分）: 進める段階なら進め、値を名前つきメッセージで全員へ配る（変わったとき＋0.5秒ごと。途中から入った人にも届く）
    /// ・Client: 届いた「進んでいるか・速さ」で自分でも進め、届いた秒数に寄せる（大きくずれたら飛ぶ）
    /// 仕掛け（歯車・動く障害物）の形は時刻だけで決まるので、通信はこの 10 バイトほどだけ。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-50)] // 仕掛け（既定の順）より先に時計を進める
    internal sealed class GimmickClockRunner : MonoBehaviour
    {
        private const float HeartbeatInterval = 0.5f;
        private const float SnapSeconds = 0.5f;
        private const float FollowRate = 4f;

        private static bool _changed;

        private SwarmSystem _swarm;
        private NetworkManager _registered;
        private float _nextHeartbeat;
        private bool _sentRunning;
        private float _targetTime = -1f;

        internal static void MarkChanged() => _changed = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("[Fortress] Gimmick Clock");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<GimmickClockRunner>();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Update()
        {
            DetectMatch();
            EnsureMessaging();

            var dt = Time.deltaTime;
            if (FortressNet.HasSimulationAuthority)
            {
                _targetTime = -1f;
                GimmickClock.Running = ShouldRun();
                if (GimmickClock.Running && !GimmickClock.Paused)
                {
                    GimmickClock.Time += dt * GimmickClock.Speed;
                }

                SendIfNeeded();
                return;
            }

            // Client: Host から届いた値で進める。
            if (GimmickClock.Running && !GimmickClock.Paused)
            {
                GimmickClock.Time += dt * GimmickClock.Speed;
                if (_targetTime >= 0f)
                {
                    _targetTime += dt * GimmickClock.Speed;
                }
            }

            if (_targetTime >= 0f)
            {
                var diff = _targetTime - GimmickClock.Time;
                if (Mathf.Abs(diff) > SnapSeconds)
                {
                    GimmickClock.Time = _targetTime;
                    GimmickClock.JumpCount++;
                }
                else
                {
                    GimmickClock.Time += diff * Mathf.Clamp01(FollowRate * dt);
                }
            }
        }

        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (swarm != null && !ReferenceEquals(swarm, _swarm))
            {
                GimmickClock.ResetForMatch();
                _targetTime = -1f;
                _changed = true;
            }

            _swarm = swarm;
        }

        private static bool ShouldRun()
        {
            if (SwarmSystem.Current == null || FortressMatch.HasEnded)
            {
                return false;
            }

            return FortressWaves.IsActive ? GimmickClock.RunsInPhase(FortressWaves.Phase) : FortressWaves.IsLocallyReady;
        }

        // ───────── 通信 ─────────

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
            manager.CustomMessagingManager.RegisterNamedMessageHandler(GimmickClock.StateMessage, OnStateReceived);
            _registered = manager;
            _changed = true;
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                _registered.CustomMessagingManager?.UnregisterNamedMessageHandler(GimmickClock.StateMessage);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        // 送る中身: float 秒数, byte 印（bit0 進んでいる, bit1 止めている）, float 速さ。
        private void SendIfNeeded()
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1)
            {
                return;
            }

            var now = Time.unscaledTime;
            var changed = _changed || GimmickClock.Running != _sentRunning;
            if (!changed && now < _nextHeartbeat)
            {
                return;
            }

            _changed = false;
            _sentRunning = GimmickClock.Running;
            _nextHeartbeat = now + HeartbeatInterval;

            using var writer = new FastBufferWriter(sizeof(float) * 2 + 1, Allocator.Temp);
            writer.WriteValueSafe(GimmickClock.Time);
            var flags = (byte)((GimmickClock.Running ? 1 : 0) | (GimmickClock.Paused ? 2 : 0));
            writer.WriteValueSafe(flags);
            writer.WriteValueSafe(GimmickClock.Speed);
            manager.CustomMessagingManager.SendNamedMessageToAll(GimmickClock.StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnStateReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority)
            {
                return;
            }

            reader.ReadValueSafe(out float time);
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out float speed);
            GimmickClock.Running = (flags & 1) != 0;
            GimmickClock.Paused = (flags & 2) != 0;
            GimmickClock.Speed = speed;
            _targetTime = time;
        }
    }
}
