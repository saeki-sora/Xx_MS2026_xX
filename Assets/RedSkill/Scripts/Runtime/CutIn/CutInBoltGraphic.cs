using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// 稲妻の線（カットインの窓のふち・消えるときの横一本の線）。折れ線を「まわりの光（太い・キャラの色）」と「芯（細い・白）」の2層で描く。
    /// 線の横方向はやわらかく消える（コードで作る小さなグラデーションの絵を使う。絵のファイルは要らない）。
    /// 座標はこの RectTransform の中の位置（ピボットを左下にして使う）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class CutInBoltGraphic : MaskableGraphic
    {
        private static readonly System.Random Jitter = new System.Random();
        private static Texture2D _softTexture;

        private readonly List<Vector2> _points = new List<Vector2>();
        private float _coreWidth = 4f;
        private float _glowWidth = 24f;
        private Color _glowColor = Color.white;

        public override Texture mainTexture => SoftTexture;

        private static Texture2D SoftTexture
        {
            get
            {
                if (_softTexture != null)
                {
                    return _softTexture;
                }

                // 横方向の明るさ: 真ん中が一番明るく、両端で 0 になる（芯のまわりがぼんやり光る形）。
                const int size = 64;
                _softTexture = new Texture2D(1, size, TextureFormat.RGBA32, false)
                {
                    name = "CutInBoltSoft",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                for (var y = 0; y < size; y++)
                {
                    var d = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    var a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                    _softTexture.SetPixel(0, y, new Color(1f, 1f, 1f, a));
                }

                _softTexture.Apply(false, true);
                return _softTexture;
            }
        }

        /// <summary>太さと光の色を決める（芯は白）。</summary>
        public void SetStyle(float coreWidth, float glowWidth, Color glowColor)
        {
            _coreWidth = coreWidth;
            _glowWidth = glowWidth;
            _glowColor = glowColor;
            SetVerticesDirty();
        }

        /// <summary>a から b へのギザギザの線を作り直す（端は動かさず、真ん中ほど大きく揺れる）。</summary>
        public void Rebuild(Vector2 a, Vector2 b, int segments, float jag)
        {
            SkillCutInMath.BoltPoints(a, b, segments, jag, Jitter, _points);
            SetVerticesDirty();
        }

        /// <summary>端の位置だけ変わったときに、今のギザギザの形を保ったまま a → b へ伸ばし直す。</summary>
        public void Refit(Vector2 a, Vector2 b)
        {
            SkillCutInMath.RefitPoints(_points, a, b);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2)
            {
                return;
            }

            var alpha = color.a;
            var glow = _glowColor;
            glow.a *= alpha;
            AddStrip(vh, _glowWidth, glow);
            AddStrip(vh, _coreWidth * 2.2f, new Color(1f, 1f, 1f, alpha));
        }

        // 1区間ごとに長方形を置く（つなぎ目は重なるが、光なので目立たない）。
        private void AddStrip(VertexHelper vh, float width, Color colorValue)
        {
            if (width <= 0f || colorValue.a <= 0f)
            {
                return;
            }

            var c = (Color32)colorValue;
            var half = width * 0.5f;
            for (var i = 0; i < _points.Count - 1; i++)
            {
                var p0 = _points[i];
                var p1 = _points[i + 1];
                var dir = p1 - p0;
                var len = dir.magnitude;
                if (len < 1e-4f)
                {
                    continue;
                }

                dir /= len;
                var n = new Vector2(-dir.y, dir.x) * half;
                // つなぎ目のすき間が出ないよう、少しだけ前後に伸ばす。
                var ext = dir * Mathf.Min(half * 0.5f, len * 0.5f);
                var a = p0 - ext;
                var b = p1 + ext;
                var start = vh.currentVertCount;
                vh.AddVert(a - n, c, new Vector2(0.5f, 0f));
                vh.AddVert(a + n, c, new Vector2(0.5f, 1f));
                vh.AddVert(b + n, c, new Vector2(0.5f, 1f));
                vh.AddVert(b - n, c, new Vector2(0.5f, 0f));
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }
    }
}
