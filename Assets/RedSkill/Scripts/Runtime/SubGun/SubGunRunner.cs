using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 副砲を動かす係の共通部分。砲台（<see cref="LaserTurret"/>）に1つ付く。
    /// 本体のレーザーが出ているか・どこまで届いているか（壁で止まった所まで）をここで調べ、派生クラスはそれを使って撃つ。
    /// 群衆の敵に当てる処理は、群衆の配列を読める瞬間（<see cref="SwarmSystem.StorageReadable"/>）に <see cref="OnSwarmReadable"/> で行う
    /// （緑のスキルの葉と同じやり方）。
    /// 見た目は毎フレーム <see cref="Draw"/> で描き直す。敵に当たった所の合図（星形の光）は <see cref="AddFlash"/> で出す。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class SubGunRunner : MonoBehaviour
    {
        private const int MaxFlashes = 32;

        private struct Flash
        {
            public Vector2 Position;
            public float Age;
            public float Scale;
            public float Angle;
        }

        private readonly List<Flash> _flashes = new List<Flash>(MaxFlashes);
        private SwarmSystem _swarm;
        private LaserBeamVisual _beam;
        private SubGunCanvas _canvas;

        /// <summary>撃つ砲台。</summary>
        public LaserTurret Turret { get; private set; }

        /// <summary>このキャラの設定。</summary>
        protected SubGunSettings BaseSettings { get; private set; }

        /// <summary>撃ったプレイヤー（倒した敵の手柄）。</summary>
        protected int Owner => Turret != null ? Turret.playerIndex : -1;

        /// <summary>本体のレーザーが今出ているか（握っている・オーバーヒートしていない・スキルで止められていない）。</summary>
        protected bool MainBeamOn { get; private set; }

        /// <summary>本体のレーザーの根元。</summary>
        protected Vector2 BeamOrigin { get; private set; }

        /// <summary>本体のレーザーの向き（長さ1）。</summary>
        protected Vector2 BeamDirection { get; private set; }

        /// <summary>本体のレーザーが届いている長さ（壁・障害物で止まったらそこまで）。</summary>
        protected float BeamLength { get; private set; }

        /// <summary>本体のレーザーの先（壁で止まったらその場所）。</summary>
        protected Vector2 BeamEnd => BeamOrigin + BeamDirection * BeamLength;

        /// <summary>本体のレーザーの半分の太さ（m）。</summary>
        protected float BeamHalfWidth { get; private set; }

        /// <summary>握る強さ（本体の太さ 0〜1）。</summary>
        protected float Grip01 { get; private set; }

        /// <summary>本体のレーザーの線（描く順番をそろえるのに使う）。無いこともある。</summary>
        protected LineRenderer BeamLine => _beam != null ? _beam.lineRenderer : null;

        /// <summary>本体のレーザーが当たっている物（壁・障害物・ボスの部位など）。無ければ null。</summary>
        protected Collider2D BeamImpactCollider => MainBeamOn && _beam != null && _beam.HasImpact ? _beam.ImpactCollider : null;

        /// <summary>壁・障害物・Actor の敵を探すときの層（本体のレーザーと同じ）。</summary>
        protected LayerMask HitMask => _beam != null ? _beam.targetLayerMask : (LayerMask)~0;

        /// <summary>地面の高さ（z）。副砲の見た目はこの高さに置く。</summary>
        protected float GroundZ { get; private set; }

        /// <summary>
        /// 砲台に、そのキャラの副砲を付ける（もう付いていれば外してから付け直す）。スキルが無い・副砲が無いキャラは何も付かない。
        /// <see cref="PlayerSkillInstaller.Install"/> から呼ぶ。
        /// </summary>
        public static SubGunRunner Install(LaserTurret turret, CharacterSkill skill)
        {
            if (turret == null)
            {
                return null;
            }

            var old = turret.GetComponents<SubGunRunner>();
            for (var i = 0; i < old.Length; i++)
            {
                if (old[i] != null)
                {
                    old[i].enabled = false;
                    Destroy(old[i]);
                }
            }

            var settings = skill != null ? skill.SubGun : null;
            if (settings == null)
            {
                return null;
            }

            var runner = settings.AddRunner(turret.gameObject);
            if (runner != null)
            {
                runner.Bind(turret, settings);
            }

            return runner;
        }

        private void Bind(LaserTurret turret, SubGunSettings settings)
        {
            Turret = turret;
            BaseSettings = settings;
            _beam = turret.GetComponent<LaserBeamVisual>();
            _canvas = new SubGunCanvas("SubGun_" + GetType().Name + "_P" + (turret.playerIndex + 1));
            OnBound();
        }

        /// <summary>結びついた直後に1回。</summary>
        protected virtual void OnBound()
        {
        }

        /// <summary>毎フレーム。本体のレーザーの状態を調べた後に呼ぶ。dt は秒。</summary>
        protected abstract void Tick(float dt);

        /// <summary>毎フレームの見た目。back は本体のレーザーの下、front は上に描く層。</summary>
        protected abstract void Draw(SubGunLayer back, SubGunLayer front);

        /// <summary>群衆の配列を読める瞬間（このフレームの当たり判定を受け付ける直前）。</summary>
        protected virtual void OnSwarmReadable(SwarmSystem swarm)
        {
        }

        /// <summary>
        /// 群衆の敵にレーザーが当たった瞬間の記録（全員のレーザー分。演出用）。受け取りたい派生クラスは <see cref="WantsBeamHits"/> を true にする。
        /// </summary>
        protected virtual void OnBeamHits(IReadOnlyList<SwarmBeamHit> hits)
        {
        }

        /// <summary>当たった瞬間の記録を受け取るか。</summary>
        protected virtual bool WantsBeamHits => false;

        /// <summary>敵に当たった所の合図（キャラの色の星形の光）を出す。scale は大きさの倍率。</summary>
        protected void AddFlash(Vector2 position, float scale = 1f)
        {
            if (BaseSettings == null || BaseSettings.hitFlashSize <= 0f || _flashes.Count >= MaxFlashes)
            {
                return;
            }

            _flashes.Add(new Flash { Position = position, Scale = scale, Angle = Random.Range(0f, 90f) * Mathf.Deg2Rad });
        }

        /// <summary>縁取り付きの折れ線（暗い縁 → 色 の順に描く）。</summary>
        protected void OutlinedPolyline(SubGunLayer layer, IReadOnlyList<Vector2> points, int start, int count, float width, Color color, float outlineAlpha = 1f)
        {
            var settings = BaseSettings;
            if (settings.outlineWidth > 0f)
            {
                layer.Polyline(points, start, count, width + settings.outlineWidth * 2f, settings.OutlineColor(color.a * outlineAlpha));
            }

            layer.Polyline(points, start, count, width, color);
        }

        /// <summary>縁取り付きの丸。</summary>
        protected void OutlinedDisc(SubGunLayer layer, Vector2 center, float radius, Color color, int segments = 14)
        {
            var settings = BaseSettings;
            if (settings.outlineWidth > 0f)
            {
                layer.Disc(center, radius + settings.outlineWidth, settings.OutlineColor(color.a), segments);
            }

            layer.Disc(center, radius, color, segments);
        }

        /// <summary>縁取り付きの輪。</summary>
        protected void OutlinedRing(SubGunLayer layer, Vector2 center, float radius, float width, Color color, int segments = 22)
        {
            var settings = BaseSettings;
            if (settings.outlineWidth > 0f)
            {
                layer.Ring(center, radius, width + settings.outlineWidth * 2f, settings.OutlineColor(color.a), segments);
            }

            layer.Ring(center, radius, width, color, segments);
        }

        /// <summary>縁取り付きの葉の形。</summary>
        protected void OutlinedLeaf(SubGunLayer layer, Vector2 center, Vector2 dir, float length, float width, Color color)
        {
            var settings = BaseSettings;
            if (settings.outlineWidth > 0f)
            {
                layer.Leaf(center, dir, length + settings.outlineWidth * 3f, width + settings.outlineWidth * 2f, settings.OutlineColor(color.a));
            }

            layer.Leaf(center, dir, length, width, color);
        }

        /// <summary>見た目を全部消す（外すとき・止めたとき）。</summary>
        protected void HideAll()
        {
            _canvas?.Hide();
        }

        private void Update()
        {
            if (Turret == null || BaseSettings == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            RefreshSwarm();
            ReadBeam();
            Tick(dt);
            AgeFlashes(dt);
            Render();
        }

        private void Render()
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.Begin(GroundZ);
            Draw(_canvas.Back, _canvas.Front);
            DrawFlashes(_canvas.Front);
            _canvas.End(BeamLine);
        }

        private void AgeFlashes(float dt)
        {
            var life = BaseSettings.hitFlashSeconds;
            for (var i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                flash.Age += dt;
                if (flash.Age >= life)
                {
                    _flashes.RemoveAt(i);
                }
                else
                {
                    _flashes[i] = flash;
                }
            }
        }

        /// <summary>星形の光（キャラの色・暗い縁・白い芯）。前半は大きく、後半は小さく（止めのある2段）。</summary>
        private void DrawFlashes(SubGunLayer layer)
        {
            var settings = BaseSettings;
            for (var i = 0; i < _flashes.Count; i++)
            {
                var flash = _flashes[i];
                var first = flash.Age < settings.hitFlashSeconds * 0.5f;
                var size = settings.hitFlashSize * flash.Scale * (first ? 1f : 0.6f);
                var alpha = first ? 1f : 0.7f;
                var color = settings.ColorFor(Owner, alpha);
                var a = new Vector2(Mathf.Cos(flash.Angle), Mathf.Sin(flash.Angle));
                var b = new Vector2(-a.y, a.x);
                OutlinedLeaf(layer, flash.Position, a, size, size * 0.28f, color);
                OutlinedLeaf(layer, flash.Position, b, size, size * 0.28f, color);
                layer.Disc(flash.Position, size * 0.16f, new Color(1f, 1f, 1f, alpha * settings.opacity), 10);
            }
        }

        private void ReadBeam()
        {
            var tuning = Turret.tuning;
            if (_beam == null)
            {
                _beam = Turret.GetComponent<LaserBeamVisual>();
            }

            // スキル中は普段のレーザー（LaserBeamVisual）が止められるので、副砲も止まる。
            MainBeamOn = BaseSettings.enabled && tuning != null && Turret.IsFiring && _beam != null && _beam.isActiveAndEnabled;
            var origin = Turret.MuzzlePosition;
            GroundZ = origin.z;
            BeamOrigin = origin;
            BeamDirection = Turret.AimDirection.sqrMagnitude > 1e-6f ? Turret.AimDirection.normalized : Vector2.up;
            BeamHalfWidth = Turret.CurrentThicknessMeters * 0.5f;
            Grip01 = Turret.CurrentThickness01;
            var length = tuning != null ? tuning.range : 0f;
            if (MainBeamOn && _beam.HasImpact)
            {
                length = Mathf.Min(length, Vector2.Distance(BeamOrigin, _beam.ImpactPoint));
            }

            BeamLength = Mathf.Max(0f, length);
        }

        private void RefreshSwarm()
        {
            var current = SwarmSystem.Current;
            if (current == _swarm)
            {
                return;
            }

            Unsubscribe();
            _swarm = current;
            if (_swarm != null)
            {
                _swarm.StorageReadable += HandleStorageReadable;
                if (WantsBeamHits)
                {
                    _swarm.BeamHitsReported += HandleBeamHits;
                }
            }
        }

        private void Unsubscribe()
        {
            if (_swarm == null)
            {
                return;
            }

            _swarm.StorageReadable -= HandleStorageReadable;
            _swarm.BeamHitsReported -= HandleBeamHits;
        }

        private void HandleStorageReadable()
        {
            if (_swarm == null || _swarm.Storage == null || Turret == null || BaseSettings == null)
            {
                return;
            }

            OnSwarmReadable(_swarm);
        }

        private void HandleBeamHits(IReadOnlyList<SwarmBeamHit> hits)
        {
            if (Turret != null && BaseSettings != null && hits != null)
            {
                OnBeamHits(hits);
            }
        }

        protected virtual void OnDisable()
        {
            HideAll();
        }

        protected virtual void OnDestroy()
        {
            Unsubscribe();
            _swarm = null;
            _canvas?.Dispose();
            _canvas = null;
        }
    }
}
