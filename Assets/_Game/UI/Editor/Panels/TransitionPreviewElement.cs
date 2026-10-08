using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 画面切り替えの幕を、小さな画面の上で繰り返し再生して見せる（模様の雰囲気を見比べる用。本物はPlayで確認）。
    /// 覆う → 少し待つ → 開く → 少し待つ を繰り返す。描画は Painter2D。
    /// </summary>
    public sealed class TransitionPreviewElement : VisualElement
    {
        private const float Pause = 0.35f;

        private readonly UiScreenTransition _transition;
        private readonly double _startTime;

        public TransitionPreviewElement(UiScreenTransition transition, float width = 196f, float height = 110f)
        {
            _transition = transition;
            _startTime = EditorApplication.timeSinceStartup;
            style.width = width;
            style.height = height;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 6;
            style.overflow = Overflow.Hidden;
            style.marginBottom = 6;
            generateVisualContent += Draw;
            schedule.Execute(MarkDirtyRepaint).Every(33);
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_transition == null)
            {
                return;
            }

            var rect = contentRect;
            var painter = context.painter2D;
            DrawScreen(painter, rect);

            var (progress, revealing) = Progress();
            var eased = _transition.ease != null && _transition.ease.length > 0 ? _transition.ease.Evaluate(progress) : progress;
            if (eased <= 0.001f)
            {
                return;
            }

            var color = _transition.color;
            switch (_transition.pattern)
            {
                case UiTransitionPattern.Fade:
                case UiTransitionPattern.RuleTexture:
                    FillRect(painter, rect, new Color(color.r, color.g, color.b, color.a * eased));
                    break;
                case UiTransitionPattern.Wipe:
                    DrawWipe(painter, rect, eased, revealing, color);
                    break;
                case UiTransitionPattern.Iris:
                    DrawIris(painter, rect, eased, color);
                    break;
                case UiTransitionPattern.Blinds:
                    DrawBlinds(painter, rect, eased, color);
                    break;
                case UiTransitionPattern.Diamonds:
                    DrawDiamonds(painter, rect, eased, color);
                    break;
                case UiTransitionPattern.Burn:
                    DrawBurn(painter, rect, eased, color);
                    break;
            }
        }

        /// <summary>今の進み具合（0〜1）と、開いている途中か。</summary>
        private (float progress, bool revealing) Progress()
        {
            var cover = Mathf.Max(0.05f, _transition.coverSeconds);
            var hold = Mathf.Max(Pause, _transition.holdSeconds);
            var reveal = Mathf.Max(0.05f, _transition.revealSeconds);
            var cycle = cover + hold + reveal + Pause;
            var t = (float)((EditorApplication.timeSinceStartup - _startTime) % cycle);
            if (t < cover) return (t / cover, false);
            if (t < cover + hold) return (1f, false);
            if (t < cover + hold + reveal) return (1f - (t - cover - hold) / reveal, true);
            return (0f, true);
        }

        private static void DrawScreen(Painter2D painter, Rect rect)
        {
            FillRect(painter, rect, new Color(0.2f, 0.42f, 0.55f));
            FillRect(painter, new Rect(rect.x + rect.width * 0.1f, rect.y + rect.height * 0.62f, rect.width * 0.8f, rect.height * 0.14f), new Color(1f, 0.48f, 0.24f));
            FillRect(painter, new Rect(rect.x + rect.width * 0.3f, rect.y + rect.height * 0.2f, rect.width * 0.4f, rect.height * 0.22f), new Color(0.93f, 0.94f, 0.95f, 0.9f));
        }

        private void DrawWipe(Painter2D painter, Rect rect, float p, bool revealing, Color color)
        {
            var angle = (_transition.angle + (revealing && _transition.revealPassThrough ? 180f : 0f)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)); // 画面は上が -Y
            var center = rect.center;
            var extent = Mathf.Abs(dir.x) * rect.width * 0.5f + Mathf.Abs(dir.y) * rect.height * 0.5f;
            var threshold = -extent + p * 2f * extent;
            var corners = new List<Vector2> { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
            FillPolygon(painter, ClipBelow(corners, center, dir, threshold), color);
        }

        private void DrawIris(Painter2D painter, Rect rect, float p, Color color)
        {
            var center = new Vector2(rect.x + rect.width * _transition.center.x, rect.y + rect.height * (1f - _transition.center.y));
            var max = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height);
            var radius = Mathf.Max(0f, (1f - p) * max);
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            if (radius > 0.5f)
            {
                painter.MoveTo(center + new Vector2(radius, 0f));
                painter.Arc(center, radius, 0f, 360f);
                painter.ClosePath();
            }

            painter.Fill(FillRule.OddEven);
        }

        private void DrawBlinds(Painter2D painter, Rect rect, float p, Color color)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(_transition.count));
            var vertical = Mathf.Abs(Mathf.Cos(_transition.angle * Mathf.Deg2Rad)) > 0.7f;
            for (var i = 0; i < count; i++)
            {
                if (vertical)
                {
                    var w = rect.width / count;
                    FillRect(painter, new Rect(rect.x + i * w, rect.y, w * p, rect.height), color);
                }
                else
                {
                    var h = rect.height / count;
                    FillRect(painter, new Rect(rect.x, rect.y + i * h, rect.width, h * p), color);
                }
            }
        }

        private void DrawDiamonds(Painter2D painter, Rect rect, float p, Color color)
        {
            var rows = Mathf.Max(2, Mathf.RoundToInt(_transition.count * 0.6f));
            var size = rect.height / rows;
            var cols = Mathf.CeilToInt(rect.width / size);
            for (var y = 0; y <= rows; y++)
            {
                for (var x = 0; x <= cols; x++)
                {
                    var sweep = (x + y) / (float)(cols + rows);
                    var local = Mathf.Clamp01(p * 2f - sweep);
                    if (local <= 0f)
                    {
                        continue;
                    }

                    var c = new Vector2(rect.x + x * size, rect.y + y * size);
                    var r = local * size * 0.75f;
                    FillPolygon(painter, new List<Vector2> { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) }, color);
                }
            }
        }

        private void DrawBurn(Painter2D painter, Rect rect, float p, Color color)
        {
            var random = new System.Random(7);
            var edge = _transition.edgeColor;
            var glow = new Color(Mathf.Min(1f, edge.r), Mathf.Min(1f, edge.g), Mathf.Min(1f, edge.b), 1f);
            for (var i = 0; i < 28; i++)
            {
                var c = new Vector2(rect.x + (float)random.NextDouble() * rect.width, rect.y + (float)random.NextDouble() * rect.height);
                var start = (float)random.NextDouble() * 0.5f;
                var local = Mathf.Clamp01((p - start) / (1f - start));
                if (local <= 0f)
                {
                    continue;
                }

                var r = local * rect.width * 0.35f;
                DrawCircle(painter, c, r + 2.5f, glow);
                DrawCircle(painter, c, r, color);
            }

            if (p >= 0.999f)
            {
                FillRect(painter, rect, color);
            }
        }

        /// <summary>多角形のうち、dot(点−中心, dir) が threshold より小さい部分だけ残す。</summary>
        private static List<Vector2> ClipBelow(List<Vector2> polygon, Vector2 center, Vector2 dir, float threshold)
        {
            var result = new List<Vector2>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var da = Vector2.Dot(a - center, dir) - threshold;
                var db = Vector2.Dot(b - center, dir) - threshold;
                if (da <= 0f)
                {
                    result.Add(a);
                }

                if ((da <= 0f) != (db <= 0f))
                {
                    result.Add(Vector2.Lerp(a, b, da / (da - db)));
                }
            }

            return result;
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            FillPolygon(painter, new List<Vector2> { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) }, color);
        }

        private static void FillPolygon(Painter2D painter, List<Vector2> points, Color color)
        {
            if (points.Count < 3)
            {
                return;
            }

            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var i = 1; i < points.Count; i++)
            {
                painter.LineTo(points[i]);
            }

            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
