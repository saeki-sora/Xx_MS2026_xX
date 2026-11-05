using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えでカメラが次の席へ回っている間だけ、少し引いて全体を見せ、着地に合わせて戻す（2026-11-02）。
    /// 90° 回っても、どこにいるか分からなくならないように。各PCが自分のカメラにだけ足す。
    /// </summary>
    public sealed class SeatPullBackModifier : ICameraViewModifier
    {
        private readonly float _amount;
        private readonly float _seconds;
        private float _elapsed;

        /// <param name="amount">いちばん引いたときに広く映す割合（0.16 = 16%）。</param>
        /// <param name="seconds">引いて戻るまでの秒数（カメラが次の席へ移る秒数に合わせる）。</param>
        public SeatPullBackModifier(float amount, float seconds)
        {
            _amount = Mathf.Max(0f, amount);
            _seconds = Mathf.Max(0.01f, seconds);
        }

        public bool Apply(ref CameraViewSettings view, float deltaTime)
        {
            _elapsed += deltaTime;
            var weight = Mathf.Sin(Mathf.PI * Mathf.Clamp01(_elapsed / _seconds));
            view = view.WithVisibleHalfHeight(view.VisibleHalfHeight * (1f + _amount * weight));
            return _elapsed < _seconds;
        }
    }
}
