using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 緑の副砲「置き種」: 本体のレーザーが通った所に小さな種を落とし、少しあとで芽が開いて、まわりの敵をはじく。
    /// 砲台は回りながら撃つので、通った跡に時間差の攻撃が残る。強く握るほど、芽が開く輪が大きい。
    /// スキルの葉っぱと見分けられるよう、種は丸（葉の形は開いた芽にだけ使う）。
    /// 開く直前は種がふくらんで光り（予告）、開いた瞬間は輪と葉が一気に広がる。
    /// </summary>
    [Serializable]
    public sealed class SeedSubGun : SubGunSettings
    {
        [SkillQuick(1, "種を落とす間隔（秒）")]
        [Tooltip("種を1つ落とす間隔（秒）。")]
        [SkillLabel("落とす間隔（秒）")]
        [Min(0.02f)]
        public float dropInterval = 0.35f;

        [SkillQuick(2, "芽が開くまで（秒）")]
        [Tooltip("種を落としてから芽が開く（ダメージが出る）までの時間（秒）。")]
        [SkillLabel("芽が開くまで（秒）")]
        [Min(0f)]
        public float sproutDelay = 0.75f;

        [SkillQuick(3, "芽の輪の大きさ（m）")]
        [Tooltip("芽が開いたときにダメージが届く半径（m）。落としたときの握る強さで決まる。")]
        [SkillLabel("芽の輪の大きさ（半径 m）")]
        public GripRange radius = new GripRange(0.8f, 1.3f);

        [SkillQuick(4, "芽のダメージ")]
        [Tooltip("芽が開いたとき、輪の中の敵に与えるダメージ（ふつうの敵の体力は 10）。")]
        [SkillLabel("ダメージ")]
        [Min(0f)]
        public float damage = 8f;

        [Tooltip("芽が開いたとき、輪の中の障害物・壁に与えるダメージ。")]
        [SkillLabel("障害物へのダメージ")]
        [Min(0f)]
        public float obstacleDamage = 3f;

        [Tooltip("根元から、種を落とし始める距離（m）。砲台のすぐそばには落とさない。")]
        [SkillLabel("落とし始める距離（m）")]
        [Min(0f)]
        public float minDistance = 1f;

        [Tooltip("同時に置いておける種の数（1台あたり）。")]
        [SkillLabel("同時に置ける数の上限")]
        [Range(4, 48)]
        public int maxSeeds = 24;

        [SkillQuick(5, "種の大きさ（m）")]
        [Tooltip("種の見た目の大きさ（半径 m）。")]
        [SkillLabel("種の大きさ（m）")]
        [Min(0.01f)]
        public float seedRadius = 0.26f;

        [Tooltip("開く直前の予告（種がふくらんで光る）を始めるのは、開く何秒前か。")]
        [SkillLabel("予告の長さ（秒）")]
        [Min(0f)]
        public float warnSeconds = 0.3f;

        [Tooltip("芽が開く演出の時間（秒）。")]
        [SkillLabel("芽が開く演出の時間（秒）")]
        [Min(0.05f)]
        public float sproutDuration = 0.6f;

        [Tooltip("開いた芽の葉の数。")]
        [SkillLabel("葉の数")]
        [Range(3, 8)]
        public int leafCount = 6;

        [Tooltip("開いた芽の葉の長さ（m）。")]
        [SkillLabel("葉の長さ（m）")]
        [Min(0.01f)]
        public float leafLength = 0.6f;

        [Tooltip("芽が開いたときの輪の線の太さ（m）。")]
        [SkillLabel("輪の線の太さ（m）")]
        [Min(0f)]
        public float ringWidth = 0.16f;

        [Tooltip("芽が開いた瞬間、輪の中をうすく塗る濃さ（ダメージの届く範囲が一目で分かる）。0 で塗らない。")]
        [SkillLabel("開いた瞬間の塗りの濃さ")]
        [Range(0f, 1f)]
        public float burstAlpha = 0.4f;

        [Tooltip("種が落ちたときに、大きく見えてから元の大きさへ縮むまでの時間（秒）。どこに落ちたか分かるようにする。")]
        [SkillLabel("落ちたときの縮む時間（秒）")]
        [Min(0f)]
        public float landSeconds = 0.15f;

        [Tooltip("芽が開いたときの効果音・エフェクト（空なら無し）。")]
        [SkillLabel("芽が開いたとき（効果音・エフェクト）")]
        public FortressEffect sproutEffect = new FortressEffect();

        public override SubGunRunner AddRunner(GameObject turretObject) => turretObject.AddComponent<SeedSubGunRunner>();
    }

    /// <summary>「置き種」を動かす係。</summary>
    public sealed class SeedSubGunRunner : SubGunRunner
    {
        private struct Seed
        {
            public Vector2 Position;
            public float Age;
            public float Radius;
            public float Spin;
        }

        private readonly List<Seed> _seeds = new List<Seed>(32);
        private readonly List<Seed> _sprouts = new List<Seed>(16);
        private readonly List<Seed> _pendingSwarm = new List<Seed>(8);
        private float _cooldown;

        private SeedSubGun Settings => (SeedSubGun)BaseSettings;

        protected override void Tick(float dt)
        {
            var settings = Settings;
            if (MainBeamOn && BeamLength > settings.minDistance)
            {
                _cooldown -= dt;
                if (_cooldown <= 0f)
                {
                    _cooldown = settings.dropInterval;
                    Drop(settings);
                }
            }

            for (var i = _seeds.Count - 1; i >= 0; i--)
            {
                var seed = _seeds[i];
                seed.Age += dt;
                if (seed.Age >= settings.sproutDelay)
                {
                    _seeds.RemoveAt(i);
                    SproutAt(settings, seed);
                    continue;
                }

                _seeds[i] = seed;
            }

            for (var i = _sprouts.Count - 1; i >= 0; i--)
            {
                var sprout = _sprouts[i];
                sprout.Age += dt;
                if (sprout.Age >= settings.sproutDuration)
                {
                    _sprouts.RemoveAt(i);
                }
                else
                {
                    _sprouts[i] = sprout;
                }
            }
        }

        private void Drop(SeedSubGun settings)
        {
            if (_seeds.Count >= settings.maxSeeds)
            {
                return;
            }

            var distance = UnityEngine.Random.Range(settings.minDistance, BeamLength);
            var side = new Vector2(-BeamDirection.y, BeamDirection.x) * (UnityEngine.Random.Range(-1f, 1f) * BeamHalfWidth);
            _seeds.Add(new Seed
            {
                Position = BeamOrigin + BeamDirection * distance + side,
                Radius = settings.radius.At(Grip01),
                Spin = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            });
        }

        private void SproutAt(SeedSubGun settings, Seed seed)
        {
            seed.Age = 0f;
            _sprouts.Add(seed);
            if (_pendingSwarm.Count < 8)
            {
                _pendingSwarm.Add(seed);
            }

            AddFlash(seed.Position, 1.8f);
            SubGunHits.DamageObjectsInCircle(seed.Position, seed.Radius, HitMask, Turret, settings.damage, settings.obstacleDamage);
            FortressEffectPlayer.PlayOnce(settings.sproutEffect, new Vector3(seed.Position.x, seed.Position.y, GroundZ), Quaternion.identity, Owner);
        }

        protected override void OnSwarmReadable(SwarmSystem swarm)
        {
            var settings = Settings;
            for (var i = 0; i < _pendingSwarm.Count; i++)
            {
                SubGunHits.QueueCircle(swarm, _pendingSwarm[i].Position, _pendingSwarm[i].Radius, settings.damage, Owner);
            }

            _pendingSwarm.Clear();
        }

        protected override void Draw(SubGunLayer back, SubGunLayer front)
        {
            var settings = Settings;
            var white = new Color(1f, 1f, 1f, settings.opacity);
            for (var i = 0; i < _seeds.Count; i++)
            {
                var seed = _seeds[i];
                var untilSprout = settings.sproutDelay - seed.Age;
                var warn = settings.warnSeconds > 0f ? Mathf.Clamp01(1f - untilSprout / settings.warnSeconds) : 0f;
                // 予告: 輪が開く範囲を見せ、種がふくらんで白く光る（カクッと2段で）。
                if (warn > 0f)
                {
                    OutlinedRing(front, seed.Position, seed.Radius, 0.05f, settings.ColorFor(Owner, warn > 0.5f ? 0.8f : 0.45f), 30);
                }

                // 落ちた瞬間は大きく見せ、すぐ元の大きさへ（どこに落ちたか分かるように）。
                var land = settings.landSeconds > 0f ? Mathf.Clamp01(seed.Age / settings.landSeconds) : 1f;
                var drop = Mathf.Lerp(1.8f, 1f, 1f - (1f - land) * (1f - land));
                var swell = warn > 0.5f ? 1.4f : warn > 0f ? 1.2f : 1f;
                var r = settings.seedRadius * drop * swell;
                var fill = warn > 0.5f ? Color.Lerp(settings.ColorFor(Owner), white, 0.6f) : settings.ColorFor(Owner);
                // 暗い縁 → 白い縁 → キャラの色 → 光の点。床がどんな色でも丸が読めるようにする。
                OutlinedDisc(front, seed.Position, r, white, 16);
                front.Disc(seed.Position, r * 0.78f, fill, 16);
                front.Disc(seed.Position + new Vector2(-0.3f, 0.3f) * r, r * 0.25f, white, 8);
            }

            for (var i = 0; i < _sprouts.Count; i++)
            {
                var sprout = _sprouts[i];
                var p = Mathf.Clamp01(sprout.Age / settings.sproutDuration);
                var g = Mathf.Clamp01(p / 0.4f);
                var grow = 1f - (1f - g) * (1f - g);
                var reach = sprout.Radius * grow;
                // 前半ははっきり、後半はうすく（止めのある2段）。
                var first = p < 0.5f;
                var alpha = first ? 1f : 0.5f;
                if (first && settings.burstAlpha > 0f)
                {
                    back.Disc(sprout.Position, reach, settings.ColorFor(Owner, settings.burstAlpha), 32);
                }

                OutlinedRing(front, sprout.Position, reach, settings.ringWidth * (first ? 1f : 0.5f), settings.ColorFor(Owner, alpha), 32);
                front.Ring(sprout.Position, reach, settings.ringWidth * 0.3f, new Color(1f, 1f, 1f, alpha * settings.opacity), 32);
                for (var k = 0; k < settings.leafCount; k++)
                {
                    var angle = sprout.Spin + k * Mathf.PI * 2f / settings.leafCount + p * 0.8f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    OutlinedLeaf(front, sprout.Position + dir * (reach * 0.72f), dir, settings.leafLength, settings.leafLength * 0.45f, settings.ColorFor(Owner, alpha));
                }
            }
        }
    }
}
