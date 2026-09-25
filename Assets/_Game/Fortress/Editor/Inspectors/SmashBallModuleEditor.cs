using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>スマッシュボールのInspector。要塞デザイナーの「スマッシュボール」タブと同じセクション分けで表示する。</summary>
    [CustomEditor(typeof(SmashBallModule))]
    [CanEditMultipleObjects]
    public sealed class SmashBallModuleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "耐久・見た目・演出などは同じオブジェクトの破壊可能物の設定（DestructibleObstacle）で編集します。" +
                "ここでは「割れたら誰が割ったか記録する・特別な演出を足す」など、スマッシュボール固有の項目だけを扱います。",
                MessageType.None);

            if (Application.isPlaying && targets.Length == 1)
            {
                DrawRuntimeState();
            }

            SmashBallSettingsDrawer.Draw(serializedObject);
        }

        private void DrawRuntimeState()
        {
            var module = (SmashBallModule)target;
            var obstacle = module.Obstacle;
            if (obstacle == null)
            {
                return;
            }

            var state = module.IsWaitingInRotation
                ? "待機中（他のスマッシュボールが出現中）"
                : obstacle.IsDestroyed
                    ? $"割れた（{obstacle.DestroyedBy.DisplayName}）"
                    : "出現中";
            EditorGUILayout.LabelField("状態", state);
            Repaint();
        }
    }
}
