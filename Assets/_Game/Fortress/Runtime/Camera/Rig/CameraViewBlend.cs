using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 視点の切り替え(プレイヤー変更・プリセット変更)をなめらかに補間する。
    /// 遷移先は毎フレーム受け取るので、遷移中にツールで値を変えてもそのまま追従する。
    /// </summary>
    public sealed class CameraViewBlend
    {
        private CameraViewSettings _from;
        private float _duration;
        private float _elapsed;
        private AnimationCurve _curve;

        public bool IsBlending => _elapsed < _duration;

        /// <summary>0-1の進み具合。遷移していなければ1。</summary>
        public float Progress01 => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

        /// <summary>現在の見た目(from)から遷移を始める。durationが0以下なら即座に切り替わる。</summary>
        public void Begin(CameraViewSettings from, float duration, AnimationCurve curve = null)
        {
            _from = from;
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;
            _curve = curve;
        }

        public void Cancel()
        {
            _elapsed = _duration;
        }

        public CameraViewSettings Evaluate(CameraViewSettings target, float deltaTime)
        {
            if (!IsBlending)
            {
                return target;
            }

            _elapsed += Mathf.Max(0f, deltaTime);
            var t = Progress01;
            var eased = _curve != null && _curve.length > 0 ? _curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
            return CameraViewSettings.Lerp(_from, target, eased);
        }
    }
}
