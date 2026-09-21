using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// NavigationField（経路探索）の結果を、ジョブから読める平らな配列にコピーして保持する。
    /// 経路プロファイルごとの進行方向・移動速度倍率・壁の距離場を持つ。
    /// </summary>
    public sealed class SwarmNavigationData : IDisposable
    {
        public NativeArray<float2> FlowDirs;
        public NativeArray<float> SpeedMultiplier;
        public NativeArray<float> Sdf;
        public NativeArray<float2> SdfGradient;

        public float2 Origin;
        public float CellSize = 1f;
        public int Width = 1;
        public int Height = 1;
        public int ProfileCount;

        /// <summary>この配列の元になったNavigationField.RebuildCount。</summary>
        public int BuiltVersion = -1;

        public bool IsCreated => FlowDirs.IsCreated;
        public int CellCount => Width * Height;
        public float2 Size => new float2(Width, Height) * CellSize;

        public void Rebuild(NavigationField field, IReadOnlyList<NavigationProfile> profiles)
        {
            field.EnsureBuilt();
            var data = field.Data;

            var sameLayout = IsCreated
                             && Width == data.Width
                             && Height == data.Height
                             && ProfileCount == profiles.Count
                             && FlowDirs.Length == data.CellCount * profiles.Count;

            Width = data.Width;
            Height = data.Height;
            CellSize = data.CellSize;
            Origin = data.Origin;
            ProfileCount = profiles.Count;

            var cells = CellCount;
            if (!sameLayout)
            {
                // 大きさが変わったときだけ確保し直す。障害物の破壊・再生のたびの再計算では配列を使い回す。
                Dispose();
                FlowDirs = new NativeArray<float2>(cells * ProfileCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                SpeedMultiplier = new NativeArray<float>(cells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                Sdf = new NativeArray<float>(cells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                SdfGradient = new NativeArray<float2>(cells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            for (var p = 0; p < ProfileCount; p++)
            {
                var result = field.GetResult(profiles[p]);
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var d = result.DirectionAtCell(x, y);
                        FlowDirs[p * cells + y * Width + x] = new float2(d.x, d.y);
                    }
                }
            }

            for (var i = 0; i < cells; i++)
            {
                SpeedMultiplier[i] = data.SpeedMultiplier[i];
            }

            SwarmWallField.Build(data, Sdf, SdfGradient);
            BuiltVersion = field.RebuildCount;
        }

        /// <summary>NavigationFieldが無いときの代用。方向は全てゼロ（＝目的地へ直進）、壁は無い。</summary>
        public void CreateFallback(float2 center, float areaSize, int profileCount)
        {
            Dispose();

            Width = 1;
            Height = 1;
            CellSize = areaSize;
            Origin = center - new float2(areaSize, areaSize) * 0.5f;
            ProfileCount = math.max(1, profileCount);

            FlowDirs = new NativeArray<float2>(ProfileCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            SpeedMultiplier = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Sdf = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            SdfGradient = new NativeArray<float2>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            SpeedMultiplier[0] = 1f;
            Sdf[0] = 1000f;
            BuiltVersion = -1;
        }

        public void Dispose()
        {
            if (FlowDirs.IsCreated) FlowDirs.Dispose();
            if (SpeedMultiplier.IsCreated) SpeedMultiplier.Dispose();
            if (Sdf.IsCreated) Sdf.Dispose();
            if (SdfGradient.IsCreated) SdfGradient.Dispose();
        }
    }
}
