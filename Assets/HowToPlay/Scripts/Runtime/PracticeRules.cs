using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習（新人研修）の決まり。熱のたまり方・砲台の回り方・泡とスキルの決まり、それぞれの課題ができたとみなす値。
    /// 熱は本番の砲台と同じ「握力² × 速さ」でたまる。研修2（オーバーヒート）だけは、握る力が弱い人でも数秒で届くよう最低の速さを決めてある。
    /// （2026-11-12 レイアウトB: 研修1「握る・握り続ける・離す」／研修2「オーバーヒート・冷ます」／研修3「泡を割る・スキル」）
    /// </summary>
    [Serializable]
    public sealed class PracticeRules
    {
        [Header("熱")]
        [Tooltip("握っている間、熱が「握力² × この値」毎秒たまる（1 でいっぱい＝オーバーヒート）。")]
        [Min(0.01f)]
        public float heatGainPerSecond = 0.45f;

        [Tooltip("ON = 研修1（握る・握り続けて倒す・離す）の間は、握り続けても熱がたまらない（オーバーヒートしない）。" +
                 "オーバーヒートは研修2で初めて体験する。練習のシーンの本物の砲台にも、その人が研修1の間だけかける（2026-11-19）。")]
        public bool noOverheatInLesson1 = true;

        [Tooltip("離している間・オーバーヒート中に、熱が毎秒この量だけ下がる。")]
        [Min(0.01f)]
        public float heatCoolingPerSecond = 0.45f;

        [Tooltip("オーバーヒートしたら、この秒数はビームが出ない。")]
        [Min(0f)]
        public float overheatSilenceSeconds = 1.2f;

        [Header("研修1 歯を守れ")]
        [Tooltip("「握る」: この握力（0〜1）以上で握ったらできた。")]
        [Range(0.05f, 1f)]
        public float firstGripThreshold = 0.25f;

        [Tooltip("「握り続けて敵を倒す」: 練習のシーン（本物の敵）では、この数の敵を倒したらできた。")]
        [Min(1)]
        public int lesson1Kills = 3;

        [Tooltip("「握り続ける」: 絵で描いたフィールド（本物の敵がいないとき）では、ビームを出している時間がこの秒数（合計）になったらできた。")]
        [Min(0.1f)]
        public float holdSeconds = 1.5f;

        [Header("研修2 オーバーヒートに注意")]
        [Tooltip("研修2の「握り続けてオーバーヒート」の間だけ、熱は最低でも毎秒この量たまる（握る力が弱い人でも数秒でオーバーヒートを体験できる）。")]
        [Min(0f)]
        public float overheatStepMinGainPerSecond = 0.32f;

        [Tooltip("「離して冷ます」: 手を離して熱がこの割合以下まで下がったらできた。")]
        [Range(0f, 1f)]
        public float cooledHeat01 = 0.3f;

        [Header("砲台（自分の画面の見た目と泡の当たり）")]
        [Tooltip("砲台が回る速さ（度/秒）。本番と同じく勝手に回る。")]
        [Min(1f)]
        public float turretDegreesPerSecond = 70f;

        [Tooltip("ビームを出している間の回る速さの倍率。")]
        [Range(0.1f, 1f)]
        public float firingTurnScale = 0.6f;

        [Header("研修3 泡でスキル")]
        [Tooltip("泡が出る位置（砲台の今の向きから、回る方向へ何度先か）。")]
        public float bubbleAheadDegrees = 75f;

        [Tooltip("ビームの向きと泡の向きの差がこの角度以内なら、ビームが泡に当たっている。")]
        [Range(1f, 45f)]
        public float bubbleHitDegrees = 16f;

        [Tooltip("泡にビームをこの秒数当て続けると割れる。")]
        [Min(0.05f)]
        public float bubbleBreakSeconds = 0.5f;

        [Tooltip("スキルが出ている秒数（この間に次へ進む）。")]
        [Min(0.1f)]
        public float skillSeconds = 2.2f;

        [Tooltip("スキルを使い切ってから、研修修了（準備完了）にするまでの秒数（スキルの効果を見届けるため）。")]
        [Min(0f)]
        public float afterSkillSeconds = 5f;
    }
}
