using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // 上の帯（今は出さない）、右上の研修の欄（手の絵つきのカード）、右下の準備中の欄（キャラのアイコン）。2026-11-19 ユーザーのレイアウト案に合わせた。
    public sealed partial class PracticeScreen
    {
        // ───────── 上の帯 ─────────

        private PracticeShape _tube;
        private readonly PracticeShape[] _chips = new PracticeShape[PracticeSteps.LessonCount];
        private readonly Text[] _chipSmall = new Text[PracticeSteps.LessonCount];
        private readonly Text[] _chipTitle = new Text[PracticeSteps.LessonCount];
        private readonly RawImage[] _chipStamp = new RawImage[PracticeSteps.LessonCount];
        private Image _timerRing;
        private Text _timerText;
        private const float TubeX = 330f;
        private const float TubeY = 64f;
        private const float TubeW = 1380f;

        private void BuildHeader()
        {
            if (_cfg.logo != null)
            {
                var aspect = _cfg.logo.width / (float)Mathf.Max(1, _cfg.logo.height);
                Raw("Logo", _root, _cfg.logo, P(144f, 62f), new Vector2(112f * aspect, 112f));
            }

            _tube = Shape("Progress Tube", _root, P(TubeX + (TubeW * 0.5f), TubeY), new Vector2(TubeW + 80f, 100f));
            for (var i = 0; i < PracticeSteps.LessonCount; i++)
            {
                var cx = ChipX(i);
                _chips[i] = Shape("Lesson " + (i + 1), _root, P(cx, TubeY), new Vector2(240f, 80f));
                _chipSmall[i] = Label("No", _chips[i].transform, $"研修{i + 1}", 16, Ink, TextAnchor.MiddleLeft, new Vector2(-14f, 11f), new Vector2(170f, 22f));
                _chipTitle[i] = Label("Title", _chips[i].transform, PracticeScreenSettings.Pick(_cfg.lessonTitles, i), 21, Ink, TextAnchor.MiddleLeft, new Vector2(-14f, -11f), new Vector2(170f, 26f));
                _chipStamp[i] = Raw("Stamp", _chips[i].transform, _cfg.stamp, new Vector2(80f, 0f), new Vector2(46f, 46f));
                _chipStamp[i].color = Color.white;
                _chipStamp[i].enabled = false;
            }

            var timer = Shape("Timer", _root, P(1858f, 62f), new Vector2(100f, 100f));
            timer.FillStroke(PracticeShape.Arc(Vector2.zero, 44f, 0f, 360f, 48), Color.white, Ink, 4f);
            _timerRing = HowToPlayArt.MakeImage("Ring", timer.transform, Ring, Mint, Vector2.zero, new Vector2(78f, 78f));
            _timerRing.type = Image.Type.Filled;
            _timerRing.fillMethod = Image.FillMethod.Radial360;
            _timerRing.fillOrigin = (int)Image.Origin360.Top;
            _timerRing.fillClockwise = true;
            _timerText = Label("Seconds", timer.transform, "60", 30, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80f, 40f));
        }

        private static float ChipX(int lesson) => TubeX + 176f + ((TubeW - 176f) * (lesson + 1) / 3f) - 120f;

        /// <summary>研修の進み（0〜3。研修の中の課題の割合も足す）。</summary>
        private float LocalProgress()
        {
            var step = LocalStep;
            var lesson = PracticeSteps.Lesson(step);
            if (lesson >= PracticeSteps.LessonCount)
            {
                return PracticeSteps.LessonCount;
            }

            var first = (int)PracticeSteps.FirstOf(lesson);
            var count = PracticeSteps.TaskCount(lesson);
            return lesson + (((int)step - first) / (float)Mathf.Max(1, count));
        }

        private void TickHeader()
        {
            var info = _seats[_local];
            var progress = LocalProgress();
            var lesson = PracticeSteps.Lesson(LocalStep);

            // 歯磨き粉のチューブ（左）と、出てきた線（研修の進み）。
            _tube.Clear();
            var x0 = -(TubeW * 0.5f);
            var tube = new List<Vector2> { new Vector2(x0, 30f), new Vector2(x0 + 150f, 16f), new Vector2(x0 + 166f, 10f), new Vector2(x0 + 166f, -10f), new Vector2(x0 + 150f, -16f), new Vector2(x0, -30f) };
            _tube.FillStroke(tube, Color.white, Ink, 4f);
            _tube.Fill(PracticeShape.Parallelogram(new Vector2(x0 + 75f, 10f), new Vector2(110f, 7f), 0f), Pink);
            _tube.Fill(PracticeShape.Parallelogram(new Vector2(x0 + 75f, -10f), new Vector2(110f, 7f), 0f), Mint);
            var cap = PracticeShape.RoundRect(new Vector2(x0 - 12f, 0f), new Vector2(30f, 64f), 6f);
            _tube.FillStroke(cap, new Color32(0x3a, 0x44, 0xc9, 0xff), Ink, 4f);
            var tx = x0 + 176f;
            var tw = TubeW - 176f;
            _tube.Fill(PracticeShape.RoundRect(new Vector2(tx + (tw * 0.5f), 0f), new Vector2(tw, 22f), 11f), new Color(Ink.r, Ink.g, Ink.b, 0.15f));
            var len = tw * Mathf.Clamp01(progress / PracticeSteps.LessonCount);
            if (len > 6f)
            {
                var paste = PracticeShape.RoundRect(new Vector2(tx + (len * 0.5f), 0f), new Vector2(len, 26f), 13f);
                _tube.FillStroke(paste, Color.white, Ink, 3f);
                _tube.Fill(PracticeShape.RoundRect(new Vector2(tx + (len * 0.5f), 5f), new Vector2(Mathf.Max(1f, len - 10f), 4f), 2f), Pink);
                _tube.Fill(PracticeShape.RoundRect(new Vector2(tx + (len * 0.5f), -5f), new Vector2(Mathf.Max(1f, len - 10f), 4f), 2f), Mint);
            }

            for (var i = 0; i < PracticeSteps.LessonCount; i++)
            {
                var done = lesson > i;
                var current = lesson == i;
                var chip = _chips[i];
                chip.Clear();
                var body = PracticeShape.Parallelogram(Vector2.zero, new Vector2(200f, 52f), 14f);
                chip.Fill(PracticeShape.Parallelogram(new Vector2(5f, -5f), new Vector2(200f, 52f), 14f), Ink);
                chip.FillStroke(body, done ? Ok : current ? info.Color : Color.white, Ink, 4f);
                if (current)
                {
                    chip.Stroke(PracticeShape.Parallelogram(Vector2.zero, new Vector2(214f, 66f), 16f), 3f, new Color(1f, 1f, 1f, 0.5f + (0.5f * Mathf.Sin(_time * 6f))), true);
                }

                var textColor = done || current ? Color.white : new Color(Ink.r, Ink.g, Ink.b, 0.6f);
                _chipSmall[i].color = textColor;
                _chipTitle[i].color = textColor;
                _chipStamp[i].enabled = done;
            }

            // 時間制限なし（0）なら、時計は出さない。
            var hasLimit = _settings.timeLimitSeconds > 0f;
            _timerRing.transform.parent.gameObject.SetActive(hasLimit);
            var remaining = Mathf.Max(0f, _board.remaining);
            var limit = Mathf.Max(1f, _settings.timeLimitSeconds);
            _timerRing.fillAmount = Mathf.Clamp01(remaining / limit);
            _timerText.text = Mathf.CeilToInt(remaining).ToString();
        }

        // ───────── 準備中（右下・2026-11-19 ユーザーのレイアウト案の配置。見た目は前の素材） ─────────
        // 見出し「準備中 ...」と「0/4」、その下にキャラのアイコンを大きく横に4つ。自分のアイコンの下に「あなた」。
        // アイコンは握っている間・修了したら光る絵（前と同じ）。研修を終えた人は右上に ✓、オーバーヒート中は !。いない席は薄く。

        private const float RosterX = 1288f;
        private const float RosterY = 776f;
        private const float RosterW = 602f;
        private const float RosterH = 272f;
        private const float RosterIcon = 124f;
        private const float RosterIconY = 0f;

        private sealed class Card
        {
            public RawImage Photo;
            public PracticeShape Over;
            public Text Mark;
            public Text You;
            public float DoneAt = -1f;
        }

        private readonly Card[] _cards = new Card[PracticeBoard.SeatCount];
        private PracticeShape _roster;
        private Text _rosterTitle;
        private Text _rosterCount;
        private RawImage _rosterImage;

        private static float SlotX(int i) => -(RosterW * 0.5f) + 93f + (i * 139f);

        private void BuildCards()
        {
            _roster = Shape("Roster", _root, P(RosterX + (RosterW * 0.5f), RosterY + (RosterH * 0.5f)), new Vector2(RosterW + 20f, RosterH + 20f));
            var rt = _roster.transform;
            var headY = (RosterH * 0.5f) - 34f;
            _rosterImage = Raw("Panel Image", rt, null, Vector2.zero, new Vector2(RosterW, RosterH));
            _rosterTitle = Label("Title", rt, Ui.rosterTitle, 32, Ink, TextAnchor.MiddleLeft, new Vector2(-(RosterW * 0.5f) + 40f + 160f, headY), new Vector2(320f, 46f));
            _rosterCount = Label("Count", rt, string.Empty, 32, Ink, TextAnchor.MiddleRight, new Vector2((RosterW * 0.5f) - 30f - 80f, headY), new Vector2(160f, 46f));
            var panelTex = Art != null ? Art.rosterPanel : null;
            _rosterImage.texture = panelTex;
            _rosterImage.enabled = panelTex != null;
            if (panelTex != null)
            {
                // 絵の「準備中 ...」をそのまま使い、人数は絵の右上の四角（絵の 470〜560, 16〜60）に白い文字で出す。
                _rosterImage.rectTransform.sizeDelta = new Vector2(panelTex.width, panelTex.height);
                _rosterTitle.enabled = false;
                _rosterCount.alignment = TextAnchor.MiddleCenter;
                SetSize(_rosterCount, 30);
                _rosterCount.rectTransform.anchoredPosition = new Vector2(515f - (panelTex.width * 0.5f), (panelTex.height * 0.5f) - 38f);
                _rosterCount.rectTransform.sizeDelta = new Vector2(90f, 44f);
            }
            for (var i = 0; i < _cards.Length; i++)
            {
                var info = _seats[i];
                var x = SlotX(i);
                var card = new Card();
                card.Photo = Raw("Icon", rt, info.IconOn, new Vector2(x, RosterIconY), Vector2.one * RosterIcon);
                card.Photo.uvRect = info.IconOnUv;
                card.Over = Shape("Over", rt, new Vector2(x, 0f), new Vector2(150f, RosterH));
                card.Mark = Heading("Mark", rt, string.Empty, 20, new Vector2(x + 46f, RosterIconY + 46f), new Vector2(32f, 32f), TextAnchor.MiddleCenter, Ink);
                card.You = Label("You", rt, i == _local ? _cfg.localMark : string.Empty, 22, Color.white, TextAnchor.MiddleCenter, new Vector2(x, RosterIconY - 100f), new Vector2(110f, 30f));
                _cards[i] = card;
            }
        }

        private void TickCards()
        {
            _roster.Clear();
            var dummyIds = PracticeArtLibrary.DummyCharacterIds(seat => seat == _local || _board.seats[seat].present);
            var all = _board.AllDone || _board.finished;
            var headColor = all ? (Color)new Color32(0x11, 0x80, 0x4c, 0xff) : Ink;
            _rosterTitle.text = all ? Ui.rosterAllReady : Ui.rosterTitle;
            _rosterTitle.color = headColor;
            _rosterCount.text = PracticeUiTexts.Format(Ui.rosterCountFormat, _board.DoneCount, Mathf.Max(1, _board.PresentCount));
            if (_rosterImage.enabled)
            {
                _rosterCount.color = Color.white;
            }
            else
            {
                Panel(_roster, Vector2.zero, new Vector2(RosterW, RosterH), 18f);
                _rosterCount.color = headColor;
            }
            for (var i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                var info = _seats[i];
                var data = _board.seats[i];
                var isLocal = i == _local;
                var present = data.present;
                var done = present && data.IsDone;
                if (done && card.DoneAt < 0f)
                {
                    card.DoneAt = _time;
                }

                // アイコン: 研修を終えたら光る絵（2026-11-23）。誰もいない席は、練習のシーンのダミーの砲台と同じキャラのアイコン（光らない）。
                var on = present && done;
                card.Photo.texture = on ? info.IconOn : info.IconOff;
                card.Photo.uvRect = on ? info.IconOnUv : info.IconOffUv;
                card.Photo.rectTransform.sizeDelta = Vector2.one * RosterIcon * (on ? 1.45f : 1f);
                card.Photo.color = card.Photo.texture == null ? info.Color : Color.white;
                if (!present)
                {
                    var dummy = PracticeArtLibrary.FindRosterCharacter(dummyIds[i]);
                    if (dummy != null)
                    {
                        SpriteUv(dummy.buttonOff != null ? dummy.buttonOff : dummy.IconOrSprite, out var tex, out var uv);
                        card.Photo.texture = tex;
                        card.Photo.uvRect = uv;
                        card.Photo.color = tex != null ? Color.white : dummy.themeColor;
                    }
                    else
                    {
                        card.Photo.color = new Color(1f, 1f, 1f, 0.3f);
                    }
                }

                var over = card.Over;
                over.Clear();

                // 右上の丸: ✓ 修了、! オーバーヒート。
                var markC = new Vector2(46f, RosterIconY + 46f);
                if (done || (present && data.overheated))
                {
                    var k = done ? EaseOutBack(Mathf.Clamp01((_time - card.DoneAt) / 0.35f)) : 1f + (0.12f * Mathf.Sin(_time * 14f));
                    over.FillStroke(PracticeShape.Arc(markC, 17f * k, 0f, 360f, 28), done ? Ok : Burn, Ink, 2.5f);
                    card.Mark.text = done ? "✓" : "!";
                    card.Mark.rectTransform.localScale = Vector3.one * k;
                }
                else
                {
                    card.Mark.text = string.Empty;
                }

                // 自分の「あなた」（前と同じ紺の札）。
                if (isLocal)
                {
                    over.Fill(PracticeShape.RoundRect(new Vector2(0f, RosterIconY - 100f), new Vector2(104f, 30f), 10f), Ink);
                }
            }
        }

        // ───────── 研修の欄（右上・2026-11-19 ユーザーのレイアウト案の配置。見た目は前の素材） ─────────
        // 上: 研修の名前（大きい見出しの文字）と、右に研修の番号の丸、その下に「練習」の札（キャラの色）。
        // 下: 課題の行（2〜3行）。左に丸の中の手の絵、真ん中に やること と小さい説明、右に大きい番号。行の間に ▼。
        //     終わった課題 = 薄い緑にハンコ ／ 今の課題 = キャラの色にふち（点滅） ／ まだの課題 = 薄い紺。
        //     今の課題が「握り続けて倒す」「オーバーヒート」「冷ます」なら、手の絵のまわりの輪で進み（倒した数・熱）を出す（前と同じ）。

        private PracticeShape _list;
        private PracticeShape _listHead;
        private Text _listTitle;
        private Text _listNumber;
        private Text _listChip;
        private readonly PracticeShape[] _rowCards = new PracticeShape[3];
        private readonly RawImage[] _rowIcons = new RawImage[3];
        private readonly Text[] _rowLabels = new Text[3];
        private readonly Text[] _rowHints = new Text[3];
        private readonly Text[] _rowNumbers = new Text[3];
        private readonly RawImage[] _rowStamps = new RawImage[3];
        private readonly RawImage[] _rowCardImages = new RawImage[3];
        private readonly RawImage[] _rowFrames = new RawImage[3];
        private readonly PracticeShape[] _rowOverlays = new PracticeShape[3];
        private RawImage _listPanelImage;
        private Text _listMessage;
        private const float ListX = 1288f;
        private const float ListY = 36f;
        private const float ListW = 602f;
        private const float ListH = 704f;
        private const float ListCardW = 558f;
        private const float ListCardH = 128f;
        private const float ListIconSize = 90f;

        /// <summary>i 枚目のカードの真ん中の高さ（欄の真ん中から）。間隔は欄の絵の ▼ の間隔（167）に合わせた。</summary>
        private static float ListCardY(int i) => 94f - (i * 167f);

        private void BuildChecklist()
        {
            _list = Shape("Checklist", _root, P(ListX + (ListW * 0.5f), ListY + (ListH * 0.5f)), new Vector2(ListW + 20f, ListH + 20f));
            var t = _list.transform;
            _listPanelImage = Raw("Panel Image", t, null, Vector2.zero, new Vector2(ListW, ListH));
            _listPanelImage.enabled = false;
            _listHead = Shape("Head", t, Vector2.zero, new Vector2(ListW, ListH));
            _listTitle = Heading("Title", t, string.Empty, 60, new Vector2(-59f, 280f), new Vector2(430f, 86f), TextAnchor.MiddleLeft);
            _listTitle.resizeTextForBestFit = true;
            _listTitle.resizeTextMinSize = 28;
            _listTitle.resizeTextMaxSize = 60;
            _listNumber = Label("Number", t, string.Empty, 44, Ink, TextAnchor.MiddleCenter, new Vector2(232f, 280f), new Vector2(90f, 60f));
            _listChip = Heading("Chip", t, Ui.panelChip, 28, new Vector2(-201f, 204f), new Vector2(150f, 44f), TextAnchor.MiddleCenter, Ink);
            for (var i = 0; i < 3; i++)
            {
                var y = ListCardY(i);
                _rowCards[i] = Shape("Card", t, new Vector2(0f, y), new Vector2(ListCardW + 40f, ListCardH + 40f));
                var ct = _rowCards[i].transform;
                _rowCardImages[i] = Raw("Card Image", ct, null, Vector2.zero, new Vector2(ListCardW, ListCardH));
                _rowCardImages[i].enabled = false;
                _rowFrames[i] = Raw("Card Frame", ct, null, Vector2.zero, new Vector2(ListCardW, ListCardH));
                _rowFrames[i].enabled = false;
                _rowOverlays[i] = Shape("Overlay", ct, Vector2.zero, new Vector2(ListCardW + 40f, ListCardH + 40f));
                _rowIcons[i] = Raw("Hand", ct, null, new Vector2(-(ListCardW * 0.5f) + 75f, 0f), Vector2.one * ListIconSize);
                _rowLabels[i] = Label("Label", ct, string.Empty, 40, Ink, TextAnchor.MiddleLeft, new Vector2(18f, 21f), new Vector2(300f, 60f));
                _rowLabels[i].resizeTextForBestFit = true;
                _rowLabels[i].resizeTextMinSize = 14;
                _rowLabels[i].resizeTextMaxSize = 40;
                HowToPlayArt.AddOutline(_rowLabels[i], Color.white, 2.5f); // 研修1のカードの絵の文字と同じ、白いふち
                _rowHints[i] = Label("Hint", ct, string.Empty, 22, Ink, TextAnchor.MiddleLeft, new Vector2(38f, -27f), new Vector2(340f, 30f));
                _rowHints[i].resizeTextForBestFit = true;
                _rowHints[i].resizeTextMinSize = 11; // 長い説明（25 文字ほど）も1行に収める
                _rowHints[i].resizeTextMaxSize = 22;
                _rowNumbers[i] = Label("No", ct, (i + 1).ToString(), 96, Ink, TextAnchor.MiddleCenter, new Vector2(226f, 0f), new Vector2(90f, 128f));
                _rowNumbers[i].fontStyle = FontStyle.BoldAndItalic;
                _rowStamps[i] = Raw("Stamp", ct, _cfg.stamp, new Vector2(165f, 26f), new Vector2(70f, 70f));
                _rowStamps[i].color = Ok;
                _rowStamps[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, 12f);
            }

            _listMessage = Label("Message", t, string.Empty, 30, Ink, TextAnchor.MiddleCenter, new Vector2(0f, -60f), new Vector2(ListW - 60f, 360f));
            _listMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private bool TaskDone(PracticeStep task) => LocalStep > task;

        private void TickChecklist()
        {
            var info = _seats[_local];
            var step = LocalStep;
            var lesson = PracticeSteps.Lesson(step);
            var finished = lesson >= PracticeSteps.LessonCount;

            // 欄の絵（研修ごと。研修の名前・「練習」・▼ も絵の中）があれば、それを敷く。無ければコードで描く。
            // 研修ごとの絵が無い研修は、研修1の絵から名前を消して作った「名前なしの欄」の絵を敷き、名前はコードで書く。
            var panelTex = finished || Art == null ? null : PracticeArtLibrary.At(Art.lessonPanels, lesson);
            var blankPanel = panelTex == null && !finished && Art != null ? Art.lessonPanelBlank : null;
            var panelImage = panelTex != null ? panelTex : blankPanel;
            _list.Clear();
            _listPanelImage.enabled = panelImage != null;
            if (panelImage != null)
            {
                _listPanelImage.texture = panelImage;
                _listPanelImage.rectTransform.sizeDelta = new Vector2(panelImage.width, panelImage.height);
            }
            else
            {
                Panel(_list, Vector2.zero, new Vector2(ListW, ListH), 20f);
            }

            // 見出し: 研修の名前・番号の丸（白い丸に紺のふち・キャラの色の輪）・「練習」の札（キャラの色の斜めの札）。
            _listHead.Clear();
            var badge = new Vector2(232f, 280f);
            _listHead.Fill(PracticeShape.Arc(badge + new Vector2(5f, -6f), 50f, 0f, 360f, 48), new Color(Ink.r, Ink.g, Ink.b, 0.25f));
            _listHead.FillStroke(PracticeShape.Arc(badge, 50f, 0f, 360f, 48), Color.white, Ink, 4f);
            // 時間制限があれば、丸は残り時間（輪が上から時計回りに減り、残り 10 秒で赤く点滅）。無ければ研修の番号。
            var hasLimit = _settings.timeLimitSeconds > 0f;
            var remaining = Mathf.Max(0f, _board.remaining);
            var urgent = hasLimit && !_board.finished && remaining <= 10f;
            var ringColor = urgent ? Color.Lerp(Burn, Color.white, 0.35f * (0.5f + (0.5f * Mathf.Sin(_time * 10f)))) : finished ? Ok : info.Color;
            if (hasLimit)
            {
                var frac = Mathf.Clamp01(remaining / Mathf.Max(1f, _settings.timeLimitSeconds));
                _listHead.Stroke(PracticeShape.Arc(badge, 41f, 0f, 360f, 48), 7f, new Color(Ink.r, Ink.g, Ink.b, 0.12f), true);
                if (frac > 0.005f)
                {
                    _listHead.Stroke(PracticeShape.Arc(badge, 41f, 90f - (360f * frac), 90f, 48), 7f, ringColor, false);
                }
            }
            else
            {
                _listHead.Stroke(PracticeShape.Arc(badge, 41f, 0f, 360f, 48), 7f, ringColor, true);
            }

            // 研修の名前: 研修ごとの絵には入っているので書かない。「練習」の札: 絵（名前なしの欄も）に入っているので描かない。
            _listTitle.enabled = panelTex == null;
            _listChip.enabled = panelImage == null;
            if (panelImage == null)
            {
                var chipPos = new Vector2(-201f, 204f);
                _listHead.Fill(PracticeShape.Parallelogram(chipPos + new Vector2(5f, -5f), new Vector2(152f, 46f), 12f), Ink);
                _listHead.FillStroke(PracticeShape.Parallelogram(chipPos, new Vector2(152f, 46f), 12f), finished ? Ok : info.Color, Ink, 3f);
            }

            _listTitle.text = finished ? Ui.listDoneTitle : PracticeScreenSettings.Pick(_cfg.lessonTitles, lesson);
            _listNumber.text = hasLimit ? PracticeUiTexts.Format(Ui.timerFormat, Mathf.CeilToInt(remaining))
                : finished ? "✓" : PracticeUiTexts.Format(Ui.lessonNumberFormat, lesson + 1);
            _listNumber.color = urgent ? Burn : Ink;
            _listNumber.rectTransform.localScale = Vector3.one * (urgent ? 1f + (0.08f * Mathf.Abs(Mathf.Sin(_time * 6f))) : 1f);

            _listMessage.enabled = finished;
            if (finished)
            {
                for (var i = 0; i < 3; i++)
                {
                    HideRow(i);
                }

                var all = _board.AllDone || _board.finished;
                _listMessage.text = all
                    ? $"{_cfg.allDoneText}\n<size=24>{Ui.listAllDoneSub}</size>"
                    : $"{_cfg.waitingText}（{_board.DoneCount}/{Mathf.Max(1, _board.PresentCount)}）\n\n<size=24>{Ui.listFreePlay}</size>";
                _listMessage.color = all ? (Color)new Color32(0x11, 0x80, 0x4c, 0xff) : Ink;
                return;
            }

            var first = (int)PracticeSteps.FirstOf(lesson);
            var count = PracticeSteps.TaskCount(lesson);
            for (var i = 0; i < 3; i++)
            {
                if (i >= count)
                {
                    HideRow(i);
                    continue;
                }

                var task = (PracticeStep)(first + i);
                var done = TaskDone(task);
                var current = task == step;
                DrawTaskCard(i, task, current, done, info);

                // 行の間の ▼。研修ごとの絵には入っているので描かない。名前なしの欄の絵には 1 つ目だけ入っているので、2 つ目からは絵と同じ灰色で描く。
                if (i < count - 1 && blankPanel != null && i >= 1)
                {
                    var arrowY = ListCardY(i) - (ListCardH * 0.5f) - 21f;
                    var tri = new List<Vector2> { new Vector2(-15f, arrowY + 12f), new Vector2(15f, arrowY + 12f), new Vector2(0f, arrowY - 12f) };
                    _list.Fill(tri, new Color32(0xa6, 0xa6, 0xa6, 0xff));
                }
                else if (i < count - 1 && panelImage == null)
                {
                    var arrowY = ListCardY(i) - (ListCardH * 0.5f) - 21f;
                    var tri = new List<Vector2> { new Vector2(-20f, arrowY + 10f), new Vector2(20f, arrowY + 10f), new Vector2(0f, arrowY - 12f) };
                    if (done)
                    {
                        _list.FillStroke(tri, info.Color, Ink, 2f);
                    }
                    else
                    {
                        _list.Fill(tri, new Color(Ink.r, Ink.g, Ink.b, 0.2f));
                    }
                }
            }
        }

        private void HideRow(int i)
        {
            _rowCards[i].Clear();
            _rowOverlays[i].Clear();
            _rowIcons[i].enabled = _rowLabels[i].enabled = _rowHints[i].enabled = _rowNumbers[i].enabled = _rowStamps[i].enabled = false;
            _rowCardImages[i].enabled = _rowFrames[i].enabled = false;
        }

        /// <summary>課題の行1つ（前の素材の見た目: 今の課題 = キャラの色にふち、終わった課題 = 薄い緑にハンコ、まだ = 薄い紺）。</summary>
        private void DrawTaskCard(int i, PracticeStep task, bool current, bool done, SeatInfo info)
        {
            var card = _rowCards[i];
            card.Clear();
            _rowOverlays[i].Clear();
            var future = !current && !done;

            // カードの絵（番号・やること・説明も絵の中）があれば、それを出す。
            var cardTex = Art != null ? PracticeArtLibrary.At(Art.taskCards, (int)task) : null;
            var blankCard = cardTex == null && Art != null ? PracticeArtLibrary.At(Art.taskCardBlanks, i) : null;
            _rowCardImages[i].enabled = cardTex != null || blankCard != null;
            _rowFrames[i].enabled = false;
            if (cardTex != null || blankCard != null)
            {
                DrawTaskCardImage(i, task, current, done, future, cardTex != null ? cardTex : blankCard, info, blankCard != null);
                return;
            }

            var body = PracticeShape.RoundRect(Vector2.zero, new Vector2(ListCardW, ListCardH), 14f);
            card.Fill(body, current ? Color.Lerp(info.Color, Color.white, 0.75f) : done ? new Color(Ok.r, Ok.g, Ok.b, 0.12f) : new Color(Ink.r, Ink.g, Ink.b, 0.05f));
            if (current)
            {
                card.Stroke(body, 3f, new Color(info.Dark.r, info.Dark.g, info.Dark.b, 0.6f + (0.4f * Mathf.Sin(_time * 6f))), true);
            }

            // 手の絵（白い丸の中。今の課題は、まわりの輪で進みを出す）。
            var iconPos = new Vector2(-(ListCardW * 0.5f) + 75f, 0f);
            var r = ListIconSize * 0.55f;
            card.Fill(PracticeShape.Arc(iconPos, r, 0f, 360f, 36), current ? Color.white : new Color(1f, 1f, 1f, 0.6f));
            card.Stroke(PracticeShape.Arc(iconPos, r, 0f, 360f, 36), current ? 4f : 2f, current ? info.Dark : new Color(Ink.r, Ink.g, Ink.b, 0.3f), true);
            if (task == PracticeStep.Skill)
            {
                // スキルの行は、丸の後ろにキャラの色の星（前と同じ）。
                card.FillStroke(PracticeShape.Star(iconPos, r + 8f, r * 0.55f), current ? info.Color : new Color(info.Color.r, info.Color.g, info.Color.b, 0.35f), Ink, 2f);
                card.FillStroke(PracticeShape.Arc(iconPos, r, 0f, 360f, 36), current ? Color.white : new Color(1f, 1f, 1f, 0.6f), current ? info.Dark : new Color(Ink.r, Ink.g, Ink.b, 0.3f), current ? 4f : 2f);
            }

            if (current)
            {
                DrawProgressRing(card, task, iconPos, r + 6f, info);
            }

            DrawTaskHand(i, task, current, done, future, iconPos, info);

            // 文字（行の中で、右の大きい番号と重ならない所: -132〜168）。
            _rowLabels[i].enabled = _rowHints[i].enabled = _rowNumbers[i].enabled = true;
            _rowLabels[i].rectTransform.anchoredPosition = new Vector2(18f, 21f);
            _rowLabels[i].rectTransform.sizeDelta = new Vector2(300f, 56f);
            _rowHints[i].rectTransform.anchoredPosition = new Vector2(18f, -27f);
            _rowHints[i].rectTransform.sizeDelta = new Vector2(300f, 30f);
            _rowLabels[i].text = TaskLabel(task, current);
            _rowLabels[i].color = done ? new Color(Ink.r, Ink.g, Ink.b, 0.45f) : Ink;
            _rowHints[i].text = PracticeScreenSettings.Pick(_cfg.taskHints, (int)task);
            _rowHints[i].color = new Color(Ink.r, Ink.g, Ink.b, done ? 0.35f : 0.65f);
            _rowNumbers[i].text = (i + 1).ToString();
            _rowNumbers[i].color = current ? new Color(info.Dark.r, info.Dark.g, info.Dark.b, 0.9f) : done ? new Color(Ink.r, Ink.g, Ink.b, 0.15f) : new Color(Ink.r, Ink.g, Ink.b, 0.3f);
            _rowStamps[i].enabled = done && _cfg.stamp != null;
            _rowStamps[i].rectTransform.anchoredPosition = new Vector2(165f, 26f);
        }

        /// <summary>
        /// カードの絵で出す（番号・やること・説明は絵の中なので、文字は出さない）。
        /// 終わった課題 = 薄く＋ハンコ ／ 今の課題 = 光るふちの絵を重ねる ／ まだの課題 = 少し暗く。
        /// 手の絵は、カードの絵の左寄りの灰色の四角（573×143 の絵で 117〜207）に重ねる。
        /// </summary>
        private void DrawTaskCardImage(int i, PracticeStep task, bool current, bool done, bool future, Texture cardTex, SeatInfo info, bool writeText)
        {
            var image = _rowCardImages[i];
            image.texture = cardTex;
            image.rectTransform.sizeDelta = new Vector2(cardTex.width, cardTex.height);
            image.color = done ? new Color(1f, 1f, 1f, 0.55f) : future ? new Color(0.8f, 0.8f, 0.88f, 1f) : Color.white;

            var frameTex = Art != null ? Art.taskCardCurrent : null;
            if (current && frameTex != null)
            {
                var frame = _rowFrames[i];
                frame.enabled = true;
                frame.texture = frameTex;
                frame.rectTransform.sizeDelta = new Vector2(frameTex.width, frameTex.height);
                frame.color = new Color(1f, 1f, 1f, 0.7f + (0.3f * Mathf.Sin(_time * 5f)));
            }

            var scale = cardTex.width / 573f;
            var iconPos = new Vector2((-cardTex.width * 0.5f) + (162f * scale), 0f);
            if (current)
            {
                DrawProgressRing(_rowOverlays[i], task, iconPos, (ListIconSize * 0.55f) + 6f, info);
            }

            DrawTaskHand(i, task, current, done, future, iconPos, info);
            _rowNumbers[i].enabled = false;
            _rowStamps[i].enabled = done && _cfg.stamp != null;
            _rowStamps[i].rectTransform.anchoredPosition = new Vector2((cardTex.width * 0.5f) - 55f, 22f);

            // 文字なしのカードの絵: やること・説明を、研修1のカードの絵と同じ場所・色で書く（絵の 237〜555 の白い所の中に収める）。
            _rowLabels[i].enabled = _rowHints[i].enabled = writeText;
            if (writeText)
            {
                var left = (-cardTex.width * 0.5f) + (237f * scale);
                var width = (555f - 237f) * scale;
                var a = done ? 0.55f : 1f;
                _rowLabels[i].text = TaskLabel(task, current);
                _rowLabels[i].color = new Color(0.29f, 0.25f, 0.56f, a);
                _rowLabels[i].rectTransform.anchoredPosition = new Vector2(left + (width * 0.5f), 21f);
                _rowLabels[i].rectTransform.sizeDelta = new Vector2(width, 54f);
                _rowHints[i].text = PracticeScreenSettings.Pick(_cfg.taskHints, (int)task);
                _rowHints[i].color = new Color(0.24f, 0.22f, 0.55f, 0.95f * a);
                _rowHints[i].rectTransform.anchoredPosition = new Vector2(left + (width * 0.5f), -27f);
                _rowHints[i].rectTransform.sizeDelta = new Vector2(width, 30f);
            }
        }

        /// <summary>今の課題の進みの輪（握り続けて倒す = 倒した割合、オーバーヒート = 熱、冷ます = 残りの熱）。</summary>
        private void DrawProgressRing(PracticeShape target, PracticeStep task, Vector2 pos, float radius, SeatInfo info)
        {
            float? ring = null;
            var ringColor = info.Color;
            if (task == PracticeStep.Hold && Source != null)
            {
                ring = Source.HoldProgress01;
            }
            else if (task == PracticeStep.Overheat || task == PracticeStep.Cool)
            {
                ring = Mathf.Clamp01(Source != null ? Source.Heat01 : _board.seats[_local].Heat01);
                ringColor = task == PracticeStep.Cool ? Mint : Color.Lerp(new Color(1f, 0.78f, 0.2f), Burn, ring.Value);
            }

            if (ring.HasValue && ring.Value > 0.01f)
            {
                target.Stroke(PracticeShape.Arc(pos, radius, 90f - (360f * Mathf.Clamp01(ring.Value)), 90f, 40), 7f, ringColor, false);
            }
        }

        /// <summary>課題の手の絵。握る課題はこぶし、離す課題は開いた手、泡は泡の絵。今の課題は動く（ギュッ・パッ）。</summary>
        private void DrawTaskHand(int i, PracticeStep task, bool current, bool done, bool future, Vector2 pos, SeatInfo info)
        {
            var icon = _rowIcons[i];
            var phase = Mathf.Repeat(_time, 1.2f) / 1.2f;
            var closed = true;
            var scale = 1f;
            var bubble = false;
            switch (task)
            {
                case PracticeStep.Grip:
                case PracticeStep.Skill:
                    closed = !current || phase > 0.5f;
                    scale = current && closed ? 1.08f : 1f;
                    break;
                case PracticeStep.Hold:
                case PracticeStep.Overheat:
                    scale = current ? 1f + (0.03f * Mathf.Sin(_time * 22f)) : 1f;
                    break;
                case PracticeStep.Release:
                    closed = current && phase < 0.5f;
                    scale = current && !closed ? 1.08f : 1f;
                    break;
                case PracticeStep.Cool:
                    closed = false;
                    break;
                case PracticeStep.Bubble:
                    bubble = true;
                    scale = current ? 1f + (0.05f * Mathf.Sin(_time * 5f)) : 1f;
                    break;
            }

            icon.enabled = true;
            if (bubble)
            {
                icon.texture = _cfg.bubble;
                icon.uvRect = new UnityEngine.Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                icon.texture = _cfg.hand;
                // 手の絵は、左半分が開いた手・右半分が握った手。
                icon.uvRect = closed ? new UnityEngine.Rect(0.5f, 0f, 0.5f, 1f) : new UnityEngine.Rect(0f, 0f, 0.5f, 1f);
            }

            icon.color = icon.texture == null ? info.Color : done ? new Color(1f, 1f, 1f, 0.5f) : future ? new Color(1f, 1f, 1f, 0.75f) : Color.white;
            icon.rectTransform.anchoredPosition = pos;
            icon.rectTransform.sizeDelta = Vector2.one * (ListIconSize - 10f);
            icon.rectTransform.localScale = Vector3.one * scale;
        }

        /// <summary>手の絵の下の文字（Resources/PracticeTextLibrary の「手の絵の下の文字」）。</summary>
        private static string Caption(PracticeStep task) => PracticeScreenSettings.Pick(Ui.captions, (int)task);

        /// <summary>課題の名前。研修1は倒す数を入れ、練習のシーン（本物の敵）では倒した数も出す。絵のフィールドでは「握り続ける」。</summary>
        private string TaskLabel(PracticeStep task, bool current)
        {
            var label = PracticeScreenSettings.Pick(_cfg.taskLabels, (int)task);
            if (task != PracticeStep.Hold)
            {
                return label;
            }

            var need = Mathf.Max(1, (_settings.rules ?? new PracticeRules()).lesson1Kills);
            if (PracticeTrainer.World == null)
            {
                return Ui.holdLabelNoEnemies;
            }

            label = label.Contains("{0}") ? label.Replace("{0}", need.ToString()) : label;
            var trainer = PracticeTrainer.Local;
            return current && trainer != null ? $"{label}（{trainer.KillsInLesson1}/{need}）" : label;
        }
    }
}
