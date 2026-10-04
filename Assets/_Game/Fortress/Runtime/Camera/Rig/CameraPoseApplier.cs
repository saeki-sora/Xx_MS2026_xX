using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>視点をTransform(位置・回転)とCamera(投影)に書き込む。本番のリグとエディタのプレビュー用カメラで共有する。</summary>
    public static class CameraPoseApplier
    {
        public static void Apply(Transform poseTarget, Camera camera, in CameraViewSettings view, float nearClip, float farClip)
        {
            CameraViewMath.GetPose(view, out var position, out var rotation);

            if (poseTarget != null)
            {
                poseTarget.SetPositionAndRotation(position, rotation);
            }

            if (camera == null)
            {
                return;
            }

            camera.orthographic = view.IsOrthographic;
            if (view.IsOrthographic)
            {
                camera.orthographicSize = Mathf.Max(CameraViewSettings.MinOrthographicSize, view.orthographicSize);
            }
            else
            {
                camera.fieldOfView = Mathf.Clamp(view.fieldOfView, CameraViewSettings.MinFieldOfView, CameraViewSettings.MaxFieldOfView);
            }

            camera.nearClipPlane = Mathf.Max(0.01f, nearClip);
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 0.01f, farClip);
        }
    }
}
