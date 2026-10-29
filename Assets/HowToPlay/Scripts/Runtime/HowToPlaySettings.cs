using MS2026.Fortress;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// あそびかた（2026-10-29）の設定。Resources/HowToPlaySettings に置く（無ければ初期値で動く）。
    /// ・練習: キャラ選択・ステージ抽選の後、各自が自分の区画で「握る → 思いきり握る → 離して冷ます」を試す。全員できたら（または時間切れで）ゲームへ。
    /// ・ヒント: ゲームへ進む歯磨き粉の幕が覆っている間に、歯みがきタイムのカードを出す（最低秒数は幕を上げない）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/あそびかた/あそびかたの設定", fileName = "HowToPlaySettings")]
    public sealed class HowToPlaySettings : ScriptableObject
    {
        public const string ResourcePath = "HowToPlaySettings";

        private static HowToPlaySettings _loaded;

        [Header("練習（ステージ抽選の後、握って試す）")]
        [Tooltip("OFF にすると練習を出さず、ステージ抽選の後すぐゲームへ進む。")]
        public bool practiceEnabled = true;

        [Tooltip("全員できていなくても、この秒数で練習を終えてゲームへ進む。")]
        [Min(1f)]
        public float timeLimitSeconds = 20f;

        [Tooltip("全員できた（または時間切れ）後、ゲームへの幕を出すまでの秒数（「OK!」を見せる間）。")]
        [Min(0f)]
        public float finishDelaySeconds = 1.2f;

        public PracticeRules rules = new PracticeRules();

        [Header("練習の文字")]
        public string title = "握って試そう";
        public string finishedText = "はじまるよ！";
        public string footer = "全員できたら はじまる";
        [Tooltip("3つの課題の名前（上から順）。")]
        public string[] stepLabels = { "握ってみる", "思いきり握る", "離して冷ます" };
        public string doneText = "OK!";
        public string overheatText = "オーバーヒート！";
        public string heatLabel = "熱";
        public string localMark = "あなた";
        public string absentText = "参加していません";

        [Header("練習の見た目")]
        [Tooltip("ビームの模様（空なら白一色）。ゲームの歯磨き粉ビームと同じ縞の絵を使う。")]
        public Texture beamTexture;

        [Tooltip("描く順番。ステージ抽選より上、歯磨き粉の幕（950）より下。")]
        public int sortingOrder = 920;

        [Header("練習の音（各PCが自分の分だけ鳴らす）")]
        [Tooltip("自分の課題が1つできたとき（最後の1つは下の「自分が全部できたとき」）。")]
        public FortressEffect onStepDone = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("自分が3つ全部できたとき。")]
        public FortressEffect onLocalDone = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("自分の砲台がオーバーヒートしたとき。")]
        public FortressEffect onOverheat = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("練習が終わってゲームへ進むとき。")]
        public FortressEffect onFinished = new FortressEffect { tintWithPlayerColor = false };

        [Header("読み込み中のヒント（ゲームへの幕の上）")]
        [Tooltip("OFF にするとヒントを出さず、読み込みが終わればすぐ幕を上げる。")]
        public bool tipEnabled = true;

        [Tooltip("読み込みが早く終わっても、ヒントをこの秒数は見せる（幕を上げない）。")]
        [Min(0f)]
        public float tipMinSeconds = 4f;

        public string tipBadge = "HINT";
        public string tipTitle = "WAVEの合間は 歯みがきタイム！";

        [TextArea(2, 4)]
        public string tipBody = "にぎにぎして 歯をみがこう\nみがくほど歯が回復、あまった分はコーティングになる";

        [Tooltip("カードの左の絵（空なら白い丸）。")]
        public Sprite tipPicture;

        [Tooltip("絵の横に交互に出る文字。")]
        public string tipGripText = "ギュッ";

        [Tooltip("描く順番。歯磨き粉の幕（950）より上。")]
        public int tipSortingOrder = 960;

        [Tooltip("ヒントが出たとき。")]
        public FortressEffect onTipShow = new FortressEffect { tintWithPlayerColor = false };

        /// <summary>設定を読む（Resources に無ければ初期値の物を作る）。一度読んだら同じ物を使う。</summary>
        public static HowToPlaySettings Load()
        {
            if (_loaded != null)
            {
                return _loaded;
            }

            _loaded = Resources.Load<HowToPlaySettings>(ResourcePath);
            if (_loaded == null)
            {
                _loaded = CreateInstance<HowToPlaySettings>();
                _loaded.hideFlags = HideFlags.HideAndDontSave;
            }

            return _loaded;
        }

        /// <summary>課題の名前（足りなければ空）。</summary>
        public string StepLabel(int index) => stepLabels != null && index >= 0 && index < stepLabels.Length ? stepLabels[index] : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _loaded = null;
    }
}
