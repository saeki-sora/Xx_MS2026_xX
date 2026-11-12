using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Stages;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CyberCleanLook
{
    /// <summary>
    /// WAVE の区切りの画面演出（見た目だけ）。<see cref="FortressWaves"/> のイベントを見て、各PCが自分で鳴らす。
    /// ・WAVE の発表: 走査線が画面の上から下へ走り、通った跡に方眼が出る（HUD の下）
    /// ・WAVE クリア: すぐにコアの位置で「キラーン」と光る（HUD の上）
    /// ・歯みがきタイムへ: 泡が HUD ごと画面を覆い、その下で切り替わってから、はじけて歯みがきタイムの画面が出る（<see cref="CyberCleanFoam"/>）
    /// 押しても何も起きない（ボタンを邪魔しない）。
    /// </summary>
    public sealed class CyberCleanScreenFx : MonoBehaviour
    {
        private const float Width = 1920f;
        private const float Height = 1080f;
        private const int RayCount = 12;
        private const int SparkleCount = 12;

        private static CyberCleanScreenFx _current;
        private static readonly Dictionary<(Texture2D, Vector2), Sprite> Sprites = new Dictionary<(Texture2D, Vector2), Sprite>();

        /// <summary>キラーンを動かす速さ（1=そのまま。確認用の画面がスロー再生に使う。ゲームの中では変えない）。</summary>
        public static float KiraPreviewSpeed { get; set; } = 1f;

        /// <summary>画面演出の係がいるか（確認用の画面の表示用）。</summary>
        public static bool IsReady => _current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPreview()
        {
            KiraPreviewSpeed = 1f;
        }

        private CyberCleanSettings _settings;
        private RectTransform _scanRoot;
        private RectTransform _kiraRoot;
        private RectTransform _kiraGroup;
        private RawImage _scanLine, _scanGrid;
        private Image _kiraGlow, _kiraStar, _kiraRing;
        private readonly List<Image> _rays = new List<Image>();
        private readonly List<Image> _sparkles = new List<Image>();
        private CyberCleanFoam _foam;
        private float _scanAge = -1f, _kiraAge = -1f;
        private Vector2 _kiraCenter;
        private CoreCrystalController _core;
        private readonly System.Random _random = new System.Random();

        /// <summary>試す用（メニューから）。0=走査線、1=キラーン、2=泡。</summary>
        public static void Test(int kind)
        {
            if (_current == null)
            {
                Debug.LogWarning("[CyberCleanLook] 画面演出の係がいません。設定の stageScenes に入っているステージで Play してください。");
                return;
            }

            if (kind == 0) _current.PlayScan();
            else if (kind == 1) _current.PlayKira();
            else _current._foam.Test();
        }

        internal void Init(CyberCleanSettings settings)
        {
            _settings = settings;
            _current = this;
            Build();
        }

        private void OnEnable()
        {
            FortressWaves.WaveAnnounced += HandleWaveAnnounced;
            FortressWaves.WaveCleared += HandleWaveCleared;
            FortressWaves.BreakStarted += HandleBreakStarted;
        }

        private void OnDisable()
        {
            FortressWaves.WaveAnnounced -= HandleWaveAnnounced;
            FortressWaves.WaveCleared -= HandleWaveCleared;
            FortressWaves.BreakStarted -= HandleBreakStarted;
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        private void HandleWaveAnnounced(int index)
        {
            if (_settings.scanOnWaveStart && !StageDropIntro.IsHolding)
            {
                PlayScan();
            }
        }

        private void HandleWaveCleared(int index)
        {
            if (_settings.kiraOnWaveClear)
            {
                PlayKira();
            }

            // 最後の WAVE の後は歯みがきタイムが無いので、泡の切り替えは出さない。
            var isLast = FortressWaves.WaveCount > 0 && index >= FortressWaves.WaveCount - 1;
            if (_settings.foamOnBreak && !isLast)
            {
                _foam.OnWaveCleared();
            }
        }

        private void HandleBreakStarted(int index)
        {
            if (_settings.foamOnBreak)
            {
                _foam.OnBreakStarted();
            }
        }

        private void PlayScan()
        {
            _scanAge = 0f;
            FortressEffectPlayer.PlaySound(_settings.onScan, Vector3.zero);
        }

        private void PlayKira()
        {
            _kiraAge = 0f;
            _kiraCenter = CoreOnScreen();
            for (var i = 0; i < _sparkles.Count; i++)
            {
                var angle = (float)_random.NextDouble() * Mathf.PI * 2f;
                var distance = (110f + (float)_random.NextDouble() * 260f) * _settings.kiraScale;
                _sparkles[i].rectTransform.anchoredPosition = _kiraCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }

            FortressEffectPlayer.PlaySound(_settings.onKira, Vector3.zero);
        }

        private void Update()
        {
            // 試合のスローモーション中も同じ速さで動かす。
            var dt = Time.unscaledDeltaTime;
            StepScan(dt);
            StepKira(dt);
            _foam.Update(dt);
        }

        private void StepScan(float dt)
        {
            var active = _scanAge >= 0f;
            _scanLine.enabled = active;
            _scanGrid.enabled = active;
            if (!active)
            {
                return;
            }

            _scanAge += dt;
            var t = _scanAge / _settings.scanSeconds;
            if (t >= 1.25f)
            {
                _scanAge = -1f;
                return;
            }

            var y = Mathf.Lerp(Height * 0.5f + 60f, -Height * 0.5f - 60f, Mathf.Min(1f, t));
            var fade = t <= 1f ? 1f : 1f - (t - 1f) / 0.25f;
            _scanLine.rectTransform.anchoredPosition = new Vector2(0f, y);
            _scanLine.color = WithAlpha(_settings.scanColor, _settings.scanColor.a * fade);
            _scanGrid.rectTransform.anchoredPosition = new Vector2(0f, y);
            _scanGrid.color = WithAlpha(_settings.scanGridColor, _settings.scanGridColor.a * fade);
        }

        private void StepKira(float dt)
        {
            var active = _kiraAge >= 0f;
            if (_kiraGroup.gameObject.activeSelf != active)
            {
                _kiraGroup.gameObject.SetActive(active);
            }

            if (!active)
            {
                return;
            }

            _kiraAge += dt * Mathf.Max(0.01f, KiraPreviewSpeed);
            var t = _kiraAge / _settings.kiraSeconds;
            if (t >= 1f)
            {
                _kiraAge = -1f;
                _kiraGroup.gameObject.SetActive(false);
                return;
            }

            var power = _settings.kiraStrength;
            var main = _settings.kiraColor;
            var accent = _settings.kiraAccentColor;
            // 最初の少しの間ははっきり見せて、後半で消す。
            var hold = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
            var scale = _settings.kiraScale;

            _kiraGlow.rectTransform.anchoredPosition = _kiraCenter;
            _kiraGlow.rectTransform.sizeDelta = Vector2.one * (Mathf.Lerp(320f, 900f, CyberCleanMath.EaseOut(t)) * scale);
            _kiraGlow.color = WithAlpha(main, 0.6f * power * hold);

            _kiraRing.rectTransform.anchoredPosition = _kiraCenter;
            _kiraRing.rectTransform.sizeDelta = Vector2.one * (Mathf.Lerp(180f, 1500f, CyberCleanMath.EaseOut(t)) * scale);
            _kiraRing.color = WithAlpha(accent, power * (1f - t));

            var starT = Mathf.Clamp01(t / 0.3f);
            _kiraStar.rectTransform.anchoredPosition = _kiraCenter;
            _kiraStar.rectTransform.sizeDelta = Vector2.one * (420f * scale * CyberCleanMath.EaseOutBack(starT) * (1f - 0.35f * t));
            _kiraStar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f * t);
            _kiraStar.color = WithAlpha(Color.white, power * hold);

            for (var i = 0; i < _rays.Count; i++)
            {
                var ray = _rays[i].rectTransform;
                ray.anchoredPosition = _kiraCenter;
                ray.localRotation = Quaternion.Euler(0f, 0f, i * 360f / _rays.Count + 7f + t * 18f);
                ray.sizeDelta = new Vector2(Mathf.Lerp(46f, 14f, t) * Mathf.Sqrt(scale), 1200f * scale * CyberCleanMath.EaseOut(t * 1.6f));
                _rays[i].color = WithAlpha(i % 2 == 0 ? main : accent, 0.9f * power * hold);
            }

            for (var i = 0; i < _sparkles.Count; i++)
            {
                var delay = 0.1f + i * 0.04f;
                var local = Mathf.Clamp01((t - delay) / 0.35f);
                _sparkles[i].rectTransform.sizeDelta = Vector2.one * (90f * scale * CyberCleanMath.Bump(local));
                _sparkles[i].color = WithAlpha(i % 2 == 0 ? Color.white : accent, power * CyberCleanMath.Bump(local));
            }
        }

        // コアが画面のどこに映っているか（画面の真ん中が 0,0 の、1920×1080 の座標）。見つからなければ真ん中。
        private Vector2 CoreOnScreen()
        {
            if (_core == null)
            {
                _core = FindFirstObjectByType<CoreCrystalController>();
            }

            var rig = FortressCameraRig.Active;
            var camera = rig != null ? rig.targetCamera : null;
            if (_core == null || camera == null)
            {
                return Vector2.zero;
            }

            var screen = camera.WorldToScreenPoint(_core.transform.position);
            if (screen.z <= 0f)
            {
                return Vector2.zero;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_kiraGroup, screen, null, out var local) ? local : Vector2.zero;
        }

        private void Build()
        {
            _scanRoot = NewCanvas("CyberClean Scan", _settings.canvasSortingOrder);
            _kiraRoot = NewCanvas("CyberClean Kira", _settings.kiraSortingOrder);
            var foamRoot = NewCanvas("CyberClean Foam", _settings.foamSortingOrder);

            _scanGrid = NewChild<RawImage>(_scanRoot, "ScanGrid", new Vector2(0.5f, 0f));
            _scanGrid.texture = CyberCleanArt.Grid;
            _scanGrid.rectTransform.sizeDelta = new Vector2(Width + 40f, 260f);
            _scanGrid.uvRect = new Rect(0f, 0f, (Width + 40f) / 256f, 260f / 256f);
            _scanLine = NewChild<RawImage>(_scanRoot, "ScanLine", new Vector2(0.5f, 0.5f));
            _scanLine.texture = CyberCleanArt.Line;
            _scanLine.rectTransform.sizeDelta = new Vector2(Width + 40f, 70f);

            // キラーンの部品は入れ物にまとめ、入れ物だけを出し入れする（画面そのものは常に出しておき、コアの位置を正しく計算できるようにする）。
            _kiraGroup = NewChild<Image>(_kiraRoot, "KiraGroup", new Vector2(0.5f, 0.5f)).rectTransform;
            _kiraGroup.GetComponent<Image>().enabled = false;
            _kiraGroup.anchorMin = Vector2.zero;
            _kiraGroup.anchorMax = Vector2.one;
            _kiraGroup.offsetMin = _kiraGroup.offsetMax = Vector2.zero;
            _kiraGlow = Picture(_kiraGroup, "KiraGlow", CyberCleanArt.Soft, new Vector2(0.5f, 0.5f));
            _kiraRing = Picture(_kiraGroup, "KiraRing", CyberCleanArt.Ring, new Vector2(0.5f, 0.5f));
            for (var i = 0; i < RayCount; i++)
            {
                _rays.Add(Picture(_kiraGroup, "KiraRay", CyberCleanArt.Ray, new Vector2(0.5f, 0f)));
            }

            _kiraStar = Picture(_kiraGroup, "KiraStar", CyberCleanArt.Star, new Vector2(0.5f, 0.5f));
            for (var i = 0; i < SparkleCount; i++)
            {
                _sparkles.Add(Picture(_kiraGroup, "KiraSparkle", CyberCleanArt.Star, new Vector2(0.5f, 0.5f)));
            }

            _foam = new CyberCleanFoam(_settings, foamRoot);
            _scanLine.enabled = _scanGrid.enabled = false;
            _kiraGroup.gameObject.SetActive(false);
        }

        private RectTransform NewCanvas(string name, int order)
        {
            var canvasObject = new GameObject(name);
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas.GetComponent<RectTransform>();
        }

        internal static Image Picture(RectTransform parent, string name, Texture2D texture, Vector2 pivot)
        {
            var image = NewChild<Image>(parent, name, pivot);
            // 同じ絵の Sprite は使い回す（試合のたびに増えないように）。
            if (!Sprites.TryGetValue((texture, pivot), out var sprite) || sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 100f);
                sprite.hideFlags = HideFlags.DontSave;
                Sprites[(texture, pivot)] = sprite;
            }

            image.sprite = sprite;
            return image;
        }

        private static T NewChild<T>(RectTransform parent, string name, Vector2 pivot) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            var graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
