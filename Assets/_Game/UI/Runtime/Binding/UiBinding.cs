using System;
using UnityEngine;

namespace MS2026.UI
{
    /// <summary>
    /// 値の掲示板（<see cref="UiValues"/>）の1つの値を見て、見た目を変える部品の共通部分。
    /// 編集中も動く（ExecuteAlways）ので、UIスタジオのサンプル値で Play せずに見た目を確かめられる。
    /// </summary>
    [ExecuteAlways]
    public abstract class UiBinding : MonoBehaviour
    {
        [Tooltip("見る値の名前。UIスタジオの「値」ページの一覧から選べる。")]
        [UiValueKey]
        public string key;

        private IDisposable _subscription;
        private string _subscribedKey;

        /// <summary>最後に受け取った値（まだ無ければ HasValue=false）。</summary>
        protected UiValue Current { get; private set; }

        protected virtual void OnEnable()
        {
            Resubscribe();
        }

        protected virtual void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            _subscribedKey = null;
        }

        protected virtual void OnValidate()
        {
            if (isActiveAndEnabled && _subscribedKey != key)
            {
                Resubscribe();
            }
        }

        /// <summary>値が変わったとき（と、見始めたとき）に呼ばれる。</summary>
        protected abstract void Apply(UiValue value);

        private void Resubscribe()
        {
            _subscription?.Dispose();
            _subscribedKey = key;
            _subscription = UiValues.Subscribe(key, OnChanged);
        }

        private void OnChanged(UiValue value)
        {
            if (this == null)
            {
                return;
            }

            Current = value;
            Apply(value);
        }
    }
}
