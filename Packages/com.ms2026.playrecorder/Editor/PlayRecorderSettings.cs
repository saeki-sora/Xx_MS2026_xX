using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>上下反転の補正のしかた。</summary>
    internal enum FlipMode
    {
        Auto = 0,
        Flip = 1,
        NoFlip = 2,
    }

    /// <summary>
    /// 録画の設定。EditorPrefs に保存するので、PC ごと（全プロジェクト共通）の設定になる。
    /// 保存先フォルダだけはプロジェクトからの相対パス。
    /// </summary>
    internal static class PlayRecorderSettings
    {
        const string Prefix = "MS2026.PlayRecorder.";

        public const float DefaultLimitMB = 20f;
        public const int DefaultMaxHeight = 720;
        public const int DefaultMaxFps = 30;
        public const string DefaultOutputFolder = "Recordings";

        public static readonly int[] MaxHeightChoices = { 480, 720, 1080 };
        public static readonly int[] MaxFpsChoices = { 30, 60 };

        /// <summary>上限サイズ（MB。1MB = 1,000,000 バイトで数えるので、MiB 表記のサービスでも必ず下回る）。</summary>
        public static float LimitMB
        {
            get => Mathf.Clamp(EditorPrefs.GetFloat(Prefix + "LimitMB", DefaultLimitMB), 1f, 4000f);
            set => EditorPrefs.SetFloat(Prefix + "LimitMB", Mathf.Clamp(value, 1f, 4000f));
        }

        public static long LimitBytes => (long)(LimitMB * 1000_000.0);

        public static int MaxHeight
        {
            get => EditorPrefs.GetInt(Prefix + "MaxHeight", DefaultMaxHeight);
            set => EditorPrefs.SetInt(Prefix + "MaxHeight", value);
        }

        public static int MaxFps
        {
            get => EditorPrefs.GetInt(Prefix + "MaxFps", DefaultMaxFps);
            set => EditorPrefs.SetInt(Prefix + "MaxFps", value);
        }

        public static bool RecordAudio
        {
            get => EditorPrefs.GetBool(Prefix + "RecordAudio", true);
            set => EditorPrefs.SetBool(Prefix + "RecordAudio", value);
        }

        public static bool RevealWhenDone
        {
            get => EditorPrefs.GetBool(Prefix + "RevealWhenDone", true);
            set => EditorPrefs.SetBool(Prefix + "RevealWhenDone", value);
        }

        public static FlipMode Flip
        {
            get => (FlipMode)EditorPrefs.GetInt(Prefix + "Flip", (int)FlipMode.Auto);
            set => EditorPrefs.SetInt(Prefix + "Flip", (int)value);
        }

        /// <summary>保存先（プロジェクトのフォルダからの相対パス、または絶対パス）。</summary>
        public static string OutputFolder
        {
            get
            {
                var v = EditorPrefs.GetString(Prefix + "OutputFolder", DefaultOutputFolder);
                return string.IsNullOrWhiteSpace(v) ? DefaultOutputFolder : v;
            }
            set => EditorPrefs.SetString(Prefix + "OutputFolder", value);
        }

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string OutputFolderAbsolute => Path.GetFullPath(Path.Combine(ProjectRoot, OutputFolder));

        /// <summary>最後に書き出した動画の絶対パス（無ければ空）。</summary>
        public static string LastOutputPath
        {
            get => EditorPrefs.GetString(Prefix + "LastOutput." + ProjectRoot, "");
            set => EditorPrefs.SetString(Prefix + "LastOutput." + ProjectRoot, value);
        }

        public static bool ShouldFlip()
        {
            switch (Flip)
            {
                case FlipMode.Flip: return true;
                case FlipMode.NoFlip: return false;
                default: return SystemInfo.graphicsUVStartsAtTop;
            }
        }

        public static void ResetToDefaults()
        {
            foreach (var key in new[] { "LimitMB", "MaxHeight", "MaxFps", "RecordAudio", "RevealWhenDone", "Flip", "OutputFolder" })
                EditorPrefs.DeleteKey(Prefix + key);
        }
    }
}
