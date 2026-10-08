using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// Maya等のモデル（FBX / Prefab）を背景オブジェクトとして取り込む。
    /// 1. <see cref="BuildPlan"/> で中身を調べて予定表を作る（名前から役割と目印を推測）
    /// 2. 予定表を人が直す
    /// 3. <see cref="Import"/> で背景オブジェクトを作る（通れない範囲・マテリアルも用意）
    /// まとめて1つで取り込むときはPrefabのつながりを保つので、Mayaで直したモデルがそのまま反映される。
    /// 分けて取り込むときは各部分のコピーになる（形の更新は「モデルを取り込み直す」で反映）。
    /// </summary>
    public static class StageModelImporter
    {
        public sealed class Result
        {
            public readonly List<StageProp> Props = new List<StageProp>();
            public readonly List<(StageMarkerKind kind, int index, Vector3 position, string name)> Markers =
                new List<(StageMarkerKind, int, Vector3, string)>();
        }

        public static StageImportPlan BuildPlan(GameObject source, StageImportOptions options)
        {
            var plan = new StageImportPlan { SourceAsset = source };
            if (source == null)
            {
                return plan;
            }

            var conversion = options.ConversionMatrix();
            var root = source.transform;
            var hasBounds = false;

            void AddNode(Transform t, string path)
            {
                var node = new StageImportNode
                {
                    Source = t,
                    Path = path,
                    Name = t.name,
                    Marker = StageNameRules.ParseMarker(t.name, out var index),
                    MarkerIndex = index,
                    Role = StageNameRules.GuessRole(t.name)
                };

                var bounds = MeasureConverted(t, root, conversion, out node.TriangleCount);
                node.Center = bounds.center;
                node.Size = bounds.size;
                if (node.IsMarker)
                {
                    node.Center = conversion.MultiplyPoint3x4(root.worldToLocalMatrix.MultiplyPoint3x4(t.position));
                    node.Include = true;
                }
                else if (node.TriangleCount == 0)
                {
                    node.Include = false; // 形の無い物（空の親・カメラ・ライトなど）は既定で取り込まない
                }

                if (!node.IsMarker && node.TriangleCount > 0)
                {
                    if (hasBounds)
                    {
                        plan.TotalBounds.Encapsulate(bounds);
                    }
                    else
                    {
                        plan.TotalBounds = bounds;
                        hasBounds = true;
                    }
                }

                plan.Nodes.Add(node);
            }

            if (options.splitChildren && root.childCount > 0)
            {
                var splitRoot = UnwrapGroups(root);
                foreach (Transform child in splitRoot)
                {
                    AddNode(child, PathFrom(root, child));
                }
            }
            else
            {
                AddNode(root, string.Empty);
            }

            // 目印（MARK_）はどの深さにあっても拾う（物の中に入れてあっても使えるように）。
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && StageNameRules.ParseMarker(t.name, out _) != StageMarkerKind.None && !plan.Nodes.Exists(n => n.Source == t))
                {
                    AddNode(t, PathFrom(root, t));
                }
            }

            return plan;
        }

        public static Result Import(StageImportPlan plan, StageImportOptions options, Transform container, StageSet stage)
        {
            var result = new Result();
            if (plan.SourceAsset == null || container == null)
            {
                return result;
            }

            Undo.SetCurrentGroupName("モデルを取り込む");
            var group = Undo.GetCurrentGroup();
            var sourceRoot = plan.SourceAsset.transform;
            var toGame = Matrix4x4.Translate(plan.CenterOffset(options)) * options.ConversionMatrix();
            var folder = StageAssetFactory.FolderOf(stage);

            try
            {
                for (var i = 0; i < plan.Nodes.Count; i++)
                {
                    var node = plan.Nodes[i];
                    EditorUtility.DisplayProgressBar("モデルを取り込む", node.Name, (float)i / plan.Nodes.Count);
                    if (!node.Include)
                    {
                        continue;
                    }

                    if (node.IsMarker)
                    {
                        var local = toGame.MultiplyPoint3x4(sourceRoot.worldToLocalMatrix.MultiplyPoint3x4(node.Source.position));
                        result.Markers.Add((node.Marker, node.MarkerIndex, container.TransformPoint(local), node.Name));
                        continue;
                    }

                    var instance = Instantiate(plan.SourceAsset, node, sourceRoot);
                    var world = container.localToWorldMatrix * toGame * (sourceRoot.worldToLocalMatrix * node.Source.localToWorldMatrix);
                    Place(instance.transform, container, world);
                    StripMarkers(instance.transform);

                    var prop = StagePropComposer.CreateFromModel(container, node.Name, instance, node.Role, plan.SourceAsset, node.Path);
                    if (options.convertMaterials)
                    {
                        StageMaterialConverter.Convert(prop, folder);
                    }

                    result.Props.Add(prop);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(group);
            StageSceneService.MarkSceneDirty();
            return result;
        }

        /// <summary>
        /// Mayaの書き出しでよくある「全部が1つのグループの下」の形なら、形を持たないグループを開いて中身を分ける。
        /// </summary>
        private static Transform UnwrapGroups(Transform root)
        {
            var current = root;
            while (current.childCount == 1)
            {
                var only = current.GetChild(0);
                if (only.childCount == 0 || only.GetComponent<MeshFilter>() != null || only.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    break;
                }

                current = only;
            }

            return current;
        }

        /// <summary>root から見た t の場所（"Group/Shampoo" のような名前の並び）。Transform.Find で戻れる形。</summary>
        private static string PathFrom(Transform root, Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }

        /// <summary>モデルの中の、パス（"Shelf/Bottle" のような名前の並び）の物を探す。空ならモデル全体。</summary>
        public static Transform FindNode(GameObject source, string path)
        {
            if (source == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(path) ? source.transform : source.transform.Find(path);
        }

        /// <summary>取り込み直し・差し替え用に、モデル（の一部分）の実体を作る。全体ならPrefabのつながりを保つ。</summary>
        public static GameObject InstantiateNode(GameObject source, string path)
        {
            var node = FindNode(source, path);
            if (node == null)
            {
                return null;
            }

            if (node == source.transform)
            {
                return (GameObject)PrefabUtility.InstantiatePrefab(source);
            }

            var copy = Object.Instantiate(node.gameObject);
            copy.name = node.name;
            return copy;
        }

        private static GameObject Instantiate(GameObject source, StageImportNode node, Transform sourceRoot)
        {
            var instance = node.Source == sourceRoot
                ? (GameObject)PrefabUtility.InstantiatePrefab(source)
                : Object.Instantiate(node.Source.gameObject);
            instance.name = node.Name;
            Undo.RegisterCreatedObjectUndo(instance, "モデルを取り込む");
            return instance;
        }

        /// <summary>world の位置・回転・大きさになるよう、container の子として置く。</summary>
        private static void Place(Transform t, Transform container, Matrix4x4 world)
        {
            t.SetParent(container, false);
            var local = container.worldToLocalMatrix * world;
            t.localPosition = local.GetColumn(3);
            t.localRotation = local.rotation;
            t.localScale = local.lossyScale;
        }

        /// <summary>目印（MARK_）は見た目ではないので、取り込んだ物の中からは消す。</summary>
        private static void StripMarkers(Transform root)
        {
            var markers = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && StageNameRules.ParseMarker(t.name, out _) != StageMarkerKind.None)
                {
                    markers.Add(t.gameObject);
                }
            }

            foreach (var marker in markers)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(marker))
                {
                    Object.DestroyImmediate(marker);
                }
                else
                {
                    marker.SetActive(false);
                }
            }
        }

        /// <summary>変換後の座標で、t の下の見た目の範囲と三角形の数を調べる。</summary>
        private static Bounds MeasureConverted(Transform t, Transform root, Matrix4x4 conversion, out int triangles)
        {
            triangles = 0;
            var has = false;
            var bounds = new Bounds();
            var toRoot = root.worldToLocalMatrix;

            foreach (var filter in t.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    triangles += (int)(mesh.GetIndexCount(s) / 3);
                }

                var matrix = conversion * toRoot * filter.transform.localToWorldMatrix;
                var local = mesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = matrix.MultiplyPoint3x4(corner);
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

            foreach (var skinned in t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh != null)
                {
                    triangles += skinned.sharedMesh.triangles.Length / 3;
                }
            }

            return bounds;
        }
    }
}
