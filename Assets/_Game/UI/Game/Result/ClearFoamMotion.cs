using UnityEngine;

namespace MS2026.UI.Game
{
    public static class ClearFoamMotion
    {
        private static Vector2 _cachedSize;
        private static Vector2[] _centers;
        private static float[] _radii;
        private static int _cachedColumns, _cachedRows;

        public static float Variation(int index, int salt)
        {
            unchecked
            {
                var hash = (uint)(index + salt * 7919) * 747796405u + 2891336453u;
                hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
                hash = (hash >> 22) ^ hash;
                return (hash & 0x00ffffff) / 16777215f;
            }
        }

        // 下の段から生まれ、同じ段の泡も少しずつタイミングをずらす。
        public static float Rise(float elapsed, int row, int index)
        {
            var delay = row * 0.08f + (Mathf.Sin(index * 27.31f) + 1f) * 0.05f;
            var start = ClearIntroTiming.FillStart + delay;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, ClearIntroTiming.Covered, elapsed));
        }

        public static float RisingProgress(float elapsed, Vector2 size, int index, int columns, int rows)
        {
            EnsurePacking(size, columns, rows);
            // 揺れている座標を段に丸めず、固定の到達地点から一度決めたタイミングで上昇する。
            var height = Mathf.Clamp01(_centers[index].y / size.y + 0.5f);
            var delay = height * 0.55f + Variation(index, 11) * 0.1f;
            var t = Mathf.InverseLerp(ClearIntroTiming.FillStart + delay, ClearIntroTiming.Covered, elapsed);
            // 速度・加速度とも開始と終了で0になる曲線で、飛び出しと急停止を抑える。
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static Vector2 RisingPosition(float elapsed, Vector2 size, int index, int columns, int rows)
        {
            var progress = RisingProgress(elapsed, size, index, columns, rows);
            var target = FilledPosition(size, index, columns, rows, elapsed);
            var radius = Diameter(size, index, columns, rows) * 0.5f;
            // 大きい泡も完全に画面の下から生まれる。
            var origin = new Vector2(_centers[index].x, -size.y * 0.5f - radius - 16f);
            var position = Vector2.Lerp(origin, target, progress);
            // 近い横位置の泡が同じ流れに乗り、大小の房として沸き上がる。
            var stream = Mathf.Sin(_centers[index].x / size.x * Mathf.PI * 5f);
            var drift = (stream * 0.7f + (Variation(index, 12) - 0.5f) * 0.3f) * size.x * 0.04f;
            var envelope = Mathf.Sin(progress * Mathf.PI);
            position.x += envelope * drift;
            position.y += envelope * envelope * Mathf.Sin(progress * Mathf.PI * 2f + stream) * size.y * 0.018f;
            // Small eddies follow the shared upward stream, fading out at both ends.
            var eddy = progress * Mathf.PI * 3f + stream * 1.7f;
            position.x += Mathf.Sin(eddy) * envelope * envelope * radius * 0.12f;
            return position;
        }

        // 中央から外へ膜の破裂を伝播させる。
        public static float Pop(float elapsed, Vector2 normalizedPosition)
        {
            var distance = Mathf.Clamp01(normalizedPosition.magnitude);
            var start = ClearIntroTiming.OpenStart + distance * 0.8f;
            return Mathf.InverseLerp(start, start + 0.3f, elapsed);
        }

        public static Vector2 FilledPosition(Vector2 size, int index, int columns, int rows, float time)
        {
            EnsurePacking(size, columns, rows);
            var center = _centers[index];
            var flowPhase = center.x / size.x * 5f + center.y / size.y * 3f;
            var detail = Variation(index, 24) * Mathf.PI * 2f;
            return center + new Vector2(
                Mathf.Sin(time * 1.25f + flowPhase) * 6f + Mathf.Sin(time * 1.8f + detail) * 2f,
                Mathf.Cos(time * 1.1f + flowPhase) * 4f + Mathf.Sin(time * 1.6f + detail) * 1.5f);
        }

        public static float Diameter(Vector2 size, int index, int columns, int rows)
        {
            EnsurePacking(size, columns, rows);
            return _radii[index] * 2f;
        }

        private static void EnsurePacking(Vector2 size, int columns, int rows)
        {
            if (_centers != null && _cachedSize == size && _cachedColumns == columns && _cachedRows == rows) return;
            _cachedSize = size;
            _cachedColumns = columns;
            _cachedRows = rows;
            var count = columns * rows;
            _centers = new Vector2[count];
            _radii = new float[count];
            for (var i = 0; i < count; i++)
            {
                // 整列した列・段を使わず、画面全体へ不規則に分散する。
                var x = Mathf.Repeat(0.5f + i * 0.754877666f + Variation(i, 1) * 0.22f, 1f);
                var y = Mathf.Repeat(0.5f + i * 0.569840296f + Variation(i, 2) * 0.22f, 1f);
                _centers[i] = new Vector2((x * 1.08f - 0.54f) * size.x, (y * 1.08f - 0.54f) * size.y);
                var densityScale = Mathf.Sqrt(216f / count);
                _radii[i] = Mathf.Min(size.x, size.y) * densityScale * Mathf.Lerp(0.025f, 0.07f, Variation(i, 3));
            }
            const int samplesX = 48, samplesY = 28;
            var sampleMargin = new Vector2(size.x / samplesX, size.y / samplesY).magnitude * 0.5f;
            // 各サンプル点を担当する泡に必要半径を持たせる。点の間と揺れの分まで余白を足す。
            // 不規則配置のまま、背景が見える穴ができないように敷き詰める。
            for (var y = 0; y <= samplesY; y++)
            for (var x = 0; x <= samplesX; x++)
            {
                var point = new Vector2((x / (float)samplesX - 0.5f) * size.x, (y / (float)samplesY - 0.5f) * size.y);
                var nearest = 0;
                var distanceSq = float.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    var candidate = (point - _centers[i]).sqrMagnitude;
                    if (candidate >= distanceSq) continue;
                    distanceSq = candidate;
                    nearest = i;
                }
                _radii[nearest] = Mathf.Max(_radii[nearest], Mathf.Sqrt(distanceSq) + sampleMargin + 12f);
            }
            for (var i = 0; i < count; i++)
                _radii[i] *= Mathf.Lerp(1f, 1.18f, Variation(i, 4));
        }
    }
}
