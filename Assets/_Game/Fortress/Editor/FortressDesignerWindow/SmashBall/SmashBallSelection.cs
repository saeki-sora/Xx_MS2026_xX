using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>シーン内のスマッシュボール・現在選択しているスマッシュボールの取得。</summary>
    public static class SmashBallSelection
    {
        public static SmashBallModule[] FindAllInScene()
        {
            return Object.FindObjectsByType<SmashBallModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        public static List<SmashBallModule> Get()
        {
            var result = new List<SmashBallModule>();
            foreach (var go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    continue;
                }

                var module = go.GetComponentInParent<SmashBallModule>();
                if (module != null && !result.Contains(module))
                {
                    result.Add(module);
                }
            }

            return result;
        }
    }
}
