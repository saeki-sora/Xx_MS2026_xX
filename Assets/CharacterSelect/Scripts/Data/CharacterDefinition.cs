using System;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラクター1人分の設定（名前・色・立ち絵・顔のアイコン・能力の棒グラフ・スキルの説明）。1キャラ = 1ファイル。
    /// 選択画面では、そのキャラを選んでいるプレイヤーの区画に、立ち絵・名前・棒グラフ・スキルが出る。
    /// 文章は仮でよいので Inspector で打ち込む（あとで本番の文章に書き換える）。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/キャラ選択/キャラクター", fileName = "Character", order = 0)]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Serializable]
        public struct Stat
        {
            [Tooltip("棒グラフの名前（例: パワー）。空なら名前は出さず棒だけ。")]
            public string label;

            [Tooltip("棒の長さ（0〜1）。")]
            [Range(0f, 1f)]
            public float value;
        }

        [Header("名前")]
        [Tooltip("ゲームの中でキャラを見分けるための名前（半角英数字。あとから変えないこと）。")]
        public string id = "chara";

        [Tooltip("画面に出す名前（区画の上の札・下のアイコンの名前）。")]
        public string displayName = "キャラ名";

        [Header("説明（仮の文章でよい）")]
        [Tooltip("能力の棒グラフ（上から順に並ぶ。いくつでもよい）。")]
        public Stat[] stats = Array.Empty<Stat>();

        [Tooltip("スキルの名前（空なら出さない）。")]
        public string skillName = "スキル名";

        [Tooltip("スキルの説明。")]
        [TextArea(2, 5)]
        public string skillDescription = "スキル内容";

        [Header("見た目")]
        [Tooltip("キャラの色（立ち絵が無いときの丸の色）。仮のキューブの色は下の「立ち絵にかける色」で付けている。")]
        public Color themeColor = Color.white;

        [Tooltip("区画の左に大きく出る立ち絵（2Dの絵）。今は仮の白いキューブの絵。空なら、キャラの色の丸に名前の1文字目。")]
        public Sprite sprite;

        [Tooltip("立ち絵にかける色。アイコンが空のときは下の丸の顔にもかかる。\n仮のキューブはここでキャラの色にしている（本番の絵に替えたら白＝絵のまま に戻す）。")]
        public Color spriteTint = Color.white;

        [Tooltip("立ち絵の大きさの倍率（1 = 枠の高さいっぱい）。")]
        [Min(0.1f)]
        public float spriteScale = 1f;

        [Tooltip("立ち絵の位置のずれ（ピクセル。画面は 1920×1080 として）。足元・顔の位置を合わせたいときに使う。")]
        public Vector2 spriteOffset = Vector2.zero;

        [Tooltip("画面の下の丸（1P〜4P）に出す顔の絵（色はかけず絵のまま）。空なら立ち絵（立ち絵の色つき）を使い、それも空なら名前の1文字目。")]
        public Sprite icon;

        /// <summary>画面に出す名前（空ならアセットの名前）。</summary>
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>下のアイコンに出す絵（アイコン、無ければ立ち絵、どちらも無ければ null）。</summary>
        public Sprite IconOrSprite => icon != null ? icon : sprite;
    }
}
