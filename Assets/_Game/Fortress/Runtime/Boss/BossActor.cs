using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボス本体（<see cref="BossWaveRunner"/> がボスウェーブで作る。2026-11-15）。
    /// 右から入ってきて止まり、少しずつ左（コア）へ近づく。上下にゆれる。部位（<see cref="BossPart"/>）を壊すと少し押し戻される。
    /// 本体は無敵（レーザーは体で止まる）。砲口はコアへ弾を撃ち、菌を出す口は雑魚を呼ぶ。
    /// 動き・攻撃・ダメージを決めるのは Host（オフラインでは自分）。Client への同期は後の段階（今は Host と同じ場所に置くだけ）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossActor : MonoBehaviour
    {
        private enum Mode
        {
            Hidden,
            Entering,
            Ready,
            Fighting,
            Finale,
            Reached,
            Dying,
            Gone
        }

        private readonly List<BossPart> _parts = new List<BossPart>();

        private BossDefinition _def;
        private BossFormationLayout _layout;
        private Transform _visual;
        private SpriteRenderer _body;
        private CircleCollider2D _bodyCollider;
        private CoreCrystalController _core;
        private float _coreRadius;
        private float _z;
        private float _bulletZ;
        private int _layer;
        private Mode _mode;
        private float _modeTime;
        private float _x;
        private float _y;
        private float _push;
        private float _bobTime;
        private float _speed;
        private float _scale = 1f;
        private float _alpha = 1f;
        private bool _authority;
        private bool _reachDamageDone;
        private EnemyTypeDefinition _minionType;

        public float LaserDamageMultiplier => _def != null ? _def.laserDamageMultiplier : 1f;

        public float SkillDamageMultiplier => _def != null ? _def.skillDamageMultiplier : 1f;

        /// <summary>部位の HP のバーを出すか（戦っている間だけ）。</summary>
        public bool ShowsHealthBars => _mode == Mode.Fighting || _mode == Mode.Ready;

        public IReadOnlyList<BossPart> Parts => _parts;

        /// <summary>まだ画面の外で待っている（WARNING の間）。</summary>
        public bool IsHidden => _mode == Mode.Hidden;

        public Vector2 Position => new Vector2(_x, _y);

        public float Radius => _layout.bossRadius;

        public static BossActor Create(BossDefinition def, BossFormationLayout layout, float z, float bulletZ, int layer, EnemyTypeDefinition minionType)
        {
            var go = new GameObject($"[Boss] {def.displayName}") { layer = layer };
            var actor = go.AddComponent<BossActor>();
            actor.Setup(def, layout, z, bulletZ, layer, minionType);
            return actor;
        }

        private void Setup(BossDefinition def, BossFormationLayout layout, float z, float bulletZ, int layer, EnemyTypeDefinition minionType)
        {
            _def = def;
            _layout = layout;
            _z = z;
            _bulletZ = bulletZ;
            _layer = layer;
            _minionType = minionType;
            _x = layout.bossEntry.x;
            _y = layout.bossEntry.y;
            _speed = BossFormationMath.ApproachSpeed(layout, def.approachSeconds);
            _core = FindFirstObjectByType<CoreCrystalController>();
            _coreRadius = EstimateCoreRadius(_core);

            var radius = layout.bossRadius;
            _bodyCollider = gameObject.AddComponent<CircleCollider2D>();
            _bodyCollider.radius = radius * 0.93f;
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            _visual = new GameObject("Body").transform;
            _visual.SetParent(transform, false);
            _visual.localPosition = new Vector3(0f, 0f, 0.02f);
            _body = _visual.gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = def.bodySprite != null ? def.bodySprite : BossArt.Body;
            _body.sortingOrder = 1;
            _visual.localScale = Vector3.one * radius * def.bodySpriteScale;

            var parts = def.parts ?? new List<BossPartDefinition>();
            for (var i = 0; i < parts.Count; i++)
            {
                var partDef = parts[i];
                if (partDef == null)
                {
                    continue;
                }

                var go = new GameObject($"Part{i} {partDef.label}");
                go.transform.SetParent(transform, false);
                var part = go.AddComponent<BossPart>();
                var sprite = partDef.kind == BossPartKind.Cannon
                    ? (def.cannonSprite != null ? def.cannonSprite : BossArt.Cannon)
                    : (def.ventSprite != null ? def.ventSprite : BossArt.Vent);
                part.Setup(this, _parts.Count, partDef, radius, sprite, layer);
                _parts.Add(part);
            }

            FortressBoss.PartCount = _parts.Count;
            FortressBoss.PartsLeft = _parts.Count;
            FortressBoss.AliveMask = _parts.Count >= 31 ? ~0 : (1 << _parts.Count) - 1;
            SetMode(Mode.Hidden);
            ApplyTransform();
        }

        private static float EstimateCoreRadius(CoreCrystalController core)
        {
            if (core == null)
            {
                return 1.5f;
            }

            var collider = core.GetComponentInChildren<Collider2D>();
            if (collider != null)
            {
                var e = collider.bounds.extents;
                return Mathf.Max(0.6f, Mathf.Min(e.x, e.y));
            }

            return 1.5f;
        }

        // ───────── 段階（ボスの係から呼ぶ） ─────────

        public void BeginEnter() => SetMode(Mode.Entering);

        public void BeginFight(bool authority)
        {
            _authority = authority;
            if (_mode == Mode.Hidden || _mode == Mode.Entering)
            {
                _x = _layout.bossStop.x;
                _y = _layout.bossStop.y;
            }

            SetMode(Mode.Fighting);
        }

        public void BeginFinale(bool authority)
        {
            _authority = authority;
            BossBullet.PopAll();
            SetMode(Mode.Finale);
        }

        public void BeginReached(bool authority)
        {
            _authority = authority;
            _reachDamageDone = false;
            BossBullet.PopAll();
            SetMode(Mode.Reached);
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            _modeTime = 0f;
            FortressBoss.IsVisible = mode != Mode.Hidden && mode != Mode.Gone;
        }

        // ───────── 毎フレーム ─────────

        private void Update()
        {
            var dt = Time.deltaTime;
            _modeTime += dt;
            switch (_mode)
            {
                case Mode.Entering:
                    TickEnter();
                    break;
                case Mode.Fighting:
                    TickFight(dt);
                    break;
                case Mode.Finale:
                    TickFinale();
                    break;
                case Mode.Reached:
                    TickReached(dt);
                    break;
                case Mode.Dying:
                    TickDying();
                    break;
            }

            FortressBoss.Approach01 = BossFormationMath.Approach01(_layout, _x);
            ApplyTransform();
        }

        private void TickEnter()
        {
            var seconds = Mathf.Max(0.2f, _def.enterSeconds);
            var p = Mathf.Clamp01(_modeTime / seconds);
            _x = Mathf.Lerp(_layout.bossEntry.x, _layout.bossStop.x, BossFormationMath.EaseOut(p));
            _y = _layout.bossStop.y;
            if (p >= 1f)
            {
                Shake(_def.enterShake, 1f);
                Play(_def.onEnter, transform.position);
                BossPuff.Spawn(new Vector3(_x - Radius * 0.6f, _y, _z), Radius * 0.5f, Radius * 2.2f, 0.5f, new Color(0.85f, 0.6f, 1f, 0.6f));
                SetMode(Mode.Ready);
            }
        }

        private void TickFight(float dt)
        {
            if (!_authority)
            {
                // Client: 今は Host と同じ動きを自分で計算しない（同期は後の段階）。ゆれだけ見せる。
                _bobTime += dt;
                _y = _layout.bossStop.y + Mathf.Sin(_bobTime * Mathf.PI * 2f / Mathf.Max(1f, _def.bobPeriodSeconds)) * _layout.bobAmplitude;
                return;
            }

            _x -= _speed * dt;
            if (_push > 0f)
            {
                var step = Mathf.Min(_push, Radius * 3f * dt);
                _x += step;
                _push -= step;
            }

            _x = Mathf.Min(_x, _layout.bossStop.x + Radius * 0.5f);
            _bobTime += dt;
            _y = _layout.bossStop.y + Mathf.Sin(_bobTime * Mathf.PI * 2f / Mathf.Max(1f, _def.bobPeriodSeconds)) * _layout.bobAmplitude;
            ApplyTransform();
            TickParts(dt);

            if (_x <= _layout.reachCenterX && !FortressBoss.ReachedCore)
            {
                FortressBoss.ReachedCore = true;
            }
        }

        private void TickParts(float dt)
        {
            var target = _core != null ? (Vector2)_core.transform.position : _layout.core;
            foreach (var part in _parts)
            {
                if (part == null || !part.Alive)
                {
                    continue;
                }

                part.Aim(target);
                part.Cooldown -= dt;
                if (part.Cooldown > 0f)
                {
                    continue;
                }

                part.Cooldown = part.Definition.RandomInterval();
                if (part.Definition.kind == BossPartKind.Cannon)
                {
                    FireBullet(part, target);
                }
                else
                {
                    SpawnMinions(part);
                }
            }
        }

        private void FireBullet(BossPart part, Vector2 target)
        {
            var muzzle = part.Muzzle(target);
            var toCore = target - muzzle;
            var distance = toCore.magnitude;
            if (distance < 1e-3f)
            {
                return;
            }

            var velocity = toCore / distance * (distance / Mathf.Max(0.5f, part.Definition.bulletTravelSeconds));
            BossBullet.Spawn(_def, part.Definition, muzzle, velocity, _bulletZ, _layer, _core, _coreRadius);
            Play(_def.onCannonFire, new Vector3(muzzle.x, muzzle.y, _bulletZ));
            BossPuff.Spawn(new Vector3(muzzle.x, muzzle.y, _bulletZ), part.WorldRadius * 0.3f, part.WorldRadius * 1.1f, 0.2f, new Color(1f, 0.55f, 0.75f, 0.8f));
        }

        private void SpawnMinions(BossPart part)
        {
            var swarm = SwarmSystem.Current;
            if (swarm == null || _minionType == null)
            {
                return;
            }

            var origin = (Vector2)part.transform.position + Vector2.left * part.WorldRadius;
            for (var i = 0; i < part.Definition.spawnCount; i++)
            {
                swarm.Spawn(_minionType, origin + Random.insideUnitCircle * part.Definition.spawnSpread);
            }

            Play(_def.onVentSpawn, part.transform.position);
            BossPuff.Spawn(part.transform.position, part.WorldRadius * 0.6f, part.WorldRadius * 1.8f, 0.3f, new Color(0.92f, 0.45f, 0.7f, 0.7f));
        }

        // 「全員で握れ！」の間: 目を回して、ふるえて待つ（爆発はとどめの係が呼ぶ）。
        private void TickFinale()
        {
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_modeTime * 40f) * 3f * Mathf.Clamp01(_modeTime / 0.4f));
        }

        /// <summary>
        /// 合体ビームが当たった瞬間の大爆発（とどめの係が全員のPCで呼ぶ）。閃光 → 爆炎 → 黒煙 → 収まる（桜井さんの原則: 爆発は段階を踏む）。
        /// 止め・スローの間はゲームの時間で動くので、一緒に止まる・ゆっくりになる。
        /// </summary>
        public void Explode(float shakeStrength)
        {
            if (_mode == Mode.Dying || _mode == Mode.Gone)
            {
                return;
            }

            if (shakeStrength > 0f)
            {
                Shake(_def.defeatShake, shakeStrength);
            }

            Play(_def.onDefeat, transform.position);
            var r = Radius;
            // 閃光
            BossPuff.Spawn(new Vector3(_x, _y, _z), r * 0.5f, r * 5f, 0.9f, new Color(1f, 1f, 1f, 0.95f));
            // 爆炎（4人の色も混ぜる）
            for (var i = 0; i < 16; i++)
            {
                var offset = Random.insideUnitCircle * r;
                var color = i % 3 == 0 ? new Color(1f, 0.85f, 0.45f, 0.95f) : i % 3 == 1 ? new Color(1f, 0.55f, 0.25f, 0.9f) : FortressColors.PlayerColor(i % 4);
                BossPuff.Spawn(new Vector3(_x + offset.x, _y + offset.y, _z), r * 0.2f, r * Random.Range(0.8f, 1.8f), Random.Range(0.4f, 0.9f), color, offset * 1.5f);
            }

            // 黒煙（少し遅れて湧き、ふくらみながら薄くなって収まる）
            for (var i = 0; i < 20; i++)
            {
                var offset = Random.insideUnitCircle * r * 0.9f;
                var drift = offset.normalized * Random.Range(0.3f, 1.2f) * r * 0.4f + Vector2.up * r * 0.1f;
                BossPuff.Smoke(new Vector3(_x + offset.x, _y + offset.y, _z), r * 0.25f, r * Random.Range(0.5f, 0.9f), Random.Range(1.2f, 1.9f), new Color(0.17f, 0.14f, 0.19f, 0.6f), drift, 0.12f);
            }

            FortressBoss.RaiseDefeated();
            SetMode(Mode.Dying);
        }

        private void TickDying()
        {
            var p = Mathf.Clamp01(_modeTime / 0.6f);
            _scale = 1f - p;
            _alpha = 1f - p;
            if (p >= 1f)
            {
                SetMode(Mode.Gone);
                gameObject.SetActive(false);
            }
        }

        private void TickReached(float dt)
        {
            if (!_reachDamageDone)
            {
                _reachDamageDone = true;
                if (_authority && _core != null)
                {
                    _core.TakeDamage(_core.maxHealth * _def.reachCoreDamage01 / Mathf.Max(0.01f, _core.damageMultiplier));
                }

                Shake(_def.reachShake, 1f);
                Play(_def.onReachCore, _core != null ? _core.transform.position : transform.position);
                if (_core != null)
                {
                    BossPuff.Spawn(_core.transform.position, _coreRadius, _coreRadius * 4f, 0.6f, new Color(1f, 0.25f, 0.2f, 0.9f));
                }

                FortressBoss.RaiseReached();
            }

            // かみつく（前へ突き出して戻る）→ 少し待って右へ去る。
            var bite = Mathf.Clamp01(_modeTime / 0.45f);
            var lunge = Mathf.Sin(bite * Mathf.PI) * Radius * 0.35f;
            var leave = Mathf.Clamp01((_modeTime - 1.1f) / 1.4f);
            _x = _layout.reachCenterX - lunge + leave * leave * (_layout.bossEntry.x - _layout.reachCenterX);
            _alpha = 1f - leave;
            if (leave >= 1f)
            {
                SetMode(Mode.Gone);
                gameObject.SetActive(false);
            }
        }

        // ───────── 部位 ─────────

        internal void DamagePart(BossPart part, float amount, int player)
        {
            if (!FortressNet.HasSimulationAuthority || part == null || !part.Alive || _mode != Mode.Fighting || amount <= 0f)
            {
                return;
            }

            part.Health -= amount;
            part.FlashHit();
            if (part.Health <= 0f)
            {
                BreakPart(part, player);
            }
        }

        private void BreakPart(BossPart part, int player)
        {
            part.Break(_def.brokenPartSprite != null ? _def.brokenPartSprite : BossArt.Broken);
            FortressBoss.AliveMask &= ~(1 << part.Index);
            FortressBoss.PartsLeft = Mathf.Max(0, FortressBoss.PartsLeft - 1);
            _push += _speed * _def.pushBackSeconds;
            Shake(_def.partBreakShake, 1f);
            if (_def.onPartBreak != null && !_def.onPartBreak.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(_def.onPartBreak, part.transform.position, Quaternion.identity, player);
            }
            var color = player >= 0 ? FortressColors.PlayerColor(player) : new Color(1f, 0.85f, 0.5f, 1f);
            for (var i = 0; i < 5; i++)
            {
                var offset = Random.insideUnitCircle * part.WorldRadius;
                BossPuff.Spawn(part.transform.position + (Vector3)offset, part.WorldRadius * 0.3f, part.WorldRadius * Random.Range(1.4f, 2.6f), Random.Range(0.35f, 0.6f), i % 2 == 0 ? color : new Color(1f, 0.85f, 0.5f, 0.9f), offset * 2f);
            }

            FortressBoss.RaisePartBroken(part.Index, player);
        }

        // ───────── そのほか ─────────

        private void ApplyTransform()
        {
            transform.position = new Vector3(_x, _y, _z);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, _scale);
            if (_body != null)
            {
                // ぷるぷる（ゆっくり息をするように少しふくらむ）。
                var breathe = 1f + 0.025f * Mathf.Sin(Time.time * 2.4f);
                _visual.localScale = new Vector3(breathe, 2f - breathe, 1f) * (_layout.bossRadius * _def.bodySpriteScale);
                _body.color = new Color(1f, 1f, 1f, _alpha);
            }
        }

        private static void Shake(SimpleShakeSettings settings, float strength)
        {
            var rig = FortressCameraRig.Active;
            if (settings != null && rig != null)
            {
                rig.AddModifier(new SimpleShakeModifier(settings, strength));
            }
        }

        private static void Play(FortressEffect effect, Vector3 position)
        {
            if (effect != null && !effect.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(effect, position, Quaternion.identity);
            }
        }
    }
}
