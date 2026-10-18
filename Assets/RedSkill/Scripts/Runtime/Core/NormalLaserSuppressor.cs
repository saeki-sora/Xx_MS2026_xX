using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills
{
    /// <summary>
    /// 砲台の普段のレーザー（<see cref="LaserBeamVisual"/>）と照準線（<see cref="TurretAimIndicator"/>）を隠す係。
    /// 既存のコードには手を入れず、コンポーネントを一時的に止めるだけで実現している
    /// （レーザーを止めている間は、普段のレーザーのダメージ・着弾演出も出ない）。
    /// </summary>
    internal sealed class NormalLaserSuppressor
    {
        private readonly LaserTurret _turret;
        private LaserBeamVisual _beam;
        private TurretAimIndicator _aim;
        private bool _beamWasDisabledByUs;
        private bool _aimWasDisabledByUs;

        public NormalLaserSuppressor(LaserTurret turret)
        {
            _turret = turret;
        }

        /// <summary>普段のレーザーを止めているか。</summary>
        public bool IsBeamHidden { get; private set; }

        /// <summary>照準線を止めているか。</summary>
        public bool IsAimHidden { get; private set; }

        /// <summary>普段のレーザー・照準線を、それぞれ隠す／元に戻す。</summary>
        public void Apply(bool hideBeam, bool hideAim)
        {
            if (_turret == null)
            {
                return;
            }

            if (_beam == null)
            {
                _beam = _turret.GetComponent<LaserBeamVisual>();
            }

            if (_aim == null)
            {
                _aim = _turret.GetComponent<TurretAimIndicator>();
            }

            if (hideBeam != IsBeamHidden)
            {
                IsBeamHidden = hideBeam;
                if (hideBeam)
                {
                    if (_beam != null && _beam.enabled)
                    {
                        _beam.enabled = false;
                        _beamWasDisabledByUs = true;
                    }

                    HideLines(true, false);
                }
                else
                {
                    if (_beam != null && _beamWasDisabledByUs)
                    {
                        _beam.enabled = true;
                    }

                    _beamWasDisabledByUs = false;
                }
            }

            if (hideAim != IsAimHidden)
            {
                IsAimHidden = hideAim;
                if (hideAim)
                {
                    if (_aim != null && _aim.enabled)
                    {
                        _aim.enabled = false;
                        _aimWasDisabledByUs = true;
                    }

                    HideLines(false, true);
                }
                else
                {
                    if (_aim != null && _aimWasDisabledByUs)
                    {
                        _aim.enabled = true;
                    }

                    _aimWasDisabledByUs = false;
                }
            }
        }

        /// <summary>全部元に戻す。</summary>
        public void Release()
        {
            Apply(false, false);
        }

        /// <summary>止めたコンポーネントは線を消さないので、残った線をこちらで消す（ビームの光・照準線の子も含む）。</summary>
        private void HideLines(bool beam, bool aim)
        {
            if (beam && _beam != null && _beam.lineRenderer != null)
            {
                _beam.lineRenderer.enabled = false;
            }

            var lines = _turret.GetComponentsInChildren<LineRenderer>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line == null)
                {
                    continue;
                }

                // LaserBeamVisual が作る光の線（BeamGlow0..）と TurretAimIndicator が作る照準線（AimIndicator）。
                var objectName = line.gameObject.name;
                if ((beam && objectName.StartsWith("BeamGlow")) || (aim && objectName == "AimIndicator"))
                {
                    line.enabled = false;
                }
            }
        }
    }
}
