using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 極太レーザーの見た目一式（<see cref="RedLaserSkillRunner"/> が1つ持つ）。プレハブや画像は使わず、
    /// 専用シェーダー3つ（Beam / Radial / Particle）とコードで作る:
    /// ・レーザー本体: 根元から先へ広がる帯のメッシュ。横に分けた筋（<see cref="RedLaserLanes"/>）ごとに長さが違い、
    ///   壁に当たった筋だけがそこで終わる。断面・長さのグラデーション、流れるスジ、荒れたふち。
    /// ・レーザー本体の色: 斜めのしま模様（しま模様のグラデーション）× 断面の色 × 長さ方向の色。しまは先へ流れる。
    /// ・光（溜め・発射口・当たった場所・壁に沿って広がる光）、衝撃の輪: 四角い板1枚に Radial シェーダーで描く。
    /// ・火花・トゲ状の光・光の粒・レーザーに沿って走る光の線: パーティクルをコードから1粒ずつ出す。
    /// ・溜めの歯磨き粉: <see cref="RedLaserTube"/>。使える間〜撃ち終わるまで砲台の上に乗り、レーザー・発射口の演出はその口（<see cref="FirePoint"/>）から出る。
    /// ・ビリビリ（赤い稲妻）: <see cref="RedLaserLightning"/>（LineRenderer の使い回し）。溜め中・ため・撃っている間・当たった場所。
    /// 作った物は保存されない一時オブジェクト（シーンの一番上に "[RedSkill] Pn" として出る）。
    /// </summary>
    internal sealed class RedLaserVisuals : IDisposable
    {
        private const string BeamShaderName = "MS2026/RedSkill/Beam";
        private const string RadialShaderName = "MS2026/RedSkill/Radial";
        private const string ParticleShaderName = "MS2026/RedSkill/Particle";
        private const int RingPoolSize = 8;
        private const int MaxImpactAreas = 4;
        private const int BeamRingPoolSize = 12;
        private const float ZOffset = -0.05f;

        private static readonly int CrossTexId = Shader.PropertyToID("_CrossTex");
        private static readonly int LengthTexId = Shader.PropertyToID("_LengthTex");
        private static readonly int BeamParamsId = Shader.PropertyToID("_BeamParams");
        private static readonly int StreakParamsId = Shader.PropertyToID("_StreakParams");
        private static readonly int JagParamsId = Shader.PropertyToID("_JagParams");
        private static readonly int StripeTexId = Shader.PropertyToID("_StripeTex");
        private static readonly int StripeParamsId = Shader.PropertyToID("_StripeParams");
        private static readonly int ShineParamsId = Shader.PropertyToID("_ShineParams");
        private static readonly int PulseParamsId = Shader.PropertyToID("_PulseParams");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
        private static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
        private static readonly int ShapeCId = Shader.PropertyToID("_ShapeC");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int LineModeId = Shader.PropertyToID("_LineMode");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private const int LightningPoolSize = 64;

        private static Mesh _sharedQuad;

        private readonly RedLaserSkill _def;
        private readonly LaserTurret _turret;
        private readonly GameObject _root;
        private readonly Material _beamMaterial;
        private readonly Material _radialMaterial;
        private readonly Material _particleMaterial;
        private readonly Material _lightningMaterial;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private readonly MeshRenderer _beamRenderer;
        private readonly Mesh _beamMesh;
        private readonly StripMesh _beamStrips;
        private readonly Material _haloMaterial;
        private readonly MeshRenderer _haloRenderer;
        private readonly Mesh _haloMesh;
        private readonly StripMesh _haloStrips;
        private Texture2D _haloCrossTexture;
        private readonly Gradient _haloGradient = new Gradient();
        private readonly Glow _muzzleFlare2;
        private readonly BeamRing[] _beamRings = new BeamRing[BeamRingPoolSize];
        private float _beamRingTimer;
        private RedLaserLanes _lanes;
        private readonly List<int> _blockedLanes = new List<int>(48);
        private readonly ImpactArea[] _impactAreas = new ImpactArea[MaxImpactAreas];
        private int _impactAreaCount;

        private readonly Glow _chargeGlow;
        private readonly Glow _muzzleFlare;
        private readonly Glow[] _impactFlares = new Glow[MaxImpactAreas];
        private readonly Glow[] _impactSplashes = new Glow[MaxImpactAreas];
        private readonly Ring[] _rings = new Ring[RingPoolSize];

        private readonly ParticleSystem _sparks;
        private readonly ParticleSystem _shards;
        private readonly ParticleSystem _embers;
        private readonly ParticleSystem _speedLines;
        private readonly RedLaserLightning _lightning;
        private readonly RedLaserTube _tube;
        private readonly Glow _tubeAura;
        private bool _firing;
        private float _fireTime;
        private float _tubeBoltCarry;
        private float _dripCarry;
        private float _thunderTimer;

        private Texture2D _crossTexture;
        private Texture2D _lengthTexture;
        private Texture2D _stripeTexture;
        private float _rebakeTimer;

        private bool _charging;
        private float _chargeTime;
        private float _windup01 = -1f;
        private float _chargeEmberCarry;
        private float _muzzleSparkCarry;
        private float _shardCarry;
        private float _beamEmberCarry;
        private float _impactSparkCarry;
        private float _impactEmberCarry;
        private float _shockwaveTimer;
        private float _time;
        private float _particleZ;
        private float _chargeBoltCarry;
        private float _beamBoltCarry;
        private float _edgeBoltCarry;
        private float _impactBoltCarry;
        private float _speedLineCarry;
        private float _chargeFlash;

        public RedLaserVisuals(RedLaserSkill definition, LaserTurret turret)
        {
            _def = definition;
            _turret = turret;
            _root = new GameObject("[RedSkill] P" + (turret.playerIndex + 1)) { hideFlags = HideFlags.DontSave };

            _beamMaterial = CreateMaterial(definition.shaders.beam, BeamShaderName);
            _haloMaterial = CreateMaterial(definition.shaders.beam, BeamShaderName);
            _radialMaterial = CreateMaterial(definition.shaders.radial, RadialShaderName);
            _particleMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName);
            _lightningMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName);
            BakeGradients();

            var order = definition.look.sortingOrder;
            _beamMesh = new Mesh { name = "RedSkillBeam" };
            _beamMesh.MarkDynamic();
            _beamRenderer = CreateRenderer("Beam", _beamMesh, _beamMaterial, order);
            _beamStrips = new StripMesh { Mesh = _beamMesh };
            _haloMesh = new Mesh { name = "RedSkillBeamHalo" };
            _haloMesh.MarkDynamic();
            _haloRenderer = CreateRenderer("BeamHalo", _haloMesh, _haloMaterial, HaloSortingOrder);
            _haloStrips = new StripMesh { Mesh = _haloMesh };

            _chargeGlow = new Glow(CreateRenderer("ChargeGlow", SharedQuad, _radialMaterial, order + 1));
            _muzzleFlare = new Glow(CreateRenderer("MuzzleFlare", SharedQuad, _radialMaterial, order + 1));
            _muzzleFlare2 = new Glow(CreateRenderer("MuzzleFlare2", SharedQuad, _radialMaterial, order + 1));
            for (var i = 0; i < _beamRings.Length; i++)
            {
                _beamRings[i] = new BeamRing { Glow = new Glow(CreateRenderer("BeamRing" + i, SharedQuad, _radialMaterial, order + 1)) };
            }
            for (var i = 0; i < MaxImpactAreas; i++)
            {
                _impactSplashes[i] = new Glow(CreateRenderer("ImpactSplash" + i, SharedQuad, _radialMaterial, order + 1));
                _impactFlares[i] = new Glow(CreateRenderer("ImpactFlare" + i, SharedQuad, _radialMaterial, order + 2));
            }
            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i] = new Ring { Glow = new Glow(CreateRenderer("Ring" + i, SharedQuad, _radialMaterial, order + 1)) };
            }

            _sparks = CreateParticles("Sparks", order + 3, 0.035f, 2f, 1500, 2.5f);
            _shards = CreateParticles("Shards", order + 2, 0.09f, 1.5f, 600, 1.2f);
            _embers = CreateParticles("Embers", order + 3, 0f, 0f, 800, 1f);
            _speedLines = CreateParticles("SpeedLines", order + 2, 0.05f, 1f, 600, 0f);
            _lightning = new RedLaserLightning(_root.transform, _lightningMaterial, LightningPoolSize, order + 3);
            _tube = new RedLaserTube(definition, turret, _root.transform);
            _tubeAura = new Glow(CreateRenderer("TubeAura", SharedQuad, _radialMaterial, definition.tube.sortingOrder - 1));
            HideAll();
        }

        /// <summary>外側の光を描く順番（レーザーの上に重ねる設定なら本体の1つ上、そうでなければ1つ下）。</summary>
        private int HaloSortingOrder => _def.look.sortingOrder + (_def.flash.haloOnTop ? 1 : -1);

        /// <summary>レーザーが出る場所。歯磨き粉が出ていればその口、出ていなければ砲台の発射口。</summary>
        public Vector3 FirePoint => _tube.IsShown ? _tube.NozzlePosition : _turret.MuzzlePosition;

        private static Mesh SharedQuad
        {
            get
            {
                if (_sharedQuad != null)
                {
                    return _sharedQuad;
                }

                _sharedQuad = new Mesh
                {
                    name = "RedSkillQuad",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                    },
                    uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                    triangles = new[] { 0, 2, 1, 2, 3, 1 }
                };
                _sharedQuad.RecalculateBounds();
                return _sharedQuad;
            }
        }

        /// <summary>溜め（使える状態）の見た目を出す／消す。出した瞬間に輪と火花を広げる。</summary>
        public void SetCharging(bool on)
        {
            if (_charging == on)
            {
                return;
            }

            _charging = on;
            _chargeTime = 0f;
            if (on)
            {
                var charge = _def.charge;
                if (charge.readyBurstSize > 0f)
                {
                    SpawnRing(FirePoint, charge.readyBurstSize, 0.6f, charge.glowColor);
                }

                BurstSparks(FirePoint, Vector2.up, 180f, 30, _def.muzzle.sparkSpeed * 0.6f, _def.charge.emberColor);
                BurstLightning(FirePoint, Vector2.up, 180f, 8, _def.lightning.chargeRadius);
            }
        }

        /// <summary>見た目をまとめて出す／隠す（砲台が非アクティブになったとき用）。</summary>
        public void SetVisible(bool visible)
        {
            if (_root == null || _root.activeSelf == visible)
            {
                return;
            }

            _root.SetActive(visible);
            if (visible)
            {
                // 非アクティブにするとパーティクルは止まるので、もう一度動かす（自分では湧かない設定なので Play しても何も出ない）。
                _sparks.Play();
                _shards.Play();
                _embers.Play();
                _speedLines.Play();
            }
        }

        public void BeginWindup()
        {
            _windup01 = 0f;
        }

        public void UpdateWindup(float t01)
        {
            _windup01 = t01;
        }

        public void BeginFire()
        {
            _windup01 = -1f;
            _shockwaveTimer = 0f;
            _firing = true;
            _fireTime = 0f;
            _tube.Kick();
            var muzzle = _def.muzzle;
            if (muzzle.fireBurstSize > 0f)
            {
                SpawnRing(FirePoint, muzzle.fireBurstSize, 0.45f, muzzle.flareColor);
                SpawnRing(FirePoint, muzzle.fireBurstSize * 0.6f, 0.3f, _def.charge.glowColor);
            }

            BurstSparks(FirePoint, _turret.AimDirection, 100f, 60, muzzle.sparkSpeed * 1.4f, muzzle.particleColor);
            BurstLightning(FirePoint, _turret.AimDirection, 110f, 12, _def.lightning.chargeRadius * 1.3f);
        }

        public void EndFire()
        {
            _windup01 = -1f;
            _firing = false;
            _beamRenderer.enabled = false;
            _haloRenderer.enabled = false;
            _muzzleFlare.Hide();
            _muzzleFlare2.Hide();
            HideBeamRings();
            HideImpacts();
        }

        /// <summary>毎フレーム最初に呼ぶ。溜め・衝撃の輪・グラデーションの焼き直しを進め、レーザー本体は一旦隠す（撃っていれば <see cref="UpdateBeam"/> がまた出す）。</summary>
        public void Tick(float dt)
        {
            _time += dt;
            if (_firing)
            {
                _fireTime += dt;
            }

            var fire01 = _firing ? _fireTime / Mathf.Max(0.05f, _def.timing.fireSeconds) : -1f;
            _tube.Tick(dt, _charging || _windup01 >= 0f || _firing, _windup01, fire01);
            _particleZ = _turret.MuzzlePosition.z + ZOffset;

            // 撃っている間は、このあと同じフレームの UpdateBeam がまた表示する。
            _beamRenderer.enabled = false;
            _haloRenderer.enabled = false;
            _muzzleFlare.Hide();
            _muzzleFlare2.Hide();
            HideBeamRings();
            HideImpacts();

            // Play 中に設定を変えても反映されるよう、エディタでは時々グラデーションを焼き直す。
            if (Application.isEditor)
            {
                _rebakeTimer -= dt;
                if (_rebakeTimer <= 0f)
                {
                    _rebakeTimer = 0.25f;
                    BakeGradients();
                    ApplySortingOrders();
                }
            }

            TickCharge(dt);
            TickTube(dt);
            TickRings(dt);
            _lightning.Tick(dt);
            _chargeFlash = Mathf.MoveTowards(_chargeFlash, 0f, dt * 12f);
        }

        /// <summary>レーザー本体・発射口・当たった場所の見た目を今フレームの状態に合わせる（筋ごとの長さ・当たり方は <paramref name="lanes"/>）。</summary>
        public void UpdateBeam(Vector3 origin, Vector2 direction, float widthScale, RedLaserLanes lanes)
        {
            _lanes = lanes;
            var dt = Time.deltaTime;
            var z = origin.z + ZOffset;
            var origin2 = (Vector2)origin;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var length = lanes.MaxLength;

            BuildStripMesh(_beamStrips, origin2, direction, perpendicular, widthScale, z, lanes, 1f);
            ApplyBeamParameters(length);
            _beamRenderer.enabled = length > 0.01f;

            // 外側の大きなにじみ（本体と同じ筋の形で、太く作る。壁で止まった所では一緒に止まる）。
            var flash = _def.flash;
            if (flash.haloEnabled && length > 0.01f)
            {
                // レーザーの上に重ねるときは手前、後ろのときは奥に置く。
                var haloZ = z + (flash.haloOnTop ? -0.005f : 0.005f);
                var haloWidth = flash.keepInsideLaser ? 1f : Mathf.Max(1f, flash.haloWidthScale);
                BuildStripMesh(_haloStrips, origin2, direction, perpendicular, widthScale, haloZ, lanes, haloWidth);
                _haloRenderer.sortingOrder = HaloSortingOrder;
                _haloRenderer.enabled = true;
            }
            else
            {
                _haloRenderer.enabled = false;
            }

            var muzzleFx = _def.muzzle;
            var look = _def.look;
            var flicker = 1f + (Mathf.PerlinNoise(_time * 23f, 0.37f) - 0.5f) * look.flicker * 2f;
            var open01 = Mathf.Clamp01(widthScale);

            // 発射口の光（トゲ付き）
            var muzzleSize = muzzleFx.flareSize * Mathf.Max(0.2f, widthScale) * flicker;
            _muzzleFlare.Show(new Vector3(origin.x, origin.y, z - 0.01f), Quaternion.Euler(0f, 0f, _time * 90f), new Vector3(muzzleSize, muzzleSize, 1f));
            _muzzleFlare.Apply(_block, muzzleFx.flareColor * open01,
                new Vector4(0.45f, 2.2f, muzzleFx.flareRays, 0.9f),
                new Vector4(6f, 0f, 0f, 0f),
                new Vector4(0f, 0f, Mathf.Floor(_time * 15f), 0.55f));

            // 発射口の二重の光（後ろで逆向きに回る大きな光と輪）
            if (flash.muzzleFlare2Scale > 0f)
            {
                var size2 = muzzleSize * flash.muzzleFlare2Scale;
                _muzzleFlare2.Show(new Vector3(origin.x, origin.y, z - 0.005f), Quaternion.Euler(0f, 0f, -_time * 60f), new Vector3(size2, size2, 1f));
                _muzzleFlare2.Apply(_block, muzzleFx.flareColor * (0.55f * open01),
                    new Vector4(0.25f, 1.5f, flash.muzzleFlare2Rays, 0.7f),
                    new Vector4(10f, 0f, 0.78f, 0.05f),
                    new Vector4(0.6f, 0f, Mathf.Floor(_time * 8f) + 3f, 0.3f));
            }

            // レーザーを先へ進む光の輪
            UpdateBeamRings(origin2, direction, widthScale, length, dt);

            // 発射口から飛び散る火花と、まわりへ吹き出すトゲ状の光、レーザーに沿って舞う粒（どれも壁で止まった筋の先には出さない）
            EmitMuzzleSparks(origin2, direction, dt, open01);
            EmitShards(origin2, direction, perpendicular, length, widthScale, dt);
            EmitBeamEmbers(origin2, direction, perpendicular, length, widthScale, dt);
            EmitSpeedLines(origin2, direction, perpendicular, length, widthScale, dt);
            EmitBeamLightning(origin2, direction, perpendicular, length, widthScale, dt);

            if (lanes.AnyBlocked)
            {
                UpdateImpacts(direction, widthScale, z, dt, flicker);
            }
            else
            {
                HideImpacts();
            }
        }

        public void Dispose()
        {
            if (_root != null)
            {
                DestroySafe(_root);
            }

            _tube.Dispose();
            DestroySafe(_beamMesh);
            DestroySafe(_beamMaterial);
            DestroySafe(_haloMesh);
            DestroySafe(_haloMaterial);
            DestroySafe(_haloCrossTexture);
            DestroySafe(_radialMaterial);
            DestroySafe(_particleMaterial);
            DestroySafe(_lightningMaterial);
            DestroySafe(_crossTexture);
            DestroySafe(_lengthTexture);
            DestroySafe(_stripeTexture);
        }

        // ───────────── レーザー本体 ─────────────

        /// <summary>帯のメッシュ1つ分（レーザー本体用と、外側の光用の2つを使う）。</summary>
        private sealed class StripMesh
        {
            public Mesh Mesh;
            public Vector3[] Vertices;
            public Vector4[] Uvs;
            public Vector4[] LaneUvs;
            public int Rows = -1;
            public int Capacity = -1;
        }

        /// <summary>
        /// 帯（<see cref="RedLaserLanes"/> の Strip）ごとに、左右2列・根元から先へ頂点を並べたメッシュを作る。帯の左右で長さが違ってよく、
        /// 壁に当たった部分はそこで終わり、当たっていない部分は先まで伸びる。帯の数は毎フレーム変わるので、上限ぶん作っておき、使わない帯はつぶしておく。
        /// <paramref name="widthMultiplier"/> は見た目の太さに掛ける倍率（外側の光は太く作る）。
        /// UV0 = (根元からの距離 / 届く距離, 断面 0〜1, 距離 m, 見た目の太さ m)、UV1 = (その列の長さ m, 壁で止まっているか 0/1)。
        /// </summary>
        private void BuildStripMesh(StripMesh target, Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale, float z,
            RedLaserLanes lanes, float widthMultiplier)
        {
            var rows = Mathf.Clamp(_def.shape.visualSegments, 4, 128) + 1;
            var capacity = lanes.StripCapacity;
            if (rows != target.Rows || capacity != target.Capacity)
            {
                target.Rows = rows;
                target.Capacity = capacity;
                var count = capacity * 2 * rows;
                target.Vertices = new Vector3[count];
                target.Uvs = new Vector4[count];
                target.LaneUvs = new Vector4[count];
                var triangles = new int[capacity * (rows - 1) * 6];
                var t = 0;
                for (var strip = 0; strip < capacity; strip++)
                {
                    var leftBase = strip * 2 * rows;
                    var rightBase = leftBase + rows;
                    for (var k = 0; k < rows - 1; k++)
                    {
                        triangles[t++] = leftBase + k;
                        triangles[t++] = leftBase + k + 1;
                        triangles[t++] = rightBase + k;
                        triangles[t++] = rightBase + k;
                        triangles[t++] = leftBase + k + 1;
                        triangles[t++] = rightBase + k + 1;
                    }
                }

                target.Mesh.Clear();
                target.Mesh.vertices = target.Vertices;
                target.Mesh.SetUVs(0, target.Uvs);
                target.Mesh.SetUVs(1, target.LaneUvs);
                target.Mesh.triangles = triangles;
            }

            var visualScale = _def.shape.visualWidthScale * widthMultiplier;
            var range = Mathf.Max(0.01f, _def.shape.range);
            var collapsed = new Vector3(origin.x, origin.y, z);
            for (var strip = 0; strip < capacity; strip++)
            {
                var leftBase = strip * 2 * rows;
                var rightBase = leftBase + rows;
                if (strip >= lanes.StripCount)
                {
                    // 使わない帯は1点につぶす（面積0なので何も描かれない）。
                    for (var k = 0; k < rows; k++)
                    {
                        target.Vertices[leftBase + k] = collapsed;
                        target.Vertices[rightBase + k] = collapsed;
                    }

                    continue;
                }

                WriteColumn(target, leftBase, rows, lanes.StripLeft[strip], lanes.StripLengthLeft[strip], lanes.StripBlockedLeft[strip],
                    origin, direction, perpendicular, widthScale, z, visualScale, range);
                WriteColumn(target, rightBase, rows, lanes.StripRight[strip], lanes.StripLengthRight[strip], lanes.StripBlockedRight[strip],
                    origin, direction, perpendicular, widthScale, z, visualScale, range);
            }

            target.Mesh.vertices = target.Vertices;
            target.Mesh.SetUVs(0, target.Uvs);
            target.Mesh.SetUVs(1, target.LaneUvs);
            target.Mesh.RecalculateBounds();
        }

        private void WriteColumn(StripMesh target, int start, int rows, float fraction, float laneLength, bool blocked,
            Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale, float z, float visualScale, float range)
        {
            var laneUv = new Vector4(laneLength, blocked ? 1f : 0f, 0f, 0f);
            var v = (fraction + 1f) * 0.5f;
            for (var k = 0; k < rows; k++)
            {
                var distance = laneLength * k / (rows - 1);
                var width = _def.WidthAt(distance, widthScale) * visualScale;
                var point = origin + direction * distance + perpendicular * (fraction * width * 0.5f);
                target.Vertices[start + k] = new Vector3(point.x, point.y, z);
                target.Uvs[start + k] = new Vector4(distance / range, v, distance, width);
                target.LaneUvs[start + k] = laneUv;
            }
        }

        private void ApplyBeamParameters(float length)
        {
            var look = _def.look;
            _beamMaterial.SetVector(BeamParamsId, new Vector4(look.intensity, length, look.tipFadeLength, look.bodyOpacity));
            _beamMaterial.SetVector(StreakParamsId, new Vector4(look.streakStrength, look.streakDensity, look.streakSpeed, look.flicker));
            _beamMaterial.SetVector(JagParamsId, new Vector4(look.edgeJag, look.edgeJagFrequency, look.edgeJagSpeed, _turret.playerIndex * 13.7f));
            var slant = Mathf.Tan(Mathf.Clamp(look.stripeAngle, -80f, 80f) * Mathf.Deg2Rad);
            _beamMaterial.SetVector(StripeParamsId, new Vector4(Mathf.Max(0.05f, look.stripePeriod), slant, look.stripeScrollSpeed, look.stripeStrength));
            _beamMaterial.SetVector(ShineParamsId, new Vector4(look.coreShine, look.wallGlow, 0f, 0f));
            var flash = _def.flash;
            var pulse = new Vector4(flash.pulseStrength, Mathf.Max(0.2f, flash.pulseSpacing), flash.pulseSpeed, flash.pulseWidth);
            _beamMaterial.SetVector(PulseParamsId, pulse);

            // 外側の光: しま・スジなし、光を足すだけ。荒れたふちと光の波は本体と同じに走らせる。
            _haloMaterial.SetVector(BeamParamsId, new Vector4(flash.haloIntensity, length, look.tipFadeLength * 1.5f, 0f));
            _haloMaterial.SetVector(StreakParamsId, new Vector4(0f, look.streakDensity, look.streakSpeed, look.flicker));
            _haloMaterial.SetVector(JagParamsId, new Vector4(Mathf.Min(1f, look.edgeJag * 1.5f), look.edgeJagFrequency * 0.7f, look.edgeJagSpeed, _turret.playerIndex * 13.7f + 5f));
            _haloMaterial.SetVector(StripeParamsId, new Vector4(1f, 0f, 0f, 0f));
            _haloMaterial.SetVector(ShineParamsId, new Vector4(0f, look.wallGlow * 0.6f, 0f, 0f));
            _haloMaterial.SetVector(PulseParamsId, new Vector4(flash.pulseStrength * 0.6f, Mathf.Max(0.2f, flash.pulseSpacing), flash.pulseSpeed, flash.pulseWidth * 1.4f));
        }

        // ───────────── レーザーを進む光の輪 ─────────────

        private sealed class BeamRing
        {
            public Glow Glow;
            public bool Active;
            public float Distance;
            public float Fraction;
        }

        private void HideBeamRings()
        {
            for (var i = 0; i < _beamRings.Length; i++)
            {
                _beamRings[i].Glow.Hide();
            }
        }

        /// <summary>
        /// 発射口から一定の間隔で光の輪を出し、レーザーに沿って先へ飛ばす（輪はレーザーの太さに合わせて広がる）。
        /// 輪は真ん中の筋の長さまで進むと消える（壁で止まっていればそこで消える）。
        /// </summary>
        private void UpdateBeamRings(Vector2 origin, Vector2 direction, float widthScale, float length, float dt)
        {
            var flash = _def.flash;
            if (flash.beamRingInterval > 0f)
            {
                _beamRingTimer -= dt;
                if (_beamRingTimer <= 0f)
                {
                    _beamRingTimer = flash.beamRingInterval;
                    for (var i = 0; i < _beamRings.Length; i++)
                    {
                        if (!_beamRings[i].Active)
                        {
                            _beamRings[i].Active = true;
                            _beamRings[i].Distance = 0f;
                            _beamRings[i].Fraction = 0f;
                            break;
                        }
                    }
                }
            }

            var rotation = Quaternion.LookRotation(Vector3.forward, direction);
            var z = _particleZ - 0.012f;
            for (var i = 0; i < _beamRings.Length; i++)
            {
                var ring = _beamRings[i];
                if (!ring.Active)
                {
                    continue;
                }

                ring.Distance += flash.beamRingSpeed * dt;
                var end = LaneLength(0f, length);
                if (ring.Distance >= end || length <= 0.01f)
                {
                    ring.Active = false;
                    ring.Glow.Hide();
                    continue;
                }

                var travel = ring.Distance / Mathf.Max(0.01f, end);
                var fade = Mathf.Clamp01(ring.Distance / 1.5f) * (1f - travel * travel);
                var width = _def.WidthAt(ring.Distance, widthScale) * _def.shape.visualWidthScale * (flash.keepInsideLaser ? 1f : 1.15f);
                var position = origin + direction * ring.Distance;
                ring.Glow.Show(new Vector3(position.x, position.y, z), rotation, new Vector3(width, flash.beamRingThickness, 1f));
                ring.Glow.Apply(_block, flash.beamRingColor * (fade * Mathf.Clamp01(widthScale)),
                    new Vector4(0f, 1f, 0f, 0f),
                    new Vector4(1f, 0f, 0.8f, 0.12f),
                    new Vector4(1.3f, 0f, 0f, 0f));
            }
        }

        // ───────────── 当たった場所 ─────────────

        /// <summary>壁に当たっている筋のまとまり1つ（となり合っていて、当たった距離が近い筋）。</summary>
        private struct ImpactArea
        {
            public Vector2 Center;
            public Vector2 Normal;
            public float Width;
        }

        private void HideImpacts()
        {
            for (var i = 0; i < MaxImpactAreas; i++)
            {
                _impactFlares[i].Hide();
                _impactSplashes[i].Hide();
            }
        }

        /// <summary>当たっている筋を、となり合っていて当たった距離が近いものどうしでまとめる（別々の壁・障害物は別のまとまり）。</summary>
        private void CollectImpactAreas(float widthScale)
        {
            var lanes = _lanes;
            _blockedLanes.Clear();
            _impactAreaCount = 0;
            var visualScale = _def.shape.visualWidthScale;
            var runStart = -1;
            for (var c = 0; c <= lanes.Count; c++)
            {
                var blocked = c < lanes.Count && lanes.Blocked[c];
                if (blocked)
                {
                    _blockedLanes.Add(c);
                }

                var continues = blocked && runStart >= 0 && Mathf.Abs(lanes.HitDistance[c] - lanes.HitDistance[c - 1]) < 1.5f;
                if (runStart >= 0 && !continues)
                {
                    AddImpactArea(runStart, c - 1, widthScale, visualScale);
                    runStart = -1;
                }

                if (blocked && runStart < 0)
                {
                    runStart = c;
                }
            }
        }

        private void AddImpactArea(int first, int last, float widthScale, float visualScale)
        {
            if (_impactAreaCount >= MaxImpactAreas)
            {
                return;
            }

            var lanes = _lanes;
            var center = Vector2.zero;
            var normal = Vector2.zero;
            for (var c = first; c <= last; c++)
            {
                center += lanes.HitPoint[c];
                normal += lanes.HitNormal[c];
            }

            var count = last - first + 1;
            var distance = lanes.HitDistance[first];
            var laneSpacing = _def.WidthAt(distance, widthScale) * visualScale / (lanes.Count - 1);
            _impactAreas[_impactAreaCount++] = new ImpactArea
            {
                Center = center / count,
                Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up,
                Width = Vector2.Distance(lanes.HitPoint[first], lanes.HitPoint[last]) + laneSpacing
            };
        }

        private void UpdateImpacts(Vector2 direction, float widthScale, float z, float dt, float flicker)
        {
            CollectImpactAreas(widthScale);
            var impactFx = _def.impact;
            var open01 = Mathf.Clamp01(widthScale);
            var lanes = _lanes;

            // まとまりごとに、光（トゲ付き、毎フレーム向きが変わってバチバチする）と、壁に沿って横に押し広がる光（楕円）
            for (var i = 0; i < MaxImpactAreas; i++)
            {
                if (i >= _impactAreaCount)
                {
                    _impactFlares[i].Hide();
                    _impactSplashes[i].Hide();
                    continue;
                }

                var area = _impactAreas[i];
                var pos = new Vector3(area.Center.x, area.Center.y, z - 0.02f);
                var flareSize = Mathf.Max(0.5f, area.Width * impactFx.flareScale) * flicker;
                _impactFlares[i].Show(pos, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)), new Vector3(flareSize, flareSize, 1f));
                _impactFlares[i].Apply(_block, impactFx.flareColor * open01,
                    new Vector4(0.5f, 1.8f, 16f, 1.1f),
                    new Vector4(4f, 0f, 0f, 0f),
                    new Vector4(0f, 0f, Mathf.Floor(_time * 30f) + i * 7f, 0.8f));

                var splashLength = _def.flash.keepInsideLaser
                    ? Mathf.Max(0.3f, area.Width)
                    : Mathf.Max(0.5f, area.Width * impactFx.splashScale) * flicker;
                var splashThickness = Mathf.Max(0.3f, area.Width * 0.35f);
                _impactSplashes[i].Show(pos + (Vector3)(area.Normal * splashThickness * 0.15f), Quaternion.LookRotation(Vector3.forward, area.Normal),
                    new Vector3(splashLength, splashThickness, 1f));
                _impactSplashes[i].Apply(_block, impactFx.flareColor * (0.8f * open01),
                    new Vector4(0.95f, 1.6f, 0f, 0f),
                    Vector4.zero,
                    Vector4.zero);
            }

            // 火花・煙っぽい粒・稲妻は、当たっている筋のどれかから出す（当たっている部分が多いほどたくさん出る）。
            if (_blockedLanes.Count == 0)
            {
                return;
            }

            var blockedShare = _blockedLanes.Count / (float)lanes.Count;
            var laneSpacing = _def.WidthAt(lanes.MaxLength, widthScale) * _def.shape.visualWidthScale / (lanes.Count - 1);

            var sparks = Accumulate(ref _impactSparkCarry, impactFx.sparkRate * open01 * blockedShare * dt);
            for (var i = 0; i < sparks; i++)
            {
                RandomImpactPoint(laneSpacing, out var point, out var normal);
                var dir = Rotate(normal, UnityEngine.Random.Range(-75f, 75f));
                var speed = impactFx.sparkSpeed * UnityEngine.Random.Range(0.35f, 1.1f);
                Emit(_sparks, point, dir * speed, UnityEngine.Random.Range(0.08f, 0.2f), UnityEngine.Random.Range(0.15f, 0.45f), impactFx.sparkColor);
            }

            var embers = Accumulate(ref _impactEmberCarry, impactFx.emberRate * open01 * blockedShare * dt);
            for (var i = 0; i < embers; i++)
            {
                RandomImpactPoint(laneSpacing, out var point, out var normal);
                var dir = Rotate(normal, UnityEngine.Random.Range(-60f, 60f));
                Emit(_embers, point, dir * UnityEngine.Random.Range(1f, 4f), UnityEngine.Random.Range(0.4f, 1.1f), UnityEngine.Random.Range(0.35f, 0.8f), _def.muzzle.shardColor);
            }

            var bolts = Accumulate(ref _impactBoltCarry, _def.lightning.impactRate * open01 * blockedShare * dt);
            for (var i = 0; i < bolts; i++)
            {
                RandomImpactPoint(laneSpacing, out var point, out var normal);
                var dir = Rotate(normal, UnityEngine.Random.Range(-80f, 80f));
                var end = point + dir * (UnityEngine.Random.Range(0.4f, 1f) * Mathf.Max(1f, laneSpacing * 3f));
                SpawnBolt(point, end, 1f);
            }

            // 一定の間隔で、まとまりごとに衝撃の輪
            if (impactFx.shockwaveInterval > 0f)
            {
                _shockwaveTimer -= dt;
                if (_shockwaveTimer <= 0f)
                {
                    _shockwaveTimer = impactFx.shockwaveInterval;
                    for (var i = 0; i < _impactAreaCount; i++)
                    {
                        var area = _impactAreas[i];
                        SpawnRing(new Vector3(area.Center.x, area.Center.y, z), Mathf.Max(1f, area.Width * impactFx.shockwaveScale), 0.35f, impactFx.flareColor * 0.7f);
                    }
                }
            }
        }

        /// <summary>当たっている筋を1本選び、その当たった場所の近く（筋の幅の中）と、壁の向きを返す。</summary>
        private void RandomImpactPoint(float laneSpacing, out Vector2 point, out Vector2 normal)
        {
            var lane = _blockedLanes[UnityEngine.Random.Range(0, _blockedLanes.Count)];
            normal = _lanes.HitNormal[lane];
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector2.up;
            }

            normal.Normalize();
            var tangent = new Vector2(-normal.y, normal.x);
            point = _lanes.HitPoint[lane] + tangent * (UnityEngine.Random.Range(-0.5f, 0.5f) * laneSpacing);
        }

        /// <summary>横の位置 fraction（-1〜+1、見た目の太さに対する割合）での、今の筋の長さ。まだ筋が無ければ fallback。</summary>
        private float LaneLength(float fraction, float fallback)
        {
            return _lanes != null && _lanes.Count >= 2 ? _lanes.LengthAt(fraction) : fallback;
        }

        // ───────────── 発射口・レーザーまわりの粒 ─────────────

        private void EmitMuzzleSparks(Vector2 origin, Vector2 direction, float dt, float open01)
        {
            var muzzleFx = _def.muzzle;
            var count = Accumulate(ref _muzzleSparkCarry, muzzleFx.sparkRate * open01 * dt);
            for (var i = 0; i < count; i++)
            {
                var dir = Rotate(direction, UnityEngine.Random.Range(-muzzleFx.sparkSpreadAngle, muzzleFx.sparkSpreadAngle));
                var speed = muzzleFx.sparkSpeed * UnityEngine.Random.Range(0.4f, 1.2f);
                Emit(_sparks, origin, dir * speed, UnityEngine.Random.Range(0.08f, 0.18f), UnityEngine.Random.Range(0.15f, 0.4f), muzzleFx.particleColor);
            }
        }

        private void EmitShards(Vector2 origin, Vector2 direction, Vector2 perpendicular, float length, float widthScale, float dt)
        {
            var muzzleFx = _def.muzzle;
            var reach = Mathf.Min(muzzleFx.shardReach, length);
            var count = Accumulate(ref _shardCarry, muzzleFx.shardRate * Mathf.Clamp01(widthScale) * dt);
            for (var i = 0; i < count; i++)
            {
                // 根元ほど多く、レーザーのふちから外へ斜め前に吹き出す。
                var distance = reach * UnityEngine.Random.value * UnityEngine.Random.value;
                var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                var half = _def.WidthAt(distance, widthScale) * 0.5f;
                var offset = side * UnityEngine.Random.Range(0.3f, 1f);
                if (distance > LaneLength(offset / _def.shape.visualWidthScale, length))
                {
                    continue;
                }

                var start = origin + direction * distance + perpendicular * (offset * half);
                // 「同じ太さに収める」ときは、レーザーに沿って前へ（横へはほとんど出ない。前ほど太くなるので外へはみ出さない）。
                var sideways = _def.flash.keepInsideLaser ? UnityEngine.Random.Range(0f, 0.15f) : UnityEngine.Random.Range(0.6f, 1.4f);
                var outward = (perpendicular * side * sideways + direction * UnityEngine.Random.Range(0.4f, 1.6f)).normalized;
                var speed = muzzleFx.sparkSpeed * UnityEngine.Random.Range(0.8f, 1.6f);
                Emit(_shards, start, outward * speed, UnityEngine.Random.Range(0.25f, 0.55f), UnityEngine.Random.Range(0.08f, 0.2f), muzzleFx.shardColor);
            }
        }

        private void EmitBeamEmbers(Vector2 origin, Vector2 direction, Vector2 perpendicular, float length, float widthScale, float dt)
        {
            var muzzleFx = _def.muzzle;
            var count = Accumulate(ref _beamEmberCarry, muzzleFx.emberRate * Mathf.Clamp01(widthScale) * dt);
            for (var i = 0; i < count; i++)
            {
                var across = UnityEngine.Random.Range(-1f, 1f);
                var distance = LaneLength(across / _def.shape.visualWidthScale, length) * UnityEngine.Random.value;
                var half = _def.WidthAt(distance, widthScale) * 0.5f;
                var start = origin + direction * distance + perpendicular * (across * half);
                var drift = _def.flash.keepInsideLaser ? UnityEngine.Random.Range(0f, 0.4f) : UnityEngine.Random.Range(0.5f, 3f);
                var velocity = direction * UnityEngine.Random.Range(4f, 12f) + perpendicular * (across * drift);
                Emit(_embers, start, velocity, UnityEngine.Random.Range(0.15f, 0.4f), UnityEngine.Random.Range(0.3f, 0.7f), muzzleFx.particleColor);
            }
        }

        // ───────────── ビリビリ（稲妻）・走る光の線 ─────────────

        private void SpawnBolt(Vector2 from, Vector2 to, float widthScale)
        {
            var lightning = _def.lightning;
            _lightning.Spawn(from, to, _particleZ - 0.01f,
                lightning.width * widthScale * UnityEngine.Random.Range(0.7f, 1.3f),
                lightning.boltLife * UnityEngine.Random.Range(0.7f, 1.4f),
                lightning.color, lightning.jitter, lightning.branchChance);
        }

        /// <summary>中心から外へ、一度にたくさんの稲妻（使えるようになった瞬間・撃ち始め）。</summary>
        private void BurstLightning(Vector3 center, Vector2 direction, float spreadAngle, int count, float reach)
        {
            _particleZ = center.z + ZOffset;
            var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            for (var i = 0; i < count; i++)
            {
                var to = (Vector2)center + Rotate(dir, UnityEngine.Random.Range(-spreadAngle, spreadAngle)) * (reach * UnityEngine.Random.Range(0.5f, 1f));
                SpawnBolt(center, to, 1.3f);
            }
        }

        /// <summary>溜め中: 発射口から外へ・砲台のまわりを弧で走る稲妻。ため中: まわりから発射口へ吸い込まれる稲妻が増える。</summary>
        private void EmitChargeLightning(Vector3 muzzle, bool windingUp, float windup, float dt)
        {
            var lightning = _def.lightning;
            var rate = lightning.chargeRate * Mathf.Lerp(1f, lightning.windupMultiplier, windup);
            var count = Accumulate(ref _chargeBoltCarry, rate * dt);
            var center = (Vector2)muzzle;
            var basePosition = (Vector2)_turret.transform.position;
            var radius = lightning.chargeRadius;
            for (var i = 0; i < count; i++)
            {
                Vector2 from;
                Vector2 to;
                var roll = UnityEngine.Random.value;
                if (windingUp && roll < 0.7f)
                {
                    from = center + RandomDirection() * (radius * UnityEngine.Random.Range(0.8f, 1.3f));
                    to = center;
                }
                else if (roll < 0.55f)
                {
                    from = center;
                    to = center + RandomDirection() * (radius * UnityEngine.Random.Range(0.35f, 1f));
                }
                else
                {
                    var a0 = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    var a1 = a0 + UnityEngine.Random.Range(0.6f, 1.8f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                    var r = radius * UnityEngine.Random.Range(0.45f, 0.8f);
                    from = basePosition + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                    to = basePosition + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                }

                SpawnBolt(from, to, windingUp ? 1.2f : 1f);
                Emit(_sparks, to, UnityEngine.Random.insideUnitCircle * 3f, UnityEngine.Random.Range(0.06f, 0.12f), UnityEngine.Random.Range(0.1f, 0.2f), lightning.color);
                _chargeFlash = 1f;
            }
        }

        /// <summary>撃っている間: レーザーにまとわりつく長い稲妻と、ふちから外へ飛び出す短い稲妻。</summary>
        private void EmitBeamLightning(Vector2 origin, Vector2 direction, Vector2 perpendicular, float length, float widthScale, float dt)
        {
            var lightning = _def.lightning;
            var open01 = Mathf.Clamp01(widthScale);
            var visualScale = _def.shape.visualWidthScale;

            var along = Accumulate(ref _beamBoltCarry, lightning.beamRate * open01 * dt);
            for (var i = 0; i < along; i++)
            {
                var f0 = UnityEngine.Random.Range(-0.9f, 0.9f);
                var f1 = Mathf.Clamp(f0 + UnityEngine.Random.Range(-0.5f, 0.5f), -0.9f, 0.9f);
                var d0 = UnityEngine.Random.Range(0f, Mathf.Max(0.01f, LaneLength(f0, length) - 0.5f));
                var d1 = Mathf.Min(LaneLength(f1, length), d0 + lightning.beamBoltLength * UnityEngine.Random.Range(0.5f, 1.2f));
                if (d1 <= d0)
                {
                    continue;
                }

                var h0 = _def.WidthAt(d0, widthScale) * 0.5f * visualScale;
                var h1 = _def.WidthAt(d1, widthScale) * 0.5f * visualScale;
                var from = origin + direction * d0 + perpendicular * (f0 * h0);
                var to = origin + direction * d1 + perpendicular * (f1 * h1);
                SpawnBolt(from, to, 1f);
            }

            var edges = Accumulate(ref _edgeBoltCarry, lightning.edgeRate * open01 * dt);
            for (var i = 0; i < edges; i++)
            {
                var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                var d = UnityEngine.Random.Range(0f, LaneLength(side * 0.85f, length));
                var h = _def.WidthAt(d, widthScale) * 0.5f * visualScale * 0.85f;
                var from = origin + direction * d + perpendicular * (side * h);
                Vector2 to;
                if (_def.flash.keepInsideLaser)
                {
                    // ふちに沿って前へ走り、少し内側へ入る（外へは出ない）。
                    var ahead = Mathf.Min(LaneLength(side * 0.85f, length), d + UnityEngine.Random.Range(0.8f, 2.2f));
                    var hAhead = _def.WidthAt(ahead, widthScale) * 0.5f * visualScale * UnityEngine.Random.Range(0.55f, 0.85f);
                    to = origin + direction * ahead + perpendicular * (side * hAhead);
                }
                else
                {
                    var outward = (perpendicular * side + direction * UnityEngine.Random.Range(-0.3f, 0.8f)).normalized;
                    to = from + outward * UnityEngine.Random.Range(0.6f, 1.8f);
                }
                SpawnBolt(from, to, 0.8f);
            }
        }

        /// <summary>レーザーと同じ向きに走る細い光の線（ほとんどはレーザーのすぐ外側、一部はレーザーの上）。</summary>
        private void EmitSpeedLines(Vector2 origin, Vector2 direction, Vector2 perpendicular, float length, float widthScale, float dt)
        {
            var muzzleFx = _def.muzzle;
            var count = Accumulate(ref _speedLineCarry, muzzleFx.speedLineRate * Mathf.Clamp01(widthScale) * dt);
            for (var i = 0; i < count; i++)
            {
                var acrossFraction = _def.flash.keepInsideLaser || UnityEngine.Random.value < 0.3f
                    ? UnityEngine.Random.Range(-0.85f, 0.85f)
                    : (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(0.95f, 1.5f);
                var d = UnityEngine.Random.Range(0f, LaneLength(acrossFraction, length) * 0.9f);
                var h = _def.WidthAt(d, widthScale) * 0.5f * _def.shape.visualWidthScale;
                var across = acrossFraction * h;
                var position = origin + direction * d + perpendicular * across;
                var velocity = direction * (muzzleFx.speedLineSpeed * UnityEngine.Random.Range(0.7f, 1.3f));
                Emit(_speedLines, position, velocity, UnityEngine.Random.Range(0.04f, 0.09f), UnityEngine.Random.Range(0.08f, 0.18f), muzzleFx.particleColor);
            }
        }

        private static Vector2 RandomDirection()
        {
            var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        // ───────────── 溜めの歯磨き粉 ─────────────

        /// <summary>歯磨き粉の後ろの光と、使える間（ため中も）の「今にも出そう」な演出（口からにじむ粒・チューブを走る稲妻）。</summary>
        private void TickTube(float dt)
        {
            var tube = _def.tube;
            if (!_tube.IsShown || tube.auraScale <= 0f)
            {
                _tubeAura.Hide();
            }
            else
            {
                var size = _tube.CurrentLength * tube.auraScale * (1f + Mathf.Sin(_time * 7f) * 0.12f) * (1f + _chargeFlash * 0.25f);
                var center = _tube.SurfacePoint(0f, 0f);
                var z = _turret.transform.position.z - 0.05f;
                _tubeAura.Show(new Vector3(center.x, center.y, z), Quaternion.LookRotation(Vector3.forward, _tube.NozzleDirection), new Vector3(size * 0.65f, size, 1f));
                _tubeAura.Apply(_block, tube.auraColor * (_firing ? 1.4f : 1f),
                    new Vector4(0.95f, 1.6f, 0f, 0f),
                    Vector4.zero,
                    Vector4.zero);
            }

            var windingUp = _windup01 >= 0f;
            if (!_tube.IsShown || (!_charging && !windingUp))
            {
                return;
            }

            var windup = windingUp ? _windup01 : 0f;
            var nozzle = (Vector2)_tube.NozzlePosition;
            var aim = _tube.NozzleDirection;
            var side = new Vector2(aim.y, -aim.x);

            // 口からにじみ出る歯磨き粉の粒（中身がパンパンで、今にも出そう）
            var drips = Accumulate(ref _dripCarry, tube.dripRate * Mathf.Lerp(1f, 3f, windup) * dt);
            for (var i = 0; i < drips; i++)
            {
                var velocity = aim * UnityEngine.Random.Range(0.4f, 2f) + side * UnityEngine.Random.Range(-1.2f, 1.2f);
                Emit(_embers, nozzle, velocity, UnityEngine.Random.Range(0.12f, 0.26f), UnityEngine.Random.Range(0.35f, 0.8f), tube.dripColor);
            }

            // 雷: まわりの遠くから、太い稲妻が歯磨き粉に落ちてくる（光って、輪と火花が出る）。ため中は3倍の速さ。
            _thunderTimer -= dt * Mathf.Lerp(1f, 3f, windup);
            if (tube.thunderIntervalMax > 0f && _thunderTimer <= 0f)
            {
                _thunderTimer = UnityEngine.Random.Range(Mathf.Min(tube.thunderIntervalMin, tube.thunderIntervalMax), tube.thunderIntervalMax);
                StrikeThunder(tube);
            }

            // チューブの表面を走る稲妻
            var bolts = Accumulate(ref _tubeBoltCarry, tube.lightningRate * Mathf.Lerp(1f, 3f, windup) * dt);
            for (var i = 0; i < bolts; i++)
            {
                var from = _tube.SurfacePoint(UnityEngine.Random.Range(-0.9f, 0.9f), UnityEngine.Random.Range(-1f, 1f));
                var to = _tube.SurfacePoint(UnityEngine.Random.Range(-0.9f, 1f), (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(1f, 1.5f));
                SpawnBolt(from, to, 0.8f);
                _chargeFlash = 1f;
            }
        }

        /// <summary>太い雷を1本、歯磨き粉のどこかに落とす（枝分かれあり）。</summary>
        private void StrikeThunder(RedLaserTubeFx tube)
        {
            var lightning = _def.lightning;
            var target = _tube.SurfacePoint(UnityEngine.Random.Range(-0.6f, 1f), UnityEngine.Random.Range(-0.8f, 0.8f));
            var from = target + RandomDirection() * (tube.thunderLength * UnityEngine.Random.Range(0.7f, 1.1f));
            var width = lightning.width * tube.thunderWidthScale;

            // 本体の太い雷（少し長く光る）と、そのまわりの細い雷2本で「ドーン」と見せる。
            _lightning.Spawn(from, target, _particleZ - 0.02f, width, lightning.boltLife * 2f, lightning.color, lightning.jitter * 0.8f, 1f);
            for (var i = 0; i < 2; i++)
            {
                var offset = RandomDirection() * UnityEngine.Random.Range(0.3f, 1f);
                _lightning.Spawn(from + offset, target, _particleZ - 0.02f, width * 0.4f, lightning.boltLife * 1.4f, lightning.color, lightning.jitter, 0.5f);
            }

            SpawnRing(new Vector3(target.x, target.y, _particleZ), Mathf.Max(1.5f, tube.width * 2.5f), 0.25f, _def.charge.glowColor);
            BurstSparks(new Vector3(target.x, target.y, _particleZ - ZOffset), -(Vector2)(from - target).normalized, 80f, 14, _def.muzzle.sparkSpeed * 0.7f, lightning.color);
            _chargeFlash = 1f;
        }

        // ───────────── 溜め ─────────────

        private void TickCharge(float dt)
        {
            var charge = _def.charge;
            var windingUp = _windup01 >= 0f;
            if (!_charging && !windingUp)
            {
                _chargeGlow.Hide();
                return;
            }

            _chargeTime += dt;
            var pulse = 1f + Mathf.Sin(_chargeTime * charge.pulseFrequency * Mathf.PI * 2f) * charge.pulseAmount;
            var appear = Mathf.Clamp01(_chargeTime / 0.25f);
            var windup = windingUp ? _windup01 : 0f;
            var muzzle = FirePoint;

            // 発射口（歯磨き粉の口）に灯る光（ためでは大きく・速く回り・明るくなる）
            var spin = _chargeTime * 90f * Mathf.Lerp(1f, 6f, windup);
            var glowSize = charge.muzzleGlowSize * pulse * Mathf.Lerp(1f, 2.4f, windup) * appear;
            _chargeGlow.Show(new Vector3(muzzle.x, muzzle.y, muzzle.z + ZOffset - 0.01f), Quaternion.Euler(0f, 0f, -spin * 2f), new Vector3(glowSize, glowSize, 1f));
            _chargeGlow.Apply(_block, charge.glowColor * (Mathf.Lerp(0.9f, 1.8f, windup) * (1f + _chargeFlash * 0.8f)),
                new Vector4(0.5f, 2.5f, 6f, 0.5f),
                new Vector4(8f, 0f, 0f, 0f),
                new Vector4(0f, 0f, Mathf.Floor(_chargeTime * 12f), 0.4f));

            // ビリビリ（赤い稲妻）。ためでは一気に増え、まわりから発射口へ吸い込まれる。
            EmitChargeLightning(muzzle, windingUp, windup, dt);

            // 吸い込まれていく光の粒（ためでは一気に増える）
            var rate = charge.emberRate * Mathf.Lerp(1f, charge.windupEmberMultiplier, windup);
            var count = Accumulate(ref _chargeEmberCarry, rate * dt);
            for (var i = 0; i < count; i++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var radius = charge.emberRadius * UnityEngine.Random.Range(0.6f, 1f) * Mathf.Lerp(1f, 1.5f, windup);
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var life = UnityEngine.Random.Range(0.35f, 0.7f) * Mathf.Lerp(1f, 0.5f, windup);
                // 火花には空気抵抗があるので、少し速めに飛ばして発射口まで届かせる。
                Emit(_sparks, (Vector2)muzzle + offset, -offset / life * 1.8f, UnityEngine.Random.Range(0.08f, 0.16f), life, charge.emberColor);
            }
        }

        // ───────────── 衝撃の輪 ─────────────

        private void SpawnRing(Vector3 position, float size, float duration, Color color)
        {
            var slot = _rings[0];
            for (var i = 0; i < _rings.Length; i++)
            {
                if (!_rings[i].Active)
                {
                    slot = _rings[i];
                    break;
                }

                if (_rings[i].Elapsed > slot.Elapsed)
                {
                    slot = _rings[i];
                }
            }

            slot.Active = true;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(0.05f, duration);
            slot.Size = size;
            slot.Color = color;
            slot.Position = new Vector3(position.x, position.y, position.z + ZOffset - 0.015f);
        }

        private void TickRings(float dt)
        {
            for (var i = 0; i < _rings.Length; i++)
            {
                var ring = _rings[i];
                if (!ring.Active)
                {
                    ring.Glow.Hide();
                    continue;
                }

                ring.Elapsed += dt;
                var t = ring.Elapsed / ring.Duration;
                if (t >= 1f)
                {
                    ring.Active = false;
                    ring.Glow.Hide();
                    continue;
                }

                var eased = 1f - (1f - t) * (1f - t) * (1f - t);
                var size = ring.Size * Mathf.Lerp(0.15f, 1f, eased);
                var fade = 1f - t;
                ring.Glow.Show(ring.Position, Quaternion.identity, new Vector3(size, size, 1f));
                ring.Glow.Apply(_block, ring.Color * fade,
                    new Vector4(0f, 1f, 0f, 0f),
                    new Vector4(1f, 0f, 0.82f, Mathf.Lerp(0.12f, 0.03f, t)),
                    new Vector4(1.4f, 0f, 0f, 0f));
            }
        }

        // ───────────── 作る・こまごま ─────────────

        /// <summary>描く順番を設定ファイルの値に合わせる（Play 中に変えたとき用）。</summary>
        private void ApplySortingOrders()
        {
            var order = _def.look.sortingOrder;
            _beamRenderer.sortingOrder = order;
            _haloRenderer.sortingOrder = HaloSortingOrder;
            _muzzleFlare2.SortingOrder = order + 1;
            for (var i = 0; i < _beamRings.Length; i++)
            {
                _beamRings[i].Glow.SortingOrder = order + 1;
            }
            _chargeGlow.SortingOrder = order + 1;
            _muzzleFlare.SortingOrder = order + 1;
            for (var i = 0; i < MaxImpactAreas; i++)
            {
                _impactSplashes[i].SortingOrder = order + 1;
                _impactFlares[i].SortingOrder = order + 2;
            }
            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i].Glow.SortingOrder = order + 1;
            }

            _sparks.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 3;
            _shards.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 2;
            _embers.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 3;
            _speedLines.GetComponent<ParticleSystemRenderer>().sortingOrder = order + 2;
            _lightning.SortingOrder = order + 3;
            _tubeAura.SortingOrder = _def.tube.sortingOrder - 1;
        }

        private void HideAll()
        {
            _tube.Hide();
            _tubeAura.Hide();
            _lightning.HideAll();
            _beamRenderer.enabled = false;
            _haloRenderer.enabled = false;
            _chargeGlow.Hide();
            _muzzleFlare.Hide();
            _muzzleFlare2.Hide();
            HideBeamRings();
            HideImpacts();
            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i].Glow.Hide();
            }
        }

        private void BakeGradients()
        {
            var look = _def.look;
            var resolution = Mathf.Clamp(look.gradientResolution, 16, 512);
            _crossTexture = Bake(_crossTexture, look.crossGradient, resolution, "RedSkillCross");
            _lengthTexture = Bake(_lengthTexture, look.lengthGradient, resolution, "RedSkillLength");
            _stripeTexture = Bake(_stripeTexture, look.stripeGradient, resolution, "RedSkillStripe", true, look.stripeBlend);
            if (_beamMaterial != null)
            {
                _beamMaterial.SetTexture(CrossTexId, _crossTexture);
                _beamMaterial.SetTexture(LengthTexId, _lengthTexture);
                _beamMaterial.SetTexture(StripeTexId, _stripeTexture);
            }

            // 外側の光の断面: 真ん中は薄め（本体の後ろなので）、少し外で一番濃く、ふちで消える。
            var halo = _def.flash.haloColor;
            _haloGradient.SetKeys(
                new[] { new GradientColorKey(halo, 0f), new GradientColorKey(halo, 1f) },
                new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0.18f, 0.8f), new GradientAlphaKey(0f, 1f) });
            _haloCrossTexture = Bake(_haloCrossTexture, _haloGradient, resolution, "RedSkillHaloCross");
            if (_haloMaterial != null)
            {
                _haloMaterial.SetTexture(CrossTexId, _haloCrossTexture);
                _haloMaterial.SetTexture(LengthTexId, _lengthTexture);
                _haloMaterial.SetTexture(StripeTexId, _stripeTexture);
            }

            if (_particleMaterial != null)
            {
                _particleMaterial.SetFloat(IntensityId, look.particleBrightness);
            }

            if (_lightningMaterial != null)
            {
                _lightningMaterial.SetFloat(LineModeId, 1f);
                _lightningMaterial.SetFloat(SoftnessId, 1.3f);
                _lightningMaterial.SetFloat(IntensityId, _def.lightning.brightness);
            }
        }

        /// <param name="periodic">true ならくり返し模様用（右端の次が左端につながるよう、両端のすき間を色の間でなめらかにつなぐ）。</param>
        /// <param name="blend">くり返し模様用: 色をなじませる強さ（0〜1）。1 で 1 周の半分の幅でぼかす（色の帯がほぼ全部グラデーションになる）。</param>
        private static Texture2D Bake(Texture2D texture, Gradient gradient, int resolution, string name, bool periodic = false, float blend = 0f)
        {
            if (texture == null || texture.width != resolution)
            {
                DestroySafe(texture);
                texture = new Texture2D(resolution, 1, TextureFormat.RGBAHalf, false, true)
                {
                    name = name,
                    wrapMode = periodic ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // 色は画面の色（ガンマ）で選ばれているので、リニア色空間のプロジェクトではリニアに直して入れる（マテリアルの色と同じ扱い）。
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var pixels = new Color[resolution];
            for (var x = 0; x < resolution; x++)
            {
                var color = gradient == null ? Color.white
                    : periodic ? EvaluatePeriodic(gradient, x / (float)resolution)
                    : gradient.Evaluate(x / (resolution - 1f));
                if (linear)
                {
                    var alpha = color.a;
                    color = color.linear;
                    color.a = alpha;
                }

                pixels[x] = color;
            }

            if (periodic && blend > 0f)
            {
                // 1周でつながったまま横にぼかす（2回かけて、角のないなめらかなグラデーションにする）。
                var radius = Mathf.Clamp(Mathf.RoundToInt(blend * 0.25f * resolution), 1, resolution / 2);
                pixels = BlurPeriodic(BlurPeriodic(pixels, radius), radius);
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>くり返し模様の色を、端と端がつながったまま平均でぼかす（radius ピクセルぶん左右を平均）。</summary>
        private static Color[] BlurPeriodic(Color[] source, int radius)
        {
            var count = source.Length;
            var result = new Color[count];
            var window = radius * 2 + 1;
            var sum = Color.clear;
            for (var k = -radius; k <= radius; k++)
            {
                sum += source[((k % count) + count) % count];
            }

            for (var x = 0; x < count; x++)
            {
                result[x] = sum / window;
                sum -= source[((x - radius) % count + count) % count];
                sum += source[(x + radius + 1) % count];
            }

            return result;
        }

        /// <summary>くり返し模様用: 最後の色のキーから（1周して）最初の色のキーまでを、なめらかにつなぐ。</summary>
        private static Color EvaluatePeriodic(Gradient gradient, float t)
        {
            var keys = gradient.colorKeys;
            if (keys == null || keys.Length < 2 || gradient.mode == GradientMode.Fixed)
            {
                return gradient.Evaluate(t);
            }

            var first = keys[0].time;
            var last = keys[keys.Length - 1].time;
            if (t >= first && t <= last)
            {
                return gradient.Evaluate(t);
            }

            var gap = 1f - last + first;
            if (gap <= 0.0001f)
            {
                return gradient.Evaluate(t);
            }

            var into = t > last ? t - last : t + 1f - last;
            var color = Color.Lerp(gradient.Evaluate(last), gradient.Evaluate(first), into / gap);
            color.a = gradient.Evaluate(t).a;
            return color;
        }

        private static Material CreateMaterial(Shader shader, string fallbackName)
        {
            if (shader == null)
            {
                shader = Shader.Find(fallbackName);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[RedSkill] シェーダー {fallbackName} が見つかりません。スキルの設定ファイルの「シェーダー」欄を確認してください。");
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3100 };
        }

        private MeshRenderer CreateRenderer(string objectName, Mesh mesh, Material material, int sortingOrder)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        private ParticleSystem CreateParticles(string objectName, int sortingOrder, float velocityScale, float lengthScale, int maxParticles, float drag)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize = 0.2f;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.25f)));

            if (drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
                limit.limit = 1000f;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particleMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (velocityScale > 0f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = lengthScale;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            system.Play();
            return system;
        }

        private void Emit(ParticleSystem system, Vector2 position, Vector2 velocity, float size, float lifetime, Color color)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, _particleZ),
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color
            };
            system.Emit(parameters, 1);
        }

        private void BurstSparks(Vector3 origin, Vector2 direction, float spreadAngle, int count, float speed, Color color)
        {
            _particleZ = origin.z + ZOffset;
            for (var i = 0; i < count; i++)
            {
                var dir = Rotate(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up, UnityEngine.Random.Range(-spreadAngle, spreadAngle));
                Emit(_sparks, origin, dir * speed * UnityEngine.Random.Range(0.4f, 1.1f), UnityEngine.Random.Range(0.1f, 0.22f), UnityEngine.Random.Range(0.25f, 0.55f), color);
            }
        }

        /// <summary>毎秒の数×経過秒を整数の粒数にする（端数は次のフレームへ持ち越す）。</summary>
        private static int Accumulate(ref float carry, float amount)
        {
            carry += Mathf.Max(0f, amount);
            var count = Mathf.FloorToInt(carry);
            carry -= count;
            return Mathf.Min(count, 200);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private static void DestroySafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>Radial シェーダーで描く四角い板1枚（光・輪）。</summary>
        private sealed class Glow
        {
            private readonly MeshRenderer _renderer;
            private readonly Transform _transform;

            public Glow(MeshRenderer renderer)
            {
                _renderer = renderer;
                _transform = renderer.transform;
            }

            public void Show(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                _transform.SetPositionAndRotation(position, rotation);
                _transform.localScale = scale;
                _renderer.enabled = scale.x > 0.001f && scale.y > 0.001f;
            }

            public void Hide()
            {
                _renderer.enabled = false;
            }

            public int SortingOrder
            {
                set => _renderer.sortingOrder = value;
            }

            /// <param name="color">色（HDR）。アルファは濃さ。</param>
            /// <param name="shapeA">x=芯の大きさ(0〜1), y=芯のしぼり, z=トゲの本数, w=トゲの強さ</param>
            /// <param name="shapeB">x=トゲの細さ, y=回転(ラジアン), z=輪の半径(0〜1), w=輪の太さ</param>
            /// <param name="shapeC">x=輪の強さ, y=（未使用）, z=トゲの長さの乱数の種, w=トゲの長さのばらつき</param>
            public void Apply(MaterialPropertyBlock block, Color color, Vector4 shapeA, Vector4 shapeB, Vector4 shapeC)
            {
                block.Clear();
                block.SetColor(ColorId, color);
                block.SetVector(ShapeAId, shapeA);
                block.SetVector(ShapeBId, shapeB);
                block.SetVector(ShapeCId, shapeC);
                _renderer.SetPropertyBlock(block);
            }
        }

        private sealed class Ring
        {
            public Glow Glow;
            public bool Active;
            public float Elapsed;
            public float Duration;
            public float Size;
            public Color Color;
            public Vector3 Position;
        }
    }
}
