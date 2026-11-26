using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// とどめの合体ビームの見た目（見本 E。<see cref="BossFinaleController"/> が作る。各PCで出す）。
    /// 0〜0.55 秒: 4人のレーザーが前の1点へ伸びる → 0.45 秒〜: 光の玉がふくらむ（4色の光が回る）→ 0.8 秒〜: 4色を束ねた太い1本がボスへ伸びる。
    /// 当たってから 0.5 秒で細くなって消える。仮の絵（コードで作った帯と丸）。実時間で動く（止めの間もビームは見せたまま）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class BossFinaleBeams : MonoBehaviour
    {
        private const float ConvergeSeconds = 0.55f;
        private const float OrbStart = 0.45f;
        private const float BigBeamStart = 0.8f;

        private readonly SpriteRenderer[] _thinGlow = new SpriteRenderer[4];
        private readonly SpriteRenderer[] _thin = new SpriteRenderer[4];
        private readonly SpriteRenderer[] _thinCore = new SpriteRenderer[4];
        private readonly SpriteRenderer[] _bigStripes = new SpriteRenderer[4];
        private readonly SpriteRenderer[] _orbLights = new SpriteRenderer[4];
        private SpriteRenderer _bigGlow;
        private SpriteRenderer _bigCore;
        private SpriteRenderer _orb;
        private float _z;
        private float _bossRadius;

        public static BossFinaleBeams Create(float z, float bossRadius)
        {
            var go = new GameObject("[Boss] Finale Beams");
            var beams = go.AddComponent<BossFinaleBeams>();
            beams._z = z;
            beams._bossRadius = Mathf.Max(0.5f, bossRadius);
            beams.Build();
            return beams;
        }

        private void Build()
        {
            for (var i = 0; i < 4; i++)
            {
                var color = FortressColors.PlayerColor(i);
                _thinGlow[i] = Part($"Thin{i}Glow", BossArt.BeamSoft, new Color(color.r, color.g, color.b, 0.35f), 30);
                _thin[i] = Part($"Thin{i}", BossArt.BeamSoft, color, 31);
                _thinCore[i] = Part($"Thin{i}Core", BossArt.BeamSoft, Color.white, 32);
                _bigStripes[i] = Part($"Big{i}", BossArt.BeamSoft, color, 35);
                _orbLights[i] = Part($"Orb{i}", BossArt.SoftDisc, color, 38);
            }

            _bigGlow = Part("BigGlow", BossArt.BeamSoft, new Color(1f, 1f, 1f, 0.3f), 34);
            _bigCore = Part("BigCore", BossArt.BeamSoft, Color.white, 36);
            _orb = Part("Orb", BossArt.SoftDisc, Color.white, 37);
        }

        private SpriteRenderer Part(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>集まり始めてから t 秒の見た目にする。</summary>
        public void Show(float t, Vector2[] muzzles, Vector2 merge, Vector2 target)
        {
            var hit = BossDefinition.MergeToHitSeconds;
            var fade = t < hit ? 1f : Mathf.Clamp01(1f - (t - hit - 0.1f) / 0.4f);
            var thinWidth = _bossRadius * 0.12f * fade;
            var grow = EaseOut(t / ConvergeSeconds);
            for (var i = 0; i < 4; i++)
            {
                var from = muzzles != null && i < muzzles.Length ? muzzles[i] : merge;
                var to = Vector2.Lerp(from, merge, grow);
                Segment(_thinGlow[i], from, to, thinWidth * 2.2f, fade > 0f);
                Segment(_thin[i], from, to, thinWidth, fade > 0f);
                Segment(_thinCore[i], from, to, thinWidth * 0.35f, fade > 0f);
            }

            // 光の玉（4色の光が回る）
            var orbK = Mathf.Clamp01((t - OrbStart) / 0.3f) * fade;
            var orbRadius = _bossRadius * (0.12f + 0.32f * orbK);
            _orb.enabled = orbK > 0.01f;
            _orb.transform.position = new Vector3(merge.x, merge.y, _z - 0.01f);
            _orb.transform.localScale = Vector3.one * orbRadius * 1.6f;
            for (var i = 0; i < 4; i++)
            {
                var angle = Time.unscaledTime * 6f + i * Mathf.PI * 0.5f;
                var p = merge + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbRadius;
                _orbLights[i].enabled = _orb.enabled;
                _orbLights[i].transform.position = new Vector3(p.x, p.y, _z - 0.02f);
                _orbLights[i].transform.localScale = Vector3.one * orbRadius * 0.45f;
            }

            // 太い1本（4色の帯を束ねて、真ん中は白）
            var bigK = EaseOut((t - BigBeamStart) / Mathf.Max(0.05f, hit - BigBeamStart));
            var show = t >= BigBeamStart && fade > 0f;
            var end = Vector2.Lerp(merge, target, bigK);
            var width = _bossRadius * 0.55f * fade;
            Segment(_bigGlow, merge, end, width * 1.8f, show);
            var dir = end - merge;
            var normal = dir.sqrMagnitude > 1e-6f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
            for (var i = 0; i < 4; i++)
            {
                var offset = normal * ((i - 1.5f) * width * 0.2f + Mathf.Sin(Time.unscaledTime * 30f + i) * width * 0.02f);
                Segment(_bigStripes[i], merge + offset, end + offset, width * 0.26f, show);
            }

            Segment(_bigCore, merge, end, width * 0.42f, show);
        }

        private void Segment(SpriteRenderer renderer, Vector2 a, Vector2 b, float width, bool visible)
        {
            var d = b - a;
            var length = d.magnitude;
            renderer.enabled = visible && length > 0.01f && width > 0.001f;
            if (!renderer.enabled)
            {
                return;
            }

            // 帯の絵は 2×2 単位。横を長さ、縦を太さに合わせる。
            var t = renderer.transform;
            t.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, _z);
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length * 0.5f + width * 0.5f, width * 0.5f, 1f);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
    }
}
