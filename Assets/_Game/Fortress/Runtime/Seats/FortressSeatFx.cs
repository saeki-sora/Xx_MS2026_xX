using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの世界の中の演出（起動時に席替えの係と一緒に自動で作られる。2026-11-02 ユーザー決定の見本帳の番号）。
    /// 跳ぶ前: 行き先の点線（2）・コアのまわりを回る矢印の輪（3）。
    /// 跳んでいる間: 地面の影（6）・光の尾（8）。（残像（7）は 2026-11-02 にやめた）
    /// 着地: 衝撃波の輪（14）・床の光の波（19）・キラッ（20）。
    /// 締め: 自分の砲台の光の柱（21）・4色の光がコアへ集まる（22）。
    /// 形はコードで作る（<see cref="SeatFxArt"/>）。各PCが自分で描く（通信なし）。着地のヒットストップの間は一緒に止まる。
    /// どれを出すかは <see cref="WaveSeatRotation"/> のチェックで切り替える。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(120)]
    internal sealed class FortressSeatFx : MonoBehaviour
    {
        private const int DotCount = 16;
        private const int RingArrowCount = 6;
        private const float GroundLift = -0.03f; // 地面より少しカメラ側（Z がマイナスほど手前）
        private const float ShockwaveLife = 0.55f;
        private const float WaveLife = 1.4f;
        private const float WaveSpeed = 14f;
        private const float SparkleDelay = 0.05f;
        private const float SparkleLife = 0.45f;
        private const float OrbSeconds = 0.6f;
        private static readonly Color RingColor = new Color(0.5f, 0.89f, 1f, 1f);
        private static readonly WaveSeatRotation DefaultSettings = new WaveSeatRotation();

        // プレイヤーごとに1組、使い回す見た目。
        private sealed class Lane
        {
            public SpriteRenderer Shadow;
            public TrailRenderer Trail;
            public SpriteRenderer[] Dots;
            public SpriteRenderer Arrow;
        }

        private sealed class Track
        {
            public SeatJumper Jumper;
            public LaserTurret Turret;
            public int Player;
            public int Rank;
            public Color Color;
            public float Radius;
            public Vector3 From;
            public Vector3 To;
            public Vector3 Control;
            public float LandTime;
            public bool WasFlying;
            public SpriteRenderer[] Sources;
            public int SortingLayer;
            public int SortingOrder;
            public Lane Lane;
        }

        private sealed class Fading
        {
            public SpriteRenderer A;
            public SpriteRenderer B;
            public Track Track;
            public Vector3 Position;
            public Color Color;
            public float Age;
            public bool Done;
        }

        private struct FloorWave
        {
            public Vector2 Position;
            public Color Color;
            public float Age;
        }

        private readonly Lane[] _lanes = new Lane[FortressSeats.SeatCount];
        private readonly List<Track> _tracks = new List<Track>();
        private readonly List<Fading> _shockwaves = new List<Fading>();
        private readonly List<Fading> _sparkles = new List<Fading>();
        private readonly List<Fading> _orbs = new List<Fading>();
        private readonly List<FloorWave> _waves = new List<FloorWave>();
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        private readonly List<Vector2> _tileCenters = new List<Vector2>();
        private readonly SpriteRenderer[] _ringArrows = new SpriteRenderer[RingArrowCount];
        private Transform _root;
        private SpriteRenderer _ring;
        private SpriteRenderer _pillar;
        private SpriteRenderer _pillarPulse;
        private SpriteRenderer _coreGlow;
        private Vector3 _core;
        private float _coreGlowAmount;
        private float _ringRadius;
        private float _lastLanding;
        private bool _moving;
        private bool _finale;
        private Rect _tileBounds;
        private float _tileSize;
        private bool _tilesLit;

        private static WaveSeatRotation Settings => FortressSeats.Settings ?? DefaultSettings;

        private void OnEnable()
        {
            FortressSeats.MoveStarted += OnMoveStarted;
            FortressSeats.MoveFinished += OnMoveFinished;
            FortressSeats.TurretLanded += OnTurretLanded;
            FortressWaves.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            FortressSeats.MoveStarted -= OnMoveStarted;
            FortressSeats.MoveFinished -= OnMoveFinished;
            FortressSeats.TurretLanded -= OnTurretLanded;
            FortressWaves.PhaseChanged -= OnPhaseChanged;
            ClearAll();
        }

        // 席替えの段階が終わったら（テストで飛ばしたときも）、残っている演出（床の光・輪・光の柱など）を片付ける。
        // 跳んでいる最中（テストの「今すぐ1つ席替え」は段階と関係なく動く）は片付けない。
        private void OnPhaseChanged(WavePhase previous, WavePhase next)
        {
            if (next != WavePhase.SeatChange && !FortressSeats.IsMoving)
            {
                ClearAll();
            }
        }

        // ───────── お知らせ ─────────

        private void OnMoveStarted(int shift)
        {
            ClearAll();
            EnsureRoot();
            var settings = Settings;
            var core = FindFirstObjectByType<CoreCrystalController>();
            var hasSeats = FortressSeats.TryGetRegisteredSeats(out _, out var center);
            _core = core != null ? core.transform.position : hasSeats ? (Vector3)center : Vector3.zero;
            _ringRadius = float.MaxValue;
            _lastLanding = 0f;
            foreach (var jumper in FortressSeats.Jumpers)
            {
                if (jumper.Turret == null || jumper.Player < 0 || jumper.Player >= _lanes.Length)
                {
                    continue;
                }

                var track = new Track
                {
                    Jumper = jumper,
                    Turret = jumper.Turret,
                    Player = jumper.Player,
                    Rank = jumper.Rank,
                    Color = FortressColors.PlayerColor(jumper.Player),
                    From = jumper.From,
                    To = jumper.To,
                    LandTime = settings.AnticipationTime + settings.StaggerTime * jumper.Rank + settings.FlightSeconds,
                    Sources = jumper.Turret.GetComponentsInChildren<SpriteRenderer>(false),
                    Lane = EnsureLane(jumper.Player)
                };
                track.Radius = MeasureRadius(track.Sources);
                var main = track.Sources.Length > 0 ? track.Sources[0] : null;
                track.SortingLayer = main != null ? main.sortingLayerID : 0;
                track.SortingOrder = main != null ? main.sortingOrder : 0;
                track.Control = CurveControl(track.From, track.To, _core);
                _ringRadius = Mathf.Min(_ringRadius, Vector2.Distance(track.From, _core));
                _lastLanding = Mathf.Max(_lastLanding, track.LandTime);
                _tracks.Add(track);
            }

            _ringRadius = _ringRadius < float.MaxValue ? _ringRadius * 0.52f : 3.6f;
            _moving = _tracks.Count > 0;
        }

        private void OnMoveFinished(int shift)
        {
            if (_root == null)
            {
                ForgetRenderers();
                return;
            }

            _moving = false;
            HideLanes();
            if (!FortressSeats.LastMoveCompleted || _tracks.Count == 0)
            {
                ClearAll();
                return;
            }

            var settings = Settings;
            _finale = true;
            if (settings.gatherToCore)
            {
                foreach (var track in _tracks)
                {
                    var orb = new Fading { A = Rent(SeatFxArt.Air, SeatFxArt.SoftCircle, "Orb"), B = Rent(SeatFxArt.Air, SeatFxArt.Disc, "OrbCore"), Track = track, Color = track.Color };
                    SetSorting(orb.A, track, 20);
                    SetSorting(orb.B, track, 21);
                    _orbs.Add(orb);
                }
            }
        }

        private void OnTurretLanded(SeatJumper jumper)
        {
            var track = _tracks.Find(t => t.Jumper == jumper);
            if (track == null)
            {
                return;
            }

            var settings = Settings;
            var ground = new Vector3(jumper.To.x, jumper.To.y, jumper.To.z + GroundLift);
            if (settings.shockwave)
            {
                var wave = new Fading { A = Rent(SeatFxArt.Ground, SeatFxArt.Ring, "Shockwave"), B = Rent(SeatFxArt.Ground, SeatFxArt.ThinRing, "ShockwaveEdge"), Position = ground, Color = track.Color };
                wave.A.sortingOrder = 2;
                wave.B.sortingOrder = 3;
                _shockwaves.Add(wave);
            }

            if (settings.floorWave)
            {
                EnsureTiles(settings.floorTileSize);
                _waves.Add(new FloorWave { Position = jumper.To, Color = track.Color });
            }

            if (settings.sparkle)
            {
                var sparkle = new Fading { A = Rent(SeatFxArt.Air, SeatFxArt.Star, "Sparkle"), B = Rent(SeatFxArt.Air, SeatFxArt.Star, "SparkleSmall"), Track = track, Age = -SparkleDelay };
                SetSorting(sparkle.A, track, 10);
                SetSorting(sparkle.B, track, 10);
                sparkle.A.enabled = sparkle.B.enabled = false;
                _sparkles.Add(sparkle);
                if (!settings.onSparkle.IsEmpty)
                {
                    FortressEffectPlayer.SpawnVfx(settings.onSparkle, jumper.To, Quaternion.identity, track.Player);
                    if (track.Rank == 0)
                    {
                        FortressEffectPlayer.PlaySound(settings.onSparkle, jumper.To);
                    }
                }
            }
        }

        // ───────── 毎フレーム ─────────

        private void LateUpdate()
        {
            if (_root == null || (!_moving && !_finale && _shockwaves.Count == 0 && _sparkles.Count == 0 && _waves.Count == 0 && !_tilesLit))
            {
                return;
            }

            var settings = Settings;
            // 止まるのは跳んでいる間のヒットストップだけ（着地の後に残る演出は、普通に時間が進んで必ず消える）。
            var dt = FortressSeats.IsMoving ? FortressSeats.FxDeltaTime : Time.deltaTime;
            var up = FortressSeatMover.ScreenUp();
            if (_moving)
            {
                var time = FortressSeats.MoveTime;
                UpdateRing(settings, time);
                foreach (var track in _tracks)
                {
                    UpdatePath(settings, track, time);
                    UpdateShadowAndTrail(settings, track, time);
                }
            }

            UpdateShockwaves(settings, dt);
            UpdateFloor(settings, dt);
            UpdateSparkles(up, dt);
            if (_finale)
            {
                UpdateFinale(settings, up);
            }
        }

        // 行き先の点線（コアから外へふくらむ弧。点が行き先へ流れる）。跳ぶ少し前に消える。
        private static void UpdatePath(WaveSeatRotation settings, Track track, float time)
        {
            var lane = track.Lane;
            var alpha = settings.showPathPreview ? Mathf.Clamp01(time / 0.25f) * (1f - Mathf.Clamp01((time - (track.LandTime - 0.5f)) / 0.3f)) : 0f;
            var draw = Mathf.Clamp01(time / 0.45f);
            var flow = Mathf.Repeat(time * 1.2f, 1f);
            for (var i = 0; i < lane.Dots.Length; i++)
            {
                var dot = lane.Dots[i];
                var u = (i + flow) / lane.Dots.Length;
                dot.enabled = alpha > 0.01f && u <= draw;
                if (!dot.enabled)
                {
                    continue;
                }

                dot.transform.position = Grounded(Bezier(track.From, track.Control, track.To, u), track.From.z);
                dot.transform.localScale = Vector3.one * Mathf.Max(0.18f, track.Radius * 0.32f);
                dot.color = WithAlpha(track.Color, alpha * Mathf.Lerp(0.55f, 1f, u));
            }

            lane.Arrow.enabled = alpha > 0.01f && draw >= 1f;
            if (lane.Arrow.enabled)
            {
                var tip = Bezier(track.From, track.Control, track.To, 0.9f);
                var dir = (Vector2)(track.To - track.Control);
                lane.Arrow.transform.position = Grounded(tip, track.From.z);
                lane.Arrow.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
                lane.Arrow.transform.localScale = Vector3.one * track.Radius * 1.3f;
                lane.Arrow.color = WithAlpha(track.Color, alpha);
            }
        }

        // コアのまわりを回る矢印の輪。全員が着地したら消える。
        private void UpdateRing(WaveSeatRotation settings, float time)
        {
            var alpha = settings.showDirectionRing ? Mathf.Clamp01(time / 0.3f) * (1f - Mathf.Clamp01((time - _lastLanding) / 0.4f)) : 0f;
            EnsureRing();
            _ring.enabled = alpha > 0.01f;
            foreach (var arrow in _ringArrows)
            {
                arrow.enabled = _ring.enabled;
            }

            if (!_ring.enabled)
            {
                return;
            }

            var center = Grounded(_core, _core.z);
            _ring.transform.position = center;
            _ring.transform.localScale = Vector3.one * (_ringRadius * 2f / 0.96f);
            _ring.color = WithAlpha(RingColor, 0.4f * alpha);
            var sign = FortressSeats.Clockwise ? -1f : 1f;
            for (var i = 0; i < _ringArrows.Length; i++)
            {
                var angle = sign * time * 2.2f + i * Mathf.PI * 2f / _ringArrows.Length;
                var along = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * sign;
                var arrow = _ringArrows[i];
                arrow.transform.position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _ringRadius;
                arrow.transform.rotation = Quaternion.LookRotation(Vector3.forward, along);
                arrow.transform.localScale = Vector3.one * Mathf.Max(0.6f, _ringRadius * 0.24f);
                arrow.color = WithAlpha(RingColor, 0.95f * alpha);
            }
        }

        // 影（浮くほど小さく薄く）と光の尾（跳んでいる間だけ出す）。
        private static void UpdateShadowAndTrail(WaveSeatRotation settings, Track track, float time)
        {
            var lane = track.Lane;
            var jumper = track.Jumper;
            var h = jumper.Height01;
            lane.Shadow.enabled = settings.showShadow;
            if (lane.Shadow.enabled)
            {
                var fade = Mathf.Clamp01(time / 0.25f);
                lane.Shadow.transform.position = Grounded(jumper.Ground, track.From.z);
                lane.Shadow.transform.localScale = Vector3.one * (track.Radius * 2.4f * (1f - 0.5f * h));
                lane.Shadow.color = new Color(0f, 0f, 0f, 0.45f * (1f - 0.6f * h) * fade);
            }

            var flying = jumper.Flying && !jumper.Landed && track.Turret != null;
            if (flying && !track.WasFlying)
            {
                lane.Trail.Clear();
            }

            track.WasFlying = flying;
            lane.Trail.emitting = flying && settings.showTrail;
            if (track.Turret != null)
            {
                lane.Trail.transform.position = track.Turret.transform.position + new Vector3(0f, 0f, 0.01f);
            }

            lane.Trail.widthMultiplier = track.Radius * 1.5f;
            lane.Trail.sortingLayerID = track.SortingLayer;
            lane.Trail.sortingOrder = track.SortingOrder - 2;
        }

        private void UpdateShockwaves(WaveSeatRotation settings, float dt)
        {
            foreach (var wave in _shockwaves)
            {
                wave.Age += dt;
                var u = Mathf.Clamp01(wave.Age / ShockwaveLife);
                var radius = Mathf.Lerp(0.8f, settings.shockwaveRadius, 1f - Mathf.Pow(1f - u, 3f));
                wave.A.transform.position = wave.B.transform.position = wave.Position;
                wave.A.transform.localScale = Vector3.one * (radius * 2f / 0.96f);
                wave.B.transform.localScale = Vector3.one * (radius * 0.86f * 2f / 0.96f);
                wave.A.color = WithAlpha(wave.Color, 0.9f * (1f - u));
                wave.B.color = WithAlpha(Color.white, 0.8f * (1f - u));
                wave.Done = u >= 1f;
            }

            Sweep(_shockwaves);
        }

        // 床の光の波: 着地点から輪のように広がり、通ったタイルが自分の色で光る。
        private void UpdateFloor(WaveSeatRotation settings, float dt)
        {
            if (_waves.Count == 0 && !_tilesLit)
            {
                return;
            }

            for (var i = _waves.Count - 1; i >= 0; i--)
            {
                var wave = _waves[i];
                wave.Age += dt;
                _waves[i] = wave;
                if (wave.Age >= WaveLife)
                {
                    _waves.RemoveAt(i);
                }
            }

            var width = Mathf.Max(0.5f, _tileSize * 1.4f);
            var lit = false;
            for (var t = 0; t < _tiles.Count; t++)
            {
                var center = _tileCenters[t];
                var best = 0f;
                var color = Color.white;
                foreach (var wave in _waves)
                {
                    var d = (Vector2.Distance(center, wave.Position) - wave.Age * WaveSpeed) / width;
                    var v = Mathf.Exp(-d * d) * (1f - wave.Age / WaveLife);
                    if (v > best)
                    {
                        best = v;
                        color = wave.Color;
                    }
                }

                var tile = _tiles[t];
                if (tile == null)
                {
                    continue;
                }

                tile.enabled = best > 0.02f;
                lit |= tile.enabled;
                if (tile.enabled)
                {
                    tile.color = WithAlpha(Color.Lerp(color, Color.white, 0.15f), Mathf.Min(0.75f, best) * 0.85f);
                }
            }

            _tilesLit = lit;
        }

        private void UpdateSparkles(Vector3 up, float dt)
        {
            var right = new Vector3(up.y, -up.x, 0f);
            foreach (var sparkle in _sparkles)
            {
                sparkle.Age += dt;
                var u = sparkle.Age / SparkleLife;
                sparkle.Done = u >= 1f || sparkle.Track.Turret == null;
                var visible = u >= 0f && !sparkle.Done;
                sparkle.A.enabled = sparkle.B.enabled = visible;
                if (!visible)
                {
                    continue;
                }

                var r = sparkle.Track.Radius;
                var s = Mathf.Sin(Mathf.PI * u);
                var basePos = sparkle.Track.Turret.transform.position + new Vector3(0f, 0f, -0.05f);
                var rotation = Quaternion.LookRotation(Vector3.forward, up) * Quaternion.Euler(0f, 0f, 45f * u);
                sparkle.A.transform.SetPositionAndRotation(basePos + up * (r * 0.95f) + right * (r * 0.8f), rotation);
                sparkle.B.transform.SetPositionAndRotation(basePos + up * (r * 1.3f) - right * (r * 0.5f), rotation);
                sparkle.A.transform.localScale = Vector3.one * (r * 2f * s);
                sparkle.B.transform.localScale = Vector3.one * (r * 1f * s);
                sparkle.A.color = sparkle.B.color = Color.white;
            }

            Sweep(_sparkles);
        }

        // 締め: 光の玉がコアへ飛んでコアが光る。自分の砲台に光の柱と、足元に広がる輪。
        private void UpdateFinale(WaveSeatRotation settings, Vector3 up)
        {
            var age = FortressSeats.FinaleAge;
            var duration = Mathf.Max(0.1f, settings.youMarkerSeconds);
            var realDt = Time.deltaTime;

            foreach (var orb in _orbs)
            {
                var u = Mathf.Clamp01(age / OrbSeconds);
                var turret = orb.Track.Turret;
                if (turret == null || u >= 1f)
                {
                    if (!orb.Done && turret != null)
                    {
                        OnOrbArrived(settings);
                    }

                    orb.Done = true;
                    continue;
                }

                var start = turret.transform.position;
                var to = _core;
                var mid = (start + to) * 0.5f;
                var d = to - start;
                var control = mid + new Vector3(-d.y, d.x, 0f) * 0.25f;
                var e = FortressSeatMath.EaseInOutCubic(u);
                var p = Bezier(start, control, to, e) + new Vector3(0f, 0f, -0.1f);
                var r = orb.Track.Radius;
                orb.A.transform.position = orb.B.transform.position = p;
                orb.A.transform.localScale = Vector3.one * (r * 2.4f);
                orb.B.transform.localScale = Vector3.one * (r * 0.55f);
                orb.A.color = WithAlpha(orb.Color, 0.9f);
                orb.B.color = Color.white;
            }

            Sweep(_orbs);

            _coreGlowAmount = Mathf.Max(0f, _coreGlowAmount - realDt * 0.9f);
            EnsureFinaleRenderers();
            _coreGlow.enabled = _coreGlowAmount > 0.01f;
            if (_coreGlow.enabled)
            {
                _coreGlow.transform.position = _core + new Vector3(0f, 0f, -0.2f);
                _coreGlow.transform.localScale = Vector3.one * (_ringRadius * 2.6f * (1f + 0.3f * (1f - _coreGlowAmount)));
                _coreGlow.color = new Color(0.85f, 0.97f, 1f, 0.6f * _coreGlowAmount);
            }

            var local = FindLocalTrack();
            var showPillar = settings.youPillar && local != null && local.Turret != null && age < duration;
            _pillar.enabled = _pillarPulse.enabled = showPillar;
            if (showPillar)
            {
                var fade = Mathf.Min(1f, age / 0.15f, (duration - age) / 0.3f);
                var position = local.Turret.transform.position;
                var r = local.Radius;
                var light = Color.Lerp(local.Color, Color.white, 0.45f);
                _pillar.transform.SetPositionAndRotation(position + new Vector3(0f, 0f, 0.03f), Quaternion.LookRotation(Vector3.forward, up));
                _pillar.transform.localScale = new Vector3(r * 2.2f * (1f + 0.07f * Mathf.Sin(age * 10f)) / 0.25f, 9f, 1f);
                _pillar.color = WithAlpha(light, 0.55f * fade);
                _pillar.sortingLayerID = local.SortingLayer;
                _pillar.sortingOrder = local.SortingOrder - 3;
                var pulse = Mathf.Repeat(age, 0.7f) / 0.7f;
                _pillarPulse.transform.position = Grounded(position, local.To.z);
                _pillarPulse.transform.localScale = Vector3.one * (r * (2f + 3.2f * pulse) / 0.96f);
                _pillarPulse.color = WithAlpha(Color.white, (1f - pulse) * fade);
            }

            if (age > duration + 0.1f && _orbs.Count == 0 && _coreGlowAmount <= 0.01f)
            {
                _finale = false;
                _pillar.enabled = _pillarPulse.enabled = _coreGlow.enabled = false;
            }
        }

        private void OnOrbArrived(WaveSeatRotation settings)
        {
            if (_coreGlowAmount < 0.5f && !settings.onGather.IsEmpty)
            {
                FortressEffectPlayer.PlayOnce(settings.onGather, _core, Quaternion.identity);
            }

            _coreGlowAmount = 1f;
        }

        private Track FindLocalTrack()
        {
            var local = FortressSeatMover.LocalPlayer();
            Track first = null;
            foreach (var track in _tracks)
            {
                if (track.Player == local)
                {
                    return track;
                }

                if (track.Rank == 0)
                {
                    first = track;
                }
            }

            return first;
        }

        // ───────── 作る・片付ける ─────────

        // 絵を置く入れ物は、今のシーン（ステージ）の普通のオブジェクトにする（敵のルートの線と同じ）。
        // この係（DontSave）の子にすると Play を止めても消えずに残り、次の Play で前の床の光などが映ってしまうため（2026-11-02）。
        // シーンが閉じて入れ物が消えたら、覚えている絵も全部忘れて作り直す。
        private void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            ForgetRenderers();
            var go = new GameObject("[Seats] Fx");
            go.AddComponent<BillboardIgnore>();
            _root = go.transform;
        }

        private void ForgetRenderers()
        {
            for (var i = 0; i < _lanes.Length; i++)
            {
                _lanes[i] = null;
            }

            for (var i = 0; i < _ringArrows.Length; i++)
            {
                _ringArrows[i] = null;
            }

            _ring = null;
            _pillar = null;
            _pillarPulse = null;
            _coreGlow = null;
            _pool.Clear();
            _tiles.Clear();
            _tileCenters.Clear();
            _tilesLit = false;
            _shockwaves.Clear();
            _sparkles.Clear();
            _orbs.Clear();
            _waves.Clear();
            _tracks.Clear();
            _moving = false;
            _finale = false;
        }

        private Lane EnsureLane(int player)
        {
            if (_lanes[player] != null)
            {
                return _lanes[player];
            }

            var lane = new Lane
            {
                Shadow = NewRenderer(SeatFxArt.Ground, SeatFxArt.SoftCircle, "Shadow", 4),
                Arrow = NewRenderer(SeatFxArt.Ground, SeatFxArt.Chevron, "PathArrow", 3),
                Dots = new SpriteRenderer[DotCount]
            };
            for (var i = 0; i < DotCount; i++)
            {
                lane.Dots[i] = NewRenderer(SeatFxArt.Ground, SeatFxArt.Disc, "PathDot", 3);
            }

            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(_root, false);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.numCapVertices = 4;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = SeatFxArt.Air;
            trail.emitting = false;
            var color = FortressColors.PlayerColor(player);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.3f), 0f), new GradientColorKey(color, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            lane.Trail = trail;
            _lanes[player] = lane;
            return lane;
        }

        private void EnsureRing()
        {
            if (_ring != null)
            {
                return;
            }

            _ring = NewRenderer(SeatFxArt.Ground, SeatFxArt.ThinRing, "DirectionRing", 1);
            for (var i = 0; i < _ringArrows.Length; i++)
            {
                _ringArrows[i] = NewRenderer(SeatFxArt.Ground, SeatFxArt.Chevron, "DirectionArrow", 1);
            }
        }

        private void EnsureFinaleRenderers()
        {
            if (_pillar != null)
            {
                return;
            }

            _pillar = NewRenderer(SeatFxArt.Air, SeatFxArt.Beam, "YouPillar", 0);
            _pillarPulse = NewRenderer(SeatFxArt.Ground, SeatFxArt.ThinRing, "YouPulse", 4);
            _coreGlow = NewRenderer(SeatFxArt.Air, SeatFxArt.SoftCircle, "CoreGlow", 50);
        }

        // 床の光るタイル（ステージの席のまわり）。大きさが変わったときだけ作り直す。
        private void EnsureTiles(float tileSize)
        {
            var bounds = TileBounds();
            if (_tiles.Count > 0 && Mathf.Approximately(_tileSize, tileSize) && _tileBounds == bounds)
            {
                return;
            }

            foreach (var tile in _tiles)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }

            _tiles.Clear();
            _tileCenters.Clear();
            _tilesLit = false;
            _tileSize = tileSize;
            _tileBounds = bounds;
            var z = _tracks.Count > 0 ? _tracks[0].From.z + GroundLift * 0.5f : GroundLift * 0.5f;
            var x0 = Mathf.Floor(bounds.xMin / tileSize) * tileSize;
            var y0 = Mathf.Floor(bounds.yMin / tileSize) * tileSize;
            for (var x = x0; x < bounds.xMax; x += tileSize)
            {
                for (var y = y0; y < bounds.yMax; y += tileSize)
                {
                    var center = new Vector2(x + tileSize * 0.5f, y + tileSize * 0.5f);
                    var tile = NewRenderer(SeatFxArt.Ground, SeatFxArt.Tile, "FloorTile", 0);
                    tile.transform.position = new Vector3(center.x, center.y, z);
                    tile.transform.localScale = Vector3.one * tileSize * 0.94f;
                    _tiles.Add(tile);
                    _tileCenters.Add(center);
                }
            }
        }

        private Rect TileBounds()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var track in _tracks)
            {
                min = Vector2.Min(min, Vector2.Min(track.From, track.To));
                max = Vector2.Max(max, Vector2.Max(track.From, track.To));
            }

            if (min.x > max.x)
            {
                min = new Vector2(-8f, -8f);
                max = new Vector2(8f, 8f);
            }

            min -= Vector2.one * 6f;
            max += Vector2.one * 6f;
            return Rect.MinMaxRect(Mathf.Round(min.x), Mathf.Round(min.y), Mathf.Round(max.x), Mathf.Round(max.y));
        }

        private SpriteRenderer NewRenderer(Material material, Sprite sprite, string name, int order)
        {
            EnsureRoot();
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private SpriteRenderer Rent(Material material, Sprite sprite, string name)
        {
            EnsureRoot();
            SpriteRenderer renderer;
            if (_pool.Count > 0)
            {
                renderer = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                renderer.gameObject.name = name;
            }
            else
            {
                renderer = NewRenderer(material, sprite, name, 0);
            }

            renderer.sharedMaterial = material;
            renderer.sprite = sprite;
            renderer.SetPropertyBlock(null);
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = 0;
            renderer.flipX = renderer.flipY = false;
            renderer.transform.rotation = Quaternion.identity;
            renderer.enabled = true;
            return renderer;
        }

        private void Return(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = false;
            _pool.Add(renderer);
        }

        private void Sweep(List<Fading> list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!list[i].Done)
                {
                    continue;
                }

                Return(list[i].A);
                Return(list[i].B);
                list.RemoveAt(i);
            }
        }

        private static void SetSorting(SpriteRenderer renderer, Track track, int above)
        {
            renderer.sortingLayerID = track.SortingLayer;
            renderer.sortingOrder = track.SortingOrder + above;
        }

        private void HideLanes()
        {
            foreach (var lane in _lanes)
            {
                if (lane == null)
                {
                    continue;
                }

                lane.Shadow.enabled = false;
                lane.Arrow.enabled = false;
                lane.Trail.emitting = false;
                foreach (var dot in lane.Dots)
                {
                    dot.enabled = false;
                }
            }

            if (_ring != null)
            {
                _ring.enabled = false;
                foreach (var arrow in _ringArrows)
                {
                    arrow.enabled = false;
                }
            }
        }

        private void ClearAll()
        {
            if (_root == null)
            {
                ForgetRenderers();
                return;
            }

            _moving = false;
            _finale = false;
            HideLanes();
            foreach (var lane in _lanes)
            {
                if (lane != null && lane.Trail != null)
                {
                    lane.Trail.Clear();
                }
            }

            foreach (var list in new[] { _shockwaves, _sparkles, _orbs })
            {
                foreach (var item in list)
                {
                    item.Done = true;
                }

                Sweep(list);
            }

            _waves.Clear();
            _tilesLit = false;
            foreach (var tile in _tiles)
            {
                if (tile != null)
                {
                    tile.enabled = false;
                }
            }

            _coreGlowAmount = 0f;
            if (_pillar != null)
            {
                _pillar.enabled = _pillarPulse.enabled = _coreGlow.enabled = false;
            }

            _tracks.Clear();
        }

        // ───────── 計算 ─────────

        private static float MeasureRadius(SpriteRenderer[] sources)
        {
            if (sources.Length == 0 || sources[0] == null || sources[0].sprite == null)
            {
                return 0.9f;
            }

            var extents = sources[0].sprite.bounds.extents;
            var scale = sources[0].transform.lossyScale;
            return Mathf.Clamp(Mathf.Max(extents.x * Mathf.Abs(scale.x), extents.y * Mathf.Abs(scale.y)), 0.3f, 3f);
        }

        // 前の席から次の席への弧の曲がり具合（コアから外へふくらませる）。
        private static Vector3 CurveControl(Vector3 from, Vector3 to, Vector3 core)
        {
            var mid = (Vector2)(from + to) * 0.5f;
            var out2 = mid - (Vector2)core;
            if (out2.sqrMagnitude < 1e-4f)
            {
                var d = (Vector2)(to - from);
                out2 = new Vector2(-d.y, d.x);
            }

            var c = (Vector2)core + out2.normalized * (Vector2.Distance(core, from) * 1.3f);
            return new Vector3(c.x, c.y, from.z);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector3 Grounded(Vector3 position, float groundZ) => new Vector3(position.x, position.y, groundZ + GroundLift);

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
