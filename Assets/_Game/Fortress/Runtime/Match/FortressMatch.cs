using System;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>試合の状態。</summary>
    public enum MatchOutcome : byte
    {
        /// <summary>試合中（または試合の外）。</summary>
        Playing = 0,

        /// <summary>クリア（全員で決まった数の敵を倒した）。</summary>
        Clear = 1,

        /// <summary>ゲームオーバー（コアが壊れた）。</summary>
        GameOver = 2
    }

    /// <summary>試合が終わった理由。通信で byte として送るので、足すときは最後に足す。</summary>
    public enum MatchEndReason : byte
    {
        None = 0,

        /// <summary>全員の撃破数が決まった数に届いた（ウェーブの計画が無いステージ）。</summary>
        KillGoal = 1,

        /// <summary>最後のウェーブの目標を倒した。</summary>
        AllWavesCleared = 2,

        /// <summary>コアが壊れた。</summary>
        CoreDestroyed = 3,

        /// <summary>試合の制限時間が来た。</summary>
        TimeUp = 4
    }

    /// <summary>試合が終わったときの結果（リザルト画面が読む）。全員のPCで同じ値。</summary>
    public struct MatchResult
    {
        public MatchOutcome outcome;

        /// <summary>終わった理由。</summary>
        public MatchEndReason reason;

        /// <summary>どのウェーブまで行けたか（1 から。ウェーブの計画が無ければ 0）。</summary>
        public int reachedWave;

        /// <summary>ウェーブの数（計画が無ければ 0）。</summary>
        public int waveCount;

        /// <summary>試合が始まってから終わるまでの秒数。</summary>
        public float durationSeconds;

        /// <summary>終わったときのコアの残りHP（割合 0〜1）。</summary>
        public float coreHealth01;

        /// <summary>全員で倒した数。</summary>
        public int totalKills;

        /// <summary>P1〜P4 が倒した数。</summary>
        public int[] kills;

        /// <summary>P1〜P4 の最大コンボ。</summary>
        public int[] maxCombos;

        /// <summary>いちばん多く倒した人（0〜3。だれも倒していなければ -1）。同じ数なら番号が小さい人。</summary>
        public int Mvp
        {
            get
            {
                var best = -1;
                for (var p = 0; kills != null && p < kills.Length; p++)
                {
                    if (kills[p] > 0 && (best < 0 || kills[p] > kills[best]))
                    {
                        best = p;
                    }
                }

                return best;
            }
        }
    }

    /// <summary>
    /// 試合の始まりと終わり（2026-10-13）。
    /// ・終わりを決めるのは Host（オフラインでは自分）: 全員の撃破数が <see cref="ClearKills"/>（既定 1000）に届いたらクリア、コアのHPが0ならゲームオーバー
    /// ・ステージにウェーブの計画（<see cref="WavePlan"/>）があれば、撃破数ではなく「最後のウェーブの目標を倒した」でクリア、
    ///   制限時間が来たら計画の <see cref="WavePlan.timeUpRule"/> どおり（2026-10-24）
    /// ・終わった瞬間に敵の出現を止め、残りの敵を消す（消した敵は撃破数に入れない）。結果を名前つきメッセージ <see cref="MessageName"/> で全員に配る
    /// ・コアのHPも Host が配る（今まで Host でしか減らなかった。Client のコアの見た目・HUD もこれで正しくなる）
    /// ・新しい試合（ゲームのシーンを読み込み、群衆の計算が新しくなったとき）に「試合中」に戻る
    /// 決まりの値は、ゲームのシーンに <see cref="FortressMatchRules"/> を置けば変えられる。係は起動時に自動で作られる（シーンに置く物は無い）。
    /// </summary>
    public static class FortressMatch
    {
        public const int DefaultClearKills = 1000;

        /// <summary>Host → 全員: byte 状態, float コアのHP, float コアの最大HP。終わっていれば続けて float 秒数, int 合計, int×4 撃破数, int×4 最大コンボ, byte 理由, byte 行けたウェーブ, byte ウェーブの数。</summary>
        public const string MessageName = "MS2026.Fortress.Match";

        public static MatchOutcome Outcome { get; private set; }

        public static bool HasEnded => Outcome != MatchOutcome.Playing;

        /// <summary>終わったときの結果（<see cref="HasEnded"/> のときだけ意味がある）。</summary>
        public static MatchResult Result { get; private set; }

        /// <summary>クリアに必要な撃破数（ゲームのシーンの <see cref="FortressMatchRules"/>、無ければ 1000）。</summary>
        public static int ClearKills { get; internal set; } = DefaultClearKills;

        /// <summary>試合中か（ゲームのシーンがあり、まだ終わっていない）。</summary>
        public static bool InMatch { get; internal set; }

        /// <summary>
        /// true なら試合を終わらせない（クリア・ゲームオーバーの判定をしない）。負荷計測（Tools/NetTest/bench_*.bat）用:
        /// 3万体を投入するとすぐコアが壊れてゲームオーバー → 敵が消えて計測にならないため（2026-10-15）。
        /// </summary>
        public static bool EndChecksSuspended { get; internal set; }

        /// <summary>試合が終わったとき（全員のPCで1回）。</summary>
        public static event Action<MatchResult> Ended;

        internal static void Begin(int clearKills)
        {
            Outcome = MatchOutcome.Playing;
            Result = default;
            ClearKills = Mathf.Max(1, clearKills);
            InMatch = true;
        }

        internal static void End(MatchResult result)
        {
            if (HasEnded)
            {
                return;
            }

            Outcome = result.outcome;
            Result = result;
            Ended?.Invoke(result);
        }

        internal static void Leave()
        {
            InMatch = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Outcome = MatchOutcome.Playing;
            Result = default;
            ClearKills = DefaultClearKills;
            InMatch = false;
            EndChecksSuspended = false;
            Ended = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRunner()
        {
            var go = new GameObject("[Fortress] Match");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FortressMatchRunner>();
        }
    }

    /// <summary><see cref="FortressMatch"/> の係（起動時に自動で作られる、見えないオブジェクト）。</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(110)]
    internal sealed class FortressMatchRunner : MonoBehaviour
    {
        private const float SendInterval = 0.1f;
        private const float HeartbeatInterval = 2f;
        private const int Players = EnemyKillCounter.PlayerCount;

        private readonly ComboTracker[] _combos = { new ComboTracker(), new ComboTracker(), new ComboTracker(), new ComboTracker() };
        private SwarmSystem _swarm;
        private FortressMatchRules _rules;
        private CoreCrystalController _core;
        private float _startTime;
        private NetworkManager _registered;
        private float _sentHealth = -1f;
        private MatchOutcome _sentOutcome;
        private float _nextSend;
        private float _nextHeartbeat;

        private void OnDisable()
        {
            Unregister();
        }

        private void Update()
        {
            DetectMatch();
            EnsureMessaging();
            if (!FortressMatch.InMatch || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var p = 0; p < Players; p++)
            {
                _combos[p].Update(EnemyKillCounter.ByPlayer(p), now);
            }

            if (!FortressMatch.HasEnded && !FortressMatch.EndChecksSuspended)
            {
                CheckEnd();
            }

            SendIfNeeded();
        }

        // ゲームのシーンの群衆の計算が新しくなった = 新しい試合。消えた = 試合の外。
        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            _swarm = swarm;
            if (swarm == null)
            {
                FortressMatch.Leave();
                return;
            }

            _rules = FindFirstObjectByType<FortressMatchRules>();
            _core = FindFirstObjectByType<CoreCrystalController>();
            ApplyCoreRules();
            _startTime = Time.unscaledTime;
            foreach (var combo in _combos)
            {
                combo.Reset();
            }

            _sentHealth = -1f;
            _sentOutcome = MatchOutcome.Playing;
            FortressMatch.Begin(_rules != null ? _rules.clearKills : FortressMatch.DefaultClearKills);
        }

        // 試合の決まりのコアの体力・ダメージ倍率を、試合の始めに全員のPCで入れる（Client も同じ最大HPにしないと、HUD の割合がずれる）。
        private void ApplyCoreRules()
        {
            if (_rules == null || _core == null)
            {
                return;
            }

            _core.damageMultiplier = _rules.coreDamageMultiplier;
            if (_rules.coreMaxHealth > 0f)
            {
                _core.SetMaxHealth(_rules.coreMaxHealth, true);
            }
        }

        private void CheckEnd()
        {
            if (_core == null)
            {
                _core = FindFirstObjectByType<CoreCrystalController>();
            }

            if (!TryDecideEnd(out var outcome, out var reason))
            {
                return;
            }

            var result = new MatchResult
            {
                outcome = outcome,
                reason = reason,
                reachedWave = FortressWaves.IsActive ? FortressWaves.ReachedWave : 0,
                waveCount = FortressWaves.IsActive ? FortressWaves.WaveCount : 0,
                durationSeconds = Time.unscaledTime - _startTime,
                coreHealth01 = _core != null ? Mathf.Clamp01(_core.CurrentHealth / Mathf.Max(1f, _core.maxHealth)) : 1f,
                totalKills = EnemyKillCounter.Total,
                kills = new int[Players],
                maxCombos = new int[Players]
            };
            for (var p = 0; p < Players; p++)
            {
                result.kills[p] = EnemyKillCounter.ByPlayer(p);
                result.maxCombos[p] = _combos[p].Max;
            }

            if (_rules == null || _rules.removeEnemiesOnEnd)
            {
                RemoveEnemies();
            }

            Finish(result);
        }

        // ウェーブの計画があれば、その終わり（全部クリア・時間切れ）で決める。無ければ撃破数。コアが壊れたらどちらでもゲームオーバー。
        private bool TryDecideEnd(out MatchOutcome outcome, out MatchEndReason reason)
        {
            if (FortressWaves.IsActive)
            {
                switch (FortressWaves.EndKind)
                {
                    case WaveEndKind.AllWavesCleared:
                        outcome = MatchOutcome.Clear;
                        reason = MatchEndReason.AllWavesCleared;
                        return true;
                    case WaveEndKind.TimeUp:
                        var rule = FortressWaves.Plan != null ? FortressWaves.Plan.timeUpRule : WaveTimeUpRule.GameOver;
                        outcome = rule == WaveTimeUpRule.ClearWithReachedWave ? MatchOutcome.Clear : MatchOutcome.GameOver;
                        reason = MatchEndReason.TimeUp;
                        return true;
                }
            }
            else if (EnemyKillCounter.Total >= FortressMatch.ClearKills)
            {
                outcome = MatchOutcome.Clear;
                reason = MatchEndReason.KillGoal;
                return true;
            }

            if (_core != null && _core.CurrentHealth <= 0f)
            {
                outcome = MatchOutcome.GameOver;
                reason = MatchEndReason.CoreDestroyed;
                return true;
            }

            outcome = MatchOutcome.Playing;
            reason = MatchEndReason.None;
            return false;
        }

        // 出現を止め、残りの敵を消す（体力を減らさずに消すので、撃破数には入らない）。
        private void RemoveEnemies()
        {
            foreach (var director in FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None))
            {
                director.StopWave();
            }

            _swarm?.ClearAll();
            for (var i = EnemyController.Active.Count - 1; i >= 0; i--)
            {
                var enemy = EnemyController.Active[i];
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
        }

        private void Finish(MatchResult result)
        {
            FortressMatch.End(result);
            PlayEndEffect(result.outcome);
            var wave = result.waveCount > 0 ? $"、ウェーブ {result.reachedWave}/{result.waveCount}" : "";
            Debug.Log($"[FortressMatch] 試合終了: {result.outcome}・{result.reason}（{result.durationSeconds:0.0}秒、撃破 {result.totalKills}、コア {result.coreHealth01:P0}{wave}）");
        }

        private void PlayEndEffect(MatchOutcome outcome)
        {
            if (_rules == null)
            {
                return;
            }

            var effect = outcome == MatchOutcome.Clear ? _rules.onClear : _rules.onGameOver;
            if (effect != null && !effect.IsEmpty)
            {
                var position = _core != null ? _core.transform.position : Vector3.zero;
                FortressEffectPlayer.PlayOnce(effect, position, Quaternion.identity);
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
            manager.CustomMessagingManager.RegisterNamedMessageHandler(FortressMatch.MessageName, OnMatchReceived);
            _registered = manager;
            _sentHealth = -1f;
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                _registered.CustomMessagingManager?.UnregisterNamedMessageHandler(FortressMatch.MessageName);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        // コアのHPか試合の状態が変わったら（1秒10回まで）、変わらなくても2秒ごとに配る（途中から入った人にも届く）。
        private void SendIfNeeded()
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1)
            {
                return;
            }

            var now = Time.unscaledTime;
            var health = _core != null ? _core.CurrentHealth : -1f;
            var changed = Mathf.Abs(health - _sentHealth) > 0.001f || FortressMatch.Outcome != _sentOutcome;
            if (!(changed && now >= _nextSend) && now < _nextHeartbeat)
            {
                return;
            }

            _sentHealth = health;
            _sentOutcome = FortressMatch.Outcome;
            _nextSend = now + SendInterval;
            _nextHeartbeat = now + HeartbeatInterval;

            var ended = FortressMatch.HasEnded;
            var size = 1 + sizeof(float) * 2 + (ended ? sizeof(float) + sizeof(int) * (1 + Players * 2) + 3 : 0);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe((byte)FortressMatch.Outcome);
            writer.WriteValueSafe(health);
            writer.WriteValueSafe(_core != null ? _core.maxHealth : 0f);
            if (ended)
            {
                var result = FortressMatch.Result;
                writer.WriteValueSafe(result.durationSeconds);
                writer.WriteValueSafe(result.totalKills);
                for (var p = 0; p < Players; p++)
                {
                    writer.WriteValueSafe(result.kills[p]);
                }

                for (var p = 0; p < Players; p++)
                {
                    writer.WriteValueSafe(result.maxCombos[p]);
                }

                writer.WriteValueSafe((byte)result.reason);
                writer.WriteValueSafe((byte)Mathf.Clamp(result.reachedWave, 0, 255));
                writer.WriteValueSafe((byte)Mathf.Clamp(result.waveCount, 0, 255));
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(FortressMatch.MessageName, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnMatchReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority)
            {
                return;
            }

            reader.ReadValueSafe(out byte outcomeByte);
            reader.ReadValueSafe(out float health);
            reader.ReadValueSafe(out float maxHealth);
            if (_core == null)
            {
                _core = FindFirstObjectByType<CoreCrystalController>();
            }

            // 最大HPも Host の値に合わせる（試合の決まりの値が PC ごとに違っても、HUD の割合がずれないように）。
            if (_core != null && maxHealth >= 1f && Mathf.Abs(maxHealth - _core.maxHealth) > 0.001f)
            {
                _core.SetMaxHealth(maxHealth, false);
            }

            if (_core != null && health >= 0f)
            {
                _core.ApplyReplicatedHealth(health);
            }

            var outcome = (MatchOutcome)outcomeByte;
            if (outcome == MatchOutcome.Playing || FortressMatch.HasEnded || !FortressMatch.InMatch)
            {
                return;
            }

            var result = new MatchResult
            {
                outcome = outcome,
                coreHealth01 = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 1f,
                kills = new int[Players],
                maxCombos = new int[Players]
            };
            reader.ReadValueSafe(out result.durationSeconds);
            reader.ReadValueSafe(out result.totalKills);
            for (var p = 0; p < Players; p++)
            {
                reader.ReadValueSafe(out int kills);
                result.kills[p] = kills;
            }

            for (var p = 0; p < Players; p++)
            {
                reader.ReadValueSafe(out int combo);
                result.maxCombos[p] = combo;
            }

            reader.ReadValueSafe(out byte reason);
            reader.ReadValueSafe(out byte reachedWave);
            reader.ReadValueSafe(out byte waveCount);
            result.reason = (MatchEndReason)reason;
            result.reachedWave = reachedWave;
            result.waveCount = waveCount;

            Finish(result);
        }
    }
}
