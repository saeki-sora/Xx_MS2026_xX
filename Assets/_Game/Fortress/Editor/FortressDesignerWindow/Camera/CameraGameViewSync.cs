using MS2026.Fortress.Cameras;
using UnityEditorInternal;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Play Mode外で、ツールで編集中の視点を本物のカメラ(リグ)にも反映し、Gameビューで同じ見え方を確認できるようにする。
    /// 値が変わったときだけ書き込む。Play中はリグ自身が動くので何もしない。
    /// </summary>
    public static class CameraGameViewSync
    {
        private static CameraViewSettings? _lastApplied;
        private static FortressCameraRig _lastRig;

        /// <summary>Gameビューに今どの視点が映っているか(Play中はリグの持ち主)。反映していなければnull。</summary>
        public static int? ShownViewer(CameraTabContext context)
        {
            if (context.Rig == null)
            {
                return null;
            }

            if (Application.isPlaying)
            {
                return context.Rig.CurrentViewer;
            }

            return CameraToolState.SyncGameView ? context.SelectedViewer : (int?)null;
        }

        public static void Tick(CameraTabContext context)
        {
            if (Application.isPlaying || !CameraToolState.SyncGameView || context.Rig == null || context.Preset == null)
            {
                _lastApplied = null;
                return;
            }

            var view = context.Preset.GetView(context.SelectedViewer);
            if (_lastRig == context.Rig && _lastApplied.HasValue && _lastApplied.Value.Approximately(view, 1e-5f))
            {
                return;
            }

            context.Rig.ApplyViewImmediate(view);
            _lastApplied = view;
            _lastRig = context.Rig;
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
