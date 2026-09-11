using UnityEditor;
using UnityEngine;

namespace MS2026.ShaderFX.Editor
{
    [CustomEditor(typeof(EffectTarget))]
    [CanEditMultipleObjects]
    public sealed class EffectTargetEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProp;
        private SerializedProperty groupProp;
        private SerializedProperty rendererProp;

        private void OnEnable()
        {
            profileProp = serializedObject.FindProperty("profile");
            groupProp = serializedObject.FindProperty("group");
            rendererProp = serializedObject.FindProperty("targetRenderer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(profileProp, new GUIContent("Effect Profile"));
            EditorGUILayout.PropertyField(groupProp, new GUIContent("Group"));
            EditorGUILayout.PropertyField(rendererProp, new GUIContent("Target Renderer (Auto)"));

            if (profileProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Effect Profile が未設定です。ShaderFX/Effect Profile アセットを割り当ててください。", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                var effectTarget = (EffectTarget)target;
                if (effectTarget.TargetRenderer == null)
                {
                    EditorGUILayout.HelpBox("Renderer が見つかりません。Renderer を持つオブジェクトに付けてください。", MessageType.Error);
                }
                else if (Application.isPlaying)
                {
                    DrawRuntimeStatus(effectTarget);
                }
            }
        }

        private static void DrawRuntimeStatus(EffectTarget effectTarget)
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                var material = effectTarget.TargetRenderer.sharedMaterial;
                EditorGUILayout.LabelField("Applied Material", material != null ? material.name : "-");

                bool overridden = effectTarget.EffectiveProfile != effectTarget.Profile;
                EditorGUILayout.LabelField("Group Override Active", overridden ? $"Yes ({effectTarget.EffectiveProfile?.name})" : "No");

                if (EffectDirector.HasInstance)
                {
                    EditorGUILayout.LabelField("Director Cached Materials", EffectDirector.Instance.CachedMaterialCount.ToString());
                    EditorGUILayout.LabelField("Director Registered Targets", EffectDirector.Instance.RegisteredTargetCount.ToString());
                }
            }
        }
    }
}
