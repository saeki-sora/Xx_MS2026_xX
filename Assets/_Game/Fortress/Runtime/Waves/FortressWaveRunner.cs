using System;
using System.Collections.Generic;
using MS2026.Fortress.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// <see cref="FortressWaves"/> の係（起動時に自動で作られる、見えないオブジェクト）。
    /// ・新しい試合（群衆の計算が新しくなった）で、ステージの <see cref="EnemySpawnDirector.wavePlan"/> を探して始める
    /// ・Host（オフラインでは自分）: <see cref="WaveFlow"/> を進め、湧き（<see cref="EnemySpawnDirector"/>）・撤退（<see cref="WaveRetreat"/>）を動かし、
    ///   様子を名前つきメッセージで全員へ配る（変わったとき 1秒10回まで＋1秒ごとの定期便。途中から入った人にも届く）
    /// ・Client: 届いた様子を写し、残り秒数は自分で数える。準備ができたこと・合間の入力の数を Host へ送る
    /// ・段階が変わったら、全員のPCで演出を鳴らし、お知らせを出し、合間の係を作る／消す
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(105)]
    internal sealed class FortressWaveRunner : MonoBehaviour
    {
        private const float SendInterval = 0.1f;
        private const float HeartbeatInterval = 1f;
        private const float ReadyResendInterval = 0.5f;
        private const float InputSendInterval = 0.05f;
        private const float TimeWarningSeconds = 10f;

        private readonly HashSet<ulong> _readyClients = new HashSet<ulong>();
        private readonly float[] _stateBuffer = new float[WaveIntervalRunner.MaxStateValues];

        private SwarmSystem _swarm;
        private EnemySpawnDirector _director;
        private WavePlan _plan;
        private WaveFlow _flow;
        private bool _wasAuthority;
        private NetworkManager _registered;
        private bool _forceSend;
        private float _nextSend;
        private float _nextHeartbeat;
        private int _sentKills = -1;
        private float _nextReadySend;
        private float _nextInputSend;
        private int _sentInput = -1;
        private int _lastCountdownTick = -1;
        private int _lastWarningSecond = -1;
        private bool _hashWarned;

        private void OnEnable()
        {
            FortressWaves.DebugSkipHandler = DebugSkip;
            FortressWaves.DebugJumpHandler = DebugJump;
        }

        private void OnDisable()
        {
            Unregister();
            EndBreakRunner();
        }

        private void Update()
        {
            DetectMatch();
            EnsureMessaging();
            if (!FortressWaves.IsActive)
            {
                return;
            }

            var dt = Time.deltaTime;
            var authority = FortressNet.HasSimulationAuthority;
            if (_wasAuthority && !authority)
            {
                // 試合のシーンを読んでから Client としてつながった（テスト用の起動など）。進め方は Host に任せる。
                BecomeReplica();
            }

            _wasAuthority = authority;
            if (authority && _flow != null)
            {
                TickAuthority(dt);
            }
            else
            {
                TickReplica(dt);
            }

            if (FortressWaves.BreakRunner != null)
            {
                FortressWaves.BreakRunner.TickInternal(dt);
            }

            PlayLocalCues();
            if (authority)
            {
                FortressWaves.SetBreakInput(LocalPlayer(), FortressWaves.LocalBreakInput);
                SendStateIfNeeded();
            }
            else
            {
                SendReadyIfNeeded();
                SendInputIfNeeded();
            }
        }

        // ───────── 試合の始まり・終わり ─────────

        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            _swarm = swarm;
            StopMatch();
            if (swarm == null)
            {
                return;
            }

            _director = FindDirectorWithPlan();
            if (_director == null)
            {
                return;
            }

            _plan = _director.wavePlan;
            _director.StopWave();
            FortressWaves.ResetState();
            FortressWaves.Plan = _plan;
            FortressWaves.WaveCount = _plan.Count;
            FortressWaves.TimeLimit = _plan.timeLimitSeconds > 0f ? _plan.timeLimitSeconds : 0f;
            FortressWaves.TimeLeft = _plan.timeLimitSeconds > 0f ? _plan.timeLimitSeconds : -1f;
            FortressWaves.TargetKills = _plan.Get(0)?.targetKills ?? 0;
            _readyClients.Clear();
            _hashWarned = false;
            _sentKills = -1;
            _wasAuthority = FortressNet.HasSimulationAuthority;
            if (_wasAuthority)
            {
                _flow = new WaveFlow(WaveFlowConfig.From(_plan));
                var signal = _flow.Begin();
                CopyFlowState();
                Apply(signal);
            }
            else
            {
                _flow = null;
                SetPhase(WavePhase.WaitingForPlayers);
            }
        }

        private static EnemySpawnDirector FindDirectorWithPlan()
        {
            foreach (var director in FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None))
            {
                if (director != null && director.wavePlan != null && director.wavePlan.Count > 0)
                {
                    return director;
                }
            }

            return null;
        }

        private void StopMatch()
        {
            EndBreakRunner();
            LaserTurret.InputLocked = false;
            if (FortressWaves.IsActive)
            {
                WaveRetreat.Reset();
            }

            _flow = null;
            _director = null;
            _plan = null;
            FortressWaves.ResetState();
        }

        private void BecomeReplica()
        {
            _flow = null;
            if (_director != null)
            {
                _director.StopWave();
            }

            WaveRetreat.Reset();
        }

        // ───────── Host（オフラインでは自分） ─────────

        private void TickAuthority(float dt)
        {
            if (FortressMatch.HasEnded || FortressWaves.Suspended || _flow.HasEnded)
            {
                // コアが壊れて試合が終わった、計測中、または計画が終わった。湧きを止めて、そのままにする。
                if (_director != null && _director.IsPlaying && (FortressMatch.HasEnded || _flow.HasEnded))
                {
                    _director.StopWave();
                }

                return;
            }

            var input = new WaveFlowInput
            {
                totalKills = EnemyKillCounter.Total,
                playersReady = AllPlayersReady(),
                retreatingVisible = WaveRetreat.VisibleCount,
                breakFinished = FortressWaves.BreakRunner != null && FortressWaves.BreakRunner.IsFinished
            };
            var signal = _flow.Tick(dt, input);
            CopyFlowState();
            Apply(signal);
        }

        private void CopyFlowState()
        {
            FortressWaves.WaveIndex = _flow.WaveIndex;
            FortressWaves.KillsInWave = _flow.KillsInWave;
            FortressWaves.TargetKills = _flow.Target;
            FortressWaves.PhaseDuration = _flow.PhaseDuration;
            FortressWaves.PhaseElapsed = _flow.PhaseTime;
            FortressWaves.PhaseRemaining = _flow.PhaseDuration > 0f ? Mathf.Max(0f, _flow.PhaseDuration - _flow.PhaseTime) : 0f;
            FortressWaves.TimeLeft = _flow.TimeLeft;
            FortressWaves.ClockRunning = _flow.TimeLeft >= 0f &&
                                         (_flow.Phase == WavePhase.Combat ||
                                          (_flow.Config.clockRunsDuringBreaks && _flow.Phase != WavePhase.WaitingForPlayers && _flow.Phase != WavePhase.Finished));
            FortressWaves.EndKind = _flow.EndKind;
            FortressWaves.PlayerMask = ComputePlayerMask();
        }

        private void Apply(WaveFlowSignal signal)
        {
            if ((signal & WaveFlowSignal.StopSpawning) != 0 && _director != null)
            {
                _director.StopWave();
            }

            if ((signal & WaveFlowSignal.StartSpawning) != 0 && _director != null)
            {
                var wave = _plan.Get(_flow.WaveIndex);
                if (wave != null)
                {
                    _director.PlayEntries(wave.spawnEntries, wave.repeatUntilCleared, wave.repeatGapSeconds);
                }
            }

            if ((signal & WaveFlowSignal.BeginRetreat) != 0)
            {
                WaveRetreat.Begin(_plan.retreatSpeedMultiplier);
            }

            if ((signal & WaveFlowSignal.FinishRetreat) != 0)
            {
                WaveRetreat.Finish();
            }

            if ((signal & WaveFlowSignal.PhaseChanged) != 0)
            {
                // 発表 → 発表（テスト用の「このウェーブから」）でも、入り直したことにする。
                SetPhase(_flow.Phase, force: true);
                _forceSend = true;
            }
        }

        private bool AllPlayersReady()
        {
            if (!FortressWaves.IsLocallyReady)
            {
                return false;
            }

            var manager = _registered;
            if (manager == null || !manager.IsServer)
            {
                return true;
            }

            foreach (var id in manager.ConnectedClientsIds)
            {
                if (id != manager.LocalClientId && !_readyClients.Contains(id))
                {
                    return false;
                }
            }

            return true;
        }

        private static int ComputePlayerMask()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (!FortressNet.IsNetworked || bootstrap == null)
            {
                return 1 << LocalPlayer();
            }

            var mask = 1 << LocalPlayer();
            foreach (var pair in bootstrap.ClientIdToPlayerIndex)
            {
                if (pair.Value >= 0 && pair.Value < FortressWaves.PlayerCount)
                {
                    mask |= 1 << pair.Value;
                }
            }

            return mask;
        }

        private static int LocalPlayer()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            var index = bootstrap != null ? bootstrap.LocalPlayerIndex : -1;
            return index >= 0 && index < FortressWaves.PlayerCount ? index : 0;
        }

        private void DebugSkip()
        {
            if (_flow == null || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            var signal = _flow.Skip(EnemyKillCounter.Total);
            CopyFlowState();
            Apply(signal);
        }

        private void DebugJump(int index)
        {
            if (_flow == null || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            var signal = _flow.JumpTo(index);
            CopyFlowState();
            Apply(signal);
        }

        // ───────── Client ─────────

        private void TickReplica(float dt)
        {
            // 試合が終わった（コアが壊れた等）ら、Host と同じく時計を止める。
            if (FortressMatch.HasEnded)
            {
                return;
            }

            FortressWaves.PhaseElapsed += dt;
            FortressWaves.PhaseRemaining = Mathf.Max(0f, FortressWaves.PhaseRemaining - dt);
            if (FortressWaves.ClockRunning && FortressWaves.TimeLeft > 0f)
            {
                FortressWaves.TimeLeft = Mathf.Max(0f, FortressWaves.TimeLeft - dt);
            }
        }

        // ───────── 全員のPCで ─────────

        private void SetPhase(WavePhase next, bool force = false)
        {
            var previous = FortressWaves.Phase;
            if (previous == next && !force)
            {
                return;
            }

            FortressWaves.PreviousPhase = previous;
            FortressWaves.Phase = next;
            if (!FortressNet.HasSimulationAuthority)
            {
                FortressWaves.PhaseElapsed = 0f;
            }

            if (previous == WavePhase.Break)
            {
                EndBreakRunner();
            }

            if (next == WavePhase.Break)
            {
                // コーティングは「次の1ウェーブだけ」。合間に入ったら前のぶんは消す（歯みがきの結果で、また付く）。
                if (FortressNet.HasSimulationAuthority)
                {
                    var core = FindFirstObjectByType<CoreCrystalController>();
                    if (core != null)
                    {
                        core.SetCoating(0f);
                    }
                }

                BeginBreakRunner();
            }

            // 合間（歯みがきタイムなど）の間は撃てない・回らない（握るのはイベントのため）。
            LaserTurret.InputLocked = next == WavePhase.Break;
            WaveRetreat.SetRetreatLook(next == WavePhase.Retreat);
            _lastCountdownTick = -1;
            PlayPhaseEffect(next);
            FortressWaves.RaisePhaseChanged(previous, next);
        }

        private void PlayPhaseEffect(WavePhase phase)
        {
            if (_plan == null)
            {
                return;
            }

            FortressEffect effect = null;
            switch (phase)
            {
                case WavePhase.Announce:
                    effect = FortressWaves.IsFinalWave && !_plan.onFinalAnnounce.IsEmpty ? _plan.onFinalAnnounce : _plan.onAnnounce;
                    break;
                case WavePhase.Combat:
                    effect = _plan.onWaveStart;
                    break;
                case WavePhase.Retreat:
                    effect = _plan.onWaveClear;
                    break;
                case WavePhase.Finished:
                    effect = FortressWaves.EndKind == WaveEndKind.AllWavesCleared ? _plan.onWaveClear : null;
                    break;
            }

            PlayAtCore(effect);
        }

        private static void PlayAtCore(FortressEffect effect)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            FortressEffectPlayer.PlayOnce(effect, core != null ? core.transform.position : Vector3.zero, Quaternion.identity);
        }

        // 3・2・1 と、残り時間の警告（各PCで自分の数えた値から鳴らす）。
        private void PlayLocalCues()
        {
            if (_plan == null)
            {
                return;
            }

            if (FortressWaves.Phase == WavePhase.Announce && _plan.countdownSeconds > 0f && FortressWaves.PhaseRemaining <= _plan.countdownSeconds)
            {
                var tick = Mathf.CeilToInt(FortressWaves.PhaseRemaining);
                if (tick > 0 && tick != _lastCountdownTick)
                {
                    _lastCountdownTick = tick;
                    PlayAtCore(_plan.onCountdownTick);
                }
            }

            if (FortressWaves.ClockRunning && FortressWaves.TimeLeft > 0f && FortressWaves.TimeLeft <= TimeWarningSeconds)
            {
                var second = Mathf.CeilToInt(FortressWaves.TimeLeft);
                if (second != _lastWarningSecond)
                {
                    _lastWarningSecond = second;
                    PlayAtCore(_plan.onTimeWarning);
                }
            }
        }

        private void BeginBreakRunner()
        {
            EndBreakRunner();
            FortressWaves.ClearBreakInputs();
            _sentInput = -1;
            var definition = _plan != null ? _plan.Get(FortressWaves.WaveIndex)?.intervalEvent : null;
            if (definition == null)
            {
                return;
            }

            var runner = definition.CreateRunner(gameObject);
            if (runner == null)
            {
                return;
            }

            runner.Definition = definition;
            FortressWaves.BreakRunner = runner;
            runner.BeginInternal(new WaveBreakContext(FortressWaves.WaveIndex, FortressNet.HasSimulationAuthority, FortressWaves.PlayerMask, LocalPlayer()));
        }

        private void EndBreakRunner()
        {
            var runner = FortressWaves.BreakRunner;
            if (runner == null)
            {
                return;
            }

            FortressWaves.BreakRunner = null;
            try
            {
                runner.EndInternal();
            }
            finally
            {
                Destroy(runner);
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
            var messaging = manager.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(FortressWaves.StateMessage, OnStateReceived);
            messaging.RegisterNamedMessageHandler(FortressWaves.ReadyMessage, OnReadyReceived);
            messaging.RegisterNamedMessageHandler(FortressWaves.InputMessage, OnInputReceived);
            _registered = manager;
            _forceSend = true;
            _readyClients.Clear();
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                var messaging = _registered.CustomMessagingManager;
                messaging?.UnregisterNamedMessageHandler(FortressWaves.StateMessage);
                messaging?.UnregisterNamedMessageHandler(FortressWaves.ReadyMessage);
                messaging?.UnregisterNamedMessageHandler(FortressWaves.InputMessage);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        // 送る中身: byte 段階, byte ウェーブ番号, byte ウェーブの数, byte 終わり方, byte 印(bit0 時計が動いている), byte プレイヤー,
        // int 倒した数, int 目標, float 段階の残り, float 段階の長さ, float 試合の残り, float 制限時間, uint 計画の指紋,
        // byte 合間の様子の数 + float × その数。
        private void SendStateIfNeeded()
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1 || _plan == null)
            {
                return;
            }

            var now = Time.unscaledTime;
            var changed = _forceSend ||
                          (FortressWaves.KillsInWave != _sentKills && now >= _nextSend) ||
                          (FortressWaves.BreakRunner != null && now >= _nextSend);
            if (!changed && now < _nextHeartbeat)
            {
                return;
            }

            _forceSend = false;
            _sentKills = FortressWaves.KillsInWave;
            _nextSend = now + SendInterval;
            _nextHeartbeat = now + HeartbeatInterval;

            var runner = FortressWaves.BreakRunner;
            var stateCount = runner != null ? runner.State.Length : 0;
            var size = 6 + sizeof(int) * 2 + sizeof(float) * 4 + sizeof(uint) + 1 + sizeof(float) * stateCount;
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe((byte)FortressWaves.Phase);
            writer.WriteValueSafe((byte)Mathf.Clamp(FortressWaves.WaveIndex, 0, 255));
            writer.WriteValueSafe((byte)Mathf.Clamp(FortressWaves.WaveCount, 0, 255));
            writer.WriteValueSafe((byte)FortressWaves.EndKind);
            writer.WriteValueSafe((byte)(FortressWaves.ClockRunning ? 1 : 0));
            writer.WriteValueSafe((byte)FortressWaves.PlayerMask);
            writer.WriteValueSafe(FortressWaves.KillsInWave);
            writer.WriteValueSafe(FortressWaves.TargetKills);
            writer.WriteValueSafe(FortressWaves.PhaseRemaining);
            writer.WriteValueSafe(FortressWaves.PhaseDuration);
            writer.WriteValueSafe(FortressWaves.TimeLeft);
            writer.WriteValueSafe(FortressWaves.TimeLimit);
            writer.WriteValueSafe(_plan.ComputeHash());
            writer.WriteValueSafe((byte)stateCount);
            for (var i = 0; i < stateCount; i++)
            {
                writer.WriteValueSafe(runner.State[i]);
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(FortressWaves.StateMessage, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnStateReceived(ulong sender, FastBufferReader reader)
        {
            if (FortressNet.HasSimulationAuthority || _plan == null)
            {
                return;
            }

            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out byte waveIndex);
            reader.ReadValueSafe(out byte waveCount);
            reader.ReadValueSafe(out byte endKind);
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out byte playerMask);
            reader.ReadValueSafe(out int kills);
            reader.ReadValueSafe(out int target);
            reader.ReadValueSafe(out float phaseRemaining);
            reader.ReadValueSafe(out float phaseDuration);
            reader.ReadValueSafe(out float timeLeft);
            reader.ReadValueSafe(out float timeLimit);
            reader.ReadValueSafe(out uint hash);
            reader.ReadValueSafe(out byte stateCount);
            var count = Mathf.Min(stateCount, _stateBuffer.Length);
            for (var i = 0; i < stateCount; i++)
            {
                reader.ReadValueSafe(out float value);
                if (i < count)
                {
                    _stateBuffer[i] = value;
                }
            }

            if (hash != _plan.ComputeHash() && !_hashWarned)
            {
                _hashWarned = true;
                Debug.LogError("[FortressWaves] Host とウェーブの計画の中身が違います（違うビルド・違うデータ）。ウェーブの表示がずれます。全員同じビルドにしてください。");
            }

            var reenter = waveIndex != FortressWaves.WaveIndex && (WavePhase)phase == FortressWaves.Phase;
            FortressWaves.WaveIndex = waveIndex;
            FortressWaves.WaveCount = waveCount;
            FortressWaves.EndKind = (WaveEndKind)endKind;
            FortressWaves.ClockRunning = (flags & 1) != 0;
            FortressWaves.PlayerMask = playerMask;
            FortressWaves.KillsInWave = kills;
            FortressWaves.TargetKills = target;
            FortressWaves.PhaseRemaining = phaseRemaining;
            FortressWaves.PhaseDuration = phaseDuration;
            FortressWaves.TimeLeft = timeLeft;
            FortressWaves.TimeLimit = timeLimit;
            SetPhase((WavePhase)phase, reenter);
            if (FortressWaves.BreakRunner != null && count > 0)
            {
                FortressWaves.BreakRunner.ApplyState(_stateBuffer, count);
            }
        }

        private void SendReadyIfNeeded()
        {
            var manager = _registered;
            if (manager == null || manager.IsServer || !manager.IsConnectedClient ||
                FortressWaves.Phase != WavePhase.WaitingForPlayers || !FortressWaves.IsLocallyReady)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextReadySend)
            {
                return;
            }

            _nextReadySend = now + ReadyResendInterval;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            manager.CustomMessagingManager.SendNamedMessage(FortressWaves.ReadyMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        }

        private void OnReadyReceived(ulong sender, FastBufferReader reader)
        {
            if (_registered != null && _registered.IsServer)
            {
                _readyClients.Add(sender);
            }
        }

        private void SendInputIfNeeded()
        {
            var manager = _registered;
            var input = FortressWaves.LocalBreakInput;
            if (manager == null || manager.IsServer || !manager.IsConnectedClient || FortressWaves.BreakRunner == null || input == _sentInput)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextInputSend)
            {
                return;
            }

            _nextInputSend = now + InputSendInterval;
            _sentInput = input;
            using var writer = new FastBufferWriter(sizeof(int) + 1, Allocator.Temp);
            writer.WriteValueSafe((byte)Mathf.Clamp(FortressWaves.WaveIndex, 0, 255));
            writer.WriteValueSafe(input);
            manager.CustomMessagingManager.SendNamedMessage(FortressWaves.InputMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void OnInputReceived(ulong sender, FastBufferReader reader)
        {
            var manager = _registered;
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (manager == null || !manager.IsServer || bootstrap == null)
            {
                return;
            }

            reader.ReadValueSafe(out byte waveIndex);
            reader.ReadValueSafe(out int count);
            // 前の合間の遅れて届いた数は捨てる。プレイヤーは届いた相手から決める（中身は信じない）。
            if (FortressWaves.Phase != WavePhase.Break || waveIndex != FortressWaves.WaveIndex ||
                !bootstrap.ClientIdToPlayerIndex.TryGetValue(sender, out var player))
            {
                return;
            }

            FortressWaves.SetBreakInput(player, count);
        }
    }
}
