using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 歯みがきタイムの画面の動き（値のつなぎでは出しにくい物だけ）。UIスタジオの「ウェーブの表示を入れる」が作る「BrushTime」に付く。
    /// ・歯についたバイキン: きれい度が上がるたびに1匹ずつ、ふくらんで泡になって消える（何割きれいになったかが絵で分かる）
    /// ・手の絵: 開く／閉じるをくり返す（「にぎにぎ」のやり方を、文字を読まなくても分かるように）
    /// ・歯ブラシ: その人がにぎにぎするたびに、その人の色の歯ブラシがシャカッと動く
    /// ・「+1」と泡: 自分がにぎにぎするたびに、歯のまわりに飛び出す
    /// ・100% で歯のうしろがキラッと光る
    /// 数字・バー・文字は値のつなぎ（brush.*）で出す。
    /// </summary>
    [AddComponentMenu("UI Studio/歯みがきタイムの動き (BrushTimeView)")]
    public sealed class BrushTimeView : MonoBehaviour
    {
        private const float PopSeconds = 0.32f;
        private const float PlusSeconds = 0.7f;
        private const float BubbleSeconds = 0.7f;

        [Tooltip("歯についたバイキン（きれい度に合わせて1匹ずつ消える）。")]
        public RectTransform[] germs = new RectTransform[0];

        [Tooltip("にぎにぎの手（開いた手｜閉じた手 の2コマが横に並んだ絵）。")]
        public RawImage hand;

        [Tooltip("P1〜P4 の歯ブラシ。")]
        public RectTransform[] brushes = new RectTransform[4];

        [Tooltip("100% のときに歯のうしろで光る絵。")]
        public Graphic shine;

        [Tooltip("泡の絵。")]
        public Sprite bubble;

        [Tooltip("「+1」や泡を出す場所（歯）。")]
        public RectTransform popArea;

        [Tooltip("「+1」の文字の色。")]
        public Color plusColor = new Color(1f, 0.27f, 0.67f);

        [Tooltip("歯みがきの間だけ隠す、普段の HUD（自分のパネル・ほかの人・撃破数・WAVE の札など）。終わったら戻す。")]
        public GameObject[] hideWhileBrushing = new GameObject[0];

        [Tooltip("手が1秒に何回にぎにぎするか。")]
        [Min(0.5f)]
        public float handBeatsPerSecond = 3f;

        private readonly float[] _lastShare = new float[4];
        private readonly float[] _wiggle = new float[4];
        private readonly Vector2[] _brushHome = new Vector2[4];
        private readonly List<Floater> _floaters = new List<Floater>();
        private float[] _germPop = new float[0];
        private Vector3[] _germScale = new Vector3[0];
        private int _lastPulses;
        private BrushingRunner _runner;
        private bool _hidden;
        private Font _font;

        private sealed class Floater
        {
            public Graphic graphic;
            public float age = 99f;
            public float life;
            public Vector2 from;
            public Vector2 drift;
            public bool isText;
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (var p = 0; p < brushes.Length && p < 4; p++)
            {
                if (brushes[p] != null)
                {
                    _brushHome[p] = brushes[p].anchoredPosition;
                }
            }

            _germPop = new float[germs.Length];
            _germScale = new Vector3[germs.Length];
            for (var i = 0; i < germs.Length; i++)
            {
                _germScale[i] = germs[i] != null ? germs[i].localScale : Vector3.one;
            }
        }

        private void Update()
        {
            var runner = FortressWaves.BreakRunner as BrushingRunner;
            if (runner != _runner)
            {
                _runner = runner;
                ResetLook();
            }

            SetOthersHidden(runner != null);
            if (runner == null)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var clean = runner.Clean01;
            UpdateGerms(clean, dt);

            if (shine != null)
            {
                var c = shine.color;
                c.a = clean >= 0.999f ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
                shine.color = c;
                shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 20f);
            }

            if (hand != null)
            {
                var counting = runner.Stage != BrushingStage.Result;
                var frame = counting ? Mathf.FloorToInt(Time.unscaledTime * handBeatsPerSecond * 2f) % 2 : 0;
                hand.uvRect = new Rect(frame * 0.5f, 0f, 0.5f, 1f);
            }

            UpdateBrushes(runner, dt);
            SpawnForPulses(runner.LocalPulses);
            UpdateFloaters(dt);
        }

        private void OnDisable()
        {
            SetOthersHidden(false);
        }

        // 普段の HUD を隠す／戻す（歯みがきの画面に集中できるように）。
        private void SetOthersHidden(bool hidden)
        {
            if (_hidden == hidden)
            {
                return;
            }

            _hidden = hidden;
            foreach (var other in hideWhileBrushing)
            {
                if (other != null)
                {
                    other.SetActive(!hidden);
                }
            }
        }

        private void ResetLook()
        {
            _lastPulses = 0;
            for (var p = 0; p < 4; p++)
            {
                _lastShare[p] = 0f;
                _wiggle[p] = 0f;
            }

            for (var i = 0; i < germs.Length; i++)
            {
                _germPop[i] = 0f;
                if (germs[i] != null)
                {
                    germs[i].gameObject.SetActive(true);
                    germs[i].localScale = _germScale[i];
                    SetAlpha(germs[i], 1f);
                }
            }
        }

        // バイキン i は、きれい度が (i+1)/(数+1) を越えたら、ふくらんで泡になって消える。越える前はゆらゆら。
        private void UpdateGerms(float clean, float dt)
        {
            for (var i = 0; i < germs.Length; i++)
            {
                var germ = germs[i];
                if (germ == null)
                {
                    continue;
                }

                var threshold = (i + 1f) / (germs.Length + 1f);
                if (clean >= threshold && _germPop[i] < PopSeconds)
                {
                    if (_germPop[i] <= 0f)
                    {
                        for (var k = 0; k < 4; k++)
                        {
                            SpawnBubble(germ.anchoredPosition);
                        }
                    }

                    _germPop[i] += dt;
                    var t = Mathf.Clamp01(_germPop[i] / PopSeconds);
                    germ.localScale = _germScale[i] * (t < 0.35f ? Mathf.Lerp(1f, 1.35f, t / 0.35f) : Mathf.Lerp(1.35f, 0f, (t - 0.35f) / 0.65f));
                    SetAlpha(germ, 1f - t * t);
                    if (t >= 1f)
                    {
                        germ.gameObject.SetActive(false);
                    }
                }
                else if (clean < threshold)
                {
                    var wobble = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f + i * 1.7f);
                    germ.localScale = _germScale[i] * wobble;
                    germ.localRotation = Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(Time.unscaledTime * 3f + i));
                }
            }
        }

        // その人の頑張りが増えたら（＝にぎにぎした）、その人の歯ブラシを歯に向かってこする。結果の間は、見出しにかぶらないようにしまう。
        private void UpdateBrushes(BrushingRunner runner, float dt)
        {
            var showing = runner.Stage != BrushingStage.Result;
            for (var p = 0; p < brushes.Length && p < 4; p++)
            {
                var brush = brushes[p];
                if (brush == null)
                {
                    continue;
                }

                brush.gameObject.SetActive(showing);

                var share = runner.Share01(p);
                if (share > _lastShare[p] + 1e-4f)
                {
                    _wiggle[p] = 0.24f;
                }

                _lastShare[p] = share;
                _wiggle[p] = Mathf.Max(0f, _wiggle[p] - dt);
                var t = _wiggle[p] / 0.24f;
                var swing = Mathf.Sin(t * Mathf.PI * 4f) * t;
                brush.anchoredPosition = _brushHome[p] + new Vector2(swing * 26f, Mathf.Abs(swing) * 8f);
                brush.localRotation = Quaternion.Euler(0f, 0f, swing * 9f);
            }
        }

        // 自分がにぎにぎしたら、「+1」と泡が歯のまわりに飛び出す。
        private void SpawnForPulses(int pulses)
        {
            if (pulses < _lastPulses)
            {
                _lastPulses = 0;
            }

            for (var n = _lastPulses; n < pulses && popArea != null; n++)
            {
                var half = popArea.rect.size * 0.42f;
                var at = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y * 0.2f, half.y));
                Spawn(true, at, new Vector2(Random.Range(-20f, 20f), Random.Range(70f, 110f)), PlusSeconds);
                SpawnBubble(at);
                SpawnBubble(at);
            }

            _lastPulses = pulses;
        }

        private void SpawnBubble(Vector2 at)
        {
            if (bubble != null)
            {
                Spawn(false, at + Random.insideUnitCircle * 30f, new Vector2(Random.Range(-40f, 40f), Random.Range(40f, 110f)), BubbleSeconds);
            }
        }

        private void Spawn(bool text, Vector2 at, Vector2 drift, float life)
        {
            if (popArea == null)
            {
                return;
            }

            Floater free = null;
            foreach (var f in _floaters)
            {
                if (f.isText == text && f.age >= f.life)
                {
                    free = f;
                    break;
                }
            }

            if (free == null)
            {
                if (_floaters.Count >= 60)
                {
                    return;
                }

                free = new Floater { isText = text, graphic = text ? (Graphic)CreatePlus() : CreateBubble() };
                _floaters.Add(free);
            }

            free.age = 0f;
            free.life = life;
            free.from = at;
            free.drift = drift;
        }

        private Text CreatePlus()
        {
            var go = new GameObject("Plus1", typeof(RectTransform));
            go.transform.SetParent(popArea, false);
            var label = go.AddComponent<Text>();
            label.font = _font;
            label.text = "+1";
            label.fontSize = 54;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = new Vector2(120f, 70f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            return label;
        }

        private Image CreateBubble()
        {
            var go = new GameObject("Bubble", typeof(RectTransform));
            go.transform.SetParent(popArea, false);
            var image = go.AddComponent<Image>();
            image.sprite = bubble;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = Vector2.one * 44f;
            return image;
        }

        private void UpdateFloaters(float dt)
        {
            foreach (var f in _floaters)
            {
                if (f.age >= f.life)
                {
                    f.graphic.enabled = false;
                    continue;
                }

                f.age += dt;
                var t = Mathf.Clamp01(f.age / f.life);
                f.graphic.enabled = true;
                var rect = f.graphic.rectTransform;
                rect.anchoredPosition = f.from + f.drift * t;
                if (f.isText)
                {
                    rect.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(0.4f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, (t - 0.15f) / 0.85f));
                    f.graphic.color = new Color(plusColor.r, plusColor.g, plusColor.b, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
                }
                else
                {
                    rect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, t);
                    f.graphic.color = new Color(1f, 1f, 1f, 1f - t);
                }
            }
        }

        private static void SetAlpha(RectTransform rect, float alpha)
        {
            var graphic = rect.GetComponent<Graphic>();
            if (graphic != null)
            {
                var c = graphic.color;
                c.a = alpha;
                graphic.color = c;
            }
        }
    }
}
