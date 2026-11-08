using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// コアの見た目を3Dモデル（歯）にする・置き直す・元の丸い絵に戻す（全ステージ共通。Game シーンのコアを変える）。
    /// モデルはコアの子の入れ物（<see cref="CoreToothModel"/>）の下に置き、向き・大きさ・高さを <see cref="CoreToothFit"/> で合わせる。
    /// 材質は歯専用のトゥーン（MS2026/Fortress/CoreTooth）にそろえる。すべて Undo に対応する。
    /// </summary>
    public static class CoreToothSetup
    {
        public const string GameScenePath = "Assets/Scenes/Game.unity";
        public const string MaterialPath = "Assets/_Game/Fortress/Art/Core/CoreTooth.mat";
        private const string ShaderName = "MS2026/Fortress/CoreTooth";
        private const string CommonToonMaterialName = "ObjectToon_Common";

        /// <summary>読み込まれているシーンのコア（無ければ null）。</summary>
        public static CoreCrystalController FindCore()
        {
            return Object.FindFirstObjectByType<CoreCrystalController>(FindObjectsInactive.Include);
        }

        /// <summary>コアの3Dモデルの入れ物（無ければ null）。</summary>
        public static CoreToothModel FindModel(CoreCrystalController core)
        {
            return core != null ? core.GetComponentInChildren<CoreToothModel>(true) : null;
        }

        /// <summary>コアの元の丸い絵（無ければ null）。</summary>
        public static SpriteRenderer FindSprite(CoreCrystalController core)
        {
            return core != null && core.TryGetComponent<SpriteRenderer>(out var sprite) ? sprite : null;
        }

        /// <summary>Game シーンを、今のシーンに足して開く。</summary>
        public static void OpenGameScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) != null)
            {
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }
        }

        /// <summary>
        /// <paramref name="asset"/>（FBX・Prefab）をコアの見た目にする。前のモデルがあれば入れ替える。
        /// 置き方は入れ物に残っている値（初めてなら幅 2.5m・Maya の向き）を使う。
        /// </summary>
        public static CoreToothModel Apply(CoreCrystalController core, GameObject asset)
        {
            if (core == null || asset == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("コアを3Dモデルにする");

            var holder = FindModel(core);
            if (holder == null)
            {
                var go = new GameObject(CoreToothModel.ObjectName);
                Undo.RegisterCreatedObjectUndo(go, "コアを3Dモデルにする");
                Undo.SetTransformParent(go.transform, core.transform, "コアを3Dモデルにする");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                holder = Undo.AddComponent<CoreToothModel>(go);
            }

            for (var i = holder.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(holder.transform.GetChild(i).gameObject);
            }

            var model = InstantiateModel(asset, holder.transform);
            Undo.RecordObject(holder, "コアを3Dモデルにする");
            holder.sourceModel = asset;

            var material = EnsureMaterial(FirstMaterial(model));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (var i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }

                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }

            Refit(holder);
            ShowSprite(core, false);

            var look = core.GetComponent<CoreToothLook>();
            if (look == null)
            {
                look = Undo.AddComponent<CoreToothLook>(core.gameObject);
            }

            look.Rebind();
            Undo.CollapseUndoOperations(group);
            MarkDirty(core);
            return holder;
        }

        /// <summary>入れ物に覚えている値（幅・向き・高さ）で、モデルを置き直す。</summary>
        public static void Refit(CoreToothModel holder)
        {
            if (holder == null || holder.transform.childCount == 0 || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var model = holder.transform.GetChild(0);
            Undo.RecordObject(model, "コアのモデルを置き直す");
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;

            // 大きさは、まっすぐ立てたとき（回さず・傾けず）の幅で決める（向きを変えても大きさが変わらないように）。
            model.localRotation = CoreToothFit.Rotation(holder.mayaUp, 0f);
            if (!TryOrientedBounds(holder.transform, model, out var upright))
            {
                return;
            }

            model.localRotation = CoreToothFit.Rotation(holder.mayaUp, holder.yawDegrees, holder.tiltForward, holder.tiltSide);
            TryOrientedBounds(holder.transform, model, out var oriented);

            // 入れ物の座標はコアの大きさ（例: 2.5倍）を含むので、メートルを入れ物の座標の長さに直す。
            var unit = Mathf.Max(1e-4f, holder.transform.lossyScale.x);
            var scale = CoreToothFit.ScaleFor(upright, holder.widthMeters / unit);
            var offset = new Vector2(holder.offsetX, holder.offsetY) / unit;
            model.localScale = Vector3.one * scale;
            model.localPosition = CoreToothFit.Place(oriented, scale, holder.sinkMeters / unit, offset);
            MarkDirty(holder);
        }

        /// <summary>3Dモデルを外して、元の丸い絵に戻す。</summary>
        public static void RevertToSprite(CoreCrystalController core)
        {
            var holder = FindModel(core);
            if (core == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("コアを元の丸い絵に戻す");
            if (holder != null)
            {
                Undo.DestroyObjectImmediate(holder.gameObject);
            }

            var look = core.GetComponent<CoreToothLook>();
            if (look != null)
            {
                Undo.DestroyObjectImmediate(look);
            }

            ShowSprite(core, true);
            Undo.CollapseUndoOperations(group);
            MarkDirty(core);
        }

        /// <summary>コアのあるシーン（Game）を保存する。</summary>
        public static bool SaveCoreScene(CoreCrystalController core)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            // 渡されたコアが消えていたら（Play を止めたあとなど）、今のシーンから探し直す。
            if (core == null)
            {
                core = FindCore();
            }

            return core != null && EditorSceneManager.SaveScene(core.gameObject.scene);
        }

        /// <summary>
        /// 歯の材質を用意する（無ければ作る）。新しく作るときは、トゥーンの値をステージの物の「共通の設定」（ObjectToon_Common）から写し、
        /// 色はモデルの元の材質の色にする。
        /// </summary>
        public static Material EnsureMaterial(Material source)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"歯のシェーダー「{ShaderName}」が見つかりません。");
                return null;
            }

            var material = new Material(shader) { name = "CoreTooth" };
            var common = FindCommonToonMaterial();
            if (common != null)
            {
                CopyMatching(common, material, skipColor: true);
            }

            if (source != null)
            {
                if (source.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                }
                else if (source.HasProperty("_Color"))
                {
                    material.SetColor("_BaseColor", source.GetColor("_Color"));
                }
            }

            material.SetShaderPassEnabled("SRPDefaultUnlit", !material.HasProperty("_OutlineOn") || material.GetFloat("_OutlineOn") > 0.5f);
            var folder = System.IO.Path.GetDirectoryName(MaterialPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static GameObject InstantiateModel(GameObject asset, Transform parent)
        {
            GameObject model;
            if (PrefabUtility.IsPartOfPrefabAsset(asset) && asset.transform.parent == null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            }
            else
            {
                model = Object.Instantiate(asset);
                model.name = asset.name;
            }

            Undo.RegisterCreatedObjectUndo(model, "コアを3Dモデルにする");
            Undo.SetTransformParent(model.transform, parent, "コアを3Dモデルにする");
            return model;
        }

        private static Material FirstMaterial(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        // 向きを付けたモデルの外形（入れ物の座標）。頂点から測るので、回したあとでもぴったり。
        private static bool TryOrientedBounds(Transform holder, Transform model, out Bounds bounds)
        {
            bounds = new Bounds();
            var has = false;
            var toHolder = holder.worldToLocalMatrix;
            var vertices = new List<Vector3>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                {
                    mesh = filter.sharedMesh;
                }

                if (mesh == null)
                {
                    continue;
                }

                var matrix = toHolder * renderer.transform.localToWorldMatrix;
                mesh.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var p = matrix.MultiplyPoint3x4(vertex);
                    if (has)
                    {
                        bounds.Encapsulate(p);
                    }
                    else
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                }
            }

            return has;
        }

        private static void ShowSprite(CoreCrystalController core, bool show)
        {
            var sprite = FindSprite(core);
            if (sprite != null && sprite.enabled != show)
            {
                Undo.RecordObject(sprite, show ? "コアを元の丸い絵に戻す" : "コアを3Dモデルにする");
                sprite.enabled = show;
            }
        }

        private static Material FindCommonToonMaterial()
        {
            foreach (var guid in AssetDatabase.FindAssets(CommonToonMaterialName + " t:Material"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null && material.name == CommonToonMaterialName)
                {
                    return material;
                }
            }

            return null;
        }

        // 同じ名前の項目だけ写す（トゥーンの値はステージの物と項目名がそろっている）。
        private static void CopyMatching(Material from, Material to, bool skipColor)
        {
            var shader = from.shader;
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                if (!to.HasProperty(name) || (skipColor && (name == "_BaseColor" || name == "_BaseMap")))
                {
                    continue;
                }

                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        to.SetColor(name, from.GetColor(name));
                        break;
                    case ShaderPropertyType.Vector:
                        to.SetVector(name, from.GetVector(name));
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        to.SetFloat(name, from.GetFloat(name));
                        break;
                    case ShaderPropertyType.Int:
                        to.SetInteger(name, from.GetInteger(name));
                        break;
                    case ShaderPropertyType.Texture:
                        to.SetTexture(name, from.GetTexture(name));
                        break;
                }
            }
        }

        private static void MarkDirty(Component component)
        {
            if (component != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                SceneView.RepaintAll();
            }
        }
    }
}
