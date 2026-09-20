using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 経路探索用の2Dグリッド。通行不可・追加コスト・速度倍率をセルごとに持つだけの純粋なデータで、
    /// 「何がこれらを書き込むか」（障害物・地帯など）には依存しない。
    /// </summary>
    public sealed class NavigationGridData
    {
        public const int MaxCellsPerAxis = 400;

        public readonly Vector2 Origin;
        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        /// <summary>trueのセルは通行不可。</summary>
        public readonly bool[] Blocked;

        /// <summary>通行コストへの加算値（0で通常、2なら通常の3倍のコスト）。</summary>
        public readonly float[] ExtraCost;

        /// <summary>そのセル上での移動速度倍率（1で通常）。</summary>
        public readonly float[] SpeedMultiplier;

        public NavigationGridData(Vector2 center, Vector2 size, float cellSize)
        {
            CellSize = Mathf.Max(0.05f, cellSize);
            Width = Mathf.Clamp(Mathf.CeilToInt(size.x / CellSize), 1, MaxCellsPerAxis);
            Height = Mathf.Clamp(Mathf.CeilToInt(size.y / CellSize), 1, MaxCellsPerAxis);
            Origin = center - new Vector2(Width, Height) * (CellSize * 0.5f);

            var count = Width * Height;
            Blocked = new bool[count];
            ExtraCost = new float[count];
            SpeedMultiplier = new float[count];
            for (var i = 0; i < count; i++)
            {
                SpeedMultiplier[i] = 1f;
            }
        }

        public int CellCount => Width * Height;

        public Rect WorldBounds => new Rect(Origin, new Vector2(Width, Height) * CellSize);

        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool ContainsWorld(Vector2 world)
        {
            var cell = WorldToCell(world);
            return InBounds(cell.x, cell.y);
        }

        public Vector2Int WorldToCell(Vector2 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - Origin.x) / CellSize),
                Mathf.FloorToInt((world.y - Origin.y) / CellSize));
        }

        public Vector2Int WorldToCellClamped(Vector2 world)
        {
            var cell = WorldToCell(world);
            return new Vector2Int(Mathf.Clamp(cell.x, 0, Width - 1), Mathf.Clamp(cell.y, 0, Height - 1));
        }

        public Vector2 CellCenter(int x, int y)
        {
            return Origin + new Vector2(x + 0.5f, y + 0.5f) * CellSize;
        }
    }
}
