using System.Collections.Generic;

namespace MS2026.Fortress
{
    /// <summary>
    /// 初めての人向けの「NEW ENEMY」（その試合で初めて出てくる敵の紹介）を選ぶ（Unity に頼らない計算。テストあり）。
    /// ウェーブ index の行に出てくる種類のうち、それより前のウェーブに1度も出ていない物（紹介文のある物だけ）。
    /// </summary>
    public static class WaveIntro
    {
        public static List<EnemyTypeDefinition> NewTypes(IReadOnlyList<WaveDefinition> waves, int index)
        {
            var result = new List<EnemyTypeDefinition>();
            if (waves == null || index < 0 || index >= waves.Count || waves[index]?.spawnEntries == null)
            {
                return result;
            }

            var seen = new HashSet<EnemyTypeDefinition>();
            for (var i = 0; i < index; i++)
            {
                if (waves[i]?.spawnEntries == null)
                {
                    continue;
                }

                foreach (var entry in waves[i].spawnEntries)
                {
                    if (entry?.enemyType != null)
                    {
                        seen.Add(entry.enemyType);
                    }
                }
            }

            foreach (var entry in waves[index].spawnEntries)
            {
                var type = entry?.enemyType;
                if (type != null && !seen.Contains(type) && !result.Contains(type) && !string.IsNullOrWhiteSpace(type.introText))
                {
                    result.Add(type);
                }
            }

            return result;
        }
    }
}
