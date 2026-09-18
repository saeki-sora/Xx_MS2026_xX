using System;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// [SpawnPointId]の付いたstringフィールドを、シーン内のEnemySpawnPointから選ぶ
    /// ドロップダウンとして描画する。タイプミスによる「湧く位置が見つからない」事故を防ぐ。
    /// </summary>
    [CustomPropertyDrawer(typeof(SpawnPointIdAttribute))]
    public sealed class SpawnPointIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var knownIds = UnityEngine.Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None)
                .Select(sp => sp.ResolvedId)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            EditorGUI.BeginProperty(position, label, property);

            if (knownIds.Length == 0)
            {
                EditorGUI.HelpBox(position, "シーンにEnemySpawnPointがありません", MessageType.None);
                EditorGUI.EndProperty();
                return;
            }

            var currentValue = property.stringValue;
            var currentIndex = Array.IndexOf(knownIds, currentValue);

            string[] displayOptions;
            int selectedIndex;

            if (currentIndex >= 0)
            {
                displayOptions = knownIds;
                selectedIndex = currentIndex;
            }
            else if (string.IsNullOrEmpty(currentValue))
            {
                displayOptions = knownIds;
                selectedIndex = -1;
            }
            else
            {
                displayOptions = knownIds.Concat(new[] { currentValue + "  (見つかりません)" }).ToArray();
                selectedIndex = displayOptions.Length - 1;
            }

            var newIndex = EditorGUI.Popup(position, label.text, selectedIndex, displayOptions);
            if (newIndex >= 0 && newIndex < knownIds.Length)
            {
                property.stringValue = knownIds[newIndex];
            }

            EditorGUI.EndProperty();
        }
    }
}
