using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress.Billboards
{
    /// <summary>
    /// 対象のSpriteRendererのマテリアルをビルボード用に差し替え、元のマテリアルを覚えておいて、いつでも元に戻す。
    /// 足元の位置を求めるために、絵の外形(中心と半径)を各SpriteRendererへ渡す(絵が切り替わったときだけ更新)。
    /// 他の仕組み(ShaderFXのエフェクト等)が後からマテリアルを付け替えた絵は、そちらを優先して手を引く(取り合いにしない)。
    /// </summary>
    public sealed class BillboardMaterialSwapper
    {
        private static readonly int BoundsId = Shader.PropertyToID("_BillboardBounds");

        private sealed class Entry
        {
            public SpriteRenderer Renderer;
            public Material Original;
            public Sprite LastSprite;
            public SpriteDrawMode LastDrawMode;
            public Vector2 LastSize;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly HashSet<SpriteRenderer> _wanted = new HashSet<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> _yielded = new HashSet<SpriteRenderer>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        public int Count => _entries.Count;

        /// <summary>対象の一覧に合わせて、新しく加わった物は差し替え、外れた物(破棄された物を含む)は元に戻す。</summary>
        public void Sync(List<SpriteRenderer> wanted, Material billboardMaterial)
        {
            _wanted.Clear();
            foreach (var renderer in wanted)
            {
                if (renderer != null && !_yielded.Contains(renderer))
                {
                    _wanted.Add(renderer);
                }
            }

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (entry.Renderer == null || !_wanted.Remove(entry.Renderer))
                {
                    Restore(entry);
                    _entries.RemoveAt(i);
                }
            }

            // ここで_wantedに残っているのは、まだ差し替えていない物だけ。
            foreach (var renderer in _wanted)
            {
                _entries.Add(new Entry { Renderer = renderer, Original = renderer.sharedMaterial });
                renderer.sharedMaterial = billboardMaterial;
            }
        }

        /// <summary>
        /// 毎フレーム呼ぶ。絵が変わっていたら外形を渡し直す。他の処理がマテリアルを付け替えていたら、その絵からは手を引く。
        /// </summary>
        public void Refresh(Material billboardMaterial)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                var renderer = entry.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.sharedMaterial != billboardMaterial)
                {
                    // 付け替えた側の見た目を尊重し、元に戻さず管理から外す(次の集め直しでも対象にしない)。
                    _yielded.Add(renderer);
                    _entries.RemoveAt(i);
                    continue;
                }

                if (renderer.sprite != entry.LastSprite || renderer.drawMode != entry.LastDrawMode || renderer.size != entry.LastSize)
                {
                    UploadBounds(renderer);
                    entry.LastSprite = renderer.sprite;
                    entry.LastDrawMode = renderer.drawMode;
                    entry.LastSize = renderer.size;
                }
            }
        }

        public void RestoreAll()
        {
            foreach (var entry in _entries)
            {
                Restore(entry);
            }

            _entries.Clear();
            _yielded.Clear();
        }

        private static void Restore(Entry entry)
        {
            if (entry.Renderer != null)
            {
                entry.Renderer.sharedMaterial = entry.Original;
            }
        }

        private void UploadBounds(SpriteRenderer renderer)
        {
            if (!TryGetLocalBounds(renderer, out var center, out var extents))
            {
                return;
            }

            renderer.GetPropertyBlock(_block);
            _block.SetVector(BoundsId, new Vector4(center.x, center.y, extents.x, extents.y));
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>反転前の、ローカル座標での絵の外形。スライス/タイル表示のときは表示サイズから求める。</summary>
        private static bool TryGetLocalBounds(SpriteRenderer renderer, out Vector2 center, out Vector2 extents)
        {
            var sprite = renderer.sprite;
            if (sprite == null)
            {
                center = extents = Vector2.zero;
                return false;
            }

            if (renderer.drawMode == SpriteDrawMode.Simple)
            {
                var bounds = sprite.bounds;
                center = bounds.center;
                extents = bounds.extents;
                return true;
            }

            var size = renderer.size;
            var pivot01 = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            center = Vector2.Scale(new Vector2(0.5f, 0.5f) - pivot01, size);
            extents = size * 0.5f;
            return true;
        }
    }
}
