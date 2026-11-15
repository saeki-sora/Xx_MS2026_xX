using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボスの部位1つ（<see cref="BossActor"/> が作る）。レーザー・スキルが当たると HP が減り、0 で壊れる（生え直さない）。
    /// ダメージを決めるのは Host（オフラインでは自分）だけ。普段のレーザーは砲台の情報付き（<see cref="IAttributedLaserTarget"/>）で届き、
    /// 砲台の情報が無いもの（スキル）はスキルの倍率を掛ける（2026-11-15 ユーザー決定）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossPart : MonoBehaviour, IAttributedLaserTarget
    {
        private const float HitFlashSeconds = 0.08f;

        private SpriteRenderer _renderer;
        private Transform _visual;
        private SpriteRenderer _barBack;
        private SpriteRenderer _barFill;
        private CircleCollider2D _collider;
        private float _hitFlash;

        public BossActor Owner { get; private set; }

        public int Index { get; private set; }

        public BossPartDefinition Definition { get; private set; }

        public float Health { get; set; }

        public bool Alive { get; private set; } = true;

        /// <summary>次の攻撃までの秒数（Host が数える）。</summary>
        public float Cooldown { get; set; }

        /// <summary>この部位の世界での半径（メートル）。</summary>
        public float WorldRadius { get; private set; }

        public float Health01 => Definition != null && Definition.maxHealth > 0f ? Mathf.Clamp01(Health / Definition.maxHealth) : 0f;

        internal void Setup(BossActor owner, int index, BossPartDefinition definition, float bodyRadius, Sprite sprite, int layer)
        {
            Owner = owner;
            Index = index;
            Definition = definition;
            Health = definition.maxHealth;
            Cooldown = definition.firstDelaySeconds;
            WorldRadius = Mathf.Max(0.05f, definition.radius * bodyRadius);
            gameObject.layer = layer;
            transform.localPosition = new Vector3(definition.offset.x, definition.offset.y, 0f) * bodyRadius;

            _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.radius = WorldRadius;
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visual.localScale = Vector3.one * (WorldRadius * (definition.kind == BossPartKind.Cannon ? 1.45f : 1.2f));
            _renderer = _visual.gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = sprite;
            _renderer.sortingOrder = 2;

            _barBack = MakeBar("HpBack", new Color(0.08f, 0.04f, 0.1f, 0.85f), 3);
            _barFill = MakeBar("HpFill", new Color(1f, 0.82f, 0.4f, 1f), 4);
            UpdateBar();
        }

        private SpriteRenderer MakeBar(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BossArt.White;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>撃つ向き（コアの方）へ絵を向ける（砲口だけ）。</summary>
        internal void Aim(Vector2 worldTarget)
        {
            if (Definition == null || Definition.kind != BossPartKind.Cannon || !Alive || _visual == null)
            {
                return;
            }

            var dir = worldTarget - (Vector2)transform.position;
            if (dir.sqrMagnitude > 1e-6f)
            {
                _visual.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>砲口の先（弾が出る場所）。</summary>
        internal Vector2 Muzzle(Vector2 worldTarget)
        {
            var origin = (Vector2)transform.position;
            var dir = worldTarget - origin;
            return dir.sqrMagnitude > 1e-6f ? origin + dir.normalized * WorldRadius * 1.25f : origin;
        }

        internal void Break(Sprite brokenSprite)
        {
            if (!Alive)
            {
                return;
            }

            Alive = false;
            Health = 0f;
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (_renderer != null)
            {
                _renderer.sprite = brokenSprite;
                _renderer.color = Color.white;
                _visual.rotation = Quaternion.identity;
                _visual.localScale = Vector3.one * WorldRadius * 1.1f;
            }

            UpdateBar();
        }

        internal void FlashHit()
        {
            _hitFlash = HitFlashSeconds;
        }

        private void LateUpdate()
        {
            _hitFlash = Mathf.Max(0f, _hitFlash - Time.deltaTime);
            if (_renderer != null && Alive)
            {
                _renderer.color = _hitFlash > 0f ? new Color(1f, 0.55f, 0.6f, 1f) : Color.white;
            }

            UpdateBar();
        }

        private void UpdateBar()
        {
            if (_barBack == null || _barFill == null)
            {
                return;
            }

            var show = Alive && Owner != null && Owner.ShowsHealthBars;
            _barBack.enabled = _barFill.enabled = show;
            if (!show)
            {
                return;
            }

            var width = WorldRadius * 1.8f;
            var height = Mathf.Max(0.12f, WorldRadius * 0.18f);
            var y = -WorldRadius - height * 1.6f;
            _barBack.transform.localPosition = new Vector3(0f, y, -0.01f);
            _barBack.transform.localScale = new Vector3(width + height * 0.6f, height * 1.6f, 1f);
            var k = Health01;
            _barFill.transform.localPosition = new Vector3(-width * 0.5f * (1f - k), y, -0.02f);
            _barFill.transform.localScale = new Vector3(width * k, height, 1f);
        }

        // 普段のレーザー（砲台の情報付き）。
        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning, LaserTurret attacker)
        {
            var multiplier = Owner != null ? Owner.LaserDamageMultiplier : 1f;
            Owner?.DamagePart(this, baseDamage * multiplier, attacker != null ? attacker.playerIndex : -1);
        }

        // 砲台の情報が無いもの（スキルのビームなど）。
        public void ApplyLaserDamage(float baseDamage, LaserTuningConfig tuning)
        {
            var multiplier = Owner != null ? Owner.SkillDamageMultiplier : 1f;
            Owner?.DamagePart(this, baseDamage * multiplier, -1);
        }
    }
}
