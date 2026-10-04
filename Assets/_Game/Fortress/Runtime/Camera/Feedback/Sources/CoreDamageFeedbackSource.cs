using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>シーン内のコアクリスタルがダメージを受けたら、ダメージ量を大きさとして演出を依頼する。</summary>
    public sealed class CoreDamageFeedbackSource : ICameraFeedbackSource
    {
        private readonly List<(CoreCrystalController core, Action<float, float> handler)> _subscriptions =
            new List<(CoreCrystalController, Action<float, float>)>();

        public void Enable(Action<CameraFeedbackRequest> raise)
        {
            var cores = UnityEngine.Object.FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None);
            foreach (var core in cores)
            {
                var target = core;
                var previousHealth = core.CurrentHealth > 0f ? core.CurrentHealth : core.maxHealth;

                void Handler(float current, float max)
                {
                    var damage = previousHealth - current;
                    previousHealth = current;

                    if (damage > 0f && target != null)
                    {
                        raise(new CameraFeedbackRequest(CameraFeedbackEvent.CoreDamaged, ViewerIndex.Overview, (Vector2)target.transform.position, damage));
                    }
                }

                Action<float, float> handler = Handler;
                core.OnHealthChanged += handler;
                _subscriptions.Add((core, handler));
            }
        }

        public void Disable()
        {
            foreach (var (core, handler) in _subscriptions)
            {
                if (core != null)
                {
                    core.OnHealthChanged -= handler;
                }
            }

            _subscriptions.Clear();
        }
    }
}
