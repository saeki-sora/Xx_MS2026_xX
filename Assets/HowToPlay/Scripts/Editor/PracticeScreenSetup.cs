using System.Collections.Generic;
using System.IO;
using MS2026.Fortress;
using MS2026.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MS2026.HowToPlay.EditorTools
{
    /// <summary>
    /// 練習の画面（レイアウトB）の絵をそろえる（2026-11-12）。
    /// ・本番のステージ（StageCatalog の全部）を、コアを真ん中にして真上から撮り、フィールドの背景にする（コアと砲台は消して撮る）。
    /// ・本番のコアと泡（スマッシュボール）を、背景を透明にして撮る（ステージの背景からは泡を消して撮る）。
    /// ・画面で使う絵（ロゴ・手・ハンコ・敵など、プロジェクトにある絵）を設定に入れる。
    /// 撮った絵は Assets/HowToPlay/Art/Field/ に入る。開いているシーンは元に戻す（保存はしない）。
    /// </summary>
    public static class PracticeScreenSetup
    {
        private const string OutFolder = "Assets/HowToPlay/Art/Field";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string SettingsPath = "Assets/HowToPlay/Resources/HowToPlaySettings.asset";
        private const int PlatePixels = 2048;
        private const float PlateWorldSize = 44f;
        private const int CoreLayer = 31;

        [MenuItem("Tools/あそびかた/練習の画面の絵をそろえる（ステージとコアを撮る）")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("練習の画面", "再生中はできません。再生を止めてから押してください。", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var report = Bake();
            EditorUtility.DisplayDialog("練習の画面", report, "OK");
        }

        /// <summary>バッチモード用（-executeMethod MS2026.HowToPlay.EditorTools.PracticeScreenSetup.RunBatch）。</summary>
        public static void RunBatch()
        {
            var report = Bake();
            Debug.Log("[PracticeScreenSetup] " + report);
            EditorApplication.Exit(0);
        }

        private static string Bake()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var lines = new List<string>();
            Directory.CreateDirectory(OutFolder);
            var plates = new List<PracticeStagePlate>();
            string corePath = null;
            string bubblePath = null;
            try
            {
                var catalog = StageCatalog.Load();
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                var core = Object.FindAnyObjectByType<CoreCrystalController>();
                var center = core != null ? core.transform.position : Vector3.zero;
                corePath = core != null ? BakeIsolated(core.gameObject, $"{OutFolder}/PracticeField_Core.png", "コア", lines) : null;
                if (core == null)
                {
                    lines.Add("コア: Game のシーンに見つからない（設定の絵はそのまま）");
                }

                var stageCount = catalog != null ? catalog.Count : 0;
                for (var i = 0; i < stageCount; i++)
                {
                    var stage = catalog.Get(i);
                    if (stage == null || string.IsNullOrEmpty(stage.sceneName))
                    {
                        continue;
                    }

                    var path = FindScenePath(stage.sceneName);
                    if (path == null)
                    {
                        lines.Add($"ステージ {stage.sceneName}: シーンが見つからない");
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(scene);
                    if (bubblePath == null)
                    {
                        var ball = Object.FindAnyObjectByType<SmashBallModule>();
                        if (ball != null)
                        {
                            bubblePath = BakeIsolated(ball.gameObject, $"{OutFolder}/PracticeField_SmashBall.png", "泡（スマッシュボール）", lines);
                        }
                    }

                    var platePath = $"{OutFolder}/PracticeField_{stage.sceneName}.png";
                    var radius = TurretRadius(center);
                    BakePlate(center, platePath);
                    EditorSceneManager.CloseScene(scene, true);
                    plates.Add(new PracticeStagePlate { sceneName = stage.sceneName, plateWorldSize = PlateWorldSize, turretRadius = radius });
                    lines.Add($"ステージ {stage.sceneName}: 背景を撮った（砲台までの距離 {radius:0.0}m）");
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }

            AssetDatabase.Refresh();
            foreach (var plate in plates)
            {
                var path = $"{OutFolder}/PracticeField_{plate.sceneName}.png";
                ImportAsTexture(path, 2048);
                plate.plate = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            if (corePath != null)
            {
                ImportAsTexture(corePath, 512);
            }

            if (bubblePath != null)
            {
                ImportAsTexture(bubblePath, 512);
            }

            AssignSettings(plates, corePath, bubblePath, lines);
            AssetDatabase.SaveAssets();
            return string.Join("\n", lines);
        }

        // ───────── 撮る ─────────

        private static Camera MakeCamera()
        {
            var main = Camera.main;
            var go = new GameObject("[PracticeScreenSetup] Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
            }

            var data = go.GetComponent<UniversalAdditionalCameraData>() ?? go.AddComponent<UniversalAdditionalCameraData>();
            var mainData = main != null ? main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (mainData != null)
            {
                data.renderPostProcessing = mainData.renderPostProcessing;
                data.volumeLayerMask = mainData.volumeLayerMask;
                data.antialiasing = mainData.antialiasing;
            }

            cam.orthographic = true;
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.enabled = false;
            return cam;
        }

        private static void BakePlate(Vector3 center, string path)
        {
            var hidden = HideDynamic();
            var cam = MakeCamera();
            try
            {
                cam.transform.position = new Vector3(center.x, center.y, center.z - 60f);
                cam.orthographicSize = PlateWorldSize * 0.5f;
                cam.aspect = 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.55f, 0.7f, 1f);
                SaveRender(cam, PlatePixels, PlatePixels, path, false);
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
                foreach (var go in hidden)
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                    }
                }
            }
        }

        /// <summary>その物だけを、背景を透明にして正方形に撮る。</summary>
        private static string BakeIsolated(GameObject target, string path, string label, List<string> lines)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                lines.Add(label + ": 見た目が無い");
                return null;
            }

            var bounds = renderers[0].bounds;
            var layers = new Dictionary<GameObject, int>();
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
                layers[r.gameObject] = r.gameObject.layer;
                r.gameObject.layer = CoreLayer;
            }

            var cam = MakeCamera();
            try
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                cam.cullingMask = 1 << CoreLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.allowHDR = false;
                var size = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f;
                cam.orthographicSize = size;
                cam.aspect = 1f;
                cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 30f);
                SaveRender(cam, 512, 512, path, true);
                lines.Add($"{label}: 撮った（幅 {size * 2f:0.00}m）");
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
                foreach (var pair in layers)
                {
                    pair.Key.layer = pair.Value;
                }
            }

            return path;
        }

        private static List<GameObject> HideDynamic()
        {
            var hidden = new List<GameObject>();
            void Hide(Component c)
            {
                if (c != null && c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(false);
                    hidden.Add(c.gameObject);
                }
            }

            foreach (var t in Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Hide(t);
            }

            foreach (var c in Object.FindObjectsByType<CoreCrystalController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Hide(c);
            }

            // 泡（スマッシュボール）は練習の画面で別に出すので、背景からは消す。
            foreach (var b in Object.FindObjectsByType<SmashBallModule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Hide(b);
            }

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Hide(canvas);
                }
            }

            return hidden;
        }

        private static float TurretRadius(Vector3 center)
        {
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (turrets.Length == 0)
            {
                return 5.9f;
            }

            var sum = 0f;
            foreach (var t in turrets)
            {
                var d = t.transform.position - center;
                sum += new Vector2(d.x, d.y).magnitude;
            }

            var r = sum / turrets.Length;
            return r > 0.5f ? r : 5.9f;
        }

        private static void SaveRender(Camera cam, int width, int height, string path, bool alpha)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static string FindScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return null;
        }

        private static void ImportAsTexture(string path, int maxSize)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        // ───────── 設定に入れる ─────────

        private static void AssignSettings(List<PracticeStagePlate> plates, string corePath, string bubblePath, List<string> lines)
        {
            var settings = AssetDatabase.LoadAssetAtPath<HowToPlaySettings>(SettingsPath);
            if (settings == null)
            {
                lines.Add("設定（" + SettingsPath + "）が無いので、絵を入れられなかった");
                return;
            }

            Undo.RecordObject(settings, "練習の画面の絵");
            var screen = settings.screen ??= new PracticeScreenSettings();
            if (plates.Count > 0)
            {
                screen.stagePlates = plates.ToArray();
            }

            if (corePath != null)
            {
                screen.core = AssetDatabase.LoadAssetAtPath<Texture2D>(corePath);
            }

            T Load<T>(string path, T current) where T : Object
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    lines.Add("見つからない絵: " + path);
                }

                return asset != null ? asset : current;
            }

            screen.enemy = Load<Texture>("Assets/_Game/Fortress/Art/Enemies/ノーマルスピキ.png", screen.enemy);
            screen.bubble = bubblePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(bubblePath) : Load<Texture>("Assets/CharacterSelect/Art/CS_Bubble.png", screen.bubble);
            screen.background = Load<Texture>("Assets/_Game/UI/Art/Session/Session_Background.png", screen.background);
            screen.logo = Load<Texture>("Assets/Title/Sprites/Title_Logo.png", screen.logo);
            screen.halftone = Load<Texture>("Assets/CharacterSelect/Art/CS_Halftone.png", screen.halftone);
            screen.stripes = Load<Texture>("Assets/CharacterSelect/Art/CS_Stripes.png", screen.stripes);
            screen.hand = Load<Texture>("Assets/_Game/UI/Art/Hud/Brush_Hand.png", screen.hand);
            screen.stamp = Load<Texture>("Assets/_Game/UI/Art/Hud/Result_Stamp.png", screen.stamp);
            screen.sparkle = Load<Texture>("Assets/CharacterSelect/Art/CS_Sparkle.png", screen.sparkle);
            screen.arrow = Load<Texture>("Assets/CharacterSelect/Art/UI/other/charabutton_arrow.png", screen.arrow);
            screen.seatBadges = new Texture[4];
            for (var i = 0; i < 4; i++)
            {
                screen.seatBadges[i] = Load<Texture>($"Assets/CharacterSelect/Art/UI/other/charabutton_icon_{i + 1}P.png", null);
            }

            EditorUtility.SetDirty(settings);
            lines.Add("設定に絵を入れた: " + SettingsPath);
        }
    }
}
