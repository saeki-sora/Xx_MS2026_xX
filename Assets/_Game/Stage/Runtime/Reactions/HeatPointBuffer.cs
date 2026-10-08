using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// レーザーが当たった「熱の点」を最大 N 個覚えておく純粋な計算クラス。
    /// 当て続けると光り（glow）が上がり、光っている間に焦げ（scorch）が少しずつ溜まる。光りは冷めるが焦げは残る。
    /// 近くに当たったら同じ点にまとめ、いっぱいなら一番弱い点を置き換える。
    /// </summary>
    public sealed class HeatPointBuffer
    {
        public struct Point
        {
            public Vector3 Position;
            public float Radius;
            public float Glow;
            public float Scorch;
        }

        private readonly Point[] _points;

        public HeatPointBuffer(int capacity)
        {
            _points = new Point[capacity];
        }

        public int Count { get; private set; }

        public Point this[int index] => _points[index];

        /// <summary>光り（glow）が残っている点があるか。</summary>
        public bool IsHot
        {
            get
            {
                for (var i = 0; i < Count; i++)
                {
                    if (_points[i].Glow > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <param name="position">当たった場所。</param>
        /// <param name="glowGain">増える光り（0..1の範囲に収まる）。</param>
        /// <param name="radius">点の広がり（ワールド単位）。</param>
        /// <param name="mergeDistance">この距離以内なら既存の点にまとめる。</param>
        public void Add(Vector3 position, float glowGain, float radius, float mergeDistance)
        {
            var nearest = -1;
            var nearestSq = mergeDistance * mergeDistance;
            for (var i = 0; i < Count; i++)
            {
                var d = (_points[i].Position - position).sqrMagnitude;
                if (d <= nearestSq)
                {
                    nearestSq = d;
                    nearest = i;
                }
            }

            if (nearest >= 0)
            {
                ref var p = ref _points[nearest];
                // レーザーが動いていれば、点もそちらへ少し寄せる（焦げ跡が帯のように伸びる）。
                p.Position = Vector3.Lerp(p.Position, position, 0.25f);
                p.Radius = Mathf.Max(p.Radius, radius);
                p.Glow = Mathf.Min(1f, p.Glow + glowGain);
                return;
            }

            var slot = Count < _points.Length ? Count++ : WeakestIndex();
            _points[slot] = new Point { Position = position, Radius = radius, Glow = Mathf.Min(1f, glowGain), Scorch = 0f };
        }

        /// <summary>時間を進める: 光りが冷め、光っている間は焦げが溜まる。</summary>
        public void Tick(float deltaTime, float coolPerSecond, float scorchPerSecond)
        {
            for (var i = 0; i < Count; i++)
            {
                ref var p = ref _points[i];
                if (p.Glow <= 0f)
                {
                    continue;
                }

                p.Scorch = Mathf.Min(1f, p.Scorch + p.Glow * scorchPerSecond * deltaTime);
                p.Glow = Mathf.Max(0f, p.Glow - coolPerSecond * deltaTime);
            }

            // 光りも焦げも無い点は捨てる。
            for (var i = Count - 1; i >= 0; i--)
            {
                if (_points[i].Glow <= 0f && _points[i].Scorch <= 0.001f)
                {
                    _points[i] = _points[Count - 1];
                    Count--;
                }
            }
        }

        public void Clear() => Count = 0;

        /// <summary>シェーダー用の配列に詰める（xyz=位置 w=半径 / x=光り y=焦げ）。</summary>
        public void CopyTo(Vector4[] points, Vector4[] values)
        {
            for (var i = 0; i < points.Length; i++)
            {
                if (i < Count)
                {
                    var p = _points[i];
                    points[i] = new Vector4(p.Position.x, p.Position.y, p.Position.z, p.Radius);
                    values[i] = new Vector4(p.Glow, p.Scorch, 0f, 0f);
                }
                else
                {
                    points[i] = Vector4.zero;
                    values[i] = Vector4.zero;
                }
            }
        }

        private int WeakestIndex()
        {
            var index = 0;
            var weakest = float.PositiveInfinity;
            for (var i = 0; i < Count; i++)
            {
                var strength = _points[i].Glow * 2f + _points[i].Scorch;
                if (strength < weakest)
                {
                    weakest = strength;
                    index = i;
                }
            }

            return index;
        }
    }
}
