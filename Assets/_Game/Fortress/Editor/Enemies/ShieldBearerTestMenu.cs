using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 盾持ちスピキ（<c>Presets/ShieldBearer_EnemyType</c>）を Play 中に湧かせて試すメニュー（まだウェーブには入れていない。2026-10-29）。
    /// お供も一緒に湧く。ネット対戦の Client では湧かせられない（敵は Host が湧かせる）。
    /// </summary>
    internal static class ShieldBearerTestMenu
    {
        private const string AssetPath = "Assets/_Game/Fortress/Presets/ShieldBearer_EnemyType.asset";
        private const string MenuRoot = "Tools/要塞/盾持ちスピキ/";

        [MenuItem(MenuRoot + "1体湧かせる（Play中・湧き位置のどれか）", priority = 300)]
        private static void SpawnOne()
        {
            Spawn(false);
        }

        [MenuItem(MenuRoot + "全ての湧き位置に1体ずつ（Play中）", priority = 301)]
        private static void SpawnEverywhere()
        {
            Spawn(true);
        }

        [MenuItem(MenuRoot + "1体湧かせる（Play中・湧き位置のどれか）", true)]
        [MenuItem(MenuRoot + "全ての湧き位置に1体ずつ（Play中）", true)]
        private static bool CanSpawn()
        {
            return Application.isPlaying && SwarmSystem.Current != null && !SwarmSystem.Current.IsReplica;
        }

        [MenuItem(MenuRoot + "設定を選ぶ", priority = 320)]
        private static void SelectAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(AssetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void Spawn(bool everywhere)
        {
            var type = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(AssetPath);
            var swarm = SwarmSystem.Current;
            if (type == null || swarm == null)
            {
                Debug.LogWarning($"[盾持ち] {AssetPath} か群衆（SwarmSystem）が見つかりません。");
                return;
            }

            var points = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            if (points.Length == 0)
            {
                swarm.Spawn(type, new Vector2(0f, 10f));
                return;
            }

            if (!everywhere)
            {
                swarm.Spawn(type, points[Random.Range(0, points.Length)].transform.position);
                return;
            }

            foreach (var point in points)
            {
                swarm.Spawn(type, point.transform.position);
            }
        }
    }
}
