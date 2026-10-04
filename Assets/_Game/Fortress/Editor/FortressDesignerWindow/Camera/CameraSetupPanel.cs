using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>カメラリグの有無・構成ミスを表示し、ワンクリックでセットアップ/修復する。</summary>
    public sealed class CameraSetupPanel
    {
        /// <summary>リグが使える状態ならtrue(falseなら他のパネルは描かない)。</summary>
        public bool Draw(CameraTabContext context)
        {
            if (context.Rig == null)
            {
                DrawNoRig();
                return false;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"カメラリグ: {context.Rig.name}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("シーンで選択", GUILayout.Width(90)))
                {
                    Selection.activeGameObject = context.Rig.gameObject;
                }

                if (GUILayout.Button(new GUIContent("足りない部品を追加", "演出・構図ガイド・デバッグキーの部品と、未割り当てのアセットを追加します。既にある物には触りません。"), GUILayout.Width(130)))
                {
                    CameraRigSetup.EnsureParts(context.Rig);
                }
            }

            foreach (var (type, message) in CameraRigSetup.Diagnose(context))
            {
                EditorGUILayout.HelpBox(message, type);
            }

            if (!Application.isPlaying && context.Rig.targetCamera != null && !CameraRigSetup.IsCameraDirectChildAtOrigin(context.Rig)
                && GUILayout.Button("カメラをリグの「位置・回転ゼロの子」に直す"))
            {
                CameraRigSetup.FixCameraParenting(context.Rig);
            }

            return context.Preset != null;
        }

        private static void DrawNoRig()
        {
            EditorGUILayout.HelpBox(
                "シーンにカメラリグ(FortressCameraRig)がありません。下のボタンで、今のMain Cameraをリグの子に入れ、" +
                "視点プリセット・演出設定・構図ガイド・デバッグキーをまとめて用意します。今のカメラの見え方がそのまま全員の初期視点になるので、" +
                "セットアップしても画面は変わりません（Undoで戻せます）。",
                MessageType.Info);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("セットアップはPlay Modeを止めてから行ってください。", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("カメラリグをセットアップ", GUILayout.Height(32)))
            {
                CameraRigSetup.CreateRig();
            }
        }
    }
}
