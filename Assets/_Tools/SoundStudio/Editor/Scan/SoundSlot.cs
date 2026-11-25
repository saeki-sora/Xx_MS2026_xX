using System;

namespace MS2026.SoundStudio
{
    public enum SoundSlotKind
    {
        Effect,
        Bgm,
        Candidate,
    }

    /// <summary>どこにある欄か。保存のしかたが変わる。</summary>
    public enum SoundSlotPlace
    {
        /// <summary>設定ファイル（ScriptableObject）。</summary>
        Asset,

        /// <summary>Prefab の中の部品。</summary>
        Prefab,

        /// <summary>シーンの中の部品。</summary>
        Scene,

        /// <summary>まだ欄がない候補。</summary>
        None,
    }

    /// <summary>サウンドスタジオの1行分の欄。</summary>
    public sealed class SoundSlot
    {
        public SoundSlotKind Kind;
        public SoundSlotPlace Place;

        /// <summary>名前の表の行（表に無ければ null で、「その他」に出る）。</summary>
        public SoundCatalogEntry Entry;

        /// <summary>候補の行のときだけ。</summary>
        public SoundCandidate Candidate;

        /// <summary>欄を持つ物（設定ファイル・Prefab の部品）。シーンの部品は閉じると消えるので null のことがある。</summary>
        public UnityEngine.Object Owner;

        /// <summary>設定ファイル・Prefab・シーンのファイルの場所。</summary>
        public string AssetPath;

        /// <summary>シーン・Prefab の部品の場所（GlobalObjectId の文字列）。</summary>
        public string GlobalObjectId;

        /// <summary>持ち主の中の欄の道筋（SerializedProperty.propertyPath）。</summary>
        public string PropertyPath;

        public Type DeclaringType;
        public string FieldName;

        /// <summary>持ち主の名前（同じ欄を複数の物が持つとき、行の後ろに小さく出す）。</summary>
        public string OwnerName;

        /// <summary>欄の説明（Tooltip）。</summary>
        public string Tooltip;

        public SoundPageId Page;
        public string Group;
        public string Label;
        public bool Personal;
        public bool Voice;

        /// <summary>見つけたときの中身（FortressEffect の実物、または BGM の番号）。シーンを閉じた後は古いことがある。</summary>
        public object Value;

        /// <summary>見つけたときに音が入っていたか。</summary>
        public bool HasSound;

        /// <summary>
        /// この欄の仕組みを使っているか（例: 爆弾ではない敵の「爆発した」は使っていない）。使っていない欄は薄く出し、音がない数に数えない。
        /// </summary>
        public bool Active = true;

        /// <summary>名前の表の並び（小見出し・行をこの順に並べる）。表に無ければ大きな数。</summary>
        public int Order = int.MaxValue;

        /// <summary>行を見分ける文字列（場所 + 部品 + 道筋）。</summary>
        public string Key => (GlobalObjectId ?? AssetPath) + "|" + PropertyPath + "|" + Candidate?.Number;
    }
}
