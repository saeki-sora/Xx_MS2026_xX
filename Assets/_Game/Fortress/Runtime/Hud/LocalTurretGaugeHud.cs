using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Fortress.Hud
{
    /// <summary>
    /// 自分の砲台の熱・チャージを画面下に出すゲージ。「自分」はカメラが映しているプレイヤー
    /// (ネット対戦なら自分のプレイヤー番号、オフラインなら起動引数やF1〜F4の切り替え)。全体視点のときは隠す。
    /// 絵を差し替えるときは、下の「差し替え用」に自分で作ったUIを割り当てる。未設定なら仮のゲージを自動で作る。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalTurretGaugeHud : MonoBehaviour
    {
        [Header("差し替え用（未設定なら仮のゲージを自動で作る）")]
        [Tooltip("表示/非表示を切り替えるまとまり。自分の砲台が無いとき(全体視点など)は非表示にする。")]
        public GameObject root;

        [Tooltip("熱ゲージの中身。ImageのTypeがFilled(画像あり)なら塗りの量で、それ以外は横幅で量を表す。")]
        public Image heatFill;

        [Tooltip("チャージゲージのまとまり。チャージ発射がOFFの設定では隠す。")]
        public GameObject chargeGaugeRoot;

        [Tooltip("チャージゲージの中身。熱ゲージと同じ仕組みで量を表す。")]
        public Image chargeFill;

        [Tooltip("オーバーヒート中だけ表示するもの。")]
        public GameObject overheatIndicator;

        [Tooltip("プレイヤー色で塗るもの(名前の文字や枠など)。")]
        public Graphic[] playerColorTargets;

        [Tooltip("「P1」などのプレイヤー名を出す文字。")]
        public Text playerLabel;

        [Header("色")]
        [Tooltip("熱ゲージの通常の色。")]
        public Color heatColor = new(1f, 0.6f, 0.15f);

        [Tooltip("熱がこの割合を超えたら危険色にする。")]
        [Range(0f, 1f)]
        public float dangerThreshold01 = 0.8f;

        [Tooltip("熱が危険域・オーバーヒート中の色。")]
        public Color dangerColor = new(1f, 0.2f, 0.15f);

        [Tooltip("チャージゲージの色。")]
        public Color chargeColor = new(0.4f, 0.85f, 1f);

        [Header("仮ゲージの大きさ（1920×1080基準）")]
        public Vector2 defaultGaugeSize = new(520f, 26f);
        public float defaultBottomMargin = 48f;

        private LaserTurret _turret;
        private int _turretPlayerIndex = int.MinValue;

        private void Awake()
        {
            if (heatFill == null)
            {
                DefaultGaugeBuilder.Build(this);
            }
        }

        private void LateUpdate()
        {
            var playerIndex = ResolveLocalPlayerIndex();
            if (playerIndex != _turretPlayerIndex || (_turret == null && ViewerIndex.IsPlayer(playerIndex)))
            {
                _turretPlayerIndex = playerIndex;
                _turret = FindTurret(playerIndex);
                ApplyPlayerColor(playerIndex);
            }

            var visible = _turret != null && _turret.tuning != null;
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            var overheated = _turret.State == TurretState.Overheated;
            var heat = overheated ? 1f : _turret.HeatRatio01;
            if (heatFill != null)
            {
                SetAmount(heatFill, heat);
                heatFill.color = overheated || heat >= dangerThreshold01 ? dangerColor : heatColor;
            }

            var showCharge = _turret.tuning.chargeToFireEnabled;
            if (chargeGaugeRoot != null && chargeGaugeRoot.activeSelf != showCharge)
            {
                chargeGaugeRoot.SetActive(showCharge);
            }

            if (showCharge && chargeFill != null)
            {
                SetAmount(chargeFill, _turret.ChargeProgress01);
                chargeFill.color = chargeColor;
            }

            if (overheatIndicator != null && overheatIndicator.activeSelf != overheated)
            {
                overheatIndicator.SetActive(overheated);
            }
        }

        private static int ResolveLocalPlayerIndex()
        {
            var rig = FortressCameraRig.Active;
            if (rig != null)
            {
                return rig.CurrentViewer;
            }

            var bootstrap = FortressNetworkBootstrap.Instance;
            return bootstrap != null ? bootstrap.LocalPlayerIndex : ViewerIndex.Overview;
        }

        private static LaserTurret FindTurret(int playerIndex)
        {
            if (!ViewerIndex.IsPlayer(playerIndex))
            {
                return null;
            }

            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (turret.playerIndex == playerIndex)
                {
                    return turret;
                }
            }

            return null;
        }

        private void ApplyPlayerColor(int playerIndex)
        {
            if (!ViewerIndex.IsPlayer(playerIndex))
            {
                return;
            }

            if (playerLabel != null)
            {
                playerLabel.text = ViewerIndex.Label(playerIndex);
            }

            if (playerColorTargets == null)
            {
                return;
            }

            var color = FortressColors.PlayerColor(playerIndex);
            foreach (var target in playerColorTargets)
            {
                if (target != null)
                {
                    target.color = color;
                }
            }
        }

        // 画像付きのFilled Imageなら塗りの量、それ以外(仮ゲージの無地の四角など)は右端の位置で量を表す。
        private static void SetAmount(Image image, float amount01)
        {
            amount01 = Mathf.Clamp01(amount01);
            if (image.type == Image.Type.Filled && image.sprite != null)
            {
                image.fillAmount = amount01;
                return;
            }

            var rect = image.rectTransform;
            rect.anchorMax = new Vector2(amount01, rect.anchorMax.y);
        }
    }
}
