using System;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 本体の上手さの「4つの目安」（<see cref="CpuSkillValues"/> を4組）。強さと分野ごとの慣れから、今の上手さを作る（<see cref="Evaluate"/>）。
    /// どの値がどの分野かは <see cref="Evaluate"/> の中に書いてある（反応・狙い・熱の読み・スキルの判断・周りを見る・歯みがき）。
    /// </summary>
    [Serializable]
    public sealed class CpuSkillProfile
    {
        [Tooltip("初めて遊ぶ人（やさしいの試合の始め）。")]
        public CpuSkillValues beginner = Beginner;

        [Tooltip("少し慣れた人（やさしいの上限 = ふつうの試合の始め）。")]
        public CpuSkillValues novice = Novice;

        [Tooltip("上手い人（ふつうの上限 = つよいの試合の始め）。")]
        public CpuSkillValues skilled = Skilled;

        [Tooltip("とても上手い人（つよいの上限）。")]
        public CpuSkillValues expert = Expert;

        /// <summary>
        /// 今の上手さ。値ごとに、その分野の上手さの目盛り（scaleOf(分野) = 強さ + 慣れ。0〜3）で4つの目安の間をつなぐ。
        /// </summary>
        public CpuSkillValues Evaluate(Func<string, float> scaleOf)
        {
            float S(string area) => scaleOf != null ? scaleOf(area) : 1f;
            float V(float b, float n, float s, float e, string area) => new CpuLevelCurve(b, n, s, e).Evaluate(S(area));

            var b0 = beginner;
            var n0 = novice;
            var s0 = skilled;
            var e0 = expert;
            const string reaction = CpuSkillAreas.Reaction;
            const string aim = CpuSkillAreas.Aim;
            const string heat = CpuSkillAreas.Heat;
            const string skill = CpuSkillAreas.Skill;
            const string awareness = CpuSkillAreas.Awareness;
            const string brushing = CpuSkillAreas.Brushing;

            return new CpuSkillValues
            {
                // 反応（見えてから動くまで・見積もり・手の速さとぶれ）
                reactionSeconds = V(b0.reactionSeconds, n0.reactionSeconds, s0.reactionSeconds, e0.reactionSeconds, reaction),
                judgementNoise = V(b0.judgementNoise, n0.judgementNoise, s0.judgementNoise, e0.judgementNoise, reaction),
                gripChangePerSecond = V(b0.gripChangePerSecond, n0.gripChangePerSecond, s0.gripChangePerSecond, e0.gripChangePerSecond, reaction),
                gripJitter = V(b0.gripJitter, n0.gripJitter, s0.gripJitter, e0.gripJitter, reaction),
                minToggleSeconds = V(b0.minToggleSeconds, n0.minToggleSeconds, s0.minToggleSeconds, e0.minToggleSeconds, reaction),

                // 狙い
                leadSeconds = V(b0.leadSeconds, n0.leadSeconds, s0.leadSeconds, e0.leadSeconds, aim),
                startThreshold = V(b0.startThreshold, n0.startThreshold, s0.startThreshold, e0.startThreshold, aim),
                keepThreshold = V(b0.keepThreshold, n0.keepThreshold, s0.keepThreshold, e0.keepThreshold, aim),
                denseCount = V(b0.denseCount, n0.denseCount, s0.denseCount, e0.denseCount, aim),
                minGrip = V(b0.minGrip, n0.minGrip, s0.minGrip, e0.minGrip, aim),

                // 熱の読み
                heatCoolStart01 = V(b0.heatCoolStart01, n0.heatCoolStart01, s0.heatCoolStart01, e0.heatCoolStart01, heat),
                heatPanic01 = V(b0.heatPanic01, n0.heatPanic01, s0.heatPanic01, e0.heatPanic01, heat),

                // スキルの判断
                skillPatienceSeconds = V(b0.skillPatienceSeconds, n0.skillPatienceSeconds, s0.skillPatienceSeconds, e0.skillPatienceSeconds, skill),
                skillGoodValue = V(b0.skillGoodValue, n0.skillGoodValue, s0.skillGoodValue, e0.skillGoodValue, skill),
                skillMinValue = V(b0.skillMinValue, n0.skillMinValue, s0.skillMinValue, e0.skillMinValue, skill),

                // 周りを見る
                smashBallAttention = V(b0.smashBallAttention, n0.smashBallAttention, s0.smashBallAttention, e0.smashBallAttention, awareness),

                // 歯みがき
                brushPulsesPerSecond = V(b0.brushPulsesPerSecond, n0.brushPulsesPerSecond, s0.brushPulsesPerSecond, e0.brushPulsesPerSecond, brushing),
                brushSync = V(b0.brushSync, n0.brushSync, s0.brushSync, e0.brushSync, brushing)
            };
        }

        // ───────── 4つの目安の初期値（段11 の調整で直す） ─────────

        /// <summary>初めて遊ぶ人: 反応が遅い・見積もりが粗い・こわごわ弱く握る・大きな群れしか撃たず、すぐやめる・熱を見ない・スキルはすぐ撃つ・周りを見ない。</summary>
        public static CpuSkillValues Beginner => new CpuSkillValues
        {
            reactionSeconds = 0.7f, judgementNoise = 0.5f, gripChangePerSecond = 3f, gripJitter = 0.1f, minToggleSeconds = 0.25f,
            leadSeconds = 0f, startThreshold = 2.8f, keepThreshold = 1.2f, denseCount = 16f, minGrip = 0.5f,
            heatCoolStart01 = 0.95f, heatPanic01 = 1.1f,
            skillPatienceSeconds = 0f, skillGoodValue = 0.2f, skillMinValue = 0f,
            smashBallAttention = 0.3f,
            brushPulsesPerSecond = 2f, brushSync = 0f
        };

        /// <summary>少し慣れた人。</summary>
        public static CpuSkillValues Novice => new CpuSkillValues
        {
            reactionSeconds = 0.45f, judgementNoise = 0.3f, gripChangePerSecond = 4.5f, gripJitter = 0.07f, minToggleSeconds = 0.16f,
            leadSeconds = 0.1f, startThreshold = 2.3f, keepThreshold = 0.9f, denseCount = 12f, minGrip = 0.62f,
            heatCoolStart01 = 0.9f, heatPanic01 = 1f,
            skillPatienceSeconds = 1.5f, skillGoodValue = 0.4f, skillMinValue = 0.05f,
            smashBallAttention = 0.6f,
            brushPulsesPerSecond = 2.6f, brushSync = 0.25f
        };

        /// <summary>上手い人。</summary>
        public static CpuSkillValues Skilled => new CpuSkillValues
        {
            reactionSeconds = 0.28f, judgementNoise = 0.18f, gripChangePerSecond = 6f, gripJitter = 0.04f, minToggleSeconds = 0.12f,
            leadSeconds = 0.25f, startThreshold = 2f, keepThreshold = 0.8f, denseCount = 10f, minGrip = 0.7f,
            heatCoolStart01 = 0.82f, heatPanic01 = 0.96f,
            skillPatienceSeconds = 4f, skillGoodValue = 0.6f, skillMinValue = 0.1f,
            smashBallAttention = 0.85f,
            brushPulsesPerSecond = 3.2f, brushSync = 0.6f
        };

        /// <summary>とても上手い人: 反応が速い・砲身が来る前から握る・強く握って少しの敵でも撃ち続ける・熱は限界近くまで使うがオーバーヒートしない・スキルは一番効く時に・周りをよく見る。</summary>
        public static CpuSkillValues Expert => new CpuSkillValues
        {
            reactionSeconds = 0.15f, judgementNoise = 0.08f, gripChangePerSecond = 9f, gripJitter = 0.02f, minToggleSeconds = 0.08f,
            leadSeconds = 0.35f, startThreshold = 1.5f, keepThreshold = 0.5f, denseCount = 6f, minGrip = 0.85f,
            heatCoolStart01 = 0.8f, heatPanic01 = 0.95f,
            skillPatienceSeconds = 6f, skillGoodValue = 0.7f, skillMinValue = 0.15f,
            smashBallAttention = 1f,
            brushPulsesPerSecond = 3.8f, brushSync = 0.9f
        };
    }
}
