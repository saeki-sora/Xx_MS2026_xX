using System;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 1つの数値の「4つの目安」: 初めて遊ぶ人／少し慣れた人／上手い人／とても上手い人。
    /// 強さ（<see cref="CpuLevel"/>）は目安の間の区間を受け持つ（やさしい = 1→2つ目、ふつう = 2→3つ目、つよい = 3→4つ目）。
    /// 区間の中のどこになるかは、その分野の慣れ（0〜1）で決まる。段階どうしは重ならない。
    /// </summary>
    [Serializable]
    public struct CpuLevelCurve
    {
        [Tooltip("初めて遊ぶ人（やさしいの試合の始め）。")]
        public float beginner;

        [Tooltip("少し慣れた人（やさしいの上限 = ふつうの試合の始め）。")]
        public float novice;

        [Tooltip("上手い人（ふつうの上限 = つよいの試合の始め）。")]
        public float skilled;

        [Tooltip("とても上手い人（つよいの上限）。")]
        public float expert;

        public CpuLevelCurve(float beginner, float novice, float skilled, float expert)
        {
            this.beginner = beginner;
            this.novice = novice;
            this.skilled = skilled;
            this.expert = expert;
        }

        /// <summary>上手さの目盛り（0 = 初めて遊ぶ人 … 3 = とても上手い人）での値。目安の間はまっすぐつなぐ。</summary>
        public float Evaluate(float scale)
        {
            scale = Mathf.Clamp(scale, 0f, 3f);
            if (scale <= 1f)
            {
                return Mathf.Lerp(beginner, novice, scale);
            }

            return scale <= 2f ? Mathf.Lerp(novice, skilled, scale - 1f) : Mathf.Lerp(skilled, expert, scale - 2f);
        }

        /// <summary>強さと慣れでの値。</summary>
        public float Evaluate(CpuLevel level, float familiarity01) => Evaluate(CpuLevels.Scale(level, familiarity01));
    }
}
