using System;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 試合中に倒した敵の数（全員の合計と、P1〜P4 それぞれの分）。画面（撃破数の表示）が読む（2026-10-13）。
    /// ・数えるのは Host（オフラインでは自分）。群衆（<see cref="SwarmSystem"/>）と Actor の敵（<see cref="EnemyController"/>）で、
    ///   体力が0になって消えた敵（コアに着いて消えた敵は数えない）。「だれの撃破か」は最後にダメージを与えたレーザーの持ち主
    /// ・ネット対戦では Host が数を名前つきメッセージ（<see cref="MessageName"/>、約20バイト）で配り、Client は届いた数を使う
    ///   （変わったときに1秒10回まで＋2秒ごと。途中から入った人にも届く）。Client は自分では数えない
    /// ・新しい試合（ゲームのシーンを読み込み、群衆の計算が新しくなったとき）に0へ戻る
    /// 数える係は起動時に自動で作られる（シーンに置く物は無い）。
    /// </summary>
    public static class EnemyKillCounter
    {
        public const int PlayerCount = 4;

        /// <summary>Host → 全員: 撃破数（int 合計 + int×4 各プレイヤー）。</summary>
        public const string MessageName = "MS2026.Fortress.Kills";

        private static readonly int[] ByPlayerValues = new int[PlayerCount];

        /// <summary>全員で倒した数（だれの撃破か分からない物も含む）。</summary>
        public static int Total { get; private set; }

        /// <summary>数が変わるたびに増える（表示側が変化に気づくため）。</summary>
        public static int Version { get; private set; }

        /// <summary>このプレイヤー(0〜3)が倒した数。</summary>
        public static int ByPlayer(int player)
        {
            return player >= 0 && player < PlayerCount ? ByPlayerValues[player] : 0;
        }

        internal static void Apply(int total, int[] byPlayer)
        {
            var changed = total != Total;
            for (var p = 0; p < PlayerCount; p++)
            {
                changed |= byPlayer[p] != ByPlayerValues[p];
            }

            if (!changed)
            {
                return;
            }

            Total = total;
            Array.Copy(byPlayer, ByPlayerValues, PlayerCount);
            Version++;
        }

        internal static void Reset()
        {
            Apply(0, new int[PlayerCount]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Total = 0;
            Array.Clear(ByPlayerValues, 0, PlayerCount);
            Version = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRunner()
        {
            var go = new GameObject("[Fortress] EnemyKillCounter");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<EnemyKillCounterRunner>();
        }
    }

    /// <summary><see cref="EnemyKillCounter"/> の数える係・配る係（起動時に自動で作られる、見えないオブジェクト）。</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(100)]
    internal sealed class EnemyKillCounterRunner : MonoBehaviour
    {
        private const float SendInterval = 0.1f;
        private const float HeartbeatInterval = 2f;

        private readonly int[] _actorByPlayer = new int[EnemyKillCounter.PlayerCount];
        private readonly int[] _byPlayer = new int[EnemyKillCounter.PlayerCount];
        private SwarmSystem _swarm;

        private int _actorTotal;
        private NetworkManager _registered;
        private int _sentVersion = -1;
        private float _nextSend;
        private float _nextHeartbeat;

        private void OnEnable()
        {
            EnemyController.Unregistered += OnEnemyUnregistered;
        }

        private void OnDisable()
        {
            EnemyController.Unregistered -= OnEnemyUnregistered;
            Unregister();
        }

        private void Update()
        {
            DetectNewMatch();
            EnsureMessaging();
            if (!FortressNet.HasSimulationAuthority)
            {
                // Client: Host から届いた数を使う（自分では数えない）。
                return;
            }

            var swarm = SwarmSystem.Current;
            var total = (swarm != null ? swarm.Stats.totalKilled : 0) + _actorTotal;
            for (var p = 0; p < _byPlayer.Length; p++)
            {
                _byPlayer[p] = (swarm != null ? swarm.KilledByPlayer(p) : 0) + _actorByPlayer[p];
            }

            EnemyKillCounter.Apply(total, _byPlayer);
            SendIfNeeded();
        }

        // 群衆の計算が新しくなった（ゲームのシーンを読み込んだ）＝新しい試合。0から数え直す。
        private void DetectNewMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            _swarm = swarm;
            if (swarm == null)
            {
                // 試合が終わった（ゲームのシーンを外した）。次の試合が始まるまで最後の数のまま。
                return;
            }

            _actorTotal = 0;
            Array.Clear(_actorByPlayer, 0, _actorByPlayer.Length);
            EnemyKillCounter.Reset();
            _sentVersion = -1;
        }

        private void OnEnemyUnregistered(EnemyController enemy)
        {
            if (enemy == null || enemy.IsReplica || enemy.RemovalReason != EnemyRemovalReason.Died || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            _actorTotal++;
            var killer = enemy.LastAttackerIndex;
            if (killer >= 0 && killer < _actorByPlayer.Length)
            {
                _actorByPlayer[killer]++;
            }
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
            manager.CustomMessagingManager.RegisterNamedMessageHandler(EnemyKillCounter.MessageName, OnKillsReceived);
            _registered = manager;
            _sentVersion = -1;
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                _registered.CustomMessagingManager?.UnregisterNamedMessageHandler(EnemyKillCounter.MessageName);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        private void SendIfNeeded()
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1)
            {
                return;
            }

            var now = Time.unscaledTime;
            var changed = EnemyKillCounter.Version != _sentVersion;
            if (!(changed && now >= _nextSend) && now < _nextHeartbeat)
            {
                return;
            }

            _sentVersion = EnemyKillCounter.Version;
            _nextSend = now + SendInterval;
            _nextHeartbeat = now + HeartbeatInterval;
            using var writer = new FastBufferWriter(sizeof(int) * (1 + EnemyKillCounter.PlayerCount), Allocator.Temp);
            writer.WriteValueSafe(EnemyKillCounter.Total);
            for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
            {
                writer.WriteValueSafe(EnemyKillCounter.ByPlayer(p));
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(EnemyKillCounter.MessageName, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnKillsReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority)
            {
                return;
            }

            reader.ReadValueSafe(out int total);
            for (var p = 0; p < _byPlayer.Length; p++)
            {
                reader.ReadValueSafe(out int value);
                _byPlayer[p] = value;
            }

            EnemyKillCounter.Apply(total, _byPlayer);
        }
    }
}
