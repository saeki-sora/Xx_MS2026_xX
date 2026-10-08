using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 見た目のモデル（子のMeshFilter / SkinnedMeshRenderer）から、輪郭づくり用の頂点と三角形を集める。
    /// 頂点は「基準Transform（背景オブジェクトの足元）」から見た座標に直す。
    /// 編集中は読み取り不可（Read/Write OFF）のメッシュも読めるが、ビルド後は読めないので輪郭は編集中に作って保存しておく。
    /// </summary>
    public static class FootprintMeshSampler
    {
        public static void Collect(Transform modelRoot, Transform reference, List<Vector3> vertices, List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();
            if (modelRoot == null || reference == null)
            {
                return;
            }

            var toReference = reference.worldToLocalMatrix;
            var meshVertices = new List<Vector3>();
            var meshTriangles = new List<int>();

            foreach (var filter in modelRoot.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Append(filter.sharedMesh, toReference * filter.transform.localToWorldMatrix, vertices, triangles, meshVertices, meshTriangles);
            }

            foreach (var skinned in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (skinned.sharedMesh == null || !skinned.enabled)
                {
                    continue;
                }

                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                Append(baked, toReference * skinned.transform.localToWorldMatrix, vertices, triangles, meshVertices, meshTriangles);
                Object.DestroyImmediate(baked);
            }
        }

        /// <summary>モデルの形・置き方が変わったかを見分けるための値（輪郭が古くなっていないかの判定に使う）。</summary>
        public static int ComputeSignature(Transform modelRoot, Transform reference, FootprintSettings settings)
        {
            unchecked
            {
                var hash = settings.GetHashCode();
                if (modelRoot == null || reference == null)
                {
                    return hash;
                }

                var toReference = reference.worldToLocalMatrix;
                foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(false))
                {
                    Mesh mesh = null;
                    if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                    {
                        mesh = filter.sharedMesh;
                    }
                    else if (renderer is SkinnedMeshRenderer skinned)
                    {
                        mesh = skinned.sharedMesh;
                    }

                    if (mesh == null || !renderer.enabled)
                    {
                        continue;
                    }

                    hash = hash * 31 + mesh.vertexCount;
                    hash = hash * 31 + mesh.name.GetHashCode();
                    hash = hash * 31 + Quantize(toReference * renderer.transform.localToWorldMatrix);
                }

                return hash;
            }
        }

        private static void Append(
            Mesh mesh,
            Matrix4x4 matrix,
            List<Vector3> vertices,
            List<int> triangles,
            List<Vector3> meshVertices,
            List<int> meshTriangles)
        {
            mesh.GetVertices(meshVertices);
            var offset = vertices.Count;
            foreach (var v in meshVertices)
            {
                vertices.Add(matrix.MultiplyPoint3x4(v));
            }

            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                {
                    continue;
                }

                mesh.GetTriangles(meshTriangles, sub);
                foreach (var index in meshTriangles)
                {
                    triangles.Add(offset + index);
                }
            }
        }

        private static int Quantize(Matrix4x4 m)
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < 16; i++)
                {
                    hash = hash * 31 + Mathf.RoundToInt(m[i] * 1000f);
                }

                return hash;
            }
        }
    }
}
