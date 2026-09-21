using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザー（太さを持つ線分）に触れている敵にダメージを与える。空間ハッシュを使い、
    /// ビームの周囲のセルだけを調べる。maxHitsが正のときは、ビームの始点に近い順にその数だけ当てる。
    /// </summary>
    [BurstCompile]
    public struct SwarmLaserJob : IJob
    {
        [ReadOnly] public NativeArray<float2> pos;
        public NativeArray<float> hp;
        public NativeArray<float> flash;
        [ReadOnly] public NativeArray<int> typeIdx;
        [ReadOnly] public NativeArray<SwarmTypeParams> types;
        [ReadOnly] public NativeArray<SwarmBeam> beams;
        public int beamCount;

        [ReadOnly] public NativeArray<int> cellStart;
        [ReadOnly] public NativeArray<int> cellItems;
        public float2 hashOrigin;
        public float hashInvCell;
        public int hashW;
        public int hashH;

        public float maxRadius;
        public float dt;

        public NativeArray<float> hitT;
        public NativeArray<int> hitIdx;

        public void Execute()
        {
            for (var b = 0; b < beamCount; b++)
            {
                ApplyBeam(beams[b]);
            }
        }

        private void ApplyBeam(SwarmBeam beam)
        {
            var a = beam.origin;
            var ab = beam.end - a;
            var lengthSq = math.lengthsq(ab);
            var margin = beam.halfWidth + maxRadius;
            var minCell = SwarmMath.HashCell(math.min(a, beam.end) - margin, hashOrigin, hashInvCell, hashW, hashH);
            var maxCell = SwarmMath.HashCell(math.max(a, beam.end) + margin, hashOrigin, hashInvCell, hashW, hashH);

            var limited = beam.maxHits > 0;
            var cap = math.min(beam.maxHits, hitT.Length);
            var buffered = 0;
            var damage = beam.damagePerSecond * dt;

            for (var y = minCell.y; y <= maxCell.y; y++)
            {
                for (var x = minCell.x; x <= maxCell.x; x++)
                {
                    var c = y * hashW + x;
                    var end = cellStart[c + 1];
                    for (var k = cellStart[c]; k < end; k++)
                    {
                        var j = cellItems[k];
                        var p = pos[j];
                        var t = lengthSq > 1e-8f ? math.clamp(math.dot(p - a, ab) / lengthSq, 0f, 1f) : 0f;
                        var reach = beam.halfWidth + types[typeIdx[j]].radius;
                        if (math.distancesq(p, a + ab * t) > reach * reach)
                        {
                            continue;
                        }

                        if (!limited)
                        {
                            hp[j] = hp[j] - damage;
                            flash[j] = 1f;
                            continue;
                        }

                        int slot;
                        if (buffered < cap)
                        {
                            slot = buffered;
                            buffered++;
                        }
                        else if (t < hitT[cap - 1])
                        {
                            slot = cap - 1;
                        }
                        else
                        {
                            continue;
                        }

                        while (slot > 0 && hitT[slot - 1] > t)
                        {
                            hitT[slot] = hitT[slot - 1];
                            hitIdx[slot] = hitIdx[slot - 1];
                            slot--;
                        }

                        hitT[slot] = t;
                        hitIdx[slot] = j;
                    }
                }
            }

            for (var n = 0; n < buffered; n++)
            {
                var target = hitIdx[n];
                hp[target] = hp[target] - damage;
                flash[target] = 1f;
            }
        }
    }
}
