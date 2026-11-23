using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ウェーブの発表で、その試合で初めて出てくる敵を「NEW ENEMY」のカードで紹介する（2026-10-24、初めての人向け）。
    /// どの敵が初登場かは <see cref="WaveIntro.NewTypes"/>（紹介文 <see cref="EnemyTypeDefinition.introText"/> のある敵だけ）。
    /// カードの絵は introIcon、無ければ群衆の絵の1コマ目、それも無ければ仮絵の色の丸。
    /// UIスタジオの「ウェーブの表示を入れる」が作る「WaveNewEnemies」に付く（出し入れは wave.announcing）。
    /// </summary>
    [AddComponentMenu("UI Studio/初登場の敵のカード (WaveNewEnemyCards)")]
    public sealed class WaveNewEnemyCards : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Card
        {
            public GameObject root;
            public RawImage picture;
            public Image fallback;
            public Text title;
            public Text body;

            /// <summary>紹介文のいちばん大きい文字の大きさ（文ごとに1行に収めるため小さくした後、次の敵で元に戻す）。</summary>
            [System.NonSerialized] public int bodyMaxSize;

            [System.NonSerialized] public int bodyMinSize;
        }

        /// <summary>文ごとに1行に収めるとき、ここまでは小さくしてよい。</summary>
        private const int SentenceLineMinSize = 14;

        public Card[] cards = new Card[0];

        [Tooltip("NEW ENEMY のカードが出た瞬間に鳴らす効果音（初めての敵がいるウェーブだけ。サウンドスタジオ N4）。")]
        public FortressEffect onCardsShown = new FortressEffect { tintWithPlayerColor = false };

        private int _shownWave = -2;
        private WavePlan _shownPlan;
        private int _shownCount;
        private bool _wasShowing;

        private void Update()
        {
            // 出し入れ（何秒後に出すか）は値 wave.showNewEnemies で。中身は発表が始まった時点で入れておく。
            var announcing = FortressWaves.IsActive && FortressWaves.Phase == WavePhase.Announce && WaveDisplayTiming.Current.showNewEnemyCards;
            var plan = FortressWaves.Plan;
            var wave = announcing ? FortressWaves.WaveIndex : -1;

            // カードが見え始めた瞬間（値 wave.showNewEnemies が ON になった瞬間）に1回だけ鳴らす。
            var showing = announcing && _shownCount > 0 && UiValues.GetBool(FortressUiKeys.WaveShowNewEnemies);
            if (showing && !_wasShowing)
            {
                FortressEffectPlayer.PlaySound(onCardsShown, Vector3.zero);
            }

            _wasShowing = showing;
            if (wave == _shownWave && plan == _shownPlan)
            {
                return;
            }

            _shownWave = wave;
            _shownPlan = plan;
            var types = announcing && plan != null ? WaveIntro.NewTypes(plan.waves, wave) : new List<EnemyTypeDefinition>();
            _shownCount = System.Math.Min(types.Count, cards.Length);
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card?.root == null)
                {
                    continue;
                }

                var type = i < types.Count ? types[i] : null;
                card.root.SetActive(type != null);
                if (type != null)
                {
                    Fill(card, type);
                }
            }
        }

        private static void FitText(Text text, int minSize)
        {
            if (text.resizeTextForBestFit)
            {
                return;
            }

            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(minSize, text.fontSize);
            text.resizeTextForBestFit = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // 行の頭に来てはいけない文字（句読点・閉じかっこ・小さい字・伸ばし棒など）と、行の終わりに来てはいけない文字（開きかっこ）。
        private const string NoLineStart = "。、，．・：；？！ー…‥）」』】〕〉》］｝ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ!?.,:;)]}%";
        private const string NoLineEnd = "（「『【〔〈《［｛([{";

        /// <summary>
        /// 文字を入れ、折り返しで行の頭に「。」などが来るときは、その前の1文字ごと次の行へ送る（日本語の禁則処理。
        /// Unity の Text は日本語をどこでも折り返すので、ここで改行を足す。2026-11-18、ユーザー依頼「2行目の先頭に。が来てキモい」）。
        /// </summary>
        private static void SetTextWithKinsoku(Text text, string value)
        {
            value = BreakAfterSentences(value);
            text.text = value;
            var size = text.rectTransform.rect.size;
            if (string.IsNullOrEmpty(value) || size.x <= 0f || text.font == null)
            {
                return;
            }

            var s = value;
            var generator = new TextGenerator();

            // まず、文ごとにちょうど1行（途中で折り返さない）に収まる、いちばん大きい文字の大きさを探す。
            var maxSize = text.resizeTextForBestFit ? text.resizeTextMaxSize : text.fontSize;
            var minSize = Mathf.Min(SentenceLineMinSize, text.resizeTextMinSize);
            for (var fontSize = maxSize; fontSize >= minSize; fontSize--)
            {
                var settings = text.GetGenerationSettings(size);
                settings.fontSize = fontSize;
                settings.resizeTextForBestFit = false;
                generator.Populate(s, settings);
                if (OnlyExplicitBreaks(s, generator))
                {
                    if (text.resizeTextForBestFit)
                    {
                        // 一番小さい大きさより小さくしたときは、そちらも下げる（Unity は最小より小さくしない）。
                        text.resizeTextMaxSize = fontSize;
                        text.resizeTextMinSize = Mathf.Min(text.resizeTextMinSize, fontSize);
                    }
                    else
                    {
                        text.fontSize = fontSize;
                    }

                    text.text = s;
                    return;
                }
            }

            // 収まらないときは今までどおり折り返し、行の頭の「。」などだけ直す。
            for (var guard = 0; guard < 8; guard++)
            {
                generator.Populate(s, text.GetGenerationSettings(size));
                var lines = generator.lines;
                var breakAt = -1;
                for (var i = 1; i < lines.Count; i++)
                {
                    var start = lines[i].startCharIdx;
                    if (start <= 1 || start >= s.Length || s[start - 1] == '\n')
                    {
                        continue;
                    }

                    if (NoLineStart.IndexOf(s[start]) >= 0 || NoLineEnd.IndexOf(s[start - 1]) >= 0)
                    {
                        breakAt = start - 1;
                        break;
                    }
                }

                // 前の行が1文字だけになるときは送らない（空の行ができるだけなので）。
                if (breakAt <= 0 || s[breakAt - 1] == '\n')
                {
                    break;
                }

                s = s.Insert(breakAt, "\n");
            }

            text.text = s;
        }

        /// <summary>全部の行が出ていて、行の切れ目がどれも自分で入れた改行か（勝手な折り返し・はみ出して消えた行が無いか）。</summary>
        private static bool OnlyExplicitBreaks(string s, TextGenerator generator)
        {
            var explicitLines = 1;
            foreach (var c in s)
            {
                if (c == '\n')
                {
                    explicitLines++;
                }
            }

            var lines = generator.lines;
            if (lines.Count != explicitLines)
            {
                return false;
            }

            for (var i = 1; i < lines.Count; i++)
            {
                var start = lines[i].startCharIdx;
                if (start <= 0 || start > s.Length || s[start - 1] != '\n')
                {
                    return false;
                }
            }

            return true;
        }

        // 文の終わりの字。この後にまだ文が続くなら、そこで改行する。
        private const string SentenceEnd = "。！？!?";

        /// <summary>
        /// 文の終わり（「。」「！」「？」）の後にまだ文が続くときは、そこで改行する（「〜まっすぐ来る。／ビームで倒そう！」。2026-11-18、ユーザー依頼）。
        /// 「！！」のように続くときは、最後の字の後で改行する。
        /// </summary>
        private static string BreakAfterSentences(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var sb = new System.Text.StringBuilder(value.Length + 4);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                sb.Append(c);
                if (SentenceEnd.IndexOf(c) < 0 || i + 1 >= value.Length)
                {
                    continue;
                }

                var next = value[i + 1];
                if (SentenceEnd.IndexOf(next) >= 0 || NoLineStart.IndexOf(next) >= 0 || next == '\n')
                {
                    continue;
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static void Fill(Card card, EnemyTypeDefinition type)
        {
            // 長い名前・紹介文でも枠からはみ出さないよう、文字を小さくして収める（紹介文は上限を超えた分を「…」に）。
            if (card.title != null)
            {
                FitText(card.title, 20);
                card.title.text = string.IsNullOrEmpty(type.displayName) ? type.name : type.displayName;
            }

            if (card.body != null)
            {
                FitText(card.body, 14);
                if (card.bodyMaxSize <= 0)
                {
                    card.bodyMaxSize = card.body.resizeTextMaxSize;
                    card.bodyMinSize = card.body.resizeTextMinSize;
                }

                card.body.resizeTextMaxSize = card.bodyMaxSize;
                card.body.resizeTextMinSize = card.bodyMinSize;
                SetTextWithKinsoku(card.body, type.IntroTextForCard);
            }

            Texture texture = null;
            var uv = new Rect(0f, 0f, 1f, 1f);
            if (type.introIcon != null)
            {
                texture = type.introIcon.texture;
                var r = type.introIcon.textureRect;
                uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height);
            }
            else if (type.swarm != null && type.swarm.spriteSheet != null)
            {
                // 群衆の絵: 横＝コマ、縦＝向き（一番上の段＝右向き）。1コマ目の右向き。
                texture = type.swarm.spriteSheet;
                var columns = Mathf.Max(1, type.swarm.frameCount);
                var rows = Mathf.Max(1, type.swarm.directionCount);
                uv = new Rect(0f, 1f - 1f / rows, 1f / columns, 1f / rows);
            }

            if (card.picture != null)
            {
                card.picture.texture = texture;
                card.picture.uvRect = uv;
                card.picture.enabled = texture != null;
            }

            if (card.fallback != null)
            {
                card.fallback.enabled = texture == null;
                card.fallback.color = type.placeholderColor;
            }
        }
    }
}
