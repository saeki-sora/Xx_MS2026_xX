using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 本番アセットが用意されるまでのプレースホルダー図形を生成する。
    /// 各コンポーネントの visualPrefab / sprite フィールドに本番アセットを設定すれば、
    /// このプレースホルダーは使われなくなる（差し替え前提の設計）。
    /// </summary>
    public static class PlaceholderSpriteFactory
    {
        private static Sprite _circleSprite;
        private static Sprite _squareSprite;
        private static Sprite _dashedSquareSprite;
        private static Sprite _triangleHalfSprite;
        private static Sprite _crackOverlaySprite;

        public static Sprite CreateCircleSprite()
        {
            if (_circleSprite != null)
            {
                return _circleSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderCircle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2(size / 2f, size / 2f);
            var radius = size / 2f - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    var alpha = Mathf.Clamp01(radius - dist + 1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderCircleSprite";
            _circleSprite = sprite;
            return _circleSprite;
        }

        /// <summary>地形障害物の本体用。塗りつぶしの正方形(1x1ワールド単位@スケール1)。</summary>
        public static Sprite CreateSquareSprite()
        {
            if (_squareSprite != null)
            {
                return _squareSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderSquare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = white;
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderSquareSprite";
            _squareSprite = sprite;
            return _squareSprite;
        }

        /// <summary>
        /// 地形障害物が破壊されている間の「ここに再生する」ゴースト表示用。破線の枠だけの正方形。
        /// </summary>
        public static Sprite CreateDashedSquareSprite()
        {
            if (_dashedSquareSprite != null)
            {
                return _dashedSquareSprite;
            }

            const int size = 64;
            const int borderThickness = 4;
            const int dashLength = 6;
            const int gapLength = 4;
            const int cycle = dashLength + gapLength;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderDashedSquare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);

            void SetPixel(int x, int y)
            {
                pixels[y * size + x] = white;
            }

            var perimeterIndex = 0;

            // 下辺 → 右辺 → 上辺 → 左辺の順に一周し、一定間隔で破線状に塗る。
            for (var x = 0; x < size; x++, perimeterIndex++)
            {
                if (perimeterIndex % cycle < dashLength)
                {
                    for (var t = 0; t < borderThickness; t++)
                    {
                        SetPixel(x, t);
                    }
                }
            }

            for (var y = 0; y < size; y++, perimeterIndex++)
            {
                if (perimeterIndex % cycle < dashLength)
                {
                    for (var t = 0; t < borderThickness; t++)
                    {
                        SetPixel(size - 1 - t, y);
                    }
                }
            }

            for (var x = size - 1; x >= 0; x--, perimeterIndex++)
            {
                if (perimeterIndex % cycle < dashLength)
                {
                    for (var t = 0; t < borderThickness; t++)
                    {
                        SetPixel(x, size - 1 - t);
                    }
                }
            }

            for (var y = size - 1; y >= 0; y--, perimeterIndex++)
            {
                if (perimeterIndex % cycle < dashLength)
                {
                    for (var t = 0; t < borderThickness; t++)
                    {
                        SetPixel(t, y);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderDashedSquareSprite";
            _dashedSquareSprite = sprite;
            return _dashedSquareSprite;
        }

        /// <summary>
        /// 正方形を対角線で割った片方の直角三角形。破壊エフェクト(<see cref="ObstacleBreakEffect"/>)用。
        /// この1枚を0度・180度で2つ並べれば、ちょうど元の正方形を斜めに割った2破片になる。
        /// </summary>
        public static Sprite CreateTriangleHalfSprite()
        {
            if (_triangleHalfSprite != null)
            {
                return _triangleHalfSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderTriangleHalf",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isLowerLeftHalf = (x + 0.5f) + (y + 0.5f) <= size;
                    if (isLowerLeftHalf)
                    {
                        pixels[y * size + x] = white;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderTriangleHalfSprite";
            _triangleHalfSprite = sprite;
            return _triangleHalfSprite;
        }

        /// <summary>
        /// 地形障害物がダメージを受けるほど濃くなる、ひび割れの重ね表示用。中心付近から放射状に
        /// ジグザグなひびが伸びる模様を毎回同じ形で生成する(固定シード)。
        /// </summary>
        public static Sprite CreateCrackOverlaySprite()
        {
            if (_crackOverlaySprite != null)
            {
                return _crackOverlaySprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Fortress_PlaceholderCrackOverlay",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            var crackColor = new Color32(25, 22, 20, 255);
            var random = new System.Random(20260918); // 常に同じひび模様になるよう固定シード

            const int crackCount = 6;
            var center = new Vector2(size * 0.5f, size * 0.5f);

            for (var c = 0; c < crackCount; c++)
            {
                var angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var position = center + dir * (size * 0.06f);

                var steps = 6 + random.Next(0, 5);
                var stepLength = size * 0.5f / steps;

                for (var s = 0; s < steps; s++)
                {
                    var jitterAngle = ((float)random.NextDouble() - 0.5f) * 1.1f;
                    dir = RotateDirection(dir, jitterAngle);

                    var next = position + dir * stepLength;
                    DrawCrackSegment(pixels, size, position, next, crackColor);
                    position = next;

                    if (position.x < 0f || position.x >= size || position.y < 0f || position.y >= size)
                    {
                        break;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "Fortress_PlaceholderCrackOverlaySprite";
            _crackOverlaySprite = sprite;
            return _crackOverlaySprite;
        }

        private static Vector2 RotateDirection(Vector2 dir, float radians)
        {
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }

        private static void DrawCrackSegment(Color32[] pixels, int size, Vector2 from, Vector2 to, Color32 color)
        {
            var distance = Vector2.Distance(from, to);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance));

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var p = Vector2.Lerp(from, to, t);
                var x = Mathf.RoundToInt(p.x);
                var y = Mathf.RoundToInt(p.y);

                if (x < 0 || x >= size || y < 0 || y >= size)
                {
                    continue;
                }

                pixels[y * size + x] = color;
            }
        }
    }
}
