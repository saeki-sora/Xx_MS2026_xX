using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Skills.Yellow
{
    /// <summary>
    /// 黄色キャラのスキル「リコシェアロー」の設定ファイル。
    /// スマッシュボールを割ると使える状態になり、次に握った瞬間に砲台の向きへ矢を放つ。矢は壁で跳ね返り、触れた敵にダメージを与え、一定時間で消える。
    /// 「いつ撃つか」は赤・緑と同じく <see cref="PlayerSkillSlot"/> が受け持ち、矢を出すのは <see cref="YellowArrowSkillRunner"/>（スキル欄が自動で付ける）。
    /// 矢そのものの動き・見た目は <see cref="YellowSkillArrow"/>。
    /// </summary>
    [CreateAssetMenu(menuName = "MS2026/スキル/イエロー：リコシェアロー", fileName = "YellowArrowSkill")]
    public sealed class YellowArrowSkill : CharacterSkill
    {
        [Tooltip("放つ矢の速さ・長さ・寿命・ダメージ・跳ね返る壁。")]
        [SkillLabel("矢")]
        public YellowArrowSettings arrow = new YellowArrowSettings();

        [Tooltip("矢を放ったとき・壁で跳ね返ったときのカメラの揺れ。")]
        [SkillLabel("カメラの揺れ")]
        public YellowArrowCameraShake cameraShake = new YellowArrowCameraShake();

        [Tooltip("効果音と、D-Drive の追加エフェクト（空なら何もしない）。")]
        [SkillLabel("効果音・追加エフェクト（D-Drive）")]
        public YellowArrowSounds sounds = new YellowArrowSounds();

        public override CharacterSkillRunner CreateRunner(PlayerSkillSlot slot)
        {
            var runner = slot.gameObject.AddComponent<YellowArrowSkillRunner>();
            runner.Configure(this);
            return runner;
        }

        private void Reset()
        {
            skillId = "yellow_ricochet_arrow";
            displayName = "リコシェアロー";
            description = "スキル玉を壊すと、砲台の向きに矢を放つ。矢は壁で跳ね返り、一定時間で消滅する。";
            themeColor = new Color(0.98f, 0.84f, 0.2f);
        }
    }

    [System.Serializable]
    public sealed class YellowArrowSettings
    {
        [Tooltip("矢の速さ（メートル／秒）。")]
        [SkillLabel("速さ（m/秒）")]
        [Min(0.01f)]
        public float speed = 12f;

        [Tooltip("発射から消滅までの秒数。")]
        [SkillLabel("消えるまで（秒）")]
        [Min(0.01f)]
        public float lifetime = 5f;

        [Tooltip("矢の長さ（メートル）。")]
        [SkillLabel("長さ（m）")]
        [Min(0.01f)]
        public float length = 4.8f;

        [Tooltip("矢に触れている敵に与える毎秒ダメージ。コア・砲台・壁にはダメージを与えない。")]
        [SkillLabel("毎秒ダメージ")]
        [Min(0f)]
        public float damagePerSecond = 200f;

        [Tooltip("壁として反射するCollider2Dのレイヤー。敵・コア・砲台・スキル玉・Triggerは除外。")]
        [SkillLabel("跳ね返る壁のレイヤー")]
        public LayerMask wallLayers = ~0;
    }

    [System.Serializable]
    public sealed class YellowArrowCameraShake
    {
        [Tooltip("矢を放った瞬間の揺れ。")]
        [SkillLabel("放ったとき")]
        public SimpleShakeSettings launch = new SimpleShakeSettings
            { amplitude = 0.65f, rollAmplitude = 1.1f, frequency = 17f, duration = 0.32f };

        [Tooltip("壁で跳ね返ったときの揺れ。")]
        [SkillLabel("跳ね返ったとき")]
        public SimpleShakeSettings bounce = new SimpleShakeSettings
            { amplitude = 0.45f, rollAmplitude = 0.75f, frequency = 20f, duration = 0.24f };
    }

    [System.Serializable]
    public sealed class YellowArrowSounds
    {
        [Tooltip("使えるようになった瞬間（砲台の位置）。")]
        [SkillLabel("使えるようになった瞬間")]
        public FortressEffect onReady = new FortressEffect();

        [Tooltip("矢を放った瞬間に鳴らすSE（VFX も入れれば砲台の位置に出す）。音量は下の「発射音の音量」と D-Drive の SE 設定で調整する。")]
        [SkillLabel("矢を放った瞬間")]
        public FortressEffect onLaunch = new FortressEffect();

        [Tooltip("矢の発射音の音量。通常のレーザーの発射音には影響しない。")]
        [SkillLabel("発射音の音量")]
        [Range(0f, 1f)]
        public float launchVolume = 1f;
    }
}
