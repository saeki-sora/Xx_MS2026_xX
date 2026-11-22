namespace MS2026.SoundStudio
{
    /// <summary>まだゲームに欄がないタイミングの候補（設計書の N1〜N20）。画面では紫の点線の行で出す。</summary>
    public sealed class SoundCandidate
    {
        public SoundCandidate(string number, SoundPageId page, string group, string label, bool personal, bool voice = false, bool isBgm = false)
        {
            Number = number;
            Page = page;
            Group = group;
            Label = label;
            Personal = personal;
            Voice = voice;
            IsBgm = isBgm;
        }

        public string Number { get; }
        public SoundPageId Page { get; }
        public string Group { get; }
        public string Label { get; }
        public bool Personal { get; }
        public bool Voice { get; }
        public bool IsBgm { get; }
    }
}
