using System;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 床に描く水の渦1枚（Vortex シェーダーの四角い板）。デバフエリアの渦と、溜め中に砲台の足元に出す小さな渦の両方で使う。
    /// 色・筋の数などは <see cref="BlueVortexLook"/> から毎回読む（Play 中に値を変えてもすぐ反映される）。
    /// 「水のシェーダーで描く」が ON なら水のシェーダー版（MS2026/BlueSkill/WaterVortex、2D-Water-Shader をもとにしたもの）で描く。
    /// 両方のマテリアルを作っておき、毎回どちらかを付ける（Play 中に切り替えてもよい）。水のシェーダーが見つからなければ前の渦で描く。
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
        private static readonly int DisplacementTexId = Shader.PropertyToID("_DisplacementTex");
        private static readonly int DisplacementDetailTexId = Shader.PropertyToID("_DisplacementDetailTex");
        private static readonly int WaterFoamColorId = Shader.PropertyToID("_WaterFoamColor");
        private static readonly int WaterParamsId = Shader.PropertyToID("_WaterParams");
        private static readonly int WaterParams2Id = Shader.PropertyToID("_WaterParams2");
        private static readonly int WaterParams3Id = Shader.PropertyToID("_WaterParams3");
        private static readonly int WaterParams4Id = Shader.PropertyToID("_WaterParams4");
        private static readonly int WaterParams5Id = Shader.PropertyToID("_WaterParams5");
        private static readonly int RingParamsId = Shader.PropertyToID("_RingParams");
        private static readonly int BubbleColorId = Shader.PropertyToID("_BubbleColor");

        private readonly Material _material;
        private readonly Material _waterMaterial;
        private readonly MeshRenderer _renderer;
        private Texture2D _gradient;
        // 水の模様が中心へ流れ込んだ量（消えるときに速さが変わっても模様が飛ばないよう、毎フレーム足していく）。
        private float _inflowPhase;
#if UNITY_EDITOR
        private float _rebakeTimer;
#endif

        public BlueVortexVisual(Transform parent, string objectName, Shader shader, Shader waterShader, BlueVortexLook look, int sortingOrder)
        {
            _material = BlueFx.CreateMaterial(shader, BlueFx.VortexShaderName, look.vortexRenderQueue);
            if (waterShader == null)
            {
                waterShader = Shader.Find(BlueFx.WaterVortexShaderName);
            }

            if (waterShader != null)
            {
                _waterMaterial = new Material(waterShader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = look.vortexRenderQueue };
            }

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
            DrawCore(look, center, radius, look.eyeSize, 0f, rotation, time, rim, clockwise, suck, look.opacity * Mathf.Clamp01(opacityScale), intensityScale, sortingOrder);
        }

        /// <summary>
        /// 中心が空いた「水の輪」として描く（水のシェーダーのときだけ。使えないときはふつうの渦で描く）。
        /// </summary>
        /// <param name="hole">内側の空いている所の大きさ（半径に対する割合、0〜0.95）</param>
        /// <param name="opacity">濃さ（0〜1。渦の「濃さ」は掛けない）</param>
        public void DrawRing(BlueVortexLook look, Vector3 center, float radius, float hole, float rotation, float time, float rim, bool clockwise,
            float opacity, float intensityScale, int sortingOrder)
        {
            DrawCore(look, center, radius, Mathf.Clamp(hole, 0f, 0.95f), 1f, rotation, time, rim, clockwise, 0f, Mathf.Clamp01(opacity), intensityScale, sortingOrder);
        }

        private void DrawCore(BlueVortexLook look, Vector3 center, float radius, float eyeSize, float hollow, float rotation, float time, float rim,
            bool clockwise, float suck, float opacity, float intensityScale, int sortingOrder)
        {
            if (radius <= 0.01f || opacity <= 0.001f)
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
            var material = look.useWaterShader && _waterMaterial != null ? _waterMaterial : _material;
            material.SetColor(FoamColorId, look.foamColor);
            material.SetColor(RimColorId, look.rimColor);
            material.SetColor(EyeColorId, look.eyeRingColor);
            material.SetVector(ParamsId, new Vector4(look.arms, look.twist, look.armSharpness, look.turbulence));
            material.SetVector(Params2Id, new Vector4(rotation, eyeSize, opacity, look.intensity * intensityScale));
            material.SetVector(Params3Id, new Vector4(time, rim, clockwise ? -1f : 1f, suck));
            if (material == _waterMaterial)
            {
                material.SetTexture(DisplacementTexId, look.waterDisplacement);
                material.SetTexture(DisplacementDetailTexId, look.waterDisplacementDetail);
                material.SetColor(WaterFoamColorId, look.waterFoamColor);
                material.SetVector(WaterParamsId, new Vector4(look.waterFlowSpeed, look.waterDetailFlowSpeed, look.waterDistortion, look.waterFoamThreshold));
                // 消えるとき（suck 0→1）は流れ込みが速くなっていく。
                _inflowPhase += Time.deltaTime * look.waterInflowSpeed * Mathf.Lerp(1f, look.drainInflowBoost, suck * suck);
                if (_inflowPhase > 1000f)
                {
                    _inflowPhase -= 1000f;
                }

                material.SetColor(BubbleColorId, look.gargleBubbleColor);
                material.SetVector(WaterParams2Id, new Vector4(look.waterAngularRepeat, look.waterRadialScale, look.drainEyeSize, look.waterEdgeFoam));
                material.SetVector(WaterParams3Id, new Vector4(look.waterFoamAlpha, look.waterArmStrength, _inflowPhase, look.waterSheen));
                material.SetVector(WaterParams4Id, new Vector4(look.gargleBubbleAmount, look.gargleBubbleRepeat, look.gargleBubbleSize, look.waterDepth));
                // 溝の本数 0 = 出さない（シェーダーは本数を1以上に丸めるので、ツヤと影を0にする）。
                var grooves = look.waterGrooveCount > 0;
                material.SetVector(WaterParams5Id, new Vector4(Mathf.Max(1, look.waterGrooveCount), look.waterGrooveTightness,
                    grooves ? look.waterGrooveShine : 0f, grooves ? look.waterGrooveShade : 0f));
                material.SetVector(RingParamsId, new Vector4(hollow, 1f, 0f, 0f));
            }

            material.renderQueue = look.vortexRenderQueue;
            if (_renderer.sharedMaterial != material)
            {
                _renderer.sharedMaterial = material;
            }

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
            if (_waterMaterial != null)
            {
                _waterMaterial.SetTexture(RadialTexId, _gradient);
            }

        }

        public void Dispose()
        {
            if (_renderer != null)
            {
                BlueFx.Destroy(_renderer.gameObject);
            }

            BlueFx.Destroy(_material);
            BlueFx.Destroy(_waterMaterial);
            BlueFx.Destroy(_gradient);
        }
    }
}
