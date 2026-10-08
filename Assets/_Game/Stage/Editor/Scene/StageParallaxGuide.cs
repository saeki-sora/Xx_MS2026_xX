using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「見た目のずれ」をシーンビューに描く: 遠近のあるカメラでは背の高い物ほど、床に置いた位置から外側へずれて映る。
    /// 選んだ視点のカメラから見て、物が床のどこに重なって見えるか（＝その下の物を隠す範囲）を、その視点の色の影で塗る。
    /// さらに「てっぺんがどれだけずれて見えるか」を矢印と数字で出す。
    /// </summary>
    public static class StageParallaxGuide
    {
        private static readonly List<Vector2> Projected = new List<Vector2>(2048);
        private static GUIStyle _label;

        public static void Draw(StageStudioContext context)
        {
            var viewer = StagePlaceSettings.ParallaxViewer;
            if (viewer == StagePlaceSettings.ParallaxOff || !StageGameCamera.TryGetPose(viewer, out var cameraPosition, out var rotation, out var view))
            {
                return;
            }

            if (!StageGameCamera.HasParallax(view))
            {
                return; // 真上から遠近なしで見る視点では、ずれは起きない
            }

            _label ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            var forward = rotation * Vector3.forward;
            var color = ViewerIndex.Color(viewer);
            var props = StagePlaceSettings.ParallaxForAll ? context.Props : context.SelectedProps;
            foreach (var prop in props)
            {
                if (prop != null && prop.isActiveAndEnabled && prop.role != StagePropRole.Floor)
                {
                    DrawProp(prop, cameraPosition, forward, view.IsOrthographic, color, ViewerIndex.Label(viewer));
                }
            }
        }

        /// <summary>その視点で、物のてっぺんが床の位置からどれだけずれて見えるか（メートル）。</summary>
        public static float TopShift(StageProp prop, int viewer)
        {
            if (!StageGameCamera.TryGetPose(viewer, out var position, out var rotation, out var view) || !StageGameCamera.HasParallax(view))
            {
                return 0f;
            }

            var bounds = prop.VisualBounds;
            var top = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            return StagePlaceMath.ProjectToFloor(top, position, rotation * Vector3.forward, view.IsOrthographic, out var floor)
                ? Vector2.Distance(floor, new Vector2(top.x, top.y))
                : 0f;
        }

        private static void DrawProp(StageProp prop, Vector3 cameraPosition, Vector3 forward, bool orthographic, Color color, string viewerLabel)
        {
            Projected.Clear();
            foreach (var point in StagePropShape.WorldVertices(prop))
            {
                // 床より下（z>0）の点は床に置いたのと同じなので、そのまま床へ。
                var p = point.z > 0f ? new Vector3(point.x, point.y, 0f) : point;
                if (StagePlaceMath.ProjectToFloor(p, cameraPosition, forward, orthographic, out var floor))
                {
                    Projected.Add(floor);
                }
            }

            if (Projected.Count < 3)
            {
                return;
            }

            var hull = StagePlaceMath.ConvexHull(Projected);
            var outline = new Vector3[hull.Count + 1];
            for (var i = 0; i < hull.Count; i++)
            {
                outline[i] = hull[i];
            }

            outline[hull.Count] = outline[0];
            Handles.color = new Color(color.r, color.g, color.b, 0.16f);
            Handles.DrawAAConvexPolygon(System.Array.ConvertAll(hull.ToArray(), v => (Vector3)v));
            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawDottedLines(ToSegments(outline), 4f);

            // てっぺんのずれ（真上の床の点 → 見た目で重なる床の点）
            var bounds = prop.VisualBounds;
            var top = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            if (!StagePlaceMath.ProjectToFloor(top, cameraPosition, forward, orthographic, out var shifted))
            {
                return;
            }

            var from = new Vector3(top.x, top.y, 0f);
            var to = new Vector3(shifted.x, shifted.y, 0f);
            var distance = Vector3.Distance(from, to);
            if (distance < 0.05f)
            {
                return;
            }

            Handles.DrawAAPolyLine(3f, from, to);
            Handles.DrawSolidDisc(to, Vector3.forward, 0.06f);
            _label.normal.textColor = color;
            Handles.Label(to, $"{viewerLabel}では {distance:0.0}m ずれて見える", _label);
        }

        private static Vector3[] ToSegments(Vector3[] polyline)
        {
            var segments = new Vector3[(polyline.Length - 1) * 2];
            for (var i = 0; i < polyline.Length - 1; i++)
            {
                segments[i * 2] = polyline[i];
                segments[i * 2 + 1] = polyline[i + 1];
            }

            return segments;
        }
    }
}
