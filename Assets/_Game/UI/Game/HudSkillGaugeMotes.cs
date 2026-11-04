using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 自分が敵を倒すと、倒した所から自分の色の光の粒が弧を描いて飛び、左下のスキルゲージの先端に吸い込まれる（見た目だけ。2026-11-04）。
    /// 着いた所でゲージの先端が白く光る。ゲージの量は今まで通り <see cref="PlayerSkillGauge"/> が決める（粒は数えない）。
    /// 今までの「火花が砲台へ飛ぶ」演出はそのまま。通信しない（倒した人は各PCで見えていたビームから決まる）。
    /// プレハブは作り直さない: ゲーム中に、自分のスキルゲージ（UiBindFill の key が local.skillGauge の画像）を見つけて自動で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI Studio/撃破の光がスキルゲージへ飛ぶ演出 (HudSkillGaugeMotes)")]
    public sealed class HudSkillGaugeMotes : MonoBehaviour
    {
        [Tooltip("OFFにすると粒を飛ばさない。")]
        public bool effectEnabled = true;

        [Range(1, 6), Tooltip("1体倒したときに飛ばす粒の数。")]
        public int motesPerKill = 1;

        [Range(1, 30), Tooltip("1フレームに新しく飛ばす粒の最大数（たくさん同時に倒したときに画面が粒だらけにならないように）。")]
        public int maxNewPerFrame = 4;

        [Range(8, 128), Tooltip("同時に飛んでいる粒の最大数。")]
        public int maxActive = 48;

        [Tooltip("飛ぶ時間（秒。最小〜最大）。")]
        public Vector2 flightSeconds = new Vector2(0.55f, 0.8f);

        [Min(4f), Tooltip("粒の大きさ（画面の高さ 1080 のときの px）。")]
        public float moteSize = 46f;

        [Range(0f, 1f), Tooltip("弧のふくらみ（飛ぶ距離に対する割合）。")]
        public float arc = 0.35f;

        [Tooltip("ゲージに着いたときに先端が光る秒数。")]
        [Min(0.01f)]
        public float arriveFlashSeconds = 0.22f;

        private struct Mote
        {
            public Image Image;
            public Vector2 From;
            public Vector2 Control;
            public float Age;
            public float Duration;
            public Color Color;
        }

        private readonly List<Mote> _active = new List<Mote>();
        private readonly Stack<Image> _pool = new Stack<Image>();

        private Image _fill;
        private RectTransform _layer;
        private Image _flash;
        private float _flashAge = 99f;
        private SwarmSystem _subscribed;
        private int _spawnedThisFrame;
        private static Sprite _glowSprite;

        /// <summary>このゲージの画像に付いていなければ付ける（自動で付ける係が使う）。</summary>
        public static void Attach(Image fill)
        {
            if (fill == null || fill.GetComponent<HudSkillGaugeMotes>() != null)
            {
                return;
            }

            fill.gameObject.AddComponent<HudSkillGaugeMotes>();
        }

        private void Awake()
        {
            _fill = GetComponent<Image>();
        }

        private void OnDisable()
        {
            Subscribe(null);
            foreach (var m in _active)
            {
                Release(m.Image);
            }

            _active.Clear();
            if (_flash != null)
            {
                _flash.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_layer != null)
            {
                Destroy(_layer.gameObject);
            }
        }

        private void Update()
        {
            Subscribe(effectEnabled ? SwarmSystem.Current : null);
        }

        private void Subscribe(SwarmSystem system)
        {
            if (_subscribed == system)
            {
                return;
            }

            if (_subscribed != null)
            {
                _subscribed.AgentsDied -= HandleDeaths;
            }

            _subscribed = system;
            if (_subscribed != null)
            {
                _subscribed.AgentsDied += HandleDeaths;
            }
        }

        private void HandleDeaths(IReadOnlyList<SwarmDeath> deaths)
        {
            var local = LocalPlayer();
            var camera = FortressCameraRig.Active != null ? FortressCameraRig.Active.targetCamera : null;
            if (!effectEnabled || local < 0 || camera == null || _fill == null || !EnsureLayer())
            {
                return;
            }

            var color = FortressColors.PlayerColor(local);
            for (var i = 0; i < deaths.Count; i++)
            {
                if (deaths[i].owner != local)
                {
                    continue;
                }

                var p = deaths[i].position;
                var screen = camera.WorldToScreenPoint(new Vector3(p.x, p.y, -0.4f));
                if (screen.z <= 0f)
                {
                    continue;
                }

                for (var k = 0; k < motesPerKill; k++)
                {
                    if (_spawnedThisFrame >= maxNewPerFrame || _active.Count >= maxActive)
                    {
                        return;
                    }

                    Spawn(ScreenToLayer(screen), color);
                    _spawnedThisFrame++;
                }
            }
        }

        private void LateUpdate()
        {
            _spawnedThisFrame = 0;
            if (_layer == null)
            {
                return;
            }

            var target = GaugeTip();
            var scale = LayerScale();
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var m = _active[i];
                m.Age += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(m.Age / m.Duration);
                if (t >= 1f)
                {
                    Release(m.Image);
                    _active.RemoveAt(i);
                    _flashAge = 0f;
                    continue;
                }

                // だんだん速くなってゲージに吸い込まれる（2乗の曲線）。
                var u = t * t;
                var a = Vector2.Lerp(m.From, m.Control, u);
                var b = Vector2.Lerp(m.Control, target, u);
                var rect = m.Image.rectTransform;
                rect.anchoredPosition = Vector2.Lerp(a, b, u);
                var size = moteSize * scale * Mathf.Lerp(1.1f, 0.55f, u);
                rect.sizeDelta = new Vector2(size, size);
                var c = m.Color;
                c.a = Mathf.SmoothStep(0f, 1f, m.Age / 0.08f);
                m.Image.color = c;
                _active[i] = m;
            }

            UpdateFlash(target, scale);
        }

        private void Spawn(Vector2 from, Color color)
        {
            var target = GaugeTip();
            var delta = target - from;
            var side = Random.value < 0.5f ? -1f : 1f;
            var normal = new Vector2(-delta.y, delta.x).normalized * side;
            var control = Vector2.Lerp(from, target, 0.3f) + normal * (delta.magnitude * arc) + Vector2.up * (delta.magnitude * 0.15f);
            var image = Acquire();
            image.rectTransform.anchoredPosition = from;
            var glow = color;
            glow.r = Mathf.Lerp(glow.r, 1f, 0.35f);
            glow.g = Mathf.Lerp(glow.g, 1f, 0.35f);
            glow.b = Mathf.Lerp(glow.b, 1f, 0.35f);
            _active.Add(new Mote
            {
                Image = image,
                From = from,
                Control = control,
                Age = 0f,
                Duration = Random.Range(Mathf.Min(flightSeconds.x, flightSeconds.y), Mathf.Max(flightSeconds.x, flightSeconds.y)),
                Color = glow
            });
        }

        private void UpdateFlash(Vector2 target, float scale)
        {
            if (_flash == null)
            {
                return;
            }

            _flashAge += Time.unscaledDeltaTime;
            var t = _flashAge / arriveFlashSeconds;
            _flash.enabled = t < 1f;
            if (!_flash.enabled)
            {
                return;
            }

            var size = moteSize * scale * Mathf.Lerp(2.4f, 1.4f, t);
            _flash.rectTransform.anchoredPosition = target;
            _flash.rectTransform.sizeDelta = new Vector2(size, size);
            _flash.color = new Color(1f, 1f, 1f, 1f - t);
        }

        // ゲージの塗りの先端（今の量の所）。粒を置く層の中の位置で返す。
        private Vector2 GaugeTip()
        {
            var rect = _fill.rectTransform.rect;
            var amount = _fill.type == Image.Type.Filled ? _fill.fillAmount : 1f;
            var local = new Vector3(rect.xMin + rect.width * Mathf.Clamp01(amount), rect.center.y, 0f);
            var world = _fill.rectTransform.TransformPoint(local);
            return (Vector2)_layer.InverseTransformPoint(world);
        }

        private Vector2 ScreenToLayer(Vector3 screen)
        {
            var canvas = _layer.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, uiCamera, out var local);
            return local;
        }

        // 画面の高さ 1080 を基準にした大きさの倍率（粒の層の座標で）。
        private float LayerScale()
        {
            var h = _layer.rect.height;
            return h > 1f ? h / 1080f : 1f;
        }

        // 粒を置く層（ゲージのいちばん上のキャンバスの上に、画面いっぱいの透明な層を1つ作る）。
        private bool EnsureLayer()
        {
            if (_layer != null)
            {
                return true;
            }

            var canvas = _fill.canvas != null ? _fill.canvas.rootCanvas : null;
            if (canvas == null)
            {
                return false;
            }

            // DontSave は付けない（Play を止めたら HUD と一緒に消えるように）。
            var go = new GameObject("[HUD] SkillGaugeMotes", typeof(RectTransform));
            _layer = (RectTransform)go.transform;
            _layer.SetParent(canvas.transform, false);
            _layer.anchorMin = Vector2.zero;
            _layer.anchorMax = Vector2.one;
            _layer.offsetMin = Vector2.zero;
            _layer.offsetMax = Vector2.zero;
            _layer.SetAsLastSibling();

            _flash = NewImage("ArriveFlash");
            _flash.enabled = false;
            return true;
        }

        private Image Acquire()
        {
            var image = _pool.Count > 0 ? _pool.Pop() : NewImage("Mote");
            image.enabled = true;
            image.transform.SetAsLastSibling();
            return image;
        }

        private void Release(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = false;
            _pool.Push(image);
        }

        private Image NewImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_layer, false);
            var image = go.GetComponent<Image>();
            image.sprite = GlowSprite();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        // 白い芯＋やわらかいにじみの丸（画像ファイルを使わず作る）。
        private static Sprite GlowSprite()
        {
            if (_glowSprite != null)
            {
                return _glowSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SkillGaugeMoteGlow", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var core = Mathf.Clamp01(1f - Mathf.Max(0f, r - 0.26f) / 0.08f);
                    var halo = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f) * 0.9f;
                    var a = Mathf.Clamp01(Mathf.Max(core, halo));
                    // 芯は白く、まわりは色（Image の色）になるように、芯だけ明るさを上げる。
                    var white = (byte)Mathf.RoundToInt(Mathf.Lerp(200f, 255f, core));
                    pixels[y * size + x] = new Color32(white, white, white, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _glowSprite.hideFlags = HideFlags.DontSave;
            return _glowSprite;
        }

        // 自分のプレイヤー番号（HUD の掲示板に毎フレーム書かれている値）。
        private static int LocalPlayer()
        {
            return Mathf.RoundToInt(UiValues.GetNumber(FortressUiKeys.LocalPlayer, -1f));
        }

        // ───────────── 自動で付ける係 ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFinder()
        {
            if (FindFirstObjectByType<Finder>() != null)
            {
                return;
            }

            var go = new GameObject("[UI] SkillGaugeMotesFinder") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<Finder>();
        }

        /// <summary>0.5 秒おきに、自分のスキルゲージ（key が local.skillGauge の UiBindFill）を探して、粒の演出を付ける。</summary>
        private sealed class Finder : MonoBehaviour
        {
            private float _timer;

            private void Update()
            {
                _timer -= Time.unscaledDeltaTime;
                if (_timer > 0f)
                {
                    return;
                }

                _timer = 0.5f;
                foreach (var bind in FindObjectsByType<UiBindFill>(FindObjectsSortMode.None))
                {
                    if (bind.key == FortressUiKeys.LocalSkillGauge && bind.fill != null && bind.isActiveAndEnabled)
                    {
                        Attach(bind.fill);
                    }
                }
            }
        }
    }
}
