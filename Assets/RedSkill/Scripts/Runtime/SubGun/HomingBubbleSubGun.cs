using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 青の副砲「追尾バブル」: 一定の間隔で、左右から泡の弾が1つずつ出て、いちばん近い敵を追いかける。
    /// 敵・壁・障害物に触れるとはじけ、まわりの敵にダメージ。強く握るほど次の泡までの間隔が短い。
    /// 泡の動きは各PCがそれぞれ計算する（ダメージは結果を決めるPCの泡だけ）。
    /// </summary>
    [Serializable]
    public sealed class HomingBubbleSubGun : SubGunSettings
    {
        [SkillQuick(1, "泡を出す間隔（秒）")]
        [Tooltip("左右1つずつの泡を出す間隔（秒）。")]
        [SkillLabel("出す間隔（秒）")]
        public GripRange interval = new GripRange(1.0f, 0.5f);

        [SkillQuick(2, "はじけたときのダメージ")]
        [Tooltip("泡がはじけたとき、まわりの敵に与えるダメージ（ふつうの敵の体力は 10）。")]
        [SkillLabel("はじけたときのダメージ")]
        [Min(0f)]
        public float damage = 8f;

        [SkillQuick(3, "はじける範囲（m）")]
        [Tooltip("はじけたときにダメージが届く半径（m）。")]
        [SkillLabel("はじける範囲（m）")]
        [Min(0f)]
        public float popRadius = 0.7f;

        [SkillQuick(4, "泡の速さ（m/秒）")]
        [Tooltip("泡の速さ（m/秒）。")]
        [SkillLabel("速さ（m/秒）")]
        [Min(0.1f)]
        public float speed = 8f;

        [Tooltip("泡が向きを変える速さ（度/秒）。大きいほどよく曲がって追いかける。")]
        [SkillLabel("曲がる速さ（度/秒）")]
        [Min(0f)]
        public float turnRate = 320f;

        [Tooltip("追いかける敵を探す範囲（m）。この中にいなければまっすぐ飛ぶ。")]
        [SkillLabel("敵を探す範囲（m）")]
        [Min(0f)]
        public float searchRadius = 9f;

        [Tooltip("出てから追いかけ始めるまでの時間（秒）。この間は左右へふくらむように飛ぶ。")]
        [SkillLabel("追いかけ始めるまで（秒）")]
        [Min(0f)]
        public float homingDelay = 0.15f;

        [Tooltip("泡が消えるまでの時間（秒）。何にも触れなければ、この時間ではじける。")]
        [SkillLabel("消えるまで（秒）")]
        [Min(0.1f)]
        public float lifetime = 2.5f;

        [Tooltip("泡の当たりの大きさ（半径 m）。")]
        [SkillLabel("当たりの大きさ（m）")]
        [Min(0.01f)]
        public float hitRadius = 0.28f;

        [Tooltip("はじけたとき、障害物・壁に与えるダメージ。")]
        [SkillLabel("障害物へのダメージ")]
        [Min(0f)]
        public float obstacleDamage = 2f;

        [Tooltip("砲台の中心から、泡が出る位置までの横の距離（m）。")]
        [SkillLabel("出る位置（横の距離 m）")]
        [Min(0f)]
        public float launchSideOffset = 0.4f;

        [Tooltip("出た直後の向き。0 で本体と同じ向き、1 で真横。")]
        [SkillLabel("出た直後の横向きの強さ")]
        [Range(0f, 1f)]
        public float launchSideways = 0.75f;

        [Tooltip("同時に飛んでいられる泡の数（1台あたり）。")]
        [SkillLabel("同時に飛ぶ数の上限")]
        [Range(2, 24)]
        public int maxBubbles = 12;

        [SkillQuick(5, "泡の見た目の大きさ（m）")]
        [Tooltip("泡の見た目の大きさ（半径 m）。")]
        [SkillLabel("見た目の大きさ（m）")]
        [Min(0.01f)]
        public float visualRadius = 0.36f;

        [Tooltip("泡の後ろに残る点の数（0 で無し）。")]
        [SkillLabel("後ろに残る点の数")]
        [Range(0, 12)]
        public int trailPoints = 8;

        [Tooltip("はじけたときの輪が広がる時間（秒）。")]
        [SkillLabel("はじけた輪の時間（秒）")]
        [Min(0.01f)]
        public float popDuration = 0.35f;

        [Tooltip("泡を出したときの効果音・エフェクト（空なら無し）。")]
        [SkillLabel("出したとき（効果音・エフェクト）")]
        public FortressEffect launchEffect = new FortressEffect();

        [Tooltip("泡がはじけたときの効果音・エフェクト（空なら無し）。")]
        [SkillLabel("はじけたとき（効果音・エフェクト）")]
        public FortressEffect popEffect = new FortressEffect();

        public override SubGunRunner AddRunner(GameObject turretObject) => turretObject.AddComponent<HomingBubbleSubGunRunner>();
    }

    /// <summary>「追尾バブル」を動かす係。</summary>
    public sealed class HomingBubbleSubGunRunner : SubGunRunner
    {
        private const int TrailCapacity = 12;
        private const int MaxTracked = 24;

        private sealed class Bubble
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public bool HasTarget;
            public Vector2 Target;
            public bool Popped;
            public readonly Vector2[] Trail = new Vector2[TrailCapacity];
            public int TrailCount;
            public float TrailTimer;
        }

        private struct Pop
        {
            public Vector2 Position;
            public float Age;
        }

        private readonly List<Bubble> _bubbles = new List<Bubble>(16);
        private readonly Stack<Bubble> _pool = new Stack<Bubble>();
        private readonly List<Pop> _pops = new List<Pop>(16);
        private readonly List<Vector2> _pendingSwarmPops = new List<Vector2>(8);
        private readonly float[] _bestDistance = new float[MaxTracked];
        private float _cooldown;

        private HomingBubbleSubGun Settings => (HomingBubbleSubGun)BaseSettings;

        protected override void Tick(float dt)
        {
            var settings = Settings;
            if (MainBeamOn)
            {
                _cooldown -= dt;
                if (_cooldown <= 0f)
                {
                    _cooldown = Mathf.Max(0.05f, settings.interval.At(Grip01));
                    Launch(settings);
                }
            }
            else
            {
                // 撃ち始めはすぐに1組出るように、待ち時間を短めに戻しておく。
                _cooldown = Mathf.Min(_cooldown, 0.2f);
            }

            Move(settings, dt);
            for (var i = _pops.Count - 1; i >= 0; i--)
            {
                var pop = _pops[i];
                pop.Age += dt;
                if (pop.Age >= settings.popDuration)
                {
                    _pops.RemoveAt(i);
                }
                else
                {
                    _pops[i] = pop;
                }
            }
        }

        private void Launch(HomingBubbleSubGun settings)
        {
            var side = new Vector2(-BeamDirection.y, BeamDirection.x);
            for (var s = -1; s <= 1; s += 2)
            {
                if (_bubbles.Count >= Mathf.Min(settings.maxBubbles, MaxTracked))
                {
                    break;
                }

                var bubble = _pool.Count > 0 ? _pool.Pop() : new Bubble();
                var sideDir = side * s;
                bubble.Position = BeamOrigin + sideDir * settings.launchSideOffset;
                var dir = (BeamDirection * (1f - settings.launchSideways) + sideDir * settings.launchSideways).normalized;
                bubble.Velocity = dir * settings.speed * 0.7f;
                bubble.Age = 0f;
                bubble.HasTarget = false;
                bubble.Popped = false;
                bubble.TrailCount = 0;
                bubble.TrailTimer = 0f;
                _bubbles.Add(bubble);
                // 出た所に小さな光（どこから出たか分かるように）。
                AddFlash(bubble.Position, 0.55f);
            }

            FortressEffectPlayer.PlayOnce(settings.launchEffect, new Vector3(BeamOrigin.x, BeamOrigin.y, GroundZ),
                FortressEffectPlayer.RotationFacing(BeamDirection), Owner);
        }

        private void Move(HomingBubbleSubGun settings, float dt)
        {
            var maxTurn = settings.turnRate * Mathf.Deg2Rad * dt;
            for (var i = _bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = _bubbles[i];
                bubble.Age += dt;
                if (bubble.Age >= settings.lifetime)
                {
                    PopAt(settings, bubble.Position);
                    Recycle(i);
                    continue;
                }

                var speed = Mathf.MoveTowards(bubble.Velocity.magnitude, settings.speed, settings.speed * 3f * dt);
                var heading = bubble.Velocity.sqrMagnitude > 1e-6f ? bubble.Velocity.normalized : BeamDirection;
                if (bubble.HasTarget && bubble.Age >= settings.homingDelay)
                {
                    var want = bubble.Target - bubble.Position;
                    if (want.sqrMagnitude > 1e-6f)
                    {
                        var current = Mathf.Atan2(heading.y, heading.x);
                        var target = Mathf.Atan2(want.y, want.x);
                        var next = current + Mathf.Clamp(Mathf.DeltaAngle(current * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad, -maxTurn, maxTurn);
                        heading = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
                    }
                }

                bubble.Velocity = heading * speed;
                bubble.Position += bubble.Velocity * dt;

                bubble.TrailTimer -= dt;
                if (bubble.TrailTimer <= 0f && settings.trailPoints > 0)
                {
                    bubble.TrailTimer = 0.03f;
                    var max = Mathf.Min(settings.trailPoints, TrailCapacity);
                    if (bubble.TrailCount < max)
                    {
                        bubble.TrailCount++;
                    }

                    for (var t = bubble.TrailCount - 1; t > 0; t--)
                    {
                        bubble.Trail[t] = bubble.Trail[t - 1];
                    }

                    bubble.Trail[0] = bubble.Position;
                }

                // 壁・障害物・Actor の敵・ボスの部位に触れたらはじける（群衆の敵は群衆の配列を読める瞬間に調べる）。
                if (SubGunHits.TouchesSolid(bubble.Position, settings.hitRadius, HitMask, Turret, out _))
                {
                    PopAt(settings, bubble.Position);
                    Recycle(i);
                }
            }
        }

        private void Recycle(int index)
        {
            var bubble = _bubbles[index];
            _bubbles.RemoveAt(index);
            _pool.Push(bubble);
        }

        /// <summary>はじける: 見た目・音・群衆以外へのダメージは今すぐ、群衆の敵へのダメージは配列を読める瞬間に置く。</summary>
        private void PopAt(HomingBubbleSubGun settings, Vector2 position)
        {
            ShowPop(settings, position);
            if (_pendingSwarmPops.Count < 8)
            {
                _pendingSwarmPops.Add(position);
            }

            SubGunHits.DamageObjectsInCircle(position, settings.popRadius, HitMask, Turret, settings.damage, settings.obstacleDamage);
        }

        private void ShowPop(HomingBubbleSubGun settings, Vector2 position)
        {
            _pops.Add(new Pop { Position = position });
            AddFlash(position, 1.2f);
            FortressEffectPlayer.PlayOnce(settings.popEffect, new Vector3(position.x, position.y, GroundZ), Quaternion.identity, Owner);
        }

        protected override void OnSwarmReadable(SwarmSystem swarm)
        {
            var settings = Settings;
            for (var p = 0; p < _pendingSwarmPops.Count; p++)
            {
                SubGunHits.QueueCircle(swarm, _pendingSwarmPops[p], settings.popRadius, settings.damage, Owner);
            }

            _pendingSwarmPops.Clear();
            if (_bubbles.Count == 0)
            {
                return;
            }

            var storage = swarm.Storage;
            var replica = swarm.IsReplica;
            var count = Mathf.Min(_bubbles.Count, MaxTracked);
            var search2 = settings.searchRadius * settings.searchRadius;
            for (var b = 0; b < count; b++)
            {
                _bestDistance[b] = search2;
                _bubbles[b].HasTarget = false;
            }

            for (var i = 0; i < storage.Count; i++)
            {
                if (!SubGunHits.TryGetEnemy(storage, i, replica, out var position))
                {
                    continue;
                }

                var reach = settings.hitRadius + SubGunHits.EnemyRadius(swarm, storage, i);
                for (var b = 0; b < count; b++)
                {
                    var bubble = _bubbles[b];
                    if (bubble.Popped)
                    {
                        continue;
                    }

                    var d2 = (position - bubble.Position).sqrMagnitude;
                    if (d2 < _bestDistance[b])
                    {
                        _bestDistance[b] = d2;
                        bubble.HasTarget = true;
                        bubble.Target = position;
                    }

                    if (d2 <= reach * reach)
                    {
                        // 触れた: この場ではじけて、まわりにダメージ（同じフレームの判定に間に合う）。
                        bubble.Popped = true;
                        SubGunHits.QueueCircle(swarm, bubble.Position, settings.popRadius, settings.damage, Owner);
                        SubGunHits.DamageObjectsInCircle(bubble.Position, settings.popRadius, HitMask, Turret, settings.damage, settings.obstacleDamage);
                        ShowPop(settings, bubble.Position);
                    }
                }
            }

            for (var b = _bubbles.Count - 1; b >= 0; b--)
            {
                if (_bubbles[b].Popped)
                {
                    Recycle(b);
                }
            }
        }

        protected override void Draw(SubGunLayer back, SubGunLayer front)
        {
            var settings = Settings;
            var r = settings.visualRadius;
            for (var i = 0; i < _bubbles.Count; i++)
            {
                var bubble = _bubbles[i];
                var grow = Mathf.Clamp01(bubble.Age / 0.08f);
                var fadeOut = Mathf.Clamp01((settings.lifetime - bubble.Age) / 0.2f);
                var alpha = grow * fadeOut;
                for (var t = 0; t < bubble.TrailCount; t++)
                {
                    var k = 1f - (t + 1f) / (bubble.TrailCount + 1f);
                    front.Disc(bubble.Trail[t], r * 0.35f * k + 0.02f, settings.ColorFor(Owner, alpha * 0.6f * k), 8);
                }

                var radius = r * Mathf.Lerp(0.4f, 1f, grow);
                // 中を塗った泡＋はっきりした縁＋光の点。
                OutlinedDisc(front, bubble.Position, radius, settings.ColorFor(Owner, alpha * 0.55f), 18);
                front.Ring(bubble.Position, radius * 0.86f, r * 0.24f, settings.ColorFor(Owner, alpha), 22);
                front.Disc(bubble.Position + new Vector2(-0.32f, 0.32f) * radius, radius * 0.24f, new Color(1f, 1f, 1f, alpha * settings.opacity), 10);
            }

            for (var i = 0; i < _pops.Count; i++)
            {
                var pop = _pops[i];
                var p = Mathf.Clamp01(pop.Age / settings.popDuration);
                var ease = 1f - (1f - p) * (1f - p);
                var radius = Mathf.Lerp(r, settings.popRadius, ease);
                // 前半ははっきり、後半はうすく（止めのある2段）。
                var alpha = p < 0.5f ? 1f : 0.5f;
                OutlinedRing(front, pop.Position, radius, r * 0.3f * (1f - p) + 0.03f, settings.ColorFor(Owner, alpha), 26);
                // 飛び散るしずく。
                for (var k = 0; k < 6; k++)
                {
                    var angle = k * Mathf.PI / 3f + pop.Position.x;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    front.Disc(pop.Position + dir * radius * 1.15f, r * 0.16f * (1f - p) + 0.015f, settings.ColorFor(Owner, alpha), 8);
                }
            }
        }
    }
}
