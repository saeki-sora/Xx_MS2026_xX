using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>映り込みチェックの結果1件。</summary>
    public readonly struct CameraVisibilityIssue
    {
        public readonly int Viewer;
        public readonly MessageType Severity;
        public readonly string Message;

        public CameraVisibilityIssue(int viewer, MessageType severity, string message)
        {
            Viewer = viewer;
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// 各視点で「映っていないと困る物」が画面(安全域)に入っているかを調べる。
    /// 自分の砲台が画面の端で欠ける・コアが見えない・敵が画面外から湧く、といった事故を事前に見つけるためのもの。
    /// </summary>
    public static class CameraVisibilityChecker
    {
        private static readonly Rect FullScreen = new Rect(0f, 0f, 1f, 1f);
        private static readonly Vector3[] Corners = new Vector3[4];

        public static List<CameraVisibilityIssue> Check(CameraTabContext context)
        {
            var issues = new List<CameraVisibilityIssue>();
            foreach (var viewer in ViewerIndex.All)
            {
                CheckViewer(context, viewer, issues);
            }

            return issues;
        }

        public static int CountFor(List<CameraVisibilityIssue> issues, int viewer, MessageType minimumSeverity)
        {
            var count = 0;
            foreach (var issue in issues)
            {
                if (issue.Viewer == viewer && issue.Severity >= minimumSeverity)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CheckViewer(CameraTabContext context, int viewer, List<CameraVisibilityIssue> issues)
        {
            var view = context.Preset.GetView(viewer);
            var aspect = context.Aspect;
            var safe = context.Guides.GetSafeRect(FullScreen);
            var points = context.Points;

            if (ViewerIndex.IsPlayer(viewer))
            {
                CheckOwnTurret(view, aspect, safe, viewer, points, issues);
            }

            foreach (var core in points.Cores)
            {
                if (!CameraViewMath.IsVisible(view, aspect, core, FullScreen))
                {
                    issues.Add(new CameraVisibilityIssue(viewer, MessageType.Warning, "コアが画面に映っていません。"));
                    break;
                }
            }

            var hiddenTurrets = 0;
            foreach (var turret in points.Turrets)
            {
                if (turret.PlayerIndex != viewer && !CameraViewMath.IsVisible(view, aspect, turret.Position, FullScreen))
                {
                    hiddenTurrets++;
                }
            }

            if (hiddenTurrets > 0)
            {
                issues.Add(new CameraVisibilityIssue(viewer,
                    viewer == ViewerIndex.Overview ? MessageType.Warning : MessageType.Info,
                    $"{(viewer == ViewerIndex.Overview ? "" : "他の人の")}砲台が{hiddenTurrets}基、画面外です。"));
            }

            var hiddenSpawns = 0;
            foreach (var spawn in points.SpawnPoints)
            {
                if (!CameraViewMath.IsVisible(view, aspect, spawn, FullScreen))
                {
                    hiddenSpawns++;
                }
            }

            if (hiddenSpawns > 0)
            {
                issues.Add(new CameraVisibilityIssue(viewer, MessageType.Info,
                    $"敵の出現地点 {hiddenSpawns}/{points.SpawnPoints.Count} 個が画面外です（敵が画面の外から現れます。意図通りなら問題ありません）。"));
            }

            if (!CameraViewMath.GetGroundQuad(view, aspect, Corners))
            {
                issues.Add(new CameraVisibilityIssue(viewer, MessageType.Warning, "画面の一部が地平線より上(地面の無い所)を映しています。傾きか視野角を小さくしてください。"));
            }
        }

        private static void CheckOwnTurret(in CameraViewSettings view, float aspect, Rect safe, int viewer, CameraScenePoints points, List<CameraVisibilityIssue> issues)
        {
            if (!points.TryGetTurret(viewer, out var turret))
            {
                issues.Add(new CameraVisibilityIssue(viewer, MessageType.Info, $"シーンに{ViewerIndex.LongLabel(viewer)}の砲台がありません。"));
                return;
            }

            if (!CameraViewMath.IsVisible(view, aspect, turret, FullScreen))
            {
                issues.Add(new CameraVisibilityIssue(viewer, MessageType.Error, "自分の砲台が画面に映っていません。"));
            }
            else if (!CameraViewMath.IsVisible(view, aspect, turret, safe))
            {
                issues.Add(new CameraVisibilityIssue(viewer, MessageType.Warning, "自分の砲台が安全域の外(画面の端)にあります。モニターの縁で欠ける恐れがあります。"));
            }
        }
    }
}
