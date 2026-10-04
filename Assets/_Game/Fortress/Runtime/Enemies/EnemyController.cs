using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>敵が消えた理由。</summary>
    public enum EnemyRemovalReason : byte
    {
        /// <summary>理由不明(シーン切り替え・スクリプトからの削除など)。</summary>
        Removed,

        /// <summary>倒された。</summary>
        Died,

        /// <summary>コアに到達してダメージを与えた。</summary>
        ReachedCore
    }

    /// <summary>
    /// 敵の最小実装。コアを目指して進み、接触するとダメージを与えて消滅する。
    /// 進行方向はEnemyNavigation.Current（経路探索）に問い合わせ、無ければ目標へ直進する。
    /// ネット対戦のClientでは<see cref="IsReplica"/>になり、Hostから届いた位置を表示するだけになる(移動・ダメージ・消滅はHostが決める)。
    /// </summary>
    public sealed class EnemyController : MonoBehaviour, ILaserTarget
    {
        public EnemyTypeDefinition definition;
        public Transform target;

        public event Action<EnemyController> OnDeath;

        /// <summary>有効な敵が増えた/減った(Play中のみ)。ネット同期など、敵の出入りを知りたい側が購読する。</summary>
        public static event Action<EnemyController> Registered;
        public static event Action<EnemyController> Unregistered;

        private static readonly List<EnemyController> ActiveList = new List<EnemyController>();

        /// <summary>今いる敵(Actorモード)の一覧。</summary>
        public static IReadOnlyList<EnemyController> Active => ActiveList;

        /// <summary>消えた理由。消える直前(OnDisable/Unregistered時点)に読める。</summary>
        public EnemyRemovalReason RemovalReason { get; private set; } = EnemyRemovalReason.Removed;

        /// <summary>今の移動速度(ワールド単位/秒)。見た目(向き・アニメーション)を変えたいスクリプトが読む。Clientでも有効。</summary>
        public Vector2 Velocity { get; private set; }

        /// <summary>trueの間は自分で動かず、Hostから届いた位置を表示するだけ(ネット対戦のClient側)。</summary>
        public bool IsReplica { get; private set; }

        private float _currentHealth;
        private Vector2 _moveDirection;
        private const float ArrivalDistance = 0.2f;

        // レプリカ用。届く位置は毎秒20回程度なので、次が届くまでは届いた速度で進め、ズレは滑らかに詰める。
        private const float ReplicaCorrectionSharpness = 10f;
        private const float ReplicaSnapDistance = 3f;
        private Vector2 _replicaTarget;
        private bool _hasReplicaTarget;

        /// <summary>レプリカ表示にする。生成直後(Startより前)に呼ぶ。</summary>
        public void BeginReplica()
        {
            IsReplica = true;
        }

        /// <summary>Hostから届いた位置と速度を反映する。snapなら補間せずにその位置へ置く(出現直後など)。</summary>
        public void ApplyReplicatedState(Vector2 position, Vector2 velocity, bool snap)
        {
            Velocity = velocity;
            _replicaTarget = position;

            var current = (Vector2)transform.position;
            if (snap || !_hasReplicaTarget || (position - current).sqrMagnitude > ReplicaSnapDistance * ReplicaSnapDistance)
            {
                transform.position = new Vector3(position.x, position.y, transform.position.z);
            }

            _hasReplicaTarget = true;
        }

        /// <summary>Hostで消えたことを反映する。倒された場合はOnDeathも発火する(倒れる演出などを全員の画面で出すため)。</summary>
        public void ApplyReplicatedRemoval(EnemyRemovalReason reason)
        {
            RemovalReason = reason;
            if (reason == EnemyRemovalReason.Died)
            {
                OnDeath?.Invoke(this);
            }

            Destroy(gameObject);
        }

        private void OnEnable()
        {
            ActiveList.Add(this);
            Registered?.Invoke(this);
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
            Unregistered?.Invoke(this);
        }

        private void Start()
        {
            _currentHealth = definition != null ? definition.maxHealth : 10f;

            if (target == null)
            {
                var core = FindFirstObjectByType<CoreCrystalController>();
                if (core != null)
                {
                    target = core.transform;
                }
            }

            ApplyPlaceholderVisual();
            EnsureHitCollider();
        }

        private void Update()
        {
            if (IsReplica)
            {
                TickReplica(Time.deltaTime);
                return;
            }

            if (target == null || definition == null)
            {
                Velocity = Vector2.zero;
                return;
            }

            var position = (Vector2)transform.position;
            var toTarget = (Vector2)target.position - position;

            if (toTarget.magnitude <= ArrivalDistance)
            {
                target.GetComponent<CoreCrystalController>()?.TakeDamage(definition.damageToCore);
                RemovalReason = EnemyRemovalReason.ReachedCore;
                Destroy(gameObject);
                return;
            }

            var navigator = EnemyNavigation.Current;
            var desired = toTarget.normalized;
            if (navigator != null)
            {
                var navigated = navigator.GetDirection(position, definition.navigationProfile);
                if (navigated.sqrMagnitude > 1e-6f)
                {
                    desired = navigated;
                }
            }

            var blend = 1f - Mathf.Exp(-definition.turnSharpness * Time.deltaTime);
            _moveDirection = _moveDirection.sqrMagnitude < 1e-6f
                ? desired
                : Vector2.Lerp(_moveDirection, desired, blend);
            if (_moveDirection.sqrMagnitude > 1e-6f)
            {
                _moveDirection.Normalize();
            }

            var speed = definition.moveSpeed * (navigator != null ? navigator.GetSpeedMultiplier(position) : 1f);
            Velocity = _moveDirection * speed;
            transform.position += (Vector3)(Velocity * Time.deltaTime);
        }

        private void TickReplica(float dt)
        {
            if (!_hasReplicaTarget)
            {
                return;
            }

            _replicaTarget += Velocity * dt;
            var current = (Vector2)transform.position + Velocity * dt;
            var next = Vector2.Lerp(current, _replicaTarget, 1f - Mathf.Exp(-ReplicaCorrectionSharpness * dt));
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            TakeDamage(baseDamage);
        }

        public void TakeDamage(float amount)
        {
            if (IsReplica || _currentHealth <= 0f)
            {
                return;
            }

            _currentHealth -= amount;

            if (_currentHealth <= 0f)
            {
                RemovalReason = EnemyRemovalReason.Died;
                OnDeath?.Invoke(this);
                Destroy(gameObject);
            }
        }

        private void ApplyPlaceholderVisual()
        {
            if (definition == null || definition.visualPrefab != null || GetComponentInChildren<SpriteRenderer>() != null)
            {
                return;
            }

            var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite();
            spriteRenderer.color = definition.placeholderColor;
        }

        /// <summary>
        /// レーザーのRaycast判定(LaserBeamVisual)が敵に当たるためには何らかのCollider2Dが要る。
        /// 本番プレファブに専用のコライダーが無いケースの保険として、無ければ円形を追加する。
        /// </summary>
        private void EnsureHitCollider()
        {
            if (GetComponent<Collider2D>() != null)
            {
                return;
            }

            gameObject.AddComponent<CircleCollider2D>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ActiveList.Clear();
            Registered = null;
            Unregistered = null;
        }
    }
}
