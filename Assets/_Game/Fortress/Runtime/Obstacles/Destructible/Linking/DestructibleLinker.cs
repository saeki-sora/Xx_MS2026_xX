using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 他の破壊可能物との関係を担当する。「最初は無敵→条件で解除」「グループでの連動破壊/ダメージ共有」「壊れたときの連鎖」。
    /// 連動で伝わったダメージ(DamageSource.Linked)は再び連動させず、無限ループを防ぐ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleLinker : MonoBehaviour
    {
        private readonly List<DestructibleObstacle> _buffer = new List<DestructibleObstacle>();
        private readonly List<DestructibleObstacle> _watchedTargets = new List<DestructibleObstacle>();
        private DestructibleObstacle _obstacle;
        private float _elapsedSinceEnable;

        private void OnEnable()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Damaged += OnDamaged;
            _obstacle.Destroyed += OnDestroyed;

            _elapsedSinceEnable = 0f;
            _watchedTargets.Clear();
            foreach (var target in _obstacle.protection.unlockWhenDestroyed)
            {
                if (target != null)
                {
                    target.Destroyed += OnUnlockTargetDestroyed;
                    _watchedTargets.Add(target);
                }
            }
        }

        private void OnDisable()
        {
            if (_obstacle == null)
            {
                return;
            }

            _obstacle.Damaged -= OnDamaged;
            _obstacle.Destroyed -= OnDestroyed;

            foreach (var target in _watchedTargets)
            {
                if (target != null)
                {
                    target.Destroyed -= OnUnlockTargetDestroyed;
                }
            }

            _watchedTargets.Clear();
        }

        private void Update()
        {
            if (_obstacle == null)
            {
                return;
            }

            var protection = _obstacle.protection;
            if (!_obstacle.IsInvulnerable || protection.unlockAfterSeconds <= 0f)
            {
                return;
            }

            _elapsedSinceEnable += Time.deltaTime;
            if (_elapsedSinceEnable >= protection.unlockAfterSeconds)
            {
                _obstacle.Unlock();
            }
        }

        private void OnUnlockTargetDestroyed(DestructibleObstacle destroyedTarget)
        {
            if (!_obstacle.IsInvulnerable)
            {
                return;
            }

            foreach (var target in _watchedTargets)
            {
                if (target != null && !target.IsDestroyed)
                {
                    return;
                }
            }

            _obstacle.Unlock();
        }

        private void OnDamaged(DestructibleObstacle obstacle, float amount, DamageSource source)
        {
            var link = obstacle.link;
            if (source == DamageSource.Linked || !link.HasGroup || link.groupMode != DestructibleGroupMode.ShareDamage)
            {
                return;
            }

            DestructibleRegistry.CollectGroup(link.groupId, obstacle, _buffer);
            foreach (var member in _buffer)
            {
                member.ApplyDamage(amount, DamageSource.Linked, obstacle.LastAttacker);
            }
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            var link = obstacle.link;

            if (link.HasGroup
                && link.groupMode == DestructibleGroupMode.DestroyTogether
                && obstacle.LastDestroySource != DamageSource.Linked)
            {
                DestructibleRegistry.CollectGroup(link.groupId, obstacle, _buffer);
                foreach (var member in _buffer)
                {
                    if (!member.IsDestroyed && !member.IsInvulnerable)
                    {
                        member.DestroyNow(DamageSource.Linked, obstacle.DestroyedBy);
                    }
                }
            }

            if (link.chainRadius > 0f && link.chainDamage > 0f && isActiveAndEnabled)
            {
                StartCoroutine(ChainRoutine(link, obstacle.DestroyedBy));
            }
        }

        private IEnumerator ChainRoutine(DestructibleLinkSettings link, DestructibleAttacker attacker)
        {
            if (link.chainDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(link.chainDelaySeconds);
            }

            DestructibleRegistry.CollectNear(transform.position, link.chainRadius, _obstacle, _buffer);
            foreach (var neighbour in _buffer)
            {
                neighbour.ApplyDamage(link.chainDamage, DamageSource.Chain, attacker);
            }
        }
    }
}
