using DDrive.Runtime.Ui;
using MS2026.Fortress;
using MS2026.StudioKit;
using MS2026.UI.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 雛形の画面（空・ロビー・HUD・確認ポップアップ）を作り、Prefab として保存して画面の一覧に入れる。
    /// どれも仮の見た目で、値のつなぎ・動き・ボタンの役目まで入った「そのまま動く」状態で作る。
    /// </summary>
    public static class UiTemplateFactory
    {
        public enum Template
        {
            Empty,
            Lobby,
            Hud,
            Popup
        }

        public static (Template template, string icon, string title, string body)[] All =>
            new[]
            {
                (Template.Empty, "□", "空の画面", "まっさらな画面。タイトル・リザルトなど自由に作る。"),
                (Template.Lobby, "◎", "ロビー（4人の接続待ち）", "P1〜P4 を選んで「ホストになる」「参加する」。つながったら HUD へ切り替える。"),
                (Template.Hud, "▮", "ゲーム中HUD", "自分の熱ゲージ・OVERHEAT・コアのHP・敵の数・4人の熱。"),
                (Template.Popup, "▢", "確認ポップアップ", "「本当に？」の小窓。後ろを暗くして押せなくする（モーダル）。")
            };

        /// <summary>雛形から画面を作って保存する。戻り値は保存した Prefab の画面。</summary>
        public static UiScreen Create(Template template, string screenId)
        {
            // 組み立てた物は使い捨てのシーンへ移して保存し、そのシーンごと閉じる（今開いているシーンには何も残らない）。
            var scratch = EditorSceneManager.NewPreviewScene();
            UiScreen prefab;
            try
            {
                var screen = template switch
                {
                    Template.Lobby => BuildLobby(screenId),
                    Template.Hud => BuildHud(screenId),
                    Template.Popup => BuildPopup(screenId),
                    _ => BuildEmpty(screenId)
                };

                SceneManager.MoveGameObjectToScene(screen.gameObject, scratch);
                var path = StudioAssets.UniquePath(UiStudioSetup.ScreensFolder, $"{screenId}.prefab");
                prefab = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, path).GetComponent<UiScreen>();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scratch);
            }

            var catalog = UiStudioSetup.Catalog;
            Undo.RecordObject(catalog, "画面を一覧に追加");
            catalog.screens.Add(prefab);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>一覧の中で使われていない画面の名前（Lobby, Lobby2…）。</summary>
        public static string UniqueId(string baseId)
        {
            var catalog = UiStudioSetup.Catalog;
            var id = baseId;
            for (var n = 2; catalog.Find(id) != null; n++)
            {
                id = baseId + n;
            }

            return id;
        }

        private static UiScreen BuildEmpty(string id)
        {
            var screen = B.Screen(id, "新しい画面", "Menu");
            var title = B.Text(screen.transform, "Title", "タイトル", 64, B.Ink, TextAnchor.MiddleCenter, true);
            B.Place(title.rectTransform, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(1200f, 120f));
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.4f);
            return screen;
        }

        private static UiScreen BuildLobby(string id)
        {
            var screen = B.Screen(id, "ロビー", "Menu");
            screen.openOnStart = true;
            screen.closeOnBack = false;

            var back = B.Panel(screen.transform, "Background", new Color(0.05f, 0.05f, 0.07f, 0.92f), false);
            B.Stretch(back.rectTransform);

            var title = B.Text(screen.transform, "Title", "ロビー", 72, B.Ink, TextAnchor.MiddleCenter, true);
            B.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 110f));
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.45f);

            var lead = B.Text(screen.transform, "Lead", "自分のプレイヤーを選んで、ホストになるか参加してください", 28, B.SubInk);
            B.Place(lead.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1400f, 50f));

            var controller = screen.gameObject.AddComponent<LobbyScreenController>();
            var slots = B.Rect(screen.transform, "PlayerSlots");
            B.Place(slots, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1520f, 330f));
            for (var p = 0; p < 4; p++)
            {
                controller.playerButtons[p] = BuildPlayerSlot(slots, p);
            }

            var address = BuildAddressField(screen.transform);
            controller.addressField = address;

            var buttons = B.Rect(screen.transform, "Buttons");
            B.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1200f, 90f));
            controller.hostButton = B.Button(buttons, "HostButton", "ホストになる", B.Accent, new Vector2(340f, 84f));
            B.Place((RectTransform)controller.hostButton.transform, new Vector2(0.5f, 0.5f), new Vector2(-380f, 0f), new Vector2(340f, 84f));
            controller.joinButton = B.Button(buttons, "JoinButton", "参加する", new Color(0.31f, 0.76f, 0.97f), new Vector2(340f, 84f));
            B.Place((RectTransform)controller.joinButton.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 84f));
            controller.leaveButton = B.Button(buttons, "LeaveButton", "やめる", new Color(0.62f, 0.64f, 0.68f), new Vector2(260f, 84f));
            B.Place((RectTransform)controller.leaveButton.transform, new Vector2(0.5f, 0.5f), new Vector2(340f, 0f), new Vector2(260f, 84f));

            var status = B.Text(screen.transform, "Status", "", 28, B.Ink);
            B.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1400f, 50f));
            status.gameObject.AddComponent<UiBindText>().key = FortressUiKeys.LobbyStatus;

            screen.firstSelected = controller.hostButton;
            return screen;
        }

        private static Button BuildPlayerSlot(Transform parent, int player)
        {
            var color = FortressColors.PlayerColor(player);
            var card = B.Panel(parent, $"Slot_P{player + 1}", B.CardColor);
            B.Place(card.rectTransform, new Vector2(0f, 0.5f), new Vector2(player * 385f, 0f), new Vector2(360f, 320f));
            card.rectTransform.pivot = new Vector2(0f, 0.5f);
            var button = card.gameObject.AddComponent<Button>();

            var stripe = B.Panel(card.transform, "Color", color);
            B.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(320f, 14f));
            var name = B.Text(card.transform, "Name", $"P{player + 1}", 110, color, TextAnchor.MiddleCenter, true);
            B.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(320f, 150f));

            var state = B.Text(card.transform, "State", "空き", 30, B.SubInk);
            B.Place(state.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 50f));
            var empty = state.gameObject.AddComponent<UiBindVisible>();
            empty.key = FortressUiKeys.PlayerConnected(player);
            empty.condition = UiVisibleCondition.WhenOff;
            var connected = B.Text(card.transform, "Connected", "参加中", 30, color, TextAnchor.MiddleCenter, true);
            B.Place(connected.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 50f));
            var visible = connected.gameObject.AddComponent<UiBindVisible>();
            visible.key = FortressUiKeys.PlayerConnected(player);
            B.AddMotion(connected.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);

            var selected = B.Panel(card.transform, "Selected", new Color(color.r, color.g, color.b, 0.22f));
            B.Stretch(selected.rectTransform);
            selected.raycastTarget = false;
            selected.gameObject.AddComponent<CanvasGroup>();
            var mark = selected.gameObject.AddComponent<UiBindVisible>();
            mark.key = FortressUiKeys.LobbySelectedPlayer;
            mark.condition = UiVisibleCondition.WhenEqual;
            mark.threshold = player;
            B.AddMotion(selected.gameObject, UiMotionTrigger.OnShow, UiPreset.FadeIn, 0.15f);

            B.AddMotion(card.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f, 0.08f * player);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnHover, UiPreset.PunchScale, 0.2f);
            return button;
        }

        private static Selectable BuildAddressField(Transform parent)
        {
            var field = B.Panel(parent, "AddressField", B.CardColor);
            B.Place(field.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 310f), new Vector2(560f, 70f));
            var caption = B.Text(field.transform, "Caption", "ホストのIPアドレス", 22, B.SubInk, TextAnchor.MiddleLeft);
            B.Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(-250f, 0f), new Vector2(240f, 60f));
            caption.rectTransform.pivot = new Vector2(0f, 0.5f);
            var text = B.Text(field.transform, "Text", "", 30, B.Ink, TextAnchor.MiddleLeft);
            B.Stretch(text.rectTransform, 14f);
            text.supportRichText = false;
            var placeholder = B.Text(field.transform, "Placeholder", "127.0.0.1", 30, B.SubInk, TextAnchor.MiddleLeft);
            B.Stretch(placeholder.rectTransform, 14f);
            var input = field.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static UiScreen BuildHud(string id)
        {
            var screen = B.Screen(id, "ゲーム中HUD", "HUD");
            screen.closeOnBack = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.3f);

            // 自分の熱ゲージ（画面下の中央）
            var heat = B.Rect(screen.transform, "LocalHeat");
            B.Place(heat, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(760f, 110f));
            var label = B.Text(heat, "Player", "P1", 44, B.Ink, TextAnchor.MiddleLeft, true);
            B.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(110f, 80f));
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.gameObject.AddComponent<UiBindText>().key = FortressUiKeys.LocalPlayerLabel;
            var labelColor = label.gameObject.AddComponent<UiBindColor>();
            labelColor.key = FortressUiKeys.LocalColor;
            var gauge = B.Gauge(heat, "HeatGauge", FortressUiKeys.LocalHeat, new Vector2(620f, 46f), new Color(1f, 0.6f, 0.2f), true);
            B.Place((RectTransform)gauge.transform.parent, new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(620f, 46f));
            ((RectTransform)gauge.transform.parent).pivot = new Vector2(1f, 0.5f);
            var caption = B.Text(gauge.transform.parent, "Caption", "HEAT", 22, B.Ink, TextAnchor.MiddleLeft, true);
            B.Stretch(caption.rectTransform, 12f);

            // 表示の切り替え（透明度）と点滅（これも透明度）がぶつからないよう、外枠で出し入れし、中の文字を点滅させる。
            var overheat = B.Rect(screen.transform, "Overheat");
            B.Place(overheat, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(800f, 100f));
            overheat.gameObject.AddComponent<CanvasGroup>();
            overheat.gameObject.AddComponent<UiBindVisible>().key = FortressUiKeys.LocalOverheated;
            B.AddMotion(overheat.gameObject, UiMotionTrigger.OnShow, UiPreset.ShakeHard, 0.4f);
            var overheatLabel = B.Text(overheat, "Label", "OVERHEAT", 72, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleCenter, true);
            B.Stretch(overheatLabel.rectTransform);
            B.AddMotion(overheatLabel.gameObject, UiMotionTrigger.Loop, UiPreset.Blink, 0.5f);

            // コアのHP（上の中央）
            var core = B.Rect(screen.transform, "CoreHp");
            B.Place(core, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 90f));
            var coreTitle = B.Text(core, "Caption", "CORE", 26, B.SubInk, TextAnchor.UpperCenter, true);
            B.Place(coreTitle.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 34f));
            var coreGauge = B.Gauge(core, "CoreGauge", FortressUiKeys.CoreHp01, new Vector2(860f, 34f), new Color(0.37f, 0.83f, 0.61f), false);
            B.Place((RectTransform)coreGauge.transform.parent, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(860f, 34f));
            B.AddMotion(coreGauge.transform.parent.gameObject, UiMotionTrigger.OnValueDown, UiPreset.Shake, 0.3f, 0f, FortressUiKeys.CoreHp01);

            // 敵の数（右上）
            var enemies = B.Text(screen.transform, "Enemies", "敵 0", 34, B.Ink, TextAnchor.MiddleRight, true);
            B.Place(enemies.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(360f, 60f));
            var enemiesText = enemies.gameObject.AddComponent<UiBindText>();
            enemiesText.key = FortressUiKeys.SwarmAlive;
            enemiesText.format = "敵 {0:N0}";

            // 4人の熱（左上）
            var team = B.Rect(screen.transform, "Team");
            B.Place(team, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(360f, 200f));
            for (var p = 0; p < 4; p++)
            {
                var row = B.Rect(team, $"P{p + 1}");
                B.Place(row, new Vector2(0f, 1f), new Vector2(0f, -p * 46f), new Vector2(360f, 40f));
                var name = B.Text(row, "Name", $"P{p + 1}", 22, FortressColors.PlayerColor(p), TextAnchor.MiddleLeft, true);
                B.Place(name.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80f, 40f));
                name.rectTransform.pivot = new Vector2(0f, 0.5f);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                // ロビーで付けた名前（無ければ P1 など）。
                var nameText = name.gameObject.AddComponent<UiBindText>();
                nameText.key = FortressUiKeys.PlayerName(p);
                nameText.whenEmpty = $"P{p + 1}";
                var bar = B.Gauge(row, "Heat", FortressUiKeys.PlayerHeat(p), new Vector2(280f, 20f), FortressColors.PlayerColor(p), false);
                B.Place((RectTransform)bar.transform.parent, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(280f, 20f));
                ((RectTransform)bar.transform.parent).pivot = new Vector2(1f, 0.5f);
            }

            return screen;
        }

        private static UiScreen BuildPopup(string id)
        {
            var screen = B.Screen(id, "確認", "Popup");
            screen.modal = true;
            screen.showMotion = UiMotion.FromPreset(UiPreset.ZoomInFade, 0.25f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.ZoomOutFade, 0.2f);

            var window = B.Panel(screen.transform, "Window", B.PanelColor);
            B.Place(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));
            var title = B.Text(window.transform, "Title", "本当にやめますか？", 44, B.Ink, TextAnchor.MiddleCenter, true);
            B.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 80f));
            var body = B.Text(window.transform, "Body", "ここに説明を書きます。", 28, B.SubInk);
            B.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(720f, 120f));

            var ok = B.Button(window.transform, "OkButton", "はい", B.Accent, new Vector2(280f, 76f));
            B.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(-160f, 40f), new Vector2(280f, 76f));
            var okAction = ok.gameObject.AddComponent<UiButtonAction>();
            okAction.action = UiButtonActionKind.CloseThisScreen;

            var cancel = B.Button(window.transform, "CancelButton", "いいえ", new Color(0.62f, 0.64f, 0.68f), new Vector2(280f, 76f));
            B.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(160f, 40f), new Vector2(280f, 76f));
            cancel.gameObject.AddComponent<UiButtonAction>().action = UiButtonActionKind.CloseThisScreen;

            screen.firstSelected = cancel;
            return screen;
        }
    }
}
