using System;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の画面（全員の PC に出る）。画面を4つに分け（キャラ選択と同じ並び: 1P 左上・2P 左下・3P 右上・4P 右下）、
    /// 席ごとに砲台・ビーム・熱の棒・3つの課題を出す。中身は <see cref="PracticeBoard"/> を見て描くだけ（動かすのはセッションの側）。
    /// 音は自分の席の分だけ鳴らす。歯磨き粉の幕（950）より下に描き、ゲームが読み込まれる（keepVisible が false になる）まで出したまま。
    /// </summary>
    public sealed class PracticeView : MonoBehaviour
    {
        private const float FadeSeconds = 0.3f;
        private const float StampSeconds = 0.25f;
        private const float BeamLength = 360f;
        private const float HeatBarWidth = 470f;
        private const int StepCount = 3;

        private static readonly Vector2 PanelSize = new Vector2(930f, 420f);

        // 1P 左上・2P 左下・3P 右上・4P 右下（キャラ選択と同じ）。
        private static readonly Vector2[] PanelCenters =
        {
            new Vector2(-475f, 205f), new Vector2(-475f, -235f), new Vector2(475f, 205f), new Vector2(475f, -235f)
        };

        private static PracticeView _current;

        private readonly Panel[] _panels = new Panel[PracticeBoard.SeatCount];

        private HowToPlaySettings _settings;
        private PracticeBoard _board;
        private int _localSeat;
        private Func<bool> _keepVisible;
        private float _time;
        private float _fade;
        private float _finishedAt = -1f;

        private CanvasGroup _group;
        private Text _title;
        private Text _footer;
        private Text _timer;

        private sealed class Panel
        {
            public CanvasGroup Group;
            public Text Absent;
            public Text LocalMark;
            public RectTransform Turret;
            public RawImage Beam;
            public Image BeamGlow;
            public Text Overheat;
            public Image HeatFill;
            public readonly Image[] Boxes = new Image[StepCount];
            public readonly Text[] Checks = new Text[StepCount];
            public readonly Text[] Labels = new Text[StepCount];
            public Text Arrow;
            public RectTransform Stamp;
            public float Grip;
            public float Heat;
            public PracticeStep LastStep;
            public bool LastOverheated;
            public float StepPopAt = -1f;
            public int StepPopIndex = -1;
            public float StampAt = -1f;
        }

        /// <summary>練習の画面を出しているか。</summary>
        public static bool IsShowing => _current != null;

        /// <summary>練習の画面を出す。keepVisible が false になったら消える（null ならずっと。<see cref="Hide"/> で消す）。</summary>
        public static PracticeView Play(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible)
        {
            Hide(true);
            if (settings == null || board == null)
            {
                return null;
            }

            var go = new GameObject("[HowToPlay] Practice");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<PracticeView>();
            view.Begin(settings, board, localSeat, keepVisible);
            _current = view;
            return view;
        }

        /// <summary>出している練習の画面を消す（immediate なら一瞬で、そうでなければふわっと）。</summary>
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
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Begin(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible)
        {
            _settings = settings;
            _board = board;
            _localSeat = localSeat;
            _keepVisible = keepVisible;
            BuildUi();
            for (var i = 0; i < _panels.Length; i++)
            {
                _panels[i].LastStep = board.seats[i].step;
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _time += dt;

            var keep = _keepVisible == null || _keepVisible();
            _fade = keep ? Mathf.Min(1f, _fade + dt / FadeSeconds) : _fade - dt / FadeSeconds;
            if (!keep && _fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _group.alpha = Mathf.Clamp01(_fade);
            for (var i = 0; i < _panels.Length; i++)
            {
                TickPanel(i, dt);
            }

            TickTexts();
        }

        // ───────── 動き ─────────

        private void TickPanel(int seat, float dt)
        {
            var panel = _panels[seat];
            var data = _board.seats[seat];
            var isLocal = seat == _localSeat;

            panel.Group.alpha = data.present ? 1f : 0.3f;
            panel.Absent.enabled = !data.present;
            panel.LocalMark.enabled = isLocal && data.present;

            // ほかの人の値は 1 秒に 15 回ほどしか届かないので、なめらかに追いかける。
            var grip = data.gripping ? data.Grip01 : 0f;
            panel.Grip = isLocal ? grip : Mathf.MoveTowards(panel.Grip, grip, dt * 6f);
            panel.Heat = isLocal ? data.Heat01 : Mathf.MoveTowards(panel.Heat, data.Heat01, dt * 3f);

            var firing = data.present && data.gripping && !data.overheated && panel.Grip > 0.02f;
            panel.Beam.enabled = firing;
            panel.BeamGlow.enabled = firing;
            if (firing)
            {
                var wobble = 1f + 0.06f * Mathf.Sin(_time * 22f + seat);
                var height = (12f + 84f * panel.Grip) * wobble;
                panel.Beam.rectTransform.sizeDelta = new Vector2(BeamLength, height);
                panel.BeamGlow.rectTransform.sizeDelta = new Vector2(BeamLength + 60f, height * 2.2f + 30f);
            }

            panel.Turret.localScale = Vector3.one * (1f + 0.08f * (firing ? panel.Grip : 0f));

            panel.Overheat.enabled = data.overheated && Mathf.Repeat(_time * 3f, 1f) < 0.65f;
            panel.HeatFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(28f, HeatBarWidth * panel.Heat), 28f);
            panel.HeatFill.enabled = panel.Heat > 0.005f;
            panel.HeatFill.color = HowToPlayArt.HeatColor(panel.Heat);

            // 課題の進み。
            if (data.step > panel.LastStep)
            {
                panel.StepPopIndex = (int)data.step - 1;
                panel.StepPopAt = _time;
                if (data.step == PracticeStep.Done)
                {
                    panel.StampAt = _time;
                }

                if (isLocal && !_board.finished)
                {
                    FortressEffectPlayer.PlaySound(data.step == PracticeStep.Done ? _settings.onLocalDone : _settings.onStepDone, Vector3.zero);
                }

                panel.LastStep = data.step;
            }

            if (isLocal && data.overheated && !panel.LastOverheated)
            {
                FortressEffectPlayer.PlaySound(_settings.onOverheat, Vector3.zero);
            }

            panel.LastOverheated = data.overheated;

            var color = FortressColors.SeatColor(seat);
            for (var k = 0; k < StepCount; k++)
            {
                var done = (int)data.step > k;
                var now = (int)data.step == k;
                panel.Boxes[k].color = done ? color : new Color(1f, 1f, 1f, 0.15f);
                panel.Checks[k].enabled = done;
                panel.Labels[k].color = done ? new Color(1f, 1f, 1f, 0.55f) : now ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                var pop = k == panel.StepPopIndex && panel.StepPopAt >= 0f ? Mathf.Clamp01((_time - panel.StepPopAt) / 0.3f) : 1f;
                panel.Boxes[k].rectTransform.localScale = Vector3.one * (1f + 0.5f * (1f - pop));
            }

            var current = (int)data.step;
            panel.Arrow.enabled = data.present && current < StepCount;
            if (panel.Arrow.enabled)
            {
                panel.Arrow.rectTransform.anchoredPosition = new Vector2(125f + 8f * Mathf.Sin(_time * 6f), StepRowY(current));
            }

            panel.Stamp.gameObject.SetActive(data.present && data.IsDone);
            if (panel.Stamp.gameObject.activeSelf)
            {
                var k = panel.StampAt >= 0f ? Mathf.Clamp01((_time - panel.StampAt) / StampSeconds) : 1f;
                panel.Stamp.localScale = Vector3.one * (1f + 1.6f * (1f - HowToPlayArt.EaseOutBack(k)));
            }
        }

        private void TickTexts()
        {
            if (_board.finished && _finishedAt < 0f)
            {
                _finishedAt = _time;
                FortressEffectPlayer.PlaySound(_settings.onFinished, Vector3.zero);
            }

            if (_finishedAt >= 0f)
            {
                _title.text = _settings.finishedText;
                var k = Mathf.Clamp01((_time - _finishedAt) / 0.3f);
                _title.rectTransform.localScale = Vector3.one * (1f + 0.8f * (1f - HowToPlayArt.EaseOutBack(k)));
            }

            var present = _board.PresentCount;
            _footer.text = present > 0 ? $"{_settings.footer}　（OK {_board.DoneCount} / {present}人）" : _settings.footer;
            _timer.enabled = !_board.finished;
            _timer.text = $"あと {Mathf.CeilToInt(_board.remaining)}";
        }

        private static float StepRowY(int index) => 95f - index * 90f;

        // ───────── 作る ─────────

        private void BuildUi()
        {
            _group = HowToPlayArt.MakeCanvas(gameObject, _settings.sortingOrder);
            var root = (RectTransform)transform;

            var backdrop = HowToPlayArt.MakeImage("Backdrop", root, null, new Color(0.02f, 0.04f, 0.1f, 0.92f), Vector2.zero, Vector2.zero);
            HowToPlayArt.Stretch(backdrop.rectTransform);

            _title = HowToPlayArt.MakeText("Title", root, _settings.title, 76, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 485f), new Vector2(1600f, 110f));
            HowToPlayArt.AddOutline(_title, HowToPlayArt.Ink, 4f);

            for (var i = 0; i < _panels.Length; i++)
            {
                _panels[i] = MakePanel(root, i);
            }

            var bar = HowToPlayArt.MakeImage("BottomBar", root, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -497f), new Vector2(1920f, 86f));
            _footer = HowToPlayArt.MakeText("Footer", bar.rectTransform, _settings.footer, 38, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, new Vector2(-20f, 0f), new Vector2(1800f, 80f));
            _timer = HowToPlayArt.MakeText("Timer", bar.rectTransform, string.Empty, 38, new Color(0.82f, 0.92f, 1f), TextAnchor.MiddleRight, new Vector2(20f, 0f), new Vector2(1800f, 80f));
        }

        private Panel MakePanel(RectTransform root, int seat)
        {
            var color = FortressColors.SeatColor(seat);
            var panel = new Panel();
            var rect = HowToPlayArt.MakeRect($"Panel {seat + 1}P", root, PanelCenters[seat], PanelSize);
            panel.Group = rect.gameObject.AddComponent<CanvasGroup>();

            HowToPlayArt.MakeImage("Back", rect, HowToPlayArt.Rounded, HowToPlayArt.Navy, Vector2.zero, PanelSize);
            HowToPlayArt.MakeImage("Tint", rect, HowToPlayArt.Rounded, new Color(color.r, color.g, color.b, 0.16f), Vector2.zero, PanelSize);
            HowToPlayArt.MakeImage("Frame", rect, HowToPlayArt.Outline, color, Vector2.zero, PanelSize);

            var labelBack = HowToPlayArt.MakeImage("LabelBack", rect, HowToPlayArt.Rounded, color, new Vector2(-395f, 160f), new Vector2(100f, 56f));
            HowToPlayArt.MakeText("Label", labelBack.rectTransform, $"{seat + 1}P", 36, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(100f, 56f));
            panel.LocalMark = HowToPlayArt.MakeText("LocalMark", rect, _settings.localMark, 32, Color.white, TextAnchor.MiddleLeft, new Vector2(-210f, 160f), new Vector2(260f, 56f));

            // 砲台とビーム（ビームは砲台の右へ、握る強さで太くなる）。
            const float TurretX = -320f;
            const float TurretY = 10f;
            panel.BeamGlow = HowToPlayArt.MakeImage("BeamGlow", rect, HowToPlayArt.Soft, new Color(color.r, color.g, color.b, 0.55f), new Vector2(TurretX + 20f, TurretY), new Vector2(BeamLength, 40f));
            HowToPlayArt.PivotLeft(panel.BeamGlow.rectTransform);

            panel.Beam = new GameObject("Beam", typeof(RectTransform)).AddComponent<RawImage>();
            panel.Beam.transform.SetParent(rect, false);
            panel.Beam.texture = _settings.beamTexture;
            panel.Beam.color = _settings.beamTexture != null ? Color.white : Color.Lerp(color, Color.white, 0.6f);
            panel.Beam.raycastTarget = false;
            HowToPlayArt.PivotLeft(panel.Beam.rectTransform);
            panel.Beam.rectTransform.anchoredPosition = new Vector2(TurretX + 50f, TurretY);

            panel.Turret = HowToPlayArt.MakeRect("Turret", rect, new Vector2(TurretX, TurretY), new Vector2(130f, 130f));
            var barrel = HowToPlayArt.MakeImage("Barrel", panel.Turret, HowToPlayArt.Rounded, new Color(0.92f, 0.96f, 1f), new Vector2(20f, 0f), new Vector2(96f, 40f));
            HowToPlayArt.PivotLeft(barrel.rectTransform);
            HowToPlayArt.MakeImage("Ring", panel.Turret, HowToPlayArt.Circle, HowToPlayArt.Ink, Vector2.zero, new Vector2(124f, 124f));
            HowToPlayArt.MakeImage("Body", panel.Turret, HowToPlayArt.Circle, color, Vector2.zero, new Vector2(110f, 110f));

            panel.Overheat = HowToPlayArt.MakeText("Overheat", rect, _settings.overheatText, 50, new Color(1f, 0.3f, 0.3f), TextAnchor.MiddleCenter, new Vector2(-90f, TurretY), new Vector2(520f, 80f));
            HowToPlayArt.AddOutline(panel.Overheat, HowToPlayArt.Ink, 4f);

            // 熱の棒。
            HowToPlayArt.MakeText("HeatLabel", rect, _settings.heatLabel, 32, new Color(1f, 0.85f, 0.65f), TextAnchor.MiddleCenter, new Vector2(-415f, -150f), new Vector2(60f, 50f));
            var heatBack = HowToPlayArt.MakeImage("HeatBack", rect, HowToPlayArt.Rounded, new Color(1f, 1f, 1f, 0.14f), new Vector2(-380f, -150f), new Vector2(HeatBarWidth, 28f));
            HowToPlayArt.PivotLeft(heatBack.rectTransform);
            panel.HeatFill = HowToPlayArt.MakeImage("HeatFill", rect, HowToPlayArt.Rounded, HowToPlayArt.HeatColor(0f), new Vector2(-380f, -150f), new Vector2(28f, 28f));
            HowToPlayArt.PivotLeft(panel.HeatFill.rectTransform);

            // 3つの課題。
            for (var k = 0; k < StepCount; k++)
            {
                var y = StepRowY(k);
                panel.Boxes[k] = HowToPlayArt.MakeImage($"Box{k + 1}", rect, HowToPlayArt.Rounded, new Color(1f, 1f, 1f, 0.15f), new Vector2(175f, y), new Vector2(48f, 48f));
                panel.Checks[k] = HowToPlayArt.MakeText($"Check{k + 1}", panel.Boxes[k].rectTransform, "✓", 38, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(48f, 48f));
                panel.Labels[k] = HowToPlayArt.MakeText($"Step{k + 1}", rect, _settings.StepLabel(k), 40, Color.white, TextAnchor.MiddleLeft, new Vector2(360f, y), new Vector2(300f, 56f));
            }

            panel.Arrow = HowToPlayArt.MakeText("Arrow", rect, "▶", 34, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleCenter, new Vector2(125f, StepRowY(0)), new Vector2(50f, 50f));

            // 全部できたら「OK!」の判子。
            panel.Stamp = HowToPlayArt.MakeRect("Stamp", rect, new Vector2(0f, 10f), new Vector2(380f, 140f));
            panel.Stamp.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var stampBack = HowToPlayArt.MakeImage("Back", panel.Stamp, HowToPlayArt.Rounded, color, Vector2.zero, new Vector2(380f, 140f));
            HowToPlayArt.AddOutline(stampBack, Color.white, 5f);
            var stampText = HowToPlayArt.MakeText("Text", panel.Stamp, _settings.doneText, 92, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(380f, 140f));
            HowToPlayArt.AddOutline(stampText, HowToPlayArt.Ink, 4f);
            panel.Stamp.gameObject.SetActive(false);

            panel.Absent = HowToPlayArt.MakeText("Absent", rect, _settings.absentText, 44, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(800f, 80f));
            return panel;
        }
    }
}
