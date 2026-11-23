using System.Collections.Generic;
using DDrive.Foundation.Data;
using DDrive.Runtime.Audio;
using UnityEditor;

namespace MS2026.SoundStudio
{
    /// <summary>プロジェクトにある D-Drive の SE・BGM のデータを、番号から引けるようにした一覧。</summary>
    public sealed class SoundLibrary
    {
        private readonly Dictionary<ulong, SeData> _se = new Dictionary<ulong, SeData>();
        private readonly Dictionary<ulong, BgmData> _bgm = new Dictionary<ulong, BgmData>();
        private readonly List<SeData> _allSe = new List<SeData>();
        private readonly List<BgmData> _allBgm = new List<BgmData>();

        public IReadOnlyList<SeData> AllSe => _allSe;
        public IReadOnlyList<BgmData> AllBgm => _allBgm;

        public void Reload()
        {
            Load(_se, _allSe);
            Load(_bgm, _allBgm);
        }

        public SeData Se(ulong id) => id != 0 && _se.TryGetValue(id, out var data) ? data : null;
        public BgmData Bgm(ulong id) => id != 0 && _bgm.TryGetValue(id, out var data) ? data : null;

        /// <summary>画面に出す音の名前（表示名、無ければファイル名）。</summary>
        public static string NameOf(AssetDataBase data)
        {
            if (data == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(data.DisplayName) ? data.name : data.DisplayName;
        }

        private static void Load<T>(Dictionary<ulong, T> map, List<T> all) where T : AssetDataBase
        {
            map.Clear();
            all.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var data = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null)
                {
                    continue;
                }

                all.Add(data);
                if (data.Id != 0)
                {
                    map[data.Id] = data;
                }
            }

            all.Sort((a, b) => string.CompareOrdinal(NameOf(a), NameOf(b)));
        }
    }
}
