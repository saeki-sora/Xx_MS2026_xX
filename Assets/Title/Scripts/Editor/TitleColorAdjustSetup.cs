using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// タイトルの配置物に「色の調整」（<see cref="TitleColorAdjust"/>）を付ける。
    /// 色の調整は ShaderFX/Uber Sprite のシェーダーでしか効かないので、Unity 標準のスプライト用マテリアルの絵は Title_SpriteFX に替える
    /// （エフェクトの無い Uber Sprite なので、見た目は同じ）。タイトルシーンを組み立てるときにも行う。
    /// </summary>
    internal static class TitleColorAdjustSetup
    {
        private const string MenuPath = "Tools/タイトル/色の調整を付ける";
        private const string FxMaterialPath = TitleSceneBuilder.MaterialDir + "/Title_SpriteFX.mat";

        /// <summary>色の調整を付ける配置物（[Title] から見た場所）。背景の明るい方と暗い方は別々に調整できるよう分ける。</summary>
        internal static readonly string[] TargetPaths =
        {
            "Background", "Background/SpriteBefore", "Opening/Toothpaste", "Opening/PasteStream",
            "Logo", "CharacterLeft", "CharacterRight", "PressStart",
        };

        [MenuItem(MenuPath)]
        private static void Menu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("タイトル", "再生中は付けられません。再生を止めてから実行してください。", "OK");
                return;
            }

            var (added, swapped, saved) = EnsureInTitleScene();
            EditorUtility.DisplayDialog("タイトル", added + swapped > 0
                ? $"色の調整を {added} か所に付け、絵のマテリアルを {swapped} 枚替えました。" + (saved ? "タイトルのシーンを保存しました。" : "保存していない変更があったので、シーンを保存してください。") +
                  "\n\n調整は、進行役（[Title]）の Inspector の「⑤ 色の調整」か、各配置物の Title Color Adjust で行えます。"
                : "もう全部付いています。調整は、進行役（[Title]）の Inspector の「⑤ 色の調整」で行えます。", "OK");
        }

        /// <summary>
        /// Title.unity に色の調整を付けて保存する。開いていなければ裏で開いて閉じる。
        /// タイトルのシーンを開いていて保存していない変更があるときは、保存しない（saved = false）。
        /// </summary>
        internal static (int added, int swapped, bool saved) EnsureInTitleScene()
        {
            var scene = SceneManager.GetSceneByPath(TitleSceneBuilder.ScenePath);
            var wasOpen = scene.IsValid() && scene.isLoaded;
            var hadChanges = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(TitleSceneBuilder.ScenePath, OpenSceneMode.Additive);

            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == TitleSceneBuilder.RootName);
                if (root == null) return (0, 0, false);
                var (added, swapped) = Ensure(root.transform, true);
                if (added + swapped == 0) return (0, 0, true);
                EditorSceneManager.MarkSceneDirty(scene);
                if (hadChanges) return (added, swapped, false);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Title] 色の調整を {added} か所に付け、絵のマテリアルを {swapped} 枚替えて、タイトルのシーンを保存しました。");
                return (added, swapped, true);
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>配置物に色の調整を付け、対応していないマテリアルの絵を Title_SpriteFX に替える。付けた数・替えた数を返す。</summary>
        internal static (int added, int swapped) Ensure(Transform root, bool recordUndo)
        {
            var added = 0;
            foreach (var path in TargetPaths)
            {
                var t = root.Find(path);
                if (t == null || t.GetComponent<TitleColorAdjust>() != null) continue;
                if (recordUndo) Undo.AddComponent<TitleColorAdjust>(t.gameObject);
                else t.gameObject.AddComponent<TitleColorAdjust>();
                added++;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
            var swapped = 0;
            if (material == null) return (added, swapped);
            foreach (var adjust in root.GetComponentsInChildren<TitleColorAdjust>(true))
            {
                foreach (var r in adjust.Renderers)
                {
                    if (!IsPlainSpriteMaterial(r.sharedMaterial)) continue;
                    if (recordUndo) Undo.RecordObject(r, "色の調整を付ける");
                    r.sharedMaterial = material;
                    swapped++;
                }
            }
            return (added, swapped);
        }

        /// <summary>Unity 標準のスプライト用（色の調整が効かない、エフェクトも無い）マテリアルか。独自のシェーダーは替えない。</summary>
        internal static bool IsPlainSpriteMaterial(Material m)
        {
            if (m == null) return true;
            if (m.HasProperty("_ColorAdjustOn")) return false;
            var shader = m.shader != null ? m.shader.name : "";
            return shader.EndsWith("Sprite-Unlit-Default") || shader.EndsWith("Sprite-Lit-Default") || shader == "Sprites/Default";
        }

        /// <summary>Title_SpriteFX（無ければ null）。</summary>
        internal static Material FxMaterial => AssetDatabase.LoadAssetAtPath<Material>(FxMaterialPath);
    }

    [CustomEditor(typeof(TitleColorAdjust))]
    [CanEditMultipleObjects]
    internal sealed class TitleColorAdjustEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TitleInspectorButtons.Draw();
            DrawDefaultInspector();
            if (GUILayout.Button("元に戻す（調整なし）"))
            {
                foreach (var t in targets)
                {
                    Undo.RecordObject(t, "色の調整を元に戻す");
                    ((TitleColorAdjust)t).ResetValues();
                    EditorUtility.SetDirty(t);
                }
            }
            if (targets.Length == 1) TitleColorAdjustGUI.DrawUnsupportedFix((TitleColorAdjust)target);
        }
    }

    internal static class TitleColorAdjustGUI
    {
        /// <summary>色の調整が効かない絵があるときの注意書きと、マテリアルを替えるボタン。</summary>
        public static void DrawUnsupportedFix(TitleColorAdjust adjust)
        {
            var unsupported = adjust.UnsupportedCount;
            if (unsupported == 0) return;
            EditorGUILayout.HelpBox($"この中の {unsupported} 枚の絵は、シェーダーが色の調整に対応していないので変わりません（ShaderFX/Uber Sprite のマテリアルが必要）。", MessageType.Warning);
            var material = TitleColorAdjustSetup.FxMaterial;
            using (new EditorGUI.DisabledScope(material == null || EditorApplication.isPlaying))
            {
                if (!GUILayout.Button("標準のスプライト用の絵を Title_SpriteFX に替える")) return;
                foreach (var r in adjust.Renderers)
                {
                    if (!TitleColorAdjustSetup.IsPlainSpriteMaterial(r.sharedMaterial)) continue;
                    Undo.RecordObject(r, "色の調整を付ける");
                    r.sharedMaterial = material;
                }
            }
        }
    }
}
