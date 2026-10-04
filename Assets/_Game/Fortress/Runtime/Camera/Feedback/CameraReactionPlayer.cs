using DDrive.Runtime.CameraShake;

namespace MS2026.Fortress.Cameras
{
    /// <summary>決まった強さで、1つの演出設定(揺れ・寄り)を実際に鳴らす。誰の画面で鳴らすかの判断はしない。</summary>
    public static class CameraReactionPlayer
    {
        public static void Play(CameraReaction reaction, in CameraFeedbackRequest request, float strength, FortressCameraRig rig)
        {
            if (reaction == null || strength <= 0f)
            {
                return;
            }

            PlayShake(reaction.shake, strength, rig);
            PlayZoom(reaction.zoom, request, strength, rig);
        }

        private static void PlayShake(CameraShakeReaction shake, float strength, FortressCameraRig rig)
        {
            if (shake == null || !shake.enabled)
            {
                return;
            }

            var scaled = strength * shake.strength;

            // D-DriveのCameraFxはCamera.mainの直上に揺れ用ノードを挟んで揺らす。未設定・未起動なら簡易揺れで代用する。
            if (shake.UsesDDriveAsset && CameraFx.IsBound)
            {
                CameraFx.Shake(shake.ddriveShake, scaled);
                return;
            }

            if (rig != null)
            {
                rig.AddModifier(new SimpleShakeModifier(shake.simple, scaled));
            }
        }

        private static void PlayZoom(CameraZoomReaction zoom, in CameraFeedbackRequest request, float strength, FortressCameraRig rig)
        {
            if (zoom == null || !zoom.enabled || rig == null)
            {
                return;
            }

            var focus = request.HasPosition ? request.WorldPosition : (UnityEngine.Vector2?)null;
            rig.AddModifier(new ZoomPunchModifier(zoom.punch, focus, strength));
        }
    }
}
