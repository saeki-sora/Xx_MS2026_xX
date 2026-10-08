using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージ背景スタジオを開いている間、シーンビューに描く物:
    /// ・通れない範囲の輪郭（壁=オレンジ、壊せる壁=紫、遅くなる地帯=水色。選んでいる物は太く）
    /// ・背景どうしのすき間のものさし（赤=経路上は塞がっている、黄=敵より狭い、緑=通れる）と幅の数字
    /// ・見た目のずれ（StageParallaxGuide）、砲台・コアとの距離（StageDistanceGuide）、ゲーム画面の枠（StageSceneViewSync）
    /// </summary>
    [InitializeOnLoad]
    public static class StageSceneOverlay
    {
        private const double GapInterval = 0.5;

        private static List<StageGapFinder.Gap> _gaps = new List<StageGapFinder.Gap>();
        private static double _nextGapScan;
        private static float _enemyDiameter = 0.44f;
        private static GUIStyle _label;

        static StageSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        /// <summary>スタジオのウィンドウが設定する（null なら何も描かない）。</summary>
        public static StageStudioContext Context { get; set; }

        private static void OnSceneGui(SceneView view)
        {
            var context = Context;
            if (context == null || context.Root == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            _label ??= new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter };
            if (context.ShowFootprints)
            {
                DrawFootprints(context);
            }

            if (context.ShowGaps)
            {
                DrawGaps(context);
            }

            StageParallaxGuide.Draw(context);
            StageDistanceGuide.Draw(context);
            StageSceneViewSync.DrawGameFrame(view);
        }

        private static void DrawFootprints(StageStudioContext context)
        {
            foreach (var prop in context.Props)
            {
                if (prop == null || !prop.isActiveAndEnabled || !prop.role.NeedsFootprint())
                {
                    continue;
                }

                var selected = IsSelected(context, prop);
                var color = StageRoleStyle.Color(prop.role);
                Handles.color = selected ? color : new Color(color.r, color.g, color.b, 0.75f);
                foreach (var path in StageGapFinder.WorldPaths(prop))
                {
                    var points = new Vector3[path.Length + 1];
                    for (var i = 0; i < path.Length; i++)
                    {
                        points[i] = path[i];
                    }

                    points[path.Length] = path[0];
                    Handles.DrawAAPolyLine(selected ? 5f : 2.5f, points);
                }

                if (selected)
                {
                    var p = prop.transform.position;
                    Handles.Label(p, $"{prop.name}（{prop.role.DisplayName()}）", _label);
                }
            }
        }

        private static void DrawGaps(StageStudioContext context)
        {
            if (EditorApplication.timeSinceStartup >= _nextGapScan)
            {
                _nextGapScan = EditorApplication.timeSinceStartup + GapInterval;
                _gaps = StageGapFinder.Find(context.Props);
                _enemyDiameter = StageGapFinder.LargestEnemyDiameter();
            }

            foreach (var gap in _gaps)
            {
                if (gap.A == null || gap.B == null)
                {
                    continue;
                }

                var color = !gap.PassableOnGrid ? new Color(1f, 0.36f, 0.42f)
                    : gap.Width < _enemyDiameter ? new Color(1f, 0.78f, 0.34f)
                    : new Color(0.37f, 0.83f, 0.61f);
                Handles.color = color;
                Handles.DrawAAPolyLine(3f, gap.PointA, gap.PointB);
                Handles.DrawSolidDisc(gap.PointA, Vector3.forward, 0.05f);
                Handles.DrawSolidDisc(gap.PointB, Vector3.forward, 0.05f);
                var style = new GUIStyle(_label) { normal = { textColor = color } };
                Handles.Label(gap.Middle, gap.Width.ToString("0.00"), style);
            }
        }

        private static bool IsSelected(StageStudioContext context, StageProp prop)
        {
            foreach (var selected in context.SelectedProps)
            {
                if (selected == prop)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
