using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 握力を太さ・熱に変換する砲台の本体ロジック。見た目は持たず、状態だけを管理する
    /// （見た目は<see cref="LaserBeamVisual"/>など別コンポーネントが担当し、差し替え可能にする）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaserTurret : MonoBehaviour
    {
        [Tooltip("0-3。GripInputBridgeが返す握力値のプレイヤー番号に対応する。")]
        [Range(0, 3)]
        public int playerIndex;

        [Tooltip("この砲台の太さ・熱・射程などのチューニングデータ。")]
        public LaserTuningConfig tuning;

        /// <summary>状態(Idle/Firing/Overheated)が変化した際に発火する。</summary>
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

        private float _silenceRemaining;

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
                return;
            }

            var provider = global::MS2026.GripInputBridge.GripInputBridge.Provider;
            var isGripping = provider.IsGripping(playerIndex);
            var grip = provider.GetGripValue(playerIndex);

            if (isGripping)
            {
                Heat += grip * grip * tuning.heatGainPerSecond * dt;
                CurrentThickness01 = tuning.EvaluateThickness01(grip);
                CurrentThicknessMeters = tuning.EvaluateThicknessMeters(grip);
                SetState(TurretState.Firing);
            }
            else
            {
                Heat = Mathf.Max(0f, Heat - tuning.heatCoolingPerSecond * dt);
                CurrentThickness01 = 0f;
                CurrentThicknessMeters = 0f;
                SetState(TurretState.Idle);
            }

            if (Heat >= tuning.overheatThreshold)
            {
                EnterOverheated();
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
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            var range = tuning != null ? tuning.range : 5f;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * range);
        }
    }
}
