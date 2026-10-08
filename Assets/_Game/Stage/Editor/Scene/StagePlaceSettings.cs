using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>シーンで背景を直接動かすときの設定（このPCだけに保存。チームで共有しない好みの設定）。</summary>
    public static class StagePlaceSettings
    {
        private const string Prefix = "MS2026.StageStudio.Place.";

        /// <summary>吸着（Ctrl を押している間は逆になる）。</summary>
        public static bool Snap
        {
            get => EditorPrefs.GetBool(Prefix + "Snap", true);
            set => EditorPrefs.SetBool(Prefix + "Snap", value);
        }

        /// <summary>マス目の大きさ（メートル）。</summary>
        public static float GridStep
        {
            get => EditorPrefs.GetFloat(Prefix + "Grid", 0.25f);
            set => EditorPrefs.SetFloat(Prefix + "Grid", Mathf.Max(0.01f, value));
        }

        /// <summary>回すときの刻み（度）。</summary>
        public static float AngleStep
        {
            get => EditorPrefs.GetFloat(Prefix + "Angle", 15f);
            set => EditorPrefs.SetFloat(Prefix + "Angle", Mathf.Clamp(value, 1f, 90f));
        }

        /// <summary>他の物の端に吸着する距離（メートル）。0なら端には吸着しない。</summary>
        public static float EdgeSnapDistance
        {
            get => EditorPrefs.GetFloat(Prefix + "Edge", 0.2f);
            set => EditorPrefs.SetFloat(Prefix + "Edge", Mathf.Max(0f, value));
        }

        /// <summary>砲台・コア・湧き位置との距離を出す。</summary>
        public static bool ShowDistances
        {
            get => EditorPrefs.GetBool(Prefix + "Distances", true);
            set => EditorPrefs.SetBool(Prefix + "Distances", value);
        }

        /// <summary>この距離より近いと「近すぎ」（赤）にする（メートル）。</summary>
        public static float NearWarning
        {
            get => EditorPrefs.GetFloat(Prefix + "Near", 1f);
            set => EditorPrefs.SetFloat(Prefix + "Near", Mathf.Max(0f, value));
        }

        /// <summary>距離を出す範囲（メートル）。これより遠い砲台などは出さない。</summary>
        public const float DistanceRange = 8f;

        /// <summary>
        /// 「見た目のずれ」を出す視点（ViewerIndex。-1=全体、0〜3=P1〜P4）。<see cref="ParallaxOff"/> なら出さない。
        /// </summary>
        public static int ParallaxViewer
        {
            get => EditorPrefs.GetInt(Prefix + "Parallax", 0);
            set => EditorPrefs.SetInt(Prefix + "Parallax", value);
        }

        public const int ParallaxOff = -99;

        /// <summary>ずれを、選んでいる物だけでなく全部の物に出す。</summary>
        public static bool ParallaxForAll
        {
            get => EditorPrefs.GetBool(Prefix + "ParallaxAll", false);
            set => EditorPrefs.SetBool(Prefix + "ParallaxAll", value);
        }

        /// <summary>背景を選んだら、自動でステージ配置ツールに切り替える。</summary>
        public static bool AutoTool
        {
            get => EditorPrefs.GetBool(Prefix + "AutoTool", true);
            set => EditorPrefs.SetBool(Prefix + "AutoTool", value);
        }

        /// <summary>配置ページのゲーム画面で見る視点（1画面のとき）。</summary>
        public static int PreviewViewer
        {
            get => EditorPrefs.GetInt(Prefix + "PreviewViewer", 0);
            set => EditorPrefs.SetInt(Prefix + "PreviewViewer", ViewerIndex.IsValid(value) ? value : 0);
        }

        /// <summary>配置ページのゲーム画面を4人分並べる。</summary>
        public static bool PreviewQuad
        {
            get => EditorPrefs.GetBool(Prefix + "PreviewQuad", false);
            set => EditorPrefs.SetBool(Prefix + "PreviewQuad", value);
        }

        /// <summary>配置ページのゲーム画面に、通れない範囲の輪郭を重ねる。</summary>
        public static bool PreviewOutlines
        {
            get => EditorPrefs.GetBool(Prefix + "PreviewOutlines", true);
            set => EditorPrefs.SetBool(Prefix + "PreviewOutlines", value);
        }

        /// <summary>今の操作で吸着するか（設定と Ctrl キーの組み合わせ）。</summary>
        public static bool SnapActive(Event e) => Snap != (e != null && (e.control || e.command));
    }
}
