using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 被ダメージ・段階の進行・破壊・再生のたびに、設定された演出（D-DriveのVFX/SE）を出す。
    /// 破壊時のエフェクトが空なら、仮の破片を飛ばす（素材が無い間の代わり）。
    /// ネット対戦のClientでも、Hostから届いた状態変化で同じイベントが起きるので、そのまま全員の画面に出る。
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
            Play(Settings.onHit, obstacle.LastAttacker.PlayerIndex);
        }

        private void OnStageChanged(DestructibleObstacle obstacle, int stage)
        {
            // より壊れた段階に進んだときだけ鳴らす（自己修復・再生で戻るときは鳴らさない）。
            if (stage > _lastStage)
            {
                Play(Settings.onStageChanged, obstacle.LastAttacker.PlayerIndex);
            }

            _lastStage = stage;
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            var effect = Settings.onDestroyed;
            Play(effect, obstacle.DestroyedBy.PlayerIndex);

            if (!effect.HasVfx && Settings.placeholderDebris && Settings.debrisCount > 0)
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
            Play(Settings.onRegenerated, -1);
        }

        private void Play(FortressEffect effect, int playerIndex)
        {
            FortressEffectPlayer.PlayOnce(effect, transform.position, Quaternion.identity, playerIndex);
        }
    }
}
