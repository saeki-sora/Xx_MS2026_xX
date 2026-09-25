using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>ダメージがどこから来たか。連動・連鎖の無限ループを防ぐために使う。</summary>
    public enum DamageSource
    {
        Laser,
        Script,

        /// <summary>同じグループの仲間から伝わってきた。</summary>
        Linked,

        /// <summary>近くの破壊可能物の連鎖で受けた。</summary>
        Chain
    }

    /// <summary>
    /// レーザーで壊せる障害物の「状態」（耐久・破壊・再生・無敵）だけを持つ本体。
    /// 見た目・当たり判定・演出・ドロップ・連動は、同じGameObjectに付く専用コンポーネントが
    /// このクラスのイベントを購読して担当する（このクラスは彼らの中身を呼び出さない）。
    /// 旧バージョンの保存データ（耐久・再生設定）は読み込み時に自動で新形式へ移す。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavigationObstacle), typeof(DestructibleCollision), typeof(DestructibleVisual))]
    [RequireComponent(typeof(DestructibleFeedback), typeof(DestructibleDropper), typeof(DestructibleLinker))]
    [RequireComponent(typeof(DestructibleHealthBar))]
    public sealed class DestructibleObstacle : MonoBehaviour, IAttributedLaserTarget, IDamageable, ISerializationCallbackReceiver
    {
        private const int CurrentDataVersion = 1;

        [Tooltip("最後に適用したプリセット。「同じプリセットの物を選択」や再適用の目印になる（設定自体は下の値が使われる）。")]
        public DestructiblePreset sourcePreset;

        public DestructibleSettings settings = new DestructibleSettings();

        [Header("個体ごとの設定")]
        public DestructibleLinkSettings link = new DestructibleLinkSettings();

        public DestructibleProtection protection = new DestructibleProtection();

        // 旧バージョン(v0)が保存していた値。名前を変えないことで既存のシーン/プレハブのデータを読める。
        [SerializeField, HideInInspector] private float maxHealth = 30f;
        [SerializeField, HideInInspector] private bool regenerates = true;
        [SerializeField, HideInInspector] private float regenDelaySeconds = 10f;
        [SerializeField, HideInInspector] private float destroyedGhostAlpha = 0.15f;
        [SerializeField, HideInInspector] private int dataVersion;

        /// <summary>ダメージを受けた（倍率適用後のダメージ量つき）。</summary>
        public event Action<DestructibleObstacle, float, DamageSource> Damaged;

        /// <summary>耐久が変化した（ダメージ・自己修復・再生）。</summary>
        public event Action<DestructibleObstacle> HealthChanged;

        /// <summary>見た目の段階（ひび割れ等）が変わった。引数は新しい段階の番号（-1=通常）。</summary>
        public event Action<DestructibleObstacle, int> StageChanged;

        public event Action<DestructibleObstacle> Destroyed;
        public event Action<DestructibleObstacle> Regenerated;

        /// <summary>設定が編集された（Inspector・ツールでの変更）。見た目などの作り直しの合図。</summary>
        public event Action SettingsChanged;

        /// <summary>無敵状態が変わった。</summary>
        public event Action<DestructibleObstacle> InvulnerabilityChanged;

        public float MaxHealth => Mathf.Max(1f, settings.durability.maxHealth);
        public float CurrentHealth
        {
            get
            {
                EnsureInitialized();
                return _health;
            }
        }

        public float Health01 => Mathf.Clamp01(CurrentHealth / MaxHealth);
        public bool IsDestroyed { get; private set; }
        public bool IsInvulnerable { get; private set; }

        /// <summary>これまでに壊された回数。</summary>
        public int DestroyCount { get; private set; }

        /// <summary>直近の破壊のきっかけ。</summary>
        public DamageSource LastDestroySource { get; private set; }

        /// <summary>直近に分かっている攻撃者。攻撃者不明のダメージ（自己修復・スクリプト等）では上書きされない。</summary>
        public DestructibleAttacker LastAttacker { get; private set; }

        /// <summary>現在（直近）の破壊を行った攻撃者。壊れていなければNone。</summary>
        public DestructibleAttacker DestroyedBy { get; private set; }

        /// <summary>現在の見た目の段階の番号。-1は通常。</summary>
        public int StageIndex { get; private set; } = -1;

        /// <summary>再生までの進み具合(0-1)。破壊中のみ意味を持つ。</summary>
        public float RegenProgress01 => IsDestroyed && settings.durability.regenDelaySeconds > 0f
            ? Mathf.Clamp01(_regenTimer / settings.durability.regenDelaySeconds)
            : 0f;

        /// <summary>回転を無視した、おおよその範囲（連鎖の距離判定に使う）。</summary>
        public Bounds ApproximateBounds
        {
            get
            {
                var scale = transform.lossyScale;
                return new Bounds(transform.position, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 0.1f));
            }
        }

        private float _health = -1f;
        private float _regenTimer;
        private float _secondsSinceDamage;

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            ApplyLaserDamage(baseDamage, tuning, null);
        }

        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning, LaserTurret attacker)
        {
            var multiplier = tuning != null ? tuning.obstacleDamageMultiplier : 1f;
            ApplyDamage(baseDamage * multiplier, DamageSource.Laser, DestructibleAttacker.FromTurret(attacker));
        }

        public void TakeDamage(float amount)
        {
            ApplyDamage(amount, DamageSource.Script);
        }

        public void ApplyDamage(float amount, DamageSource source, DestructibleAttacker attacker = default)
        {
            EnsureInitialized();
            if (IsDestroyed || IsInvulnerable || amount <= 0f)
            {
                return;
            }

            var scaled = amount * settings.durability.damageMultiplier;
            if (scaled <= 0f)
            {
                return;
            }

            if (attacker.IsKnown)
            {
                LastAttacker = attacker;
            }

            _health = Mathf.Max(0f, _health - scaled);
            _secondsSinceDamage = 0f;

            Damaged?.Invoke(this, scaled, source);
            HealthChanged?.Invoke(this);
            RefreshStage();

            if (_health <= 0f)
            {
                DestroyNow(source, attacker.IsKnown ? attacker : LastAttacker);
            }
        }

        /// <summary>耐久に関係なく今すぐ壊す。</summary>
        public void DestroyNow(DamageSource source = DamageSource.Script, DestructibleAttacker attacker = default)
        {
            EnsureInitialized();
            if (IsDestroyed)
            {
                return;
            }

            if (attacker.IsKnown)
            {
                LastAttacker = attacker;
            }

            _health = 0f;
            _regenTimer = 0f;
            IsDestroyed = true;
            DestroyCount++;
            LastDestroySource = source;
            DestroyedBy = attacker.IsKnown ? attacker : LastAttacker;

            RefreshStage();
            HealthChanged?.Invoke(this);
            Destroyed?.Invoke(this);
        }

        /// <summary>壊れていれば再生し、壊れていなければ耐久を全快させる。</summary>
        public void Regenerate()
        {
            var wasDestroyed = IsDestroyed;
            IsDestroyed = false;
            DestroyedBy = DestructibleAttacker.None;
            _regenTimer = 0f;
            _secondsSinceDamage = 0f;
            _health = MaxHealth * (wasDestroyed ? settings.durability.regenHealthRatio : 1f);

            RefreshStage();
            HealthChanged?.Invoke(this);
            if (wasDestroyed)
            {
                Regenerated?.Invoke(this);
            }
        }

        public void SetInvulnerable(bool value)
        {
            if (IsInvulnerable == value)
            {
                return;
            }

            IsInvulnerable = value;
            InvulnerabilityChanged?.Invoke(this);
        }

        /// <summary>無敵を解除して壊せるようにする。</summary>
        public void Unlock()
        {
            SetInvulnerable(false);
        }

        /// <summary>
        /// 必要な部品（見た目・当たり判定・演出など）が無ければ追加する。
        /// 部品が増える前の旧バージョンで作られたオブジェクトには、RequireComponentが自動では効かないため。
        /// </summary>
        public void EnsureRequiredComponents()
        {
            EnsureComponent<NavigationObstacle>();
            EnsureComponent<DestructibleCollision>();
            EnsureComponent<DestructibleVisual>();
            EnsureComponent<DestructibleFeedback>();
            EnsureComponent<DestructibleDropper>();
            EnsureComponent<DestructibleLinker>();
            EnsureComponent<DestructibleHealthBar>();
        }

        private void EnsureComponent<T>() where T : Component
        {
            if (GetComponent<T>() == null)
            {
                gameObject.AddComponent<T>();
            }
        }

        private void Awake()
        {
            EnsureRequiredComponents();
            EnsureInitialized();
            dataVersion = CurrentDataVersion;
            IsInvulnerable = protection.startsInvulnerable;
        }

        private void OnEnable()
        {
            DestructibleRegistry.Register(this);
        }

        private void OnDisable()
        {
            DestructibleRegistry.Unregister(this);
        }

        private void Update()
        {
            var durability = settings.durability;

            if (IsDestroyed)
            {
                if (durability.regenerates)
                {
                    _regenTimer += Time.deltaTime;
                    if (_regenTimer >= durability.regenDelaySeconds)
                    {
                        Regenerate();
                    }
                }

                return;
            }

            if (durability.selfRepairPerSecond > 0f && _health < MaxHealth)
            {
                _secondsSinceDamage += Time.deltaTime;
                if (_secondsSinceDamage >= durability.selfRepairDelaySeconds)
                {
                    _health = Mathf.Min(MaxHealth, _health + durability.selfRepairPerSecond * Time.deltaTime);
                    HealthChanged?.Invoke(this);
                    RefreshStage();
                }
            }
        }

        private void EnsureInitialized()
        {
            if (_health < 0f)
            {
                _health = MaxHealth;
            }
        }

        private void RefreshStage()
        {
            var stage = IsDestroyed ? -1 : settings.visual.ResolveStage(Health01);
            if (stage == StageIndex)
            {
                return;
            }

            StageIndex = stage;
            StageChanged?.Invoke(this, stage);
        }

        private void Reset()
        {
            dataVersion = CurrentDataVersion;
        }

        private void OnValidate()
        {
            if (_health > MaxHealth)
            {
                _health = MaxHealth;
            }

            SettingsChanged?.Invoke();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (dataVersion >= CurrentDataVersion)
            {
                return;
            }

            // 旧バージョンで保存されたデータ。耐久・再生の値を引き継ぎ、仮の四角の色は元のまま触らない。
            settings.durability.maxHealth = maxHealth;
            settings.durability.regenerates = regenerates;
            settings.durability.regenDelaySeconds = regenDelaySeconds;
            settings.visual.destroyedGhostAlpha = destroyedGhostAlpha;
            settings.visual.controlPlaceholderColor = false;
            dataVersion = CurrentDataVersion;
        }
    }
}
