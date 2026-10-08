using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>背景用シェーダー（MS2026/Stage/Prop）と群衆のシルエットが使う値の名前。</summary>
    public static class StageShaderIds
    {
        public const string PropShaderName = "MS2026/Stage/Prop";

        /// <summary>背景オブジェクトが書き込むステンシルのビット。群衆のシルエットはこのビットがある所にだけ出る。</summary>
        public const int SilhouetteStencilBit = 64;

        // マテリアルの項目
        public static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
        public static readonly int ReceiveHoles = Shader.PropertyToID("_ReceiveHoles");

        // オブジェクトごと（MaterialPropertyBlock）。どれも0が「何もしない」。
        public static readonly int Fade = Shader.PropertyToID("_StageFade");
        public static readonly int NoHole = Shader.PropertyToID("_StageNoHole");
        public static readonly int Darken = Shader.PropertyToID("_StageDarken");
        public static readonly int Flash = Shader.PropertyToID("_StageFlash");
        public static readonly int FlashColor = Shader.PropertyToID("_StageFlashColor");
        public static readonly int Dissolve = Shader.PropertyToID("_StageDissolve");
        public static readonly int DissolveColor = Shader.PropertyToID("_StageDissolveColor");
        public static readonly int HeatCount = Shader.PropertyToID("_StageHeatCount");
        public static readonly int HeatPoints = Shader.PropertyToID("_StageHeatPoints");
        public static readonly int HeatValues = Shader.PropertyToID("_StageHeatValues");

        // 全体（Shader.SetGlobal）
        public static readonly int FocusCount = Shader.PropertyToID("_StageFocusCount");
        public static readonly int FocusPoints = Shader.PropertyToID("_StageFocusPoints");
        public static readonly int HoleSoftness = Shader.PropertyToID("_StageHoleSoftness");
        public static readonly int HeatGlowColor = Shader.PropertyToID("_StageHeatGlowColor");
        public static readonly int HeatHotColor = Shader.PropertyToID("_StageHeatHotColor");
        public static readonly int ScorchColor = Shader.PropertyToID("_StageScorchColor");
        public static readonly int SilhouetteColor = Shader.PropertyToID("_FortressSilhouetteColor");
        public static readonly int SilhouetteEnabled = Shader.PropertyToID("_FortressSilhouetteEnabled");

        /// <summary>シェーダー側の配列の大きさ（変えるときは StagePropCommon.hlsl も合わせる）。</summary>
        public const int MaxFocusPoints = 8;

        public const int MaxHeatPoints = 8;
    }
}
