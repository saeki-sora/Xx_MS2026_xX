using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトのマテリアルを専用シェーダー（MS2026/Stage/Prop）のコピーに切り替える／元に戻す。
    /// 元のマテリアル（FBXの中の読み取り専用の物でも可）は変えず、ステージのフォルダの Materials/ にコピーを作る。
    /// コピーには元のマテリアルの場所を覚えさせておく（アセットの userData）ので、いつでも元に戻せる。
    /// 床用（透け穴なし・敵の影の印なし）は別のコピーになる。
    /// </summary>
    public static class StageMaterialConverter
    {
        private const string SourceTag = "stage-source:";

        public static Shader PropShader => Shader.Find(StageShaderIds.PropShaderName);

        /// <summary>オブジェクトの全Rendererのマテリアルを専用シェーダーのコピーに替える。戻り値は替えた数。</summary>
        public static int Convert(StageProp prop, string folder)
        {
            var shader = PropShader;
            if (shader == null)
            {
                Debug.LogError($"[StageStudio] シェーダー {StageShaderIds.PropShaderName} が見つかりません。");
                return 0;
            }

            var floor = prop.role == StagePropRole.Floor;
            var mark = !floor && prop.look.showHiddenEnemies;
            var changed = 0;
            foreach (var renderer in Renderers(prop))
            {
                var materials = renderer.sharedMaterials;
                var dirty = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = OriginalOf(materials[i]);
                    if (source == null)
                    {
                        continue;
                    }

                    var converted = GetOrCreateCopy(source, folder, shader, holes: !floor, enemyMark: mark);
                    if (converted != materials[i])
                    {
                        materials[i] = converted;
                        dirty = true;
                        changed++;
                    }
                }

                if (dirty)
                {
                    Undo.RecordObject(renderer, "専用シェーダーに切り替え");
                    renderer.sharedMaterials = materials;
                }
            }

            return changed;
        }

        /// <summary>専用シェーダーのコピーを、元のマテリアルに戻す。</summary>
        public static int Revert(StageProp prop)
        {
            var changed = 0;
            foreach (var renderer in Renderers(prop))
            {
                var materials = renderer.sharedMaterials;
                var dirty = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = OriginalOf(materials[i]);
                    if (source != null && source != materials[i])
                    {
                        materials[i] = source;
                        dirty = true;
                        changed++;
                    }
                }

                if (dirty)
                {
                    Undo.RecordObject(renderer, "元のマテリアルに戻す");
                    renderer.sharedMaterials = materials;
                }
            }

            return changed;
        }

        /// <summary>専用シェーダーを使っていないマテリアルの数（点検用）。</summary>
        public static int CountUnconverted(StageProp prop)
        {
            var count = 0;
            foreach (var renderer in Renderers(prop))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null && material.shader.name != StageShaderIds.PropShaderName)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>コピーなら元のマテリアル、コピーでなければ自分自身。</summary>
        public static Material OriginalOf(Material material)
        {
            if (material == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(material);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData == null || !importer.userData.StartsWith(SourceTag))
            {
                return material;
            }

            var parts = importer.userData.Substring(SourceTag.Length).Split('|');
            var sourcePath = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (parts.Length > 1 && long.TryParse(parts[1], out var localId))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
                {
                    if (asset is Material m && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out _, out long id) && id == localId)
                    {
                        return m;
                    }
                }
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            return loaded != null ? loaded : material;
        }

        /// <summary>同じ元・同じ種類（床用か・影の印あり）のコピーが既にあればそれを使い回す。</summary>
        private static Material FindExistingCopy(Material source, string folder, bool holes, bool enemyMark)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null || candidate == source || OriginalOf(candidate) != source)
                {
                    continue;
                }

                var candidateHoles = candidate.GetFloat("_StageNoHoles") < 0.5f;
                var candidateMark = candidate.GetFloat(StageShaderIds.StencilRef) > 0.5f;
                if (candidateHoles == holes && candidateMark == enemyMark)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Material GetOrCreateCopy(Material source, string folder, Shader shader, bool holes, bool enemyMark)
        {
            var suffix = holes ? (enemyMark ? "" : "_NoMark") : "_Floor";
            var materialsFolder = folder + "/Materials";
            StageAssetFactory.EnsureFolder(materialsFolder);
            var path = $"{materialsFolder}/{StageAssetFactory.MakeSafeFileName(source.name)}_Stage{suffix}.mat";

            var reusable = FindExistingCopy(source, materialsFolder, holes, enemyMark);
            if (reusable != null)
            {
                return reusable;
            }

            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var copy = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            CopyLookFrom(source, copy);
            copy.SetFloat("_StageNoHoles", holes ? 0f : 1f);
            copy.SetFloat(StageShaderIds.StencilRef, enemyMark ? StageShaderIds.SilhouetteStencilBit : 0f);
            StagePropShaderGUI.SetupKeywords(copy);
            AssetDatabase.CreateAsset(copy, path);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out var guid, out long localId);
            AssetImporter.GetAtPath(path).userData = $"{SourceTag}{guid}|{localId}";
            AssetDatabase.WriteImportSettingsIfDirty(path);
            return copy;
        }

        /// <summary>URP Lit / Simple Lit / 旧Standard など、よくある項目名から色・テクスチャ・つやを引き継ぐ。</summary>
        private static void CopyLookFrom(Material source, Material target)
        {
            CopyTexture(source, target, "_BaseMap", "_BaseMap", "_MainTex");
            CopyColor(source, target, "_BaseColor", "_BaseColor", "_Color");
            CopyTexture(source, target, "_BumpMap", "_BumpMap", "_NormalMap");
            CopyFloat(source, target, "_BumpScale", "_BumpScale");
            CopyTexture(source, target, "_MetallicGlossMap", "_MetallicGlossMap");
            CopyFloat(source, target, "_Metallic", "_Metallic");
            CopyFloat(source, target, "_Smoothness", "_Smoothness", "_Glossiness");
            CopyTexture(source, target, "_OcclusionMap", "_OcclusionMap");
            CopyFloat(source, target, "_OcclusionStrength", "_OcclusionStrength");
            CopyTexture(source, target, "_EmissionMap", "_EmissionMap");
            CopyColor(source, target, "_EmissionColor", "_EmissionColor");
            CopyFloat(source, target, "_Cutoff", "_Cutoff");
            CopyFloat(source, target, "_Cull", "_Cull");

            var alphaClip = source.IsKeywordEnabled("_ALPHATEST_ON") || (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f);
            target.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            if (source.IsKeywordEnabled("_EMISSION"))
            {
                target.EnableKeyword("_EMISSION");
            }
        }

        private static void CopyTexture(Material source, Material target, string to, params string[] from)
        {
            foreach (var name in from)
            {
                if (source.HasProperty(name) && source.GetTexture(name) != null)
                {
                    target.SetTexture(to, source.GetTexture(name));
                    target.SetTextureScale(to, source.GetTextureScale(name));
                    target.SetTextureOffset(to, source.GetTextureOffset(name));
                    return;
                }
            }
        }

        private static void CopyColor(Material source, Material target, string to, params string[] from)
        {
            foreach (var name in from)
            {
                if (source.HasProperty(name))
                {
                    target.SetColor(to, source.GetColor(name));
                    return;
                }
            }
        }

        private static void CopyFloat(Material source, Material target, string to, params string[] from)
        {
            foreach (var name in from)
            {
                if (source.HasProperty(name))
                {
                    target.SetFloat(to, source.GetFloat(name));
                    return;
                }
            }
        }

        private static IEnumerable<Renderer> Renderers(StageProp prop)
        {
            var root = prop.visualRoot != null ? prop.visualRoot : prop.transform;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    yield return renderer;
                }
            }
        }
    }
}
