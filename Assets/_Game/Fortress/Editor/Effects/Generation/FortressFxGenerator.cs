using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 仮の演出素材（パーティクルPrefab・効果音・BGM）をコードで生成して D-Drive の SourceAssets に置く。
    /// 見た目はコンセプトアート（水色・ピンク・紫・白、泡としぶき、4点のきらめき、0と1、RGBずれ）に寄せてある。
    /// 生成後は D-Drive が自動で VFX / SE / BGM として登録する。続けて「2 演出欄へ割り当て」を実行する。
    /// </summary>
    public static class FortressFxGenerator
    {
        public const string SourceRoot = "Assets/_Game/DDrive/SourceAssets";
        public const string VfxFolder = SourceRoot + "/Vfx/Fortress";
        public const string SeFolder = SourceRoot + "/Se/Fortress";
        public const string BgmFolder = SourceRoot + "/Bgm/Fortress";
        public const string WorkRoot = "Assets/_Game/Fortress/Effects/Generated";
        private const string TexFolder = WorkRoot + "/Textures";
        private const string MatFolder = WorkRoot + "/Materials";

        /// <summary>識別名（ファイル名 = D-DriveのID名の元）。</summary>
        public static readonly string[] VfxNames =
        {
            "LaserMuzzle", "LaserImpact", "SwarmHit", "DestructHit", "DestructStage", "DestructBreak", "DestructRegen", "SmashBreak",
        };

        public static readonly string[] SeNames =
        {
            "LaserMuzzle", "LaserImpact", "SwarmHit", "DestructHit", "DestructStage", "DestructBreak", "DestructRegen", "SmashBreak",
        };

        public const string BgmName = "CyberRun";

        private static readonly Color Cyan = new Color(0.35f, 0.92f, 1f, 1f);
        private static readonly Color Pink = new Color(1f, 0.45f, 0.82f, 1f);
        private static readonly Color Purple = new Color(0.62f, 0.45f, 1f, 1f);
        private static readonly Color White = new Color(1f, 1f, 1f, 1f);

        private enum Shape { Circle, Cone, Sphere }

        private sealed class Spec
        {
            public string name = "Layer";
            public Material mat;
            public bool loop;
            public float duration = 1f;
            public Vector2 life = new Vector2(0.4f, 0.8f);
            public Vector2 speed = new Vector2(1f, 3f);
            public Vector2 size = new Vector2(0.2f, 0.4f);
            public float rate;
            public int burst;
            public Color c0 = White;
            public Color c1 = White;
            public Shape shape = Shape.Circle;
            public float angle = 25f;
            public float radius = 0.1f;
            public bool facingUp;
            public float gravity;
            public bool randRot = true;
            public bool shrink;
            public bool grow;
            public Vector3 offset;
            public int cols = 1;
            public bool stretch;
            public float stretchLen = 2f;
            public float spin;
            public bool worldSpace = true;
            public int maxParticles = 100;
            public bool noFade;
        }

        private sealed class Mats
        {
            public Material glow, bubble, star, digits, streak, puff;
        }

        [MenuItem("Tools/要塞/演出素材/1 素材を生成", priority = 100)]
        public static void GenerateAll()
        {
            EnsureFolder(VfxFolder);
            EnsureFolder(SeFolder);
            EnsureFolder(BgmFolder);
            EnsureFolder(TexFolder);
            EnsureFolder(MatFolder);

            FortressFxTextures.SaveAll(TexFolder);
            AssetDatabase.Refresh();
            ConfigureTextures();

            var mats = BuildMaterials();
            BuildPrefabs(mats);
            BuildAudio();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FortressFx] 素材を生成しました（VFX 8 / SE 8 / BGM 1）。D-Drive の自動登録が終わったら、" +
                      "Tools › 要塞 › 演出素材 › 2 演出欄へ割り当て を実行してください。");
        }

        // ───────── フォルダ・テクスチャ・マテリアル ─────────

        public static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }

                cur = next;
            }
        }

        private static void ConfigureTextures()
        {
            foreach (var path in Directory.GetFiles(TexFolder, "*.png"))
            {
                var assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static Mats BuildMaterials()
        {
            return new Mats
            {
                glow = MakeMaterial("Fx_Glow", "Fx_Soft", additive: true),
                bubble = MakeMaterial("Fx_Bubble", "Fx_Bubble", additive: false),
                star = MakeMaterial("Fx_Star", "Fx_Star", additive: true),
                digits = MakeMaterial("Fx_Digits", "Fx_Digits", additive: true),
                streak = MakeMaterial("Fx_Streak", "Fx_Streak", additive: true),
                puff = MakeMaterial("Fx_Puff", "Fx_Soft", additive: false),
            };
        }

        private static Material MakeMaterial(string matName, string texName, bool additive)
        {
            var path = MatFolder + "/" + matName + ".mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/" + texName + ".png");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[FortressFx] URP の Particles/Unlit シェーダーが見つかりません。Sprites/Default で代用します。");
                shader = Shader.Find("Sprites/Default");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f); // 2=Additive, 0=Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }

            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ───────── パーティクルの組み立て ─────────

        private static ParticleSystem Layer(Transform parent, Spec s)
        {
            var go = new GameObject(s.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = s.offset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = Mathf.Max(0.1f, s.duration);
            main.loop = s.loop;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.life.x, s.life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.speed.x, s.speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(s.size.x, s.size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(s.c0, s.c1);
            main.startRotation = s.randRot ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = s.gravity;
            main.maxParticles = s.maxParticles;
            main.simulationSpace = s.worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = s.rate;
            if (s.burst > 0)
            {
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.burst) });
            }

            var shape = ps.shape;
            shape.enabled = true;
            switch (s.shape)
            {
                case Shape.Cone:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = s.angle;
                    shape.radius = s.radius;
                    break;
                case Shape.Sphere:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = s.radius;
                    break;
                default:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = s.radius;
                    break;
            }

            // 2Dのゲーム面（XY）で見えるよう、コーンは +Y 向きに倒す（円・球はそのままXYに広がる）
            shape.rotation = s.facingUp ? new Vector3(-90f, 0f, 0f) : Vector3.zero;

            if (!s.noFade)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            if (s.shrink || s.grow)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, s.shrink
                    ? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f))
                    : new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(1f, 1f)));
            }

            if (Mathf.Abs(s.spin) > 0.01f)
            {
                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-s.spin * Mathf.Deg2Rad, s.spin * Mathf.Deg2Rad);
            }

            if (s.cols > 1)
            {
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Grid;
                tsa.numTilesX = s.cols;
                tsa.numTilesY = 1;
                tsa.animation = ParticleSystemAnimationType.WholeSheet;
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
                tsa.cycleCount = 1;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = s.mat;
            r.renderMode = s.stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (s.stretch)
            {
                r.lengthScale = s.stretchLen;
                r.velocityScale = 0.1f;
            }

            r.sortingOrder = 100;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>RGBずれ風: 同じレイヤーを左右にずらし、ピンクと水色で重ねる。</summary>
        private static void Ghosts(Transform parent, Spec s, float shift)
        {
            var left = Clone(s, s.name + "_Pink", Pink, new Vector3(-shift, 0f, 0f));
            var right = Clone(s, s.name + "_Cyan", Cyan, new Vector3(shift, 0f, 0f));
            Layer(parent, left);
            Layer(parent, right);
        }

        private static Spec Clone(Spec s, string name, Color tint, Vector3 offset)
        {
            return new Spec
            {
                name = name, mat = s.mat, loop = s.loop, duration = s.duration, life = s.life, speed = s.speed,
                size = s.size, rate = s.rate * 0.7f, burst = s.burst > 0 ? Mathf.Max(1, Mathf.RoundToInt(s.burst * 0.7f)) : 0,
                c0 = new Color(tint.r, tint.g, tint.b, 0.45f), c1 = new Color(tint.r, tint.g, tint.b, 0.3f), shape = s.shape,
                angle = s.angle, radius = s.radius, facingUp = s.facingUp, gravity = s.gravity, randRot = s.randRot,
                shrink = s.shrink, grow = s.grow, offset = s.offset + offset, cols = s.cols, stretch = s.stretch,
                stretchLen = s.stretchLen, spin = s.spin, worldSpace = s.worldSpace, maxParticles = s.maxParticles, noFade = s.noFade,
            };
        }

        private static void Save(GameObject root, string name)
        {
            var path = VfxFolder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject Root(string name) => new GameObject(name);

        private static void BuildPrefabs(Mats m)
        {
            // レーザー発射口（撃っている間ずっと）: 全方向のミント色の光とあわ。向きに依存しない。
            {
                var root = Root("LaserMuzzle");
                var glow = new Spec { name = "Glow", mat = m.glow, loop = true, duration = 1f, life = new Vector2(0.28f, 0.4f), speed = new Vector2(0f, 0.1f),
                    size = new Vector2(0.9f, 1.2f), rate = 7f, c0 = new Color(0.55f, 1f, 1f, 0.75f), c1 = new Color(0.8f, 1f, 1f, 0.6f), worldSpace = false, maxParticles = 20 };
                Layer(root.transform, glow);
                Ghosts(root.transform, new Spec { name = "Shift", mat = m.glow, loop = true, duration = 1f, life = new Vector2(0.25f, 0.35f), speed = Vector2.zero,
                    size = new Vector2(1.1f, 1.4f), rate = 6f, worldSpace = false, maxParticles = 12 }, 0.07f);
                Layer(root.transform, new Spec { name = "Sparkle", mat = m.star, loop = true, duration = 1f, life = new Vector2(0.3f, 0.6f), speed = new Vector2(0.5f, 1.8f),
                    size = new Vector2(0.15f, 0.32f), rate = 10f, c0 = White, c1 = Cyan, radius = 0.35f, spin = 90f, shrink = true, maxParticles = 30 });
                Layer(root.transform, new Spec { name = "Bubble", mat = m.bubble, loop = true, duration = 1f, life = new Vector2(0.5f, 0.9f), speed = new Vector2(0.4f, 1.1f),
                    size = new Vector2(0.12f, 0.26f), rate = 5f, c0 = White, c1 = new Color(0.8f, 1f, 1f, 0.8f), radius = 0.3f, gravity = -0.12f, maxParticles = 20 });
                Layer(root.transform, new Spec { name = "Digits", mat = m.digits, loop = true, duration = 1f, life = new Vector2(0.5f, 0.8f), speed = new Vector2(0.5f, 1.2f),
                    size = new Vector2(0.14f, 0.2f), rate = 3f, c0 = Purple, c1 = Cyan, radius = 0.4f, cols = 2, randRot = false, maxParticles = 12 });
                Save(root, "LaserMuzzle");
            }

            // 着弾点（当たっている間）: 壁側（+Y=法線）へ飛ぶ泡の水しぶき。
            {
                var root = Root("LaserImpact");
                Layer(root.transform, new Spec { name = "Flash", mat = m.glow, loop = true, duration = 1f, life = new Vector2(0.18f, 0.26f), speed = Vector2.zero,
                    size = new Vector2(0.7f, 1f), rate = 9f, c0 = new Color(0.7f, 1f, 1f, 0.8f), c1 = new Color(1f, 0.8f, 1f, 0.7f), worldSpace = false, maxParticles = 12 });
                Ghosts(root.transform, new Spec { name = "Shift", mat = m.glow, loop = true, duration = 1f, life = new Vector2(0.15f, 0.22f), speed = Vector2.zero,
                    size = new Vector2(0.8f, 1.1f), rate = 8f, worldSpace = false, maxParticles = 10 }, 0.06f);
                Layer(root.transform, new Spec { name = "Spray", mat = m.bubble, loop = true, duration = 1f, life = new Vector2(0.3f, 0.65f), speed = new Vector2(2f, 4.5f),
                    size = new Vector2(0.1f, 0.26f), rate = 20f, c0 = White, c1 = new Color(0.8f, 1f, 1f, 0.9f), shape = Shape.Cone, facingUp = true, angle = 40f,
                    radius = 0.12f, gravity = 0.6f, maxParticles = 40 });
                Layer(root.transform, new Spec { name = "Sparkle", mat = m.star, loop = true, duration = 1f, life = new Vector2(0.2f, 0.4f), speed = new Vector2(2f, 5f),
                    size = new Vector2(0.12f, 0.24f), rate = 12f, c0 = White, c1 = Pink, shape = Shape.Cone, facingUp = true, angle = 55f, radius = 0.1f, spin = 120f, shrink = true, maxParticles = 25 });
                Save(root, "LaserImpact");
            }

            // 群衆ヒット（毎秒数十回）: 軽い。パチッとはじける小さな泡・星・0/1。
            {
                var root = Root("SwarmHit");
                Layer(root.transform, new Spec { name = "Pop", mat = m.glow, duration = 0.4f, life = new Vector2(0.12f, 0.18f), speed = Vector2.zero,
                    size = new Vector2(0.55f, 0.75f), burst = 1, c0 = new Color(0.85f, 0.7f, 1f, 0.9f), c1 = new Color(0.6f, 1f, 1f, 0.9f), maxParticles = 4 });
                Layer(root.transform, new Spec { name = "Ring", mat = m.bubble, duration = 0.4f, life = new Vector2(0.2f, 0.26f), speed = Vector2.zero,
                    size = new Vector2(0.3f, 0.45f), burst = 1, c0 = White, c1 = White, grow = true, maxParticles = 4 });
                Layer(root.transform, new Spec { name = "Star", mat = m.star, duration = 0.4f, life = new Vector2(0.18f, 0.3f), speed = new Vector2(1.5f, 3.5f),
                    size = new Vector2(0.12f, 0.22f), burst = 4, c0 = White, c1 = Cyan, radius = 0.05f, spin = 150f, shrink = true, maxParticles = 8 });
                Layer(root.transform, new Spec { name = "Digits", mat = m.digits, duration = 0.4f, life = new Vector2(0.25f, 0.4f), speed = new Vector2(1f, 2.2f),
                    size = new Vector2(0.12f, 0.16f), burst = 2, c0 = Purple, c1 = Pink, radius = 0.05f, cols = 2, randRot = false, maxParticles = 4 });
                Save(root, "SwarmHit");
            }

            // 破壊物ヒット
            {
                var root = Root("DestructHit");
                Layer(root.transform, new Spec { name = "Puff", mat = m.puff, duration = 0.5f, life = new Vector2(0.25f, 0.4f), speed = new Vector2(0.3f, 1f),
                    size = new Vector2(0.35f, 0.6f), burst = 3, c0 = new Color(1f, 1f, 1f, 0.6f), c1 = new Color(0.85f, 1f, 1f, 0.5f), radius = 0.2f, grow = true, maxParticles = 8 });
                Layer(root.transform, new Spec { name = "Star", mat = m.star, duration = 0.5f, life = new Vector2(0.2f, 0.35f), speed = new Vector2(2f, 4f),
                    size = new Vector2(0.14f, 0.26f), burst = 4, c0 = White, c1 = Cyan, radius = 0.1f, spin = 120f, shrink = true, maxParticles = 8 });
                Layer(root.transform, new Spec { name = "Bubble", mat = m.bubble, duration = 0.5f, life = new Vector2(0.3f, 0.5f), speed = new Vector2(1f, 2.5f),
                    size = new Vector2(0.1f, 0.2f), burst = 3, c0 = White, c1 = White, radius = 0.15f, gravity = 0.3f, maxParticles = 8 });
                Save(root, "DestructHit");
            }

            // 段階変化（ひび）
            {
                var root = Root("DestructStage");
                Layer(root.transform, new Spec { name = "Crack", mat = m.streak, duration = 0.5f, life = new Vector2(0.15f, 0.25f), speed = new Vector2(5f, 9f),
                    size = new Vector2(0.18f, 0.3f), burst = 8, c0 = White, c1 = Cyan, radius = 0.1f, stretch = true, stretchLen = 3f, randRot = false, maxParticles = 12 });
                Layer(root.transform, new Spec { name = "Puff", mat = m.puff, duration = 0.5f, life = new Vector2(0.3f, 0.5f), speed = new Vector2(0.5f, 1.5f),
                    size = new Vector2(0.4f, 0.7f), burst = 5, c0 = new Color(1f, 1f, 1f, 0.55f), c1 = new Color(0.8f, 0.95f, 1f, 0.45f), radius = 0.3f, grow = true, maxParticles = 10 });
                Layer(root.transform, new Spec { name = "Digits", mat = m.digits, duration = 0.5f, life = new Vector2(0.4f, 0.6f), speed = new Vector2(1f, 2.5f),
                    size = new Vector2(0.14f, 0.2f), burst = 4, c0 = Purple, c1 = Cyan, radius = 0.2f, cols = 2, randRot = false, maxParticles = 8 });
                Layer(root.transform, new Spec { name = "Star", mat = m.star, duration = 0.5f, life = new Vector2(0.25f, 0.4f), speed = new Vector2(2f, 5f),
                    size = new Vector2(0.15f, 0.28f), burst = 5, c0 = White, c1 = Pink, radius = 0.15f, spin = 150f, shrink = true, maxParticles = 10 });
                Save(root, "DestructStage");
            }

            // 破壊
            {
                var root = Root("DestructBreak");
                Layer(root.transform, new Spec { name = "Flash", mat = m.glow, duration = 0.5f, life = new Vector2(0.2f, 0.3f), speed = Vector2.zero,
                    size = new Vector2(2.2f, 2.8f), burst = 1, c0 = new Color(0.8f, 1f, 1f, 0.9f), c1 = new Color(1f, 0.8f, 1f, 0.9f), grow = true, maxParticles = 4 });
                Ghosts(root.transform, new Spec { name = "Shift", mat = m.glow, duration = 0.5f, life = new Vector2(0.2f, 0.3f), speed = Vector2.zero,
                    size = new Vector2(2.4f, 3f), burst = 1, grow = true, maxParticles = 4 }, 0.12f);
                Layer(root.transform, new Spec { name = "Puff", mat = m.puff, duration = 0.8f, life = new Vector2(0.5f, 0.85f), speed = new Vector2(0.8f, 2.5f),
                    size = new Vector2(0.6f, 1.2f), burst = 10, c0 = new Color(1f, 1f, 1f, 0.6f), c1 = new Color(0.8f, 0.95f, 1f, 0.5f), radius = 0.5f, grow = true, maxParticles = 20 });
                Layer(root.transform, new Spec { name = "Bubble", mat = m.bubble, duration = 0.8f, life = new Vector2(0.5f, 0.95f), speed = new Vector2(2f, 6f),
                    size = new Vector2(0.2f, 0.5f), burst = 18, c0 = White, c1 = new Color(0.85f, 1f, 1f, 0.9f), radius = 0.3f, gravity = 0.5f, maxParticles = 30 });
                Layer(root.transform, new Spec { name = "Star", mat = m.star, duration = 0.8f, life = new Vector2(0.4f, 0.8f), speed = new Vector2(3f, 7f),
                    size = new Vector2(0.2f, 0.4f), burst = 14, c0 = White, c1 = Pink, radius = 0.2f, spin = 180f, shrink = true, maxParticles = 24 });
                Layer(root.transform, new Spec { name = "Lines", mat = m.streak, duration = 0.8f, life = new Vector2(0.2f, 0.35f), speed = new Vector2(8f, 14f),
                    size = new Vector2(0.2f, 0.34f), burst = 12, c0 = White, c1 = Cyan, radius = 0.2f, stretch = true, stretchLen = 4f, randRot = false, maxParticles = 20 });
                Layer(root.transform, new Spec { name = "Digits", mat = m.digits, duration = 0.8f, life = new Vector2(0.6f, 1f), speed = new Vector2(1.5f, 4f),
                    size = new Vector2(0.16f, 0.26f), burst = 8, c0 = Purple, c1 = Cyan, radius = 0.3f, cols = 2, randRot = false, maxParticles = 14 });
                Save(root, "DestructBreak");
            }

            // 再生（復活）: 内側へ集まるきらめきと、ふわっと上がる泡。
            {
                var root = Root("DestructRegen");
                Layer(root.transform, new Spec { name = "Glow", mat = m.glow, duration = 0.8f, life = new Vector2(0.45f, 0.6f), speed = Vector2.zero,
                    size = new Vector2(1.6f, 2f), burst = 1, c0 = new Color(0.6f, 1f, 1f, 0.8f), c1 = new Color(0.85f, 0.8f, 1f, 0.8f), grow = true, maxParticles = 4 });
                Layer(root.transform, new Spec { name = "Gather", mat = m.star, duration = 0.8f, life = new Vector2(0.45f, 0.6f), speed = new Vector2(-2.5f, -1.5f),
                    size = new Vector2(0.15f, 0.3f), burst = 14, c0 = White, c1 = Cyan, radius = 1f, spin = 90f, shrink = true, maxParticles = 20 });
                Layer(root.transform, new Spec { name = "Bubble", mat = m.bubble, duration = 0.8f, life = new Vector2(0.6f, 1f), speed = new Vector2(0.3f, 1f),
                    size = new Vector2(0.12f, 0.28f), burst = 8, c0 = White, c1 = White, radius = 0.6f, gravity = -0.4f, maxParticles = 12 });
                Save(root, "DestructRegen");
            }

            // スマッシュボールが割れる: 集中線、閃光、RGBずれのリング、泡・星・0/1の大放出。
            {
                var root = Root("SmashBreak");
                Layer(root.transform, new Spec { name = "Flash", mat = m.glow, duration = 1f, life = new Vector2(0.25f, 0.35f), speed = Vector2.zero,
                    size = new Vector2(5f, 6f), burst = 1, c0 = new Color(0.9f, 1f, 1f, 1f), c1 = new Color(1f, 0.9f, 1f, 1f), grow = true, maxParticles = 4 });
                Layer(root.transform, new Spec { name = "Ring", mat = m.bubble, duration = 1f, life = new Vector2(0.45f, 0.55f), speed = Vector2.zero,
                    size = new Vector2(6f, 7f), burst = 1, c0 = Purple, c1 = Cyan, grow = true, maxParticles = 4 });
                Ghosts(root.transform, new Spec { name = "RingShift", mat = m.bubble, duration = 1f, life = new Vector2(0.45f, 0.55f), speed = Vector2.zero,
                    size = new Vector2(6f, 7f), burst = 1, grow = true, maxParticles = 4 }, 0.2f);
                Layer(root.transform, new Spec { name = "Lines", mat = m.streak, duration = 1f, life = new Vector2(0.3f, 0.45f), speed = new Vector2(10f, 18f),
                    size = new Vector2(0.18f, 0.32f), burst = 28, c0 = White, c1 = Pink, radius = 0.4f, stretch = true, stretchLen = 6f, randRot = false, maxParticles = 40 });
                Layer(root.transform, new Spec { name = "Bubble", mat = m.bubble, duration = 1f, life = new Vector2(0.7f, 1.3f), speed = new Vector2(3f, 9f),
                    size = new Vector2(0.25f, 0.7f), burst = 30, c0 = White, c1 = new Color(0.85f, 1f, 1f, 0.9f), radius = 0.4f, gravity = 0.3f, maxParticles = 50 });
                Layer(root.transform, new Spec { name = "Star", mat = m.star, duration = 1f, life = new Vector2(0.8f, 1.3f), speed = new Vector2(4f, 10f),
                    size = new Vector2(0.3f, 0.6f), burst = 30, c0 = White, c1 = Pink, radius = 0.3f, spin = 200f, shrink = true, maxParticles = 50 });
                Layer(root.transform, new Spec { name = "Digits", mat = m.digits, duration = 1f, life = new Vector2(0.9f, 1.4f), speed = new Vector2(2f, 6f),
                    size = new Vector2(0.2f, 0.34f), burst = 20, c0 = Purple, c1 = Cyan, radius = 0.4f, cols = 2, randRot = false, maxParticles = 30 });
                Save(root, "SmashBreak");
            }
        }

        // ───────── 効果音・BGM ─────────

        private static void BuildAudio()
        {
            var seBuilders = new Dictionary<string, System.Func<float[]>>
            {
                { "LaserMuzzle", FortressSynth.LaserMuzzle },
                { "LaserImpact", FortressSynth.LaserImpact },
                { "SwarmHit", FortressSynth.SwarmHit },
                { "DestructHit", FortressSynth.DestructHit },
                { "DestructStage", FortressSynth.DestructStage },
                { "DestructBreak", FortressSynth.DestructBreak },
                { "DestructRegen", FortressSynth.DestructRegen },
                { "SmashBreak", FortressSynth.SmashBreak },
            };

            foreach (var kv in seBuilders)
            {
                FortressSynth.SaveWav(SeFolder + "/" + kv.Key + ".wav", kv.Value());
            }

            FortressSynth.SaveWav(BgmFolder + "/" + BgmName + ".wav", FortressSynth.BgmCyberRun());
        }
    }
}
