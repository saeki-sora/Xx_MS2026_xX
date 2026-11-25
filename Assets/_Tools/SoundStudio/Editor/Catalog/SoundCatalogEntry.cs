namespace MS2026.SoundStudio
{
    /// <summary>
    /// 名前の表の1行: 「どの型のどの欄を、どのページのどの小見出しに、何という名前で出すか」。
    /// 欄は「その欄を書いている型（入れ子の設定クラスならその型）」と欄の名前で見分ける。
    /// こうすると、同じ設定クラスを4人分のレーザーや色ごとのスキルが持っていても1行で済む。
    /// </summary>
    public sealed class SoundCatalogEntry
    {
        public SoundCatalogEntry(string declaringType, string fieldName, SoundPageId page, string group, string label,
            bool personal, bool voice = false, bool isBgm = false)
        {
            DeclaringType = declaringType;
            FieldName = fieldName;
            Page = page;
            Group = group;
            Label = label;
            Personal = personal;
            Voice = voice;
            IsBgm = isBgm;
        }

        /// <summary>欄を書いている型の完全な名前（入れ子の型は <c>+</c> でつなぐ）。</summary>
        public string DeclaringType { get; }

        public string FieldName { get; }
        public SoundPageId Page { get; }

        /// <summary>ページの中の小見出し（例「レーザー」）。</summary>
        public string Group { get; }

        /// <summary>画面に出す名前（やさしい日本語）。</summary>
        public string Label { get; }

        /// <summary>その人の事か（鳴らす所が持ち主の番号を渡している）。そうなら「鳴らすPC」を選べる。</summary>
        public bool Personal { get; }

        /// <summary>声の欄か（声全体の音量が効く）。</summary>
        public bool Voice { get; }

        /// <summary>BGM の欄か。</summary>
        public bool IsBgm { get; }

        /// <summary>表の中の並び（0 から）。</summary>
        public int Order { get; internal set; }
    }
}
