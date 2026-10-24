using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>敵が湧く場所1つ（ルートの始まり）。</summary>
    public readonly struct WaveRouteSpawn
    {
        public WaveRouteSpawn(string id, Vector2 position, int enemies)
        {
            Id = id;
            Position = position;
            Enemies = enemies;
        }

        public string Id { get; }

        public Vector2 Position { get; }

        /// <summary>このウェーブの1周で、ここから出る数。</summary>
        public int Enemies { get; }
    }

    /// <summary>敵の通り道1本（湧く場所 → コア）。</summary>
    public sealed class WaveRoute
    {
        public readonly List<Vector2> points = new List<Vector2>();
        public string spawnId;
        public int enemies;
        public float width;
        public float length;

        /// <summary>コアまでたどり着ける道か（行き止まりなら false。線は途中まで）。</summary>
        public bool reachesGoal;
    }

    /// <summary>
    /// 次のウェーブの敵のルート（アークナイツの赤い線のように、発表の間に見せる）。2026-10-24。
    /// 各PCが自分で計算する（経路のマップは全員同じ：壊れた物の状態も同期済み）ので、通信はしない。
    /// 線を描くのは <see cref="WaveRouteView"/>、画面の外の湧き場所の矢印は UI 側（WaveEdgeMarkers）。どちらもここを読む。
    /// </summary>
    public static class WaveRoutes
    {
        public const float MinWidth = 0.28f;
        public const float MaxWidth = 0.85f;

        private static readonly List<WaveRoute> RouteList = new List<WaveRoute>();
        private static readonly List<WaveRouteSpawn> SpawnList = new List<WaveRouteSpawn>();
        private static readonly List<Vector2> Buffer = new List<Vector2>();

        public static IReadOnlyList<WaveRoute> Routes => RouteList;

        public static IReadOnlyList<WaveRouteSpawn> Spawns => SpawnList;

        /// <summary>今、どのウェーブのルートか（無ければ −1）。</summary>
        public static int WaveIndex { get; private set; } = -1;

        /// <summary>見せている濃さ（0〜1。線の係が書く。画面のふちの矢印も同じ濃さで出す）。</summary>
        public static float Alpha { get; internal set; }

        /// <summary>計画の index のウェーブのルートを作って、見せる用に覚える（ゲーム中）。</summary>
        public static void Build(WavePlan plan, int index, EnemySpawnDirector director)
        {
            Clear();
            if (Compute(plan, index, director, EnemyNavigation.Current as NavigationField, RouteList, SpawnList))
            {
                WaveIndex = index;
            }
        }

        /// <summary>
        /// 計画の index のウェーブのルートを routes / spawns に作る（覚えない。要塞デザイナーの Scene ビューの表示にも使う）。
        /// field が null なら、湧く場所からコアへまっすぐ。
        /// </summary>
        public static bool Compute(WavePlan plan, int index, EnemySpawnDirector director, NavigationField field, List<WaveRoute> routes, List<WaveRouteSpawn> spawns)
        {
            routes.Clear();
            spawns.Clear();
            var wave = plan != null ? plan.Get(index) : null;
            if (wave == null || director == null)
            {
                return false;
            }

            var goal = FindGoal();

            // 湧く場所 × 通り方（敵の種類の大きさ）ごとに1本。数は1周の合計。
            var byRoute = new Dictionary<(string, NavigationProfile), int>();
            var bySpawn = new Dictionary<string, int>();
            foreach (var entry in wave.spawnEntries)
            {
                if (entry == null || entry.enemyType == null || entry.count <= 0 || string.IsNullOrEmpty(entry.spawnPointId))
                {
                    continue;
                }

                var key = (entry.spawnPointId, entry.enemyType.navigationProfile);
                byRoute.TryGetValue(key, out var count);
                byRoute[key] = count + entry.count;
                bySpawn.TryGetValue(entry.spawnPointId, out var spawnCount);
                bySpawn[entry.spawnPointId] = spawnCount + entry.count;
            }

            foreach (var pair in bySpawn)
            {
                if (director.TryGetSpawnPoint(pair.Key, out var point))
                {
                    spawns.Add(new WaveRouteSpawn(pair.Key, point.transform.position, pair.Value));
                }
            }

            foreach (var pair in byRoute)
            {
                if (!director.TryGetSpawnPoint(pair.Key.Item1, out var point))
                {
                    continue;
                }

                var route = new WaveRoute { spawnId = pair.Key.Item1, enemies = pair.Value };
                var start = (Vector2)point.transform.position;
                if (field != null)
                {
                    route.reachesGoal = field.GetResult(pair.Key.Item2).TracePath(start, Buffer);
                    route.points.AddRange(WaveRouteShapes.Smooth(WaveRouteShapes.Simplify(Buffer, 0.25f), 2));
                }
                else
                {
                    // 経路のマップが無いステージ: まっすぐコアへ。
                    route.reachesGoal = true;
                    route.points.Add(start);
                    route.points.Add(goal);
                }

                if (route.points.Count < 2)
                {
                    continue;
                }

                route.length = WaveRouteShapes.Length(route.points);
                route.width = WaveRouteShapes.WidthFor(route.enemies, MinWidth, MaxWidth);
                routes.Add(route);
            }

            return true;
        }

        public static void Clear()
        {
            RouteList.Clear();
            SpawnList.Clear();
            WaveIndex = -1;
            Alpha = 0f;
        }

        private static Vector2 FindGoal()
        {
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            return core != null ? (Vector2)core.transform.position : Vector2.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
        }
    }
}
