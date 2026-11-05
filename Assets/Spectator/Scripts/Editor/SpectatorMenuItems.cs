using UnityEditor;
using UnityEngine;

namespace MS2026.Spectator.Editor
{
    /// <summary>
    /// メニュー「Tools/観客の菌/…」。エディタの Play で観客ページを試すためのもの（ビルドでは何もしなくても動く）。
    /// </summary>
    internal static class SpectatorMenu
    {
        private const string Root = "Tools/観客の菌/";
        private const string ToggleItem = Root + "エディタの Play でも観客ページを動かす";

        [MenuItem(ToggleItem, priority = 0)]
        private static void Toggle()
        {
            var on = PlayerPrefs.GetInt(SpectatorConfig.EditorEnabledKey, 0) == 1;
            PlayerPrefs.SetInt(SpectatorConfig.EditorEnabledKey, on ? 0 : 1);
            PlayerPrefs.Save();
            Debug.Log(on
                ? "[Spectator] エディタの Play では観客ページを動かしません。"
                : "[Spectator] 次の Play から観客ページを動かします（Play 中に切り替えたときは、Play し直すと効きます）。");
        }

        [MenuItem(ToggleItem, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(ToggleItem, PlayerPrefs.GetInt(SpectatorConfig.EditorEnabledKey, 0) == 1);
            return true;
        }

        [MenuItem(Root + "観客ページをブラウザで開く", priority = 20)]
        private static void OpenInBrowser()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Spectator] 観客ページは Play 中だけ開けます（上の「エディタの Play でも観客ページを動かす」をオンにしてから Play）。");
            }

            Application.OpenURL($"http://localhost:{SpectatorConfig.DefaultPort}/");
        }

        [MenuItem(Root + "スマホで開く住所を表示", priority = 21)]
        private static void ShowAddress()
        {
            Debug.Log($"[Spectator] 同じ Wi-Fi のスマホで開く住所: {SpectatorAddresses.Urls(SpectatorConfig.DefaultPort)}（Windows のファイアウォールで、このアプリの「プライベート」ネットワークを許可しておく）");
        }
    }
}
