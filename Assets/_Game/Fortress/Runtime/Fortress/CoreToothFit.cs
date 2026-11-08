using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯のモデルをコアの位置に置く計算（Unityの部品に触らない純粋な計算。テストあり）。
    /// ゲームの床は XY の面、「上」は −Z（カメラ側）。
    /// </summary>
    public static class CoreToothFit
    {
        /// <summary>モデルの向き。Maya（上が Y）のモデルは X を −90° 回して起こし、そのあと床の上で Z を中心に回す。</summary>
        public static Quaternion Rotation(bool mayaUp, float yawDegrees)
        {
            return Rotation(mayaUp, yawDegrees, 0f, 0f);
        }

        /// <summary>
        /// モデルの向き（起こす → 床の上で回す → 傾ける）。
        /// tiltForward は＋で歯の上が画面の上（+Y）へ、tiltSide は＋で右（+X）へ倒れる。
        /// </summary>
        public static Quaternion Rotation(bool mayaUp, float yawDegrees, float tiltForward, float tiltSide)
        {
            var stand = mayaUp ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            var tilt = Quaternion.AngleAxis(-tiltSide, Vector3.up) * Quaternion.AngleAxis(tiltForward, Vector3.right);
            return tilt * Quaternion.Euler(0f, 0f, yawDegrees) * stand;
        }

        /// <summary>
        /// まっすぐ立てたモデルの外形（入れ物の座標。大きさ1のとき）から、床の上で見た縦横の長い方が <paramref name="width"/> になる倍率。
        /// 回したり傾けたりする前の外形で決めるので、向きを変えても大きさは変わらない。
        /// </summary>
        public static float ScaleFor(Bounds uprightBounds, float width)
        {
            var footprint = Mathf.Max(uprightBounds.size.x, uprightBounds.size.y);
            return footprint > 1e-6f ? Mathf.Max(0f, width) / footprint : 1f;
        }

        /// <summary>
        /// 向きを付けたモデルの外形（入れ物の座標。大きさ1・位置0のとき）と倍率から、真ん中がコアの真上（＋ずれ）・
        /// 底が床（Z=<paramref name="sink"/>。+Z が下）に来る位置を求める。長さはすべて入れ物の座標。
        /// </summary>
        public static Vector3 Place(Bounds orientedBounds, float scale, float sink, Vector2 offset)
        {
            var center = orientedBounds.center * scale;
            var bottom = orientedBounds.max.z * scale;
            return new Vector3(offset.x - center.x, offset.y - center.y, sink - bottom);
        }

        /// <summary>
        /// 向きを付けたモデルの外形から、幅が <paramref name="width"/> になる倍率と、真ん中がコアの真上・底が床に来る位置を求める
        /// （回さず・傾けず・ずらさないとき用）。
        /// </summary>
        public static void Solve(Bounds orientedBounds, float width, float sink, out float scale, out Vector3 position)
        {
            scale = ScaleFor(orientedBounds, width);
            position = Place(orientedBounds, scale, sink, Vector2.zero);
        }

        /// <summary>
        /// 模様をモデルの大きさにそろえるための値（モデルの座標）。up はモデルの座標での「上」の向き（長さ1）。
        /// center = 外形の真ん中、invHeight = 1 / 上向きに測った高さ。
        /// </summary>
        public static void PatternFrame(Bounds localBounds, Vector3 up, out Vector3 center, out float invHeight)
        {
            center = localBounds.center;
            var size = localBounds.size;
            var height = Mathf.Abs(size.x * up.x) + Mathf.Abs(size.y * up.y) + Mathf.Abs(size.z * up.z);
            invHeight = height > 1e-6f ? 1f / height : 1f;
        }

        /// <summary>当たったときの赤い輪の半径。歯が大きいときは、歯のふちより外まで広がるようにする。</summary>
        public static float RingRadius(float settingRadius, float coreVisualRadius, float reach = 2.8f)
        {
            return Mathf.Max(0.5f, settingRadius, coreVisualRadius * reach);
        }

        /// <summary>
        /// 歯の入れ物を、ゲームの床の軸（Z）のまわりに、カメラの画面の回転と同じだけ回す（入れ物の親から見た回転）。
        /// 席の視点は「1P の視点をコアのまわりに回したもの」なので、これで どの席からも 1P と同じ見え方になる。
        /// </summary>
        public static Quaternion FacingLocal(Quaternion parentRotation, float rollDegrees)
        {
            var turn = Quaternion.AngleAxis(rollDegrees, Vector3.forward);
            return Quaternion.Inverse(parentRotation) * turn * parentRotation;
        }
    }
}
