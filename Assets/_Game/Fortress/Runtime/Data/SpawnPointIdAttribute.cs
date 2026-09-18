using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// string フィールドに付与すると、Fortress.Editor側でシーン内のEnemySpawnPointから
    /// IDを選ぶドロップダウンとして描画される（タイプミス防止のため）。
    /// </summary>
    public sealed class SpawnPointIdAttribute : PropertyAttribute
    {
    }
}
