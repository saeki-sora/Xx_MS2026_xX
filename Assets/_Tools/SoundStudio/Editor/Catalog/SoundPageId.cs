namespace MS2026.SoundStudio
{
    /// <summary>サウンドスタジオの左のナビのページ。並びは遊ぶ流れの順（ユーザー決定）。</summary>
    public enum SoundPageId
    {
        Start,
        Title,
        Lobby,
        CharacterSelect,
        HowToPlay,
        Roulette,
        StageIntro,
        Match,
        Boss,
        Result,
        Other,
    }

    /// <summary>ページの名前と、wav を入れるフォルダ名。</summary>
    public static class SoundPageIds
    {
        public static string Label(SoundPageId page)
        {
            switch (page)
            {
                case SoundPageId.Start: return "はじめに";
                case SoundPageId.Title: return "タイトル";
                case SoundPageId.Lobby: return "ロビー";
                case SoundPageId.CharacterSelect: return "キャラ選択";
                case SoundPageId.HowToPlay: return "遊び方";
                case SoundPageId.Roulette: return "ステージ抽選";
                case SoundPageId.StageIntro: return "ステージ登場";
                case SoundPageId.Match: return "試合";
                case SoundPageId.Boss: return "ボス";
                case SoundPageId.Result: return "結果";
                default: return "その他";
            }
        }

        /// <summary>wav を入れる <c>Assets/_Game/DDrive/SourceAssets/Se/</c> の下のフォルダ名（英字。番号札の名前の頭になる）。</summary>
        public static string SeFolder(SoundPageId page)
        {
            return page == SoundPageId.Start ? "Other" : page.ToString();
        }
    }
}
