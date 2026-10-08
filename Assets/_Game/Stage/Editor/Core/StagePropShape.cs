using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトの「形」を調べる共通の道具:
    /// 視線で当たる物を探す（当たり判定なしでメッシュに当てる）、見た目の頂点（間引き済み）、床の上の広がり（吸着・距離用）。
    /// シーンビューの配置ツールと、配置ページのゲーム画面が同じものを使う。
    /// </summary>
    public static class StagePropShape
    {
        private const int MaxVerticesPerMesh = 600;

        private static readonly Dictionary<Mesh, Vector3[]> SampledVertices = new Dictionary<Mesh, Vector3[]>();
        private static readonly List<Vector3> Scratch = new List<Vector3>(4096);
        private static MethodInfo _intersectRayMesh;

        /// <summary>視線に最初に当たる背景オブジェクトと、その当たった点。</summary>
        public static bool Pick(Ray ray, IReadOnlyList<StageProp> props, out StageProp picked, out Vector3 hit)
        {
            picked = null;
            hit = default;
            var best = float.PositiveInfinity;
            foreach (var prop in props)
            {
                if (prop == null || !prop.isActiveAndEnabled)
                {
                    continue;
                }

                var root = StagePropTransformOps.Model(prop);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer || !renderer.bounds.IntersectRay(ray, out var boundsDistance) || boundsDistance > best)
                    {
                        continue;
                    }

                    if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                    {
                        if (TryIntersect(ray, filter, out var point, out var distance) && distance < best)
                        {
                            best = distance;
                            picked = prop;
                            hit = point;
                        }
                    }
                    else if (boundsDistance < best)
                    {
                        // 骨で曲がるモデルなどは、見た目の箱で代用する。
                        best = boundsDistance;
                        picked = prop;
                        hit = ray.GetPoint(boundsDistance);
                    }
                }
            }

            return picked != null;
        }

        /// <summary>メッシュに視線を当てる（Unity 内部の HandleUtility.IntersectRayMesh を使う。無い版では false）。</summary>
        public static bool TryIntersect(Ray ray, MeshFilter filter, out Vector3 point, out float distance)
        {
            point = default;
            distance = 0f;
            _intersectRayMesh ??= typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.NonPublic | BindingFlags.Static);
            if (_intersectRayMesh == null)
            {
                return false;
            }

            var args = new object[] { ray, filter.sharedMesh, filter.transform.localToWorldMatrix, null };
            if (!(bool)_intersectRayMesh.Invoke(null, args))
            {
                return false;
            }

            var hit = (RaycastHit)args[3];
            point = hit.point;
            distance = hit.distance;
            return true;
        }

        /// <summary>見た目の頂点（ワールド座標）。大きなメッシュは間引く。骨で曲がるモデルは見た目の箱の角。</summary>
        public static IEnumerable<Vector3> WorldVertices(StageProp prop)
        {
            var root = StagePropTransformOps.Model(prop);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled)
                {
                    continue;
                }

                var matrix = filter.transform.localToWorldMatrix;
                foreach (var v in Sample(filter.sharedMesh))
                {
                    yield return matrix.MultiplyPoint3x4(v);
                }
            }

            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                var b = skinned.bounds;
                for (var i = 0; i < 8; i++)
                {
                    yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                }
            }
        }

        /// <summary>
        /// 床の上の広がりの点（通れない範囲があればその輪郭、無ければ見た目の箱の床の四角）。吸着と距離に使う。
        /// </summary>
        public static IEnumerable<Vector2> FloorPoints(StageProp prop)
        {
            if (prop == null)
            {
                yield break;
            }

            var any = false;
            if (prop.role.NeedsFootprint())
            {
                foreach (var path in StageGapFinder.WorldPaths(prop))
                {
                    foreach (var p in path)
                    {
                        any = true;
                        yield return p;
                    }
                }
            }

            if (!any)
            {
                var b = prop.VisualBounds;
                yield return new Vector2(b.min.x, b.min.y);
                yield return new Vector2(b.max.x, b.max.y);
            }
        }

        public static IEnumerable<Vector2> FloorPoints(IEnumerable<StageProp> props)
        {
            foreach (var prop in props)
            {
                foreach (var p in FloorPoints(prop))
                {
                    yield return p;
                }
            }
        }

        /// <summary>床の上の広がりを多角形で（通れない範囲の輪郭、無ければ見た目の箱の床の四角）。距離を測るのに使う。</summary>
        public static List<Vector2[]> FloorShapes(StageProp prop)
        {
            var shapes = new List<Vector2[]>();
            if (prop.role.NeedsFootprint())
            {
                shapes.AddRange(StageGapFinder.WorldPaths(prop));
            }

            if (shapes.Count == 0)
            {
                var b = prop.VisualBounds;
                shapes.Add(new[]
                {
                    new Vector2(b.min.x, b.min.y), new Vector2(b.max.x, b.min.y),
                    new Vector2(b.max.x, b.max.y), new Vector2(b.min.x, b.max.y)
                });
            }

            return shapes;
        }

        private static Vector3[] Sample(Mesh mesh)
        {
            if (SampledVertices.TryGetValue(mesh, out var cached) && cached != null)
            {
                return cached;
            }

            Scratch.Clear();
            mesh.GetVertices(Scratch);
            var stride = Mathf.Max(1, Mathf.CeilToInt(Scratch.Count / (float)MaxVerticesPerMesh));
            var result = new Vector3[(Scratch.Count + stride - 1) / stride];
            for (int i = 0, k = 0; i < Scratch.Count; i += stride, k++)
            {
                result[k] = Scratch[i];
            }

            SampledVertices[mesh] = result;
            return result;
        }
    }
}
