using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// 落ちてくる物 1 つ（動かすのは見た目の Transform だけ）。
    /// 位置と大きさは「元の値 + 演出の分」を毎フレーム書く。ほかの係（揺れ・目印への当てはめなど）が値を書き換えたら、
    /// それを新しい元の値として受け取る（演出の分が積み重なったり、ほかの係の動きを消したりしない）。
    /// 演出が終わったら元の値に戻す。
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

        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private Vector3 _writtenPosition;
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

        /// <summary>着地する位置（今の元の位置）。</summary>
        public Vector3 LandingPosition
        {
            get
            {
                Capture();
                return _basePosition;
            }
        }

        public bool IsAlive => Transform != null;

        /// <summary>元の値から offset だけずらし、大きさを scale 倍にして書く。</summary>
        public void Apply(Vector3 offset, Vector3 scale)
        {
            if (Transform == null)
            {
                return;
            }

            Capture();
            _writtenPosition = _basePosition + offset;
            _writtenScale = Vector3.Scale(_baseScale, scale);
            Transform.position = _writtenPosition;
            Transform.localScale = _writtenScale;
            _written = true;
        }

        /// <summary>元の値に戻す（ほかの係があとから書いた値はそのまま）。</summary>
        public void Release()
        {
            if (Transform != null && _written)
            {
                if ((Transform.position - _writtenPosition).sqrMagnitude < 1e-10f)
                {
                    Transform.position = _basePosition;
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

            var position = Transform.position;
            if (!_written || (position - _writtenPosition).sqrMagnitude > 1e-10f)
            {
                _basePosition = position;
            }

            var scale = Transform.localScale;
            if (!_written || scale != _writtenScale)
            {
                _baseScale = scale;
            }
        }
    }
}
