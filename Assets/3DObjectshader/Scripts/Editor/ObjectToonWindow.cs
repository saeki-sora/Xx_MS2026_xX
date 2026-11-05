using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 3Dオブジェクトのトゥーン調整ウィンドウ（Tools › 3Dオブジェクト トゥーン › 調整ウィンドウ）。
    /// ・選んでいる物: シーンでクリックした物だけを個別に調整する（その物専用の材質を作る）
    /// ・3Dの物 / 2Dの絵: 材質ごとのトゥーン材質と「共通の設定」をまとめて調整する
    /// ・開いている間はエディタでもトゥーンで見え、値を変えるとすぐシーンビュー・ゲームビューに出る
    /// 元の材質・シーン・Prefab は変えない（材質の差し替えは実行時とプレビュー中だけ）。
    /// </summary>
    public sealed class ObjectToonWindow : EditorWindow
    {
        private const string MenuRoot = "Tools/3Dオブジェクト トゥーン/";
        private static readonly string[] Tabs = { "選んでいる物", "3Dの物", "2Dの絵", "全体" };

        [SerializeField] private int tab;
        [SerializeField] private bool previewWanted = true;
        private Vector2 scroll;
        private bool showCommon = true;
        private bool onlySelected;
        private readonly Dictionary<Material, bool> expanded = new Dictionary<Material, bool>();
        private readonly Dictionary<Material, MaterialEditor> editors = new Dictionary<Material, MaterialEditor>();
        private int lastCommonDirty = -1;
        private int lastSpriteCommonDirty = -1;

        [MenuItem(MenuRoot + "調整ウィンドウ")]
        public static void Open()
        {
            var window = GetWindow<ObjectToonWindow>();
            window.titleContent = new GUIContent("3Dトゥーン調整");
            window.minSize = new Vector2(440, 380);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ObjectToonEditPreview.IsOn = previewWanted;
            }
        }

        // 閉じたらプレビューも止める（ほかの作業中にトゥーンの材質が入ったままにならないように）。
        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ObjectToonEditPreview.IsOn = false;
            }

            foreach (var editor in editors.Values)
            {
                if (editor != null)
                {
                    DestroyImmediate(editor);
                }
            }

            editors.Clear();
        }

        // 共通の設定が「元に戻す」やほかの画面で変わったときも配る。
        private void OnInspectorUpdate()
        {
            var profile = ObjectToonMaterials.LoadProfile();
            if (profile == null)
            {
                return;
            }

            if (WatchDirty(profile.commonSettings, ref lastCommonDirty))
            {
                ObjectToonMaterials.PushCommon(profile.commonSettings, profile.entries);
                Repaint();
            }

            if (WatchDirty(profile.spriteCommonSettings, ref lastSpriteCommonDirty))
            {
                ObjectToonMaterials.PushCommon(profile.spriteCommonSettings, profile.spriteEntries);
                Repaint();
            }
        }

        private static bool WatchDirty(Material material, ref int last)
        {
            if (material == null)
            {
                return false;
            }

            int dirty = EditorUtility.GetDirtyCount(material);
            bool changed = last >= 0 && dirty != last;
            last = dirty;
            return changed;
        }

        private void OnGUI()
        {
            if (ObjectToonMaterials.ToonShader == null || ObjectToonMaterials.SpriteShader == null)
            {
                EditorGUILayout.HelpBox("シェーダー（" + ObjectToonMaterials.ShaderName + " / " + ObjectToonMaterials.SpriteShaderName +
                                        "）が見つかりません。コンソールにシェーダーのエラーが出ていないか確認してください。", MessageType.Error);
                return;
            }

            var profile = ObjectToonMaterials.LoadProfile();
            if (profile == null)
            {
                EditorGUILayout.HelpBox("ステージの3Dオブジェクトを、トゥーン（アニメ塗り＋輪郭線）で描くための調整画面です。\n" +
                                        "元の材質・シーン・Prefab は変えず、ゲームを動かしたときだけトゥーンの材質に差し替えます。\n" +
                                        "まず下のボタンで設定ファイルを作ってください（Assets/3DObjectshader の中に作られます）。", MessageType.Info);
                if (GUILayout.Button("はじめる（設定ファイルを作る）", GUILayout.Height(32)))
                {
                    ObjectToonMaterials.EnsureProfile();
                }

                return;
            }

            if (profile.commonSettings == null || profile.spriteCommonSettings == null)
            {
                if (GUILayout.Button("「共通の設定」の材質を作る（3D・2D）"))
                {
                    ObjectToonMaterials.EnsureProfile();
                }
            }

            DrawPreviewBar();
            tab = GUILayout.Toolbar(tab, Tabs);
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0:
                    DrawSelected(profile);
                    break;
                case 1:
                    DrawProps(profile);
                    break;
                case 2:
                    DrawSprites(profile);
                    break;
                default:
                    DrawGeneral(profile);
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPreviewBar()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                bool preview = EditorGUILayout.ToggleLeft("このウィンドウを開いている間、エディタでもトゥーンで見る（保存はされない）", ObjectToonEditPreview.IsOn);
                if (preview != ObjectToonEditPreview.IsOn)
                {
                    previewWanted = preview;
                    ObjectToonEditPreview.IsOn = preview;
                }
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("実行中です。ここで変えた値はすぐゲームに出て、止めたあとも残ります。", MessageType.None);
            }
            else if (ObjectToonEditPreview.IsOn)
            {
                EditorGUILayout.HelpBox("プレビュー中は、ステージ背景スタジオの「専用シェーダーに切り替え」や、Prefab の「Overrides › Apply」を押さないでください（保存は自動で元に戻してから書き込みます）。", MessageType.None);
            }
        }

        // ───────── 選んでいる物 ─────────

        private void DrawSelected(ObjectToonProfile profile)
        {
            var go = Selection.activeGameObject;
            if (go == null || !go.scene.IsValid() || !go.scene.isLoaded)
            {
                EditorGUILayout.HelpBox("シーンビューかヒエラルキーで、調整したい物（石けん・コップ・鏡など）をクリックしてください。\n" +
                                        "その物だけの材質を作って、ほかの同じ材質の物とは別に調整できます。", MessageType.Info);
                return;
            }

            var renderers = new List<Renderer>();
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer)
                {
                    renderers.Add(renderer);
                }
            }

            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("「" + go.name + "」には描画する部品（MeshRenderer / SpriteRenderer）がありません。", MessageType.Info);
                return;
            }

            if (renderers.Count > 8)
            {
                EditorGUILayout.HelpBox("中に描画する物が " + renderers.Count + " 個あります。最初の8個だけ出しています（子の物を直接選ぶと出ます）。", MessageType.None);
                renderers.RemoveRange(8, renderers.Count - 8);
            }

            foreach (var renderer in renderers)
            {
                DrawRenderer(profile, renderer);
            }
        }

        private void DrawRenderer(ObjectToonProfile profile, Renderer renderer)
        {
            var sceneName = renderer.gameObject.scene.name;
            var path = ObjectToonApplier.GetPath(renderer.transform);
            bool sprite = renderer is SpriteRenderer;
            var materials = renderer.sharedMaterials;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(renderer.name + (sprite ? "（2Dの絵）" : "（3Dの物）"), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(sceneName + " / " + path, EditorStyles.miniLabel);
                bool sceneIsTarget = sprite ? profile.spriteScenes.Contains(sceneName) : profile.targetScenes.Contains(sceneName);
                if (!sceneIsTarget)
                {
                    EditorGUILayout.HelpBox("このシーン（" + sceneName + "）はトゥーンにするシーンに入っていないので、ここで調整しても使われません（" +
                                            (sprite ? "「2Dの絵」" : "「全体」") + "タブで追加できます）。", MessageType.Warning);
                }

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    DrawSlot(profile, sceneName, path, slot, materials[slot], sprite);
                }
            }
        }

        private void DrawSlot(ObjectToonProfile profile, string sceneName, string path, int slot, Material current, bool sprite)
        {
            var entries = sprite ? profile.spriteEntries : profile.entries;
            var own = profile.FindOverride(sceneName, path, slot);
            var original = ResolveOriginal(profile, own, current);
            var entry = original != null ? profile.FindByOriginal(entries, original) : null;

            EditorGUILayout.Space(2);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("材質 " + slot + "（元）", original, typeof(Material), false);
            }

            if (own != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.ToggleLeft("この物だけの材質を使う", own.enabled, EditorStyles.boldLabel);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(profile, "この物だけの材質");
                        own.enabled = enabled;
                        EditorUtility.SetDirty(profile);
                        ObjectToonEditPreview.Refresh();
                    }

                    if (GUILayout.Button("個別の調整をやめる", GUILayout.Width(130)) &&
                        EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", "この物の個別の調整をやめて、材質ごとの設定に戻しますか？\n（作った材質のファイルは消さずに残します）", "やめる", "キャンセル"))
                    {
                        Undo.RecordObject(profile, "個別の調整をやめる");
                        profile.objectOverrides.Remove(own);
                        EditorUtility.SetDirty(profile);
                        ObjectToonEditPreview.Refresh();
                        GUIUtility.ExitGUI();
                    }
                }

                if (own.toon != null)
                {
                    DrawMaterialEditor(profile, own.toon);
                }

                return;
            }

            if (entry != null && entry.toon != null)
            {
                EditorGUILayout.LabelField("今は材質ごとの設定（同じ材質の物すべてと同じ）: " + entry.toon.name, EditorStyles.wordWrappedMiniLabel);
            }
            else if (original == null || !ObjectToonMaterials.IsSourceCandidate(original, sprite))
            {
                EditorGUILayout.LabelField("この材質はトゥーンの対象外です（" + (original != null && original.shader != null ? original.shader.name : "なし") + "）", EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool canCreate = original != null && (entry != null || ObjectToonMaterials.IsSourceCandidate(original, sprite));
                using (new EditorGUI.DisabledScope(!canCreate))
                {
                    if (GUILayout.Button("この物だけ個別に調整する"))
                    {
                        CreateOverride(profile, sceneName, path, slot, original, entry, sprite);
                        GUIUtility.ExitGUI();
                    }
                }

                if (entry != null && entry.toon != null && GUILayout.Button("同じ材質の物をまとめて調整", GUILayout.Width(170)))
                {
                    tab = sprite ? 2 : 1;
                    onlySelected = true;
                    expanded[entry.toon] = true;
                }
            }
        }

        private static Material ResolveOriginal(ObjectToonProfile profile, ObjectToonProfile.ObjectOverride own, Material current)
        {
            if (own != null && own.original != null)
            {
                return own.original;
            }

            var entry = current != null ? profile.FindByToon(current) : null;
            return entry != null ? entry.original : current;
        }

        private static void CreateOverride(ObjectToonProfile profile, string sceneName, string path, int slot, Material original,
            ObjectToonProfile.Entry entry, bool sprite)
        {
            Material source = entry != null ? entry.toon : null;
            if (source == null && sprite)
            {
                source = profile.spriteCommonSettings;
            }

            var objectName = path.Substring(path.LastIndexOf('/') + 1);
            var toon = ObjectToonMaterials.CreateObjectMaterial(source, original, sceneName + "_" + objectName, slot);
            Undo.RecordObject(profile, "この物だけ個別に調整する");
            profile.objectOverrides.Add(new ObjectToonProfile.ObjectOverride
            {
                scene = sceneName, path = path, slot = slot, original = original, toon = toon,
            });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            ObjectToonEditPreview.Refresh();
        }

        // ───────── 3Dの物 ─────────

        private void DrawProps(ObjectToonProfile profile)
        {
            if (GUILayout.Button("シーンの材質を探してトゥーン材質を作る（3D）"))
            {
                ScanAndCreate(profile, sprite: false);
            }

            EditorGUILayout.Space(6);
            DrawCommon(profile, profile.commonSettings, "共通の設定（3Dの物すべてに配る）",
                "ここで変えた値は「共通に合わせる」がオンの物すべてにすぐ反映されます（基本の色・鏡・ステージ用の値は物ごと）。");
            EditorGUILayout.Space(6);
            DrawEntries(profile, profile.entries, sprite: false);
        }

        // ───────── 2Dの絵 ─────────

        private void DrawSprites(ObjectToonProfile profile)
        {
            EditorGUILayout.HelpBox("SpriteRenderer で描いている絵（砲台・コア・壊せる物など）を、3Dの物のトゥーンに合わせます（輪郭線・明暗の段・彩度）。\n" +
                                    "群れの敵は専用の描き方（Fortress の群衆の描画）なので、ここでは変わりません。\n" +
                                    "輪郭線は絵の四角の中にしか描けないので、絵の端まで色がある絵は Sprite の Mesh Type を Full Rect にし、余白をあけてください。", MessageType.None);

            EditorGUI.BeginChangeCheck();
            bool on = EditorGUILayout.ToggleLeft("2Dの絵もトゥーンにする", profile.spriteEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "2Dの絵のトゥーン");
                profile.spriteEnabled = on;
                EditorUtility.SetDirty(profile);
                ObjectToonEditPreview.Refresh();
            }

            DrawSceneList(profile, profile.spriteScenes, "2Dの絵をトゥーンにするシーン（3Dのシーンを読み込んでいるときだけ）");

            if (GUILayout.Button("シーンの絵の材質を探してトゥーン材質を作る（2D）"))
            {
                ScanAndCreate(profile, sprite: true);
            }

            EditorGUILayout.Space(6);
            DrawCommon(profile, profile.spriteCommonSettings, "共通の設定（2Dの絵すべてに配る）",
                "ここで変えた値は「共通に合わせる」がオンの絵すべてにすぐ反映されます。");
            EditorGUILayout.Space(6);
            DrawEntries(profile, profile.spriteEntries, sprite: true);
        }

        // ───────── 全体 ─────────

        private void DrawGeneral(ObjectToonProfile profile)
        {
            EditorGUI.BeginChangeCheck();
            bool inPlay = EditorGUILayout.ToggleLeft("ゲームを動かしたときにトゥーンにする", profile.enabledInPlay);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "トゥーンのオンオフ");
                profile.enabledInPlay = inPlay;
                EditorUtility.SetDirty(profile);
            }

            DrawSceneList(profile, profile.targetScenes, "3Dの物をトゥーンにするシーン");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("個別に調整している物（" + profile.objectOverrides.Count + "）", EditorStyles.boldLabel);
            if (profile.objectOverrides.Count == 0)
            {
                EditorGUILayout.HelpBox("まだありません。「選んでいる物」タブで、物を選んで「この物だけ個別に調整する」を押すと増えます。", MessageType.None);
            }

            foreach (var item in profile.objectOverrides)
            {
                if (item == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(item.scene + " / " + item.path + "  [" + item.slot + "]", EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("選ぶ", GUILayout.Width(48)))
                    {
                        SelectByPath(item);
                    }
                }
            }
        }

        // ───────── 共通の部品 ─────────

        private void DrawCommon(ObjectToonProfile profile, Material common, string title, string help)
        {
            if (common == null)
            {
                return;
            }

            showCommon = EditorGUILayout.Foldout(showCommon, title, true, EditorStyles.foldoutHeader);
            if (!showCommon)
            {
                return;
            }

            EditorGUILayout.HelpBox(help, MessageType.None);
            DrawMaterialEditor(profile, common);
        }

        private void DrawEntries(ObjectToonProfile profile, List<ObjectToonProfile.Entry> entries, bool sprite)
        {
            EditorGUILayout.LabelField("材質ごと（" + entries.Count + "）", EditorStyles.boldLabel);
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("まだありません。上の「…材質を探してトゥーン材質を作る」を押してください。", MessageType.Info);
                return;
            }

            onlySelected = EditorGUILayout.ToggleLeft("シーンで選んでいる物の材質だけ表示する", onlySelected);
            var selectedMaterials = onlySelected ? CollectSelectedMaterials() : null;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部トゥーンにする"))
                {
                    SetAll(profile, entries, e => e.enabled = true);
                }

                if (GUILayout.Button("全部元に戻す"))
                {
                    SetAll(profile, entries, e => e.enabled = false);
                }
            }

            foreach (var entry in entries)
            {
                if (entry == null || entry.original == null)
                {
                    continue;
                }

                if (selectedMaterials != null && !selectedMaterials.Contains(entry.original) && !selectedMaterials.Contains(entry.toon))
                {
                    continue;
                }

                DrawEntry(profile, entry, sprite);
            }
        }

        private void DrawEntry(ObjectToonProfile profile, ObjectToonProfile.Entry entry, bool sprite)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.ToggleLeft(entry.original.name, entry.enabled, EditorStyles.boldLabel);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(profile, "トゥーンにする");
                        entry.enabled = enabled;
                        EditorUtility.SetDirty(profile);
                        ObjectToonEditPreview.Refresh();
                    }

                    if (GUILayout.Button("シーンで選ぶ", GUILayout.Width(84)))
                    {
                        SelectUsers(entry);
                    }
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("元の材質", entry.original, typeof(Material), false);
                    EditorGUILayout.ObjectField("トゥーン材質", entry.toon, typeof(Material), false);
                }

                if (entry.toon == null)
                {
                    if (GUILayout.Button("トゥーン材質を作り直す"))
                    {
                        Undo.RecordObject(profile, "トゥーン材質を作る");
                        entry.toon = sprite
                            ? ObjectToonMaterials.CreateSpriteToon(entry.original, profile.spriteCommonSettings)
                            : ObjectToonMaterials.CreateToon(entry.original, profile.commonSettings);
                        EditorUtility.SetDirty(profile);
                        AssetDatabase.SaveAssets();
                        ObjectToonEditPreview.Refresh();
                    }

                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool useCommon = EditorGUILayout.ToggleLeft("共通の設定に合わせる", entry.useCommon);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(profile, "共通に合わせる");
                        entry.useCommon = useCommon;
                        EditorUtility.SetDirty(profile);
                        if (useCommon)
                        {
                            ObjectToonMaterials.CopyCommon(sprite ? profile.spriteCommonSettings : profile.commonSettings, entry.toon);
                            ObjectToonMaterials.RepaintViews();
                        }
                    }

                    if (!sprite)
                    {
                        if (GUILayout.Button("元の色を読み直す", GUILayout.Width(110)))
                        {
                            Undo.RecordObject(entry.toon, "元の材質の色を読み直す");
                            ObjectToonMaterials.CopyFromOriginal(entry.original, entry.toon);
                            ObjectToonMaterials.RepaintViews();
                        }

                        if (GUILayout.Button("鏡にする", GUILayout.Width(70)))
                        {
                            Undo.RecordObject(entry.toon, "鏡のおすすめの値");
                            ObjectToonMaterials.ApplyMirrorPreset(entry.toon);
                            ObjectToonMaterials.RepaintViews();
                        }
                    }
                }

                if (!sprite && ObjectToonMaterials.LooksLikeMirror(entry.original) && entry.toon.GetFloat("_ReflectOn") < 0.5f)
                {
                    EditorGUILayout.HelpBox("元の材質は鏡（つや最大・金属）のようです。真っ白に見えるときは「鏡にする」を押してください。", MessageType.Info);
                }

                if (entry.useCommon && !sprite)
                {
                    DrawPerMaterialFields(entry.toon);
                }

                bool open = expanded.TryGetValue(entry.toon, out var value) && value;
                open = EditorGUILayout.Foldout(open, entry.useCommon ? "全部の値を見る（共通の値は共通の設定で変える）" : "個別に調整する", true);
                expanded[entry.toon] = open;
                if (open)
                {
                    DrawMaterialEditor(profile, entry.toon);
                }
            }
        }

        // 共通に合わせている材質でも、物ごとの値（色・裏面・透け穴）はここで変えられる。
        private static void DrawPerMaterialFields(Material toon)
        {
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField("基本の色", toon.GetColor("_BaseColor"));
            var texture = (Texture)EditorGUILayout.ObjectField("基本の色（テクスチャ）", toon.GetTexture("_BaseMap"), typeof(Texture), false);
            var emission = EditorGUILayout.ColorField(new GUIContent("自己発光の色"), toon.GetColor("_EmissionColor"), true, false, true);
            var cull = (UnityEngine.Rendering.CullMode)EditorGUILayout.EnumPopup("裏面を描くか（Off=両面）", (UnityEngine.Rendering.CullMode)(int)toon.GetFloat("_Cull"));
            bool noHoles = EditorGUILayout.Toggle("透け穴をあけない（床用）", toon.GetFloat("_StageNoHoles") > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(toon, "トゥーン材質の色");
                toon.SetColor("_BaseColor", color);
                toon.SetTexture("_BaseMap", texture);
                toon.SetColor("_EmissionColor", emission);
                toon.SetFloat("_Cull", (float)cull);
                toon.SetFloat("_StageNoHoles", noHoles ? 1f : 0f);
                ObjectToonMaterials.SetKeyword(toon, "_STAGE_NO_HOLES", noHoles);
                EditorUtility.SetDirty(toon);
                ObjectToonMaterials.RepaintViews();
            }
        }

        private void DrawMaterialEditor(ObjectToonProfile profile, Material material)
        {
            if (!editors.TryGetValue(material, out var editor) || editor == null)
            {
                editor = (MaterialEditor)Editor.CreateEditor(material, typeof(MaterialEditor));
                editors[material] = editor;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (editor.PropertiesGUI())
                {
                    ObjectToonMaterials.SyncOutlinePass(material);
                    ObjectToonMaterials.PushIfCommon(profile, material);
                    ObjectToonMaterials.RepaintViews();
                }
            }
        }

        private static void DrawSceneList(ObjectToonProfile profile, List<string> scenes, string label)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < scenes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var sceneName = EditorGUILayout.DelayedTextField(scenes[i]);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(profile, "トゥーンにするシーン");
                        scenes[i] = sceneName.Trim();
                        EditorUtility.SetDirty(profile);
                        ObjectToonEditPreview.Refresh();
                    }

                    if (GUILayout.Button("外す", GUILayout.Width(48)))
                    {
                        Undo.RecordObject(profile, "トゥーンにするシーン");
                        scenes.RemoveAt(i);
                        EditorUtility.SetDirty(profile);
                        ObjectToonEditPreview.Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("今のシーン（" + SceneManager.GetActiveScene().name + "）を追加"))
            {
                Undo.RecordObject(profile, "トゥーンにするシーン");
                if (!scenes.Contains(SceneManager.GetActiveScene().name))
                {
                    scenes.Add(SceneManager.GetActiveScene().name);
                }

                EditorUtility.SetDirty(profile);
                ObjectToonEditPreview.Refresh();
            }
        }

        private static void ScanAndCreate(ObjectToonProfile profile, bool sprite)
        {
            var report = new System.Text.StringBuilder();
            var entries = sprite ? profile.spriteEntries : profile.entries;
            int created = 0;
            foreach (var sceneName in sprite ? profile.spriteScenes : profile.targetScenes)
            {
                var scenePath = ObjectToonMaterials.FindScenePath(sceneName);
                if (scenePath == null)
                {
                    report.AppendLine("・シーン「" + sceneName + "」が見つかりません");
                    continue;
                }

                // 材質はシーンのファイルから探すので、保存していない変更は入らない。
                var open = SceneManager.GetSceneByPath(scenePath);
                if (open.IsValid() && open.isDirty)
                {
                    report.AppendLine("・「" + sceneName + "」は保存されていない変更があります。保存後にもう一度押すと、新しく置いた物の材質も見つかります");
                }

                foreach (var material in ObjectToonMaterials.FindSourceMaterials(scenePath, sprite))
                {
                    if (profile.FindByOriginal(entries, material) != null)
                    {
                        continue;
                    }

                    Undo.RecordObject(profile, "トゥーン材質を作る");
                    Material toon;
                    bool enabled = true;
                    if (sprite)
                    {
                        toon = ObjectToonMaterials.CreateSpriteToon(material, profile.spriteCommonSettings);
                    }
                    else
                    {
                        toon = ObjectToonMaterials.CreateToon(material, profile.commonSettings);
                        // ステージの物の専用シェーダーの材質だけ、はじめからトゥーンにする（URP 標準の材質は候補として並べるだけ）。
                        enabled = material.shader.name == ObjectToonMaterials.SourceShaderNames[0];
                    }

                    entries.Add(new ObjectToonProfile.Entry { original = material, toon = toon, enabled = enabled });
                    created++;
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            ObjectToonEditPreview.Refresh();

            report.Insert(0, "新しく作ったトゥーン材質: " + created + " 個\n（元の材質・シーン・Prefab は変えていません）\n\n");
            EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", report.ToString(), "OK");
        }

        private static void SetAll(ObjectToonProfile profile, List<ObjectToonProfile.Entry> entries, System.Action<ObjectToonProfile.Entry> change)
        {
            Undo.RecordObject(profile, "トゥーンのオンオフ");
            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    change(entry);
                }
            }

            EditorUtility.SetDirty(profile);
            ObjectToonEditPreview.Refresh();
        }

        private static HashSet<Material> CollectSelectedMaterials()
        {
            var result = new HashSet<Material>();
            foreach (var go in Selection.gameObjects)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            result.Add(material);
                        }
                    }
                }
            }

            return result;
        }

        // 開いているシーンで、この材質（元かトゥーン）を使っている物を選ぶ。
        private static void SelectUsers(ObjectToonProfile.Entry entry)
        {
            var users = new List<Object>();
            ForEachLoadedRenderer(renderer =>
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && (material == entry.original || material == entry.toon))
                    {
                        users.Add(renderer.gameObject);
                        break;
                    }
                }
            });

            SelectOrTell(users, "開いているシーンに、この材質を使っている物はありません（対象のシーンを開いてから押してください）。");
        }

        private static void SelectByPath(ObjectToonProfile.ObjectOverride item)
        {
            var users = new List<Object>();
            ForEachLoadedRenderer(renderer =>
            {
                if (renderer.gameObject.scene.name == item.scene && ObjectToonApplier.GetPath(renderer.transform) == item.path)
                {
                    users.Add(renderer.gameObject);
                }
            });

            SelectOrTell(users, "その物が見つかりません。シーン「" + item.scene + "」を開いているか、物の名前や並びが変わっていないか確認してください。");
        }

        private static void ForEachLoadedRenderer(System.Action<Renderer> action)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        action(renderer);
                    }
                }
            }
        }

        private static void SelectOrTell(List<Object> users, string message)
        {
            if (users.Count == 0)
            {
                EditorUtility.DisplayDialog("3Dオブジェクト トゥーン", message, "OK");
                return;
            }

            Selection.objects = users.ToArray();
            EditorGUIUtility.PingObject(users[0]);
        }
    }
}
