using MS2026.Fortress.Cameras;
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

        // ネット対戦のClient(レプリカ)用。Hostから届いた位置へ滑らかに寄せる(届くのは毎秒十数回なのでそのまま置くとカクつく)。
        private const float ReplicaFollowSharpness = 12f;
        private const float ReplicaSnapDistance = 3f;
        private Vector2 _replicaTarget;
        private bool _hasReplicaTarget;
        private bool _snapToNextReplicaTarget = true;

        // 画面内に戻る(全員の画面の外へ出たまま見えなくなるのを防ぐ)。
        private const float ReturnTurnBoost = 3f;
        private const float ReturnArriveDistance = 1f;
        private const int RespawnAttempts = 12;
        private static readonly Collider2D[] OverlapBuffer = new Collider2D[8];
        private float _offScreenSeconds;
        private bool _isReturning;
        private Vector2 _returnTarget;
        private Rect _returnArea;
        private NavigationField _navigationField;

        private SmashBallFloatSettings Settings => _module.settings.floating;

        /// <summary>画面内へ戻る途中か(デバッグ表示用)。</summary>
        public bool IsReturningToScreen => _isReturning;

        /// <summary>今この瞬間、漂っている(位置をHostから配る必要がある)か。</summary>
        public bool IsFloating => _module != null && _obstacle != null && Settings.enabled && !_obstacle.IsDestroyed && isActiveAndEnabled;

        /// <summary>Hostから届いた位置(揺れを含む)を目標にする。レプリカ中のみ使う。</summary>
        public void ApplyReplicatedPosition(Vector2 position)
        {
            _replicaTarget = position;
            _hasReplicaTarget = true;
        }

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
            _hasReplicaTarget = false;
            _snapToNextReplicaTarget = true;
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

            if (_obstacle.IsReplica)
            {
                FollowReplicaTarget();
                return;
            }

            Steer();
            Move();
        }

        private void FollowReplicaTarget()
        {
            if (!_hasReplicaTarget)
            {
                return;
            }

            var current = (Vector2)transform.position;
            var next = _snapToNextReplicaTarget || (_replicaTarget - current).sqrMagnitude > ReplicaSnapDistance * ReplicaSnapDistance
                ? _replicaTarget
                : Vector2.Lerp(current, _replicaTarget, 1f - Mathf.Exp(-ReplicaFollowSharpness * Time.deltaTime));
            _snapToNextReplicaTarget = false;

            _position = next;
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        private void Steer()
        {
            UpdateReturnToScreen();

            if (_isReturning)
            {
                var toTarget = _returnTarget - _position;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    _desiredDirection = toTarget.normalized;
                }
            }
            else if (Time.time >= _nextDirectionChangeTime)
            {
                PickNewDirection();
            }

            var sharpness = Settings.turnSharpness * (_isReturning ? ReturnTurnBoost : 1f);
            var turn = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            _direction = Vector2.Lerp(_direction, _desiredDirection, turn).normalized;
        }

        /// <summary>
        /// 全員の画面に映る範囲の外に<see cref="SmashBallFloatSettings.maxSecondsOffScreen"/>秒以上いたら、
        /// 画面の内側(中央寄りの範囲)のランダムな地点へ向かわせる。内側に入ったら普段の漂い方に戻る。
        /// </summary>
        private void UpdateReturnToScreen()
        {
            var settings = Settings;
            if (!settings.returnToScreen || !TryGetScreenArea(FortressCameraRig.Active, out var screen))
            {
                _isReturning = false;
                _offScreenSeconds = 0f;
                return;
            }

            _returnArea = new Rect(screen.center - screen.size * 0.25f, screen.size * 0.5f);

            if (_isReturning)
            {
                if (_returnArea.Contains(_position) || (_returnTarget - _position).sqrMagnitude <= ReturnArriveDistance * ReturnArriveDistance)
                {
                    _isReturning = false;
                    _offScreenSeconds = 0f;
                    PickNewDirection();
                }

                return;
            }

            if (screen.Contains(_position))
            {
                _offScreenSeconds = 0f;
                return;
            }

            _offScreenSeconds += Time.deltaTime;
            if (_offScreenSeconds >= settings.maxSecondsOffScreen)
            {
                _isReturning = true;
                _returnTarget = RandomPointIn(_returnArea);
            }
        }

        private void Move()
        {
            var settings = Settings;
            var step = settings.driftSpeed * (_isReturning ? settings.returnSpeedMultiplier : 1f) * Time.deltaTime;
            var next = _position + _direction * step;

            next = BounceWithinBounds(next);
            next = AvoidObstacles(next, step);
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

        private Vector2 AvoidObstacles(Vector2 next, float step)
        {
            var settings = Settings;
            var radius = AvoidanceRadius();
            var hit = FindBlockingCollider(next, radius);
            if (hit == null)
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

            if (_isReturning)
            {
                // 戻る途中で壁に当たったら、別の地点を目指し直す(同じ地点を狙い続けると壁に張り付く)。
                _returnTarget = RandomPointIn(_returnArea);
            }
            else
            {
                _desiredDirection = away;
                _nextDirectionChangeTime = Time.time + Random.Range(settings.directionChangeInterval.x, settings.directionChangeInterval.y);
            }

            // 既に重なっている(壁の中に出現した等)なら、止まらずに外へ押し出す。止めると二度と抜け出せない。
            if (FindBlockingCollider(_position, radius) != null)
            {
                return _position + away * step;
            }

            // 今回はぶつかる分だけ進まず、次のフレームから新しい向きで進む。
            return _position;
        }

        // 自分自身(子を含む)とトリガーを除いて、pointの周りに重なる障害物を1つ返す。
        private Collider2D FindBlockingCollider(Vector2 point, float radius)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Settings.obstacleLayerMask);
            var count = Physics2D.OverlapCircle(point, radius, filter, OverlapBuffer);
            for (var i = 0; i < count; i++)
            {
                var hit = OverlapBuffer[i];
                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return hit;
                }
            }

            return null;
        }

        private float AvoidanceRadius()
        {
            var settings = Settings;
            return settings.avoidanceRadius > 0f ? settings.avoidanceRadius : ApproxRadius();
        }

        private static Vector2 RandomPointIn(Rect area)
        {
            return new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
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
            if (obstacle.IsReplica)
            {
                // 再出現位置はHostが乱数で決める。次に届いた位置へ、滑らせずに瞬間移動させる。
                _hasReplicaTarget = false;
                _snapToNextReplicaTarget = true;
                return;
            }

            // 再出現するときは、画面内(画面内に戻るがOFFなら浮遊範囲内)の、障害物と重ならないランダムな位置から漂い直す。
            _position = PickRespawnPosition();
            transform.position = new Vector3(_position.x, _position.y, transform.position.z);
            _isReturning = false;
            _offScreenSeconds = 0f;
            PickNewDirection();
        }

        private Vector2 PickRespawnPosition()
        {
            Rect area;
            if (!Settings.returnToScreen || !TryGetScreenArea(FortressCameraRig.Active, out area))
            {
                var (center, size) = GetWanderBounds();
                area = new Rect(center - size * 0.5f, size);
            }

            var radius = AvoidanceRadius();
            var candidate = area.center;
            for (var i = 0; i < RespawnAttempts; i++)
            {
                candidate = RandomPointIn(area);
                if (FindBlockingCollider(candidate, radius) == null)
                {
                    return candidate;
                }
            }

            // 空いている場所が見つからなければ最後の候補を使う(重なっていても押し出しで抜け出せる)。
            return candidate;
        }

        /// <summary>
        /// 全員の画面に映る範囲(余白を除き、浮遊範囲と重なる部分)。カメラリグ・視点セットが無いときはfalse。
        /// ネット対戦でもHostだけが計算するため、窓の大きさに左右されないよう構図ガイドの基準の縦横比を使う。
        /// </summary>
        public bool TryGetScreenArea(FortressCameraRig rig, out Rect area)
        {
            area = default;
            if (rig == null || rig.preset == null)
            {
                return false;
            }

            var module = _module != null ? _module : GetComponent<SmashBallModule>();
            var settings = module != null ? module.settings.floating : new SmashBallFloatSettings();
            var screen = CameraViewArea.Inset(CameraViewArea.SharedPlayerRect(rig.preset, rig.guides.ReferenceAspect), settings.screenMargin);
            var (center, size) = GetWanderBounds();
            var wander = new Rect(center - size * 0.5f, size);
            if (!CameraViewArea.TryIntersect(screen, wander, out area))
            {
                return false;
            }

            return true;
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
                // 毎フレーム呼ばれるため、見つけた経路フィールドを覚えておく(シーン全体の検索は重い)。
                if (_navigationField == null)
                {
                    _navigationField = FindFirstObjectByType<NavigationField>();
                }

                if (_navigationField != null)
                {
                    return (_navigationField.areaCenter, _navigationField.areaSize);
                }
            }

            return (settings.fallbackAreaCenter, settings.fallbackAreaSize);
        }
    }
}
