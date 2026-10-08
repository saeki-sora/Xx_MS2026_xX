using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「ゲームのカメラ」を編集中に調べる窓口: 今の見え方プリセット・Game ビューの縦横比・各視点のカメラの位置。
    /// 配置ページのゲーム画面、シーンビューの目線合わせ、見た目のずれ表示が同じ計算を使う。
    /// シーンが変わるたびに増える番号（<see cref="Version"/>）も持ち、ゲーム画面はこれが変わったときだけ描き直す。
    /// </summary>
    [InitializeOnLoad]
    public static class StageGameCamera
    {
        static StageGameCamera()
        {
            ObjectChangeEvents.changesPublished += (ref ObjectChangeEventStream _) => MarkDirty();
            Undo.undoRedoPerformed += MarkDirty;
        }

        /// <summary>シーン（背景・光・カメラ）が変わるたびに増える番号。</summary>
        public static int Version { get; private set; } = 1;

        /// <summary>ゲーム画面を描き直してほしいとき（背景を動かした・設定を変えたとき）に呼ぶ。</summary>
        public static void MarkDirty() => Version++;

        /// <summary>シーンのカメラリグのプリセット。リグが無ければ今のステージに記録されたプリセット。</summary>
        public static CameraViewPreset Preset
        {
            get
            {
                var rig = Rig;
                if (rig != null && rig.preset != null)
                {
                    return rig.preset;
                }

                var root = StageRoot.Active != null ? StageRoot.Active : StageSceneService.FindRoot();
                return root != null && root.current != null ? root.current.cameraPreset : null;
            }
        }

        public static FortressCameraRig Rig =>
            FortressCameraRig.Active != null ? FortressCameraRig.Active : Object.FindFirstObjectByType<FortressCameraRig>();

        /// <summary>Game ビューの縦横比（取れなければ 16:9）。</summary>
        public static float Aspect
        {
            get
            {
                var size = Handles.GetMainGameViewSize();
                return size.x > 1f && size.y > 1f ? size.x / size.y : 16f / 9f;
            }
        }

        public static bool TryGetView(int viewer, out CameraViewSettings view)
        {
            var preset = Preset;
            if (preset == null)
            {
                view = default;
                return false;
            }

            view = preset.GetView(viewer).Sanitized();
            return true;
        }

        /// <summary>その視点のカメラの位置と向き。</summary>
        public static bool TryGetPose(int viewer, out Vector3 position, out Quaternion rotation, out CameraViewSettings view)
        {
            if (!TryGetView(viewer, out view))
            {
                position = default;
                rotation = Quaternion.identity;
                return false;
            }

            CameraViewMath.GetPose(view, out position, out rotation);
            return true;
        }

        /// <summary>その視点で、高い所の物が床の位置からずれて見えるか（真上からの遠近なしなら、ずれない）。</summary>
        public static bool HasParallax(in CameraViewSettings view) => !view.IsOrthographic || view.tiltDegrees > 0.01f;
    }
}
