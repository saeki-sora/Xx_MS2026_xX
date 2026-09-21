using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 負荷確認用の連続スポナー。指定した敵を、秒間N体のペースで湧き位置（または指定位置）から出し続ける。
    /// 「何体まで60FPSを保てるか」を測るために使う。要塞デザイナーの「群衆」タブから操作できる。
    /// </summary>
    public sealed class SwarmStressSpawner : MonoBehaviour
    {
        public EnemyTypeDefinition enemyType;

        [Tooltip("秒間に湧かせる敵の数。")]
        [Min(0f)]
        public float perSecond = 200f;

        [Tooltip("ONなら、シーン内の全てのEnemySpawnPointから均等に湧かせる。OFFならこのGameObjectの位置。")]
        public bool useSpawnPoints = true;

        public bool active;

        private float _accumulator;
        private EnemySpawnPoint[] _points;
        private float _refreshTimer;

        private void Update()
        {
            var swarm = SwarmSystem.Current;
            if (!active || swarm == null || enemyType == null)
            {
                return;
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_points == null || _refreshTimer <= 0f)
            {
                _refreshTimer = 1f;
                _points = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
            }

            _accumulator += perSecond * Time.deltaTime;
            var count = Mathf.FloorToInt(_accumulator);
            _accumulator -= count;

            for (var i = 0; i < count; i++)
            {
                var origin = (Vector2)transform.position;
                if (useSpawnPoints && _points.Length > 0)
                {
                    origin = _points[Random.Range(0, _points.Length)].transform.position;
                }

                if (!swarm.Spawn(enemyType, origin))
                {
                    return;
                }
            }
        }
    }
}
