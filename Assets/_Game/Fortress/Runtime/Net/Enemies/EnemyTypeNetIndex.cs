using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 敵の種類(EnemyTypeDefinition)に全PCで共通の番号を振る。シーン内のウェーブ設定が使っている種類を、
    /// アセット名の順に並べる(同じビルドなら全PCで一致)。並びの指紋(<see cref="LayoutHash"/>)をHostと比べる。
    /// ウェーブに入っていない種類はビルドに含まれない場合があるので、番号を持たない(=同期できない)。
    /// </summary>
    public sealed class EnemyTypeNetIndex
    {
        private readonly List<EnemyTypeDefinition> _types = new();
        private readonly Dictionary<EnemyTypeDefinition, int> _indexOf = new();

        public int Count => _types.Count;
        public uint LayoutHash { get; private set; }

        public void Build()
        {
            _types.Clear();
            _indexOf.Clear();

            var unique = new HashSet<EnemyTypeDefinition>();
            foreach (var director in Object.FindObjectsByType<EnemySpawnDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (director.wave == null || director.wave.spawnEntries == null)
                {
                    continue;
                }

                foreach (var entry in director.wave.spawnEntries)
                {
                    if (entry != null && entry.enemyType != null)
                    {
                        unique.Add(entry.enemyType);
                    }
                }
            }

            _types.AddRange(unique);
            _types.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var names = new List<string>(_types.Count);
            for (var i = 0; i < _types.Count; i++)
            {
                _indexOf[_types[i]] = i;
                names.Add(_types[i].name);
            }

            LayoutHash = DestructibleNetIndex.ComputeLayoutHash(names);
        }

        public bool TryGetIndex(EnemyTypeDefinition type, out int index)
        {
            index = -1;
            return type != null && _indexOf.TryGetValue(type, out index);
        }

        public EnemyTypeDefinition Get(int index)
        {
            return index >= 0 && index < _types.Count ? _types[index] : null;
        }
    }
}
