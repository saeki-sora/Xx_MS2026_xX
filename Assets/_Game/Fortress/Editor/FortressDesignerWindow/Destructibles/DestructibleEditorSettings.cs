using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>破壊可能物タブの編集用の設定（スナップ・配置モード・表示のON/OFF）。エディタを閉じても覚えている。</summary>
    public static class DestructibleEditorSettings
    {
        private const string Prefix = "Fortress.Destructible.";

        /// <summary>Sceneをクリック/ドラッグして配置するモードか。</summary>
        public static bool PlacementMode
        {
            get => EditorPrefs.GetBool(Prefix + "PlacementMode", false);
            set => EditorPrefs.SetBool(Prefix + "PlacementMode", value);
        }

        public static bool SnapEnabled
        {
            get => EditorPrefs.GetBool(Prefix + "SnapEnabled", true);
            set => EditorPrefs.SetBool(Prefix + "SnapEnabled", value);
        }

        public static float SnapSize
        {
            get => Mathf.Max(0.05f, EditorPrefs.GetFloat(Prefix + "SnapSize", 0.5f));
            set => EditorPrefs.SetFloat(Prefix + "SnapSize", Mathf.Max(0.05f, value));
        }

        /// <summary>trueなら、クリック配置の大きさにプリセットの既定サイズを使う。falseなら下のPlaceSizeを使う。</summary>
        public static bool UsePresetSize
        {
            get => EditorPrefs.GetBool(Prefix + "UsePresetSize", true);
            set => EditorPrefs.SetBool(Prefix + "UsePresetSize", value);
        }

        public static Vector2 PlaceSize
        {
            get => new Vector2(
                Mathf.Max(0.1f, EditorPrefs.GetFloat(Prefix + "PlaceW", 2f)),
                Mathf.Max(0.1f, EditorPrefs.GetFloat(Prefix + "PlaceH", 2f)));
            set
            {
                EditorPrefs.SetFloat(Prefix + "PlaceW", Mathf.Max(0.1f, value.x));
                EditorPrefs.SetFloat(Prefix + "PlaceH", Mathf.Max(0.1f, value.y));
            }
        }

        public static bool ShowResizeHandles
        {
            get => EditorPrefs.GetBool(Prefix + "ResizeHandles", true);
            set => EditorPrefs.SetBool(Prefix + "ResizeHandles", value);
        }

        public static bool ShowLabels
        {
            get => EditorPrefs.GetBool(Prefix + "Labels", true);
            set => EditorPrefs.SetBool(Prefix + "Labels", value);
        }

        public static bool ShowLinks
        {
            get => EditorPrefs.GetBool(Prefix + "Links", true);
            set => EditorPrefs.SetBool(Prefix + "Links", value);
        }

        /// <summary>配置に使うプリセット（無ければ既定設定で作る）。</summary>
        public static DestructiblePreset ActivePreset
        {
            get
            {
                var guid = EditorPrefs.GetString(Prefix + "ActivePreset", "");
                if (string.IsNullOrEmpty(guid))
                {
                    return null;
                }

                var path = AssetDatabase.GUIDToAssetPath(guid);
                return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<DestructiblePreset>(path);
            }
            set
            {
                var path = value != null ? AssetDatabase.GetAssetPath(value) : "";
                EditorPrefs.SetString(Prefix + "ActivePreset", string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
            }
        }

        /// <summary>これから配置する物の大きさ。</summary>
        public static Vector2 CurrentPlaceSize()
        {
            var preset = ActivePreset;
            return UsePresetSize && preset != null ? preset.defaultSize : PlaceSize;
        }

        public static float Snap(float value)
        {
            return SnapEnabled ? Mathf.Round(value / SnapSize) * SnapSize : value;
        }

        public static Vector2 Snap(Vector2 point)
        {
            return new Vector2(Snap(point.x), Snap(point.y));
        }
    }
}
