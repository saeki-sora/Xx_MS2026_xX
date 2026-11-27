using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.GripInputBridge.EditorTools
{
    /// <summary>
    /// 「なめらかさ」タブのグラフ。直近の数秒間の 生の値・比べる2つのフィルター の線を重ねて描く。
    /// 値は 0〜1（キャリブレーション前の値）。新しい値ほど右に来る。
    /// </summary>
    internal sealed class GripFilterGraph : VisualElement
    {
        public const int LineCount = 3;

        private const int Capacity = 2048;
        private const float WindowSeconds = 4f;

        private readonly double[] _times = new double[Capacity];
        private readonly float[,] _values = new float[LineCount, Capacity];
        private readonly Color[] _colors;
        private int _head;
        private int _count;

        public GripFilterGraph(Color[] lineColors)
        {
            _colors = lineColors;
            style.height = 170;
            style.marginTop = 4;
            style.marginBottom = 4;
            style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            generateVisualContent += Draw;
        }

        public void ClearHistory()
        {
            _head = 0;
            _count = 0;
            MarkDirtyRepaint();
        }

        public void Add(double time, float raw, float lineB, float lineC)
        {
            _times[_head] = time;
            _values[0, _head] = raw;
            _values[1, _head] = lineB;
            _values[2, _head] = lineC;
            _head = (_head + 1) % Capacity;
            _count = Mathf.Min(_count + 1, Capacity);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            var painter = context.painter2D;

            // 目盛り(0% / 50% / 100%)
            painter.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            painter.lineWidth = 1f;
            for (var i = 0; i <= 2; i++)
            {
                var y = rect.height * (1f - i * 0.5f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(rect.width, y));
                painter.Stroke();
            }

            if (_count < 2)
            {
                return;
            }

            var newest = _times[(_head - 1 + Capacity) % Capacity];
            for (var line = 0; line < LineCount; line++)
            {
                painter.strokeColor = _colors[line];
                painter.lineWidth = line == 0 ? 1f : 2f;
                painter.BeginPath();
                var started = false;
                for (var k = _count - 1; k >= 0; k--)
                {
                    var index = (_head - 1 - k + Capacity * 2) % Capacity;
                    var age = (float)(newest - _times[index]);
                    if (age > WindowSeconds)
                    {
                        continue;
                    }

                    var x = rect.width * (1f - age / WindowSeconds);
                    var y = rect.height * (1f - Mathf.Clamp01(_values[line, index]));
                    var point = new Vector2(x, y);
                    if (!started)
                    {
                        painter.MoveTo(point);
                        started = true;
                    }
                    else
                    {
                        painter.LineTo(point);
                    }
                }

                painter.Stroke();
            }
        }
    }
}
