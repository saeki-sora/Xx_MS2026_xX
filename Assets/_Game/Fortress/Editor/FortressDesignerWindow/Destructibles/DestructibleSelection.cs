using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>今選択している破壊可能物（子オブジェクトを選んでいても親の破壊可能物として扱う）。</summary>
    public static class DestructibleSelection
    {
        public static List<DestructibleObstacle> Get()
        {
            var result = new List<DestructibleObstacle>();
            foreach (var go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    continue;
                }

                var obstacle = go.GetComponentInParent<DestructibleObstacle>();
                if (obstacle != null && !result.Contains(obstacle))
                {
                    result.Add(obstacle);
                }
            }

            return result;
        }

        public static void Set(IEnumerable<DestructibleObstacle> obstacles)
        {
            var objects = new List<Object>();
            foreach (var obstacle in obstacles)
            {
                objects.Add(obstacle.gameObject);
            }

            Selection.objects = objects.ToArray();
        }

        /// <summary>
        /// シーン内の破壊可能物を全て返す（非アクティブな物も含む）。
        /// スマッシュボールのローテーション待機中はGameObjectごと非アクティブになるため、含めないとツールから消えて見える。
        /// </summary>
        public static DestructibleObstacle[] FindAllInScene()
        {
            return Object.FindObjectsByType<DestructibleObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
