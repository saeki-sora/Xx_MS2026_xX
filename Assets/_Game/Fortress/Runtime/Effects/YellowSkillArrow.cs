using System.Collections.Generic;
using MS2026.Fortress.Net;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>移動距離全体をRaycastして、薄い壁も通り抜けず反射する矢。</summary>
    public sealed class YellowSkillArrow : MonoBehaviour
    {
        private const float Skin = 0.002f;
        private readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>();
        private readonly List<Collider2D> _enemyHits = new List<Collider2D>();
        private readonly HashSet<EnemyController> _damagedEnemies = new HashSet<EnemyController>();
        private LaserTurret _attacker;
        private float _damagePerSecond;
        private Vector2 _direction;
        private float _speed;
        private float _remainingLife;
        private ContactFilter2D _filter;
        private Material _material;
        private LineRenderer _body;
        private LineRenderer _glow;
        private LineRenderer _core;
        private readonly LineRenderer[] _lightning = new LineRenderer[4];
        private readonly LineRenderer[] _glints = new LineRenderer[16];
        private readonly LineRenderer[] _halos = new LineRenderer[3];
        private readonly LineRenderer[] _speedLines = new LineRenderer[10];
        private readonly LineRenderer[] _impactStars = new LineRenderer[8];
        private TrailRenderer _trail;
        private float _length;
        private float _flash;
        private SimpleShakeSettings _bounceShake;
        private float _nextShakeTime;
        private const float SparkLifetime = 0.32f;
        private readonly LineRenderer[] _sparks = new LineRenderer[64];
        private readonly Vector3[] _sparkCenters = new Vector3[64];
        private readonly Vector3[] _sparkDirections = new Vector3[64];
        private readonly float[] _sparkAges = new float[64];
        private int _nextSpark;
        private const float AfterimageInterval = 0.025f;
        private const float AfterimageLifetime = 0.65f;
        private readonly LineRenderer[] _afterimages = new LineRenderer[28];
        private readonly float[] _afterimageAges = new float[28];
        private readonly LineRenderer[] _auras = new LineRenderer[3];
        private readonly LineRenderer[] _rings = new LineRenderer[8];
        private readonly float[] _ringAges = new float[8];
        private readonly Vector3[] _ringCenters = new Vector3[8];
        private readonly float[] _ringScales = new float[8];
        private const float RingLifetime = 0.55f;
        private int _nextRing;
        private Vector3[] _arrowPoints;
        private float _afterimageTimer;
        private int _nextAfterimage;
        private bool _expired;
        private static readonly Color Gold = new Color(1f, 0.72f, 0.08f);

        public void Initialize(Vector2 direction, float speed, float lifetime, float length, LayerMask walls,
            LaserTurret attacker = null, float damagePerSecond = 0f,
            SimpleShakeSettings launchShake = null, SimpleShakeSettings bounceShake = null)
        {
            _attacker = attacker;
            _bounceShake = bounceShake;
            _damagePerSecond = Mathf.Max(0f, damagePerSecond);
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
            _speed = Mathf.Max(0.01f, speed);
            _remainingLife = Mathf.Max(0.01f, lifetime);
            _filter = new ContactFilter2D { useTriggers = false };
            _filter.SetLayerMask(walls);
            var line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 5;
            length = Mathf.Max(0.01f, length);
            line.SetPositions(new[] {
                new Vector3(0f, -length, 0f), Vector3.zero,
                new Vector3(-length * 0.3f, -length * 0.4f, 0f), Vector3.zero,
                new Vector3(length * 0.3f, -length * 0.4f, 0f)
            });
            line.startWidth = line.endWidth = length * 0.1f;
            line.startColor = line.endColor = new Color(1f, 0.84f, 0.1f);
            line.sortingOrder = 110;
            _material = new Material(Shader.Find("Sprites/Default"));
            line.sharedMaterial = _material;
            _body = line;
            _length = length;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.startColor = line.endColor = new Color(1f, 0.97f, 0.65f);
            var glowObject = new GameObject("GoldenOutline");
            glowObject.transform.SetParent(transform, false);
            _glow = glowObject.AddComponent<LineRenderer>();
            _glow.useWorldSpace = false;
            _glow.positionCount = line.positionCount;
            var points = new Vector3[line.positionCount];
            line.GetPositions(points);
            _arrowPoints = points;
            _glow.SetPositions(points);
            _glow.sharedMaterial = _material;
            _glow.startWidth = _glow.endWidth = length * 0.23f;
            _glow.startColor = _glow.endColor = new Color(1f, 0.6f, 0.02f, 0.35f);
            _glow.numCapVertices = _glow.numCornerVertices = 6;
            _glow.sortingOrder = 109;
            CreateEffects();
            CreateAfterimages();
            CreateOverdriveEffects();
            CreateFlightEffects();
            Orient();
            PlayFlash(transform.position);
            PlayShake(launchShake);
        }

        private LineRenderer CreateFlightLine(string label, int points, int order)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points;
            line.sharedMaterial = _material;
            line.sortingOrder = order;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            return line;
        }

        private void CreateFlightEffects()
        {
            _core = CreateFlightLine("WhiteGoldCore", _arrowPoints.Length, 112);
            _core.SetPositions(_arrowPoints);
            _core.startWidth = _core.endWidth = _length * 0.045f;
            _core.startColor = _core.endColor = new Color(1f, 1f, 0.95f);
            _body.startWidth = _body.endWidth = _length * 0.14f;
            _glow.startColor = _glow.endColor = new Color(1f, 0.56f, 0.02f, 0.65f);
            for (var i = 0; i < _lightning.Length; i++)
            {
                var line = CreateFlightLine("GoldenLightning", 13, 111);
                line.startWidth = _length * 0.035f;
                line.endWidth = _length * 0.012f;
                line.startColor = new Color(1f, 0.65f, 0.04f, 0.25f);
                line.endColor = new Color(1f, 1f, 0.8f, 0.95f);
                _lightning[i] = line;
            }
            for (var i = 0; i < _glints.Length; i++)
            {
                var line = CreateFlightLine("OrbitingGoldGlint", 4, 115);
                line.loop = true;
                _glints[i] = line;
            }
            for (var i = 0; i < _halos.Length; i++)
            {
                var line = CreateFlightLine("RotatingSolarHalo", 48, 106);
                line.loop = true;
                _halos[i] = line;
            }
            for (var i = 0; i < _speedLines.Length; i++)
                _speedLines[i] = CreateFlightLine("CometSpeedLine", 2, 105);
            UpdateFlightEffects();
        }

        private void UpdateFlightEffects()
        {
            // 固定数の線を動かし、飛行中にオブジェクトや配列を生成しない。
            var time = Time.time;
            for (var i = 0; i < _halos.Length; i++)
            {
                var halo = _halos[i];
                var rotation = time * (i % 2 == 0 ? 3f : -4f) + i * 1.1f;
                var radius = _length * (0.36f + i * 0.13f + _flash * 0.1f);
                for (var j = 0; j < halo.positionCount; j++)
                {
                    var angle = j * Mathf.PI * 2f / halo.positionCount;
                    var x = Mathf.Cos(angle) * radius;
                    var y = Mathf.Sin(angle) * radius * 0.35f;
                    halo.SetPosition(j, new Vector3(
                        x * Mathf.Cos(rotation) - y * Mathf.Sin(rotation),
                        x * Mathf.Sin(rotation) + y * Mathf.Cos(rotation) - _length * 0.35f, 0f));
                }
                halo.startWidth = halo.endWidth = _length * (0.025f + _flash * 0.025f);
                halo.startColor = halo.endColor = new Color(1f, 0.75f + i * 0.08f, 0.2f, 0.65f);
            }
            for (var i = 0; i < _speedLines.Length; i++)
            {
                var progress = Mathf.Repeat(time * 2.4f + i * 0.3819f, 1f);
                var side = i % 2 == 0 ? 1f : -1f;
                var center = new Vector3(side * (0.25f + (i % 5) * 0.09f),
                    -0.15f - progress * 1.9f, 0f) * _length;
                var line = _speedLines[i];
                line.SetPosition(0, center);
                line.SetPosition(1, center + Vector3.down * _length * (0.15f + progress * 0.35f));
                line.startWidth = _length * 0.028f * Mathf.Sin(progress * Mathf.PI);
                line.endWidth = 0f;
                var color = new Color(1f, 0.85f, 0.4f, Mathf.Sin(progress * Mathf.PI) * 0.8f);
                line.startColor = line.endColor = color;
            }
            for (var i = 0; i < _lightning.Length; i++)
            {
                var line = _lightning[i];
                var side = i % 2 == 0 ? 1f : -1f;
                for (var j = 0; j < line.positionCount; j++)
                {
                    var t = j / (float)(line.positionCount - 1);
                    var envelope = Mathf.Sin(t * Mathf.PI);
                    var noise = Mathf.PerlinNoise(j * 2.7f + i * 13f, time * 16f) - 0.5f;
                    var x = side * (0.2f + i * 0.07f + noise * 0.32f) * envelope;
                    line.SetPosition(j, new Vector3(x, -1.35f * (1f - t), 0f) * _length);
                }
            }
            for (var i = 0; i < _glints.Length; i++)
            {
                var progress = Mathf.Repeat(time * 1.6f + i * 0.618f, 1f);
                var angle = time * 5f + i * 2.4f;
                var center = new Vector3(Mathf.Sin(angle) * (0.18f + progress * 0.4f),
                    -progress * 1.65f, 0f) * _length;
                var size = _length * 0.055f * Mathf.Sin(progress * Mathf.PI);
                var glint = _glints[i];
                glint.SetPosition(0, center + Vector3.up * size * 2f);
                glint.SetPosition(1, center + Vector3.right * size);
                glint.SetPosition(2, center - Vector3.up * size * 2f);
                glint.SetPosition(3, center - Vector3.right * size);
                glint.startWidth = glint.endWidth = _length * 0.018f;
                var color = Color.Lerp(new Color(1f, 1f, 0.9f), Gold, progress);
                color.a = Mathf.Sin(progress * Mathf.PI);
                glint.startColor = glint.endColor = color;
            }
        }

        private void CreateOverdriveEffects()
        {
            for (var i = 0; i < _sparks.Length; i++)
            {
                var spark = new GameObject("GoldenSpark");
                spark.transform.SetParent(transform, false);
                var line = spark.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.sharedMaterial = _material;
                line.numCapVertices = 3;
                line.sortingOrder = 114;
                line.enabled = false;
                _sparks[i] = line;
            }
            for (var i = 0; i < _auras.Length; i++)
            {
                var aura = new GameObject("GoldenAura" + i);
                aura.transform.SetParent(transform, false);
                var line = aura.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = _arrowPoints.Length;
                line.SetPositions(_arrowPoints);
                line.sharedMaterial = _material;
                line.numCapVertices = line.numCornerVertices = 6;
                line.sortingOrder = 104 - i;
                line.startColor = line.endColor = new Color(1f, 0.48f + i * 0.12f, 0.02f, 0.16f - i * 0.035f);
                _auras[i] = line;
            }

            for (var i = 0; i < _rings.Length; i++)
            {
                var ring = new GameObject("ImpactShockRing");
                ring.transform.SetParent(transform, false);
                var line = ring.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = 48;
                line.sharedMaterial = _material;
                line.sortingOrder = 113;
                line.enabled = false;
                _rings[i] = line;
                var star = CreateFlightLine("ImpactStarburst", 16, 116);
                star.useWorldSpace = true;
                star.loop = true;
                star.enabled = false;
                _impactStars[i] = star;
            }
        }

        private void AddRing(Vector3 center, float scale)
        {
            _ringCenters[_nextRing] = center;
            _ringScales[_nextRing] = scale;
            _ringAges[_nextRing] = 0f;
            _rings[_nextRing].enabled = true;
            DrawRing(_nextRing);
            _nextRing = (_nextRing + 1) % _rings.Length;
        }

        private void DrawRing(int index)
        {
            var ring = _rings[index];
            var progress = Mathf.Clamp01(_ringAges[index] / RingLifetime);
            var radius = _length * _ringScales[index] * (0.15f + 1.85f * (1f - (1f - progress) * (1f - progress)));
            for (var j = 0; j < ring.positionCount; j++)
            {
                var angle = j * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(j, _ringCenters[index] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            var color = Color.Lerp(new Color(1f, 1f, 0.75f), Gold, progress);
            color.a = 0.85f * (1f - progress);
            ring.startColor = ring.endColor = color;
            ring.startWidth = ring.endWidth = _length * 0.12f * (1f - progress);
            ring.enabled = progress < 1f;
            var star = _impactStars[index];
            // 八方向の閃光は素早く広がり、輪より早く消える。
            var starProgress = Mathf.Clamp01(progress * 2.2f);
            var starRadius = _length * _ringScales[index] * (0.3f + starProgress * 1.4f);
            for (var j = 0; j < star.positionCount; j++)
            {
                var angle = j * Mathf.PI * 2f / star.positionCount + index * 0.17f;
                var size = j % 2 == 0 ? starRadius : starRadius * 0.12f;
                star.SetPosition(j, _ringCenters[index] + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size);
            }
            star.startWidth = star.endWidth = _length * 0.065f * (1f - starProgress);
            star.startColor = star.endColor = new Color(1f, 0.95f, 0.65f, 1f - starProgress);
            star.enabled = starProgress < 1f;
        }

        private void UpdateOverdriveEffects()
        {
            for (var i = 0; i < _sparks.Length; i++)
            {
                var spark = _sparks[i];
                if (!spark.enabled) continue;
                _sparkAges[i] += Time.deltaTime;
                var progress = Mathf.Clamp01(_sparkAges[i] / SparkLifetime);
                var travel = _length * (0.08f + progress * 0.85f);
                spark.SetPosition(0, _sparkCenters[i] + _sparkDirections[i] * travel);
                spark.SetPosition(1, _sparkCenters[i] + _sparkDirections[i] *
                    (travel + _length * 0.22f * (1f - progress)));
                var color = Color.Lerp(new Color(1f, 1f, 0.85f), Gold, progress);
                color.a = 1f - progress;
                spark.startColor = spark.endColor = color;
                spark.startWidth = _length * 0.045f * (1f - progress);
                spark.endWidth = 0f;
                spark.enabled = progress < 1f;
            }
            for (var i = 0; i < _rings.Length; i++)
            {
                if (!_rings[i].enabled) continue;
                _ringAges[i] += Time.deltaTime;
                DrawRing(i);
            }
            if (_expired) return;
            for (var i = 0; i < _auras.Length; i++)
            {
                var width = _length * (0.45f + i * 0.3f) *
                    (1f + 0.15f * Mathf.Sin(Time.time * 14f + i) + _flash * 0.5f);
                _auras[i].startWidth = _auras[i].endWidth = width;
            }
        }

        private void CreateAfterimages()
        {
            // 固定数を使い回し、発射中に残像オブジェクトを作り続けない。
            for (var i = 0; i < _afterimages.Length; i++)
            {
                var ghost = new GameObject("ArrowAfterimage");
                ghost.transform.SetParent(transform, false);
                var line = ghost.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = _arrowPoints.Length;
                line.sharedMaterial = _material;
                line.startWidth = line.endWidth = _length * 0.16f;
                line.numCapVertices = line.numCornerVertices = 4;
                line.sortingOrder = 107;
                line.enabled = false;
                _afterimages[i] = line;
            }
        }

        private void UpdateAfterimages()
        {
            for (var i = 0; i < _afterimages.Length; i++)
            {
                var line = _afterimages[i];
                if (!line.enabled) continue;
                _afterimageAges[i] += Time.deltaTime;
                var progress = Mathf.Clamp01(_afterimageAges[i] / AfterimageLifetime);
                var color = Color.Lerp(new Color(1f, 0.9f, 0.35f), Gold, progress);
                color.a = 0.75f * (1f - progress) * (1f - progress);
                line.startColor = line.endColor = color;
                line.enabled = progress < 1f;
            }
        }

        private void LeaveAfterimage()
        {
            var line = _afterimages[_nextAfterimage];
            // ワールド座標で形を保存し、本体が曲がっても過去の位置・向きを保つ。
            for (var i = 0; i < _arrowPoints.Length; i++)
                line.SetPosition(i, transform.TransformPoint(_arrowPoints[i]));
            line.startColor = line.endColor = new Color(1f, 0.9f, 0.35f, 0.75f);
            line.enabled = true;
            _afterimageAges[_nextAfterimage] = 0f;
            _nextAfterimage = (_nextAfterimage + 1) % _afterimages.Length;
        }

        private void CreateEffects()
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.sharedMaterial = _material;
            _trail.time = 0.75f;
            _trail.minVertexDistance = 0.04f;
            _trail.widthCurve = AnimationCurve.Linear(0f, _length * 0.65f, 1f, 0f);
            _trail.startColor = new Color(1f, 0.85f, 0.25f, 0.85f);
            _trail.endColor = new Color(1f, 0.35f, 0.02f, 0f);
            _trail.sortingOrder = 108;
            _trail.numCapVertices = 4;

        }

        private void PlayFlash(Vector3 position)
        {
            _flash = 1f;
            AddRing(position, 1f);
            AddRing(position, 0.65f);
            AddRing(position, 0.4f);
            for (var i = 0; i < 16; i++)
            {
                var index = _nextSpark;
                _nextSpark = (_nextSpark + 1) % _sparks.Length;
                var angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / 16f;
                _sparkCenters[index] = position;
                _sparkDirections[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                _sparkAges[index] = 0f;
                _sparks[index].SetPosition(0, position);
                _sparks[index].SetPosition(1, position + _sparkDirections[index] * _length * 0.22f);
                _sparks[index].startColor = _sparks[index].endColor = new Color(1f, 1f, 0.85f);
                _sparks[index].startWidth = _length * 0.045f;
                _sparks[index].endWidth = 0f;
                _sparks[index].enabled = true;
            }
        }

        private void PlayShake(SimpleShakeSettings settings)
        {
            if (settings == null || Time.unscaledTime < _nextShakeTime) return;
            var rig = FortressCameraRig.Active;
            if (rig == null) return;
            rig.AddModifier(new ArrowImpactShake(settings, _direction));
            _nextShakeTime = Time.unscaledTime + 0.1f;
        }

        // 矢専用。最初から確実に振幅が出る振動にし、広い視点でも同じ割合で揺らす。
        private sealed class ArrowImpactShake : ICameraViewModifier
        {
            private readonly float _amplitude;
            private readonly float _roll;
            private readonly float _frequency;
            private readonly float _duration;
            private readonly Vector2 _direction;
            private float _elapsed;

            public ArrowImpactShake(SimpleShakeSettings settings, Vector2 direction)
            {
                _amplitude = Mathf.Max(0f, settings.amplitude);
                _roll = Mathf.Max(0f, settings.rollAmplitude);
                _frequency = Mathf.Max(0.1f, settings.frequency);
                _duration = Mathf.Max(0.01f, settings.duration);
                _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
            }

            public bool Apply(ref CameraViewSettings view, float deltaTime)
            {
                var progress = Mathf.Clamp01(_elapsed / _duration);
                var weight = 1f - progress;
                weight *= weight;
                var phase = _elapsed * _frequency * Mathf.PI * 2f;
                var perpendicular = new Vector2(-_direction.y, _direction.x);
                var offset = -_direction * Mathf.Cos(phase)
                    + perpendicular * (Mathf.Sin(phase * 0.73f) * 0.55f);
                var viewScale = Mathf.Max(0.1f, view.VisibleHalfHeight / 14f);
                view.center += offset * (_amplitude * viewScale * weight);
                view.rollDegrees += Mathf.Cos(phase * 0.83f) * _roll * weight;
                _elapsed += Mathf.Max(0f, deltaTime);
                return _elapsed < _duration;
            }
        }

        private void Update()
        {
            UpdateAfterimages();
            UpdateOverdriveEffects();
            if (_expired) return;
            _afterimageTimer += Time.deltaTime;
            if (_afterimageTimer >= AfterimageInterval)
            {
                _afterimageTimer %= AfterimageInterval;
                LeaveAfterimage();
            }
            var step = Mathf.Min(Time.deltaTime, _remainingLife);
            _remainingLife -= step;
            var distance = _speed * step;
            var position = (Vector2)transform.position;
            // 角や狭い隙間で無限反射しない。上限時は残りの移動を捨てる。
            for (var bounce = 0; bounce < 16 && distance > 0f; bounce++)
            {
                Physics2D.Raycast(position, _direction, _filter, _hits, distance);
                var nearest = default(RaycastHit2D);
                foreach (var hit in _hits)
                {
                    if (!IsWall(hit.collider) || hit.fraction == 0f) continue;
                    if (nearest.collider == null || hit.distance < nearest.distance) nearest = hit;
                }

                if (nearest.collider == null)
                {
                    DamageEnemies(position, position + _direction * distance, distance / _speed);
                    position += _direction * distance;
                    break;
                }

                DamageEnemies(position, nearest.point, nearest.distance / _speed);
                position = nearest.point + nearest.normal * Skin;
                // 反射点も軌跡に加え、角を斜めに横切る線を防ぐ。
                _trail.AddPosition(new Vector3(position.x, position.y, transform.position.z));
                PlayFlash(new Vector3(position.x, position.y, transform.position.z));
                PlayShake(_bounceShake);
                distance = Mathf.Max(0f, distance - nearest.distance - Skin);
                _direction = Vector2.Reflect(_direction, nearest.normal).normalized;
            }

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Orient();
            UpdateFlightEffects();
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 6f);
            var pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.06f + _flash * 0.35f;
            _glow.startWidth = _glow.endWidth = _length * 0.32f * pulse;
            if (_remainingLife <= 0f)
            {
                PlayFlash(transform.position);
                _body.enabled = _glow.enabled = false;
                _core.enabled = false;
                foreach (var lightning in _lightning) lightning.enabled = false;
                foreach (var glint in _glints) glint.enabled = false;
                foreach (var halo in _halos) halo.enabled = false;
                foreach (var speedLine in _speedLines) speedLine.enabled = false;
                foreach (var aura in _auras) aura.enabled = false;
                _trail.emitting = false;
                _expired = true;
                // 軌跡と光の輪がフェードしてからマテリアルごと片付ける。
                Destroy(gameObject, 0.9f);
            }
        }

        private void DamageEnemies(Vector2 from, Vector2 to, float duration)
        {
            if (!FortressNet.HasSimulationAuthority || _damagePerSecond <= 0f || duration <= 0f)
                return;

            // 本体の長さ・矢じりの幅と、このフレームに進んだ区間をまとめて判定。
            // 反射の前後は別区間として扱い、壁をまたぐ直線を作らない。
            var start = from - _direction * _length;
            var radius = _length * 0.3f;
            var segment = to - start;
            var angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
            var filter = new ContactFilter2D { useTriggers = true };
            Physics2D.OverlapCapsule((start + to) * 0.5f,
                new Vector2(segment.magnitude + radius * 2f, radius * 2f),
                CapsuleDirection2D.Horizontal, angle, filter, _enemyHits);
            _damagedEnemies.Clear();
            foreach (var collider in _enemyHits)
            {
                var enemy = collider.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.isActiveAndEnabled || !_damagedEnemies.Add(enemy)) continue;
                enemy.ApplyLaserDamage(_damagePerSecond * duration,
                    _attacker != null ? _attacker.tuning : null, _attacker);
            }

            // 群衆の敵はColliderを持たないので、既存の線分ダメージ処理へ渡す。
            // ジョブの時間上限も考慮し、通常の敵と同じダメージ量にする。
            var swarm = SwarmSystem.Current;
            if (swarm == null || swarm.IsReplica || swarm.ActiveSettings == null) return;
            var jobDelta = Mathf.Min(Time.deltaTime, swarm.ActiveSettings.maxFrameDelta);
            if (jobDelta <= 0f) return;
            swarm.QueueBeam(start, to, radius, _damagePerSecond * duration / jobDelta,
                0, _attacker != null ? _attacker.playerIndex : -1);
        }

        private static bool IsWall(Collider2D collider)
        {
            if (collider == null || collider.isTrigger
                || collider.GetComponentInParent<EnemyController>() != null
                || collider.GetComponentInParent<CoreCrystalController>() != null
                || collider.GetComponentInParent<LaserTurret>() != null
                || collider.GetComponentInParent<SmashBallModule>() != null) return false;
            var obstacle = collider.GetComponentInParent<NavigationObstacle>();
            return obstacle == null || obstacle.mode == NavigationObstacleMode.Block;
        }

        private void Orient() => transform.rotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg - 90f);

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
