using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>全PCがホストの決着位置へ寄り、泡→CLEAR→ノイズ→既存リザルトへ進む。</summary>
    [DefaultExecutionOrder(1100)]
    internal sealed class ClearMatchIntro : MonoBehaviour
    {
        private static readonly Color Mint = new Color(0.60f, 0.87f, 0.81f);
        private FortressCameraRig _rig;
        private Camera _camera;
        private CameraViewSettings _before, _zoom;
        private Vector3 _beforePosition, _focus;
        private Vector3 _rigPosition;
        private Quaternion _cameraRotation, _rigRotation;
        private float _beforeSize, _beforeFov, _started, _effectStart;
        private bool _restored, _done;
        private Action _onFinished;
        private RectTransform _viewport;
        private CanvasGroup _scene;
        private RawImage _background, _portrait, _clearLogo;
        private RectTransform _gauge;
        private RenderTexture _portraitTexture;
        private Material _logoNoise;
        private Material _foamMaterial;
        private Image _cover;
        private const int BubbleColumns = 28;
        private const int BubbleRows = 18;
        private const int BubbleCount = BubbleColumns * BubbleRows;
        private readonly ClearBubbleGraphic[] _bubbles = new ClearBubbleGraphic[BubbleCount];
        private readonly ClearBubbleBurstGraphic[] _bubblePopRings = new ClearBubbleBurstGraphic[BubbleCount];
        private readonly ClearBubbleGraphic[] _bubbleSprays = new ClearBubbleGraphic[BubbleCount];
        private readonly ClearBubbleGraphic[] _edgeFoam = new ClearBubbleGraphic[28];
        private readonly ClearBubbleGraphic[] _microFoam = new ClearBubbleGraphic[192];
        private readonly RawImage[] _titleSlices = new RawImage[48];
        private readonly GameOverRippleGraphic[] _ripples = new GameOverRippleGraphic[3];

        public static ClearMatchIntro Begin(Transform owner, MatchResult result, Action finished)
        {
            var root = new GameObject("Clear decision intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            canvas.sortingOrder = 850;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var intro = root.AddComponent<ClearMatchIntro>();
            intro._focus = result.finishPosition;
            intro._onFinished = finished;
            intro.Prepare();
            return intro;
        }

        private static RectTransform Full(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void Prepare()
        {
            _started = Time.unscaledTime;
            _effectStart = Time.time;
            _rig = FortressCameraRig.Active;
            _camera = _rig != null && _rig.targetCamera != null ? _rig.targetCamera : Camera.main;
            if (_camera != null)
            {
                _beforePosition = _camera.transform.position;
                _cameraRotation = _camera.transform.rotation;
                _beforeSize = _camera.orthographicSize;
                _beforeFov = _camera.fieldOfView;
                if (_rig != null)
                {
                    _rigPosition = _rig.transform.position;
                    _rigRotation = _rig.transform.rotation;
                    _before = _rig.CurrentView;
                    _zoom = _before.WithVisibleHalfHeight(Mathf.Min(3.2f, _before.VisibleHalfHeight));
                    _zoom.center = _focus;
                }
            }
            CoreBreakSlowMotion.BeginDecisionSequence();
            _viewport = Full(transform, "Gameplay viewport");
            if (_camera != null)
            {
                _viewport.anchorMin = _camera.rect.min;
                _viewport.anchorMax = _camera.rect.max;
            }
            _viewport.gameObject.AddComponent<RectMask2D>();
            var scene = Full(_viewport, "Clear reveal");
            _scene = scene.gameObject.AddComponent<CanvasGroup>();
            _scene.alpha = 0f;
            var baseColor = scene.gameObject.AddComponent<Image>();
            baseColor.color = new Color(0.92f, 0.98f, 0.97f);
            baseColor.raycastTarget = false;
            _background = Full(scene, "Moving pattern").gameObject.AddComponent<RawImage>();
            _background.texture = Resources.Load<Texture2D>("GameOver/background");
            _background.color = Color.white;
            _background.raycastTarget = false;

            _portrait = Full(scene, "Final core").gameObject.AddComponent<RawImage>();
            var pr = _portrait.rectTransform;
            pr.anchorMin = new Vector2(97f / 1920f, 1f - 921f / 1080f);
            pr.anchorMax = new Vector2(597f / 1920f, 1f - 461f / 1080f);
            _portrait.texture = Resources.Load<Texture2D>("GameOver/core_tooth");
            _portrait.raycastTarget = false;
            _gauge = Full(scene, "Core durability gauge");
            GaugeArt("core_gauge", 128f, 158f, 727f, 152f);
            var health = Mathf.Clamp01(FortressMatch.Result.coreHealth01);
            var gaugeFill = GaugeArt("Hud_CoreFill", 265f, 216f, 566f * health, 34f);
            gaugeFill.uvRect = new Rect(0f, 0f, health, 1f);
            var logo = Full(scene, "CLEAR artwork");
            var ratio = logo.gameObject.AddComponent<AspectRatioFitter>();
            ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            ratio.aspectRatio = 16f / 9f;
            var texture = Resources.Load<Texture2D>("ClearIntro/clear");
            _clearLogo = Full(logo, "CLEAR image").gameObject.AddComponent<RawImage>();
            _clearLogo.texture = texture;
            _clearLogo.raycastTarget = false;
            var noiseShader = Resources.Load<Shader>("ClearIntro/ClearLogoNoise");
            if (noiseShader != null)
                _logoNoise = new Material(noiseShader) { name = "CLEAR noise dissolve", hideFlags = HideFlags.DontSave };
            for (var i = 0; i < _titleSlices.Length; i++)
            {
                var slice = Full(logo, $"Noise slice {i}").gameObject.AddComponent<RawImage>();
                slice.texture = texture;
                slice.enabled = false;
                if (_logoNoise != null) slice.material = _logoNoise;
                slice.raycastTarget = false;
                slice.rectTransform.anchorMin = new Vector2(0f, i / (float)_titleSlices.Length);
                slice.rectTransform.anchorMax = new Vector2(1f, (i + 1f) / _titleSlices.Length);
                slice.uvRect = new Rect(0f, i / (float)_titleSlices.Length, 1f, 1f / _titleSlices.Length);
                _titleSlices[i] = slice;
            }
            // 全面の不透明な下地を泡の奥に敷く。メッシュの隙間や四隅からの漏れを防ぐ。
            _cover = Full(_viewport, "Opaque foam backing").gameObject.AddComponent<Image>();
            _cover.color = Mint;
            _cover.raycastTarget = false;
            _cover.enabled = false;
            for (var i = 0; i < _bubbles.Length; i++)
            {
                var bubble = Full(_viewport, "Rising bubble").gameObject.AddComponent<ClearBubbleGraphic>();
                bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bubble.raycastTarget = false;
                bubble.enabled = false;
                _bubbles[i] = bubble;
                var pop = Full(_viewport, "Bubble film and droplets").gameObject.AddComponent<ClearBubbleBurstGraphic>();
                pop.rectTransform.anchorMin = pop.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pop.raycastTarget = false;
                pop.enabled = false;
                _bubblePopRings[i] = pop;
                var spray = Full(_viewport, "Bubble spray").gameObject.AddComponent<ClearBubbleGraphic>();
                spray.rectTransform.anchorMin = spray.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                spray.raycastTarget = false;
                spray.enabled = false;
                _bubbleSprays[i] = spray;
            }
            // 大きい泡を奥、小さい泡を手前へ。大きな円が手前を横切って隠す見え方を抑える。
            var bubbleOrder = new List<int>();
            var referenceSize = new Vector2(1920f, 1080f);
            for (var i = 0; i < _bubbles.Length; i++) bubbleOrder.Add(i);
            bubbleOrder.Sort((a, b) => ClearFoamMotion.Diameter(referenceSize, b, BubbleColumns, BubbleRows)
                .CompareTo(ClearFoamMotion.Diameter(referenceSize, a, BubbleColumns, BubbleRows)));
            foreach (var i in bubbleOrder) _bubbles[i].transform.SetAsLastSibling();
            for (var i = 0; i < _microFoam.Length; i++)
            {
                var bubble = Full(_viewport, "Fine foam").gameObject.AddComponent<ClearBubbleGraphic>();
                bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bubble.raycastTarget = false;
                bubble.enabled = false;
                _microFoam[i] = bubble;
            }
            for (var i = 0; i < _bubblePopRings.Length; i++)
            {
                _bubblePopRings[i].transform.SetAsLastSibling();
                _bubbleSprays[i].transform.SetAsLastSibling();
            }
            for (var i = 0; i < _edgeFoam.Length; i++)
            {
                var bubble = Full(_viewport, "CLEAR border foam").gameObject.AddComponent<ClearBubbleGraphic>();
                bubble.rectTransform.anchorMin = bubble.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                bubble.raycastTarget = false;
                bubble.enabled = false;
                _edgeFoam[i] = bubble;
            }
            var foamShader = Resources.Load<Shader>("ClearIntro/CloudFoamBubble");
            if (foamShader != null && foamShader.isSupported)
            {
                _foamMaterial = new Material(foamShader) { name = "Soft pearl foam", hideFlags = HideFlags.DontSave };
                foreach (var bubble in _viewport.GetComponentsInChildren<ClearBubbleGraphic>(true))
                    bubble.material = _foamMaterial;
            }
            for (var i = 0; i < _ripples.Length; i++)
            {
                var ripple = Full(_viewport, "Last enemy ripple").gameObject.AddComponent<GameOverRippleGraphic>();
                ripple.rectTransform.anchorMin = ripple.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ripple.raycastTarget = false;
                _ripples[i] = ripple;
            }
        }

        private void LateUpdate()
        {
            var elapsed = Time.unscaledTime - _started;
            if (elapsed >= ClearIntroTiming.Finished)
            {
                Complete();
                return;
            }
            ApplyCamera(elapsed);
            Render(elapsed);
        }

        private void ApplyCamera(float elapsed)
        {
            if (_restored || _camera == null) return;
            if (elapsed >= ClearIntroTiming.Covered)
            {
                RestoreCamera();
                var core = FindFirstObjectByType<CoreToothModel>();
                if (core != null)
                {
                    _portraitTexture = GameOverResultView.CaptureCore(core);
                    if (_portraitTexture != null) _portrait.texture = _portraitTexture;
                }
                return;
            }
            var t = ClearIntroTiming.Zoom(elapsed);
            if (_rig != null) _rig.ApplyViewImmediate(CameraViewSettings.Lerp(_before, _zoom, t));
            else
            {
                var target = _focus - _camera.transform.forward * Vector3.Dot(_focus - _beforePosition, _camera.transform.forward);
                _camera.transform.position = Vector3.Lerp(_beforePosition, target, t);
                _camera.orthographicSize = Mathf.Lerp(_beforeSize, Mathf.Min(3.2f, _beforeSize), t);
                _camera.fieldOfView = Mathf.Lerp(_beforeFov, Mathf.Min(25f, _beforeFov), t);
            }
        }

        private void Render(float elapsed)
        {
            var size = _viewport.rect.size;
            var slideRemaining = 1f - ClearIntroTiming.CoreSlide(elapsed);
            var viewportScale = new Vector2(size.x / 1920f, size.y / 1080f);
            _portrait.rectTransform.anchoredPosition =
                Vector2.Scale(new Vector2(612f, 110f), viewportScale) * slideRemaining;
            _gauge.anchoredPosition =
                Vector2.Scale(new Vector2(470f, 45f), viewportScale) * slideRemaining;
            _scene.alpha = elapsed >= ClearIntroTiming.OpenStart ? 1f : 0f;
            _background.uvRect = new Rect(Mathf.Repeat(elapsed * 660f / 1920f, 1f),
                Mathf.Repeat(elapsed * 240f / 1080f, 1f), 1f, 1f);
            var noise = ClearIntroTiming.Noise(elapsed);
            // 通常表示は標準UIの一枚絵。ノイズ開始時だけ分割描画へ切り替える。
            _clearLogo.enabled = noise <= 0f;
            if (_logoNoise != null)
            {
                _logoNoise.SetFloat("_Progress", noise);
                _logoNoise.SetFloat("_NoiseTime", elapsed);
            }
            // 埋まり切ってから破裂開始までは、画面全体を必ずアルファ1で覆う。
            // 破裂開始後にだけ下地を消し、破れた膜の隙間からCLEARを見せる。
            _cover.enabled = elapsed >= ClearIntroTiming.Covered && elapsed < ClearIntroTiming.OpenStart;
            _cover.color = new Color(Mint.r, Mint.g, Mint.b, 1f);
            for (var i = 0; i < _bubbles.Length; i++)
            {
                var bubble = _bubbles[i];
                var filled = ClearFoamMotion.FilledPosition(size, i, BubbleColumns, BubbleRows, elapsed);
                var normalized = new Vector2(filled.x / size.x, filled.y / size.y);
                var rise = ClearFoamMotion.RisingProgress(elapsed, size, i, BubbleColumns, BubbleRows);
                var pop = ClearFoamMotion.Pop(elapsed, normalized * 1.4f);
                bubble.enabled = rise > 0f && pop < 0.95f;
                bubble.SetFilm(pop, ClearFoamMotion.Variation(i, 41));
                var position = ClearFoamMotion.RisingPosition(elapsed, size, i, BubbleColumns, BubbleRows);
                bubble.rectTransform.anchoredPosition = position;
                var diameter = ClearFoamMotion.Diameter(size, i, BubbleColumns, BubbleRows);
                // 膜が張ってから破れる。円を縮めて消すのではなく飛沫へ引き継ぐ。
                var burstScale = 1f + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pop / 0.28f)) * 0.09f;
                // 見えてから大きく膨らみ直す動きを抑え、泡の塊として自然に湧き上がる。
                var emergenceScale = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rise / 0.6f)));
                bubble.rectTransform.sizeDelta = Vector2.one * diameter * emergenceScale * burstScale;
                var stretch = Mathf.Sin(rise * Mathf.PI);
                var bounce = Mathf.Sin(rise * Mathf.PI * 2f) * stretch;
                var pressure = Mathf.Sin(rise * Mathf.PI * 3f + ClearFoamMotion.Variation(i, 25) * 6.28f) * stretch * 0.018f;
                bubble.rectTransform.localScale = new Vector3(1f - stretch * 0.055f + bounce * 0.02f + pressure,
                    1f + stretch * 0.075f - bounce * 0.02f - pressure, 1f);
                bubble.rectTransform.localRotation = Quaternion.Euler(0f, 0f,
                    stretch * (ClearFoamMotion.Variation(i, 15) - 0.5f) * 12f);
                bubble.color = FoamColor(i,
                    1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.70f, 0.95f, pop)));

                var ring = _bubblePopRings[i];
                ring.enabled = pop > 0.18f && pop < 1f;
                ring.rectTransform.anchoredPosition = position;
                ring.rectTransform.sizeDelta = Vector2.one * diameter * 2.2f;
                ring.SetBurst(Mathf.InverseLerp(0.18f, 1f, pop), i);
                ring.color = new Color(0.92f, 0.98f, 1f, 0.85f);
                var spray = _bubbleSprays[i];
                spray.enabled = pop > 0.28f && pop < 1f && i % 3 == 0;
                var direction = new Vector2(Mathf.Cos(i * 2.39996f), Mathf.Sin(i * 2.39996f));
                spray.rectTransform.anchoredPosition = position + direction * pop * diameter * 0.65f;
                spray.rectTransform.sizeDelta = Vector2.one * diameter * 0.12f * (1f - pop);
                spray.color = FoamColor(i, 1f - pop);
            }
            // 大泡だけの集合にせず、小さな泡をかたまりの間へ添えて細かな泡立ちを作る。
            for (var i = 0; i < _microFoam.Length; i++)
            {
                var bubble = _microFoam[i];
                var parent = (i * 37 + 11) % BubbleCount;
                var rise = ClearFoamMotion.RisingProgress(elapsed, size, parent, BubbleColumns, BubbleRows);
                var target = ClearFoamMotion.FilledPosition(size, parent, BubbleColumns, BubbleRows, elapsed);
                var pop = ClearFoamMotion.Pop(elapsed, new Vector2(target.x / size.x, target.y / size.y) * 1.4f);
                bubble.enabled = rise > 0f && pop < 1f;
                var position = ClearFoamMotion.RisingPosition(elapsed, size, parent, BubbleColumns, BubbleRows);
                var diameter = ClearFoamMotion.Diameter(size, parent, BubbleColumns, BubbleRows);
                var angle = ClearFoamMotion.Variation(i, 17) * Mathf.PI * 2f;
                position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * diameter * 0.37f * rise;
                bubble.rectTransform.anchoredPosition = position;
                bubble.rectTransform.sizeDelta = Vector2.one * diameter *
                    Mathf.Lerp(0.09f, 0.24f, ClearFoamMotion.Variation(i, 18)) *
                    Mathf.Lerp(0.65f, 1f, rise) * (1f - pop);
                bubble.color = FoamColor(i + BubbleCount, 1f - pop);
            }
            // CLEARの周囲だけは泡を残す。中央の結果とロゴにかからない位置で漂わせる。
            for (var i = 0; i < _edgeFoam.Length; i++)
            {
                var bubble = _edgeFoam[i];
                var appear = ClearIntroTiming.Open(elapsed);
                bubble.enabled = appear > 0f && noise < 1f;
                Vector2 edge;
                var scatter = (ClearFoamMotion.Variation(i, 8) - 0.5f) * 0.08f;
                if (i < 10) edge = new Vector2((i / 9f - 0.5f + scatter) * size.x, -size.y * 0.54f);
                else if (i < 20) edge = new Vector2(((i - 10) / 9f - 0.5f + scatter) * size.x, size.y * 0.54f);
                else edge = new Vector2((i < 24 ? -1f : 1f) * size.x * 0.54f, ((i % 4) / 3f - 0.5f + scatter) * size.y);
                edge.y += Mathf.Sin(elapsed * 1.2f + i) * 6f;
                bubble.rectTransform.anchoredPosition = edge;
                bubble.rectTransform.sizeDelta = Vector2.one * size.y * Mathf.Lerp(0.12f, 0.32f, ClearFoamMotion.Variation(i, 9));
                bubble.color = FoamColor(i + BubbleCount + _microFoam.Length, appear * (1f - noise));
            }
            for (var i = 0; i < _titleSlices.Length; i++)
            {
                var slice = _titleSlices[i];
                slice.enabled = noise > 0f;
                var hash = Mathf.Repeat(Mathf.Sin(i * 43f + Mathf.Floor(elapsed * 36f) * 13.1f) * 43758.5f, 1f);
                slice.color = _logoNoise != null ? Color.white :
                    new Color(1f, 1f, 1f, noise == 0f ? 1f : (hash > noise ? 1f - noise : 0f));
                slice.rectTransform.anchoredPosition = new Vector2(noise * (hash - 0.5f) * size.x * 0.3f, 0f);
            }
            _portrait.color = Color.white;
            for (var i = 0; i < _ripples.Length; i++)
            {
                var ripple = _ripples[i];
                var age = Time.time - _effectStart - i * 0.07f;
                ripple.enabled = elapsed < ClearIntroTiming.FillStart && age >= 0f && age < 1.1f && _camera != null;
                if (!ripple.enabled) continue;
                var phase = age / 1.1f;
                var screen = _camera.WorldToScreenPoint(_focus);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, null, out var local);
                ripple.rectTransform.anchoredPosition = local;
                ripple.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(25f, size.y * 0.8f, phase);
                ripple.color = new Color(1f, 1f, 1f, 0.8f * (1f - phase));
            }
        }

        private static Color FoamColor(int index, float alpha)
        {
            Color tint;
            switch (Mathf.FloorToInt(ClearFoamMotion.Variation(index, 23) * 5f))
            {
                case 0: tint = new Color(0.48f, 0.83f, 1f); break;
                case 1: tint = new Color(1f, 0.59f, 0.78f); break;
                case 2: tint = new Color(1f, 0.87f, 0.43f); break;
                case 3: tint = new Color(0.47f, 0.94f, 0.73f); break;
                default: tint = new Color(0.76f, 0.62f, 1f); break;
            }
            tint.a = alpha;
            return tint;
        }

        private void RestoreCamera()
        {
            if (_restored) return;
            _restored = true;
            // 終了中はリグの更新APIを呼ばない。保存済みの実体だけを戻す。
            if (_rig != null && _camera != null) _rig.transform.SetPositionAndRotation(_rigPosition, _rigRotation);
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_beforePosition, _cameraRotation);
                _camera.orthographicSize = _beforeSize;
                _camera.fieldOfView = _beforeFov;
            }
            CoreBreakSlowMotion.EndDecisionSequence();
        }

        private RawImage GaugeArt(string name, float x, float y, float width, float height)
        {
            var art = Full(_gauge, name).gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("GameOver/" + name);
            art.raycastTarget = false;
            art.rectTransform.anchorMin = new Vector2(x / 1920f, 1f - (y + height) / 1080f);
            art.rectTransform.anchorMax = new Vector2((x + width) / 1920f, 1f - y / 1080f);
            return art;
        }

        public RenderTexture ReleaseCorePortrait()
        {
            var portrait = _portraitTexture;
            _portraitTexture = null;
            return portrait;
        }

        private void Complete()
        {
            if (_done) return;
            _done = true;
            RestoreCamera();
            var callback = _onFinished;
            _onFinished = null;
            gameObject.SetActive(false);
            callback?.Invoke();
            Destroy(gameObject);
        }

        private void OnDisable() => RestoreCamera();
        public void Cancel()
        {
            _done = true;
            _onFinished = null;
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
            RestoreCamera();
            enabled = false;
        }
        private void OnDestroy()
        {
            if (_logoNoise != null) Destroy(_logoNoise);
            if (_foamMaterial != null) Destroy(_foamMaterial);
            if (_portraitTexture == null) return;
            _portraitTexture.Release();
            Destroy(_portraitTexture);
        }
    }

    /// <summary>破れた膜の弧と、慣性で広がって落ちる微細な水滴。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ClearBubbleBurstGraphic : MaskableGraphic
    {
        private float _progress;
        private int _seed;

        public void SetBurst(float progress, int seed)
        {
            if (_progress == progress && _seed == seed) return;
            _progress = progress;
            _seed = seed;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) / 4.4f;
            if (radius <= 0.1f || _progress >= 1f) return;
            var t = _progress;
            var phase = ClearFoamMotion.Variation(_seed, 31) * Mathf.PI * 2f;
            var travel = 1f - Mathf.Pow(1f - t, 3f);
            var filmAlpha = (1f - Mathf.SmoothStep(0f, 1f, t / 0.65f)) *
                            Mathf.SmoothStep(0f, 1f, t / 0.09f);
            var filmRadius = radius * (1.05f + travel * 0.3f);
            var width = Mathf.Max(0.85f, radius * 0.018f) * (1f - t * 0.6f);
            const int segments = 64;
            for (var s = 0; s < segments; s++)
            {
                var angle = s * Mathf.PI * 2f / segments + phase;
                var next = angle + Mathf.PI * 2f / segments;
                var breakup = Mathf.Clamp01(0.5f + Mathf.Sin(angle * 5f + phase) * 0.35f + Mathf.Sin(angle * 9f - phase) * 0.15f);
                var opacity = filmAlpha * Mathf.SmoothStep(0f, 0.18f, breakup - t * 0.8f);
                if (opacity <= 0.001f) continue;
                var a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var b = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
                var start = vh.currentVertCount;
                // Feather both sides of the membrane instead of a hard white ring.
                for (var band = 0; band < 3; band++)
                {
                    var r = filmRadius + (band - 1) * width;
                    var tint = color;
                    tint.a *= band == 1 ? opacity : 0f;
                    vh.AddVert(rect.center + a * r, tint, Vector2.zero);
                    vh.AddVert(rect.center + b * r, tint, Vector2.zero);
                }
                for (var band = 0; band < 2; band++)
                {
                    var k = start + band * 2;
                    vh.AddTriangle(k, k + 1, k + 3);
                    vh.AddTriangle(k, k + 3, k + 2);
                }
            }
            for (var d = 0; d < 7; d++)
            {
                var random = ClearFoamMotion.Variation(_seed * 7 + d, 32);
                var angle = phase + d * 2.399963f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var center = rect.center + direction * radius * (0.94f + travel * Mathf.Lerp(0.28f, 0.88f, random));
                center.y -= radius * 0.28f * t * t;
                var droplet = Mathf.Lerp(0.014f, 0.043f, random) * radius * (1f - t * 0.65f);
                var fadeEnd = Mathf.Lerp(0.7f, 1f, random);
                var fade = Mathf.SmoothStep(0f, 0.10f, t) * (1f - Mathf.SmoothStep(0.4f, fadeEnd, t));
                var tint = color;
                tint.a *= fade;
                var start = vh.currentVertCount;
                vh.AddVert(center, tint, Vector2.zero);
                for (var band = 0; band < 2; band++)
                for (var v = 0; v <= 12; v++)
                {
                    var a = v * Mathf.PI * 2f / 12;
                    var offset = direction * Mathf.Cos(a) * droplet * (1.4f - t * 0.4f) +
                                 new Vector2(-direction.y, direction.x) * Mathf.Sin(a) * droplet;
                    tint = color;
                    tint.a *= band == 0 ? fade : 0f;
                    vh.AddVert(center + offset * (band == 0 ? 0.55f : 1f), tint, Vector2.zero);
                    if (v == 0) continue;
                    if (band == 0) vh.AddTriangle(start, start + v, start + v + 1);
                    else
                    {
                        var inner = start + v;
                        var outer = inner + 13;
                        vh.AddTriangle(inner, outer, outer + 1);
                        vh.AddTriangle(inner, outer + 1, inner + 1);
                    }
                }
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ClearBubbleGraphic : MaskableGraphic
    {
        private Vector2 _film;

        public void SetFilm(float progress, float seed)
        {
            var next = new Vector2(progress, seed);
            if (_film == next) return;
            _film = next;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0.1f) return;
            if (material != null && (material.shader.name == "MS2026/UI/FoamBubble" ||
                                     material.shader.name == "MS2026/UI/CloudFoamBubble"))
            {
                AddFilmVertex(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(0, 0));
                AddFilmVertex(vh, new Vector2(rect.xMin, rect.yMax), new Vector2(0, 1));
                AddFilmVertex(vh, new Vector2(rect.xMax, rect.yMax), new Vector2(1, 1));
                AddFilmVertex(vh, new Vector2(rect.xMax, rect.yMin), new Vector2(1, 0));
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
                return;
            }
            Body(vh, rect.center, radius);
            // 点を貼ったような丸い白ではなく、縁の柔らかい斜めの反射を描く。
            Reflection(vh, rect.center + new Vector2(-radius * 0.34f, radius * 0.38f),
                new Vector2(radius * 0.24f, radius * 0.125f), 0.88f);
            Reflection(vh, rect.center + new Vector2(radius * 0.38f, -radius * 0.30f),
                Vector2.one * radius * 0.047f, 0.45f);
            Reflection(vh, rect.center + new Vector2(radius * 0.16f, radius * 0.72f),
                new Vector2(radius * 0.25f, radius * 0.025f), 0.26f);
        }

        private void AddFilmVertex(VertexHelper vh, Vector2 position, Vector2 uv)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = uv;
            vertex.uv1 = _film;
            vh.AddVert(vertex);
        }

        private void Body(VertexHelper vh, Vector2 center, float radius)
        {
            const int segments = 64;
            var start = vh.currentVertCount;
            var rim = new Color(0.47f, 0.78f, 0.71f, color.a);
            var centerColor = Color.Lerp(color, Color.white, 0.12f);
            centerColor.a = color.a;
            vh.AddVert(center, centerColor, Vector2.zero);
            for (var band = 0; band < 4; band++)
            {
                var r = band == 0 ? radius * 0.80f :
                    band == 1 ? Mathf.Max(radius * 0.80f, radius - 2.4f) :
                    band == 2 ? Mathf.Max(radius * 0.80f, radius - 0.7f) : radius + 0.7f;
                var ring = vh.currentVertCount;
                for (var i = 0; i <= segments; i++)
                {
                    var a = i * Mathf.PI * 2f / segments;
                    var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var tint = band >= 2 ? rim : Color.Lerp(color, Color.white, Mathf.Clamp01(direction.y) * 0.16f);
                    if (band == 0)
                        tint = Color.Lerp(tint, new Color(0.80f, 0.94f, 0.90f, color.a),
                            Mathf.Clamp01(-direction.y) * 0.22f);
                    if (band == 1) tint = Color.Lerp(tint, rim, 0.18f);
                    tint.a = band == 3 ? 0f : color.a;
                    vh.AddVert(center + direction * r, tint, Vector2.zero);
                    if (i == 0) continue;
                    if (band == 0) vh.AddTriangle(start, ring + i - 1, ring + i);
                    else
                    {
                        var previous = ring - segments - 1;
                        vh.AddTriangle(previous + i - 1, ring + i - 1, ring + i);
                        vh.AddTriangle(previous + i - 1, ring + i, previous + i);
                    }
                }
            }
        }

        private void Reflection(VertexHelper vh, Vector2 center, Vector2 radius, float opacity)
        {
            const int segments = 24;
            var start = vh.currentVertCount;
            vh.AddVert(center, new Color(1f, 1f, 1f, color.a * opacity), Vector2.zero);
            for (var band = 0; band < 2; band++)
            {
                var ring = vh.currentVertCount;
                var scale = band == 0 ? 0.65f : 1f;
                var tint = new Color(1f, 1f, 1f, band == 0 ? color.a * opacity * 0.8f : 0f);
                for (var i = 0; i <= segments; i++)
                {
                    var a = i * Mathf.PI * 2f / segments;
                    var point = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y) * scale;
                    point = new Vector2(point.x * 0.866f - point.y * 0.5f, point.x * 0.5f + point.y * 0.866f);
                    vh.AddVert(center + point, tint, Vector2.zero);
                    if (i == 0) continue;
                    if (band == 0) vh.AddTriangle(start, ring + i - 1, ring + i);
                    else
                    {
                        var previous = ring - segments - 1;
                        vh.AddTriangle(previous + i - 1, ring + i - 1, ring + i);
                        vh.AddTriangle(previous + i - 1, ring + i, previous + i);
                    }
                }
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ClearFoamCover : MaskableGraphic
    {
        public float Opening;
        public float Fill;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            // 泡が湧く間は下から覆い、ひと呼吸の間は不透明、最後は中央の穴を広げる。
            var top = Mathf.Lerp(rect.yMin - 10f, rect.yMax, Fill);
            if (top <= rect.yMin) return;
            var center = rect.center;
            var radius = new Vector2(rect.width, rect.height).magnitude * 0.52f * Opening;
            const int segments = 128;
            for (var i = 0; i < segments; i++)
            {
                Vector2 Outer(float a)
                {
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var t = Mathf.Min(rect.width * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(d.x)),
                        rect.height * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(d.y)));
                    return center + d * t;
                }
                Vector2 Inner(float a, Vector2 outer)
                {
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    return center + d * Mathf.Min(radius, Vector2.Distance(center, outer));
                }
                var a0 = i * Mathf.PI * 2f / segments;
                var a1 = (i + 1) * Mathf.PI * 2f / segments;
                var o0 = Outer(a0);
                var o1 = Outer(a1);
                var n0 = Inner(a0, o0);
                var n1 = Inner(a1, o1);
                o0.y = Mathf.Min(o0.y, top); o1.y = Mathf.Min(o1.y, top);
                n0.y = Mathf.Min(n0.y, top); n1.y = Mathf.Min(n1.y, top);
                var start = vh.currentVertCount;
                vh.AddVert(n0, color, Vector2.zero); vh.AddVert(o0, color, Vector2.zero);
                vh.AddVert(o1, color, Vector2.zero); vh.AddVert(n1, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
