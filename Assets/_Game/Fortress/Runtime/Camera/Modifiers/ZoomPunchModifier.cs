using System;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>一時的な「寄り(ズームイン)」の設定。寄る→少し止まる→戻る、の3段階。</summary>
    [Serializable]
    public sealed class ZoomPunchSettings
    {
        [Tooltip("どれだけ寄るか。0.15で映る範囲が15%狭くなる。負の値にすると引き(ズームアウト)になる。")]
        [Range(-0.5f, 0.8f)]
        public float zoomAmount = 0.12f;

        [Tooltip("出来事が起きた場所へ画面の中心をどれだけ寄せるか。0で中心は動かない、1で出来事の位置が画面中央に来る。")]
        [Range(0f, 1f)]
        public float focusTowardSource = 0.2f;

        [Tooltip("寄り切るまでの秒数。")]
        [Min(0f)]
        public float attackSeconds = 0.08f;

        [Tooltip("寄ったまま止まる秒数。")]
        [Min(0f)]
        public float holdSeconds = 0.1f;

        [Tooltip("元に戻るまでの秒数。")]
        [Min(0f)]
        public float releaseSeconds = 0.35f;

        public float TotalSeconds => attackSeconds + holdSeconds + releaseSeconds;

        /// <summary>経過時間に対する効き具合(0-1)。</summary>
        public float Envelope(float elapsed)
        {
            if (elapsed < attackSeconds)
            {
                return attackSeconds > 0f ? Mathf.SmoothStep(0f, 1f, elapsed / attackSeconds) : 1f;
            }

            elapsed -= attackSeconds;
            if (elapsed < holdSeconds)
            {
                return 1f;
            }

            elapsed -= holdSeconds;
            return releaseSeconds > 0f ? Mathf.SmoothStep(1f, 0f, elapsed / releaseSeconds) : 0f;
        }
    }

    /// <summary>一時的に寄る(ズームパンチ)演出。映る広さを縮め、必要なら出来事の位置へ中心を寄せる。</summary>
    public sealed class ZoomPunchModifier : ICameraViewModifier
    {
        private readonly ZoomPunchSettings _settings;
        private readonly Vector2? _focus;
        private readonly float _strength;
        private float _elapsed;

        public ZoomPunchModifier(ZoomPunchSettings settings, Vector2? focus, float strength)
        {
            _settings = settings;
            _focus = focus;
            _strength = Mathf.Max(0f, strength);
        }

        public bool Apply(ref CameraViewSettings view, float deltaTime)
        {
            if (_settings == null)
            {
                return false;
            }

            _elapsed += deltaTime;
            var weight = _settings.Envelope(_elapsed) * _strength;

            var scale = Mathf.Max(0.05f, 1f - _settings.zoomAmount * weight);
            view = view.WithVisibleHalfHeight(view.VisibleHalfHeight * scale);

            if (_focus.HasValue)
            {
                view.center = Vector2.Lerp(view.center, _focus.Value, Mathf.Clamp01(_settings.focusTowardSource * weight));
            }

            return _elapsed < _settings.TotalSeconds;
        }
    }
}
