using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の手（握力）。人のセンサーの代わりに <see cref="PlayerPilots"/> へ入り、砲台・歯みがき・倒れたときの連打がここを読む。
    /// 行動は「握りたい強さ」を言うだけ。実際の握力は人の手のように:
    /// ・少しずつしか変わらない（<see cref="GripChangePerSecond"/>）
    /// ・握っている間は少しぶれる（<see cref="Jitter"/>）
    /// ・握る ⇄ 離す は、続けざまには切り替えられない（<see cref="MinToggleSeconds"/>）
    /// </summary>
    public sealed class CpuHands : ITurretGripSource
    {
        // 「握っている」の境目（GripInputBridge の既定の握り始め onThreshold と同じ値）。
        public const float GrippingThreshold = 0.12f;

        // ぶれが向きを変える速さ（大きいほど細かく震える）。
        private const float JitterRate = 6f;

        private readonly System.Random _random;
        private float _target;
        private bool _pressed;
        private float _sinceToggle = float.PositiveInfinity;
        private float _noise;

        public CpuHands(int player, int seed = 0)
        {
            Player = player;
            _random = new System.Random(seed);
        }

        public int Player { get; }

        /// <summary>今の握力（0〜1）。</summary>
        public float Grip01 { get; private set; }

        /// <summary>今「握っている」か。</summary>
        public bool IsGripping => Grip01 >= GrippingThreshold;

        /// <summary>握りたい強さ（0〜1）。</summary>
        public float Target => _target;

        /// <summary>握力が変わる速さ（1 秒に変えられる量。0 以下ならすぐ変わる）。</summary>
        public float GripChangePerSecond { get; set; }

        /// <summary>握っている間のぶれの大きさ（0〜1）。</summary>
        public float Jitter { get; set; }

        /// <summary>握る ⇄ 離す を切り替えられる最短の間隔（秒）。</summary>
        public float MinToggleSeconds { get; set; }

        /// <summary>この強さで握りたい（「握っている」の境目より弱い値は、離したいのと同じ）。</summary>
        public void Hold(float grip01)
        {
            _target = Mathf.Clamp01(grip01);
        }

        /// <summary>離したい。</summary>
        public void Release()
        {
            _target = 0f;
        }

        /// <summary>すぐに離す（CPU が止まるとき）。</summary>
        public void ReleaseNow()
        {
            _target = 0f;
            _pressed = false;
            _noise = 0f;
            Grip01 = 0f;
        }

        /// <summary>上手さの「手」の値を入れる。</summary>
        public void Apply(in CpuSkillValues skill)
        {
            GripChangePerSecond = skill.gripChangePerSecond;
            Jitter = skill.gripJitter;
            MinToggleSeconds = skill.minToggleSeconds;
        }

        /// <summary>毎フレーム、握りたい強さへ近づける。</summary>
        public void Tick(float deltaTime)
        {
            var dt = Mathf.Max(0f, deltaTime);
            _sinceToggle += dt;

            var wantPressed = _target >= GrippingThreshold;
            if (wantPressed != _pressed && _sinceToggle >= MinToggleSeconds)
            {
                _pressed = wantPressed;
                _sinceToggle = 0f;
            }

            // ぶれ: ゆっくり向きを変える揺れ（握っている間だけ）。
            if (Jitter > 0f && _pressed)
            {
                var step = ((float)_random.NextDouble() * 2f - 1f) * Jitter * JitterRate * dt;
                _noise = Mathf.Clamp(_noise * Mathf.Exp(-JitterRate * 0.5f * dt) + step, -Jitter, Jitter);
            }
            else
            {
                _noise = 0f;
            }

            // 押している間は握りたい強さ（離したくても切り替えられない間は、握りの境目のすぐ上を保つ）。
            var goal = _pressed ? Mathf.Clamp01(Mathf.Max(_target, GrippingThreshold + 0.02f) + _noise) : 0f;
            if (_pressed && goal < GrippingThreshold)
            {
                goal = GrippingThreshold;
            }

            Grip01 = GripChangePerSecond > 0f ? Mathf.MoveTowards(Grip01, goal, GripChangePerSecond * dt) : goal;
        }

        void ITurretGripSource.GetGrip(int playerIndex, out bool isGripping, out float grip01)
        {
            grip01 = Grip01;
            isGripping = IsGripping;
        }
    }
}
