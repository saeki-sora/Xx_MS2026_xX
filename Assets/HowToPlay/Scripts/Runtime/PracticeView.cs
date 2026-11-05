using System;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の画面（全員の PC に出る）。自分の席を左に大きく（砲台・ビーム・3つの課題の名前）、
    /// ほかの3人を右に小さく縦に並べる（砲台・ビーム・課題のチェックだけ。2026-10-29、ユーザー依頼「自分だけ大きく」）。
    /// 中身は <see cref="PracticeBoard"/> を見て描くだけ（動かすのはセッションの側）。音は自分の席の分だけ鳴らす。
    /// 後ろは不透明（キャラ選択の画面は見せない）。歯磨き粉の幕（950）より下に描き、ゲームが読み込まれる（keepVisible が false になる）まで出したまま。
    /// </summary>
    public sealed class PracticeView : MonoBehaviour
    {
        private const float FadeSeconds = 0.3f;
        private const float StampSeconds = 0.25f;
        private const int StepCount = 3;

        private static readonly Vector2 BigSize = new Vector2(1240f, 860f);
        private static readonly Vector2 BigCenter = new Vector2(-320f, -10f);
        private static readonly Vector2 SmallSize = new Vector2(620f, 273f);

        // 右の列の上・中・下（ほかの人を席の番号の順に入れる）。
        private static readonly Vector2[] SmallCenters = { new Vector2(630f, 283f), new Vector2(630f, -10f), new Vector2(630f, -303f) };

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
        private RawImage _stripes;
        private Text _title;
        private Text _footer;
        private Text _timer;

        private sealed class Panel
        {
            public bool Big;
            public float BeamLength;
            public float BeamMin;
            public float BeamMax;
            public CanvasGroup Group;
            public Text Absent;
            public RectTransform Turret;
            public RawImage Beam;
            public Image BeamGlow;
            public Text Overheat;
            public readonly Image[] Boxes = new Image[StepCount];
            public readonly Text[] Checks = new Text[StepCount];
            public readonly Text[] Numbers = new Text[StepCount];
            public readonly Text[] Labels = new Text[StepCount];
            public Text Arrow;
            public RectTransform Stamp;
            public float Grip;
            public PracticeStep LastStep;
            public bool LastOverheated;
            public float StepPopAt = -1f;
            public int StepPopIndex = -1;
            public float StampAt = -1f;
        }

        /// <summary>練習の画面を出しているか。</summary>
        public static bool IsShowing => _current != null;

        /// <summary>
        /// 練習の画面を出す。keepVisible が false になったら消える（null ならずっと。<see cref="Hide"/> で消す）。
        /// fadeIn が false なら最初から見えている（前の画面から、間にキャラ選択を見せずに切り替えるとき）。
        /// </summary>
        public static PracticeView Play(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible, bool fadeIn = true)
        {
            Hide(true);
            if (settings == null || board == null)
            {
                return null;
            }

            var go = new GameObject("[HowToPlay] Practice");
            DontDestroyOnLoad(go);
            var view = go.AddComponent<PracticeView>();
            view.Begin(settings, board, localSeat, keepVisible, fadeIn);
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

        private void Begin(HowToPlaySettings settings, PracticeBoard board, int localSeat, Func<bool> keepVisible, bool fadeIn)
        {
            _settings = settings;
            _board = board;
            _localSeat = localSeat;
            _keepVisible = keepVisible;
            _fade = fadeIn ? 0f : 1f;
            BuildUi();
            _group.alpha = _fade;
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
            var rect = _stripes.uvRect;
            rect.x = Mathf.Repeat(rect.x + 0.05f * dt, 1f);
            _stripes.uvRect = rect;

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

            // ほかの人の値は 1 秒に 15 回ほどしか届かないので、なめらかに追いかける。
            var grip = data.gripping ? data.Grip01 : 0f;
            panel.Grip = isLocal ? grip : Mathf.MoveTowards(panel.Grip, grip, dt * 6f);

            var firing = data.present && data.gripping && !data.overheated && panel.Grip > 0.02f;
            panel.Beam.enabled = firing;
            panel.BeamGlow.enabled = firing;
            if (firing)
            {
                var wobble = 1f + 0.06f * Mathf.Sin(_time * 22f + seat);
                var height = Mathf.Lerp(panel.BeamMin, panel.BeamMax, panel.Grip) * wobble;
                panel.Beam.rectTransform.sizeDelta = new Vector2(panel.BeamLength, height);
                panel.BeamGlow.rectTransform.sizeDelta = new Vector2(panel.BeamLength * 1.1f, height * 2.2f + panel.BeamMin * 2f);
            }

            panel.Turret.localScale = Vector3.one * (1f + 0.08f * (firing ? panel.Grip : 0f));
            panel.Overheat.enabled = data.overheated && Mathf.Repeat(_time * 3f, 1f) < 0.65f;

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
                panel.Boxes[k].color = done ? color : new Color(1f, 1f, 1f, now ? 0.35f : 0.12f);
                panel.Checks[k].enabled = done;
                if (panel.Numbers[k] != null)
                {
                    panel.Numbers[k].enabled = !done;
                }

                if (panel.Labels[k] != null)
                {
                    panel.Labels[k].color = done ? new Color(1f, 1f, 1f, 0.55f) : now ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }

                var pop = k == panel.StepPopIndex && panel.StepPopAt >= 0f ? Mathf.Clamp01((_time - panel.StepPopAt) / 0.3f) : 1f;
                panel.Boxes[k].rectTransform.localScale = Vector3.one * (1f + 0.5f * (1f - pop));
            }

            // 今の課題の上で ▼ が弾む（大きい区画だけ）。
            var current = (int)data.step;
            if (panel.Arrow != null)
            {
                panel.Arrow.enabled = data.present && current < StepCount;
                if (panel.Arrow.enabled)
                {
                    var box = panel.Boxes[current].rectTransform.anchoredPosition;
                    panel.Arrow.rectTransform.anchoredPosition = new Vector2(box.x, box.y + 78f + 10f * Mathf.Abs(Mathf.Sin(_time * 5f)));
                }
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

        // ───────── 作る ─────────

        private void BuildUi()
        {
            _group = HowToPlayArt.MakeCanvas(gameObject, _settings.sortingOrder);
            var root = (RectTransform)transform;

            // 後ろは不透明な紺に、斜めのしま（ステージ抽選のルーレットとそろえる）。
            var backdrop = HowToPlayArt.MakeImage("Backdrop", root, null, HowToPlayArt.Backdrop, Vector2.zero, Vector2.zero);
            HowToPlayArt.Stretch(backdrop.rectTransform);
            _stripes = new GameObject("Stripes", typeof(RectTransform)).AddComponent<RawImage>();
            _stripes.transform.SetParent(root, false);
            _stripes.texture = HowToPlayArt.Stripes;
            _stripes.color = new Color(0.35f, 0.95f, 1f, 0.08f);
            _stripes.uvRect = new Rect(0f, 0f, 6f, 3.4f);
            _stripes.raycastTarget = false;
            HowToPlayArt.Stretch(_stripes.rectTransform);

            _title = HowToPlayArt.MakeText("Title", root, _settings.title, 76, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 485f), new Vector2(1600f, 110f));
            HowToPlayArt.AddOutline(_title, HowToPlayArt.Ink, 4f);

            var row = 0;
            for (var i = 0; i < _panels.Length; i++)
            {
                if (i == _localSeat)
                {
                    _panels[i] = MakePanel(root, i, true, BigCenter);
                }
                else
                {
                    _panels[i] = MakePanel(root, i, false, SmallCenters[Mathf.Min(row, SmallCenters.Length - 1)]);
                    row++;
                }
            }

            var bar = HowToPlayArt.MakeImage("BottomBar", root, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -497f), new Vector2(1920f, 86f));
            _footer = HowToPlayArt.MakeText("Footer", bar.rectTransform, _settings.footer, 38, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, new Vector2(-20f, 0f), new Vector2(1800f, 80f));
            _timer = HowToPlayArt.MakeText("Timer", bar.rectTransform, string.Empty, 38, new Color(0.82f, 0.92f, 1f), TextAnchor.MiddleRight, new Vector2(20f, 0f), new Vector2(1800f, 80f));
        }

        private Panel MakePanel(RectTransform root, int seat, bool big, Vector2 center)
        {
            var color = FortressColors.SeatColor(seat);
            var size = big ? BigSize : SmallSize;
            var half = size * 0.5f;
            var panel = new Panel { Big = big };
            var rect = HowToPlayArt.MakeRect($"Panel {seat + 1}P", root, center, size);
            panel.Group = rect.gameObject.AddComponent<CanvasGroup>();

            HowToPlayArt.MakeImage("Back", rect, HowToPlayArt.Rounded, HowToPlayArt.Navy, Vector2.zero, size);
            HowToPlayArt.MakeImage("Tint", rect, HowToPlayArt.Rounded, new Color(color.r, color.g, color.b, 0.16f), Vector2.zero, size);
            HowToPlayArt.MakeImage("Frame", rect, HowToPlayArt.Outline, color, Vector2.zero, size);

            // 「1P」の札（自分の区画には「あなた」も）。
            var labelSize = big ? new Vector2(130f, 72f) : new Vector2(90f, 50f);
            var labelPos = new Vector2(-half.x + 24f + labelSize.x * 0.5f, half.y - 24f - labelSize.y * 0.5f);
            var labelBack = HowToPlayArt.MakeImage("LabelBack", rect, HowToPlayArt.Rounded, color, labelPos, labelSize);
            HowToPlayArt.MakeText("Label", labelBack.rectTransform, $"{seat + 1}P", big ? 46 : 32, Color.white, TextAnchor.MiddleCenter, Vector2.zero, labelSize);
            if (big)
            {
                HowToPlayArt.MakeText("LocalMark", rect, _settings.localMark, 44, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(labelPos.x + labelSize.x * 0.5f + 20f + 150f, labelPos.y), new Vector2(300f, 72f));
            }

            // 砲台とビーム（ビームは砲台の右へ、握る強さで太くなる）。
            var turretPos = big ? new Vector2(-470f, 110f) : new Vector2(-220f, -40f);
            var turretSize = big ? 200f : 100f;
            panel.BeamLength = big ? 900f : 420f;
            panel.BeamMin = big ? 20f : 8f;
            panel.BeamMax = big ? 140f : 60f;
            var beamStart = turretPos + new Vector2(turretSize * 0.38f, 0f);

            panel.BeamGlow = HowToPlayArt.MakeImage("BeamGlow", rect, HowToPlayArt.Soft, new Color(color.r, color.g, color.b, 0.55f), beamStart - new Vector2(turretSize * 0.2f, 0f), new Vector2(panel.BeamLength, panel.BeamMin));
            HowToPlayArt.PivotLeft(panel.BeamGlow.rectTransform);

            panel.Beam = new GameObject("Beam", typeof(RectTransform)).AddComponent<RawImage>();
            panel.Beam.transform.SetParent(rect, false);
            panel.Beam.texture = _settings.beamTexture;
            panel.Beam.color = _settings.beamTexture != null ? Color.white : Color.Lerp(color, Color.white, 0.6f);
            panel.Beam.raycastTarget = false;
            HowToPlayArt.PivotLeft(panel.Beam.rectTransform);
            panel.Beam.rectTransform.anchoredPosition = beamStart;

            panel.Turret = HowToPlayArt.MakeRect("Turret", rect, turretPos, new Vector2(turretSize, turretSize));
            var barrel = HowToPlayArt.MakeImage("Barrel", panel.Turret, HowToPlayArt.Rounded, new Color(0.92f, 0.96f, 1f), new Vector2(turretSize * 0.15f, 0f), new Vector2(turretSize * 0.75f, turretSize * 0.3f));
            HowToPlayArt.PivotLeft(barrel.rectTransform);
            HowToPlayArt.MakeImage("Ring", panel.Turret, HowToPlayArt.Circle, HowToPlayArt.Ink, Vector2.zero, Vector2.one * (turretSize * 0.95f));
            HowToPlayArt.MakeImage("Body", panel.Turret, HowToPlayArt.Circle, color, Vector2.zero, Vector2.one * (turretSize * 0.85f));

            panel.Overheat = HowToPlayArt.MakeText("Overheat", rect, _settings.overheatText, big ? 72 : 40, new Color(1f, 0.3f, 0.3f), TextAnchor.MiddleCenter,
                new Vector2(big ? 60f : 40f, turretPos.y), new Vector2(big ? 800f : 420f, big ? 110f : 60f));
            HowToPlayArt.AddOutline(panel.Overheat, HowToPlayArt.Ink, big ? 5f : 3f);

            // 3つの課題。大きい区画: 下に横並びで名前つき。小さい区画: 右上に番号つきの四角だけ。
            for (var k = 0; k < StepCount; k++)
            {
                if (big)
                {
                    var itemX = -400f + 400f * k;
                    var boxPos = new Vector2(itemX - 150f, -270f);
                    panel.Boxes[k] = HowToPlayArt.MakeImage($"Box{k + 1}", rect, HowToPlayArt.Rounded, new Color(1f, 1f, 1f, 0.12f), boxPos, new Vector2(64f, 64f));
                    panel.Checks[k] = HowToPlayArt.MakeText($"Check{k + 1}", panel.Boxes[k].rectTransform, "✓", 52, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(64f, 64f));
                    panel.Labels[k] = HowToPlayArt.MakeText($"Step{k + 1}", rect, _settings.StepLabel(k), 48, Color.white, TextAnchor.MiddleLeft,
                        new Vector2(itemX - 105f + 160f, -270f), new Vector2(320f, 70f));
                }
                else
                {
                    var boxPos = new Vector2(half.x - 200f + 70f * k, half.y - 24f - 25f);
                    panel.Boxes[k] = HowToPlayArt.MakeImage($"Box{k + 1}", rect, HowToPlayArt.Rounded, new Color(1f, 1f, 1f, 0.12f), boxPos, new Vector2(50f, 50f));
                    panel.Checks[k] = HowToPlayArt.MakeText($"Check{k + 1}", panel.Boxes[k].rectTransform, "✓", 40, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(50f, 50f));
                    panel.Numbers[k] = HowToPlayArt.MakeText($"Number{k + 1}", panel.Boxes[k].rectTransform, (k + 1).ToString(), 30, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(50f, 50f));
                }
            }

            if (big)
            {
                panel.Arrow = HowToPlayArt.MakeText("Arrow", rect, "▼", 44, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(60f, 60f));
            }

            // 全部できたら「OK!」の判子。
            var stampSize = big ? new Vector2(520f, 190f) : new Vector2(300f, 110f);
            panel.Stamp = HowToPlayArt.MakeRect("Stamp", rect, new Vector2(0f, big ? 40f : -10f), stampSize);
            panel.Stamp.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var stampBack = HowToPlayArt.MakeImage("Back", panel.Stamp, HowToPlayArt.Rounded, color, Vector2.zero, stampSize);
            HowToPlayArt.AddOutline(stampBack, Color.white, big ? 6f : 4f);
            var stampText = HowToPlayArt.MakeText("Text", panel.Stamp, _settings.doneText, big ? 130 : 76, Color.white, TextAnchor.MiddleCenter, Vector2.zero, stampSize);
            HowToPlayArt.AddOutline(stampText, HowToPlayArt.Ink, big ? 5f : 3f);
            panel.Stamp.gameObject.SetActive(false);

            panel.Absent = HowToPlayArt.MakeText("Absent", rect, _settings.absentText, big ? 56 : 36, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(size.x - 40f, 80f));
            return panel;
        }
    }
}
