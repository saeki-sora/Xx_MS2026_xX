using MS2026.CharacterSelect;
using DDrive.Runtime.Loop;
using UnityEngine;
using MS2026.Fortress.Cameras;

namespace MS2026.Fortress
{
    /// <summary>黄色キャラがスキル玉を壊すと、そのプレイヤーの砲台の位置から反射する矢を放つ。</summary>
    [DisallowMultipleComponent]
    public sealed class YellowArrowSkill : MonoBehaviour
    {
        [Tooltip("発動するキャラクターのID。黄色は delta。")]
        public string characterId = "delta";
        [Tooltip("キャラ選択を通さずゲームを直接再生した場合の担当。0=P1、3=P4。")]
        [Range(0, 3)] public int previewPlayerIndex = 3;
        [Min(0.01f)] public float arrowSpeed = 12f;
        [Tooltip("発射から消滅までの秒数。")]
        [Min(0.01f)] public float arrowLifetime = 5f;
        [Min(0.01f)] public float arrowLength = 4.8f;
        [Tooltip("矢に触れている敵に与える毎秒ダメージ。コア・砲台・壁にはダメージを与えない。")]
        [Min(0f)] public float arrowDamagePerSecond = 200f;
        [Tooltip("壁として反射するCollider2Dのレイヤー。敵・コア・砲台・スキル玉・Triggerは除外。")]
        public LayerMask wallLayers = ~0;

        [Header("矢の画面揺れ")]
        public SimpleShakeSettings launchShake = new SimpleShakeSettings
            { amplitude = 0.65f, rollAmplitude = 1.1f, frequency = 17f, duration = 0.32f };
        public SimpleShakeSettings bounceShake = new SimpleShakeSettings
            { amplitude = 0.45f, rollAmplitude = 0.75f, frequency = 20f, duration = 0.24f };

        [Header("発射音")]
        [Tooltip("矢を放った瞬間に鳴らすSE。音量などは選択したD-DriveのSE設定で調整する。")]
        public FortressEffect launchSound = new FortressEffect();
        [Tooltip("矢の発射音の音量。通常のレーザーの発射音には影響しない。")]
        [Range(0f, 1f)] public float launchVolume = 1f;

        private void OnEnable() => SmashBallModule.AnyBroken += OnBroken;
        private void OnDisable()
        {
            SmashBallModule.AnyBroken -= OnBroken;
            foreach (Transform child in transform)
            {
                if (child.GetComponent<YellowSkillArrow>() != null)
                    Destroy(child.gameObject);
            }
        }

        private void OnBroken(SmashBallBreakInfo info)
        {
            // 不明な攻撃者のdefault値をP1として扱わない。
            var turret = info.Attacker.Turret;
            if (turret == null) return;
            var character = CharacterSelectionResult.Get(turret.playerIndex);
            if (CharacterSelectionResult.HasResult
                ? character == null || character.id != characterId
                : turret.playerIndex != previewPlayerIndex) return;

            var arrow = new GameObject("YellowSkillArrow");
            arrow.transform.SetParent(transform, false);
            arrow.transform.position = turret.transform.position;
            arrow.AddComponent<YellowSkillArrow>().Initialize(
                turret.AimDirection, arrowSpeed, arrowLifetime, arrowLength, wallLayers,
                turret, arrowDamagePerSecond, launchShake, bounceShake);
            var bootstrap = DDriveRuntimeBootstrap.Instance;
            if (launchSound != null && launchSound.HasSe && bootstrap != null
                && bootstrap.IsReady && bootstrap.Audio != null)
            {
                var audio = bootstrap.Audio;
                var handle = audio.PlaySe(launchSound.se);
                audio.SetVolume(handle, Mathf.Clamp01(launchVolume));
            }
        }
    }
}
