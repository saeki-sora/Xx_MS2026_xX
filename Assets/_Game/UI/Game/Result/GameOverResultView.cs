using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>クリア・ゲームオーバー共通の結果表示。クリアではGAME OVERのバナーを省略する。</summary>
    [DefaultExecutionOrder(1020)] // 歯の材質がそのフレームのHPに更新された後に撮る。
    internal sealed class GameOverResultView : MonoBehaviour
    {
        private const int BannerFps = 15;
        private const int BannerFrames = 75;
        private const int Columns = 4;
        private const int Rows = 5;
        private const float Width = 1920f;
        private const float Height = 1080f;
        // 参考動画では GAME OVER の文字が残っている4秒付近から、約1秒かけて移動する。
        private const float CoreSlideStart = 4.05f;
        private const float CoreSlideEnd = 5.1f;
        private const float PlayerCapsSlideDuration = 0.25f;
        // Titleシーンの背景Sprite (TitleSpriteFit.CoverCamera) と同じ余白。
        private const float TitleBackgroundOverscan = 1.12f;
        // タイトル連番の1コマの移動（左22px・下8px）×30コマ/秒。
        private const float BackgroundLeftPerSecond = 660f;
        private const float BackgroundDownPerSecond = 240f;
        private static readonly Color Ink = new Color(0.09f, 0.08f, 0.25f);
        private static readonly string[] PlayerColors = { "red", "blue", "green", "yellow" };

        private Dictionary<string, Texture2D> _art;
        private Font _font;
        private bool _clear;
        private RectTransform _frame;
        private Camera _gameplayCamera;
        private RawImage _banner;
        private RawImage _background;
        private RawImage _tooth;
        private RectTransform _gauge;
        private RawImage _gaugeFill;
        private float _finalCoreHealth;
        private const float GaugeFillLeft = 265f;
        private const float GaugeFillWidth = 566f;
        private bool _captureCore;
        private RenderTexture _corePortrait;
        private readonly Texture2D[] _bannerPages = new Texture2D[4];
        private CanvasGroup _panel;
        private RectTransform _playerCaps;
        private Text _next;
        private RectTransform _holdFill;
        private readonly List<RectMask2D> _rowMasks = new List<RectMask2D>();
        private readonly List<RectTransform> _rowFaces = new List<RectTransform>();

        public static GameOverResultView Create(Transform parent, MatchResult result, float brightness, RenderTexture corePortrait = null)
        {
            var textures = Resources.LoadAll<Texture2D>("GameOver");
            var art = new Dictionary<string, Texture2D>();
            foreach (var texture in textures)
                art[texture.name] = texture;
            if (!art.ContainsKey("background") || !art.ContainsKey("result_header_line")) return null;
            if (result.outcome != MatchOutcome.Clear)
                for (var page = 0; page < 4; page++)
                    if (!art.ContainsKey($"banner_{page}")) return null;

            var root = new GameObject("Match Result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            // Presenterはシーンをまたぐが、試合の全面Canvasは試合シーンと一緒に破棄する。
            var swarm = SwarmSystem.Current;
            if (swarm != null && swarm.gameObject.scene.isLoaded)
                SceneManager.MoveGameObjectToScene(root, swarm.gameObject.scene);
            else
                root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250; // HUD・メニューより上、ポーズや遷移より下。
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UiRoot.Active?.layerSettings?.matchWidthOrHeight ?? 0.5f;

            var view = root.AddComponent<GameOverResultView>();
            view._art = art;
            view._clear = result.outcome == MatchOutcome.Clear;
            view._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            view.Build((RectTransform)root.transform, result, brightness);
            if (corePortrait != null)
            {
                view._corePortrait = corePortrait;
                view._tooth.texture = corePortrait;
                view._tooth.color = Color.white;
                view._captureCore = false;
            }
            view.Render(0f, 0f, MatchResultPresenter.AutoSeconds);
            return view;
        }

        private void Build(RectTransform root, MatchResult result, float brightness)
        {
            Solid(root, "Letterbox", 0, 0, Width, Height, Ink);
            var frame = _frame = Rect(root, "Gameplay viewport", 0, 0, Width, Height);
            var rig = FortressCameraRig.Active;
            _gameplayCamera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            MatchGameplayViewport();
            frame.gameObject.AddComponent<RectMask2D>();
            BuildBackground(frame);

            _tooth = Art(frame, "core_tooth", 97, 461, 500, 460);
            _tooth.color = Color.Lerp(new Color(0.76f, 0.70f, 0.65f), Color.white, Mathf.Clamp01(brightness));
            _captureCore = true;
            // 冒頭は中央上部で満タンから減り、試合終了時の耐久値で止まる。
            _gauge = Rect(frame, "Durability gauge", 0, 0, Width, Height);
            Art(_gauge, "core_gauge", 128, 158, 727, 152);
            _gaugeFill = Art(_gauge, "Hud_CoreFill", GaugeFillLeft, 216, GaugeFillWidth, 34);
            _finalCoreHealth = result.coreHealth01;

            var details = Rect(frame, "Result panels", 0, 0, Width, Height);
            _panel = details.gameObject.AddComponent<CanvasGroup>();
            Art(details, "result_header_line", 0, 0, Width, 125);
            Art(details, "result_under_line", 0, 997, Width, 83);
            Label(details, "Total", $"全員で {result.totalKills:N0} 体", 790, 23, 340, 54, 30, Ink);
            Label(details, "Reason", WaveHudLogic.ResultSubtitle(result.reason, result.reachedWave, result.waveCount),
                88, 305, 850, 58, 29, Ink);
            Status(details, 617, $"コアの耐久  {result.coreHealth01:P0}");
            Status(details, 761, $"コアの輝き  {brightness:P0}");

            var players = ResultRules.PlayersByKills(result.kills, EnemyKillCounter.PlayerCount);
            _playerCaps = Rect(details, "Player color caps", 0, 0, Width, Height);
            for (var slot = 0; slot < players.Length; slot++)
            {
                BuildPlayerRow(details, result, players[slot], slot);
            }
            _playerCaps.SetAsLastSibling();

            _next = Label(details, "Countdown", "", 277, 1001, 740, 51, 25, Ink, TextAnchor.MiddleLeft);
            Solid(details, "Hold back", 276, 1053, 320, 8, new Color(0.1f, 0.12f, 0.24f, 0.2f));
            _holdFill = Solid(details, "Hold fill", 276, 1053, 320, 8, new Color(0.27f, 0.19f, 0.73f)).rectTransform;

            _banner = Art(frame, "banner_0", 80, 150, 1760, 720);
            for (var page = 0; page < _bannerPages.Length; page++)
                _art.TryGetValue($"banner_{page}", out _bannerPages[page]);
        }

        private void Status(RectTransform panel, int y, string value)
        {
            Art(panel, "result_status_line", 545, y - 1, 85, 69);
            Art(panel, "result_status_shadow", 623, y + 13, 325, 92);
            Art(panel, "result_status_base", 615, y + 5, 325, 92);
            Label(panel, "Core value", value, 636, y + 10, 284, 78, 28, Color.white);
        }

        private void BuildPlayerRow(RectTransform panel, MatchResult result, int player, int slot)
        {
            var width = new[] { 840, 810, 760, 720 }[slot];
            var row = Rect(panel, $"Player {player + 1}", Width - width - 55, 205 + slot * 190, width + 55, 180);
            _rowMasks.Add(row.gameObject.AddComponent<RectMask2D>());
            // 子も1920×1080座標で配置できるよう、内側に同じ基準寸法の座標面を置く。
            var face = new GameObject("Card art", typeof(RectTransform)).GetComponent<RectTransform>();
            _rowFaces.Add(face);
            face.SetParent(row, false);
            face.anchorMin = face.anchorMax = new Vector2(0f, 1f);
            face.pivot = new Vector2(0f, 1f);
            face.sizeDelta = new Vector2(Width, Height);
            Art(face, $"result_player_base_{slot + 1}", 0, 0, width, 180);
            Art(_playerCaps, $"result_player_design_{PlayerColors[player]}", Width - 59, 209 + slot * 190, 59, 171);
            var inset = new[] { 0, 2, 8, 6 }[slot];
            Label(face, "Player", $"{player + 1}P", 185 - inset, 22, 102, 37, 22, Ink);
            Label(face, "Kills", $"{ResultRules.KillsAt(result.kills, player):N0}", 271 - inset, 83, 120, 49, 32, Ink);
            Label(face, "Combo", $"×{At(result.maxCombos, player):N0}", 495 - inset, 83, 120, 49, 32, Ink);
            if (player == result.Mvp)
                Art(face, "result_design_king", 62, 48, 66, 67);
        }

        private void BuildBackground(RectTransform frame)
        {
            // タイトル連番には青い台座が描き込まれているため、リザルト用の帯だけを流す。
            _background = Art(frame, "background", Width / 2f, Height / 2f, 0f, 0f);
            UpdateBackground(0f);
        }

        private void UpdateBackground(float elapsed)
        {
            if (_background == null || _background.texture == null) return;
            var size = new Vector2(_background.texture.width, _background.texture.height);
            var viewportSize = _frame.rect.size;
            var scale = Mathf.Max(viewportSize.x / size.x, viewportSize.y / size.y) * TitleBackgroundOverscan;
            var fittedSize = size * scale;
            if (_background.rectTransform.sizeDelta != fittedSize)
                _background.rectTransform.sizeDelta = fittedSize;
            _background.uvRect = new Rect(Mathf.Repeat(elapsed * BackgroundLeftPerSecond / Width, 1f),
                Mathf.Repeat(elapsed * BackgroundDownPerSecond / Height, 1f), 1f, 1f);
        }

        private void MatchGameplayViewport()
        {
            var viewport = _gameplayCamera != null ? _gameplayCamera.rect : new Rect(0f, 0f, 1f, 1f);
            if (_frame.anchorMin == viewport.min && _frame.anchorMax == viewport.max) return;
            _frame.anchorMin = viewport.min;
            _frame.anchorMax = viewport.max;
            _frame.offsetMin = _frame.offsetMax = Vector2.zero;
        }

        public void Render(float elapsed, float hold, float autoLeft)
        {
            // CLEARの入りは専用演出で完了している。最終状態のコアと結果パネルから表示する。
            if (_clear) elapsed += ResultRules.ClearResultEntryTime;
            MatchGameplayViewport();
            UpdateBackground(elapsed);
            var viewportScale = new Vector2(_frame.rect.width / Width, _frame.rect.height / Height);
            var health = ResultRules.GameOverGaugeHealth(elapsed, _finalCoreHealth);
            var fillRect = _gaugeFill.rectTransform;
            fillRect.anchorMax = new Vector2((GaugeFillLeft + GaugeFillWidth * health) / Width, fillRect.anchorMax.y);
            _gaugeFill.uvRect = new Rect(0f, 0f, health, 1f);
            var bannerVisible = elapsed < ResultRules.GameOverPanelTime;
            _banner.enabled = bannerVisible;
            if (bannerVisible)
            {
                var frame = Mathf.Clamp((int)(elapsed * BannerFps), 0, BannerFrames - 1);
                var cell = frame % (Columns * Rows);
                _banner.texture = _bannerPages[frame / (Columns * Rows)];
                _banner.uvRect = new Rect((cell % Columns) / (float)Columns,
                    (Rows - 1 - cell / Columns) / (float)Rows, 1f / Columns, 1f / Rows);
            }

            var panelElapsed = elapsed - ResultRules.GameOverPanelTime;
            var panelProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(panelElapsed / 0.35f));
            var coreSlideRemaining = 1f - Mathf.InverseLerp(CoreSlideStart, CoreSlideEnd, elapsed);
            _tooth.rectTransform.anchoredPosition = Vector2.Scale(new Vector2(612f, 110f), viewportScale) * coreSlideRemaining;
            _gauge.anchoredPosition = Vector2.Scale(new Vector2(470f, 45f), viewportScale) * coreSlideRemaining;
            _panel.alpha = panelProgress;
            var capsProgress = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(CoreSlideEnd, CoreSlideEnd + PlayerCapsSlideDuration, elapsed));
            _playerCaps.anchoredPosition = new Vector2((1f - capsProgress) * 59f * viewportScale.x, 0f);
            for (var i = 0; i < _rowMasks.Count; i++)
            {
                // カードの位置は固定。下位から順に、右から左へマスクを開く。
                var progress = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((panelElapsed - 0.8f - (_rowMasks.Count - 1 - i) * 0.65f) / 0.6f));
                var mask = _rowMasks[i];
                mask.gameObject.SetActive(progress > 0f);
                var row = (RectTransform)mask.transform;
                // 右端の55pxは4色のパーツ用。カード自体の幅で開く。
                var cardWidth = Mathf.Max(0f, row.rect.width - 55f * viewportScale.x);
                mask.padding = new Vector4(cardWidth * (1f - progress), 0f, 55f * viewportScale.x, 0f);
                _rowFaces[i].localScale = new Vector3(viewportScale.x, viewportScale.y, 1f);
            }

            var ready = elapsed >= ResultRules.GameOverNextTime;
            _next.text = ready ? $"{Mathf.CeilToInt(autoLeft)} 秒で自動的にタイトルへ" : "リザルト表示中…";
            _holdFill.anchorMax = new Vector2((276f + 320f * Mathf.Clamp01(hold)) / Width, _holdFill.anchorMax.y);
        }

        private static int At(int[] values, int index) => values != null && index < values.Length ? values[index] : 0;

        private void LateUpdate()
        {
            if (!_captureCore) return;
            _captureCore = false;

            var core = FindFirstObjectByType<CoreCrystalController>();
            var model = core != null ? core.GetComponentInChildren<CoreToothModel>() : null;
            if (model == null) return;
            _corePortrait = CaptureCore(model);
            if (_corePortrait == null) return;
            _tooth.texture = _corePortrait;
            _tooth.color = Color.white;
            _tooth.enabled = true;
        }

        private void OnDestroy()
        {
            if (_corePortrait == null) return;
            _corePortrait.Release();
            Destroy(_corePortrait);
        }

        internal static RenderTexture CaptureCore(CoreToothModel model)
        {
            var sources = new List<Renderer>();
            model.CollectRenderers(sources);
            // コピーはステージから離した場所に置き、ゲームのコアやカメラには触らない。
            const int captureLayer = 31;
            var offset = new Vector3(4096f, 0f, 0f) - model.transform.position;
            var studio = new GameObject("Result core capture");
            var block = new MaterialPropertyBlock();
            var bakedMeshes = new List<Mesh>();
            RenderTexture target = null;
            try
            {
                var bounds = new Bounds();
                var hasBounds = false;
                foreach (var source in sources)
                {
                    if (!source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy) continue;
                    Mesh mesh;
                    if (source is SkinnedMeshRenderer skinned)
                    {
                        mesh = new Mesh();
                        bakedMeshes.Add(mesh);
                        skinned.BakeMesh(mesh);
                    }
                    else if (source.TryGetComponent<MeshFilter>(out var filter))
                    {
                        mesh = filter.sharedMesh;
                    }
                    else continue;
                    if (mesh == null) continue;

                    var part = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.layer = captureLayer;
                    part.transform.SetParent(studio.transform, false);
                    part.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
                    part.transform.localScale = source.transform.lossyScale;
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = part.GetComponent<MeshRenderer>();
                    copy.sharedMaterials = source.sharedMaterials;
                    copy.renderingLayerMask = source.renderingLayerMask;
                    source.GetPropertyBlock(block);
                    if (!block.isEmpty) copy.SetPropertyBlock(block);
                    for (var i = 0; i < copy.sharedMaterials.Length; i++)
                    {
                        source.GetPropertyBlock(block, i);
                        if (!block.isEmpty) copy.SetPropertyBlock(block, i);
                    }

                    if (hasBounds) bounds.Encapsulate(copy.bounds);
                    else bounds = copy.bounds;
                    hasBounds = true;
                }
                if (!hasBounds) return null;

                var cameraObject = new GameObject("Portrait camera");
                cameraObject.transform.SetParent(studio.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << captureLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var gameplayCamera = Camera.main;
                camera.transform.rotation = gameplayCamera != null ? gameplayCamera.transform.rotation : Quaternion.identity;
                var extents = bounds.extents;
                var right = camera.transform.right;
                var up = camera.transform.up;
                float Project(Vector3 axis) => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;
                const int width = 500;
                const int height = 460;
                camera.aspect = (float)width / height;
                camera.orthographicSize = Mathf.Max(0.05f, Mathf.Max(Project(up), Project(right) / camera.aspect) * 1.06f);
                var distance = Mathf.Max(1f, extents.magnitude * 2f);
                camera.transform.position = bounds.center - camera.transform.forward * distance;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 2f;

                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Result core portrait" };
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else if (GraphicsSettings.currentRenderPipeline == null)
                {
                    camera.targetTexture = target;
                    camera.Render();
                    camera.targetTexture = null;
                }
                else
                {
                    Destroy(target);
                    return null;
                }
                return target;
            }
            catch (System.Exception exception)
            {
                if (target != null) Destroy(target);
                Debug.LogWarning($"[Result] コアの画像を撮影できませんでした: {exception.Message}");
                return null;
            }
            finally
            {
                studio.SetActive(false);
                Destroy(studio);
                foreach (var mesh in bakedMeshes) Destroy(mesh);
            }
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x / Width, 1f - (y + h) / Height);
            rect.anchorMax = new Vector2((x + w) / Width, 1f - y / Height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private RawImage Art(Transform parent, string name, float x, float y, float w, float h)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<RawImage>();
            if (_art.TryGetValue(name, out var texture))
                image.texture = texture;
            else
                image.enabled = false;
            image.raycastTarget = false;
            return image;
        }

        private static Image Solid(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float w, float h,
            int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.text = value;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }
    }
}
