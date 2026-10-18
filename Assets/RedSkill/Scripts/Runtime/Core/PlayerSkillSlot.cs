using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>スキル欄の状態。</summary>
    public enum PlayerSkillState
    {
        /// <summary>スキルが設定されていない。</summary>
        NoSkill,

        /// <summary>まだ使えない（溜まっていない）。</summary>
        Empty,

        /// <summary>使える。次にレーザーを撃ち始めたときにスキルになる。</summary>
        Ready,

        /// <summary>スキルを撃っている最中。</summary>
        Active
    }

    /// <summary>
    /// 砲台1台ぶんの「スキル欄」。どのスキルを持っているか・使えるか・いつ撃つかを受け持つ（スキルの中身は持たない）。
    /// ・スマッシュボールを自分が割る（または <see cref="GrantCharge"/>）と「使える」になり、溜めの見た目が出る。
    /// ・「使える」状態で次にレーザーを撃ち始めた瞬間（砲台の状態が Firing になった瞬間）にスキルを撃つ。
    ///   撃っている最中に使えるようになった場合は、そのレーザーを離して撃ち終わってからの「次の」撃ち始めで発動する。
    /// ・キャラに紐づけるときは <see cref="SetSkill"/> でスキルを差し替える（<see cref="PlayerSkillInstaller"/> が自動で行う）。
    /// ネット対戦: スマッシュボールの「割れた」と砲台の状態は全PCに届くので、各PCがこの判断を自分で行う（通信は増やさない）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    [DefaultExecutionOrder(-5)]
    public sealed class PlayerSkillSlot : MonoBehaviour
    {
        [Tooltip("この砲台が持つスキル。空ならスキルなし。キャラに紐づけるときはここが差し替わる。")]
        [SkillLabel("スキル")]
        [SerializeField]
        private CharacterSkill skill;

        [Tooltip("テスト用: ONなら、ゲーム開始時から使える状態にする。")]
        [SkillLabel("最初から使える（テスト）")]
        public bool startReady;

        private static readonly List<PlayerSkillSlot> ActiveSlots = new List<PlayerSkillSlot>();

        private LaserTurret _turret;
        private CharacterSkillRunner _runner;
        private NormalLaserSuppressor _suppressor;
        private bool _waitForRelease;
        private bool _pendingCharge;
        private bool _holdSuppressUntilRelease;
        private bool _subscribed;

        /// <summary>どの砲台のスキル欄でも、状態が変わったときに呼ばれる（UIの表示などに使う）。</summary>
        public static event Action<PlayerSkillSlot> AnyStateChanged;

        /// <summary>このスキル欄の状態が変わったときに呼ばれる。</summary>
        public event Action<PlayerSkillSlot> StateChanged;

        /// <summary>シーン内で有効なスキル欄の一覧。</summary>
        public static IReadOnlyList<PlayerSkillSlot> All => ActiveSlots;

        public PlayerSkillState State { get; private set; } = PlayerSkillState.NoSkill;

        public CharacterSkill Skill => skill;

        public CharacterSkillRunner Runner => _runner;

        public LaserTurret Turret
        {
            get
            {
                if (_turret == null)
                {
                    _turret = GetComponent<LaserTurret>();
                }

                return _turret;
            }
        }

        public int PlayerIndex => Turret != null ? Turret.playerIndex : -1;

        public bool IsReady => State == PlayerSkillState.Ready;

        public bool IsActive => State == PlayerSkillState.Active;

        /// <summary>使える状態だが、今のレーザーを離すまで待っているか（撃っている最中に溜まった場合）。</summary>
        public bool IsWaitingForRelease => IsReady && _waitForRelease;

        /// <summary>そのプレイヤー番号の砲台のスキル欄を探す（無ければ null）。</summary>
        public static PlayerSkillSlot Find(int playerIndex)
        {
            for (var i = 0; i < ActiveSlots.Count; i++)
            {
                if (ActiveSlots[i] != null && ActiveSlots[i].PlayerIndex == playerIndex)
                {
                    return ActiveSlots[i];
                }
            }

            return null;
        }

        // 非アクティブな砲台に付けられた場合は Awake より先に SetSkill が呼ばれることがあるので、必要になった時点で作る。
        private NormalLaserSuppressor Suppressor => _suppressor ??= new NormalLaserSuppressor(Turret);

        private void OnEnable()
        {
            ActiveSlots.Add(this);
            Subscribe(true);
            RefreshSuppression();
        }

        private void Start()
        {
            if (_runner == null && skill != null)
            {
                ApplySkill(skill);
            }

            if (startReady)
            {
                GrantCharge();
            }
        }

        private void OnDisable()
        {
            ActiveSlots.Remove(this);
            Subscribe(false);
            if (_runner != null && _runner.IsActive)
            {
                _runner.Cancel();
            }

            Suppressor.Release();
            _holdSuppressUntilRelease = false;
            if (State == PlayerSkillState.Active)
            {
                SetState(PlayerSkillState.Empty);
            }
        }

        private void OnDestroy()
        {
            DestroyRunner();
        }

        /// <summary>
        /// スキルを差し替える（キャラに紐づけるときに使う）。null でスキルなし。撃っている最中なら中断する。
        /// 使える状態だった場合は、新しいスキルでもそのまま使える状態を引き継ぐ。
        /// </summary>
        public void SetSkill(CharacterSkill newSkill)
        {
            if (skill == newSkill && (_runner != null || newSkill == null))
            {
                return;
            }

            ApplySkill(newSkill);
        }

        /// <summary>スキルを使える状態にする（スマッシュボールを割ったときと同じ）。</summary>
        public void GrantCharge()
        {
            if (skill == null || _runner == null)
            {
                return;
            }

            if (State == PlayerSkillState.Ready)
            {
                return;
            }

            if (State == PlayerSkillState.Active && skill.ignoreChargeWhileActive)
            {
                return;
            }

            // 撃っている最中に溜まったら、そのレーザーを離すまで待つ（次の撃ち始めでスキル）。
            _waitForRelease = Turret != null && (Turret.IsFiring || State == PlayerSkillState.Active);
            if (State == PlayerSkillState.Active)
            {
                // スキル中に溜まった場合（連発を許す設定）は、今のスキルが終わってから Ready にする。
                _pendingCharge = true;
                return;
            }

            SetState(PlayerSkillState.Ready);
            _runner.SetReadyVisual(true);
        }

        /// <summary>使える状態を取り消す（撃っている最中なら何もしない）。</summary>
        public void ClearCharge()
        {
            _pendingCharge = false;
            if (State != PlayerSkillState.Ready)
            {
                return;
            }

            _runner.SetReadyVisual(false);
            SetState(PlayerSkillState.Empty);
        }

        /// <summary>テスト用: レーザーを撃っていなくても今すぐスキルを撃つ。使えない状態なら false。</summary>
        public bool ActivateNow()
        {
            if (State != PlayerSkillState.Ready)
            {
                return false;
            }

            Activate();
            return true;
        }

        /// <summary>撃っている最中のスキルをすぐに止める。</summary>
        public void CancelActive()
        {
            if (State != PlayerSkillState.Active || _runner == null)
            {
                return;
            }

            _runner.Cancel();
            HandleRunnerFinished(_runner);
        }

        internal void HandleRunnerFinished(CharacterSkillRunner runner)
        {
            if (runner != _runner || State != PlayerSkillState.Active)
            {
                return;
            }

            // 握り続けている間は普段のレーザーを出さない（設定でON のとき）。
            _holdSuppressUntilRelease = skill != null && skill.replacesNormalLaser && skill.hideNormalLaserUntilRelease
                                        && Turret != null && Turret.IsFiring;
            SetState(PlayerSkillState.Empty);
            if (_pendingCharge)
            {
                _pendingCharge = false;
                GrantCharge();
            }
        }

        private void ApplySkill(CharacterSkill newSkill)
        {
            var wasReady = State == PlayerSkillState.Ready;
            if (_runner != null && _runner.IsActive)
            {
                _runner.Cancel();
            }

            _holdSuppressUntilRelease = false;
            DestroyRunner();
            skill = newSkill;
            _pendingCharge = false;

            if (skill == null)
            {
                SetState(PlayerSkillState.NoSkill);
                return;
            }

            _runner = skill.CreateRunner(this);
            if (_runner != null)
            {
                _runner.Bind(this);
            }

            SetState(PlayerSkillState.Empty);
            if (wasReady)
            {
                GrantCharge();
            }
        }

        private void DestroyRunner()
        {
            if (_runner == null)
            {
                return;
            }

            _runner.SetReadyVisual(false);
            Destroy(_runner);
            _runner = null;
        }

        private void Subscribe(bool on)
        {
            if (_subscribed == on || Turret == null)
            {
                return;
            }

            _subscribed = on;
            if (on)
            {
                Turret.OnStateChanged += HandleTurretStateChanged;
                SmashBallModule.AnyBroken += HandleSmashBallBroken;
            }
            else
            {
                Turret.OnStateChanged -= HandleTurretStateChanged;
                SmashBallModule.AnyBroken -= HandleSmashBallBroken;
            }
        }

        private void HandleSmashBallBroken(SmashBallBreakInfo info)
        {
            if (skill == null || !skill.chargeBySmashBall)
            {
                return;
            }

            var attacker = info.Attacker;
            var mine = attacker.Turret != null ? attacker.Turret == Turret : attacker.PlayerIndex == PlayerIndex;
            if (mine)
            {
                GrantCharge();
            }
        }

        private void HandleTurretStateChanged(TurretState next)
        {
            if (next != TurretState.Firing)
            {
                // レーザーを離した（または撃てなくなった）ので、次の撃ち始め（次の攻撃）でスキルを出せる。
                _waitForRelease = false;
                _holdSuppressUntilRelease = false;
                RefreshSuppression();
                return;
            }

            if (State == PlayerSkillState.Ready && !_waitForRelease)
            {
                Activate();
            }
        }

        private void Activate()
        {
            if (_runner == null)
            {
                return;
            }

            _runner.SetReadyVisual(false);
            _holdSuppressUntilRelease = false;
            SetState(PlayerSkillState.Active);
            _runner.Activate();
        }

        private void SetState(PlayerSkillState next)
        {
            if (State == next)
            {
                RefreshSuppression();
                return;
            }

            State = next;
            RefreshSuppression();
            StateChanged?.Invoke(this);
            AnyStateChanged?.Invoke(this);
        }

        /// <summary>
        /// 今の状態に合わせて、普段のレーザー・照準線を隠す／戻す。
        /// ・使える状態の間は何もしない。次にレーザーを撃ち始めた瞬間（次の攻撃）にスキルが始まり、ここで普段のレーザーを隠すので、
        ///   その攻撃で普段のレーザーが出ることはない（同じフレームで隠すので一瞬も映らない）。
        ///   撃っている最中に溜まった場合は、そのレーザーは離すまでそのまま出る。
        /// ・スキル中: 普段のレーザーと照準線を隠す（スキルのレーザーに置き換える）。
        /// ・スキルの後も握り続けている間（設定でON）: 普段のレーザーを隠したまま。
        /// </summary>
        private void RefreshSuppression()
        {
            if (skill == null || !isActiveAndEnabled)
            {
                Suppressor.Release();
                return;
            }

            var active = State == PlayerSkillState.Active && skill.replacesNormalLaser;
            Suppressor.Apply(active || _holdSuppressUntilRelease, active);
        }
    }
}
