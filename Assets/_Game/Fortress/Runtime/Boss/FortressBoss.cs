using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボス戦の今の様子（2026-11-15）。HUD・ウェーブの係・テストはここを読む。
    /// 決めるのは Host（オフラインでは自分）。部位の残り・コアに届いたか・最後の握りが終わったかを、ウェーブの係が流れ（<see cref="WaveFlow"/>）に渡す。
    /// 係（<see cref="BossWaveRunner"/>）は起動時にウェーブの係と一緒に自動で作られる。
    /// </summary>
    public static class FortressBoss
    {
        /// <summary>今のボス（いなければ null）。</summary>
        public static BossDefinition Definition { get; internal set; }

        /// <summary>今の配置（<see cref="HasLayout"/> のときだけ意味がある）。</summary>
        public static BossFormationLayout Layout { get; internal set; }

        public static bool HasLayout { get; internal set; }

        /// <summary>ボスが画面に出ているか（入ってくる途中から、倒れて消えるまで）。</summary>
        public static bool IsVisible { get; internal set; }

        /// <summary>部位の数。</summary>
        public static int PartCount { get; internal set; }

        /// <summary>残りの部位の数（ボスがいない・まだ分からないときは −1）。</summary>
        public static int PartsLeft { get; internal set; } = -1;

        /// <summary>部位 i が残っているか（bit i）。</summary>
        public static int AliveMask { get; internal set; }

        /// <summary>ボスがコアに届いたか。</summary>
        public static bool ReachedCore { get; internal set; }

        /// <summary>最後の「全員で握れ！」が終わった（ボスを倒し切った）か。</summary>
        public static bool FinaleDone { get; internal set; }

        /// <summary>近づき具合（止まる場所 0 〜 コアに届く 1）。</summary>
        public static float Approach01 { get; internal set; }

        /// <summary>ボス戦の全員同じ引きの画面（並び替えのときに決めたカメラの視点）。</summary>
        public static Cameras.CameraViewSettings View { get; internal set; }

        /// <summary>とどめの段階。</summary>
        public static BossFinaleStage FinaleStage { get; internal set; }

        /// <summary>「全員で握れ！」の4人のゲージ（0〜1。P1〜P4）。</summary>
        public static readonly float[] Gauges = new float[4];

        /// <summary>今の画面の光（正 = 白、負 = 暗。とどめの止めの間だけ）。</summary>
        public static float FinaleFlash { get; internal set; }

        /// <summary>今の画面のまわりの暗さ（0〜1）。</summary>
        public static float FinaleDim { get; internal set; }

        /// <summary>部位が壊れた（部位の番号, 壊した人。分からなければ −1）。全員のPCで。</summary>
        public static event Action<int, int> PartBroken;

        /// <summary>ボスがコアに届いた。</summary>
        public static event Action Reached;

        /// <summary>ボスを倒した（大爆発）。</summary>
        public static event Action Defeated;

        public static bool IsPartAlive(int index) => index >= 0 && index < 31 && (AliveMask & (1 << index)) != 0;

        internal static void RaisePartBroken(int index, int player) => PartBroken?.Invoke(index, player);

        internal static void RaiseReached() => Reached?.Invoke();

        internal static void RaiseDefeated() => Defeated?.Invoke();

        internal static void ResetState()
        {
            Definition = null;
            Layout = default;
            HasLayout = false;
            IsVisible = false;
            PartCount = 0;
            PartsLeft = -1;
            AliveMask = 0;
            ReachedCore = false;
            FinaleDone = false;
            Approach01 = 0f;
            View = default;
            FinaleStage = BossFinaleStage.None;
            FinaleFlash = 0f;
            FinaleDim = 0f;
            Array.Clear(Gauges, 0, Gauges.Length);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetState();
            PartBroken = null;
            Reached = null;
            Defeated = null;
        }
    }
}
