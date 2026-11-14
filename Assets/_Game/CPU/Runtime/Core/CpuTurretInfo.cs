using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 自分の砲台の今の様子を、行動が使いやすい形で読む（人が自分の画面で見て分かること：砲身の向き・回る向き・熱のゲージ）。
    /// </summary>
    public readonly struct CpuTurretInfo
    {
        public readonly bool Valid;

        /// <summary>今、握れば撃てる（歯みがきタイム・オーバーヒート中・倒れている（差し込みがあるとき）でない）。</summary>
        public readonly bool CanFire;

        public readonly bool IsFiring;
        public readonly bool IsOverheated;

        /// <summary>撃つ前のチャージが要る設定か。</summary>
        public readonly bool NeedsCharge;

        /// <summary>チャージで握り続ける強さ。</summary>
        public readonly float ChargeGrip;

        /// <summary>熱の割合（0〜1。1 でオーバーヒート）。</summary>
        public readonly float Heat01;

        /// <summary>砲身の向き（度。<see cref="CpuSectorMath"/> の決まり）。</summary>
        public readonly float AimDegrees;

        /// <summary>回る向き（反時計回り +1、時計回り -1、回らない 0）。</summary>
        public readonly float RotationSign;

        /// <summary>撃っていないときに回る速さ（度/秒）。</summary>
        public readonly float IdleRotationSpeed;

        private CpuTurretInfo(bool canFire, bool isFiring, bool isOverheated, bool needsCharge, float chargeGrip, float heat01,
            float aimDegrees, float rotationSign, float idleRotationSpeed)
        {
            Valid = true;
            CanFire = canFire;
            IsFiring = isFiring;
            IsOverheated = isOverheated;
            NeedsCharge = needsCharge;
            ChargeGrip = chargeGrip;
            Heat01 = heat01;
            AimDegrees = aimDegrees;
            RotationSign = rotationSign;
            IdleRotationSpeed = idleRotationSpeed;
        }

        /// <summary>テスト・試し用に、値を直接入れて作る。</summary>
        public static CpuTurretInfo ForTest(bool canFire, bool isFiring, bool isOverheated, bool needsCharge, float chargeGrip,
            float heat01, float aimDegrees, float rotationSign, float idleRotationSpeed)
        {
            return new CpuTurretInfo(canFire, isFiring, isOverheated, needsCharge, chargeGrip, heat01, aimDegrees, rotationSign,
                idleRotationSpeed);
        }

        public CpuTurretInfo(LaserTurret turret)
        {
            Valid = turret != null && turret.tuning != null;
            if (!Valid)
            {
                this = default;
                return;
            }

            var tuning = turret.tuning;
            IsFiring = turret.IsFiring;
            IsOverheated = turret.State == TurretState.Overheated;
            // 倒れている（実験中の要素）は、差し込み口の「仲間の調子」で知る（本体は倒れる仕組みを直接見ない）。
            var downed = CpuPlugins.TeammateStatus != null &&
                         CpuPlugins.TeammateStatus.GetCondition(turret.playerIndex) == CpuTeammateCondition.Downed;
            CanFire = !IsOverheated && !LaserTurret.InputLocked && !downed && turret.isActiveAndEnabled;
            NeedsCharge = tuning.chargeToFireEnabled;
            ChargeGrip = Mathf.Clamp01(tuning.chargeGripThreshold01 + 0.03f);
            Heat01 = turret.HeatRatio01;
            AimDegrees = CpuSectorMath.AngleOf(turret.AimDirection);
            RotationSign = !turret.autoRotate ? 0f : turret.rotationDirection == TurretRotationDirection.Clockwise ? -1f : 1f;
            IdleRotationSpeed = Mathf.Abs(tuning.rotationSpeedDegPerSec * turret.rotationSpeedScale);
        }
    }
}
