using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    public enum NavigationObstacleMode
    {
        /// <summary>通行不可。敵は迂回する。</summary>
        Block,

        /// <summary>通行は可能だがコストが上がる/速度が落ちる地帯（泥・水たまり・危険地帯など）。</summary>
        Cost
    }

    /// <summary>
    /// このオブジェクトの有効なCollider2Dの範囲を、経路探索グリッドに反映する。
    /// 壊れる/壊れないの区別はせず「経路にどう影響するか」だけを担当する（壊れる挙動はDestructibleObstacle）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationObstacle : MonoBehaviour
    {
        [Tooltip("Block=通れない壁。Cost=通れるが通りにくい/遅くなる地帯。")]
        public NavigationObstacleMode mode = NavigationObstacleMode.Block;

        [Tooltip("Costモード時の通行コスト倍率。大きいほど敵はその地帯を避けて迂回する。")]
        [Min(1f)]
        public float costMultiplier = 3f;

        [Tooltip("Costモード時、その地帯を歩く敵の移動速度倍率。0.5なら半分の速さ。")]
        [Range(0.05f, 1f)]
        public float speedMultiplier = 0.6f;

        [Tooltip("実行中に動く障害物ならON。毎フレーム位置を監視し、動いたら経路を再計算する。")]
        public bool isDynamic;

        private static readonly HashSet<NavigationObstacle> Registry = new HashSet<NavigationObstacle>();

        /// <summary>障害物の追加・削除・破壊・再生・移動など、経路に影響する変化があったときに発火する。</summary>
        public static event Action Changed;

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>Play中は登録済みの全障害物、編集中はシーンから検索した全障害物。</summary>
        public static NavigationObstacle[] FindAll()
        {
            if (!Application.isPlaying)
            {
                return FindObjectsByType<NavigationObstacle>(FindObjectsSortMode.None);
            }

            var result = new NavigationObstacle[Registry.Count];
            Registry.CopyTo(result);
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Registry.Clear();
            Changed = null;
        }

        /// <summary>現在有効なCollider2D（無効化された=破壊中のものは含まない）をbufferに集める。</summary>
        public void CollectActiveColliders(List<Collider2D> buffer)
        {
            GetComponentsInChildren(false, buffer);
            buffer.RemoveAll(c => c == null || !c.enabled);
        }

        private void OnEnable()
        {
            Registry.Add(this);
            NotifyChanged();
        }

        private void OnDisable()
        {
            Registry.Remove(this);
            NotifyChanged();
        }

        private void Update()
        {
            if (isDynamic && transform.hasChanged)
            {
                transform.hasChanged = false;
                NotifyChanged();
            }
        }

        private void OnValidate()
        {
            NotifyChanged();
        }
    }
}
