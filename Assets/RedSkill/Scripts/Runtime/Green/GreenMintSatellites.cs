using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// コアの周りを回るタブレット（衛星）と、コアを包むミントの膜、守る範囲の点線の輪。
    /// 流れ: 飛んでくる（ケースの口 → 自分の場所へ、弧を描いて順番に）→ 回る（ふわふわ伸び縮み・撃つと弾む）→ 吸い込まれる（らせんを描いてコアへ）。
    /// </summary>
    internal sealed class GreenMintSatellites
    {
        private enum Phase
        {
            Hidden,
            Launch,
            Orbit,
            End
        }

        private sealed class Satellite
        {
            public SpriteRenderer Sprite;
            public MeshRenderer Glow;
            public TrailRenderer Trail;
            public Vector2 Position;
            public float Punch;
            public float Spin;
            public float LaunchDelay;
            public float ArcSide;
        }

        private const float LaunchStagger = 0.07f;

        private readonly GreenMintFx _fx;
        private readonly Satellite[] _satellites;
        private readonly SpriteRenderer _membrane;
        private readonly MeshRenderer _coreGlow;
        private readonly SpriteRenderer _rangeRing;

        private Phase _phase = Phase.Hidden;
        private float _phaseTime;
        private float _angle;
        private float _time;
        private float _launchSeconds;
        private float _endSeconds;
        private float _presence;
        private Vector2 _launchFrom;

        public GreenMintSatellites(GreenMintFx fx)
        {
            _fx = fx;
            var settings = fx.Definition.satellites;
            var order = settings.sortingOrder;
            _rangeRing = fx.CreateSprite("RangeRing", fx.Art.DashedRing, order - 4);
            _coreGlow = fx.CreateQuad("CoreGlow", fx.RadialMaterial, order - 3);
            _membrane = fx.CreateSprite("Membrane", fx.Art.Membrane, order - 2);

            _satellites = new Satellite[Mathf.Clamp(settings.count, 1, 10)];
            for (var i = 0; i < _satellites.Length; i++)
            {
                var satellite = new Satellite
                {
                    Glow = fx.CreateQuad("SatelliteGlow" + i, fx.RadialMaterial, order - 1),
                    Sprite = fx.CreateSprite("Satellite" + i, fx.Art.Tablet, order),
                    ArcSide = i % 2 == 0 ? 1f : -1f
                };
                satellite.Trail = CreateTrail("SatelliteTrail" + i, order - 1);
                _satellites[i] = satellite;
            }

            Hide();
        }

        public int Count => _satellites.Length;

        /// <summary>撃てる状態か（回っている間だけ）。</summary>
        public bool CanShoot => _phase == Phase.Orbit;

        public Vector2 PositionOf(int index) => _satellites[index].Position;

        /// <summary>ケースの口から飛ばし始める。seconds 後には全員が自分の場所に着く。</summary>
        public void Launch(Vector2 from, float seconds)
        {
            _phase = Phase.Launch;
            _phaseTime = 0f;
            _launchFrom = from;
            _launchSeconds = Mathf.Max(0.05f, seconds);
            for (var i = 0; i < _satellites.Length; i++)
            {
                var satellite = _satellites[i];
                satellite.LaunchDelay = Mathf.Min(i * LaunchStagger, _launchSeconds * 0.45f);
                satellite.Punch = 0f;
                satellite.Spin = Random.Range(0f, 360f);
                satellite.Position = from;
                satellite.Trail.transform.position = new Vector3(from.x, from.y, GreenMintFx.ZOffset);
                satellite.Trail.Clear();
                satellite.Trail.emitting = false;
            }
        }

        /// <summary>着いた瞬間（回り始める）。</summary>
        public void BeginOrbit()
        {
            _phase = Phase.Orbit;
            _phaseTime = 0f;
        }

        /// <summary>らせんを描いてコアへ吸い込まれる。</summary>
        public void BeginEnd(float seconds)
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            _phase = Phase.End;
            _phaseTime = 0f;
            _endSeconds = Mathf.Max(0.05f, seconds);
            for (var i = 0; i < _satellites.Length; i++)
            {
                _satellites[i].Trail.emitting = true;
            }
        }

        /// <summary>撃った衛星を弾ませる。</summary>
        public void Punch(int index)
        {
            _satellites[index].Punch = 1f;
        }

        public void Hide()
        {
            _phase = Phase.Hidden;
            _presence = 0f;
            for (var i = 0; i < _satellites.Length; i++)
            {
                var satellite = _satellites[i];
                satellite.Sprite.enabled = false;
                satellite.Glow.enabled = false;
                satellite.Trail.emitting = false;
                satellite.Trail.Clear();
            }

            _membrane.enabled = false;
            _coreGlow.enabled = false;
            _rangeRing.enabled = false;
        }

        /// <summary>毎フレーム。core = コアの位置、boost = 握る強さ（0〜1）。</summary>
        public void Tick(Vector2 core, float boost, float dt)
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            var settings = _fx.Definition.satellites;
            _phaseTime += dt;
            _time += dt;
            _angle += settings.orbitDegreesPerSecond * (1f + boost * 0.6f) * dt;

            switch (_phase)
            {
                case Phase.Launch:
                    _presence = Mathf.Clamp01(_phaseTime / _launchSeconds);
                    TickLaunch(core);
                    break;
                case Phase.Orbit:
                    _presence = 1f;
                    TickOrbit(core, 1f, 1f);
                    break;
                case Phase.End:
                {
                    var t = Mathf.Clamp01(_phaseTime / _endSeconds);
                    _presence = 1f - t;
                    // らせん: 半径が縮みながら速く回る。
                    _angle += settings.orbitDegreesPerSecond * 4f * t * dt;
                    var radiusScale = 1f - GreenMintUtil.EaseOutCubic(t) * 0.95f;
                    TickOrbit(core, radiusScale, Mathf.Lerp(1f, 0.25f, t));
                    if (t >= 1f)
                    {
                        Hide();
                        return;
                    }

                    break;
                }
            }

            TickMembrane(core, boost);
        }

        private void TickLaunch(Vector2 core)
        {
            var settings = _fx.Definition.satellites;
            for (var i = 0; i < _satellites.Length; i++)
            {
                var satellite = _satellites[i];
                var flight = Mathf.Max(0.05f, _launchSeconds - satellite.LaunchDelay);
                var t = Mathf.Clamp01((_phaseTime - satellite.LaunchDelay) / flight);
                var target = SlotPosition(core, i, 1f);
                if (t <= 0f)
                {
                    satellite.Sprite.enabled = false;
                    satellite.Glow.enabled = false;
                    continue;
                }

                // 弧を描いて飛ぶ（2次ベジェ。横へふくらむ向きは1つおきに逆）。
                var eased = GreenMintUtil.EaseOutCubic(t);
                var mid = (_launchFrom + target) * 0.5f;
                var axis = target - _launchFrom;
                var normal = new Vector2(-axis.y, axis.x).normalized;
                var control = mid + normal * axis.magnitude * 0.45f * satellite.ArcSide;
                var a = Vector2.Lerp(_launchFrom, control, eased);
                var b = Vector2.Lerp(control, target, eased);
                satellite.Position = Vector2.Lerp(a, b, eased);
                satellite.Spin += 900f * (1f - t) * Time.deltaTime;
                satellite.Trail.emitting = true;
                var size = settings.size * Mathf.Lerp(0.45f, 1f, eased);
                Draw(satellite, size, 1f, 0.7f + 0.6f * (1f - t));
            }
        }

        private void TickOrbit(Vector2 core, float radiusScale, float sizeScale)
        {
            var settings = _fx.Definition.satellites;
            var dt = Time.deltaTime;
            for (var i = 0; i < _satellites.Length; i++)
            {
                var satellite = _satellites[i];
                satellite.Position = SlotPosition(core, i, radiusScale);
                satellite.Spin += settings.orbitDegreesPerSecond * 0.6f * dt;
                satellite.Punch = Mathf.Max(0f, satellite.Punch - dt * 6f);
                if (_phase == Phase.Orbit && satellite.Trail.emitting && _phaseTime > 0.25f)
                {
                    satellite.Trail.emitting = false;
                }

                var punch = satellite.Punch * satellite.Punch;
                var size = settings.size * sizeScale * (1f + settings.shotPunch * punch);
                Draw(satellite, size, _phase == Phase.End ? Mathf.Lerp(1f, 0.4f, 1f - radiusScale) : 1f, 1f + punch * 1.6f);
            }
        }

        private Vector2 SlotPosition(Vector2 core, int index, float radiusScale)
        {
            var settings = _fx.Definition.satellites;
            var angle = (_angle + 360f * index / _satellites.Length) * Mathf.Deg2Rad;
            var bob = Mathf.Sin(_time * 2.3f + index * 1.7f) * settings.bobAmount;
            var radius = (settings.orbitRadius + bob) * radiusScale;
            return core + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void Draw(Satellite satellite, float size, float alpha, float glow)
        {
            var palette = _fx.Definition.palette;
            var z = GreenMintFx.ZOffset - 0.03f;
            var transform = satellite.Sprite.transform;
            transform.SetPositionAndRotation(new Vector3(satellite.Position.x, satellite.Position.y, z), Quaternion.Euler(0f, 0f, satellite.Spin));
            transform.localScale = new Vector3(size, size, 1f);
            satellite.Sprite.color = new Color(1f, 1f, 1f, alpha);
            satellite.Sprite.enabled = size > 0.01f;
            satellite.Trail.transform.position = new Vector3(satellite.Position.x, satellite.Position.y, z + 0.004f);

            var glowSize = size * 2.1f;
            satellite.Glow.transform.SetPositionAndRotation(new Vector3(satellite.Position.x, satellite.Position.y, z + 0.005f), Quaternion.identity);
            satellite.Glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
            _fx.ApplyRadial(satellite.Glow, palette.glow * (0.45f * glow * alpha),
                new Vector4(0.6f, 2.2f, 0f, 0f), new Vector4(4f, 0f, 0.55f, 0.06f), new Vector4(0.5f, 0f, 0f, 0f));
            satellite.Glow.enabled = size > 0.01f;
        }

        private void TickMembrane(Vector2 core, float boost)
        {
            var settings = _fx.Definition.satellites;
            var palette = _fx.Definition.palette;
            var presence = Mathf.SmoothStep(0f, 1f, _presence);
            var breathe = 1f + 0.035f * Mathf.Sin(_time * 3.1f) + boost * 0.05f;
            var up = GreenMintUtil.ScreenUp;

            var size = settings.membraneRadius * 2f * breathe * Mathf.Lerp(0.6f, 1f, presence);
            _membrane.transform.SetPositionAndRotation(new Vector3(core.x, core.y, GreenMintFx.ZOffset - 0.01f), GreenMintUtil.FacingUp(up));
            _membrane.transform.localScale = new Vector3(size, size, 1f);
            var tint = Color.Lerp(palette.mint, palette.green, 0.25f);
            tint.a = settings.membraneOpacity * presence;
            _membrane.color = tint;
            _membrane.enabled = presence > 0.001f;

            var glowSize = settings.membraneRadius * 3.4f * breathe;
            _coreGlow.transform.SetPositionAndRotation(new Vector3(core.x, core.y, GreenMintFx.ZOffset), Quaternion.identity);
            _coreGlow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
            _fx.ApplyRadial(_coreGlow, palette.glow * (0.35f + boost * 0.25f) * presence,
                new Vector4(0.7f, 1.8f, 12f, 0.18f), new Vector4(3f, _time * 0.35f, 0.62f, 0.05f), new Vector4(0.6f, 0f, 5f, 0.5f));
            _coreGlow.enabled = presence > 0.001f;

            if (settings.showRangeRing)
            {
                var range = _fx.Definition.attack.range * 2f;
                var ringScale = range * Mathf.Lerp(0.85f, 1f, presence);
                _rangeRing.transform.SetPositionAndRotation(new Vector3(core.x, core.y, GreenMintFx.ZOffset + 0.04f), Quaternion.Euler(0f, 0f, _time * 6f));
                _rangeRing.transform.localScale = new Vector3(ringScale, ringScale, 1f);
                var ringColor = palette.mint;
                ringColor.a = settings.rangeRingOpacity * presence * (0.85f + 0.15f * Mathf.Sin(_time * 2f));
                _rangeRing.color = ringColor;
                _rangeRing.enabled = presence > 0.001f;
            }
            else
            {
                _rangeRing.enabled = false;
            }
        }

        /// <summary>光の尾（Renderer は1つの物に1つしか付けられないので、タブレットとは別の物にして位置だけ合わせる）。</summary>
        private TrailRenderer CreateTrail(string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_fx.Root, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _fx.TrailMaterial;
            trail.time = 0.22f;
            trail.minVertexDistance = 0.08f;
            trail.widthMultiplier = _fx.Definition.satellites.size * 0.7f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.colorGradient = TrailGradient(_fx.Definition.palette);
            trail.sortingOrder = sortingOrder;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        public static Gradient TrailGradient(GreenMintPalette palette)
        {
            var gradient = new Gradient();
            var mint = palette.mint;
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(mint, Color.white, 0.4f), 0f), new GradientColorKey(palette.green, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
