using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>ストレステスト。指定した敵を大量に一斉投入／連続投入して、負荷や押し合いの見た目を確認する。</summary>
    public sealed class SwarmStressPanel
    {
        private EnemyTypeDefinition _enemyType;
        private int _burstCount = 20000;
        private float _burstRadius = 6f;
        private float _perSecond = 1000f;

        public void Draw(SwarmSystem system)
        {
            EditorGUILayout.LabelField("ストレステスト（Play中のみ）", EditorStyles.miniBoldLabel);

            if (_enemyType == null)
            {
                _enemyType = SwarmTools.FindAllEnemyTypes().FirstOrDefault();
            }

            _enemyType = (EnemyTypeDefinition)EditorGUILayout.ObjectField(
                new GUIContent("投入する敵の種類"), _enemyType, typeof(EnemyTypeDefinition), false);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入ると、ここから敵を大量に投入して負荷を測れます。", MessageType.Info);
                return;
            }

            if (_enemyType == null)
            {
                return;
            }

            _burstCount = EditorGUILayout.IntSlider(new GUIContent("一斉投入の数"), _burstCount, 100, 50000);
            _burstRadius = EditorGUILayout.Slider(new GUIContent("投入範囲の半径"), _burstRadius, 0.5f, 20f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("範囲の中心に一斉投入", "経路フィールドの中心に円状にばらまきます。")))
                {
                    var spawned = system.SpawnBurst(_enemyType, GetAreaCenter(), _burstRadius, _burstCount);
                    Debug.Log($"[Swarm] {spawned}体を一斉投入しました。");
                }

                if (GUILayout.Button(new GUIContent("湧き位置から均等に投入", "全ての湧き位置へ均等に振り分けて投入します。")))
                {
                    SpawnFromSpawnPoints(system);
                }

                if (GUILayout.Button("全ての敵を削除"))
                {
                    system.ClearAll();
                }
            }

            DrawContinuous(system);
        }

        private void DrawContinuous(SwarmSystem system)
        {
            EditorGUILayout.Space(4);
            var spawner = system.GetComponent<SwarmStressSpawner>();
            var isActive = spawner != null && spawner.active;

            _perSecond = EditorGUILayout.Slider(new GUIContent("連続投入 (体/秒)"), _perSecond, 10f, 10000f);

            var label = isActive ? "連続投入を停止" : "湧き位置から連続投入を開始";
            if (!GUILayout.Button(label))
            {
                if (spawner != null)
                {
                    spawner.perSecond = _perSecond;
                }

                return;
            }

            if (spawner == null)
            {
                spawner = system.gameObject.AddComponent<SwarmStressSpawner>();
            }

            spawner.enemyType = _enemyType;
            spawner.perSecond = _perSecond;
            spawner.useSpawnPoints = true;
            spawner.active = !isActive;
        }

        private void SpawnFromSpawnPoints(SwarmSystem system)
        {
            var points = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            if (points.Length == 0)
            {
                Debug.LogWarning("[Swarm] 湧き位置(EnemySpawnPoint)がシーンにありません。");
                return;
            }

            var perPoint = Mathf.Max(1, _burstCount / points.Length);
            var total = 0;
            foreach (var point in points)
            {
                total += system.SpawnBurst(_enemyType, point.transform.position, 0.5f, perPoint);
            }

            Debug.Log($"[Swarm] 湧き位置{points.Length}箇所から合計{total}体を投入しました。");
        }

        private static Vector2 GetAreaCenter()
        {
            var field = Object.FindFirstObjectByType<NavigationField>();
            return field != null ? field.areaCenter : Vector2.zero;
        }
    }
}
