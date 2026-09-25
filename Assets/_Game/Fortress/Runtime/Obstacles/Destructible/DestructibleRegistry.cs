using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>Play中に有効な破壊可能物の一覧。グループ・連鎖の相手探しに使う。</summary>
    public static class DestructibleRegistry
    {
        private static readonly List<DestructibleObstacle> Items = new List<DestructibleObstacle>();

        public static IReadOnlyList<DestructibleObstacle> All => Items;

        public static void Register(DestructibleObstacle obstacle)
        {
            if (!Items.Contains(obstacle))
            {
                Items.Add(obstacle);
            }
        }

        public static void Unregister(DestructibleObstacle obstacle)
        {
            Items.Remove(obstacle);
        }

        /// <summary>同じグループ名を持つ、自分以外の破壊可能物をbufferに集める。</summary>
        public static void CollectGroup(string groupId, DestructibleObstacle except, List<DestructibleObstacle> buffer)
        {
            buffer.Clear();
            foreach (var item in Items)
            {
                if (item != except && item.link.HasGroup && item.link.groupId == groupId)
                {
                    buffer.Add(item);
                }
            }
        }

        /// <summary>centerからradius以内（相手の範囲との距離）にある、自分以外の破壊可能物をbufferに集める。</summary>
        public static void CollectNear(Vector2 center, float radius, DestructibleObstacle except, List<DestructibleObstacle> buffer)
        {
            buffer.Clear();
            var point = new Vector3(center.x, center.y, 0f);
            var sqrRadius = radius * radius;
            foreach (var item in Items)
            {
                if (item != except && item.ApproximateBounds.SqrDistance(point) <= sqrRadius)
                {
                    buffer.Add(item);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Items.Clear();
        }
    }
}
