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

        public event Action<float, float> OnHealthChanged;
        public event Action OnCoreDestroyed;

        public float CurrentHealth { get; private set; }

        private float _nextDamageEffectTime;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
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

        private void Play(FortressEffect effect)
        {
            if (effect != null && !effect.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(effect, transform.position, Quaternion.identity);
            }
        }
    }
}
