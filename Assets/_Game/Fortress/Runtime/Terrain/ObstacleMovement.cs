using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 障害物の位置を動かす。tuning.movementModeで方式を切り替える:
    /// Stationary=動かない / Wander=プレイヤー側とコア側の基準点の間をランダムに漂う /
    /// Orbit=コア(farAnchor)を軸に一定半径で円形に周回する(オブジェクト自体の自転ではなく公転)。
    /// 座標はワールド空間のVector3をそのまま扱うだけなので、2D(Z=0)でも、3D背景に乗った
    /// 2Dゲームプレイ面(Z≠0)でもそのまま動作する。
    /// </summary>
    [RequireComponent(typeof(DestructibleObstacle))]
    public sealed class ObstacleMovement : MonoBehaviour
    {
        [Tooltip("プレイヤー側(手前)の基準点。Wanderで使う。未設定なら起動時に一番近いLaserTurretを自動採用する。")]
        public Transform nearAnchor;

        [Tooltip("コア側(奥・中心)の基準点。Wander/Orbit両方で使う。未設定なら起動時にシーン内のCoreCrystalControllerを自動採用する。")]
        public Transform farAnchor;

        private DestructibleObstacle _obstacle;
        private ObstacleMovementMode _activeMode = ObstacleMovementMode.Stationary;

        // Wander用
        private Vector3 _wanderTarget;
        private float _wanderPauseRemaining;

        // Orbit用
        private float _orbitAngleDegrees;
        private float _orbitZOffset;

        private void Awake()
        {
            _obstacle = GetComponent<DestructibleObstacle>();
        }

        private void Start()
        {
            ResolveAnchorsIfNeeded();
        }

        private void Update()
        {
            var tuning = _obstacle.tuning;

            if (tuning == null)
            {
                return;
            }

            if (tuning.movementMode != _activeMode)
            {
                EnterMode(tuning.movementMode);
            }

            switch (_activeMode)
            {
                case ObstacleMovementMode.Wander:
                    TickWander(tuning);
                    break;
                case ObstacleMovementMode.Orbit:
                    TickOrbit(tuning);
                    break;
            }
        }

        private void EnterMode(ObstacleMovementMode mode)
        {
            _activeMode = mode;

            switch (mode)
            {
                case ObstacleMovementMode.Wander:
                    _wanderPauseRemaining = 0f;
                    PickNewWanderTarget();
                    break;

                case ObstacleMovementMode.Orbit:
                    if (farAnchor != null)
                    {
                        // 今いる位置から自然に周回を始められるよう、現在の角度・Zオフセットを起点にする。
                        var toObstacle = transform.position - farAnchor.position;
                        _orbitAngleDegrees = Mathf.Atan2(toObstacle.y, toObstacle.x) * Mathf.Rad2Deg;
                        _orbitZOffset = toObstacle.z;
                    }
                    break;
            }
        }

        private void TickWander(DestructibleObstacleTuning tuning)
        {
            if (nearAnchor == null || farAnchor == null)
            {
                return;
            }

            if (_wanderPauseRemaining > 0f)
            {
                _wanderPauseRemaining -= Time.deltaTime;
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, _wanderTarget, tuning.wanderSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, _wanderTarget) <= 0.01f)
            {
                var pauseRange = tuning.wanderPauseSecondsRange;
                _wanderPauseRemaining = Random.Range(Mathf.Min(pauseRange.x, pauseRange.y), Mathf.Max(pauseRange.x, pauseRange.y));
                PickNewWanderTarget();
            }
        }

        private void PickNewWanderTarget()
        {
            if (nearAnchor == null || farAnchor == null)
            {
                return;
            }

            var tuning = _obstacle.tuning;
            var range = tuning != null ? tuning.wanderRange01 : new Vector2(0.2f, 0.7f);
            var t = Random.Range(Mathf.Clamp01(Mathf.Min(range.x, range.y)), Mathf.Clamp01(Mathf.Max(range.x, range.y)));
            _wanderTarget = Vector3.Lerp(nearAnchor.position, farAnchor.position, t);
        }

        private void TickOrbit(DestructibleObstacleTuning tuning)
        {
            if (farAnchor == null)
            {
                return;
            }

            _orbitAngleDegrees += tuning.orbitSpeedDegreesPerSecond * Time.deltaTime;
            var radians = _orbitAngleDegrees * Mathf.Deg2Rad;

            var offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * tuning.orbitRadius;
            offset.z = _orbitZOffset;
            transform.position = farAnchor.position + offset;
        }

        private void ResolveAnchorsIfNeeded()
        {
            if (nearAnchor == null)
            {
                LaserTurret nearest = null;
                var nearestDistance = float.MaxValue;

                foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
                {
                    var distance = Vector3.Distance(turret.transform.position, transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = turret;
                    }
                }

                if (nearest != null)
                {
                    nearAnchor = nearest.transform;
                }
            }

            if (farAnchor == null)
            {
                var core = FindFirstObjectByType<CoreCrystalController>();
                if (core != null)
                {
                    farAnchor = core.transform;
                }
            }
        }
    }
}
