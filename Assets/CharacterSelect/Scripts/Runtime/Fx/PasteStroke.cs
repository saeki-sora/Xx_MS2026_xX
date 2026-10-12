using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 歯磨き粉を絞り出して描いた太い線（UI）。通り道（点の並び）をなめらかにつなぎ、その一部（<see cref="SetRange"/>）だけを描く。
    /// 絵は線の向きに沿って貼る（絵の縦じまが、線に沿ったしまになる）。両端は丸い。
    /// 親に RectMask2D があれば、その中だけに見える。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("キャラ選択/演出/歯磨き粉の線 (PasteStroke)")]
    public sealed class PasteStroke : MaskableGraphic
    {
        private const int CapSegments = 8;

        [Tooltip("線に貼る絵（縦じまの絵だと、線に沿ったしまになる）。空なら色だけ。")]
        public Texture texture;

        [Tooltip("線の太さ（ピクセル）。")]
        [Min(1f)]
        public float width = 85f;

        [Tooltip("線の幅に使う、絵の横の範囲（0〜1）。狭いほどしまが太い。")]
        public Vector2 textureU = new Vector2(0.1f, 0.36f);

        [Tooltip("線の長さに使う、絵の縦の範囲（0〜1）。")]
        public Vector2 textureV = new Vector2(0.25f, 0.7f);

        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<float> _lengths = new List<float>();
        private readonly List<Vector2> _drawn = new List<Vector2>();
        private readonly List<float> _drawnLengths = new List<float>();
        private float _total;
        private float _from;
        private float _to;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        /// <summary>線の長さ全体（ピクセル）。</summary>
        public float TotalLength => _total;

        /// <summary>通り道を決める（点をなめらかにつなぐ。点の座標はこの物の中の位置）。描く範囲は 0 に戻る。</summary>
        public void SetPath(IList<Vector2> controls, int samplesPerSegment = 12)
        {
            _points.Clear();
            _lengths.Clear();
            _total = 0f;
            if (controls == null || controls.Count < 2)
            {
                SetRange(0f, 0f);
                return;
            }

            for (var i = 0; i < controls.Count - 1; i++)
            {
                var p0 = controls[Mathf.Max(0, i - 1)];
                var p1 = controls[i];
                var p2 = controls[i + 1];
                var p3 = controls[Mathf.Min(controls.Count - 1, i + 2)];
                for (var s = 0; s < samplesPerSegment; s++)
                {
                    AddPoint(CatmullRom(p0, p1, p2, p3, s / (float)samplesPerSegment));
                }
            }

            AddPoint(controls[controls.Count - 1]);
            _from = 0f;
            _to = 0f;
            SetVerticesDirty();
        }

        /// <summary>描く範囲（線の長さ全体を 0〜1 として、from から to まで）。</summary>
        public void SetRange(float from, float to)
        {
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);
            if (Mathf.Approximately(from, _from) && Mathf.Approximately(to, _to))
            {
                return;
            }

            _from = from;
            _to = to;
            SetVerticesDirty();
        }

        /// <summary>線の上の点（0〜1）。</summary>
        public Vector2 PointAt(float t)
        {
            if (_points.Count == 0)
            {
                return Vector2.zero;
            }

            var target = Mathf.Clamp01(t) * _total;
            for (var i = 1; i < _points.Count; i++)
            {
                if (_lengths[i] >= target)
                {
                    var segment = _lengths[i] - _lengths[i - 1];
                    var k = segment > 0f ? (target - _lengths[i - 1]) / segment : 0f;
                    return Vector2.Lerp(_points[i - 1], _points[i], k);
                }
            }

            return _points[_points.Count - 1];
        }

        private void AddPoint(Vector2 point)
        {
            if (_points.Count > 0)
            {
                var step = Vector2.Distance(_points[_points.Count - 1], point);
                if (step < 0.01f)
                {
                    return;
                }

                _total += step;
            }

            _points.Add(point);
            _lengths.Add(_total);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2 || _to - _from <= 0f || _total <= 0f)
            {
                return;
            }

            // 描く範囲の点を集める（両端は線の上を補間した点）。
            var start = _from * _total;
            var end = _to * _total;
            _drawn.Clear();
            _drawnLengths.Clear();
            _drawn.Add(PointAt(_from));
            _drawnLengths.Add(start);
            for (var i = 0; i < _points.Count; i++)
            {
                if (_lengths[i] > start && _lengths[i] < end)
                {
                    _drawn.Add(_points[i]);
                    _drawnLengths.Add(_lengths[i]);
                }
            }

            _drawn.Add(PointAt(_to));
            _drawnLengths.Add(end);
            if (_drawn.Count < 2)
            {
                return;
            }

            var half = width * 0.5f;
            var c = (Color32)color;
            for (var i = 0; i < _drawn.Count; i++)
            {
                var tangent = Tangent(i);
                var normal = new Vector2(-tangent.y, tangent.x);
                var v = Mathf.Lerp(textureV.x, textureV.y, _drawnLengths[i] / _total);
                vh.AddVert(_drawn[i] + normal * half, c, new Vector2(textureU.x, v));
                vh.AddVert(_drawn[i] - normal * half, c, new Vector2(textureU.y, v));
                if (i > 0)
                {
                    var a = (i - 1) * 2;
                    vh.AddTriangle(a, a + 1, a + 3);
                    vh.AddTriangle(a, a + 3, a + 2);
                }
            }

            // 両端を丸くする（半円）。
            AddCap(vh, 0, -1f, half, c);
            AddCap(vh, _drawn.Count - 1, 1f, half, c);
        }

        private Vector2 Tangent(int i)
        {
            var a = _drawn[Mathf.Max(0, i - 1)];
            var b = _drawn[Mathf.Min(_drawn.Count - 1, i + 1)];
            var t = b - a;
            return t.sqrMagnitude > 0.0001f ? t.normalized : Vector2.up;
        }

        private void AddCap(VertexHelper vh, int index, float direction, float half, Color32 c)
        {
            var center = _drawn[index];
            var tangent = Tangent(index) * direction;
            var normal = new Vector2(-tangent.y, tangent.x);
            var v = Mathf.Lerp(textureV.x, textureV.y, _drawnLengths[index] / _total);
            var uMid = (textureU.x + textureU.y) * 0.5f;
            var uHalf = (textureU.y - textureU.x) * 0.5f;
            var centerIndex = vh.currentVertCount;
            vh.AddVert(center, c, new Vector2(uMid, v));
            for (var s = 0; s <= CapSegments; s++)
            {
                // 片側の縁 → 先端 → 反対側の縁 と半円を回る。
                var angle = Mathf.PI * s / CapSegments;
                var dir = normal * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
                var side = Vector2.Dot(dir, normal) * direction;
                vh.AddVert(center + dir * half, c, new Vector2(uMid - side * uHalf, v));
                if (s > 0)
                {
                    vh.AddTriangle(centerIndex, centerIndex + s, centerIndex + s + 1);
                }
            }
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
