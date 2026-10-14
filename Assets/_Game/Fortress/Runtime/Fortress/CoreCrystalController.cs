using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 要塞の中心にあるコアクリスタルの耐久値管理。
    /// 見た目がプレイスタイルで変化する仕様は将来フェーズで拡張する想定で、
    /// ここではHPと破壊イベントのみを扱う最小実装にしている。
    /// </summary>
    public sealed class CoreCrystalController : MonoBehaviour
    {
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("ダメージを受けたとき・壊れたときの演出（エフェクトと効果音）。")]
        public CoreEffectSettings effects = new CoreEffectSettings();

        [Tooltip("受けるダメージの倍率（1 = そのまま、0.5 = 半分）。試合の決まり（FortressMatchRules）が試合の始めに入れる。")]
        [Min(0f)]
        public float damageMultiplier = 1f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnCoreDestroyed;

        public float CurrentHealth { get; private set; }

        /// <summary>最大HPを変える（試合の始めに使う。refill なら満タンにする）。</summary>
        public void SetMaxHealth(float max, bool refill)
        {
            maxHealth = Mathf.Max(1f, max);
            CurrentHealth = refill ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private float _nextDamageEffectTime;

        private void Awake()
        {
            CurrentHealth = maxHealth;

            // HPに応じて少しずつ汚れていく見た目（無ければ自動で付ける）。
            if (GetComponent<CoreDirtVisual>() == null)
            {
                gameObject.AddComponent<CoreDirtVisual>();
            }
        }

        public void TakeDamage(float amount)
        {
            amount *= damageMultiplier;
            if (CurrentHealth <= 0f || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth <= 0f)
            {
                Play(effects?.onDestroyed);
                OnCoreDestroyed?.Invoke();
            }
            else if (effects != null && Time.time >= _nextDamageEffectTime)
            {
                _nextDamageEffectTime = Time.time + effects.damageEffectInterval;
                Play(effects.onDamaged);
            }
        }

        /// <summary>
        /// ネット対戦の Client 用: Host から届いた HP を反映する（2026-10-13。今までコアの HP は Host でしか減らなかった）。
        /// 見た目（汚れ）・HUD・演出は、ふつうにダメージを受けたときと同じく変化を見て各PCで出す。
        /// </summary>
        public void ApplyReplicatedHealth(float current)
        {
            current = Mathf.Clamp(current, 0f, maxHealth);
            if (Mathf.Abs(current - CurrentHealth) < 0.001f)
            {
                return;
            }

            var before = CurrentHealth;
            CurrentHealth = current;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (current >= before)
            {
                return;
            }

            if (current <= 0f)
            {
                Play(effects?.onDestroyed);
                OnCoreDestroyed?.Invoke();
            }
            else if (effects != null && Time.time >= _nextDamageEffectTime)
            {
                _nextDamageEffectTime = Time.time + effects.damageEffectInterval;
                Play(effects.onDamaged);
            }
        }

        private void Play(FortressEffect effect)
        {
            if (effect != null && !effect.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(effect, transform.position, Quaternion.identity);
            }
        }
    }
}
