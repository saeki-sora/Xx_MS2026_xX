using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// CPU 調整スタジオで共有する物: CPU の設定（Resources/CpuSettings。無ければ作る）と、初期値と比べるための新しい設定。
    /// </summary>
    internal sealed class CpuStudioContext
    {
        public const string SettingsFolder = "Assets/_Game/CPU/Runtime/Resources";
        public const string SettingsPath = SettingsFolder + "/" + CpuSettings.ResourcePath + ".asset";

        private CpuSettings _defaults;
        private SerializedObject _defaultsObject;

        public CpuSettings Settings { get; private set; }
        public SerializedObject Object { get; private set; }

        /// <summary>設定を読む（無ければ作る）。</summary>
        public void Ensure()
        {
            if (Settings == null)
            {
                Settings = AssetDatabase.LoadAssetAtPath<CpuSettings>(SettingsPath);
                if (Settings == null)
                {
                    Directory.CreateDirectory(SettingsFolder);
                    Settings = ScriptableObject.CreateInstance<CpuSettings>();
                    AssetDatabase.CreateAsset(Settings, SettingsPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[CPU] 設定を作りました: {SettingsPath}");
                }

                Object = new SerializedObject(Settings);
            }

            if (_defaults == null)
            {
                _defaults = ScriptableObject.CreateInstance<CpuSettings>();
                _defaults.hideFlags = HideFlags.HideAndDontSave;
                _defaultsObject = new SerializedObject(_defaults);
            }

            Object.Update();
        }

        /// <summary>
        /// 1つの設定の行: 名前と値（Unity の普通の入力欄。元に戻す・Undo が効く）＋ 初期値から変えたら名前の色が変わり「戻す」が出る。
        /// </summary>
        public VisualElement Field(string path, string label, out System.Action refresh)
        {
            Ensure();
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var property = Object.FindProperty(path);
            var field = new PropertyField(property, label);
            field.style.flexGrow = 1f;
            field.tooltip = property != null ? property.tooltip : null;
            field.Bind(Object);
            // 設定ファイルの見出し（[Header]）はスタジオでは出さない（ページの見出しと二重になるので）。
            field.RegisterCallback<GeometryChangedEvent>(_ =>
                field.Query(className: "unity-decorator-drawers-container").ForEach(e => e.style.display = DisplayStyle.None));
            row.Add(field);

            var reset = new Button(() =>
            {
                Object.Update();
                Object.CopyFromSerializedProperty(_defaultsObject.FindProperty(path));
                Object.ApplyModifiedProperties();
            })
            {
                text = "戻す",
                tooltip = "この項目を初期値に戻す（Ctrl+Z で取り消せる）。"
            };
            reset.style.marginLeft = 6f;
            row.Add(reset);

            refresh = () =>
            {
                var current = Object.FindProperty(path);
                var initial = _defaultsObject.FindProperty(path);
                var changed = current != null && initial != null && !SerializedProperty.DataEquals(current, initial);
                reset.style.display = changed ? DisplayStyle.Flex : DisplayStyle.None;
                var name = field.Q<Label>();
                if (name != null)
                {
                    name.style.color = changed ? new Color(1f, 0.75f, 0.3f) : StyleKeyword.Null;
                }
            };
            return row;
        }

        public void Dispose()
        {
            if (_defaults != null)
            {
                UnityEngine.Object.DestroyImmediate(_defaults);
            }
        }
    }
}
