using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>トゥーン材質と設定ファイルを作る・値を配る処理（調整ウィンドウと材質の画面から使う）。</summary>
    public static class ObjectToonMaterials
    {
        public const string ShaderName = "MS2026/3DObject/Toon";
        public const string SpriteShaderName = "MS2026/3DObject/ToonSprite";
        public const string Root = "Assets/3DObjectshader";
        public const string ProfilePath = Root + "/Resources/ObjectToon/ObjectToonProfile.asset";
        public const string CommonPath = Root + "/Materials/ObjectToon_Common.mat";
        public const string SpriteCommonPath = Root + "/Materials/ObjectToonSprite_Common.mat";
        public const string MaterialFolder = Root + "/Materials/Stages";
        public const string SpriteMaterialFolder = Root + "/Materials/Sprites";
        public const string ObjectMaterialFolder = Root + "/Materials/Objects";

        /// <summary>3Dの差し替えの候補にする元のシェーダー（ステージの物の専用シェーダーと URP の標準）。</summary>
        public static readonly string[] SourceShaderNames =
        {
            "MS2026/Stage/Prop",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
        };

        /// <summary>2Dの差し替えの候補にする元のシェーダー（SpriteRenderer の標準）。</summary>
        public static readonly string[] SpriteSourceShaderNames =
        {
            "Universal Render Pipeline/2D/Sprite-Lit-Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Sprites/Default",
        };

        /// <summary>材質ごとに持つ値（共通の設定から配らない）。色などは元の材質から写す。鏡の値は物ごとに決める。</summary>
        public static readonly HashSet<string> PerMaterialProperties = new HashSet<string>
        {
            "_BaseMap", "_BaseColor", "_AlphaClip", "_Cutoff", "_Cull", "_EmissionColor", "_StageNoHoles", "_StencilRef",
            "_ReflectOn", "_ReflectStrength", "_ReflectSkyColor", "_ReflectHorizonColor", "_ReflectGroundColor",
            "_ReflectSteps", "_ReflectFresnel", "_ReflectEnvBlend",
            "_StreakOn", "_StreakColor", "_StreakAngle", "_StreakWidth", "_StreakGap", "_StreakOffset", "_StreakSoftness",
            "_StreakScreenSpace",
        };

        public const string OutlinePassLightMode = "SRPDefaultUnlit";

        public static Shader ToonShader => Shader.Find(ShaderName);
        public static Shader SpriteShader => Shader.Find(SpriteShaderName);

        public static ObjectToonProfile LoadProfile()
        {
            return AssetDatabase.LoadAssetAtPath<ObjectToonProfile>(ProfilePath);
        }

        /// <summary>設定ファイルと「共通の設定」の材質（3D・2D）が無ければ作る。</summary>
        public static ObjectToonProfile EnsureProfile()
        {
            var profile = LoadProfile();
            if (profile == null)
            {
                EnsureFolder(Path.GetDirectoryName(ProfilePath));
                profile = ScriptableObject.CreateInstance<ObjectToonProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            if (profile.commonSettings == null)
            {
                profile.commonSettings = LoadOrCreateMaterial(CommonPath, ToonShader, "ObjectToon_Common");
                EditorUtility.SetDirty(profile);
            }

            if (profile.spriteCommonSettings == null)
            {
                profile.spriteCommonSettings = LoadOrCreateMaterial(SpriteCommonPath, SpriteShader, "ObjectToonSprite_Common");
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            return profile;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader, string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null && shader != null)
            {
                EnsureFolder(Path.GetDirectoryName(path));
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        public static bool IsSourceCandidate(Material material, bool sprite)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            return System.Array.IndexOf(sprite ? SpriteSourceShaderNames : SourceShaderNames, material.shader.name) >= 0;
        }

        /// <summary>シーンが使っている（Prefab の中も含む）候補の材質を集める。シーンを開かなくても探せる。</summary>
        public static List<Material> FindSourceMaterials(string scenePath, bool sprite)
        {
            var result = new List<Material>();
            foreach (var path in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (!path.EndsWith(".mat") || path.StartsWith(Root + "/"))
                {
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (IsSourceCandidate(material, sprite))
                {
                    result.Add(material);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        public static string FindScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>元の材質からトゥーン材質を作って保存する（元の材質は変えない）。</summary>
        public static Material CreateToon(Material original, Material common)
        {
            var toon = new Material(ToonShader) { name = original.name + "_Toon" };
            if (common != null)
            {
                CopyCommon(common, toon);
            }

            CopyFromOriginal(original, toon);
            if (LooksLikeMirror(original))
            {
                ApplyMirrorPreset(toon);
            }

            SaveNew(toon, MaterialFolder);
            return toon;
        }

        /// <summary>2Dの絵の材質からトゥーン材質を作って保存する。</summary>
        public static Material CreateSpriteToon(Material original, Material common)
        {
            var toon = new Material(SpriteShader) { name = original.name + "_ToonSprite" };
            if (common != null)
            {
                CopyCommon(common, toon);
            }

            SaveNew(toon, SpriteMaterialFolder);
            return toon;
        }

        /// <summary>1つの物だけに使う材質を、今使っているトゥーン材質（無ければ元の材質から作る）を写して作る。</summary>
        public static Material CreateObjectMaterial(Material source, Material original, string objectName, int slot)
        {
            Material toon;
            if (source != null && (source.shader == ToonShader || source.shader == SpriteShader))
            {
                toon = new Material(source);
            }
            else
            {
                toon = new Material(ToonShader);
                CopyFromOriginal(original, toon);
                if (LooksLikeMirror(original))
                {
                    ApplyMirrorPreset(toon);
                }
            }

            toon.name = objectName + "_" + slot + "_Toon";
            SyncOutlinePass(toon);
            SaveNew(toon, ObjectMaterialFolder);
            return toon;
        }

        /// <summary>つやが最大で金属っぽい材質（鏡）か。</summary>
        public static bool LooksLikeMirror(Material original)
        {
            return original != null && original.HasProperty("_Smoothness") && original.HasProperty("_Metallic") &&
                   original.GetFloat("_Smoothness") >= 0.9f && original.GetFloat("_Metallic") >= 0.2f;
        }

        /// <summary>鏡の見た目（空色の映り込み＋斜めの光の筋）にする。</summary>
        public static void ApplyMirrorPreset(Material toon)
        {
            toon.SetFloat("_ReflectOn", 1f);
            toon.SetFloat("_ReflectStrength", 0.9f);
            toon.SetColor("_ReflectSkyColor", new Color(0.82f, 0.93f, 1f));
            toon.SetColor("_ReflectHorizonColor", new Color(0.6f, 0.76f, 0.9f));
            toon.SetColor("_ReflectGroundColor", new Color(0.4f, 0.48f, 0.6f));
            toon.SetFloat("_ReflectSteps", 3f);
            toon.SetFloat("_ReflectFresnel", 0.3f);
            toon.SetFloat("_ReflectEnvBlend", 0f);
            toon.SetFloat("_StreakOn", 1f);
            toon.SetColor("_StreakColor", new Color(1.2f, 1.2f, 1.2f));
            toon.SetFloat("_StreakAngle", 35f);
            toon.SetFloat("_StreakWidth", 0.06f);
            toon.SetFloat("_StreakGap", 0.16f);
            toon.SetFloat("_StreakOffset", 0.5f);
            toon.SetFloat("_StreakSoftness", 0.1f);
            toon.SetFloat("_StreakScreenSpace", 0f);
            EditorUtility.SetDirty(toon);
        }

        /// <summary>色・テクスチャ・裏面・透け穴などを元の材質から写す。</summary>
        public static void CopyFromOriginal(Material original, Material toon)
        {
            if (original.HasProperty("_BaseMap"))
            {
                toon.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
                toon.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap"));
                toon.SetTextureOffset("_BaseMap", original.GetTextureOffset("_BaseMap"));
            }
            else if (original.HasProperty("_MainTex"))
            {
                toon.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
            }

            if (original.HasProperty("_BaseColor"))
            {
                toon.SetColor("_BaseColor", original.GetColor("_BaseColor"));
            }
            else if (original.HasProperty("_Color"))
            {
                toon.SetColor("_BaseColor", original.GetColor("_Color"));
            }

            CopyFloat(original, toon, "_Cull");
            CopyFloat(original, toon, "_Cutoff");
            CopyFloat(original, toon, "_StencilRef");

            bool alphaClip = original.IsKeywordEnabled("_ALPHATEST_ON");
            toon.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            SetKeyword(toon, "_ALPHATEST_ON", alphaClip);

            bool noHoles = original.IsKeywordEnabled("_STAGE_NO_HOLES");
            toon.SetFloat("_StageNoHoles", noHoles ? 1f : 0f);
            SetKeyword(toon, "_STAGE_NO_HOLES", noHoles);

            bool emission = original.IsKeywordEnabled("_EMISSION") && original.HasProperty("_EmissionColor");
            toon.SetColor("_EmissionColor", emission ? original.GetColor("_EmissionColor") : Color.black);

            SyncOutlinePass(toon);
            EditorUtility.SetDirty(toon);
        }

        /// <summary>「共通の設定」の値（材質ごとの値は除く）を写す。</summary>
        public static void CopyCommon(Material common, Material target)
        {
            if (common == null || target == null || common == target || common.shader != target.shader)
            {
                return;
            }

            var shader = common.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                if (PerMaterialProperties.Contains(name) ||
                    (shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.PerRendererData) != 0)
                {
                    continue;
                }

                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        target.SetColor(name, common.GetColor(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        target.SetVector(name, common.GetVector(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        target.SetFloat(name, common.GetFloat(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Int:
                        target.SetInteger(name, common.GetInteger(name));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        target.SetTexture(name, common.GetTexture(name));
                        break;
                }
            }

            SyncOutlinePass(target);
            EditorUtility.SetDirty(target);
        }

        /// <summary>「共通に合わせる」がオンの材質すべてに、共通の設定を配る。配った数を返す。</summary>
        public static int PushCommon(Material common, List<ObjectToonProfile.Entry> entries)
        {
            if (common == null || entries == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var entry in entries)
            {
                if (entry != null && entry.useCommon && entry.toon != null)
                {
                    CopyCommon(common, entry.toon);
                    count++;
                }
            }

            RepaintViews();
            return count;
        }

        /// <summary>共通の設定の材質なら、その仲間に配る（どちらでもなければ何もしない）。</summary>
        public static void PushIfCommon(ObjectToonProfile profile, Material material)
        {
            if (profile == null || material == null)
            {
                return;
            }

            if (material == profile.commonSettings)
            {
                PushCommon(profile.commonSettings, profile.entries);
            }
            else if (material == profile.spriteCommonSettings)
            {
                PushCommon(profile.spriteCommonSettings, profile.spriteEntries);
            }
        }

        /// <summary>シーンビューとゲームビューをすぐ描き直す（値を変えた結果をその場で見せる）。</summary>
        public static void RepaintViews()
        {
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        /// <summary>「輪郭線を出す」がオフなら輪郭線のパスごと止める（描画が1回減る）。3Dのトゥーン材質だけ。</summary>
        public static void SyncOutlinePass(Material material)
        {
            if (material == null || material.shader == null || material.shader.name != ShaderName)
            {
                return;
            }

            bool on = material.GetFloat("_OutlineOn") > 0.5f && material.GetFloat("_OutlineWidth") > 0f;
            if (material.GetShaderPassEnabled(OutlinePassLightMode) != on)
            {
                material.SetShaderPassEnabled(OutlinePassLightMode, on);
                EditorUtility.SetDirty(material);
            }
        }

        public static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void SaveNew(Material material, string folder)
        {
            EnsureFolder(folder);
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Sanitize(material.name) + ".mat");
            AssetDatabase.CreateAsset(material, path);
        }

        private static void CopyFloat(Material from, Material to, string name)
        {
            if (from.HasProperty(name))
            {
                to.SetFloat(name, from.GetFloat(name));
            }
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
