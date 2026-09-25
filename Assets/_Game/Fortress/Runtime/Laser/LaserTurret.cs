using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 握力を太さ・熱に変換し、自動で360度回転する砲台の本体ロジック。見た目は持たず、状態だけを管理する
    /// （見た目は子オブジェクトの絵と<see cref="LaserBeamVisual"/>などが担当し、後から差し替え可能にする）。
    /// 向きは「このGameObjectの上方向(transform.up)」。レーザーの発射位置は muzzle（未設定なら自分の位置）。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class LaserTurret : MonoBehaviour
    {
        [Tooltip("0-3。GripInputBridgeが返す握力値のプレイヤー番号に対応する。")]
        [Range(0, 3)]
        public int playerIndex;

        [Tooltip("この砲台の太さ・熱・回転・射程などのチューニングデータ。")]
        public LaserTuningConfig tuning;

        [Header("回転")]
        [Tooltip("ONなら自動で回り続ける。OFFなら初期の向きのまま固定。")]
        public bool autoRotate = true;

        [Tooltip("回る向き。砲台ごとに変えられる。")]
        public TurretRotationDirection rotationDirection = TurretRotationDirection.Clockwise;

        [Tooltip("この砲台だけの回転速度の倍率（共有チューニングの回転速度に掛ける）。")]
        [Min(0f)]
        public float rotationSpeedScale = 1f;

        [Header("見た目との接続（絵を差し替えるときの目印）")]
        [Tooltip("レーザーの発射位置。砲身の先端に置いた空のGameObjectを指定する。未設定なら砲台の中心から出る。")]
        public Transform muzzle;

        /// <summary>状態(Idle/Charging/Firing/Overheated)が変化した際に発火する。</summary>
        public event Action<TurretState> OnStateChanged;

        public TurretState State { get; private set; } = TurretState.Idle;

        /// <summary>現在の蓄積熱量。</summary>
        public float Heat { get; private set; }

        /// <summary>0-1に正規化した熱量。UI表示（熱ゲージ）に使う。</summary>
        public float HeatRatio01 => tuning != null && tuning.overheatThreshold > 0f
            ? Mathf.Clamp01(Heat / tuning.overheatThreshold)
            : 0f;

        public float CurrentThickness01 { get; private set; }
        public float CurrentThicknessMeters { get; private set; }
        public bool IsFiring => State == TurretState.Firing;

        /// <summary>チャージの進み具合(0-1)。発射中は常に1。UI表示（チャージゲージ）に使う。</summary>
        public float ChargeProgress01 { get; private set; }

        /// <summary>現在の回転速度の倍率（射出中の強さなどで下がる）。滑らかに追従した後の値。</summary>
        public float CurrentRotationMultiplier { get; private set; } = 1f;

        /// <summary>現在の回転速度(度/秒)。</summary>
        public float CurrentRotationSpeed { get; private set; }

        /// <summary>レーザーの発射位置（ワールド座標）。</summary>
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;

        /// <summary>砲台が向いている方向（ワールド座標の単位ベクトル）。</summary>
        public Vector2 AimDirection => transform.up;

        /// <summary>砲台の向き。0度で真上、反時計回りが正。</summary>
        public float AimAngleDegrees => transform.eulerAngles.z;

        private float _silenceRemaining;
        private float _chargeTimer;
        private bool _hasChargedThisGrip;

        private void Update()
        {
            if (tuning == null)
            {
                return;
            }

            var dt = Time.deltaTime;

            if (State == TurretState.Overheated)
            {
                TickOverheated(dt);
            }
            else
            {
                UpdateGripState(dt);
            }

            UpdateRotation(dt);
        }

        private void UpdateGripState(float dt)
        {
            var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;
            var isGripping = provider.IsGripping(playerIndex);
            var grip = provider.GetGripValue(playerIndex);

            if (!isGripping)
            {
                Heat = Mathf.Max(0f, Heat - tuning.heatCoolingPerSecond * dt);
                CurrentThickness01 = 0f;
                CurrentThicknessMeters = 0f;
                ChargeProgress01 = 0f;
                _chargeTimer = 0f;
                _hasChargedThisGrip = false;
                SetState(TurretState.Idle);
                return;
            }

            var chargeRequired = tuning.chargeToFireEnabled && tuning.chargeToFireSeconds > 0f;
            if (!chargeRequired)
            {
                // チャージ発射がOFFなら、握った瞬間から発射する（握力に応じて太さが変わる）。
                _hasChargedThisGrip = true;
            }
            else if (!_hasChargedThisGrip)
            {
                _chargeTimer = grip >= tuning.chargeGripThreshold01 ? _chargeTimer + dt : 0f;
                ChargeProgress01 = tuning.chargeToFireSeconds > 0f
                    ? Mathf.Clamp01(_chargeTimer / tuning.chargeToFireSeconds)
                    : 1f;

                if (_chargeTimer >= tuning.chargeToFireSeconds)
                {
                    _hasChargedThisGrip = true;
                }
            }

            if (_hasChargedThisGrip)
            {
                ChargeProgress01 = 1f;
                Heat += grip * grip * tuning.heatGainPerSecond * dt;
                CurrentThickness01 = tuning.EvaluateThickness01(grip);
                CurrentThicknessMeters = tuning.EvaluateThicknessMeters(grip);
                SetState(TurretState.Firing);
            }
            else
            {
                CurrentThickness01 = 0f;
                CurrentThicknessMeters = 0f;
                SetState(TurretState.Charging);
            }

            if (Heat >= tuning.overheatThreshold)
            {
                EnterOverheated();
            }
        }

        /// <summary>状態と、射出中のレーザーの強さに応じた倍率へ滑らかに追従しながら、砲台を回転させる。</summary>
        private void UpdateRotation(float dt)
        {
            var targetMultiplier = autoRotate ? ResolveTargetRotationMultiplier() : 0f;
            var blend = tuning.rotationResponse <= 0f ? 1f : 1f - Mathf.Exp(-tuning.rotationResponse * dt);
            CurrentRotationMultiplier = Mathf.Lerp(CurrentRotationMultiplier, targetMultiplier, blend);
            CurrentRotationSpeed = tuning.rotationSpeedDegPerSec * rotationSpeedScale * CurrentRotationMultiplier;

            // Unityの回転は反時計回りが正。時計回りは負の向きに回す。
            var sign = rotationDirection == TurretRotationDirection.Clockwise ? -1f : 1f;
            transform.Rotate(0f, 0f, sign * CurrentRotationSpeed * dt);
        }

        private float ResolveTargetRotationMultiplier()
        {
            switch (State)
            {
                case TurretState.Firing:
                    return tuning.EvaluateFiringRotationMultiplier(CurrentThickness01);
                case TurretState.Charging:
                    return tuning.chargingRotationMultiplier;
                case TurretState.Overheated:
                    return tuning.overheatedRotationMultiplier;
                default:
                    return 1f;
            }
        }

        private void TickOverheated(float dt)
        {
            _silenceRemaining -= dt;
            Heat = Mathf.Max(0f, Heat - tuning.heatCoolingPerSecond * dt);
            CurrentThickness01 = 0f;
            CurrentThicknessMeters = 0f;

            if (_silenceRemaining <= 0f)
            {
                SetState(TurretState.Idle);
            }
        }

        private void EnterOverheated()
        {
            _silenceRemaining = tuning.overheatSilenceDuration;
            CurrentThickness01 = 0f;
            CurrentThicknessMeters = 0f;
            ChargeProgress01 = 0f;
            _chargeTimer = 0f;
            _hasChargedThisGrip = false;
            SetState(TurretState.Overheated);
        }

        private void SetState(TurretState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            OnStateChanged?.Invoke(State);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = FortressColors.PlayerColor(playerIndex);
            var origin = MuzzlePosition;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            var range = tuning != null ? tuning.range : 5f;
            Gizmos.DrawLine(origin, origin + transform.up * range);
        }
    }
}
