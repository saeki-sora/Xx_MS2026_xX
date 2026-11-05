using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの画面の演出（起動時に席替えの係と一緒に自動で作られる。2026-11-02 ユーザー決定の見本帳の番号）。
    /// 「席替え！」の帯（4）・画面の端を暗く（5）・カメラが回っている間の勢いの線（12）・着地した人の頭の上の OK マーク（23）。
    /// HUD より手前の画面いっぱいの枠に描く（画面の端の暗さは HUD にも少しかかる）。各PCが自分で描く（通信なし）。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(130)]
    internal sealed class FortressSeatOverlay : MonoBehaviour
    {
        private const int SortingOrder = 90;
        private const int StreakCount = 28;
        private const float BannerHeight = 150f;
        private const float OkPopSeconds = 0.25f;
        private const float OkDelay = 0.15f;
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color BandColor = new Color(0.04f, 0.055f, 0.086f, 0.86f);
        private static readonly Color AccentColor = new Color(0.5f, 0.89f, 1f, 1f);
        private static readonly WaveSeatRotation DefaultSettings = new WaveSeatRotation();

        private sealed class OkMark
        {
            public RectTransform Root;
            public Image Disc;
            public LaserTurret Turret;
            public int Player;
            public bool Local;
            public float PopAt;
            public bool Played;
        }

        private readonly List<OkMark> _marks = new List<OkMark>();
        private readonly List<RectTransform> _streaks = new List<RectTransform>();
        private readonly List<Image> _streakImages = new List<Image>();
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Image _vignette;
        private RectTransform _banner;
        private Text _title;
        private Text _subtitle;
        private float _vignetteAlpha;
        private float _okHideAt = -1f;
        private bool _moving;

        private static WaveSeatRotation Settings => FortressSeats.Settings ?? DefaultSettings;

        private void OnEnable()
        {
            FortressSeats.MoveStarted += OnMoveStarted;
            FortressSeats.MoveFinished += OnMoveFinished;
            FortressSeats.TurretLanded += OnTurretLanded;
            FortressWaves.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            FortressSeats.MoveStarted -= OnMoveStarted;
            FortressSeats.MoveFinished -= OnMoveFinished;
            FortressSeats.TurretLanded -= OnTurretLanded;
            FortressWaves.PhaseChanged -= OnPhaseChanged;
            HideAll();
        }

        // 席替えの段階が終わったら（テストで飛ばしたときも）、残っている OK マークなどを片付ける。跳んでいる最中は片付けない。
        private void OnPhaseChanged(WavePhase previous, WavePhase next)
        {
            if (next != WavePhase.SeatChange && !FortressSeats.IsMoving)
            {
                HideAll();
            }
        }

        private void OnMoveStarted(int shift)
        {
            EnsureCanvas();
            HideMarks();
            _moving = true;
            _okHideAt = -1f;
            var settings = Settings;
            _title.text = string.IsNullOrEmpty(settings.bannerText) ? "席替え！" : settings.bannerText;
            _subtitle.text = !string.IsNullOrEmpty(settings.bannerSubText) ? settings.bannerSubText : FortressSeats.Clockwise ? "時計回りに1つ移動" : "反時計回りに1つ移動";
            var font = settings.bannerFont != null ? settings.bannerFont : DefaultFont;
            _title.font = _subtitle.font = font;
        }

        private void OnMoveFinished(int shift)
        {
            _moving = false;
            if (_banner != null)
            {
                _banner.gameObject.SetActive(false);
            }

            if (!FortressSeats.LastMoveCompleted)
            {
                HideMarks();
                return;
            }

            _okHideAt = Time.unscaledTime + Settings.youMarkerSeconds;
        }

        private void OnTurretLanded(SeatJumper jumper)
        {
            if (!Settings.okMarks || _canvas == null)
            {
                return;
            }

            var mark = new OkMark
            {
                Turret = jumper.Turret,
                Player = jumper.Player,
                Local = jumper.Player == FortressSeatMover.LocalPlayer(),
                PopAt = Time.unscaledTime + OkDelay
            };
            BuildMark(mark);
            _marks.Add(mark);
        }

        private void LateUpdate()
        {
            if (_canvas == null)
            {
                return;
            }

            var settings = Settings;
            var time = FortressSeats.MoveTime;
            UpdateVignette(settings, time);
            UpdateBanner(settings, time);
            UpdateStreaks(settings, time);
            UpdateMarks(settings);
        }

        // 画面の端を暗く（溜めから全員の着地まで。着地の後は 0.5 秒で戻る）。
        private void UpdateVignette(WaveSeatRotation settings, float time)
        {
            var lastLanding = settings.AnticipationTime + settings.StaggerTime * (FortressSeats.SeatCount - 1) + settings.FlightSeconds;
            var target = settings.darkenEdges && _moving ? Mathf.Clamp01(time / 0.3f) * (1f - Mathf.Clamp01((time - lastLanding) / 0.5f)) : 0f;
            _vignetteAlpha = _moving ? target : Mathf.MoveTowards(_vignetteAlpha, 0f, Time.unscaledDeltaTime * 2f);
            _vignette.enabled = _vignetteAlpha > 0.01f;
            _vignette.color = new Color(0f, 0f, 0f, settings.darkenAmount * _vignetteAlpha);
        }

        // 「席替え！」の帯: 左から滑り込み、溜めの終わり少し後まで止まって、右へ抜ける。
        private void UpdateBanner(WaveSeatRotation settings, float time)
        {
            var end = Mathf.Max(1f, settings.AnticipationTime + 0.55f);
            var show = settings.showBanner && _moving && time < end;
            _banner.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var width = _canvasRect.rect.width;
            var slideIn = 1f - Mathf.Pow(1f - Mathf.Clamp01(time / 0.22f), 3f);
            var slideOut = Mathf.Clamp01((time - (end - 0.25f)) / 0.25f);
            _banner.anchoredPosition = new Vector2((slideIn - 1f) * width + slideOut * slideOut * width, Reference.y * 0.17f);
        }

        // 勢いの線: カメラが次の席へ回っている間だけ、画面の端を回る向きに流れる。
        private void UpdateStreaks(WaveSeatRotation settings, float time)
        {
            var p = Mathf.Clamp01((time - settings.CameraStartTime) / Mathf.Max(0.01f, settings.cameraBlendSeconds));
            var strength = settings.speedLines && _moving && p > 0f && p < 1f ? Mathf.Sin(Mathf.PI * p) : 0f;
            var size = _canvasRect.rect.size;
            var radius = size.magnitude * 0.5f;
            var seed = Mathf.FloorToInt(Time.unscaledTime * 30f);
            var sign = FortressSeats.Clockwise ? -1f : 1f;
            for (var i = 0; i < _streaks.Count; i++)
            {
                var image = _streakImages[i];
                image.enabled = strength > 0.05f;
                if (!image.enabled)
                {
                    continue;
                }

                var f = Hash(seed, i);
                var angle = (i / (float)_streaks.Count + f * 0.03f) * Mathf.PI * 2f;
                var along = radius * (0.8f + f * 0.15f);
                var rect = _streaks[i];
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * along;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f * sign);
                rect.sizeDelta = new Vector2(radius * (0.18f + f * 0.25f), 6f + f * 10f);
                image.color = new Color(1f, 1f, 1f, 0.5f * strength * (0.5f + 0.5f * f));
            }
        }

        // OK マーク: 着地した人から順にポンと出て、締めが終わると縮んで消える。自分の砲台は YOU の矢印と重ならないよう右に出す。
        private void UpdateMarks(WaveSeatRotation settings)
        {
            if (_marks.Count == 0)
            {
                return;
            }

            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            var now = Time.unscaledTime;
            var shrink = _okHideAt > 0f ? Mathf.Clamp01((now - (_okHideAt - 0.3f)) / 0.3f) : 0f;
            for (var i = _marks.Count - 1; i >= 0; i--)
            {
                var mark = _marks[i];
                var age = now - mark.PopAt;
                if (mark.Turret == null || camera == null || shrink >= 1f)
                {
                    Destroy(mark.Root.gameObject);
                    _marks.RemoveAt(i);
                    continue;
                }

                var screen = camera.WorldToScreenPoint(mark.Turret.transform.position);
                var point = Vector2.zero;
                var visible = age >= 0f && screen.z > 0f &&
                              RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out point);
                mark.Root.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                if (!mark.Played)
                {
                    mark.Played = true;
                    if (!settings.onOk.IsEmpty)
                    {
                        FortressEffectPlayer.PlayOnce(settings.onOk, mark.Turret.transform.position, Quaternion.identity, mark.Player);
                    }
                }

                var u = Mathf.Clamp01(age / OkPopSeconds);
                var pop = u < 1f ? 1.35f * Mathf.Sin(u * Mathf.PI * 0.5f) - 0.35f * u : 1f;
                mark.Root.anchoredPosition = point + (mark.Local ? new Vector2(120f, 60f) : new Vector2(0f, 110f));
                mark.Root.localScale = Vector3.one * Mathf.Max(0f, pop * (1f - shrink));
                var color = FortressColors.PlayerColor(mark.Player);
                mark.Disc.color = new Color(color.r, color.g, color.b, 1f);
            }
        }

        // ───────── 作る ─────────

        private static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }

            // 今のシーンの普通のオブジェクトにする（この係は DontSave なので、子にすると Play を止めても残ってしまう。2026-11-02）。
            // シーンが閉じて消えたら、覚えている部品を忘れて作り直す。
            _streaks.Clear();
            _streakImages.Clear();
            _marks.Clear();
            var go = new GameObject("[Seats] Overlay", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)go.transform;

            _vignette = NewImage("Vignette", _canvasRect, SeatFxArt.Vignette);
            Stretch(_vignette.rectTransform);
            _vignette.enabled = false;

            for (var i = 0; i < StreakCount; i++)
            {
                var streak = NewImage("SpeedLine", _canvasRect, SeatFxArt.Streak);
                streak.enabled = false;
                _streaks.Add(streak.rectTransform);
                _streakImages.Add(streak);
            }

            BuildBanner();
        }

        private void BuildBanner()
        {
            var band = NewImage("Banner", _canvasRect, null);
            band.color = BandColor;
            _banner = band.rectTransform;
            _banner.anchorMin = new Vector2(0f, 0.5f);
            _banner.anchorMax = new Vector2(1f, 0.5f);
            _banner.sizeDelta = new Vector2(0f, BannerHeight);
            _banner.localRotation = Quaternion.Euler(0f, 0f, 1.2f);

            var accent = NewImage("Accent", _banner, null);
            accent.color = AccentColor;
            var accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 6f);
            accentRect.anchoredPosition = new Vector2(0f, -4f);

            _title = NewText("Title", _banner, 92, FontStyle.Bold, Color.white);
            _title.rectTransform.anchoredPosition = new Vector2(-110f, 0f);
            _subtitle = NewText("Subtitle", _banner, 34, FontStyle.Bold, AccentColor);
            _subtitle.rectTransform.anchoredPosition = new Vector2(330f, -18f);
            _banner.gameObject.SetActive(false);
        }

        private void BuildMark(OkMark mark)
        {
            var root = new GameObject("OkMark", typeof(RectTransform));
            root.transform.SetParent(_canvasRect, false);
            mark.Root = (RectTransform)root.transform;
            mark.Root.sizeDelta = new Vector2(64f, 64f);
            var edge = NewImage("Edge", mark.Root, SeatFxArt.Disc);
            edge.rectTransform.sizeDelta = new Vector2(72f, 72f);
            edge.color = Color.white;
            mark.Disc = NewImage("Disc", mark.Root, SeatFxArt.Disc);
            mark.Disc.rectTransform.sizeDelta = new Vector2(60f, 60f);
            var check = NewImage("Check", mark.Root, SeatFxArt.Check);
            check.rectTransform.sizeDelta = new Vector2(60f, 60f);
            check.color = Color.white;
            root.SetActive(false);
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(600f, BannerHeight);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float Hash(int seed, int index)
        {
            var v = Mathf.Sin(seed * 12.9898f + index * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private void HideMarks()
        {
            foreach (var mark in _marks)
            {
                if (mark.Root != null)
                {
                    Destroy(mark.Root.gameObject);
                }
            }

            _marks.Clear();
        }

        private void HideAll()
        {
            _moving = false;
            HideMarks();
            if (_canvas == null)
            {
                return;
            }

            _vignetteAlpha = 0f;
            _vignette.enabled = false;
            _banner.gameObject.SetActive(false);
            foreach (var image in _streakImages)
            {
                image.enabled = false;
            }
        }
    }
}
