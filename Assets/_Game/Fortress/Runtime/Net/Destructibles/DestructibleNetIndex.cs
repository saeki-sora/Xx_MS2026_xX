using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// シーン上の破壊可能物に、全PCで共通の番号を振る。番号は「シーン内の階層パス(名前+兄弟順)」の並びで決めるので、
    /// 同じビルド・同じシーンなら全PCで一致する。並びの指紋(<see cref="LayoutHash"/>)をHostと比べ、違えば同期しない。
    /// 隠れている(ローテーション待機中の)スマッシュボールも含める。
    /// </summary>
    public sealed class DestructibleNetIndex
    {
        private readonly List<DestructibleObstacle> _items = new();
        private readonly Dictionary<DestructibleObstacle, int> _indexOf = new();

        public IReadOnlyList<DestructibleObstacle> Items => _items;
        public int Count => _items.Count;
        public uint LayoutHash { get; private set; }

        public void Build()
        {
            _items.Clear();
            _indexOf.Clear();

            var found = UnityEngine.Object.FindObjectsByType<DestructibleObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var keyed = new List<(string path, DestructibleObstacle obstacle)>(found.Length);
            foreach (var obstacle in found)
            {
                // Prefabアセット等、シーンに属さない物は除く。
                if (obstacle.gameObject.scene.IsValid())
                {
                    keyed.Add((HierarchyPath(obstacle.transform), obstacle));
                }
            }

            keyed.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            var paths = new List<string>(keyed.Count);
            foreach (var (path, obstacle) in keyed)
            {
                _indexOf[obstacle] = _items.Count;
                _items.Add(obstacle);
                paths.Add(path);
            }

            LayoutHash = ComputeLayoutHash(paths);
        }

        public bool TryGetIndex(DestructibleObstacle obstacle, out int index)
        {
            return _indexOf.TryGetValue(obstacle, out index);
        }

        public DestructibleObstacle Get(int index)
        {
            return index >= 0 && index < _items.Count ? _items[index] : null;
        }

        /// <summary>並び(パスの一覧)の指紋。32bit FNV-1a。</summary>
        public static uint ComputeLayoutHash(IReadOnlyList<string> orderedPaths)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var path in orderedPaths)
                {
                    foreach (var b in Encoding.UTF8.GetBytes(path))
                    {
                        hash = (hash ^ b) * 16777619u;
                    }

                    hash = (hash ^ 0x0Au) * 16777619u; // 区切り
                }

                return hash;
            }
        }

        // 例: "Obstacles#0/Crate#2"。#の後ろは「同じ名前の兄弟の中で何番目か」。
        // 兄弟順そのものは、実行中に生成・削除される無関係なオブジェクト(破片の演出、DontDestroyOnLoadへ移る物など)で
        // PCごとにずれうるため使わない。同名の兄弟どうしの前後関係は、それらの影響を受けない。
        private static string HierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (var t = transform; t != null; t = t.parent)
            {
                parts.Push($"{t.name}#{SameNameRank(t)}");
            }

            return string.Join("/", parts);
        }

        private static int SameNameRank(Transform transform)
        {
            var rank = 0;
            var siblingIndex = transform.GetSiblingIndex();
            var parent = transform.parent;
            if (parent != null)
            {
                for (var i = 0; i < siblingIndex; i++)
                {
                    if (parent.GetChild(i).name == transform.name)
                    {
                        rank++;
                    }
                }

                return rank;
            }

            foreach (var root in transform.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform != transform && root.name == transform.name && root.transform.GetSiblingIndex() < siblingIndex)
                {
                    rank++;
                }
            }

            return rank;
        }
    }
}
