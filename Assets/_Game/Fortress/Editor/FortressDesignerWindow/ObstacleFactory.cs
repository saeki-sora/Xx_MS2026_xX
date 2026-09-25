using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    public enum ObstacleKind
    {
        Destructible,
        Solid,
        CostZone
    }

    /// <summary>障害物・地帯のプレースホルダー付きオブジェクトを生成する。見た目は後から本番の絵に差し替え可能。</summary>
    public static class ObstacleFactory
    {
        public static GameObject Create(ObstacleKind kind, Vector2 position)
        {
            if (kind == ObstacleKind.Destructible)
            {
                // 破壊可能物は専用のファクトリ（プリセット・見た目・演出などを含む）で作る。
                return DestructibleFactory.Create(
                    DestructibleEditorSettings.ActivePreset, position, DestructibleEditorSettings.CurrentPlaceSize()).gameObject;
            }

            var go = new GameObject(MakeName(kind));
            Undo.RegisterCreatedObjectUndo(go, "Add Obstacle");
            Undo.SetTransformParent(go.transform, GetOrCreateRoot().transform, "Add Obstacle");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var visual = Undo.AddComponent<PlaceholderVisual>(go);
            visual.shape = PlaceholderShape.Square;

            var collider = Undo.AddComponent<BoxCollider2D>(go);
            collider.size = Vector2.one;

            var obstacle = Undo.AddComponent<NavigationObstacle>(go);

            switch (kind)
            {
                case ObstacleKind.Solid:
                    go.transform.localScale = new Vector3(4f, 1f, 1f);
                    visual.color = new Color(0.42f, 0.44f, 0.5f);
                    visual.sortingOrder = -1;
                    obstacle.mode = NavigationObstacleMode.Block;
                    break;

                case ObstacleKind.CostZone:
                    go.transform.localScale = new Vector3(4f, 4f, 1f);
                    visual.color = new Color(0.3f, 0.55f, 0.95f, 0.35f);
                    visual.sortingOrder = -3;
                    collider.isTrigger = true;
                    obstacle.mode = NavigationObstacleMode.Cost;
                    break;
            }

            visual.Apply();
            return go;
        }

        public static string KindLabel(NavigationObstacle obstacle)
        {
            if (obstacle.mode == NavigationObstacleMode.Cost)
            {
                return "地帯";
            }

            return obstacle.GetComponent<DestructibleObstacle>() != null ? "破壊可" : "壁";
        }

        private static string MakeName(ObstacleKind kind)
        {
            var baseName = kind switch
            {
                ObstacleKind.Destructible => "Destructible",
                ObstacleKind.Solid => "Wall",
                _ => "Zone"
            };

            var existing = NavigationObstacle.FindAll().Select(o => o.name).ToHashSet();
            for (var i = 0; ; i++)
            {
                var candidate = $"{baseName}_{i}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        internal static GameObject GetOrCreateRoot()
        {
            var existing = GameObject.Find("Obstacles");
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("Obstacles");
            Undo.RegisterCreatedObjectUndo(root, "Add Obstacle");

            var fortress = GameObject.Find("Fortress");
            if (fortress != null)
            {
                Undo.SetTransformParent(root.transform, fortress.transform, "Add Obstacle");
            }

            return root;
        }
    }
}
