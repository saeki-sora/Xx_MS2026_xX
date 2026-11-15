using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の今の気持ち（性格 ＋ 試合の様子で毎回変わる）。行動はこれを読んで、協力と個人スコアの塩梅を決める。
    /// </summary>
    public readonly struct CpuMood
    {
        /// <summary>仲間のための動きをする気持ち（0〜1）。</summary>
        public readonly float Coop;

        /// <summary>自分の撃破数を稼ぐ気持ち（0〜1）。</summary>
        public readonly float Greed;

        public CpuMood(float coop, float greed)
        {
            Coop = Mathf.Clamp01(coop);
            Greed = Mathf.Clamp01(greed);
        }

        public static CpuMood Neutral => new CpuMood(0.5f, 0.5f);
    }

    /// <summary>
    /// 気持ちの決まり（テストできるように Unity のオブジェクトに触らない）。
    /// ・コアが危ない（体力 70% より下）ほど、仲間が倒れているほど、協力の気持ちが上がり、稼ぐ気持ちが下がる
    /// ・余裕があるときは性格どおり。撃破数が自分だけ最下位なら少し欲張り、1位なら少し譲る
    /// </summary>
    public static class CpuMoodMath
    {
        public const float DangerStartHealth = 0.7f;
        public const float DangerFullHealth = 0.2f;

        /// <param name="rank">撃破数の順位（0 = 1位）。</param>
        /// <param name="players">試合にいる人数。</param>
        public static CpuMood Evaluate(in CpuPersonality personality, float coreHealth01, int downedMates, int rank, int players)
        {
            var danger = Mathf.Clamp01((DangerStartHealth - coreHealth01) / (DangerStartHealth - DangerFullHealth));
            var downed = Mathf.Clamp01(downedMates * 0.5f);

            var coop = personality.teamSpirit + danger * 0.6f + downed * 0.3f;

            var rankPush = 0f;
            if (players > 1)
            {
                if (rank <= 0)
                {
                    rankPush = -0.2f; // 1位なら少し譲る
                }
                else if (rank >= players - 1)
                {
                    rankPush = 0.2f; // 最下位なら少し欲張る
                }
            }

            var greed = personality.greed * (1f - danger * 0.7f) * (1f - downed * 0.3f) + rankPush * (1f - danger);
            return new CpuMood(coop, greed);
        }

        /// <summary>撃破数の順位（0 = 1位。同じ数なら上の順位）。</summary>
        public static int Rank(int[] kills, int player, int players)
        {
            if (kills == null || player < 0 || player >= kills.Length)
            {
                return 0;
            }

            var rank = 0;
            for (var p = 0; p < Mathf.Min(players, kills.Length); p++)
            {
                if (p != player && kills[p] > kills[player])
                {
                    rank++;
                }
            }

            return rank;
        }

        /// <summary>
        /// 「撃つ」に使う敵の重み: ほかの人のビームがもう当たっている敵（contested）は、協力の気持ちの分だけ数えない
        /// （仲間に任せて、手薄な所を撃つ）。稼ぐ気持ちが強いほど、任せずに数える。results に入れる。
        /// </summary>
        public static void ShareWeights(float[] weights, float[] contested, in CpuMood mood, float[] results)
        {
            var leave = Mathf.Clamp01(mood.Coop - mood.Greed * 0.5f) * 0.8f;
            for (var s = 0; s < weights.Length; s++)
            {
                var c = contested != null && s < contested.Length ? Mathf.Min(contested[s], weights[s]) : 0f;
                results[s] = Mathf.Max(0f, weights[s] - c * leave);
            }
        }

        /// <summary>スマッシュボールを狙う気持ちの倍率: 稼ぐ気持ちで上がり、ほかの人がもう狙っていて協力の気持ちが強ければ譲る。</summary>
        public static float BallFactor(in CpuMood mood, int contenders)
        {
            var factor = Mathf.Lerp(0.7f, 1.3f, mood.Greed);
            if (contenders > 0 && mood.Coop > mood.Greed)
            {
                factor *= 0.4f;
            }

            return factor;
        }

        /// <summary>仲間を助ける気持ちの倍率（協力の気持ちで上がる）。</summary>
        public static float RescueFactor(in CpuMood mood) => Mathf.Lerp(0.6f, 1.3f, mood.Coop);
    }
}
