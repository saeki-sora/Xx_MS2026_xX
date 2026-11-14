using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】砲台の移動の計算（Unity の物理や入力に依存しない部分）。</summary>
    public static class TurretMoveMath
    {
        /// <summary>
        /// 今の速さ vel を、目指す速さ desired へ近づける。押している間は acceleration、離したら deceleration で変える。
        /// 向きを切り返すとき（目指す向きと今の向きが逆）は止まる側の速さも使い、もたつかないようにする。
        /// </summary>
        public static Vector2 StepVelocity(Vector2 vel, Vector2 desired, float acceleration, float deceleration, float dt)
        {
            if (dt <= 0f)
            {
                return vel;
            }

            var pressing = desired.sqrMagnitude > 1e-6f;
            var rate = pressing ? acceleration : deceleration;
            if (pressing && Vector2.Dot(vel, desired) < 0f)
            {
                rate = Mathf.Max(acceleration, deceleration);
            }

            return Vector2.MoveTowards(vel, desired, rate * dt);
        }

        /// <summary>範囲の端の幅 softEdge に入ったら、外へ向かう分だけを弱める（内へ戻る動きはそのまま）。</summary>
        public static Vector2 SoftenAtEdge(Vector2 vel, Vector2 position, Vector2 home, float range, float softEdge)
        {
            var fromHome = position - home;
            var dist = fromHome.magnitude;
            if (dist < 1e-4f || softEdge <= 0f)
            {
                return vel;
            }

            var outward = fromHome / dist;
            var outSpeed = Vector2.Dot(vel, outward);
            if (outSpeed <= 0f)
            {
                return vel;
            }

            var room01 = Mathf.Clamp01((range - dist) / softEdge);
            // 端に近づくほどなめらかに弱まる（二乗にすると、ふわっと止まる）。
            var keep = room01 * room01;
            return vel - outward * (outSpeed * (1f - keep));
        }

        /// <summary>home を中心とした半径 range の円の中に収める。</summary>
        public static Vector2 ClampToRange(Vector2 position, Vector2 home, float range)
        {
            var fromHome = position - home;
            var max = Mathf.Max(0f, range);
            return fromHome.sqrMagnitude > max * max ? home + fromHome.normalized * max : position;
        }

        /// <summary>長方形を内側へ margin だけ縮める（体が床からはみ出さないように）。margin より細い向きは真ん中の線にする。</summary>
        public static Rect Inset(Rect rect, float margin)
        {
            var xMin = rect.xMin + margin;
            var xMax = rect.xMax - margin;
            var yMin = rect.yMin + margin;
            var yMax = rect.yMax - margin;
            if (xMin > xMax)
            {
                xMin = xMax = rect.center.x;
            }

            if (yMin > yMax)
            {
                yMin = yMax = rect.center.y;
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>円どうしの押し出し。中心 center・半径 blockRadius の丸から、半径 bodyRadius の体を外へ出す。</summary>
        public static Vector2 PushOutOfCircle(Vector2 position, float bodyRadius, Vector2 center, float blockRadius)
        {
            var reach = bodyRadius + blockRadius;
            var d = position - center;
            var distSq = d.sqrMagnitude;
            if (blockRadius <= 0f || distSq >= reach * reach)
            {
                return position;
            }

            var dist = Mathf.Sqrt(distSq);
            var dir = dist > 1e-4f ? d / dist : Vector2.up;
            return center + dir * reach;
        }

        /// <summary>押し出しの合計を速さにする（強さ × 重なり、上限つき）。</summary>
        public static Vector2 PushToVelocity(Vector2 rawPush, float strength, float maxSpeed)
        {
            var v = rawPush * strength;
            return Vector2.ClampMagnitude(v, maxSpeed);
        }

        /// <summary>なめらかに追う（指数で近づく）。フレームの長さが変わっても同じ速さで追いつく。</summary>
        public static float Blend(float sharpness, float dt)
        {
            return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, dt));
        }
    }
}
