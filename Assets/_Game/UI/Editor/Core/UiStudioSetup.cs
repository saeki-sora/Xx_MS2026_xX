using System.Collections.Generic;
using System.IO;
using MS2026.StudioKit;
using MS2026.UI.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>UIスタジオのデータの置き場所と、最初の準備（UIの置き場所・レイヤー・一覧・画面切り替えのひな形）。</summary>
    public static class UiStudioSetup
    {
        public const string DataFolder = "Assets/_Game/UI/Data";
        public const string ScreensFolder = "Assets/_Game/UI/Screens";
        public const string TransitionsFolder = "Assets/_Game/UI/Transitions";
        public const string LayerSettingsPath = DataFolder + "/UiLayerSettings.asset";
        public const string CatalogPath = DataFolder + "/UiScreenCatalog.asset";
        public const string ValueCatalogPath = DataFolder + "/UiValueCatalog.asset";
        public const string RootName = "[UI]";

        public static UiRoot FindRoot() => UiRoot.Active != null ? UiRoot.Active : Object.FindFirstObjectByType<UiRoot>(FindObjectsInactive.Include);

        public static UiLayerSettings LayerSettings => LoadOrCreate<UiLayerSettings>(LayerSettingsPath, s => s.ResetToDefaults());
        public static UiScreenCatalog Catalog => LoadOrCreate<UiScreenCatalog>(CatalogPath, null);
        public static UiValueCatalog ValueCatalog => LoadOrCreate<UiValueCatalog>(ValueCatalogPath, c => RegisterGameValues(c));

        /// <summary>シーンにUIの置き場所（[UI]）を作る。レイヤーの Canvas・ゲームの値の書き込み役・EventSystem も用意する。</summary>
        public static UiRoot CreateRoot()
        {
            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "UIの置き場所を作成");
            var root = go.AddComponent<UiRoot>();
            root.layerSettings = LayerSettings;
            root.catalog = Catalog;
            go.AddComponent<FortressUiValues>();
            go.AddComponent<FortressLegacyUiSwitch>();
            root.EnsureLayers();
            EnsureEventSystem();
            EnsureDefaultTransitions();
            MarkSceneDirty();
            return root;
        }

        public static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.GetComponent<BaseInputModule>() == null)
                {
                    Undo.AddComponent<InputSystemUIInputModule>(existing.gameObject);
                }

                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem を作成");
        }

        /// <summary>ゲームの値（熱・コアのHP…）を値の一覧に登録する（無い物だけ足す）。戻り値は足した数。</summary>
        public static int RegisterGameValues(UiValueCatalog catalog)
        {
            var added = 0;
            foreach (var (key, label, description, kind, category, range, sample) in FortressUiKeys.Describe())
            {
                if (catalog.AddIfMissing(key, label, description, kind, category, range, sample))
                {
                    added++;
                }
            }

            foreach (var item in catalog.items)
            {
                if (item.kind == UiValueKind.Text && (string.IsNullOrEmpty(item.sampleText) || item.sampleText == "サンプル"))
                {
                    item.sampleText = SampleText(item.key);
                }

                if (item.kind == UiValueKind.Color && item.key.EndsWith(".color"))
                {
                    item.sampleColor = MS2026.Fortress.FortressColors.PlayerColor(item.key.StartsWith("player.") ? item.key[7] - '1' : 0);
                }
            }

            EditorUtility.SetDirty(catalog);
            return added;
        }

        private static readonly string[] SampleNames = { "たろう", "はなこ", "ジロー", "" };

        /// <summary>文字の値のサンプル（UIスタジオの「サンプル値で表示」で、Play しなくても画面の見た目がわかるように）。</summary>
        private static string SampleText(string key)
        {
            if (key.StartsWith("player.") && key.EndsWith(".name"))
            {
                return SampleNames[Mathf.Clamp(key[7] - '1', 0, 3)];
            }

            return key switch
            {
                FortressUiKeys.LocalPlayerLabel => "P1",
                FortressUiKeys.NetMode => "ホスト",
                FortressUiKeys.LobbyStatus => "みんなが入ってくるのを待っています",
                FortressUiKeys.LobbySelectedLabel => "P1",
                FortressUiKeys.LobbyName => "たろう",
                FortressUiKeys.LobbyPhase => "準備OKを待っています",
                FortressUiKeys.LobbyAddress => "192.168.1.12",
                FortressUiKeys.LobbyHostAddress => "192.168.1.12",
                FortressUiKeys.MessageTitle => "部屋との接続が切れました",
                FortressUiKeys.MessageBody => "ホストが部屋を閉じたか、通信が途切れました。",
                FortressUiKeys.ToastText => "はなこ（P2）が入りました",
                _ => "サンプル"
            };
        }

        /// <summary>画面切り替えのひな形（暗転・ワイプ・まるく・ブラインド・ひし形・灼ける）が1つも無ければ作る。</summary>
        public static void EnsureDefaultTransitions()
        {
            if (FindAll<UiScreenTransition>().Count > 0)
            {
                return;
            }

            EnsureFolder(TransitionsFolder);
            Create("Transition_Fade", "暗転", UiTransitionPattern.Fade, 0.3f, 0.3f, 0f);
            Create("Transition_Wipe", "ワイプ", UiTransitionPattern.Wipe, 0.35f, 0.35f, 0f);
            Create("Transition_Iris", "まるく閉じる", UiTransitionPattern.Iris, 0.45f, 0.4f, 0f);
            Create("Transition_Blinds", "ブラインド", UiTransitionPattern.Blinds, 0.4f, 0.4f, 90f);
            Create("Transition_Diamonds", "ひし形", UiTransitionPattern.Diamonds, 0.5f, 0.5f, 30f);
            var burn = Create("Transition_Burn", "灼ける", UiTransitionPattern.Burn, 0.7f, 0.5f, 0f);
            burn.color = new Color(0.08f, 0.03f, 0.02f, 1f);
            EditorUtility.SetDirty(burn);
            AssetDatabase.SaveAssets();
        }

        public static List<T> FindAll<T>() where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        /// <summary>フォルダが無ければ親から順に作る（StudioKit の共通処理）。</summary>
        public static void EnsureFolder(string folder) => StudioAssets.EnsureFolder(folder);

        public static void MarkSceneDirty()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }

        private static UiScreenTransition Create(string file, string label, UiTransitionPattern pattern, float cover, float reveal, float angle)
        {
            var t = ScriptableObject.CreateInstance<UiScreenTransition>();
            t.displayName = label;
            t.pattern = pattern;
            t.coverSeconds = cover;
            t.revealSeconds = reveal;
            t.angle = angle;
            AssetDatabase.CreateAsset(t, $"{TransitionsFolder}/{file}.asset");
            return t;
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            var found = FindAll<T>();
            if (found.Count > 0)
            {
                return found[0];
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
