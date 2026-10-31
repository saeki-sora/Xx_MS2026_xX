using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>盾の板1枚分（盾の中の座標。+X が正面）。</summary>
    public struct ShieldBox
    {
        public Vector2 center;
        public Vector2 size;
    }

    /// <summary>
    /// 盾持ちの計算（純粋な関数。テストあり）: 盾の形、お供の並ぶ場所、にらむ相手の選び方、向きの回し方。
    /// </summary>
    public static class EnemyShieldMath
    {
        /// <summary>にらむ相手を決めるのに必要な「当てた量」の下限（秒ぶん）。これ未満なら誰もにらまない（一番近い砲台を向く）。</summary>
        public const float MinThreat = 0.08f;

        /// <summary>コの字の板3枚（正面・左・右）。左右の板は正面の板の端から後ろへ伸びる。</summary>
        public static void Boxes(EnemyShieldSettings s, ShieldBox[] into)
        {
            var t = s.thickness;
            var front = s.frontDistance;
            var half = s.halfWidth;
            var side = Mathf.Max(0f, s.sideLength);
            into[0] = new ShieldBox { center = new Vector2(front, 0f), size = new Vector2(t, 2f * half + t) };
            into[1] = new ShieldBox { center = new Vector2(front - side * 0.5f, half), size = new Vector2(side + t, t) };
            into[2] = new ShieldBox { center = new Vector2(front - side * 0.5f, -half), size = new Vector2(side + t, t) };
        }

        /// <summary>
        /// k 番目（0 始まり）のお供の場所（盾持ちの中心から、進む向きを +X とした座標）。
        /// 1つ目の輪に6体、2つ目に12体…と外へ並ぶ。輪ごとに半歩ずらす。
        /// </summary>
        public static float2 EscortSlot(int k, float spacing)
        {
            var ring = 1;
            var first = 0;
            while (k >= first + 6 * ring)
            {
                first += 6 * ring;
                ring++;
            }

            var count = 6 * ring;
            var step = 2f * math.PI / count;
            var angle = (k - first + (ring % 2 == 0 ? 0.5f : 0f)) * step;
            var radius = spacing * ring + 0.06f;
            return new float2(math.cos(angle), math.sin(angle)) * radius;
        }

        /// <summary>点 p から線分 ab までの距離の2乗。</summary>
        public static float SegmentDistanceSq(float2 p, float2 a, float2 b)
        {
            var ab = b - a;
            var len = math.lengthsq(ab);
            var u = len > 1e-8f ? math.saturate(math.dot(p - a, ab) / len) : 0f;
            return math.distancesq(p, a + ab * u);
        }

        /// <summary>当てた量を時間で薄める（memorySeconds で 1/e）。</summary>
        public static void Decay(float[] threat, float dt, float memorySeconds)
        {
            var k = math.exp(-dt / math.max(0.05f, memorySeconds));
            for (var i = 0; i < threat.Length; i++)
            {
                threat[i] *= k;
            }
        }

        /// <summary>
        /// にらむ相手（プレイヤー番号）。いちばん当ててきている人。ただし今の相手から変えるのは、新しい人が switchRatio 倍を超えたときだけ
        /// （同じくらいの2人の間で首を振り続けないように）。誰も <see cref="MinThreat"/> に届かなければ fallback（一番近い砲台）。
        /// </summary>
        public static int ChooseFocus(float[] threat, int current, float switchRatio, int fallback)
        {
            var best = -1;
            var bestValue = MinThreat;
            for (var i = 0; i < threat.Length; i++)
            {
                if (threat[i] > bestValue)
                {
                    bestValue = threat[i];
                    best = i;
                }
            }

            if (best < 0)
            {
                return current >= 0 && current < threat.Length && threat[current] > MinThreat * 0.5f ? current : fallback;
            }

            if (current >= 0 && current < threat.Length && best != current && threat[best] < threat[current] * switchRatio)
            {
                return current;
            }

            return best;
        }

        /// <summary>角度（度）を目標へ最大 maxStep 度だけ回す（近い回り方で）。</summary>
        public static float TurnToward(float angleDegrees, float targetDegrees, float maxStepDegrees)
        {
            var delta = Mathf.DeltaAngle(angleDegrees, targetDegrees);
            return angleDegrees + Mathf.Clamp(delta, -maxStepDegrees, maxStepDegrees);
        }
    }
}
