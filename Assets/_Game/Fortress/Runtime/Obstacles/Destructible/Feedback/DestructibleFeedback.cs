using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 被ダメージ・段階の進行・破壊・再生のたびに、設定されたエフェクトPrefabと効果音を出す。
    /// 破壊時の演出Prefabが空なら、仮の破片を飛ばす（素材が無い間の代わり）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleFeedback : MonoBehaviour
    {
        private DestructibleObstacle _obstacle;
        private float _nextHitTime;
        private int _lastStage = -1;

        private DestructibleFeedbackSettings Settings => _obstacle.settings.feedback;

        private void OnEnable()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Damaged += OnDamaged;
            _obstacle.StageChanged += OnStageChanged;
            _obstacle.Destroyed += OnDestroyed;
            _obstacle.Regenerated += OnRegenerated;
        }

        private void OnDisable()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Damaged -= OnDamaged;
            _obstacle.StageChanged -= OnStageChanged;
            _obstacle.Destroyed -= OnDestroyed;
            _obstacle.Regenerated -= OnRegenerated;
        }

        private void OnDamaged(DestructibleObstacle obstacle, float amount, DamageSource source)
        {
            if (Time.time < _nextHitTime)
            {
                return;
            }

            _nextHitTime = Time.time + Settings.hitInterval;
            Play(Settings.onHit);
        }

        private void OnStageChanged(DestructibleObstacle obstacle, int stage)
        {
            // より壊れた段階に進んだときだけ鳴らす（自己修復・再生で戻るときは鳴らさない）。
            if (stage > _lastStage)
            {
                Play(Settings.onStageChanged);
            }

            _lastStage = stage;
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            var effect = Settings.onDestroyed;
            Play(effect);

            if (effect.prefab == null && Settings.placeholderDebris && Settings.debrisCount > 0)
            {
                var scale = transform.lossyScale;
                PlaceholderDebrisBurst.Spawn(
                    transform.position,
                    new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)),
                    Settings.debrisCount,
                    Settings.debrisColor);
            }
        }

        private void OnRegenerated(DestructibleObstacle obstacle)
        {
            Play(Settings.onRegenerated);
        }

        private void Play(DestructibleEffect effect)
        {
            DestructibleEffectPlayer.Play(effect, transform.position, Settings.effectLifetimeSeconds);
        }
    }
}
