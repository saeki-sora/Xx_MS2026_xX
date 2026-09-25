using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>壊れた瞬間に、設定された確率・個数でPrefabを落とす。</summary>
    [DisallowMultipleComponent]
    public sealed class DestructibleDropper : MonoBehaviour
    {
        private DestructibleObstacle _obstacle;

        private void OnEnable()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
            if (_obstacle != null)
            {
                _obstacle.Destroyed += OnDestroyed;
            }
        }

        private void OnDisable()
        {
            if (_obstacle != null)
            {
                _obstacle.Destroyed -= OnDestroyed;
            }
        }

        private void OnDestroyed(DestructibleObstacle obstacle)
        {
            var drops = obstacle.settings.drops;
            if (drops.onlyFirstDestruction && obstacle.DestroyCount > 1)
            {
                return;
            }

            foreach (var entry in drops.entries)
            {
                if (entry == null || entry.prefab == null || Random.value > entry.chance)
                {
                    continue;
                }

                var count = Random.Range(entry.minCount, Mathf.Max(entry.minCount, entry.maxCount) + 1);
                for (var i = 0; i < count; i++)
                {
                    var offset = Random.insideUnitCircle * drops.scatterRadius;
                    Instantiate(entry.prefab, transform.position + (Vector3)offset, Quaternion.identity);
                }
            }
        }
    }
}
