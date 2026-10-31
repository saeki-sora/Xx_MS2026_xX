using System.Collections.Generic;
using System.Linq;
using MS2026.ShaderFX;
using MS2026.ShaderFX.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// メニュー「Tools/タイトル/タイトルシーンを組み立てる」。Title.unity に仮のタイトル画面一式を置く。
    /// 2回目以降は「[Title]」を作り直す。差し替えた画像とキャラのモデルは必ず引き継ぎ、調整した数値・位置は
    /// 「数値を引き継いで作り直す」を選べば引き継ぐ（<see cref="TitleValueSnapshot"/>）。素材・マテリアル・ShaderFXの設定は、既にあれば上書きしない。
    /// </summary>
    internal static class TitleSceneBuilder
    {
        private const string MenuPath = "Tools/タイトル/タイトルシーンを組み立てる";
        internal const string ScenePath = "Assets/Scenes/Title.unity";
        private const string Root = "Assets/Title";
        private const string SpriteDir = Root + "/Sprites";
        internal const string MaterialDir = Root + "/Materials";
        private const string EffectDir = Root + "/Effects";
        internal const string RootName = "[Title]";
        private const string ModelName = "Model";
        // キャラの立ち絵（2026-10-22: 左 = 赤、右 = 黄色）。無ければ仮のキューブ。
        // 大きさ = 絵の高さ（画面の高さは 10.8）、ずれ = キャラの位置から絵の真ん中まで（足元が画面の下近く、ロゴにかからない所）。
        // 2026-10-22〜: どのキャラがどの大きさ・位置かは、キャラの一覧（TitleCharacters.asset）で決める。
        // シーンに置くのは一覧の1人目（左）と2人目（右）。再生すると TitleScreenController が毎回ランダムに入れ替える。
        internal const string RosterPath = Root + "/Resources/" + TitleCharacterRoster.ResourcePath + ".asset";
        private const int CharacterSortingOrder = 10;
        // 今の作りの目印: どろーっと垂れる歯磨き粉（Raw Image の「FadeCanvas/PasteCurtain」）がある。
        private static bool IsCurrentLayout(Transform root)
        {
            var curtain = root.Find("FadeCanvas/PasteCurtain");
            return curtain != null && curtain.GetComponent<RawImage>() != null && root.Find("Opening/PasteStream") != null;
        }

        // カメラに映る高さ = 10.8（1920x1080 の画像を 100px=1 で並べたときにぴったり）。
        private const float OrthoSize = 5.4f;

        [MenuItem(MenuPath)]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("タイトル", "再生中は組み立てられません。再生を止めてから実行してください。", "OK");
                return;
            }

            if (!OpenTitleScene()) return;
            var scene = SceneManager.GetActiveScene();

            var oldRoot = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            var keptSprites = new Dictionary<string, Sprite>();
            var keptModels = new Dictionary<string, GameObject>();
            var keptTextures = new Dictionary<string, Texture2D>();
            TitleValueSnapshot keptValues = null;
            if (oldRoot != null)
            {
                // 古い作りのシーンは配置が違うので、数値は引き継がずに作り直す。
                // （作りを大きく変えたら IsCurrentLayout の目印も変える）
                var oldLayout = !IsCurrentLayout(oldRoot.transform);
                if (oldLayout)
                {
                    if (!EditorUtility.DisplayDialog("タイトル",
                            $"シーンの「{RootName}」は古い作りです。新しい作りで作り直しますか？\n（差し替えた画像は引き継ぎます。位置や動きの数値は新しい初期値になります）",
                            "作り直す", "やめる"))
                    {
                        return;
                    }
                }
                else
                {
                    var choice = EditorUtility.DisplayDialogComplex("タイトル",
                        $"シーンに「{RootName}」が既にあります。作り直しますか？\n\n" +
                        "・数値を引き継いで作り直す … 調整した位置・大きさ・タイミング・揺れなどをそのまま残す\n" +
                        "・初期値で作り直す … 位置や動きの数値をすべて最初の値に戻す\n\n" +
                        "（どちらでも、差し替えた画像とキャラのモデルは引き継ぎます）",
                        "数値を引き継いで作り直す", "やめる", "初期値で作り直す");
                    if (choice == 1) return;
                    if (choice == 0) keptValues = TitleValueSnapshot.Capture(oldRoot.transform, includeTransforms: true);
                }
                foreach (var sr in oldRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sprite != null && sr.transform.parent != null) keptSprites[SpriteKey(sr.transform)] = sr.sprite;
                }
                foreach (var raw in oldRoot.GetComponentsInChildren<RawImage>(true))
                {
                    if (raw.texture is Texture2D tex) keptTextures[raw.name] = tex;
                }
                // キャラの子「Model」（差し替えたモデル）は、作り直す前に外して取っておく。
                foreach (var model in oldRoot.GetComponentsInChildren<Transform>(true)
                             .Where(t => t.name == ModelName && t.parent != null && t.parent.name.StartsWith("Character")).ToArray())
                {
                    keptModels[model.parent.name] = model.gameObject;
                    Undo.SetTransformParent(model, null, "タイトルシーンを組み立てる");
                }
                Undo.DestroyObjectImmediate(oldRoot);
            }

            ConfigureSpriteImports();
            var fxMaterial = GetOrCreateMaterial(MaterialDir + "/Title_SpriteFX.mat", "ShaderFX/Uber Sprite", null);
            var bubbleMaterial = GetOrCreateMaterial(MaterialDir + "/Title_Bubble.mat",
                "Universal Render Pipeline/2D/Sprite-Unlit-Default", AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir + "/Title_SoftDot.png"));
            var germMaterial = GetOrCreateMaterial(MaterialDir + "/Title_Germ.mat",
                "Universal Render Pipeline/2D/Sprite-Unlit-Default", AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir + "/Title_Germ.png"));
            // 世界観（電脳世界のクリーニング）: 水色・マゼンタの色ずれ、グリッチ、白いキラキラ。
            // グリッチの量は 0 にしておき、Title Element Motion が必要なときだけ上げる。
            var logoProfile = GetOrCreateProfile(EffectDir + "/TitleFX_Logo.asset", p =>
            {
                p.modules.Add(new DissolveModule { edgeWidth = 0.12f, edgeColor = new Color(0.3f, 0.95f, 1f, 1f), noiseScale = 6f });
                p.modules.Add(new GlitchModule { amount = 0f, blockSize = 14f, speed = 18f, rgbSplit = 0.03f });
                p.modules.Add(new HitFlashModule { color = Color.white });
            });
            var characterProfile = GetOrCreateCharacterProfile();

            var cubeLeftMaterial = GetOrCreateLitMaterial(MaterialDir + "/Title_CubeLeft.mat", new Color(1f, 0.75f, 0.93f, 1f));
            var cubeRightMaterial = GetOrCreateLitMaterial(MaterialDir + "/Title_CubeRight.mat", new Color(0.7f, 0.93f, 1f, 1f));

            var camera = SetupCamera();
            var cameraMotion = camera != null ? camera.GetComponent<TitleCameraMotion>() : null;

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "タイトルシーンを組み立てる");
            var controller = root.AddComponent<TitleScreenController>();
            var fader = CreateFader(root.transform);

            // --- 背景: 最初から、ゆっくり引きながら現れ、ごくゆっくり揺れ続ける
            var background = CreateElement(root.transform, "Background", new Vector3(0f, 0f, 5f),
                Pick(keptSprites, "Background", "Title_Background.png"), 0, TitleSpriteFit.FitMode.CoverCamera, 0f, null, null);
            background.enterDuration = 3f;
            background.enterEase = TitleEaseType.OutCubic;
            background.fadeIn = true;
            background.scaleFrom = 1.12f;
            background.floatAmplitude = new Vector3(0.08f, 0.05f, 0f);
            background.floatSpeed = 0.08f;
            background.pulseAmount = 0.01f;
            background.pulseSpeed = 0.12f;

            // 塗り替わる前の、紫でどんよりした背景（明るい背景の手前に重ねる。歯磨き粉が画面を覆いきった瞬間に消える）。
            var backgroundBefore = new GameObject("SpriteBefore");
            backgroundBefore.transform.SetParent(background.transform, false);
            var beforeRenderer = backgroundBefore.AddComponent<SpriteRenderer>();
            beforeRenderer.sprite = Pick(keptSprites, "Background/SpriteBefore", "Title_BackgroundBefore.png");
            beforeRenderer.sortingOrder = 1;
            var beforeFit = backgroundBefore.AddComponent<TitleSpriteFit>();
            beforeFit.mode = TitleSpriteFit.FitMode.CoverCamera;
            beforeFit.Fit();

            // --- 泡・キラキラ（下から湧き上がる）と、ふわふわ漂うばいきん（背景とキャラの間）
            var bubbles = CreateBubbles(root.transform, bubbleMaterial);
            var germs = CreateGerms(root.transform, germMaterial);

            // --- オープニング: 上向きの歯磨き粉の真ん中をつまむ → ふくらむ（カメラが寄ってブルブル）
            //     → キャップから上へ噴き出す → 上から歯磨き粉の幕が落ちてきて画面が塗り替わる
            var curtainTexture = keptTextures.TryGetValue("PasteCurtain", out var keptCurtain) && keptCurtain != null
                ? keptCurtain
                : AssetDatabase.LoadAssetAtPath<Texture2D>(SpriteDir + "/Title_PasteCurtain.png");
            var opening = CreateOpening(root.transform, keptSprites, bubbleMaterial, fader, curtainTexture);
            opening.cameraMotion = cameraMotion;
            opening.beforeObjects = new[] { backgroundBefore };
            opening.afterObjects = new[] { bubbles.gameObject }; // 泡は塗り替わった後のきれいな世界だけ

            // ここから下は、幕が抜け始めた後の登場（Enter Delay は「オープニングの合図」から数える）。

            // --- ロゴ: 上から落ちてきて真ん中上に「ドン」（白フラッシュ・揺れ・むにっ）。たまにキラッ・ザザッ
            var logo = CreateElement(root.transform, "Logo", new Vector3(0f, 2.4f, 0f),
                Pick(keptSprites, "Logo", "Title_Logo.png"), 20, TitleSpriteFit.FitMode.Height, 3.6f, fxMaterial, logoProfile);
            logo.enterDelay = 0f;
            logo.enterDuration = 0.55f;
            logo.enterEase = TitleEaseType.InCubic;
            logo.fadeIn = false;
            logo.moveFrom = new Vector3(0f, 7f, 0f);
            logo.glitchIn = 0.6f;
            logo.flashOnLand = 1f;
            logo.shakeOnLand = 0.3f;
            logo.squashOnLand = 0.18f;
            logo.pulseAmount = 0.012f;
            logo.pulseSpeed = 0.6f;
            logo.glintInterval = 3.5f;
            logo.glintStrength = 0.45f;
            logo.glitchInterval = 5f;
            logo.glitchStrength = 0.6f;
            logo.flashOnExit = 0.8f;
            logo.glitchOnExit = 1f;

            // --- キャラ: 左下・右下に落ちてきて「ドン」（画面が揺れる）。立ち絵（左 = 赤、右 = 黄色。2026-10-22）があればそれ、
            //     無ければ仮のキューブ（回転しながら落ちる）。立ち絵は平らなので、横に傾くだけにする。
            var leftEntry = DefaultCharacter(false, keptSprites);
            var leftPortrait = leftEntry?.sprite;
            var characterLeft = CreateCharacterElement(root.transform, "CharacterLeft", new Vector3(-6.2f, -2.6f, 0f), 35f,
                cubeLeftMaterial, characterProfile, keptModels, leftEntry, fxMaterial, false, out var leftWasCube);
            SetupFallingCharacter(characterLeft, 0.45f, leftPortrait != null ? new Vector3(0f, 0f, 25f) : new Vector3(90f, 200f, 60f));
            var rightEntry = DefaultCharacter(true, keptSprites);
            var rightPortrait = rightEntry?.sprite;
            var characterRight = CreateCharacterElement(root.transform, "CharacterRight", new Vector3(6.2f, -2.6f, 0f), -35f,
                cubeRightMaterial, characterProfile, keptModels, rightEntry, fxMaterial, true, out var rightWasCube);
            SetupFallingCharacter(characterRight, 0.7f, rightPortrait != null ? new Vector3(0f, 0f, -25f) : new Vector3(90f, -200f, -60f));

            // --- 「〇〇でスタート！」: 真ん中より少し下に弾んで現れ、ゆっくり点滅。スタートでノイズを走らせ、速く点滅して消える
            var pressStart = CreateElement(root.transform, "PressStart", new Vector3(0f, -1.5f, 0f),
                Pick(keptSprites, "PressStart", "Title_PressStart.png"), 30, TitleSpriteFit.FitMode.Height, 1.1f, fxMaterial, logoProfile);
            pressStart.enterDelay = 1.5f;
            pressStart.enterDuration = 0.5f;
            pressStart.enterEase = TitleEaseType.OutBack;
            pressStart.scaleFrom = 0.6f;
            pressStart.pulseAmount = 0.03f;
            pressStart.pulseSpeed = 0.8f;
            pressStart.blinkMinAlpha = 0.35f;
            pressStart.blinkSpeed = 0.8f;
            pressStart.exitStyle = TitleElementMotion.ExitStyle.BlinkOut;
            pressStart.exitDuration = 0.6f;
            pressStart.glitchOnExit = 0.8f;

            controller.opening = opening;
            controller.background = background;
            controller.afterOpening = new[] { logo, characterLeft, characterRight, pressStart };
            controller.pressStart = pressStart;
            controller.ambientParticles = new[] { bubbles, germs };
            controller.characters = AssetDatabase.LoadAssetAtPath<TitleCharacterRoster>(RosterPath);
            controller.characterLeft = characterLeft;
            controller.characterRight = characterRight;
            controller.fader = fader;
            controller.cameraMotion = cameraMotion;

            // 引き継げなかった古いモデル（キャラの名前を変えた場合など）は消す。
            foreach (var leftover in keptModels.Values)
            {
                if (leftover != null) Undo.DestroyObjectImmediate(leftover);
            }

            // 配置物ごとの色の調整（「数値を引き継ぐ」なら、この後で調整した値に戻る）。
            TitleColorAdjustSetup.Ensure(root.transform, false);

            if (keptValues != null)
            {
                var applied = keptValues.Apply(root.transform, "タイトルシーンを組み立てる");
                Debug.Log($"[Title] 調整した数値・位置を引き継ぎました（{applied} か所）。");
                // キューブから立ち絵に替えたときは、キューブ用の「回転しながら落ちる」を引き継がない（平らな絵が裏返って見えるため）。
                if (leftWasCube) characterLeft.rotateFrom = new Vector3(0f, 0f, 25f);
                if (rightWasCube) characterRight.rotateFrom = new Vector3(0f, 0f, -25f);
            }

            AddSceneToBuildList();
            EnsureGripReader(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Title] タイトルシーンを組み立てました。再生して A キー（または握力センサーを握る）でゲームのシーンへ進みます。画像は各オブジェクトの子「Sprite」の Sprite を差し替えてください。");
        }

        // 握力センサーの読み取り（ArduinoSerialReader）。Assembly-CSharp にあって参照できないので、型の名前で探す。
        private const string GripReaderType = "ArduinoSerialReader, Assembly-CSharp";
        private const string GripReaderObjectName = "Arduino";
        // ロビー・ゲームのシーンと同じポート（PCごとに違っても、読み取りが自動で別のポートもさがす）。
        private const string GripReaderPort = "COM5";

        /// <summary>
        /// タイトルのシーンに握力センサーの読み取りを置く（無ければ作る。あればポートなどの設定はそのままで「シーンをまたぐ」だけONにする）。
        /// タイトルで握ってスタートするため。シーンをまたいで残るので、ロビー・キャラ選択・ゲームでもポートを開け直さずに使い続ける。
        /// [Title] の外（シーンの一番上）に置くので、作り直しても消えない。
        /// タイトルシーンを組み立てるときにも行う。このメニューは、組み立て直さずにこれだけ行う。
        /// </summary>
        [MenuItem("Tools/タイトル/握力センサーの読み取りを置く")]
        private static void PlaceGripReaderOnly()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("タイトル", "再生中は置けません。再生を止めてから実行してください。", "OK");
                return;
            }

            if (!OpenTitleScene()) return;
            var scene = SceneManager.GetActiveScene();
            EnsureGripReader(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 無ければ作る。あればポートなどの設定はそのままで「シーンをまたぐ」だけONにする。
        private static void EnsureGripReader(Scene scene)
        {
            var type = System.Type.GetType(GripReaderType);
            if (type == null)
            {
                Debug.LogWarning("[Title] ArduinoSerialReader が見つからないため、握力センサーの読み取りをタイトルに置けませんでした（タイトルはキーでしかスタートできません）。");
                return;
            }

            Component reader = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                reader = go.GetComponentInChildren(type, true);
                if (reader != null) break;
            }

            var created = reader == null;
            if (created)
            {
                var go = new GameObject(GripReaderObjectName);
                Undo.RegisterCreatedObjectUndo(go, "タイトルシーンを組み立てる");
                reader = go.AddComponent(type);
            }

            var so = new SerializedObject(reader);
            if (created)
            {
                var port = so.FindProperty("portName");
                if (port != null) port.stringValue = GripReaderPort;
            }

            var keep = so.FindProperty("keepAcrossScenes");
            if (keep != null) keep.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(created
                ? $"[Title] 握力センサーの読み取り「{GripReaderObjectName}」を置きました（{GripReaderPort}。開けない・値が届かなければほかのポートも自動でさがします）。"
                : $"[Title] 握力センサーの読み取り「{reader.name}」の「シーンをまたぐ」をONにしました。");
        }

        internal static bool OpenTitleScene()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return SceneManager.GetActiveScene().path == ScenePath;
        }

        private static void ConfigureSpriteImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        // 差し替えた絵を引き継ぐときの名前。子「Sprite」は親の名前、それ以外は「親/自分」。
        private static string SpriteKey(Transform t) =>
            t.name == "Sprite" ? t.parent.name : t.parent.name + "/" + t.name;

        private static Sprite Pick(Dictionary<string, Sprite> kept, string elementName, string defaultFile)
        {
            return kept.TryGetValue(elementName, out var sprite)
                ? sprite
                : AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + defaultFile);
        }

        private static Material GetOrCreateMaterial(string path, string shaderName, Texture texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning($"[Title] シェーダー「{shaderName}」が見つかりません。{path} は作りません。");
                return null;
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var material = new Material(shader);
            if (texture != null) material.mainTexture = texture;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static EffectProfile GetOrCreateProfile(string path, System.Action<EffectProfile> fill)
        {
            var existing = AssetDatabase.LoadAssetAtPath<EffectProfile>(path);
            if (existing != null) return existing;

            EnsureFolder(EffectDir);
            var profile = ScriptableObject.CreateInstance<EffectProfile>();
            fill(profile);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        private static Camera SetupCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[Title] Main Camera が見つかりません。カメラの設定を飛ばします。");
                return null;
            }

            Undo.RecordObject(camera, "タイトルのカメラ設定");
            Undo.RecordObject(camera.transform, "タイトルのカメラ設定");
            camera.orthographic = true;
            camera.orthographicSize = OrthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            if (camera.GetComponent<TitleCameraMotion>() == null) Undo.AddComponent<TitleCameraMotion>(camera.gameObject);
            return camera;
        }

        private static TitleElementMotion CreateElement(Transform parent, string name, Vector3 position, Sprite sprite,
            int sortingOrder, TitleSpriteFit.FitMode fitMode, float fitSize, Material fxMaterial, EffectProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var motion = go.AddComponent<TitleElementMotion>();

            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            var fit = spriteGo.AddComponent<TitleSpriteFit>();
            fit.mode = fitMode;
            if (fitSize > 0f) fit.size = fitSize;
            fit.Fit();

            if (fxMaterial != null && profile != null)
            {
                sr.sharedMaterial = fxMaterial;
                var target = spriteGo.AddComponent<EffectTarget>();
                var so = new SerializedObject(target);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.FindProperty("targetRenderer").objectReferenceValue = sr;
                so.FindProperty("originalMaterial").objectReferenceValue = fxMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return motion;
        }

        private static TitleOpening CreateOpening(Transform parent, Dictionary<string, Sprite> keptSprites, Material burstMaterial, TitleScreenFader fader,
            Texture2D curtainTexture)
        {
            var go = new GameObject("Opening");
            go.transform.SetParent(parent, false);
            var opening = go.AddComponent<TitleOpening>();

            // 歯磨き粉（上向き。キャップが上）。高さ 8（画面の高さ 10.8 の約 3/4）、少し下寄りに置く。
            var tube = CreateSpriteChild(go.transform, "Toothpaste", new Vector3(0f, -1.4f, 0f),
                Pick(keptSprites, "Toothpaste", "Title_Toothpaste.png"), 15, TitleSpriteFit.FitMode.Height, 8f);
            opening.tube = tube;

            // キャップ: チューブの子（一緒に出てきて震える）。噴き出す瞬間に外れて吹き飛ぶ。
            // 位置は元の絵でキャップがあった所（チューブの中心から上へ 545px × 8/1300 = 3.35）。大きさも同じ倍率（170px → 1.05）。
            var cap = CreateSpriteChild(tube, "Cap", new Vector3(0f, 3.35f, 0f),
                Pick(keptSprites, "Cap", "Title_ToothpasteCap.png"), 18, TitleSpriteFit.FitMode.Height, 1.05f);
            opening.cap = cap;

            // つまむ指は置かない（ユーザー指定 2026-10-07）。つまんだ形（真ん中が細くなる）は歯磨き粉自身の変形で見せる。
            // 指などを置きたくなったら、子に置いて Title Opening の Pinchers に入れれば真ん中へ寄っていく。
            opening.pinchers = System.Array.Empty<Transform>();

            // 噴き出す歯磨き粉: 根元（親の位置）から上へ伸びる。噴き出す瞬間にキャップの位置へ動く。
            var stream = new GameObject("PasteStream");
            stream.transform.SetParent(go.transform, false);
            stream.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            var streamSprite = Pick(keptSprites, "PasteStream", "Title_PasteStream.png");
            var streamGo = new GameObject("Sprite");
            streamGo.transform.SetParent(stream.transform, false);
            var streamRenderer = streamGo.AddComponent<SpriteRenderer>();
            streamRenderer.sprite = streamSprite;
            streamRenderer.sortingOrder = 16;
            // 幅 約1.4、長さ 約11（画面の上まで届く）。根元が親の位置に来るよう、絵の半分だけ上にずらす。
            streamGo.transform.localScale = new Vector3(0.55f, 0.9f, 1f);
            var streamHeight = streamSprite != null ? streamSprite.bounds.size.y * 0.9f : 11f;
            streamGo.transform.localPosition = new Vector3(0f, streamHeight * 0.5f, 0f);
            opening.stream = stream.transform;

            opening.burstParticles = CreateBurst(go.transform, burstMaterial);
            opening.curtain = CreateCurtain(fader.transform.parent, curtainTexture);

            // AE などの連番を取り込み済みなら、それを使う（書き出したときと同じ位置・大きさ。「数値を引き継ぐ」なら後で調整した位置に戻る）。
            var sequence = AssetDatabase.LoadAssetAtPath<TitleFrameSequence>(TitleFrameSequenceImporter.SequencePath);
            if (sequence != null && sequence.IsUsable) TitleFrameSequenceImporter.ApplyToOpening(opening, sequence, placeLikeSource: true, recordUndo: false);
            return opening;
        }

        private static Transform CreateSpriteChild(Transform parent, string name, Vector3 position, Sprite sprite, int sortingOrder,
            TitleSpriteFit.FitMode fitMode, float fitSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(go.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            var fit = spriteGo.AddComponent<TitleSpriteFit>();
            fit.mode = fitMode;
            fit.size = fitSize;
            fit.Fit();
            return go.transform;
        }

        // 画面を塗りつぶす歯磨き粉。画面の一番手前の UI（暗転用の板の後ろ）に、画面いっぱいの枠として置く。
        // 絵は Raw Image の Texture。再生中に TitleOpening がこの絵を縦の列に分けて、上からどろーっと垂らす（この Raw Image 自体は隠す）。
        private static RectTransform CreateCurtain(Transform canvas, Texture2D texture)
        {
            var go = new GameObject("PasteCurtain", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            go.transform.SetSiblingIndex(0);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false; // 編集中は画面を隠さない
            return rect;
        }

        // 噴き出したときにキャップから上へ飛び散る泡（1回だけ）。
        private static ParticleSystem CreateBurst(Transform parent, Material material)
        {
            var ps = CreateParticleObject(parent, "BurstSplash", new Vector3(0f, 2f, -1f), material, 40);

            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 1f), new Color(0.6f, 0.95f, 1f, 1f));
            main.gravityModifier = 1.5f;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)80), new ParticleSystem.Burst(0.06f, (short)40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.5f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.8f, 0.95f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            return ps;
        }

        // キャラ1人分。差し替えた 3D モデル（子「Model」）があればそのまま、無ければ立ち絵（子「Sprite」）、立ち絵も無ければ仮のキューブ。
        // replacedCube = 前の仮のキューブを立ち絵に替えた。
        private static TitleElementMotion CreateCharacterElement(Transform parent, string name, Vector3 position, float yaw,
            Material material, EffectProfile profile, Dictionary<string, GameObject> keptModels,
            TitleCharacterRoster.Entry entry, Material fxMaterial, bool rightSide, out bool replacedCube)
        {
            var portrait = entry?.sprite;
            replacedCube = false;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var motion = go.AddComponent<TitleElementMotion>();

            keptModels.TryGetValue(name, out var kept);
            if (kept != null && !(portrait != null && IsPlaceholderCube(kept)))
            {
                Undo.SetTransformParent(kept.transform, go.transform, "タイトルシーンを組み立てる");
                kept.transform.localPosition = Vector3.zero;
                keptModels.Remove(name);
                return motion;
            }

            if (portrait != null)
            {
                // 仮のキューブは、最後に「引き継げなかったモデル」として消える。
                replacedCube = kept != null;
                CreatePortraitChild(go.transform, portrait, fxMaterial, profile, entry.Side(rightSide).height, TitleCharacterRoster.LocalPosition(entry, rightSide));
                return motion;
            }

            // 仮のキャラ: 少し斜めから見たキューブ（あとで子「Model」ごと差し替える）。
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = ModelName;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = Vector3.one * 2.3f;
            cube.transform.localRotation = Quaternion.Euler(-20f, yaw, 0f);
            var mr = cube.GetComponent<MeshRenderer>();
            if (material != null) mr.sharedMaterial = material;

            if (material != null && profile != null)
            {
                var target = cube.AddComponent<EffectTarget>();
                var so = new SerializedObject(target);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.FindProperty("targetRenderer").objectReferenceValue = mr;
                so.FindProperty("originalMaterial").objectReferenceValue = material;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return motion;
        }

        // キャラの立ち絵（子「Sprite」）。白フラッシュ・グリッチ・輪郭の光は ShaderFX（キューブと同じ設定）。
        private static GameObject CreatePortraitChild(Transform parent, Sprite portrait, Material fxMaterial, EffectProfile profile,
            float size, Vector3 offset)
        {
            var spriteGo = new GameObject("Sprite");
            spriteGo.transform.SetParent(parent, false);
            spriteGo.transform.localPosition = offset;
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = portrait;
            sr.sortingOrder = CharacterSortingOrder;
            var fit = spriteGo.AddComponent<TitleSpriteFit>();
            fit.mode = TitleSpriteFit.FitMode.Height;
            fit.size = size;
            fit.Fit();
            if (fxMaterial != null && profile != null)
            {
                sr.sharedMaterial = fxMaterial;
                var target = spriteGo.AddComponent<EffectTarget>();
                var so = new SerializedObject(target);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.FindProperty("targetRenderer").objectReferenceValue = sr;
                so.FindProperty("originalMaterial").objectReferenceValue = fxMaterial;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return spriteGo;
        }

        private static EffectProfile GetOrCreateCharacterProfile() =>
            GetOrCreateProfile(EffectDir + "/TitleFX_Character.asset", p =>
            {
                p.modules.Add(new RimLightModule { color = new Color(0.35f, 0.95f, 1f, 1f), power = 3f, intensity = 1.2f });
                p.modules.Add(new GlitchModule { amount = 0f, blockSize = 10f, speed = 16f, rgbSplit = 0.025f });
                p.modules.Add(new HitFlashModule { color = Color.white });
            });

        /// <summary>
        /// メニュー「Tools/タイトル/キャラを立ち絵にする」。組み立て直さずに、タイトルのシーンの仮のキューブだけを立ち絵に替える
        /// （位置・動きの数値はそのまま。落ちるときの回転だけ立ち絵用の「少し傾く」にする）。差し替えた 3D モデルはそのまま。
        /// </summary>
        [MenuItem("Tools/タイトル/キャラを立ち絵にする")]
        private static void SwapCubesToPortraitsMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("タイトル", "再生中は替えられません。再生を止めてから実行してください。", "OK");
                return;
            }

            var swapped = SwapCubesToPortraitsInTitleScene();
            EditorUtility.DisplayDialog("タイトル", swapped > 0
                ? $"仮のキューブ {swapped} 個を立ち絵に替えて、タイトルのシーンを保存しました。"
                : $"替えるキューブはありませんでした（もう立ち絵か、キャラの一覧 {RosterPath} に立ち絵が入っていません）。", "OK");
        }

        /// <summary>
        /// Title.unity の仮のキューブを立ち絵に替えて保存する。開いていなければ裏で開いて閉じる。替えた数を返す。
        /// タイトルのシーンを開いていて、保存していない変更があるときは保存せずに、保存するようログで知らせる。
        /// </summary>
        internal static int SwapCubesToPortraitsInTitleScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var wasOpen = scene.IsValid() && scene.isLoaded;
            var hadChanges = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var swapped = 0;
            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
                if (root == null) return 0;
                ConfigureSpriteImports();
                var fxMaterial = GetOrCreateMaterial(MaterialDir + "/Title_SpriteFX.mat", "ShaderFX/Uber Sprite", null);
                var profile = GetOrCreateCharacterProfile();
                var empty = new Dictionary<string, Sprite>();
                if (SwapCubeToPortrait(root.transform, "CharacterLeft", DefaultCharacter(false, empty), false, 25f, fxMaterial, profile)) swapped++;
                if (SwapCubeToPortrait(root.transform, "CharacterRight", DefaultCharacter(true, empty), true, -25f, fxMaterial, profile)) swapped++;
                if (swapped > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (hadChanges)
                    {
                        Debug.LogWarning("[Title] キャラを立ち絵に替えました。タイトルのシーンに保存していない変更があったので、保存はしていません。シーンを保存してください。");
                    }
                    else
                    {
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"[Title] 仮のキューブ {swapped} 個を立ち絵に替えて、タイトルのシーンを保存しました。");
                    }
                }
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
            return swapped;
        }

        private static bool SwapCubeToPortrait(Transform root, string name, TitleCharacterRoster.Entry entry, bool rightSide, float tilt,
            Material fxMaterial, EffectProfile profile)
        {
            var character = root.Find(name);
            var model = character != null ? character.Find(ModelName) : null;
            if (model == null || !IsPlaceholderCube(model.gameObject) || character.Find("Sprite") != null) return false;
            if (entry == null || entry.sprite == null) return false;

            Undo.DestroyObjectImmediate(model.gameObject);
            var spriteGo = CreatePortraitChild(character, entry.sprite, fxMaterial, profile, entry.Side(rightSide).height, TitleCharacterRoster.LocalPosition(entry, rightSide));
            Undo.RegisterCreatedObjectUndo(spriteGo, "キャラを立ち絵にする");
            var motion = character.GetComponent<TitleElementMotion>();
            if (motion != null)
            {
                Undo.RecordObject(motion, "キャラを立ち絵にする");
                motion.rotateFrom = new Vector3(0f, 0f, tilt);
            }
            return true;
        }

        // シーンに置いておくキャラ（左 = 一覧で立ち絵のある1人目、右 = 2人目）。前のシーンで手で差し替えた絵があれば、その絵
        // （一覧に同じ絵があればその大きさ・位置、無ければ一覧の1人目・2人目の大きさ・位置）。どちらも無ければ null（仮のキューブ）。
        private static TitleCharacterRoster.Entry DefaultCharacter(bool rightSide, Dictionary<string, Sprite> keptSprites)
        {
            var roster = AssetDatabase.LoadAssetAtPath<TitleCharacterRoster>(RosterPath);
            var usable = roster != null ? roster.UsableIndices() : System.Array.Empty<int>();
            var index = rightSide ? 1 : 0;
            var fromRoster = usable.Length > index ? roster.characters[usable[index]] : null;
            if (!keptSprites.TryGetValue(rightSide ? "CharacterRight" : "CharacterLeft", out var kept) || kept == null) return fromRoster;

            var same = usable.Select(i => roster.characters[i]).FirstOrDefault(e => e.sprite == kept);
            if (same != null) return same;
            return new TitleCharacterRoster.Entry
            {
                label = kept.name,
                sprite = kept,
                left = fromRoster?.Side(false).Copy() ?? new TitleCharacterRoster.Placement(),
                right = fromRoster?.Side(true).Copy() ?? new TitleCharacterRoster.Placement(),
            };
        }

        // 組み立てが置いた仮のキューブか（キューブの形で、仮キューブの材質）。差し替えた 3D モデルは false。
        private static bool IsPlaceholderCube(GameObject model)
        {
            var filter = model.GetComponent<MeshFilter>();
            var renderer = model.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cube" || renderer == null) return false;
            var material = renderer.sharedMaterial;
            return material == null || material.name.StartsWith("Title_Cube");
        }

        private static void SetupFallingCharacter(TitleElementMotion motion, float delay, Vector3 tumble)
        {
            motion.enterDelay = delay;
            motion.enterDuration = 0.5f;
            motion.enterEase = TitleEaseType.InCubic;
            motion.fadeIn = false;
            motion.moveFrom = new Vector3(0f, 10f, 0f);
            motion.rotateFrom = tumble;
            motion.glitchIn = 0.5f;
            motion.flashOnLand = 0.6f;
            motion.shakeOnLand = 0.35f;
            motion.squashOnLand = 0.22f;
            motion.floatAmplitude = new Vector3(0f, 0.08f, 0f);
            motion.floatSpeed = 0.4f;
            motion.flashOnExit = 0.6f;
            motion.glitchOnExit = 0.7f;
        }

        // 仮キューブ用の、光の当たる 3D マテリアル（ShaderFX の 3D 版。着地の白フラッシュ・グリッチが効く）。
        private static Material GetOrCreateLitMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = GetOrCreateMaterial(path, "ShaderFX/Uber", null);
            if (material == null) return null;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem CreateBubbles(Transform parent, Material material)
        {
            var ps = CreateParticleObject(parent, "Bubbles", new Vector3(0f, -6.2f, 3f), material, 5);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.97f, 1f, 1f), new Color(1f, 0.75f, 0.95f, 1f));
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 0.5f, 0.5f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // 上向きに飛ばす

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.3f;

            SetFade(ps, Color.white, 0.8f);
            return ps;
        }

        private static ParticleSystem CreateGerms(Transform parent, Material material)
        {
            var ps = CreateParticleObject(parent, "Germs", new Vector3(0f, 2.6f, 4f), material, 4);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.75f, 1f, 0.75f));
            main.maxParticles = 20;

            var emission = ps.emission;
            emission.rateOverTime = 1.2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 5f, 0.1f);
            shape.randomDirectionAmount = 1f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.2f;

            SetFade(ps, Color.white, 1f);
            return ps;
        }

        private static ParticleSystem CreateParticleObject(Transform parent, string name, Vector3 position, Material material, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false; // 再生は Title Screen Controller が登場と同時に始める
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return ps;
        }

        // 生まれたときと消えるときを透明にして、ふっと現れてふっと消えるようにする。
        private static void SetFade(ParticleSystem ps, Color tint, float peakAlpha)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(peakAlpha, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
        }

        private static TitleScreenFader CreateFader(Transform parent)
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var fadeGo = new GameObject("Fade", typeof(RectTransform));
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)fadeGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = fadeGo.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            image.enabled = false; // 編集中は画面を隠さない。再生開始時に暗転から始まる。
            return fadeGo.AddComponent<TitleScreenFader>();
        }

        private static void AddSceneToBuildList()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            // ゲームの起動の流れ（通信テスト等）を変えないよう、末尾に足すだけにする。最初に出したいときは Build Profiles で並べ替える。
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Title] Build Profiles のシーン一覧の末尾に Title を追加しました。");
        }
    }
}
