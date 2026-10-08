using System;

namespace MS2026.StudioKit
{
    public enum StudioIssueSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>点検で見つかった問題1つ。直し方が決まっている物は Fix が付く。</summary>
    public sealed class StudioIssue
    {
        public StudioIssueSeverity Severity;
        public string Title;
        public string Detail;
        public UnityEngine.Object Target;
        public string FixLabel;
        public Action Fix;

        public StudioIssue(StudioIssueSeverity severity, string title, string detail, UnityEngine.Object target = null)
        {
            Severity = severity;
            Title = title;
            Detail = detail;
            Target = target;
        }

        public StudioIssue WithFix(string label, Action fix)
        {
            FixLabel = label;
            Fix = fix;
            return this;
        }
    }
}
