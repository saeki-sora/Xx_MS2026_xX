using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// SceneView・既存カメラと視点の相互変換。「Sceneで見やすく合わせた画角をそのまま取り込む」
    /// 「視点をSceneで確認する」ための橋渡し。
    /// </summary>
    public static class CameraSceneViewBridge
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>カメラの見え方を視点に変換する。templateは地面に届かない場合などの既定値に使う。</summary>
        public static CameraViewSettings ViewFromCamera(Camera camera, CameraViewSettings template)
        {
            var view = template;
            if (camera == null)
            {
                return view;
            }

            var cameraTransform = camera.transform;
            var position = cameraTransform.position;
            var forward = cameraTransform.forward;
            var up = cameraTransform.up;

            if (forward.z > 1e-4f)
            {
                var distance = -position.z / forward.z;
                view.center = position + forward * distance;
                view.distance = Mathf.Max(CameraViewSettings.MinDistance, distance);
            }
            else
            {
                view.center = position;
            }

            // 画面の上(camera.up)をXY平面に投影した向きが、視点の回転(roll)。
            view.rollDegrees = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg;
            view.tiltDegrees = Mathf.Min(CameraViewSettings.MaxTiltDegrees, Vector3.Angle(forward, Vector3.forward));
            view.projection = camera.orthographic ? CameraProjection.Orthographic : CameraProjection.Perspective;
            view.orthographicSize = camera.orthographicSize;
            view.fieldOfView = camera.fieldOfView;
            return view.Sanitized();
        }

        /// <summary>
        /// 最後に操作したSceneViewの見え方を取り込む。投影方式は取り込み先(current)のまま、映る広さだけ合わせる。
        /// </summary>
        public static bool TryCaptureFromSceneView(CameraViewSettings current, out CameraViewSettings captured)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                captured = current;
                return false;
            }

            captured = ViewFromCamera(sceneView.camera, current).WithProjection(current.projection);
            if (current.IsOrthographic)
            {
                captured.tiltDegrees = current.tiltDegrees;
            }

            return true;
        }

        /// <summary>視点に映る範囲がSceneViewにぴったり収まるよう移動する。2Dモードでは回転は反映されない。</summary>
        public static void FrameInSceneView(in CameraViewSettings view, float aspect)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            CameraViewMath.GetGroundQuad(view, aspect, Corners);
            var bounds = new Bounds(Corners[0], Vector3.zero);
            for (var i = 1; i < Corners.Length; i++)
            {
                bounds.Encapsulate(Corners[i]);
            }

            if (!sceneView.in2DMode)
            {
                CameraViewMath.GetPose(view, out _, out var rotation);
                sceneView.rotation = rotation;
            }

            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        }
    }
}
