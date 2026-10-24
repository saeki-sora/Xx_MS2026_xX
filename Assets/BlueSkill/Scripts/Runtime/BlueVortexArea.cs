using System;
using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// ボトルが落ちた場所に広がる水の渦（デバフエリア）1つ。<see cref="BlueVortexSkillRunner"/> が <see cref="Spawn"/> で作る（シーンに置く必要はない）。
    /// 流れ: 破裂（光・衝撃の輪・しぶき・かけら・泡）→ 広がる → 続く（渦・巻き込まれるしぶき・泡・波紋・一定間隔のダメージ）
    /// → 吸い込まれるように縮んで消える（最後のしぶき）→ 足止めが少し残る → 自分を消す。
    /// 敵への効果そのものは <see cref="BlueVortexField"/> がまとめてかける（この渦は「どこに・どれだけ」を渡すだけ）。
    /// 見た目・効果音・揺れは全PCがそれぞれ出す。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class BlueVortexArea : MonoBehaviour
    {
        private enum Phase
        {
            Expanding,
            Holding,
            Collapsing,
            Lingering,
            Done
        }

        private const float ParticleFadeSeconds = 1.2f;

        private static readonly List<BlueVortexArea> ActiveList = new List<BlueVortexArea>();

        // MaterialPropertyBlock は MonoBehaviour のフィールド初期化子では作れない（Unity の制限）ので、Initialize で作る。
        private MaterialPropertyBlock _block;

        private BlueVortexSkill _def;
        private int _owner;
        private Phase _phase;
        private float _phaseTime;
        private float _time;
        private float _rotation;
        private float _counterRotation;
        private float _tickTimer;
        private int _tickFrame = -1;
        private float _shakeTimer;
        private float _rippleTimer;
        private float _spiralCarry;
        private float _bubbleCarry;
        private float _sparkleCarry;
        private float _trappedCarry;
        private float _radius;
        private float _radiusScale = 1f;
        private float _shakeScale = 1f;

        private BlueVortexVisual _main;
        private BlueVortexVisual _counter;
        private Material _glowMaterial;
        private Material _underGlowMaterial;
        private Material _burstMaterial;
        private Material _dropMaterial;
        private Material _bubbleMaterial;
        private BlueGlow _flash;
        private BlueGlow _endFlash;
        private float _flashAge = 99f;
        private float _endFlashAge = 99f;
        private float _flashRotation;
        private BlueRingPool _rings;
        private BlueRingPool _ripples;
        private ParticleSystem _droplets;
        private ParticleSystem _shards;
        private ParticleSystem _bubbles;
        private ParticleSystem _sparkles;
        private BlueSpiralParticles _spiral;
        private Handle<VfxMarker> _loopVfx = Handle<VfxMarker>.Invalid;

        /// <summary>今出ている渦の一覧。</summary>
        public static IReadOnlyList<BlueVortexArea> Active => ActiveList;

        /// <summary>どこかで渦が出た瞬間に呼ばれる（UI・別の演出などに使う）。</summary>
        public static event Action<BlueVortexArea> AnySpawned;

        public BlueVortexSkill Definition => _def;

        /// <summary>渦を出したプレイヤー（0〜3）。</summary>
        public int OwnerPlayerIndex => _owner;

        /// <summary>渦の中心（床の上）。</summary>
        public Vector3 Center => transform.position;

        /// <summary>広がりきったときの半径（m）＝「渦の半径」×この渦の倍率。</summary>
        public float BaseRadius => _def != null ? Mathf.Max(0.05f, _def.area.radius * _radiusScale) : 0.05f;

        /// <summary>今の見た目の半径（m）。</summary>
        public float CurrentRadius => _radius;

        /// <summary>破裂してからの秒数。</summary>
        public float Age => _time;

        /// <summary>敵への効果がもう終わったか。</summary>
        public bool IsFinished => _phase == Phase.Lingering || _phase == Phase.Done;

        internal bool AffectsActors => _def != null && _def.debuff.affectActors;

        internal bool ActorTickThisFrame => _tickFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveList.Clear();
            AnySpawned = null;
        }

        /// <summary>
        /// 渦を出す。center は床の上の位置、owner は出したプレイヤー番号（撃破数・揺れに使う）。
        /// radiusScale = 「渦の半径」に掛ける倍率（まとめて投げたとき用）、shakeScale = 画面の揺れの倍率（重なりすぎないように）。
        /// </summary>
        public static BlueVortexArea Spawn(BlueVortexSkill definition, Vector3 center, int owner, float radiusScale = 1f, float shakeScale = 1f)
        {
            if (definition == null)
            {
                return null;
            }

            // ふつうのシーンのオブジェクトにする（DontSave にすると Play を止めたときに消えずに残ってしまう）。
            var go = new GameObject("[BlueSkill] Vortex P" + (owner + 1));
            go.transform.position = center;
            go.AddComponent<BillboardIgnore>();
            var area = go.AddComponent<BlueVortexArea>();
            area._radiusScale = Mathf.Max(0.01f, radiusScale);
            area._shakeScale = Mathf.Max(0f, shakeScale);
            area.Initialize(definition, owner);
            AnySpawned?.Invoke(area);
            return area;
        }

        /// <summary>途中でも消し始める（すぐ縮んで消える）。</summary>
        public void Dismiss()
        {
            if (_phase == Phase.Expanding || _phase == Phase.Holding)
            {
                SetPhase(Phase.Collapsing);
            }
        }

        /// <summary>
        /// 今の効果（<see cref="BlueVortexField"/> が毎フレーム読む）。効果が無いときは false。
        /// </summary>
        public bool TryGetZone(out BlueVortexZone zone)
        {
            zone = default;
            if (_def == null)
            {
                return false;
            }

            var area = _def.area;
            var debuff = _def.debuff;
            var radius = BaseRadius;
            var strength = 1f;
            var pullBoost = 1f;
            switch (_phase)
            {
                case Phase.Expanding:
                    if (!area.affectWhileExpanding)
                    {
                        return false;
                    }

                    radius = Mathf.Max(0.05f, _radius);
                    break;
                case Phase.Holding:
                    break;
                case Phase.Collapsing:
                    // 吸い込まれるように、引き寄せを強める
                    pullBoost = 1f + 1.5f * Phase01(area.collapseSeconds);
                    break;
                case Phase.Lingering:
                    var linger = debuff.lingerSeconds;
                    if (linger <= 0f || _phaseTime >= linger)
                    {
                        return false;
                    }

                    strength = 1f - _phaseTime / linger;
                    break;
                default:
                    return false;
            }

            var center = transform.position;
            zone = new BlueVortexZone
            {
                center = new Unity.Mathematics.float2(center.x, center.y),
                radius = radius,
                softWidth = radius * Mathf.Clamp01(debuff.edgeSoftness),
                strength = strength,
                hold = Mathf.Clamp01(debuff.holdStrength),
                swirl = _phase == Phase.Lingering ? 0f : Mathf.Max(0f, debuff.swirlSpeed),
                pull = _phase == Phase.Lingering ? 0f : Mathf.Max(0f, debuff.pullSpeed) * pullBoost,
                pullInner = radius * Mathf.Clamp01(debuff.pullInnerRadius),
                direction = debuff.clockwise ? -1f : 1f,
                damageTakenMultiplier = Mathf.Max(1f, debuff.damageTakenMultiplier),
                tickDamage = _tickFrame == Time.frameCount ? Mathf.Max(0f, debuff.tickDamage) : 0f,
                owner = _owner
            };
            return true;
        }

        private void Initialize(BlueVortexSkill definition, int owner)
        {
            _def = definition;
            _owner = owner;
            _block = new MaterialPropertyBlock();
            BuildVisuals();
            ActiveList.Add(this);
            BlueVortexField.Ensure();
            SetPhase(Phase.Expanding);
            Burst();
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
        }

        private void OnEnable()
        {
            if (_def != null && !ActiveList.Contains(this) && _phase != Phase.Done)
            {
                ActiveList.Add(this);
            }
        }

        private void OnDestroy()
        {
            ActiveList.Remove(this);
            FortressEffectPlayer.Stop(ref _loopVfx);
            _main?.Dispose();
            _counter?.Dispose();
            BlueFx.Destroy(_glowMaterial);
            BlueFx.Destroy(_underGlowMaterial);
            BlueFx.Destroy(_burstMaterial);
            BlueFx.Destroy(_dropMaterial);
            BlueFx.Destroy(_bubbleMaterial);
        }

        // ------------------------------------------------------------------
        // 毎フレーム
        // ------------------------------------------------------------------

        private void Update()
        {
            if (_def == null)
            {
                Destroy(gameObject);
                return;
            }

            var dt = Time.deltaTime;
            _time += dt;
            _phaseTime += dt;
            var area = _def.area;

            switch (_phase)
            {
                case Phase.Expanding:
                    if (_phaseTime >= area.expandSeconds)
                    {
                        SetPhase(Phase.Holding);
                    }

                    break;
                case Phase.Holding:
                    if (_phaseTime >= area.durationSeconds)
                    {
                        SetPhase(Phase.Collapsing);
                    }

                    break;
                case Phase.Collapsing:
                    if (_phaseTime >= area.collapseSeconds)
                    {
                        CollapseBurst();
                        SetPhase(Phase.Lingering);
                    }

                    break;
                case Phase.Lingering:
                    if (_phaseTime >= Mathf.Max(_def.debuff.lingerSeconds, ParticleFadeSeconds))
                    {
                        SetPhase(Phase.Done);
                        Destroy(gameObject);
                        return;
                    }

                    break;
            }

            UpdateTicks(dt);
            UpdateShake(dt);
            UpdateVisuals(dt);
        }

        private void SetPhase(Phase next)
        {
            _phase = next;
            _phaseTime = 0f;
            if (next == Phase.Lingering || next == Phase.Done)
            {
                FortressEffectPlayer.Stop(ref _loopVfx);
            }

            if (next == Phase.Done)
            {
                ActiveList.Remove(this);
            }
        }

        private float Phase01(float seconds)
        {
            return seconds > 0.0001f ? Mathf.Clamp01(_phaseTime / seconds) : 1f;
        }

        private void UpdateTicks(float dt)
        {
            var debuff = _def.debuff;
            var ticking = _phase == Phase.Holding || (_phase == Phase.Expanding && _def.area.affectWhileExpanding);
            if (!ticking || debuff.tickDamage <= 0f)
            {
                return;
            }

            _tickTimer += dt;
            var interval = Mathf.Max(0.05f, debuff.tickInterval);
            if (_tickTimer < interval)
            {
                return;
            }

            _tickTimer -= interval;
            _tickFrame = Time.frameCount;
            var center = transform.position;
            FortressEffectPlayer.PlayOnce(_def.sounds.onTick, center, Quaternion.identity, _owner);

            // 中の敵を白く光らせる（ダメージ0の判定を1つ置く。Client でも光る）。ダメージ本体は BlueVortexField が与える。
            var swarm = SwarmSystem.Current;
            if (debuff.tickFlash && swarm != null)
            {
                swarm.QueueBeam(center, center, Mathf.Max(0.05f, _radius), 0f, 0, _owner);
            }

            var look = _def.look;
            _rings.Spawn(center + Vector3.forward * -0.02f, _radius * 0.4f, _radius * 2.15f, 0.45f, look.foamColor * 0.7f, 0.05f);
            for (var i = 0; i < 14; i++)
            {
                var dir = BlueFx.RandomDirection();
                var p = center + (Vector3)(dir * UnityEngine.Random.Range(0.3f, 0.95f) * _radius);
                BlueFx.Emit(_sparkles, p + new Vector3(0f, 0f, -0.1f), new Vector3(0f, 0f, -UnityEngine.Random.Range(0.6f, 1.6f)),
                    UnityEngine.Random.Range(0.1f, 0.2f), UnityEngine.Random.Range(0.3f, 0.5f), Color.white);
            }
        }

        private void UpdateShake(float dt)
        {
            var shake = _def.cameraShake;
            if (_phase != Phase.Holding || shake.whileActiveStrength <= 0f)
            {
                return;
            }

            _shakeTimer -= dt;
            if (_shakeTimer > 0f)
            {
                return;
            }

            _shakeTimer = Mathf.Max(0.05f, shake.whileActiveInterval);
            BlueFx.Shake(shake.whileActive, shake.whileActiveStrength * _shakeScale, shake.ownerViewOnly, _owner);
        }

        // ------------------------------------------------------------------
        // 見た目
        // ------------------------------------------------------------------

        private void BuildVisuals()
        {
            var look = _def.look;
            var shaders = _def.shaders;
            var parent = transform;
            var overlay = look.overlaySortingOrder;

            _main = new BlueVortexVisual(parent, "Vortex", shaders.vortex, look, 0);
            _counter = new BlueVortexVisual(parent, "VortexInner", shaders.vortex, look, 1);

            _glowMaterial = BlueFx.CreateMaterial(shaders.glow, BlueFx.GlowShaderName, BlueFx.OverlayRenderQueue);
            _underGlowMaterial = BlueFx.CreateMaterial(shaders.glow, BlueFx.GlowShaderName, look.vortexRenderQueue + 1);
            _burstMaterial = BlueFx.CreateMaterial(shaders.particle, BlueFx.ParticleShaderName, BlueFx.OverlayRenderQueue);
            _dropMaterial = BlueFx.CreateMaterial(shaders.particle, BlueFx.ParticleShaderName, BlueFx.OverlayRenderQueue);
            _bubbleMaterial = BlueFx.CreateMaterial(shaders.particle, BlueFx.ParticleShaderName, BlueFx.OverlayRenderQueue);
            _bubbleMaterial.SetFloat(BlueFx.ModeId, 1f);
            ApplyParticleBrightness();

            _flash = new BlueGlow(BlueFx.CreateRenderer(parent, "BurstFlash", BlueFx.SharedQuad, _glowMaterial, overlay + 4));
            _endFlash = new BlueGlow(BlueFx.CreateRenderer(parent, "EndFlash", BlueFx.SharedQuad, _glowMaterial, overlay + 4));
            _rings = new BlueRingPool(parent, "Ring", _glowMaterial, overlay + 3, 10);
            _ripples = new BlueRingPool(parent, "Ripple", _underGlowMaterial, 2, 8);

            _droplets = BlueFx.CreateParticles(parent, "Droplets", _burstMaterial, overlay + 2, 0.06f, 1.4f, 1500, 2.2f, 0f);
            _shards = BlueFx.CreateParticles(parent, "Shards", _burstMaterial, overlay + 3, 0.05f, 1.2f, 200, 1.6f, 0f);
            _bubbles = BlueFx.CreateParticles(parent, "Bubbles", _bubbleMaterial, overlay + 1, 0f, 0f, 600, 0.8f, 0f);
            _sparkles = BlueFx.CreateParticles(parent, "Sparkles", _dropMaterial, overlay + 2, 0f, 0f, 600, 0.5f, 0f);
            var spiralSystem = BlueFx.CreateParticles(parent, "SpiralDroplets", _dropMaterial, overlay + 1, 0.05f, 1.6f, 500, 0f, 0f);
            _spiral = new BlueSpiralParticles(spiralSystem, 500);

            // 渦はふつうのシーンのオブジェクトなので、子も同じにする（Play を止めたときに一緒に消えるように）。
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.hideFlags = HideFlags.None;
            }
        }

        private void ApplyParticleBrightness()
        {
            _burstMaterial.SetFloat(BlueFx.IntensityId, _def.burst.particleBrightness);
            _dropMaterial.SetFloat(BlueFx.IntensityId, _def.look.particleBrightness);
            _bubbleMaterial.SetFloat(BlueFx.IntensityId, _def.look.particleBrightness);
        }

        /// <summary>落ちた瞬間の破裂。</summary>
        private void Burst()
        {
            var center = transform.position;
            var burst = _def.burst;
            var radius = BaseRadius;
            var rot = transform.rotation;

            FortressEffectPlayer.PlayOnce(_def.sounds.onBurst, center, rot, _owner);
            FortressEffectPlayer.Stop(ref _loopVfx);
            _loopVfx = FortressEffectPlayer.StartFollowing(_def.sounds.whileVortex, transform, _owner);
            BlueFx.Shake(_def.cameraShake.onBurst, _def.cameraShake.burstStrength * _shakeScale, _def.cameraShake.ownerViewOnly, _owner);

            _flashAge = 0f;
            _flashRotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            var lift = new Vector3(0f, 0f, -0.05f);
            for (var k = 0; k < burst.shockwaveCount; k++)
            {
                var to = radius * 2f * burst.shockwaveScale * (1f - 0.15f * k);
                _rings.Spawn(center + lift, radius * 0.3f, to, 0.55f + 0.12f * k, burst.shockwaveColor, 0.07f - 0.01f * k, 0.07f * k);
            }

            for (var i = 0; i < burst.dropletCount; i++)
            {
                var dir = BlueFx.RandomDirection();
                var speed = burst.dropletSpeed * UnityEngine.Random.Range(0.3f, 1.1f);
                var up = UnityEngine.Random.Range(0f, 5f);
                BlueFx.Emit(_droplets, center + lift + (Vector3)(dir * UnityEngine.Random.Range(0f, 0.4f)),
                    new Vector3(dir.x * speed, dir.y * speed, -up), UnityEngine.Random.Range(0.12f, 0.32f),
                    UnityEngine.Random.Range(0.45f, 0.95f), burst.dropletColor);
            }

            // 水の王冠: カメラの方向へ大きく跳ね上がるしぶき
            var crown = Mathf.Max(0, burst.crownSpikes) * 2;
            for (var i = 0; i < crown; i++)
            {
                var dir = BlueFx.Rotate(Vector2.right, 360f * i / Mathf.Max(1, crown) + UnityEngine.Random.Range(-8f, 8f));
                var speed = UnityEngine.Random.Range(3f, 6f);
                BlueFx.Emit(_droplets, center + lift + (Vector3)(dir * radius * 0.25f),
                    new Vector3(dir.x * speed, dir.y * speed, -UnityEngine.Random.Range(6f, 10f)), UnityEngine.Random.Range(0.25f, 0.42f),
                    UnityEngine.Random.Range(0.5f, 0.8f), Color.Lerp(burst.dropletColor, Color.white, 0.4f));
            }

            for (var i = 0; i < burst.shardCount; i++)
            {
                var dir = BlueFx.RandomDirection();
                var speed = UnityEngine.Random.Range(6f, 13f);
                BlueFx.Emit(_shards, center + lift, new Vector3(dir.x * speed, dir.y * speed, -UnityEngine.Random.Range(1f, 4f)),
                    UnityEngine.Random.Range(0.08f, 0.16f), UnityEngine.Random.Range(0.35f, 0.6f), burst.shardColor);
            }

            for (var i = 0; i < burst.bubbleCount; i++)
            {
                var p = center + (Vector3)(UnityEngine.Random.insideUnitCircle * radius * 0.7f);
                BlueFx.Emit(_bubbles, p + lift, new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.6f, 0.6f), -UnityEngine.Random.Range(1f, 2.5f)),
                    UnityEngine.Random.Range(0.15f, 0.4f), UnityEngine.Random.Range(0.7f, 1.4f), _def.look.particleColor);
            }
        }

        /// <summary>渦が縮みきって消える瞬間のしぶき。</summary>
        private void CollapseBurst()
        {
            var center = transform.position;
            var burst = _def.burst;
            var radius = BaseRadius;
            FortressEffectPlayer.PlayOnce(_def.sounds.onEnd, center, transform.rotation, _owner);
            BlueFx.Shake(_def.cameraShake.onCollapse, _def.cameraShake.collapseStrength * _shakeScale, _def.cameraShake.ownerViewOnly, _owner);
            _endFlashAge = 0f;
            var lift = new Vector3(0f, 0f, -0.05f);
            _rings.Spawn(center + lift, radius * 0.15f, radius * 1.8f, 0.5f, _def.look.rimColor, 0.06f);
            for (var i = 0; i < burst.collapseSplashCount; i++)
            {
                var dir = BlueFx.RandomDirection();
                var speed = burst.dropletSpeed * UnityEngine.Random.Range(0.25f, 0.75f);
                BlueFx.Emit(_droplets, center + lift, new Vector3(dir.x * speed, dir.y * speed, -UnityEngine.Random.Range(2f, 7f)),
                    UnityEngine.Random.Range(0.1f, 0.26f), UnityEngine.Random.Range(0.4f, 0.8f), burst.dropletColor);
            }

            _spiral.Clear();
        }

        private void UpdateVisuals(float dt)
        {
            var look = _def.look;
            var area = _def.area;
            var debuff = _def.debuff;
            var center = transform.position;
            var floor = center + new Vector3(0f, 0f, -look.floorLift);
            var baseRadius = BaseRadius;
            var visualDir = debuff.clockwise ? 1f : -1f;
            var spin = look.spinSpeed * Mathf.PI * 2f;
            var spinMul = 1f;
            var opacity = 1f;
            var suck = 0f;
            var emit = 0f;

            switch (_phase)
            {
                case Phase.Expanding:
                {
                    var t = Phase01(area.expandSeconds);
                    _radius = baseRadius * Mathf.Max(0f, area.expandCurve != null && area.expandCurve.length > 0 ? area.expandCurve.Evaluate(t) : t);
                    spinMul = Mathf.Lerp(3f, 1f, t);
                    opacity = Mathf.Clamp01(t * 3f);
                    emit = t;
                    break;
                }
                case Phase.Holding:
                    _radius = baseRadius * (1f + 0.015f * Mathf.Sin(_time * 3.1f));
                    emit = 1f;
                    break;
                case Phase.Collapsing:
                {
                    var t = Phase01(area.collapseSeconds);
                    var ease = t * t;
                    _radius = baseRadius * (1f - ease);
                    spinMul = 1f + 4f * t;
                    suck = t;
                    opacity = 1f - ease * ease;
                    emit = 1f - t;
                    break;
                }
                default:
                    _radius = 0f;
                    opacity = 0f;
                    break;
            }

            _rotation += visualDir * spin * spinMul * dt;
            _counterRotation -= visualDir * spin * spinMul * 1.35f * dt;

            // 消える前の点滅
            var rim = 1f + 0.25f * Mathf.Sin(_time * 6f);
            if (_phase == Phase.Holding && area.warnSeconds > 0f)
            {
                var remaining = area.durationSeconds - _phaseTime;
                if (remaining < area.warnSeconds)
                {
                    var warn = 1f - remaining / area.warnSeconds;
                    rim *= Mathf.Lerp(1f, 0.2f + 1.3f * Mathf.Abs(Mathf.Sin(_time * Mathf.PI * (3f + 4f * warn))), warn);
                }
            }

            _main.Draw(look, floor, _radius, _rotation, _time, rim, debuff.clockwise, suck, opacity, 1f, 0);
            if (look.counterLayer)
            {
                _counter.Draw(look, floor + new Vector3(0f, 0f, -0.005f), _radius * 0.62f, _counterRotation, _time * 1.3f + 7f, 0f, !debuff.clockwise, suck,
                    opacity * 0.45f, 0.8f, 1);
            }
            else
            {
                _counter.Hide();
            }

#if UNITY_EDITOR
            ApplyParticleBrightness();
#endif

            UpdateFlashes(dt, center, baseRadius);
            _rings.Tick(dt);
            _ripples.Tick(dt);

            var lift = new Vector3(0f, 0f, -0.05f);
            if (emit > 0f && _radius > 0.05f)
            {
                EmitContinuous(dt, center + lift, emit, visualDir);
            }

            var inner = Mathf.Max(0.05f, _radius * look.eyeSize);
            var spiralSpeed = _phase == Phase.Collapsing ? 1f + 3f * Phase01(area.collapseSeconds) : 1f;
            _spiral.Tick(dt, center + lift, inner, spiralSpeed);
        }

        private void UpdateFlashes(float dt, Vector3 center, float radius)
        {
            var burst = _def.burst;
            const float flashLife = 0.45f;
            _flashAge += dt;
            if (_flashAge < flashLife && burst.flashScale > 0f)
            {
                var t = _flashAge / flashLife;
                var size = radius * 2f * burst.flashScale * Mathf.Lerp(0.55f, 1.15f, 1f - (1f - t) * (1f - t));
                var color = burst.flashColor;
                color.a *= (1f - t) * (1f - t);
                _flash.Show(center + new Vector3(0f, 0f, -0.1f), 0f, new Vector2(size, size));
                _flash.Apply(_block, color,
                    new Vector4(0.42f, 1.7f, burst.crownSpikes, 1.5f),
                    new Vector4(4.5f, _flashRotation + t * 0.6f, 0.55f + 0.3f * t, 0.07f),
                    new Vector4(0.8f, 0f, _flashRotation, 0.7f));
            }
            else
            {
                _flash.Hide();
            }

            const float endLife = 0.35f;
            _endFlashAge += dt;
            if (_endFlashAge < endLife)
            {
                var t = _endFlashAge / endLife;
                var size = radius * 1.4f * Mathf.Lerp(0.3f, 1f, t);
                var color = _def.look.rimColor;
                color.a *= (1f - t);
                _endFlash.Show(center + new Vector3(0f, 0f, -0.1f), 0f, new Vector2(size, size));
                _endFlash.Apply(_block, color, new Vector4(0.5f, 2f, 8f, 0.8f), new Vector4(5f, t * 2f, 0f, 0f), new Vector4(0f, 0f, 3.1f, 0.5f));
            }
            else
            {
                _endFlash.Hide();
            }
        }

        private void EmitContinuous(float dt, Vector3 center, float amount, float visualDir)
        {
            var look = _def.look;
            var radius = _radius;
            var color = look.particleColor;

            // 渦に巻き込まれて中心へ吸い込まれるしぶき（見た目の渦と同じ向きに回る）
            var spiralCount = BlueFx.Accumulate(ref _spiralCarry, look.spiralDropletRate * amount * dt);
            var angularDir = -visualDir;
            for (var i = 0; i < spiralCount; i++)
            {
                _spiral.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f), radius * UnityEngine.Random.Range(0.8f, 1.02f),
                    angularDir * UnityEngine.Random.Range(1.6f, 2.8f), radius * UnityEngine.Random.Range(0.3f, 0.55f),
                    UnityEngine.Random.Range(1.8f, 2.6f), UnityEngine.Random.Range(0.1f, 0.22f), color, UnityEngine.Random.Range(0f, 0.12f));
            }

            // 浮かぶ泡
            var bubbleCount = BlueFx.Accumulate(ref _bubbleCarry, look.bubbleRate * amount * dt);
            for (var i = 0; i < bubbleCount; i++)
            {
                var p = center + (Vector3)(UnityEngine.Random.insideUnitCircle * radius * 0.95f);
                BlueFx.Emit(_bubbles, p, new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(-0.2f, 0.2f), -UnityEngine.Random.Range(0.5f, 1.3f)),
                    UnityEngine.Random.Range(0.1f, 0.3f), UnityEngine.Random.Range(0.8f, 1.5f), color);
            }

            // ふちのきらめき（ふちに沿って流れる）
            var sparkleCount = BlueFx.Accumulate(ref _sparkleCarry, look.rimSparkleRate * amount * dt);
            for (var i = 0; i < sparkleCount; i++)
            {
                var dir = BlueFx.RandomDirection();
                var tangent = new Vector2(-dir.y, dir.x) * angularDir;
                var p = center + (Vector3)(dir * radius * UnityEngine.Random.Range(0.86f, 1f));
                BlueFx.Emit(_sparkles, p, (Vector3)(tangent * UnityEngine.Random.Range(1f, 2.4f) + dir * 0.2f) + new Vector3(0f, 0f, -0.3f),
                    UnityEngine.Random.Range(0.07f, 0.15f), UnityEngine.Random.Range(0.35f, 0.7f), Color.Lerp(color, Color.white, 0.6f));
            }

            // 中心へ吸い込まれていく波紋（ふちから内側へ縮む輪）
            if (look.rippleInterval > 0f)
            {
                _rippleTimer -= dt;
                if (_rippleTimer <= 0f)
                {
                    _rippleTimer = look.rippleInterval;
                    _ripples.Spawn(center, radius * 2.05f, radius * 0.35f, 1.1f, look.rimColor * 0.55f, 0.045f);
                }
            }

            // つかまっている敵のところから出る泡
            var samples = BlueVortexField.TrappedSamples;
            if (look.trappedBubbleRate > 0f && samples.Count > 0)
            {
                var trappedCount = BlueFx.Accumulate(ref _trappedCarry, look.trappedBubbleRate * amount * dt);
                var r2 = radius * radius;
                for (var i = 0; i < trappedCount; i++)
                {
                    var sample = samples[UnityEngine.Random.Range(0, samples.Count)];
                    if ((sample - (Vector2)center).sqrMagnitude > r2)
                    {
                        continue;
                    }

                    BlueFx.Emit(_bubbles, new Vector3(sample.x, sample.y, center.z - 0.3f),
                        new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.3f, 0.3f), -UnityEngine.Random.Range(0.8f, 1.6f)),
                        UnityEngine.Random.Range(0.1f, 0.22f), UnityEngine.Random.Range(0.5f, 0.9f), Color.Lerp(color, Color.white, 0.3f));
                }
            }
        }
    }
}
