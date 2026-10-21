using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// タブレットがコアに着いて「ドン」と回復する瞬間の演出（全員の画面に出る。通信はせず各PCが自分で出す）。
    /// ミントの大きな波が画面いっぱいに広がり（遅れてもう1本）、コアがパッと光り、光の帯がコアの上を斜めに通り抜け（磨かれたように）、
    /// 画面のふちが一瞬ミント色に光り、キラキラ・葉っぱ・回復の十字が舞い、カメラが軽く揺れる。
    /// </summary>
    internal sealed class GreenMintBurst
    {
        private const float ShineSeconds = 0.55f;

        private readonly GreenMintFx _fx;
        private readonly MeshRenderer _shine;
        private readonly Canvas _canvas;
        private readonly RawImage _edge;

        private float _shineTime = -1f;
        private Vector2 _shineCenter;
        private float _edgeTime = -1f;
        private float _secondWaveTimer = -1f;
        private Vector2 _secondWaveCenter;

        public GreenMintBurst(GreenMintFx fx)
        {
            _fx = fx;
            _shine = fx.CreateQuad("CoreShine", fx.RadialMaterial, fx.Definition.satellites.sortingOrder + 1);

            // 画面のふち: 画面に重ねる UI（HUD より奥に描く）。
            var canvasObject = new GameObject("ScreenEdge") { hideFlags = HideFlags.DontSave };
            canvasObject.transform.SetParent(fx.Root, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -100;
            var imageObject = new GameObject("Edge") { hideFlags = HideFlags.DontSave };
            imageObject.transform.SetParent(canvasObject.transform, false);
            _edge = imageObject.AddComponent<RawImage>();
            _edge.texture = fx.Art.Vignette;
            _edge.raycastTarget = false;
            var rect = _edge.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _canvas.enabled = false;
        }

        /// <summary>「ドン」。core = コアの位置、viewerIsOwner = この画面が緑のプレイヤーのものか。</summary>
        public void Play(Vector2 core, int ownerIndex)
        {
            var definition = _fx.Definition;
            var burst = definition.burst;
            var palette = definition.palette;
            var up = GreenMintUtil.ScreenUp;

            var wave = Color.Lerp(palette.mint, Color.white, 0.15f);
            wave.a = 0.95f;
            _fx.WaveAt(core, 1.5f, burst.waveSize, burst.waveSeconds, wave);
            _secondWaveTimer = 0.14f;
            _secondWaveCenter = core;

            _fx.FlashAt(core, burst.flashSize, 0.7f, palette.glow * 1.4f, 14, 0.9f);
            _fx.FlashAt(core, burst.flashSize * 0.45f, 0.4f, new Color(1.6f, 2f, 1.7f), 0, 0f);
            _fx.Sparkles(core, burst.sparkleCount, 16f, 0.45f, palette.glow, default, 180f, 0.9f);
            _fx.Leaves(core, burst.leafCount, 0.8f, 7f);
            _fx.Powder(core, 14, 1.4f, 6f);
            _fx.Crosses(core, burst.crossCount, 0.75f, definition.satellites.membraneRadius, up);

            if (burst.shine)
            {
                _shineTime = 0f;
                _shineCenter = core;
            }

            if (burst.screenEdgeStrength > 0f)
            {
                _edgeTime = 0f;
            }

            var shake = definition.cameraShake;
            if (shake.burst != null && shake.burstStrength > 0f)
            {
                var rig = FortressCameraRig.Active;
                if (rig != null && (!shake.ownerViewOnly || rig.CurrentViewer == ownerIndex))
                {
                    rig.AddModifier(new SimpleShakeModifier(shake.burst, shake.burstStrength));
                }
            }
        }

        public void Tick(float dt)
        {
            TickSecondWave(dt);
            TickShine(dt);
            TickEdge(dt);
        }

        public void Hide()
        {
            _shineTime = -1f;
            _edgeTime = -1f;
            _secondWaveTimer = -1f;
            _shine.enabled = false;
            _canvas.enabled = false;
        }

        private void TickSecondWave(float dt)
        {
            if (_secondWaveTimer < 0f)
            {
                return;
            }

            _secondWaveTimer -= dt;
            if (_secondWaveTimer < 0f)
            {
                var burst = _fx.Definition.burst;
                var color = _fx.Definition.palette.green;
                color.a = 0.7f;
                _fx.WaveAt(_secondWaveCenter, 1f, burst.waveSize * 0.7f, burst.waveSeconds * 0.9f, color);
            }
        }

        /// <summary>光の帯が、画面で見て左上から右下へコアの上を通り抜ける。</summary>
        private void TickShine(float dt)
        {
            if (_shineTime < 0f)
            {
                _shine.enabled = false;
                return;
            }

            _shineTime += dt;
            var t = _shineTime / ShineSeconds;
            if (t >= 1f)
            {
                _shineTime = -1f;
                _shine.enabled = false;
                return;
            }

            var settings = _fx.Definition.satellites;
            var up = GreenMintUtil.ScreenUp;
            var right = new Vector2(up.y, -up.x);
            var sweep = Mathf.Lerp(-1.3f, 1.3f, GreenMintUtil.EaseOutCubic(t)) * settings.membraneRadius;
            var center = _shineCenter + right * sweep + up * (-sweep * 0.35f);
            var rotation = GreenMintUtil.FacingUp(up) * Quaternion.Euler(0f, 0f, -35f);
            _shine.transform.SetPositionAndRotation(new Vector3(center.x, center.y, GreenMintFx.ZOffset - 0.06f), rotation);
            _shine.transform.localScale = new Vector3(settings.membraneRadius * 0.55f, settings.membraneRadius * 2.6f, 1f);
            var fade = Mathf.Sin(t * Mathf.PI);
            _fx.ApplyRadial(_shine, new Color(1.6f, 2f, 1.75f) * fade, new Vector4(0.9f, 1.5f, 0f, 0f), new Vector4(4f, 0f, 0f, 0f), Vector4.zero);
            _shine.enabled = true;
        }

        private void TickEdge(float dt)
        {
            if (_edgeTime < 0f)
            {
                _canvas.enabled = false;
                return;
            }

            var burst = _fx.Definition.burst;
            _edgeTime += dt;
            var t = _edgeTime / burst.screenEdgeSeconds;
            if (t >= 1f)
            {
                _edgeTime = -1f;
                _canvas.enabled = false;
                return;
            }

            // すぐ明るくなって、ゆっくり引く。
            var rise = Mathf.Clamp01(t / 0.1f);
            var strength = burst.screenEdgeStrength * rise * (1f - Mathf.SmoothStep(0.1f, 1f, t));
            var color = Color.Lerp(_fx.Definition.palette.mint, _fx.Definition.palette.green, 0.2f);
            color.a = strength;
            _edge.color = color;
            _canvas.enabled = true;
        }
    }
}
