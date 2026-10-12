using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の下の丸（<see cref="PlayerCircle"/>）に付く「1P」などの札。
    /// 決定すると大きくなってチェックが付き、ほかの人が決定済みで選べないキャラを選んでいる間は薄くなる。
    /// </summary>
    [AddComponentMenu("キャラ選択/プレイヤーの札 (PlayerTag)")]
    public sealed class PlayerTag : MonoBehaviour
    {
        // 毎フレーム文字を作らないよう、先に作っておく。
        private static readonly string[] Names = { "1P", "2P", "3P", "4P" };

        [Tooltip("札の背景（プレイヤーの色になる）。")]
        public Image background;

        [Tooltip("矢印など、背景と同じ色にする物（空でもよい）。")]
        public Image pointer;

        [Tooltip("「1P」などの文字。")]
        public Text label;

        [Tooltip("決定したときだけ出す物（チェックなど。空でもよい）。")]
        public GameObject decidedMark;

        [Tooltip("決定したときの大きさ。")]
        public float decidedScale = 1.2f;

        [Tooltip("選べないキャラの上にいるときの濃さ。")]
        [Range(0f, 1f)]
        public float blockedAlpha = 0.45f;

        public void Set(int seat, Color color, bool confirmed, bool blocked)
        {
            if (label != null && seat >= 0 && seat < Names.Length)
            {
                label.text = Names[seat];
            }

            var c = color;
            c.a = blocked && !confirmed ? blockedAlpha : 1f;
            if (background != null)
            {
                background.color = c;
            }

            if (pointer != null)
            {
                pointer.color = c;
            }

            if (decidedMark != null && decidedMark.activeSelf != confirmed)
            {
                decidedMark.SetActive(confirmed);
            }

            transform.localScale = Vector3.one * (confirmed ? decidedScale : 1f);
        }
    }
}
