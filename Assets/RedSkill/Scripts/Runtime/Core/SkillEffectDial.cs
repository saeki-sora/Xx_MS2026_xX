using System;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 演出1つぶんの「付け外し」と「強さのメーター」。スキル調整スタジオでは1行（スイッチ＋メーター）で出る。
    /// 演出のコードは <see cref="Value"/>（外していれば 0、付けていれば強さ）を、その演出の量や明るさに掛ける。
    /// </summary>
    [Serializable]
    public sealed class SkillEffectDial
    {
        /// <summary>メーターの一番上（標準の2倍まで強くできる）。</summary>
        public const float MaxAmount = 2f;

        [Tooltip("ONで出す、OFFで出さない。")]
        [SkillLabel("出す")]
        public bool enabled = true;

        [Tooltip("強さ（1 = 標準。0 で出さないのと同じ、2 で標準の2倍）。")]
        [SkillLabel("強さ")]
        [Range(0f, MaxAmount)]
        public float amount = 1f;

        public SkillEffectDial()
        {
        }

        public SkillEffectDial(bool enabled, float amount)
        {
            this.enabled = enabled;
            this.amount = amount;
        }

        /// <summary>外していれば 0、付けていれば強さ（0〜2）。</summary>
        public float Value => enabled ? Mathf.Clamp(amount, 0f, MaxAmount) : 0f;

        /// <summary>出すか（付いていて、強さが 0 より大きい）。</summary>
        public bool IsOn => Value > 0.0001f;
    }
}
