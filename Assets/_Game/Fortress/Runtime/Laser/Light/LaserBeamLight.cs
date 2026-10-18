using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ビームがまわりをプレイヤー色で照らす（<see cref="LaserTuningConfig.beamLight"/>）。<see cref="LaserTurret"/> が起動時に自動で付けるので、
    /// シーンに手で置く必要はない。
    /// ビームの線分・色・明るさを <see cref="BeamLightGlobals"/> でシェーダーへ渡すだけで、ライトのオブジェクトは作らない。
    /// ・床・背景の物（ステージの MS2026/Stage/Prop）: ビームの線分そのものを光源にして、線に沿って途切れなく照らす。
    /// ・群衆の敵（SwarmSprite）: ビームの近くの敵を明るくする。
    /// 撃っているか・太さはネット対戦のClientにもHostから届いているので、通信を増やさずに全員の画面で同じように見える。
    /// ビームの線・うねり・当たり判定・既存の演出には触れない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserBeamLight : MonoBehaviour
    {
        private LaserTurret _turret;
        private LaserBeamVisual _beam;
        private float _fade;
        private Vector3 _start;
        private Vector3 _end;
        private bool _hasImpact;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();
            _beam = GetComponent<LaserBeamVisual>();
        }

        private void OnDisable()
        {
            _fade = 0f;
            BeamLightGlobals.Clear(_turret.playerIndex);
        }

        private void LateUpdate()
        {
            var settings = _turret.tuning != null ? _turret.tuning.beamLight : null;
            if (settings == null || !settings.enabled)
            {
                _fade = 0f;
                BeamLightGlobals.Clear(_turret.playerIndex);
                return;
            }

            // LaserBeamVisual(Update)がこのフレームの線を作った後に読む。撃ち終わった後は最後の位置のまま消えていく。
            var line = _beam != null ? _beam.lineRenderer : null;
            var firing = _turret.IsFiring && line != null && line.enabled && line.positionCount >= 2;
            if (firing)
            {
                _start = line.GetPosition(0);
                _end = line.GetPosition(line.positionCount - 1);
                _hasImpact = _beam.HasImpact;
            }

            _fade = Mathf.MoveTowards(_fade, firing ? 1f : 0f, settings.fadeSpeed * Time.deltaTime);
            if (_fade <= 0.001f)
            {
                BeamLightGlobals.Clear(_turret.playerIndex);
                return;
            }

            var color = LaserLightMath.Saturate(FortressColors.PlayerColor(_turret.playerIndex), settings.colorSaturation);
            var brightness = LaserLightMath.Brightness(_turret.CurrentThickness01, settings.weakGripBrightness) * _fade;
            var floor = color * (settings.floorIntensity * brightness);
            BeamLightGlobals.Set(_turret.playerIndex, new BeamLightGlobals.Beam
            {
                start = _start,
                end = _end,
                enemyColor = color * (settings.enemyStrength * brightness),
                enemyRadius = settings.enemyRadius,
                floorColor = floor,
                floorRange = settings.floorRange,
                floorHeight = settings.lightHeight,
                impactExtra = _hasImpact ? Mathf.Max(0f, settings.impactBoost - 1f) : 0f,
                surfaceColorInfluence = settings.surfaceColorInfluence,
                enemyColorOverlay = settings.enemyColorOverlay
            });
        }
    }
}
