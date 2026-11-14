using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 歯みがきの「にぎにぎ」のリズム（決まりだけ）。自分の速さで握る・離すを繰り返し、
    /// ほかの人が握った瞬間を知らされたら（<see cref="Follow"/>）、次の握りをそれに合わせて早める（シンクロのボーナスのため）。
    /// </summary>
    public sealed class CpuBrushRhythm
    {
        private float _nextPress;
        private float _releaseAt = float.NegativeInfinity;
        private bool _started;

        /// <summary>今握っているか（握る時間の中か）。</summary>
        public bool Pressed { get; private set; }

        public void Reset()
        {
            _started = false;
            Pressed = false;
            _releaseAt = float.NegativeInfinity;
        }

        /// <summary>
        /// 時刻 now の握り。period = 1回の間隔（秒）、pressFraction = そのうち握っている割合、firstDelay = 始めの待ち。
        /// </summary>
        public bool Tick(float now, float period, float pressFraction, float firstDelay = 0f)
        {
            period = Mathf.Max(0.05f, period);
            if (!_started)
            {
                _started = true;
                _nextPress = now + Mathf.Max(0f, firstDelay);
            }

            if (Pressed && now >= _releaseAt)
            {
                Pressed = false;
            }

            if (!Pressed && now >= _nextPress)
            {
                Pressed = true;
                _releaseAt = now + period * Mathf.Clamp(pressFraction, 0.1f, 0.9f);
                _nextPress = now + period;
            }

            return Pressed;
        }

        /// <summary>
        /// ほかの人に合わせて、次の握りを pressAt に早める（今握っている最中・すぐ前に握ったばかりのときは合わせない）。
        /// </summary>
        public void Follow(float pressAt, float period)
        {
            if (!_started || Pressed)
            {
                return;
            }

            if (pressAt < _nextPress && pressAt >= _releaseAt + period * 0.2f)
            {
                _nextPress = pressAt;
            }
        }
    }

    /// <summary>
    /// 行動「歯みがき」: 歯みがきタイムの「みがく」の間、小刻みに握る（ほかの行動より必ず優先）。
    /// 上手いほど速く、ほかの人が握った直後に合わせて握るのでシンクロのボーナスが出やすい。
    /// </summary>
    public sealed class CpuBrushingBehavior : ICpuBehavior
    {
        public const string BehaviorId = "brushing";
        public const float BrushScore = 2f;
        public const string Pulses = "歯みがきの回";

        // 握る強さ（歯みがきの「押した」の境目 0.55 より上）と、1回のうち握っている割合。
        private const float PressGrip = 0.9f;
        private const float PressFraction = 0.5f;

        private readonly CpuBrushRhythm _rhythm = new CpuBrushRhythm();
        private readonly int[] _seenCounts = new int[FortressWaves.PlayerCount];
        private bool _wasPressed;

        public string Id => BehaviorId;

        public float Score(CpuContext context) => IsBrushing() ? BrushScore : 0f;

        public void Enter(CpuContext context)
        {
            _rhythm.Reset();
            _wasPressed = false;
            for (var p = 0; p < _seenCounts.Length; p++)
            {
                _seenCounts[p] = FortressWaves.BreakInput(p);
            }
        }

        public void Tick(CpuContext context, float deltaTime)
        {
            var skill = context.Skill;
            var period = 1f / Mathf.Max(0.5f, skill.brushPulsesPerSecond);
            var now = context.Time;

            // ほかの人が握った（数が増えた）ら、合わせる上手さの割合で、少し後に握りを合わせる。
            for (var p = 0; p < _seenCounts.Length; p++)
            {
                var count = FortressWaves.BreakInput(p);
                if (p != context.Player && count > _seenCounts[p] && context.NextFloat() < Mathf.Clamp01(skill.brushSync))
                {
                    var delay = Mathf.Lerp(0.3f, 0.05f, Mathf.Clamp01(skill.brushSync)) + skill.reactionSeconds * 0.3f;
                    _rhythm.Follow(now + delay, period);
                }

                _seenCounts[p] = count;
            }

            var pressed = _rhythm.Tick(now, period, PressFraction, context.NextFloat() * period);
            if (pressed && !_wasPressed)
            {
                context.Stats.Add(Pulses, 1f);
            }

            _wasPressed = pressed;
            if (pressed)
            {
                context.Hands.Hold(PressGrip);
            }
            else
            {
                context.Hands.Release();
            }
        }

        public void Exit(CpuContext context)
        {
            _rhythm.Reset();
        }

        private static bool IsBrushing()
        {
            return FortressWaves.BreakRunner is BrushingRunner runner && runner.Stage == BrushingStage.Brushing;
        }
    }
}
