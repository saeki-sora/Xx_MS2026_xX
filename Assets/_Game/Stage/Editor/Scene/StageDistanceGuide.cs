using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景から近くの砲台・コア・湧き位置までの距離（近すぎると赤）。
    /// <see cref="Measure"/> で測り、シーンビュー（<see cref="Draw"/>）と配置ページのゲーム画面・右の欄が同じ結果を使う。
    /// 通れない範囲がある物はその輪郭から、無い物（飾り・床）は見た目の箱の床の四角から測る。
    /// </summary>
    public static class StageDistanceGuide
    {
        private const double TargetScanInterval = 1.0;

        private static readonly List<(string label, Vector2 position)> Targets = new List<(string, Vector2)>();
        private static double _nextScan;
        private static GUIStyle _label;

        /// <summary>1つの距離（target=砲台などの位置、closest=背景の上の一番近い点、distance=0なら重なっている）。</summary>
        public struct Reading
        {
            public string Label;
            public Vector2 Target;
            public Vector2 Closest;
            public float Distance;

            public bool Overlaps => Distance <= 0f;
            public bool TooClose => Distance < StagePlaceSettings.NearWarning;

            public string Text => Overlaps ? $"{Label}に重なっています" : TooClose ? $"{Label} {Distance:0.0}m 近すぎ" : $"{Label} {Distance:0.0}m";
        }

        public static readonly Color NearColor = new Color(1f, 0.36f, 0.42f);
        public static readonly Color FarColor = new Color(0.92f, 0.92f, 0.92f, 0.85f);

        /// <summary>その背景から <see cref="StagePlaceSettings.DistanceRange"/> 以内にある砲台・コア・湧き位置までの距離（近い順）。</summary>
        public static List<Reading> Measure(StageProp prop)
        {
            var readings = new List<Reading>();
            if (prop == null)
            {
                return readings;
            }

            ScanTargets();
            var shapes = StagePropShape.FloorShapes(prop);
            foreach (var (label, position) in Targets)
            {
                var best = float.PositiveInfinity;
                var closest = position;
                foreach (var shape in shapes)
                {
                    var d = StagePlaceMath.DistanceToPolygon(shape, position, out var onShape);
                    if (d < best)
                    {
                        best = d;
                        closest = onShape;
                    }
                }

                if (best <= StagePlaceSettings.DistanceRange)
                {
                    readings.Add(new Reading { Label = label, Target = position, Closest = closest, Distance = best });
                }
            }

            readings.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return readings;
        }

        /// <summary>シーンビューに描く（StageSceneOverlay から Repaint のときに呼ぶ）。</summary>
        public static void Draw(StageStudioContext context)
        {
            if (!StagePlaceSettings.ShowDistances || context.SelectedProps.Count == 0)
            {
                return;
            }

            _label ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            foreach (var prop in context.SelectedProps)
            {
                foreach (var reading in Measure(prop))
                {
                    var color = reading.TooClose ? NearColor : FarColor;
                    Handles.color = color;
                    var a = new Vector3(reading.Closest.x, reading.Closest.y, 0f);
                    var b = new Vector3(reading.Target.x, reading.Target.y, 0f);
                    if (!reading.Overlaps)
                    {
                        Handles.DrawDottedLine(a, b, 3f);
                    }

                    Handles.DrawWireDisc(b, Vector3.forward, 0.18f);
                    _label.normal.textColor = color;
                    Handles.Label((a + b) * 0.5f, reading.Text, _label);
                }
            }
        }

        /// <summary>砲台などが動いたり増えたりしたときに、すぐ探し直す。</summary>
        public static void Invalidate() => _nextScan = 0;

        private static void ScanTargets()
        {
            if (EditorApplication.timeSinceStartup < _nextScan)
            {
                return;
            }

            _nextScan = EditorApplication.timeSinceStartup + TargetScanInterval;
            Targets.Clear();
            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                Targets.Add(($"P{turret.playerIndex + 1}の砲台", turret.transform.position));
            }

            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                Targets.Add(("コア", core.transform.position));
            }

            foreach (var spawn in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                Targets.Add(("湧き位置", spawn.transform.position));
            }
        }
    }
}
