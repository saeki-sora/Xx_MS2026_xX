using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.ObjectToon.KillFx.EditorTools
{
    /// <summary>
    /// 倒したときの演出のウィンドウ（Tools › 3Dオブジェクト トゥーン › 倒したときの演出）。
    /// 1 設定（エフェクト・効果音・光） / 2 手描き風エフェクトの素材を読み込む / 3 作ったエフェクトを D-Drive に登録する手順 / 4 実行中に試す。
    /// </summary>
    public sealed class KillFxWindow : EditorWindow
    {
        private const string SettingsPath = "Assets/3DObjectshader/Resources/ObjectToon/KillFxSettings.asset";
        private const string SourcesKey = "MS2026.KillFx.Sources";

        private Vector2 scroll;
        private Editor settingsEditor;
        private List<string> sources = new List<string>();
        private List<KillFxImporter.Sequence> found = new List<KillFxImporter.Sequence>();
        private readonly KillFxImporter.BuildOptions options = new KillFxImporter.BuildOptions();
        private static readonly int[] FrameSizes = { 128, 192, 256, 384, 512 };

        [MenuItem("Tools/3Dオブジェクト トゥーン/倒したときの演出")]
        public static void Open()
        {
            var window = GetWindow<KillFxWindow>();
            window.titleContent = new GUIContent("倒したときの演出");
            window.minSize = new Vector2(440, 420);
        }

        private void OnEnable()
        {
            sources = new List<string>(EditorPrefs.GetString(SourcesKey, "").Split('|'));
            sources.RemoveAll(string.IsNullOrEmpty);
        }

        private void OnDisable()
        {
            if (settingsEditor != null)
            {
                DestroyImmediate(settingsEditor);
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSettings();
            EditorGUILayout.Space(10);
            DrawImport();
            EditorGUILayout.Space(10);
            DrawBuilt();
            EditorGUILayout.Space(10);
            DrawTest();
            EditorGUILayout.EndScrollView();
        }

        // ───────── 1 設定 ─────────

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("1  設定", EditorStyles.boldLabel);
            var settings = AssetDatabase.LoadAssetAtPath<KillFxSettings>(SettingsPath);
            if (settings == null)
            {
                EditorGUILayout.HelpBox("群れの敵を倒したときに、手描き風のエフェクト・効果音・まわりを照らす光を出します。\n" +
                                        "群れの仕組みは変えず、「敵が倒れた」の知らせを聞いて出すだけです（各PCが自分で出すので通信はしません）。", MessageType.Info);
                if (GUILayout.Button("はじめる（設定ファイルを作る）", GUILayout.Height(28)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                    AssetDatabase.Refresh();
                    AssetDatabase.CreateAsset(CreateInstance<KillFxSettings>(), SettingsPath);
                    AssetDatabase.SaveAssets();
                }

                return;
            }

            if (EditorApplication.isPlaying && KillFxDirector.Instance == null)
            {
                EditorGUILayout.HelpBox("設定ファイルは、ゲームを動かし始めるときに読み込まれます。いったん止めて、もう一度動かしてください。", MessageType.Warning);
            }

            Editor.CreateCachedEditor(settings, null, ref settingsEditor);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                settingsEditor.OnInspectorGUI();
            }

            EditorGUILayout.HelpBox("光は床・ステージの物・3Dのトゥーンを照らします（群れの敵は照らしません）。3Dのトゥーンで光が弱いときは、" +
                                    "3Dトゥーン調整の共通の設定「追加の光の強さ」を上げてください。", MessageType.None);
        }

        // ───────── 2 素材を読み込む ─────────

        private void DrawImport()
        {
            EditorGUILayout.LabelField("2  手描き風エフェクトの素材を読み込む", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ダウンロードした素材の zip（展開したフォルダでもよい）から、コマを1枚に並べた画像とエフェクトの Prefab を作ります。\n" +
                                    "・作った画像は git に入りません（素材の利用規約で、加工したものも含めて再配布・ダウンロードできる状態での提供が禁止されていて、このリポジトリは公開されているため）。\n" +
                                    "・ほかのPCでも見たい人は、自分で同じ素材をダウンロードして、ここで読み込んでください（同じ名前なら同じものになります）。\n" +
                                    "・読み込んでいないPCでは、エフェクトは透明になるだけで、エラーにはなりません。", MessageType.Info);

            for (int i = 0; i < sources.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Path.GetFileName(sources[i]), EditorStyles.miniLabel);
                    if (GUILayout.Button("外す", GUILayout.Width(48)))
                    {
                        sources.RemoveAt(i);
                        SaveSources();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("zip を追加"))
                {
                    var path = EditorUtility.OpenFilePanel("素材の zip", LastFolder(), "zip");
                    AddSource(path);
                }

                if (GUILayout.Button("フォルダを追加"))
                {
                    var path = EditorUtility.OpenFolderPanel("素材のフォルダ", LastFolder(), "");
                    AddSource(path);
                }

                using (new EditorGUI.DisabledScope(sources.Count == 0))
                {
                    if (GUILayout.Button("中身を調べる"))
                    {
                        found = KillFxImporter.Scan(sources);
                        KillFxImporter.SelectRecommended(found);
                    }
                }
            }

            if (found.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("見つかったエフェクト（" + found.Count + "）", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("おすすめを選ぶ", GUILayout.Width(100)))
                {
                    KillFxImporter.SelectRecommended(found);
                }

                if (GUILayout.Button("全部外す", GUILayout.Width(70)))
                {
                    found.ForEach(s => s.selected = false);
                }
            }

            foreach (var sequence in found)
            {
                sequence.selected = EditorGUILayout.ToggleLeft(new GUIContent(sequence.Label, Path.GetFileName(sequence.source) + " / " + sequence.location), sequence.selected);
            }

            EditorGUILayout.Space(4);
            int sizeIndex = Mathf.Max(0, System.Array.IndexOf(FrameSizes, options.frameSize));
            sizeIndex = EditorGUILayout.Popup("1コマの大きさ（画素）", sizeIndex, System.Array.ConvertAll(FrameSizes, s => s + " px"));
            options.frameSize = FrameSizes[sizeIndex];
            options.fps = EditorGUILayout.Slider("1秒あたりのコマ数（素材のおすすめは 8〜12）", options.fps, 4f, 30f);
            options.size = EditorGUILayout.Slider("ゲームの中での大きさ（m）", options.size, 0.3f, 10f);
            options.glow = EditorGUILayout.Toggle("光らせて重ねる（加算）", options.glow);
            options.randomRotation = EditorGUILayout.Toggle("出すたびに向きを変える", options.randomRotation);
            EditorGUILayout.LabelField("大きい画素ほどきれいですが、画像が重くなります（256 px で1つあたりおよそ 1〜2 MB）。", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(!found.Exists(s => s.selected)))
            {
                if (GUILayout.Button("選んだエフェクトを作る", GUILayout.Height(28)))
                {
                    try
                    {
                        int built = KillFxImporter.Build(found, options,
                            (label, progress) => EditorUtility.DisplayProgressBar("倒したときの演出", label + " を作っています…", progress));
                        EditorUtility.DisplayDialog("倒したときの演出", built + " 個のエフェクトを作りました。\n下の「3 D-Drive に登録する」の手順で、使えるようにしてください。", "OK");
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
        }

        // ───────── 3 作ったエフェクト ─────────

        private void DrawBuilt()
        {
            EditorGUILayout.LabelField("3  作ったエフェクトを D-Drive に登録する", EditorStyles.boldLabel);
            var built = KillFxImporter.ListBuilt();
            if (built.Count == 0)
            {
                EditorGUILayout.HelpBox("まだありません（上の 2 で作ってください）。", MessageType.None);
                return;
            }

            foreach (var (prefab, hasTexture) in built)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                    EditorGUILayout.LabelField(hasTexture ? "画像あり" : "このPCに画像なし（透明）", GUILayout.Width(150));
                }
            }

            EditorGUILayout.HelpBox("ゲームの演出は D-Drive の番号札（VFX）で出す決まりなので、使いたいエフェクトを登録します。\n" +
                                    "① 下の「Asset Browser を開く」→ 種別 Vfx で新規作成し、上の Prefab を割り当てる\n" +
                                    "② VFX の設定: 寿命 LifeMode = OneShot（または Duration）/ Flags › Net = Local / Flags › Pool = Pooled（数は 8〜16）\n" +
                                    "③ 上の 1 設定の「倒したときに出すエフェクト」で、登録した VFX を選ぶ（効果音も選べます）\n" +
                                    "④ Tools › D-Drive › Validation › Run All で Error が無いことを確かめる", MessageType.None);
            if (GUILayout.Button("Asset Browser を開く"))
            {
                if (!EditorApplication.ExecuteMenuItem("Tools/D-Drive/Asset Browser"))
                {
                    EditorUtility.DisplayDialog("倒したときの演出", "メニュー「Tools › D-Drive › Asset Browser」を開けませんでした。メニューから直接開いてください。", "OK");
                }
            }
        }

        // ───────── 4 試す ─────────

        private void DrawTest()
        {
            EditorGUILayout.LabelField("4  試す", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("ゲームを動かしている間だけ試せます（敵を倒さなくても、画面のまん中に出せます）。", MessageType.None);
                return;
            }

            var director = KillFxDirector.Instance;
            using (new EditorGUI.DisabledScope(director == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int player = -1; player < 4; player++)
                    {
                        if (GUILayout.Button(player < 0 ? "色なし" : "P" + (player + 1) + "の色"))
                        {
                            director.Play(ScreenCenterOnFloor(), player);
                        }
                    }
                }
            }
        }

        private static Vector2 ScreenCenterOnFloor()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return Vector2.zero;
            }

            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-4f)
            {
                return Vector2.zero;
            }

            var t = -ray.origin.z / ray.direction.z; // 床（z = 0）との交点
            var point = ray.origin + ray.direction * t;
            return new Vector2(point.x, point.y);
        }

        private void AddSource(string path)
        {
            if (!string.IsNullOrEmpty(path) && !sources.Contains(path))
            {
                sources.Add(path);
                SaveSources();
            }
        }

        private void SaveSources()
        {
            EditorPrefs.SetString(SourcesKey, string.Join("|", sources));
        }

        private string LastFolder()
        {
            return sources.Count > 0 ? Path.GetDirectoryName(sources[sources.Count - 1]) : System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        }
    }
}
