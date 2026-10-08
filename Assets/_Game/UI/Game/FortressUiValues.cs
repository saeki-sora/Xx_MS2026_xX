using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ゲーム（砲台・コア・群衆・ウェーブ・通信）の今の状態を、画面の値の掲示板（UiValues）に毎フレーム書き込む。
    /// 値が変わらないフレームは何も起きないので軽い。UIの置き場所（[UI]）に1つ付けておく（UIスタジオが自動で付ける）。
    /// 値の名前は <see cref="FortressUiKeys"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI Studio/ゲームの値を画面へ (FortressUiValues)")]
    public sealed class FortressUiValues : MonoBehaviour
    {
        private const float RefreshInterval = 1f;

        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private readonly string[] _labels = { "P1", "P2", "P3", "P4" };
        private CoreCrystalController _core;
        private EnemySpawnDirector _director;
        private FortressNetworkBootstrap _bootstrap;
        private float _nextRefresh;

        private void OnEnable()
        {
            _nextRefresh = 0f;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshInterval;
                FindSources();
            }

            var local = ResolveLocalPlayer();
            PublishPlayers(local);
            PublishCore();
            PublishEnemies();
            PublishNet(local);
        }

        private void FindSources()
        {
            _turrets.Clear();
            _turrets.AddRange(FindObjectsByType<LaserTurret>(FindObjectsSortMode.None));
            _core = FindFirstObjectByType<CoreCrystalController>();
            _director = FindFirstObjectByType<EnemySpawnDirector>();
            _bootstrap = FortressNetworkBootstrap.Instance != null ? FortressNetworkBootstrap.Instance : FindFirstObjectByType<FortressNetworkBootstrap>();
        }

        private int ResolveLocalPlayer()
        {
            if (_bootstrap != null && _bootstrap.LocalPlayerIndex >= 0)
            {
                return _bootstrap.LocalPlayerIndex;
            }

            var rig = FortressCameraRig.Active;
            return rig != null && ViewerIndex.IsPlayer(rig.CurrentViewer) ? rig.CurrentViewer : 0;
        }

        private void PublishPlayers(int local)
        {
            UiValues.Set(FortressUiKeys.LocalPlayer, local);
            UiValues.Set(FortressUiKeys.LocalPlayerLabel, local >= 0 && local < 4 ? _labels[local] : "-");
            UiValues.Set(FortressUiKeys.LocalColor, FortressColors.PlayerColor(Mathf.Max(0, local)));

            var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
            foreach (var turret in _turrets)
            {
                if (turret == null)
                {
                    continue;
                }

                var p = turret.playerIndex;
                var overheated = turret.State == TurretState.Overheated;
                var grip = provider != null ? provider.GetGripValue(p) : 0f;
                UiValues.Set(FortressUiKeys.PlayerHeat(p), turret.HeatRatio01);
                UiValues.Set(FortressUiKeys.PlayerOverheated(p), overheated);
                UiValues.Set(FortressUiKeys.PlayerFiring(p), turret.IsFiring);
                UiValues.Set(FortressUiKeys.PlayerGrip(p), grip);
                UiValues.Set(FortressUiKeys.PlayerColor(p), FortressColors.PlayerColor(p));

                if (p == local)
                {
                    UiValues.Set(FortressUiKeys.LocalHeat, turret.HeatRatio01);
                    UiValues.Set(FortressUiKeys.LocalOverheated, overheated);
                    UiValues.Set(FortressUiKeys.LocalFiring, turret.IsFiring);
                    UiValues.Set(FortressUiKeys.LocalCharge, turret.ChargeProgress01);
                    UiValues.Set(FortressUiKeys.LocalThickness, turret.CurrentThickness01);
                    UiValues.Set(FortressUiKeys.LocalGrip, grip);
                }
            }
        }

        private void PublishCore()
        {
            if (_core == null)
            {
                return;
            }

            var max = Mathf.Max(1f, _core.maxHealth);
            UiValues.Set(FortressUiKeys.CoreHp, _core.CurrentHealth);
            UiValues.Set(FortressUiKeys.CoreHp01, Mathf.Clamp01(_core.CurrentHealth / max));
            UiValues.Set(FortressUiKeys.CoreDestroyed, _core.CurrentHealth <= 0f);
        }

        private void PublishEnemies()
        {
            var swarm = SwarmSystem.Current;
            if (swarm != null)
            {
                UiValues.Set(FortressUiKeys.SwarmAlive, swarm.Stats.alive);
            }

            if (_director != null)
            {
                UiValues.Set(FortressUiKeys.WaveTime, Mathf.Floor(_director.ElapsedTime * 10f) / 10f);
                UiValues.Set(FortressUiKeys.WavePlaying, _director.IsPlaying);
            }
        }

        private void PublishNet(int local)
        {
            var manager = NetworkManager.Singleton;
            var listening = manager != null && manager.IsListening;
            UiValues.Set(FortressUiKeys.NetMode, !listening ? "オフライン" : manager.IsHost ? "ホスト" : "参加中");
            if (_bootstrap != null && !string.IsNullOrEmpty(_bootstrap.LastDisconnectReason))
            {
                UiValues.Set(FortressUiKeys.NetLastError, _bootstrap.LastDisconnectReason);
            }

            for (var p = 0; p < 4; p++)
            {
                UiValues.Set(FortressUiKeys.PlayerConnected(p), IsConnected(p, local, listening, manager));
            }

            var session = GameSession.Active;
            UiValues.Set(FortressUiKeys.NetPlayers, session != null && listening ? session.Roster.PresentCount
                : listening && manager.IsHost && _bootstrap != null ? _bootstrap.ClientIdToPlayerIndex.Count : listening ? 1 : 0);
        }

        private bool IsConnected(int player, int local, bool listening, NetworkManager manager)
        {
            // ロビーから来たときは、ホストが配る部屋の一覧で全員分がわかる（参加側でも正しい）。
            var session = GameSession.Active;
            if (session != null && listening)
            {
                return session.Roster.seats[player].present;
            }

            if (!listening)
            {
                return player == local;
            }

            if (player == local)
            {
                return true;
            }

            if (!manager.IsHost || _bootstrap == null)
            {
                return false; // 参加側には他の人の接続状況が届かない
            }

            foreach (var pair in _bootstrap.ClientIdToPlayerIndex)
            {
                if (pair.Value == player)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
