using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 守護霊が着地する「ドン」と、おしまいの「きらっ」の演出（全員の画面に出る。通信はせず各PCが自分で出す）。
    /// ドン: 衝撃波が2重に広がり（＋小さな輪）、コアが光り、光の粒・葉・回復の十字・煙が飛び散り、画面のふちが緑に、画面全体が一瞬白く光り、カメラが揺れる。
    /// きらっ: コアに光・輪・十字・光の粒。
    /// </summary>
    internal sealed class GreenMintBurst
    {
        private readonly GreenMintFx _fx;
        private readonly Canvas _canvas;
        private readonly RawImage _edge;
        private readonly RawImage _white;
        private float _edgeTime = -1f;

        public GreenMintBurst(GreenMintFx fx)
        {
            _fx = fx;

            // 画面のふち・白い光: 画面に重ねる UI（HUD より奥に描く）。
            var canvasObject = new GameObject("ScreenEdge") { hideFlags = HideFlags.DontSave };
            canvasObject.transform.SetParent(fx.Root, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -100;
            _edge = CreateFullScreen(canvasObject.transform, "Edge", fx.Art.Vignette);
            _white = CreateFullScreen(canvasObject.transform, "White", Texture2D.whiteTexture);
            _canvas.enabled = false;
        }

        /// <summary>ドン（守護霊の着地）。</summary>
        public void PlayLand(Vector2 core, int ownerIndex)
        {
            var definition = _fx.Definition;
            var burst = definition.burst;
            var palette = definition.palette;
            var up = GreenMintUtil.ScreenUp;

            var wave = palette.ring;
            wave.a = 0.95f;
            _fx.WaveAt(core, 1.5f, burst.waveSize, burst.waveSeconds, wave);
            var second = palette.ring;
            second.a = 0.75f;
            _fx.WaveAt(core, 1f, burst.waveSize * 0.7f, burst.waveSeconds * 0.9f, second, 0.14f);
            _fx.WaveAt(core, 1f, burst.waveSize * 0.45f, 0.7f, second);

            _fx.FlashAt(core, burst.flashSize, 0.6f, palette.glow * 1.3f, 14, 0.9f);
            _fx.FlashAt(core, burst.flashSize * 0.45f, 0.4f, new Color(1.6f, 2f, 1.6f), 0, 0f);
            _fx.Sparkles(core, burst.sparkleCount, 14f, 0.4f, palette.glow, default, 180f, 1f);
            _fx.Leaves(core, burst.leafCount, 0.9f, 10f);
            _fx.Crosses(core, burst.crossCount, 0.85f, 1f, up, 2f);
            _fx.Smoke(core - up * 1.5f, burst.smokeCount, 1.2f, 10f, Vector2.zero, 1.1f);

            if (burst.screenEdgeStrength > 0f || burst.screenFlash > 0f)
            {
                _edgeTime = 0f;
            }

            var shake = definition.cameraShake;
            if (shake.land != null && shake.landStrength > 0f)
            {
                var rig = FortressCameraRig.Active;
                if (rig != null && (!shake.ownerViewOnly || rig.CurrentViewer == ownerIndex))
                {
                    rig.AddModifier(new SimpleShakeModifier(shake.land, shake.landStrength));
                }
            }
        }

        /// <summary>きらっ（葉と守護霊がコアに吸い込まれ終わった瞬間）。</summary>
        public void PlayEnd(Vector2 core)
        {
            var definition = _fx.Definition;
            var palette = definition.palette;
            _fx.FlashAt(core, definition.burst.endFlashSize, 0.45f, palette.glow * 1.2f, 10, 0.6f);
            var ring = palette.ring;
            ring.a = 0.85f;
            _fx.WaveAt(core, 1f, 12f, 0.7f, ring);
            _fx.Crosses(core, 8, 0.7f, 0.6f, GreenMintUtil.ScreenUp, 2f);
            _fx.Sparkles(core, 30, 8f, 0.4f, palette.glow, default, 180f, 0.7f);
        }

        public void Tick(float dt)
        {
            if (_edgeTime < 0f)
            {
                _canvas.enabled = false;
                return;
            }

            var burst = _fx.Definition.burst;
            _edgeTime += dt;
            var t = _edgeTime / Mathf.Max(0.05f, burst.screenEdgeSeconds);
            if (t >= 1f)
            {
                _edgeTime = -1f;
                _canvas.enabled = false;
                return;
            }

            // すぐ明るくなって、ゆっくり引く。白い光は 0.4 秒で消える。
            var rise = Mathf.Clamp01(t / 0.08f);
            var strength = burst.screenEdgeStrength * rise * (1f - Mathf.SmoothStep(0.08f, 1f, t));
            var color = Color.Lerp(_fx.Definition.palette.ring, _fx.Definition.palette.leafLight, 0.3f);
            color.a = strength;
            _edge.color = color;
            var white = Mathf.Max(0f, 1f - _edgeTime * 2.5f) * burst.screenFlash;
            _white.color = new Color(0.9f, 1f, 0.9f, white);
            _white.enabled = white > 0.001f;
            _canvas.enabled = true;
        }

        public void Hide()
        {
            _edgeTime = -1f;
            _canvas.enabled = false;
        }

        private static RawImage CreateFullScreen(Transform parent, string objectName, Texture texture)
        {
            var imageObject = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }
    }
}
