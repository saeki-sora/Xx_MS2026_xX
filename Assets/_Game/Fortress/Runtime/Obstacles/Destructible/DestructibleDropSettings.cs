using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>破壊時に落とすものの1行分。</summary>
    [Serializable]
    public sealed class DestructibleDropEntry
    {
        [Tooltip("落とすPrefab（回復アイテム・スコア玉など）。")]
        public GameObject prefab;

        [Tooltip("落とす確率。1で必ず落とす。")]
        [Range(0f, 1f)]
        public float chance = 1f;

        [Min(0)]
        public int minCount = 1;

        [Min(0)]
        public int maxCount = 1;
    }

    /// <summary>破壊時のドロップ設定。</summary>
    [Serializable]
    public sealed class DestructibleDropSettings
    {
        [Tooltip("落とす候補。それぞれ独立に確率判定される。")]
        public List<DestructibleDropEntry> entries = new List<DestructibleDropEntry>();

        [Tooltip("ドロップ位置を中心からばらつかせる半径(ワールド単位)。")]
        [Min(0f)]
        public float scatterRadius = 0.5f;

        [Tooltip("ONなら、最初に破壊されたときだけ落とす（再生後に壊してももう落とさない）。")]
        public bool onlyFirstDestruction;
    }
}
