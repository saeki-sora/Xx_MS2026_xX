using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で動ける範囲。
    /// ・枠（<see cref="Frames"/>）があれば、枠の内側だけ（ワールドの XY の多角形。枠が何個かあればどれかの内側）。
    /// ・枠が無ければ、床の長方形（<see cref="Rects"/>）を合わせた全域（つなぎ目では止まらない）。
    /// どちらも「体の半径 margin だけ内側」まで。
    /// </summary>
    public sealed class TurretMoveRegion
    {
        private const int EdgeSamples = 6;
        private const int ClampIterations = 3;

        /// <summary>枠の内側の輪郭（閉じた多角形。向きはどちらでもよい）。<see cref="AddFrame"/> で足す。</summary>
        public readonly List<Vector2[]> Frames = new List<Vector2[]>();

        /// <summary>枠の形（内側・外側のふち）。<see cref="Frames"/> と同じ順。</summary>
        public readonly List<TurretMoveFrame> Shapes = new List<TurretMoveFrame>();

        /// <summary>床の範囲（枠が無いときだけ使う）。</summary>
        public readonly List<Rect> Rects = new List<Rect>();

        public bool IsEmpty => Frames.Count == 0 && Rects.Count == 0;

        public void AddFrame(TurretMoveFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            Shapes.Add(frame);
            Frames.Add(frame.InnerPolygon);
        }

        public void Clear()
        {
            Frames.Clear();
            Shapes.Clear();
            Rects.Clear();
        }

        /// <summary>体（半径 margin）がまるごと範囲の中に入るか。</summary>
        public bool Contains(Vector2 p, float margin)
        {
            if (Frames.Count > 0)
            {
                for (var i = 0; i < Frames.Count; i++)
                {
                    var poly = Frames[i];
                    if (PolygonContains(poly, p) && DistanceToEdges(poly, p) >= margin)
                    {
                        return true;
                    }
                }

                return false;
            }

            for (var i = 0; i < Rects.Count; i++)
            {
                var r = TurretMoveMath.Inset(Rects[i], margin);
                if (p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>範囲から出ていたら、いちばん近い「入っていられる場所」へ戻す。</summary>
        public Vector2 Clamp(Vector2 p, float margin)
        {
            if (IsEmpty || Contains(p, margin))
            {
                return p;
            }

            var best = p;
            var bestSq = float.PositiveInfinity;
            if (Frames.Count > 0)
            {
                for (var i = 0; i < Frames.Count; i++)
                {
                    var candidate = ClampIntoPolygon(Frames[i], p, margin);
                    var sq = (candidate - p).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = candidate;
                    }
                }

                return best;
            }

            for (var i = 0; i < Rects.Count; i++)
            {
                var r = TurretMoveMath.Inset(Rects[i], margin);
                var candidate = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
                var sq = (candidate - p).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 範囲の端の幅 softEdge に入ったら、外へ向かう分だけを横・縦それぞれで弱める（端に沿って滑る動きはそのまま）。
        /// </summary>
        public Vector2 Soften(Vector2 vel, Vector2 p, float margin, float softEdge)
        {
            if (softEdge <= 0f || IsEmpty || !Contains(p, margin))
            {
                return vel;
            }

            var keepX = Room01(p, new Vector2(Mathf.Sign(vel.x), 0f), margin, softEdge, vel.x);
            var keepY = Room01(p, new Vector2(0f, Mathf.Sign(vel.y)), margin, softEdge, vel.y);
            return new Vector2(vel.x * keepX * keepX, vel.y * keepY * keepY);
        }

        // その向きへ softEdge 進む間に範囲から出るなら、出るまでの割合（0〜1）。出なければ 1。
        private float Room01(Vector2 p, Vector2 dir, float margin, float softEdge, float speed)
        {
            if (Mathf.Abs(speed) < 1e-5f)
            {
                return 1f;
            }

            for (var k = 1; k <= EdgeSamples; k++)
            {
                if (!Contains(p + dir * (softEdge * k / EdgeSamples), margin))
                {
                    return (k - 1) / (float)EdgeSamples;
                }
            }

            return 1f;
        }

        // ───────── 多角形 ─────────

        // いちばん近い縁から、内向きに margin だけ入った所。へこみの角では1回で収まらないことがあるので、数回くり返す。
        private static Vector2 ClampIntoPolygon(Vector2[] poly, Vector2 p, float margin)
        {
            var inwardSign = SignedArea(poly) >= 0f ? 1f : -1f;
            var q = p;
            for (var iteration = 0; iteration < ClampIterations; iteration++)
            {
                if (PolygonContains(poly, q) && DistanceToEdges(poly, q) >= margin - 1e-4f)
                {
                    break;
                }

                ClosestOnEdges(poly, q, out var closest, out var edgeDir);
                // 反時計回りの多角形なら、辺の左側が内側。
                var inward = new Vector2(-edgeDir.y, edgeDir.x) * inwardSign;
                q = closest + inward * margin;
            }

            return q;
        }

        public static bool PolygonContains(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistanceToEdges(Vector2[] poly, Vector2 p)
        {
            ClosestOnEdges(poly, p, out var closest, out _);
            return (closest - p).magnitude;
        }

        private static void ClosestOnEdges(Vector2[] poly, Vector2 p, out Vector2 closest, out Vector2 edgeDir)
        {
            closest = p;
            edgeDir = Vector2.right;
            var bestSq = float.PositiveInfinity;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var b = poly[i];
                var ab = b - a;
                var lengthSq = ab.sqrMagnitude;
                var t = lengthSq > 1e-10f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                var point = a + ab * t;
                var sq = (point - p).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    closest = point;
                    edgeDir = lengthSq > 1e-10f ? ab / Mathf.Sqrt(lengthSq) : Vector2.right;
                }
            }
        }

        private static float SignedArea(Vector2[] poly)
        {
            var area = 0f;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                area += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
            }

            return area * 0.5f;
        }
    }
}
