using System;
using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 「うがい薬の渦」を実際に投げる係（<see cref="BlueVortexSkill.CreateRunner"/> が砲台に付ける。シーンに手で置く必要はない）。
    /// 流れ: 使える状態でボトルを持つ → 次の攻撃（握った瞬間）で振りかぶる（windup）→ 投げる（flight）
    /// → 落ちた場所に <see cref="BlueVortexArea"/>（渦）を出す。渦はその後も自分で続いて消える。
    /// 投げ方（<see cref="BlueVortexSkill.multi"/>）:
    /// ・1本だけ（元の動き）／まとめて一気に: 投げたボトルが全部落ちたらスロットに終了を知らせる。
    /// ・1本ずつ順番に: 1回の溜めで「ボトルの数」だけ投げられる。投げた瞬間にスロットに終了を知らせ、まだ残っていれば
    ///   すぐ「使える」に戻す（<see cref="PlayerSkillSlot.GrantCharge"/>）。握ったままなら一度離して、次の攻撃で次の1本を投げる。
    ///   飛んでいるボトルは別に動くので、残りのボトルは持ったまま。
    /// 落ちる場所は投げた瞬間の砲台の向きと <see cref="BlueVortexSkill.target"/> の設定で決まる（<see cref="PredictLandings"/>）。
    /// 「握って飛距離を決める」（<see cref="BlueVortexSkill.gripDistance"/>）を使うときは、振りかぶりの代わりに握っている間構えて（Aiming）
    /// 距離を伸ばし、離した瞬間に投げる。
    /// ネット対戦: 全PCが自分で同じように動かす（見た目・効果音・揺れは各PC、敵への効果は <see cref="BlueVortexField"/> が結果を決めるPCだけでかける）。
    /// </summary>
    // スキルを別の設定ファイルに差し替えるとき、古い係の削除はフレームの最後なので、同じ砲台に一時的に2つ付くことがある（DisallowMultipleComponent にしない）。
    [DefaultExecutionOrder(10)]
    public sealed class BlueVortexSkillRunner : CharacterSkillRunner
    {
        private enum Phase
        {
            Idle,
            Windup,

            /// <summary>握って飛距離を決めている（「握って飛距離を決める」を使うとき。離したら投げる）。</summary>
            Aiming,

            /// <summary>投げたボトルが落ちるのを待っている（1本だけ／まとめて一気に のとき。落ちたら終了）。</summary>
            Flight
        }

        /// <summary>飛んでいるボトル1本。</summary>
        private struct InAir
        {
            public int FlightId;
            public Vector3 Target;
            public bool FirstOfThrow;
            public bool FinishesSkill;
            public Handle<VfxMarker> Loop;
        }

        [SerializeField]
        private BlueVortexSkill definition;

        private readonly List<Vector3> _targets = new List<Vector3>(BlueMultiThrow.MaxCount);
        private readonly List<Vector3> _predicted = new List<Vector3>(BlueMultiThrow.MaxCount);
        private readonly List<int> _flightIds = new List<int>(BlueMultiThrow.MaxCount);
        private readonly List<InAir> _inAir = new List<InAir>(BlueMultiThrow.MaxCount * 2);

        private BlueThrowVisuals _visuals;
        private Phase _phase = Phase.Idle;
        private float _phaseTime;
        private int _awaitingLandings;
        private int _bottlesLeft;
        private float _gripProgress;
        private float _gripReference;
        private bool _rearming;
        private bool _rotationOverridden;
        private float _savedRotationScale;

        /// <summary>どこかの砲台が渦を出したときに呼ばれる（だれが・どの渦）。キャラの演出・UI などに使う。</summary>
        public static event Action<BlueVortexSkillRunner, BlueVortexArea> AnyVortexSpawned;

        /// <summary>使っている設定ファイル。</summary>
        public BlueVortexSkill Definition => definition;

        public override bool IsActive => _phase != Phase.Idle;

        /// <summary>この砲台で最後に出した渦（もう消えていれば null）。</summary>
        public BlueVortexArea LastArea { get; private set; }

        /// <summary>今の溜めで、あと何回投げられるか（「1本ずつ順番に」以外は、使える間 1）。</summary>
        public int BottlesLeft => _bottlesLeft;

        /// <summary>最後に投げたときの落ちる場所（真ん中の1本）。</summary>
        public Vector3 CurrentTarget => _targets.Count > 0 ? _targets[(_targets.Count - 1) / 2] : transform.position;

        /// <summary>最後に投げたときの、全部のボトルの落ちる場所（左から右の順）。</summary>
        public IReadOnlyList<Vector3> CurrentTargets => _targets;

        /// <summary>握って飛距離を決めている最中か。</summary>
        public bool IsAiming => _phase == Phase.Aiming;

        /// <summary>
        /// 今投げたときの距離の、近い→遠いの割合（0〜1。カーブをかける前）。
        /// 「握って飛距離を決める」を使わないときは 0。ゲージなどの表示に使う。
        /// </summary>
        public float GripDistance01 => GripMode ? definition.gripDistance.Ratio01(_gripProgress) : 0f;

        /// <summary>今投げたときの、真ん中の1本の落ちる距離（m）。</summary>
        public float ThrowDistance
        {
            get
            {
                if (definition == null)
                {
                    return 0f;
                }

                return GripMode ? definition.gripDistance.Distance(GripDistance01) : definition.target.distance;
            }
        }

        private bool GripMode => definition != null && definition.gripDistance != null && definition.gripDistance.Enabled;

        internal void Configure(BlueVortexSkill skill)
        {
            definition = skill;
        }

        /// <summary>今投げたらボトルが落ちる場所（まとめて投げるときは真ん中の1本）。</summary>
        public Vector3 PredictLanding()
        {
            var landings = PredictLandings();
            return landings[(landings.Count - 1) / 2];
        }

        /// <summary>次の攻撃で投げる全部のボトルが落ちる場所（左から右の順）。握って飛距離を決めている間は、今の距離での場所。</summary>
        public IReadOnlyList<Vector3> PredictLandings()
        {
            if (definition != null && Turret != null)
            {
                definition.PredictLandings(Turret, _predicted, ThrowDistance);
            }
            else
            {
                _predicted.Clear();
                _predicted.Add(transform.position);
            }

            return _predicted;
        }

        protected override void OnBound()
        {
            if (_visuals == null && definition != null && Turret != null)
            {
                _visuals = new BlueThrowVisuals(definition, Turret);
            }
        }

        public override void SetReadyVisual(bool ready)
        {
            if (_visuals == null)
            {
                return;
            }

            if (ready && !_rearming)
            {
                // 新しく溜まった（スマッシュボールなど）。ボトルを全部持ち直す。
                _bottlesLeft = definition != null ? definition.ThrowsPerCharge : 1;
                if (definition != null && Turret != null)
                {
                    FortressEffectPlayer.PlayOnce(definition.sounds.onReady, Turret.transform.position, Turret.transform.rotation, Turret.playerIndex);
                }
            }

            _visuals.SetReady(ready, !_rearming);
        }

        public override void Activate()
        {
            if (definition == null || Turret == null)
            {
                NotifyFinished();
                return;
            }

            OnBound();
            if (_bottlesLeft <= 0)
            {
                _bottlesLeft = 1;
            }

            OverrideRotation(true);
            _gripProgress = 0f;
            _gripReference = Turret.CurrentThickness01;
            if (GripMode)
            {
                // 握っている間ずっと振りかぶって構え、離したら投げる（UpdateAiming）。
                SetPhase(Phase.Aiming);
                _visuals.BeginWindup();
                FortressEffectPlayer.PlayOnce(definition.sounds.onWindup, _visuals.BottlePosition, Turret.transform.rotation, Turret.playerIndex);
            }
            else if (definition.timing.windupSeconds > 0f)
            {
                SetPhase(Phase.Windup);
                _visuals.BeginWindup();
                FortressEffectPlayer.PlayOnce(definition.sounds.onWindup, _visuals.BottlePosition, Turret.transform.rotation, Turret.playerIndex);
            }
            else
            {
                Throw();
            }
        }

        public override void Cancel()
        {
            if (_phase == Phase.Idle && _inAir.Count == 0)
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
            if (_phase != Phase.Idle || _inAir.Count > 0)
            {
                StopEverything();
            }

            // 見た目はシーンの一番上に置いてあるので、砲台が消えたら一緒に隠す。
            _visuals?.SetVisible(false);
        }

        private void OnDestroy()
        {
            StopLoops();
            _visuals?.Dispose();
            _visuals = null;
        }

        private void Update()
        {
            if (_visuals == null || definition == null || Turret == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            _visuals.Tick(dt, PredictLandings(), HeldCount);

            if (_phase == Phase.Windup)
            {
                _phaseTime += dt;
                _visuals.UpdateWindup(Mathf.Clamp01(_phaseTime / Mathf.Max(0.0001f, definition.timing.windupSeconds)));
                if (_phaseTime >= definition.timing.windupSeconds)
                {
                    Throw();
                }
            }
            else if (_phase == Phase.Aiming)
            {
                UpdateAiming(dt);
            }

            LandDueBottles();
            if (_phase == Phase.Flight && _awaitingLandings <= 0)
            {
                SetPhase(Phase.Idle);
                NotifyFinished();
            }
        }

        /// <summary>持っているボトルの数（見た目用）。</summary>
        private int HeldCount
        {
            get
            {
                var multi = definition.multi;
                switch (multi.mode)
                {
                    case BlueThrowMode.AllAtOnce:
                        return definition.ThrowCount;
                    case BlueThrowMode.OneByOne:
                        return Mathf.Clamp(_bottlesLeft, 1, BlueMultiThrow.MaxCount);
                    default:
                        return 1;
                }
            }
        }

        /// <summary>
        /// 握って飛距離を決めている間。握っている間は距離を伸ばし（最大で止まる／往復）、
        /// 離した（オーバーヒートも含む）・ゆるめた（「ゆるめたら投げる」）・「自動で投げる」の時間がたった・設定で「使わない」に変えたら投げる。
        /// 握力は離してもすぐ 0 にならず、砲台が「離した」になるまで少しかかるので、ゆるめ始めた瞬間に距離を止め（往復で
        /// 離した後に動いてしまわないように）、ある程度下がったら完全に離すのを待たずに投げる。
        /// 握る強さは、ネット対戦でも全PCに届いている「レーザーの太さ」（握る強さで決まる 0〜1）を使うので、全PCで同じように投げる
        /// （届く間隔ぶんのずれはある）。
        /// </summary>
        private void UpdateAiming(float dt)
        {
            var grip = definition.gripDistance;
            _phaseTime += dt;

            // 「さっきまでの強さ」: 握力の一番強いところを覚えて、ゆっくり下げていく。
            var strength = Turret.CurrentThickness01;
            _gripReference = Mathf.Max(strength, _gripReference - BlueGripDistance.ReferenceDecayPerSecond * dt);
            var drop = _gripReference - strength;

            var released = !Turret.IsFiring || (grip.releaseDrop > 0f && drop >= grip.releaseDrop);
            var timedOut = grip.autoThrowSeconds > 0f && _phaseTime >= grip.autoThrowSeconds;
            if (released || timedOut || !grip.Enabled)
            {
                Throw();
                return;
            }

            var loosening = grip.freezeWhileLoosening && drop > BlueGripDistance.LoosenMargin;
            if (!loosening)
            {
                _gripProgress = grip.Wrap(_gripProgress + grip.Advance(strength, dt));
            }

            _visuals.UpdateWindup(GripDistance01);
        }

        private void Throw()
        {
            // 落ちる場所は投げた瞬間の向き（と、握って決めた距離）で決める。
            _targets.Clear();
            _targets.AddRange(PredictLandings());
            _gripProgress = 0f;
            OverrideRotation(false);

            var oneByOne = definition.multi.mode == BlueThrowMode.OneByOne;
            _bottlesLeft = oneByOne ? Mathf.Max(0, _bottlesLeft - 1) : 0;
            var keepHolding = oneByOne && _bottlesLeft > 0;
            _visuals.Throw(_targets, definition.multi.staggerSeconds, keepHolding, _flightIds);

            var center = (_flightIds.Count - 1) / 2;
            for (var i = 0; i < _flightIds.Count; i++)
            {
                var entry = new InAir
                {
                    FlightId = _flightIds[i],
                    Target = _targets[i],
                    FirstOfThrow = i == 0,
                    FinishesSkill = !oneByOne,
                    Loop = Handle<VfxMarker>.Invalid
                };
                if (i == center)
                {
                    var follow = _visuals.FlightTransform(_flightIds[i]);
                    if (follow != null)
                    {
                        entry.Loop = FortressEffectPlayer.StartFollowing(definition.sounds.whileFlying, follow, Turret.playerIndex);
                    }
                }

                _inAir.Add(entry);
            }

            FortressEffectPlayer.PlayOnce(definition.sounds.onThrow, _visuals.BottlePosition, Turret.transform.rotation, Turret.playerIndex);
            var shake = definition.cameraShake;
            BlueFx.Shake(shake.onThrow, shake.throwStrength, shake.ownerViewOnly, Turret.playerIndex);

            if (oneByOne)
            {
                // 1本ずつ: 投げた時点でこの攻撃は終わり。まだ残っていれば、すぐ次の1本を構える（次の攻撃で投げる）。
                SetPhase(Phase.Idle);
                NotifyFinished();
                if (_bottlesLeft > 0 && Slot != null)
                {
                    _rearming = true;
                    Slot.GrantCharge();
                    _rearming = false;
                }
            }
            else
            {
                _awaitingLandings = _flightIds.Count;
                SetPhase(Phase.Flight);
            }
        }

        /// <summary>落ちる時刻になったボトルから順に渦を出す。</summary>
        private void LandDueBottles()
        {
            for (var i = 0; i < _inAir.Count; i++)
            {
                var entry = _inAir[i];
                if (!_visuals.IsDue(entry.FlightId))
                {
                    continue;
                }

                _inAir.RemoveAt(i);
                i--;
                _visuals.Land(entry.FlightId);
                FortressEffectPlayer.Stop(ref entry.Loop);
                if (entry.FinishesSkill)
                {
                    _awaitingLandings--;
                }

                // 揺れが重なりすぎないよう、まとめて投げたときの2つ目以降の渦の揺れは弱める。
                var shakeScale = entry.FirstOfThrow ? 1f : definition.multi.extraShakeScale;
                var area = BlueVortexArea.Spawn(definition, entry.Target, Turret.playerIndex, definition.AreaRadiusScale, shakeScale);
                LastArea = area;
                if (area != null)
                {
                    AnyVortexSpawned?.Invoke(this, area);
                }
            }
        }

        private void StopEverything()
        {
            StopLoops();
            _inAir.Clear();
            _awaitingLandings = 0;
            _bottlesLeft = 0;
            _gripProgress = 0f;
            OverrideRotation(false);
            _visuals?.StopAll();
            SetPhase(Phase.Idle);
        }

        private void StopLoops()
        {
            for (var i = 0; i < _inAir.Count; i++)
            {
                var entry = _inAir[i];
                FortressEffectPlayer.Stop(ref entry.Loop);
                _inAir[i] = entry;
            }
        }

        private void SetPhase(Phase next)
        {
            _phase = next;
            _phaseTime = 0f;
        }

        /// <summary>振りかぶっている間だけ、砲台の回る速さを落とす（狙いを定めやすくする）。</summary>
        private void OverrideRotation(bool on)
        {
            var turret = Turret;
            if (turret == null)
            {
                return;
            }

            if (on)
            {
                if (!_rotationOverridden)
                {
                    _savedRotationScale = turret.rotationSpeedScale;
                    _rotationOverridden = true;
                }

                turret.rotationSpeedScale = _savedRotationScale * definition.timing.rotationSpeedScale;
            }
            else if (_rotationOverridden)
            {
                turret.rotationSpeedScale = _savedRotationScale;
                _rotationOverridden = false;
            }
        }
    }
}
