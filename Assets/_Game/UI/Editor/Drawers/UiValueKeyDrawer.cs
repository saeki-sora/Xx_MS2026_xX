using UnityEditor;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 「値の名前（キー）」の欄。手で打つこともできるが、▼ で値の一覧（UiValueCatalog）から選べる。
    /// 一覧に無い名前を入れると黄色で知らせる（打ち間違いの発見用）。
    /// </summary>
    [CustomPropertyDrawer(typeof(UiValueKeyAttribute))]
    public sealed class UiValueKeyDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 22f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var catalog = UiStudioSetup.ValueCatalog;
            var item = catalog != null ? catalog.Find(property.stringValue) : null;
            var known = string.IsNullOrEmpty(property.stringValue) || item != null;
            var fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            var content = new GUIContent(label.text, item != null ? $"{item.label}\n{item.description}" : label.tooltip);
            var old = GUI.color;
            if (!known)
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                content.tooltip = "この名前は値の一覧にありません（打ち間違い？）。UIスタジオの「値」ページで確認・登録できます。";
            }

            EditorGUI.PropertyField(fieldRect, property, content);
            GUI.color = old;

            if (GUI.Button(buttonRect, "▼", EditorStyles.miniButton) && catalog != null)
            {
                var menu = new GenericMenu();
                foreach (var entry in catalog.items)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    var key = entry.key;
                    var path = $"{entry.category}/{entry.label}  ({key})";
                    menu.AddItem(new GUIContent(path), key == property.stringValue, () =>
                    {
                        property.serializedObject.Update();
                        property.stringValue = key;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.DropDown(buttonRect);
            }
        }
    }

    /// <summary>「レイヤー（段）の名前」の欄。レイヤーの一覧から選ぶ。</summary>
    [CustomPropertyDrawer(typeof(UiLayerIdAttribute))]
    public sealed class UiLayerIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var settings = UiStudioSetup.LayerSettings;
            if (property.propertyType != SerializedPropertyType.String || settings == null || settings.layers.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var ids = new string[settings.layers.Count];
            var names = new GUIContent[settings.layers.Count];
            var index = -1;
            for (var i = 0; i < ids.Length; i++)
            {
                var layer = settings.layers[i];
                ids[i] = layer.id;
                names[i] = new GUIContent($"{layer.label}（{layer.id}）", layer.description);
                if (layer.id == property.stringValue)
                {
                    index = i;
                }
            }

            if (index < 0)
            {
                // 一覧に無い名前は勝手に書き換えず、黄色で知らせて手で直せるようにする。
                var old = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.4f);
                EditorGUI.PropertyField(position, property, new GUIContent(label.text, "このレイヤーはレイヤーの一覧にありません。UIスタジオの「レイヤー」ページで確認してください。"));
                GUI.color = old;
                return;
            }

            EditorGUI.BeginChangeCheck();
            var chosen = EditorGUI.Popup(position, new GUIContent(label.text, label.tooltip), index, names);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = ids[chosen];
            }
        }
    }
}
