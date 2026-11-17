using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 副砲の見た目を描く板。毎フレーム中身を作り直す、色付きの平らな図形の集まり（線・丸・輪・葉の形）。
    /// 本体のレーザーの「下に描く層」（<see cref="Back"/>）と「上に描く層」（<see cref="Front"/>）の2枚。
    /// 図形は地面（XY 平面）に寝かせて描く。素材の画像は使わない（色は頂点の色）。
    /// </summary>
    public sealed class SubGunCanvas : IDisposable
    {
        public SubGunCanvas(string name)
        {
            Back = new SubGunLayer(name + "_Back");
            Front = new SubGunLayer(name + "_Front");
        }

        /// <summary>本体のレーザーの下に描く層。</summary>
        public SubGunLayer Back { get; }

        /// <summary>本体のレーザーの上に描く層。</summary>
        public SubGunLayer Front { get; }

        public void Begin(float z)
        {
            Back.Begin(z);
            Front.Begin(z);
        }

        /// <summary>描き終わり。描く順番は本体のレーザーの線に合わせる（無ければ 0 の前後）。</summary>
        public void End(LineRenderer beamLine)
        {
            var order = beamLine != null ? beamLine.sortingOrder : 0;
            var layer = beamLine != null ? beamLine.sortingLayerID : 0;
            // 本体の光（BeamGlow）が order-1 なので、下の層はさらにその下。
            Back.End(layer, order - 2);
            Front.End(layer, order + 1);
        }

        public void Hide()
        {
            Back.Hide();
            Front.Hide();
        }

        public void Dispose()
        {
            Back.Dispose();
            Front.Dispose();
        }
    }

    /// <summary>副砲の板の1層。</summary>
    public sealed class SubGunLayer : IDisposable
    {
        private static Material _material;

        private readonly GameObject _object;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly List<Vector3> _vertices = new List<Vector3>(512);
        private readonly List<Color> _colors = new List<Color>(512);
        private readonly List<int> _triangles = new List<int>(1024);
        private float _z;

        public SubGunLayer(string name)
        {
            // 砲台は回るので子にはしない（世界の向きのまま描く）。砲台が消えるときに一緒に消す。
            _object = new GameObject(name);
            var filter = _object.AddComponent<MeshFilter>();
            _renderer = _object.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sharedMaterial = Material;
            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;
            _renderer.enabled = false;
        }

        private static Material Material
        {
            get
            {
                if (_material == null)
                {
                    // 頂点の色をそのまま出す素材（黄色のスキルの矢と同じ）。
                    _material = new Material(Shader.Find("Sprites/Default")) { name = "SubGun (generated)", hideFlags = HideFlags.DontSave };
                }

                return _material;
            }
        }

        public void Begin(float z)
        {
            _z = z;
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        public void End(int sortingLayerId, int sortingOrder)
        {
            if (_object == null)
            {
                return;
            }

            _object.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _mesh.Clear();
            if (_vertices.Count == 0)
            {
                _renderer.enabled = false;
                return;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
            }

            if (_object != null)
            {
                UnityEngine.Object.Destroy(_object);
            }
        }

        /// <summary>まっすぐな線（太さ width）。</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-8f || width <= 0f || color.a <= 0f)
            {
                return;
            }

            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            var i = _vertices.Count;
            Add(a - n, color);
            Add(a + n, color);
            Add(b + n, color);
            Add(b - n, color);
            Quad(i, i + 1, i + 2, i + 3);
        }

        /// <summary>折れ線（つなぎ目のすき間が出ないように、点ごとに向きをならす）。</summary>
        public void Polyline(IReadOnlyList<Vector2> points, int start, int count, float width, Color color)
        {
            if (count < 2 || width <= 0f || color.a <= 0f)
            {
                return;
            }

            var half = width * 0.5f;
            var first = _vertices.Count;
            for (var k = 0; k < count; k++)
            {
                var p = points[start + k];
                var prev = k > 0 ? points[start + k - 1] : p;
                var next = k < count - 1 ? points[start + k + 1] : p;
                var t = next - prev;
                if (t.sqrMagnitude < 1e-10f)
                {
                    t = Vector2.right;
                }

                var n = new Vector2(-t.y, t.x).normalized * half;
                Add(p - n, color);
                Add(p + n, color);
            }

            for (var k = 0; k < count - 1; k++)
            {
                var i = first + k * 2;
                Quad(i, i + 1, i + 3, i + 2);
            }
        }

        /// <summary>塗りつぶした丸。</summary>
        public void Disc(Vector2 center, float radius, Color color, int segments = 14)
        {
            if (radius <= 0f || color.a <= 0f)
            {
                return;
            }

            var c = _vertices.Count;
            Add(center, color);
            for (var s = 0; s <= segments; s++)
            {
                var a = s * Mathf.PI * 2f / segments;
                Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color);
            }

            for (var s = 0; s < segments; s++)
            {
                _triangles.Add(c);
                _triangles.Add(c + 1 + s);
                _triangles.Add(c + 2 + s);
            }
        }

        /// <summary>輪（太さ width）。</summary>
        public void Ring(Vector2 center, float radius, float width, Color color, int segments = 20)
        {
            if (radius <= 0f || width <= 0f || color.a <= 0f)
            {
                return;
            }

            var inner = Mathf.Max(0f, radius - width * 0.5f);
            var outer = radius + width * 0.5f;
            var first = _vertices.Count;
            for (var s = 0; s <= segments; s++)
            {
                var a = s * Mathf.PI * 2f / segments;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Add(center + dir * inner, color);
                Add(center + dir * outer, color);
            }

            for (var s = 0; s < segments; s++)
            {
                var i = first + s * 2;
                Quad(i, i + 1, i + 3, i + 2);
            }
        }

        /// <summary>葉の形（両端がとがった菱形）。dir の向きに長さ length。</summary>
        public void Leaf(Vector2 center, Vector2 dir, float length, float width, Color color)
        {
            if (length <= 0f || color.a <= 0f)
            {
                return;
            }

            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.up;
            var side = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            var tip = dir * (length * 0.5f);
            var i = _vertices.Count;
            Add(center - tip, color);
            Add(center + side, color);
            Add(center + tip, color);
            Add(center - side, color);
            Quad(i, i + 1, i + 2, i + 3);
        }

        private void Add(Vector2 p, Color color)
        {
            _vertices.Add(new Vector3(p.x, p.y, _z));
            _colors.Add(color);
        }

        private void Quad(int a, int b, int c, int d)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);
            _triangles.Add(a);
            _triangles.Add(c);
            _triangles.Add(d);
        }
    }
}
