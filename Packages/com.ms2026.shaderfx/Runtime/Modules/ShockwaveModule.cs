using System;
using UnityEngine;

namespace MS2026.ShaderFX.Modules
{
    // 画面系: EffectDirector.RebuildScreenSettingsは登録された全EffectTargetを走査して「最後に処理された
    // ものが勝つ」形で集約するため(既存の画面系モジュール共通の仕様)、progressの制御は基本的にスクリプトから
    // 都度Profileを差し替えるか、SetInstanceFloat相当の仕組みではなくProfile自体をTween/Timelineで
    // 差し替える運用を想定しています。
    [Serializable]
    [EffectModuleInfo("衝撃波(ディストーション)", "画面系",
        Description = "画面上の指定した点から輪状に広がる歪みです。爆発や魔法発動の瞬間などに、画面全体を大きく揺らす" +
            "「感動する」演出として使えます。")]
    public sealed class ShockwaveModule : EffectModule
    {
        [Tooltip("衝撃波の中心(画面座標、0〜1)です。(0.5, 0.5)で画面中央です。")]
        public Vector2 center = new(0.5f, 0.5f);

        [Range(0f, 1f)]
        [Tooltip("衝撃波の広がり具合です。0で震源に集まった状態、1で画面全体まで広がりきった状態です。" +
            "0→1へアニメーションさせることで「輪が広がっていく」演出になります。")]
        public float progress;

        [Range(0f, 0.3f)]
        [Tooltip("歪みの強さです。値を大きくするほど画面が大きく揺らぎます。")]
        public float strength = 0.05f;

        [Range(0.02f, 1f)]
        [Tooltip("輪の太さです。値を大きくすると広い範囲がまとめて歪み、小さくすると細い輪だけが歪みます。")]
        public float width = 0.15f;

        public override string Keyword => string.Empty;

        public override void ApplyTo(Material material) { }

        public override void ApplyToScreen(ScreenFXSettings settings)
        {
            settings.shockwaveEnabled = true;
            settings.shockwaveCenter = center;
            settings.shockwaveProgress = progress;
            settings.shockwaveStrength = strength;
            settings.shockwaveWidth = width;
        }

        public override int ComputeParameterHash() => HashCode.Combine(center, progress, strength, width);

        public override string GetSummary() => $"Progress {progress:0.00}";
    }
}
