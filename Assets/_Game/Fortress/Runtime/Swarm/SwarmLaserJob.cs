using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザー（太さを持つ線分）に触れている敵にダメージを与える。空間ハッシュを使い、
    /// ビームの周囲のセルだけを調べる。maxHitsが正のときは、ビームの始点に近い順にその数だけ当てる。
    /// あわせて、被弾フラッシュが消えていた敵に当たった「瞬間」を newHits に記録する（maxNewHits 件まで。演出用）。
    /// 記録の枠を特定のレーザーが使い切らないよう、調べるビームの順番を beamOffset でフレームごとにずらす。
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

        public NativeArray<SwarmBeamHit> newHits;
        public NativeArray<int> newHitCount;
        public int maxNewHits;
        public int beamOffset;

        public void Execute()
        {
            newHitCount[0] = 0;
            for (var n = 0; n < beamCount; n++)
            {
                ApplyBeam(beams[(n + beamOffset) % beamCount]);
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
                            Hit(j, beam.owner, damage);
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
                Hit(hitIdx[n], beam.owner, damage);
            }
        }

        private void Hit(int j, int owner, float damage)
        {
            // 被弾フラッシュが消えていた(=しばらく当たっていなかった)敵に当たった瞬間だけ記録する。
            // 同じフレームに2本目のビームが当たっても、1本目でフラッシュが付くので二重には記録されない。
            if (flash[j] <= 0f)
            {
                var count = newHitCount[0];
                if (count < maxNewHits)
                {
                    newHits[count] = new SwarmBeamHit { position = pos[j], owner = owner };
                    newHitCount[0] = count + 1;
                }
            }

            hp[j] = hp[j] - damage;
            flash[j] = 1f;
        }
    }
}
