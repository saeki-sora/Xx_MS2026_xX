using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 背景を「つかんで床の上を動かす」1回分の操作（つかむ→動かす→離す／やめる）。マウスの位置は視線（Ray）で受け取るので、
    /// シーンビューの配置ツールでも、配置ページのゲーム画面でも同じように動く。
    /// つかんだ点の高さの水平な面の上でマウスを追いかけるので、遠近のある視点でも、つかんだ所がマウスに付いてくる。
    /// 吸着が有効なら、他の物の端にくっつく・そろう（Guide に線の位置が入る）か、マス目に乗る。全体で1回の「元に戻す」。
    /// </summary>
    public sealed class StageMoveSession
    {
        private readonly List<StageProp> _props = new List<StageProp>();
        private readonly List<Vector3> _startPositions = new List<Vector3>();
        private readonly List<Rect> _others = new List<Rect>();
        private Rect _startRect;
        private float _grabHeight;
        private Vector2 _grabStart;
        private int _undoGroup;

        public bool IsActive { get; private set; }

        public bool Moved { get; private set; }

        public IReadOnlyList<StageProp> Props => _props;

        /// <summary>今の吸着の結果（線を描くのに使う）。</summary>
        public StagePlaceMath.EdgeSnapResult Snap { get; private set; }

        /// <summary>つかむ。grabPoint はつかんだ点（その高さの面の上で追いかける）。</summary>
        public bool Begin(IReadOnlyList<StageProp> props, Ray ray, Vector3 grabPoint, IReadOnlyList<StageProp> everyone)
        {
            _props.Clear();
            foreach (var prop in props)
            {
                if (prop != null && !_props.Contains(prop))
                {
                    _props.Add(prop);
                }
            }

            _grabHeight = Mathf.Min(0f, grabPoint.z);
            if (_props.Count == 0 || !RayToPlane(ray, _grabHeight, out _grabStart))
            {
                return false;
            }

            _startPositions.Clear();
            foreach (var prop in _props)
            {
                _startPositions.Add(prop.transform.position);
            }

            _startRect = StagePlaceMath.BoundsOf(StagePropShape.FloorPoints(_props));
            _others.Clear();
            if (everyone != null)
            {
                foreach (var other in everyone)
                {
                    if (other != null && other.isActiveAndEnabled && !_props.Contains(other))
                    {
                        _others.Add(StagePlaceMath.BoundsOf(StagePropShape.FloorPoints(other)));
                    }
                }
            }

            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("背景を動かす");
            Moved = false;
            Snap = default;
            IsActive = true;
            return true;
        }

        public void Update(Ray ray, bool snap)
        {
            if (!IsActive || !RayToPlane(ray, _grabHeight, out var current))
            {
                return;
            }

            var delta = current - _grabStart;
            var result = default(StagePlaceMath.EdgeSnapResult);
            if (snap)
            {
                if (StagePlaceSettings.EdgeSnapDistance > 0f)
                {
                    var moving = new Rect(_startRect.position + delta, _startRect.size);
                    result = StagePlaceMath.EdgeSnap(moving, _others, StagePlaceSettings.EdgeSnapDistance);
                    delta += result.Offset;
                }

                // 端に吸着しなかった向きは、つかんだ物の根元をマス目に乗せる。
                var step = StagePlaceSettings.GridStep;
                var start = (Vector2)_startPositions[0];
                if (!result.SnappedX)
                {
                    delta.x = StagePlaceMath.SnapValue(start.x + delta.x, step) - start.x;
                }

                if (!result.SnappedY)
                {
                    delta.y = StagePlaceMath.SnapValue(start.y + delta.y, step) - start.y;
                }
            }

            Snap = result;
            StagePropTransformOps.RecordMove(_props, "背景を動かす");
            for (var i = 0; i < _props.Count; i++)
            {
                if (_props[i] != null)
                {
                    _props[i].transform.position = _startPositions[i] + new Vector3(delta.x, delta.y, 0f);
                }
            }

            Moved = true;
            StageGameCamera.MarkDirty();
        }

        public void End()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            if (Moved)
            {
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
                    _props[i].transform.position = _startPositions[i];
                }
            }

            if (Moved)
            {
                StagePropTransformOps.Commit();
            }

            StageGameCamera.MarkDirty();
        }

        /// <summary>今動かしている物の、床の上の広がり（吸着の線を描く長さに使う）。</summary>
        public Rect CurrentRect() => StagePlaceMath.BoundsOf(StagePropShape.FloorPoints(_props));

        /// <summary>視線と、高さ z の水平な面との交点（床の座標）。</summary>
        public static bool RayToPlane(Ray ray, float z, out Vector2 point)
        {
            point = default;
            if (Mathf.Abs(ray.direction.z) < 1e-6f)
            {
                return false;
            }

            var t = (z - ray.origin.z) / ray.direction.z;
            if (t < 0f)
            {
                return false;
            }

            var hit = ray.origin + ray.direction * t;
            point = new Vector2(hit.x, hit.y);
            return true;
        }
    }
}
