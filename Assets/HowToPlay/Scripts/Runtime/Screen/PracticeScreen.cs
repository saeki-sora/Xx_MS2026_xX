using System;
using System.Text;
using MS2026.CharacterSelect;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の画面「新人研修」（レイアウトB 下部ダイアログ・2026-11-12）。全員の PC に出る。
    /// ・上: 横長のフィールド。本番のステージ・コア・砲台（キャラのポッド）・敵の絵で、4人の砲台が全員映る（自分の砲台が上。本番のカメラと同じ向き）。
    ///   自分の砲台は自分の握力で動く（<see cref="PracticeTrainer.Local"/>）。ほかの人は届いた握力・熱・課題で動かす（敵は各PCが自分で出すだけ）。
    /// ・右: 全員の社員証（顔・研修のハンコ・熱・スキル・今やっていること）。
    /// ・下: 自分のキャラの顔の窓と、ギザギザの吹き出し（歯磨き粉のチューブ付き）、今の研修でやることの一覧。
    /// ・上の帯: ロゴ、研修の進み（歯磨き粉のチューブから出る線）、残り時間。
    /// 中身は <see cref="PracticeBoard"/>（と自分の <see cref="PracticeTrainer.Local"/>）を見て描くだけ。音は自分の席の分だけ鳴らす。
    /// </summary>
    public sealed partial class PracticeScreen : MonoBehaviour
    {
        private const float FadeSeconds = 0.3f;

        // 色（ゲームのUIと同じ、パステル＋紺の線）。
        internal static readonly Color Ink = new Color32(0x23, 0x2c, 0x7a, 0xff);
        internal static readonly Color Pink = new Color32(0xff, 0x66, 0xb3, 0xff);
        internal static readonly Color Mint = new Color32(0x5c, 0xe1, 0xf5, 0xff);
        internal static readonly Color Lavender = new Color32(0xa0, 0x8c, 0xff, 0xff);
        internal static readonly Color Burn = new Color32(0xff, 0x4b, 0x2b, 0xff);
        internal static readonly Color Ok = new Color32(0x1f, 0xbf, 0x74, 0xff);
        internal static readonly Color PanelTop = Color.white;
        internal static readonly Color PanelBottom = new Color32(0xeb, 0xe8, 0xff, 0xff);

        private static PracticeScreen _current;

        private HowToPlaySettings _settings;
        private PracticeScreenSettings _cfg;
        private PracticeBoard _board;
        private int _local;
        private Func<bool> _keepVisible;
        private string _stageScene;
        private float _time;
        private float _fade;
        private float _finishedAt = -1f;
        private readonly SeatInfo[] _seats = new SeatInfo[PracticeBoard.SeatCount];
        private readonly PracticeStep[] _lastSteps = new PracticeStep[PracticeBoard.SeatCount];
        private bool _lastOverheated;

        private CanvasGroup _group;
        private RectTransform _root;

        private sealed class SeatInfo
        {
            public CharacterDefinition Def;
            public PracticeCharacterLook Look;
            public Color Color;
            public Color Light;
            public Color Dark;
            public string Name;
            public Texture Pod;
            public Rect PodUv;
            public Texture IconOn;
            public Rect IconOnUv;
            public Texture IconOff;
            public Rect IconOffUv;
            public Texture Stand;
            public Rect StandUv;

            /// <summary>顔の窓の切り抜き（差し替えの絵があればその切り抜き、無ければキャラごとの設定）。</summary>
            public Rect PortraitCrop = new Rect(0.5f, 0.8f, 0.6f, 0f);
        }

        /// <summary>出しているか。</summary>
        public static bool IsShowing => _current != null;

        /// <summary>この画面を使う設定か（OFF なら前の画面 PracticeView）。</summary>
        public static bool Enabled(HowToPlaySettings settings) => settings != null && settings.screen != null && settings.screen.enabled;

        /// <summary>
        /// 練習の画面を出す。keepVisible が false になったら消える（null ならずっと）。
        /// stageSceneName = 抽選で決まったステージのシーン名（フィールドの背景に使う。分からなければ null）。
        /// </summary>
        public static PracticeScreen Play(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible, bool fadeIn, string stageSceneName)
        {
            // 練習のシーン（本物の砲台・敵・スキル）が使えるなら、それを重ねて読み込んで映す。
            var screen = settings != null ? settings.screen : null;
            var useScene = screen != null && screen.useTutorialScene && !string.IsNullOrEmpty(screen.tutorialSceneName) && Application.CanStreamedLevelBeLoaded(screen.tutorialSceneName);
            var view = Play(settings, board, localSeat, keepVisible, fadeIn, stageSceneName, useScene, null);
            if (view != null && useScene)
            {
                view.BeginScene(screen.tutorialSceneName);
            }

            return view;
        }

        /// <summary>
        /// realField = true: ゲームの中の研修。フィールドの枠に本物のゲームのカメラを映す（自分では描かない）。
        /// source = 自分の細かい様子を読む先（null ならロビーの練習の係 <see cref="PracticeTrainer.Local"/>）。
        /// </summary>
        public static PracticeScreen Play(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible, bool fadeIn, string stageSceneName, bool realField, IPracticeLocalSource source)
        {
            Hide(true);
            if (settings == null || board == null)
            {
                return null;
            }

            var go = new GameObject("[HowToPlay] Practice (新人研修)");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<PracticeScreen>();
            view._realField = realField;
            view._sourceOverride = source;
            view.Begin(settings, board, localSeat, keepVisible, fadeIn, stageSceneName);
            _current = view;
            return view;
        }

        /// <summary>出している画面を消す（immediate なら一瞬で、そうでなければふわっと）。</summary>
        public static void Hide(bool immediate = false)
        {
            if (_current == null)
            {
                return;
            }

            if (immediate)
            {
                Destroy(_current.gameObject);
                _current = null;
                return;
            }

            _current._keepVisible = () => false;
        }

        private void OnDestroy()
        {
            EndRealField();
            EndScene();
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Begin(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible, bool fadeIn, string stageSceneName)
        {
            _settings = settings;
            _cfg = PracticeArtLibrary.Fill(settings.screen); // 絵は Resources/PracticeArtLibrary の絵で差し替える
            PracticeTextLibrary.Apply(_cfg); // 文字は Resources/PracticeTextLibrary の文字で差し替える
            _board = board;
            _local = PracticeBoard.IsValid(localSeat) ? localSeat : 0;
            _keepVisible = keepVisible;
            _stageScene = stageSceneName;
            _fade = fadeIn ? 0f : 1f;
            for (var i = 0; i < _seats.Length; i++)
            {
                _seats[i] = MakeSeat(i);
                _lastSteps[i] = board.seats[i].step;
            }

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _group = HowToPlayArt.MakeCanvas(canvasGo, settings.sortingOrder);
            _root = HowToPlayArt.MakeRect("Screen", canvasGo.transform, Vector2.zero, new Vector2(1920f, 1080f));
            _group.alpha = _fade;

            BuildBackground();
            BuildField();
            // 上の帯（ロゴ・研修の進み・残り時間）は出さない（2026-11-19、その分フィールドと右の欄を広げた）。
            BuildCards();
            BuildChecklist();
            BuildDialog();
            BuildGuide();
            BuildOverlay();
        }

        private SeatInfo MakeSeat(int seat)
        {
            var info = new SeatInfo();
            var def = CharacterSelectionResult.Get(seat);
            info.Def = def;
            info.Look = def != null ? _cfg.FindCharacter(def.id) : null;
            if (info.Look == null && _cfg.characters != null && _cfg.characters.Length > 0)
            {
                info.Look = _cfg.characters[seat % _cfg.characters.Length];
            }

            // 色は選んだキャラの色（席の色ではない。P3 で黄色のキャラを選んだら黄色）。ゲーム中の砲台・ビームの色と同じ。
            info.Color = def != null ? def.themeColor : FortressColors.PlayerColor(seat);
            info.Color.a = 1f;
            info.Light = Color.Lerp(info.Color, Color.white, 0.6f);
            info.Dark = Color.Lerp(info.Color, Color.black, 0.35f);
            info.Name = info.Look != null && !string.IsNullOrEmpty(info.Look.displayName) ? info.Look.displayName : def != null ? def.DisplayName : $"{seat + 1}P";
            if (def != null)
            {
                SpriteUv(def.icon != null ? def.icon : def.sprite, out info.Pod, out info.PodUv);
                SpriteUv(def.buttonOn != null ? def.buttonOn : def.IconOrSprite, out info.IconOn, out info.IconOnUv);
                SpriteUv(def.buttonOff != null ? def.buttonOff : def.IconOrSprite, out info.IconOff, out info.IconOffUv);
                SpriteUv(def.sprite, out info.Stand, out info.StandUv);
            }

            // キャラの絵: Resources/PracticeArtLibrary の「キャラごとの絵」に入っていればそれ、無ければキャラ選択の絵。
            if (info.Look != null)
            {
                info.PortraitCrop = info.Look.portraitCrop;
            }

            var lib = PracticeArtLibrary.Load();
            var art = lib != null && def != null ? lib.FindCharacter(def.id) : null;
            if (art != null)
            {
                if (art.image != null)
                {
                    SpriteUv(art.image, out info.Stand, out info.StandUv);
                    info.PortraitCrop = art.crop;
                }

                if (art.iconOn != null)
                {
                    SpriteUv(art.iconOn, out info.IconOn, out info.IconOnUv);
                }

                if (art.iconOff != null)
                {
                    SpriteUv(art.iconOff, out info.IconOff, out info.IconOffUv);
                }

                if (art.pod != null)
                {
                    SpriteUv(art.pod, out info.Pod, out info.PodUv);
                }
            }

            return info;
        }

        private static void SpriteUv(Sprite sprite, out Texture texture, out Rect uv)
        {
            texture = null;
            uv = new Rect(0f, 0f, 1f, 1f);
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            texture = sprite.texture;
            var r = sprite.textureRect;
            uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height);
        }

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _time += dt;
            var keep = _keepVisible == null || _keepVisible();
            _fade = keep ? Mathf.Min(1f, _fade + (dt / FadeSeconds)) : _fade - (dt / FadeSeconds);
            if (!keep && _fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _group.alpha = Mathf.Clamp01(_fade);
            if (_board.finished && _finishedAt < 0f)
            {
                _finishedAt = _time;
                FortressEffectPlayer.PlaySound(_settings.onFinished, Vector3.zero);
            }

            TickScene();
            TickSounds();
            TickBackground(dt);
            TickField(dt);
            TickCards();
            TickChecklist();
            TickDialog(dt);
            TickGuide();
            TickOverlay();
        }

        private void TickSounds()
        {
            for (var i = 0; i < _seats.Length; i++)
            {
                var step = _board.seats[i].step;
                if (step > _lastSteps[i])
                {
                    OnStepChanged(i, _lastSteps[i], step);
                    if (i == _local && !_board.finished)
                    {
                        FortressEffectPlayer.PlaySound(step == PracticeStep.Done ? _settings.onLocalDone : _settings.onStepDone, Vector3.zero);
                    }

                    _lastSteps[i] = step;
                }
            }

            var overheated = LocalOverheated;
            if (overheated && !_lastOverheated)
            {
                FortressEffectPlayer.PlaySound(_settings.onOverheat, Vector3.zero);
            }

            _lastOverheated = overheated;
        }

        // ───────── 自分の様子（係があれば係から、無ければ届いた値から） ─────────

        private bool _realField;
        private IPracticeLocalSource _sourceOverride;

        /// <summary>ロビーの練習の係（自分の砲台を描くのに使う）。ゲームの中の研修では無い。</summary>
        private PracticeTrainer Trainer => _realField ? null : PracticeTrainer.Local;

        /// <summary>自分の細かい様子（握り続けの進み・スキル・オーバーヒート）。</summary>
        private IPracticeLocalSource Source => _sourceOverride ?? PracticeTrainer.Local;

        private PracticeStep LocalStep => _board.seats[_local].step;

        private bool LocalOverheated => Source != null ? Source.Overheated : _board.seats[_local].overheated;

        // ───────── 部品 ─────────

        /// <summary>画面の左上を原点にした位置（1920×1080）を、真ん中原点の位置にする。</summary>
        internal static Vector2 P(float x, float y) => new Vector2(x - 960f, 540f - y);

        internal static RectTransform MakeRect(string name, Transform parent, float x, float y, float w, float h)
        {
            return HowToPlayArt.MakeRect(name, parent, P(x + (w * 0.5f), y + (h * 0.5f)), new Vector2(w, h));
        }

        internal static PracticeShape Shape(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var shape = go.AddComponent<PracticeShape>();
            shape.raycastTarget = false;
            shape.rectTransform.anchoredPosition = position;
            shape.rectTransform.sizeDelta = size;
            return shape;
        }

        internal static RawImage Raw(string name, Transform parent, Texture texture, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        /// <summary>
        /// 文字（2026-11-19）: フォントは絵の置き場（Resources/PracticeArtLibrary）の「文字のフォント」（Zen Maru Gothic Bold）。
        /// 箱からはみ出さないよう、入らないときは自動で小さくする（Best Fit。いちばん大きくて size、いちばん小さくて size の 55%）。
        /// 1 文字ずつ出る吹き出しのセリフだけは、途中で大きさが変わらないよう、これを切って自分で大きさを決める（WrapLine）。
        /// </summary>
        internal static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor anchor, Vector2 position, Vector2 box)
        {
            var label = HowToPlayArt.MakeText(name, parent, text, size, color, anchor, position, box);
            var lib = PracticeArtLibrary.Load();
            if (lib != null && lib.font != null)
            {
                label.font = lib.font;
                label.fontStyle = FontStyle.Normal; // Bold の絵のフォントなので、さらに太らせない
            }

            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = size;
            label.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.55f));
            return label;
        }

        /// <summary>あとから文字の大きさを変える（自動で小さくする幅も一緒に変える）。</summary>
        internal static void SetSize(Text label, int size)
        {
            label.fontSize = size;
            label.resizeTextMaxSize = size;
            label.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.55f));
        }

        /// <summary>白い文字に紺のふち（見出し用）。</summary>
        internal static Text Heading(string name, Transform parent, string text, int size, Vector2 position, Vector2 box, TextAnchor anchor = TextAnchor.MiddleCenter, Color? shadow = null)
        {
            var label = Label(name, parent, text, size, Color.white, anchor, position, box);
            HowToPlayArt.AddOutline(label, Ink, Mathf.Max(2f, size * 0.08f));
            var second = label.gameObject.AddComponent<Shadow>();
            second.effectColor = shadow ?? Pink;
            second.effectDistance = new Vector2(size * 0.08f, -size * 0.08f);
            return label;
        }

        /// <summary>白→薄紫の、紺のふちの札（角丸）。</summary>
        internal static void Panel(PracticeShape shape, Vector2 center, Vector2 size, float radius, Color? top = null, Color? bottom = null, Color? edge = null, float edgeWidth = 4f)
        {
            shape.Fill(PracticeShape.RoundRect(center + new Vector2(5f, -7f), size, radius), new Color(Ink.r, Ink.g, Ink.b, 0.22f));
            var body = PracticeShape.RoundRect(center, size, radius);
            shape.Fill(body, top ?? PanelTop, bottom ?? PanelBottom);
            shape.Stroke(body, edgeWidth, edge ?? Ink, true);
            shape.Fill(PracticeShape.RoundRect(center + new Vector2(0f, (size.y * 0.5f) - 11f), new Vector2(size.x - 24f, 7f), 3.5f), new Color(1f, 1f, 1f, 0.85f));
        }

        internal static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>【】で囲んだ所を色付きにし、visible 文字だけ見せる（タグは数えない）。</summary>
        internal static string Rich(string marked, Color highlight, int visible, out bool complete)
        {
            complete = true;
            if (string.IsNullOrEmpty(marked))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var open = false;
            var shown = 0;
            foreach (var ch in marked)
            {
                if (ch == '【')
                {
                    sb.Append("<color=#").Append(Hex(highlight)).Append('>');
                    open = true;
                    continue;
                }

                if (ch == '】')
                {
                    if (open)
                    {
                        sb.Append("</color>");
                    }

                    open = false;
                    continue;
                }

                if (shown >= visible)
                {
                    complete = false;
                    break;
                }

                sb.Append(ch);
                shown++;
            }

            if (open)
            {
                sb.Append("</color>");
            }

            return sb.ToString();
        }

        /// <summary>画面の文字（Resources/PracticeTextLibrary。Tools › あそびかた › 研修の絵と文字 で差し替える）。</summary>
        private static PracticeUiTexts Ui => PracticeTextLibrary.Ui;

        internal static string Plain(string marked) => string.IsNullOrEmpty(marked) ? string.Empty : marked.Replace("【", string.Empty).Replace("】", string.Empty);

        private static Sprite _ring;
        private static Sprite _vignette;

        /// <summary>輪（ふちの太さ 18%）。円グラフのように塗る用。</summary>
        internal static Sprite Ring
        {
            get
            {
                if (_ring != null)
                {
                    return _ring;
                }

                const int S = 128;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (var y = 0; y < S; y++)
                {
                    for (var x = 0; x < S; x++)
                    {
                        var d = new Vector2(x - (S * 0.5f) + 0.5f, y - (S * 0.5f) + 0.5f).magnitude / (S * 0.5f);
                        var a = Mathf.Clamp01((1f - d) * S * 0.5f) * Mathf.Clamp01((d - 0.78f) * S * 0.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                tex.Apply();
                _ring = Sprite.Create(tex, new UnityEngine.Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                return _ring;
            }
        }

        /// <summary>まわりだけ色が付く絵（オーバーヒートの赤い縁）。</summary>
        internal static Sprite Vignette
        {
            get
            {
                if (_vignette != null)
                {
                    return _vignette;
                }

                const int S = 64;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (var y = 0; y < S; y++)
                {
                    for (var x = 0; x < S; x++)
                    {
                        var u = Mathf.Abs((x + 0.5f) / S * 2f - 1f);
                        var v = Mathf.Abs((y + 0.5f) / S * 2f - 1f);
                        var d = Mathf.Max(u, v);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d))));
                    }
                }

                tex.Apply();
                _vignette = Sprite.Create(tex, new UnityEngine.Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                return _vignette;
            }
        }

        internal static float EaseOutBack(float t) => HowToPlayArt.EaseOutBack(t);

        internal static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        // ───────── 背景 ─────────

        private void BuildBackground()
        {
            if (_realField)
            {
                // フィールドの所だけ穴をあける（そこに本物のゲームのカメラが映る）。背景の絵を4枚に切って並べる。
                // 穴はカメラが映す所（フィールド）とぴったり同じ大きさ（2026-11-20）。前は 8 ずつ大きくしていて、
                // カメラが映らない細い帯が黒く見えていた（太い枠のときは隠れていたが、枠の絵は細く角が丸いので、端に黒い物が見えた）。
                BuildBackgroundAround(FieldX, FieldY, FieldW, FieldH);
                return;
            }

            var bg = Raw("Background", _root, _cfg.background, Vector2.zero, new Vector2(1920f, 1080f));
            if (bg.texture == null)
            {
                bg.color = new Color32(0xee, 0xe9, 0xff, 0xff);
            }

            if (_cfg.halftone != null)
            {
                var a = Raw("Halftone A", _root, _cfg.halftone, P(1660f, 950f), new Vector2(520f, 260f));
                a.color = new Color(1f, 1f, 1f, 0.22f);
                var b = Raw("Halftone B", _root, _cfg.halftone, P(260f, 130f), new Vector2(520f, 260f));
                b.color = new Color(1f, 1f, 1f, 0.22f);
                b.rectTransform.localScale = new Vector3(-1f, -1f, 1f);
            }

            var words = Label("Words", _root, string.Empty, 120, new Color(Lavender.r, Lavender.g, Lavender.b, 0.08f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(3600f, 2400f));
            var sb = new StringBuilder();
            for (var i = 0; i < 9; i++)
            {
                sb.Append(i % 2 == 0 ? "BUGBRUSH BUGBRUSH BUGBRUSH BUGBRUSH\n" : "SHBUGBRU SHBUGBRU SHBUGBRU SHBUGBRU\n");
            }

            words.text = sb.ToString();
            words.lineSpacing = 1.1f;
            words.rectTransform.localEulerAngles = new Vector3(0f, 0f, 17f);
            _bgWordsText = words;
        }

        private Text _bgWordsText;

        private void TickBackground(float dt)
        {
            if (_bgWordsText != null)
            {
                var p = _bgWordsText.rectTransform.anchoredPosition;
                p.x = Mathf.Repeat(p.x + (dt * 30f) + 300f, 600f) - 300f;
                _bgWordsText.rectTransform.anchoredPosition = p;
            }
        }
    }
}
