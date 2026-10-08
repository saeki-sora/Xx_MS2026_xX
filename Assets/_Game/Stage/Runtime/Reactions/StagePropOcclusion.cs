using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 手前の背景オブジェクトが砲台・コアを隠したときの見せ方を、オブジェクトの設定に合わせる。
    /// 「穴をあける」はシェーダーが画素ごとに自動で行うので、ここではON/OFFを伝えるだけ。
    /// 「全体を薄く」は、カメラと砲台・コアを結ぶ線がこのオブジェクトを通るかを調べて、全体を薄くする。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StagePropRenderer))]
    public sealed class StagePropOcclusion : MonoBehaviour
    {
        private StageProp _prop;
        private StagePropRenderer _renderer;
        private float _fade;

        /// <summary>今「全体を薄く」が効いているか（ツールの表示用）。</summary>
        public bool IsFading => _fade > 0.01f;

        private void Awake()
        {
            _prop = GetComponent<StageProp>();
            _renderer = GetComponent<StagePropRenderer>();
        }

        private void LateUpdate()
        {
            var mode = _prop.role == StagePropRole.Floor ? StageOcclusionMode.None : _prop.look.occlusion;
            var look = _renderer.Look;
            var noHole = mode != StageOcclusionMode.Hole;
            if (look.NoHole != noHole)
            {
                look.NoHole = noHole;
                _renderer.MarkDirty();
            }

            var target = mode == StageOcclusionMode.WholeFade && IsHidingFocus() ? 1f - _prop.look.wholeFadeAlpha : 0f;
            if (Mathf.Approximately(_fade, target))
            {
                return;
            }

            _fade = Mathf.MoveTowards(_fade, target, StageLookProfile.Current.wholeFadeSpeed * Time.deltaTime);
            look.Fade = _fade;
            _renderer.MarkDirty();
        }

        /// <summary>カメラから砲台・コアへ向かう線（太さ＝透かす半径の半分）が、このオブジェクトの箱に当たるか。</summary>
        private bool IsHidingFocus()
        {
            var camera = ResolveCamera();
            if (camera == null || StageFocusPoints.Count == 0)
            {
                return false;
            }

            var bounds = _renderer.ComputeBounds();
            for (var i = 0; i < StageFocusPoints.Count; i++)
            {
                var focus = StageFocusPoints.Get(i);
                var point = new Vector3(focus.x, focus.y, focus.z);
                var toCamera = camera.orthographic ? -camera.transform.forward : camera.transform.position - point;
                var length = camera.orthographic ? camera.farClipPlane : toCamera.magnitude;

                var expanded = bounds;
                expanded.Expand(focus.w);
                if (expanded.Contains(point))
                {
                    continue; // 砲台がオブジェクトの中や真横にあるときは隠しているとはみなさない
                }

                if (expanded.IntersectRay(new Ray(point, toCamera.normalized), out var distance) && distance <= length)
                {
                    return true;
                }
            }

            return false;
        }

        private static Camera ResolveCamera()
        {
            var rig = FortressCameraRig.Active;
            return rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
        }
    }
}
