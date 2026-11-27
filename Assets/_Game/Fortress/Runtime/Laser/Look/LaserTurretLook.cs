using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯磨き粉ビームの見た目の強化（<see cref="LaserTuningConfig.look"/>）と、熱の見せ方（<see cref="LaserTuningConfig.heatLook"/>、2026-11-27 案A）。
    /// <see cref="LaserTurret"/> が起動時に自動で付けるので、シーンに手で置く必要はない。
    /// ビームのシェーダー（MS2026/Fortress/ToothpasteBeam）へ、流れ・強さ・長さ・太さ・芯の熱の色を渡す（<see cref="BeamGelDriver"/>）。
    /// 砲台のまわりの熱の輪（<see cref="TurretHeatRing"/>）もここで動かす（芯と輪の点滅をそろえるため）。
    /// 太さ・状態・熱はネット対戦のClientにもHostから届いているので、通信を増やさずに全員の画面で同じように見える。
    /// 既存の演出（<see cref="LaserTurretEffects"/> の発射口・着弾点・湯気、カメラの揺れ、ビームのうねり・光の層）には触れない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserTurretLook : MonoBehaviour
    {
        private LaserTurret _turret;
        private BeamGelDriver _beam;
        private TurretHeatRing _ring;
        private float _blinkPhase;
        private float _silenceRemaining;
        private TurretState _lastState = TurretState.Idle;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();
            _beam = new BeamGelDriver(GetComponent<LaserBeamVisual>());
            _ring = new TurretHeatRing(transform);
        }

        private void OnDisable()
        {
            _ring?.Hide();
        }

        private void OnDestroy()
        {
            _ring?.Destroy();
            _ring = null;
        }

        private void LateUpdate()
        {
            var tuning = _turret.tuning;
            var dt = Time.deltaTime;
            var heatLook = UpdateHeat(tuning, dt);

            var look = tuning != null ? tuning.look : null;
            if (look == null || !look.enabled)
            {
                _beam.Clear();
                return;
            }

            // LaserBeamVisual(Update)がこのフレームの線を作った後に読む。
            _beam.Update(look, _turret, dt, heatLook);
        }

        private BeamHeatLook UpdateHeat(LaserTuningConfig tuning, float dt)
        {
            var settings = tuning != null ? tuning.heatLook : null;
            var state = _turret.State;
            var silenceDuration = tuning != null ? tuning.overheatSilenceDuration : 0f;

            // オーバーヒートで止まっている残り時間（Client には届かないので、各PCで入った瞬間から数える）。
            if (state == TurretState.Overheated && _lastState != TurretState.Overheated)
            {
                _silenceRemaining = silenceDuration;
            }

            _silenceRemaining = state == TurretState.Overheated ? Mathf.Max(0f, _silenceRemaining - dt) : 0f;
            _lastState = state;

            if (settings == null || !settings.enabled)
            {
                _blinkPhase = 0f;
                _ring.Hide();
                return BeamHeatLook.None;
            }

            var heat01 = _turret.HeatRatio01;
            var overheated = state == TurretState.Overheated;
            var warnAt = tuning.effects != null ? tuning.effects.heatWarnAt : 0.8f;
            _blinkPhase = LaserHeatLookMath.AdvancePhase(_blinkPhase, dt, overheated ? 0f : heat01, warnAt,
                settings.blinkPerSecondStart, settings.blinkPerSecondEnd);
            var blink = overheated ? 1f : LaserHeatLookMath.Blink(_blinkPhase, heat01, warnAt, settings.blinkDepth);

            _ring.Update(settings, transform.position, heat01, overheated, _silenceRemaining, silenceDuration, blink, dt);

            var tint = settings.coreColors.Evaluate(heat01);
            tint.a = settings.coreTintStrength;
            return new BeamHeatLook(tint, LaserHeatLookMath.CoreWiden(heat01, settings.coreWidenAtFull), blink);
        }
    }
}
