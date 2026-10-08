using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// タイトル画面の部品の「調整した数値」を写し取って、別の（同じ場所・同じ種類の）部品に戻す。
    /// 組み立て直し（古い [Title] → 新しい [Title]）と、再生中に変えた値を再生後に残すのに使う。
    /// 場所は「基準のオブジェクトからの階層パス」で対応させる。部品どうしのつながり（参照）は戻す先のものを保つ。
    /// </summary>
    [Serializable]
    internal sealed class TitleValueSnapshot
    {
        // 数値を写し取る部品の種類（カメラ・UI・ShaderFX の部品は対象外）。
        private static readonly Type[] CopiedTypes =
        {
            typeof(TitleScreenController), typeof(TitleOpening), typeof(TitleElementMotion), typeof(TitleSpriteFit),
            typeof(TitleCameraMotion), typeof(ParticleSystem),
        };

        [Serializable]
        private sealed class ComponentEntry
        {
            public string path;
            public string type;
            public int index;
            public string json;
        }

        [Serializable]
        private sealed class TransformEntry
        {
            public string path;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        [SerializeField] private List<ComponentEntry> components = new();
        [SerializeField] private List<TransformEntry> transforms = new();

        public int Count => components.Count + transforms.Count;

        /// <param name="root">基準のオブジェクト。パスはここから数える（root 自身は ""）。</param>
        /// <param name="includeTransforms">位置・向き・大きさも写すか（UI の RectTransform は除く）。</param>
        public static TitleValueSnapshot Capture(Transform root, bool includeTransforms)
        {
            var snapshot = new TitleValueSnapshot();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = PathOf(root, t);
                if (includeTransforms && t is not RectTransform && t != root)
                {
                    snapshot.transforms.Add(new TransformEntry { path = path, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
                }

                foreach (var type in CopiedTypes)
                {
                    var found = t.GetComponents(type);
                    for (var i = 0; i < found.Length; i++)
                    {
                        snapshot.components.Add(new ComponentEntry { path = path, type = type.FullName, index = i, json = EditorJsonUtility.ToJson(found[i]) });
                    }
                }
            }
            return snapshot;
        }

        /// <summary>同じパス・同じ種類の部品に数値を戻す。戻した数を返す。</summary>
        public int Apply(Transform root, string undoName)
        {
            var applied = 0;
            foreach (var entry in transforms)
            {
                var t = Find(root, entry.path);
                if (t == null) continue;
                Undo.RecordObject(t, undoName);
                t.localPosition = entry.position;
                t.localRotation = entry.rotation;
                t.localScale = entry.scale;
                applied++;
            }

            foreach (var entry in components)
            {
                var t = Find(root, entry.path);
                var type = CopiedTypes.FirstOrDefault(x => x.FullName == entry.type);
                if (t == null || type == null) continue;
                var found = t.GetComponents(type);
                if (entry.index >= found.Length) continue;
                Undo.RecordObject(found[entry.index], undoName);
                OverwriteKeepingReferences(found[entry.index], entry.json);
                applied++;
            }
            return applied;
        }

        /// <summary>
        /// 数値だけを json の内容で上書きし、オブジェクトへの参照（つながり）と参照の配列の長さは今のものを保つ。
        /// </summary>
        private static void OverwriteKeepingReferences(UnityEngine.Object target, string json)
        {
            var refs = new List<(string path, UnityEngine.Object value)>();
            var before = new SerializedObject(target);
            var it = before.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.editable) refs.Add((it.propertyPath, it.objectReferenceValue));
            }

            var arraySizes = new List<(string path, int size)>();
            foreach (var arrayPath in refs.Select(r => r.path).Where(p => p.Contains(".Array.data["))
                         .Select(p => p.Substring(0, p.LastIndexOf(".Array.data[", StringComparison.Ordinal))).Distinct())
            {
                var array = before.FindProperty(arrayPath);
                if (array != null && array.isArray) arraySizes.Add((arrayPath, array.arraySize));
            }

            EditorJsonUtility.FromJsonOverwrite(json, target);

            var after = new SerializedObject(target);
            foreach (var (path, size) in arraySizes)
            {
                var array = after.FindProperty(path);
                if (array != null && array.isArray) array.arraySize = size;
            }
            foreach (var (path, value) in refs)
            {
                var prop = after.FindProperty(path);
                if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference) prop.objectReferenceValue = value;
            }
            after.ApplyModifiedPropertiesWithoutUndo();
        }

        public static string PathOf(Transform root, Transform t)
        {
            if (t == root) return "";
            var names = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) names.Add(x.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static Transform Find(Transform root, string path) => path.Length == 0 ? root : root.Find(path);
    }
}
