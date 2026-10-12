using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play 中の物 ⇔ 住所（UiPlayTarget）の変換。
    /// ・一覧の Prefab から作られた画面の中 → その Prefab ファイル（Prefab のルートからの道のり）
    /// ・シーンに置いた画面の中 → そのシーンの、同じ名前（screenId）の画面からの道のり
    /// ・[UI]（UiRoot）の下で画面の外 → そのシーンの [UI] からの道のり
    /// それ以外（UI と関係ない物）は扱わない。Play 中に名前を変えた物は、Play 前の名前で道のりを作る。
    /// </summary>
    public static class UiPlayLocator
    {
        private static readonly Dictionary<int, string> OriginalNames = new Dictionary<int, string>();
        private static readonly List<string> SiblingNames = new List<string>();
        private static readonly List<Component> Components = new List<Component>();

        /// <summary>Play が始まったとき、名前の控えを捨てる。</summary>
        public static void ResetNames() => OriginalNames.Clear();

        /// <summary>Play 中に名前が変わる前の名前を覚える（最初の1回だけ）。</summary>
        public static void RememberOriginalName(GameObject go, string name)
        {
            if (go != null && !OriginalNames.ContainsKey(go.GetInstanceID()))
            {
                OriginalNames[go.GetInstanceID()] = name;
            }
        }

        /// <summary>
        /// Play 中の物の住所を作る。UI と関係ない物なら false。
        /// UI の物でも書き戻せない場合（保存していないシーンなど）は true を返し、problem に理由を入れる。
        /// </summary>
        public static bool TryLocate(Object obj, out UiPlayTarget target, out string problem)
        {
            target = null;
            problem = "";
            var component = obj as Component;
            var go = component != null ? component.gameObject : obj as GameObject;
            // ファイルそのもの・Prefab の編集画面の中の物は、Play を止めても消えないので扱わない。
            if (go == null || EditorUtility.IsPersistent(go) || EditorSceneManager.IsPreviewSceneObject(go))
            {
                return false;
            }

            Transform anchor;
            string head;
            target = new UiPlayTarget();
            var screen = go.GetComponentInParent<UiScreen>(true);
            if (screen != null)
            {
                anchor = screen.transform;
                if (screen.SourcePrefab != null)
                {
                    target.destination = UiPlayDestination.Prefab;
                    target.assetPath = AssetDatabase.GetAssetPath(screen.SourcePrefab);
                    head = screen.Label;
                }
                else
                {
                    target.destination = UiPlayDestination.Scene;
                    target.assetPath = ScenePathOf(anchor);
                    target.anchorId = screen.screenId ?? "";
                    head = screen.Label;
                }
            }
            else
            {
                var root = go.GetComponentInParent<UiRoot>(true);
                if (root == null)
                {
                    target = null;
                    return false;
                }

                anchor = root.transform;
                target.destination = UiPlayDestination.Scene;
                target.assetPath = ScenePathOf(anchor);
                head = NameOf(anchor);
            }

            for (var t = go.transform; t != anchor && t != null; t = t.parent)
            {
                target.names.Insert(0, NameOf(t));
                target.indices.Insert(0, SameNameIndex(t));
            }

            if (component != null)
            {
                var type = component.GetType();
                target.componentType = type.AssemblyQualifiedName;
                target.componentIndex = SameTypeComponents(go, type).IndexOf(component);
                target.componentLabel = ObjectNames.GetInspectorTitle(component);
            }
            else
            {
                target.componentLabel = "GameObject";
            }

            target.where = UiPlayPath.Describe(head, target.names);
            if (target.names.Exists(n => n.EndsWith("(Clone)", StringComparison.Ordinal)))
            {
                problem = "Play 中に作られた部品（名前が「(Clone)」）なので、書き戻す先がありません。元になった部品（ひな形）を変えてください。";
            }
            else if (string.IsNullOrEmpty(target.assetPath))
            {
                problem = target.destination == UiPlayDestination.Prefab
                    ? "元の Prefab ファイルが見つかりません。"
                    : "シーンが保存されていないため、書き戻す先がありません。";
            }

            return true;
        }

        /// <summary>目印（Prefab のルート・画面・[UI]）から、道のりをたどって物を探す。無ければ null。</summary>
        public static GameObject FindGameObject(Transform anchor, UiPlayTarget target)
        {
            var current = anchor;
            for (var i = 0; i < target.names.Count && current != null; i++)
            {
                SiblingNames.Clear();
                foreach (Transform child in current)
                {
                    SiblingNames.Add(child.name);
                }

                var position = UiPlayPath.FindPosition(SiblingNames, target.names[i], i < target.indices.Count ? target.indices[i] : 0);
                current = position >= 0 ? current.GetChild(position) : null;
            }

            return current != null ? current.gameObject : null;
        }

        /// <summary>
        /// 物の中から、住所の部品を探す（GameObject そのものなら go）。
        /// addMissing なら、Play 中に足した部品（同じ型の次の番号）を足す。
        /// </summary>
        public static Object FindTarget(GameObject go, UiPlayTarget target, bool addMissing, bool undo, out string problem)
        {
            problem = "";
            if (target.IsGameObject)
            {
                return go;
            }

            var type = Type.GetType(target.componentType);
            if (type == null)
            {
                problem = $"部品の種類「{target.componentLabel}」が見つかりません（名前が変わったか、消されました）。";
                return null;
            }

            var list = SameTypeComponents(go, type);
            if (target.componentIndex < list.Count)
            {
                return list[target.componentIndex];
            }

            if (addMissing && target.componentIndex == list.Count)
            {
                var added = undo ? Undo.AddComponent(go, type) : go.AddComponent(type);
                if (added != null)
                {
                    return added;
                }
            }

            problem = $"部品「{target.componentLabel}」が見つかりません。";
            return null;
        }

        private static string ScenePathOf(Transform t)
        {
            var root = t.GetComponentInParent<UiRoot>(true);
            return root != null && !string.IsNullOrEmpty(root.OriginScenePath) ? root.OriginScenePath : t.gameObject.scene.path;
        }

        private static string NameOf(Transform t) =>
            OriginalNames.TryGetValue(t.gameObject.GetInstanceID(), out var name) ? name : t.name;

        private static int SameNameIndex(Transform t)
        {
            if (t.parent == null)
            {
                return 0;
            }

            SiblingNames.Clear();
            foreach (Transform child in t.parent)
            {
                SiblingNames.Add(NameOf(child));
            }

            return UiPlayPath.IndexAmongSameName(SiblingNames, t.GetSiblingIndex());
        }

        private static List<Component> SameTypeComponents(GameObject go, Type type)
        {
            var result = new List<Component>();
            go.GetComponents(Components);
            foreach (var c in Components)
            {
                if (c != null && c.GetType() == type)
                {
                    result.Add(c);
                }
            }

            return result;
        }
    }
}
