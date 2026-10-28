using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>仕掛け（歯車・腕・動く障害物）を作る・付ける（すべて Undo できる）。</summary>
    public static class GimmickFactory
    {
        public const string RootName = "Gimmicks";
        public const string DiscName = "Disc";
        private static readonly Color GearColor = new Color(0.85f, 0.65f, 0.3f);
        private static readonly Color ArmColor = new Color(0.55f, 0.4f, 0.2f);

        /// <summary>仕掛けを入れる親。今のシーンに「Gimmicks」が無ければ、経路（NavigationField）と同じ親の下に作る。</summary>
        public static Transform GetOrCreateRoot()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.name == RootName ? root.transform : FindDeep(root.transform, RootName);
                if (found != null)
                {
                    return found;
                }
            }

            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "仕掛けの置き場所を作る");
            var field = Object.FindObjectsByType<NavigationField>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.scene == scene);
            if (field != null && field.transform.parent != null)
            {
                Undo.SetTransformParent(go.transform, field.transform.parent, "仕掛けの置き場所を作る");
            }
            else
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            return go.transform;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>歯車を置く（回る部分・仮の円盤・棒の腕 1 本つき）。</summary>
        public static GimmickGear CreateGear(Vector2 position)
        {
            var go = new GameObject(UniqueName("Gear"));
            Undo.RegisterCreatedObjectUndo(go, "歯車を置く");
            Undo.SetTransformParent(go.transform, GetOrCreateRoot(), "歯車を置く");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var rotor = new GameObject(GimmickGear.RotorName);
            Undo.RegisterCreatedObjectUndo(rotor, "歯車を置く");
            rotor.transform.SetParent(go.transform, false);

            var gear = Undo.AddComponent<GimmickGear>(go);
            gear.rotor = rotor.transform;
            SetDiscPlaceholder(gear, true);
            AddArm(gear, 0f, 0f, gear.radius * 2f + 0.4f, 0.7f);
            return gear;
        }

        /// <summary>
        /// 腕（経路の壁）を足す。angle = 向き（度）、distance = 歯車の真ん中から腕の真ん中までの距離（0 なら真ん中を通る棒）。
        /// </summary>
        public static GimmickGearArm AddArm(GimmickGear gear, float angle, float distance, float length, float thickness)
        {
            var rotor = gear.Rotor;
            if (rotor == null)
            {
                return null;
            }

            var go = new GameObject(UniqueChildName(rotor, "Arm"));
            Undo.RegisterCreatedObjectUndo(go, "歯車に腕を足す");
            go.transform.SetParent(rotor, false);
            var arm = go.AddComponent<GimmickGearArm>();
            SetArmPose(arm, angle, distance);
            arm.SetSize(length, thickness);
            var visual = go.AddComponent<PlaceholderVisual>();
            visual.shape = PlaceholderShape.Square;
            visual.color = ArmColor;
            visual.sortingOrder = -1;
            visual.Apply();
            NavigationObstacle.NotifyChanged();
            return arm;
        }

        /// <summary>腕の向き（度）と、真ん中からの距離。</summary>
        public static void GetArmPose(GimmickGearArm arm, out float angle, out float distance)
        {
            var t = arm.transform;
            angle = t.localEulerAngles.z;
            Vector2 local = t.localPosition;
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            distance = Vector2.Dot(local, dir);
        }

        public static void SetArmPose(GimmickGearArm arm, float angle, float distance)
        {
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            arm.transform.localPosition = new Vector3(dir.x * distance, dir.y * distance, 0f);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>仮の円盤（回る部分の中の「Disc」）を出す・消す。</summary>
        public static void SetDiscPlaceholder(GimmickGear gear, bool on)
        {
            var rotor = gear.Rotor;
            if (rotor == null)
            {
                return;
            }

            var disc = rotor.Find(DiscName);
            if (!on)
            {
                if (disc != null)
                {
                    Undo.DestroyObjectImmediate(disc.gameObject);
                }

                return;
            }

            if (disc == null)
            {
                var go = new GameObject(DiscName);
                Undo.RegisterCreatedObjectUndo(go, "歯車の仮の見た目");
                go.transform.SetParent(rotor, false);
                disc = go.transform;
            }

            disc.localPosition = new Vector3(0f, 0f, 0.02f);
            disc.localScale = new Vector3(gear.radius * 2f, gear.radius * 2f, 1f);
            var visual = disc.GetComponent<PlaceholderVisual>();
            if (visual == null)
            {
                visual = Undo.AddComponent<PlaceholderVisual>(disc.gameObject);
            }
            visual.shape = PlaceholderShape.Circle;
            visual.color = new Color(GearColor.r, GearColor.g, GearColor.b, 0.55f);
            visual.sortingOrder = -2;
            visual.Apply();
        }

        /// <summary>腕の仮の見た目（四角）を出す・消す。</summary>
        public static void SetArmPlaceholder(GimmickGearArm arm, bool on)
        {
            var visual = arm.GetComponent<PlaceholderVisual>();
            if (on && visual == null)
            {
                visual = Undo.AddComponent<PlaceholderVisual>(arm.gameObject);
                visual.shape = PlaceholderShape.Square;
                visual.color = ArmColor;
                visual.sortingOrder = -1;
                visual.Apply();
            }
            else if (!on && visual != null)
            {
                var renderer = arm.GetComponent<SpriteRenderer>();
                Undo.DestroyObjectImmediate(visual);
                if (renderer != null)
                {
                    Undo.DestroyObjectImmediate(renderer);
                }
            }
        }

        /// <summary>
        /// 見た目のモデル（FBX・Prefab）を回る部分の中に置く（腕の中ではなく隣に。腕の大きさで伸びないように）。
        /// mayaUp: Maya など Y が上のモデルを、床（XY）に寝かせる（-90° 回す）。
        /// </summary>
        public static GameObject AttachModel(GimmickGear gear, GameObject model, bool mayaUp)
        {
            var rotor = gear.Rotor;
            if (rotor == null || model == null)
            {
                return null;
            }

            var instance = PrefabUtility.IsPartOfPrefabAsset(model) && model.transform.parent == null
                ? (GameObject)PrefabUtility.InstantiatePrefab(model, rotor)
                : Object.Instantiate(model, rotor);
            Undo.RegisterCreatedObjectUndo(instance, "歯車に見た目を付ける");
            instance.name = "Model_" + model.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = mayaUp ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            return instance;
        }

        /// <summary>今ある障害物（選んだ物）を動かせるようにする。作った数を返す。断った理由は reasons へ。</summary>
        public static List<GimmickMover> MakeMovers(IEnumerable<GameObject> targets, List<string> reasons)
        {
            var created = new List<GimmickMover>();
            foreach (var go in targets)
            {
                if (go == null)
                {
                    continue;
                }

                if (!CanMove(go, out var reason))
                {
                    reasons.Add($"{go.name}: {reason}");
                    continue;
                }

                var mover = Undo.AddComponent<GimmickMover>(go);
                mover.CaptureHome();
                EditorUtility.SetDirty(mover);
                created.Add(mover);
            }

            return created;
        }

        /// <summary>動かせる障害物か（経路の障害物・壊せる物で、スマッシュボールでも歯車の中でもない）。</summary>
        public static bool CanMove(GameObject go, out string reason)
        {
            if (go.GetComponent<GimmickMover>() != null)
            {
                reason = "もう動く障害物です";
                return false;
            }

            if (go.GetComponent<SmashBallModule>() != null || go.GetComponent<SmashBallFloater>() != null)
            {
                reason = "スマッシュボールは自分で浮いて動くので、付けられません";
                return false;
            }

            if (go.GetComponentInParent<GimmickGear>() != null)
            {
                reason = "歯車の中の物です（歯車と一緒に回ります）";
                return false;
            }

            if (go.GetComponent<GimmickGear>() != null)
            {
                reason = "歯車そのものです（歯車は「歯車」の欄で動かし方を決めます）";
                return false;
            }

            if (go.GetComponent<NavigationObstacle>() == null && go.GetComponent<DestructibleObstacle>() == null)
            {
                reason = "障害物（壁・地帯・壊せる物）ではありません";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>選んだ物を、歯車の回る部分の中へ入れる（一緒に回るようになる）。</summary>
        public static int MoveIntoRotor(GimmickGear gear, IEnumerable<GameObject> targets)
        {
            var rotor = gear.Rotor;
            var count = 0;
            if (rotor == null)
            {
                return 0;
            }

            foreach (var go in targets)
            {
                if (go == null || go == gear.gameObject || go.transform.IsChildOf(rotor) || gear.transform.IsChildOf(go.transform) ||
                    go.GetComponent<GimmickBehaviour>() != null || PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                {
                    continue;
                }

                Undo.SetTransformParent(go.transform, rotor, "歯車と一緒に回す");
                count++;
            }

            if (count > 0)
            {
                NavigationObstacle.NotifyChanged();
            }

            return count;
        }

        private static string UniqueName(string baseName)
        {
            var existing = new HashSet<string>(GimmickClock.FindAllInScenes().Select(g => g.name));
            for (var i = 0; ; i++)
            {
                var candidate = $"{baseName}_{i}";
                if (!existing.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        private static string UniqueChildName(Transform parent, string baseName)
        {
            for (var i = 0; ; i++)
            {
                var candidate = $"{baseName}_{i}";
                if (parent.Find(candidate) == null)
                {
                    return candidate;
                }
            }
        }
    }
}
