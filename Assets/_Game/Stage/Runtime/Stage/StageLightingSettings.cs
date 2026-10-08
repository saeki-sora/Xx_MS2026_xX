using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Stage
{
    /// <summary>ステージごとの光の当たり方（太陽の向き・色、まわりの明るさ、霧）。</summary>
    [Serializable]
    public sealed class StageLightingSettings
    {
        [Tooltip("ONなら、このステージを出したときにシーンの光をこの設定にする。OFFならシーンの光をそのまま使う。")]
        public bool apply = true;

        // ── 太陽（メインの光） ──（見出しはステージ背景スタジオの「光」ページが出す）
        [Tooltip("太陽の色。")]
        public Color sunColor = new Color(1f, 0.96f, 0.9f);

        [Tooltip("太陽の明るさ。")]
        [Range(0f, 4f)] public float sunIntensity = 1.2f;

        [Tooltip("太陽がどの方向から照らすか（床の上の向き、度。0=画面の上から）。")]
        [Range(-180f, 180f)] public float sunDirection = -35f;

        [Tooltip("太陽の高さ（度。90=真上から、小さいほど影が長くなる）。")]
        [Range(5f, 90f)] public float sunElevation = 55f;

        [Tooltip("影の濃さ（0=影なし）。")]
        [Range(0f, 1f)] public float shadowStrength = 0.6f;

        // ── まわりの明るさ ──（見出しはステージ背景スタジオの「光」ページが出す）
        [Tooltip("上からの環境光の色。")]
        public Color skyColor = new Color(0.62f, 0.68f, 0.78f);

        [Tooltip("横からの環境光の色。")]
        public Color equatorColor = new Color(0.5f, 0.5f, 0.52f);

        [Tooltip("下からの環境光の色（床の照り返し）。")]
        public Color groundColor = new Color(0.32f, 0.3f, 0.28f);

        // ── 霧（遠くをかすませる） ──（見出しはステージ背景スタジオの「光」ページが出す）
        [Tooltip("ONなら、カメラから遠い所ほど霧の色にかすませる。")]
        public bool fog;

        [Tooltip("霧の色。")]
        public Color fogColor = new Color(0.75f, 0.8f, 0.85f);

        [Tooltip("カメラからこの距離でかすみ始める（ワールド単位）。")]
        [Min(0f)] public float fogStart = 20f;

        [Tooltip("カメラからこの距離で完全に霧の色になる（ワールド単位）。")]
        [Min(0f)] public float fogEnd = 60f;

        /// <summary>シーンの環境光・霧と、渡された太陽（Directional Light）に反映する。</summary>
        public void ApplyTo(Light sun)
        {
            if (!apply)
            {
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = Mathf.Max(fogStart + 0.01f, fogEnd);

            if (sun == null)
            {
                return;
            }

            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = shadowStrength > 0f ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = shadowStrength;
            sun.transform.rotation = SunRotation(sunDirection, sunElevation);
        }

        /// <summary>床（XY平面、上が-Z）に対する太陽の向き。elevation=90で真上（+Z向き）から照らす。</summary>
        public static Quaternion SunRotation(float directionDegrees, float elevationDegrees)
        {
            var azimuth = directionDegrees * Mathf.Deg2Rad;
            var elevation = elevationDegrees * Mathf.Deg2Rad;
            // 光が「どこから来るか」（床の上の向き＋高さ）。光の進む向きはその逆。
            var from = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Cos(azimuth) * Mathf.Cos(elevation), -Mathf.Sin(elevation));
            var forward = -from;
            return Quaternion.LookRotation(forward, Mathf.Abs(forward.z) > 0.99f ? Vector3.up : Vector3.back);
        }
    }
}
