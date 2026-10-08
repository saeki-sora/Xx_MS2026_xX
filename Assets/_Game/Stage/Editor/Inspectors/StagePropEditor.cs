using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>StageProp のインスペクター: 要点だけ見せて、細かい調整はスタジオへ案内する。</summary>
    [CustomEditor(typeof(StageProp))]
    [CanEditMultipleObjects]
    public sealed class StagePropEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var prop = (StageProp)target;
            EditorGUILayout.HelpBox(
                $"背景オブジェクト（{prop.role.DisplayName()}）\n{StageRoleStyle.Explain(prop.role)}\n" +
                "役割の変更・通れない範囲の作り直し・モデルの差し替えは、ステージ背景スタジオで行うと部品の付け外しまで自動で行われます。",
                MessageType.Info);

            if (GUILayout.Button("ステージ背景スタジオで開く", GUILayout.Height(26)))
            {
                StageStudioWindow.OpenPage<StagePropsPage>();
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
