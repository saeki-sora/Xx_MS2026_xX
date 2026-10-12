using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 「てっぺんの丸をつかんで、倒したい方へ引く」1回分の操作（つかむ→引く→離す／やめる）。
    /// マウスは視線（Ray）で受け取るので、シーンビューの配置ツールでも配置ページのゲーム画面でも同じように動く。
    /// まっすぐ立てたときのてっぺんの高さの水平な面の上でマウスを追い、根元の真上からずれた分だけ、その向きへ倒す。
    /// 複数選んでいるときは、最初の物が傾いた量（X・Y の度）を全部に同じだけ足す。全体で1回の「元に戻す」。
    /// </summary>
    public sealed class StageTiltSession
    {
        /// <summary>吸着しているときの刻み（度）。</summary>
        public const float SnapDegrees = 5f;

        private readonly List<StageProp> _props = new List<StageProp>();
        private readonly List<Vector2> _startTilts = new List<Vector2>();
        private StageProp _primary;
        private Vector3 _pivot;
        private float _height;
        private int _undoGroup;

        public bool IsActive { get; private set; }

        public bool Changed { get; private set; }

        public StageProp Primary => _primary;

        /// <summary>
        /// 取っ手の場所: 傾ける支点（根元の真下の、見た目のいちばん下）と、そこから「上」の向きに伸ばしたてっぺん。
        /// </summary>
        public static bool TryGetKnob(StageProp prop, out Vector3 pivot, out Vector3 top)
        {
            pivot = top = default;
            if (prop == null || StagePropTransformOps.Model(prop) == prop.transform)
            {
                return false;
            }

            pivot = StagePropTransformOps.TiltPivot(prop);
            var up = StagePropTransformOps.TiltUpWorld(prop);
            top = pivot + up * Mathf.Max(0.2f, StagePropShape.ExtentAlong(prop, pivot, up));
            return true;
        }

        public bool Begin(IReadOnlyList<StageProp> props)
        {
            _props.Clear();
            _startTilts.Clear();
            foreach (var prop in props)
            {
                if (prop != null && !_props.Contains(prop))
                {
                    _props.Add(prop);
                    _startTilts.Add(StagePropTransformOps.Tilt(prop));
                }
            }

            _primary = _props.Count > 0 ? _props[0] : null;
            if (!TryGetKnob(_primary, out _pivot, out var top))
            {
                _props.Clear();
                return false;
            }

            _height = Vector3.Distance(_pivot, top);
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("背景を傾ける");
            Changed = false;
            IsActive = true;
            return true;
        }

        public void Update(Ray ray, bool snap)
        {
            if (!IsActive || _primary == null || !StageMoveSession.RayToPlane(ray, _pivot.z - _height, out var point))
            {
                return;
            }

            // 床の上のずれを、根元から見た向きに直してから傾きにする。
            var offsetWorld = point - (Vector2)_pivot;
            var offsetLocal = Quaternion.Inverse(_primary.transform.rotation) * new Vector3(offsetWorld.x, offsetWorld.y, 0f);
            var tilt = StagePlaceMath.TiltFromLean(new Vector2(offsetLocal.x, offsetLocal.y), _height);
            if (snap)
            {
                tilt = new Vector2(StagePlaceMath.SnapValue(tilt.x, SnapDegrees), StagePlaceMath.SnapValue(tilt.y, SnapDegrees));
            }
            else if (offsetWorld.magnitude < _height * 0.03f)
            {
                tilt = Vector2.zero; // 吸着を切っていても、真上ではまっすぐに立てる
            }

            var delta = tilt - _startTilts[0];
            for (var i = 0; i < _props.Count; i++)
            {
                if (_props[i] != null)
                {
                    StagePropTransformOps.SetTilt(_props[i], _startTilts[i] + delta);
                }
            }

            Changed = true;
            StageGameCamera.MarkDirty();
        }

        public void End()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            if (Changed)
            {
                StageFootprintRefresher.Flush();
                StagePropTransformOps.Commit();
                Undo.CollapseUndoOperations(_undoGroup);
            }
        }

        public void Cancel()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            for (var i = 0; i < _props.Count; i++)
            {
                if (_props[i] != null)
                {
                    StagePropTransformOps.SetTilt(_props[i], _startTilts[i]);
                }
            }

            if (Changed)
            {
                StageFootprintRefresher.Flush();
                StagePropTransformOps.Commit();
                Undo.CollapseUndoOperations(_undoGroup);
            }

            StageGameCamera.MarkDirty();
        }
    }
}
