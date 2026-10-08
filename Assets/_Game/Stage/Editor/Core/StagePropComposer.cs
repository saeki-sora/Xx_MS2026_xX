using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景オブジェクトの組み立て役: 子の構成（Visual / Footprint）をそろえ、役割に合わせて部品を付け外しし、
    /// 通れない範囲を作り直し、モデルを差し替える。すべて Undo に対応する。
    /// </summary>
    public static class StagePropComposer
    {
        public const string VisualName = "Visual";
        public const string FootprintName = "Footprint";

        // 壊せる壁の部品。外すときは「他の部品に必要とされている物」から順に外す。
        private static readonly Type[] DestructibleParts =
        {
            typeof(StagePropDestructibleView),
            typeof(DestructibleObstacle),
            typeof(DestructibleCollision),
            typeof(DestructibleVisual),
            typeof(DestructibleFeedback),
            typeof(DestructibleDropper),
            typeof(DestructibleLinker),
            typeof(DestructibleHealthBar)
        };

        /// <summary>
        /// モデル（シーンに置いた実体）から背景オブジェクトを作る。足元（モデルの真下の床、z=0）を根元にする。
        /// </summary>
        public static StageProp CreateFromModel(Transform parent, string name, GameObject model, StagePropRole role, GameObject sourceAsset, string sourceNodePath)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "背景オブジェクトを作成");
            root.transform.SetParent(parent, false);
            root.transform.position = FloorCenterOf(model);

            var prop = Undo.AddComponent<StageProp>(root);
            prop.sourceModel = sourceAsset;
            prop.sourceNodePath = sourceNodePath;
            EnsureStructure(prop);

            Undo.SetTransformParent(model.transform, prop.visualRoot, "背景オブジェクトを作成");
            prop.modelRoot = model.transform;
            SetRole(prop, role);
            RebuildFootprint(prop);
            return prop;
        }

        /// <summary>子の構成と、どの役割でも使う部品をそろえる（足りない物だけ足す）。</summary>
        public static void EnsureStructure(StageProp prop)
        {
            var t = prop.transform;
            if (prop.visualRoot == null)
            {
                prop.visualRoot = FindOrCreateChild(t, VisualName);
            }

            if (prop.footprint == null)
            {
                var footprintTransform = FindOrCreateChild(t, FootprintName);
                var created = !footprintTransform.TryGetComponent<StagePropFootprint>(out var footprint);
                prop.footprint = created ? Undo.AddComponent<StagePropFootprint>(footprintTransform.gameObject) : footprint;
                if (created)
                {
                    // 足した瞬間の PolygonCollider2D は Unity の既定の五角形なので消しておく（形はモデルから作る）。
                    prop.footprint.ApplyPaths(System.Array.Empty<Vector2[]>());
                }
            }

            AddIfMissing<StagePropRenderer>(prop.gameObject);
            AddIfMissing<StagePropHeat>(prop.gameObject);
            AddIfMissing<StagePropWobble>(prop.gameObject);
            AddIfMissing<StagePropOcclusion>(prop.gameObject);
            EditorUtility.SetDirty(prop);
        }

        /// <summary>役割を変える。壊せる壁なら破壊可能物の部品を付け、それ以外なら外す。</summary>
        public static bool SetRole(StageProp prop, StagePropRole role, Action<string> onError = null)
        {
            EnsureStructure(prop);
            var go = prop.gameObject;

            if (role == StagePropRole.DestructibleWall)
            {
                AddDestructibleParts(prop);
            }
            else if (go.GetComponent<DestructibleObstacle>() != null)
            {
                if (go.GetComponent<SmashBallModule>() != null)
                {
                    onError?.Invoke("スマッシュボールが付いているため「壊せる壁」から変えられません。先に要塞デザイナーのスマッシュボールタブで外してください。");
                    return false;
                }

                foreach (var type in DestructibleParts)
                {
                    var component = go.GetComponent(type);
                    if (component != null)
                    {
                        Undo.DestroyObjectImmediate(component);
                    }
                }
            }

            if (role.NeedsFootprint())
            {
                AddIfMissing<NavigationObstacle>(go);
            }

            Undo.RecordObject(prop, "役割を変更");
            prop.role = role;
            prop.ApplyRoleState();
            EditorUtility.SetDirty(prop);
            NavigationObstacle.NotifyChanged();
            return true;
        }

        /// <summary>モデルの形から通れない範囲を作り直す（手で直した印は外れる）。</summary>
        public static FootprintBuilder.Report RebuildFootprint(StageProp prop)
        {
            EnsureStructure(prop);
            var footprint = prop.footprint;
            Undo.RecordObjects(new UnityEngine.Object[] { footprint, footprint.Collider }, "通れない範囲を作り直す");
            var report = footprint.Rebuild(prop.modelRoot != null ? prop.modelRoot : prop.visualRoot);
            EditorUtility.SetDirty(footprint);
            EditorUtility.SetDirty(footprint.Collider);
            prop.ApplyRoleState();
            return report;
        }

        /// <summary>モデルを差し替える。位置・役割・設定はそのまま、通れない範囲は作り直す。</summary>
        public static void ReplaceModel(StageProp prop, GameObject newModel, GameObject sourceAsset, string sourceNodePath)
        {
            EnsureStructure(prop);
            var old = prop.modelRoot;
            if (old != null)
            {
                newModel.transform.SetPositionAndRotation(old.position, old.rotation);
                newModel.transform.localScale = old.lossyScale;
            }

            Undo.SetTransformParent(newModel.transform, prop.visualRoot, "モデルを差し替え");
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            Undo.RecordObject(prop, "モデルを差し替え");
            prop.modelRoot = newModel.transform;
            prop.sourceModel = sourceAsset;
            prop.sourceNodePath = sourceNodePath;
            if (prop.TryGetComponent<StagePropRenderer>(out var propRenderer))
            {
                propRenderer.CollectRenderers();
            }
            RebuildFootprint(prop);
        }

        /// <summary>根元（揺れの支点・回転の中心）を、見た目の真下の床へ移す。子の見た目の位置は変わらない。</summary>
        public static void RecenterPivot(StageProp prop)
        {
            var target = prop.modelRoot != null ? prop.modelRoot.gameObject : prop.visualRoot.gameObject;
            var center = FloorCenterOf(target);
            var delta = center - prop.transform.position;
            if (delta.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var children = new List<Transform>();
            foreach (Transform child in prop.transform)
            {
                children.Add(child);
            }

            Undo.RecordObject(prop.transform, "根元を中心へ");
            foreach (var child in children)
            {
                Undo.RecordObject(child, "根元を中心へ");
            }

            prop.transform.position = center;
            foreach (var child in children)
            {
                child.position -= delta;
            }

            // 通れない範囲は根元の子なので一緒に動いてしまう。形を作り直して元の場所に合わせる。
            RebuildFootprint(prop);
        }

        /// <summary>見た目全体の真下の床の点（XYは見た目の中心、Zは0）。</summary>
        public static Vector3 FloorCenterOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                var p = model.transform.position;
                return new Vector3(p.x, p.y, 0f);
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            return new Vector3(bounds.center.x, bounds.center.y, 0f);
        }

        private static void AddDestructibleParts(StageProp prop)
        {
            var go = prop.gameObject;
            var obstacle = AddIfMissing<DestructibleObstacle>(go);

            // 当たり判定は Footprint の形を使う（自動の四角は作らない）。見た目は3Dモデルを StagePropDestructibleView が動かす。
            var so = new SerializedObject(obstacle);
            so.FindProperty("settings.collision.shape").enumValueIndex = (int)DestructibleShape.Custom;
            so.FindProperty("settings.visual.darkenWithDamage").boolValue = false;
            so.FindProperty("settings.visual.hitShake").floatValue = 0f;
            so.FindProperty("settings.visual.destroyedGhostAlpha").floatValue = 0f;
            so.FindProperty("settings.visual.controlPlaceholderColor").boolValue = false;
            so.ApplyModifiedProperties();

            // 部品を付けた瞬間に自動で作られた四角い当たり判定を消す（根元の物だけ。Footprint の形は残す）。
            foreach (var collider in go.GetComponents<Collider2D>())
            {
                Undo.DestroyObjectImmediate(collider);
            }

            AddIfMissing<StagePropDestructibleView>(go);
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "背景オブジェクトの部品を作成");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // GetComponent は見つからないとき編集中は「偽のnull」を返すので、?? ではなく TryGetComponent で調べる。
        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var existing) ? existing : Undo.AddComponent<T>(go);
        }
    }
}
