using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 見た目Prefabを、破壊可能物の大きさ（親のスケール）に合わせて拡縮・配置する。
    /// 親のスケールがそのまま「大きさ」なので、Scene上でスケールを変えるだけで見た目も追従する。
    /// </summary>
    public static class DestructibleVisualFitter
    {
        private const float MinScale = 1e-4f;

        public static void Fit(Transform node, DestructibleFitMode mode, Vector2 offset)
        {
            node.localScale = Vector3.one;
            node.localPosition = Vector3.zero;
            node.localRotation = Quaternion.identity;

            var parentScale = AbsScale(node.parent != null ? node.parent.lossyScale : Vector3.one);

            // 子のスケールを1にした状態での、Prefab本来の大きさ（子のローカル座標）を測る。
            if (!TryMeasureLocalBounds(node, out var bounds))
            {
                node.localPosition = new Vector3(offset.x, offset.y, 0f);
                return;
            }

            var size = new Vector3(
                Mathf.Max(MinScale, bounds.size.x),
                Mathf.Max(MinScale, bounds.size.y),
                Mathf.Max(MinScale, bounds.size.z));

            // 目標の「ワールドでの拡大率」を決め、親のスケール分を引いて子のスケールにする。
            Vector3 worldScale;
            switch (mode)
            {
                case DestructibleFitMode.Stretch:
                {
                    var fx = parentScale.x / size.x;
                    var fy = parentScale.y / size.y;
                    worldScale = new Vector3(fx, fy, Mathf.Min(fx, fy));
                    break;
                }

                case DestructibleFitMode.Contain:
                {
                    var uniform = Mathf.Min(parentScale.x / size.x, parentScale.y / size.y);
                    worldScale = new Vector3(uniform, uniform, uniform);
                    break;
                }

                default:
                    worldScale = Vector3.one;
                    break;
            }

            var local = new Vector3(
                worldScale.x / parentScale.x,
                worldScale.y / parentScale.y,
                worldScale.z / parentScale.z);

            node.localScale = local;
            node.localPosition = new Vector3(
                -bounds.center.x * local.x + offset.x,
                -bounds.center.y * local.y + offset.y,
                -bounds.center.z * local.z);
        }

        private static bool TryMeasureLocalBounds(Transform node, out Bounds result)
        {
            result = default;
            var found = false;

            foreach (var renderer in node.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    continue;
                }

                var world = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = world.center + Vector3.Scale(world.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    var local = node.InverseTransformPoint(corner);

                    if (!found)
                    {
                        result = new Bounds(local, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        result.Encapsulate(local);
                    }
                }
            }

            return found;
        }

        private static Vector3 AbsScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Max(MinScale, Mathf.Abs(scale.x)),
                Mathf.Max(MinScale, Mathf.Abs(scale.y)),
                Mathf.Max(MinScale, Mathf.Abs(scale.z)));
        }
    }
}
