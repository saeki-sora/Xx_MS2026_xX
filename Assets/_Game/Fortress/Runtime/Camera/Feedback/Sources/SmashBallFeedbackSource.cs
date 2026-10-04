using System;

namespace MS2026.Fortress.Cameras
{
    /// <summary>スマッシュボールが割れたら、割ったプレイヤーを関係者として演出を依頼する。</summary>
    public sealed class SmashBallFeedbackSource : ICameraFeedbackSource
    {
        private Action<CameraFeedbackRequest> _raise;

        public void Enable(Action<CameraFeedbackRequest> raise)
        {
            _raise = raise;
            SmashBallModule.AnyBroken += HandleBroken;
        }

        public void Disable()
        {
            SmashBallModule.AnyBroken -= HandleBroken;
            _raise = null;
        }

        private void HandleBroken(SmashBallBreakInfo info)
        {
            _raise?.Invoke(new CameraFeedbackRequest(
                CameraFeedbackEvent.SmashBallBroken,
                info.Attacker.PlayerIndex,
                (UnityEngine.Vector2)info.WorldPosition));
        }
    }
}
