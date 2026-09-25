using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>破壊可能物をシーンに生成する。見た目は仮の四角で、後からPrefabに差し替えられる。</summary>
    public static class DestructibleFactory
    {
        private const string UndoName = "Add Destructible";

        /// <summary>centerを中心に、sizeの大きさで破壊可能物を作る。presetがあればその設定を適用する。</summary>
        public static DestructibleObstacle Create(DestructiblePreset preset, Vector2 center, Vector2 size)
        {
            var undoGroup = Undo.GetCurrentGroup();
            var go = new GameObject(MakeName(preset));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            Undo.SetTransformParent(go.transform, ObstacleFactory.GetOrCreateRoot().transform, UndoName);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), 1f);

            var placeholder = Undo.AddComponent<PlaceholderVisual>(go);
            placeholder.shape = PlaceholderShape.Square;
            placeholder.sortingOrder = -1;

            var obstacle = Undo.AddComponent<DestructibleObstacle>(go);
            if (preset != null)
            {
                DestructiblePresetTools.Apply(preset, new[] { obstacle });
            }

            go.GetComponent<DestructibleCollision>().EnsureShape();
            EnsureNavigationCovers(center, size);

            Undo.SetCurrentGroupName(UndoName);
            Undo.CollapseUndoOperations(undoGroup);
            return obstacle;
        }

        /// <summary>
        /// 敵が障害物を避けるには経路フィールドが必要。無ければ作り、置いた場所が範囲外なら範囲を広げる。
        /// </summary>
        private static void EnsureNavigationCovers(Vector2 center, Vector2 size)
        {
            var field = Object.FindFirstObjectByType<NavigationField>();
            if (field == null)
            {
                NavigationFieldTools.CreateField();
                return;
            }

            var area = new Rect(field.areaCenter - field.areaSize * 0.5f, field.areaSize);
            var placed = new Rect(center - size * 0.5f, size);
            if (!area.Contains(placed.min) || !area.Contains(placed.max))
            {
                NavigationFieldTools.AutoFit(field, 4f);
            }
        }

        /// <summary>少しずらした位置に複製する（設定・連動もそのまま）。</summary>
        public static DestructibleObstacle Duplicate(DestructibleObstacle source, Vector2 offset)
        {
            var copy = Object.Instantiate(source.gameObject, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Destructible");
            copy.name = MakeUniqueName(source.name);
            copy.transform.position = source.transform.position + (Vector3)offset;
            return copy.GetComponent<DestructibleObstacle>();
        }

        private static string MakeName(DestructiblePreset preset)
        {
            return MakeUniqueName(preset != null && !string.IsNullOrWhiteSpace(preset.displayName)
                ? preset.displayName
                : "Destructible");
        }

        private static string MakeUniqueName(string baseName)
        {
            // 「木箱_3_copy」のように増えていかないよう、末尾の連番・copyを取り除いてから採番する。
            var trimmed = System.Text.RegularExpressions.Regex.Replace(baseName, @"(_\d+)?(_copy)*$", "");
            if (string.IsNullOrEmpty(trimmed))
            {
                trimmed = "Destructible";
            }

            var existing = NavigationObstacle.FindAll().Select(o => o.name).ToHashSet();
            for (var i = 0; ; i++)
            {
                var candidate = $"{trimmed}_{i}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
