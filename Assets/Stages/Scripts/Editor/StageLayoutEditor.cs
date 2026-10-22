using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stages.EditorTools
{
    /// <summary>配置の目印のインスペクター（説明と「今の砲台・コアの位置を写す」ボタン）。</summary>
    [CustomEditor(typeof(StageLayout))]
    internal sealed class StageLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "このステージで、コアと砲台 4 台をどこに置くか・カメラの視点をどれにするかを決めます。\n" +
                "子の Core・Turret_P1〜P4 を動かす／回す（Z の回転＝砲台の向き）と、ゲームの始めに Game のコア・砲台がそこへ移ります。\n" +
                "砲台やコアの位置を変えたら、カメラの視点（プレイヤーごとの画面の向き）も合わせて作ってください。",
                MessageType.None);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button(new GUIContent("Game のコア・砲台をこの目印に並べる（編集画面）",
                        "Game も開いているとき、Game のコア・砲台 4 台とカメラの視点を、この目印の位置・向き・視点にして見せる（Play で動く位置と同じ）。" +
                        "見せるだけで、Game のファイルには保存されない。ステージを Game と一緒に開いたときは自動で並ぶ。")))
                {
                    if (!StageEditPreview.Place((StageLayout)target, true))
                    {
                        EditorUtility.DisplayDialog("配置の目印", "コア・砲台が見つかりません。Game のシーンも一緒に開いてください（Tools › ステージ › 選んだステージを Game と一緒に開く）。", "OK");
                    }
                }

                if (GUILayout.Button(new GUIContent("今の砲台・コアの位置を目印に写す", "Game も開いているとき、Game のコア・砲台の今の位置と向きを、この目印に写す。")))
                {
                    CopyFromScene((StageLayout)target);
                }
            }
        }

        private static void CopyFromScene(StageLayout layout)
        {
            var core = Object.FindObjectsByType<CoreCrystalController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (core == null && turrets.Length == 0)
            {
                EditorUtility.DisplayDialog("配置の目印", "コア・砲台が見つかりません。Game のシーンも一緒に開いてください（Tools › ステージ › 選んだステージを Game と一緒に開く）。", "OK");
                return;
            }

            if (core != null && layout.coreAnchor != null)
            {
                Undo.RecordObject(layout.coreAnchor, "目印に写す");
                layout.coreAnchor.position = core.transform.position;
            }

            foreach (var turret in turrets)
            {
                var anchor = layout.AnchorFor(turret.playerIndex);
                if (anchor == null)
                {
                    continue;
                }

                Undo.RecordObject(anchor, "目印に写す");
                anchor.SetPositionAndRotation(turret.transform.position, Quaternion.Euler(0f, 0f, turret.transform.eulerAngles.z));
            }

            EditorUtility.SetDirty(layout);
        }
    }
}
