using System;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>スキルの見せ方（練習の画面だけの簡単な演出。本番のスキルの動きを短くまねる）。</summary>
    public enum PracticeSkillLook
    {
        /// <summary>太いビーム（赤）。</summary>
        ThickLaser,

        /// <summary>ボトルを投げて、うずで敵を吸い込む（青）。</summary>
        Vortex,

        /// <summary>壁ではね返る矢（黄）。</summary>
        Ricochet,

        /// <summary>歯のまわりを回る玉＋歯の回復（緑）。</summary>
        Satellite
    }

    /// <summary>
    /// キャラごとの、練習の画面での見せ方と話し方（キャラ選択の CharacterDefinition.id で探す）。
    /// セリフは【】で囲んだ所がキャラの色になる。空のセリフは、下の共通のセリフを使う。
    /// </summary>
    [Serializable]
    public sealed class PracticeCharacterLook
    {
        [Tooltip("キャラ選択のキャラの id（alpha・beta など）。")]
        public string characterId = "alpha";

        [Tooltip("練習の画面に出す名前（空ならキャラ選択の名前）。")]
        public string displayName = "";

        [Tooltip("スキルの見せ方。")]
        public PracticeSkillLook skill = PracticeSkillLook.ThickLaser;

        [Tooltip("最初のセリフ。")]
        public string introLine = "";

        [Tooltip("課題ごとのセリフ（Grip・Hold・Release・Overheat・Cool・Bubble・Skill の順）。")]
        public string[] stepLines = Array.Empty<string>();

        [Tooltip("研修1でばい菌を倒したとき（1体目・2体目…の順。足りなければ最後のを使う）。{0} = 倒した数、{1} = あと何体。")]
        public string[] killLines = Array.Empty<string>();

        [Tooltip("オーバーヒートしたとき。")]
        public string overheatLine = "";

        [Tooltip("スキルを出したとき（いくつか書くと順番に使う）。")]
        public string[] skillLines = Array.Empty<string>();

        [Tooltip("研修3でスキルを使い切ったあと、修了までの数秒に出す（空なら共通のセリフ）。")]
        public string afterSkillLine = "";

        [Tooltip("自分が研修を全部終えたとき。")]
        public string doneLine = "";

        [Tooltip("全員そろったとき。")]
        public string allDoneLine = "";

        [Tooltip("左下の顔の窓に出す、立ち絵の切り抜き。x,y = 真ん中（立ち絵の幅・高さに対する割合、y は下から）、w = 幅（立ち絵の幅に対する割合）。高さは窓の形に合わせて決まる（h は使わない）。")]
        public Rect portraitCrop = new Rect(0.5f, 0.8f, 0.6f, 0f);
    }

    /// <summary>ステージごとの、練習フィールドの背景（本番のステージを真上から撮った絵。メニューで作る）。</summary>
    [Serializable]
    public sealed class PracticeStagePlate
    {
        [Tooltip("ステージのシーン名（StageCatalog の sceneName）。")]
        public string sceneName = "Stage1";

        [Tooltip("真上から撮った絵（コア＝絵の真ん中）。Tools › あそびかた › 練習フィールドの絵を作る で作る。")]
        public Texture plate;

        [Tooltip("絵の幅が、ゲームの中の何メートルぶんか。")]
        [Min(1f)]
        public float plateWorldSize = 44f;

        [Tooltip("コアから砲台までの距離（メートル）。")]
        [Min(0.5f)]
        public float turretRadius = 5.9f;
    }

    /// <summary>
    /// 練習の画面（レイアウトB「下部ダイアログ」・2026-11-12）の設定。
    /// 上に横長のフィールド（本番のステージ・コア・砲台・敵の絵で、4人の砲台が全員映る）、右に全員の社員証、下に自分のキャラの顔と吹き出し・やることの一覧。
    /// </summary>
    [Serializable]
    public sealed class PracticeScreenSettings
    {
        [Tooltip("ON = この新しい画面（レイアウトB）。OFF = 前の画面（自分の区画を左に大きく）。")]
        public bool enabled = true;

        [Tooltip("ON = フィールドに、練習のシーン（本物の砲台・敵・スマッシュボール・スキル）を重ねて映す（2026-11-13）。\nOFF、またはシーンがビルドの一覧に無いときは、絵で描いた練習のフィールド。")]
        public bool useTutorialScene = true;

        [Tooltip("練習のシーンの名前（Tools › あそびかた › 練習のシーンを作る で作る。ビルドの一覧に入っていること）。")]
        public string tutorialSceneName = "PracticeTutorial";

        [Header("フィールドの絵（本番と同じ絵）")]
        [Tooltip("ステージごとの背景。ステージが分からないときは一番上を使う。")]
        public PracticeStagePlate[] stagePlates = Array.Empty<PracticeStagePlate>();

        [Tooltip("真ん中のコア（本番のコアを撮った絵）。")]
        public Texture core;

        [Tooltip("コアの絵の高さ（メートル）。")]
        [Min(0.1f)]
        public float coreWorldHeight = 2.4f;

        [Tooltip("敵（ばい菌）の絵。")]
        public Texture enemy;

        [Tooltip("敵の大きさ（メートル）。")]
        [Min(0.1f)]
        public float enemyWorldSize = 1.0f;

        [Tooltip("砲台の絵の高さ（メートル。本番の砲台の絵と同じ）。")]
        [Min(0.1f)]
        public float turretWorldHeight = 3.06f;

        [Tooltip("フィールドの 1 メートルを何ピクセル（1920×1080 として）で描くか。")]
        [Min(5f)]
        public float pixelsPerMeter = 36f;

        [Tooltip("泡（スマッシュボール）の絵。")]
        public Texture bubble;

        [Header("画面の絵")]
        public Texture background;
        public Texture logo;
        public Texture halftone;
        public Texture stripes;

        [Tooltip("開いた手と握った手が左右に並んだ絵。")]
        public Texture hand;

        [Tooltip("できたときのハンコの輪。")]
        public Texture stamp;

        public Texture sparkle;

        [Tooltip("今の課題を指す矢印。")]
        public Texture arrow;

        [Tooltip("1P〜4P の札（上から順）。")]
        public Texture[] seatBadges = new Texture[4];

        [Header("キャラごと")]
        public PracticeCharacterLook[] characters =
        {
            // 赤星レイザ: 自信家で負けず嫌い。強気で仕切りたがるが、面倒見はいい。「〜なさい！」「〜わよ！」
            new PracticeCharacterLook
            {
                characterId = "alpha", displayName = "赤星レイザ", skill = PracticeSkillLook.ThickLaser,
                portraitCrop = new Rect(0.5f, 0.8f, 0.95f, 0f), // 2026-11-19: 左下の大きい立ち絵（450×340）に合わせた
                introLine = "新人研修、始めるわよ！ 真ん中の【歯】を守れなきゃ話にならないんだから！",
                stepLines = new[]
                {
                    "まずは【ギュッと握って】みなさい！ 砲台から【歯磨き粉】が出るわ！",
                    "その【歯磨き粉】をばい菌に当てれば倒せるの！ 握ったまま【3体】片付けなさい！",
                    "3体撃破、やるじゃない！ 【パッと離す】と歯磨き粉が止まるわ。",
                    "次は【オーバーヒート】するまで握りっぱなしにしてみなさい！ 限界を知っておくのよ！",
                    "【手を離して】冷ますの！ 冷えたら、また撃てるわ",
                    "【泡】を壊すと、次の攻撃が【スキル】になるわ！ 研修では【あなたの泡】を割りなさい！",
                    "次の攻撃は【スキル】よ！ 一度離して【もう一度握る】と、【極太レーザー】でまとめてなぎ払えるわ！"
                },
                killLines = new[] { "【{0}体】撃破！ あと{1}体、さっさと片付けるわよ！", "【{0}体】目！ あと{1}体、気を抜くんじゃないわよ！" },
                overheatLine = "【オーバーヒート】ね！ 少しの間撃てないわ、待ちなさい！",
                skillLines = new[] { "まとめてかかってきなさい！", "【蹴散らして】あげるわ！" },
                afterSkillLine = "やるじゃない！ それが【スキル】よ。本番でもガンガン使いなさい！",
                doneLine = "私にかかれば楽勝よ！ 待つ間は【自由に撃って】いいわ",
                allDoneLine = "全員そろったわね！ 本番、勝ちに行くわよ！"
            },

            // 青波雫: 落ち着いていて口数が少ない。仕事人で、短く的確に言う。「〜ね。」「〜よ。」
            new PracticeCharacterLook
            {
                characterId = "beta", displayName = "青波雫", skill = PracticeSkillLook.Vortex,
                portraitCrop = new Rect(0.5f, 0.83f, 1f, 0f), // 2026-11-19: 左下の大きい立ち絵（450×340）に合わせた
                introLine = "新人研修を始める。真ん中の【歯】をばい菌から守る。それが仕事よ。",
                stepLines = new[]
                {
                    "【握って】。砲台から【歯磨き粉】が出る。",
                    "【歯磨き粉】を当てれば、ばい菌は倒せる。握ったまま【3体】。",
                    "3体。上出来ね。【離せば】歯磨き粉は止まる。",
                    "次は【オーバーヒート】するまで握り続けて。限界を知るのも仕事よ。",
                    "【手を離して】冷ます。冷えれば、また撃てる。",
                    "【泡】を壊すと、次の攻撃が【スキル】になる。研修では【あなたの泡】を割って。",
                    "次の攻撃は【スキル】。【長押し】するほど遠くへ、【離す】と投げる。ボトルは【3本】、全部投げて。"
                },
                killLines = new[] { "【{0}体】。あと{1}体。", "【{0}体】。…あと{1}体よ。" },
                overheatLine = "【オーバーヒート】ね。少しの間、撃てない。焦らないで。",
                skillLines = new[] { "全部【流して】あげる。", "【掃除】の時間よ。" },
                afterSkillLine = "上出来ね。それが【スキル】。本番でも頼りにしてる。",
                doneLine = "簡単な仕事ね。待つ間は【自由に撃って】いいわ。",
                allDoneLine = "全員そろったわね。…いい仕事をしましょう。"
            },

            // パチ: 元気でがんばり屋。ちょっと気が弱くて慌てやすいけど、すぐ笑顔に戻る。擬音が多い。「〜だよ！」「〜ね！」
            new PracticeCharacterLook
            {
                characterId = "delta", displayName = "パチ", skill = PracticeSkillLook.Ricochet,
                portraitCrop = new Rect(0.5f, 0.56f, 1f, 0f), // 2026-11-19: 左下の大きい立ち絵（450×340）に合わせた
                introLine = "し、新人研修はじめるよ〜！ 真ん中の【歯】を ばい菌から守ろうね！",
                stepLines = new[]
                {
                    "まずは【ギュッと握って】みて！ 【歯磨き粉】がむにゅ〜って出るよ！",
                    "【歯磨き粉】をばい菌に当てると、やっつけられるよ！ 握りっぱなしで【3体】いこ〜！",
                    "3体やったね！ 【パッと離す】と歯磨き粉が止まるよ！",
                    "次はね、【オーバーヒート】するまで握りっぱなしにしてみよ！ ちょっとドキドキ…！",
                    "わわっ、【手を離して】冷まそう！ 冷えたらまた撃てるよ！",
                    "【泡】をパチン！って壊すと、次の攻撃が【スキル】になるよ！ 研修では【あなたの泡】をねらってね！",
                    "次の攻撃は【スキル】だよ！ 一回離して【もう一回握る】と、壁で【はね返る矢】がびゅーんって飛ぶよ！"
                },
                killLines = new[] { "やった、【{0}体】！ あと{1}体だよ〜！", "【{0}体】目！ あと{1}体、がんばろ！" },
                overheatLine = "【オーバーヒート】しちゃった〜！ ちょっとだけ撃てないよ、待ってて！",
                skillLines = new[] { "【任せて】！", "【とりゃー】！" },
                afterSkillLine = "すごいすごい！ それが【スキル】だよ！ 本番でも使ってね！",
                doneLine = "無事に生還！ みんなを待つ間は【自由に撃って】いいよ！",
                allDoneLine = "みんなのおかげ！ 本番もがんばろうね！"
            },

            // 緑谷ミラ: 物腰やわらかで上品なお姉さん。ほほえみながら面倒を見るが、敵には容赦がない（「愛」が重い）。会社に忠実。「〜わ」「〜ね」「〜ちょうだい」
            new PracticeCharacterLook
            {
                characterId = "gamma", displayName = "緑谷ミラ", skill = PracticeSkillLook.Satellite,
                portraitCrop = new Rect(0.56f, 0.73f, 0.85f, 0f), // 2026-11-19: 左下の大きい立ち絵（450×340）に合わせた
                introLine = "ようこそBBカンパニーへ。研修を始めましょうね。守るのは真ん中の【歯】よ。",
                stepLines = new[]
                {
                    "まずは【ギュッと握って】ごらんなさい。砲台から【歯磨き粉】が出るわ。",
                    "その【歯磨き粉】を当てれば、ばい菌は倒せるの。握ったまま【3体】、片付けてちょうだい。",
                    "ふふ、3体。上手ね。【手を離す】と歯磨き粉が止まるわ。",
                    "次は【オーバーヒート】するまで握り続けてみて。どこまで頑張れるか、知っておきましょうね。",
                    "【手を離して】冷ましましょう。無理はだめよ？",
                    "【泡】を壊すとね、次の攻撃が【スキル】になるの。研修では【あなたの泡】を割ってちょうだい。",
                    "次の攻撃は【スキル】よ。一度離して【もう一度握って】。歯がばい菌を撃って【回復】もするの。握るほど強くなるわ。"
                },
                killLines = new[] { "ふふ、【{0}体】。あと{1}体よ。", "【{0}体】目。あと{1}体…逃がさないでね。" },
                overheatLine = "あら、【オーバーヒート】ね。少しお休み、今は撃てないわ。",
                skillLines = new[] { "ちょっと…【お痛】が過ぎるわよ。", "【静かに】しなさい。", "これが私の【「愛」】よ。" },
                afterSkillLine = "ふふ、上手ね。それが【スキル】よ。本番でも見せてちょうだい。",
                doneLine = "よくできました。待つ間は【自由に撃って】いいわよ。",
                allDoneLine = "全員そろったわね。BBカンパニーに【勝利】を。"
            }
        };

        [Header("文字（【】で囲んだ所はキャラの色）")]
        public string title = "新人研修";
        public string[] lessonTitles = { "歯を守れ", "オーバーヒートに注意", "泡でスキル" };

        [Tooltip("課題ごとの名前（Grip・Hold・Release・Overheat・Cool・Bubble・Skill の順）。{0} は研修1で倒す数。")]
        public string[] taskLabels = { "握る", "握り続けて 敵を{0}体倒す", "離す", "握り続けてオーバーヒート", "離して冷ます", "「あなたの泡」を割る", "もう一度握って スキル" };

        [Tooltip("課題ごとの小さい説明（同じ順）。")]
        public string[] taskHints = { "歯磨き粉が出る", "ばい菌に歯磨き粉を当てる", "歯磨き粉が止まる", "熱がたまる", "冷えたら また撃てる", "本番の泡は 先に割った人のもの（研修だけ 1人1つ）", "スキルを最後まで使うと 研修修了" };

        [Tooltip("最初に出すセリフ（この秒数だけ）。")]
        public string introLine = "新人研修をはじめるよ！ 真ん中の【歯】を ばい菌から守ろう！";

        [Min(0f)]
        public float introSeconds = 3.2f;

        [Tooltip("セリフの文字が出る速さ（1秒に何文字）。")]
        [Min(1f)]
        public float lineCharsPerSecond = 22f;

        [Tooltip("セリフを出し終わってから、次のセリフに替えるまで最低この秒数は見せる（読む時間）。課題が続けて進んでも、この間は替えない（オーバーヒートの警告だけはすぐ割り込む）。")]
        [Min(0f)]
        public float lineReadSeconds = 1.8f;

        [Tooltip("課題ごとのセリフ（Grip・Hold・Release・Overheat・Cool・Bubble・Skill の順）。")]
        public string[] stepLines =
        {
            "まずは【ギュッと握って】みて！ 砲台から【歯磨き粉】が出るよ！",
            "【歯磨き粉】をばい菌に当てると倒せる！ 握ったまま【3体】倒そう！",
            "3体倒した！ 【パッと離す】と、歯磨き粉が止まるよ！",
            "次は【オーバーヒート】するまで握りっぱなしにしてみよう！",
            "【手を離して】冷まそう！ 冷えたら また撃てる",
            "【泡】を壊すと、次の攻撃が【スキル】になる！ 研修では【あなたの泡】を割ろう！",
            "次の攻撃は【スキル】！ 一度離して【もう一度握る】と発動！"
        };

        [Tooltip("研修1でばい菌を倒したとき（キャラのセリフが空のとき）。{0} = 倒した数、{1} = あと何体。")]
        public string[] killLines = { "【{0}体】倒した！ あと{1}体！" };

        public string overheatLine = "【オーバーヒート】！ しばらく撃てない！";
        public string afterSkillLine = "それが【スキル】！ 本番でも使ってね！";
        public string doneLine = "研修修了！ 他の社員を待つ間は【自由に撃って】OK";
        public string allDoneLine = "全員そろった！ 本番いくよ！";
        public string waitingText = "他の社員を待ってね…";
        public string allDoneText = "全員 研修修了！";
        public string startText = "はじまるよ！";
        public string localMark = "あなた";
        public string idCardHeader = "BBカンパニー 社員証";
        public string completeStamp = "修了";

        /// <summary>写し（中の配列は同じ物を指す）。画面が絵を埋めるときに使い、設定のファイルは変えない。</summary>
        public PracticeScreenSettings Copy() => (PracticeScreenSettings)MemberwiseClone();

        public PracticeCharacterLook FindCharacter(string id)
        {
            if (characters != null)
            {
                foreach (var look in characters)
                {
                    if (look != null && look.characterId == id)
                    {
                        return look;
                    }
                }
            }

            return null;
        }

        public PracticeStagePlate FindPlate(string sceneName)
        {
            if (stagePlates == null || stagePlates.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(sceneName))
            {
                foreach (var plate in stagePlates)
                {
                    if (plate != null && plate.sceneName == sceneName && plate.plate != null)
                    {
                        return plate;
                    }
                }
            }

            foreach (var plate in stagePlates)
            {
                if (plate != null && plate.plate != null)
                {
                    return plate;
                }
            }

            return null;
        }

        /// <summary>キャラのセリフがあればそれ、無ければ共通のセリフ。</summary>
        public static string Line(string own, string common) => string.IsNullOrEmpty(own) ? common : own;

        public static string Pick(string[] list, int index) => list != null && index >= 0 && index < list.Length ? list[index] : string.Empty;
    }
}
