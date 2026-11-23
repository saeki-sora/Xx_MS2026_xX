using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習の画面（新人研修）の、セリフ以外の画面の文字（2026-11-19）。{0}・{1} には数や名前が入る（消さないこと）。
    /// </summary>
    [Serializable]
    public sealed class PracticeUiTexts
    {
        [Header("フィールド左上「いまの研修」の札")]
        [Tooltip("{0} = 今の研修の番号、{1} = 研修の数。")]
        public string nowLessonFormat = "いまの研修　{0}／{1}";
        public string nowFinished = "研修修了！";
        public string nowFreePlay = "▶ 待つ間は 自由に撃って練習";

        [Tooltip("フィールドの左上の札。")]
        public string fieldTag = "ためしてみてね";

        [Header("フィールドの札・目印")]
        public string enemyCardName = "ばい菌（敵）";
        public string enemyCardHint = "真ん中の歯に向かってくる！\n歯磨き粉を当てて倒そう";

        [Tooltip("ばい菌を倒すたびに浮かぶ文字。{0} = 倒した数、{1} = 倒す数。")]
        public string killFloatFormat = "ばい菌 {0}/{1}";

        [Tooltip("自分の泡の上の札。")]
        public string myBubble = "あなたの泡";

        public string fieldChip = "研修フィールド ・ 4人で操作中";
        public string fieldLoading = "研修フィールドを準備中…";

        [Header("研修2の熱のゲージ")]
        public string heatOverheated = "オーバーヒート！ 冷めるまで撃てない";

        [Tooltip("{0} = 熱（%）。")]
        public string heatCoolFormat = "熱 {0}%　離して冷まそう";

        [Tooltip("{0} = 熱（%）。8 割を超えたとき。")]
        public string heatNearFormat = "熱 {0}%　もうすぐオーバーヒート！";

        [Tooltip("{0} = 熱（%）。")]
        public string heatNormalFormat = "熱 {0}%　握り続けると たまる";

        [Header("右上「いまの研修」の欄")]
        [Tooltip("研修の名前の下の札。")]
        public string panelChip = "練習";

        [Tooltip("時間制限が無いときの、右上の丸の中の数。{0} = 研修の番号（{0:00} なら 01・02・03）。")]
        public string lessonNumberFormat = "{0:00}";

        [Tooltip("時間制限があるときの、右上の丸の中の数（残りの秒数）。{0} = 残りの秒数（{0:00} なら 60・09・00）。")]
        public string timerFormat = "{0:00}";

        [Tooltip("見出しの札。{0} = 研修の番号。")]
        public string lessonChipFormat = "研修{0}";

        [Tooltip("今の課題の行に付く札。")]
        public string nowHere = "いまここ！";

        [Tooltip("手の絵の下の文字（握る・握り続ける・離す・オーバーヒート・冷ます・泡・スキル の順）。")]
        public string[] captions = { "ギュッ！", "ギュ〜〜", "パッ！", "ギュ〜〜", "パッ", "泡を割る", "またギュッ！" };

        [Tooltip("絵で描いたフィールドのときの、研修1「握り続ける」の課題の名前（本物の敵がいないので）。")]
        public string holdLabelNoEnemies = "握り続ける";

        public string listDoneTitle = "研修修了！";
        public string listFreePlay = "待つ間は 自由に撃って練習できる";
        public string listAllDoneSub = "まもなく本番";

        [Header("右下「みんなの研修」")]
        public string rosterTitle = "準備中 ...";

        [Tooltip("全員が研修を終えたときの見出し。")]
        public string rosterAllReady = "準備完了！";

        [Tooltip("{0} = 修了した人数、{1} = いる人数。")]
        public string rosterCountFormat = "{0}/{1}";

        public string absent = "不参加";

        [Header("研修の始まり・クリア・最後")]
        [Tooltip("研修の始まりの帯の小さい文字。{0} = 研修の番号。")]
        public string bannerFormat = "研修 {0}";

        [Tooltip("クリアのハンコの小さい文字。{0} = 研修の番号。")]
        public string clearSmallFormat = "研修{0}";

        public string clearBig = "クリア！";
        public string timeUp = "時間になったよ！";

        [Header("絵で描いたフィールド（練習のシーンを使わないとき）")]
        public string fakeBubbleHint = "泡を割れ！";
        public string burnLabel = "オーバーヒート！";

        [Tooltip("{0} = 席の番号。")]
        public string skillReadyFloatFormat = "{0}P スキル READY！";

        [Tooltip("自分が修了したとき。{0} = 席の番号、{1} = 名前。")]
        public string doneFloatFormat = "{0}P {1} 研修修了！";

        [Tooltip("ほかの人が修了したとき。{0} = 席の番号。")]
        public string otherDoneFloatFormat = "{0}P 研修修了！";

        [Tooltip("スキルの掛け声が1つも無いキャラのとき。")]
        public string skillFallback = "スキル発動！";

        /// <summary>{0}・{1} に入れる（書き方を間違えていても止まらない）。</summary>
        public static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }

    /// <summary>
    /// 練習の画面（新人研修）の文字の置き場（Resources/PracticeTextLibrary・2026-11-19）。
    /// キャラのセリフ・研修の名前とやること・画面の文字を、ここで全部差し替えられる（最初は今の文字が入っている）。
    /// Tools › あそびかた › 研修の絵と文字（差し替え） の画面で直すのがわかりやすい。
    /// このファイルが無いときは、コードに書いた文字（<see cref="PracticeScreenSettings"/>）を使う。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/あそびかた/練習の画面の文字", fileName = "PracticeTextLibrary")]
    public sealed class PracticeTextLibrary : ScriptableObject
    {
        public const string ResourcePath = "PracticeTextLibrary";

        private static readonly PracticeScreenSettings Defaults = new PracticeScreenSettings();

        [Header("キャラごとのセリフ（【】で囲んだ所はキャラの色）")]
        public PracticeCharacterLook[] characters = new PracticeScreenSettings().characters;

        [Header("共通のセリフ（キャラのセリフが空のときに使う）")]
        public string introLine = Defaults.introLine;

        [Tooltip("握る・握り続ける・離す・オーバーヒート・冷ます・泡・スキル の順。")]
        public string[] stepLines = (string[])Defaults.stepLines.Clone();

        public string[] killLines = (string[])Defaults.killLines.Clone();
        public string overheatLine = Defaults.overheatLine;
        public string afterSkillLine = Defaults.afterSkillLine;
        public string doneLine = Defaults.doneLine;
        public string allDoneLine = Defaults.allDoneLine;

        [Header("研修の名前と、やること")]
        [Tooltip("研修1〜3の名前。")]
        public string[] lessonTitles = (string[])Defaults.lessonTitles.Clone();

        [Tooltip("課題の名前（握る・握り続ける・離す・オーバーヒート・冷ます・泡・スキル の順）。{0} は研修1で倒す数。")]
        public string[] taskLabels = (string[])Defaults.taskLabels.Clone();

        [Tooltip("課題の小さい説明（同じ順）。")]
        public string[] taskHints = (string[])Defaults.taskHints.Clone();

        [Header("いろいろな文字")]
        public string waitingText = Defaults.waitingText;
        public string allDoneText = Defaults.allDoneText;
        public string localMark = Defaults.localMark;

        [Tooltip("全員そろって本番へ進むときの大きい文字。")]
        public string startText = Defaults.startText;

        public PracticeUiTexts ui = new PracticeUiTexts();

        private static PracticeTextLibrary _loaded;

        public static PracticeTextLibrary Load()
        {
            if (_loaded == null)
            {
                _loaded = Resources.Load<PracticeTextLibrary>(ResourcePath);
            }

            return _loaded;
        }

        /// <summary>画面の文字（このファイルが無ければ、コードに書いた文字）。</summary>
        public static PracticeUiTexts Ui
        {
            get
            {
                var lib = Load();
                return lib != null && lib.ui != null ? lib.ui : FallbackUi;
            }
        }

        private static readonly PracticeUiTexts FallbackUi = new PracticeUiTexts();

        /// <summary>設定の写しの文字を、ここの文字にする（ここが空の所は設定の文字のまま）。</summary>
        public static void Apply(PracticeScreenSettings copy)
        {
            var lib = Load();
            if (lib == null || copy == null)
            {
                return;
            }

            if (lib.characters != null && lib.characters.Length > 0)
            {
                copy.characters = lib.characters;
            }

            copy.introLine = Pick(lib.introLine, copy.introLine);
            copy.stepLines = Pick(lib.stepLines, copy.stepLines);
            copy.killLines = Pick(lib.killLines, copy.killLines);
            copy.overheatLine = Pick(lib.overheatLine, copy.overheatLine);
            copy.afterSkillLine = Pick(lib.afterSkillLine, copy.afterSkillLine);
            copy.doneLine = Pick(lib.doneLine, copy.doneLine);
            copy.allDoneLine = Pick(lib.allDoneLine, copy.allDoneLine);
            copy.lessonTitles = Pick(lib.lessonTitles, copy.lessonTitles);
            copy.taskLabels = Pick(lib.taskLabels, copy.taskLabels);
            copy.taskHints = Pick(lib.taskHints, copy.taskHints);
            copy.waitingText = Pick(lib.waitingText, copy.waitingText);
            copy.allDoneText = Pick(lib.allDoneText, copy.allDoneText);
            copy.localMark = Pick(lib.localMark, copy.localMark);
            copy.startText = Pick(lib.startText, copy.startText);
        }

        private static string Pick(string mine, string fallback) => string.IsNullOrEmpty(mine) ? fallback : mine;

        private static string[] Pick(string[] mine, string[] fallback) => mine != null && mine.Length > 0 ? mine : fallback;
    }
}
