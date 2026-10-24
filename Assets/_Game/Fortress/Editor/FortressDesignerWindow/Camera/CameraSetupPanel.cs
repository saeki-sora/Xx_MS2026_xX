using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>カメラリグの有無・構成ミスを表示し、ワンクリックでセットアップ/修復する。ステージが開いていれば、どのステージの視点を編集しているかも出す。</summary>
    public sealed class CameraSetupPanel
    {
        /// <summary>リグが使える状態ならtrue(falseなら他のパネルは描かない)。</summary>
        public bool Draw(CameraTabContext context)
        {
            DrawStrayRigs(context);

            if (context.Rig == null)
            {
                if (context.Stage != null)
                {
                    DrawStageWithoutGame(context.Stage);
                }
                else
                {
                    DrawNoRig();
                }

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

            if (!Application.isPlaying)
            {
                DrawStageOwnership(context);
            }

            return context.Preset != null;
        }

        /// <summary>ステージのシーンに置かれたリグ（Play では使われないので、ここで視点を変えても反映されない）。</summary>
        private static void DrawStrayRigs(CameraTabContext context)
        {
            if (context.StrayRigs == null || context.StrayRigs.Count == 0)
            {
                return;
            }

            foreach (var stray in context.StrayRigs)
            {
                EditorGUILayout.HelpBox(
                    $"ステージのシーン「{stray.gameObject.scene.name}」にカメラリグ「{stray.name}」があります。カメラは共通の Game のシーンにだけ置きます。" +
                    "このリグの視点を変えても Play では使われません（Play では Game のリグに、ステージの「配置の目印」の視点が当てはめられます）。",
                    MessageType.Error);
            }

            if (!Application.isPlaying && GUILayout.Button(new GUIContent("ステージのシーンにあるカメラリグを消す",
                    "リグのオブジェクトを子ごと消します（Undo で戻せます）。消した後でステージのシーンを保存してください。")))
            {
                foreach (var stray in context.StrayRigs)
                {
                    if (stray != null)
                    {
                        Undo.DestroyObjectImmediate(stray.gameObject);
                    }
                }
            }
        }

        /// <summary>ステージだけを開いているとき。ここでリグを作るとステージのシーンに入ってしまうので、Game を一緒に開いてもらう。</summary>
        private static void DrawStageWithoutGame(CameraStageInfo stage)
        {
            EditorGUILayout.HelpBox(
                $"ステージ{stage.Label}だけが開いています。カメラ（カメラリグ）は共通の Game のシーンにあり、ステージの視点はステージの「配置の目印」が持っています。" +
                "下のボタンで Game を一緒に開いてから、視点を編集してください（ここでカメラリグを作ると、ステージのシーンに入ってしまい、Play では使われません）。",
                MessageType.Info);

            if (Application.isPlaying)
            {
                return;
            }

            if (GUILayout.Button("Game を一緒に開く（ステージの視点を編集する）", GUILayout.Height(32)) && CameraStageBinding.Current != null)
            {
                CameraStageBinding.Current.OpenCommonScene();
            }
        }

        /// <summary>どの視点を編集しているか（ステージの物か、Game の物か）と、ほかのステージと共用していないか。</summary>
        private static void DrawStageOwnership(CameraTabContext context)
        {
            var binding = CameraStageBinding.Current;
            if (binding == null)
            {
                return;
            }

            var stage = context.Stage;
            if (stage == null)
            {
                var forPlay = binding.FindStageForPlay();
                if (forPlay == null)
                {
                    return;
                }

                var willChange = forPlay.Preset != null && forPlay.Preset != context.Preset;
                var users = context.Preset != null ? binding.OtherStagesUsing(null, context.Preset) : null;
                EditorGUILayout.HelpBox(
                    "ステージのシーンが開いていません。ここで編集しているのは Game のカメラの視点です" +
                    (users != null && users.Count > 0 ? $"（この視点は {string.Join("・", users)} の視点でもあるので、変えるとそのステージも変わります）。" : "。") +
                    $"Play すると既定のステージ{forPlay.Label}が重ねて読み込まれ、" +
                    (willChange ? $"カメラはそのステージの視点（{forPlay.Preset.name}）に切り替わります（ここでの編集は映りません）。" : "カメラはそのステージの視点になります。") +
                    "ステージの視点を直すときは、そのステージを Game と一緒に開いてください。",
                    willChange ? MessageType.Warning : MessageType.Info);
                if (GUILayout.Button($"{forPlay.Label}を Game と一緒に開く"))
                {
                    binding.OpenWithCommonScene(forPlay);
                }

                return;
            }

            if (stage.Preset == null)
            {
                EditorGUILayout.HelpBox(
                    $"ステージ{stage.Label}には専用の視点がありません。Play では Game のカメラの視点（{(context.Preset != null ? context.Preset.name : "なし")}）のままになり、" +
                    "ここで編集すると、ほかのステージにも同じ変更が入ります。",
                    MessageType.Warning);
                if (context.Preset != null && GUILayout.Button("このステージ専用の視点を作る（今の視点を写す）"))
                {
                    MakeOwnPreset(context, stage);
                }

                return;
            }

            EditorGUILayout.LabelField($"編集中: ステージ{stage.Label}の視点（{stage.Preset.name}）", EditorStyles.miniBoldLabel);
            if (context.Preset != stage.Preset)
            {
                EditorGUILayout.HelpBox($"カメラに、このステージの視点（{stage.Preset.name}）ではなく「{(context.Preset != null ? context.Preset.name : "なし")}」が付いています。", MessageType.Warning);
                if (GUILayout.Button("このステージの視点を表示する"))
                {
                    binding.AssignPreset(stage, stage.Preset);
                }
            }

            var others = binding.OtherStagesUsing(stage, stage.Preset);
            if (others.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"この視点（{stage.Preset.name}）は {string.Join("・", others)} と共用です。ここで変えると、そのステージの視点も変わります。",
                    MessageType.Warning);
                if (GUILayout.Button("このステージ専用にする（今の視点を写す）"))
                {
                    MakeOwnPreset(context, stage);
                }
            }
        }

        private static void MakeOwnPreset(CameraTabContext context, CameraStageInfo stage)
        {
            var source = stage.Preset != null ? stage.Preset : context.Preset;
            var copy = CameraPresetEditing.CreatePresetAsset($"CameraViewPreset_{stage.SceneName}", source);
            Undo.RecordObject(copy, "Name Stage Camera Preset");
            copy.presetName = $"{(string.IsNullOrEmpty(stage.DisplayName) ? stage.SceneName : stage.DisplayName)}の視点";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            context.UsePreset(copy, 0f);
            Debug.Log($"[カメラ] ステージ{stage.Label}専用の視点「{AssetDatabase.GetAssetPath(copy)}」を作りました（もと: {(source != null ? source.name : "なし")}）。ステージのシーンを保存してください。");
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
