using System;
using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// タイトル画面の左右に落ちてくるキャラの一覧（2026-10-22〜）。開くたびにこの中から2人をランダムに選んで、
    /// 左（CharacterLeft）と右（CharacterRight）に出す（<see cref="TitleScreenController"/> が始まる前に入れ替える）。
    /// キャラごとに絵の高さと位置のずれを持つので、だれが左右どちらに出ても、そのキャラに合わせた大きさ・位置になる。
    /// Resources に置いた1つ（<see cref="ResourcePath"/>）を使う。シーンを組み立て直しても消えない。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/タイトル/タイトルのキャラ一覧", fileName = "TitleCharacters", order = 20)]
    public sealed class TitleCharacterRoster : ScriptableObject
    {
        /// <summary>Resources から読む名前（Assets/Title/Resources/TitleCharacters.asset）。</summary>
        public const string ResourcePath = "TitleCharacters";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("メモ用の名前（画面には出ない）。")]
            public string label;

            [Tooltip("立ち絵（背景が透明な画像。余白は切り落としておくと大きさがそろいやすい）。")]
            public Sprite sprite;

            [Tooltip("絵の高さ（ワールドの単位。画面の高さは 10.8）。")]
            [Min(0.1f)]
            public float height = 10.35f;

            [Tooltip("キャラの置き場所（左右の決まった位置）から絵の真ん中までのずれ。\nX = 画面の端の方へ（左に出たときは左へ、右に出たときは右へ）、Y = 上へ。")]
            public Vector2 offset = new(0.3f, 1.1f);
        }

        [Tooltip("ONなら、タイトルを開くたびに、下の一覧から2人をランダムに選んで左右に出す（左右どちらに出るかもランダム）。\nOFF ならシーンに置いたままのキャラが出る。")]
        public bool randomize = true;

        [Tooltip("出てくるキャラ。立ち絵が入っているものから選ぶ（2人以上必要）。")]
        public Entry[] characters = Array.Empty<Entry>();

        /// <summary>左右それぞれの絵の位置（キャラの置き場所から見たずれ）。</summary>
        public static Vector3 LocalPosition(Entry entry, bool rightSide) =>
            new(rightSide ? entry.offset.x : -entry.offset.x, entry.offset.y, 0f);

        /// <summary><see cref="LocalPosition"/> の逆（シーンで動かした位置を一覧の値に戻す）。</summary>
        public static Vector2 OffsetFrom(Vector3 localPosition, bool rightSide) =>
            new(rightSide ? localPosition.x : -localPosition.x, localPosition.y);

        /// <summary>
        /// count 人から、違う2人を選ぶ（left, right）。2人未満なら (-1, -1)。
        /// randomRange(n) は 0 以上 n 未満の整数を返すもの（ふつうは UnityEngine.Random.Range(0, n)）。
        /// </summary>
        public static (int left, int right) PickPair(int count, Func<int, int> randomRange)
        {
            if (count < 2) return (-1, -1);
            var left = Mathf.Clamp(randomRange(count), 0, count - 1);
            // 残りの count-1 人から選んで、左と同じ番号以降を1つずらす（同じキャラが並ばない）。
            var right = Mathf.Clamp(randomRange(count - 1), 0, count - 2);
            if (right >= left) right++;
            return (left, right);
        }

        /// <summary>立ち絵が入っているキャラの番号（一覧の何番目か）。</summary>
        public int[] UsableIndices()
        {
            var list = new System.Collections.Generic.List<int>();
            for (var i = 0; i < (characters?.Length ?? 0); i++)
            {
                if (characters[i] != null && characters[i].sprite != null) list.Add(i);
            }
            return list.ToArray();
        }

        /// <summary>キャラ1人を、置き場所の子「Sprite」（SpriteRenderer + TitleSpriteFit）に当てはめる。当てはめられたら true。</summary>
        public static bool ApplyTo(Transform slot, Entry entry, bool rightSide)
        {
            if (slot == null || entry == null || entry.sprite == null) return false;
            var spriteTransform = slot.Find("Sprite");
            var renderer = spriteTransform != null ? spriteTransform.GetComponent<SpriteRenderer>() : null;
            if (renderer == null) return false;
            renderer.sprite = entry.sprite;
            spriteTransform.localPosition = LocalPosition(entry, rightSide);
            var fit = spriteTransform.GetComponent<TitleSpriteFit>();
            if (fit != null)
            {
                fit.mode = TitleSpriteFit.FitMode.Height;
                fit.size = entry.height;
                fit.Fit();
            }
            return true;
        }
    }
}
