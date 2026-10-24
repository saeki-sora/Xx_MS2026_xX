using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
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

        /// <summary>開いているステージ（ステージのシーンが開いていなければ null）。視点はこのステージの物を編集する。</summary>
        public CameraStageInfo Stage { get; private set; }

        /// <summary>ステージのシーンに置かれてしまったカメラリグ（Play では使われない。消すこと）。</summary>
        public IReadOnlyList<FortressCameraRig> StrayRigs { get; private set; }

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

            // ステージのシーンに紛れ込んだリグは使わない（Play では共通の Game のリグが動き、ステージの視点はそちらに当てはめられる）。
            var usable = new List<FortressCameraRig>();
            var stray = new List<FortressCameraRig>();
            foreach (var candidate in rigs)
            {
                (CameraStageBinding.IsInStageScene(candidate) ? stray : usable).Add(candidate);
            }

            var rig = FortressCameraRig.Active != null && usable.Contains(FortressCameraRig.Active)
                ? FortressCameraRig.Active
                : (usable.Count > 0 ? usable[0] : null);

            return new CameraTabContext
            {
                Rig = rig,
                RigCount = usable.Count,
                StrayRigs = stray,
                Stage = CameraStageBinding.Current?.FindOpenStage(),
                Director = FindNearRig<CameraFeedbackDirector>(rig),
                Billboards = FindNearRig<BillboardDirector>(rig)
            };
        }

        /// <summary>
        /// 視点プリセットを切り替える。Play 中はリグだけ（その場限り）。Play 外でステージが開いていれば、そのステージの視点として
        /// ステージのシーンに入れる（Game のリグには見せるだけ）。ステージが無ければ今までどおりリグに入れる。
        /// </summary>
        public void UsePreset(CameraViewPreset preset, float blendSeconds)
        {
            if (Rig == null || preset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Rig.SetPreset(preset, blendSeconds);
                return;
            }

            if (Stage != null && CameraStageBinding.Current != null)
            {
                CameraStageBinding.Current.AssignPreset(Stage, preset);
                return;
            }

            Undo.RecordObject(Rig, "Assign Camera Preset");
            Rig.preset = preset;
            EditorUtility.SetDirty(Rig);
        }

        private static T FindNearRig<T>(FortressCameraRig rig) where T : Component
        {
            // GetComponentはエディタで「見つからない」ときに偽のnullを返すことがあるため、??ではなく明示的に比較する。
            var found = rig != null ? rig.GetComponentInChildren<T>() : null;
            return found != null ? found : Object.FindFirstObjectByType<T>();
        }
    }
}
