using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 近くの敵だけを高速に探すための空間ハッシュ（一様グリッド）。
    /// 1フレームに1回、全敵をセルに振り分け直す（カウンティングソート）。
    /// </summary>
    public sealed class SwarmSpatialGrid : IDisposable
    {
        private const int MaxCellsPerAxis = 512;

        public float2 Origin;
        public float CellSize = 1f;
        public int Width = 1;
        public int Height = 1;

        public NativeArray<int> CellStart;
        public NativeArray<int> Cursor;
        public NativeArray<int> CellItems;
        public NativeArray<int> CellOf;

        public SwarmSpatialGrid(int capacity)
        {
            CellItems = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            CellOf = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            ResizeCells(1, 1);
        }

        public float InvCellSize => 1f / CellSize;

        /// <summary>範囲とセルの大きさを設定する。セル数が変わったときだけ配列を作り直す。</summary>
        public void Configure(float2 origin, float2 size, float cellSize)
        {
            Origin = origin;
            CellSize = math.max(0.05f, cellSize);
            var width = math.clamp((int)math.ceil(size.x / CellSize), 1, MaxCellsPerAxis);
            var height = math.clamp((int)math.ceil(size.y / CellSize), 1, MaxCellsPerAxis);
            if (width != Width || height != Height)
            {
                ResizeCells(width, height);
            }
        }

        public int CountInCell(int cell)
        {
            return CellStart[cell + 1] - CellStart[cell];
        }

        public void Dispose()
        {
            if (CellStart.IsCreated) CellStart.Dispose();
            if (Cursor.IsCreated) Cursor.Dispose();
            if (CellItems.IsCreated) CellItems.Dispose();
            if (CellOf.IsCreated) CellOf.Dispose();
        }

        private void ResizeCells(int width, int height)
        {
            if (CellStart.IsCreated) CellStart.Dispose();
            if (Cursor.IsCreated) Cursor.Dispose();

            Width = width;
            Height = height;
            var cells = width * height;
            CellStart = new NativeArray<int>(cells + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Cursor = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }
    }

    /// <summary>全敵が属するセル番号を並列に求める。</summary>
    [BurstCompile]
    public struct SwarmCellIndexJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> pos;
        [WriteOnly] public NativeArray<int> cellOf;
        public float2 origin;
        public float invCell;
        public int width;
        public int height;

        public void Execute(int i)
        {
            var cell = SwarmMath.HashCell(pos[i], origin, invCell, width, height);
            cellOf[i] = cell.y * width + cell.x;
        }
    }

    /// <summary>セル番号から、セルごとの開始位置と、セル順に並んだ敵の番号一覧（cellItems）を作る。</summary>
    [BurstCompile]
    public struct SwarmBuildGridJob : IJob
    {
        public int count;
        public int width;
        public int height;

        [ReadOnly] public NativeArray<int> cellOf;
        public NativeArray<int> cellStart;
        public NativeArray<int> cursor;
        public NativeArray<int> cellItems;

        public void Execute()
        {
            var cells = width * height;
            for (var c = 0; c <= cells; c++)
            {
                cellStart[c] = 0;
            }

            for (var i = 0; i < count; i++)
            {
                var index = cellOf[i];
                cellStart[index + 1] = cellStart[index + 1] + 1;
            }

            for (var c = 0; c < cells; c++)
            {
                cellStart[c + 1] = cellStart[c + 1] + cellStart[c];
                cursor[c] = cellStart[c];
            }

            for (var i = 0; i < count; i++)
            {
                var index = cellOf[i];
                var slot = cursor[index];
                cellItems[slot] = i;
                cursor[index] = slot + 1;
            }
        }
    }
}
