using System.Collections;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using DDrive.Runtime.Loop;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Tooltip("追加で切り替えられるBGM。上の「bgm」が1曲目、ここが2曲目以降。片方ずつ鳴らし、切り替えキーで次の曲へ（クロスフェード）。")]
        public AssetId<BgmMarker>[] extraBgms = new AssetId<BgmMarker>[0];

        [Tooltip("起動時に鳴らす曲の番号（0=bgm、1=Extra Bgms の1つ目…）。選んだ1曲だけが、その曲の中でループ再生される。")]
        public int startIndex;

        [Tooltip("試聴用に次の曲へ切り替えるキー。通常は None（切り替えず、選んだ曲を鳴らし続ける）。")]
        public Key nextKey = Key.None;

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
        private int _index;

        private int Count
        {
            get
            {
                var n = bgm.IsValid ? 1 : 0;
                if (extraBgms != null)
                {
                    for (var i = 0; i < extraBgms.Length; i++)
                    {
                        if (extraBgms[i].IsValid) n++;
                    }
                }

                return n;
            }
        }

        private AssetId<BgmMarker> Current
        {
            get
            {
                var n = 0;
                if (bgm.IsValid)
                {
                    if (_index == 0) return bgm;
                    n = 1;
                }

                if (extraBgms != null)
                {
                    for (var i = 0; i < extraBgms.Length; i++)
                    {
                        if (!extraBgms[i].IsValid) continue;
                        if (n == _index) return extraBgms[i];
                        n++;
                    }
                }

                return bgm;
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void Update()
        {
            if (nextKey == Key.None || Keyboard.current == null || !_playing)
            {
                return;
            }

            if (Keyboard.current[nextKey].wasPressedThisFrame)
            {
                Next();
            }
        }

        /// <summary>次の曲へ切り替える（最後の曲の次は1曲目）。鳴っている曲とはクロスフェードする。</summary>
        public void Next()
        {
            var count = Count;
            if (count < 2 || !_playing)
            {
                return;
            }

            _index = (_index + 1) % count;
            Audio.PlayBgm(Current, fadeInSeconds);
            Debug.Log("[FortressBgm] " + (_index + 1) + " / " + count + " 曲目に切り替えました。");
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
                Debug.LogWarning("[FortressBgm] 鳴らしません: bgm が空、またはオブジェクトが無効です。", this);
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

            _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, Count - 1));
            Debug.Log("[FortressBgm] 再生します: 曲 " + (_index + 1) + " / " + Count + "（ID " + Current.Value + "）, Audio.IsBound=" + Audio.IsBound +
                      ", AudioListener=" + (FindFirstObjectByType<AudioListener>() != null), this);
            Audio.PlayBgm(Current, fadeInSeconds);
            _playing = true;
            _starting = null;
        }
    }
}
