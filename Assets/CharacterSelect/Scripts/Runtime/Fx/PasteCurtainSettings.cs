using MS2026.Fortress;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 歯磨き粉の幕（<see cref="PasteCurtain"/>）の設定。Resources に置いた1つ（<see cref="ResourcePath"/>）を使う。
    /// 絵はタイトルのオープニングと同じ歯磨き粉の絵（Title_PasteCurtain）を入れておくと、タイトルと同じ見た目になる。
    /// 「連番の絵」を入れると、縦の列を垂らす代わりにその連番を画面いっぱいに順番に出す（2026-10-22〜）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/キャラ選択/歯磨き粉の幕の設定", fileName = "CharacterSelectCurtain", order = 10)]
    public sealed class PasteCurtainSettings : ScriptableObject
    {
        /// <summary>Resources から読む名前（Assets/CharacterSelect/Resources/CharacterSelectCurtain.asset）。</summary>
        public const string ResourcePath = "CharacterSelectCurtain";

        [Header("見た目")]
        [Tooltip("幕の絵（縦のしまで、下のふちが垂れている絵）。空なら色だけの幕。")]
        public Texture texture;

        [Tooltip("幕にかける色（ふつうは白＝絵のまま）。")]
        public Color color = Color.white;

        [Tooltip("縦の列の数（列ごとに少しずつ遅れて垂れてくる）。")]
        [Range(1, 64)]
        public int columns = 28;

        [Tooltip("垂れてくる先端のでこぼこ（0 で横一直線）。")]
        [Range(0f, 0.9f)]
        public float unevenness = 0.45f;

        [Header("連番の絵（入れると、上の列の幕の代わりにこれを再生）")]
        [Tooltip("画面いっぱいの連番（After Effects などで書き出した透過PNG）。名前順に並べる。空なら上の列の幕。")]
        public Texture2D[] frames = new Texture2D[0];

        [Tooltip("連番の1秒の枚数。")]
        [Min(1f)]
        public float frameRate = 30f;

        [Tooltip("画面を覆いきるコマ（0 から数える）。ここまで来たら次の画面の読み込みを始める。")]
        [Min(0)]
        public int coveredFrame = 24;

        [Tooltip("抜け始めるコマ（0 から数える）。覆いきったコマからこの1つ前までを、読み込みを待つ間に出す（最後は止めて待つ）。")]
        [Min(0)]
        public int revealFrame = 32;

        [Header("時間（秒。連番の絵を使うときは、覆ったままにする時間だけ使う）")]
        [Tooltip("上から垂れてきて画面を覆いきるまで（長いほど、どろーっと遅い）。")]
        [Min(0.05f)]
        public float dropSeconds = 1.1f;

        [Tooltip("覆ったままにする最短の時間（次の画面の読み込みが終わるまでは、これより長く覆ったまま）。")]
        [Min(0f)]
        public float holdSeconds = 0.2f;

        [Tooltip("下へ流れ落ちて次の画面が見えるまで。")]
        [Min(0.05f)]
        public float revealSeconds = 0.9f;

        [Header("その他")]
        [Tooltip("ONなら、全員が決めた「GO!」のあとゲームへ進むときも、この幕で切り替える（OFF ならロビーのいつもの幕。1台で試すときはそのまま切り替わる）。")]
        public bool useForGameStart = true;

        [Tooltip("ONなら、キャラ選択のシーンだけで再生したときも、最初に覆った状態から流れ落ちる（見た目を試すため）。")]
        public bool playOnStandalone = true;

        [Tooltip("画面の重なり順（ロビーの幕 900 より手前）。")]
        public int sortingOrder = 950;

        [Header("音（D-Drive の番号札。空なら鳴らさない）")]
        [Tooltip("垂れ始めたとき。")]
        public FortressEffect onCover = new FortressEffect();

        [Tooltip("流れ落ち始めたとき。")]
        public FortressEffect onReveal = new FortressEffect();
    }
}
