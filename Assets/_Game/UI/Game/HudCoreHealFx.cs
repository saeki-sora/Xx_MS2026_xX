using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// HUD のコアの体力バー（`CoreBar`）に、コアが回復したときの緑の演出を付ける（2026-10-20、緑スキル「ミント・サテライト」と一緒に追加）。
    /// ・回復した分の区間がミント色に光ってから消える（じわじわ回復の間は区間が伸び続ける）。
    /// ・バーの枠の外側がミント色に光り、歯のアイコンの後ろが光り、回復の十字（＋）がバーの先から浮かぶ。
    /// ・一度に大きく回復したとき（既定 4% 以上）は、光が強く十字も多い。
    /// 回復の理由は問わない（`core.hp01` が増えたら出る）。試合の始め（HP を満タンにする）では出さない。
    /// プレハブは作り直さない: ゲーム中に `CoreBar`（子に Body/Gauge/Fill がある物）を見つけて自動で付ける。
    /// 手で付けても良い（そのときは値を自由に変えられる）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI Studio/コアの体力バーの回復演出 (HudCoreHealFx)")]
    public sealed class HudCoreHealFx : MonoBehaviour
    {
        private const int SparkleCount = 14;

        [Tooltip("回復の光の色。")]
        public Color healColor = new Color(0.42f, 1f, 0.66f, 1f);

        [Tooltip("一度にこれ以上（0〜1、最大HPに対する割合）回復したら「大きな回復」として強く光らせる。")]
        [Range(0.005f, 0.5f)]
        public float bigHealThreshold = 0.04f;

        [Tooltip("回復した区間の光が消えるまでの秒数。")]
        [Min(0.05f)]
        public float segmentFadeSeconds = 0.9f;

        [Tooltip("この秒数より間が空いたら、別の回復として区間を作り直す（じわじわ回復をひと続きに見せるため）。")]
        [Min(0.05f)]
        public float streakGapSeconds = 0.4f;

        [Tooltip("見つけてから（試合の始めから）この秒数は、HP が増えても光らせない（満タンにするときに光らないように）。")]
        [Min(0f)]
        public float ignoreSecondsAfterStart = 1.5f;

        [Tooltip("じわじわ回復の間に十字を出す間隔（秒）。")]
        [Min(0.05f)]
        public float sparkleInterval = 0.22f;

        private static readonly List<HudCoreHealFx> Instances = new List<HudCoreHealFx>();
        private static Texture2D _gradientTexture;
        private static Texture2D _softTexture;
        private static Texture2D _crossTexture;

        private RectTransform _fill;
        private RectTransform _body;
        private Image _segment;
        private Image _frameGlow;
        private Image _toothGlow;
        private readonly Sparkle[] _sparkles = new Sparkle[SparkleCount];
        private IDisposable _subscription;

        private float _enabledTime;
        private float _last = -1f;
        private float _segmentStart;
        private float _segmentEnd;
        private float _lastHealTime = -100f;
        private float _segmentAlpha;
        private float _glow;
        private float _sparkleTimer;
        private bool _built;

        private sealed class Sparkle
        {
            public RawImage Image;
            public float Age = 99f;
            public float Life = 1f;
            public Vector2 Start;
            public Vector2 Velocity;
            public float Size;
        }

        /// <summary>この CoreBar に付いていなければ付ける（自動で付ける係が使う）。</summary>
        public static HudCoreHealFx Attach(GameObject coreBar)
        {
            if (coreBar == null)
            {
                return null;
            }

            var existing = coreBar.GetComponent<HudCoreHealFx>();
            return existing != null ? existing : coreBar.AddComponent<HudCoreHealFx>();
        }

        private void OnEnable()
        {
            Instances.Add(this);
            Build();
            _enabledTime = Time.unscaledTime;
            _last = -1f;
            _subscription = UiValues.Subscribe(FortressUiKeys.CoreHp01, HandleHealthChanged);
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            _subscription?.Dispose();
            _subscription = null;
            HideAll();
        }

        private void HandleHealthChanged(UiValue value)
        {
            if (value.Kind != UiValueKind.Number)
            {
                return;
            }

            var current = Mathf.Clamp01(value.Number);
            var previous = _last;
            _last = current;
            if (previous < 0f || previous <= 0.0001f || current <= previous + 0.0002f)
            {
                return;
            }

            if (Time.unscaledTime - _enabledTime < ignoreSecondsAfterStart)
            {
                return;
            }

            OnHealed(previous, current);
        }

        private void OnHealed(float from, float to)
        {
            if (!_built)
            {
                return;
            }

            var now = Time.unscaledTime;
            var big = to - from >= bigHealThreshold;
            if (now - _lastHealTime > streakGapSeconds || _segmentAlpha <= 0.01f)
            {
                _segmentStart = from;
            }

            _segmentEnd = to;
            _lastHealTime = now;
            _segmentAlpha = 1f;
            _glow = Mathf.Max(_glow, big ? 1f : 0.45f);
            if (big)
            {
                for (var i = 0; i < 8; i++)
                {
                    SpawnSparkle(Mathf.Lerp(from, to, UnityEngine.Random.value), 1.25f);
                }
            }
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var healing = Time.unscaledTime - _lastHealTime < streakGapSeconds;
            if (!healing)
            {
                _segmentAlpha = Mathf.Max(0f, _segmentAlpha - dt / segmentFadeSeconds);
            }

            _glow = Mathf.Max(healing ? 0.35f : 0f, _glow - dt * 1.6f);

            if (healing)
            {
                _sparkleTimer -= dt;
                if (_sparkleTimer <= 0f)
                {
                    _sparkleTimer = sparkleInterval;
                    SpawnSparkle(_segmentEnd, 0.9f);
                }
            }

            ApplySegment();
            ApplyGlows();
            TickSparkles(dt);
        }

        // ───────────── 作る ─────────────

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _fill = FindChild(transform, "Fill") as RectTransform;
            _body = FindChild(transform, "Body") as RectTransform;
            if (_fill == null || _body == null)
            {
                return;
            }

            // 回復した区間: Fill の子（Fill の四角はバー全体なので、横の 0〜1 がそのまま HP の割合になる）。
            _segment = CreateImage("HealSegment", _fill, Sprite.Create(GradientTexture, new Rect(0f, 0f, GradientTexture.width, GradientTexture.height), new Vector2(0.5f, 0.5f)));
            _segment.rectTransform.anchorMin = new Vector2(0f, 0f);
            _segment.rectTransform.anchorMax = new Vector2(0f, 1f);

            // バーの外側の光: やわらかい光をバーより一回り大きく、バーの後ろ（Gauge の手前の順番）へ。
            var frame = FindChild(_body, "Frame") as RectTransform;
            var gauge = FindChild(_body, "Gauge") as RectTransform;
            if (frame != null)
            {
                var soft = Sprite.Create(SoftTexture, new Rect(0f, 0f, SoftTexture.width, SoftTexture.height), new Vector2(0.5f, 0.5f));
                _frameGlow = CreateImage("HealFrameGlow", frame.parent, soft);
                Match(_frameGlow.rectTransform, frame, 28f);
                var behind = gauge != null && gauge.parent == frame.parent ? gauge : frame;
                _frameGlow.rectTransform.SetSiblingIndex(behind.GetSiblingIndex());
            }

            // 歯のアイコンの後ろの光。
            var tooth = FindChild(_body, "Tooth") as RectTransform;
            if (tooth != null)
            {
                _toothGlow = CreateImage("HealToothGlow", tooth.parent, Sprite.Create(SoftTexture, new Rect(0f, 0f, SoftTexture.width, SoftTexture.height), new Vector2(0.5f, 0.5f)));
                Match(_toothGlow.rectTransform, tooth, Mathf.Max(tooth.rect.width, tooth.rect.height) * 0.35f);
                _toothGlow.rectTransform.SetSiblingIndex(tooth.GetSiblingIndex());
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                var go = new GameObject("HealCross" + i, typeof(RectTransform));
                go.transform.SetParent(_body, false);
                var image = go.AddComponent<RawImage>();
                image.texture = CrossTexture;
                image.raycastTarget = false;
                image.enabled = false;
                _sparkles[i] = new Sparkle { Image = image };
            }

            _built = true;
            HideAll();
        }

        private Image CreateImage(string objectName, Transform parent, Sprite sprite)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            var rect = image.rectTransform;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>target と同じ場所・大きさ（+ grow ピクセル）にする。</summary>
        private static void Match(RectTransform rect, RectTransform target, float grow)
        {
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.anchoredPosition = target.anchoredPosition;
            rect.sizeDelta = target.sizeDelta + new Vector2(grow * 2f, grow * 2f);
            rect.localRotation = target.localRotation;
            rect.localScale = target.localScale;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var found = FindChild(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ───────────── 動かす ─────────────

        private void ApplySegment()
        {
            if (_segment == null)
            {
                return;
            }

            if (_segmentAlpha <= 0.001f || _segmentEnd <= _segmentStart)
            {
                _segment.enabled = false;
                return;
            }

            var rect = _segment.rectTransform;
            rect.anchorMin = new Vector2(Mathf.Clamp01(_segmentStart), 0f);
            rect.anchorMax = new Vector2(Mathf.Clamp01(_segmentEnd), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 14f);
            var color = Color.Lerp(healColor, Color.white, 0.25f * pulse);
            color.a = _segmentAlpha;
            _segment.color = color;
            _segment.enabled = true;
        }

        private void ApplyGlows()
        {
            var breathe = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 9f);
            if (_frameGlow != null)
            {
                var color = healColor;
                color.a = Mathf.Clamp01(_glow * 0.9f * breathe);
                _frameGlow.color = color;
                _frameGlow.enabled = color.a > 0.005f;
            }

            if (_toothGlow != null)
            {
                var color = healColor;
                color.a = Mathf.Clamp01(_glow * breathe);
                _toothGlow.color = color;
                _toothGlow.enabled = color.a > 0.005f;
                var scale = 1f + _glow * 0.15f;
                _toothGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>バーの横の位置 at01（0〜1）から十字を浮かばせる。</summary>
        private void SpawnSparkle(float at01, float strength)
        {
            if (_fill == null || _body == null)
            {
                return;
            }

            Sparkle slot = null;
            for (var i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i].Age >= _sparkles[i].Life)
                {
                    slot = _sparkles[i];
                    break;
                }
            }

            if (slot == null)
            {
                return;
            }

            // Fill の中の位置を Body の座標へ。
            var fillRect = _fill.rect;
            var local = new Vector3(Mathf.Lerp(fillRect.xMin, fillRect.xMax, Mathf.Clamp01(at01)), Mathf.Lerp(fillRect.yMin, fillRect.yMax, UnityEngine.Random.Range(0.2f, 0.8f)), 0f);
            var world = _fill.TransformPoint(local);
            var inBody = (Vector2)_body.InverseTransformPoint(world);
            slot.Start = inBody;
            slot.Velocity = new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(55f, 95f)) * strength;
            slot.Size = UnityEngine.Random.Range(16f, 26f) * strength;
            slot.Life = UnityEngine.Random.Range(0.7f, 1f);
            slot.Age = 0f;
            slot.Image.transform.SetAsLastSibling();
        }

        private void TickSparkles(float dt)
        {
            for (var i = 0; i < _sparkles.Length; i++)
            {
                var sparkle = _sparkles[i];
                if (sparkle.Age >= sparkle.Life)
                {
                    sparkle.Image.enabled = false;
                    continue;
                }

                sparkle.Age += dt;
                var t = Mathf.Clamp01(sparkle.Age / sparkle.Life);
                var rect = sparkle.Image.rectTransform;
                rect.anchoredPosition = sparkle.Start + sparkle.Velocity * sparkle.Age;
                var pop = t < 0.15f ? Mathf.Lerp(0.3f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 0.8f, (t - 0.15f) / 0.85f);
                rect.sizeDelta = new Vector2(sparkle.Size * pop, sparkle.Size * pop);
                var color = healColor;
                color.a = 1f - t * t;
                sparkle.Image.color = color;
                sparkle.Image.enabled = true;
            }
        }

        private void HideAll()
        {
            _segmentAlpha = 0f;
            _glow = 0f;
            if (_segment != null)
            {
                _segment.enabled = false;
            }

            if (_frameGlow != null)
            {
                _frameGlow.enabled = false;
            }

            if (_toothGlow != null)
            {
                _toothGlow.enabled = false;
            }

            for (var i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] != null && _sparkles[i].Image != null)
                {
                    _sparkles[i].Age = 99f;
                    _sparkles[i].Image.enabled = false;
                }
            }
        }

        // ───────────── 絵（コードで描く） ─────────────

        /// <summary>右（回復の先）ほど明るくなる横のグラデーション。</summary>
        private static Texture2D GradientTexture
        {
            get
            {
                if (_gradientTexture != null)
                {
                    return _gradientTexture;
                }

                _gradientTexture = NewTexture(64, 8, (x, y) => new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 1f, x * x)));
                return _gradientTexture;
            }
        }

        private static Texture2D SoftTexture
        {
            get
            {
                if (_softTexture != null)
                {
                    return _softTexture;
                }

                _softTexture = NewTexture(64, 64, (x, y) =>
                {
                    var r = new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude;
                    return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.8f));
                });
                return _softTexture;
            }
        }

        private static Texture2D CrossTexture
        {
            get
            {
                if (_crossTexture != null)
                {
                    return _crossTexture;
                }

                _crossTexture = NewTexture(48, 48, (x, y) =>
                {
                    var p = new Vector2(Mathf.Abs(x * 2f - 1f), Mathf.Abs(y * 2f - 1f));
                    var arm = Mathf.Min(Mathf.Max(p.x - 0.8f, p.y - 0.26f), Mathf.Max(p.x - 0.26f, p.y - 0.8f));
                    var a = Mathf.Clamp01(0.5f - arm * 24f);
                    var glow = Mathf.Pow(Mathf.Clamp01(1f - new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude), 2f) * 0.35f;
                    return new Color(1f, 1f, 1f, Mathf.Max(a, glow));
                });
                return _crossTexture;
            }
        }

        private static Texture2D NewTexture(int width, int height, Func<float, float, Color> paint)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HudCoreHealFx",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = paint((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // ───────────── 自動で付ける係 ─────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFinder()
        {
            if (FindFirstObjectByType<Finder>() != null)
            {
                return;
            }

            var go = new GameObject("[UI] CoreHealFxFinder") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<Finder>();
        }

        /// <summary>0.5 秒おきに、画面に出ている `CoreBar`（Body/Gauge/Fill を持つ物）を探して、回復の演出を付ける。</summary>
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
                if (Instances.Count > 0)
                {
                    return;
                }

                var coreBar = GameObject.Find("CoreBar");
                if (coreBar != null && FindChild(coreBar.transform, "Fill") != null && coreBar.GetComponentInParent<Canvas>() != null)
                {
                    Attach(coreBar);
                }
            }
        }
    }
}
