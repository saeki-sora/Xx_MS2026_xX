using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 要塞デザイナーの「敵ウェーブ」で開いているウェーブの敵のルートを、Scene ビューに赤い線で出す（ゲームの発表の赤い線と同じ計算）。
    /// 湧き位置や障害物を動かしながら「どこを通るか」を確かめられる。切り替えは WAVE n のページの「Scene ビューにルートを表示」。
    /// </summary>
    [InitializeOnLoad]
    public static class WaveRouteSceneOverlay
    {
        private const string PrefEnabled = "MS2026.FortressDesigner.Wave.SceneRoutes";
        private const double RecomputeInterval = 0.5;

        private static readonly List<WaveRoute> Routes = new List<WaveRoute>();
        private static readonly List<WaveRouteSpawn> Spawns = new List<WaveRouteSpawn>();
        private static WavePlan _plan;
        private static int _wave = -1;
        private static EnemySpawnDirector _director;
        private static double _nextCompute;
        private static double _lastTarget;

        static WaveRouteSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefEnabled, true);
            set
            {
                EditorPrefs.SetBool(PrefEnabled, value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>今開いているウェーブ（WAVE n のページが毎回呼ぶ）。ほかのページでは Clear。</summary>
        public static void Target(WavePlan plan, int wave, EnemySpawnDirector director)
        {
            if (plan != _plan || wave != _wave || director != _director)
            {
                _nextCompute = 0;
                SceneView.RepaintAll();
            }

            _plan = plan;
            _wave = wave;
            _director = director;
            _lastTarget = EditorApplication.timeSinceStartup;
        }

        public static void Clear()
        {
            if (_plan != null)
            {
                SceneView.RepaintAll();
            }

            _plan = null;
            _wave = -1;
            Routes.Clear();
            Spawns.Clear();
        }

        private static void OnSceneGui(SceneView view)
        {
            // 要塞デザイナーで WAVE n のページを開いている間だけ（ほかのタブにしたり閉じたりしたら、少しして消える）。
            if (!Enabled || _plan == null || _director == null || Event.current.type != EventType.Repaint ||
                EditorApplication.timeSinceStartup - _lastTarget > 1.5)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup >= _nextCompute)
            {
                _nextCompute = EditorApplication.timeSinceStartup + RecomputeInterval;
                var field = Object.FindFirstObjectByType<NavigationField>();
                WaveRoutes.Compute(_plan, _wave, _director, field, Routes, Spawns);
            }

            using (new Handles.DrawingScope())
            {
                foreach (var route in Routes)
                {
                    var points = new Vector3[route.points.Count];
                    for (var i = 0; i < points.Length; i++)
                    {
                        points[i] = new Vector3(route.points[i].x, route.points[i].y, -0.05f);
                    }

                    Handles.color = route.reachesGoal ? new Color(1f, 0.2f, 0.25f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.9f);
                    Handles.DrawAAPolyLine(2f + route.width * 10f, points);
                    if (points.Length >= 2)
                    {
                        // 終わりの向き（コアの方）に小さな矢じり。
                        var end = points[points.Length - 1];
                        var dir = (end - points[points.Length - 2]).normalized;
                        var side = new Vector3(-dir.y, dir.x, 0f);
                        Handles.DrawAAPolyLine(4f, end - dir * 0.8f + side * 0.5f, end, end - dir * 0.8f - side * 0.5f);
                    }
                }

                foreach (var spawn in Spawns)
                {
                    var position = new Vector3(spawn.Position.x, spawn.Position.y, -0.05f);
                    Handles.color = new Color(1f, 0.25f, 0.3f, 1f);
                    Handles.DrawWireDisc(position, Vector3.forward, 0.8f);
                    Handles.Label(position + Vector3.up * 1.1f, $"{spawn.Id} ×{spawn.Enemies}", EditorStyles.boldLabel);
                }
            }
        }
    }
}
