using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアが壊れた瞬間のスローモーション（見せ方だけ）。設定は <see cref="Settings"/>（ステージの見た目の設定
    /// StageLookProfile.coreBreakSlowMotion を、ステージが開いたときに入れる）。Play 中だけ、係のオブジェクトがコアを見つけて
    /// 「コアが壊れた」知らせを聞き、自分の PC の <see cref="Time.timeScale"/> を一瞬下げてから、なめらかに元へ戻す。
    /// 戻す先は始めたときの値。係が消えるとき（シーンの切り替え・Play 終了）にゆっくりの途中なら、必ず元の値へ戻す。通信しない。
    /// </summary>
    public static class CoreBreakSlowMotion
    {
        private static CoreBreakSlowMotionSettings _settings;

        /// <summary>今の設定。null なら何もしない（ステージが無いシーン）。</summary>
        public static CoreBreakSlowMotionSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                if (value != null && Application.isPlaying)
                {
                    CoreBreakSlowMotionRunner.Ensure();
                }
            }
        }

        /// <summary>
        /// 経過時間（実際の秒）から、その時の速さ。hold の間は slow、そのあと recover 秒でなめらかに normal へ戻る。
        /// </summary>
        public static float ScaleAt(float elapsed, float slow, float normal, float hold, float recover)
        {
            if (elapsed < hold)
            {
                return slow;
            }

            var t = Mathf.Clamp01((elapsed - hold) / Mathf.Max(recover, 0.0001f));
            return Mathf.Lerp(slow, normal, t * t * (3f - 2f * t));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
        }
    }

    /// <summary><see cref="CoreBreakSlowMotion"/> の係（Play 中だけ。Play を止めると消える）。</summary>
    [AddComponentMenu("")]
    internal sealed class CoreBreakSlowMotionRunner : MonoBehaviour
    {
        private static CoreBreakSlowMotionRunner _instance;

        private CoreCrystalController _core;
        private float _searchTimer;
        private bool _playing;
        private float _elapsed;
        private float _normalScale = 1f;

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("[Look] Core Break Slow Motion");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CoreBreakSlowMotionRunner>();
        }

        private void OnDestroy()
        {
            Watch(null);
            Restore();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_core == null)
            {
                _searchTimer -= Time.unscaledDeltaTime;
                if (_searchTimer <= 0f)
                {
                    _searchTimer = 0.5f;
                    Watch(FindFirstObjectByType<CoreCrystalController>());
                }
            }

            if (!_playing)
            {
                return;
            }

            var s = CoreBreakSlowMotion.Settings;
            if (s == null)
            {
                Restore();
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= s.holdSeconds + s.recoverSeconds)
            {
                Restore();
                return;
            }

            Time.timeScale = CoreBreakSlowMotion.ScaleAt(_elapsed, s.slowScale * _normalScale, _normalScale, s.holdSeconds, s.recoverSeconds);
        }

        private void Watch(CoreCrystalController core)
        {
            if (_core == core)
            {
                return;
            }

            if (_core != null)
            {
                _core.OnCoreDestroyed -= HandleCoreDestroyed;
            }

            _core = core;
            if (_core != null)
            {
                _core.OnCoreDestroyed += HandleCoreDestroyed;
            }
        }

        private void HandleCoreDestroyed()
        {
            var s = CoreBreakSlowMotion.Settings;
            if (s == null || !s.enabled || _playing)
            {
                return;
            }

            _normalScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _playing = true;
            _elapsed = 0f;
            Time.timeScale = s.slowScale * _normalScale;
        }

        private void Restore()
        {
            if (!_playing)
            {
                return;
            }

            _playing = false;
            Time.timeScale = _normalScale;
        }
    }
}
