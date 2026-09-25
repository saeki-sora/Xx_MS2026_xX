using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// スマッシュボールを画面内で無軌道にふわふわ漂わせる（スマブラのアイテムのような動き）。
    /// ランダムな方向へゆっくり曲がりながら進み、壁や範囲の端に近づくと向きを変え、上下にわずかに揺れる。
    /// 浮遊中は敵の経路の壁として扱わない（アイテムなので敵は素通りする）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SmashBallModule))]
    public sealed class SmashBallFloater : MonoBehaviour
    {
        private SmashBallModule _module;
        private DestructibleObstacle _obstacle;
        private NavigationObstacle _navigation;

        private Vector2 _position;
        private Vector2 _direction = Vector2.right;
        private Vector2 _desiredDirection = Vector2.right;
        private float _nextDirectionChangeTime;
        private float _bobPhase;

        private SmashBallFloatSettings Settings => _module.settings.floating;

        private void OnEnable()
        {
            _module = GetComponent<SmashBallModule>();
            _obstacle = GetComponent<DestructibleObstacle>();
            _navigation = GetComponent<NavigationObstacle>();
            if (_module == null || _obstacle == null)
            {
                return;
            }

            _obstacle.Regenerated += OnRegenerated;

            _position = transform.position;
            _bobPhase = Random.value * Mathf.PI * 2f;
            PickNewDirection();
        }

        private void OnDisable()
        {
            if (_obstacle != null)
            {
                _obstacle.Regenerated -= OnRegenerated;
            }
        }

        private void Update()
        {
            if (_module == null || _obstacle == null || !Settings.enabled || _obstacle.IsDestroyed)
            {
                return;
            }

            // 浮遊中は敵の壁として扱わない（毎フレーム位置が変わるため、経路の再計算コストも避けられる）。
            if (_navigation != null)
            {
                _navigation.enabled = false;
            }

            Steer();
            Move();
        }

        private void Steer()
        {
            if (Time.time >= _nextDirectionChangeTime)
            {
                PickNewDirection();
            }

            var turn = 1f - Mathf.Exp(-Settings.turnSharpness * Time.deltaTime);
            _direction = Vector2.Lerp(_direction, _desiredDirection, turn).normalized;
        }

        private void Move()
        {
            var settings = Settings;
            var next = _position + _direction * (settings.driftSpeed * Time.deltaTime);

            next = BounceWithinBounds(next);
            next = AvoidObstacles(next);
            _position = next;

            _bobPhase += settings.bobFrequency * Mathf.PI * 2f * Time.deltaTime;
            var bob = Mathf.Sin(_bobPhase) * settings.bobAmplitude;

            transform.position = new Vector3(_position.x, _position.y + bob, transform.position.z);
        }

        private Vector2 BounceWithinBounds(Vector2 next)
        {
            var (center, size) = GetWanderBounds();
            var half = size * 0.5f;
            var min = center - half;
            var max = center + half;

            if (next.x < min.x)
            {
                next.x = min.x;
                Reflect(axisX: true);
            }
            else if (next.x > max.x)
            {
                next.x = max.x;
                Reflect(axisX: true);
            }

            if (next.y < min.y)
            {
                next.y = min.y;
                Reflect(axisX: false);
            }
            else if (next.y > max.y)
            {
                next.y = max.y;
                Reflect(axisX: false);
            }

            return next;
        }

        private void Reflect(bool axisX)
        {
            if (axisX)
            {
                _direction.x = -_direction.x;
                _desiredDirection.x = -_desiredDirection.x;
            }
            else
            {
                _direction.y = -_direction.y;
                _desiredDirection.y = -_desiredDirection.y;
            }
        }

        private Vector2 AvoidObstacles(Vector2 next)
        {
            var settings = Settings;
            var radius = settings.avoidanceRadius > 0f ? settings.avoidanceRadius : ApproxRadius();
            var hit = Physics2D.OverlapCircle(next, radius, settings.obstacleLayerMask);
            if (hit == null || hit.isTrigger || hit.gameObject == gameObject)
            {
                return next;
            }

            var away = _position - (Vector2)hit.bounds.center;
            if (away.sqrMagnitude < 1e-4f)
            {
                away = Random.insideUnitCircle;
            }

            away.Normalize();
            _direction = Vector2.Reflect(_direction, away).normalized;
            _desiredDirection = away;
            _nextDirectionChangeTime = Time.time + Random.Range(settings.directionChangeInterval.x, settings.directionChangeInterval.y);

            // 今回はぶつかる分だけ進まず、次のフレームから新しい向きで進む。
            return _position;
        }

        private void PickNewDirection()
        {
            var angle = Random.value * Mathf.PI * 2f;
            _desiredDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var interval = Random.Range(Settings.directionChangeInterval.x, Settings.directionChangeInterval.y);
            _nextDirectionChangeTime = Time.time + Mathf.Max(0.1f, interval);
        }

        private void OnRegenerated(DestructibleObstacle obstacle)
        {
            // 再出現するときは、範囲内のランダムな位置から漂い直す。
            var (center, size) = GetWanderBounds();
            var half = size * 0.5f;
            _position = center + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
            PickNewDirection();
        }

        private float ApproxRadius()
        {
            var scale = transform.lossyScale;
            return Mathf.Max(0.05f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * 0.5f);
        }

        /// <summary>現在、漂う範囲としている中心と大きさ。Sceneでの可視化からも参照する。</summary>
        public (Vector2 center, Vector2 size) GetWanderBounds()
        {
            var module = _module != null ? _module : GetComponent<SmashBallModule>();
            var settings = module != null ? module.settings.floating : new SmashBallFloatSettings();
            if (settings.useNavigationFieldBounds)
            {
                var field = FindFirstObjectByType<NavigationField>();
                if (field != null)
                {
                    return (field.areaCenter, field.areaSize);
                }
            }

            return (settings.fallbackAreaCenter, settings.fallbackAreaSize);
        }
    }
}
