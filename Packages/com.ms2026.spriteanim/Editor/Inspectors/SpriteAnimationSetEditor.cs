using UnityEditor;
using UnityEngine;

namespace MS2026.SpriteAnim.Editor
{
    [CustomEditor(typeof(SpriteAnimationSet))]
    public class SpriteAnimationSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Sprite Anim Studio で開く", GUILayout.Height(24)))
            {
                SpriteAnimStudioWindow.OpenWithSet((SpriteAnimationSet)target);
            }
        }
    }
}
