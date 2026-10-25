using System;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 床に描く水の渦1枚（Vortex シェーダーの四角い板）。デバフエリアの渦と、溜め中に砲台の足元に出す小さな渦の両方で使う。
    /// 色・筋の数などは <see cref="BlueVortexLook"/> から毎回読む（Play 中に値を変えてもすぐ反映される）。
    /// </summary>
    internal sealed class BlueVortexVisual : IDisposable
    {
        private static readonly int RadialTexId = Shader.PropertyToID("_RadialTex");
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int EyeColorId = Shader.PropertyToID("_EyeColor");
        private static readonly int ParamsId = Shader.PropertyToID("_Params");
        private static readonly int Params2Id = Shader.PropertyToID("_Params2");
        private static readonly int Params3Id = Shader.PropertyToID("_Params3");

        private readonly Material _material;
        private readonly MeshRenderer _renderer;
        private Texture2D _gradient;
#if UNITY_EDITOR
        private float _rebakeTimer;
#endif

        public BlueVortexVisual(Transform parent, string objectName, Shader shader, BlueVortexLook look, int sortingOrder)
        {
            _material = BlueFx.CreateMaterial(shader, BlueFx.VortexShaderName, look.vortexRenderQueue);
            _renderer = BlueFx.CreateRenderer(parent, objectName, BlueFx.SharedQuad, _material, sortingOrder);
            Rebake(look);
        }

        /// <param name="radius">半径（m）</param>
        /// <param name="rotation">回転（ラジアン、積算した値）</param>
        /// <param name="rim">ふちの明るさ（点滅）</param>
        /// <param name="suck">中心への吸い込み（0〜1、消えるとき）</param>
        /// <param name="opacityScale">濃さの倍率（出てくる／消えるとき）</param>
        /// <param name="intensityScale">明るさの倍率</param>
        public void Draw(BlueVortexLook look, Vector3 center, float radius, float rotation, float time, float rim, bool clockwise,
            float suck, float opacityScale, float intensityScale, int sortingOrder)
        {
            if (radius <= 0.01f || opacityScale <= 0.001f)
            {
                _renderer.enabled = false;
                return;
            }

#if UNITY_EDITOR
            // エディタで Play 中に色を変えたとき用に、ときどき焼き直す。
            _rebakeTimer -= Time.unscaledDeltaTime;
            if (_rebakeTimer <= 0f)
            {
                _rebakeTimer = 0.25f;
                Rebake(look);
            }
#endif

            var t = _renderer.transform;
            t.SetPositionAndRotation(center, Quaternion.identity);
            t.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            _material.SetColor(FoamColorId, look.foamColor);
            _material.SetColor(RimColorId, look.rimColor);
            _material.SetColor(EyeColorId, look.eyeRingColor);
            _material.SetVector(ParamsId, new Vector4(look.arms, look.twist, look.armSharpness, look.turbulence));
            _material.SetVector(Params2Id, new Vector4(rotation, look.eyeSize, look.opacity * Mathf.Clamp01(opacityScale), look.intensity * intensityScale));
            _material.SetVector(Params3Id, new Vector4(time, rim, clockwise ? -1f : 1f, suck));
            _material.renderQueue = look.vortexRenderQueue;
            _renderer.sortingOrder = sortingOrder;
            _renderer.enabled = true;
        }

        public void Hide()
        {
            _renderer.enabled = false;
        }

        public void Rebake(BlueVortexLook look)
        {
            _gradient = BlueFx.BakeGradient(look.radialGradient, 64, _gradient);
            _material.SetTexture(RadialTexId, _gradient);
        }

        public void Dispose()
        {
            if (_renderer != null)
            {
                BlueFx.Destroy(_renderer.gameObject);
            }

            BlueFx.Destroy(_material);
            BlueFx.Destroy(_gradient);
        }
    }
}
