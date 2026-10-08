using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI
{
    /// <summary>
    /// 値をゲージ（バー）の長さで出す。Image の「Filled」なら塗りの量、そうでなければ横幅（アンカー）で伸び縮みする。
    /// 「残像バー」を入れると、減ったときに一瞬遅れて後を追う（格闘ゲームのHPバーのような）表現になる。
    /// </summary>
    [AddComponentMenu("UI Studio/値をゲージで出す (UiBindFill)")]
    public sealed class UiBindFill : UiBinding
    {
        [Tooltip("伸び縮みさせる画像（空なら同じ GameObject の Image）。")]
        public Image fill;

        [Tooltip("値がこの数のときゲージが空になる。")]
        public float min;

        [Tooltip("値がこの数のときゲージが満タンになる。")]
        public float max = 1f;

        [Tooltip("変わるときのなめらかさ（秒。0=すぐ）。")]
        [Min(0f)]
        public float smoothSeconds = 0.08f;

        [Tooltip("減ったときに遅れて後を追う画像（任意）。fill の後ろに置いた、色違いの同じ形の画像を入れる。")]
        public Image trail;

        [Tooltip("残像バーが追いかけ始めるまでの時間（秒）。")]
        [Min(0f)]
        public float trailDelay = 0.4f;

        [Tooltip("残像バーが追いかける速さ（1秒あたりの割合）。")]
        [Min(0.01f)]
        public float trailSpeed = 1.2f;

        private float _target;
        private float _current = -1f;
        private float _trail;
        private float _trailWait;

        /// <summary>今の見た目の割合（0〜1）。</summary>
        public float Shown => _current;

        protected override void OnEnable()
        {
            if (fill == null)
            {
                fill = GetComponent<Image>();
            }

            base.OnEnable();
        }

        protected override void Apply(UiValue value)
        {
            var ratio = Mathf.Approximately(max, min) ? 0f : Mathf.Clamp01((value.AsNumber - min) / (max - min));
            if (ratio < _target)
            {
                _trailWait = trailDelay;
            }

            _target = ratio;
            if (_current < 0f || !Application.isPlaying || smoothSeconds <= 0f)
            {
                _current = ratio;
                _trail = Mathf.Max(_trail, ratio);
                if (!Application.isPlaying)
                {
                    _trail = ratio;
                }

                Draw();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || _current < 0f)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var changed = false;
            if (!Mathf.Approximately(_current, _target))
            {
                _current = smoothSeconds > 0f ? Mathf.MoveTowards(_current, _target, dt / smoothSeconds) : _target;
                changed = true;
            }

            if (_trail > _current)
            {
                _trailWait -= dt;
                if (_trailWait <= 0f)
                {
                    _trail = Mathf.MoveTowards(_trail, _current, trailSpeed * dt);
                }

                changed = true;
            }
            else if (_trail < _current)
            {
                _trail = _current;
                changed = true;
            }

            if (changed)
            {
                Draw();
            }
        }

        private void Draw()
        {
            SetAmount(fill, _current);
            if (trail != null)
            {
                SetAmount(trail, Mathf.Max(_trail, _current));
            }
        }

        private static void SetAmount(Image image, float amount)
        {
            if (image == null)
            {
                return;
            }

            if (image.type == Image.Type.Filled)
            {
                image.fillAmount = amount;
                return;
            }

            var rect = image.rectTransform;
            var anchorMax = rect.anchorMax;
            anchorMax.x = rect.anchorMin.x + amount * (1f - rect.anchorMin.x);
            rect.anchorMax = anchorMax;
        }
    }
}
