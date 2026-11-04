using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// 落ちてくる物 1 つ（動かすのは見た目の Transform だけ）。
    /// 位置と大きさは「元の値 + 演出の分」を毎フレーム書く。ほかの係（揺れ・目印への当てはめなど）が値を書き換えたら、
    /// それを新しい元の値として受け取る（演出の分が積み重なったり、ほかの係の動きを消したりしない）。
    /// 演出が終わったら元の値に戻す。
    /// 位置は親から見た位置（localPosition）で覚えて比べる。親が動いても（浮遊するスマッシュボールの見た目など）
    /// 「ほかの係が書き換えた」と取り違えず、演出の分が積み重なって見た目が置き去りにならない。
    /// </summary>
    internal sealed class StageDropTarget
    {
        public readonly Transform Transform;
        public readonly StageDropKind Kind;

        /// <summary>絵（スプライト）の物か。絵は縦横（XY）で、3D モデルは高さ（Z）でつぶれる。</summary>
        public readonly bool Flat;

        /// <summary>プレイヤーの番号（0 = 1P）。プレイヤー以外は -1。</summary>
        public readonly int PlayerIndex;

        /// <summary>見た目の大きさ（半径くらい。画面の外へ出しきるのに使う）。</summary>
        public readonly float Radius;

        public float Delay;
        public bool Landed;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseScale;
        private Vector3 _writtenLocalPosition;
        private Vector3 _writtenScale;
        private bool _written;

        public StageDropTarget(Transform transform, StageDropKind kind, bool flat, int playerIndex, float radius)
        {
            Transform = transform;
            Kind = kind;
            Flat = flat;
            PlayerIndex = playerIndex;
            Radius = radius;
        }

        /// <summary>着地する位置（今の元の位置。ワールド座標）。</summary>
        public Vector3 LandingPosition
        {
            get
            {
                Capture();
                return ToWorld(_baseLocalPosition);
            }
        }

        public bool IsAlive => Transform != null;

        /// <summary>元の値から offset（ワールドでのずらし量）だけずらし、大きさを scale 倍にして書く。</summary>
        public void Apply(Vector3 offset, Vector3 scale)
        {
            if (Transform == null)
            {
                return;
            }

            Capture();
            var parent = Transform.parent;
            _writtenLocalPosition = _baseLocalPosition + (parent != null ? parent.InverseTransformVector(offset) : offset);
            _writtenScale = Vector3.Scale(_baseScale, scale);
            Transform.localPosition = _writtenLocalPosition;
            Transform.localScale = _writtenScale;
            _written = true;
        }

        /// <summary>元の値に戻す（ほかの係があとから書いた値はそのまま）。</summary>
        public void Release()
        {
            if (Transform != null && _written)
            {
                if ((Transform.localPosition - _writtenLocalPosition).sqrMagnitude < 1e-10f)
                {
                    Transform.localPosition = _baseLocalPosition;
                }

                if (Transform.localScale == _writtenScale)
                {
                    Transform.localScale = _baseScale;
                }
            }

            _written = false;
        }

        // 前に書いた値から変わっていたら、ほかの係が書いたので、それを元の値にする。
        private void Capture()
        {
            if (Transform == null)
            {
                return;
            }

            var localPosition = Transform.localPosition;
            if (!_written || (localPosition - _writtenLocalPosition).sqrMagnitude > 1e-10f)
            {
                _baseLocalPosition = localPosition;
            }

            var scale = Transform.localScale;
            if (!_written || scale != _writtenScale)
            {
                _baseScale = scale;
            }
        }

        private Vector3 ToWorld(Vector3 localPosition)
        {
            var parent = Transform != null ? Transform.parent : null;
            return parent != null ? parent.TransformPoint(localPosition) : localPosition;
        }
    }
}
