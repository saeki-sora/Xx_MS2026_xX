using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// モデルの目印（MARK_Core / MARK_Turret1〜4 / MARK_Spawn...）に、シーンのコア・砲台・湧き位置を合わせる。
    /// 高さ（Z）は変えず、床の上の位置（X・Y）だけを動かす。
    /// </summary>
    public static class StageMarkerAligner
    {
        /// <summary>合わせた物の説明の一覧を返す（表示用）。</summary>
        public static List<string> Align(IEnumerable<(StageMarkerKind kind, int index, Vector3 position, string name)> markers, bool includeSpawns)
        {
            var log = new List<string>();
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None);
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            var spawns = new List<EnemySpawnPoint>(Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None));
            spawns.Sort((a, b) => string.CompareOrdinal(a.ResolvedId, b.ResolvedId));
            var spawnIndex = 0;

            Undo.SetCurrentGroupName("目印に合わせる");
            foreach (var marker in markers)
            {
                switch (marker.kind)
                {
                    case StageMarkerKind.Core when core != null:
                        Move(core.transform, marker.position);
                        log.Add($"コア → {marker.name}");
                        break;
                    case StageMarkerKind.Turret:
                        foreach (var turret in turrets)
                        {
                            if (turret.playerIndex == marker.index)
                            {
                                Move(turret.transform, marker.position);
                                log.Add($"P{marker.index + 1}の砲台 → {marker.name}");
                            }
                        }

                        break;
                    case StageMarkerKind.Spawn when includeSpawns && spawnIndex < spawns.Count:
                        Move(spawns[spawnIndex].transform, marker.position);
                        log.Add($"湧き位置 {spawns[spawnIndex].ResolvedId} → {marker.name}");
                        spawnIndex++;
                        break;
                }
            }

            if (log.Count > 0)
            {
                NavigationObstacle.NotifyChanged();
                StageSceneService.MarkSceneDirty();
            }

            return log;
        }

        private static void Move(Transform t, Vector3 position)
        {
            Undo.RecordObject(t, "目印に合わせる");
            t.position = new Vector3(position.x, position.y, t.position.z);
        }
    }
}
