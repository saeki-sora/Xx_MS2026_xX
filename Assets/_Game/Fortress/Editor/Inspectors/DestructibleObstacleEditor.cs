using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>破壊可能物のInspector。要塞デザイナーの詳細編集と同じセクション分けで表示する。</summary>
    [CustomEditor(typeof(DestructibleObstacle))]
    [CanEditMultipleObjects]
    public sealed class DestructibleObstacleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "大きさはこのオブジェクトのスケールで決まります（見た目・当たり判定が追従）。" +
                "要塞デザイナーの「破壊可能物」タブでは、配置・一括編集・プリセット保存ができます。",
                MessageType.None);

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sourcePreset"));
            serializedObject.ApplyModifiedProperties();

            DrawRuntimeState();
            DestructibleSettingsDrawer.Draw(serializedObject);
        }

        private void DrawRuntimeState()
        {
            if (!Application.isPlaying || targets.Length != 1)
            {
                return;
            }

            var obstacle = (DestructibleObstacle)target;
            EditorGUILayout.LabelField(
                "状態",
                obstacle.IsDestroyed
                    ? $"破壊中（再生まで {obstacle.RegenProgress01:P0}）"
                    : $"耐久 {obstacle.CurrentHealth:0.#} / {obstacle.MaxHealth:0.#}" + (obstacle.IsInvulnerable ? "（無敵）" : ""));
            Repaint();
        }
    }
}
