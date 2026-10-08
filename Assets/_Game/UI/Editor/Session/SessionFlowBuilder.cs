using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MS2026.Fortress.Net;
using MS2026.UI.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 「タイトル → ロビー → ゲーム」の流れを組み立てる（メニュー・UIスタジオの「はじめに」から）。
    /// 1. ロビーまわりの画面（Prefab）と仮の絵を作る（<see cref="SessionScreens"/>）
    /// 2. ロビーのシーン（Assets/Scenes/Lobby.unity）を作る: ゲームのシーンの通信の土台（D-Drive・NetworkManager・NgoNetBridge・センサー）を写し、
    ///    UIの置き場所・EventSystem・カメラ・GameSession を置く。どれも「シーンをまたいで残る」設定は切る（ロビーはずっと読み込まれたまま）
    /// 3. ゲームのシーンに番人（GameSceneSessionGuard）を置く: ロビーから来たときだけ、ゲームのシーンの同じ物を外す
    /// 4. タイトルのスタートの行き先をロビーにする
    /// 5. ビルドのシーンの順番を タイトル → ロビー → ゲーム にする
    /// </summary>
    public static class SessionFlowBuilder
    {
        public const string TitleScenePath = "Assets/Scenes/Title.unity";
        public const string LobbyScenePath = "Assets/Scenes/Lobby.unity";
        public const string GameScenePath = "Assets/Scenes/Game.unity";
        public const string GuardName = "[Session] Guard";

        private const string MenuPath = "Tools/UIスタジオ/タイトル→ロビー→ゲームの流れを組み立てる";

        /// <summary>組み立てた結果（ダイアログ・ログに出す）。</summary>
        public sealed class Report
        {
            public readonly List<string> Lines = new List<string>();
            public readonly List<string> Problems = new List<string>();

            public override string ToString() => string.Join("\n", Lines.Concat(Problems.Select(p => "⚠ " + p)));
        }

        [MenuItem(MenuPath, priority = 20)]
        public static void BuildFromMenu() => BuildInteractive();

        /// <summary>確認のダイアログを出してから組み立てる（UIスタジオのボタンからも呼ぶ）。</summary>
        public static void BuildInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("組み立てられません", "Play を止めてから押してください。", "OK");
                return;
            }

            var exists = File.Exists(LobbyScenePath);
            var choice = EditorUtility.DisplayDialogComplex("タイトル → ロビー → ゲームの流れを組み立てる",
                (exists ? "ロビーのシーンはもうあります。\n\n" : "") +
                "・ロビーまわりの画面（最初の画面・部屋を選ぶ・部屋・3・2・1・一時メニュー・お知らせ・通知・背景）を作ります\n" +
                "・ロビーのシーン（Assets/Scenes/Lobby.unity）を作り、ゲームのシーンから通信の土台を写します\n" +
                "・ゲームのシーンに「ロビーから来たときに外す物」の番人を置きます（ゲームのシーンだけで遊ぶときは今まで通り）\n" +
                "・タイトルのスタートの行き先をロビーにし、ビルドのシーンの順番を タイトル → ロビー → ゲーム にします\n\n" +
                "開いているシーンは保存を確認してから閉じます。",
                exists ? "足りない物だけ作る" : "組み立てる", "やめる", "画面とロビーも作り直す");
            if (choice == 1)
            {
                return;
            }

            var report = Build(choice == 2);
            if (report == null)
            {
                return;
            }

            Debug.Log("[UIスタジオ] タイトル → ロビー → ゲームの流れを組み立てました。\n" + report);
            EditorUtility.DisplayDialog("組み立てました", report + "\n\nロビーのシーンを開きました。タイトルのシーンから Play すると、タイトル → ロビー → ゲームの順に進みます。", "OK");
        }

        /// <summary>組み立てる。rebuild=true なら画面とロビーのシーンも作り直す。保存を断られたら null。</summary>
        public static Report Build(bool rebuild)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return null;
            }

            var report = new Report();
            UiStudioSetup.EnsureDefaultTransitions();
            var added = UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);
            if (added > 0)
            {
                report.Lines.Add($"値の一覧に {added} 個登録しました（ロビーの値など）");
            }

            var screens = SessionScreens.BuildAll(rebuild);
            report.Lines.Add(screens.Count > 0 ? $"画面を作りました: {string.Join("・", screens)}" : "画面はもう揃っていたので、そのままにしました");

            if (!File.Exists(GameScenePath))
            {
                report.Problems.Add($"ゲームのシーン（{GameScenePath}）がありません。");
                return report;
            }

            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var stack = Stack.Find(game);
            if (stack.Network == null || stack.NetworkManager == null || stack.Bridge == null)
            {
                report.Problems.Add("ゲームのシーンに通信の土台（[D-Drive] Runtime・NetworkManager・NgoNetBridge）が見つかりません。要塞デザイナーのネットワークの設定を確かめてください。");
                return report;
            }

            EnsureGuard(game, stack, report);
            EditorSceneManager.SaveScene(game);

            if (!File.Exists(LobbyScenePath) || rebuild)
            {
                BuildLobbyScene(stack, report);
            }
            else
            {
                report.Lines.Add("ロビーのシーンはそのままにしました（作り直すときは「画面とロビーも作り直す」）");
            }

            PointTitleToLobby(report);
            UpdateBuildSettings(report);
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            return report;
        }

        // ───────── ゲームのシーン ─────────

        /// <summary>ゲームのシーンの「このシーンだけで遊ぶとき用」の物。</summary>
        private sealed class Stack
        {
            public GameObject DDrive;
            public FortressNetworkBootstrap Network;
            public GameObject NetworkManager;
            public GameObject Bridge;
            public GameObject Arduino;
            public GameObject UiRootObject;
            public GameObject EventSystem;

            public IEnumerable<GameObject> All => new[] { DDrive, NetworkManager, Bridge, Arduino, UiRootObject, EventSystem }.Where(g => g != null).Distinct();

            public static Stack Find(Scene scene)
            {
                var stack = new Stack();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (Has(root, "DDriveRuntimeBootstrap"))
                    {
                        stack.DDrive = root;
                        stack.Network = root.GetComponent<FortressNetworkBootstrap>();
                    }

                    if (Has(root, "NetworkManager"))
                    {
                        stack.NetworkManager = root;
                    }

                    if (Has(root, "NgoNetBridge"))
                    {
                        stack.Bridge = root;
                    }

                    if (Has(root, "ArduinoSerialReader"))
                    {
                        stack.Arduino = root;
                    }

                    if (root.GetComponent<UiRoot>() != null)
                    {
                        stack.UiRootObject = root;
                    }

                    if (root.GetComponent<EventSystem>() != null)
                    {
                        stack.EventSystem = root;
                    }
                }

                if (stack.Network == null)
                {
                    var bootstrap = Object.FindObjectsByType<FortressNetworkBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.scene == scene);
                    stack.Network = bootstrap;
                }

                return stack;
            }

            // パッケージの型（D-Drive・NGO・Assembly-CSharp のセンサー）に頼らず、部品の名前で見分ける。
            private static bool Has(GameObject go, string typeName) =>
                go.GetComponents<Component>().Any(c => c != null && c.GetType().Name == typeName);
        }

        private static void EnsureGuard(Scene game, Stack stack, Report report)
        {
            var guardObject = game.GetRootGameObjects().FirstOrDefault(g => g.GetComponent<GameSceneSessionGuard>() != null);
            if (guardObject == null)
            {
                guardObject = new GameObject(GuardName);
                SceneManager.MoveGameObjectToScene(guardObject, game);
                guardObject.AddComponent<GameSceneSessionGuard>();
            }

            var guard = guardObject.GetComponent<GameSceneSessionGuard>();
            guard.standaloneOnly = stack.All.ToArray();
            EditorUtility.SetDirty(guard);
            report.Lines.Add($"ゲームのシーンに番人を置きました（ロビーから来たときに外す物: {string.Join("・", guard.standaloneOnly.Select(g => g.name))}）");
        }

        // ───────── ロビーのシーン ─────────

        private static void BuildLobbyScene(Stack stack, Report report)
        {
            var lobby = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(lobby);

            // 通信の土台をゲームのシーンから写す（設定・カタログなどがそのまま同じになる）。
            var ddrive = Copy(stack.DDrive, lobby);
            var networkManager = Copy(stack.NetworkManager, lobby);
            var bridge = Copy(stack.Bridge, lobby);
            var arduino = stack.Arduino != null ? Copy(stack.Arduino, lobby) : null;
            WireCopies(ddrive, networkManager, bridge);

            var camera = new GameObject("[Lobby] Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            var cam = camera.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.91f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var root = UiStudioSetup.CreateRoot();
            root.keepAcrossScenes = false; // ロビーのシーンは遊んでいる間も読み込まれたままなので、残す必要がない（タイトルへ戻るときに一緒に消える）
            EditorUtility.SetDirty(root);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var sessionObject = new GameObject("[Lobby] Session");
            var session = sessionObject.AddComponent<GameSession>();
            session.network = ddrive.GetComponent<FortressNetworkBootstrap>();
            session.lobbyCamera = cam;
            session.menuTransition = FindTransition("Transition_Wipe");
            session.gameTransition = FindTransition("Transition_Iris");
            session.titleTransition = FindTransition("Transition_Fade");
            EditorUtility.SetDirty(session);

            foreach (var go in new[] { camera, root.gameObject, eventSystem, sessionObject })
            {
                if (go.scene != lobby)
                {
                    SceneManager.MoveGameObjectToScene(go, lobby);
                }
            }

            StudioDir(LobbyScenePath);
            EditorSceneManager.SaveScene(lobby, LobbyScenePath);
            // 写した NetworkObject の識別番号を、保存した後のロビーのシーンの物として作り直す（ゲームのシーンの物と同じ番号のままにしない）。
            RefreshNetworkObjectIds(lobby);
            EditorSceneManager.SaveScene(lobby, LobbyScenePath);
            report.Lines.Add($"ロビーのシーンを作りました（{LobbyScenePath}）: 通信の土台・センサー{(arduino != null ? "" : "（ゲームのシーンに無いので無し）")}・UIの置き場所・EventSystem・カメラ・GameSession");
        }

        private static GameObject Copy(GameObject source, Scene into)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            SceneManager.MoveGameObjectToScene(copy, into);
            return copy;
        }

        private static void WireCopies(GameObject ddrive, GameObject networkManager, GameObject bridge)
        {
            // ゲームのシーン用の仮の接続の画面（OnGUI）は、ロビーの画面があるので要らない。
            foreach (var ui in ddrive.GetComponents<FortressConnectUI>())
            {
                Object.DestroyImmediate(ui);
            }

            foreach (var component in ddrive.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var so = new SerializedObject(component);
                var name = component.GetType().Name;
                if (name == "DDriveRuntimeBootstrap")
                {
                    SetBool(so, "KeepAcrossScenes", false);
                }
                else if (name == "DDriveNgoBootstrapHook")
                {
                    SetReference(so, "NetworkManagerRef", networkManager.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "NetworkManager"));
                    SetReference(so, "NgoBridgeRef", bridge.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "NgoNetBridge"));
                }
                else if (component is FortressNetworkBootstrap)
                {
                    SetReference(so, "ddriveBootstrap", ddrive.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "DDriveRuntimeBootstrap"));
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RefreshNetworkObjectIds(Scene scene)
        {
            var onValidate = typeof(Unity.Netcode.NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var networkObject in root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true))
                {
                    onValidate?.Invoke(networkObject, null);
                    EditorUtility.SetDirty(networkObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            var p = so.FindProperty(property);
            if (p != null)
            {
                p.boolValue = value;
            }
        }

        private static void SetReference(SerializedObject so, string property, Object value)
        {
            var p = so.FindProperty(property);
            if (p != null && value != null)
            {
                p.objectReferenceValue = value;
            }
        }

        private static UiScreenTransition FindTransition(string name) =>
            UiStudioSetup.FindAll<UiScreenTransition>().FirstOrDefault(t => t.name == name);

        private static void StudioDir(string scenePath)
        {
            var folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
            {
                UiStudioSetup.EnsureFolder(folder);
            }
        }

        // ───────── タイトル・ビルドの設定 ─────────

        private static void PointTitleToLobby(Report report)
        {
            if (!File.Exists(TitleScenePath))
            {
                report.Problems.Add("タイトルのシーンがありません（メニューの「タイトルシーンを組み立てる」で作れます）。");
                return;
            }

            var title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            var changed = false;
            foreach (var root in title.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "TitleScreenController")
                    {
                        continue;
                    }

                    var so = new SerializedObject(behaviour);
                    var next = so.FindProperty("gameSceneName");
                    if (next != null && next.stringValue != "Lobby")
                    {
                        next.stringValue = "Lobby";
                        changed = true;
                    }

                    var direct = so.FindProperty("directSceneForTestLaunch");
                    if (direct != null && string.IsNullOrEmpty(direct.stringValue))
                    {
                        direct.stringValue = "Game";
                        changed = true;
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(title);
                EditorSceneManager.SaveScene(title);
            }

            report.Lines.Add("タイトルのスタートの行き先をロビーにしました（テスト用の起動ではゲームへ直行）");
        }

        /// <summary>ビルドのシーンの順番を タイトル → ロビー → ゲーム にする（点検の「直す」から）。</summary>
        public static void FixBuildOrder() => UpdateBuildSettings(new Report());

        private static void UpdateBuildSettings(Report report)
        {
            var wanted = new[] { TitleScenePath, LobbyScenePath, GameScenePath };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var path in wanted)
            {
                if (File.Exists(path))
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!wanted.Contains(scene.path))
                {
                    list.Add(scene);
                }
            }

            EditorBuildSettings.scenes = list.ToArray();
            report.Lines.Add("ビルドのシーンの順番: " + string.Join(" → ", list.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }
    }
}
