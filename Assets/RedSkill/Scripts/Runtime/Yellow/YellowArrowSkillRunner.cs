using System.Collections.Generic;
using DDrive.Runtime.Loop;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Yellow
{
    /// <summary>
    /// 「リコシェアロー」を実際に動かす係（<see cref="YellowArrowSkill.CreateRunner"/> が砲台に付ける。シーンに手で置く必要はない）。
    /// 発動（使える状態で次に握った瞬間）で矢を1本放ち、すぐにスロットへ終了を知らせる。矢は自分で飛び、寿命が来たら消える。
    /// 発動した握りの間は普段のレーザーを出さない（<see cref="CharacterSkill.hideNormalLaserUntilRelease"/>）。一度離せば普段のレーザーに戻る。
    /// ダメージは結果を決めるPC（オフライン／Host）だけ（<see cref="YellowSkillArrow"/> 側で判定）。見た目・効果音・揺れは全PCがそれぞれ出す。
    /// </summary>
    // スキルを別の設定ファイルに差し替えるとき、古い係の削除はフレームの最後なので、同じ砲台に一時的に2つ付くことがある（DisallowMultipleComponent にしない）。
    public sealed class YellowArrowSkillRunner : CharacterSkillRunner
    {
        [SerializeField]
        private YellowArrowSkill definition;

        private readonly List<GameObject> _arrows = new List<GameObject>();

        /// <summary>使っている設定ファイル。</summary>
        public YellowArrowSkill Definition => definition;

        // 矢を放った瞬間に終わる（矢が飛んでいる間もスロットは次の溜めを受け付ける）。
        public override bool IsActive => false;

        internal void Configure(YellowArrowSkill skill)
        {
            definition = skill;
        }

        public override void SetReadyVisual(bool ready)
        {
            if (ready && definition != null && Turret != null)
            {
                FortressEffectPlayer.PlayOnce(definition.sounds.onReady, Turret.transform.position, Quaternion.identity, Turret.playerIndex);
            }
        }

        public override void Activate()
        {
            if (definition != null && Turret != null)
            {
                Launch();
            }

            NotifyFinished();
        }

        public override void Cancel()
        {
        }

        private void OnDestroy()
        {
            foreach (var arrow in _arrows)
            {
                if (arrow != null)
                {
                    Destroy(arrow);
                }
            }

            _arrows.Clear();
        }

        private void Launch()
        {
            _arrows.RemoveAll(a => a == null);
            var turret = Turret;
            var settings = definition.arrow;
            var position = turret.transform.position;

            // 砲台の子にすると砲台の回転に引っぱられるので、シーン直下に置く（矢は寿命が来たら自分で消える）。
            var arrow = new GameObject($"[YellowSkill] Arrow P{turret.playerIndex + 1}");
            arrow.transform.position = position;
            arrow.AddComponent<YellowSkillArrow>().Initialize(
                turret.AimDirection, settings.speed, settings.lifetime, settings.length, settings.wallLayers,
                turret, settings.damagePerSecond, definition.cameraShake.launch, definition.cameraShake.bounce);
            _arrows.Add(arrow);

            PlayLaunchSound(position, turret.playerIndex);
        }

        private void PlayLaunchSound(Vector3 position, int playerIndex)
        {
            var effect = definition.sounds.onLaunch;
            if (effect == null)
            {
                return;
            }

            if (effect.HasVfx)
            {
                FortressEffectPlayer.SpawnVfx(effect, position, Quaternion.identity, playerIndex);
            }

            // 発射音だけ音量を変えられるようにするため、FortressEffectPlayer を通さず D-Drive で直接鳴らす。
            var bootstrap = DDriveRuntimeBootstrap.Instance;
            if (effect.HasSe && bootstrap != null && bootstrap.IsReady && bootstrap.Audio != null)
            {
                var audio = bootstrap.Audio;
                var handle = audio.PlaySe(effect.se);
                audio.SetVolume(handle, Mathf.Clamp01(definition.sounds.launchVolume));
            }
        }
    }
}
