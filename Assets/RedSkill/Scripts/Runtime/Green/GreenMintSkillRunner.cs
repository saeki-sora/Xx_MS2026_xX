using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑の守護霊のスキルを実際に動かす係（<see cref="GreenMintSkill.CreateRunner"/> が砲台に付ける。シーンに手で置く必要はない）。
    /// 流れ（決定版ページと同じ。秒はカットインの後、この係が動き始めてから）:
    /// 0秒 コアに光の柱 → 0.35秒 巨大な守護霊（26m）が画面の上から降りる（両手を上げた姿から広げた姿へ）
    /// → 1.5秒「ドン」着地: 衝撃波・画面のふち・揺れ、コア回復（20%）、葉の輪がコアから広がる、しっぽの煙が緑の砲台へつながる
    /// → 2.7〜3.5秒 一回り小さく（11m）なってコアの背後に収まり、コアを抱える姿へ
    /// → 12秒まで 葉で守る（触れた敵にダメージ、コアはじわじわ回復 15%）
    /// → 1秒かけて葉と守護霊が渦になってコアへ吸い込まれる → コアがきらっと光る → スロットに終了を知らせる。
    /// スキル中に緑のプレイヤーが握ると、輪が速く寄せては返し、回転も速く、葉が大きく光る（握っている間は砲台の熱は普段どおり上がる）。
    /// 回復とダメージは結果を決めるPC（オフライン／Host）だけ。見た目・効果音・揺れは全PCがそれぞれ出す（通信は増やさない）。
    /// </summary>
    // スキルを別の設定ファイルに差し替えるとき、古い係の削除はフレームの最後なので、同じ砲台に一時的に2つ付くことがある（DisallowMultipleComponent にしない）。
    [DefaultExecutionOrder(10)]
    public sealed class GreenMintSkillRunner : CharacterSkillRunner
    {
        private enum Phase
        {
            Idle,
            Descend,
            Guard,
            Suck
        }

        private const int MaxHitFxPerFrame = 12;

        [SerializeField]
        private GreenMintSkill definition;

        private GreenMintFx _fx;
        private GreenToothpasteTube _tube;
        private GreenGuardianSpirit _spirit;
        private GreenLeafRing _ring;
        private GreenLeafContacts _contacts;
        private GreenMintBurst _burst;
        private SpriteRenderer _pillar;
        private MeshRenderer _coreGlow;
        private MeshRenderer _turretGlow;

        private Phase _phase = Phase.Idle;
        private float _time;
        private float _boost;
        private float _regenPending;
        private float _crossCarry;
        private float _sparkCarry;
        private float _smokeCarry;
        private float _coreFlash;
        private float _nextHitSound;
        private bool _landed;
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
            _tube = new GreenToothpasteTube(_fx, Turret);
            _spirit = new GreenGuardianSpirit(_fx);
            _ring = new GreenLeafRing(_fx);
            _contacts = new GreenLeafContacts(_ring);
            _contacts.Configure(definition);
            _burst = new GreenMintBurst(_fx);
            _pillar = _fx.CreateSprite("Pillar", _fx.Art.Pillar, GreenMintFx.SortingBase + 1);
            _coreGlow = _fx.CreateQuad("CoreGroundGlow", _fx.GroundGlowMaterial, 0);
            _turretGlow = _fx.CreateQuad("TurretGroundGlow", _fx.GroundGlowMaterial, 0);
        }

        public override void SetReadyVisual(bool ready)
        {
            OnBound();
            if (_tube == null)
            {
                return;
            }

            if (ready)
            {
                _tube.Show();
                FortressEffectPlayer.PlayOnce(definition.sounds.onReady, Turret.transform.position, Quaternion.identity, Turret.playerIndex);
            }
            else
            {
                // 発動（カットインの始まり）でも、取り消し（テストの「止める」など）でも、最後にぼふっとして消える。
                _tube.Dismiss(true);
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
            _tube.Dismiss(false);
            _phase = Phase.Descend;
            _time = 0f;
            _boost = 0f;
            _regenPending = 0f;
            _landed = false;
            HealedThisUse = 0f;
            _ring.ResetLeaves();
            _contacts.SetActive(true, Turret);
            var core = CorePosition;
            FortressEffectPlayer.PlayOnce(definition.sounds.onPillar, new Vector3(core.x, core.y, 0f), Quaternion.identity, Turret.playerIndex);
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
            _fx?.SetVisible(true);
        }

        private void OnDisable()
        {
            if (_phase != Phase.Idle)
            {
                StopEverything();
            }

            _tube?.Hide();
            _fx?.SetVisible(false);
        }

        private void OnDestroy()
        {
            FortressEffectPlayer.Stop(ref _loopVfx);
            _contacts?.Dispose();
            _spirit?.Dispose();
            _fx?.Dispose();
            _fx = null;
        }

        private Vector2 CorePosition => GreenMintUtil.CorePosition(Turret.transform.position);

        private void Update()
        {
            if (_fx == null || Turret == null)
            {
                return;
            }

            var dt = Time.deltaTime;
            var core = CorePosition;
            UpdateBoost(dt);
            _fx.Tick(dt);
            _tube.Tick(dt);
            _burst.Tick(dt);
            _coreFlash = Mathf.Max(0f, _coreFlash - dt * 1.5f);

            if (_phase == Phase.Idle)
            {
                _spirit.Hide();
                _ring.Hide();
                _pillar.enabled = false;
                UpdateGroundGlows(core, false, Slot != null && Slot.IsWaitingForCutIn);
                return;
            }

            _time += dt;
            var timing = definition.timing;
            if (_phase == Phase.Descend && _time >= timing.Land)
            {
                Land(core);
            }

            if (_phase == Phase.Guard && _time >= timing.GuardEnd)
            {
                BeginSuck(core);
            }

            if (_phase == Phase.Suck && _time >= timing.GuardEnd + timing.suckSeconds)
            {
                Finish(core);
                return;
            }

            var state = SpiritState(core, dt);
            _spirit.Apply(state, Time.time);
            UpdatePillar(core);
            UpdateRing(core, dt);
            if (_phase == Phase.Guard)
            {
                Regenerate(dt);
                EmitRegenCrosses(core, dt);
            }

            UpdateGroundGlows(core, _phase == Phase.Guard, true);
        }

        // ───────────── 段階 ─────────────

        /// <summary>ドン: 守護霊が着地した瞬間。</summary>
        private void Land(Vector2 core)
        {
            _phase = Phase.Guard;
            _landed = true;
            _coreFlash = 1f;
            _burst.PlayLand(core, Turret.playerIndex);

            var coreController = GreenMintUtil.Core;
            if (coreController != null)
            {
                HealedThisUse += GreenMintUtil.Heal(coreController.maxHealth * definition.heal.burstPercent / 100f, definition.heal.stopWhenMatchEnded);
                FortressEffectPlayer.Stop(ref _loopVfx);
                _loopVfx = FortressEffectPlayer.StartFollowing(definition.sounds.whileGuarding, coreController.transform, Turret.playerIndex);
            }

            var position = new Vector3(core.x, core.y, 0f);
            FortressEffectPlayer.PlayOnce(definition.sounds.onLand, position, Quaternion.identity, Turret.playerIndex);
            FortressEffectPlayer.PlayOnce(definition.sounds.landVoice, position, Quaternion.identity, Turret.playerIndex);
        }

        private void BeginSuck(Vector2 core)
        {
            _phase = Phase.Suck;
            FlushRegen(true);
            FortressEffectPlayer.Stop(ref _loopVfx);
            FortressEffectPlayer.PlayOnce(definition.sounds.onSuck, new Vector3(core.x, core.y, 0f), Quaternion.identity, Turret.playerIndex);
        }

        private void Finish(Vector2 core)
        {
            _burst.PlayEnd(core);
            _coreFlash = 1f;
            FortressEffectPlayer.PlayOnce(definition.sounds.onEnd, new Vector3(core.x, core.y, 0f), Quaternion.identity, Turret.playerIndex);
            StopEverything();
            // 地面の光は、きらっの後もしばらく残して消す（Idle の間も少しずつ弱まる）。
            NotifyFinished();
        }

        /// <summary>止める（見た目もすぐ消す）。</summary>
        private void StopEverything()
        {
            _phase = Phase.Idle;
            FortressEffectPlayer.Stop(ref _loopVfx);
            _boost = 0f;
            _contacts?.SetActive(false, Turret);
            _contacts?.ClearHits();
            _spirit?.Hide();
            _ring?.Hide();
            if (_pillar != null)
            {
                _pillar.enabled = false;
            }
        }

        // ───────────── 守護霊 ─────────────

        private GreenSpiritState SpiritState(Vector2 core, float dt)
        {
            var timing = definition.timing;
            var spirit = definition.spirit;
            var t = _time;
            var land = timing.Land;
            var up = GreenMintUtil.ScreenUp;
            var right = GreenMintUtil.RightOf(up);
            var bob = Mathf.Sin(Time.time * spirit.bobSpeed) * spirit.bobAmount;
            var state = new GreenSpiritState
            {
                Visible = true,
                TailTarget = Turret.transform.position,
                Height = spirit.giantHeight,
                Alpha = 1f,
                TailFrom = 0f,
                TailTo = 1f
            };

            switch (_phase)
            {
                case Phase.Descend:
                {
                    // 画面の上から降りてくる（だんだんゆっくり）。降りながら両手を広げていく。
                    var descend = GreenMintUtil.EaseOutCubic(GreenMintUtil.Segment(t, timing.descendStart, land));
                    state.Center = core + up * Mathf.Lerp(spirit.startAbove, 0f, descend);
                    state.Pose = GreenMintUtil.Segment(t, land * 0.6f, land);
                    state.Alpha = GreenMintUtil.Segment(t, timing.descendStart, timing.descendStart + 0.35f);
                    state.Glow = spirit.glow.x;
                    state.TailMode = 1;
                    RainSparkles(core, up, right, dt);
                    break;
                }
                case Phase.Guard:
                {
                    // 一回り小さくなってコアの背後に収まり、コアを抱える姿へ。
                    var shrink = GreenMintUtil.EaseInOutCubic(GreenMintUtil.Segment(t, timing.shrinkStart, Mathf.Max(timing.shrinkStart + 0.05f, timing.shrinkEnd)));
                    state.Height = Mathf.Lerp(spirit.giantHeight, spirit.stayHeight, shrink);
                    state.Center = core + up * bob;
                    state.Pose = 1f + shrink;
                    state.Glow = Mathf.Lerp(spirit.glow.y, spirit.glow.z, GreenMintUtil.Segment(t, land, Mathf.Max(land + 0.1f, timing.shrinkEnd - 0.1f)));
                    state.Glow = Mathf.Max(state.Glow, 0.9f * _boost);
                    state.TailMode = 2;
                    state.TailTo = GreenMintUtil.EaseOutCubic(GreenMintUtil.Segment(t, land, land + 0.6f));
                    if (shrink > 0f && shrink < 1f)
                    {
                        // 縮むとき、体からふわっと煙がこぼれる。
                        var count = GreenMintUtil.Accumulate(ref _smokeCarry, 42f * dt);
                        for (var i = 0; i < count; i++)
                        {
                            var p = state.Center + right * (Random.Range(-0.25f, 0.25f) * state.Height) - up * (Random.Range(-0.5f, 0.2f) * state.Height);
                            _fx.Smoke(p, 1, 1.3f, 2f, Vector2.zero, 0.8f);
                        }
                    }

                    break;
                }
                case Phase.Suck:
                {
                    // 回りながら小さくなってコアへ吸い込まれる。しっぽの煙は先に消える。
                    var p = GreenMintUtil.Segment(t, timing.GuardEnd, timing.GuardEnd + timing.suckSeconds);
                    var g = GreenMintUtil.EaseInCubic(p);
                    state.Height = Mathf.Lerp(spirit.stayHeight, 0.5f, g);
                    state.Center = core + up * Mathf.Lerp(bob, 0f, g);
                    state.Pose = 2f;
                    state.Spin = g * 360f * 1.1f;
                    state.Alpha = 1f - GreenMintUtil.Segment(p, 0.75f, 1f);
                    state.Glow = spirit.glow.z + g;
                    state.TailMode = 2;
                    state.TailTo = 1f - GreenMintUtil.EaseOutCubic(GreenMintUtil.Segment(p, 0f, 0.6f));
                    var count = GreenMintUtil.Accumulate(ref _smokeCarry, 50f * dt);
                    for (var i = 0; i < count; i++)
                    {
                        var a = Time.time * 10f + i * 0.4f;
                        var r = Mathf.Lerp(5f, 0.5f, g);
                        _fx.Smoke(core + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 1, 0.9f, 0.3f, Vector2.zero, 0.5f);
                    }

                    break;
                }
            }

            return state;
        }

        /// <summary>光の柱の中を、上から光の粒が降る（降りてくる間）。</summary>
        private void RainSparkles(Vector2 core, Vector2 up, Vector2 right, float dt)
        {
            if (_time >= 1.4f)
            {
                return;
            }

            var count = GreenMintUtil.Accumulate(ref _sparkCarry, 48f * dt);
            var color = definition.palette.glow;
            for (var i = 0; i < count; i++)
            {
                var position = core + right * Random.Range(-2.2f, 2.2f) + up * 14f;
                _fx.SparkAt(position, -up * Random.Range(12f, 20f), Random.Range(0.3f, 0.5f), Random.Range(0.65f, 0.9f), color);
            }
        }

        private void UpdatePillar(Vector2 core)
        {
            var settings = definition.burst.pillar;
            var duration = Mathf.Max(0.05f, settings.y);
            var f = _time / duration;
            if (f >= 1f || settings.x <= 0f)
            {
                _pillar.enabled = false;
                return;
            }

            var up = GreenMintUtil.ScreenUp;
            var bottom = core - up * 0.5f;
            var width = settings.x * (1f - f * 0.4f);
            _pillar.transform.SetPositionAndRotation(new Vector3(bottom.x, bottom.y, GreenMintFx.ZOffset - 0.09f), GreenMintUtil.FacingUp(up));
            // 絵は幅 1・縦 4。縦 40m まで伸ばす（画面の上の外まで）。
            _pillar.transform.localScale = new Vector3(width, 10f, 1f);
            _pillar.color = new Color(0.92f, 1f, 0.88f, 0.75f * Mathf.Sin(f * Mathf.PI));
            _pillar.enabled = true;
        }

        // ───────────── 葉の輪 ─────────────

        private void UpdateRing(Vector2 core, float dt)
        {
            var timing = definition.timing;
            var appear = _landed ? GreenMintUtil.Segment(_time, timing.Land, timing.Land + timing.leafAppearSeconds) : 0f;
            if (appear <= 0f && _landed)
            {
                appear = 0.0001f;
            }

            var suck = _phase == Phase.Suck ? GreenMintUtil.Segment(_time, timing.GuardEnd, timing.GuardEnd + timing.suckSeconds) : -1f;
            _ring.Tick(core, appear, suck, _boost, Time.time, dt);

            if (_phase == Phase.Guard)
            {
                _contacts.Tick(core);
            }
            else
            {
                _contacts.Pause();
            }

            PlayHitFx();
        }

        private void PlayHitFx()
        {
            var hits = _contacts.Hits;
            if (hits.Count == 0)
            {
                return;
            }

            var leaves = definition.leaves;
            var palette = definition.palette;
            var sounds = definition.sounds;
            var shown = Mathf.Min(hits.Count, MaxHitFxPerFrame);
            for (var i = 0; i < shown; i++)
            {
                var hit = hits[i];
                _fx.Powder(hit.Position, leaves.hitPowder, 1f, 3f);
                _fx.Sparkles(hit.Position, leaves.hitSparkles, 6f, 0.3f, palette.glow, default, 180f, 0.4f);
                _fx.SlashAt(hit.Position, _ring.RotationOf(hit.Leaf), leaves.hitSlashSize);
                _ring.MarkHit(hit.Leaf);
            }

            if (Time.time >= _nextHitSound && !sounds.onLeafHit.IsEmpty)
            {
                _nextHitSound = Time.time + sounds.leafHitInterval;
                var p = hits[0].Position;
                FortressEffectPlayer.PlayOnce(sounds.onLeafHit, new Vector3(p.x, p.y, 0f), Quaternion.identity, Turret.playerIndex);
            }

            _contacts.ClearHits();
        }

        // ───────────── 回復・握る・地面の光 ─────────────

        /// <summary>握る強さ: 握っている間の太さ（0〜1）をなめらかに追う。スキル中以外は 0 へ戻る。</summary>
        private void UpdateBoost(float dt)
        {
            var target = 0f;
            if ((_phase == Phase.Descend || _phase == Phase.Guard) && Turret.IsFiring)
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

            var seconds = Mathf.Max(0.1f, definition.timing.GuardEnd - definition.timing.Land);
            _regenPending += core.maxHealth * definition.heal.regenPercent / 100f * dt / seconds;
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

        private void EmitRegenCrosses(Vector2 core, float dt)
        {
            var crosses = GreenMintUtil.Accumulate(ref _crossCarry, definition.burst.regenCrossRate * dt);
            for (var i = 0; i < crosses; i++)
            {
                _fx.Crosses(core + Random.insideUnitCircle, 1, 0.55f, 0f, GreenMintUtil.ScreenUp, 1.6f);
            }
        }

        /// <summary>コアと緑の砲台の足元の光（敵より下）。守っている間・きらっの後はコア、カットインからスキル中は砲台。</summary>
        private void UpdateGroundGlows(Vector2 core, bool guarding, bool turretBusy)
        {
            var glow = definition.palette.glow;
            var coreStrength = (guarding ? 0.25f : 0f) + 0.5f * _coreFlash;
            if (coreStrength > 0.005f)
            {
                _coreGlow.transform.SetPositionAndRotation(new Vector3(core.x, core.y, GreenMintFx.ZOffset + 0.02f), Quaternion.identity);
                _coreGlow.transform.localScale = new Vector3(6.8f, 6.8f, 1f);
                _fx.ApplySoftGlow(_coreGlow, glow * coreStrength);
                _coreGlow.enabled = true;
            }
            else
            {
                _coreGlow.enabled = false;
            }

            if (turretBusy)
            {
                var turret = (Vector2)Turret.transform.position;
                _turretGlow.transform.SetPositionAndRotation(new Vector3(turret.x, turret.y, GreenMintFx.ZOffset + 0.02f), Quaternion.identity);
                _turretGlow.transform.localScale = new Vector3(3.6f, 3.6f, 1f);
                _fx.ApplySoftGlow(_turretGlow, glow * (0.45f + 0.2f * _boost));
                _turretGlow.enabled = true;
            }
            else
            {
                _turretGlow.enabled = false;
            }
        }
    }
}
