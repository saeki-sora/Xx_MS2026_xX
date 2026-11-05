using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客の群れの「光の輪」と「名前札」を、この PC の画面に描く（起動時に自動で作られる。全員の PC で動く）。
    /// 菌の見た目はふつうのまま。群れのまわりを観客の色でぼんやり光らせ、上に「むしばい菌 #23  18体」を出す。
    /// 群れがばらけて輪が画面の 22% より大きくなったら、輪は消して札だけにする。
    /// 視点は PC ごとに違う（回っている人もいる）ので、各 PC が自分のカメラで位置を決める。
    /// 光の輪は HUD より下（並び順 <see cref="SortingOrder"/>）、名前札は HUD より上（<see cref="TagSortingOrder"/>。
    /// 出口が HUD の近くにあると、出てすぐの札が HUD に隠れるため）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SpectatorGroupDisplay : MonoBehaviour
    {
        private const int SortingOrder = -20;
        private const int TagSortingOrder = 30;
        private const float StaleSeconds = 1.5f;
        private const float FollowRate = 8f;
        private const float FadeRate = 5f;
        private const float HaloAlpha = 0.5f;
        private const float MaxHaloScreenFraction = 0.22f;
        private const float MinHaloPixels = 70f;
        private const int FontSize = 26;

        private sealed class View
        {
            public int Number;
            public bool Boss;
            public RectTransform Root;
            public RectTransform TagRoot;
            public Image Halo;
            public Image Pill;
            public Outline PillOutline;
            public Text Label;
            public Vector2 World;
            public float Radius;
            public float Alpha;
            public bool Live;
            public int Alive;
            public int ColorIndex;
        }

        private readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        private readonly List<int> _removeKeys = new List<int>();
        private RectTransform _canvasRect;
        private RectTransform _tagLayer;
        private Font _font;
        private Sprite _haloSprite;
        private Sprite _pillSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SpectatorGroupDisplay>() != null)
            {
                return;
            }

            // DontSave は付けない（付けると Play を止めても消えずに残る）。
            var go = new GameObject("[Spectator] Group Display");
            DontDestroyOnLoad(go);
            go.AddComponent<SpectatorGroupDisplay>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)transform;

            var tags = new GameObject("Tags", typeof(RectTransform));
            tags.transform.SetParent(transform, false);
            _tagLayer = (RectTransform)tags.transform;
            _tagLayer.anchorMin = Vector2.zero;
            _tagLayer.anchorMax = Vector2.one;
            _tagLayer.offsetMin = _tagLayer.offsetMax = Vector2.zero;
            var tagCanvas = tags.AddComponent<Canvas>();
            tagCanvas.overrideSorting = true;
            tagCanvas.sortingOrder = TagSortingOrder;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _haloSprite = MakeHaloSprite();
            _pillSprite = MakePillSprite();
        }

        private void OnDestroy()
        {
            SpectatorGroupFeed.Unregister();
            if (_haloSprite != null)
            {
                Destroy(_haloSprite.texture);
            }

            if (_pillSprite != null)
            {
                Destroy(_pillSprite.texture);
            }
        }

        private void LateUpdate()
        {
            SpectatorGroupFeed.EnsureMessaging();

            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            var show = FortressMatch.InMatch && camera != null && Time.unscaledTime - SpectatorGroupFeed.ReceivedAt < StaleSeconds;

            foreach (var view in _views.Values)
            {
                view.Live = false;
            }

            if (show)
            {
                foreach (var group in SpectatorGroupFeed.Latest)
                {
                    if (!_views.TryGetValue(group.Number * 2 + (group.Boss ? 1 : 0), out var view))
                    {
                        view = Create(group);
                        view.World = new Vector2(group.X, group.Y);
                        view.Radius = group.Radius;
                        _views.Add(group.Number * 2 + (group.Boss ? 1 : 0), view);
                    }

                    view.Live = true;
                    view.Alive = group.Alive;
                    view.ColorIndex = group.ColorIndex;
                    var follow = 1f - Mathf.Exp(-FollowRate * Time.unscaledDeltaTime);
                    view.World = Vector2.Lerp(view.World, new Vector2(group.X, group.Y), follow);
                    view.Radius = Mathf.Lerp(view.Radius, group.Radius, follow);
                }
            }

            _removeKeys.Clear();
            foreach (var pair in _views)
            {
                var view = pair.Value;
                var target = view.Live ? 1f : 0f;
                view.Alpha = Mathf.MoveTowards(view.Alpha, target, FadeRate * Time.unscaledDeltaTime);
                if (!view.Live && view.Alpha <= 0f)
                {
                    Destroy(view.Root.gameObject);
                    Destroy(view.TagRoot.gameObject);
                    _removeKeys.Add(pair.Key);
                    continue;
                }

                if (camera != null)
                {
                    Place(view, camera);
                }
            }

            foreach (var key in _removeKeys)
            {
                _views.Remove(key);
            }
        }

        private void Place(View view, Camera camera)
        {
            var center = camera.WorldToScreenPoint(new Vector3(view.World.x, view.World.y, 0f));
            if (center.z < 0f)
            {
                view.Root.gameObject.SetActive(false);
                view.TagRoot.gameObject.SetActive(false);
                return;
            }

            view.Root.gameObject.SetActive(true);
            view.TagRoot.gameObject.SetActive(true);
            var edge = camera.WorldToScreenPoint(new Vector3(view.World.x + Mathf.Max(0.01f, view.Radius), view.World.y, 0f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, center, null, out var localCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, edge, null, out var localEdge);
            var canvasSize = _canvasRect.rect.size;
            var radius = Mathf.Max(MinHaloPixels, Vector2.Distance(localCenter, localEdge) + 34f) * (view.Boss ? 1.35f : 1f);
            var tooSpread = radius > Mathf.Min(canvasSize.x, canvasSize.y) * MaxHaloScreenFraction;

            var color = view.Boss ? new Color(1f, 0.42f, 0.65f) : ParseColor(SpectatorColors.Hex[SpectatorColors.Clamp(view.ColorIndex)]);
            var pulse = view.Boss ? 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;

            view.Root.anchoredPosition = Vector2.zero;
            view.Halo.enabled = !tooSpread;
            view.Halo.rectTransform.anchoredPosition = localCenter;
            view.Halo.rectTransform.sizeDelta = Vector2.one * (radius * 2f);
            view.Halo.color = new Color(color.r, color.g, color.b, HaloAlpha * view.Alpha * pulse);

            view.Label.text = view.Boss ? $"みんなのボス菌" : $"{SpectatorNames.Make(view.Number)}  {view.Alive}体";
            var width = view.Label.preferredWidth + 40f;
            var height = FontSize * 1.7f;
            var tagCenter = localCenter + new Vector2(0f, (tooSpread ? 30f : Mathf.Max(40f, radius * 0.5f)) + height * 0.5f);
            var half = canvasSize * 0.5f;
            tagCenter.x = Mathf.Clamp(tagCenter.x, -half.x + width * 0.5f + 8f, half.x - width * 0.5f - 8f);
            tagCenter.y = Mathf.Clamp(tagCenter.y, -half.y + height * 0.5f + 8f, half.y - height * 0.5f - 8f);

            view.Pill.rectTransform.anchoredPosition = tagCenter;
            view.Pill.rectTransform.sizeDelta = new Vector2(width, height);
            view.Pill.color = new Color(0.03f, 0.05f, 0.08f, 0.82f * view.Alpha);
            view.PillOutline.effectColor = new Color(color.r, color.g, color.b, view.Alpha);
            view.Label.rectTransform.anchoredPosition = tagCenter;
            view.Label.color = new Color(1f, 1f, 1f, view.Alpha);
        }

        private View Create(SpectatorGroupState group)
        {
            var root = new GameObject(group.Boss ? "Boss" : "Group " + group.Number, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;

            var tagRootGo = new GameObject(root.name, typeof(RectTransform));
            tagRootGo.transform.SetParent(_tagLayer, false);
            var tagRect = (RectTransform)tagRootGo.transform;
            tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 0.5f);
            tagRect.sizeDelta = Vector2.zero;

            var halo = NewImage("Halo", rootRect, _haloSprite);
            var pill = NewImage("Tag", tagRect, _pillSprite);
            pill.type = Image.Type.Sliced;
            var outline = pill.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(tagRect, false);
            var label = labelGo.AddComponent<Text>();
            label.font = _font;
            label.fontSize = FontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(400f, FontSize * 1.6f);

            return new View
            {
                Number = group.Number,
                Boss = group.Boss,
                Root = rootRect,
                TagRoot = tagRect,
                Halo = halo,
                Pill = pill,
                PillOutline = outline,
                Label = label,
                ColorIndex = group.ColorIndex
            };
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Color ParseColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;

        /// <summary>中心が濃く、ふちへ向かってなめらかに消える白い丸。</summary>
        private static Sprite MakeHaloSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    var a = Mathf.SmoothStep(1f, 0f, d);
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>角が丸い白い四角（9 分割で伸ばして札の形にする）。</summary>
        private static Sprite MakePillSprite()
        {
            const int size = 32;
            const float r = 15.5f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var cx = Mathf.Clamp(px, r, size - r);
                    var cy = Mathf.Clamp(py, r, size - r);
                    var d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    var a = Mathf.Clamp01(r - d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(15f, 15f, 15f, 15f));
        }
    }
}
