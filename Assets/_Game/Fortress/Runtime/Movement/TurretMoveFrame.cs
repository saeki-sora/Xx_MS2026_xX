using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】ステージの枠（stage_frame など、輪の形の床）の形。
    /// 枠の中心から見た向きごとに「内側のふちまでの距離」と「外側のふちまでの距離」を持つ（向きは等間隔に <see cref="Bins"/> 個）。
    /// ・内側 … WASD で動ける範囲（<see cref="InnerPolygon"/>）
    /// ・外側 … スマッシュボールが漂う範囲（外側のふちの、さらに外）
    /// </summary>
    public sealed class TurretMoveFrame
    {
        public readonly Vector2 Center;
        public readonly float[] Inner;
        public readonly float[] Outer;

        /// <summary>内側のふちを結んだ多角形（ワールドの XY）。</summary>
        public readonly Vector2[] InnerPolygon;

        public TurretMoveFrame(Vector2 center, float[] inner, float[] outer)
        {
            Center = center;
            Inner = inner;
            Outer = outer;
            InnerPolygon = new Vector2[inner.Length];
            for (var b = 0; b < inner.Length; b++)
            {
                InnerPolygon[b] = center + DirectionOfBin(b, inner.Length) * inner[b];
            }
        }

        public int Bins => Inner.Length;

        /// <summary>向き b（0〜bins-1）の真ん中の向き。b=0 が -180°、反時計回り。</summary>
        public static Vector2 DirectionOfBin(int b, int bins)
        {
            var angle = (b + 0.5f) / bins * Mathf.PI * 2f - Mathf.PI;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>向き direction（中心から見た向き）での、内側のふちまでの距離。</summary>
        public float InnerAt(Vector2 direction) => Sample(Inner, direction);

        /// <summary>向き direction での、外側のふちまでの距離。</summary>
        public float OuterAt(Vector2 direction) => Sample(Outer, direction);

        // 隣り合う2つの向きの値を、間の角度でなめらかにつなぐ。
        private static float Sample(float[] values, Vector2 direction)
        {
            var bins = values.Length;
            var angle = Mathf.Atan2(direction.y, direction.x);
            var t = (angle + Mathf.PI) / (Mathf.PI * 2f) * bins - 0.5f;
            var i0 = Mathf.FloorToInt(t);
            var frac = t - i0;
            var a = values[((i0 % bins) + bins) % bins];
            var b = values[(((i0 + 1) % bins) + bins) % bins];
            return Mathf.Lerp(a, b, frac);
        }
    }
}
