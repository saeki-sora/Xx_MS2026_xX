using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 「ミント・サテライト」を実際に動かす係（<see cref="GreenMintSkill.CreateRunner"/> が砲台に付ける。シーンに手で置く必要はない）。
    /// 流れ: 使える間はケースが震える → 握ると蓋が開き、タブレットがコアへ飛ぶ・ジェルがコアへ伸びる（launch）
    /// → コアに着いて「ドン」と回復＋全員の画面に波（ここから active）→ 衛星が回って近い敵を撃ち、コアがじわじわ回復
    /// → 衛星がらせんを描いてコアに吸い込まれ、ジェルの根元もコアへ吸い込まれる（end）→ スロットに終了を知らせる。
    /// スキル中に緑のプレイヤーが握ると、ジェルが太く速くなり、弾が増える（握っている間は砲台の熱は普段どおり上がる）。
    /// 回復とダメージは結果を決めるPC（オフライン／Host）だけ。見た目・効果音・揺れは全PCがそれぞれ出す（通信は増やさない）。
    /// </summary>
    // スキルを別の設定ファイルに差し替えるとき、古い係の削除はフレームの最後なので、同じ砲台に一時的に2つ付くことがある（DisallowMultipleComponent にしない）。
    [DefaultExecutionOrder(10)]
    public sealed class GreenMintSkillRunner : CharacterSkillRunner
    {
        private enum Phase
        {
            Idle,
            Launch,
            Active,
            Ending
        }

        [SerializeField]
        private GreenMintSkill definition;

        private GreenMintFx _fx;
        private GreenMintCase _case;
        private GreenMintSatellites _satellites;
        private GreenGelLink _gel;
        private GreenTabletVolley _volley;
        private GreenMintBurst _burst;
        private GreenTargetScanner _scanner;

        private Phase _phase = Phase.Idle;
        private float _phaseTime;
        private float _boost;
        private float _shotCarry;
        private float _crossCarry;
        private float _regenPending;
        private float _nextShotSound;
        private int _nextSatellite;
        private Handle<VfxMarker> _loopVfx = Handle<VfxMarker>.Invalid;

        /// <summary>使っている設定ファイル。</summary>
        public GreenMintSkill Definition => definition;

        public override bool IsActive => _phase != Phase.Idle;

        /// <summary>今の握りの強さ（0〜1、なめらかにしたもの）。スキル中だけ動く。</summary>
        public float Boost01 => _boost;

        /// <summary>このスキルで今までに回復した量（このスキル1回ぶん）。</summary>
        public float HealedThisUse { get; private set; }

        internal void Configure(GreenMintSkill skill)
        {
            definition = skill;
        }

        protected override void OnBound()
        {
            if (_fx != null || definition == null || Turret == null)
            {
                return;
            }

            _fx = new GreenMintFx(definition, Turret.playerIndex);
            _scanner = new GreenTargetScanner();
            _case = new GreenMintCase(_fx, Turret);
            _satellites = new GreenMintSatellites(_fx);
            _gel = new GreenGelLink(_fx);
            _volley = new GreenTabletVolley(_fx, _scanner);
            _burst = new GreenMintBurst(_fx);
        }

        public override void SetReadyVisual(bool ready)
        {
            OnBound();
            if (_case == null)
            {
                return;
            }

            if (ready)
            {
                _case.Show();
                FortressEffectPlayer.PlayOnce(definition.sounds.onReady, Turret.transform.position, Quaternion.identity, Turret.playerIndex);
            }
            else if (_phase == Phase.Idle)
            {
                // 発動以外で取り消された（テストの「止める」など）。発動のときは Activate がケースを開く。
                _case.Dismiss();
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
            SetPhase(Phase.Launch);
            _boost = 0f;
            _shotCarry = 0f;
            _regenPending = 0f;
            HealedThisUse = 0f;
            _case.Open();
            var from = _case.IsShown ? (Vector2)_case.MouthPosition : (Vector2)Turret.transform.position;
            _satellites.Launch(from, definition.timing.launchSeconds);
            _scanner.SetActive(true);
            FortressEffectPlayer.PlayOnce(definition.sounds.onActivate, _case.IsShown ? _case.MouthPosition : Turret.transform.position, Quaternion.identity, Turret.playerIndex);
        }

        public override void Cancel()
        {
            if (_phase == Phase.Idle)
            {
                return;
            }

            StopEverything(true);
        }

        private void OnEnable()
        {
            _fx?.SetVisible(true);
        }

        private void OnDisable()
        {
            if (_phase != Phase.Idle)
            {
                StopEverything(true);
            }

            _case?.Hide();
            _fx?.SetVisible(false);
        }

        private void OnDestroy()
        {
            FortressEffectPlayer.Stop(ref _loopVfx);
            _scanner?.Dispose();
            _gel?.Dispose();
            _fx?.Dispose();
            _fx = null;
        }

        private void Update()
        {
            if (_fx == null || Turret == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            var turretPosition = (Vector2)Turret.transform.position;
            var core = (Vector2)GreenMintUtil.CorePosition(Turret.transform.position);
            UpdateBoost(dt);

            _fx.Tick(dt);
            _case.Tick(dt);
            _burst.Tick(dt);

            switch (_phase)
            {
                case Phase.Launch:
                    _phaseTime += dt;
                    if (_phaseTime >= definition.timing.launchSeconds)
                    {
                        BeginActive(core);
                    }

                    break;
                case Phase.Active:
                    _phaseTime += dt;
                    Regenerate(dt);
                    Shoot(core, dt);
                    EmitRegenFx(core, dt);
                    if (_phaseTime >= definition.timing.activeSeconds)
                    {
                        BeginEnding();
                    }

                    break;
                case Phase.Ending:
                    _phaseTime += dt;
                    if (_phaseTime >= definition.timing.endSeconds)
                    {
                        Finish(core);
                    }

                    break;
            }

            _satellites.Tick(core, _boost, dt);
            _volley.Tick(Turret, dt);
            UpdateGel(turretPosition, core, dt);

            // スキルが終わっても、飛んでいる弾が着くまでは敵の位置を追う。
            if (_phase == Phase.Idle && _volley.ActiveCount == 0)
            {
                _scanner.SetActive(false);
            }
        }

        // ───────────── 段階 ─────────────

        private void BeginActive(Vector2 core)
        {
            SetPhase(Phase.Active);
            _satellites.BeginOrbit();
            _burst.Play(core, Turret.playerIndex);

            var coreController = GreenMintUtil.Core;
            if (coreController != null)
            {
                HealedThisUse += GreenMintUtil.Heal(coreController.maxHealth * definition.heal.burstPercent / 100f, definition.heal.stopWhenMatchEnded);
                FortressEffectPlayer.Stop(ref _loopVfx);
                _loopVfx = FortressEffectPlayer.StartFollowing(definition.sounds.whileActive, coreController.transform, Turret.playerIndex);
            }

            FortressEffectPlayer.PlayOnce(definition.sounds.onBurst, new Vector3(core.x, core.y, 0f), Quaternion.identity, Turret.playerIndex);
        }

        private void BeginEnding()
        {
            SetPhase(Phase.Ending);
            FlushRegen(true);
            _satellites.BeginEnd(definition.timing.endSeconds);
        }

        private void Finish(Vector2 core)
        {
            var palette = definition.palette;
            _fx.FlashAt(core, definition.satellites.membraneRadius * 2.6f, 0.45f, palette.glow, 10, 0.6f);
            _fx.Sparkles(core, 40, 9f, 0.35f, palette.glow);
            var ring = palette.mint;
            ring.a = 0.8f;
            _fx.WaveAt(core, 1f, definition.satellites.membraneRadius * 3.5f, 0.45f, ring);
            FortressEffectPlayer.PlayOnce(definition.sounds.onEnd, new Vector3(core.x, core.y, 0f), Quaternion.identity, Turret.playerIndex);
            StopEverything(false);
            NotifyFinished();
        }

        /// <summary>止める。immediate なら見た目もすぐ消す（中断）。そうでなければ飛んでいる弾は最後まで飛ばす。</summary>
        private void StopEverything(bool immediate)
        {
            SetPhase(Phase.Idle);
            FortressEffectPlayer.Stop(ref _loopVfx);
            _boost = 0f;
            if (immediate)
            {
                _volley?.Clear();
                _satellites?.Hide();
                _gel?.Hide();
                _burst?.Hide();
                _case?.Dismiss();
                _scanner?.SetActive(false);
            }
        }

        private void SetPhase(Phase next)
        {
            _phase = next;
            _phaseTime = 0f;
        }

        // ───────────── 中身 ─────────────

        /// <summary>握る強さ: 握っている間の太さ（0〜1）をなめらかに追う。スキル中以外は 0 へ戻る。</summary>
        private void UpdateBoost(float dt)
        {
            var target = 0f;
            if ((_phase == Phase.Launch || _phase == Phase.Active) && Turret.IsFiring)
            {
                target = Mathf.Clamp01(Turret.CurrentThickness01);
            }

            var response = definition.boost.response;
            _boost = Mathf.Lerp(_boost, target, 1f - Mathf.Exp(-response * dt));
        }

        private void Regenerate(float dt)
        {
            var core = GreenMintUtil.Core;
            if (core == null || definition.heal.regenPercent <= 0f)
            {
                return;
            }

            _regenPending += core.maxHealth * definition.heal.regenPercent / 100f * dt / Mathf.Max(0.1f, definition.timing.activeSeconds);
            FlushRegen(false);
        }

        /// <summary>じわじわ回復を、ある程度たまったらまとめてコアに入れる（細かすぎる変化はコア側で無視されるため）。</summary>
        private void FlushRegen(bool force)
        {
            if (_regenPending <= 0f || (!force && _regenPending < 0.02f))
            {
                return;
            }

            HealedThisUse += GreenMintUtil.Heal(_regenPending, definition.heal.stopWhenMatchEnded);
            _regenPending = 0f;
        }

        private void Shoot(Vector2 core, float dt)
        {
            if (!_satellites.CanShoot)
            {
                return;
            }

            var attack = definition.attack;
            _scanner.Configure(core, attack.range, attack.bombPriorityMeters, attack.targetActors);
            _scanner.BeginFrame();

            var rate = Mathf.Lerp(attack.shotsPerSecond, Mathf.Max(attack.shotsPerSecond, definition.boost.boostedShotsPerSecond), _boost);
            _shotCarry += rate * dt;
            var shots = Mathf.Min(Mathf.FloorToInt(_shotCarry), 8);
            for (var i = 0; i < shots; i++)
            {
                if (!_scanner.TryTake(out var target))
                {
                    // 狙う敵がいない: 溜めすぎないようにして待つ。
                    _shotCarry = Mathf.Min(_shotCarry, 1f);
                    return;
                }

                _shotCarry -= 1f;
                var satellite = PickSatellite(target.Position);
                var from = _satellites.PositionOf(satellite);
                if (!_volley.Fire(from, from - core, target))
                {
                    _scanner.Release(target);
                    _shotCarry = Mathf.Min(_shotCarry, 1f);
                    return;
                }

                _satellites.Punch(satellite);
                var shotFx = definition.shots;
                if (shotFx.muzzleFlashSize > 0f)
                {
                    _fx.FlashAt(from, shotFx.muzzleFlashSize, 0.14f, definition.palette.glow, 4);
                }

                var sounds = definition.sounds;
                if (Time.time >= _nextShotSound && !sounds.onShot.IsEmpty)
                {
                    _nextShotSound = Time.time + sounds.shotInterval;
                    FortressEffectPlayer.PlayOnce(sounds.onShot, new Vector3(from.x, from.y, 0f), FortressEffectPlayer.RotationFacing(target.Position - from), Turret.playerIndex);
                }
            }
        }

        /// <summary>撃つ衛星: 敵に近い衛星を優先しつつ、同じ衛星ばかりにならないよう順番も混ぜる。</summary>
        private int PickSatellite(Vector2 target)
        {
            var count = _satellites.Count;
            var best = _nextSatellite % count;
            var bestDistance = float.MaxValue;
            for (var k = 0; k < Mathf.Min(3, count); k++)
            {
                var index = (_nextSatellite + k) % count;
                var distance = (_satellites.PositionOf(index) - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            _nextSatellite = (best + 1) % count;
            return best;
        }

        private void EmitRegenFx(Vector2 core, float dt)
        {
            var crosses = GreenMintUtil.Accumulate(ref _crossCarry, definition.burst.regenCrossRate * dt);
            if (crosses > 0)
            {
                _fx.Crosses(core, crosses, 0.55f, definition.satellites.membraneRadius * 0.8f, GreenMintUtil.ScreenUp);
            }
        }

        /// <summary>ジェル: 発動で砲台からコアへ伸び、スキル中は流れ続け、終わりは根元がコアへ吸い込まれる。</summary>
        private void UpdateGel(Vector2 turret, Vector2 core, float dt)
        {
            var link = definition.link;
            float tail;
            float head;
            var strength = 1f;
            switch (_phase)
            {
                case Phase.Launch:
                    tail = 0f;
                    head = GreenMintUtil.EaseOutCubic(_phaseTime / Mathf.Max(0.01f, Mathf.Min(link.extendSeconds, definition.timing.launchSeconds)));
                    break;
                case Phase.Active:
                    tail = 0f;
                    head = 1f;
                    break;
                case Phase.Ending:
                    var t = _phaseTime / Mathf.Max(0.01f, Mathf.Min(link.extendSeconds, definition.timing.endSeconds));
                    tail = Mathf.SmoothStep(0f, 1f, t);
                    head = 1f;
                    strength = 1f - Mathf.Clamp01(t - 0.6f) / 0.4f;
                    break;
                default:
                    _gel.Hide();
                    return;
            }

            var axis = core - turret;
            var length = axis.magnitude;
            if (length < 0.1f)
            {
                _gel.Hide();
                return;
            }

            var dir = axis / length;
            var from = turret + dir * 0.55f;
            var to = core - dir * (definition.satellites.membraneRadius * 0.8f);
            _gel.Tick(from, to, tail, head, _boost, strength, dt);
        }
    }
}
