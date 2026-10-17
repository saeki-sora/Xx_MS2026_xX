using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵の絵の向き」。画面を回した視点(画面の回転が0°以外)で、群衆の敵の絵を画面に合わせて正立させるかどうか。
    /// 設定は視点プリセットごと(enemiesFaceScreen)。実際の計算は各PCのシェーダーと並べ替えで行う(<see cref="EnemyScreenAlign"/>)。
    /// </summary>
    public sealed class CameraEnemyFacingPanel
    {
        private readonly List<string> _rolledViewers = new List<string>();

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("enemyFacing", "敵の絵の向き（画面を回した視点用）"))
            {
                return;
            }

            var preset = context.Preset;
            EditorGUI.BeginChangeCheck();
            var faceScreen = EditorGUILayout.ToggleLeft(
                new GUIContent("敵の絵を画面の向きに合わせる",
                    "ONなら、画面を回した人の画面でも敵の絵が横倒しにならず、絵の上が画面の上、左右の向きと重なり順も画面基準になります。" +
                    "各PCが自分の画面で計算するので、4台の通信でもそれぞれ正しく見え、当たり判定などゲームの中身は変わりません。"),
                preset.enemiesFaceScreen, EditorStyles.boldLabel);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(preset, "Toggle Enemies Face Screen");
                preset.enemiesFaceScreen = faceScreen;
                EditorUtility.SetDirty(preset);
            }

            _rolledViewers.Clear();
            foreach (var viewer in ViewerIndex.All)
            {
                var roll = Mathf.DeltaAngle(0f, preset.GetView(viewer).rollDegrees);
                if (Mathf.Abs(roll) > 0.5f)
                {
                    _rolledViewers.Add($"{ViewerIndex.Label(viewer)}({roll:0}°)");
                }
            }

            var status = _rolledViewers.Count == 0
                ? "このプリセットはどの視点も回転0°なので、ON/OFFで見た目は変わりません。"
                : $"画面を回している視点: {string.Join(" / ", _rolledViewers)}。" +
                  (faceScreen ? "これらの画面でも敵は正立して見えます。" : "OFFなので、これらの画面では敵の絵が回って(横倒しに)見えます。");
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField(
                    $"いまこのPCの画面: {(EnemyScreenAlign.Enabled ? "画面基準" : "ワールド基準")}（画面の上 = ({EnemyScreenAlign.ScreenUp.x:0.00}, {EnemyScreenAlign.ScreenUp.y:0.00})）",
                    EditorStyles.miniLabel);
            }
        }
    }
}
