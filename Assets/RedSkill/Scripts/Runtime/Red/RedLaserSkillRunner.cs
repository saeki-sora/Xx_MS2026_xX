using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// 「極太レーザー」を実際に撃つ係（<see cref="RedLaserSkill.CreateRunner"/> が砲台に付ける。シーンに手で置く必要はない）。
    /// 流れ: ため（windup）→ 撃つ（open → 維持）→ 細くなって消える（close）→ スロットに終了を知らせる。
    /// 毎フレーム、レーザーを横に何本かの「筋」に分けて筋ごとに判定を飛ばし、壁・障害物に当たった筋だけそこで止める
    /// （太いレーザーの一部だけが壁に当たったら、その部分だけが止まり、残りは先へ伸びる。<see cref="RedLaserLanes"/>）。
    /// ダメージ（群衆の敵・破壊できる物）は結果を決めるPC（オフライン／Host）だけが与え、
    /// 見た目・効果音・揺れは全PCがそれぞれ出す（群衆の被弾フラッシュはClientでも出る）。
    /// </summary>
    // スキルを別の設定ファイルに差し替えるとき、古い係の削除はフレームの最後なので、同じ砲台に一時的に2つ付くことがある（DisallowMultipleComponent にしない）。
    [DefaultExecutionOrder(10)]
    public sealed class RedLaserSkillRunner : CharacterSkillRunner
    {
        private enum Phase
        {
            Idle,
            Windup,
            Firing,
            Closing
        }

        private const int CastBufferSize = 16;
        private const int PathBreaks = 5;

        private static readonly RaycastHit2D[] CastBuffer = new RaycastHit2D[CastBufferSize];

        [SerializeField]
        private RedLaserSkill definition;

        private readonly HashSet<ILaserTarget> _damagedThisFrame = new HashSet<ILaserTarget>();
        private readonly RedLaserLanes _lanes = new RedLaserLanes();
        private readonly List<TargetHit> _targetHits = new List<TargetHit>(32);
        private readonly float[] _pathBreaks = new float[PathBreaks];
        private readonly List<float> _medianBuffer = new List<float>(16);

        private RedLaserVisuals _visuals;
        private Phase _phase = Phase.Idle;
        private float _phaseTime;
        private float _length;
        private float _impactShakeTimer;
        private float _firingShakeTimer;
        private bool _hadImpact;
        private bool _rotationOverridden;
        private float _savedRotationScale;
        private Handle<VfxMarker> _loopVfx = Handle<VfxMarker>.Invalid;

        /// <summary>使っている設定ファイル。</summary>
        public RedLaserSkill Definition => definition;

        public override bool IsActive => _phase != Phase.Idle;

        /// <summary>今のレーザーの長さ（一番長い筋、メートル）。撃っていないときは 0。</summary>
        public float CurrentLength => _length;

        /// <summary>今、どこかの筋が壁・障害物に当たって止まっているか。</summary>
        public bool HasImpact => _hadImpact;

        /// <summary>レーザーの筋のどれかに当たった、ダメージを与えられる物1件。</summary>
        private struct TargetHit
        {
            public ILaserTarget Target;
            public float Distance;
            public int Lane;
        }

        internal void Configure(RedLaserSkill skill)
        {
            definition = skill;
        }

        protected override void OnBound()
        {
            if (_visuals == null && definition != null && Turret != null)
            {
                _visuals = new RedLaserVisuals(definition, Turret);
            }
        }

        public override void SetReadyVisual(bool ready)
        {
            if (_visuals == null)
            {
                return;
            }

            _visuals.SetCharging(ready);
            if (ready && definition != null && Turret != null)
            {
                FortressEffectPlayer.PlayOnce(definition.sounds.onReady, Turret.transform.position, Turret.transform.rotation, Turret.playerIndex);
            }
        }

        public override void Activate()
        {
            if (definition == null || Turret == null)
            {
                NotifyFinished();
                return;
            }

            OnBound();
            OverrideRotation(true);
            _length = 0f;
            _lanes.ResetLengths();
            _hadImpact = false;
            _impactShakeTimer = 0f;
            SetPhase(definition.timing.windupSeconds > 0f ? Phase.Windup : Phase.Firing);
            if (_phase == Phase.Windup)
            {
                FortressEffectPlayer.PlayOnce(definition.sounds.onWindup, _visuals.FirePoint, Turret.transform.rotation, Turret.playerIndex);
                _visuals.BeginWindup();
            }
            else
            {
                BeginFire();
            }
        }

        public override void Cancel()
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            StopEverything();
        }

        private void OnEnable()
        {
            _visuals?.SetVisible(true);
        }

        private void OnDisable()
        {
            if (_phase != Phase.Idle)
            {
                StopEverything();
            }

            // 見た目はシーンの一番上に置いてあるので、砲台が消えたら一緒に隠す。
            _visuals?.SetVisible(false);
        }

        private void OnDestroy()
        {
            FortressEffectPlayer.Stop(ref _loopVfx);
            _visuals?.Dispose();
            _visuals = null;
        }

        private void Update()
        {
            if (_visuals == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            _visuals.Tick(dt);

            switch (_phase)
            {
                case Phase.Idle:
                    return;
                case Phase.Windup:
                    _phaseTime += dt;
                    _visuals.UpdateWindup(Mathf.Clamp01(_phaseTime / Mathf.Max(0.0001f, definition.timing.windupSeconds)));
                    if (_phaseTime >= definition.timing.windupSeconds)
                    {
                        BeginFire();
                    }

                    return;
                case Phase.Firing:
                    _phaseTime += dt;
                    if (_phaseTime >= definition.timing.fireSeconds
                        || (definition.timing.endWhenGripReleased && !Turret.IsFiring))
                    {
                        SetPhase(Phase.Closing);
                    }

                    break;
                case Phase.Closing:
                    _phaseTime += dt;
                    if (_phaseTime >= definition.timing.closeSeconds)
                    {
                        Finish();
                        return;
                    }

                    break;
            }

            TickBeam(dt);
        }

        /// <summary>今の太さの倍率（開き・脈動・閉じ）。</summary>
        private float CurrentWidthScale()
        {
            var timing = definition.timing;
            if (_phase == Phase.Closing)
            {
                return Mathf.Max(0f, Evaluate(timing.closeCurve, _phaseTime / timing.closeSeconds, 1f - _phaseTime / timing.closeSeconds));
            }

            var open = _phaseTime < timing.openSeconds
                ? Evaluate(timing.openCurve, _phaseTime / timing.openSeconds, _phaseTime / timing.openSeconds)
                : 1f;
            var shape = definition.shape;
            var pulse = 1f + Mathf.Sin(_phaseTime * shape.pulseFrequency * Mathf.PI * 2f) * shape.pulseAmount;
            return Mathf.Max(0f, open * pulse);
        }

        private void TickBeam(float dt)
        {
            // レーザーは歯磨き粉の口から出る（歯磨き粉を出さない設定なら砲台の発射口）。
            var origin3 = _visuals.FirePoint;
            var origin = (Vector2)origin3;
            var direction = Turret.AimDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            var perpendicular = new Vector2(-direction.y, direction.x);
            var widthScale = CurrentWidthScale();

            CastLanes(origin, direction, perpendicular, widthScale, dt);
            _length = _lanes.MaxLength;

            if (FortressNet.HasSimulationAuthority || IsSwarmReplica())
            {
                QueueSwarmDamage(origin, direction, perpendicular, widthScale);
            }

            if (FortressNet.HasSimulationAuthority)
            {
                ApplyObstacleDamage(widthScale, dt);
            }

            UpdateImpactFeedback(direction, dt);
            _visuals.UpdateBeam(origin3, direction, widthScale, _lanes);
        }

        private static bool IsSwarmReplica()
        {
            var swarm = SwarmSystem.Current;
            return swarm != null && swarm.IsReplica;
        }

        /// <summary>横の位置 fraction（-1〜+1、見た目の太さに対する割合）・根元からの距離 distance の場所。</summary>
        private Vector2 LanePoint(Vector2 origin, Vector2 direction, Vector2 perpendicular, float fraction, float distance, float widthScale)
        {
            var half = definition.WidthAt(distance, widthScale) * definition.shape.visualWidthScale * 0.5f;
            return origin + direction * distance + perpendicular * (fraction * half);
        }

        /// <summary>当たり判定の太さが、見た目の太さの何割か（筋の横の位置と比べる）。</summary>
        private float HitFractionLimit => definition.damage.hitWidthScale / Mathf.Max(0.01f, definition.shape.visualWidthScale);

        /// <summary>
        /// 筋ごとに、根元から先へ判定を飛ばして「止める物」を探す。筋は広がる形に沿って折れ線で進む。
        /// 群衆以外のダメージを与えられる物（破壊できる物・Actorの敵）は、通り道で見つけた分を覚えておく（ダメージは後でまとめて）。
        /// Actorの敵は止めずに貫通する。破壊できる物は「破壊できる物でも止まる」のときだけ止める。壁（それ以外）は必ず止める。
        /// </summary>
        private void CastLanes(Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale, float dt)
        {
            var settings = definition.damage;
            var range = definition.shape.range;
            _lanes.Resize(Mathf.Clamp(settings.laneCount, 4, 48) + 1);
            _targetHits.Clear();

            // 広がっている間ほど細かく区切る（その間は筋が斜めに曲がっていくため）。
            var spread = Mathf.Clamp(definition.shape.spreadDistance, 0.1f, range);
            _pathBreaks[0] = 0f;
            _pathBreaks[1] = spread * 0.25f;
            _pathBreaks[2] = spread * 0.5f;
            _pathBreaks[3] = spread;
            _pathBreaks[4] = range;

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(settings.blockerLayers);
            var grow = definition.shape.extendSpeed * dt;
            var anyBlocked = false;
            var maxLength = 0f;

            for (var lane = 0; lane < _lanes.Count; lane++)
            {
                var fraction = _lanes.Fraction[lane];
                var found = CastLane(origin, direction, perpendicular, widthScale, filter, fraction, lane, out var hitDistance, out var normal);

                // 先端は一定の速さで伸びる。壁が手前に来たら（回転・動く障害物で）すぐ縮める。
                var length = Mathf.Min(_lanes.Length[lane] + grow, hitDistance);
                var blocked = found && length >= hitDistance - 0.01f;
                _lanes.HitDistance[lane] = hitDistance;
                _lanes.Length[lane] = length;
                _lanes.Blocked[lane] = blocked;
                _lanes.HitPoint[lane] = LanePoint(origin, direction, perpendicular, fraction, length, widthScale);
                _lanes.HitNormal[lane] = normal;
                anyBlocked |= blocked;
                maxLength = Mathf.Max(maxLength, length);
            }

            _lanes.AnyBlocked = anyBlocked;
            _lanes.MaxLength = maxLength;
            BuildStrips(origin, direction, perpendicular, widthScale, filter);
        }

        /// <summary>
        /// 1本の筋の判定。根元から先へ、広がる形に沿った折れ線で進み、最初の「止める物」までの距離を返す（無ければ届く距離）。
        /// <paramref name="lane"/> が 0 以上なら、通り道で見つけたダメージを与えられる物を覚える（-1 なら覚えない＝かどを探すとき）。
        /// Actorの敵は止めずに貫通する。破壊できる物は「破壊できる物でも止まる」のときだけ止める。壁（それ以外）は必ず止める。
        /// </summary>
        private bool CastLane(Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale, ContactFilter2D filter,
            float fraction, int lane, out float hitDistance, out Vector2 normal)
        {
            var stopAtDestructibles = definition.damage.stopAtDestructibles;
            hitDistance = definition.shape.range;
            normal = -direction;
            for (var segment = 0; segment < PathBreaks - 1; segment++)
            {
                var d0 = _pathBreaks[segment];
                var d1 = _pathBreaks[segment + 1];
                if (d1 - d0 < 0.001f)
                {
                    continue;
                }

                var a = LanePoint(origin, direction, perpendicular, fraction, d0, widthScale);
                var b = LanePoint(origin, direction, perpendicular, fraction, d1, widthScale);
                var count = Physics2D.Linecast(a, b, filter, CastBuffer);
                SortByFraction(count);
                for (var i = 0; i < count; i++)
                {
                    var hit = CastBuffer[i];
                    if (hit.collider == null || IsOwnCollider(hit.collider))
                    {
                        continue;
                    }

                    var distance = d0 + hit.fraction * (d1 - d0);
                    var target = hit.collider.GetComponentInParent<ILaserTarget>();
                    if (target != null)
                    {
                        if (lane >= 0)
                        {
                            _targetHits.Add(new TargetHit { Target = target, Distance = distance, Lane = lane });
                        }

                        var destructible = hit.collider.GetComponentInParent<DestructibleObstacle>() != null;
                        if (!destructible || !stopAtDestructibles)
                        {
                            continue;
                        }
                    }

                    hitDistance = distance;
                    normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -direction;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 見た目用の帯を作る。となりの筋どうしの止まる距離が大きく違うとき（片方だけ障害物に当たっている＝障害物のかどがその間にある）は、
        /// 間で何回か判定を飛ばして（二分探索）かどの位置を見つけ、そこで帯を2つに分ける。
        /// </summary>
        private void BuildStrips(Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale, ContactFilter2D filter)
        {
            const float EdgeGap = 0.75f;
            const int SearchSteps = 4;
            _lanes.ClearStrips();
            for (var lane = 0; lane < _lanes.Count - 1; lane++)
            {
                var left = _lanes.Fraction[lane];
                var right = _lanes.Fraction[lane + 1];
                var leftLength = _lanes.Length[lane];
                var rightLength = _lanes.Length[lane + 1];
                var leftBlocked = _lanes.Blocked[lane];
                var rightBlocked = _lanes.Blocked[lane + 1];
                var leftHit = _lanes.HitDistance[lane];
                var rightHit = _lanes.HitDistance[lane + 1];

                if (Mathf.Abs(leftHit - rightHit) < EdgeGap)
                {
                    _lanes.AddStrip(left, right, leftLength, rightLength, leftBlocked, rightBlocked);
                    continue;
                }

                // かどを探す: 左の筋と同じくらいの距離で止まる所までは左側。
                var low = left;
                var high = right;
                for (var step = 0; step < SearchSteps; step++)
                {
                    var middle = (low + high) * 0.5f;
                    CastLane(origin, direction, perpendicular, widthScale, filter, middle, -1, out var middleHit, out _);
                    if (Mathf.Abs(middleHit - leftHit) < Mathf.Abs(middleHit - rightHit))
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                var edge = (low + high) * 0.5f;
                _lanes.AddStrip(left, edge, leftLength, leftLength, leftBlocked, leftBlocked);
                _lanes.AddStrip(edge, right, rightLength, rightLength, rightBlocked, rightBlocked);
            }
        }

        /// <summary>判定の結果を、手前（fraction が小さい）順に並べる（数が少ないので挿入ソート）。</summary>
        private static void SortByFraction(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var item = CastBuffer[i];
                var j = i - 1;
                while (j >= 0 && CastBuffer[j].fraction > item.fraction)
                {
                    CastBuffer[j + 1] = CastBuffer[j];
                    j--;
                }

                CastBuffer[j + 1] = item;
            }
        }

        private bool IsOwnCollider(Collider2D collider)
        {
            return collider.transform == Turret.transform || collider.transform.IsChildOf(Turret.transform);
        }

        /// <summary>
        /// 群衆の敵へのダメージ。群衆側の判定は「丸い端の棒」なので、当たり判定の太さを横に何列か・長さ方向に何本かに分けて登録する。
        /// 列ごとの長さは、その列に入る筋の長さの真ん中の値（一部の筋だけが壁で止まっても、列全体は止めすぎない）。
        /// Clientでは群衆側がダメージを0にして、被弾フラッシュだけ出す。
        /// </summary>
        private void QueueSwarmDamage(Vector2 origin, Vector2 direction, Vector2 perpendicular, float widthScale)
        {
            var swarm = SwarmSystem.Current;
            if (swarm == null || _length <= 0.01f)
            {
                return;
            }

            var settings = definition.damage;
            var dps = settings.swarmDamagePerSecond * Mathf.Clamp01(widthScale);
            var columns = Mathf.Clamp(settings.hitColumns, 1, 6);
            var segments = Mathf.Clamp(settings.hitSegments, 1, 6);
            var limit = HitFractionLimit;
            var visualHalf = definition.shape.visualWidthScale * 0.5f;
            for (var column = 0; column < columns; column++)
            {
                var fa = -limit + 2f * limit * column / columns;
                var fb = fa + 2f * limit / columns;
                var fm = (fa + fb) * 0.5f;
                var columnLength = MedianLength(fa, fb, fm);
                if (columnLength <= 0.01f)
                {
                    continue;
                }

                for (var i = 0; i < segments; i++)
                {
                    var d0 = columnLength * i / segments;
                    var d1 = columnLength * (i + 1) / segments;
                    var a = LanePoint(origin, direction, perpendicular, fm, d0, widthScale);
                    var b = LanePoint(origin, direction, perpendicular, fm, d1, widthScale);
                    var halfWidth = (fb - fa) * 0.5f * definition.WidthAt(d1, widthScale) * visualHalf;
                    swarm.QueueBeam(a, b, halfWidth, dps, 0, Turret.playerIndex);
                }
            }
        }

        private float MedianLength(float fromFraction, float toFraction, float fallbackFraction)
        {
            _medianBuffer.Clear();
            for (var lane = 0; lane < _lanes.Count; lane++)
            {
                var f = _lanes.Fraction[lane];
                if (f >= fromFraction - 0.0001f && f <= toFraction + 0.0001f)
                {
                    _medianBuffer.Add(_lanes.Length[lane]);
                }
            }

            if (_medianBuffer.Count == 0)
            {
                return _lanes.LengthAt(fallbackFraction);
            }

            _medianBuffer.Sort();
            return _medianBuffer[_medianBuffer.Count / 2];
        }

        /// <summary>
        /// 破壊できる物・Actorの敵へのダメージ。当たり判定の太さの中の筋が、今フレーム届いている所までで見つけた物に、1フレーム1回ずつ与える
        /// （壁の向こうの物は、その筋の判定が壁で止まっているので見つからない）。
        /// </summary>
        private void ApplyObstacleDamage(float widthScale, float dt)
        {
            var settings = definition.damage;
            if (settings.obstacleDamagePerSecond <= 0f || _targetHits.Count == 0)
            {
                return;
            }

            // 破壊できる物はチューニングの「障害物ダメージ倍率」を自分で掛ける。掛けたくないときはチューニングを渡さない（倍率1になる）。
            var tuning = settings.useTurretObstacleMultiplier ? Turret.tuning : null;
            var amount = settings.obstacleDamagePerSecond * Mathf.Clamp01(widthScale) * dt;
            if (amount <= 0f)
            {
                return;
            }

            _damagedThisFrame.Clear();
            var limit = HitFractionLimit + 0.001f;
            for (var i = 0; i < _targetHits.Count; i++)
            {
                var hit = _targetHits[i];
                if (Mathf.Abs(_lanes.Fraction[hit.Lane]) > limit || hit.Distance > _lanes.Length[hit.Lane] + 0.15f)
                {
                    continue;
                }

                if (!_damagedThisFrame.Add(hit.Target))
                {
                    continue;
                }

                if (hit.Target is IAttributedLaserTarget attributed)
                {
                    attributed.ApplyLaserDamage(amount, tuning, Turret);
                }
                else
                {
                    hit.Target.ApplyLaserDamage(amount, tuning);
                }
            }
        }

        private void UpdateImpactFeedback(Vector2 direction, float dt)
        {
            var impact = _lanes.AnyBlocked;
            if (impact && !_hadImpact)
            {
                // 当たっている筋の真ん中あたりで鳴らす。
                var point = Vector2.zero;
                var normal = Vector2.zero;
                var count = 0;
                for (var lane = 0; lane < _lanes.Count; lane++)
                {
                    if (_lanes.Blocked[lane])
                    {
                        point += _lanes.HitPoint[lane];
                        normal += _lanes.HitNormal[lane];
                        count++;
                    }
                }

                point /= Mathf.Max(1, count);
                var rotation = FortressEffectPlayer.RotationFacing(normal.sqrMagnitude > 0.0001f ? normal.normalized : -direction);
                FortressEffectPlayer.PlayOnce(definition.sounds.onImpact, new Vector3(point.x, point.y, Turret.MuzzlePosition.z), rotation, Turret.playerIndex);
                _impactShakeTimer = 0f;
            }

            _hadImpact = impact;

            // 撃っている間はずっと、細かく弱く揺らす（ゴゴゴ…）。
            if (_phase == Phase.Firing)
            {
                _firingShakeTimer -= dt;
                if (_firingShakeTimer <= 0f)
                {
                    var shakeSettings = definition.cameraShake;
                    _firingShakeTimer = shakeSettings.whileFiringInterval;
                    Shake(shakeSettings.whileFiring, shakeSettings.whileFiringStrength);
                }
            }

            if (!impact || _phase != Phase.Firing)
            {
                return;
            }

            _impactShakeTimer -= dt;
            if (_impactShakeTimer <= 0f)
            {
                var shake = definition.cameraShake;
                _impactShakeTimer = shake.impactInterval;
                Shake(shake.impact, shake.impactStrength);
            }
        }

        private void BeginFire()
        {
            SetPhase(Phase.Firing);
            var muzzle = Turret.muzzle != null ? Turret.muzzle : Turret.transform;
            FortressEffectPlayer.PlayOnce(definition.sounds.onFire, _visuals.FirePoint, Turret.transform.rotation, Turret.playerIndex);
            FortressEffectPlayer.Stop(ref _loopVfx);
            _loopVfx = FortressEffectPlayer.StartFollowing(definition.sounds.whileFiring, muzzle, Turret.playerIndex);
            var shake = definition.cameraShake;
            Shake(shake.fire, shake.fireStrength);
            _visuals.BeginFire();
        }

        private void Finish()
        {
            FortressEffectPlayer.PlayOnce(definition.sounds.onEnd, _visuals.FirePoint, Turret.transform.rotation, Turret.playerIndex);
            StopEverything();
            NotifyFinished();
        }

        private void StopEverything()
        {
            SetPhase(Phase.Idle);
            FortressEffectPlayer.Stop(ref _loopVfx);
            OverrideRotation(false);
            _length = 0f;
            _lanes.ResetLengths();
            _hadImpact = false;
            _visuals?.EndFire();
        }

        private void SetPhase(Phase next)
        {
            _phase = next;
            _phaseTime = 0f;
        }

        /// <summary>スキル中は砲台の回る速さを落とす（砲台のコードには手を入れず、公開されている倍率を一時的に変える）。</summary>
        private void OverrideRotation(bool on)
        {
            if (Turret == null)
            {
                return;
            }

            if (on)
            {
                if (!_rotationOverridden)
                {
                    _savedRotationScale = Turret.rotationSpeedScale;
                    _rotationOverridden = true;
                }

                Turret.rotationSpeedScale = _savedRotationScale * definition.timing.rotationSpeedScale;
            }
            else if (_rotationOverridden)
            {
                Turret.rotationSpeedScale = _savedRotationScale;
                _rotationOverridden = false;
            }
        }

        private void Shake(SimpleShakeSettings settings, float strength)
        {
            if (settings == null || strength <= 0f)
            {
                return;
            }

            var rig = FortressCameraRig.Active;
            if (rig == null)
            {
                return;
            }

            if (definition.cameraShake.ownerViewOnly && rig.CurrentViewer != Turret.playerIndex)
            {
                return;
            }

            rig.AddModifier(new SimpleShakeModifier(settings, strength));
        }

        private static float Evaluate(AnimationCurve curve, float t, float fallback)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(Mathf.Clamp01(t)) : fallback;
        }
    }
}
