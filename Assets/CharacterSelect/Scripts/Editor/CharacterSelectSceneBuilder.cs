using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MS2026.CharacterSelect.EditorTools
{
    /// <summary>
    /// メニュー「Tools/キャラ選択/シーンを組み立てる」: Assets/Scenes/Character_Select.unity にキャラ選択の画面（2D）を作る。
    /// 画面は 1920×1080 基準: 4つの区画（左上 1P・左下 2P・右上 3P・右下 4P）＋下に 1P〜4P の丸（区画と同じ内容を映す）。
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

        // 画面の割り付け（1920×1080 基準のピクセル）。
        private const float BottomAreaHeight = 250f;
        private const float QuadrantWidth = 960f;
        private const float QuadrantHeight = (1080f - BottomAreaHeight) * 0.5f;

        private static readonly Color Ink = new Color(0.16f, 0.17f, 0.33f);
        private static readonly Color SubInk = new Color(0.32f, 0.35f, 0.5f);
        private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color Mint = new Color(0.3f, 0.85f, 0.62f);
        private static readonly Color Pink = new Color(1f, 0.42f, 0.7f);
        private static readonly Color BarFill = new Color(0.12f, 0.36f, 0.5f);
        private static readonly Color SkillBox = new Color(0.24f, 0.16f, 0.12f, 0.8f);

        private sealed class Placeholder
        {
            public string file;
            public string id;
            public Color color;
            public (string label, float value)[] stats;

            // 前の版の仮の値（ファイルにこの値が残っていたら、今の仮の値に入れ替える。自分で書き換えた値には触らない）。
            public string oldName;
            public Color oldColor;
            public string oldSkill;
        }

        // 仮のキャラ: 名前はすべて「キャラ名（仮）」、色は赤・青・緑・黄のキューブ（ユーザー決定 2026-10-12）。文章もすべて仮。
        private const string PlaceholderName = "キャラ名（仮）";

        // 前の版の仮の名前（どのキャラも同じ）。
        private const string OldPlaceholderName = "仮";
        private const string PlaceholderSkillName = "スキル名（仮）";
        private const string PlaceholderSkill = "ここにスキルの説明が入ります（仮）。";

        private static readonly Placeholder[] PlaceholderCharacters =
        {
            new Placeholder
            {
                file = "Chara_Alpha", id = "alpha", color = new Color(0.93f, 0.27f, 0.3f),
                stats = new[] { ("パワー", 0.8f), ("スピード", 0.45f), ("ため", 0.6f) },
                oldName = "アルファ（仮）", oldColor = new Color(1f, 0.55f, 0.78f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：握り続けるほどビームが太くなる。"
            },
            new Placeholder
            {
                file = "Chara_Beta", id = "beta", color = new Color(0.22f, 0.52f, 0.96f),
                stats = new[] { ("パワー", 0.5f), ("スピード", 0.85f), ("ため", 0.4f) },
                oldName = "ベータ（仮）", oldColor = new Color(0.35f, 0.82f, 1f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：すばやく連続で撃てる。"
            },
            new Placeholder
            {
                file = "Chara_Gamma", id = "gamma", color = new Color(0.25f, 0.74f, 0.42f),
                stats = new[] { ("パワー", 0.65f), ("スピード", 0.6f), ("ため", 0.75f) },
                oldName = "ガンマ（仮）", oldColor = new Color(0.68f, 0.58f, 1f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：ためると広い範囲をきれいにする。"
            },
            new Placeholder
            {
                file = "Chara_Delta", id = "delta", color = new Color(0.98f, 0.84f, 0.2f),
                stats = new[] { ("パワー", 0.9f), ("スピード", 0.3f), ("ため", 0.5f) },
                oldName = "デルタ（仮）", oldColor = new Color(1f, 0.7f, 0.3f), oldSkill = "ここにスキルの説明が入ります（仮）。\n例：一撃が重く、かたい敵に強い。"
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
            view.controller = controller;
            BuildUi(view);
            var kept = snapshot != null ? snapshot.Apply(new[] { stage, FindRoot(scene, UiRootName) }) : 0;
            if (kept > 0)
            {
                Debug.Log($"[キャラ選択] 前の数値を {kept} 個の部品に引き継ぎました。");
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

        private static void BuildUi(CharacterSelectView view)
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

            // 背景（会社のオフィスの絵に差し替える）
            var background = NewImage("Background", root, CharacterSelectArt.Load(CharacterSelectArt.Background), Color.white);
            Stretch(background.rectTransform);

            // 画面全体を漂う飾り（歯磨き粉の泡・2進数・キラキラ）。区画の後ろに置く（区画は半透明なので透けて見える）。
            var ambientRect = NewRect("Ambient", root);
            Stretch(ambientRect);
            var ambient = ambientRect.gameObject.AddComponent<CharacterSelectAmbient>();
            ambient.bubble = CharacterSelectArt.Load(CharacterSelectArt.Bubble);
            ambient.sparkle = CharacterSelectArt.Load(CharacterSelectArt.Sparkle);
            ambient.font = LegacyFont();

            // 区画の仕切り（縦と横の細い線）
            var dividers = NewRect("Dividers", root);
            Stretch(dividers);
            var vertical = NewImage("Vertical", dividers, null, new Color(1f, 1f, 1f, 0.5f));
            Place(vertical.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4f, QuadrantHeight * 2f), new Vector2(0.5f, 1f));
            var horizontal = NewImage("Horizontal", dividers, null, new Color(1f, 1f, 1f, 0.5f));
            Place(horizontal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -QuadrantHeight), new Vector2(1920f, 4f));

            // 4つの区画: 左上 1P・左下 2P・右上 3P・右下 4P
            var panels = NewRect("Panels", root);
            Stretch(panels);
            view.panels = new PlayerPanel[CharacterSelectBoard.SeatCount];
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                view.panels[seat] = BuildPanel(panels, seat);
            }

            // 下: 帯・1P〜4P の丸・操作の説明・ルール（まとめて Bottom の中。登場のときに下からせり上がる）
            var bottomArea = NewRect("Bottom", root);
            Stretch(bottomArea);
            view.bottomArea = bottomArea;
            var bar = NewImage("BottomBar", bottomArea, null, new Color(0.55f, 0.83f, 0.95f, 0.92f));
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(0f, 110f);

            var circles = NewRect("Circles", bottomArea);
            Place(circles, new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(10f, 10f));
            view.circles = new PlayerCircle[CharacterSelectBoard.SeatCount];
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                view.circles[seat] = BuildCircle(circles, seat);
            }

            // 下の丸（画面の真ん中）にかからないよう、左に2行で。
            view.helpText = NewText("Help", bottomArea, "短く握る→カーソル移動　長く握る→READY\n仮のキー：A", 24, Ink, FontStyle.Bold);
            view.helpText.alignment = TextAnchor.LowerLeft;
            view.helpText.lineSpacing = 1.1f;
            Place(view.helpText.rectTransform, Vector2.zero, new Vector2(24f, 12f), new Vector2(600f, 80f), Vector2.zero);
            view.ruleText = NewText("Rule", bottomArea, "同じキャラ：えらべない（早い者勝ち）", 22, SubInk, FontStyle.Bold);
            view.ruleText.alignment = TextAnchor.LowerRight;
            Place(view.ruleText.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(600f, 40f), new Vector2(1f, 0f));

            view.timerText = NewText("Timer", root, "のこり 30", 40, Color.white, FontStyle.Bold);
            view.timerText.enabled = false;
            AddOutline(view.timerText, Shadow, 2f);
            Place(view.timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(400f, 56f), new Vector2(0.5f, 1f));

            // 全員決定のときに画面いっぱいに光る板と、真ん中から弾ける泡とキラキラ。
            view.screenFlash = NewImage("ScreenFlash", root, null, new Color(1f, 1f, 1f, 0f));
            Stretch(view.screenFlash.rectTransform);
            view.centerBurst = NewBurst("CenterBurst", root, new Vector2(0.5f, 0.5f), new Vector2(0f, BottomAreaHeight * 0.5f),
                new Vector2(24f, 64f), new Vector2(450f, 1200f), 112);

            // 全員が決まったら「GO!」（区画の真ん中）
            var banner = new GameObject("DecidedBanner", typeof(RectTransform), typeof(CanvasGroup));
            banner.transform.SetParent(root, false);
            Place((RectTransform)banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, BottomAreaHeight * 0.5f), new Vector2(560f, 210f));
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

        private static PlayerPanel BuildPanel(RectTransform parent, int seat)
        {
            var color = FortressColors.SeatColor(seat);
            var right = seat >= 2;
            var bottom = seat % 2 == 1;
            var go = new GameObject($"Panel {seat + 1}P", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            var corner = new Vector2(right ? 1f : 0f, 1f);
            Place(rect, corner, new Vector2(0f, bottom ? -QuadrantHeight : 0f), new Vector2(QuadrantWidth, QuadrantHeight), corner);
            var panel = go.AddComponent<PlayerPanel>();
            panel.group = go.GetComponent<CanvasGroup>();
            panel.group.blocksRaycasts = false;

            panel.areaTint = NewImage("AreaTint", rect, null, WithAlpha(color, 0.3f));
            Stretch(panel.areaTint.rectTransform);

            // ドット柄（参考の対戦ゲームの網点。右上ほど大きい水玉）。色は再生中にプレイヤーの色を明るくしてかける。
            panel.pattern = NewImage("Pattern", rect, CharacterSelectArt.Load(CharacterSelectArt.Halftone), WithAlpha(Color.Lerp(color, Color.white, 0.45f), 0.45f));
            Stretch(panel.pattern.rectTransform);

            // ゆっくり流れる斜めのしま。
            var stripes = new GameObject("Stripes", typeof(RectTransform), typeof(RawImage));
            stripes.transform.SetParent(rect, false);
            Stretch((RectTransform)stripes.transform);
            var stripesImage = stripes.GetComponent<RawImage>();
            stripesImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterSelectArt.Stripes);
            stripesImage.color = new Color(1f, 1f, 1f, 0.12f);
            stripesImage.raycastTarget = false;
            var scroll = stripes.AddComponent<CharacterSelectScroll>();
            scroll.speed = new Vector2(0.1f, 0.1f);

            // 区画の白い枠。
            var frame = NewImage("Frame", rect, CharacterSelectArt.Load(CharacterSelectArt.Frame), new Color(1f, 1f, 1f, 0.75f));
            frame.type = Image.Type.Sliced;
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(6f, 6f);
            frame.rectTransform.offsetMax = new Vector2(-6f, -6f);

            var body = NewRect("Body", rect);
            Stretch(body);
            panel.body = body;
            var topLeft = new Vector2(0f, 1f);

            // 立ち絵（左。下の端が区画の下の少し上）
            var portraitRoot = NewRect("Portrait", body);
            // 頭が左上の「1P」の札にかからない高さ。
            Place(portraitRoot, Vector2.zero, new Vector2(180f, 10f), new Vector2(300f, 340f), new Vector2(0.5f, 0f));
            panel.portraitRoot = portraitRoot;
            panel.portraitFallback = NewImage("Fallback", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.Circle), Color.white);
            Place(panel.portraitFallback.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
            panel.portraitFallback.enabled = false;
            panel.portraitLetter = NewText("Letter", portraitRoot, "?", 120, Color.white, FontStyle.Bold);
            Place(panel.portraitLetter.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 240f));
            AddOutline(panel.portraitLetter, Shadow, 3f);
            panel.portraitLetter.enabled = false;
            // 前の立ち絵（キャラを替えるとき、歯磨き粉で覆われるまで見えている。ふだんは隠れている）。
            var previous = NewImage("Previous", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.CharaCube), Color.white);
            previous.preserveAspect = true;
            previous.enabled = false;
            Stretch(previous.rectTransform);
            previous.rectTransform.pivot = new Vector2(0.5f, 0f);

            panel.portrait = NewImage("Image", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.CharaCube), Color.white);
            panel.portrait.preserveAspect = true;
            Stretch(panel.portrait.rectTransform);
            panel.portrait.rectTransform.pivot = new Vector2(0.5f, 0f);

            // 歯磨き粉で塗り替えて切り替え: 歯磨き粉（立ち絵の枠の中だけに見える）・泡・キラーンの星。
            var swap = portraitRoot.gameObject.AddComponent<CharacterSwapFx>();
            swap.current = panel.portrait;
            swap.previous = previous;
            // 歯磨き粉を切り抜く型: キャラの絵を入れた Image + Mask（型そのものは見せない）。「キャラの上だけに塗る」(clipToCharacter) のときだけ使う。後で PasteLayer へ移す。
            var pasteMask = NewImage("Paste", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.CharaCube), Color.white);
            pasteMask.preserveAspect = true;
            Stretch(pasteMask.rectTransform);
            pasteMask.rectTransform.pivot = new Vector2(0.5f, 0f);
            pasteMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // ふつうは切り抜かない（道すじのとおりに外まで引く。ユーザー 2026-10-12）。
            pasteMask.GetComponent<Mask>().enabled = false;
            pasteMask.enabled = false;
            swap.pasteMask = pasteMask;
            var paste = pasteMask.rectTransform;
            // ジグザグに塗る歯磨き粉の線（タイトルと同じ歯磨き粉の絵を、線に沿ったしまとして貼る）。
            var strokeRect = NewRect("Stroke", paste);
            Stretch(strokeRect);
            var stroke = strokeRect.gameObject.AddComponent<PasteStroke>();
            stroke.texture = CurtainTexture();
            stroke.raycastTarget = false;
            // ユーザーの絵のように、ジグザグの線が1本ずつ見える太さ（チューブから出した歯磨き粉くらい）。
            stroke.width = 38f;
            swap.stroke = stroke;
            var foam = NewBurst("Foam", portraitRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 36f), new Vector2(40f, 200f), 48);
            foam.sprites = new[] { CharacterSelectArt.Load(CharacterSelectArt.Bubble) };
            foam.gravity = -260f;
            foam.life = new Vector2(0.4f, 0.9f);
            foam.spin = 60f;
            swap.foam = foam;
            var shinePositions = new[] { new Vector2(80f, 280f), new Vector2(-95f, 185f), new Vector2(70f, 95f) };
            swap.shines = new RectTransform[shinePositions.Length];
            for (var i = 0; i < shinePositions.Length; i++)
            {
                var shine = NewImage($"Shine {i + 1}", portraitRoot, CharacterSelectArt.Load(CharacterSelectArt.Sparkle), Color.white);
                Place(shine.rectTransform, new Vector2(0.5f, 0f), shinePositions[i], Vector2.zero);
                swap.shines[i] = shine.rectTransform;
            }

            panel.swap = swap;
            // 歯磨き粉で塗り替えて切り替えるので、立ち絵の横すべりは使わない。
            panel.slideInPixels = 0f;

            // 説明の板（右）: 能力の棒グラフ・スキル・握りのゲージ
            panel.infoPanel = NewImage("InfoPanel", body, CharacterSelectArt.Load(CharacterSelectArt.Panel), WithAlpha(color, 0.55f));
            panel.infoPanel.type = Image.Type.Sliced;
            var info = panel.infoPanel.rectTransform;
            Place(info, topLeft, new Vector2(345f, -80f), new Vector2(585f, 325f), topLeft);

            var stats = NewRect("Stats", info);
            Place(stats, topLeft, new Vector2(24f, -34f), new Vector2(540f, 120f), topLeft);
            var layout = stats.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panel.statTemplate = BuildStatTemplate(stats);

            var skill = NewImage("SkillBox", info, CharacterSelectArt.Load(CharacterSelectArt.Panel), SkillBox);
            skill.type = Image.Type.Sliced;
            Place(skill.rectTransform, topLeft, new Vector2(24f, -166f), new Vector2(537f, 118f), topLeft);
            panel.skillNameText = NewText("SkillName", skill.rectTransform, "スキル名", 24, Color.white, FontStyle.Bold);
            panel.skillNameText.alignment = TextAnchor.UpperLeft;
            Place(panel.skillNameText.rectTransform, topLeft, new Vector2(16f, -8f), new Vector2(505f, 32f), topLeft);
            panel.skillText = NewText("SkillText", skill.rectTransform, "スキル内容", 21, Color.white, FontStyle.Normal);
            panel.skillText.alignment = TextAnchor.UpperLeft;
            panel.skillText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(panel.skillText.rectTransform, topLeft, new Vector2(16f, -42f), new Vector2(505f, 70f), topLeft);

            var gaugeBack = NewImage("HoldGaugeBack", info, CharacterSelectArt.Load(CharacterSelectArt.Bar), new Color(0f, 0f, 0f, 0.3f));
            Place(gaugeBack.rectTransform, topLeft, new Vector2(24f, -296f), new Vector2(300f, 14f), topLeft);
            panel.holdGauge = NewImage("HoldGauge", gaugeBack.rectTransform, CharacterSelectArt.Load(CharacterSelectArt.Bar), color);
            MakeFilled(panel.holdGauge);
            Stretch(panel.holdGauge.rectTransform);
            panel.stateText = NewText("State", info, "えらび中", 20, Color.white, FontStyle.Bold);
            panel.stateText.alignment = TextAnchor.MiddleLeft;
            AddOutline(panel.stateText, Shadow, 1.5f);
            Place(panel.stateText.rectTransform, topLeft, new Vector2(338f, -289f), new Vector2(240f, 28f), topLeft);

            // キャラ名の札（説明の板の左上に重ねる。斜めの札）
            panel.nameTagBackground = NewImage("NameTag", body, CharacterSelectArt.Load(CharacterSelectArt.NameTag), color);
            panel.nameTagBackground.type = Image.Type.Sliced;
            Place(panel.nameTagBackground.rectTransform, topLeft, new Vector2(355f, -34f), new Vector2(340f, 64f), topLeft);
            panel.nameText = NewText("Name", panel.nameTagBackground.rectTransform, "キャラ名", 38, Color.white, FontStyle.Bold);
            Stretch(panel.nameText.rectTransform);
            // 長い名前は札に収まるように小さくする。
            panel.nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panel.nameText.verticalOverflow = VerticalWrapMode.Truncate;
            panel.nameText.resizeTextForBestFit = true;
            panel.nameText.resizeTextMinSize = 20;
            panel.nameText.resizeTextMaxSize = 38;
            AddOutline(panel.nameText, Ink, 3f);
            var drop = panel.nameText.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.45f);
            drop.effectDistance = new Vector2(3f, -4f);
            panel.nameGlitch = AddGlitch(panel.nameText, 9f);

            // 「1P」・名前・あなた
            panel.playerLabelBackground = NewImage("PlayerLabelBack", body, CharacterSelectArt.Load(CharacterSelectArt.Pill), color);
            panel.playerLabelBackground.type = Image.Type.Sliced;
            Place(panel.playerLabelBackground.rectTransform, topLeft, new Vector2(20f, -16f), new Vector2(100f, 50f), topLeft);
            panel.playerLabel = NewText("PlayerLabel", panel.playerLabelBackground.rectTransform, $"{seat + 1}P", 30, Color.white, FontStyle.Bold);
            Stretch(panel.playerLabel.rectTransform);
            AddOutline(panel.playerLabel, Shadow, 1.5f);
            // プレイヤーの名前（ネット対戦のとき）は濃い帯の上に。名前が無いとき（1台で試すとき）は帯ごと隠れる。
            var nameBar = NewImage("PlayerNameBar", body, CharacterSelectArt.Load(CharacterSelectArt.Pill), new Color(0.1f, 0.14f, 0.3f, 0.85f));
            nameBar.type = Image.Type.Sliced;
            Place(nameBar.rectTransform, topLeft, new Vector2(128f, -20f), new Vector2(210f, 42f), topLeft);
            panel.playerNameBar = nameBar.gameObject;
            panel.playerNameText = NewText("PlayerName", nameBar.rectTransform, $"P{seat + 1}", 22, Color.white, FontStyle.Bold);
            Stretch(panel.playerNameText.rectTransform);
            var local = NewText("LocalMark", body, "あなた", 22, Color.white, FontStyle.Bold);
            AddOutline(local, Shadow, 2f);
            Place(local.rectTransform, topLeft, new Vector2(20f, -70f), new Vector2(100f, 30f), topLeft);
            panel.localMark = local.gameObject;

            // 歯磨き粉を引く層（説明の板・名前の札より上、READY! のはんこより下）。道すじのとおりにキャラの外まで引くが、区画の外には出さない。
            // 中の枠は立ち絵の枠と同じ位置・大きさ（線の道すじは枠の大きさで決まる）。泡も歯磨き粉と同じ高さに出す。
            var pasteLayer = NewRect("PasteLayer", body);
            Stretch(pasteLayer);
            pasteLayer.gameObject.AddComponent<RectMask2D>();
            var pasteFrame = NewRect("PasteFrame", pasteLayer);
            Place(pasteFrame, Vector2.zero, new Vector2(180f, 10f), new Vector2(300f, 340f), new Vector2(0.5f, 0f));
            pasteMask.rectTransform.SetParent(pasteFrame, false);
            foam.transform.SetParent(pasteFrame, false);

            // READY! のはんこ（立ち絵の上。左に歯のキャラ）
            var stamp = NewRect("DecidedStamp", body);
            Place(stamp, Vector2.zero, new Vector2(180f, 190f), new Vector2(340f, 110f));
            stamp.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var stampBack = NewImage("Back", stamp, CharacterSelectArt.Load(CharacterSelectArt.Pill), new Color(1f, 1f, 1f, 0.92f));
            stampBack.type = Image.Type.Sliced;
            Stretch(stampBack.rectTransform);
            var stampTooth = NewImage("Tooth", stamp, CharacterSelectArt.Load(CharacterSelectArt.Tooth), Color.white);
            Place(stampTooth.rectTransform, new Vector2(0f, 0.5f), new Vector2(52f, 4f), new Vector2(92f, 92f));
            stampTooth.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            panel.decidedStampText = NewText("Text", stamp, "READY!", 62, color, FontStyle.Bold);
            Stretch(panel.decidedStampText.rectTransform);
            panel.decidedStampText.rectTransform.offsetMin = new Vector2(84f, 0f);
            AddOutline(panel.decidedStampText, Ink, 2f);
            panel.decidedStamp = stamp.gameObject;

            // 決定したときに白く光る板と、立ち絵のあたりから弾ける泡とキラキラ（いちばん手前）。
            panel.flash = NewImage("Flash", rect, null, new Color(1f, 1f, 1f, 0f));
            Stretch(panel.flash.rectTransform);
            panel.burst = NewBurst("Burst", rect, Vector2.zero, new Vector2(180f, 200f), new Vector2(18f, 46f), new Vector2(250f, 750f), 64);
            stamp.gameObject.SetActive(false);
            return panel;
        }

        private static StatBar BuildStatTemplate(RectTransform parent)
        {
            var rect = NewRect("StatTemplate", parent);
            rect.sizeDelta = new Vector2(540f, 30f);
            rect.pivot = new Vector2(0f, 1f);
            var bar = rect.gameObject.AddComponent<StatBar>();
            var topLeft = new Vector2(0f, 1f);
            bar.label = NewText("Label", rect, "パワー", 22, Color.white, FontStyle.Bold);
            bar.label.alignment = TextAnchor.MiddleLeft;
            AddOutline(bar.label, Shadow, 1.5f);
            Place(bar.label.rectTransform, topLeft, Vector2.zero, new Vector2(130f, 30f), topLeft);
            var back = NewImage("BarBack", rect, CharacterSelectArt.Load(CharacterSelectArt.Bar), new Color(0f, 0f, 0f, 0.3f));
            Place(back.rectTransform, topLeft, new Vector2(140f, -6f), new Vector2(380f, 18f), topLeft);
            bar.fill = NewImage("Fill", back.rectTransform, CharacterSelectArt.Load(CharacterSelectArt.Bar), BarFill);
            MakeFilled(bar.fill);
            Stretch(bar.fill.rectTransform);
            return bar;
        }

        /// <summary>下の丸（1P〜4P）。左から 1P・2P・3P・4P を 170px 間隔で並べ、「1P」の札は 1P・2P が上、3P・4P が下（モックどおり）。</summary>
        private static PlayerCircle BuildCircle(RectTransform parent, int seat)
        {
            var color = FortressColors.SeatColor(seat);
            var go = new GameObject($"Circle {seat + 1}P", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2((seat - 1.5f) * 170f, 0f), new Vector2(120f, 120f));
            var circle = go.AddComponent<PlayerCircle>();
            circle.group = go.GetComponent<CanvasGroup>();
            circle.group.blocksRaycasts = false;
            circle.body = rect;

            var center = new Vector2(0.5f, 0.5f);
            circle.holdGauge = NewImage("HoldGauge", rect, CharacterSelectArt.Load(CharacterSelectArt.Ring), color);
            circle.holdGauge.type = Image.Type.Filled;
            circle.holdGauge.fillMethod = Image.FillMethod.Radial360;
            circle.holdGauge.fillOrigin = (int)Image.Origin360.Top;
            circle.holdGauge.fillClockwise = true;
            circle.holdGauge.fillAmount = 0f;
            Place(circle.holdGauge.rectTransform, center, Vector2.zero, new Vector2(144f, 144f));
            circle.ring = NewImage("Ring", rect, CharacterSelectArt.Load(CharacterSelectArt.Ring), circle.ringColor);
            Place(circle.ring.rectTransform, center, Vector2.zero, new Vector2(124f, 124f));
            circle.background = NewImage("Back", rect, CharacterSelectArt.Load(CharacterSelectArt.Circle), Color.white);
            Place(circle.background.rectTransform, center, Vector2.zero, new Vector2(106f, 106f));
            circle.icon = NewImage("Face", rect, CharacterSelectArt.Load(CharacterSelectArt.CharaCube), Color.white);
            circle.icon.preserveAspect = true;
            Place(circle.icon.rectTransform, center, Vector2.zero, new Vector2(76f, 76f));
            circle.letter = NewText("Letter", rect, "?", 54, Color.white, FontStyle.Bold);
            AddOutline(circle.letter, Shadow, 2f);
            Place(circle.letter.rectTransform, center, Vector2.zero, new Vector2(106f, 106f));
            circle.letter.enabled = false;

            circle.label = BuildLabel(rect, seat, new Vector2(0f, seat < 2 ? 74f : -74f));
            circle.burst = NewBurst("Burst", rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 26f), new Vector2(150f, 420f), 32);
            return circle;
        }

        private static PlayerTag BuildLabel(RectTransform parent, int seat, Vector2 position)
        {
            var color = FortressColors.SeatColor(seat);
            var rect = NewRect($"Label {seat + 1}P", parent);
            Place(rect, new Vector2(0.5f, 0.5f), position, new Vector2(78f, 36f));
            var tag = rect.gameObject.AddComponent<PlayerTag>();
            tag.background = NewImage("Back", rect, CharacterSelectArt.Load(CharacterSelectArt.Pill), color);
            tag.background.type = Image.Type.Sliced;
            Stretch(tag.background.rectTransform);
            tag.label = NewText("Label", rect, $"{seat + 1}P", 22, Color.white, FontStyle.Bold);
            Stretch(tag.label.rectTransform);
            AddOutline(tag.label, Shadow, 1.5f);
            var mark = NewImage("Decided", rect, CharacterSelectArt.Load(CharacterSelectArt.Check), Mint);
            Place(mark.rectTransform, new Vector2(1f, 1f), new Vector2(-2f, -2f), new Vector2(24f, 24f));
            tag.decidedMark = mark.gameObject;
            mark.gameObject.SetActive(false);
            return tag;
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
