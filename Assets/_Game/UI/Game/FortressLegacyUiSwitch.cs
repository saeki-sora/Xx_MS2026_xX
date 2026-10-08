using System.Collections.Generic;
using MS2026.Fortress.Hud;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// UIスタジオの画面と、前からある仮の表示（[Fortress] LocalTurretHud の熱ゲージ）が二重に出ないようにする。
    /// 「HUD」の段に画面が1つでも開いている間だけ、仮の熱ゲージを隠す。HUD の画面を閉じると元に戻る。
    /// UIの置き場所（[UI]）に1つ付けておく（UIスタジオが自動で付ける）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiRoot))]
    [AddComponentMenu("UI Studio/仮の表示との切り替え (FortressLegacyUiSwitch)")]
    public sealed class FortressLegacyUiSwitch : MonoBehaviour
    {
        [Tooltip("ONなら、HUD の段に画面が出ている間、仮の熱ゲージ（LocalTurretGaugeHud）を隠す。")]
        public bool hideLegacyHeatGauge = true;

        [Tooltip("仮の熱ゲージを隠す基準にする段の名前。")]
        [UiLayerId]
        public string hudLayer = "HUD";

        private readonly List<GameObject> _hidden = new List<GameObject>();
        private UiRoot _root;

        private void OnEnable()
        {
            _root = GetComponent<UiRoot>();
            _root.ScreenOpened += OnScreensChanged;
            _root.ScreenClosed += OnScreensChanged;
        }

        private void OnDisable()
        {
            _root.ScreenOpened -= OnScreensChanged;
            _root.ScreenClosed -= OnScreensChanged;
            Restore();
        }

        private void OnScreensChanged(string _)
        {
            if (!hideLegacyHeatGauge || !AnyHudOpen())
            {
                Restore();
                return;
            }

            foreach (var legacy in FindObjectsByType<LocalTurretGaugeHud>(FindObjectsSortMode.None))
            {
                if (legacy.gameObject.activeSelf)
                {
                    legacy.gameObject.SetActive(false);
                    _hidden.Add(legacy.gameObject);
                }
            }
        }

        private bool AnyHudOpen()
        {
            foreach (var item in _root.OpenScreens)
            {
                if (item.Layer == hudLayer)
                {
                    return true;
                }
            }

            return false;
        }

        private void Restore()
        {
            foreach (var go in _hidden)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }

            _hidden.Clear();
        }
    }
}
