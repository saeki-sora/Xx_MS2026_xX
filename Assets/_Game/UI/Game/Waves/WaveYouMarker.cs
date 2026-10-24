using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合の最初（全員を待っている間と WAVE 1 の発表の間）に、このPCのプレイヤーの砲台の上に「YOU」の矢印を出す（2026-10-24、初めての人向け）。
    /// 視点がPCごとに違う（回っている人もいる）ので、「自分の砲台はどれか」をはっきり見せる。各PCが自分のカメラで場所を計算する。
    /// UIスタジオの「ウェーブの表示を入れる」が作る「WaveYouMarker」（画面いっぱいの枠）に付く。
    /// </summary>
    [AddComponentMenu("UI Studio/あなたの砲台の矢印 (WaveYouMarker)")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WaveYouMarker : MonoBehaviour
    {
        [Tooltip("矢印と「YOU」をまとめた物（これを砲台の上に動かす）。")]
        public RectTransform marker;

        [Tooltip("プレイヤーの色にする物（矢印・文字）。")]
        public Graphic[] tinted = new Graphic[0];

        [Tooltip("砲台から画面の上へ離す量（HUD の基準 1920×1080 での px）。")]
        public float offset = 110f;

        private RectTransform _rect;
        private LaserTurret _turret;
        private int _turretPlayer = -1;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            if (marker == null)
            {
                return;
            }

            var show = FortressWaves.IsActive && FortressWaves.WaveIndex == 0 &&
                       (FortressWaves.Phase == WavePhase.WaitingForPlayers || FortressWaves.Phase == WavePhase.Announce);
            var player = LocalPlayer();
            var turret = show ? FindTurret(player) : null;
            var rig = FortressCameraRig.Active;
            var camera = rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
            if (turret == null || camera == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            var screen = camera.WorldToScreenPoint(turret.transform.position);
            if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, null, out var local))
            {
                marker.gameObject.SetActive(false);
                return;
            }

            marker.gameObject.SetActive(true);
            var bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f;
            marker.anchoredPosition = local + new Vector2(0f, offset + bob);
            var color = FortressColors.PlayerColor(player);
            foreach (var graphic in tinted)
            {
                if (graphic != null)
                {
                    graphic.color = new Color(color.r, color.g, color.b, graphic.color.a);
                }
            }
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

        private LaserTurret FindTurret(int player)
        {
            if (_turret != null && _turretPlayer == player)
            {
                return _turret;
            }

            _turretPlayer = player;
            _turret = null;
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret.playerIndex == player)
                {
                    _turret = turret;
                    break;
                }
            }

            return _turret;
        }
    }
}
