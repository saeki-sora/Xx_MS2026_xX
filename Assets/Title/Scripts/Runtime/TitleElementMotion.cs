using System;
using MS2026.Fortress;
using MS2026.ShaderFX;
using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// タイトル画面の配置物1つ分の動き（登場 → 待機中のゆらぎ → スタート時の退場）。
    /// このオブジェクト（親）の位置・向き・大きさを動かし、子の SpriteRenderer の透明度と
    /// ShaderFX（EffectTarget があれば）のディゾルブ・白フラッシュ・グリッチを操作する。
    /// 子は画像（SpriteRenderer）でも 3D モデル（MeshRenderer）でもよい。画像の差し替えは子の Sprite を替えるだけ
    /// （大きさは <see cref="TitleSpriteFit"/> が合わせる）。モデルは子ごと差し替える。
    /// <see cref="TitleScreenController"/> が <see cref="PlayEnter"/> を呼ぶまでは隠れて待つ。
    /// 編集中（再生していないとき）は何もしないので、シーン上の見た目が最終的な配置。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleElementMotion : MonoBehaviour
    {
        public enum ExitStyle
        {
            [InspectorName("何もしない")] None,
            [InspectorName("フェードアウト")] FadeOut,
            [InspectorName("点滅して消える")] BlinkOut,
            [InspectorName("膨らみながら消える")] PunchOut,
        }

        [Header("登場")]
        [Tooltip("登場の合図（オープニングの歯磨き粉の幕が抜け始めた後）から、登場を始めるまでの秒数。")]
        public float enterDelay;
        [Tooltip("登場にかける秒数。0なら一瞬で出る。")]
        public float enterDuration = 0.8f;
        [Tooltip("登場の動きの緩急。")]
        public TitleEaseType enterEase = TitleEaseType.OutCubic;
        [Tooltip("ONなら透明から現れる。")]
        public bool fadeIn = true;
        [Tooltip("最終位置から見て、どこから動いてくるか（ワールドの単位）。(0,0,0)なら動かない。")]
        public Vector3 moveFrom;
        [Tooltip("登場開始時の大きさ（1 = 最終の大きさ）。")]
        public float scaleFrom = 1f;
        [Tooltip("登場開始時の向きのずれ（度。X/Y/Z軸まわり）。落ちながら回転させたいときに使う。")]
        public Vector3 rotateFrom;
        [Tooltip("ONなら燃え広がるように現れる（ShaderFXのディゾルブ。子に Effect Target が必要）。")]
        public bool burnIn;
        [Tooltip("登場し終わった瞬間の白フラッシュの強さ（0〜1、0で無し。子に Effect Target が必要）。")]
        [Range(0f, 1f)] public float flashOnLand;
        [Tooltip("登場し終わった瞬間のカメラの揺れの強さ（0で無し）。")]
        public float shakeOnLand;
        [Tooltip("着地の瞬間に「むにっ」とつぶれる強さ（0.2 = 横に20%広がり縦に20%縮む。0で無し）。")]
        [Range(0f, 0.5f)] public float squashOnLand;
        [Tooltip("登場し終わった瞬間に出すエフェクト・効果音（D-Drive）。")]
        public FortressEffect onLand = new() { tintWithPlayerColor = false };
        [Tooltip("登場中の電脳ノイズ（グリッチ）の強さ（0〜1、0で無し）。出始めが一番強く、出きると消える。子に Effect Target（グリッチ入り）が必要。")]
        [Range(0f, 1f)] public float glitchIn;

        [Header("待機中のゆらぎ")]
        [Tooltip("ふわふわ上下左右に揺れる幅（ワールドの単位）。")]
        public Vector3 floatAmplitude;
        [Tooltip("ふわふわの速さ（1秒あたりの往復回数）。")]
        public float floatSpeed = 0.5f;
        [Tooltip("大きさの脈打ち（0.05 = ±5%）。")]
        public float pulseAmount;
        [Tooltip("脈打ちの速さ（1秒あたりの回数）。")]
        public float pulseSpeed = 1f;
        [Tooltip("点滅するときの一番薄い透明度（1なら点滅しない）。")]
        [Range(0f, 1f)] public float blinkMinAlpha = 1f;
        [Tooltip("点滅の速さ（1秒あたりの回数）。")]
        public float blinkSpeed = 1f;
        [Tooltip("何秒ごとに「キラッ」と光るか（0で光らない。子に Effect Target が必要）。")]
        public float glintInterval;
        [Tooltip("キラッの強さ（0〜1）。")]
        [Range(0f, 1f)] public float glintStrength = 0.5f;
        [Tooltip("何秒ごとに一瞬「ザザッ」と電脳ノイズが走るか（0で走らない。子に Effect Target（グリッチ入り）が必要）。実際の間隔は±30%ばらつく。")]
        public float glitchInterval;
        [Tooltip("ザザッの強さ（0〜1）。")]
        [Range(0f, 1f)] public float glitchStrength = 0.6f;

        [Header("スタート時（退場）")]
        public ExitStyle exitStyle = ExitStyle.None;
        [Tooltip("退場にかける秒数。")]
        public float exitDuration = 0.5f;
        [Tooltip("スタートの瞬間の白フラッシュの強さ（0〜1）。")]
        [Range(0f, 1f)] public float flashOnExit;
        [Tooltip("スタートの瞬間の電脳ノイズの強さ（0〜1）。")]
        [Range(0f, 1f)] public float glitchOnExit;

        /// <summary>登場し終わった瞬間に呼ばれる。</summary>
        public event Action<TitleElementMotion> Landed;

        private enum Phase { Dormant, Waiting, Entering, Idle, Exiting }

        private const float FlashFadeSeconds = 0.35f;
        private const float GlintSeconds = 0.25f;
        private const float GlitchBurstSeconds = 0.18f;
        private const float SquashDamping = 7f;
        private const float SquashFrequency = 22f;
        private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");

        private Phase phase = Phase.Dormant;
        private float phaseTime;
        private float idleTime;
        private float flashAmount;
        private float glintTimer;
        private float glitchBurst;
        private float nextGlitchIn;
        private float squashTime = float.MaxValue;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private SpriteRenderer[] sprites = Array.Empty<SpriteRenderer>();
        private float[] baseAlphas = Array.Empty<float>();
        private EffectTarget[] effectTargets = Array.Empty<EffectTarget>();
        private bool captured;

        // グリッチを使う設定のときだけ値を書き込む（書き込むと SRP Batcher のまとめ描きから外れるため）。
        private bool UsesGlitch => glitchIn > 0f || glitchInterval > 0f || glitchOnExit > 0f;

        public bool IsEntering => phase == Phase.Dormant || phase == Phase.Waiting || phase == Phase.Entering;
        public float EnterEndTime => Mathf.Max(0f, enterDelay) + Mathf.Max(0f, enterDuration);
        public float ExitDuration => exitStyle == ExitStyle.None ? 0f : Mathf.Max(0f, exitDuration);

        private void Awake()
        {
            Capture();
            // 登場の合図までは隠しておく（最終位置が一瞬見えないように）。
            SetVisible(false);
            Apply(EnterPose(0f));
        }

        private void Capture()
        {
            if (captured) return;
            captured = true;
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            baseAlphas = new float[sprites.Length];
            for (var i = 0; i < sprites.Length; i++) baseAlphas[i] = sprites[i].color.a;
            effectTargets = GetComponentsInChildren<EffectTarget>(true);
        }

        /// <summary>登場前（隠れて合図を待つ状態）に戻す。「最初から再生」用。</summary>
        public void ResetToStart()
        {
            Capture();
            phase = Phase.Dormant;
            phaseTime = 0f;
            idleTime = 0f;
            flashAmount = 0f;
            glintTimer = 0f;
            glitchBurst = 0f;
            squashTime = float.MaxValue;
            SetVisible(false);
            Apply(EnterPose(0f));
        }

        /// <summary>登場を最初から始める。</summary>
        public void PlayEnter()
        {
            Capture();
            SetVisible(true);
            phase = Phase.Waiting;
            phaseTime = 0f;
            idleTime = 0f;
            glintTimer = 0f;
            glitchBurst = 0f;
            ScheduleNextGlitch();
        }

        /// <summary>登場を飛ばして待機中にする（ボタン連打でのスキップ用）。</summary>
        public void SkipToIdle()
        {
            if (!IsEntering) return;
            SetVisible(true);
            EnterIdle(false);
        }

        /// <summary>スタート時の退場を始める。</summary>
        public void PlayExit()
        {
            Capture();
            phase = Phase.Exiting;
            phaseTime = 0f;
            if (flashOnExit > 0f) flashAmount = Mathf.Max(flashAmount, flashOnExit);
            if (glitchOnExit > 0f) glitchBurst = Mathf.Max(glitchBurst, glitchOnExit);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            phaseTime += dt;
            flashAmount = Mathf.MoveTowards(flashAmount, 0f, dt / FlashFadeSeconds);
            glitchBurst = Mathf.MoveTowards(glitchBurst, 0f, dt / GlitchBurstSeconds);

            switch (phase)
            {
                case Phase.Dormant:
                    break;

                case Phase.Waiting:
                    if (phaseTime >= enterDelay)
                    {
                        phase = Phase.Entering;
                        phaseTime -= Mathf.Max(0f, enterDelay);
                        goto case Phase.Entering;
                    }
                    Apply(EnterPose(0f));
                    break;

                case Phase.Entering:
                {
                    var t = enterDuration <= 0f ? 1f : phaseTime / enterDuration;
                    if (t >= 1f)
                    {
                        EnterIdle(true);
                        goto case Phase.Idle;
                    }
                    Apply(EnterPose(t));
                    break;
                }

                case Phase.Idle:
                    idleTime += dt;
                    UpdateGlint(dt);
                    UpdateGlitch(dt);
                    Apply(IdlePose());
                    break;

                case Phase.Exiting:
                    idleTime += dt;
                    Apply(ExitPose());
                    break;
            }
        }

        private void EnterIdle(bool landed)
        {
            phase = Phase.Idle;
            phaseTime = 0f;
            idleTime = 0f;
            if (!landed) return;
            if (flashOnLand > 0f) flashAmount = Mathf.Max(flashAmount, flashOnLand);
            if (squashOnLand > 0f) squashTime = 0f;
            TitleEffects.Play(onLand, transform.position);
            Landed?.Invoke(this);
        }

        private void UpdateGlint(float dt)
        {
            if (glintInterval <= 0f) return;
            glintTimer = Mathf.Repeat(glintTimer + dt, glintInterval);
        }

        private void UpdateGlitch(float dt)
        {
            if (glitchInterval <= 0f) return;
            nextGlitchIn -= dt;
            if (nextGlitchIn > 0f) return;
            glitchBurst = Mathf.Max(glitchBurst, glitchStrength);
            ScheduleNextGlitch();
        }

        private void ScheduleNextGlitch()
        {
            nextGlitchIn = glitchInterval * UnityEngine.Random.Range(0.7f, 1.3f);
        }

        private struct Pose
        {
            public Vector3 offset;
            public float scale;
            public Vector3 rotation;
            public float alpha;
            public float dissolve;
            public float glitch;
        }

        private Pose EnterPose(float t)
        {
            var e = TitleEase.Evaluate(enterEase, t);
            return new Pose
            {
                offset = Vector3.LerpUnclamped(moveFrom, Vector3.zero, e),
                scale = Mathf.LerpUnclamped(scaleFrom, 1f, e),
                rotation = Vector3.LerpUnclamped(rotateFrom, Vector3.zero, e),
                alpha = fadeIn ? Mathf.Clamp01(t * 1.5f) : 1f,
                dissolve = burnIn ? 1f - Mathf.Clamp01(t) : 0f,
                glitch = glitchIn * (1f - Mathf.Clamp01(t)),
            };
        }

        private Pose IdlePose()
        {
            var floatPhase = idleTime * floatSpeed * Mathf.PI * 2f;
            var offset = new Vector3(
                floatAmplitude.x * Mathf.Sin(floatPhase),
                floatAmplitude.y * Mathf.Sin(floatPhase + 1.3f),
                floatAmplitude.z * Mathf.Sin(floatPhase + 2.1f));

            var alpha = 1f;
            if (blinkMinAlpha < 1f)
            {
                var wave = 0.5f + 0.5f * Mathf.Cos(idleTime * blinkSpeed * Mathf.PI * 2f);
                alpha = Mathf.Lerp(blinkMinAlpha, 1f, wave);
            }

            return new Pose
            {
                offset = offset,
                scale = 1f + pulseAmount * Mathf.Sin(idleTime * pulseSpeed * Mathf.PI * 2f),
                alpha = alpha,
            };
        }

        private Pose ExitPose()
        {
            var pose = IdlePose();
            var duration = Mathf.Max(0.0001f, exitDuration);
            var t = Mathf.Clamp01(phaseTime / duration);
            switch (exitStyle)
            {
                case ExitStyle.FadeOut:
                    pose.alpha *= 1f - t;
                    break;
                case ExitStyle.BlinkOut:
                    // 1秒に12回の速い点滅をしながら、最後に消える。
                    pose.alpha = t >= 1f ? 0f : (Mathf.Repeat(phaseTime * 12f, 1f) < 0.5f ? 1f : 0.15f);
                    pose.scale *= 1f + 0.08f * TitleEase.Evaluate(TitleEaseType.OutCubic, t);
                    break;
                case ExitStyle.PunchOut:
                    pose.scale *= 1f + 0.35f * TitleEase.Evaluate(TitleEaseType.OutCubic, t);
                    pose.alpha *= 1f - t;
                    break;
            }
            return pose;
        }

        private void Apply(Pose pose)
        {
            transform.localPosition = basePosition + pose.offset;
            transform.localRotation = Quaternion.Euler(pose.rotation) * baseRotation;
            var squash = SquashAmount();
            transform.localScale = Vector3.Scale(baseScale * pose.scale, new Vector3(1f + squash, 1f - squash, 1f + squash));

            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                var c = sprites[i].color;
                c.a = baseAlphas[i] * pose.alpha;
                sprites[i].color = c;
            }

            if (effectTargets.Length == 0) return;
            var flash = Mathf.Max(flashAmount * flashAmount, GlintAmount());
            var glitch = Mathf.Max(pose.glitch, glitchBurst);
            var useGlitch = UsesGlitch;
            foreach (var target in effectTargets)
            {
                if (target == null) continue;
                target.SetInstanceDissolveAmount(pose.dissolve);
                target.SetInstanceHitFlashAmount(flash);
                if (useGlitch) target.SetInstanceFloat(GlitchAmountId, glitch);
            }
        }

        // 着地の「むにっ」: つぶれて、揺り戻しながら元に戻る（減衰する振動）。
        private float SquashAmount()
        {
            if (squashOnLand <= 0f || squashTime == float.MaxValue) return 0f;
            squashTime += Time.deltaTime;
            return squashOnLand * Mathf.Exp(-squashTime * SquashDamping) * Mathf.Cos(squashTime * SquashFrequency);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        private float GlintAmount()
        {
            if (glintInterval <= 0f || phase != Phase.Idle || glintTimer > GlintSeconds) return 0f;
            return glintStrength * Mathf.Sin(glintTimer / GlintSeconds * Mathf.PI);
        }
    }
}
