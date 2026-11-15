using System;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の今の上手さ（実際の数値）。行動・目・手はここを読む。
    /// 「強さ × 分野ごとの慣れ」から <see cref="CpuSkillProfile.Evaluate"/> が作る（2026-11-15 段6）。4つの目安も同じ形で持つ。
    /// </summary>
    [Serializable]
    public struct CpuSkillValues
    {
        [Header("反応")]
        [Tooltip("敵の様子が見えてから、頭に届くまでの秒数（人の反応の遅れ）。")]
        [Range(0f, 1.5f)]
        public float reactionSeconds;

        [Tooltip("敵の数の見積もりの外れ具合（0 = ぴったり、0.5 = ±50% くらい外れる）。")]
        [Range(0f, 1f)]
        public float judgementNoise;

        [Header("狙い")]
        [Tooltip("回ってくる砲身が敵の多い向きに着く、この秒数前から握り始める（先読み）。")]
        [Range(0f, 1f)]
        public float leadSeconds;

        [Tooltip("握り始めるのに要る敵の数（向き1つ・6 度ぶん。近いコアの敵は多めに数える）。")]
        [Min(0.1f)]
        public float startThreshold;

        [Tooltip("撃っている間、撃ち続けるのに要る敵の数（握り始めより少なくて良い）。")]
        [Min(0f)]
        public float keepThreshold;

        [Tooltip("この数より多い所では、いちばん強く握る。")]
        [Min(1f)]
        public float denseCount;

        [Tooltip("撃つときのいちばん弱い握り（0〜1）。")]
        [Range(0.12f, 1f)]
        public float minGrip;

        [Header("熱の読み")]
        [Tooltip("熱がこの割合を超えたら、握りを弱め、敵が少ない所で冷まし始める（0〜1）。")]
        [Range(0f, 1f)]
        public float heatCoolStart01;

        [Tooltip("熱がこの割合を超えたら、敵が多くても離して冷ます（1 以上 = 冷まさずにオーバーヒートする）。")]
        [Range(0f, 1.2f)]
        public float heatPanic01;

        [Header("スキルの判断")]
        [Tooltip("スキルが使えるようになってから、良い時を待つ最長の秒数（0 = 使えたらすぐ撃つ）。待つほど妥協して撃つ。")]
        [Range(0f, 15f)]
        public float skillPatienceSeconds;

        [Tooltip("「これなら撃つ」と思うスキルの効きの目安（0〜1。待ち始めはこの値、待つほど下がる）。")]
        [Range(0f, 1f)]
        public float skillGoodValue;

        [Tooltip("待ち時間の終わりでも、これより効きが低ければ撃たずに待ち続ける（0 = 何も無くても撃つ）。")]
        [Range(0f, 1f)]
        public float skillMinValue;

        [Header("周りを見る")]
        [Tooltip("スマッシュボールを狙う気持ちの強さ（0 = 狙わない、1 = 見えたら必ず狙う）。")]
        [Range(0f, 1f)]
        public float smashBallAttention;

        [Header("歯みがき")]
        [Tooltip("歯みがきタイムの「にぎにぎ」の速さ（1 秒の回数）。")]
        [Range(0.5f, 6f)]
        public float brushPulsesPerSecond;

        [Tooltip("ほかの人の「にぎにぎ」に合わせる上手さ（0 = 合わせない、1 = すぐ合わせる）。シンクロのボーナスが出やすくなる。")]
        [Range(0f, 1f)]
        public float brushSync;

        [Header("手")]
        [Tooltip("握る強さを変えられる速さ（1 秒に変えられる量。4 なら 0.25 秒で 0 → 1）。")]
        [Range(0.5f, 30f)]
        public float gripChangePerSecond;

        [Tooltip("握る強さのぶれ（0〜1）。")]
        [Range(0f, 0.3f)]
        public float gripJitter;

        [Tooltip("握る ⇄ 離す を切り替えられる最短の間隔（秒）。")]
        [Range(0f, 0.5f)]
        public float minToggleSeconds;

        /// <summary>ふつうの試合の始め（少し慣れた人 = <see cref="CpuSkillProfile.Novice"/>）。</summary>
        public static CpuSkillValues Default => CpuSkillProfile.Novice;

        /// <summary>段5 までの仮の値（残しておく。テストの見本などに）。</summary>
        public static CpuSkillValues Legacy => new CpuSkillValues
        {
            reactionSeconds = 0.35f,
            judgementNoise = 0.25f,
            leadSeconds = 0.2f,
            startThreshold = 3f,
            keepThreshold = 1.2f,
            denseCount = 20f,
            minGrip = 0.45f,
            heatCoolStart01 = 0.65f,
            heatPanic01 = 0.9f,
            skillPatienceSeconds = 4f,
            skillGoodValue = 0.6f,
            skillMinValue = 0.1f,
            smashBallAttention = 0.8f,
            brushPulsesPerSecond = 3f,
            brushSync = 0.5f,
            gripChangePerSecond = 5f,
            gripJitter = 0.05f,
            minToggleSeconds = 0.12f
        };
    }
}
