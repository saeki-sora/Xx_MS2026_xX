using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合の始まり（全員を待っている間と、WAVE 1 の発表の前のヒントの段階）に、このPCのプレイヤーの砲台の上に「YOU」の矢印を出す（2026-10-24、初めての人向け）。
    /// 視点がPCごとに違う（回っている人もいる）ので、「自分の砲台はどれか」をはっきり見せる。各PCが自分のカメラで場所を計算する。
    /// 席替え（ウェーブごとに隣の席へ跳ぶ。2026-10-31）で着地した後にも、決まりの秒数だけ出す。
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

        [Tooltip("矢印（砲台を指す）。砲台が画面の上の方にあるときは、YOU を砲台の下に出して矢印を上向きにする。")]
        public RectTransform arrow;

        [Tooltip("「YOU」の札。")]
        public RectTransform tag;

        [Tooltip("画面の上からこの px の帯（WAVE の札・コアのバーがある所）には重ねない。")]
        public float topReserved = 230f;

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

            var timing = WaveDisplayTiming.Current;
            // 席替えで着地した後も少しの間出す（FortressSeats.YouMarkerVisible。席替えの決まりで切り替え・秒数を変えられる）。
            var show = FortressWaves.IsActive &&
                       ((timing.showYouMarker && WaveDisplayTiming.IsHintPhase(FortressWaves.Phase)) || FortressSeats.YouMarkerVisible);
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
            var bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f + SeatDrop();
            // 上に出すと画面の上の札（WAVE・コアのバー）に重なるときは、砲台の下に出して上を指す。
            var below = local.y + offset + marker.sizeDelta.y * 0.5f > _rect.rect.height * 0.5f - topReserved;
            marker.anchoredPosition = local + new Vector2(0f, below ? -(offset + bob) : offset + bob);
            if (arrow != null && tag != null)
            {
                arrow.anchoredPosition = new Vector2(0f, below ? 36f : -36f);
                arrow.localRotation = Quaternion.Euler(0f, 0f, below ? 0f : 180f);
                tag.anchoredPosition = new Vector2(0f, below ? -42f : 42f);
            }
            var color = FortressColors.PlayerColor(player);
            foreach (var graphic in tinted)
            {
                if (graphic != null)
                {
                    graphic.color = new Color(color.r, color.g, color.b, graphic.color.a);
                }
            }
        }

        // 席替えの「YOU の光の柱」が ON なら、着地の後に矢印が上から弾んで落ちてくる（2026-11-02。砲台から離れる量の px）。
        private static float SeatDrop()
        {
            var settings = FortressSeats.Settings;
            if (settings == null || !settings.youPillar || !FortressSeats.YouMarkerVisible)
            {
                return 0f;
            }

            var u = Mathf.Clamp01(FortressSeats.FinaleAge / 0.55f);
            return (1f - u) * (1f - u) * 420f * Mathf.Abs(Mathf.Cos(u * Mathf.PI * 2.5f));
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
