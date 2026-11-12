using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CyberCleanLook
{
    /// <summary>
    /// 歯みがきタイムへの泡の切り替え（見た目だけ）。<see cref="CyberCleanScreenFx"/> が持つ。
    /// 1. 待つ: WAVE クリアの後、「WAVE CLEAR!」とキラーンを見せる間
    /// 2. 覆う: 泡が左から順にふくらみ、HUD ごと画面を埋める（歯みがきタイムが始まる前に覆い終える）
    /// 3. 待つ: 覆ったまま、歯みがきタイムが始まるのを待つ（切り替わりを泡の下に隠す）
    /// 4. はじける: 大きな泡から、小さな泡は左から順にはじけて、しずくが飛び、歯みがきタイムの画面が出る
    /// </summary>
    internal sealed class CyberCleanFoam
    {
        private const float Width = 1920f;
        private const float Height = 1080f;
        private const float PopFxSeconds = 0.24f;
        private const float WaitMargin = 0.1f;
        private const float RiseSpeed = 22f;

        private enum Step { Idle, Waiting, Covering, Holding, Popping }

        private sealed class Bubble
        {
            public Image Image;
            public Vector2 Home;
            public float X01, Jitter, Size, Phase;
            public bool Glossy, Popped;
        }

        private readonly CyberCleanSettings _settings;
        private readonly RectTransform _root;
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        private Image _backdrop;
        private readonly List<(Image Image, float Age, float Size)> _pops = new List<(Image, float, float)>();
        private readonly Stack<Image> _popPool = new Stack<Image>();
        private readonly System.Random _random = new System.Random();
        private Step _step = Step.Idle;
        private float _time;
        private float _held;
        private bool _breakArrived;
        private float _clock;

        public bool IsBusy => _step != Step.Idle;

        public CyberCleanFoam(CyberCleanSettings settings, RectTransform root)
        {
            _settings = settings;
            _root = root;
            Build();
            Hide();
        }

        /// <summary>WAVE クリア: 歯みがきタイムが始まる前に覆い終わるよう、出し始めるまで待つ。</summary>
        public void OnWaveCleared()
        {
            var plan = FortressWaves.Plan;
            var banner = plan != null ? plan.clearBannerSeconds : 1.6f;
            _step = Step.Waiting;
            _time = Mathf.Max(0f, banner - _settings.foamCoverSeconds - WaitMargin);
            _breakArrived = false;
        }

        /// <summary>歯みがきタイムが始まった。覆っていなければすぐ覆い始め、覆い終わっていれば少し待ってはじける。</summary>
        public void OnBreakStarted()
        {
            _breakArrived = true;
            if (_step == Step.Waiting)
            {
                BeginCover();
            }
        }

        /// <summary>試す用: 覆って、少し待って、はじける。</summary>
        public void Test()
        {
            BeginCover();
            _breakArrived = true;
        }

        public void Update(float dt)
        {
            _clock += dt;
            switch (_step)
            {
                case Step.Waiting:
                    _time -= dt;
                    if (_time <= 0f)
                    {
                        BeginCover();
                    }

                    break;
                case Step.Covering:
                    _time += dt / _settings.foamCoverSeconds;
                    ShowCover(Mathf.Min(1f, _time));
                    if (_time >= 1f)
                    {
                        _step = Step.Holding;
                        _held = 0f;
                        _time = 0f;
                    }

                    break;
                case Step.Holding:
                    _held += dt;
                    ShowCover(1f);
                    if (_breakArrived)
                    {
                        _time += dt;
                    }

                    if ((_breakArrived && _time >= _settings.foamHoldSeconds) || _held >= _settings.foamMaxHoldSeconds)
                    {
                        BeginPop();
                    }

                    break;
                case Step.Popping:
                    _time += dt / _settings.foamPopSeconds;
                    ShowPop(_time);
                    if (_time >= 1f + PopFxSeconds / _settings.foamPopSeconds)
                    {
                        _step = Step.Idle;
                        Hide();
                    }

                    break;
            }

            StepPops(dt);
        }

        private void BeginCover()
        {
            _step = Step.Covering;
            _time = 0f;
            _held = 0f;
            foreach (var bubble in _bubbles)
            {
                bubble.Popped = false;
                bubble.Image.rectTransform.anchoredPosition = bubble.Home;
            }

            FortressEffectPlayer.PlaySound(_settings.onFoam, Vector3.zero);
        }

        private void BeginPop()
        {
            _step = Step.Popping;
            _time = 0f;
            FortressEffectPlayer.PlaySound(_settings.onFoamPop, Vector3.zero);
        }

        private void ShowCover(float t)
        {
            // 泡のすき間からゲームの画面がのぞかないよう、覆い終わる前に下地を出す。
            SetBackdrop(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.95f, t)));
            var rise = _step == Step.Holding ? _held * RiseSpeed : 0f;
            foreach (var bubble in _bubbles)
            {
                var scale = CyberCleanMath.CoverScale(bubble.X01, bubble.Jitter, bubble.Glossy, t);
                Place(bubble, scale, 1f, rise);
            }
        }

        private void ShowPop(float t)
        {
            SetBackdrop(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.55f, t)));
            var rise = _held * RiseSpeed;
            foreach (var bubble in _bubbles)
            {
                var start = CyberCleanMath.PopStart(bubble.X01, bubble.Jitter, bubble.Glossy);
                var u = (t - start) / CyberCleanMath.BubblePop;
                if (u < 0f)
                {
                    Place(bubble, 1f, 1f, rise);
                    continue;
                }

                if (!bubble.Popped)
                {
                    bubble.Popped = true;
                    SpawnPop(bubble);
                }

                if (u >= 1f)
                {
                    bubble.Image.enabled = false;
                    continue;
                }

                Place(bubble, CyberCleanMath.PopScale(u), CyberCleanMath.PopAlpha(u), rise);
            }
        }

        private void Place(Bubble bubble, float scale, float alpha, float rise)
        {
            var visible = scale > 0.001f && alpha > 0.001f;
            bubble.Image.enabled = visible;
            if (!visible)
            {
                return;
            }

            var wobble = CyberCleanMath.Wobble(_clock, bubble.Phase, bubble.Glossy ? 0.035f : 0.05f);
            var rect = bubble.Image.rectTransform;
            rect.sizeDelta = new Vector2(wobble.x, wobble.y) * (bubble.Size * scale);
            rect.anchoredPosition = bubble.Home + new Vector2(0f, rise * (bubble.Glossy ? 1.8f : 1f));
            var color = bubble.Image.color;
            color.a = alpha;
            bubble.Image.color = color;
        }

        private void SpawnPop(Bubble bubble)
        {
            var image = _popPool.Count > 0 ? _popPool.Pop() : CyberCleanScreenFx.Picture(_root, "FoamPop", CyberCleanArt.PopBurst, new Vector2(0.5f, 0.5f));
            image.transform.SetAsLastSibling();
            image.rectTransform.anchoredPosition = bubble.Image.rectTransform.anchoredPosition;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, bubble.Phase * 57f);
            var tint = bubble.Image.color;
            image.color = new Color(Mathf.Lerp(tint.r, 0.8f, 0.3f), Mathf.Lerp(tint.g, 0.95f, 0.3f), 1f, 1f);
            image.enabled = true;
            _pops.Add((image, 0f, bubble.Size));
        }

        private void StepPops(float dt)
        {
            for (var i = _pops.Count - 1; i >= 0; i--)
            {
                var (image, age, size) = _pops[i];
                age += dt;
                var u = age / PopFxSeconds;
                if (u >= 1f)
                {
                    image.enabled = false;
                    _popPool.Push(image);
                    _pops.RemoveAt(i);
                    continue;
                }

                image.rectTransform.sizeDelta = Vector2.one * (size * Mathf.Lerp(0.85f, 1.45f, CyberCleanMath.EaseOut(u)));
                var color = image.color;
                color.a = 1f - u;
                image.color = color;
                _pops[i] = (image, age, size);
            }
        }

        private void SetBackdrop(float alpha)
        {
            _backdrop.enabled = alpha > 0.001f;
            var color = _backdrop.color;
            color.a = alpha;
            _backdrop.color = color;
        }

        private void Hide()
        {
            SetBackdrop(0f);
            foreach (var bubble in _bubbles)
            {
                bubble.Image.enabled = false;
            }
        }

        // 画面を埋める小さな泡を格子のまわりに散らし、その上に大きな泡を浮かべる。並び順はばらばらにして、泡が積み重なって見えるようにする。
        private void Build()
        {
            var s = _settings;
            // 泡の下に敷く下地（画面いっぱいの、白に近いラベンダー）。
            _backdrop = CyberCleanScreenFx.Picture(_root, "FoamBackdrop", CyberCleanArt.Soft, new Vector2(0.5f, 0.5f));
            _backdrop.sprite = null;
            _backdrop.rectTransform.anchorMin = Vector2.zero;
            _backdrop.rectTransform.anchorMax = Vector2.one;
            _backdrop.rectTransform.offsetMin = _backdrop.rectTransform.offsetMax = Vector2.zero;
            _backdrop.color = new Color(0.93f, 0.92f, 1f, 0f);
            _backdrop.transform.SetAsFirstSibling();
            var colors = s.foamColors != null && s.foamColors.Length > 0 ? s.foamColors : new[] { Color.white };
            var columns = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(s.foamBubbleCount * Width / Height)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(s.foamBubbleCount / (float)columns));
            var cell = Mathf.Max(Width / columns, Height / rows);
            var mass = new List<Bubble>();
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var x01 = (col + 0.5f + Range(-0.45f, 0.45f)) / columns;
                    var y01 = (row + 0.5f + Range(-0.45f, 0.45f)) / rows;
                    mass.Add(NewBubble(CyberCleanArt.FoamBall, x01, y01, cell * Range(1.4f, 2.1f), false, colors));
                }
            }

            // 並び順をまぜる（後に作った物ほど上に描かれる）。
            for (var i = mass.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (mass[i], mass[j]) = (mass[j], mass[i]);
            }

            foreach (var bubble in mass)
            {
                bubble.Image.transform.SetAsLastSibling();
                _bubbles.Add(bubble);
            }

            // すき間を埋める細かい泡（泡の塊のきめを出す）。
            var micro = Mathf.RoundToInt(s.foamBubbleCount * 0.8f);
            for (var i = 0; i < micro; i++)
            {
                var bubble = NewBubble(CyberCleanArt.FoamBall, Range(0f, 1f), Range(0f, 1f), cell * Range(0.32f, 0.62f), false, colors);
                bubble.Image.transform.SetAsLastSibling();
                _bubbles.Add(bubble);
            }

            for (var i = 0; i < s.glossyBubbleCount; i++)
            {
                var bubble = NewBubble(CyberCleanArt.Glossy, Range(0.04f, 0.96f), Range(0.06f, 0.94f), Range(170f, 360f), true, new[] { Color.white });
                bubble.Image.transform.SetAsLastSibling();
                _bubbles.Add(bubble);
            }
        }

        private Bubble NewBubble(Texture2D texture, float x01, float y01, float size, bool glossy, Color[] colors)
        {
            var image = CyberCleanScreenFx.Picture(_root, glossy ? "GlossyBubble" : "FoamBubble", texture, new Vector2(0.5f, 0.5f));
            var home = new Vector2((x01 - 0.5f) * Width, (y01 - 0.5f) * Height);
            image.rectTransform.anchoredPosition = home;
            image.color = colors[_random.Next(colors.Length)];
            return new Bubble
            {
                Image = image,
                Home = home,
                X01 = Mathf.Clamp01(x01),
                Jitter = (float)_random.NextDouble(),
                Size = size,
                Phase = Range(0f, 6.28f),
                Glossy = glossy
            };
        }

        private float Range(float a, float b) => a + (float)_random.NextDouble() * (b - a);
    }
}
