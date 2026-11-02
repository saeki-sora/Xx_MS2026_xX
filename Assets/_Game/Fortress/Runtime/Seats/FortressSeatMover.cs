using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの係（起動時に <see cref="FortressWaves"/> の係と一緒に自動で作られる、見えないオブジェクト。2026-10-31）。
    /// <see cref="FortressSeats.Shift"/> が1つ変わったら、全員の砲台を次の席へ「ピョン」と跳ばす。
    /// 溜め（しゃがんで震える）→ 跳ぶ（山なり・頂点でふわっと・空中で回る）→ 自分から順に着地（つぶれて戻る・ヒットストップ）。
    /// 各PCが自分で動かす（砲台の場所は通信しない。席替えの回数だけ Host から届く）。浮く向きは自分のカメラの画面の上。
    /// 跳んでいる間は砲台を撃てない・回らない。カメラは少し遅れて次の席の視点へ移り（<see cref="FortressSeats.CameraShift"/>）、その間だけ少し引く。
    /// 点線・影・光の尾・衝撃波などの見た目は <see cref="FortressSeatFx"/>、帯・画面の端などは <see cref="FortressSeatOverlay"/> が受け持つ。
    /// 2つ以上離れた回数が届いたとき（途中から入った・テストで飛ばした）は跳ばずにその場へ置く。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(110)]
    internal sealed class FortressSeatMover : MonoBehaviour
    {
        private const float LaunchStretchSeconds = 0.18f;
        private static readonly WaveSeatRotation DefaultSettings = new WaveSeatRotation();

        private readonly Vector2[] _capturedSeats = new Vector2[FortressSeats.SeatCount];
        private bool _hasCaptured;
        private SwarmSystem _swarm;
        private int _appliedShift;
        private bool _moving;
        private int _toShift;
        private float _time;
        private float _hitStopLeft;
        private bool _cameraMoved;
        private bool _lockedInput;
        private WaveSeatRotation _settings;

        private static WaveSeatRotation Settings => FortressSeats.Settings ?? DefaultSettings;

        private static List<SeatJumper> Jumpers => FortressSeats.JumperList;

        private void OnDisable()
        {
            if (_moving)
            {
                FinishMove(false);
            }
        }

        private void Update()
        {
            DetectMatch();
            var target = FortressSeats.Shift;
            if (_moving)
            {
                if (target != _toShift)
                {
                    // 跳んでいる途中で次が来た（テストで続けて押したなど）。今の移動を終わらせてから次へ。
                    FinishMove(false);
                }
                else
                {
                    TickMove(Time.deltaTime);
                }
            }

            if (_moving || target == _appliedShift)
            {
                return;
            }

            var step = FortressSeatMath.StepBetween(_appliedShift, target, FortressSeats.SeatCount);
            if (step == 1 || step == -1)
            {
                BeginMove(target);
            }
            else
            {
                SnapTo(target);
            }
        }

        // 新しい試合（群衆の計算が新しくなった）では、全員が最初の席から始まる（砲台は StageLayout が最初の席に置き直す）。
        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            _swarm = swarm;
            if (_moving)
            {
                FinishMove(false);
            }

            _appliedShift = 0;
            _hasCaptured = false;
        }

        // ───────── 跳ぶ ─────────

        private void BeginMove(int to)
        {
            EnsureSeats();
            _settings = Settings;
            Jumpers.Clear();
            var local = LocalPlayer();
            foreach (var turret in FindTurrets())
            {
                var seat = FortressSeats.SeatOf(turret.playerIndex, to);
                var position = turret.transform.position;
                var landing = SeatPosition(seat);
                Jumpers.Add(new SeatJumper
                {
                    Turret = turret,
                    Player = turret.playerIndex,
                    From = position,
                    To = new Vector3(landing.x, landing.y, position.z),
                    Ground = position,
                    Rank = FortressSeatMath.LandingRank(turret.playerIndex, local, to, FortressSeats.Order, FortressSeats.Clockwise),
                    BaseScale = turret.transform.localScale,
                    BaseRotation = turret.transform.rotation
                });
            }

            _moving = true;
            _toShift = to;
            _time = 0f;
            _hitStopLeft = 0f;
            _cameraMoved = false;
            FortressSeats.IsMoving = true;
            FortressSeats.MoveTime = 0f;
            FortressSeats.IsHitStopped = false;
            LockInput();
            if (_settings.AnticipationTime > 0f)
            {
                foreach (var jumper in Jumpers)
                {
                    PlayEffect(_settings.onAnticipate, jumper, jumper.From, jumper.Rank == 0);
                }
            }

            FortressSeats.RaiseMoveStarted(to);
            TickMove(0f);
        }

        private void TickMove(float dt)
        {
            var settings = _settings ?? Settings;
            LockInput();

            // 着地のヒットストップ: 砲台の動き（と席替えの演出）を一瞬止める。カメラ・敵・通信は止めない。
            if (_hitStopLeft > 0f)
            {
                _hitStopLeft -= Mathf.Max(0f, dt);
                FortressSeats.IsHitStopped = _hitStopLeft > 0f;
                dt = 0f;
            }

            _time += Mathf.Max(0f, dt);
            FortressSeats.MoveTime = _time;
            if (!_cameraMoved && _time >= settings.CameraStartTime)
            {
                _cameraMoved = true;
                FortressSeats.CameraShift = _toShift;
                var rig = FortressCameraRig.Active;
                if (settings.cameraPullBack && settings.cameraPullAmount > 0f && rig != null)
                {
                    rig.AddModifier(new SeatPullBackModifier(settings.cameraPullAmount, settings.cameraBlendSeconds));
                }
            }

            var up = ScreenUp();
            var anticipation = settings.AnticipationTime;
            var crouch = anticipation > 0f ? 1f - Mathf.Pow(1f - Mathf.Clamp01(_time / anticipation), 3f) : 0f;
            var spinSign = FortressSeats.Clockwise ? -1f : 1f;
            var hang = settings.hangAtApex ? settings.hangPower : 1f;
            foreach (var jumper in Jumpers)
            {
                if (jumper.Turret == null)
                {
                    continue;
                }

                var t = jumper.Turret.transform;
                var flightTime = _time - (anticipation + settings.StaggerTime * jumper.Rank);
                if (flightTime < 0f)
                {
                    // 溜め（順番待ちの砲台は、しゃがんだまま震えて待つ）。
                    var squat = settings.crouchAmount * crouch;
                    var shake = settings.crouchShake * crouch * (_hitStopLeft > 0f ? 0f : 1f);
                    var jitter = shake > 0f ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero;
                    jumper.Ground = jumper.From;
                    jumper.Height01 = 0f;
                    t.position = jumper.From + jitter;
                    t.localScale = Squashed(jumper.BaseScale, 1f, squat);
                    continue;
                }

                if (!jumper.Launched)
                {
                    jumper.Launched = true;
                    jumper.Flying = true;
                    PlayEffect(settings.onJump, jumper, jumper.From, jumper.Rank == 0);
                    FortressSeats.RaiseTurretLaunched(jumper);
                }

                var pose = FortressSeatMath.Evaluate(flightTime, settings.FlightSeconds, settings.landSquash, settings.landSquashSeconds, hang);
                var ground = Vector3.Lerp(jumper.From, jumper.To, pose.Progress01);
                var grow = 1f + settings.jumpScale * pose.Height01;
                var squash = pose.Squash;
                if (settings.anticipation && flightTime < LaunchStretchSeconds && !pose.Landed)
                {
                    squash -= settings.launchStretch * (1f - flightTime / LaunchStretchSeconds); // 跳ぶ瞬間に縦へ伸びる
                }

                jumper.Ground = ground;
                jumper.Height01 = pose.Height01;
                t.position = ground + up * (settings.jumpHeight * pose.Height01);
                t.localScale = Squashed(jumper.BaseScale, grow, squash);
                var spin = settings.spinInAir && settings.spinTurns > 0 && !pose.Landed ? spinSign * 360f * settings.spinTurns * pose.Progress01 : 0f;
                t.rotation = jumper.BaseRotation * Quaternion.Euler(0f, 0f, spin);

                if (!jumper.Landed && flightTime >= settings.FlightSeconds)
                {
                    jumper.Landed = true;
                    jumper.Flying = false;
                    jumper.Height01 = 0f;
                    t.position = jumper.To;
                    t.rotation = jumper.BaseRotation;
                    Land(settings, jumper);
                }
            }

            var motionEnd = settings.TotalSeconds - settings.HitStopTotal;
            if (_time >= motionEnd && _time >= settings.CameraStartTime && _hitStopLeft <= 0f)
            {
                FinishMove(true);
            }
        }

        private static Vector3 Squashed(Vector3 baseScale, float grow, float squash)
        {
            return Vector3.Scale(baseScale, new Vector3(grow * (1f + squash), grow * (1f - squash), 1f));
        }

        private void Land(WaveSeatRotation settings, SeatJumper jumper)
        {
            // 着地の音は4基それぞれ鳴らす（順番に着地すると「ドドドドン」になる）。
            PlayEffect(settings.onLand, jumper, jumper.To, true);
            if (settings.hitStop && settings.hitStopSeconds > 0f)
            {
                _hitStopLeft = settings.hitStopSeconds;
                FortressSeats.IsHitStopped = true;
            }

            var rig = FortressCameraRig.Active;
            if (settings.shakeOnLand && settings.landShake != null && rig != null)
            {
                rig.AddModifier(new SimpleShakeModifier(settings.landShake, settings.StaggerTime > 0f ? 0.6f : 1f));
            }

            FortressSeats.RaiseTurretLanded(jumper);
        }

        private void FinishMove(bool completed)
        {
            foreach (var jumper in Jumpers)
            {
                if (jumper.Turret == null)
                {
                    continue;
                }

                var t = jumper.Turret.transform;
                t.position = jumper.To;
                t.localScale = jumper.BaseScale;
                t.rotation = jumper.BaseRotation;
            }

            Jumpers.Clear();
            _moving = false;
            _hitStopLeft = 0f;
            _appliedShift = _toShift;
            FortressSeats.CameraShift = _toShift;
            FortressSeats.IsMoving = false;
            FortressSeats.IsHitStopped = false;
            FortressSeats.MoveTime = 0f;
            FortressSeats.LastMoveCompleted = completed;
            UnlockInput();
            if (completed)
            {
                var settings = _settings ?? Settings;
                FortressSeats.FinaleStart = Time.unscaledTime;
                FortressSeats.YouMarkerUntil = Time.unscaledTime + settings.youMarkerSeconds;
            }

            FortressSeats.RaiseMoveFinished(_toShift);
        }

        // 跳ばずに、その場（shift 回進めた席）へ置く。
        private void SnapTo(int shift)
        {
            EnsureSeats();
            foreach (var turret in FindTurrets())
            {
                var seat = SeatPosition(FortressSeats.SeatOf(turret.playerIndex, shift));
                var position = turret.transform.position;
                turret.transform.position = new Vector3(seat.x, seat.y, position.z);
            }

            _appliedShift = shift;
            FortressSeats.CameraShift = shift;
        }

        // ───────── 席の場所 ─────────

        // ステージから知らされていなければ、最初の席替えの直前に砲台の今の場所を席にする（まだ1回も回っていないので、プレイヤー p が席 p）。
        private void EnsureSeats()
        {
            if (FortressSeats.TryGetRegisteredSeats(out _, out _) || _hasCaptured)
            {
                return;
            }

            var sum = Vector2.zero;
            var count = 0;
            foreach (var turret in FindTurrets())
            {
                var seat = FortressSeats.SeatOf(turret.playerIndex, _appliedShift);
                if (seat < 0 || seat >= _capturedSeats.Length)
                {
                    continue;
                }

                _capturedSeats[seat] = turret.transform.position;
                sum += _capturedSeats[seat];
                count++;
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            var center = core != null ? (Vector2)core.transform.position : count > 0 ? sum / count : Vector2.zero;
            _hasCaptured = true;
            FortressSeats.UseCapturedSeats(_capturedSeats, center);
        }

        private Vector2 SeatPosition(int seat)
        {
            if (FortressSeats.TryGetRegisteredSeats(out var seats, out _) && seat >= 0 && seat < seats.Length)
            {
                return seats[seat];
            }

            return seat >= 0 && seat < _capturedSeats.Length ? _capturedSeats[seat] : Vector2.zero;
        }

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

        // ───────── そのほか ─────────

        // 跳んでいる間は撃てない・回らない。合間（歯みがきタイム）と席替えの段階はウェーブの係が止めているので、そのままにする。
        private void LockInput()
        {
            LaserTurret.InputLocked = true;
            _lockedInput = true;
        }

        private void UnlockInput()
        {
            if (!_lockedInput)
            {
                return;
            }

            _lockedInput = false;
            if (FortressWaves.Phase != WavePhase.Break && FortressWaves.Phase != WavePhase.SeatChange)
            {
                LaserTurret.InputLocked = false;
            }
        }

        // VFX は砲台ごと（プレイヤーの色）。SE は withSound のときだけ（跳ぶ音は自分の砲台で1回、着地の音は4基それぞれ）。
        private static void PlayEffect(FortressEffect effect, SeatJumper jumper, Vector3 position, bool withSound)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            FortressEffectPlayer.SpawnVfx(effect, position, Quaternion.identity, jumper.Player);
            if (withSound)
            {
                FortressEffectPlayer.PlaySound(effect, position);
            }
        }

        internal static Vector3 ScreenUp()
        {
            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            if (camera == null)
            {
                return Vector3.up;
            }

            var up = camera.transform.up;
            up.z = 0f;
            return up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        }

        internal static int LocalPlayer()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap != null && bootstrap.LocalPlayerIndex >= 0)
            {
                return bootstrap.LocalPlayerIndex;
            }

            var rig = FortressCameraRig.Active;
            return rig != null && ViewerIndex.IsPlayer(rig.CurrentViewer) ? rig.CurrentViewer : 0;
        }
    }
}
