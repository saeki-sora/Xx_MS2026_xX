using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】砲台の絵（子も含む SpriteRenderer）を色あせて見せる・元に戻す。
    /// ほかの係が毎フレーム色を入れ直す絵（キャラの絵など）もあるので、毎フレーム「今の色」を元の色として覚え直してから重ねる
    /// （自分が前のフレームに書いた色のままなら、覚えている元の色を使う）。戻すときは、自分が書いた色のままの物だけ元に戻す。
    /// </summary>
    internal sealed class TurretFade
    {
        private struct Entry
        {
            public Color Original;
            public Color Written;
        }

        private readonly Dictionary<SpriteRenderer, Entry> _entries = new Dictionary<SpriteRenderer, Entry>();
        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _stale = new List<SpriteRenderer>();

        public void Apply(LaserTurret turret, bool faded, float desaturate, float darken, float alpha)
        {
            if (turret == null || !faded)
            {
                Restore();
                return;
            }

            turret.GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var current = renderer.color;
                var original = _entries.TryGetValue(renderer, out var entry) && current == entry.Written ? entry.Original : current;
                var written = Fade(original, desaturate, darken, alpha);
                renderer.color = written;
                _entries[renderer] = new Entry { Original = original, Written = written };
            }

            // 消えた・外れた絵は忘れる。
            _stale.Clear();
            foreach (var renderer in _entries.Keys)
            {
                if (renderer == null || !_renderers.Contains(renderer))
                {
                    _stale.Add(renderer);
                }
            }

            foreach (var renderer in _stale)
            {
                _entries.Remove(renderer);
            }
        }

        private void Restore()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            foreach (var pair in _entries)
            {
                if (pair.Key != null && pair.Key.color == pair.Value.Written)
                {
                    pair.Key.color = pair.Value.Original;
                }
            }

            _entries.Clear();
        }

        // 灰色へ寄せて暗く・薄くする（「色あせた」感じ）。darken = 明るさの倍率（小さいほど暗い）。
        private static Color Fade(Color color, float desaturate, float darken, float alpha)
        {
            var gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            var result = Color.Lerp(color, new Color(gray, gray, gray), Mathf.Clamp01(desaturate));
            var brightness = Mathf.Clamp01(darken);
            result.r *= brightness;
            result.g *= brightness;
            result.b *= brightness;
            result.a = color.a * Mathf.Clamp01(alpha);
            return result;
        }
    }
}
