using System;
using System.Collections.Generic;
using UnityEngine;
using MS2026.ShaderFX.Modules;

namespace MS2026.ShaderFX.Editor
{
    // A preset adds a whole combination of EffectModules to whatever Profile is currently open,
    // in one step — as opposed to "+ Add Effect"'s normal one-module-at-a-time flow. Lets an
    // artist start from "ホログラム敵" and still freely add/tweak more modules on top, or combine
    // two presets in the same Profile, rather than being stuck with a separate premade asset
    // (see also Samples~/PresetLibrary, which ships these same looks as standalone .asset files
    // for the Package Manager Samples workflow — this is the same catalog, just inserted directly
    // into the Profile you're already editing instead of requiring a whole new asset).
    public readonly struct EffectPreset
    {
        public readonly string Category; // "オブジェクト系" or "画面系", matching EffectModuleInfo's own categories
        public readonly string Name;
        public readonly Func<List<EffectModule>> Build;

        public EffectPreset(string category, string name, Func<List<EffectModule>> build)
        {
            Category = category;
            Name = name;
            Build = build;
        }
    }

    public static class EffectPresetLibrary
    {
        public static IReadOnlyList<EffectPreset> Presets => presets;

        private static readonly List<EffectPreset> presets = new()
        {
            // --- オブジェクト系 ---

            new EffectPreset("オブジェクト系", "アニメ調キャラ標準", () => new List<EffectModule>
            {
                new RimLightModule { color = new Color(1f, 0.95f, 0.85f, 1f), power = 4f, intensity = 1.2f },
            }),

            new EffectPreset("オブジェクト系", "聖なる守護者", () => new List<EffectModule>
            {
                new RimLightModule { color = new Color(1f, 0.9f, 0.5f, 1f), power = 2f, intensity = 2.5f },
                new EmissionControlModule { color = new Color(1f, 0.85f, 0.4f, 1f), intensity = 1.2f, pulseSpeed = 1.5f },
            }),

            new EffectPreset("オブジェクト系", "ホログラム敵", () => new List<EffectModule>
            {
                new HologramModule(),
            }),

            new EffectPreset("オブジェクト系", "サイバーステルス", () => new List<EffectModule>
            {
                new HologramModule { color = new Color(0.5f, 1f, 0.6f, 1f), fresnelPower = 3.5f },
                new GlitchModule { amount = 0.25f, speed = 6f },
            }),

            new EffectPreset("オブジェクト系", "氷結", () => new List<EffectModule>
            {
                new FrostModule(),
            }),

            new EffectPreset("オブジェクト系", "呪われたアンデッド", () => new List<EffectModule>
            {
                new FrostModule { color = new Color(0.55f, 0.75f, 0.5f, 1f), amount = 0.6f, sparkleColor = new Color(0.6f, 1f, 0.5f, 1f) },
                new DissolveModule { amount = 0.15f, edgeColor = new Color(0.4f, 0.9f, 0.3f, 1f) },
            }),

            new EffectPreset("オブジェクト系", "発火", () => new List<EffectModule>
            {
                new FireModule(),
            }),

            new EffectPreset("オブジェクト系", "炎の魔物", () => new List<EffectModule>
            {
                new FireModule { intensity = 2.5f },
                new EmissionControlModule { color = new Color(1f, 0.35f, 0.05f, 1f), intensity = 0.6f, pulseSpeed = 2f },
            }),

            new EffectPreset("オブジェクト系", "パワーアップ状態", () => new List<EffectModule>
            {
                new RimLightModule { color = new Color(1f, 0.8f, 0.2f, 1f), power = 1.5f, intensity = 3f },
                new EmissionControlModule { color = new Color(1f, 0.8f, 0.2f, 1f), intensity = 1.5f, pulseSpeed = 4f },
                new UVScrollModule { color = new Color(1f, 0.9f, 0.3f, 1f), direction = new Vector2(0f, 1f), speed = 2f },
            }),

            new EffectPreset("オブジェクト系", "トゥーン標準", () => new List<EffectModule>
            {
                new ToonShadingModule { steps = 3 },
            }),

            new EffectPreset("オブジェクト系", "ディゾルブ消滅", () => new List<EffectModule>
            {
                new DissolveModule { amount = 0f },
            }),

            new EffectPreset("オブジェクト系", "被弾フラッシュ", () => new List<EffectModule>
            {
                new HitFlashModule { amount = 0f },
            }),

            new EffectPreset("オブジェクト系", "グリッチ故障", () => new List<EffectModule>
            {
                new GlitchModule { amount = 0f },
            }),

            // --- 画面系 ---

            new EffectPreset("画面系", "回想シーン(モノクロ)", () => new List<EffectModule>
            {
                new GrayscaleModule { intensity = 1f },
            }),

            new EffectPreset("画面系", "レトロゲーム風", () => new List<EffectModule>
            {
                new PosterizeModule { levels = 5 },
                new PixelateModule { blockSize = 6f },
            }),

            new EffectPreset("画面系", "ドット絵フィルター", () => new List<EffectModule>
            {
                new PixelateModule { blockSize = 8f },
            }),

            new EffectPreset("画面系", "コミック風輪郭線", () => new List<EffectModule>
            {
                new OutlineModule(),
            }),

            new EffectPreset("画面系", "被弾赤フラッシュ", () => new List<EffectModule>
            {
                new ScreenFlashModule { color = Color.red, amount = 0f },
            }),

            new EffectPreset("画面系", "回復白フラッシュ", () => new List<EffectModule>
            {
                new ScreenFlashModule { color = Color.white, amount = 0f },
            }),

            new EffectPreset("画面系", "衝撃波(ボス登場)", () => new List<EffectModule>
            {
                new ShockwaveModule { progress = 0f, strength = 0.08f, width = 0.2f },
            }),

            new EffectPreset("画面系", "気絶・暗転演出", () => new List<EffectModule>
            {
                new ScreenFlashModule { color = Color.black, amount = 0f },
                new GrayscaleModule { intensity = 0.6f },
            }),
        };
    }
}
