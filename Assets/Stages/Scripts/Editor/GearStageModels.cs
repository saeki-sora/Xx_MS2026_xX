using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Stage;
using MS2026.Stage.EditorTools;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// 歯車ステージ（<see cref="GearStageBuilder"/>）の仮の 3D モデル（Models/Stage03_Gear の OBJ。Source~/make_models.py で作った物）を、
    /// ステージ背景スタジオの仕組みで入れる係（2026-10-29）。
    /// ・取り込みの設定（大きさ 1・法線は計算・形を読める）とマテリアル（URP Lit。床はタイルの絵）をそろえる
    /// ・床・壁などは背景スタジオの取り込み（分けて取り込む・Maya の向き・専用シェーダーへの切り替え）でステージの Prefab に入れる
    /// ・歯車・動く壁の見た目は、背景スタジオの「背景オブジェクト」（飾り）として、回る部分・動く壁の中に置く（一緒に動く）
    /// 見た目は後から差し替えてよい（背景スタジオの オブジェクト › モデル › 差し替える、または FBX をこの名前で置き換える）。
    /// </summary>
    internal static class GearStageModels
    {
        public const string StaticModel = "Stage03_Static";
        private const string TilePath = GearStageBuilder.ModelFolder + "/TX_G3_FloorTile.png";

        /// <summary>OBJ のマテリアルの名前 → 作ったマテリアル。</summary>
        public sealed class Materials
        {
            public readonly Dictionary<string, Material> ByName = new Dictionary<string, Material>();

            public Material Find(string importedName)
            {
                if (string.IsNullOrEmpty(importedName))
                {
                    return null;
                }

                foreach (var pair in ByName)
                {
                    if (importedName == pair.Key || importedName.StartsWith(pair.Key))
                    {
                        return pair.Value;
                    }
                }

                return null;
            }
        }

        // 名前: 色・金属っぽさ・つや・光る色（0 なら光らない）。
        // ほかのステージ（今の時代の洗面台）に合わせて、つやのある淡い色のプラスチック（ステージ1 の plastic_* と同じ金属 0・つや 0.8）にした（2026-11-05）。
        // 名前は最初の版のまま（OBJ の usemtl・取り込み設定のつながりを切らないため）: Gold = 歯車（桃色）、Brass = 白、Steel = 水色、Panel = うす紫。
        private static readonly (string name, Color color, float metallic, float smoothness, Color emission)[] Specs =
        {
            ("G3_Floor", new Color(0.77f, 0.96f, 0.92f), 0f, 0.8f, Color.black),
            ("G3_Gold", new Color(1.00f, 0.73f, 0.95f), 0f, 0.8f, Color.black),
            ("G3_Brass", new Color(0.99f, 1.00f, 0.90f), 0f, 0.8f, Color.black),
            ("G3_Steel", new Color(0.76f, 0.83f, 1.00f), 0f, 0.8f, Color.black),
            ("G3_Panel", new Color(0.88f, 0.82f, 1.00f), 0f, 0.8f, Color.black),
            ("G3_GlowCyan", new Color(0.63f, 1.00f, 0.76f), 0f, 0.8f, new Color(0.63f, 1f, 0.76f) * 0.35f),
            ("G3_GlowPink", new Color(1.00f, 0.55f, 0.85f), 0f, 0.8f, new Color(1f, 0.55f, 0.85f) * 0.4f),
            ("G3_Hazard", new Color(1.00f, 0.80f, 0.42f), 0f, 0.8f, Color.black),
        };

        /// <summary>取り込みの設定とマテリアルをそろえる（何度呼んでもよい。今あるマテリアルの色・つやも Specs に合わせ直す）。</summary>
        public static Materials Prepare(string modelFolder, string materialFolder, List<string> report)
        {
            StageAssets.EnsureFolder(materialFolder);
            var tile = PrepareTile();
            var materials = new Materials();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (name, color, metallic, smoothness, emission) in Specs)
            {
                var path = $"{materialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var created = material == null;
                if (created)
                {
                    material = new Material(shader) { name = name };
                    if (name == "G3_Floor" && tile != null)
                    {
                        material.SetTexture("_BaseMap", tile);
                    }
                }

                ApplySpec(material, name == "G3_Floor" && material.GetTexture("_BaseMap") != null, color, metallic, smoothness, emission);
                if (created)
                {
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    EditorUtility.SetDirty(material);
                }

                materials.ByName[name] = material;
            }

            var count = 0;
            foreach (var path in Directory.GetFiles(modelFolder, "*.obj").Select(p => p.Replace('\\', '/')))
            {
                if (ConfigureImporter(path, materials))
                {
                    count++;
                }
            }

            report.Add($"3D モデル（{modelFolder}）の取り込み設定を {count} 個直した。マテリアルは {materialFolder}。");
            return materials;
        }

        /// <summary>
        /// ステージの背景（StageSet）のフォルダにある、専用シェーダーへ切り替えた後のマテリアル（G3_*_Stage など）の色・つやも Specs に合わせる。
        /// 背景はもうあると作り直さないので、見た目の色を変えたときはこれで追いつかせる。直した数を返す。
        /// </summary>
        public static int SyncStageMaterials(StageSet set)
        {
            if (set == null)
            {
                return 0;
            }

            var count = 0;
            var folder = StageAssetFactory.FolderOf(set);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null)
                {
                    continue;
                }

                foreach (var (name, color, metallic, smoothness, emission) in Specs)
                {
                    if (!material.name.StartsWith(name + "_"))
                    {
                        continue;
                    }

                    var textured = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
                    ApplySpec(material, textured, color, metallic, smoothness, emission);
                    EditorUtility.SetDirty(material);
                    count++;
                    break;
                }
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>色・金属っぽさ・つや・光る色を入れる。絵（タイル）があるときは、絵の色をそのまま出す（色は白）。</summary>
        private static void ApplySpec(Material material, bool textured, Color color, float metallic, float smoothness, Color emission)
        {
            material.SetColor("_BaseColor", textured ? Color.white : color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
        }

        private static Texture2D PrepareTile()
        {
            if (AssetImporter.GetAtPath(TilePath) is TextureImporter importer &&
                (importer.wrapMode != TextureWrapMode.Repeat || importer.textureType != TextureImporterType.Default))
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TilePath);
        }

        /// <summary>OBJ の取り込み設定（大きさ 1・法線は角を立てて計算・形を読める・マテリアルは作った物へ）。直したら true。</summary>
        private static bool ConfigureImporter(string path, Materials materials)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                return false;
            }

            var changed = false;
            if (!Mathf.Approximately(importer.globalScale, 1f)) { importer.globalScale = 1f; changed = true; }
            if (importer.importNormals != ModelImporterNormals.Calculate) { importer.importNormals = ModelImporterNormals.Calculate; changed = true; }
            if (!Mathf.Approximately(importer.normalSmoothingAngle, 25f)) { importer.normalSmoothingAngle = 25f; changed = true; }
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                changed = true;
            }

            // OBJ のマテリアル（usemtl の名前）を、作ったマテリアルへつなぐ。
            var existing = importer.GetExternalObjectMap();
            foreach (var pair in materials.ByName)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                if (!existing.TryGetValue(id, out var current) || current != pair.Value)
                {
                    importer.AddRemap(id, pair.Value);
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        /// <summary>取り込んだモデルの中の、つなげなかったマテリアルを名前で作った物に替える（取り込みの名前が違ったとき用）。</summary>
        public static void AssignMaterials(GameObject instance, Materials materials)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                var dirty = false;
                for (var i = 0; i < shared.Length; i++)
                {
                    var found = shared[i] != null ? materials.Find(shared[i].name) : null;
                    if (found != null && found != shared[i])
                    {
                        shared[i] = found;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        /// <summary>
        /// 床・壁・台などの 3D モデル（Stage03_Static.obj）を、背景スタジオの取り込み（分けて取り込む）で今のステージに入れる。
        /// 床（Floor_）は「床」、ほかは「飾り」（当たり判定は GearStageBuilder の見えない四角が持つので、形から通れない範囲は作らない）。
        /// </summary>
        public static int ImportStatic(StageRoot root, StageSet set, List<string> report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{GearStageBuilder.ModelFolder}/{StaticModel}.obj");
            var container = StageSceneService.PropsContainer(root);
            if (source == null || container == null)
            {
                report.Add($"{GearStageBuilder.ModelFolder}/{StaticModel}.obj が無いので、床・壁の見た目は入れなかった。");
                return 0;
            }

            var options = new StageImportOptions
            {
                splitChildren = true,
                convertMayaAxes = true,
                yawSteps = 0,
                scale = 1f,
                centerOnOrigin = false,
                convertMaterials = true
            };
            var plan = StageModelImporter.BuildPlan(source, options);
            foreach (var node in plan.Nodes.Where(n => !n.IsMarker))
            {
                node.Role = node.Name.StartsWith("Floor") ? StagePropRole.Floor : StagePropRole.Decoration;
            }

            var result = StageModelImporter.Import(plan, options, container, set);
            return result.Props.Count;
        }

        /// <summary>
        /// 床・壁などのモデル（Stage03_Static.obj）から消えた部分の背景オブジェクトを、ステージの Prefab から外す
        /// （make_models.py で部品を消して作り直したとき用。例: 2026-10-29 に四隅の砲台の台をやめた）。外した数を返す。
        /// </summary>
        public static int RemoveMissingStaticParts(StageSet set)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{GearStageBuilder.ModelFolder}/{StaticModel}.obj");
            var path = set != null && set.stagePrefab != null ? AssetDatabase.GetAssetPath(set.stagePrefab) : null;
            if (source == null || string.IsNullOrEmpty(path))
            {
                return 0;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var gone = contents.GetComponentsInChildren<StageProp>(true)
                    .Where(p => p.sourceModel == source && !string.IsNullOrEmpty(p.sourceNodePath) && source.transform.Find(p.sourceNodePath) == null)
                    .ToList();
                foreach (var prop in gone)
                {
                    Object.DestroyImmediate(prop.gameObject);
                }

                if (gone.Count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }

                return gone.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// モデル（name.obj）を world の位置・Z の向き（度）に置き、背景スタジオの背景オブジェクト（飾り）にして parent の中へ入れる。
        /// マテリアルは専用シェーダー（透け・ビームの光）に切り替える。
        /// </summary>
        public static StageProp AttachProp(Transform parent, string propName, string model, Vector3 world, float yawDegrees, StageSet set,
            Materials materials)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{GearStageBuilder.ModelFolder}/{model}.obj");
            if (asset == null || parent == null)
            {
                Debug.LogWarning($"[Stage] {GearStageBuilder.ModelFolder}/{model}.obj が無いので、{parent?.name} の見た目は入れなかった。");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = model;
            instance.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, 0f, yawDegrees) * Quaternion.Euler(-90f, 0f, 0f));
            AssignMaterials(instance, materials);
            var prop = StagePropComposer.CreateFromModel(parent, propName, instance, StagePropRole.Decoration, asset, "");
            StageMaterialConverter.Convert(prop, StageAssetFactory.FolderOf(set));
            return prop;
        }
    }
}
