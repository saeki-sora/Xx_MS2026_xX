using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「誰の視点で、どの背景が、どの砲台・コアを隠しているか」を調べる（カメラの見え方プリセットの各視点で）。
    /// ゲーム中は透け・影で見えるようになるが、配置の段階で気づけるよう一覧にする。
    /// </summary>
    public static class StageOcclusionReport
    {
        public struct Finding
        {
            public int Viewer;
            public StageProp Prop;
            public string TargetLabel;

            /// <summary>透け方の設定で対策されているか（「透けない」なら false）。</summary>
            public bool Handled;
        }

        public static List<Finding> Run(CameraViewPreset preset, IReadOnlyList<StageProp> props)
        {
            var result = new List<Finding>();
            if (preset == null)
            {
                return result;
            }

            var lift = Vector3.back * StageLookProfile.Current.focusHeight;
            var targets = new List<(string label, Vector3 position)>();
            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                targets.Add(($"P{turret.playerIndex + 1}の砲台", turret.transform.position + lift));
            }

            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                targets.Add(("コア", core.transform.position + lift));
            }

            for (var viewer = 0; viewer < ViewerIndex.PlayerCount; viewer++)
            {
                var view = preset.GetView(viewer);
                CameraViewMath.GetPose(view, out var cameraPosition, out var cameraRotation);
                foreach (var prop in props)
                {
                    if (prop == null || prop.role == StagePropRole.Floor || !prop.TryGetComponent<StagePropRenderer>(out var renderer))
                    {
                        continue;
                    }

                    var bounds = renderer.ComputeBounds();
                    foreach (var (label, position) in targets)
                    {
                        if (bounds.Contains(position))
                        {
                            continue;
                        }

                        var toCamera = view.IsOrthographic ? cameraRotation * Vector3.back : cameraPosition - position;
                        if (bounds.IntersectRay(new Ray(position, toCamera.normalized), out var distance) &&
                            (view.IsOrthographic || distance <= toCamera.magnitude))
                        {
                            result.Add(new Finding
                            {
                                Viewer = viewer,
                                Prop = prop,
                                TargetLabel = label,
                                Handled = prop.look.occlusion != StageOcclusionMode.None
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}
