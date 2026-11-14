using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で動く砲台の位置と、やられる・復活の調子を LAN で配る。
    /// ・各 PC が自分の砲台を自分で動かし（すぐ反応させるため）、位置だけを送る。
    /// ・Client → Host: 自分の位置（送り主のプレイヤー番号は接続のときに決まった番号から引く。Client が他人の砲台を動かすことはできない）。
    /// ・Host → 全 Client: 4人分の位置（Client は自分の分は使わない）＋ 4人分の調子（倒れている・汚れ・復活の進み具合。Host が決める）。
    /// 席替えの回数（<see cref="FortressSeats.Shift"/>）を一緒に送り、回数が違う古い位置は捨てる（席替えの直後に前の席へ戻らないように）。
    /// NetworkObject は使わない（名前なしメッセージ <see cref="FortressNetChannel.TurretMove"/>）。取りこぼしても次がすぐ届くので Unreliable。
    /// </summary>
    internal sealed class TurretMoveNet
    {
        private const int PlayerCount = 4;

        // Client → Host の1人分 = 席替えの回数(1) + x(4) + y(4)。
        private const int OwnEntryBytes = 9;

        // Host → Client の1人分 = 位置があるか(1) + 席替えの回数(1) + x(4) + y(4) + 調子(1) + 汚れ(1) + 復活(1)。
        private const int HostEntryBytes = 13;

        private readonly Vector2[] _targets = new Vector2[PlayerCount];
        private readonly bool[] _hasTarget = new bool[PlayerCount];

        private readonly TurretCondition[] _conditions = new TurretCondition[PlayerCount];
        private readonly float[] _dirt = new float[PlayerCount];
        private readonly float[] _revive = new float[PlayerCount];
        private bool _hasConditions;

        private NetworkManager _manager;
        private CustomMessagingManager _messaging;
        private float _sendTimer;
        private int _lastShift;

        /// <summary>通信中（Host か Client）か。</summary>
        public bool IsActive { get; private set; }

        /// <summary>Client として Host に従っているか（調子は Host から届いた物を使う）。</summary>
        public bool IsClient => IsActive && _manager != null && !_manager.IsServer;

        public bool TryGetTarget(int player, out Vector2 position)
        {
            position = player >= 0 && player < PlayerCount ? _targets[player] : default;
            return player >= 0 && player < PlayerCount && _hasTarget[player];
        }

        /// <summary>Client: Host から届いた調子（まだ1回も届いていなければ false）。</summary>
        public bool TryGetCondition(int player, out TurretCondition condition, out float dirt01, out float revive01)
        {
            var ok = _hasConditions && player >= 0 && player < PlayerCount;
            condition = ok ? _conditions[player] : TurretCondition.Normal;
            dirt01 = ok ? _dirt[player] : 0f;
            revive01 = ok ? _revive[player] : 0f;
            return ok;
        }

        public void Clear()
        {
            for (var i = 0; i < PlayerCount; i++)
            {
                _hasTarget[i] = false;
            }
        }

        public void Shutdown()
        {
            Unregister();
            Clear();
            _hasConditions = false;
            IsActive = false;
        }

        /// <summary>毎フレーム呼ぶ。届いた位置の受け取り口を用意し、決まった間隔で送る。</summary>
        /// <param name="held">落下演出などで砲台を別の係が動かしている間。その間は位置を送らない（画面の外の位置を配らないように）。</param>
        public void Tick(float dt, TurretMoveSettings settings, LaserTurret[] turrets, int localPlayer, bool held, TurretDownSystem down)
        {
            EnsureRegistered();
            if (!IsActive)
            {
                Clear();
                _hasConditions = false;
                return;
            }

            if (FortressSeats.Shift != _lastShift)
            {
                // 席替えが始まった。前の席での位置はもう使わない。
                _lastShift = FortressSeats.Shift;
                Clear();
            }

            _sendTimer -= dt;
            if (_sendTimer > 0f)
            {
                return;
            }

            _sendTimer = 1f / Mathf.Max(1f, settings.sendRate);
            // 席替え・落下演出の間は各 PC が自分で動かすので、位置は送らない（調子は送る）。
            var sendPositions = !FortressSeats.IsMoving && !held;
            if (_manager.IsServer)
            {
                Broadcast(turrets, localPlayer, sendPositions, down);
            }
            else if (sendPositions)
            {
                SendOwn(turrets, localPlayer);
            }
        }

        // ───────── 送る ─────────

        private void SendOwn(LaserTurret[] turrets, int localPlayer)
        {
            var turret = localPlayer >= 0 && localPlayer < PlayerCount ? turrets[localPlayer] : null;
            if (turret == null)
            {
                return;
            }

            var writer = FortressNetMessages.Begin(FortressNetChannel.TurretMove, OwnEntryBytes);
            try
            {
                writer.WriteValueSafe((byte)FortressSeats.Shift);
                writer.WriteValueSafe(turret.transform.position.x);
                writer.WriteValueSafe(turret.transform.position.y);
                FortressNetMessages.Send(_manager, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void Broadcast(LaserTurret[] turrets, int localPlayer, bool sendPositions, TurretDownSystem down)
        {
            var clients = _manager.ConnectedClientsIds;
            if (clients.Count <= 1)
            {
                return;
            }

            var writer = FortressNetMessages.Begin(FortressNetChannel.TurretMove, HostEntryBytes * PlayerCount);
            try
            {
                for (var p = 0; p < PlayerCount; p++)
                {
                    var known = HasBroadcastPosition(turrets, localPlayer, p, out var position);
                    var hasPosition = sendPositions && known;
                    var state = down.States[p];
                    writer.WriteValueSafe((byte)(hasPosition ? 1 : 0));
                    writer.WriteValueSafe((byte)FortressSeats.Shift);
                    writer.WriteValueSafe(position.x);
                    writer.WriteValueSafe(position.y);
                    writer.WriteValueSafe((byte)state.Condition);
                    writer.WriteValueSafe(To255(state.Dirt01));
                    writer.WriteValueSafe(To255(state.Revive01));
                }

                foreach (var clientId in clients)
                {
                    if (clientId != NetworkManager.ServerClientId)
                    {
                        FortressNetMessages.Send(_manager, clientId, writer, NetworkDelivery.Unreliable);
                    }
                }
            }
            finally
            {
                writer.Dispose();
            }
        }

        // Host の自分の分は砲台の今の場所、他の人の分は届いたそのままの位置（Host でなめらかにした後の位置ではない）。
        private bool HasBroadcastPosition(LaserTurret[] turrets, int localPlayer, int player, out Vector2 position)
        {
            if (player == localPlayer && turrets[player] != null)
            {
                position = turrets[player].transform.position;
                return true;
            }

            position = _targets[player];
            return _hasTarget[player];
        }

        private static byte To255(float value01) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value01) * 255f);

        // ───────── 受け取る ─────────

        private void OnMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_manager == null)
            {
                return;
            }

            if (_manager.IsServer)
            {
                ReceiveOwn(senderClientId, ref reader);
                return;
            }

            if (senderClientId != NetworkManager.ServerClientId || !reader.TryBeginRead(HostEntryBytes * PlayerCount))
            {
                return;
            }

            var local = FortressSeatMover.LocalPlayer();
            for (var p = 0; p < PlayerCount; p++)
            {
                reader.ReadValue(out byte hasPosition);
                reader.ReadValue(out byte shift);
                reader.ReadValue(out float x);
                reader.ReadValue(out float y);
                reader.ReadValue(out byte condition);
                reader.ReadValue(out byte dirt);
                reader.ReadValue(out byte revive);

                _conditions[p] = (TurretCondition)condition;
                _dirt[p] = dirt / 255f;
                _revive[p] = revive / 255f;

                if (hasPosition != 0 && p != local && !FortressSeats.IsMoving && shift == (byte)FortressSeats.Shift)
                {
                    _targets[p] = new Vector2(x, y);
                    _hasTarget[p] = true;
                }
            }

            _hasConditions = true;
        }

        private void ReceiveOwn(ulong senderClientId, ref FastBufferReader reader)
        {
            if (FortressSeats.IsMoving)
            {
                return;
            }

            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null || !bootstrap.ClientIdToPlayerIndex.TryGetValue(senderClientId, out var player) ||
                player < 0 || player >= PlayerCount || player == bootstrap.LocalPlayerIndex || !reader.TryBeginRead(OwnEntryBytes))
            {
                return;
            }

            reader.ReadValue(out byte shift);
            reader.ReadValue(out float x);
            reader.ReadValue(out float y);
            if (shift == (byte)FortressSeats.Shift)
            {
                _targets[player] = new Vector2(x, y);
                _hasTarget[player] = true;
            }
        }

        // ───────── 受け取り口 ─────────

        private void EnsureRegistered()
        {
            var manager = NetworkManager.Singleton;
            var listening = manager != null && manager.IsListening && manager.CustomMessagingManager != null;
            if (!listening)
            {
                Unregister();
                IsActive = false;
                return;
            }

            IsActive = true;
            if (ReferenceEquals(_manager, manager) && ReferenceEquals(_messaging, manager.CustomMessagingManager))
            {
                return;
            }

            // 接続のたびに CustomMessagingManager が作り直されるので、変わったら付け直す。
            _manager = manager;
            _messaging = manager.CustomMessagingManager;
            _lastShift = FortressSeats.Shift;
            _sendTimer = 0f;
            _hasConditions = false;
            Clear();
            FortressNetMessages.Register(manager, FortressNetChannel.TurretMove, OnMessage);
        }

        private void Unregister()
        {
            if (_manager == null)
            {
                return;
            }

            FortressNetMessages.Unregister(FortressNetChannel.TurretMove);
            _manager = null;
            _messaging = null;
        }
    }
}
