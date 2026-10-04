using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// カメラタブの各パネルが共通で使う「今のシーンの状況」。描画のたびに取り直す軽量なスナップショット。
    /// パネルはシーンを個別に探さず、これを受け取って描くだけにする。
    /// </summary>
    public sealed class CameraTabContext
    {
        private static readonly CameraGuideSettings FallbackGuides = new CameraGuideSettings();

        private CameraScenePoints _points;

        public FortressCameraRig Rig { get; private set; }
        public int RigCount { get; private set; }
        public CameraFeedbackDirector Director { get; private set; }
        public BillboardDirector Billboards { get; private set; }

        public CameraViewPreset Preset => Rig != null ? Rig.preset : null;
        public CameraFeedbackConfig FeedbackConfig => Director != null ? Director.config : null;
        public CameraGuideSettings Guides => Rig != null && Rig.guides != null ? Rig.guides : FallbackGuides;

        /// <summary>エディタでの確認に使う縦横比(本番の基準解像度)。</summary>
        public float Aspect => Guides.ReferenceAspect;

        /// <summary>シーンの砲台・コアなどの位置。必要になったときに初めて集める。</summary>
        public CameraScenePoints Points => _points ??= CameraScenePoints.Collect();

        public int SelectedViewer => CameraToolState.SelectedViewer;

        public static CameraTabContext Capture()
        {
            var rigs = Object.FindObjectsByType<FortressCameraRig>(FindObjectsSortMode.None);
            var rig = FortressCameraRig.Active != null ? FortressCameraRig.Active : (rigs.Length > 0 ? rigs[0] : null);

            return new CameraTabContext
            {
                Rig = rig,
                RigCount = rigs.Length,
                Director = FindNearRig<CameraFeedbackDirector>(rig),
                Billboards = FindNearRig<BillboardDirector>(rig)
            };
        }

        private static T FindNearRig<T>(FortressCameraRig rig) where T : Component
        {
            // GetComponentはエディタで「見つからない」ときに偽のnullを返すことがあるため、??ではなく明示的に比較する。
            var found = rig != null ? rig.GetComponentInChildren<T>() : null;
            return found != null ? found : Object.FindFirstObjectByType<T>();
        }
    }
}
