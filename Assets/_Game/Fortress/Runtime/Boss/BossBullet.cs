using System.Collections.Generic;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボスの砲口が撃つ弾（2026-11-15 ユーザー決定: コアだけを狙う・レーザーで消せる）。
    /// まっすぐコアへ飛ぶ。レーザー（スキルも）が当たると消える。障害物に当たっても消える。コアに当たるとコアにダメージ。
    /// レーザーの当たり判定（Physics2D のレイ）に入るよう、当たり判定（円）を持つ。ダメージを決めるのは Host（オフラインでは自分）だけ。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossBullet : MonoBehaviour, ILaserTarget
    {
        private static readonly List<BossBullet> Live = new List<BossBullet>();
        private static readonly Collider2D[] OverlapBuffer = new Collider2D[8];
        private const float MaxLifeSeconds = 25f;

        private BossDefinition _boss;
        private CoreCrystalController _core;
        private Collider2D _ownCollider;
        private Transform _visual;
        private Vector2 _velocity;
        private float _health;
        private float _damage01;
        private float _radius;
        private float _coreRadius;
        private float _life;
        private bool _dead;

        /// <summary>今飛んでいる弾の数。</summary>
        public static int Count => Live.Count;

        public static BossBullet Spawn(BossDefinition boss, BossPartDefinition part, Vector2 position, Vector2 velocity, float z, int layer, CoreCrystalController core, float coreRadius)
        {
            var go = new GameObject("[Boss] Bullet") { layer = layer };
            go.transform.position = new Vector3(position.x, position.y, z);
            var bullet = go.AddComponent<BossBullet>();
            bullet._boss = boss;
            bullet._core = core;
            bullet._coreRadius = coreRadius;
            bullet._velocity = velocity;
            bullet._health = part.bulletHealth;
            bullet._damage01 = part.bulletDamage01;
            bullet._radius = part.bulletRadius;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = bullet._radius;
            bullet._ownCollider = collider;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            bullet._visual = new GameObject("Visual").transform;
            bullet._visual.SetParent(go.transform, false);
            bullet._visual.localScale = Vector3.one * (bullet._radius * 1.3f);
            var renderer = bullet._visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = boss.bulletSprite != null ? boss.bulletSprite : BossArt.Bullet;
            renderer.sortingOrder = 5;
            Live.Add(bullet);
            return bullet;
        }

        /// <summary>飛んでいる弾を全部消す（演出なし）。</summary>
        public static void ClearAll()
        {
            for (var i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i] != null)
                {
                    Destroy(Live[i].gameObject);
                }
            }

            Live.Clear();
        }

        /// <summary>飛んでいる弾を全部はじけさせる（倒した・届いたとき）。</summary>
        public static void PopAll()
        {
            for (var i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i] != null)
                {
                    Live[i].Pop(true);
                }
            }
        }

        private void OnDestroy()
        {
            Live.Remove(this);
        }

        private void Update()
        {
            if (_dead)
            {
                return;
            }

            var dt = Time.deltaTime;
            _life += dt;
            var p = (Vector2)transform.position + _velocity * dt;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            if (_visual != null)
            {
                _visual.Rotate(0f, 0f, 360f * dt);
                var pulse = 1f + 0.08f * Mathf.Sin(_life * 18f);
                _visual.localScale = Vector3.one * (_radius * 1.3f * pulse);
            }

            if (_life > MaxLifeSeconds || FortressMatch.HasEnded)
            {
                Pop(false);
                return;
            }

            if (_core != null && Vector2.Distance(p, _core.transform.position) <= _coreRadius + _radius)
            {
                if (FortressNet.HasSimulationAuthority)
                {
                    _core.TakeDamage(_core.maxHealth * _damage01 / Mathf.Max(0.01f, _core.damageMultiplier));
                }

                Play(_boss != null ? _boss.onBulletHitCore : null);
                BossPuff.Spawn(transform.position, _radius * 1.5f, _radius * 4.5f, 0.35f, new Color(1f, 0.3f, 0.25f, 0.95f));
                Kill();
                return;
            }

            var count = Physics2D.OverlapCircleNonAlloc(p, _radius * 0.85f, OverlapBuffer);
            for (var i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                if (other == null || other == _ownCollider || other.isTrigger)
                {
                    continue;
                }

                var obstacle = other.GetComponentInParent<NavigationObstacle>();
                if ((obstacle != null && obstacle.mode == NavigationObstacleMode.Block) || other.GetComponentInParent<DestructibleObstacle>() != null)
                {
                    Pop(true);
                    return;
                }
            }
        }

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            if (_dead || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            _health -= baseDamage;
            if (_health <= 0f)
            {
                Pop(true);
            }
        }

        private void Pop(bool withEffect)
        {
            if (_dead)
            {
                return;
            }

            if (withEffect)
            {
                Play(_boss != null ? _boss.onBulletBreak : null);
                BossPuff.Spawn(transform.position, _radius * 1.2f, _radius * 3.2f, 0.28f, new Color(1f, 0.62f, 0.25f, 0.9f));
            }

            Kill();
        }

        private void Kill()
        {
            _dead = true;
            Live.Remove(this);
            Destroy(gameObject);
        }

        private void Play(FortressEffect effect)
        {
            if (effect != null && !effect.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(effect, transform.position, Quaternion.identity);
            }
        }
    }
}
