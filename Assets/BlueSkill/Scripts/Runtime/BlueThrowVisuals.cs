using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 投げる側の見た目一式（<see cref="BlueVortexSkillRunner"/> が1つ持つ）。
    /// ・溜め（使える状態）: 砲台の上にうがい薬のボトルを持つ（ふわふわ・ちゃぷちゃぷ）。何本か持つときは扇形に並べる。
    ///   後ろに青い光、足元に小さな渦、まわりを回りながらボトルに吸い込まれる水の粒、浮かぶ泡、足元から広がる波紋、
    ///   落ちる場所の目印（点線の輪＋放物線の点線。次の攻撃で投げる本数ぶん）。
    /// ・振りかぶり: ボトルを後ろへ引いて震わせ、溜めの演出を強める。
    /// ・投げる: ボトルが回りながら放物線を描いて飛ぶ（本当にカメラの方向へ持ち上がる）。床に影、こぼれる水の粒、まわりの光。
    ///   飛んでいるボトルは「持っているボトル」とは別に動くので、「1本ずつ順番に」では1本飛んでいる間も残りを持ったままになる。
    /// 落ちた後の破裂と渦は <see cref="BlueVortexArea"/> が出す。
    /// 作った物は保存されない一時オブジェクト（シーンの一番上に "[BlueSkill] Pn" として出る）。
    /// </summary>
    internal sealed class BlueThrowVisuals : IDisposable
    {
        private enum Mode
        {
            Hidden,
            Ready,
            Windup
        }

        /// <summary>持っているボトル1本ぶん（と、その落ちる場所の目印）。</summary>
        private sealed class HeldBottle
        {
            public BlueBottle Bottle;
            public BlueGlow Marker;
            public BlueGlow MarkerCore;
            public Vector3 Position;
            public Vector2 Up = Vector2.up;

            public void HideAll()
            {
                Bottle.Hide();
                Marker.Hide();
                MarkerCore.Hide();
            }
        }

        /// <summary>飛んでいるボトル1本ぶん（使い回す）。</summary>
        private sealed class FlightBottle
        {
            public int Id;
            public bool Active;
            public BlueBottle Bottle;
            public BlueGlow Trail;
            public Vector3 From;
            public Vector3 To;
            public float FromHeight;
            public Vector2 FlightUp;
            public Vector2 Up;
            public Vector3 Position;
            public float Delay;
            public float Elapsed;
            public bool Thrown;
            public bool SplashOnRelease;

            public void Hide()
            {
                Active = false;
                Bottle.Hide();
                Trail.Hide();
            }
        }

        private readonly BlueVortexSkill _def;
        private readonly LaserTurret _turret;
        private readonly GameObject _root;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private readonly Material _glowMaterial;
        private readonly Material _behindGlowMaterial;
        private readonly Material _underGlowMaterial;
        private readonly Material _dropMaterial;
        private readonly Material _bubbleMaterial;
        private readonly List<HeldBottle> _held = new List<HeldBottle>(BlueMultiThrow.MaxCount);
        private readonly List<FlightBottle> _flights = new List<FlightBottle>(BlueMultiThrow.MaxCount * 2);
        private readonly List<Vector3> _landings = new List<Vector3>(BlueMultiThrow.MaxCount);
        private readonly BlueGlow _aura;
        private readonly BlueVortexVisual _miniVortex;
        private readonly BlueRingPool _rings;
        private readonly BlueRingPool _ripples;
        private readonly ParticleSystem _droplets;
        private readonly ParticleSystem _bubbles;
        private readonly ParticleSystem _sparkles;
        private readonly ParticleSystem _dots;
        private readonly BlueSpiralParticles _orbit;
        private ParticleSystem.Particle[] _dotBuffer = new ParticleSystem.Particle[16];

        private Mode _mode = Mode.Hidden;
        private int _heldCount = 1;
        private int _nextFlightId = 1;
        private float _appear;
        private float _time;
        private float _windup01;
        private float _miniRotation;
        private float _orbitCarry;
        private float _bubbleCarry;
        private float _sparkleCarry;
        private float _trailCarry;
        private float _rippleTimer;
        private Vector3 _centerPosition;

        public BlueThrowVisuals(BlueVortexSkill definition, LaserTurret turret)
        {
            _def = definition;
            _turret = turret;
            _root = new GameObject("[BlueSkill] P" + (turret.playerIndex + 1)) { hideFlags = HideFlags.DontSave };
            _root.AddComponent<BillboardIgnore>();
            var parent = _root.transform;
            var shaders = definition.shaders;
            var overlay = definition.look.overlaySortingOrder;

            _glowMaterial = BlueFx.CreateMaterial(shaders.glow, BlueFx.GlowShaderName, BlueFx.OverlayRenderQueue);
            // ボトル（SpriteRenderer = 3000）より先に描く光（ボトルの後ろに見える）。
            _behindGlowMaterial = BlueFx.CreateMaterial(shaders.glow, BlueFx.GlowShaderName, BlueFx.BehindSpriteRenderQueue);
            _underGlowMaterial = BlueFx.CreateMaterial(shaders.glow, BlueFx.GlowShaderName, definition.look.vortexRenderQueue + 1);
            _dropMaterial = BlueFx.CreateMaterial(shaders.particle, BlueFx.ParticleShaderName, BlueFx.OverlayRenderQueue);
            _bubbleMaterial = BlueFx.CreateMaterial(shaders.particle, BlueFx.ParticleShaderName, BlueFx.OverlayRenderQueue);
            _bubbleMaterial.SetFloat(BlueFx.ModeId, 1f);
            ApplyBrightness();

            EnsureHeld(1);
            _aura = new BlueGlow(BlueFx.CreateRenderer(parent, "Aura", BlueFx.SharedQuad, _behindGlowMaterial, definition.bottle.sortingOrder - 1));
            _miniVortex = new BlueVortexVisual(parent, "MiniVortex", shaders.vortex, definition.look, 0);
            _rings = new BlueRingPool(parent, "Ring", _glowMaterial, overlay + 3, 8);
            _ripples = new BlueRingPool(parent, "Ripple", _underGlowMaterial, 2, 6);

            _droplets = BlueFx.CreateParticles(parent, "Droplets", _dropMaterial, overlay + 2, 0.06f, 1.3f, 1200, 1.8f, 0f);
            _bubbles = BlueFx.CreateParticles(parent, "Bubbles", _bubbleMaterial, overlay + 1, 0f, 0f, 400, 0.6f, 0f);
            _sparkles = BlueFx.CreateParticles(parent, "Sparkles", _dropMaterial, overlay + 3, 0f, 0f, 400, 0f, 0f);
            _dots = BlueFx.CreateParticles(parent, "MarkerDots", _dropMaterial, overlay, 0f, 0f, 40 * BlueMultiThrow.MaxCount, 0f, 0f);
            var dotColor = _dots.colorOverLifetime;
            dotColor.enabled = false;
            var dotSize = _dots.sizeOverLifetime;
            dotSize.enabled = false;
            var orbitSystem = BlueFx.CreateParticles(parent, "OrbitDroplets", _dropMaterial, definition.bottle.sortingOrder + 1, 0.05f, 1.2f, 300, 0f, 0f);
            _orbit = new BlueSpiralParticles(orbitSystem, 300);
        }

        /// <summary>持っているボトル（真ん中）の位置。持っていなければ発射口。効果音の位置に使う。</summary>
        public Vector3 BottlePosition => CenterHeld.Bottle.IsShown ? _centerPosition : _turret.MuzzlePosition;

        private int CenterIndex => (_heldCount - 1) / 2;

        private HeldBottle CenterHeld => _held[Mathf.Clamp(CenterIndex, 0, _held.Count - 1)];

        public void SetVisible(bool visible)
        {
            if (_root == null || _root.activeSelf == visible)
            {
                return;
            }

            _root.SetActive(visible);
            if (visible)
            {
                _droplets.Play();
                _bubbles.Play();
                _sparkles.Play();
                _dots.Play();
            }
        }

        /// <summary>
        /// 溜め（使える状態）の見た目を出す／消す。announce = 使えるようになった瞬間の輪・しぶきを出すか
        /// （「1本ずつ順番に」で次の1本を構え直すときは出さない）。
        /// </summary>
        public void SetReady(bool ready, bool announce)
        {
            if (ready)
            {
                if (_mode == Mode.Ready)
                {
                    return;
                }

                _mode = Mode.Ready;
                _windup01 = 0f;
                if (!announce)
                {
                    return;
                }

                var charge = _def.charge;
                var at = _turret.transform.position + new Vector3(0f, 0f, -0.05f);
                if (charge.readyBurstSize > 0f)
                {
                    _rings.Spawn(at, 0.5f, charge.readyBurstSize, 0.6f, charge.auraColor, 0.07f);
                    _rings.Spawn(at, 0.3f, charge.readyBurstSize * 0.7f, 0.5f, _def.look.foamColor, 0.05f, 0.08f);
                }

                for (var i = 0; i < 36; i++)
                {
                    var dir = BlueFx.RandomDirection();
                    var speed = UnityEngine.Random.Range(3f, 8f);
                    BlueFx.Emit(_droplets, at, new Vector3(dir.x * speed, dir.y * speed, -UnityEngine.Random.Range(1f, 4f)),
                        UnityEngine.Random.Range(0.1f, 0.24f), UnityEngine.Random.Range(0.35f, 0.7f), charge.particleColor);
                }

                for (var i = 0; i < 12; i++)
                {
                    var p = at + (Vector3)(UnityEngine.Random.insideUnitCircle * 1.2f);
                    BlueFx.Emit(_bubbles, p, new Vector3(0f, 0f, -UnityEngine.Random.Range(0.8f, 2f)), UnityEngine.Random.Range(0.15f, 0.35f),
                        UnityEngine.Random.Range(0.6f, 1.2f), charge.particleColor);
                }
            }
            else if (_mode == Mode.Ready)
            {
                // 投げずに取り消されたとき（キャラの差し替えなど）。ボトルは小さくなって消える。
                _mode = Mode.Hidden;
            }
        }

        public void BeginWindup()
        {
            _mode = Mode.Windup;
            _windup01 = 0f;
            if (_appear < 0.6f)
            {
                _appear = 0.6f;
            }
        }

        public void UpdateWindup(float t01)
        {
            _windup01 = Mathf.Clamp01(t01);
        }

        /// <summary>
        /// 投げる。i 本目のボトルは targets[i]（床の上）へ飛ぶ。持っている本数と targets の数が同じなら（まとめて一気に）
        /// それぞれのボトルが、違えば真ん中のボトルが飛ぶ。真ん中から外側へ順に stagger 秒ずつ遅れて手を離れる。
        /// keepHolding = 残りのボトルを持ったままにするか（「1本ずつ順番に」でまだ残っているとき）。
        /// 飛んだボトルの番号を flightIds に入れる（<see cref="IsDue"/> / <see cref="Land"/> / <see cref="FlightTransform"/> に使う）。
        /// </summary>
        public void Throw(IReadOnlyList<Vector3> targets, float stagger, bool keepHolding, List<int> flightIds)
        {
            flightIds.Clear();
            var count = targets.Count;
            var fromEach = count == _heldCount && count > 1;
            var center = (count - 1) * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var source = fromEach ? _held[i] : CenterHeld;
                var from = source.Bottle.IsShown ? source.Position : HeldPosition(0f);
                var flight = RentFlight();
                var groundZ = targets[i].z;
                flight.FromHeight = Mathf.Max(0f, groundZ - from.z);
                flight.From = new Vector3(from.x, from.y, groundZ);
                flight.To = targets[i];
                flight.FlightUp = source.Up;
                flight.Up = source.Up;
                flight.Position = from;
                flight.Delay = count > 1 ? Mathf.Ceil(Mathf.Abs(i - center)) * Mathf.Max(0f, stagger) : 0f;
                flight.Elapsed = 0f;
                flight.Thrown = false;
                flight.SplashOnRelease = count > 1 && Mathf.Abs(i - center) > 0.01f;
                flight.Active = true;
                flightIds.Add(flight.Id);
            }

            // 手元のしぶき（真ん中）と輪
            var at = CenterHeld.Bottle.IsShown ? _centerPosition : HeldPosition(0f);
            HandSplash(at, _def.burst.throwSplashCount);
            var ground = count > 0 ? targets[0].z : _turret.transform.position.z;
            _rings.Spawn(new Vector3(at.x, at.y, ground - 0.05f), 0.4f, 3f, 0.35f, _def.charge.auraColor, 0.07f);

            _mode = Mode.Hidden;
            if (!keepHolding)
            {
                _appear = 0f;
                for (var i = 0; i < _held.Count; i++)
                {
                    _held[i].HideAll();
                }

                _aura.Hide();
                _dots.Clear();
            }
        }

        /// <summary>そのボトルが落ちる時刻になったか。</summary>
        public bool IsDue(int flightId)
        {
            var flight = FindFlight(flightId);
            return flight == null || flight.Elapsed >= flight.Delay + Mathf.Max(0.01f, _def.timing.flightSeconds);
        }

        /// <summary>そのボトルが落ちた（消す）。</summary>
        public void Land(int flightId)
        {
            FindFlight(flightId)?.Hide();
        }

        /// <summary>飛んでいるボトルの Transform（「飛んでいる間（ループ）」を付けていく先）。</summary>
        public Transform FlightTransform(int flightId)
        {
            var flight = FindFlight(flightId);
            return flight != null ? flight.Bottle.Transform : null;
        }

        /// <summary>途中で止められたとき。持っているボトルも飛んでいるボトルもすべて消す。</summary>
        public void StopAll()
        {
            _mode = Mode.Hidden;
            _appear = 0f;
            for (var i = 0; i < _held.Count; i++)
            {
                _held[i].HideAll();
            }

            for (var i = 0; i < _flights.Count; i++)
            {
                _flights[i].Hide();
            }

            _aura.Hide();
            _miniVortex.Hide();
            _orbit.Clear();
            _dots.Clear();
        }

        /// <summary>
        /// 毎フレーム。landings = 次の攻撃で投げるボトルの落ちる場所の予想（目印用、左から右の順）、
        /// heldCount = 持っているボトルの数。
        /// </summary>
        public void Tick(float dt, IReadOnlyList<Vector3> landings, int heldCount)
        {
            _time += dt;
            _landings.Clear();
            for (var i = 0; i < landings.Count; i++)
            {
                _landings.Add(landings[i]);
            }

            SyncHeld(heldCount);
#if UNITY_EDITOR
            ApplyBrightness();
#endif
            var showBottle = _mode == Mode.Ready || _mode == Mode.Windup;
            var appearSeconds = Mathf.Max(0.01f, _def.bottle.appearSeconds);
            if (showBottle)
            {
                _appear = Mathf.Min(1f, _appear + dt / appearSeconds);
                TickHeld(dt);
            }
            else
            {
                _appear = Mathf.Max(0f, _appear - dt / appearSeconds);
                TickHidden();
            }

            TickFlights(dt);
            _rings.Tick(dt);
            _ripples.Tick(dt);
            var orbitCenter = CenterHeld.Bottle.IsShown ? _centerPosition : _turret.MuzzlePosition;
            _orbit.Tick(dt, orbitCenter, 0.15f, _mode == Mode.Windup ? 1f + _windup01 * 2f : 1f);
        }

        // ------------------------------------------------------------------

        private Vector2 Aim
        {
            get
            {
                var aim = _turret.AimDirection;
                return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.up;
            }
        }

        private void EnsureHeld(int count)
        {
            var parent = _root.transform;
            while (_held.Count < count)
            {
                var n = _held.Count;
                var suffix = n == 0 ? "" : " " + n;
                _held.Add(new HeldBottle
                {
                    Bottle = new BlueBottle(parent),
                    Marker = new BlueGlow(BlueFx.CreateRenderer(parent, "LandingMarker" + suffix, BlueFx.SharedQuad, _underGlowMaterial, 3)),
                    MarkerCore = new BlueGlow(BlueFx.CreateRenderer(parent, "LandingMarkerCore" + suffix, BlueFx.SharedQuad, _underGlowMaterial, 4))
                });
            }
        }

        /// <summary>持つ本数を合わせる（増えたら作り、減ったら隠す）。</summary>
        private void SyncHeld(int count)
        {
            count = Mathf.Clamp(count, 1, BlueMultiThrow.MaxCount);
            EnsureHeld(count);
            _heldCount = count;
            for (var i = count; i < _held.Count; i++)
            {
                _held[i].HideAll();
            }
        }

        private FlightBottle RentFlight()
        {
            FlightBottle flight = null;
            for (var i = 0; i < _flights.Count; i++)
            {
                if (!_flights[i].Active)
                {
                    flight = _flights[i];
                    break;
                }
            }

            if (flight == null)
            {
                var parent = _root.transform;
                flight = new FlightBottle
                {
                    Bottle = new BlueBottle(parent),
                    Trail = new BlueGlow(BlueFx.CreateRenderer(parent, "TrailGlow " + _flights.Count, BlueFx.SharedQuad, _behindGlowMaterial, _def.bottle.sortingOrder - 1))
                };
                _flights.Add(flight);
            }

            flight.Id = _nextFlightId++;
            return flight;
        }

        private FlightBottle FindFlight(int id)
        {
            for (var i = 0; i < _flights.Count; i++)
            {
                if (_flights[i].Active && _flights[i].Id == id)
                {
                    return _flights[i];
                }
            }

            return null;
        }

        /// <summary>持っているときのボトルの位置（真ん中。pullBack = 後ろへ引く割合 0〜1）。</summary>
        private Vector3 HeldPosition(float pullBack)
        {
            var settings = _def.bottle;
            var aim = Aim;
            var right = new Vector2(aim.y, -aim.x);
            var basePos = (Vector2)_turret.transform.position + aim * (settings.forwardOffset - settings.windupPullBack * pullBack) + right * settings.sideOffset;
            var bob = Mathf.Sin(_time * Mathf.PI * 2f * settings.bobFrequency) * settings.bobAmount;
            var height = settings.holdHeight + bob + 0.35f * pullBack;
            return new Vector3(basePos.x, basePos.y, _turret.transform.position.z - height);
        }

        private void TickHeld(float dt)
        {
            var settings = _def.bottle;
            var charge = _def.charge;
            var windup = _mode == Mode.Windup ? _windup01 : 0f;
            var ease = windup * windup * (3f - 2f * windup);
            var strength = Mathf.Lerp(1f, charge.windupMultiplier, ease);
            var aim = Aim;

            // ボトル（何本か持つときは扇形に並べる。真ん中がいつもの位置）
            var position = HeldPosition(ease);
            if (windup > 0f)
            {
                position += (Vector3)(UnityEngine.Random.insideUnitCircle * settings.windupShake * ease);
            }

            var slosh = Mathf.Sin(_time * Mathf.PI * 2f * 1.7f) * settings.sloshAngle * (1f + 2f * ease);
            var tilt = settings.holdTilt + 35f * ease + slosh;
            var pop = _appear < 1f ? EaseOutBack(_appear) : 1f;
            var scale = pop * (1f + 0.15f * ease);
            _centerPosition = position;

            var right = new Vector2(aim.y, -aim.x);
            var center = (_heldCount - 1) * 0.5f;
            for (var i = 0; i < _heldCount; i++)
            {
                var item = _held[i];
                var offset = i - center;
                var spread = (Vector3)(right * (offset * settings.width * 0.8f) - aim * (Mathf.Abs(offset) * 0.15f));
                item.Position = position + spread + new Vector3(0f, 0f, Mathf.Abs(offset) * 0.04f);
                item.Up = BlueFx.Rotate(aim, tilt - offset * 14f);
                item.Bottle.Show(settings, item.Position, item.Up, scale, Mathf.Clamp01(_appear * 2f));
                item.Bottle.HideShadow();
            }

            // 後ろの光
            var pulse = 1f + charge.pulseAmount * Mathf.Sin(_time * Mathf.PI * 2f * charge.pulseFrequency * (1f + ease * 2f));
            var auraSize = settings.length * charge.auraScale * pulse * pop * (1f + 0.4f * ease) * (1f + 0.2f * (_heldCount - 1));
            if (auraSize > 0.01f)
            {
                var auraColor = charge.auraColor;
                auraColor.a *= _appear * (0.8f + 0.4f * ease);
                _aura.Show(position + new Vector3(0f, 0f, 0.02f), 0f, new Vector2(auraSize, auraSize));
                _aura.Apply(_block, auraColor, new Vector4(0.9f, 2.2f, 10f, 0.25f + 0.5f * ease), new Vector4(3f, _time * (0.6f + 3f * ease), 0.62f, 0.05f),
                    new Vector4(0.35f + 0.4f * ease, 0f, 1.7f, 0.6f));
            }
            else
            {
                _aura.Hide();
            }

            // 足元の小さな渦
            var floor = _turret.transform.position + new Vector3(0f, 0f, -_def.look.floorLift);
            var dirSign = _def.debuff.clockwise ? 1f : -1f;
            _miniRotation += dirSign * _def.look.spinSpeed * Mathf.PI * 2f * (1.5f + 4f * ease) * dt;
            var miniRadius = charge.miniVortexRadius * pop * (1f + 0.25f * ease);
            if (miniRadius > 0.01f)
            {
                _miniVortex.Draw(_def.look, floor, miniRadius, _miniRotation, _time, 1f + ease, _def.debuff.clockwise, ease * 0.4f,
                    charge.miniVortexOpacity * _appear, 1f + ease, 0);
            }
            else
            {
                _miniVortex.Hide();
            }

            // まわりを回りながらボトルに吸い込まれる水の粒
            var orbitCount = BlueFx.Accumulate(ref _orbitCarry, charge.orbitRate * strength * _appear * dt);
            for (var i = 0; i < orbitCount; i++)
            {
                var life = UnityEngine.Random.Range(0.7f, 1.1f) / (1f + ease);
                var radius = charge.orbitRadius * UnityEngine.Random.Range(0.75f, 1.15f);
                _orbit.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f), radius, -dirSign * UnityEngine.Random.Range(2.2f, 3.4f), radius / life,
                    life, UnityEngine.Random.Range(0.1f, 0.2f), charge.particleColor, UnityEngine.Random.Range(-0.1f, 0.25f));
            }

            // 浮かぶ泡（足元から）
            var bubbleCount = BlueFx.Accumulate(ref _bubbleCarry, charge.bubbleRate * strength * _appear * dt);
            for (var i = 0; i < bubbleCount; i++)
            {
                var p = floor + (Vector3)(UnityEngine.Random.insideUnitCircle * Mathf.Max(0.4f, charge.miniVortexRadius));
                BlueFx.Emit(_bubbles, p, new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(-0.2f, 0.2f), -UnityEngine.Random.Range(0.7f, 1.5f)),
                    UnityEngine.Random.Range(0.1f, 0.26f), UnityEngine.Random.Range(0.8f, 1.4f), charge.particleColor);
            }

            // ボトルのまわりのきらめき
            var sparkleCount = BlueFx.Accumulate(ref _sparkleCarry, 18f * strength * _appear * dt);
            for (var i = 0; i < sparkleCount; i++)
            {
                var p = position + (Vector3)(UnityEngine.Random.insideUnitCircle * settings.length * (0.7f + 0.3f * (_heldCount - 1)));
                BlueFx.Emit(_sparkles, p, new Vector3(0f, 0f, -0.4f), UnityEngine.Random.Range(0.06f, 0.14f), UnityEngine.Random.Range(0.25f, 0.5f), Color.white);
            }

            // 足元から広がる波紋
            if (charge.rippleInterval > 0f)
            {
                _rippleTimer -= dt * strength;
                if (_rippleTimer <= 0f)
                {
                    _rippleTimer = charge.rippleInterval;
                    _ripples.Spawn(floor, 0.4f, Mathf.Max(1f, charge.miniVortexRadius) * 2.6f, 1f, charge.auraColor * 0.6f, 0.05f);
                }
            }

            DrawMarkers(ease);
        }

        private void TickHidden()
        {
            if (_appear > 0f)
            {
                var settings = _def.bottle;
                for (var i = 0; i < _heldCount; i++)
                {
                    var item = _held[i];
                    item.Bottle.Show(settings, item.Position, item.Up, _appear, _appear);
                    item.Marker.Hide();
                    item.MarkerCore.Hide();
                }

                _miniVortex.Draw(_def.look, _turret.transform.position + new Vector3(0f, 0f, -_def.look.floorLift), _def.charge.miniVortexRadius * _appear,
                    _miniRotation, _time, 1f, _def.debuff.clockwise, 0f, _def.charge.miniVortexOpacity * _appear, 1f, 0);
            }
            else
            {
                for (var i = 0; i < _held.Count; i++)
                {
                    _held[i].HideAll();
                }

                _miniVortex.Hide();
            }

            _aura.Hide();
            if (_dots.particleCount > 0)
            {
                _dots.Clear();
            }
        }

        private void TickFlights(float dt)
        {
            var settings = _def.bottle;
            var timing = _def.timing;
            var burst = _def.burst;
            var flightSeconds = Mathf.Max(0.01f, timing.flightSeconds);
            var spin = -360f * timing.spinTurns * (_def.debuff.clockwise ? -1f : 1f);
            var active = 0;
            for (var i = 0; i < _flights.Count; i++)
            {
                if (_flights[i].Active)
                {
                    active++;
                }
            }

            for (var i = 0; i < _flights.Count; i++)
            {
                var flight = _flights[i];
                if (!flight.Active)
                {
                    continue;
                }

                flight.Elapsed += dt;
                if (!flight.Thrown && flight.Elapsed >= flight.Delay)
                {
                    flight.Thrown = true;
                    if (flight.SplashOnRelease)
                    {
                        // 真ん中以外のボトルが手を離れた瞬間の小さなしぶき（真ん中は Throw で出している）。
                        HandSplash(flight.Position, burst.throwSplashCount / 3);
                    }
                }

                var t = Mathf.Clamp01((flight.Elapsed - flight.Delay) / flightSeconds);
                var ground = Vector3.Lerp(flight.From, flight.To, t);
                var height = flight.FromHeight * (1f - t) + 4f * timing.arcHeight * t * (1f - t);
                var position = new Vector3(ground.x, ground.y, ground.z - height);
                flight.Up = BlueFx.Rotate(flight.FlightUp, spin * t);
                flight.Position = position;
                flight.Bottle.Show(settings, position, flight.Up, 1f, 1f);
                var shadowSize = settings.width * 1.3f / (1f + height * 0.25f);
                flight.Bottle.ShowShadow(new Vector3(ground.x, ground.y, ground.z - 0.01f), shadowSize, settings.shadowOpacity / (1f + height * 0.3f), 5);

                if (burst.trailGlowSize > 0f)
                {
                    var color = _def.charge.auraColor;
                    color.a *= 0.8f;
                    var size = burst.trailGlowSize * (1f + 0.15f * Mathf.Sin(_time * 30f + i));
                    flight.Trail.Show(position + new Vector3(0f, 0f, 0.02f), 0f, new Vector2(size, size));
                    flight.Trail.ApplySoft(_block, color, 2.2f);
                }
                else
                {
                    flight.Trail.Hide();
                }

                // こぼれる水の粒（床へ落ちていく）。本数が増えても全体の量はあまり増やさない。
                var count = BlueFx.Accumulate(ref _trailCarry, burst.trailRate * dt / Mathf.Sqrt(Mathf.Max(1, active)));
                for (var k = 0; k < count; k++)
                {
                    var jitter = UnityEngine.Random.insideUnitCircle * 0.25f;
                    BlueFx.Emit(_droplets, position + (Vector3)jitter,
                        new Vector3(jitter.x * 3f, jitter.y * 3f, UnityEngine.Random.Range(1f, 4f)),
                        UnityEngine.Random.Range(0.08f, 0.2f), UnityEngine.Random.Range(0.3f, 0.6f), burst.dropletColor);
                }
            }
        }

        private void HandSplash(Vector3 at, int count)
        {
            var aim = Aim;
            for (var i = 0; i < count; i++)
            {
                var dir = BlueFx.Rotate(aim, UnityEngine.Random.Range(-55f, 55f));
                var speed = UnityEngine.Random.Range(4f, 11f);
                BlueFx.Emit(_droplets, at, new Vector3(dir.x * speed, dir.y * speed, -UnityEngine.Random.Range(0f, 3f)),
                    UnityEngine.Random.Range(0.1f, 0.24f), UnityEngine.Random.Range(0.3f, 0.6f), _def.burst.dropletColor);
            }
        }

        /// <summary>落ちる場所の目印（次の攻撃で投げるボトルごとに、点線の輪＋中心の点＋放物線の点線）。</summary>
        private void DrawMarkers(float windup)
        {
            var target = _def.target;
            if (!target.showMarker || _landings.Count == 0 || _appear <= 0f)
            {
                for (var i = 0; i < _held.Count; i++)
                {
                    _held[i].Marker.Hide();
                    _held[i].MarkerCore.Hide();
                }

                _dots.Clear();
                return;
            }

            var color = target.markerColor;
            color.a *= _appear * (0.75f + 0.25f * Mathf.Sin(_time * 5f)) * (1f + windup);
            var diameter = Mathf.Max(0.1f, _def.area.radius * _def.AreaRadiusScale) * 2f;
            var markers = Mathf.Min(_landings.Count, BlueMultiThrow.MaxCount);
            EnsureHeld(markers);
            var dots = Mathf.Clamp(target.markerDots, 0, 40);
            var total = dots * markers;
            if (_dotBuffer.Length < total)
            {
                _dotBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(Mathf.Max(1, total))];
            }

            var fromEach = markers == _heldCount && markers > 1;
            var arc = _def.timing.arcHeight;
            var phase = Mathf.Repeat(_time * 1.2f, 1f);
            var dotColor = color;
            dotColor.a = Mathf.Clamp01(dotColor.a);
            var written = 0;
            for (var k = 0; k < _held.Count; k++)
            {
                var item = _held[k];
                if (k >= markers)
                {
                    item.Marker.Hide();
                    item.MarkerCore.Hide();
                    continue;
                }

                var landing = _landings[k];
                var floor = landing + new Vector3(0f, 0f, -_def.look.floorLift - 0.01f);
                item.Marker.Show(floor, 0f, new Vector2(diameter, diameter));
                item.Marker.ApplyRing(_block, color, 0.9f, 0.025f, 28f, _time * 0.8f * (_def.debuff.clockwise ? -1f : 1f));
                var coreSize = diameter * 0.18f * (1f + 0.2f * Mathf.Sin(_time * 7f + k));
                item.MarkerCore.Show(floor, 0f, new Vector2(coreSize, coreSize));
                item.MarkerCore.Apply(_block, color, new Vector4(0.35f, 2f, 0f, 0f), new Vector4(0f, 0f, 0.75f, 0.06f), new Vector4(1f, 0f, 0f, 0f));

                // 放物線の点線（点が、次に投げるボトルから落ちる場所へ流れていく）
                var start = fromEach ? item.Position : CenterHeld.Position;
                var groundZ = landing.z;
                var fromHeight = Mathf.Max(0f, groundZ - start.z);
                var from = new Vector3(start.x, start.y, groundZ);
                for (var i = 0; i < dots; i++)
                {
                    var t = (i + phase) / (dots + 1f);
                    var ground = Vector3.Lerp(from, landing, t);
                    var h = fromHeight * (1f - t) + 4f * arc * t * (1f - t);
                    var c = dotColor;
                    c.a *= Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - t) * 6f);
                    _dotBuffer[written++] = new ParticleSystem.Particle
                    {
                        position = new Vector3(ground.x, ground.y, ground.z - h),
                        startSize = 0.16f,
                        startColor = c,
                        remainingLifetime = 10f,
                        startLifetime = 10f
                    };
                }
            }

            if (written > 0)
            {
                _dots.SetParticles(_dotBuffer, written);
            }
            else
            {
                _dots.Clear();
            }
        }

        private void ApplyBrightness()
        {
            _dropMaterial.SetFloat(BlueFx.IntensityId, _def.charge.particleBrightness);
            _bubbleMaterial.SetFloat(BlueFx.IntensityId, _def.charge.particleBrightness);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public void Dispose()
        {
            for (var i = 0; i < _held.Count; i++)
            {
                _held[i].Bottle.Dispose();
            }

            for (var i = 0; i < _flights.Count; i++)
            {
                _flights[i].Bottle.Dispose();
            }

            _miniVortex.Dispose();
            if (_root != null)
            {
                BlueFx.Destroy(_root);
            }

            BlueFx.Destroy(_glowMaterial);
            BlueFx.Destroy(_behindGlowMaterial);
            BlueFx.Destroy(_underGlowMaterial);
            BlueFx.Destroy(_dropMaterial);
            BlueFx.Destroy(_bubbleMaterial);
        }
    }
}
