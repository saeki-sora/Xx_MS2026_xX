using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// プレイヤーの画面に映っている範囲（地面の上の四角。ワールド座標）。CPU の目はこの中だけを見る。
    /// </summary>
    public struct CpuView
    {
        /// <summary>真ん中。</summary>
        public Vector2 Center;

        /// <summary>画面の右・上の向きに沿った半分の大きさ。</summary>
        public Vector2 HalfSize;

        /// <summary>画面の上の向き（長さ 1。画面を回した視点ならワールドの上とずれる）。</summary>
        public Vector2 Up;

        public Vector2 Right => new Vector2(Up.y, -Up.x);

        public CpuView(Vector2 center, Vector2 halfSize, Vector2 up)
        {
            Center = center;
            HalfSize = new Vector2(Mathf.Abs(halfSize.x), Mathf.Abs(halfSize.y));
            Up = up.sqrMagnitude > 1e-8f ? up.normalized : Vector2.up;
        }

        /// <summary>この点が範囲の中か（margin だけ外側まで中とみなす）。</summary>
        public bool Contains(Vector2 point, float margin = 0f)
        {
            var d = point - Center;
            return Mathf.Abs(Vector2.Dot(d, Right)) <= HalfSize.x + margin &&
                   Mathf.Abs(Vector2.Dot(d, Up)) <= HalfSize.y + margin;
        }
    }

    /// <summary>
    /// プレイヤーの画面に映る範囲を CPU の目に知らせる口。
    /// 本体の既定は「その席のカメラの決まった視点」。カメラが砲台に付いていく（実験中の要素）ときは、MS2026.CPU.Experimental が差し込む。
    /// </summary>
    public interface ICpuViewSource
    {
        /// <summary>分からなければ false（目は既定の視点を使う）。</summary>
        bool TryGetView(int player, out CpuView view);
    }
}
