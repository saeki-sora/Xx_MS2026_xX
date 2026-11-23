using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボスウェーブの係（起動時に <see cref="FortressWaves"/> の係と一緒に自動で作られる。2026-11-15）。全員のPCで動き、ウェーブの段階に合わせて:
    /// ・並び替え（BossFormation）: 配置を計算し、カメラを全員同じ引きの画面へ、コアを左へ滑らせ、障害物を残す／端へ逃がし、
    ///   4人を席替えと同じ跳び方で縦一列へ移し（<see cref="FortressSeats.BeginFormationMove"/>）、砲台を扇形の往復にする
    /// ・WARNING（BossWarning）: ボスを画面の外に作り、WARNING の後に右から入れる
    /// ・戦い（BossCombat）: ボスを動かす（動き・攻撃・ダメージは Host だけ）
    /// ・最後の握り（BossFinale）／コアに届いた（BossReached）: ボスに知らせる
    /// 試合が新しくなったら全部片付ける。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(108)]
    internal sealed class BossWaveRunner : MonoBehaviour
    {
        private const float ViewSafeArea = 0.94f;
        private const float ViewPadding = 0.6f;

        private struct Slide
        {
            public Transform Target;
            public Vector3 From;
            public Vector3 To;
            public float Delay;
            public float Seconds;
        }

        private readonly List<Slide> _slides = new List<Slide>();
        private readonly List<(Transform target, Vector3 position)> _originals = new List<(Transform, Vector3)>();
        private readonly List<Vector2> _fitPoints = new List<Vector2>();

        private SwarmSystem _swarm;
        private BossDefinition _def;
        private BossActor _actor;
        private bool _formed;
        private float _slideTime;
        private bool _sliding;
        private bool _navDirty;
        private float _turretZ;
        private int _layer;

        private void OnEnable()
        {
            FortressWaves.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            FortressWaves.PhaseChanged -= OnPhaseChanged;
            Cleanup(restoreLayout: false);
        }

        private void Update()
        {
            DetectMatch();
            if (_sliding)
            {
                TickSlides(Time.deltaTime);
            }

            if (_actor != null && FortressWaves.Phase == WavePhase.BossWarning && _actor.IsHidden && _def != null &&
                FortressWaves.PhaseElapsed >= _def.warningSeconds)
            {
                _actor.BeginEnter();
            }
        }

        // 新しい試合（群衆の計算が新しくなった）では全部片付ける（ステージが読み直され、コア・砲台は StageLayout が置き直す）。
        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            _swarm = swarm;
            Cleanup(restoreLayout: false);
        }

        private void OnPhaseChanged(WavePhase previous, WavePhase next)
        {
            switch (next)
            {
                case WavePhase.BossFormation:
                    BeginFormation(animate: true);
                    break;
                case WavePhase.BossWarning:
                    EnsureFormed();
                    EnsureActor();
                    Play(_def != null ? _def.onWarning : null);
                    if (_def != null && _def.bossBgm.IsValid)
                    {
                        FortressAudioMix.PlayBgm(_def.bossBgm);
                    }

                    break;
                case WavePhase.BossCombat:
                    EnsureFormed();
                    EnsureActor();
                    _actor?.BeginFight(FortressNet.HasSimulationAuthority);
                    break;
                case WavePhase.BossFinale:
                    EnsureFormed();
                    EnsureActor();
                    _actor?.BeginFinale(FortressNet.HasSimulationAuthority);
                    break;
                case WavePhase.BossReached:
                    EnsureFormed();
                    EnsureActor();
                    _actor?.BeginReached(FortressNet.HasSimulationAuthority);
                    break;
                case WavePhase.Finished:
                case WavePhase.None:
                    // 試合の終わり（結果を見せる間）は、配置とカメラをそのままにする。
                    break;
                default:
                    if (_formed)
                    {
                        // テストで前のウェーブへ戻ったなど。元の配置へ戻す。
                        Cleanup(restoreLayout: true);
                    }

                    break;
            }
        }

        // 並び替えをしていない（途中から入った・テストで飛ばした）ときは、その場で並べる。
        private void EnsureFormed()
        {
            if (!_formed)
            {
                BeginFormation(animate: false);
            }
        }

        private void EnsureActor()
        {
            if (_actor != null || _def == null || !FortressBoss.HasLayout)
            {
                return;
            }

            _actor = BossActor.Create(_def, FortressBoss.Layout, _turretZ + 0.15f, _turretZ - 0.05f, _layer, ResolveMinionType(_def));
        }

        // ───────── 並び替え ─────────

        private void BeginFormation(bool animate)
        {
            var def = FortressWaves.CurrentBoss;
            if (def == null)
            {
                return;
            }

            if (_formed)
            {
                Cleanup(restoreLayout: true);
            }

            _def = def;
            FortressBoss.ResetState();
            FortressBoss.Definition = def;
            var turrets = FindTurrets();
            _turretZ = turrets.Count > 0 ? turrets[0].transform.position.z : 0f;
            _layer = ResolveLayer();

            var rig = FortressCameraRig.Active;
            var view = rig != null ? rig.PresetView(ViewerIndex.Overview) : CameraViewSettings.Default;
            view.rollDegrees = 0f; // ボスは画面の右から来る（ワールドの +X が画面の右）
            var aspect = rig != null ? rig.Aspect : 16f / 9f;
            var area = BossFormationMath.IntersectOr(CameraViewArea.VisibleRect(view, aspect), NavigationArea());
            var range = turrets.Count > 0 && turrets[0].tuning != null ? turrets[0].tuning.range : 20f;
            var layout = BossFormationMath.Compute(area, def.formation, def.bodyRadius01, def.bobAmount01, range);
            var markers = BossFormationMarkers.Find();
            if (markers != null)
            {
                layout = markers.Apply(layout);
            }

            FortressBoss.Layout = layout;
            FortressBoss.HasLayout = true;
            FortressSeats.FormationLocked = true;
            _formed = true;

            // カメラ: 全員同じ、コア・4人・ボスが収まる引きの画面。
            if (rig != null)
            {
                rig.SetViewOverride(FitView(view, aspect, layout), animate ? def.cameraBlendSeconds : 0f);
            }

            _slides.Clear();
            _originals.Clear();
            var core = FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                AddSlide(core.transform, layout.core, 0f, def.coreSlideSeconds);
            }

            PlanObstacles(layout, def);

            // 4人: 席替えと同じ跳び方で縦一列へ（各PCが自分で跳ばす）。向きはボスの方。
            var targets = new Vector3[FortressSeats.SeatCount];
            var angles = new float[FortressSeats.SeatCount];
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i] = new Vector3(layout.turrets[i].x, layout.turrets[i].y, _turretZ);
                angles[i] = layout.turretAngle;
            }

            var coreTarget = new Vector3(layout.core.x, layout.core.y, core != null ? core.transform.position.z : 0f);
            var jumped = animate && FortressSeats.BeginFormationMove(targets, angles, coreTarget, def.formationBanner, def.formationSubText);
            if (!jumped)
            {
                foreach (var turret in turrets)
                {
                    var p = turret.playerIndex;
                    if (p >= 0 && p < targets.Length)
                    {
                        turret.transform.position = targets[p];
                        var e = turret.transform.eulerAngles;
                        turret.transform.rotation = Quaternion.Euler(e.x, e.y, angles[p]);
                    }
                }
            }

            var sweep = TurretSweep.Create(layout.turretAngle, def.sweepHalfAngle, def.sweepSpeedMultiplier);
            foreach (var turret in turrets)
            {
                turret.Sweep = sweep;
            }

            _slideTime = 0f;
            _sliding = _slides.Count > 0;
            _navDirty = true;
            if (!animate)
            {
                TickSlides(1000f);
            }
        }

        private CameraViewSettings FitView(CameraViewSettings view, float aspect, BossFormationLayout layout)
        {
            _fitPoints.Clear();
            var r = layout.bossRadius;
            _fitPoints.Add(layout.core + new Vector2(-2f, 0f));
            _fitPoints.Add(layout.turrets[0] + new Vector2(0f, 2.2f));
            _fitPoints.Add(layout.turrets[3] + new Vector2(0f, -2.2f));
            _fitPoints.Add(layout.bossStop + new Vector2(r * 1.35f, layout.bobAmplitude + r * 1.2f));
            _fitPoints.Add(layout.bossStop + new Vector2(r * 1.35f, -layout.bobAmplitude - r * 1.2f));
            return CameraViewMath.FitToPoints(view, aspect, _fitPoints, ViewSafeArea, ViewPadding);
        }

        private static Rect NavigationArea()
        {
            if (EnemyNavigation.Current is NavigationField field && field != null)
            {
                var size = field.areaSize - new Vector2(2f, 2f);
                return new Rect(field.areaCenter - size * 0.5f, size);
            }

            return new Rect(-1000f, -1000f, 2000f, 2000f);
        }

        // 障害物: 残す物は行き先へ、残さない物は画面の上下の端へ。ステージのどれにも指定が無ければ自動で数個残す。
        private void PlanObstacles(BossFormationLayout layout, BossDefinition def)
        {
            var roots = new List<Transform>();
            foreach (var obstacle in NavigationObstacle.FindAll())
            {
                if (obstacle == null || !obstacle.isActiveAndEnabled || obstacle.mode != NavigationObstacleMode.Block)
                {
                    continue;
                }

                var destructible = obstacle.GetComponentInParent<DestructibleObstacle>();
                var root = destructible != null ? destructible.transform : obstacle.transform;
                if (roots.Contains(root))
                {
                    continue;
                }

                // 遊べる範囲の外（ステージのふちの壁など）は動かさない。
                var p = (Vector2)root.position;
                var grown = new Rect(layout.area.xMin - 2f, layout.area.yMin - 2f, layout.area.width + 4f, layout.area.height + 4f);
                if (!grown.Contains(p) || ObjectRadius(root) > layout.area.height * 0.3f)
                {
                    continue;
                }

                roots.Add(root);
            }

            var anyRule = false;
            foreach (var root in roots)
            {
                if (FindRule(root) != null)
                {
                    anyRule = true;
                    break;
                }
            }

            var kept = new HashSet<Transform>();
            if (!anyRule)
            {
                // 自動: 残す場所に近い物から順に残す。
                var shape = def.formation ?? new BossFormationShape();
                for (var i = 0; i < Mathf.Min(shape.autoKeepCount, roots.Count); i++)
                {
                    var slot = BossFormationMath.KeepPosition(i, layout.area, shape);
                    Transform best = null;
                    var bestDistance = float.MaxValue;
                    foreach (var root in roots)
                    {
                        if (kept.Contains(root))
                        {
                            continue;
                        }

                        var d = Vector2.Distance(root.position, slot);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = root;
                        }
                    }

                    if (best != null)
                    {
                        kept.Add(best);
                        AddSlide(best, slot, 0.15f, def.obstacleSlideSeconds);
                    }
                }
            }

            var margin = def.formation != null ? def.formation.escapeMargin : 1.5f;
            foreach (var root in roots)
            {
                if (kept.Contains(root))
                {
                    continue;
                }

                var rule = FindRule(root);
                if (rule != null && rule.mode == BossObstacleMode.Ignore)
                {
                    continue;
                }

                if (rule != null && rule.mode == BossObstacleMode.Keep)
                {
                    if (rule.keepTarget != null)
                    {
                        AddSlide(root, rule.keepTarget.position, 0.15f, def.obstacleSlideSeconds);
                    }

                    continue;
                }

                var to = BossFormationMath.EscapePosition(root.position, layout.area, ObjectRadius(root), margin);
                AddSlide(root, to, 0.15f, def.obstacleSlideSeconds);
            }
        }

        private static BossObstacleRule FindRule(Transform root)
        {
            var rule = root.GetComponentInChildren<BossObstacleRule>(true);
            return rule != null ? rule : root.GetComponentInParent<BossObstacleRule>();
        }

        private static float ObjectRadius(Transform root)
        {
            var radius = 0.5f;
            foreach (var collider in root.GetComponentsInChildren<Collider2D>())
            {
                var e = collider.bounds.extents;
                radius = Mathf.Max(radius, Mathf.Max(e.x, e.y));
            }

            return radius;
        }

        private void AddSlide(Transform target, Vector2 to, float delay, float seconds)
        {
            if (target == null)
            {
                return;
            }

            var from = target.position;
            _originals.Add((target, from));
            _slides.Add(new Slide
            {
                Target = target,
                From = from,
                To = new Vector3(to.x, to.y, from.z),
                Delay = delay,
                Seconds = Mathf.Max(0.05f, seconds)
            });
        }

        private void TickSlides(float dt)
        {
            _slideTime += dt;
            var done = true;
            foreach (var slide in _slides)
            {
                if (slide.Target == null)
                {
                    continue;
                }

                var t = Mathf.Clamp01((_slideTime - slide.Delay) / slide.Seconds);
                slide.Target.position = Vector3.Lerp(slide.From, slide.To, BossFormationMath.EaseInOut(t));
                done &= t >= 1f;
            }

            if (done)
            {
                _sliding = false;
            }

            if (_navDirty && done)
            {
                // コア・障害物が動き終わったら、敵の道案内を作り直す。
                _navDirty = false;
                NavigationObstacle.NotifyChanged();
                if (EnemyNavigation.Current is NavigationField field && field != null)
                {
                    field.MarkDirty();
                }
            }
        }

        // ───────── 片付け ─────────

        private void Cleanup(bool restoreLayout)
        {
            if (_actor != null)
            {
                Destroy(_actor.gameObject);
                _actor = null;
            }

            BossBullet.ClearAll();
            foreach (var turret in FindTurrets())
            {
                turret.Sweep = default;
            }

            var rig = FortressCameraRig.Active;
            if (rig != null)
            {
                rig.ClearViewOverride(restoreLayout ? 0.6f : 0f);
            }

            var wasLocked = FortressSeats.FormationLocked;
            FortressSeats.FormationLocked = false;
            if (restoreLayout)
            {
                if (wasLocked)
                {
                    FortressSeats.SnapToSeats();
                }

                foreach (var (target, position) in _originals)
                {
                    if (target != null)
                    {
                        target.position = position;
                    }
                }

                NavigationObstacle.NotifyChanged();
                if (EnemyNavigation.Current is NavigationField field && field != null)
                {
                    field.MarkDirty();
                }
            }

            _slides.Clear();
            _originals.Clear();
            _sliding = false;
            _formed = false;
            _def = null;
            FortressBoss.ResetState();
        }

        // ───────── そのほか ─────────

        private static List<LaserTurret> FindTurrets()
        {
            var result = new List<LaserTurret>();
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret != null && turret.playerIndex >= 0 && turret.playerIndex < FortressSeats.SeatCount)
                {
                    result.Add(turret);
                }
            }

            result.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            return result;
        }

        // ボスの当たり判定の層: レーザーが当たる層のうち、障害物と同じ層（無ければ Default）。
        private static int ResolveLayer()
        {
            var beam = FindFirstObjectByType<LaserBeamVisual>();
            var mask = beam != null ? beam.targetLayerMask.value : ~0;
            foreach (var obstacle in NavigationObstacle.FindAll())
            {
                if (obstacle != null && obstacle.mode == NavigationObstacleMode.Block && (mask & (1 << obstacle.gameObject.layer)) != 0)
                {
                    return obstacle.gameObject.layer;
                }
            }

            return (mask & 1) != 0 ? 0 : FirstLayerIn(mask);
        }

        private static int FirstLayerIn(int mask)
        {
            for (var i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private static EnemyTypeDefinition ResolveMinionType(BossDefinition def)
        {
            if (def.minionType != null)
            {
                return def.minionType;
            }

            var plan = FortressWaves.Plan;
            if (plan == null)
            {
                return null;
            }

            foreach (var entry in plan.AllEntries())
            {
                if (entry != null && entry.enemyType != null)
                {
                    return entry.enemyType;
                }
            }

            return null;
        }

        private static void Play(FortressEffect effect)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            FortressEffectPlayer.PlayOnce(effect, core != null ? core.transform.position : Vector3.zero, Quaternion.identity);
        }
    }
}
