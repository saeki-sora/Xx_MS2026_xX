using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// 丸（中を絵で埋める）か輪（内側から外側へ色が変わる）を描く。欄の丸いアイコンの上に出す顔・色の輪・光に使う。
    /// 丸のときは <see cref="CutInQuadGraphic"/> と同じく、絵の置き場所（<see cref="PictureRect"/>）から UV を計算して丸く切り抜く。
    /// 座標はこの RectTransform の中の位置（ピボットを左下にして使う）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class CutInDiscGraphic : MaskableGraphic
    {
        private const int Segments = 48;

        private Sprite _sprite;
        private Rect _pictureRect = new Rect(0f, 0f, 1f, 1f);
        private Vector2 _center;
        private float _inner;
        private float _outer;
        private Color _innerColor = Color.white;
        private Color _outerColor = Color.white;

        public override Texture mainTexture => _sprite != null ? _sprite.texture : s_WhiteTexture;

        /// <summary>中を埋める絵（null で白一色。輪のときは使わない）。</summary>
        public Sprite Sprite
        {
            get => _sprite;
            set
            {
                if (_sprite == value)
                {
                    return;
                }

                _sprite = value;
                SetAllDirty();
            }
        }

        /// <summary>絵全体を置く範囲。</summary>
        public Rect PictureRect
        {
            get => _pictureRect;
            set
            {
                _pictureRect = value;
                SetVerticesDirty();
            }
        }

        /// <summary>中を埋めた丸にする（半径 <paramref name="radius"/>）。</summary>
        public void SetDisc(Vector2 center, float radius)
        {
            _center = center;
            _inner = 0f;
            _outer = radius;
            _innerColor = _outerColor = Color.white;
            SetVerticesDirty();
        }

        /// <summary>輪にする（内側の半径 <paramref name="inner"/> で <paramref name="innerColor"/>、外側の半径 <paramref name="outer"/> で <paramref name="outerColor"/>）。</summary>
        public void SetRing(Vector2 center, float inner, float outer, Color innerColor, Color outerColor)
        {
            _center = center;
            _inner = inner;
            _outer = outer;
            _innerColor = innerColor;
            _outerColor = outerColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_outer <= 0f)
            {
                return;
            }

            if (_inner <= 0f)
            {
                PopulateDisc(vh);
            }
            else
            {
                PopulateRing(vh);
            }
        }

        private void PopulateDisc(VertexHelper vh)
        {
            var uvRect = SpriteUvRect(_sprite);
            var size = new Vector2(Mathf.Max(1e-4f, _pictureRect.width), Mathf.Max(1e-4f, _pictureRect.height));
            var c = (Color32)color;
            vh.AddVert(_center, c, Uv(_center, uvRect, size));
            for (var i = 0; i <= Segments; i++)
            {
                var p = _center + Direction(i) * _outer;
                vh.AddVert(p, c, Uv(p, uvRect, size));
            }

            for (var i = 1; i <= Segments; i++)
            {
                vh.AddTriangle(0, i, i + 1);
            }
        }

        private void PopulateRing(VertexHelper vh)
        {
            var inner = (Color32)(_innerColor * color);
            var outer = (Color32)(_outerColor * color);
            for (var i = 0; i <= Segments; i++)
            {
                var d = Direction(i);
                vh.AddVert(_center + d * _inner, inner, new Vector2(0.5f, 0.5f));
                vh.AddVert(_center + d * _outer, outer, new Vector2(0.5f, 0.5f));
            }

            for (var i = 0; i < Segments; i++)
            {
                var a = i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a + 3, a + 2, a);
            }
        }

        private Vector2 Uv(Vector2 p, Rect uvRect, Vector2 size)
        {
            var n = new Vector2((p.x - _pictureRect.x) / size.x, (p.y - _pictureRect.y) / size.y);
            return new Vector2(Mathf.LerpUnclamped(uvRect.xMin, uvRect.xMax, n.x), Mathf.LerpUnclamped(uvRect.yMin, uvRect.yMax, n.y));
        }

        private static Vector2 Direction(int i)
        {
            var a = (float)i / Segments * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        private static Rect SpriteUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var tex = sprite.texture;
            var r = sprite.textureRect;
            return new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        }
    }
}
