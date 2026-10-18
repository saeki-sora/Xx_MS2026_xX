using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯磨き粉ビームの見た目の強化（<see cref="LaserTuningConfig.look"/>）。<see cref="LaserTurret"/> が起動時に自動で付けるので、
    /// シーンに手で置く必要はない。ビームのシェーダー（MS2026/Fortress/ToothpasteBeam）へ、流れ・強さ・長さ・太さを渡す（<see cref="BeamGelDriver"/>）。
    /// 太さ・状態はネット対戦のClientにもHostから届いているので、通信を増やさずに全員の画面で同じように見える。
    /// 既存の演出（<see cref="LaserTurretEffects"/> の発射口・着弾点・湯気、カメラの揺れ、ビームのうねり・光の層）には触れない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserTurretLook : MonoBehaviour
    {
        private LaserTurret _turret;
        private BeamGelDriver _beam;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();
            _beam = new BeamGelDriver(GetComponent<LaserBeamVisual>());
        }

        private void LateUpdate()
        {
            var look = _turret.tuning != null ? _turret.tuning.look : null;
            if (look == null || !look.enabled)
            {
                _beam.Clear();
                return;
            }

            // LaserBeamVisual(Update)がこのフレームの線を作った後に読む。
            _beam.Update(look, _turret, Time.deltaTime);
        }
    }
}
