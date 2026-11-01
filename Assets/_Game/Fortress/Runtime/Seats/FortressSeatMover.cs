using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの係（起動時に <see cref="FortressWaves"/> の係と一緒に自動で作られる、見えないオブジェクト。2026-10-31）。
    /// <see cref="FortressSeats.Shift"/> が1つ変わったら、全員の砲台を次の席へ「ピョン」と跳ばす（山なりに浮いて、着地でつぶれて戻る）。
    /// 各PCが自分で動かす（砲台の場所は通信しない。席替えの回数だけ Host から届く）。浮く向きは自分のカメラの画面の上。
    /// 跳んでいる間は砲台を撃てない・回らない。カメラは少し遅れて次の席の視点へ移り（<see cref="FortressSeats.CameraShift"/>）、
    /// 着地で自分の画面だけ揺れる。2つ以上離れた回数が届いたとき（途中から入った・テストで飛ばした）は跳ばずにその場へ置く。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(110)]
    internal sealed class FortressSeatMover : MonoBehaviour
    {
        private static readonly WaveSeatRotation DefaultSettings = new WaveSeatRotation();

        private readonly List<Mover> _movers = new List<Mover>();
        private readonly Vector2[] _capturedSeats = new Vector2[FortressSeats.SeatCount];
        private bool _hasCaptured;
        private SwarmSystem _swarm;
        private int _appliedShift;
        private bool _moving;
        private int _toShift;
        private float _time;
        private bool _landed;
        private bool _cameraMoved;
        private bool _lockedInput;

        private struct Mover
        {
            public LaserTurret Turret;
            public Vector3 From;
            public Vector3 To;
            public Vector3 BaseScale;
        }

        private static WaveSeatRotation Settings => FortressSeats.Settings ?? DefaultSettings;

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
            _movers.Clear();
            foreach (var turret in FindTurrets())
            {
                var seat = FortressSeats.SeatOf(turret.playerIndex, to);
                var position = turret.transform.position;
                var landing = SeatPosition(seat);
                _movers.Add(new Mover
                {
                    Turret = turret,
                    From = position,
                    To = new Vector3(landing.x, landing.y, position.z),
                    BaseScale = turret.transform.localScale
                });
            }

            _moving = true;
            _toShift = to;
            _time = 0f;
            _landed = false;
            _cameraMoved = false;
            FortressSeats.IsMoving = true;
            LockInput();
            PlayEffect(Settings.onJump, false);
            FortressSeats.RaiseMoveStarted(to);
            TickMove(0f);
        }

        private void TickMove(float dt)
        {
            var settings = Settings;
            _time += Mathf.Max(0f, dt);
            LockInput();
            if (!_cameraMoved && _time >= settings.cameraDelaySeconds)
            {
                _cameraMoved = true;
                FortressSeats.CameraShift = _toShift;
            }

            var pose = FortressSeatMath.Evaluate(_time, settings.jumpSeconds, settings.landSquash, settings.landSquashSeconds);
            var up = ScreenUp();
            var lift = up * (settings.jumpHeight * pose.Height01);
            var grow = 1f + settings.jumpScale * pose.Height01;
            var scale = new Vector3(grow * (1f + pose.Squash), grow * (1f - pose.Squash), 1f);
            foreach (var mover in _movers)
            {
                if (mover.Turret == null)
                {
                    continue;
                }

                var t = mover.Turret.transform;
                t.position = Vector3.Lerp(mover.From, mover.To, pose.Progress01) + lift;
                t.localScale = Vector3.Scale(mover.BaseScale, scale);
            }

            if (!_landed && _time >= settings.jumpSeconds)
            {
                _landed = true;
                Land(settings);
            }

            if (_time >= settings.TotalSeconds && _time >= settings.cameraDelaySeconds)
            {
                FinishMove(true);
            }
        }

        private void Land(WaveSeatRotation settings)
        {
            PlayEffect(settings.onLand, true);
            var rig = FortressCameraRig.Active;
            if (settings.shakeOnLand && settings.landShake != null && rig != null)
            {
                rig.AddModifier(new SimpleShakeModifier(settings.landShake, 1f));
            }
        }

        private void FinishMove(bool completed)
        {
            foreach (var mover in _movers)
            {
                if (mover.Turret == null)
                {
                    continue;
                }

                mover.Turret.transform.position = mover.To;
                mover.Turret.transform.localScale = mover.BaseScale;
            }

            _movers.Clear();
            _moving = false;
            _appliedShift = _toShift;
            FortressSeats.CameraShift = _toShift;
            FortressSeats.IsMoving = false;
            UnlockInput();
            if (completed)
            {
                FortressSeats.YouMarkerUntil = Time.unscaledTime + Settings.youMarkerSeconds;
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

        // VFX は砲台ごと（プレイヤーの色）、SE は自分の砲台で1回だけ（4つ重なってうるさくならないように）。
        private void PlayEffect(FortressEffect effect, bool atLanding)
        {
            if (effect == null || effect.IsEmpty)
            {
                return;
            }

            var local = LocalPlayer();
            var played = false;
            foreach (var mover in _movers)
            {
                if (mover.Turret == null)
                {
                    continue;
                }

                var position = atLanding ? mover.To : mover.From;
                var player = mover.Turret.playerIndex;
                FortressEffectPlayer.SpawnVfx(effect, position, Quaternion.identity, player);
                if (player == local)
                {
                    FortressEffectPlayer.PlaySound(effect, position);
                    played = true;
                }
            }

            if (!played)
            {
                FortressEffectPlayer.PlaySound(effect, Vector3.zero);
            }
        }

        private static Vector3 ScreenUp()
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

        private static int LocalPlayer()
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
