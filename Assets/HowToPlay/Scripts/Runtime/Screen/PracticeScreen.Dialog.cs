using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // 下の会話（自分のキャラの顔の窓・ギザギザの吹き出し・歯磨き粉のチューブ）と、研修の始まり・クリアの帯、最後の「はじまるよ！」。
    public sealed partial class PracticeScreen
    {
        // 2026-11-19 ユーザーのレイアウト案の配置: 左下に立ち絵（枠なし・画面の下で切れる）、その右に吹き出しと名札。
        // 吹き出しと名札の見た目は前のまま（ギザギザの吹き出し＋右の歯磨き粉のチューブ、キャラの色の名札）。
        // チューブが右の「準備中」の欄にかからないよう、吹き出しは 380〜1080 にして、チューブを 1270 くらいまでに収める。
        private const float PortraitX = 10f;
        private const float PortraitY = 740f;
        private const float PortraitW = 450f;
        private const float PortraitH = 340f;
        private const float BubbleX = 380f;
        private const float BubbleY = 838f; // 左上の破片がフィールドの枠にかからない高さ
        private const float BubbleW = 700f;
        private const float BubbleH = 215f;

        private PracticeShape _portraitFrame;
        private RawImage _portrait;
        private PracticeShape _bubble;
        private PracticeShape _tag;
        private Text _tagText;
        private Text _line;
        private Text _more;
        private string _lineMarked;
        private string _lineWrapped;
        private float _lineStart;
        private float _bubbleStart;
        private enum Mood { Normal, Alert, Success }
        private Mood _mood;

        private void BuildDialog()
        {
            var info = _seats[_local];
            // 立ち絵（枠なし。画面の左下で、下は画面の端で切れる）。
            _portraitFrame = Shape("Portrait", _root, P(PortraitX + (PortraitW * 0.5f), PortraitY + (PortraitH * 0.5f)), new Vector2(PortraitW, PortraitH));
            var mask = HowToPlayArt.MakeRect("Mask", _portraitFrame.transform, Vector2.zero, new Vector2(PortraitW, PortraitH));
            mask.gameObject.AddComponent<RectMask2D>();
            _portrait = Raw("Stand", mask, info.Stand, Vector2.zero, new Vector2(PortraitW, PortraitH));
            _portrait.uvRect = PortraitUv(info, PortraitW, PortraitH);
            _portrait.enabled = info.Stand != null;

            // 吹き出し（前のまま: ギザギザの紺の吹き出し、左上の破片、右の歯磨き粉のチューブ）と名札。
            _bubble = Shape("Speech", _root, P(BubbleX + (BubbleW * 0.5f), BubbleY + (BubbleH * 0.5f)), new Vector2(BubbleW + 400f, BubbleH + 200f));
            _tag = Shape("Name Tag", _root, P(BubbleX + 220f, BubbleY - 10f), new Vector2(340f, 60f));
            _tagText = Heading("Name", _tag.transform, $"{_local + 1}P {info.Name}", 24, Vector2.zero, new Vector2(320f, 40f), TextAnchor.MiddleCenter, Ink);
            _line = Label("Line", _bubble.transform, string.Empty, 38, Color.white, TextAnchor.UpperLeft, new Vector2(-10f, -4f), new Vector2(BubbleW - 140f, BubbleH - 40f));
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.verticalOverflow = VerticalWrapMode.Overflow;
            _line.resizeTextForBestFit = false; // 1 文字ずつ出る間に大きさが変わらないよう、大きさは WrapLine で決める
            _line.supportRichText = true;
            _line.lineSpacing = 1.05f;
            _more = Label("More", _bubble.transform, "▼", 22, info.Color, TextAnchor.MiddleCenter, new Vector2((BubbleW * 0.5f) - 60f, -(BubbleH * 0.5f) + 34f), new Vector2(40f, 30f));
            _bubbleStart = _time;
        }

        /// <summary>立ち絵から顔のまわりを切り抜く（窓の縦横に合わせる）。</summary>
        private static Rect PortraitUv(SeatInfo info, float w, float h)
        {
            var uv = info.StandUv;
            if (info.Stand == null)
            {
                return uv;
            }

            var crop = info.PortraitCrop;
            var texW = info.Stand.width * uv.width;
            var texH = info.Stand.height * uv.height;
            var cw = Mathf.Clamp01(crop.width);
            var ch = cw * texW / texH * (h / w);
            var cx = Mathf.Clamp(crop.x, cw * 0.5f, 1f - (cw * 0.5f));
            var cy = Mathf.Clamp(crop.y, ch * 0.5f, 1f - (ch * 0.5f));
            return new Rect(uv.x + ((cx - (cw * 0.5f)) * uv.width), uv.y + ((cy - (ch * 0.5f)) * uv.height), cw * uv.width, ch * uv.height);
        }

        /// <summary>今のセリフ（自分の課題と様子で決まる）。</summary>
        private string _skillLine;

        /// <summary>今のセリフ（自分の課題と様子で決まる。キャラのセリフがあればそれを使う）。</summary>
        private string CurrentLine(out Mood mood)
        {
            mood = Mood.Normal;
            var step = LocalStep;
            var source = Source;
            var look = _seats[_local].Look;
            var skillActive = step == PracticeStep.Skill && source != null && source.SkillActive;
            if (!skillActive)
            {
                _skillLine = null;
            }

            if (_board.finished || _board.AllDone)
            {
                mood = Mood.Normal; // 修了しても吹き出しはキャラの色のまま（緑にしない・2026-11-19）
                return PracticeScreenSettings.Line(look?.allDoneLine, _cfg.allDoneLine);
            }

            if (step == PracticeStep.Done)
            {
                mood = Mood.Normal; // 修了しても吹き出しはキャラの色のまま（緑にしない・2026-11-19）
                return PracticeScreenSettings.Line(look?.doneLine, _cfg.doneLine);
            }

            if (LocalOverheated)
            {
                mood = Mood.Alert;
                return PracticeScreenSettings.Line(look?.overheatLine, _cfg.overheatLine);
            }

            if (step == PracticeStep.Grip && _time < _cfg.introSeconds)
            {
                return PracticeScreenSettings.Line(look?.introLine, _cfg.introLine);
            }

            if (skillActive)
            {
                if (_skillLine == null)
                {
                    var lines = look != null ? look.skillLines : null;
                    _skillLine = lines != null && lines.Length > 0 ? lines[Mathf.Abs(Mathf.FloorToInt(_time * 7f)) % lines.Length] : Ui.skillFallback;
                }

                return _skillLine;
            }

            // 研修3: スキルを使い切ったあと、修了までの数秒（「次の攻撃はスキル」のままにしない）。
            if (step == PracticeStep.Skill && source != null && source.SkillUsedUp)
            {
                return PracticeScreenSettings.Line(look?.afterSkillLine, _cfg.afterSkillLine);
            }

            // 研修1: ばい菌を倒すたびに「あと何体」をキャラの口調で言う（本物の敵がいるときだけ数える）。
            var trainer = PracticeTrainer.Local;
            if (step == PracticeStep.Hold && trainer != null && trainer.KillsInLesson1 > 0)
            {
                var kills = trainer.KillsInLesson1;
                var need = Mathf.Max(1, (_settings.rules ?? new PracticeRules()).lesson1Kills);
                var list = look != null && look.killLines != null && look.killLines.Length > 0 ? look.killLines : _cfg.killLines;
                if (list != null && list.Length > 0)
                {
                    var template = list[Mathf.Min(kills - 1, list.Length - 1)];
                    if (!string.IsNullOrEmpty(template))
                    {
                        return template.Replace("{0}", kills.ToString()).Replace("{1}", Mathf.Max(0, need - kills).ToString());
                    }
                }
            }

            var own =look != null ? PracticeScreenSettings.Pick(look.stepLines, (int)step) : null;
            return PracticeScreenSettings.Line(own, PracticeScreenSettings.Pick(_cfg.stepLines, (int)step));
        }

        private void TickDialog(float dt)
        {
            var info = _seats[_local];
            // 次のセリフは、今のセリフを出し終わって読む時間が経つまで待つ（課題が続けて進んでも速すぎないように）。
            // ただしオーバーヒートの警告と、スキルを出したときの掛け声は、すぐ割り込む。待っている間に次の課題へ進んだら、いちばん新しいセリフを出す。
            var wanted = CurrentLine(out var wantedMood);
            if (wanted != _lineMarked)
            {
                var cps = Mathf.Max(1f, _cfg.lineCharsPerSecond);
                var shown = _time - _lineStart;
                var need = string.IsNullOrEmpty(_lineMarked) ? 0f : (Plain(_lineMarked).Length / cps) + _cfg.lineReadSeconds;
                var interrupt = (wantedMood == Mood.Alert && _mood != Mood.Alert) || (_skillLine != null && wanted == _skillLine);
                if (shown >= need || interrupt)
                {
                    _lineMarked = wanted;
                    _lineWrapped = WrapLine(wanted);
                    _lineStart = _time;
                    _mood = wantedMood;
                }
            }

            var line = _lineWrapped ?? _lineMarked;
            var mood = _mood;
            var tText = _time - _lineStart;
            var tIn = _time - _bubbleStart;

            // 吹き出しの形（1秒に12回、ふちがガタガタ動く）。
            var appear = Mathf.Clamp01(tIn / 0.3f);
            var sx = tIn < 0.3f ? EaseOutBack(appear) : 1f;
            var sy = tIn < 0.3f ? Mathf.Lerp(0.4f, 1f, EaseOutBack(appear)) : 1f;
            if (tIn > 0.45f && tText < 0.24f)
            {
                var b = Mathf.Sin(tText / 0.24f * Mathf.PI);
                sx *= 1f + (0.04f * b);
                sy *= 1f + (0.08f * b);
            }

            var shake = Vector2.zero;
            if (mood == Mood.Alert)
            {
                var s2 = Mathf.Floor(_time * 24f);
                shake = new Vector2(Hash(s2 * 1.3f) * 6f, Hash((s2 * 2.7f) + 9f) * 4f);
            }

            _bubble.rectTransform.anchoredPosition = P(BubbleX + (BubbleW * 0.5f), BubbleY + (BubbleH * 0.5f)) + shake;
            _bubble.rectTransform.localScale = new Vector3(sx, sy, 1f);
            _bubble.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.4f);
            DrawJag(info, mood, tIn, tText);

            // 名札。
            _tag.Clear();
            var accent = info.Color; // 吹き出しまわりの色は、いつもキャラの色
            var tagBody = PracticeShape.Parallelogram(Vector2.zero, new Vector2(300f, 42f), 14f);
            _tag.Fill(PracticeShape.Parallelogram(new Vector2(5f, -5f), new Vector2(300f, 42f), 14f), Ink);
            _tag.FillStroke(tagBody, accent, Ink, 3f);
            _tag.rectTransform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01((tIn - 0.06f) / 0.25f));
            _tag.rectTransform.anchoredPosition = P(BubbleX + 220f, BubbleY - 12f) + shake;

            // 文字（1文字ずつ出る）。
            var visible = Mathf.FloorToInt(tText * Mathf.Max(1f, _cfg.lineCharsPerSecond));
            var highlight = info.Light;
            _line.text = tIn > 0.2f ? Rich(line, highlight, visible, out var complete) : string.Empty;
            _more.enabled = tIn > 0.2f && Plain(line).Length <= visible && Mathf.Repeat(_time * 1.2f, 1f) < 0.5f;
            _more.color = accent;
        }

        // ───────── 吹き出しの改行（2026-11-19） ─────────
        // uGUI の折り返しは日本語を好きな所で切る（「歯磨|き粉」、行の頭に「、」など）。そこで、実際の文字の幅を測って、
        // 言葉の切れ目（、。！？ の後・空白・【】の前後・ひらがな→漢字/カタカナの所）で改行を入れておく。
        // 行の頭に来てはいけない文字（、。！？ー っ ゃ など）と、行の終わりに来てはいけない文字（「（【）も守る。

        private const int BubbleMaxLines = 4; // 吹き出しの幅を詰めたので 4 行まで（文字の高さ 175 に 4 行入る）
        private const string NoLineStart = "、。，．,.！？!?）」』】〕ー～〜…‥・：；ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ";
        private const string NoLineEnd = "（「『【〔";
        private const string BreakAfter = "、。！？!?…」』）";

        // セリフの文字のいちばん大きい大きさ・いちばん小さい大きさ。
        private const int LineMaxSize = 38;
        private const int LineMinSize = 26;

        /// <summary>
        /// セリフを改行し、吹き出しの文字の箱（高さ）に入る大きさにする（38 から 2 ずつ小さくして、入ったらそれ）。
        /// フォントが変わって行の高さが変わっても、はみ出さない。
        /// </summary>
        private string WrapLine(string marked)
        {
            if (_line == null)
            {
                return marked;
            }

            var height = _line.rectTransform.rect.height;
            string wrapped = marked;
            for (var size = LineMaxSize; size >= LineMinSize; size -= 2)
            {
                _line.fontSize = size;
                wrapped = WrapAtCurrentSize(marked);
                var settings = _line.GetGenerationSettings(new Vector2(_line.rectTransform.rect.width, 0f));
                settings.resizeTextForBestFit = false;
                settings.richText = false;
                var h = _line.cachedTextGeneratorForLayout.GetPreferredHeight(Plain(wrapped), settings) / Mathf.Max(0.0001f, _line.pixelsPerUnit);
                if (h <= height)
                {
                    break;
                }
            }

            return wrapped;
        }

        private string WrapAtCurrentSize(string marked)
        {
            if (string.IsNullOrEmpty(marked) || _line == null || marked.IndexOf('\n') >= 0)
            {
                return marked;
            }

            var width = _line.rectTransform.rect.width * 0.96f;
            if (width <= 1f)
            {
                return marked;
            }

            var settings = _line.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            settings.richText = false;
            var gen = _line.cachedTextGeneratorForLayout;
            var ppu = Mathf.Max(0.0001f, _line.pixelsPerUnit);
            float Measure(int from, int to) => gen.GetPreferredWidth(Plain(marked.Substring(from, to - from)), settings) / ppu;

            // 言葉の切れ目を優先しすぎて行が増える（吹き出しからはみ出す）ときは、切れ目の好みをゆるめてやり直す。
            // 吹き出しには 3 行まで入る。
            var most = Mathf.Max(BubbleMaxLines, Wrap(marked, Measure, width, 2f).Count);
            foreach (var minRatio in new[] { 0.45f, 0.65f, 0.8f })
            {
                var lines = Wrap(marked, Measure, width, minRatio);
                if (lines.Count <= most)
                {
                    return string.Join("\n", lines);
                }
            }

            return string.Join("\n", Wrap(marked, Measure, width, 2f));
        }

        /// <summary>minRatio = 切れ目を探すのは、行の長さのこの割合より後ろだけ（1 より大きいと、切れ目を探さず入るだけ入れる）。</summary>
        private static List<string> Wrap(string marked, System.Func<int, int, float> measure, float width, float minRatio)
        {
            var lines = new List<string>();
            var n = marked.Length;
            var start = 0;
            var guard = 0;
            while (start < n && guard++ < 20)
            {
                while (start < n && IsSpace(marked[start]))
                {
                    start++;
                }

                if (start >= n)
                {
                    break;
                }

                // この行に入る、いちばん後ろの位置。
                var end = start + 1;
                while (end < n && measure(start, end + 1) <= width)
                {
                    end++;
                }

                if (end >= n)
                {
                    lines.Add(marked.Substring(start).TrimEnd(' ', '　'));
                    break;
                }

                var cut = ChooseBreak(marked, start, end, minRatio);
                lines.Add(marked.Substring(start, cut - start).TrimEnd(' ', '　'));
                start = cut;
            }

            return lines;
        }

        /// <summary>start〜end の間で、いちばん自然な改行の位置（その位置の文字から次の行）。</summary>
        private static int ChooseBreak(string s, int start, int end, float minRatio)
        {
            var minLen = (end - start) * minRatio;
            for (var level = 3; level >= 1; level--)
            {
                for (var k = end; k > start + 1; k--)
                {
                    if (k - start >= minLen && CanBreak(s, k) && BreakLevel(s, k) >= level)
                    {
                        return k;
                    }
                }
            }

            // 切れ目が無い: 入るだけ入れて、禁則だけ守る。
            for (var k = end; k > start + 1; k--)
            {
                if (CanBreak(s, k))
                {
                    return k;
                }
            }

            return end;
        }

        private static bool CanBreak(string s, int k)
        {
            if (k <= 0 || k >= s.Length)
            {
                return false;
            }

            // 次の行の頭（【 は飛ばして見る）と、この行の終わり（】 は飛ばして見る）。
            var next = NextVisible(s, k);
            var prev = PrevVisible(s, k);
            if (next >= 0 && NoLineStart.IndexOf(s[next]) >= 0)
            {
                return false;
            }

            if (prev >= 0 && NoLineEnd.IndexOf(s[prev]) >= 0)
            {
                return false;
            }

            // 最後の行が 1〜2 文字だけにならないように。
            if (Plain(s.Substring(k)).Trim(' ', '　').Length <= 2)
            {
                return false;
            }

            return !InsideWord(s, k);
        }

        // ひらがなと漢字がまざっていて、間で切ると読みにくい言葉。
        private static readonly string[] KeepTogether = { "ばい菌", "歯磨き粉", "握りっぱなし", "握り続け", "撃てる", "割って", "割り", "倒せ", "当て" };

        private static bool InsideWord(string s, int k)
        {
            foreach (var word in KeepTogether)
            {
                var i = s.IndexOf(word, System.StringComparison.Ordinal);
                while (i >= 0)
                {
                    if (k > i && k < i + word.Length)
                    {
                        return true;
                    }

                    i = s.IndexOf(word, i + 1, System.StringComparison.Ordinal);
                }
            }

            return false;
        }

        /// <summary>3 = 句読点・空白の後、2 = 【の前・】の後（後ろが助詞でないとき）、1 = ひらがな→漢字・カタカナ、0 = 言葉の途中。</summary>
        private static int BreakLevel(string s, int k)
        {
            var a = s[k - 1];
            var b = s[k];
            if (BreakAfter.IndexOf(a) >= 0 || IsSpace(a) || IsSpace(b))
            {
                return 3;
            }

            var prev = PrevVisible(s, k);
            var next = NextVisible(s, k);

            // 「【スキル】|になる」のように、】の後ろが助詞・送りがな（ひらがな）なら切らない。
            if (b == '【' || (a == '】' && next >= 0 && !IsHiragana(s[next])))
            {
                return 2;
            }

            if (prev >= 0 && next >= 0 && IsHiragana(s[prev]) && (IsKanji(s[next]) || IsKatakana(s[next])))
            {
                return 1;
            }

            return 0;
        }

        private static int NextVisible(string s, int k)
        {
            for (var i = k; i < s.Length; i++)
            {
                if (s[i] != '【' && s[i] != '】')
                {
                    return i;
                }
            }

            return -1;
        }

        private static int PrevVisible(string s, int k)
        {
            for (var i = k - 1; i >= 0; i--)
            {
                if (s[i] != '【' && s[i] != '】')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSpace(char c) => c == ' ' || c == '　';

        private static bool IsHiragana(char c) => c >= 'ぁ' && c <= 'ゟ';

        private static bool IsKatakana(char c) => c >= 'ァ' && c <= 'ヺ';

        private static bool IsKanji(char c) => (c >= '一' && c <= '鿿') || c == '々';

        private static float Hash(float n)
        {
            n = Mathf.Sin((n * 127.1f) + 311.7f) * 43758.5453f;
            return ((n - Mathf.Floor(n)) * 2f) - 1f;
        }

        private void DrawJag(SeatInfo info, Mood mood, float tIn, float tText)
        {
            var w = BubbleW;
            var h = BubbleH;
            var step = Mathf.Floor(_time * 12f);
            var amp = mood == Mood.Alert ? 6f : 3.2f;
            var seed = _local * 100f;
            Vector2 J(float x, float y, int i) => new Vector2(x + (Hash(seed + (i * 13.7f) + (step * 7.1f)) * amp), y + (Hash(seed + (i * 29.3f) + (step * 3.7f) + 50f) * amp));

            // 左上が原点（上が +y なので、下へは -y）。真ん中に寄せる。
            var o = new Vector2(-w * 0.5f, h * 0.5f);
            Vector2 L(float x, float y) => o + new Vector2(x, -y);
            var pts = new List<Vector2>
            {
                J(L(0.035f * w, 0.10f * h).x, L(0.035f * w, 0.10f * h).y, 0),
                J(L(w, 0f).x, L(w, 0f).y, 1),
                J(L(0.975f * w, h).x, L(0.975f * w, h).y, 2),
                J(L(0.06f * w, 0.88f * h).x, L(0.06f * w, 0.88f * h).y, 3),
                J(L(-0.035f * w, 0.80f * h).x, L(-0.035f * w, 0.80f * h).y, 4),
                J(L(0f, 0.68f * h).x, L(0f, 0.68f * h).y, 5),
                J(L(-0.05f * w, 0.54f * h).x, L(-0.05f * w, 0.54f * h).y, 6),
                J(L(-0.005f * w, 0.42f * h).x, L(-0.005f * w, 0.42f * h).y, 7),
                J(L(-0.03f * w, 0.26f * h).x, L(-0.03f * w, 0.26f * h).y, 8)
            };

            // 色はいつもキャラの色（オーバーヒートでも赤くしない・2026-11-19。揺れとギザギザだけで知らせる）。
            var accent = info.Color;
            var fillTop = new Color32(0x15, 0x1a, 0x4f, 0xff);
            var fillBottom = new Color32(0x0b, 0x0f, 0x33, 0xff);
            _bubble.Clear();
            var shadow = new List<Vector2>();
            foreach (var p in pts)
            {
                shadow.Add(p + new Vector2(14f, -13f));
            }

            _bubble.Fill(shadow, accent);
            _bubble.Stroke(pts, 20f, Ink, true);
            _bubble.Stroke(pts, 9f, Color.white, true);
            _bubble.Fill(pts, fillTop, fillBottom);

            // 左上の破片（白＋キャラの色）。
            var sk = Mathf.Clamp01((tIn - 0.1f) / 0.28f);
            if (sk > 0f)
            {
                var e = EaseOutBack(sk);
                var off = new Vector2(Mathf.Lerp(-60f, 0f, e), Mathf.Lerp(-30f, 0f, e));
                var s1 = new List<Vector2> { L(w * 0.03f, h * 0.02f) + off, L((w * 0.21f) + (Hash(step + 3f) * 4f), -h * 0.42f) + off, L(w * 0.235f, -h * 0.30f) + off, L(w * 0.07f, h * 0.12f) + off };
                var s1Shadow = new List<Vector2>();
                foreach (var p in s1)
                {
                    s1Shadow.Add(p + new Vector2(10f, 6f));
                }

                _bubble.FillStroke(s1Shadow, accent, Ink, 5f);
                _bubble.FillStroke(s1, Color.white, Ink, 5f);
                var s1b = new List<Vector2> { L(0f, h * 0.18f) + off, L(w * 0.05f, -h * 0.06f) + off, L(w * 0.075f, 0f) + off, L(w * 0.03f, h * 0.22f) + off };
                _bubble.FillStroke(s1b, info.Light, Ink, 4f);
            }

            // 右: 歯磨き粉のチューブ（キャラの色・角ばった細いチューブ）。セリフが変わると押されて、口から少し出る。
            DrawTube(info, tIn, tText, L(w * 0.95f, h * 0.66f));
        }

        private void DrawTube(SeatInfo info, float tIn, float tText, Vector2 anchor)
        {
            var ap = Mathf.Clamp01((tIn - 0.1f) / 0.34f);
            if (ap <= 0f)
            {
                return;
            }

            var e = EaseOutBack(ap);
            var h = BubbleH;
            // 右の「準備中」の欄にかからない長さ・太さまで（吹き出しの高さ 190 のときと同じくらい）。
            var len = Mathf.Min(h * 1.2f, 200f);
            var rad = Mathf.Min(h * 0.17f, 33f);
            var sqx = 1f;
            var sqy = 1f;
            var puff = -1f;
            if (tIn > 0.45f && tText >= 0f && tText < 0.32f)
            {
                var b = Mathf.Sin(Mathf.Min(1f, tText / 0.16f) * Mathf.PI);
                sqx = 1f - (0.14f * b);
                sqy = 1f + (0.16f * b);
                puff = tText / 0.32f;
            }

            var angle = (28f - (Mathf.Lerp(50f, 0f, e))) * Mathf.Deg2Rad + (0.035f * Mathf.Sin(_time * 2.2f));
            var basePos = anchor + new Vector2(Mathf.Lerp(140f, 0f, e), Mathf.Lerp(40f, 0f, e));
            var ax = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var ay = new Vector2(-ax.y, ax.x);
            var scale = e;
            var step = Mathf.Floor(_time * 12f);
            Vector2 T(float x, float y, int i) => basePos + (((ax * x * sqx) + (ay * y * sqy)) * scale) + new Vector2(Hash(step * 4.7f + (i * 31f)) * 2.2f, Hash(step * 4.7f + (i * 31f) + 50f) * 2.2f);

            var body = new List<Vector2> { T(len * 0.13f, rad, 1), T(len * 0.72f, rad * 0.9f, 2), T(len * 0.86f, rad * 0.42f, 3), T(len * 0.86f, -rad * 0.42f, 4), T(len * 0.72f, -rad * 0.9f, 5), T(len * 0.13f, -rad, 6) };
            var shadow = new List<Vector2>();
            foreach (var p in body)
            {
                shadow.Add(p + new Vector2(6f, -8f));
            }

            _bubble.Fill(shadow, new Color(Ink.r, Ink.g, Ink.b, 0.35f));

            // ふた。
            var cx0 = len * 0.85f;
            var cw = len * 0.16f;
            var ch = rad * 0.52f;
            var cap = new List<Vector2> { T(cx0, ch, 10), T(cx0 + cw, ch, 11), T(cx0 + cw, -ch, 12), T(cx0, -ch, 13) };
            _bubble.FillStroke(cap, info.Dark, Ink, 5f);

            _bubble.Fill(body, Color.white);
            var band = new List<Vector2> { T(len * 0.13f, rad * 0.55f, 20), T(len * 0.74f, rad * 0.5f, 21), T(len * 0.76f, -rad * 0.08f, 22), T(len * 0.13f, -rad * 0.1f, 23) };
            _bubble.Fill(band, info.Color);
            var light = new List<Vector2> { T(len * 0.13f, -rad * 0.25f, 24), T(len * 0.76f, -rad * 0.22f, 25), T(len * 0.75f, -rad * 0.42f, 26), T(len * 0.13f, -rad * 0.45f, 27) };
            _bubble.Fill(light, info.Light);
            _bubble.Stroke(body, 5f, Ink, true);

            // ギザギザの口（左のしぼった所）。
            var crimp = new List<Vector2> { T(-len * 0.02f, rad * 1.06f, 30), T(len * 0.14f, rad * 1.04f, 31), T(len * 0.14f, -rad * 1.04f, 32), T(-len * 0.02f, -rad * 1.06f, 33) };
            for (var i = 1; i <= 7; i++)
            {
                var y = Mathf.Lerp(-rad * 1.06f, rad * 1.06f, i / 8f);
                crimp.Add(T((i % 2 == 1 ? -len * 0.06f : -len * 0.02f), y, 40 + i));
            }

            _bubble.FillStroke(crimp, info.Color, Ink, 5f);

            if (puff >= 0f)
            {
                var px = cx0 + cw + Mathf.Lerp(0f, rad * 1.4f, puff);
                var pr = rad * 0.38f * (1f - (puff * 0.5f));
                var blob = new List<Vector2>();
                for (var i = 0; i < 16; i++)
                {
                    var a = i / 16f * Mathf.PI * 2f;
                    blob.Add(T(px + (Mathf.Cos(a) * pr), Mathf.Sin(a) * pr, 60 + i));
                }

                var alpha = 1f - puff;
                _bubble.FillStroke(blob, new Color(1f, 1f, 1f, alpha), new Color(Ink.r, Ink.g, Ink.b, alpha), 3f);
            }
        }

        // ───────── 研修の帯・クリア・はじまるよ ─────────

        private PracticeShape _banner;
        private Text _bannerSmall;
        private Text _bannerBig;
        private float _bannerAt = -10f;
        private int _bannerLesson = -1;
        private RawImage _clearStamp;
        private Text _clearSmall;
        private Text _clearBig;
        private float _clearAt = -10f;
        private int _clearLesson = -1;
        private Image _finaleDim;
        private Text _finaleSmall;
        private Text _finaleBig;
        private int _shownLesson = -1;

        private void BuildOverlay()
        {
            var center = P(FieldX + (FieldW * 0.5f), FieldY + 200f);
            _banner = Shape("Lesson Banner", _root, center, new Vector2(1100f, 200f));
            _bannerSmall = Heading("Small", _banner.transform, string.Empty, 32, new Vector2(0f, 26f), new Vector2(800f, 44f), TextAnchor.MiddleCenter, Ink);
            _bannerBig = Heading("Big", _banner.transform, string.Empty, 60, new Vector2(0f, -28f), new Vector2(900f, 80f), TextAnchor.MiddleCenter, Ink);
            _clearStamp = Raw("Clear Stamp", _root, _cfg.stamp, center, new Vector2(260f, 260f));
            _clearStamp.color = Ok;
            _clearSmall = Heading("Clear Small", _clearStamp.transform, string.Empty, 40, new Vector2(0f, 30f), new Vector2(500f, 56f), TextAnchor.MiddleCenter, Ok);
            _clearBig = Heading("Clear Big", _clearStamp.transform, Ui.clearBig, 62, new Vector2(0f, -36f), new Vector2(500f, 80f), TextAnchor.MiddleCenter, Ok);
            _finaleDim = HowToPlayArt.MakeImage("Dim", _root, null, new Color(0f, 0f, 0f, 0f), Vector2.zero, new Vector2(1920f, 1080f));
            _finaleSmall = Heading("Finale Small", _root, _cfg.allDoneText, 54, P(FieldX + (FieldW * 0.5f), 360f), new Vector2(1000f, 70f), TextAnchor.MiddleCenter, Mint);
            _finaleBig = Heading("Finale Big", _root, _cfg.startText, 120, P(FieldX + (FieldW * 0.5f), 480f), new Vector2(1200f, 150f));
        }

        private void OnLocalLessonCleared(int lesson)
        {
            if (lesson < 0 || lesson >= PracticeSteps.LessonCount || _board.finished)
            {
                return;
            }

            _clearAt = _time;
            _clearLesson = lesson;
            _shake = Mathf.Max(_shake, 0.15f);
        }

        private void TickOverlay()
        {
            var lesson = PracticeSteps.Lesson(LocalStep);
            if (lesson != _shownLesson)
            {
                _shownLesson = lesson;
                if (lesson < PracticeSteps.LessonCount)
                {
                    _bannerLesson = lesson;
                    _bannerAt = _clearAt > _time - 1.3f ? _clearAt + 1.25f : _time;
                }
            }

            // 研修の帯（1.8 秒）。
            var bt = _time - _bannerAt;
            var bannerOn = bt >= 0f && bt < 1.8f && _bannerLesson >= 0;
            _banner.gameObject.SetActive(bannerOn);
            if (bannerOn)
            {
                var k = bt < 0.3f ? Smooth(0f, 0.3f, bt) : bt > 1.5f ? 1f - Smooth(1.5f, 1.8f, bt) : 1f;
                _banner.Clear();
                var body = PracticeShape.Parallelogram(Vector2.zero, new Vector2(1000f, 150f), 50f);
                _banner.Fill(PracticeShape.Parallelogram(new Vector2(10f, -10f), new Vector2(1000f, 150f), 50f), new Color(Ink.r, Ink.g, Ink.b, k));
                _banner.Fill(body, new Color(Pink.r, Pink.g, Pink.b, k), new Color(Mint.r, Mint.g, Mint.b, k), false);
                _banner.Stroke(body, 5f, new Color(Ink.r, Ink.g, Ink.b, k), true);
                _banner.rectTransform.anchoredPosition = P(FieldX + (FieldW * 0.5f), FieldY + 200f) + new Vector2((1f - k) * -160f, 0f);
                _bannerSmall.text = PracticeUiTexts.Format(Ui.bannerFormat, _bannerLesson + 1);
                _bannerBig.text = PracticeScreenSettings.Pick(_cfg.lessonTitles, _bannerLesson);
                var c = new Color(1f, 1f, 1f, k);
                _bannerSmall.color = c;
                _bannerBig.color = c;
            }

            // クリアのハンコ（1.3 秒）。
            var ct = _time - _clearAt;
            var clearOn = ct >= 0f && ct < 1.3f && _clearLesson >= 0;
            _clearStamp.gameObject.SetActive(clearOn);
            if (clearOn)
            {
                var k = EaseOutBack(Mathf.Clamp01(ct / 0.3f));
                _clearStamp.rectTransform.localScale = Vector3.one * k;
                _clearStamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, 9f);
                var a = ct > 1f ? 1f - ((ct - 1f) / 0.3f) : 1f;
                _clearStamp.color = new Color(Ok.r, Ok.g, Ok.b, a);
                _clearSmall.text = PracticeUiTexts.Format(Ui.clearSmallFormat, _clearLesson + 1);
                _clearSmall.color = new Color(1f, 1f, 1f, a);
                _clearBig.color = new Color(1f, 1f, 1f, a);
            }

            // 全員そろった → 「はじまるよ！」（このあと歯磨き粉の幕でゲームへ）。
            var finaleOn = _board.finished && _finishedAt >= 0f;
            var ft = finaleOn ? _time - _finishedAt : -1f;
            _finaleDim.color = new Color(0f, 0f, 0f, finaleOn ? 0.45f * Smooth(0f, 0.3f, ft) : 0f);
            _finaleSmall.enabled = finaleOn;
            _finaleBig.enabled = finaleOn;
            if (finaleOn)
            {
                var k = EaseOutBack(Mathf.Clamp01(ft / 0.35f));
                _finaleSmall.rectTransform.localScale = Vector3.one * k;
                _finaleBig.rectTransform.localScale = Vector3.one * k;
                _finaleSmall.text = _board.AllDone ? _cfg.allDoneText : Ui.timeUp;
            }
        }
    }
}
