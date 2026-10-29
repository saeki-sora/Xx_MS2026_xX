using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の決まり（熱のたまり方と、3つの課題ができたとみなす値）。
    /// 熱は本番の砲台と同じ「握力² × 速さ」でたまるが、20 秒の練習で一通り試せるよう、本番（思いきり握って約 10 秒）より早くしてある。
    /// </summary>
    [Serializable]
    public sealed class PracticeRules
    {
        [Tooltip("握っている間、熱が「握力² × この値」毎秒たまる（1 でいっぱい＝オーバーヒート）。思いきり握ると 1 ÷ この値 秒でオーバーヒート。")]
        [Min(0.01f)]
        public float heatGainPerSecond = 0.7f;

        [Tooltip("離している間・オーバーヒート中に、熱が毎秒この量だけ下がる。")]
        [Min(0.01f)]
        public float heatCoolingPerSecond = 0.45f;

        [Tooltip("オーバーヒートしたら、この秒数はビームが出ない。")]
        [Min(0f)]
        public float overheatSilenceSeconds = 1.2f;

        [Tooltip("①「握ってみる」: この握力（0〜1）以上で握ったらできた。")]
        [Range(0.05f, 1f)]
        public float firstGripThreshold = 0.25f;

        [Tooltip("②「思いきり握る」: 熱がこの割合（0〜1）までたまったらできた。握る力が弱い人でも、握り続ければ届く。")]
        [Range(0.1f, 1f)]
        public float squeezeHeat01 = 0.5f;

        [Tooltip("③「離して冷ます」: ② の後、手を離して熱がこの割合以下まで下がったらできた。")]
        [Range(0f, 1f)]
        public float cooledHeat01 = 0.3f;
    }
}
