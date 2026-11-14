using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】WASD で動く砲台の位置を LAN で配る。
    /// ・各 PC が自分の砲台を自分で動かし（すぐ反応させるため）、位置だけを送る。
    /// ・Client → Host: 自分の位置（送り主のプレイヤー番号は接続のときに決まった番号から引く。Client が他人の砲台を動かすことはできない）。
    /// ・Host → 全 Client: 4人分の位置。Client は自分の分は使わない。
    /// 席替えの回数（<see cref="FortressSeats.Shift"/>）を一緒に送り、回数が違う古い位置は捨てる（席替えの直後に前の席へ戻らないように）。
    /// NetworkObject は使わない（名前なしメッセージ <see cref="FortressNetChannel.TurretMove"/>）。取りこぼしても次がすぐ届くので Unreliable。
    /// </summary>
    internal sealed class TurretMoveNet
    {
        private const int PlayerCount = 4;

        // 1人分 = 席替えの回数(1) + x(4) + y(4)。
        private const int EntryBytes = 9;

        private readonly Vector2[] _targets = new Vector2[PlayerCount];
        private readonly bool[] _hasTarget = new bool[PlayerCount];

        private NetworkManager _manager;
        private CustomMessagingManager _messaging;
        private float _sendTimer;
        private int _lastShift;

        /// <summary>通信中（Host か Client）か。</summary>
        public bool IsActive { get; private set; }

        public bool TryGetTarget(int player, out Vector2 position)
        {
            position = player >= 0 && player < PlayerCount ? _targets[player] : default;
            return player >= 0 && player < PlayerCount && _hasTarget[player];
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
            IsActive = false;
        }

        /// <summary>毎フレーム呼ぶ。届いた位置の受け取り口を用意し、決まった間隔で送る。</summary>
        /// <param name="held">落下演出などで砲台を別の係が動かしている間。その間は送らない（画面の外の位置を配らないように）。</param>
        public void Tick(float dt, TurretMoveSettings settings, LaserTurret[] turrets, int localPlayer, bool held)
        {
            EnsureRegistered();
            if (!IsActive)
            {
                Clear();
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
            if (FortressSeats.IsMoving || held)
            {
                // 席替え・落下演出の間は各 PC が自分で動かすので送らない。
                return;
            }

            if (_manager.IsServer)
            {
                Broadcast(turrets, localPlayer);
            }
            else
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

            var writer = FortressNetMessages.Begin(FortressNetChannel.TurretMove, EntryBytes);
            try
            {
                WriteEntry(ref writer, turret.transform.position);
                FortressNetMessages.Send(_manager, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void Broadcast(LaserTurret[] turrets, int localPlayer)
        {
            var clients = _manager.ConnectedClientsIds;
            if (clients.Count <= 1)
            {
                return;
            }

            byte mask = 0;
            for (var p = 0; p < PlayerCount; p++)
            {
                if (HasBroadcastPosition(turrets, localPlayer, p, out _))
                {
                    mask |= (byte)(1 << p);
                }
            }

            if (mask == 0)
            {
                return;
            }

            var writer = FortressNetMessages.Begin(FortressNetChannel.TurretMove, 1 + EntryBytes * PlayerCount);
            try
            {
                writer.WriteValueSafe(mask);
                for (var p = 0; p < PlayerCount; p++)
                {
                    if (HasBroadcastPosition(turrets, localPlayer, p, out var position))
                    {
                        WriteEntry(ref writer, position);
                    }
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

        private static void WriteEntry(ref FastBufferWriter writer, Vector2 position)
        {
            writer.WriteValueSafe((byte)FortressSeats.Shift);
            writer.WriteValueSafe(position.x);
            writer.WriteValueSafe(position.y);
        }

        // ───────── 受け取る ─────────

        private void OnMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (_manager == null || FortressSeats.IsMoving)
            {
                return;
            }

            if (_manager.IsServer)
            {
                var bootstrap = FortressNetworkBootstrap.Instance;
                if (bootstrap == null || !bootstrap.ClientIdToPlayerIndex.TryGetValue(senderClientId, out var player) ||
                    player < 0 || player >= PlayerCount || player == bootstrap.LocalPlayerIndex)
                {
                    return;
                }

                if (TryReadEntry(ref reader, out var shift, out var position) && shift == (byte)FortressSeats.Shift)
                {
                    _targets[player] = position;
                    _hasTarget[player] = true;
                }

                return;
            }

            if (senderClientId != NetworkManager.ServerClientId || !reader.TryBeginRead(1))
            {
                return;
            }

            reader.ReadValue(out byte mask);
            var local = FortressSeatMover.LocalPlayer();
            for (var p = 0; p < PlayerCount; p++)
            {
                if ((mask & (1 << p)) == 0)
                {
                    continue;
                }

                if (!TryReadEntry(ref reader, out var shift, out var position))
                {
                    return;
                }

                if (p != local && shift == (byte)FortressSeats.Shift)
                {
                    _targets[p] = position;
                    _hasTarget[p] = true;
                }
            }
        }

        private static bool TryReadEntry(ref FastBufferReader reader, out byte shift, out Vector2 position)
        {
            shift = 0;
            position = default;
            if (!reader.TryBeginRead(EntryBytes))
            {
                return false;
            }

            reader.ReadValue(out shift);
            reader.ReadValue(out float x);
            reader.ReadValue(out float y);
            position = new Vector2(x, y);
            return true;
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
