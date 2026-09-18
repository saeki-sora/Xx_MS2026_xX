using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵が湧く位置のマーカー。シーン上でTransformを動かすだけで「湧く位置」を自由に調整できる。
    /// </summary>
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [Tooltip("EnemyWaveConfigのspawnPointIdと一致させるID。空の場合はこのGameObjectの名前を使う。")]
        public string spawnPointId;

        public string ResolvedId => string.IsNullOrEmpty(spawnPointId) ? name : spawnPointId;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);
        }
    }
}
