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

        [Header("撃破数")]
        [Tooltip("撃破数を画面へ書く間隔（秒）。敵がたくさん倒れても、数字の弾む動きが毎フレームやり直しにならないよう、まとめて書く。")]
        [Min(0f)]
        public float killPublishInterval = 0.1f;

        [Tooltip("この秒数のあいだ自分が倒さなければ、コンボが途切れる。")]
        [Min(0.1f)]
        public float comboWindowSeconds = 2.5f;

        [Tooltip("お祝いする全員の撃破数（小さい順）。")]
        public int[] killMilestones = { 100, 250, 500, 1000 };

        [Tooltip("最後の区切りより後は、この数ごとにお祝いする（0 = しない）。")]
        [Min(0)]
        public int killMilestoneStepAfterLast = 1000;

        [Tooltip("お祝いを出しておく秒数。")]
        [Min(0.2f)]
        public float killMilestoneShowSeconds = 2.2f;

        private readonly KillHudLogic _killLogic = new KillHudLogic();
        private float _nextKillPublish;

        private readonly List<LaserTurret> _turrets = new List<LaserTurret>();
        private readonly string[] _labels = { "P1", "P2", "P3", "P4" };
        private readonly string[] _otherLabels = { "1P", "2P", "3P", "4P" };
        private readonly float[] _heat = new float[4];
        private readonly bool[] _overheated = new bool[4];
        private readonly bool[] _connected = new bool[4];
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
            WaveUiPublisher.Publish();
            PublishKills(local);
            PublishNet(local);
            PublishOthers(local);
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
                if (p >= 0 && p < 4)
                {
                    _heat[p] = turret.HeatRatio01;
                    _overheated[p] = overheated;
                }

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
            WaveUiPublisher.PublishCoating(_core);
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

        // 撃破数（EnemyKillCounter。Host が数えて全員に配る）と、コンボ・お祝い（各PCで計算）。
        private void PublishKills(int local)
        {
            var now = Time.unscaledTime;
            if (now < _nextKillPublish)
            {
                return;
            }

            _nextKillPublish = now + killPublishInterval;
            var own = EnemyKillCounter.ByPlayer(local);
            var total = EnemyKillCounter.Total;
            _killLogic.comboWindowSeconds = comboWindowSeconds;
            _killLogic.milestones = killMilestones;
            _killLogic.milestoneStepAfterLast = killMilestoneStepAfterLast;
            _killLogic.milestoneShowSeconds = killMilestoneShowSeconds;
            _killLogic.Update(own, total, now);

            UiValues.Set(FortressUiKeys.KillsTotal, total);
            UiValues.Set(FortressUiKeys.KillsGoal, FortressMatch.ClearKills);
            UiValues.Set(FortressUiKeys.MatchOutcome, (int)FortressMatch.Outcome);
            UiValues.Set(FortressUiKeys.KillsLocal, own);
            UiValues.Set(FortressUiKeys.KillsCombo, _killLogic.Combo);
            UiValues.Set(FortressUiKeys.KillsComboTime01, _killLogic.ComboRemaining01);
            UiValues.Set(FortressUiKeys.KillsMilestone, _killLogic.Milestone);
            UiValues.Set(FortressUiKeys.KillsMilestoneShowing, _killLogic.MilestoneShowing);
            for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
            {
                UiValues.Set(FortressUiKeys.PlayerKills(p), EnemyKillCounter.ByPlayer(p));
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
                _connected[p] = IsConnected(p, local, listening, manager);
                UiValues.Set(FortressUiKeys.PlayerConnected(p), _connected[p]);
            }

            var session = GameSession.Active;
            UiValues.Set(FortressUiKeys.NetPlayers, session != null && listening ? session.Roster.PresentCount
                : listening && manager.IsHost && _bootstrap != null ? _bootstrap.ClientIdToPlayerIndex.Count : listening ? 1 : 0);
        }

        // 自分以外の3人を番号の小さい順に（HUD の左の3段。どのPCでも同じ位置に「ほかの人」が並ぶ）。
        private void PublishOthers(int local)
        {
            for (var s = 0; s < FortressUiKeys.OtherCount; s++)
            {
                var p = FortressUiKeys.OtherPlayerIndex(local, s);
                UiValues.Set(FortressUiKeys.OtherPlayer(s), p);
                UiValues.Set(FortressUiKeys.OtherLabel(s), _otherLabels[p]);
                UiValues.Set(FortressUiKeys.OtherColor(s), FortressColors.PlayerColor(p));
                UiValues.Set(FortressUiKeys.OtherHeat(s), _heat[p]);
                UiValues.Set(FortressUiKeys.OtherOverheated(s), _overheated[p]);
                UiValues.Set(FortressUiKeys.OtherConnected(s), _connected[p]);
            }
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
