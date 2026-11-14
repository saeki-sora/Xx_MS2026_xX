using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の手（握力）。人のセンサーの代わりに <see cref="PlayerPilots"/> へ入り、砲台・歯みがき・倒れたときの連打がここを読む。
    /// 行動は「握りたい強さ」を言うだけ。実際の握力は人間の手のように、少しずつしか変わらない（速さは上手さで変わる。段1 から）。
    /// </summary>
    public sealed class CpuHands : ITurretGripSource
    {
        // 「握っている」の境目（GripInputBridge の既定の握り始め onThreshold と同じ値）。
        public const float GrippingThreshold = 0.12f;

        private float _target;

        public CpuHands(int player)
        {
            Player = player;
        }

        public int Player { get; }

        /// <summary>今の握力（0〜1）。</summary>
        public float Grip01 { get; private set; }

        /// <summary>今「握っている」か。</summary>
        public bool IsGripping => Grip01 >= GrippingThreshold;

        /// <summary>握りたい強さ（0〜1）。</summary>
        public float Target => _target;

        /// <summary>握力が変わる速さ（1 秒に変えられる量。大きいほど素早い。0 以下ならすぐ変わる）。</summary>
        public float GripChangePerSecond { get; set; }

        /// <summary>この強さで握りたい。</summary>
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
            Grip01 = 0f;
        }

        /// <summary>毎フレーム、握りたい強さへ近づける。</summary>
        public void Tick(float deltaTime)
        {
            Grip01 = GripChangePerSecond > 0f
                ? Mathf.MoveTowards(Grip01, _target, GripChangePerSecond * Mathf.Max(0f, deltaTime))
                : _target;
        }

        void ITurretGripSource.GetGrip(int playerIndex, out bool isGripping, out float grip01)
        {
            grip01 = Grip01;
            isGripping = IsGripping;
        }
    }
}
