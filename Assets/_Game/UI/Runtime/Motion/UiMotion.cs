using System;
using DDrive.Runtime.Ui;
using UnityEngine;
using SeId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.SeMarker>;
using UiTweenId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Ui.UiTweenMarker>;

namespace MS2026.UI
{
    public enum UiMotionSource
    {
        /// <summary>D-Drive の定番の動き（59種）から選ぶ。</summary>
        Preset,

        /// <summary>D-Drive の UI Tween エディタで作った動きを使う。</summary>
        Tween
    }

    /// <summary>
    /// 動き1つ分の指定。中身は D-Drive の UI の動き（UiFx）なので、動きの種類・長さはいつでも差し替えられる。
    /// </summary>
    [Serializable]
    public sealed class UiMotion
    {
        [Tooltip("定番の動きから選ぶか、UI Tween エディタで作った動きを使うか。")]
        public UiMotionSource source = UiMotionSource.Preset;

        [Tooltip("定番の動き（D-Drive）。長さ0はその動きの標準の長さ。")]
        public UiPresetRef preset;

        [Tooltip("UI Tween エディタで作った動き（D-Drive の番号札）。")]
        public UiTweenId tween;

        [Tooltip("きっかけからこの秒数待ってから動く。順番に出したいときに少しずつずらす。")]
        [Min(0f)]
        public float delay;

        [Tooltip("動きと一緒に鳴らす効果音（D-Drive）。定番の動きは、その中の効果音の欄でも指定できる。")]
        public SeId se;

        public bool IsEmpty => source == UiMotionSource.Preset ? preset.Preset == UiPreset.None : !tween.IsValid;

        public static UiMotion FromPreset(UiPreset preset, float duration = 0f, float delay = 0f) => new UiMotion
        {
            source = UiMotionSource.Preset,
            preset = new UiPresetRef { Preset = preset, Duration = duration },
            delay = delay
        };

        /// <summary>表示用の短い説明（例: 「PopIn 0.3秒」）。</summary>
        public string Summary => IsEmpty
            ? "（なし）"
            : source == UiMotionSource.Preset
                ? $"{preset.Preset}{(preset.Duration > 0f ? $" {preset.Duration:0.##}秒" : "")}{(delay > 0f ? $" +{delay:0.##}秒後" : "")}"
                : $"Tween {tween}{(delay > 0f ? $" +{delay:0.##}秒後" : "")}";
    }
}
