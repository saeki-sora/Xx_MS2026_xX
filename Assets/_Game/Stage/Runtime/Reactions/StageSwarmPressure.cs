using System.Collections.Generic;
using MS2026.Fortress;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 群衆（数千体の敵）が各背景オブジェクトのまわりにどれだけ押し寄せているかを数える。
    /// 群衆の「近くの敵探し用のマス目」の人数を読むだけなので軽い。読むのは群衆の計算が止まっている安全な瞬間（StorageReadable）だけ。
    /// ネット対戦のClientでも群衆のマス目は作られるので、同じように動く（見た目だけ・通信なし）。StageRoot が自動で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSwarmPressure : MonoBehaviour
    {
        /// <summary>1つの背景オブジェクトのまわりの様子。</summary>
        public struct Sample
        {
            /// <summary>まわりにいる敵の数。</summary>
            public float Count;

            /// <summary>敵がいる向き（オブジェクトの中心から見た、人数で重み付けした平均の向き。長さ0〜1）。</summary>
            public Vector2 Direction;
        }

        private static readonly List<StagePropWobble> Listeners = new List<StagePropWobble>();

        private SwarmSystem _subscribed;

        public static void Register(StagePropWobble wobble)
        {
            if (!Listeners.Contains(wobble))
            {
                Listeners.Add(wobble);
            }
        }

        public static void Unregister(StagePropWobble wobble) => Listeners.Remove(wobble);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Listeners.Clear();

        private void Update()
        {
            var current = SwarmSystem.Current;
            if (current == _subscribed)
            {
                return;
            }

            Unsubscribe();
            if (current != null)
            {
                current.StorageReadable += OnStorageReadable;
                _subscribed = current;
            }
        }

        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_subscribed != null)
            {
                _subscribed.StorageReadable -= OnStorageReadable;
                _subscribed = null;
            }
        }

        private void OnStorageReadable()
        {
            var grid = _subscribed != null ? _subscribed.Grid : null;
            if (grid == null || !grid.CellStart.IsCreated || Listeners.Count == 0)
            {
                return;
            }

            var range = StageLookProfile.Current.pressureRange;
            for (var i = 0; i < Listeners.Count; i++)
            {
                var wobble = Listeners[i];
                if (wobble != null && wobble.TryGetPressureArea(out var area))
                {
                    wobble.ReceivePressure(Measure(grid, area, range));
                }
            }
        }

        /// <summary>area（輪郭の範囲）を range だけ広げた範囲のマス目の人数を数える。</summary>
        private static Sample Measure(SwarmSpatialGrid grid, Bounds area, float range)
        {
            var center = (Vector2)area.center;
            var min = (float2)(Vector2)area.min - range;
            var max = (float2)(Vector2)area.max + range;
            var inv = grid.InvCellSize;
            var x0 = math.max(0, (int)math.floor((min.x - grid.Origin.x) * inv));
            var y0 = math.max(0, (int)math.floor((min.y - grid.Origin.y) * inv));
            var x1 = math.min(grid.Width - 1, (int)math.floor((max.x - grid.Origin.x) * inv));
            var y1 = math.min(grid.Height - 1, (int)math.floor((max.y - grid.Origin.y) * inv));

            var total = 0f;
            var weighted = Vector2.zero;
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var count = grid.CountInCell(y * grid.Width + x);
                    if (count <= 0)
                    {
                        continue;
                    }

                    var cellCenter = new Vector2(
                        grid.Origin.x + (x + 0.5f) * grid.CellSize,
                        grid.Origin.y + (y + 0.5f) * grid.CellSize);
                    total += count;
                    weighted += (cellCenter - center).normalized * count;
                }
            }

            return new Sample
            {
                Count = total,
                Direction = total > 0f ? weighted / total : Vector2.zero
            };
        }
    }
}
