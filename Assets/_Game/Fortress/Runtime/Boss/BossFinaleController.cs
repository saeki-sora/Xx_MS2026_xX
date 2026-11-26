using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボスのとどめ（2026-11-25 ユーザー決定: 見本 E）。部位を全部壊した（BossFinale の段階）ら <see cref="BossWaveRunner"/> が作る。
    /// 1. 「全員で握れ！」: 4人のゲージを溜める（失敗しない。握らなくても <see cref="BossDefinition.gaugeMaxSeconds"/> で満タン）。
    ///    この間は撃てない（<see cref="LaserTurret.InputLocked"/>）。握りは <see cref="LaserTurret.ReadGrip"/> で読む。残った雑魚は逃げ、弾ははじける。
    /// 2. 全員満タン → 4本のレーザーが前の1点に集まって光の玉 → 4色を束ねた太い1本がボスへ。伸びている間からカメラがゆっくりボスへ寄る。
    /// 3. 当たった瞬間: 止め（ヒットストップ）＋白→暗→白の点滅 ＋ 画面の揺れ。スローで閃光 → 爆炎 → 黒煙。
    /// 4. カメラがなめらかに4人とコアを映す絵へ戻り、少し見せてから終わり（試合のクリアへ）。
    /// ゲージを溜めるのと「終わった」を決めるのは Host（オフラインでは自分）。演出は各PCが自分で出す（同期は後の段階）。
    /// 時間は実時間で数える（止め・スローでゲームの時間を変えるため）。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(109)]
    internal sealed class BossFinaleController : MonoBehaviour
    {
        private const float BeamZOffset = -0.12f;

        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private readonly bool[] _full = new bool[4];

        private BossDefinition _def;
        private BossActor _actor;
        private BossFormationLayout _layout;
        private bool _authority;
        private float _mergeStart = -1f;
        private bool _zoomed;
        private bool _hit;
        private bool _returned;
        private bool _timeScaleOwned;
        private BossFinaleBeams _beams;

        public static BossFinaleController Create(BossDefinition def, BossActor actor, BossFormationLayout layout, bool authority)
        {
            var go = new GameObject("[Boss] Finale");
            var controller = go.AddComponent<BossFinaleController>();
            controller.Setup(def, actor, layout, authority);
            return controller;
        }

        private void Setup(BossDefinition def, BossActor actor, BossFormationLayout layout, bool authority)
        {
            _def = def;
            _actor = actor;
            _layout = layout;
            _authority = authority;
            System.Array.Clear(FortressBoss.Gauges, 0, FortressBoss.Gauges.Length);
            FortressBoss.FinaleStage = BossFinaleStage.Gauges;
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret != null && turret.playerIndex >= 0 && turret.playerIndex < 4)
                {
                    _turrets.Add(turret);
                    turret.Sweep = default; // 扇形の往復をやめ、ボスの方を向く
                }
            }

            _turrets.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            LaserTurret.InputLocked = true;

            // 残った雑魚は逃げる（Host が動かす）。
            if (_authority)
            {
                var plan = FortressWaves.Plan;
                WaveRetreat.Begin(plan != null ? plan.retreatSpeedMultiplier : 2.4f);
            }

            WaveRetreat.SetRetreatLook(true);
            Play(def.onFinaleStart, layout.core);
        }

        private void OnDestroy()
        {
            RestoreTimeScale();
            if (_beams != null)
            {
                Destroy(_beams.gameObject);
            }

            FortressBoss.FinaleFlash = 0f;
            FortressBoss.FinaleDim = 0f;
            if (FortressWaves.Phase != WavePhase.BossFinale)
            {
                WaveRetreat.SetRetreatLook(false);
            }
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            LaserTurret.InputLocked = true;
            if (_mergeStart < 0f)
            {
                TickGauges(dt);
                AimTurrets(BossTarget(), dt);
                return;
            }

            var t = Time.unscaledTime - _mergeStart;
            var stage = BossFinaleMath.StageAt(t, _def);
            FortressBoss.FinaleStage = stage;
            AimTurrets(t < 0.8f ? MergePoint() : BossTarget(), dt);
            if (_beams != null)
            {
                _beams.Show(t, MuzzlePoints(), MergePoint(), BossTarget());
            }

            ApplyTimeScale(t);
            var sinceHit = t - BossDefinition.MergeToHitSeconds;
            FortressBoss.FinaleFlash = BossFinaleMath.FlashAt(sinceHit, _def.finaleFlashStrength);
            FortressBoss.FinaleDim = BossFinaleMath.DimAt(sinceHit, _def);

            if (!_zoomed && t >= BossFinaleMath.ZoomInStart)
            {
                _zoomed = true;
                SetView(BossView(), BossFinaleMath.ZoomInSeconds);
            }

            if (!_hit && t >= BossDefinition.MergeToHitSeconds)
            {
                _hit = true;
                Play(_def.onFinaleHit, BossTarget());
                _actor?.Explode(_def.finaleShakeStrength);
            }

            if (!_returned && (stage == BossFinaleStage.Return || stage >= BossFinaleStage.Hold))
            {
                _returned = true;
                SetView(PlayersView(), _def.finaleReturnSeconds);
            }

            if (stage == BossFinaleStage.Done)
            {
                RestoreTimeScale();
                if (_authority && !FortressBoss.FinaleDone)
                {
                    FortressBoss.FinaleDone = true;
                    WaveRetreat.Finish();
                }
            }
        }

        // ───────── ゲージ ─────────

        private void TickGauges(float dt)
        {
            if (_authority)
            {
                var mask = FortressWaves.PlayerMask;
                foreach (var turret in _turrets)
                {
                    var p = turret.playerIndex;
                    float grip;
                    if ((mask & (1 << p)) == 0)
                    {
                        grip = 1f; // 試合にいない席（開発中だけ）は、待たせないように満タンの速さで溜める
                    }
                    else
                    {
                        turret.ReadGrip(out var gripping, out var value);
                        grip = gripping ? Mathf.Clamp01(value) : 0f;
                    }

                    FortressBoss.Gauges[p] = Mathf.Min(1f, FortressBoss.Gauges[p] + BossFinaleMath.GaugeRate(grip, _def.gaugeMaxSeconds, _def.gaugeFullGripSeconds) * dt);
                }
            }

            var all = true;
            for (var p = 0; p < 4; p++)
            {
                var full = FortressBoss.Gauges[p] >= 1f;
                if (full && !_full[p])
                {
                    _full[p] = true;
                    var turret = TurretOf(p);
                    if (turret != null && _def.onGaugeFull != null && !_def.onGaugeFull.IsEmpty)
                    {
                        FortressEffectPlayer.PlayOnce(_def.onGaugeFull, turret.transform.position, Quaternion.identity, p);
                    }

                    if (turret != null)
                    {
                        BossPuff.Spawn(turret.transform.position, 0.5f, 2.2f, 0.4f, FortressColors.PlayerColor(p));
                    }
                }

                all &= full;
            }

            if (all)
            {
                BeginMerge();
            }
        }

        private void BeginMerge()
        {
            _mergeStart = Time.unscaledTime;
            FortressBoss.FinaleStage = BossFinaleStage.Merge;
            Play(_def.onMerge, MergePoint());
            _beams = BossFinaleBeams.Create(BeamZ(), _layout.bossRadius);
        }

        // ───────── カメラ・時間 ─────────

        private void SetView(CameraViewSettings view, float seconds)
        {
            var rig = FortressCameraRig.Active;
            if (rig != null)
            {
                rig.SetViewOverride(view, seconds);
            }
        }

        private CameraViewSettings BaseView() => FortressBoss.View.distance > 0f || FortressBoss.View.orthographicSize > 0f
            ? FortressBoss.View
            : FortressCameraRig.Active != null ? FortressCameraRig.Active.CurrentView : CameraViewSettings.Default;

        private CameraViewSettings BossView()
        {
            var view = BaseView();
            var target = BossTarget();
            view.center = new Vector2(target.x + _layout.bossRadius * 0.15f, target.y);
            return view.WithVisibleHalfHeight(view.VisibleHalfHeight / Mathf.Max(1f, _def.finaleZoom));
        }

        private CameraViewSettings PlayersView()
        {
            var view = BaseView();
            var column = _layout.turrets != null && _layout.turrets.Length > 0 ? _layout.turrets[0].x : _layout.core.x;
            view.center = new Vector2((_layout.core.x + column) * 0.5f + _layout.bossRadius * 0.4f, _layout.area.center.y);
            return view.WithVisibleHalfHeight(view.VisibleHalfHeight / Mathf.Max(1f, _def.finalePlayersZoom));
        }

        private void ApplyTimeScale(float t)
        {
            var scale = BossFinaleMath.TimeScaleAt(t, _def);
            if (scale < 0.999f || _timeScaleOwned)
            {
                _timeScaleOwned = true;
                Time.timeScale = scale;
                if (scale >= 0.999f)
                {
                    RestoreTimeScale();
                }
            }
        }

        private void RestoreTimeScale()
        {
            if (!_timeScaleOwned)
            {
                return;
            }

            _timeScaleOwned = false;
            Time.timeScale = 1f;
        }

        // ───────── 場所 ─────────

        private Vector2 BossTarget()
        {
            if (_actor != null && _actor.gameObject.activeInHierarchy)
            {
                return _actor.Position + Vector2.left * _layout.bossRadius * 0.3f;
            }

            return _layout.bossStop;
        }

        private Vector2 MergePoint()
        {
            var column = _layout.turrets != null && _layout.turrets.Length > 0 ? _layout.turrets[0].x : _layout.core.x;
            var boss = BossTarget();
            return new Vector2(Mathf.Lerp(column, boss.x, _def.mergePoint01), _layout.area.center.y);
        }

        private Vector2[] MuzzlePoints()
        {
            var points = new Vector2[4];
            for (var p = 0; p < 4; p++)
            {
                var turret = TurretOf(p);
                points[p] = turret != null ? (Vector2)turret.MuzzlePosition : _layout.turrets[p];
            }

            return points;
        }

        private float BeamZ() => (_turrets.Count > 0 ? _turrets[0].transform.position.z : 0f) + BeamZOffset;

        private LaserTurret TurretOf(int player)
        {
            foreach (var turret in _turrets)
            {
                if (turret != null && turret.playerIndex == player)
                {
                    return turret;
                }
            }

            return null;
        }

        // 砲台をなめらかに目標へ向ける（Host の向きは Client へも届く）。
        private void AimTurrets(Vector2 target, float dt)
        {
            foreach (var turret in _turrets)
            {
                if (turret == null || turret.IsReplica)
                {
                    continue;
                }

                var dir = target - (Vector2)turret.transform.position;
                if (dir.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                var want = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // 0 度で真上
                var euler = turret.transform.eulerAngles;
                var z = Mathf.LerpAngle(euler.z, want, 1f - Mathf.Exp(-10f * dt));
                turret.transform.rotation = Quaternion.Euler(euler.x, euler.y, z);
            }
        }

        private static void Play(FortressEffect effect, Vector2 position)
        {
            if (effect != null && !effect.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(effect, position, Quaternion.identity);
            }
        }
    }
}
