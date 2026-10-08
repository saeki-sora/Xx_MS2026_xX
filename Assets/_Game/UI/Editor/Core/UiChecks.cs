using System.Collections.Generic;
using MS2026.Fortress.Net;
using MS2026.StudioKit;
using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// UIの点検: 置き場所・設定ファイルの抜け、画面の名前の重複、レイヤーの間違い、値の名前の打ち間違い、
    /// 見えないのに押せてしまう画像、役目の無いボタン、仮の接続画面との二重表示など。
    /// </summary>
    public static class UiChecks
    {
        public static IReadOnlyList<StudioIssue> Run(UiStudioContext context)
        {
            var issues = new List<StudioIssue>();
            var root = context.Root;
            if (root == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Error, "シーンにUIの置き場所がありません",
                        "画面はシーンの「[UI]」（UiRoot）が開け閉めします。ボタンで作れます（レイヤーの Canvas・ゲームの値の書き込み役も一緒に作ります）。")
                    .WithFix("UIの置き場所を作る", () => UiStudioSetup.CreateRoot()));
                return issues;
            }

            if (root.layerSettings == null || root.catalog == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Error, "UIの置き場所に設定ファイルが入っていません",
                        "レイヤーの一覧・画面の一覧のどちらかが空です。", root)
                    .WithFix("設定ファイルを入れる", () => AssignSettings(root)));
            }

            if (root.GetComponent<FortressUiValues>() == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Warning, "ゲームの値を画面へ書き込む部品がありません",
                        "熱・コアのHPなどが画面に出ません。", root)
                    .WithFix("部品を付ける", () => Undo.AddComponent<FortressUiValues>(root.gameObject)));
            }

            if (root.GetComponent<FortressLegacyUiSwitch>() == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Info, "仮の熱ゲージとの切り替え部品がありません",
                        "HUD の画面と、前からある仮の熱ゲージが二重に出ることがあります。", root)
                    .WithFix("部品を付ける", () => Undo.AddComponent<FortressLegacyUiSwitch>(root.gameObject)));
            }

            if (root.transform.parent != null && root.keepAcrossScenes)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Warning, "UIの置き場所が何かの子になっています",
                    "「シーンを切り替えてもUIを残す」は一番上に置いたときだけ働きます。[UI] をヒエラルキーの一番上に出してください。", root));
            }

            CheckScreens(context, root, issues);
            CheckElements(context, issues);
            CheckTemporaryConnectUi(context, issues);

            issues.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return issues;
        }

        private static void CheckScreens(UiStudioContext context, UiRoot root, List<StudioIssue> issues)
        {
            var seen = new Dictionary<string, UiScreen>();
            foreach (var screen in context.Screens)
            {
                if (screen == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(screen.screenId))
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"「{screen.name}」に画面の名前がありません", "名前が無いと開けません。", screen));
                }
                else if (seen.TryGetValue(screen.screenId, out var other))
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"画面の名前「{screen.screenId}」が重なっています",
                        $"「{other.name}」と「{screen.name}」が同じ名前です。どちらが開くかわからなくなります。", screen));
                }
                else
                {
                    seen[screen.screenId] = screen;
                }

                if (root.layerSettings != null && root.layerSettings.Find(screen.layer) == null)
                {
                    var target = screen;
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"「{screen.Label}」のレイヤー「{screen.layer}」がありません",
                            "レイヤーの一覧に無い名前です。", screen)
                        .WithFix("メニューの段にする", () => SetLayer(target, "Menu")));
                }

                if (!screen.modal && screen.layer == "Popup")
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Info, $"「{screen.Label}」はポップアップの段ですがモーダルではありません",
                        "後ろの画面も押せてしまいます。わざとなら問題ありません。", screen));
                }
            }

            if (root.catalog != null && root.catalog.screens.Exists(s => s == null))
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Warning, "画面の一覧に空の行があります", "消した画面の跡です。", root.catalog)
                    .WithFix("空の行を消す", () =>
                    {
                        Undo.RecordObject(root.catalog, "空の行を消す");
                        root.catalog.screens.RemoveAll(s => s == null);
                        EditorUtility.SetDirty(root.catalog);
                    }));
            }
        }

        /// <summary>値の名前の打ち間違い・見えないのに押せる画像・役目の無いボタン（一覧の Prefab とシーンの画面）。</summary>
        private static void CheckElements(UiStudioContext context, List<StudioIssue> issues)
        {
            var catalog = UiStudioSetup.ValueCatalog;
            foreach (var screen in context.Screens)
            {
                if (screen == null)
                {
                    continue;
                }

                foreach (var binding in screen.GetComponentsInChildren<UiBinding>(true))
                {
                    if (!string.IsNullOrEmpty(binding.key) && catalog != null && catalog.Find(binding.key) == null)
                    {
                        var key = binding.key;
                        issues.Add(new StudioIssue(StudioIssueSeverity.Warning, $"「{screen.Label}」の {binding.name}: 値「{key}」が一覧にありません",
                                "打ち間違いかもしれません。新しい値ならゲームのプログラムが書き込む必要があります。", binding)
                            .WithFix("値の一覧に登録", () =>
                            {
                                Undo.RecordObject(catalog, "値を登録");
                                catalog.AddIfMissing(key, key, "（UIで使われている値。説明を書いてください）", UiValueKind.Number, "未分類", Vector2.up, 0.5f);
                                EditorUtility.SetDirty(catalog);
                            }));
                    }
                    else if (string.IsNullOrEmpty(binding.key))
                    {
                        issues.Add(new StudioIssue(StudioIssueSeverity.Warning, $"「{screen.Label}」の {binding.name}: 見る値が決まっていません", "値のつなぎ部品の「見る値」が空です。", binding));
                    }
                }

                foreach (var image in screen.GetComponentsInChildren<Image>(true))
                {
                    if (image.raycastTarget && image.color.a <= 0.01f && image.GetComponent<Selectable>() == null && image.name != "[Modal Blocker]")
                    {
                        var target = image;
                        issues.Add(new StudioIssue(StudioIssueSeverity.Info, $"「{screen.Label}」の {image.name}: 見えないのにクリックを受け止めています",
                                "透明な画像の後ろにあるボタンが押せなくなることがあります。", image)
                            .WithFix("クリックを素通りにする", () =>
                            {
                                Undo.RecordObject(target, "クリックを素通りにする");
                                target.raycastTarget = false;
                                EditorUtility.SetDirty(target);
                            }));
                    }
                }

                foreach (var button in screen.GetComponentsInChildren<Button>(true))
                {
                    if (button.onClick.GetPersistentEventCount() == 0 && button.GetComponent<UiButtonAction>() == null && !IsWiredByScript(screen, button))
                    {
                        issues.Add(new StudioIssue(StudioIssueSeverity.Info, $"「{screen.Label}」の {button.name}: 押しても何も起きません",
                            "「ボタンの役目」（UiButtonAction）を付けると、画面を開く・閉じる・戻るなどをプログラムなしで決められます。", button));
                    }
                }
            }
        }

        private static void CheckTemporaryConnectUi(UiStudioContext context, List<StudioIssue> issues)
        {
            var hasLobby = false;
            foreach (var screen in context.Screens)
            {
                if (screen != null && screen.GetComponent<LobbyScreenController>() != null)
                {
                    hasLobby = true;
                }
            }

            var temporary = Object.FindFirstObjectByType<FortressConnectUI>();
            if (hasLobby && temporary != null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Info, "仮の接続画面（右上）も残っています",
                    "ロビー画面が開いている間は自動で隠れます（ロビー画面の動きの「今の仮の接続画面を隠す」）。起動引数での自動接続（テスト用のbat）は今まで通り使えます。", temporary));
            }
        }

        /// <summary>ロビーのボタンのように、プログラムでつながるボタンか。</summary>
        private static bool IsWiredByScript(UiScreen screen, Button button)
        {
            var lobby = screen.GetComponent<LobbyScreenController>();
            if (lobby == null)
            {
                return false;
            }

            if (button == lobby.hostButton || button == lobby.joinButton || button == lobby.leaveButton)
            {
                return true;
            }

            foreach (var player in lobby.playerButtons)
            {
                if (player == button)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssignSettings(UiRoot root)
        {
            Undo.RecordObject(root, "設定ファイルを入れる");
            if (root.layerSettings == null)
            {
                root.layerSettings = UiStudioSetup.LayerSettings;
            }

            if (root.catalog == null)
            {
                root.catalog = UiStudioSetup.Catalog;
            }

            root.EnsureLayers();
            EditorUtility.SetDirty(root);
        }

        private static void SetLayer(UiScreen screen, string layer)
        {
            Undo.RecordObject(screen, "レイヤーを変更");
            screen.layer = layer;
            EditorUtility.SetDirty(screen);
        }
    }
}
