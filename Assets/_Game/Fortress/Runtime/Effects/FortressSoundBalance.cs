using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// SE 全体・声全体・BGM 全体の音量（サウンドスタジオ、2026-11-22）。プロジェクトにミキサー（AudioMixer）が無いので、
    /// 鳴らす所（<see cref="FortressAudioMix"/>）で掛け算する。
    /// 読み込み: ビルドでは PlayerSettings の Preloaded Assets に入れておく（サウンドスタジオが入れる）。エディタでは
    /// エディタ側の読み込み係が入れる。どちらも無いときは全部 1 として扱う（今までと同じ音量）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/Fortress/音のバランス", fileName = "FortressSoundBalance")]
    public sealed class FortressSoundBalance : ScriptableObject
    {
        [Tooltip("効果音（声以外）全体の音量。")]
        [Range(0f, 1f)]
        public float seVolume = 1f;

        [Tooltip("声全体の音量。")]
        [Range(0f, 1f)]
        public float voiceVolume = 1f;

        [Tooltip("BGM 全体の音量。")]
        [Range(0f, 1f)]
        public float bgmVolume = 1f;

        /// <summary>今使っている音のバランス。無ければ null（全部 1）。</summary>
        public static FortressSoundBalance Current { get; set; }

        private void OnEnable()
        {
            if (Current == null)
            {
                Current = this;
            }
        }
    }
}
