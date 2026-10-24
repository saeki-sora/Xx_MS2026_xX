using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>歯みがきタイムの段階。</summary>
    public enum BrushingStage
    {
        /// <summary>はじめの説明（数えない）。</summary>
        Intro = 0,

        /// <summary>みがいている。</summary>
        Brushing = 1,

        /// <summary>結果を見せている。</summary>
        Result = 2
    }

    /// <summary>
    /// 歯みがきタイムの係（<see cref="BrushingEvent"/>）。全員のPCで動く。
    /// ・各PC: 自分のセンサーの「にぎにぎ」を数えて Host に送る（オフラインでは4つのセンサーを P1〜P4 として数える）
    /// ・Host: 全員の回数とシンクロのボーナスから「きれい度」を出し、みがき終わりにコアを回復＋コーティング。様子は数の並びで全員へ
    /// 画面（HUD）は UI 側がこの係のプロパティを読んで出す。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BrushingRunner : WaveIntervalRunner
    {
        // 全員へ届ける様子（State）の並び。
        private const int SClean = 0;
        private const int SShare = 1; // 1〜4: P1〜P4 の頑張り（0〜1）
        private const int SStage = 5;
        private const int SHeal = 6;
        private const int SCoating = 7;
        private const int SSyncCount = 8;
        private const int SPlayers = 9; // 数える人（bit）
        private const int STeam = 10;
        private const int SEnd = 11; // みがき終わりの時刻（Elapsed）。早く 100% になったら早まる

        private const int Players = FortressWaves.PlayerCount;

        private readonly GripPulseCounter[] _counters =
            { new GripPulseCounter(), new GripPulseCounter(), new GripPulseCounter(), new GripPulseCounter() };

        private readonly float[] _lastPulseTime = new float[Players];
        private readonly int[] _seenCounts = new int[Players];
        private BrushingEvent _settings;
        private int _bonus;
        private float _lastSync = float.NegativeInfinity;
        private bool _rewarded;
        private int _playedSyncCount;
        private bool _playedResult;

        /// <summary>今の段階（Host が決めて届く。届くまではこのPCの時計で）。</summary>
        public BrushingStage Stage
        {
            get
            {
                if (_settings == null)
                {
                    return BrushingStage.Intro;
                }

                if (Elapsed >= BrushEnd || (BrushingStage)Mathf.RoundToInt(State[SStage]) == BrushingStage.Result)
                {
                    return BrushingStage.Result;
                }

                return Elapsed < _settings.introSeconds ? BrushingStage.Intro : BrushingStage.Brushing;
            }
        }

        /// <summary>きれい度（0〜1）。</summary>
        public float Clean01 => State[SClean];

        /// <summary>P(player+1) の頑張り（1人分の目標に対する割合 0〜1）。</summary>
        public float Share01(int player) => player >= 0 && player < Players ? State[SShare + player] : 0f;

        /// <summary>P(player+1) が数える人か（いる人。オフラインでは握った人）。</summary>
        public bool Participates(int player) => (Mathf.RoundToInt(State[SPlayers]) & (1 << player)) != 0;

        /// <summary>みがく残り時間（秒。このPCで数える）。</summary>
        public float BrushTimeLeft => _settings == null ? 0f : Mathf.Max(0f, BrushEnd - Mathf.Max(Elapsed, _settings.introSeconds));

        /// <summary>結果: 回復した量。</summary>
        public float HealAmount => State[SHeal];

        /// <summary>結果: 付いたコーティングの量。</summary>
        public float CoatingAmount => State[SCoating];

        /// <summary>シンクロした回数。</summary>
        public int SyncCount => Mathf.RoundToInt(State[SSyncCount]);

        /// <summary>このPCのプレイヤーが、この合間ににぎにぎした回数（画面のはずむ動き用）。</summary>
        public int LocalPulses => FortressWaves.LocalBreakInput;

        /// <summary>はじめの説明の秒数（画面用）。</summary>
        public float IntroSeconds => _settings != null ? _settings.introSeconds : 0f;

        private float BrushEnd => State[SEnd] > 0f ? State[SEnd] : (_settings != null ? _settings.introSeconds + _settings.brushSeconds : 0f);

        public override bool IsFinished =>
            _settings != null && Context.HasAuthority && _rewarded && Elapsed >= BrushEnd + _settings.resultSeconds;

        protected override void OnBegin()
        {
            _settings = Definition as BrushingEvent;
            _bonus = 0;
            _rewarded = false;
            _playedResult = false;
            _playedSyncCount = 0;
            _lastSync = float.NegativeInfinity;
            for (var p = 0; p < Players; p++)
            {
                _counters[p].Reset();
                _lastPulseTime[p] = float.NegativeInfinity;
                _seenCounts[p] = 0;
                if (_settings != null)
                {
                    _counters[p].pressThreshold = _settings.pressThreshold;
                    _counters[p].releaseThreshold = Mathf.Min(_settings.releaseThreshold, _settings.pressThreshold - 0.01f);
                }
            }

            State[SPlayers] = FortressNet.IsNetworked ? Context.ConnectedPlayerMask : 0;
            State[SEnd] = _settings != null ? _settings.introSeconds + _settings.brushSeconds : 0f;
            Play(_settings != null ? _settings.onStart : null, -1);
        }

        protected override void OnTick(float dt)
        {
            if (_settings == null)
            {
                return;
            }

            var brushing = Elapsed >= _settings.introSeconds && Elapsed < BrushEnd && !_rewarded;
            if (brushing)
            {
                CountLocal();
            }

            if (Context.HasAuthority)
            {
                TickAuthority(brushing);
            }

            PlayRemoteCues();
        }

        protected override void OnEnd()
        {
            FortressWaves.SetLocalBreakInput(0);
        }

        // 自分のセンサー（オフラインでは4つとも）の「にぎにぎ」を数える。
        private void CountLocal()
        {
            var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;
            if (provider == null)
            {
                return;
            }

            var now = Elapsed;
            if (FortressNet.IsNetworked)
            {
                // ネット対戦: このPCのセンサー（0番）が自分のプレイヤー。
                if (_counters[0].Feed(provider.GetGripValue(0), now))
                {
                    FortressWaves.SetLocalBreakInput(_counters[0].Count);
                    Play(_settings.onPulse, Context.LocalPlayer);
                }

                return;
            }

            // オフライン: センサー p = P(p+1)（キーボードなら Q/W/O/P）。
            for (var p = 0; p < Players; p++)
            {
                if (!_counters[p].Feed(provider.GetGripValue(p), now))
                {
                    continue;
                }

                if (p == 0)
                {
                    FortressWaves.SetLocalBreakInput(_counters[0].Count);
                }
                else
                {
                    FortressWaves.SetBreakInput(p, _counters[p].Count);
                }

                Play(_settings.onPulse, p);
            }
        }

        private void TickAuthority(bool brushing)
        {
            var now = Elapsed;
            if (_rewarded)
            {
                // 結果が出た後は、きれい度もごほうびも変えない（遅れて届いた回数で、結果の見出しと中身がずれないように）。
                State[SStage] = (float)Stage;
                return;
            }

            var mask = Mathf.RoundToInt(State[SPlayers]);
            var offline = !FortressNet.IsNetworked;
            for (var p = 0; p < Players; p++)
            {
                var count = FortressWaves.BreakInput(p);
                if (count > _seenCounts[p])
                {
                    _seenCounts[p] = count;
                    _lastPulseTime[p] = now;
                    if (offline)
                    {
                        mask |= 1 << p; // オフラインでは、握った人が数える人
                    }
                }
            }

            var team = 0;
            for (var p = 0; p < Players; p++)
            {
                if ((mask & (1 << p)) != 0)
                {
                    team += _seenCounts[p];
                }
            }

            if (brushing && BrushingMath.IsSync(_lastPulseTime, mask, now, _settings.syncWindowSeconds) &&
                now - _lastSync >= _settings.syncCooldownSeconds)
            {
                _lastSync = now;
                _bonus += _settings.syncBonusPulses;
                State[SSyncCount] += 1f;
            }

            // オフラインは、何人で握っても1人分で 100%（ひとりでテストできるように）。
            var participants = offline ? 1 : Mathf.Max(1, CountBits(mask));
            var clean = BrushingMath.Clean01(team + _bonus, _settings.pulsesPerPlayer * participants);
            State[SClean] = clean;
            State[SPlayers] = mask;
            State[STeam] = team + _bonus;
            for (var p = 0; p < Players; p++)
            {
                State[SShare + p] = Mathf.Clamp01((float)_seenCounts[p] / _settings.pulsesPerPlayer);
            }

            // 100% になったら、その場でみがき終わり（早く結果へ）。
            if (brushing && clean >= 1f)
            {
                State[SEnd] = now;
            }

            if (!_rewarded && now >= BrushEnd)
            {
                Reward(clean);
            }

            State[SStage] = (float)Stage;
        }

        private void Reward(float clean)
        {
            _rewarded = true;
            var core = FindFirstObjectByType<CoreCrystalController>();
            if (core == null)
            {
                return;
            }

            BrushingMath.Reward(clean, core.maxHealth, core.CurrentHealth, _settings.healFraction, _settings.coatingFraction, out var heal, out var coating);
            State[SHeal] = core.Heal(heal);
            if (coating > 0f)
            {
                core.AddCoating(coating);
            }

            State[SCoating] = coating;
        }

        // シンクロ・結果の演出（Client も Host から届いた様子で鳴らす）。
        private void PlayRemoteCues()
        {
            if (SyncCount > _playedSyncCount)
            {
                _playedSyncCount = SyncCount;
                Play(_settings.onSync, -1);
            }

            if (!_playedResult && Stage == BrushingStage.Result)
            {
                _playedResult = true;
                Play(_settings.onResult, -1);
            }
        }

        private static void Play(FortressEffect effect, int player)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            FortressEffectPlayer.PlayOnce(effect, core != null ? core.transform.position : Vector3.zero, Quaternion.identity, player);
        }

        private static int CountBits(int mask)
        {
            var count = 0;
            for (var p = 0; p < Players; p++)
            {
                if ((mask & (1 << p)) != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
