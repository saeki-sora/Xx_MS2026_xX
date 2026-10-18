using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ビームがまわりをプレイヤー色で照らす（<see cref="LaserTuningConfig.beamLight"/>）。<see cref="LaserTurret"/> が起動時に自動で付けるので、
    /// シーンに手で置く必要はない。
    /// ・床・背景の物: ビームに沿って本物の点光源を数個置く（URP の Forward+ なので光が増えても描画の仕組みは変わらない）。影は落とさない。
    /// ・群衆の敵: <see cref="BeamLightGlobals"/> でビームの線分をシェーダーへ渡し、近くの敵を明るくする。
    /// 撃っているか・太さはネット対戦のClientにもHostから届いているので、通信を増やさずに全員の画面で同じように見える。
    /// ビームの線・うねり・当たり判定・既存の演出には触れない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserBeamLight : MonoBehaviour
    {
        private LaserTurret _turret;
        private LaserBeamVisual _beam;
        private readonly Light[] _lights = new Light[LaserLightMath.MaxLightsPerBeam];
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
            HideLights(0);
            BeamLightGlobals.Clear(_turret.playerIndex);
        }

        private void LateUpdate()
        {
            var settings = _turret.tuning != null ? _turret.tuning.beamLight : null;
            if (settings == null || !settings.enabled)
            {
                _fade = 0f;
                HideLights(0);
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
                HideLights(0);
                BeamLightGlobals.Clear(_turret.playerIndex);
                return;
            }

            var color = FortressColors.PlayerColor(_turret.playerIndex);
            var brightness = LaserLightMath.Brightness(_turret.CurrentThickness01, settings.weakGripBrightness) * _fade;

            UpdateFloorLights(settings, color, brightness);

            var enemyColor = color * (settings.enemyStrength * brightness);
            BeamLightGlobals.Set(_turret.playerIndex, _start, _end, enemyColor, settings.enemyRadius);
        }

        private void UpdateFloorLights(LaserLightSettings settings, Color color, float brightness)
        {
            if (settings.floorIntensity <= 0f)
            {
                HideLights(0);
                return;
            }

            var length = Vector2.Distance(_start, _end);
            var count = LaserLightMath.LightCount(length, settings.lightSpacing);
            for (var i = 0; i < count; i++)
            {
                var light = EnsureLight(i);
                var t = LaserLightMath.LightPosition01(i, count);
                var position = Vector3.Lerp(_start, _end, t);
                // カメラは +Z を向いているので、床から「上」は -Z。
                position.z -= settings.lightHeight;
                light.transform.position = position;
                light.color = color;
                light.range = settings.floorRange;
                var boost = i == count - 1 && _hasImpact ? settings.impactBoost : 1f;
                light.intensity = settings.floorIntensity * brightness * boost;
                light.enabled = true;
            }

            HideLights(count);
        }

        private Light EnsureLight(int index)
        {
            var light = _lights[index];
            if (light != null)
            {
                return light;
            }

            // 砲台の子にしておけば、砲台と一緒に消える（Play中のシーンの物なので、Playを止めれば必ず消える）。
            var go = new GameObject($"[BeamLight] P{_turret.playerIndex + 1}-{index}");
            go.transform.SetParent(transform, false);
            light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = false;
            _lights[index] = light;
            return light;
        }

        private void HideLights(int fromIndex)
        {
            for (var i = fromIndex; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].enabled = false;
                }
            }
        }
    }
}
