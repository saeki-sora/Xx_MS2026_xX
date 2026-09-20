using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>NavigationFieldの作成・範囲の自動フィットなど、編集用の共通処理。</summary>
    public static class NavigationFieldTools
    {
        public static NavigationField CreateField()
        {
            var go = new GameObject("NavigationField");
            Undo.RegisterCreatedObjectUndo(go, "Create Navigation Field");

            var fortressRoot = GameObject.Find("Fortress");
            if (fortressRoot != null)
            {
                Undo.SetTransformParent(go.transform, fortressRoot.transform, "Create Navigation Field");
            }

            var field = Undo.AddComponent<NavigationField>(go);
            AutoFit(field, 4f);
            field.RebuildNow();
            Selection.activeGameObject = go;
            return field;
        }

        /// <summary>湧き位置・砲台・コア・障害物が全て収まるように範囲を合わせる。対象が無ければfalse。</summary>
        public static bool AutoFit(NavigationField field, float margin)
        {
            Bounds? bounds = null;

            void Include(Vector3 point)
            {
                if (bounds == null)
                {
                    bounds = new Bounds(point, Vector3.zero);
                }
                else
                {
                    var b = bounds.Value;
                    b.Encapsulate(point);
                    bounds = b;
                }
            }

            foreach (var p in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                Include(p.transform.position);
            }

            foreach (var t in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                Include(t.transform.position);
            }

            foreach (var c in Object.FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
            {
                Include(c.transform.position);
            }

            foreach (var o in NavigationObstacle.FindAll())
            {
                foreach (var r in o.GetComponentsInChildren<Renderer>())
                {
                    Include(r.bounds.min);
                    Include(r.bounds.max);
                }
            }

            if (bounds == null)
            {
                return false;
            }

            var fitted = bounds.Value;
            Undo.RecordObject(field, "Auto Fit Navigation Area");
            field.areaCenter = fitted.center;
            field.areaSize = new Vector2(
                Mathf.Max(4f, Mathf.Ceil(fitted.size.x + margin * 2f)),
                Mathf.Max(4f, Mathf.Ceil(fitted.size.y + margin * 2f)));
            EditorUtility.SetDirty(field);
            return true;
        }
    }
}
