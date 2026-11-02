using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// 4つの角を自由に動かせる四角形（カットインの斜めの窓）。中に絵を「切り抜いて」映す。
    /// 絵の置き場所（<see cref="PictureRect"/>）は窓とは別に決め、窓の各角の位置から絵の UV を計算するので、
    /// 窓の形をどう変えても絵は動かず、窓の形に切り抜かれて見える（マスク不要）。
    /// 絵が無いときは白一色（光る演出に使う）。座標はこの RectTransform の中の位置（ピボットを左下にして使う）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class CutInQuadGraphic : MaskableGraphic
    {
        private readonly Vector2[] _corners = new Vector2[4];
        private Sprite _sprite;
        private Rect _pictureRect = new Rect(0f, 0f, 1f, 1f);

        public override Texture mainTexture => _sprite != null ? _sprite.texture : s_WhiteTexture;

        /// <summary>絵（null で白一色）。</summary>
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

        /// <summary>窓の4つの角（左下・左上・右上・右下の順）。</summary>
        public void SetCorners(Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight)
        {
            _corners[0] = bottomLeft;
            _corners[1] = topLeft;
            _corners[2] = topRight;
            _corners[3] = bottomRight;
            SetVerticesDirty();
        }

        /// <summary>窓の4つの角（左下・左上・右上・右下の順）。</summary>
        public void SetCorners(Vector2[] corners)
        {
            SetCorners(corners[0], corners[1], corners[2], corners[3]);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var uvRect = SpriteUvRect(_sprite);
            var size = new Vector2(Mathf.Max(1e-4f, _pictureRect.width), Mathf.Max(1e-4f, _pictureRect.height));
            var c = (Color32)color;
            for (var i = 0; i < 4; i++)
            {
                var p = _corners[i];
                var n = new Vector2((p.x - _pictureRect.x) / size.x, (p.y - _pictureRect.y) / size.y);
                var uv = new Vector2(Mathf.Lerp(uvRect.xMin, uvRect.xMax, n.x), Mathf.Lerp(uvRect.yMin, uvRect.yMax, n.y));
                vh.AddVert(p, c, uv);
            }

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
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
