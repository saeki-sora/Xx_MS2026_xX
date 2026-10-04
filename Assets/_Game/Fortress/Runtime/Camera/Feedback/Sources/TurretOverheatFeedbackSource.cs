using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>砲台がオーバーヒートしたら、その砲台のプレイヤーを関係者として演出を依頼する。</summary>
    public sealed class TurretOverheatFeedbackSource : ICameraFeedbackSource
    {
        private readonly List<(LaserTurret turret, Action<TurretState> handler)> _subscriptions =
            new List<(LaserTurret, Action<TurretState>)>();

        public void Enable(Action<CameraFeedbackRequest> raise)
        {
            var turrets = UnityEngine.Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None);
            foreach (var turret in turrets)
            {
                var target = turret;

                void Handler(TurretState state)
                {
                    if (state == TurretState.Overheated && target != null)
                    {
                        raise(new CameraFeedbackRequest(CameraFeedbackEvent.TurretOverheated, target.playerIndex, (Vector2)target.transform.position));
                    }
                }

                Action<TurretState> handler = Handler;
                turret.OnStateChanged += handler;
                _subscriptions.Add((turret, handler));
            }
        }

        public void Disable()
        {
            foreach (var (turret, handler) in _subscriptions)
            {
                if (turret != null)
                {
                    turret.OnStateChanged -= handler;
                }
            }

            _subscriptions.Clear();
        }
    }
}
