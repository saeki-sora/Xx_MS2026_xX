using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.ObjectToon.KillFx;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習のシーンの「自分だけの敵（ばい菌）」（2026-11-19）。
    /// 4人で同じ敵を見ていると、先に進んだ人が敵を倒してしまい、ほかの人の研修1が進まないことがある。
    /// そこで研修の敵は、1人ずつ別にする:
    /// ・このPCの中だけで動く（通信しない）。ほかのPCには出ない
    /// ・自分の砲台の歯磨き粉（レーザー）でしか減らない（ほかの人のレーザーが重なっても減らない）
    /// ・見た目は本番の敵と同じ（敵の種類のスプライトシート・大きさ・体力・速さ）。倒すと本番と同じ「やられ」の演出（KillFx）
    /// ・自分の砲台の側から、真ん中の歯に向かって歩いてくる。歯に着いたら消える（練習なので歯は減らない）
    /// 本物の群衆（SwarmSystem）は使わない（群衆のダメージは Host だけが計算するので、1人ずつに分けられないため）。
    /// </summary>
    public sealed class PracticeLocalEnemies : MonoBehaviour
    {
        private sealed class Enemy
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public Vector2 Pos;
            public Vector2 Dir;
            public float Speed;
            public float Hp;
            public float Anim;
            public float Hit;
            public float Wobble;
            public float GoneAt = -1f;
            public bool Killed;
        }

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private readonly List<(Enemy enemy, float along)> _hits = new List<(Enemy, float)>();
        private EnemyTypeDefinition _type;
        private PracticeSceneTuning _tuning;
        private Vector3 _core;
        private int _cols = 1;
        private int _rows = 1;
        private float _nextSpawn = 1f;
        private float _time;
        private LaserTurret _beamOf;
        private LaserBeamVisual _beam;
        private Texture2D _generated;

        /// <summary>自分が倒した数（練習のシーンを読み込んでから）。</summary>
        public int Kills { get; private set; }

        public void Setup(EnemyTypeDefinition type, PracticeSceneTuning tuning, Vector3 core)
        {
            _type = type;
            _tuning = tuning ?? new PracticeSceneTuning();
            _core = core;
            BuildSprites();
        }

        private void BuildSprites()
        {
            if (_type == null)
            {
                return;
            }

            var swarm = _type.swarm ?? new SwarmEnemySettings();
            _cols = Mathf.Max(1, swarm.frameCount);
            _rows = Mathf.Max(1, swarm.directionCount);
            var sheet = swarm.spriteSheet;

            // 練習の敵の絵の差し替え（Resources/PracticeArtLibrary の「練習の敵」）。
            var lib = PracticeArtLibrary.Load();
            if (lib != null && lib.enemySheet != null)
            {
                sheet = lib.enemySheet;
                _cols = lib.enemySheetFrames > 0 ? lib.enemySheetFrames : _cols;
                _rows = lib.enemySheetDirections > 0 ? lib.enemySheetDirections : _rows;
            }

            if (sheet == null)
            {
                _generated = SwarmPlaceholderSheet.Create(_type.placeholderColor, _cols, _rows);
                sheet = _generated;
            }

            var w = sheet.width / (float)_cols;
            var h = sheet.height / (float)_rows;
            var ppu = w / Mathf.Max(0.1f, swarm.spriteSize);
            // シートは 横 = コマ（左→右）、縦 = 向き（上→下に 右, 右上, 上, 左上, 左, 左下, 下, 右下）。
            for (var r = 0; r < _rows; r++)
            {
                for (var c = 0; c < _cols; c++)
                {
                    var rect = new Rect(c * w, (_rows - 1 - r) * h, w, h);
                    var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{_type.name} {r}-{c}";
                    _sprites.Add(sprite);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var e in _enemies)
            {
                if (e.Go != null)
                {
                    Destroy(e.Go);
                }
            }

            foreach (var s in _sprites)
            {
                if (s != null)
                {
                    Destroy(s);
                }
            }

            if (_generated != null)
            {
                Destroy(_generated);
            }
        }

        /// <summary>毎フレーム呼ぶ。turret = 自分の砲台（無ければ敵を出さない）。</summary>
        public void Tick(LaserTurret turret, Camera cam, float dt)
        {
            if (_type == null || _sprites.Count == 0)
            {
                return;
            }

            _time += dt;
            if (turret != null && _time >= _nextSpawn)
            {
                _nextSpawn = _time + Mathf.Max(0.2f, _tuning.localEnemySpawnSeconds);
                if (AliveCount() < Mathf.Max(1, _tuning.localEnemyMax))
                {
                    Spawn(turret);
                }
            }

            ApplyBeam(turret, dt);
            Move(dt);
            Draw(cam, dt);
        }

        private int AliveCount()
        {
            var n = 0;
            foreach (var e in _enemies)
            {
                n += e.GoneAt < 0f ? 1 : 0;
            }

            return n;
        }

        private void Spawn(LaserTurret turret)
        {
            // 自分の砲台の側（コアから見た砲台の向き ± 広がり）の遠くから出す。
            var toTurret = (Vector2)(turret.transform.position - _core);
            var baseAngle = toTurret.sqrMagnitude > 0.01f ? Mathf.Atan2(toTurret.y, toTurret.x) * Mathf.Rad2Deg : 90f;
            var angle = (baseAngle + Random.Range(-_tuning.localEnemySpreadDegrees, _tuning.localEnemySpreadDegrees)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var pos = (Vector2)_core + (dir * _tuning.localEnemySpawnRadius);
            var speed = _type.moveSpeed * _tuning.localEnemySpeedScale * (1f + Random.Range(-1f, 1f) * (_type.swarm != null ? _type.swarm.speedVariance : 0f));

            var go = new GameObject("Practice Enemy");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 40;
            _enemies.Add(new Enemy
            {
                Go = go,
                Renderer = renderer,
                Pos = pos,
                Dir = -dir,
                Speed = Mathf.Max(0.3f, speed),
                Hp = Mathf.Max(0.1f, _type.maxHealth * _tuning.localEnemyHealthScale),
                Anim = Random.value * _cols,
                Wobble = Random.value * 10f
            });
        }

        // ───────── 自分の歯磨き粉（レーザー）だけが当たる ─────────

        private void ApplyBeam(LaserTurret turret, float dt)
        {
            if (turret == null || turret.tuning == null || turret.State != TurretState.Firing || turret.CurrentThickness01 <= 0f)
            {
                return;
            }

            if (_beamOf != turret)
            {
                _beamOf = turret;
                _beam = turret.GetComponentInChildren<LaserBeamVisual>(true);
            }

            var tuning = turret.tuning;
            Vector2 origin = turret.MuzzlePosition;
            var dir = turret.AimDirection.normalized;
            var length = tuning.range;
            if (_beam != null && _beam.HasImpact)
            {
                // 泡などに当たっている所で止まる（見た目のビームと同じ長さ）。
                length = Mathf.Min(length, Vector2.Dot((Vector2)_beam.ImpactPoint - origin, dir));
            }

            var swarm = _type.swarm ?? new SwarmEnemySettings();
            var reach = (turret.CurrentThicknessMeters * 0.5f) + (swarm.hitRadius > 0f ? swarm.hitRadius : swarm.radius);
            _hits.Clear();
            foreach (var e in _enemies)
            {
                if (e.GoneAt >= 0f)
                {
                    continue;
                }

                var rel = e.Pos - origin;
                var along = Vector2.Dot(rel, dir);
                if (along < 0f || along > length)
                {
                    continue;
                }

                var side = Mathf.Abs((rel.x * dir.y) - (rel.y * dir.x));
                if (side <= reach)
                {
                    _hits.Add((e, along));
                }
            }

            if (_hits.Count == 0)
            {
                return;
            }

            // 貫通: 本番と同じく、砲台に近い順に（数の上限あり）。貫通しないなら最初の1体だけ。
            _hits.Sort((a, b) => a.along.CompareTo(b.along));
            var max = tuning.pierceEnemies ? (tuning.maxPierceCount > 0 ? tuning.maxPierceCount : int.MaxValue) : 1;
            var damage = tuning.maxDamagePerSecond * turret.CurrentThickness01 * dt;
            for (var i = 0; i < _hits.Count && i < max; i++)
            {
                var e = _hits[i].enemy;
                e.Hp -= damage;
                e.Hit = 0.1f;
                if (e.Hp <= 0f)
                {
                    Kill(e, turret.playerIndex);
                }
            }
        }

        private void Kill(Enemy e, int owner)
        {
            e.GoneAt = _time;
            e.Killed = true;
            Kills++;
            var director = KillFxDirector.Instance;
            if (director != null)
            {
                director.Play(e.Pos, owner);
            }
        }

        private void Move(float dt)
        {
            foreach (var e in _enemies)
            {
                if (e.GoneAt >= 0f)
                {
                    continue;
                }

                e.Wobble += dt * 3f;
                var side = new Vector2(-e.Dir.y, e.Dir.x) * (Mathf.Sin(e.Wobble) * 0.25f);
                var step = (e.Dir + side).normalized * e.Speed * dt;
                e.Pos += step;
                e.Anim += dt * (_type.swarm != null ? _type.swarm.animationFps : 12f);
                var toCore = (Vector2)_core - e.Pos;
                if (toCore.sqrMagnitude < 1.2f * 1.2f)
                {
                    // 歯に着いた（練習なので歯は減らない）。そのまま消える。
                    e.GoneAt = _time;
                }
                else
                {
                    e.Dir = Vector2.Lerp(e.Dir, toCore.normalized, dt * 2f).normalized;
                }
            }
        }

        private void Draw(Camera cam, float dt)
        {
            var camRot = cam != null ? cam.transform.rotation : Quaternion.identity;
            var inv = Quaternion.Inverse(camRot);
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                var gone = e.GoneAt >= 0f ? _time - e.GoneAt : -1f;
                var fade = e.Killed ? 0.18f : 0.35f;
                if (gone >= fade || e.Go == null)
                {
                    if (e.Go != null)
                    {
                        Destroy(e.Go);
                    }

                    _enemies.RemoveAt(i);
                    continue;
                }

                // 向き: 画面の上下に合わせた、歩く向きの行。
                var local = inv * new Vector3(e.Dir.x, e.Dir.y, 0f);
                var deg = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                var row = DirectionRow(deg);
                var col = Mathf.FloorToInt(e.Anim) % _cols;
                e.Renderer.sprite = _sprites[Mathf.Clamp((row * _cols) + col, 0, _sprites.Count - 1)];

                e.Hit = Mathf.Max(0f, e.Hit - dt);
                var tint = e.Hit > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;
                var scale = 1f;
                if (gone >= 0f)
                {
                    var k = gone / fade;
                    tint.a = 1f - k;
                    scale = e.Killed ? 1f + (0.5f * k) : 1f - (0.4f * k);
                }

                e.Renderer.color = tint;
                e.Go.transform.SetPositionAndRotation(new Vector3(e.Pos.x, e.Pos.y, _core.z - 0.1f), camRot);
                e.Go.transform.localScale = Vector3.one * scale;
            }
        }

        private int DirectionRow(float deg)
        {
            switch (_rows)
            {
                case 8: return Mod(Mathf.RoundToInt(deg / 45f), 8);
                case 4: return Mod(Mathf.RoundToInt(deg / 90f), 4);
                case 2: return Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) <= 90f ? 0 : 1;
                default: return 0;
            }
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }
}
