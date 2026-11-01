using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 席替えの今の様子（全員のPCで同じ値。2026-10-31）。
    /// 「席」はステージの砲台の目印（Turret_P1〜P4）の番号で、試合の始まりはプレイヤー p が席 p。
    /// 何回席替えしたか（<see cref="Shift"/>）は Host（オフラインでは自分）がウェーブの番号から決め、ウェーブの様子と一緒に配る（通信はウェーブの1バイトだけ）。
    /// 砲台を跳ばす・カメラを動かすのは各PCの係（<see cref="FortressSeatMover"/>）が自分で行う。
    /// カメラ（<see cref="Cameras.FortressCameraRig"/>）は <see cref="CameraSeatOf"/> の席の視点を使う。
    /// </summary>
    public static class FortressSeats
    {
        public const int SeatCount = 4;

        private static object _seatOwner;
        private static Vector2[] _registeredSeats;
        private static Vector2 _registeredCenter;
        private static int[] _order;

        /// <summary>何回席替えしたか（0〜3。4回で一周して元の席）。</summary>
        public static int Shift { get; private set; }

        /// <summary>カメラが映している席替えの回数（跳び始めて少ししてから <see cref="Shift"/> に追いつく）。</summary>
        public static int CameraShift { get; internal set; }

        /// <summary>今の試合の決まり（ウェーブの計画から。無ければ null = 席替えしない）。</summary>
        public static WaveSeatRotation Settings { get; internal set; }

        /// <summary>回る向き。</summary>
        public static bool Clockwise => Settings == null || Settings.Clockwise;

        /// <summary>カメラが次の席の視点へ移る秒数。</summary>
        public static float CameraBlendSeconds => Settings != null ? Settings.cameraBlendSeconds : 1f;

        /// <summary>砲台が跳んでいる最中か。</summary>
        public static bool IsMoving { get; internal set; }

        /// <summary>着地してから YOU の矢印を出している間か（このPCで数える）。</summary>
        public static bool YouMarkerVisible => Settings != null && Settings.showYouMarker && Time.unscaledTime < YouMarkerUntil;

        internal static float YouMarkerUntil { get; set; } = -1f;

        /// <summary>席の並び（時計回り。席の場所が分かるまでは番号順）。</summary>
        public static int[] Order => _order ??= DefaultOrder();

        /// <summary>席替えが始まった（新しい回数）。全員のPCで1回ずつ。</summary>
        public static event Action<int> MoveStarted;

        /// <summary>全員が着地して、つぶれも戻った（新しい回数）。</summary>
        public static event Action<int> MoveFinished;

        /// <summary>プレイヤー player が今座っている席（跳んでいる最中は行き先）。</summary>
        public static int SeatOf(int player) => FortressSeatMath.SeatOf(player, Shift, Order, Clockwise);

        /// <summary>席替えを shift 回進めたときのプレイヤー player の席。</summary>
        public static int SeatOf(int player, int shift) => FortressSeatMath.SeatOf(player, shift, Order, Clockwise);

        /// <summary>プレイヤー player のカメラが今使う席（カメラのプリセットの P1〜P4 のどれを使うか）。</summary>
        public static int CameraSeatOf(int player) => FortressSeatMath.SeatOf(player, CameraShift, Order, Clockwise);

        /// <summary>
        /// ステージの席の場所を知らせる（<c>StageLayout</c> が砲台を置いたときに呼ぶ）。seats は席 0〜3 の場所、center はコア。
        /// 知らされていないとき（古い Game だけのシーン）は、係が最初の席替えの直前に砲台の場所を席にする。
        /// </summary>
        public static void RegisterSeats(object owner, Vector2[] seats, Vector2 center)
        {
            if (seats == null || seats.Length != SeatCount)
            {
                return;
            }

            _seatOwner = owner;
            _registeredSeats = (Vector2[])seats.Clone();
            _registeredCenter = center;
            _order = FortressSeatMath.ClockwiseOrder(_registeredSeats, center);
        }

        /// <summary>owner が知らせた席を取り消す（ステージのシーンが閉じたとき）。</summary>
        public static void UnregisterSeats(object owner)
        {
            if (!ReferenceEquals(owner, _seatOwner))
            {
                return;
            }

            _seatOwner = null;
            _registeredSeats = null;
            _order = null;
        }

        /// <summary>ステージから知らされた席の場所（無ければ false）。</summary>
        public static bool TryGetRegisteredSeats(out Vector2[] seats, out Vector2 center)
        {
            seats = _registeredSeats;
            center = _registeredCenter;
            return seats != null;
        }

        /// <summary>係が砲台の場所から席を決めたとき（ステージから知らされていないとき）。</summary>
        internal static void UseCapturedSeats(Vector2[] seats, Vector2 center)
        {
            if (_registeredSeats == null && seats != null && seats.Length == SeatCount)
            {
                _order = FortressSeatMath.ClockwiseOrder(seats, center);
            }
        }

        internal static void SetShift(int shift)
        {
            Shift = FortressSeatMath.Mod(shift, SeatCount);
        }

        /// <summary>新しい試合（または試合の外）: 全員を最初の席に戻す。</summary>
        internal static void ResetForMatch(WaveSeatRotation settings)
        {
            Settings = settings;
            Shift = 0;
            CameraShift = 0;
            IsMoving = false;
            YouMarkerUntil = -1f;
            if (_registeredSeats == null)
            {
                _order = null;
            }
        }

        internal static void RaiseMoveStarted(int shift) => MoveStarted?.Invoke(shift);

        internal static void RaiseMoveFinished(int shift) => MoveFinished?.Invoke(shift);

        // ───────── テスト用（要塞デザイナー。Host／オフラインだけ効く） ─────────

        internal static Action DebugRotateHandler;
        internal static Action DebugResetHandler;

        /// <summary>テスト用: 今すぐ1つ席替えする。</summary>
        public static void DebugRotate() => DebugRotateHandler?.Invoke();

        /// <summary>テスト用: 全員を最初の席に戻す。</summary>
        public static void DebugReset() => DebugResetHandler?.Invoke();

        private static int[] DefaultOrder()
        {
            var order = new int[SeatCount];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            return order;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetForMatch(null);
            _seatOwner = null;
            _registeredSeats = null;
            _order = null;
            MoveStarted = null;
            MoveFinished = null;
            DebugRotateHandler = null;
            DebugResetHandler = null;
        }
    }
}
