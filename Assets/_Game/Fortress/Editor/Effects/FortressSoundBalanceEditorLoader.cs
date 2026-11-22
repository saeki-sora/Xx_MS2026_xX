using UnityEditor;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// エディタで、音のバランス（<see cref="FortressSoundBalance"/>）を読み込んで使う状態にする。
    /// ビルドでは Preloaded Assets で読まれるが、エディタではそれが読まれないことがあるため。
    /// </summary>
    [InitializeOnLoad]
    public static class FortressSoundBalanceEditorLoader
    {
        static FortressSoundBalanceEditorLoader()
        {
            EditorApplication.delayCall += Load;
            EditorApplication.playModeStateChanged += _ => Load();
        }

        /// <summary>プロジェクトの音のバランスを探して使う（サウンドスタジオが作り直したときにも呼ぶ）。</summary>
        public static void Load()
        {
            if (FortressSoundBalance.Current != null)
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:" + nameof(FortressSoundBalance));
            if (guids.Length == 0)
            {
                return;
            }

            FortressSoundBalance.Current = AssetDatabase.LoadAssetAtPath<FortressSoundBalance>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
