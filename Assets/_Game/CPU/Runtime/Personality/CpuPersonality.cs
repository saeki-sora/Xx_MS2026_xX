using System;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の性格（変わらない分）。展示の遊び方「協力してクリアを目指しつつ、個人スコア1位も狙う」の塩梅を、CPU ごとに少しずつ変える。
    /// </summary>
    [Serializable]
    public struct CpuPersonality
    {
        [Tooltip("仲間のための動き（手薄な方向を埋める・仲間が撃っている敵は任せる・倒れた仲間を助ける・ボールを譲る）をどれだけ大事にするか（0〜1）。")]
        [Range(0f, 1f)]
        public float teamSpirit;

        [Tooltip("自分の撃破数（個人スコア）をどれだけ大事にするか（0〜1）。高いほどボールを取りに行き、敵の多い所を譲らない。")]
        [Range(0f, 1f)]
        public float greed;

        public CpuPersonality(float teamSpirit, float greed)
        {
            this.teamSpirit = Mathf.Clamp01(teamSpirit);
            this.greed = Mathf.Clamp01(greed);
        }

        /// <summary>世話焼き。</summary>
        public static CpuPersonality Caretaker => new CpuPersonality(0.85f, 0.3f);

        /// <summary>欲張り。</summary>
        public static CpuPersonality Greedy => new CpuPersonality(0.3f, 0.85f);

        /// <summary>バランス。</summary>
        public static CpuPersonality Balanced => new CpuPersonality(0.55f, 0.55f);

        public string Label =>
            teamSpirit >= greed + 0.25f ? "世話焼き" : greed >= teamSpirit + 0.25f ? "欲張り" : "バランス";
    }

    /// <summary>CPU たちの性格の組み合わせ（協力と個人スコアの塩梅。設定で切り替えて比べられる）。</summary>
    public enum CpuPersonalityMix
    {
        [InspectorName("ばらばら（世話焼き・欲張り・バランスを混ぜる）")]
        Mixed = 0,

        [InspectorName("みんなバランス")]
        AllBalanced = 1,

        [InspectorName("みんな世話焼き（協力が強め）")]
        AllCaretakers = 2,

        [InspectorName("みんな欲張り（個人スコアが強め）")]
        AllGreedy = 3
    }

    public static class CpuPersonalities
    {
        private static readonly CpuPersonality[] MixedOrder =
        {
            CpuPersonality.Balanced, CpuPersonality.Caretaker, CpuPersonality.Greedy
        };

        /// <summary>
        /// CPU の性格。ばらばらのときは、CPU の何人目か（cpuOrdinal。席の番号順に 0, 1, 2…）でバランス・世話焼き・欲張りを回して配る
        /// （CPU が 3人なら必ず 1人ずつ）。乱数で少しずらす（同じ種なら毎回同じ）。jitter = ずらす大きさ。
        /// matchSeed は試合ごとに1つ（CPU 全員で同じ値）を渡すこと。CPU ごとに違う値を渡すと、配る順番がそろわず同じ性格が重なる。
        /// </summary>
        public static CpuPersonality For(CpuPersonalityMix mix, int cpuOrdinal, int matchSeed, float jitter = 0.1f)
        {
            CpuPersonality basePersonality;
            switch (mix)
            {
                case CpuPersonalityMix.AllBalanced:
                    basePersonality = CpuPersonality.Balanced;
                    break;
                case CpuPersonalityMix.AllCaretakers:
                    basePersonality = CpuPersonality.Caretaker;
                    break;
                case CpuPersonalityMix.AllGreedy:
                    basePersonality = CpuPersonality.Greedy;
                    break;
                default:
                    // 試合ごと（種ごと）に、誰がどの性格になるかもずらす。
                    var offset = ((matchSeed % MixedOrder.Length) + MixedOrder.Length) % MixedOrder.Length;
                    basePersonality = MixedOrder[(Mathf.Max(0, cpuOrdinal) + offset) % MixedOrder.Length];
                    break;
            }

            var random = new System.Random(unchecked(matchSeed * 7919 + cpuOrdinal * 104729));
            float J() => ((float)random.NextDouble() * 2f - 1f) * jitter;
            return new CpuPersonality(basePersonality.teamSpirit + J(), basePersonality.greed + J());
        }
    }
}
