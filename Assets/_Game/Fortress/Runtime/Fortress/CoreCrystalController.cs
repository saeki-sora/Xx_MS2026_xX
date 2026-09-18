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

        public event Action<float, float> OnHealthChanged;
        public event Action OnCoreDestroyed;

        public float CurrentHealth { get; private set; }

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
                OnCoreDestroyed?.Invoke();
            }
        }
    }
}
