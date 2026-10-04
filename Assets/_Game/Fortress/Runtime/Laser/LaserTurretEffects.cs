using System.Collections.Generic;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Vfx;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザーの演出（<see cref="LaserTuningConfig.effects"/>）を鳴らす。<see cref="LaserTurret"/> が起動時に自動で付けるので、
    /// シーンに手で置く必要はない。
    /// ・発射口: 撃ち始めた瞬間に効果音、撃っている間エフェクトを発射口に出し続ける（状態の変化で判断）。
    /// ・着弾点: レーザーが何かに当たっている間エフェクトを当たった場所に出し続け、当たり始めに効果音（<see cref="LaserBeamVisual"/> の当たり判定）。
    /// ・群衆の敵に当たった瞬間: <see cref="SwarmSystem.BeamHitsReported"/> のうち自分のレーザーの分を1回ずつ出す。
    /// ネット対戦では、Clientの砲台もHostから届いた状態で状態変化が起き、当たり判定（見た目用）と群衆の判定も各PCで行うので、
    /// 通信を増やさずに全員の画面に出る。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserTurret))]
    public sealed class LaserTurretEffects : MonoBehaviour
    {
        private LaserTurret _turret;
        private LaserBeamVisual _beam;
        private SwarmSystem _swarm;
        private Transform _impactPoint;
        private Handle<VfxMarker> _muzzleVfx = Handle<VfxMarker>.Invalid;
        private Handle<VfxMarker> _impactVfx = Handle<VfxMarker>.Invalid;
        private bool _impactActive;

        private LaserEffectSettings Settings => _turret != null && _turret.tuning != null ? _turret.tuning.effects : null;

        private void Awake()
        {
            _turret = GetComponent<LaserTurret>();
            _beam = GetComponent<LaserBeamVisual>();
        }

        private void OnEnable()
        {
            _turret.OnStateChanged += HandleStateChanged;
            if (_turret.IsFiring)
            {
                StartMuzzle(false);
            }
        }

        private void OnDisable()
        {
            _turret.OnStateChanged -= HandleStateChanged;
            SetSwarm(null);
            FortressEffectPlayer.Stop(ref _muzzleVfx);
            StopImpact();
        }

        private void OnDestroy()
        {
            if (_impactPoint != null)
            {
                Destroy(_impactPoint.gameObject);
            }
        }

        private void LateUpdate()
        {
            // 群衆の「当たった瞬間」は、演出が設定されているときだけ受け取る（誰も受け取らなければ群衆側は記録自体をしない）。
            var swarmHit = Settings?.swarmHit;
            var wanted = swarmHit != null && !swarmHit.IsEmpty ? SwarmSystem.Current : null;
            if (_swarm != wanted)
            {
                SetSwarm(wanted);
            }

            // LaserBeamVisual(Update)がこのフレームの当たり判定を済ませた後に読む。
            UpdateImpact();
        }

        private void HandleStateChanged(TurretState state)
        {
            if (state == TurretState.Firing)
            {
                StartMuzzle(true);
            }
            else
            {
                FortressEffectPlayer.Stop(ref _muzzleVfx);
            }
        }

        private void StartMuzzle(bool playSound)
        {
            var effect = Settings?.muzzle;
            if (effect == null)
            {
                return;
            }

            var anchor = _turret.muzzle != null ? _turret.muzzle : transform;
            FortressEffectPlayer.Stop(ref _muzzleVfx);
            _muzzleVfx = FortressEffectPlayer.StartFollowing(effect, anchor, _turret.playerIndex);
            if (playSound)
            {
                FortressEffectPlayer.PlaySound(effect, anchor.position);
            }
        }

        private void UpdateImpact()
        {
            var effect = Settings?.impact;
            var hasImpact = effect != null && !effect.IsEmpty && _beam != null && _turret.IsFiring && _beam.HasImpact;
            if (!hasImpact)
            {
                StopImpact();
                return;
            }

            var point = EnsureImpactPoint();
            point.SetPositionAndRotation(_beam.ImpactPoint, FortressEffectPlayer.RotationFacing(_beam.ImpactNormal));

            if (_impactActive)
            {
                return;
            }

            _impactActive = true;
            _impactVfx = FortressEffectPlayer.StartFollowing(effect, point, _turret.playerIndex);
            FortressEffectPlayer.PlaySound(effect, point.position);
        }

        private void StopImpact()
        {
            if (!_impactActive)
            {
                return;
            }

            _impactActive = false;
            FortressEffectPlayer.Stop(ref _impactVfx);
        }

        private Transform EnsureImpactPoint()
        {
            if (_impactPoint == null)
            {
                // エフェクトが追従する目印。シーンには保存しない。
                var go = new GameObject($"[LaserImpact] P{_turret.playerIndex + 1}") { hideFlags = HideFlags.DontSave };
                _impactPoint = go.transform;
            }

            return _impactPoint;
        }

        private void SetSwarm(SwarmSystem swarm)
        {
            if (_swarm != null)
            {
                _swarm.BeamHitsReported -= HandleSwarmHits;
            }

            _swarm = swarm;
            if (_swarm != null)
            {
                _swarm.BeamHitsReported += HandleSwarmHits;
            }
        }

        private void HandleSwarmHits(IReadOnlyList<SwarmBeamHit> hits)
        {
            var effect = Settings?.swarmHit;
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            var origin = _turret.MuzzlePosition;
            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit.owner != _turret.playerIndex)
                {
                    continue;
                }

                var position = new Vector3(hit.position.x, hit.position.y, origin.z);
                // エフェクトの上方向(Y+)を砲台の方へ向ける（火花が撃たれた側へ散る向き）。
                var rotation = FortressEffectPlayer.RotationFacing((Vector2)(origin - position));
                FortressEffectPlayer.PlayOnce(effect, position, rotation, _turret.playerIndex);
            }
        }
    }
}
