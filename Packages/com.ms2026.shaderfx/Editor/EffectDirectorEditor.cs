using UnityEditor;
using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    [CustomEditor(typeof(EffectDirector))]
    public sealed class EffectDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var director = (EffectDirector)target;

            EditorGUILayout.HelpBox("EffectDirector は自動生成される管理役です。手動で編集する項目はありません。", MessageType.Info);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Registered Targets", director.RegisteredTargetCount.ToString());
            EditorGUILayout.LabelField("Cached Materials", director.CachedMaterialCount.ToString());

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tag Mappings", EditorStyles.boldLabel);
            if (director.TagMappings.Count == 0)
            {
                EditorGUILayout.LabelField("(none)");
            }
            else
            {
                foreach (var mapping in director.TagMappings)
                {
                    EditorGUILayout.LabelField(mapping.Key, mapping.Value != null ? mapping.Value.name : "(null)");
                }
            }
        }
    }
}
