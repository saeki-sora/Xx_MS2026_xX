using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 衛星から撃つ錠剤の弾。狙った敵を追いかけて曲がりながら飛び（狙った敵が先に倒れたら最後に見た場所へ）、着いたらパッと弾ける。
    /// ダメージ: 群衆の敵は着いた場所に短い当たり判定（<see cref="SwarmSystem.QueueBeam"/>）を1フレームだけ置く（まわりの敵も巻き込む）。
    /// Client では群衆側がダメージを 0 にして被弾フラッシュだけ出す。Actor の敵へのダメージは結果を決めるPCだけ。
    /// 倒した敵は緑のプレイヤーの手柄になる（当たり判定の持ち主＝緑の番号）。
    /// </summary>
    internal sealed class GreenTabletVolley
    {
        private const int PoolSize = 64;

        private sealed class Bullet
        {
            public bool Active;
            public SpriteRenderer Sprite;
            public TrailRenderer Trail;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 TargetPosition;
            public GreenTargetScanner.Target Target;
            public float Age;
            public float Spin;
        }

        private readonly GreenMintFx _fx;
        private readonly GreenTargetScanner _scanner;
        private readonly Bullet[] _bullets = new Bullet[PoolSize];
        private float _nextHitSound;

        public GreenTabletVolley(GreenMintFx fx, GreenTargetScanner scanner)
        {
            _fx = fx;
            _scanner = scanner;
            var order = fx.Definition.satellites.sortingOrder + 2;
            var gradient = GreenMintSatellites.TrailGradient(fx.Definition.palette);
            for (var i = 0; i < PoolSize; i++)
            {
                var bullet = new Bullet { Sprite = fx.CreateSprite("Tablet" + i, fx.Art.Tablet, order) };
                var trailObject = new GameObject("TabletTrail" + i) { hideFlags = HideFlags.DontSave };
                trailObject.transform.SetParent(fx.Root, false);
                var trail = trailObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = fx.TrailMaterial;
                trail.minVertexDistance = 0.06f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.colorGradient = gradient;
                trail.sortingOrder = order - 1;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                bullet.Trail = trail;
                _bullets[i] = bullet;
            }
        }

        /// <summary>飛んでいる弾の数。</summary>
        public int ActiveCount { get; private set; }

        /// <summary>弾を1発撃つ。空きが無ければ false（そのときは予約を戻すこと）。</summary>
        public bool Fire(Vector2 from, Vector2 outward, in GreenTargetScanner.Target target)
        {
            var bullet = FindFree();
            if (bullet == null)
            {
                return false;
            }

            var attack = _fx.Definition.attack;
            var toTarget = target.Position - from;
            var dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : outward;
            // 最初は外向きに少しふくらませて、弧を描いて敵へ向かう。
            if (outward.sqrMagnitude > 0.0001f)
            {
                dir = (dir + outward.normalized * 0.8f).normalized;
                dir = GreenMintUtil.Rotate(dir, Random.Range(-25f, 25f));
            }

            bullet.Active = true;
            bullet.Position = from;
            bullet.Velocity = dir * attack.bulletSpeed * 0.75f;
            bullet.Target = target;
            bullet.TargetPosition = target.Position;
            bullet.Age = 0f;
            bullet.Spin = Random.Range(0f, 360f);
            var shots = _fx.Definition.shots;
            var z = GreenMintFx.ZOffset - 0.05f;
            bullet.Trail.transform.position = new Vector3(from.x, from.y, z + 0.005f);
            bullet.Trail.Clear();
            bullet.Trail.time = shots.trailSeconds;
            bullet.Trail.widthMultiplier = shots.bulletSize * shots.trailWidthScale;
            bullet.Trail.emitting = shots.trailSeconds > 0f;
            Draw(bullet, z);
            ActiveCount++;
            return true;
        }

        /// <summary>弾を動かし、着いた弾を弾けさせる。</summary>
        public void Tick(LaserTurret owner, float dt)
        {
            if (ActiveCount == 0)
            {
                return;
            }

            var attack = _fx.Definition.attack;
            var impacts = 0;
            var z = GreenMintFx.ZOffset - 0.05f;
            for (var i = 0; i < _bullets.Length; i++)
            {
                var bullet = _bullets[i];
                if (!bullet.Active)
                {
                    continue;
                }

                bullet.Age += dt;
                if (_scanner.TryGetPosition(bullet.Target, out var latest))
                {
                    bullet.TargetPosition = latest;
                }

                // 狙いへ向けて曲がる（曲がる速さには上限）。だんだん速くなる。
                var speed = attack.bulletSpeed * Mathf.Lerp(0.75f, 1.15f, Mathf.Clamp01(bullet.Age / 0.35f));
                var toTarget = bullet.TargetPosition - bullet.Position;
                var distance = toTarget.magnitude;
                var current = bullet.Velocity.sqrMagnitude > 0.0001f ? bullet.Velocity.normalized : toTarget.normalized;
                if (distance > 0.0001f)
                {
                    var desired = toTarget / distance;
                    var maxTurn = attack.bulletTurnRate * dt * (1f + bullet.Age * 3f);
                    var angle = Vector2.SignedAngle(current, desired);
                    current = GreenMintUtil.Rotate(current, Mathf.Clamp(angle, -maxTurn, maxTurn));
                }

                bullet.Velocity = current * speed;
                var step = speed * dt;
                var arrived = distance <= Mathf.Max(step, 0.3f);
                var expired = bullet.Age >= attack.bulletLifetime;
                if ((arrived || expired) && impacts < attack.maxImpactsPerFrame)
                {
                    impacts++;
                    var point = arrived ? bullet.TargetPosition : bullet.Position;
                    Impact(owner, bullet, point, current);
                    continue;
                }

                bullet.Position += bullet.Velocity * dt;
                bullet.Spin += _fx.Definition.shots.bulletSpin * dt;
                Draw(bullet, z);
            }
        }

        /// <summary>全部の弾を消す（予約も戻す）。</summary>
        public void Clear()
        {
            for (var i = 0; i < _bullets.Length; i++)
            {
                var bullet = _bullets[i];
                if (bullet.Active)
                {
                    _scanner.Release(bullet.Target);
                }

                bullet.Active = false;
                bullet.Sprite.enabled = false;
                bullet.Trail.emitting = false;
                bullet.Trail.Clear();
            }

            ActiveCount = 0;
        }

        private void Impact(LaserTurret owner, Bullet bullet, Vector2 point, Vector2 direction)
        {
            bullet.Active = false;
            bullet.Sprite.enabled = false;
            bullet.Trail.emitting = false;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            _scanner.Release(bullet.Target);

            ApplyDamage(owner, bullet.Target, point, direction);
            PlayImpactFx(point, direction, owner);
        }

        private void ApplyDamage(LaserTurret owner, in GreenTargetScanner.Target target, Vector2 point, Vector2 direction)
        {
            var attack = _fx.Definition.attack;
            var damage = attack.damagePerShot;
            if (damage <= 0f || owner == null)
            {
                return;
            }

            var swarm = SwarmSystem.Current;
            if (swarm != null && (FortressNet.HasSimulationAuthority || swarm.IsReplica))
            {
                // 群衆側は「1秒あたりのダメージ × そのフレームの秒数」を与えるので、1フレームで damage になるように割る。
                var frame = Time.deltaTime;
                var settings = swarm.ActiveSettings;
                if (settings != null)
                {
                    frame = Mathf.Min(frame, settings.maxFrameDelta);
                }

                var dps = damage / Mathf.Max(frame, 1f / 240f);
                var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
                swarm.QueueBeam(point - dir * 0.05f, point + dir * 0.05f, attack.splashRadius, dps, Mathf.Max(0, attack.maxHitsPerShot), owner.playerIndex);
            }

            if (target.Actor != null && target.Actor.isActiveAndEnabled && FortressNet.HasSimulationAuthority)
            {
                target.Actor.ApplyLaserDamage(damage, null, owner);
            }
        }

        private void PlayImpactFx(Vector2 point, Vector2 direction, LaserTurret owner)
        {
            var shots = _fx.Definition.shots;
            var palette = _fx.Definition.palette;
            _fx.Powder(point, shots.powderCount, shots.powderSize, 3.2f);
            _fx.Leaves(point, shots.leafCount, shots.leafSize, 2.6f);
            _fx.Sparkles(point, shots.sparkleCount, 7f, 0.3f, palette.glow, direction, 80f, 0.45f);
            if (shots.ringSize > 0f)
            {
                var ring = palette.mint;
                ring.a = 0.85f;
                _fx.WaveAt(point, shots.ringSize * 0.25f, shots.ringSize, 0.32f, ring);
            }

            _fx.FlashAt(point, shots.ringSize * 0.9f, 0.22f, palette.glow * 0.8f, 6);

            var sounds = _fx.Definition.sounds;
            if (Time.time >= _nextHitSound && !sounds.onHit.IsEmpty)
            {
                _nextHitSound = Time.time + sounds.hitInterval;
                FortressEffectPlayer.PlayOnce(sounds.onHit, new Vector3(point.x, point.y, 0f), FortressEffectPlayer.RotationFacing(-direction), owner != null ? owner.playerIndex : -1);
            }
        }

        private void Draw(Bullet bullet, float z)
        {
            var size = _fx.Definition.shots.bulletSize * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(bullet.Age / 0.08f));
            var transform = bullet.Sprite.transform;
            transform.SetPositionAndRotation(new Vector3(bullet.Position.x, bullet.Position.y, z), Quaternion.Euler(0f, 0f, bullet.Spin));
            transform.localScale = new Vector3(size, size, 1f);
            bullet.Sprite.enabled = true;
            bullet.Trail.transform.position = new Vector3(bullet.Position.x, bullet.Position.y, z + 0.005f);
        }

        private Bullet FindFree()
        {
            for (var i = 0; i < _bullets.Length; i++)
            {
                if (!_bullets[i].Active)
                {
                    return _bullets[i];
                }
            }

            return null;
        }
    }
}
