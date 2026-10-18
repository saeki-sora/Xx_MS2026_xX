using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// スキルを実際に動かす係の共通部分。<see cref="CharacterSkill.CreateRunner"/> が砲台に付け、
    /// <see cref="PlayerSkillSlot"/> から「溜め表示のON/OFF」「発動」「中断」を指示される。
    /// 撃ち終わったら <see cref="NotifyFinished"/> を呼んでスロットに知らせること。
    /// ネット対戦では全PCで同じように動く（見た目は各PC、ダメージなど結果に関わる処理は FortressNet.HasSimulationAuthority のPCだけ）。
    /// </summary>
    public abstract class CharacterSkillRunner : MonoBehaviour
    {
        /// <summary>このスキルを持っているスロット。</summary>
        public PlayerSkillSlot Slot { get; private set; }

        /// <summary>撃つ砲台。</summary>
        public LaserTurret Turret => Slot != null ? Slot.Turret : null;

        /// <summary>撃っている最中か（ため・撃つ・消えるまでを含む）。</summary>
        public abstract bool IsActive { get; }

        internal void Bind(PlayerSkillSlot slot)
        {
            Slot = slot;
            OnBound();
        }

        /// <summary>スロットに結びついた直後に1回呼ばれる。設定の読み込み・見た目の準備はここで行う。</summary>
        protected virtual void OnBound()
        {
        }

        /// <summary>「使える状態（溜め）」の見た目を出す／消す。</summary>
        public virtual void SetReadyVisual(bool ready)
        {
        }

        /// <summary>スキルを撃ち始める。</summary>
        public abstract void Activate();

        /// <summary>撃っている途中でも、すぐにやめる（見た目も消す）。</summary>
        public abstract void Cancel();

        /// <summary>撃ち終わったことをスロットに知らせる。</summary>
        protected void NotifyFinished()
        {
            if (Slot != null)
            {
                Slot.HandleRunnerFinished(this);
            }
        }
    }
}
