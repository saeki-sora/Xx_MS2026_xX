using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// メニュー「Tools/キャラ選択/シーンを組み立てる」: Assets/Scenes/Character_Select.unity にキャラ選択の画面（2D）を作る。
    /// 画面は 1920×1080 基準で、デザイナーの完成版の絵（Assets/CharacterSelect/Art/UI、2026-11-04）を見本の位置に並べる:
    /// 左に自分の大きい区画・右にスキル動画のモニター（決定するとほかの3人の欄）・下にキャラのボタンと決定のゲージ。
    /// ・仮の絵・キャラ4体のファイル・キャラの一覧は「無いときだけ」作る（差し替えた物はそのまま）
    /// ・仮のキャラのファイルで、立ち絵・能力が空の物には、仮のキューブ（キャラの色）と仮の文章を入れる（入れてある物には触らない）
    /// ・シーンの [CharacterSelect]・[CharacterSelect UI] は作り直す（シーンで直した配置は消える）
    /// ・ビルドの設定にシーンが無ければ足す（ロビーから NGO で読み込むのに必要）
    /// </summary>
    public static class CharacterSelectSceneBuilder
    {
        public const string Root = "Assets/CharacterSelect";
        public const string ScenePath = "Assets/Scenes/Character_Select.unity";
        public const string DataFolder = Root + "/Data";
        public const string CharactersFolder = DataFolder + "/Characters";
        public const string RosterPath = DataFolder + "/CharacterRoster.asset";
        public const string ResourcesFolder = Root + "/Resources";
        public const string CurtainSettingsPath = ResourcesFolder + "/" + PasteCurtainSettings.ResourcePath + ".asset";

        // タイトルのオープニングで画面を覆う歯磨き粉の絵（同じ見た目にするため、キャラ選択に入るときの幕にも使う）。
        private const string TitleCurtainTexturePath = "Assets/Title/Sprites/Title_PasteCurtain.png";

        private const string StageRootName = "[CharacterSelect]";
        private const string UiRootName = "[CharacterSelect UI]";
        private const string OldBackgroundRootName = "[CharacterSelect Background]";

        // 前の版の仮の絵（白い人型・顔）。キャラのファイルに入っていたらキューブに入れ替える。
        private const string OldPortraitPath = CharacterSelectArt.Folder + "/CS_CharaPortrait.png";
        private const string OldFacePath = CharacterSelectArt.Folder + "/CS_CharaFace.png";

        // デザイナーの完成版の絵（届いたフォルダ分けのまま）。
        public const string UiArtFolder = Root + "/Art/UI";

        // 制限時間（ユーザー決定 2026-11-05: 50 秒）。
        private const float DefaultTimeLimitSeconds = 50f;

        // 画面の割り付け（1920×1080 基準のピクセル。見本から読み取った値）。
        private const float PortraitCenterX = 235f;
        private const float PortraitBottomY = 470f;
        private const float PortraitWidth = 400f;
        private const float PortraitHeight = 440f;
        private const float ButtonFirstX = 160f;
        private const float ButtonSpacing = 230f;
        private const float ButtonY = 840f;

        private static readonly Color Ink = new Color(0.11f, 0.11f, 0.29f);
        private static readonly Color SubInk = new Color(0.32f, 0.35f, 0.5f);
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color Pink = new Color(1f, 0.42f, 0.7f);
        private static readonly Color BackgroundColor = new Color(0.95f, 0.95f, 0.98f);
        private static readonly Color MonitorBack = new Color(0.2f, 0.12f, 0.32f);

        private sealed class Placeholder
        {
            public string file;
            public string id;
            public Color color;

            // 完成版の絵のファイル名に入っている色の名前（Red・Blue・Green・Yellow）。
            public string art;
            public (string label, float value)[] stats;

            // 前の版の仮の値（ファイルにこの値が残っていたら、今の仮の値に入れ替える。自分で書き換えた値には触らない）。
            public string oldName;
            public Color oldColor;
            public string oldSkill;
        }

        // 仮のキャラ: 名前はすべて「キャラ名（仮）」、色は赤・青・緑・黄（ユーザー決定 2026-10-12）。文章もすべて仮。
        // 並びはキャラのボタンの順＝見本どおり 赤・青・黄・緑（ユーザー決定 2026-11-05）。
        private const string PlaceholderName = "キャラ名（仮）";

        // 前の版の仮の名前（どのキャラも同じ）。
        private const string OldPlaceholderName = "仮";
        private const string PlaceholderSkillName = "スキル名（仮）";
        private const string PlaceholderSkill = "ここにスキルの説明が入ります（仮）。";

        private static readonly Placeholder[] PlaceholderCharacters =
        {
            new Placeholder
            {
                file = "Chara_Alpha", id = "alpha", art = "Red", color = new Color(0.93f, 0.27f, 0.3f),
                stats = new[] { ("パワー", 0.8f), ("スピード", 0.45f), ("ため", 0.6f) },
                oldName = "アルファ（仮）", oldColor = new Color(1f, 0.55f, 0.78f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：握り続けるほどビームが太くなる。"
            },
            new Placeholder
            {
                file = "Chara_Beta", id = "beta", art = "Blue", color = new Color(0.22f, 0.52f, 0.96f),
                stats = new[] { ("パワー", 0.5f), ("スピード", 0.85f), ("ため", 0.4f) },
                oldName = "ベータ（仮）", oldColor = new Color(0.35f, 0.82f, 1f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：すばやく連続で撃てる。"
            },
            new Placeholder
            {
                file = "Chara_Delta", id = "delta", art = "Yellow", color = new Color(0.98f, 0.84f, 0.2f),
                stats = new[] { ("パワー", 0.9f), ("スピード", 0.3f), ("ため", 0.5f) },
                oldName = "デルタ（仮）", oldColor = new Color(1f, 0.7f, 0.3f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：一撃が重く、かたい敵に強い。"
            },
            new Placeholder
            {
                file = "Chara_Gamma", id = "gamma", art = "Green", color = new Color(0.25f, 0.74f, 0.42f),
                stats = new[] { ("パワー", 0.65f), ("スピード", 0.6f), ("ため", 0.75f) },
                oldName = "ガンマ（仮）", oldColor = new Color(0.68f, 0.58f, 1f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：ためると広い範囲をきれいにする。"
            }
        };

        [MenuItem("Tools/キャラ選択/シーンを組み立てる", priority = 0)]
        public static void BuildInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("キャラ選択", "再生中は組み立てられません。再生を止めてからもう一度選んでください。", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var keepValues = false;
            if (FindRoot(scene, StageRootName) != null || FindRoot(scene, UiRootName) != null)
            {
                var choice = EditorUtility.DisplayDialogComplex("キャラ選択",
                    "このシーンにはもうキャラ選択の画面があります。作り直しますか？\n\n" +
                    "・数値を引き継いで作り直す: 「見た目と数値の調整」や Inspector で変えた数値（秒数・太さ・濃さなど）はそのまま。\n" +
                    "・初期値で作り直す: 数値も最初の値に戻す（新しい初期値を使いたいとき）。\n" +
                    "・どちらでも、シーンで動かした配置や文字は作り直されます。キャラのファイル（名前・文章・絵）・絵（PNG）・幕の設定はそのままです。",
                    "数値を引き継いで作り直す", "やめる", "初期値で作り直す");
                if (choice == 1)
                {
                    return;
                }

                keepValues = choice == 0;
            }

            Build(scene, keepValues);
        }

        [MenuItem("Tools/キャラ選択/キャラの一覧を選ぶ", priority = 20)]
        public static void SelectRoster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            if (roster == null)
            {
                EditorUtility.DisplayDialog("キャラ選択", $"{RosterPath} がありません。先に「シーンを組み立てる」を選んでください。", "OK");
                return;
            }

            Selection.activeObject = roster;
            EditorGUIUtility.PingObject(roster);
        }

        [MenuItem("Tools/キャラ選択/キャラ選択のシーンを開く", priority = 21)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static void Build(Scene scene, bool keepValues)
        {
            // ── アセット（無い物だけ）──
            CharacterSelectArt.EnsureAll();
            var roster = EnsureCharacters();
            EnsureCurtainSettings();

            // ── シーン ──
            SceneManager.SetActiveScene(scene);
            var snapshot = keepValues ? CharacterSelectValueSnapshot.Capture(new[] { FindRoot(scene, StageRootName), FindRoot(scene, UiRootName) }) : null;
            foreach (var name in new[] { StageRootName, UiRootName, OldBackgroundRootName })
            {
                var old = FindRoot(scene, name);
                if (old != null)
                {
                    Object.DestroyImmediate(old);
                }
            }

            SetupCamera(scene);

            var stage = new GameObject(StageRootName);
            var controller = stage.AddComponent<CharacterSelectController>();
            var view = stage.AddComponent<CharacterSelectView>();
            controller.roster = roster;
            controller.view = view;
            controller.timeLimitSeconds = DefaultTimeLimitSeconds;
            view.controller = controller;
            BuildUi(view, roster);
            var kept = snapshot != null ? snapshot.Apply(new[] { stage, FindRoot(scene, UiRootName) }) : 0;
            if (kept > 0)
            {
                Debug.Log($"[キャラ選択] 前の数値を {kept} 個の部品に引き継ぎました。");
            }

            // 前の版は制限時間なし（0）だった。引き継いだ値が 0 なら、決まった 50 秒にする。
            if (controller.timeLimitSeconds <= 0f)
            {
                controller.timeLimitSeconds = DefaultTimeLimitSeconds;
                Debug.Log($"[キャラ選択] 制限時間を {DefaultTimeLimitSeconds} 秒にしました（[CharacterSelect] の「制限時間」で変えられます）。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Selection.activeGameObject = stage;
            Debug.Log("[キャラ選択] シーンを組み立てました（" + ScenePath + "）。キャラの名前・文章・絵は " + CharactersFolder + " のファイルで変えます。");
        }

        // ───────── キャラのファイル ─────────

        /// <summary>
        /// 仮のキャラ4体と一覧を、無ければ作る。あるファイルは、空の欄（立ち絵・能力）にだけ仮の物を入れる。
        /// 仮のキャラは、白いキューブの絵にキャラの色をかけた物（色でキャラを見分ける）。
        /// 前の版の仮の絵（白い人型・顔）が入っているファイルは、キューブに入れ替える。
        /// </summary>
        private static CharacterRoster EnsureCharacters()
        {
            StudioAssets.EnsureFolder(CharactersFolder);
            var cube = CharacterSelectArt.Load(CharacterSelectArt.CharaCube);
            var oldPortrait = CharacterSelectArt.Load(OldPortraitPath);
            var oldFace = CharacterSelectArt.Load(OldFacePath);
            var created = new CharacterDefinition[PlaceholderCharacters.Length];
            for (var i = 0; i < PlaceholderCharacters.Length; i++)
            {
                var p = PlaceholderCharacters[i];
                var path = $"{CharactersFolder}/{p.file}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                var isNew = definition == null;
                if (isNew)
                {
                    definition = ScriptableObject.CreateInstance<CharacterDefinition>();
                    definition.id = p.id;
                    definition.displayName = PlaceholderName;
                    definition.themeColor = p.color;
                }

                // 前の版の仮の値が残っていれば、今の仮の値（名前「キャラ名（仮）」・赤青緑黄・例のない説明）に入れ替える。
                if (definition.displayName == p.oldName || definition.displayName == OldPlaceholderName)
                {
                    definition.displayName = PlaceholderName;
                }

                if (SameColor(definition.themeColor, p.oldColor))
                {
                    definition.themeColor = p.color;
                }

                if (SameColor(definition.spriteTint, p.oldColor))
                {
                    definition.spriteTint = p.color;
                }

                if (definition.skillDescription == p.oldSkill)
                {
                    definition.skillDescription = PlaceholderSkill;
                }

                if (definition.sprite == null || (oldPortrait != null && definition.sprite == oldPortrait))
                {
                    definition.sprite = cube;
                    definition.spriteTint = p.color;
                }

                // 顔の絵は入れない（下の丸には立ち絵＝色つきのキューブが出る）。前の版の仮の顔だけ外す。
                if (oldFace != null && definition.icon == oldFace)
                {
                    definition.icon = null;
                }

                if (definition.stats == null || definition.stats.Length == 0)
                {
                    definition.stats = p.stats.Select(s => new CharacterDefinition.Stat { label = s.label, value = s.value }).ToArray();
                    definition.skillName = PlaceholderSkillName;
                    definition.skillDescription = PlaceholderSkill;
                }

                // 完成版の画面の絵（ボタン・ほかの人の欄の土台・自分の区画の土台）。空の欄にだけ入れる。
                definition.buttonOn = definition.buttonOn != null ? definition.buttonOn : LoadUiQuiet($"other/charabutton_{p.art.ToLowerInvariant()}_on.png");
                definition.buttonOff = definition.buttonOff != null ? definition.buttonOff : LoadUiQuiet($"other/charabutton_{p.art.ToLowerInvariant()}_off.png");
                definition.rowBase = definition.rowBase != null ? definition.rowBase : LoadUiQuiet($"R_player/sub_base_{p.art}.png");
                definition.mainBase = definition.mainBase != null ? definition.mainBase : LoadUiQuiet($"main_player/MAIN_base_{p.art}.png");

                if (isNew)
                {
                    AssetDatabase.CreateAsset(definition, path);
                }
                else
                {
                    EditorUtility.SetDirty(definition);
                }

                created[i] = definition;
            }

            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            if (roster == null)
            {
                roster = ScriptableObject.CreateInstance<CharacterRoster>();
                roster.characters = created;
                AssetDatabase.CreateAsset(roster, RosterPath);
            }
            else if (roster.characters != null && roster.characters.Length == created.Length && created.All(c => roster.characters.Contains(c))
                     && !roster.characters.SequenceEqual(created))
            {
                // 仮の4体だけの一覧なら、見本どおりの並び（赤・青・黄・緑）にそろえる。キャラを足した一覧には触らない。
                roster.characters = created;
                EditorUtility.SetDirty(roster);
                Debug.Log("[キャラ選択] キャラの一覧を 赤・青・黄・緑 の順に並べ替えました。");
            }

            AssetDatabase.SaveAssets();
            return roster;
        }

        /// <summary>
        /// キャラ選択に入るときの歯磨き粉の幕の設定を、無ければ作る（Resources に置くので、ロビーにいる間でも読める）。
        /// 絵が空なら、タイトルのオープニングの歯磨き粉の絵を入れる（入れてある物には触らない）。
        /// </summary>
        private static void EnsureCurtainSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CurtainSettingsPath);
            var isNew = settings == null;
            if (isNew)
            {
                StudioAssets.EnsureFolder(ResourcesFolder);
                settings = ScriptableObject.CreateInstance<PasteCurtainSettings>();
            }

            if (settings.texture == null)
            {
                settings.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleCurtainTexturePath);
                if (settings.texture == null)
                {
                    Debug.LogWarning($"[キャラ選択] {TitleCurtainTexturePath} が見つからないので、歯磨き粉の幕は色だけになります。設定 {CurtainSettingsPath} の「幕の絵」に絵を入れてください。");
                }
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(settings, CurtainSettingsPath);
            }
            else
            {
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>歯磨き粉の絵（幕の設定の絵、無ければタイトルのオープニングの絵）。キャラの切り替えにも同じ絵を使う。</summary>
        private static Texture CurtainTexture()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CurtainSettingsPath);
            if (settings != null && settings.texture != null)
            {
                return settings.texture;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TitleCurtainTexturePath);
        }

        [MenuItem("Tools/キャラ選択/歯磨き粉の幕の設定を選ぶ", priority = 22)]
        public static void SelectCurtainSettings()
        {
            EnsureCurtainSettings();
            var settings = AssetDatabase.LoadAssetAtPath<PasteCurtainSettings>(CurtainSettingsPath);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(s => s.path == ScenePath);
            if (existing >= 0)
            {
                if (!scenes[existing].enabled)
                {
                    scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }

                return;
            }

            // ロビーの次（無ければ最後）に入れる。順番は読み込みに関係ない（名前で読み込む）が、見やすさのため。
            var lobby = scenes.FindIndex(s => s.path.EndsWith("/Lobby.unity"));
            scenes.Insert(lobby >= 0 ? lobby + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[キャラ選択] ビルドの設定にキャラ選択のシーンを足しました。");
        }

        private static void SetupCamera(Scene scene)
        {
            // 画面はすべて UI なので、カメラは画面を塗るだけ（ロビーから来たときはロビーのカメラが止まり、これが使われる）。
            Camera camera = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.TryGetComponent(out camera))
                {
                    break;
                }
            }

            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.gameObject.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
        }

        // ───────── 画面（UI）─────────
        // 位置はデザイナーの見本（1920×1080）から読み取ったピクセル（左上が 0,0・下向きが +）。

        private static void BuildUi(CharacterSelectView view, CharacterRoster roster)
        {
            var go = new GameObject(UiRootName);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // ロビーの画面（通知 400・幕 900）より下、ロビーの背景（0）より上。
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var root = go.transform;

            // 背景（白地に柄の絵があとで届く。届いたら Background の Image に絵を入れる。今は薄い色だけ）。
            var background = NewImage("Background", root, null, BackgroundColor);
            Stretch(background.rectTransform);

            // 上の帯（「CHARACTER SELECT」「残り　秒」は絵に描いてある。数字だけ文字で重ねる）。
            var header = NewRect("Header", root);
            Stretch(header);
            view.headerArea = header;
            NewUiImage("HeaderLine", header, "header_line.png", 0f, 0f, 1920f, 122f);
            view.timerText = NewText("Timer", header, "50", 76, Ink, FontStyle.Bold);
            PlaceTL(view.timerText.rectTransform, 880f, 8f, 160f, 92f);

            // 左: 自分の大きい区画
            var left = NewRect("Left", root);
            Stretch(left);
            view.leftArea = left;
            view.mainPanel = BuildMainPanel(left);

            // 右: ほかの人の欄（自分が決定すると出る）と、その手前のスキル動画のモニター
            var right = NewRect("Right", root);
            Stretch(right);
            view.rightArea = right;
            view.rows = new OtherPlayerRow[CharacterSelectBoard.SeatCount - 1];
            for (var i = 0; i < view.rows.Length; i++)
            {
                view.rows[i] = BuildRow(right, i);
            }

            view.monitor = BuildMonitor(right);

            // 下: 帯・キャラのボタン・決定のゲージ・設定（飾り）
            var bottom = NewRect("Bottom", root);
            Stretch(bottom);
            view.bottomArea = bottom;
            NewUiImage("UnderLine", bottom, "under_line.png", 0f, 997f, 1926f, 83f);
            view.buttons = BuildButtons(bottom, roster);
            view.gauge = BuildGauge(bottom);
            // 会場は握るだけなので押す手段は無い（飾り。ユーザー決定 2026-11-05）。
            NewUiImage("OptionButton", bottom, "option_button.png", 1584f, 990f, 336f, 90f);
            // 1台で試すときだけ出す仮のキー（下の帯の、操作の説明と設定の間）。
            view.devKeysText = NewText("DevKeys", bottom, "仮のキー：A", 18, SubInk, FontStyle.Bold);
            view.devKeysText.alignment = TextAnchor.MiddleLeft;
            PlaceTL(view.devKeysText.rectTransform, 560f, 1008f, 1000f, 64f);


            // 全員決定のときに画面いっぱいに光る板と、真ん中から弾ける泡とキラキラ。
            view.screenFlash = NewImage("ScreenFlash", root, null, new Color(1f, 1f, 1f, 0f));
            Stretch(view.screenFlash.rectTransform);
            view.centerBurst = NewBurst("CenterBurst", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                new Vector2(24f, 64f), new Vector2(450f, 1200f), 112);

            // 全員が決まったら「GO!」（画面の真ん中）
            var banner = new GameObject("DecidedBanner", typeof(RectTransform), typeof(CanvasGroup));
            banner.transform.SetParent(root, false);
            Place((RectTransform)banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(560f, 210f));
            var group = banner.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var bannerBack = NewImage("Back", banner.transform, CharacterSelectArt.Load(CharacterSelectArt.Pill), new Color(1f, 1f, 1f, 0.92f));
            bannerBack.type = Image.Type.Sliced;
            Stretch(bannerBack.rectTransform);
            var bannerText = NewText("Text", banner.transform, "GO!", 150, Pink, FontStyle.Bold);
            Stretch(bannerText.rectTransform);
            AddOutline(bannerText, Ink, 5f);
            view.bannerGlitch = AddGlitch(bannerText, 14f);
            for (var side = -1; side <= 1; side += 2)
            {
                var tooth = NewImage(side < 0 ? "ToothLeft" : "ToothRight", banner.transform, CharacterSelectArt.Load(CharacterSelectArt.Tooth), Color.white);
                Place(tooth.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(side * 330f, 10f), new Vector2(150f, 150f));
                tooth.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * -14f);
            }
            view.decidedBanner = group;

            // 1台で試すとき、ゲームのシーンへ進む前に覆う板（ロビーから来たときはロビーの幕を使う）。
            var fade = new GameObject("FadeOut", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            fade.transform.SetParent(root, false);
            Stretch((RectTransform)fade.transform);
            var fadeImage = fade.GetComponent<Image>();
            fadeImage.color = new Color(0.96f, 0.95f, 1f);
            fadeImage.raycastTarget = false;
            view.fadeOut = fade.GetComponent<CanvasGroup>();
            view.fadeOut.alpha = 0f;
            view.fadeOut.blocksRaycasts = false;
        }

        /// <summary>自分の大きい区画（MAIN_base 900×501 を 58,160 に置く）。中の位置は土台の絵の中のピクセル。</summary>
        private static MainPlayerPanel BuildMainPanel(RectTransform parent)
        {
            var rect = NewRect("MainPanel", parent);
            PlaceTL(rect, 58f, 160f, 900f, 501f);
            var panel = rect.gameObject.AddComponent<MainPlayerPanel>();
            var body = NewRect("Body", rect);
            Stretch(body);
            panel.body = body;

            panel.baseImage = NewUiImage("Base", body, "main_player/MAIN_base_Red.png", 0f, 0f, 900f, 501f);
            panel.baseBySeat = new Sprite[CharacterSelectBoard.SeatCount];
            panel.baseBySeat[0] = LoadUi("main_player/MAIN_base_Red.png");

            panel.seatLabel = NewText("SeatLabel", body, "1P", 30, Color.white, FontStyle.Bold);
            PlaceTL(panel.seatLabel.rectTransform, 42f, 27f, 101f, 40f);

            // 立ち絵（左の空いた所。足元を下に合わせる）
            var portraitRoot = NewRect("Portrait", body);
            Place(portraitRoot, new Vector2(0f, 1f), new Vector2(PortraitCenterX, -PortraitBottomY), new Vector2(PortraitWidth, PortraitHeight), new Vector2(0.5f, 0f));
            panel.portraitRoot = portraitRoot;
            // 前の立ち絵（キャラを替えるとき、歯磨き粉で覆われるまで見えている。ふだんは隠れている）。
            var previous = NewImage("Previous", portraitRoot, null, Color.white);
            previous.preserveAspect = true;
            previous.enabled = false;
            Stretch(previous.rectTransform);
            previous.rectTransform.pivot = new Vector2(0.5f, 0f);
            panel.portrait = NewImage("Image", portraitRoot, null, Color.white);
            panel.portrait.preserveAspect = true;
            Stretch(panel.portrait.rectTransform);
            panel.portrait.rectTransform.pivot = new Vector2(0.5f, 0f);

            var swap = portraitRoot.gameObject.AddComponent<CharacterSwapFx>();
            swap.current = panel.portrait;
            swap.previous = previous;
            panel.swap = swap;

            // 能力の棒（土台の絵の棒の溝に重ねる。名前は絵に描いてある）
            var statTops = new[] { 124f, 173f, 222f };
            panel.stats = new StatBar[statTops.Length];
            for (var i = 0; i < statTops.Length; i++)
            {
                panel.stats[i] = NewGaugeBar($"Stat {i + 1}", body, "main_player/Main_gage", 603f, statTops[i], 247f, 36f);
            }

            // スキル（下の四角）
            panel.skillNameText = NewText("SkillName", body, "スキル名", 24, Color.white, FontStyle.Bold);
            panel.skillNameText.alignment = TextAnchor.UpperLeft;
            AddOutline(panel.skillNameText, Shadow, 1.5f);
            PlaceTL(panel.skillNameText.rectTransform, 494f, 290f, 346f, 32f);
            panel.skillText = NewText("SkillText", body, "スキルの説明", 19, Color.white, FontStyle.Normal);
            panel.skillText.alignment = TextAnchor.UpperLeft;
            panel.skillText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panel.skillText.lineSpacing = 1.05f;
            PlaceTL(panel.skillText.rectTransform, 494f, 324f, 346f, 52f);

            // 歯磨き粉を引く層（区画の中だけ。READY! のはんこより下）。中の枠は立ち絵の枠と同じ位置・大きさ。
            var pasteLayer = NewRect("PasteLayer", body);
            Stretch(pasteLayer);
            pasteLayer.gameObject.AddComponent<RectMask2D>();
            var pasteFrame = NewRect("PasteFrame", pasteLayer);
            Place(pasteFrame, new Vector2(0f, 1f), new Vector2(PortraitCenterX, -PortraitBottomY), new Vector2(PortraitWidth, PortraitHeight), new Vector2(0.5f, 0f));
            BuildPaste(swap, pasteFrame, portraitRoot);

            // READY! のはんこ（立ち絵の下の方）
            var stamp = NewRect("DecidedStamp", body);
            PlaceTL(stamp, PortraitCenterX - 170f, 330f, 340f, 110f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var stampBack = NewImage("Back", stamp, CharacterSelectArt.Load(CharacterSelectArt.Pill), new Color(1f, 1f, 1f, 0.92f));
            stampBack.type = Image.Type.Sliced;
            Stretch(stampBack.rectTransform);
            var stampTooth = NewImage("Tooth", stamp, CharacterSelectArt.Load(CharacterSelectArt.Tooth), Color.white);
            Place(stampTooth.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 4f), new Vector2(92f, 92f));
            stampTooth.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            panel.decidedStampText = NewText("Text", stamp, "READY!", 62, FortressColors.SeatColor(0), FontStyle.Bold);
            Stretch(panel.decidedStampText.rectTransform);
            panel.decidedStampText.rectTransform.offsetMin = new Vector2(84f, 0f);
            AddOutline(panel.decidedStampText, Ink, 2f);
            panel.decidedStamp = stamp.gameObject;
            stamp.gameObject.SetActive(false);

            // 決定したときに白く光る板と、立ち絵のあたりから弾ける泡とキラキラ（いちばん手前）。
            panel.flash = NewImage("Flash", body, null, new Color(1f, 1f, 1f, 0f));
            PlaceTL(panel.flash.rectTransform, 18f, 18f, 860f, 456f);
            var burst = NewBurst("Burst", body, new Vector2(0f, 1f), new Vector2(PortraitCenterX, -260f), new Vector2(18f, 46f), new Vector2(250f, 750f), 64);
            panel.burst = burst;
            return panel;
        }

        /// <summary>立ち絵を歯磨き粉で塗り替える部品（線・泡・キラーンの星）。</summary>
        private static void BuildPaste(CharacterSwapFx swap, RectTransform pasteFrame, RectTransform portraitRoot)
        {
            // 歯磨き粉を切り抜く型（「キャラの上だけに塗る」のときだけ使う。ふつうは切り抜かない）。
            var pasteMask = NewImage("Paste", pasteFrame, null, Color.white);
            pasteMask.preserveAspect = true;
            Stretch(pasteMask.rectTransform);
            pasteMask.rectTransform.pivot = new Vector2(0.5f, 0f);
            pasteMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            pasteMask.GetComponent<Mask>().enabled = false;
            pasteMask.enabled = false;
            swap.pasteMask = pasteMask;
            var strokeRect = NewRect("Stroke", pasteMask.rectTransform);
            Stretch(strokeRect);
            var stroke = strokeRect.gameObject.AddComponent<PasteStroke>();
            stroke.texture = CurtainTexture();
            stroke.raycastTarget = false;
            stroke.width = 38f;
            swap.stroke = stroke;
            var foam = NewBurst("Foam", pasteFrame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 36f), new Vector2(40f, 200f), 48);
            foam.sprites = new[] { CharacterSelectArt.Load(CharacterSelectArt.Bubble) };
            foam.gravity = -260f;
            foam.life = new Vector2(0.4f, 0.9f);
            foam.spin = 60f;
            swap.foam = foam;
            var shinePositions = new[] { new Vector2(90f, 360f), new Vector2(-105f, 240f), new Vector2(80f, 120f) };
            swap.shines = new RectTransform[shinePositions.Length];
            for (var i = 0; i < shinePositions.Length; i++)
            {
                var shine = NewImage($"Shine {i + 1}", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.Sparkle), Color.white);
                Place(shine.rectTransform, new Vector2(0.5f, 0f), shinePositions[i], Vector2.zero);
                swap.shines[i] = shine.rectTransform;
            }
        }

        /// <summary>ほかの人の欄（sub_base 851×151。上から 160・357・554 に並べる）。</summary>
        private static OtherPlayerRow BuildRow(RectTransform parent, int index)
        {
            var go = new GameObject($"Row {index + 1}", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            PlaceTL(rect, 1030f, 160f + index * 197f, 851f, 151f);
            var row = go.AddComponent<OtherPlayerRow>();
            row.group = go.GetComponent<CanvasGroup>();
            row.group.alpha = 0f;
            row.group.blocksRaycasts = false;
            var body = NewRect("Body", rect);
            Stretch(body);
            row.body = body;

            row.baseImage = NewUiImage("Base", body, "R_player/sub_base_Red.png", 0f, 0f, 851f, 151f);
            row.seatLabel = NewText("SeatLabel", body, $"{index + 2}\nP", 38, Color.white, FontStyle.Bold);
            row.seatLabel.lineSpacing = 0.85f;
            AddOutline(row.seatLabel, Shadow, 1.5f);
            PlaceTL(row.seatLabel.rectTransform, 2f, 0f, 54f, 151f);

            var statTops = new[] { 25f, 65f, 105f };
            row.stats = new StatBar[statTops.Length];
            for (var i = 0; i < statTops.Length; i++)
            {
                row.stats[i] = NewGaugeBar($"Stat {i + 1}", body, "R_player/sub_gage", 316f, statTops[i], 203f, 22f);
            }

            row.stateText = NewText("State", body, "えらび中…", 30, row.selectingColor, FontStyle.Bold);
            PlaceTL(row.stateText.rectTransform, 540f, 36f, 236f, 50f);
            row.skillNameText = NewText("SkillName", body, "スキル名", 19, SubInk, FontStyle.Bold);
            PlaceTL(row.skillNameText.rectTransform, 540f, 90f, 236f, 32f);
            row.burst = NewBurst("Burst", body, new Vector2(0f, 1f), new Vector2(660f, -75f), new Vector2(12f, 30f), new Vector2(160f, 460f), 32);
            return row;
        }

        /// <summary>スキル動画のモニター（R_monitor 930×560 を 990,150 に。右の蝶番を軸に開く）。</summary>
        private static SkillMonitor BuildMonitor(RectTransform parent)
        {
            var go = new GameObject("Monitor", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            // 回る軸（pivot）を右の端の真ん中に。
            Place(rect, new Vector2(0f, 1f), new Vector2(990f + 930f, -(150f + 280f)), new Vector2(930f, 560f), new Vector2(1f, 0.5f));
            var monitor = go.AddComponent<SkillMonitor>();
            monitor.door = rect;
            monitor.group = go.GetComponent<CanvasGroup>();
            monitor.group.blocksRaycasts = false;

            // 画面（白い所）: 動画が無いときの板 → 動画
            var screenBack = NewImage("ScreenBack", rect, null, MonitorBack);
            PlaceTL(screenBack.rectTransform, 88f, 60f, 758f, 425f);
            var placeholder = NewRect("Placeholder", rect);
            PlaceTL(placeholder, 88f, 60f, 758f, 425f);
            monitor.placeholder = placeholder.gameObject;
            monitor.placeholderPortrait = NewImage("Portrait", placeholder, null, Color.white);
            monitor.placeholderPortrait.preserveAspect = true;
            PlaceTL(monitor.placeholderPortrait.rectTransform, 30f, 20f, 280f, 395f);
            monitor.placeholderText = NewText("Text", placeholder, "スキル名\nスキル動画は準備中", 40, Color.white, FontStyle.Bold);
            monitor.placeholderText.supportRichText = true;
            monitor.placeholderText.lineSpacing = 1.2f;
            AddOutline(monitor.placeholderText, Shadow, 2f);
            PlaceTL(monitor.placeholderText.rectTransform, 320f, 20f, 420f, 385f);

            var screen = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            screen.transform.SetParent(rect, false);
            PlaceTL((RectTransform)screen.transform, 88f, 60f, 758f, 425f);
            monitor.screen = screen.GetComponent<RawImage>();
            monitor.screen.raycastTarget = false;
            monitor.player = go.AddComponent<VideoPlayer>();
            monitor.player.playOnAwake = false;
            monitor.player.isLooping = true;
            monitor.player.audioOutputMode = VideoAudioOutputMode.None;
            monitor.player.renderMode = VideoRenderMode.RenderTexture;
            monitor.player.aspectRatio = VideoAspectRatio.FitInside;

            NewUiImage("Frame", rect, "R_monitor.png", 0f, 0f, 930f, 560f);
            return monitor;
        }

        /// <summary>キャラのボタン（キャラの一覧の順に左から。見本どおり 赤・青・黄・緑）と、1P〜4P の札・矢印。</summary>
        private static CharacterButtonRow BuildButtons(RectTransform parent, CharacterRoster roster)
        {
            var rect = NewRect("CharacterButtons", parent);
            Stretch(rect);
            var row = rect.gameObject.AddComponent<CharacterButtonRow>();
            var count = roster != null ? Mathf.Max(1, roster.Count) : 4;
            row.buttons = new CharacterButton[count];
            for (var i = 0; i < count; i++)
            {
                var character = roster != null ? roster.Get(i) : null;
                var buttonRect = NewRect($"Button {i + 1}", rect);
                Place(buttonRect, new Vector2(0f, 1f), new Vector2(ButtonFirstX + i * ButtonSpacing, -ButtonY), new Vector2(10f, 10f));
                var button = buttonRect.gameObject.AddComponent<CharacterButton>();
                button.image = NewImage("Image", buttonRect, character != null ? character.buttonOff : null, Color.white);
                button.image.preserveAspect = true;
                Place(button.image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, button.offSize);
                row.buttons[i] = button;
            }

            var arrow = NewUiImage("Arrow", rect, "other/charabutton_arrow.png", 0f, 0f, 52f, 59f);
            Place(arrow.rectTransform, new Vector2(0f, 1f), new Vector2(ButtonFirstX + 150f, -ButtonY), new Vector2(52f, 59f), new Vector2(0f, 0.5f));
            row.arrow = arrow.rectTransform;

            row.badges = new RectTransform[CharacterSelectBoard.SeatCount];
            row.badgeGlows = new Graphic[CharacterSelectBoard.SeatCount];
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                var badge = NewRect($"Badge {seat + 1}P", rect);
                Place(badge, new Vector2(0f, 1f), new Vector2(ButtonFirstX, -ButtonY), new Vector2(52f, 52f));
                var glow = NewImage("Glow", badge, CharacterSelectArt.Load(CharacterSelectArt.Circle), new Color(1f, 1f, 1f, 0f));
                Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));
                var icon = NewUiImage("Icon", badge, $"other/charabutton_icon_{seat + 1}P.png", 0f, 0f, 52f, 52f);
                Stretch(icon.rectTransform);
                row.badges[seat] = badge;
                row.badgeGlows[seat] = glow;
            }

            return row;
        }

        /// <summary>決定のゲージ（R_pushgage_frame 1011×281 を 930,722 に）。</summary>
        private static PushGauge BuildGauge(RectTransform parent)
        {
            var rect = NewRect("PushGauge", parent);
            PlaceTL(rect, 930f, 722f, 1011f, 281f);
            var gauge = rect.gameObject.AddComponent<PushGauge>();
            var body = NewRect("Body", rect);
            Stretch(body);
            gauge.body = body;
            NewUiImage("Frame", body, "other/R_pushgage_frame.png", 0f, 0f, 1011f, 281f);
            gauge.fill = NewUiImage("Fill", body, "other/R_pushgage_gage.png", 237f, 105f, 729f, 67f);
            MakeFilled(gauge.fill);
            var waiting = NewUiImage("Waiting", body, "other/R_pushgage_Waitingtxt.png", 356f, 106f, 486f, 65f);
            waiting.color = new Color(1f, 1f, 1f, 0f);
            gauge.waitingText = waiting;
            return gauge;
        }

        /// <summary>土台・伸びる線・枠の3枚重ねの棒（name_base / name_line / name_frame）。</summary>
        private static StatBar NewGaugeBar(string name, RectTransform parent, string prefix, float x, float y, float w, float h)
        {
            var rect = NewRect(name, parent);
            PlaceTL(rect, x, y, w, h);
            var bar = rect.gameObject.AddComponent<StatBar>();
            Stretch(NewUiImage("Base", rect, prefix + "_base.png", 0f, 0f, w, h).rectTransform);
            bar.fill = NewUiImage("Fill", rect, prefix + "_line.png", 0f, 0f, w, h);
            Stretch(bar.fill.rectTransform);
            MakeFilled(bar.fill);
            Stretch(NewUiImage("Frame", rect, prefix + "_frame.png", 0f, 0f, w, h).rectTransform);
            return bar;
        }

        // 無くても警告を出さない（色ごとの絵がまだ届いていないときなど）。
        private static Sprite LoadUiQuiet(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(UiArtFolder + "/" + file);

        private static Sprite LoadUi(string file)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiArtFolder + "/" + file);
            if (sprite == null)
            {
                Debug.LogWarning($"[キャラ選択] {UiArtFolder}/{file} が見つからないか、Sprite になっていません。");
            }

            return sprite;
        }

        /// <summary>届いた画面の絵（Art/UI の中）を、左上 x,y・大きさ w,h に置く。</summary>
        private static Image NewUiImage(string name, Transform parent, string file, float x, float y, float w, float h)
        {
            var image = NewImage(name, parent, LoadUi(file), Color.white);
            PlaceTL(image.rectTransform, x, y, w, h);
            return image;
        }

        /// <summary>親の左上を 0,0（下向きが +）として置く。</summary>
        private static void PlaceTL(RectTransform rect, float x, float y, float w, float h)
        {
            var topLeft = new Vector2(0f, 1f);
            Place(rect, topLeft, new Vector2(x, -y), new Vector2(w, h), topLeft);
        }

        // ───────── 小物 ─────────

        private static Font LegacyFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>泡とキラキラを弾けさせる部品を置く（絵はキラキラと泡を交互に使う）。</summary>
        private static CharacterSelectBurst NewBurst(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 speed, int pool)
        {
            var rect = NewRect(name, parent);
            Place(rect, anchor, position, new Vector2(10f, 10f));
            var burst = rect.gameObject.AddComponent<CharacterSelectBurst>();
            burst.sprites = new[] { CharacterSelectArt.Load(CharacterSelectArt.Sparkle), CharacterSelectArt.Load(CharacterSelectArt.Bubble), CharacterSelectArt.Load(CharacterSelectArt.Tooth) };
            burst.size = size;
            burst.speed = speed;
            burst.poolSize = pool;
            return burst;
        }

        /// <summary>文字に色ずれノイズ（マゼンタ・水色の Shadow 2つ）を付ける。</summary>
        private static CharacterSelectGlitch AddGlitch(Graphic graphic, float amplitude)
        {
            var magenta = graphic.gameObject.AddComponent<Shadow>();
            magenta.effectColor = Color.clear;
            magenta.effectDistance = Vector2.zero;
            var cyan = graphic.gameObject.AddComponent<Shadow>();
            cyan.effectColor = Color.clear;
            cyan.effectDistance = Vector2.zero;
            var glitch = graphic.gameObject.AddComponent<CharacterSelectGlitch>();
            glitch.magenta = magenta;
            glitch.cyan = cyan;
            glitch.amplitude = amplitude;
            return glitch;
        }

        private static GameObject FindRoot(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void MakeFilled(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
        }

        private static Text NewText(string name, Transform parent, string text, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            // 日本語も出るように、Unity 内蔵のフォント（足りない文字はOSのフォントで補われる）を使う。
            label.font = LegacyFont();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void AddOutline(Graphic graphic, Color color, float distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
