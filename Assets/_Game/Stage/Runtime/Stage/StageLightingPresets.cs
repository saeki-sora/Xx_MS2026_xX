using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Stage
{
    /// <summary>
    /// 光ページの「おすすめの光」。太陽とまわりの明るさだけを入れる（霧・ほかの項目はそのまま）。
    /// 押したらすぐ効くように「光を当てる」も ON にする。
    /// </summary>
    public static class StageLightingPresets
    {
        public sealed class Preset
        {
            public Preset(string label, string tooltip, Color sunColor, float sunIntensity, float sunDirection, float sunElevation,
                float shadowStrength, Color sky, Color equator, Color ground)
            {
                Label = label;
                Tooltip = tooltip;
                _sunColor = sunColor;
                _sunIntensity = sunIntensity;
                _sunDirection = sunDirection;
                _sunElevation = sunElevation;
                _shadowStrength = shadowStrength;
                _sky = sky;
                _equator = equator;
                _ground = ground;
            }

            public string Label { get; }

            public string Tooltip { get; }

            private readonly Color _sunColor;
            private readonly float _sunIntensity;
            private readonly float _sunDirection;
            private readonly float _sunElevation;
            private readonly float _shadowStrength;
            private readonly Color _sky;
            private readonly Color _equator;
            private readonly Color _ground;

            public void ApplyTo(StageLightingSettings settings)
            {
                if (settings == null)
                {
                    return;
                }

                settings.apply = true;
                settings.sunColor = _sunColor;
                settings.sunIntensity = _sunIntensity;
                settings.sunDirection = _sunDirection;
                settings.sunElevation = _sunElevation;
                settings.shadowStrength = _shadowStrength;
                settings.skyColor = _sky;
                settings.equatorColor = _equator;
                settings.groundColor = _ground;
            }
        }

        public static readonly IReadOnlyList<Preset> All = new[]
        {
            new Preset("昼（標準）", "初めて作ったときと同じ、明るい昼の光。",
                new Color(1f, 0.96f, 0.9f), 1.2f, -35f, 55f, 0.6f,
                new Color(0.62f, 0.68f, 0.78f), new Color(0.5f, 0.5f, 0.52f), new Color(0.32f, 0.3f, 0.28f)),
            new Preset("ひかえめ", "昼より少し暗め。明るすぎる・まぶしいと感じたときに。",
                new Color(1f, 0.97f, 0.92f), 0.8f, -35f, 60f, 0.5f,
                new Color(0.55f, 0.6f, 0.68f), new Color(0.45f, 0.45f, 0.47f), new Color(0.3f, 0.28f, 0.26f)),
            new Preset("曇り（やわらか）", "太陽を弱く・真上寄りにして、影も薄い。全体がふんわり明るい。",
                new Color(0.95f, 0.96f, 1f), 0.55f, -20f, 75f, 0.3f,
                new Color(0.66f, 0.7f, 0.76f), new Color(0.58f, 0.59f, 0.62f), new Color(0.4f, 0.39f, 0.38f)),
            new Preset("夕方", "低い位置からのオレンジの光。影が長くなる。",
                new Color(1f, 0.68f, 0.42f), 1f, -70f, 20f, 0.75f,
                new Color(0.5f, 0.45f, 0.6f), new Color(0.62f, 0.46f, 0.4f), new Color(0.28f, 0.2f, 0.18f)),
            new Preset("夜", "青白い弱い光。まわりも暗い。",
                new Color(0.62f, 0.72f, 1f), 0.45f, 30f, 60f, 0.5f,
                new Color(0.16f, 0.2f, 0.34f), new Color(0.13f, 0.15f, 0.24f), new Color(0.07f, 0.07f, 0.1f)),
            new Preset("くっきり", "太陽を強く・まわりを暗めにして、影と立体感をはっきりさせる。",
                new Color(1f, 0.97f, 0.92f), 1.35f, -40f, 45f, 0.85f,
                new Color(0.42f, 0.47f, 0.58f), new Color(0.33f, 0.33f, 0.36f), new Color(0.18f, 0.17f, 0.16f)),
        };

        /// <summary>光の設定を、初めて作ったときの値（全項目）に戻す。</summary>
        public static void ResetToDefaults(StageLightingSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new StageLightingSettings()), settings);
        }

        /// <summary>名前で探す（見つからなければ null）。</summary>
        public static Preset Find(string label)
        {
            foreach (var preset in All)
            {
                if (string.Equals(preset.Label, label, StringComparison.Ordinal))
                {
                    return preset;
                }
            }

            return null;
        }
    }
}
