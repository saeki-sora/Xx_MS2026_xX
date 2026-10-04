using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 視点の自動生成・映り込みチェックで使う「画面に入れたい物」の位置をシーンから集める(砲台・コア・敵の出現地点・経路フィールド)。
    /// </summary>
    public sealed class CameraScenePoints
    {
        public readonly struct TurretPoint
        {
            public readonly int PlayerIndex;
            public readonly Vector2 Position;

            public TurretPoint(int playerIndex, Vector2 position)
            {
                PlayerIndex = playerIndex;
                Position = position;
            }
        }

        public readonly List<TurretPoint> Turrets = new List<TurretPoint>();
        public readonly List<Vector2> Cores = new List<Vector2>();
        public readonly List<Vector2> SpawnPoints = new List<Vector2>();
        public Rect? NavigationArea { get; private set; }

        /// <summary>マップの中心。コア → 経路フィールド → 砲台の重心 → 原点 の順に決める。</summary>
        public Vector2 MapCenter { get; private set; }

        public static CameraScenePoints Collect()
        {
            var points = new CameraScenePoints();

            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                points.Turrets.Add(new TurretPoint(turret.playerIndex, turret.transform.position));
            }

            foreach (var core in Object.FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
            {
                points.Cores.Add(core.transform.position);
            }

            foreach (var spawn in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                points.SpawnPoints.Add(spawn.transform.position);
            }

            var field = Object.FindFirstObjectByType<NavigationField>();
            if (field != null)
            {
                points.NavigationArea = new Rect(field.areaCenter - field.areaSize * 0.5f, field.areaSize);
            }

            points.MapCenter = points.ComputeMapCenter();
            return points;
        }

        public bool TryGetTurret(int playerIndex, out Vector2 position)
        {
            foreach (var turret in Turrets)
            {
                if (turret.PlayerIndex == playerIndex)
                {
                    position = turret.Position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>自動フィットで画面に収める点。経路フィールドは広すぎることがあるので任意。</summary>
        public List<Vector2> GetFitPoints(bool includeSpawnPoints, bool includeNavigationArea)
        {
            var result = new List<Vector2>(Cores);
            foreach (var turret in Turrets)
            {
                result.Add(turret.Position);
            }

            if (includeSpawnPoints)
            {
                result.AddRange(SpawnPoints);
            }

            if (includeNavigationArea && NavigationArea.HasValue)
            {
                var area = NavigationArea.Value;
                result.Add(new Vector2(area.xMin, area.yMin));
                result.Add(new Vector2(area.xMin, area.yMax));
                result.Add(new Vector2(area.xMax, area.yMin));
                result.Add(new Vector2(area.xMax, area.yMax));
            }

            return result;
        }

        private Vector2 ComputeMapCenter()
        {
            if (Cores.Count > 0)
            {
                return Cores[0];
            }

            if (NavigationArea.HasValue)
            {
                return NavigationArea.Value.center;
            }

            if (Turrets.Count > 0)
            {
                var sum = Vector2.zero;
                foreach (var turret in Turrets)
                {
                    sum += turret.Position;
                }

                return sum / Turrets.Count;
            }

            return Vector2.zero;
        }
    }
}
