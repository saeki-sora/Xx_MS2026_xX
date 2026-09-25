using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザーで削れて破壊され、時間経過で再生する地形障害物。見た目は持たず、状態(HP/再生タイマー)と
    /// 衝突判定の有効/無効だけを管理する（見た目は<see cref="ObstacleVisual"/>が担当し、差し替え可能にする）。
    /// 特定の砲台に紐づかない汎用コンポーネントなので、シーン上のどこにでも好きな数だけ配置できる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [DefaultExecutionOrder(-10)] // ObstacleVisualより先にAwakeでHPを初期化しておく（表示側の初期化順依存をなくす）。
    public sealed class DestructibleObstacle : MonoBehaviour
    {
        [Tooltip("この障害物の耐久・再生・見た目のチューニングデータ。")]
        public DestructibleObstacleTuning tuning;

        /// <summary>状態(Intact/Destroyed)が変化した際に発火する。</summary>
        public event Action<ObstacleState> OnStateChanged;

        /// <summary>HPが変化した際に発火する。(現在値, 最大値)</summary>
        public event Action<float, float> OnHealthChanged;

        public ObstacleState State { get; private set; } = ObstacleState.Intact;
        public float CurrentHealth { get; private set; }

        /// <summary>0-1に正規化したHP。UI表示・見た目のスケールに使う。</summary>
        public float HealthRatio01 => tuning != null && tuning.maxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / tuning.maxHealth)
            : 0f;

        /// <summary>再生待機中の残り秒数。待機を終えて再生が始まると0になる。</summary>
        public float RegenDelayRemaining { get; private set; }

        /// <summary>
        /// 現在のサイズ(幅, 高さ)。実体はtransform.localScale。BoxCollider2D側は常に1x1のまま
        /// このスケールに従うので、コリジョンと見た目(子のSpriteRenderer)が同時に、必ず同じ比率で動く。
        /// </summary>
        public Vector2 Size => transform.localScale;

        private BoxCollider2D _collider;
        private bool _isRegenerating;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.size = Vector2.one; // サイズはtransform.localScaleだけで表現する(下のSetSize参照)。
            CurrentHealth = tuning != null ? tuning.maxHealth : 0f;
        }

        /// <summary>
        /// 配置済みの障害物のサイズを変更する。transform.localScaleを直接動かすので、
        /// BoxCollider2D(コリジョン)と子のSpriteRenderer(見た目)が親子階層の仕組みだけで
        /// 常に同時に、同じ比率で追従する。ObstacleVisual側に個別の同期処理は要らない。
        /// </summary>
        public void SetSize(Vector2 size)
        {
            var clamped = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            transform.localScale = new Vector3(clamped.x, clamped.y, 1f);

            // 万一BoxCollider2D側のsizeが1x1からズレていたら(手動編集などで)、ここで正規化しておく。
            var box = _collider != null ? _collider : GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.size = Vector2.one;
            }
        }

        private void Update()
        {
            if (tuning == null)
            {
                return;
            }

            if (State == ObstacleState.Destroyed && !_isRegenerating)
            {
                TickRegenDelay();
                return;
            }

            if (_isRegenerating)
            {
                TickGradualRegen();
            }
        }

        /// <summary>ダメージを受け、HPが減る。0になると破壊される。</summary>
        public void TakeDamage(float amount)
        {
            if (tuning == null || State == ObstacleState.Destroyed || amount <= 0f)
            {
                return;
            }

            SetHealth(CurrentHealth - amount);

            if (CurrentHealth <= 0f)
            {
                EnterDestroyed();
            }
        }

        /// <summary>
        /// レーザー専用のダメージ入力口。太さ(laserStrength01, 0-1)に応じて
        /// tuning.laserStrengthToBreakSpeedCurveでダメージ倍率を決める。
        /// scaleBreakSpeedByLaserStrengthがオフなら太さを無視して等倍で通す。
        /// </summary>
        public void TakeLaserDamage(float baseDamage, float laserStrength01)
        {
            if (tuning == null)
            {
                return;
            }

            var multiplier = tuning.scaleBreakSpeedByLaserStrength
                ? Mathf.Max(0f, tuning.laserStrengthToBreakSpeedCurve.Evaluate(Mathf.Clamp01(laserStrength01)))
                : 1f;

            TakeDamage(baseDamage * multiplier);
        }

        /// <summary>即座に破壊状態にする。デバッグ/テストタブから使う。</summary>
        public void ForceDestroy()
        {
            if (tuning == null || State == ObstacleState.Destroyed)
            {
                return;
            }

            EnterDestroyed();
        }

        /// <summary>
        /// 再生待ちを飛ばして即座に満タン復活させる。デバッグ/テストタブに加えて、
        /// tuningをAwake後に割り当てるエディタツールの初期化用途にも使う。
        /// </summary>
        public void ResetToFull()
        {
            _isRegenerating = false;
            RegenDelayRemaining = 0f;
            SetHealth(tuning != null ? tuning.maxHealth : 0f);
            SetState(ObstacleState.Intact);
        }

        private void TickRegenDelay()
        {
            RegenDelayRemaining = Mathf.Max(0f, RegenDelayRemaining - Time.deltaTime);

            if (RegenDelayRemaining > 0f)
            {
                return;
            }

            if (tuning.regenMode == ObstacleRegenMode.Instant)
            {
                SetHealth(tuning.maxHealth);
                SetState(ObstacleState.Intact);
                return;
            }

            // Gradual: 再生が始まった瞬間から衝突判定を復活させ、削れながらでも育っていく見た目にする。
            _isRegenerating = true;
            SetState(ObstacleState.Intact);
        }

        private void TickGradualRegen()
        {
            var rate = tuning.maxHealth / Mathf.Max(0.01f, tuning.regenDuration);
            SetHealth(CurrentHealth + rate * Time.deltaTime);

            if (CurrentHealth >= tuning.maxHealth)
            {
                SetHealth(tuning.maxHealth);
                _isRegenerating = false;
            }
        }

        private void EnterDestroyed()
        {
            _isRegenerating = false;
            SetHealth(0f);
            RegenDelayRemaining = tuning.regenDelay;
            SetState(ObstacleState.Destroyed);
        }

        private void SetHealth(float value)
        {
            var max = tuning != null ? tuning.maxHealth : 0f;
            var clamped = Mathf.Clamp(value, 0f, max);

            if (Mathf.Approximately(clamped, CurrentHealth))
            {
                return;
            }

            CurrentHealth = clamped;
            OnHealthChanged?.Invoke(CurrentHealth, max);
        }

        private void SetState(ObstacleState next)
        {
            if (_collider != null)
            {
                // 健在(Intact)の間だけレーザーの当たり判定を持つ＝破壊後は自動で素通りするようになる。
                _collider.enabled = next == ObstacleState.Intact;
            }

            if (State == next)
            {
                return;
            }

            State = next;
            OnStateChanged?.Invoke(State);
        }

        private void OnDrawGizmosSelected()
        {
            var healthy = tuning != null ? tuning.healthyColor : Color.green;
            var damaged = tuning != null ? tuning.damagedColor : Color.red;
            Gizmos.color = State == ObstacleState.Destroyed ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : Color.Lerp(damaged, healthy, HealthRatio01);

            var box = GetComponent<BoxCollider2D>();
            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box != null ? (Vector3)box.offset : Vector3.zero, box != null ? (Vector3)box.size : Vector3.one);
            Gizmos.matrix = previousMatrix;
        }
    }
}
