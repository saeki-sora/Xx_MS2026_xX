using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// タイトルのキャラ一覧（<see cref="TitleCharacterRoster"/>）の Inspector。
    /// ・キャラごとに「左で見る」「右で見る」: 開いているタイトルのシーンの左右に、そのキャラを今の大きさ・位置で置いてみる（再生中でも使える）
    /// ・「今の左（右）のキャラの大きさ・位置を覚える」: シーンで子「Sprite」の Size・Position を動かした後に押すと、そのキャラの値として一覧に保存する
    ///   （再生中に調整して押しても残る。一覧はシーンではなくファイルなので）
    /// メニュー「Tools/タイトル/タイトルのキャラ一覧を開く」。
    /// </summary>
    [CustomEditor(typeof(TitleCharacterRoster))]
    internal sealed class TitleCharacterRosterEditor : Editor
    {
        [MenuItem("Tools/タイトル/タイトルのキャラ一覧を開く")]
        private static void SelectRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<TitleCharacterRoster>(TitleSceneBuilder.RosterPath);
            if (roster == null)
            {
                EditorUtility.DisplayDialog("タイトル", $"キャラの一覧 {TitleSceneBuilder.RosterPath} が見つかりません。", "OK");
                return;
            }
            Selection.activeObject = roster;
            EditorGUIUtility.PingObject(roster);
        }

        public override void OnInspectorGUI()
        {
            var roster = (TitleCharacterRoster)target;
            EditorGUILayout.HelpBox(
                "タイトルを開くたびに、立ち絵の入ったキャラから2人をランダムに選んで、左右に落としてきます。\n" +
                "大きさ・位置はキャラごと（高さ = 絵の高さ。画面の高さは 10.8。ずれ X = 画面の端の方へ、Y = 上へ）。\n" +
                "調整のしかた: 「左で見る」で置いてみる → シーンで CharacterLeft の子「Sprite」の Title Sprite Fit › Size と Position を動かす → 「今の左のキャラの大きさ・位置を覚える」。",
                MessageType.Info);

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomize"), new GUIContent("開くたびにランダム"));
            var list = serializedObject.FindProperty("characters");
            EditorGUILayout.PropertyField(list, new GUIContent("キャラ"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("シーンで見る・覚える", EditorStyles.boldLabel);
            var controller = FindController();
            if (controller == null)
            {
                EditorGUILayout.HelpBox("タイトルのシーン（Assets/Scenes/Title.unity）を開くと、ここから左右に置いて見られます。", MessageType.None);
                if (GUILayout.Button("タイトルのシーンを開く")) TitleSceneBuilder.OpenTitleScene();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("今の左のキャラの大きさ・位置を覚える")) Remember(roster, controller, false);
                if (GUILayout.Button("今の右のキャラの大きさ・位置を覚える")) Remember(roster, controller, true);
            }

            for (var i = 0; i < roster.characters.Length; i++)
            {
                var entry = roster.characters[i];
                if (entry == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = string.IsNullOrEmpty(entry.label) ? (entry.sprite != null ? entry.sprite.name : $"キャラ {i + 1}") : entry.label;
                    EditorGUILayout.LabelField(label, GUILayout.MinWidth(80));
                    using (new EditorGUI.DisabledScope(entry.sprite == null))
                    {
                        if (GUILayout.Button("左で見る", GUILayout.Width(80))) Show(controller, entry, false);
                        if (GUILayout.Button("右で見る", GUILayout.Width(80))) Show(controller, entry, true);
                    }
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("再生中でも使えます。再生中にシーンで動かした値は止めると戻りますが、「覚える」で一覧に入れた値は残ります。", MessageType.None);
            }
        }

        private static TitleScreenController FindController() =>
            Object.FindFirstObjectByType<TitleScreenController>(FindObjectsInactive.Include);

        private static void Show(TitleScreenController controller, TitleCharacterRoster.Entry entry, bool rightSide)
        {
            var slot = controller.CharacterSlot(rightSide);
            var sprite = slot != null ? slot.transform.Find("Sprite") : null;
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("タイトル", $"{(rightSide ? "右" : "左")}のキャラの置き場所に子「Sprite」がありません（3D モデルのときは使えません）。", "OK");
                return;
            }

            Undo.RecordObjects(new Object[] { sprite, sprite.GetComponent<SpriteRenderer>(), sprite.GetComponent<TitleSpriteFit>() }
                .Where(o => o != null).ToArray(), "キャラを置いて見る");
            TitleCharacterRoster.ApplyTo(slot.transform, entry, rightSide);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Selection.activeObject = sprite.gameObject;
            EditorGUIUtility.PingObject(sprite.gameObject);
        }

        private static void Remember(TitleCharacterRoster roster, TitleScreenController controller, bool rightSide)
        {
            var side = rightSide ? "右" : "左";
            var slot = controller.CharacterSlot(rightSide);
            var sprite = slot != null ? slot.transform.Find("Sprite") : null;
            var renderer = sprite != null ? sprite.GetComponent<SpriteRenderer>() : null;
            var fit = sprite != null ? sprite.GetComponent<TitleSpriteFit>() : null;
            if (renderer == null || renderer.sprite == null || fit == null)
            {
                EditorUtility.DisplayDialog("タイトル", $"{side}のキャラの置き場所に、立ち絵（子「Sprite」）がありません。", "OK");
                return;
            }

            var entry = roster.characters.FirstOrDefault(e => e != null && e.sprite == renderer.sprite);
            if (entry == null)
            {
                EditorUtility.DisplayDialog("タイトル", $"今{side}にいる絵（{renderer.sprite.name}）は一覧に入っていません。先に一覧の「キャラ」に足してください。", "OK");
                return;
            }

            Undo.RecordObject(roster, "キャラの大きさ・位置を覚える");
            entry.height = fit.size;
            entry.offset = TitleCharacterRoster.OffsetFrom(sprite.localPosition, rightSide);
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssetIfDirty(roster);
            Debug.Log($"[Title] {(string.IsNullOrEmpty(entry.label) ? renderer.sprite.name : entry.label)} の大きさ {entry.height}・ずれ {entry.offset} を一覧に覚えました（{side}で調整した値）。");
        }
    }
}
