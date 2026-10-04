using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Play中のカメラの状態(誰の視点か・何で決まったか・補間中か)の表示と、他のプレイヤーの視点への一時切り替え。
    /// Play Mode外では、4人通信を1台のPCで確かめる方法を案内する。
    /// </summary>
    public sealed class CameraLivePanel
    {
        private const string TileLaunchExample = "-fortress-start host -fortress-player 0 -fortress-tile";

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("live", Application.isPlaying ? "Play中のカメラ" : "4人通信での確認方法", Application.isPlaying))
            {
                return;
            }

            if (!Application.isPlaying)
            {
                DrawMultiInstanceHint();
                return;
            }

            var rig = context.Rig;
            using (new EditorGUILayout.HorizontalScope())
            {
                CameraGui.ColorChip(ViewerIndex.Color(rig.CurrentViewer));
                EditorGUILayout.LabelField($"いまの視点: {ViewerIndex.LongLabel(rig.CurrentViewer)}（決め手: {rig.ViewerSourceLabel}）", EditorStyles.boldLabel);
            }

            EditorGUILayout.LabelField(
                $"補間中: {(rig.IsBlending ? "はい" : "いいえ")}   一時演出: {rig.ActiveModifierCount}件   " +
                $"D-Driveの揺れ: {(DDrive.Runtime.CameraShake.CameraFx.IsBound ? "使用可" : "未起動(簡易揺れで代用)")}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("他の人の視点で見る（このPCの画面だけ・一時的）", EditorStyles.miniBoldLabel);

            var overrideViewer = DebugViewerOverride.IsActive ? DebugViewerOverride.Viewer : int.MinValue;
            var picked = CameraGui.ViewerButtons(overrideViewer, 22f);
            if (picked != overrideViewer)
            {
                DebugViewerOverride.Set(picked);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!DebugViewerOverride.IsActive))
                {
                    if (GUILayout.Button("自動に戻す（通信・起動引数で決まる視点）"))
                    {
                        DebugViewerOverride.Clear();
                    }
                }

                if (GUILayout.Button(new GUIContent("補間・演出を打ち切る", "遷移中や揺れの途中でも、今の視点へ即座に合わせます。")))
                {
                    rig.SnapToTarget();
                }
            }

            EditorGUILayout.HelpBox("Gameビューでも F1〜F4=P1〜P4 / F5=全体 / F6=自動 / F7=構図ガイド で切り替えられます(Development Buildでも同じ)。", MessageType.None);
        }

        private static void DrawMultiInstanceHint()
        {
            EditorGUILayout.HelpBox(
                "各PCは、Host/Client開始時に選んだ自分のプレイヤー番号の視点で表示されます（未接続のときは起動引数 -fortress-player、" +
                "それも無ければリグの「既定の視点」）。1台のPCでビルドしたexeを4つ起動し、-fortress-tile を付けると画面を2x2に並べて、" +
                "4人それぞれの視点を同時に確認できます。例:",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(TileLaunchExample, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("コピー", GUILayout.Width(60)))
                {
                    EditorGUIUtility.systemCopyBuffer = TileLaunchExample;
                }
            }

            EditorGUILayout.LabelField("2人目以降は -fortress-start client -fortress-player 1〜3 -fortress-tile。", EditorStyles.miniLabel);
        }
    }
}
