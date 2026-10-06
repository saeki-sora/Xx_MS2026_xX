using System.Collections;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// シーンのBGMを D-Drive（<c>Audio.PlayBgm</c>）で鳴らす。D-Drive のカタログ登録（IsReady）が終わってから鳴らすので、
    /// 起動直後に呼んでも Placeholder にならない。各PCが自分で鳴らす（通信しない）。
    /// 置き方: 空のGameObjectに付けて <see cref="bgm"/> を選ぶ。メニュー「Tools › 要塞 › 演出素材 › 2 演出欄へ割り当て」でも自動で置く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FortressBgmPlayer : MonoBehaviour
    {
        [Tooltip("鳴らすBGM（D-DriveのBGM）。空なら何も鳴らさない。")]
        public AssetId<BgmMarker> bgm;

        [Tooltip("ONなら、シーン開始時に自動で鳴らす。")]
        public bool playOnStart = true;

        [Tooltip("フェードインの秒数。負の値ならBGM側（BgmData）の設定を使う。")]
        public float fadeInSeconds = -1f;

        [Tooltip("このオブジェクトが無効になったときにBGMを止める。")]
        public bool stopOnDisable = true;

        [Tooltip("止めるときのフェードアウト秒数。負の値ならBGM側の設定を使う。")]
        public float fadeOutSeconds = -1f;

        private Coroutine _starting;
        private bool _playing;

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            if (_starting != null)
            {
                StopCoroutine(_starting);
                _starting = null;
            }

            if (stopOnDisable && _playing && Audio.IsBound)
            {
                Audio.StopBgm(fadeOutSeconds);
            }

            _playing = false;
        }

        /// <summary>BGMを鳴らす（D-Driveの準備ができるまで待つ）。</summary>
        public void Play()
        {
            if (!bgm.IsValid || !isActiveAndEnabled)
            {
                return;
            }

            if (_starting != null)
            {
                StopCoroutine(_starting);
            }

            _starting = StartCoroutine(PlayWhenReady());
        }

        private IEnumerator PlayWhenReady()
        {
            while (DDriveRuntimeBootstrap.Instance == null || !DDriveRuntimeBootstrap.Instance.IsReady)
            {
                yield return null;
            }

            Audio.PlayBgm(bgm, fadeInSeconds);
            _playing = true;
            _starting = null;
        }
    }
}
