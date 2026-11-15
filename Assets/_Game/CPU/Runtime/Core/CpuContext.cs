using MS2026.Fortress;

namespace MS2026.Cpu
{
    /// <summary>
    /// 行動（<see cref="ICpuBehavior"/>）・行き先（<see cref="ICpuMovePlanner"/>）に渡す、CPU 1人ぶんの持ち物。
    /// 学び・性格は、段7 以降でここに足していく。
    /// </summary>
    public sealed class CpuContext
    {
        public CpuContext(int player, CpuLevel level, int seed, CpuHands hands, CpuEyes eyes = null)
        {
            Player = player;
            Level = level;
            Seed = seed;
            Random = new System.Random(seed);
            Hands = hands;
            Eyes = eyes ?? new CpuEyes();
            Skill = CpuSkillValues.Default;
        }

        /// <summary>プレイヤーの番号（0〜3）。</summary>
        public int Player { get; }

        /// <summary>強さ。</summary>
        public CpuLevel Level { get; }

        /// <summary>この CPU の乱数の種（同じ種なら同じ動き）。</summary>
        public int Seed { get; }

        /// <summary>この CPU だけの乱数（UnityEngine.Random は使わない。自動プレイで同じ試合を再現するため）。</summary>
        public System.Random Random { get; }

        /// <summary>手（握力）。</summary>
        public CpuHands Hands { get; }

        /// <summary>目（行動は <see cref="CpuEyes.Perceived"/> の、反応の遅れのある様子を使う）。</summary>
        public CpuEyes Eyes { get; }

        /// <summary>記録（ウェーブごと）。行動は好きな名前の数を足してよい（結果の表に列が増える）。</summary>
        public CpuStats Stats { get; } = new CpuStats();

        /// <summary>今の上手さ（実際の数値。強さ × 分野ごとの慣れから作る）。</summary>
        public CpuSkillValues Skill { get; set; }

        /// <summary>分野ごとの慣れ（0〜1。試合ごとに 0 から。上げるのは学び）。</summary>
        public CpuFamiliarity Familiarity { get; internal set; } = new CpuFamiliarity();

        /// <summary>
        /// その分野の上手さの目盛り（0 = 初めて遊ぶ人 … 3 = とても上手い人。= 強さ + 慣れ）。
        /// 差し込む側の行動も、自分の分野の数値をこれと <see cref="CpuLevelCurve"/> で決める。
        /// </summary>
        public float ScaleOf(string area) => CpuLevels.Scale(Level, Familiarity.Get(area));

        /// <summary>自分の砲台（見つかっていなければ null）。</summary>
        public LaserTurret Turret { get; internal set; }

        /// <summary>この CPU が動き始めてからの秒数（ゲームの時間）。</summary>
        public float Time { get; internal set; }

        /// <summary>0〜1 の乱数。</summary>
        public float NextFloat() => (float)Random.NextDouble();
    }
}
