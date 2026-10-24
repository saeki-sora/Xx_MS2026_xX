using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 歯みがきタイムの画面の動き（値のつなぎでは出しにくい物だけ）。UIスタジオの「ウェーブの表示を入れる」が作る「BrushTime」に付く。
    /// ・歯の汚れ: きれい度に合わせて薄くなる（100% でキラッ）
    /// ・手の絵: 開く／閉じるをくり返す（「にぎにぎ」のやり方を、文字を読まなくても分かるように）
    /// ・歯ブラシ: その人がにぎにぎするたびに、その人の色の歯ブラシがシャカッと動く
    /// ・泡: 自分がにぎにぎするたびに、歯のまわりに泡がはじける
    /// 数字・バー・文字は値のつなぎ（brush.*）で出す。
    /// </summary>
    [AddComponentMenu("UI Studio/歯みがきタイムの動き (BrushTimeView)")]
    public sealed class BrushTimeView : MonoBehaviour
    {
        [Tooltip("歯の汚れ（まとめて薄くする）。")]
        public CanvasGroup stains;

        [Tooltip("にぎにぎの手（開いた手｜閉じた手 の2コマが横に並んだ絵）。")]
        public RawImage hand;

        [Tooltip("P1〜P4 の歯ブラシ。")]
        public RectTransform[] brushes = new RectTransform[4];

        [Tooltip("100% のときに光る絵。")]
        public Graphic shine;

        [Tooltip("泡の絵。")]
        public Sprite bubble;

        [Tooltip("泡を出す場所（歯）。")]
        public RectTransform bubbleArea;

        [Tooltip("手が1秒に何回にぎにぎするか。")]
        [Min(0.5f)]
        public float handBeatsPerSecond = 3f;

        private readonly float[] _lastShare = new float[4];
        private readonly float[] _wiggle = new float[4];
        private readonly Vector2[] _brushHome = new Vector2[4];
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        private int _lastPulses;
        private float _stainAlpha = 1f;
        private BrushingRunner _runner;

        private sealed class Bubble
        {
            public Image image;
            public float age = 99f;
            public Vector2 from;
            public Vector2 drift;
        }

        private void Awake()
        {
            for (var p = 0; p < brushes.Length && p < 4; p++)
            {
                if (brushes[p] != null)
                {
                    _brushHome[p] = brushes[p].anchoredPosition;
                }
            }
        }

        private void Update()
        {
            var runner = FortressWaves.BreakRunner as BrushingRunner;
            if (runner != _runner)
            {
                _runner = runner;
                _stainAlpha = 1f;
                _lastPulses = 0;
                for (var p = 0; p < 4; p++)
                {
                    _lastShare[p] = 0f;
                    _wiggle[p] = 0f;
                }
            }

            if (runner == null)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var clean = runner.Clean01;
            _stainAlpha = Mathf.MoveTowards(_stainAlpha, 1f - clean, dt * 2.5f);
            if (stains != null)
            {
                stains.alpha = _stainAlpha;
            }

            if (shine != null)
            {
                var c = shine.color;
                c.a = clean >= 0.999f ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
                shine.color = c;
            }

            if (hand != null)
            {
                var counting = runner.Stage != BrushingStage.Result;
                var frame = counting ? Mathf.FloorToInt(Time.unscaledTime * handBeatsPerSecond * 2f) % 2 : 0;
                hand.uvRect = new Rect(frame * 0.5f, 0f, 0.5f, 1f);
            }

            UpdateBrushes(runner, dt);
            UpdateBubbles(runner, dt);
        }

        // その人の頑張りが増えたら（＝にぎにぎした）、その人の歯ブラシを左右にこする。
        private void UpdateBrushes(BrushingRunner runner, float dt)
        {
            for (var p = 0; p < brushes.Length && p < 4; p++)
            {
                var brush = brushes[p];
                if (brush == null)
                {
                    continue;
                }

                var share = runner.Share01(p);
                if (share > _lastShare[p] + 1e-4f)
                {
                    _wiggle[p] = 0.22f;
                }

                _lastShare[p] = share;
                _wiggle[p] = Mathf.Max(0f, _wiggle[p] - dt);
                var t = _wiggle[p] / 0.22f;
                brush.anchoredPosition = _brushHome[p] + new Vector2(Mathf.Sin(t * Mathf.PI * 4f) * 28f * t, 0f);
                brush.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 10f * t);
            }
        }

        // 自分がにぎにぎしたら、歯のまわりに泡がはじける。
        private void UpdateBubbles(BrushingRunner runner, float dt)
        {
            var pulses = runner.LocalPulses;
            for (var n = _lastPulses; n < pulses && bubbleArea != null && bubble != null; n++)
            {
                for (var k = 0; k < 3; k++)
                {
                    Spawn();
                }
            }

            _lastPulses = pulses;
            foreach (var b in _bubbles)
            {
                if (b.age >= 0.7f)
                {
                    b.image.enabled = false;
                    continue;
                }

                b.age += dt;
                var t = b.age / 0.7f;
                b.image.enabled = true;
                b.image.rectTransform.anchoredPosition = b.from + b.drift * t;
                b.image.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.1f, t);
                b.image.color = new Color(1f, 1f, 1f, 1f - t);
            }
        }

        private void Spawn()
        {
            Bubble free = null;
            foreach (var b in _bubbles)
            {
                if (b.age >= 0.7f)
                {
                    free = b;
                    break;
                }
            }

            if (free == null)
            {
                if (_bubbles.Count >= 30)
                {
                    return;
                }

                var go = new GameObject("Bubble", typeof(RectTransform));
                go.transform.SetParent(bubbleArea, false);
                var image = go.AddComponent<Image>();
                image.sprite = bubble;
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = Vector2.one * 46f;
                free = new Bubble { image = image };
                _bubbles.Add(free);
            }

            var half = bubbleArea.rect.size * 0.4f;
            free.age = 0f;
            free.from = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            free.drift = new Vector2(Random.Range(-30f, 30f), Random.Range(40f, 90f));
        }
    }
}
