using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>目に見えているスマッシュボール1つ。</summary>
    public struct CpuBallSight
    {
        /// <summary>真ん中の位置。</summary>
        public Vector2 Position;

        /// <summary>砲台（撃ち出し口）から見た角度（度）と距離。</summary>
        public float Angle;

        public float Distance;

        /// <summary>見た目の半径（m）。</summary>
        public float Radius;

        /// <summary>残りの体力（0〜1）。</summary>
        public float Health01;

        /// <summary>ビームが途中で止まらずに届くか（ボールの手前に壁などが無いか）。</summary>
        public bool InLine;

        /// <summary>撃ち出し口から見た、ボールの幅の半分の角度（度）。</summary>
        public float HalfAngle => Distance > 0.01f ? Mathf.Atan2(Radius, Distance) * Mathf.Rad2Deg : 90f;
    }

    /// <summary>
    /// CPU の目が見た様子1回ぶん（その時刻の、砲台のまわりの敵の分布・コア・スマッシュボール）。
    /// 敵は「向き」（扇）と「向き × 距離」（輪）の2通りで数える。どちらもビームが届く所（射程の中・壁より手前）・画面に映る所だけ。
    /// </summary>
    public sealed class CpuSight
    {
        public const int DefaultRingCount = 10;

        public CpuSight(int sectorCount, int ringCount = DefaultRingCount)
        {
            Weights = new float[sectorCount];
            ObstacleDistances = new float[sectorCount];
            RingCount = Mathf.Max(1, ringCount);
            RingWeights = new float[sectorCount * RingCount];
        }

        /// <summary>見た時刻（ゲームの時間）。</summary>
        public float Time;

        /// <summary>見るたびに増える番号（0 = まだ何も見ていない）。</summary>
        public int Stamp;

        /// <summary>扇ごとの敵の重み（数。コアに近い敵は多めに数える）。角度は <see cref="CpuSectorMath"/> の決まり。</summary>
        public readonly float[] Weights;

        /// <summary>扇ごとの、ビームが壁などで止まる距離（止まらなければ射程）。</summary>
        public readonly float[] ObstacleDistances;

        /// <summary>距離の輪の数（射程を等分）。</summary>
        public readonly int RingCount;

        /// <summary>扇 × 輪ごとの敵の重み（番号 = 扇 × <see cref="RingCount"/> + 輪）。</summary>
        public readonly float[] RingWeights;

        /// <summary>見た場所（撃ち出し口）と射程。</summary>
        public Vector2 Origin;

        public float Range;

        /// <summary>見えていた敵の合計（重み）。</summary>
        public float Total;

        /// <summary>画面に映る範囲を使ったか（使えなかったときは射程の中を全部見ている）。</summary>
        public bool HasView;

        public CpuView View;

        /// <summary>コアが見えているか・その位置・体力（HUD のバーで誰でも分かる）・コアのまわりに見えている敵の数。</summary>
        public bool HasCore;

        public Vector2 CorePosition;
        public float CoreHealth01 = 1f;

        /// <summary>コアから <see cref="CpuEyes.NearCoreRadius"/> 以内に見えている敵の数（壁は関係なく、画面に映っていれば数える）。</summary>
        public float NearCoreCount;

        /// <summary>見えているスマッシュボール。</summary>
        public readonly List<CpuBallSight> Balls = new List<CpuBallSight>();

        public float RingSize => Range / RingCount;

        public int SectorCount => Weights.Length;

        /// <summary>扇 s・輪 r の真ん中の位置。</summary>
        public Vector2 CellCenter(int sector, int ring)
        {
            var angle = CpuSectorMath.CenterOf(sector, Weights.Length) * Mathf.Deg2Rad;
            var distance = (ring + 0.5f) * RingSize;
            return Origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        /// <summary>点 point のまわり radius 以内にいる敵の重み（扇 × 輪のマスの真ん中で数える、おおまかな数）。</summary>
        public float CountNear(Vector2 point, float radius)
        {
            var sum = 0f;
            var limit = radius + RingSize * 0.5f;
            var sqrLimit = limit * limit;
            for (var s = 0; s < Weights.Length; s++)
            {
                if (Weights[s] <= 0f)
                {
                    continue;
                }

                for (var r = 0; r < RingCount; r++)
                {
                    var w = RingWeights[s * RingCount + r];
                    if (w > 0f && (CellCenter(s, r) - point).sqrMagnitude <= sqrLimit)
                    {
                        sum += w;
                    }
                }
            }

            return sum;
        }

        internal void Clear()
        {
            Array.Clear(Weights, 0, Weights.Length);
            Array.Clear(RingWeights, 0, RingWeights.Length);
            Total = 0f;
            HasCore = false;
            CoreHealth01 = 1f;
            NearCoreCount = 0f;
            Balls.Clear();
        }

        internal void CopyFrom(CpuSight other)
        {
            Time = other.Time;
            Stamp = other.Stamp;
            Array.Copy(other.Weights, Weights, Weights.Length);
            Array.Copy(other.ObstacleDistances, ObstacleDistances, ObstacleDistances.Length);
            Array.Copy(other.RingWeights, RingWeights, Math.Min(RingWeights.Length, other.RingWeights.Length));
            Origin = other.Origin;
            Range = other.Range;
            Total = other.Total;
            HasView = other.HasView;
            View = other.View;
            HasCore = other.HasCore;
            CorePosition = other.CorePosition;
            CoreHealth01 = other.CoreHealth01;
            NearCoreCount = other.NearCoreCount;
            Balls.Clear();
            Balls.AddRange(other.Balls);
        }
    }
}
