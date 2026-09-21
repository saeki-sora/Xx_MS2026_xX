using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 砲台の仮の見た目（体＋砲身＋発射位置＋照準線）を作る。向きが一目で分かる形にしてある。
    /// 本番の絵に差し替えるときは、Barrel/Body のスプライトを差し替え、Muzzle をレーザーの出る位置に合わせるだけでよい。
    /// 既に存在するパーツは触らないので、差し替え後に再実行しても壊れない（作り直しを選んだ場合を除く）。
    /// </summary>
    public static class TurretVisualFactory
    {
        private const string BarrelName = "Barrel";
        private const string MuzzleName = "Muzzle";

        /// <param name="rebuild">trueなら、仮の砲身・銃口を削除して作り直す。</param>
        public static void EnsureVisual(LaserTurret turret, bool rebuild)
        {
            var root = turret.transform;
            var scale = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(root.lossyScale.x)),
                Mathf.Max(0.01f, Mathf.Abs(root.lossyScale.y)),
                1f);

            EnsureBody(turret);

            if (rebuild)
            {
                DestroyChild(root, BarrelName);
                DestroyChild(root, MuzzleName);
                Undo.RecordObject(turret, "Rebuild Turret Visual");
                turret.muzzle = null;
            }

            EnsureBarrel(turret, root, scale);
            EnsureMuzzle(turret, root, scale);
            EnsureAimIndicator(turret);
            EditorUtility.SetDirty(turret);
        }

        private static void EnsureBody(LaserTurret turret)
        {
            if (turret.GetComponent<SpriteRenderer>() != null)
            {
                return;
            }

            var visual = Undo.AddComponent<PlaceholderVisual>(turret.gameObject);
            visual.shape = PlaceholderShape.Circle;
            visual.color = FortressColors.PlayerColor(turret.playerIndex);
            visual.sortingOrder = 1;
            visual.Apply();
        }

        private static void EnsureBarrel(LaserTurret turret, Transform root, Vector3 rootScale)
        {
            if (root.Find(BarrelName) != null)
            {
                return;
            }

            var barrel = new GameObject(BarrelName);
            Undo.RegisterCreatedObjectUndo(barrel, "Create Turret Barrel");
            Undo.SetTransformParent(barrel.transform, root, "Create Turret Barrel");
            barrel.transform.localRotation = Quaternion.identity;

            // 親のスケールに関わらず、ワールドで 幅0.3 × 長さ0.9 の砲身が、中心から前方へ伸びる。
            barrel.transform.localPosition = new Vector3(0f, 0.55f / rootScale.y, 0f);
            barrel.transform.localScale = new Vector3(0.3f / rootScale.x, 0.9f / rootScale.y, 1f);

            var visual = Undo.AddComponent<PlaceholderVisual>(barrel);
            visual.shape = PlaceholderShape.Square;
            visual.color = Color.Lerp(FortressColors.PlayerColor(turret.playerIndex), Color.white, 0.55f);
            visual.sortingOrder = 2;
            visual.Apply();
        }

        private static void EnsureMuzzle(LaserTurret turret, Transform root, Vector3 rootScale)
        {
            var muzzle = root.Find(MuzzleName);
            if (muzzle == null)
            {
                var go = new GameObject(MuzzleName);
                Undo.RegisterCreatedObjectUndo(go, "Create Turret Muzzle");
                Undo.SetTransformParent(go.transform, root, "Create Turret Muzzle");
                go.transform.localRotation = Quaternion.identity;
                go.transform.localPosition = new Vector3(0f, 1f / rootScale.y, 0f);
                muzzle = go.transform;
            }

            if (turret.muzzle == null)
            {
                Undo.RecordObject(turret, "Assign Turret Muzzle");
                turret.muzzle = muzzle;
            }
        }

        private static void EnsureAimIndicator(LaserTurret turret)
        {
            if (turret.GetComponent<TurretAimIndicator>() == null)
            {
                Undo.AddComponent<TurretAimIndicator>(turret.gameObject);
            }
        }

        private static void DestroyChild(Transform root, string childName)
        {
            var child = root.Find(childName);
            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }
}
