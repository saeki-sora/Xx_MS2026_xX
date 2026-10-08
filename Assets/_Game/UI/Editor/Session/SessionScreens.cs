using System.Collections.Generic;
using DDrive.Runtime.Ui;
using MS2026.Fortress;
using MS2026.StudioKit;
using MS2026.UI.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using A = MS2026.UI.EditorTools.SessionArt;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;
using K = MS2026.UI.Game.FortressUiKeys;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ロビーまわりの画面（Prefab）を作る: 背景・最初の画面・部屋を選ぶ・部屋・3・2・1・一時メニュー・お知らせ・通知。
    /// どれも UIスタジオの部品（画面・値の部品・動き・ボタンの役目）だけでできているので、作った後は UIスタジオで自由に直せる。
    /// 絵は <see cref="SessionArt"/> の PNG（差し替え可）、文字は日本語が出る標準のフォント（TextMeshPro にもできる）。
    /// </summary>
    public static class SessionScreens
    {
        public const string Folder = UiStudioSetup.ScreensFolder + "/Session";

        public const string BackgroundId = "LobbyBackground";
        public const string TopId = "LobbyTop";
        public const string JoinId = "LobbyJoin";
        public const string RoomId = "LobbyRoom";
        public const string CountdownId = "Countdown";
        public const string PauseId = "Pause";
        public const string MessageId = "Message";
        public const string ToastId = "Toast";
        public const string HudId = "Hud";

        /// <summary>画面を作って一覧に入れる。overwrite=false なら、もう一覧にある画面はそのまま残す。戻り値は作った画面の名前。</summary>
        public static List<string> BuildAll(bool overwrite)
        {
            A.EnsureAll();
            var built = new List<string>();
            Save(BackgroundId, BuildBackground, overwrite, built);
            Save(TopId, BuildTop, overwrite, built);
            Save(JoinId, BuildJoin, overwrite, built);
            Save(RoomId, BuildRoom, overwrite, built);
            Save(CountdownId, BuildCountdown, overwrite, built);
            Save(PauseId, BuildPause, overwrite, built);
            Save(MessageId, BuildMessage, overwrite, built);
            Save(ToastId, BuildToast, overwrite, built);
            if (UiStudioSetup.Catalog.Find(HudId) == null)
            {
                UiTemplateFactory.Create(UiTemplateFactory.Template.Hud, HudId);
                built.Add(HudId);
            }

            AssetDatabase.SaveAssets();
            return built;
        }

        private static void Save(string id, System.Func<UiScreen> build, bool overwrite, List<string> built)
        {
            var catalog = UiStudioSetup.Catalog;
            var existing = catalog.Find(id);
            if (existing != null && !overwrite)
            {
                return;
            }

            StudioAssets.EnsureFolder(Folder);
            var path = existing != null ? AssetDatabase.GetAssetPath(existing) : $"{Folder}/{id}.prefab";
            var scratch = EditorSceneManager.NewPreviewScene();
            UiScreen prefab;
            try
            {
                var screen = build();
                SceneManager.MoveGameObjectToScene(screen.gameObject, scratch);
                prefab = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, path).GetComponent<UiScreen>();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scratch);
            }

            if (existing == null)
            {
                Undo.RecordObject(catalog, "画面を一覧に追加");
                catalog.screens.Add(prefab);
                EditorUtility.SetDirty(catalog);
            }

            built.Add(id);
        }

        // ───────── 背景 ─────────

        private static UiScreen BuildBackground()
        {
            var screen = B.Screen(BackgroundId, "ロビーの背景", "Background");
            screen.closeOnBack = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.4f);

            var image = Img(screen.transform, "Image", A.Background, Color.white, false);
            B.Stretch(image.rectTransform);

            // ふわふわ浮かぶ泡（ただの飾り。消しても動く）。
            var random = new System.Random(7);
            for (var i = 0; i < 14; i++)
            {
                var size = 30f + (float)random.NextDouble() * 110f;
                var bubble = Img(screen.transform, $"Bubble {i + 1}", A.Dot, new Color(1f, 1f, 1f, 0.35f + (float)random.NextDouble() * 0.3f), false);
                B.Place(bubble.rectTransform, new Vector2((float)random.NextDouble(), (float)random.NextDouble()), Vector2.zero, new Vector2(size, size));
                B.AddMotion(bubble.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 2f + (float)random.NextDouble() * 2.5f, (float)random.NextDouble());
            }

            return screen;
        }

        // ───────── 最初の画面 ─────────

        private static UiScreen BuildTop()
        {
            var screen = MenuScreen(TopId, "ロビー：最初の画面", "LOBBY", "名前とプレイヤー番号を決めて、部屋を作るか参加します");
            var controller = screen.gameObject.AddComponent<LobbyTopScreen>();

            // 左: あなた（名前・番号・センサーの確認）
            var you = Card(screen.transform, "You", A.Card);
            B.Place(you.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, -30f), new Vector2(980f, 620f));
            Caption(you.transform, "Caption", "あなた", new Vector2(40f, -30f));

            Small(you.transform, "NameCaption", "名前（なくてもOK・12文字まで）", new Vector2(40f, -100f));
            controller.nameField = Input(you.transform, "NameField", "例: たろう", new Vector2(40f, -140f), new Vector2(900f, 80f));

            Small(you.transform, "SeatCaption", "プレイヤー番号（センサーの番号とは関係なく、このPCは選んだ番号で参加します）", new Vector2(40f, -250f));
            for (var p = 0; p < 4; p++)
            {
                controller.seatButtons[p] = SeatButton(you.transform, p);
            }

            Small(you.transform, "GripCaption", "センサーの確認（握るとゲージが伸びます。キーボードなら Q）", new Vector2(40f, -480f));
            var grip = B.Gauge(you.transform, "GripGauge", K.LocalGrip, new Vector2(900f, 40f), A.Mint, false);
            TopLeft((RectTransform)grip.transform.parent, new Vector2(40f, -520f), new Vector2(900f, 40f));

            // 右: 部屋を作る・参加する
            controller.hostButton = BigButton(screen.transform, "HostButton", "部屋を作る", "ホストになって、みんなを待ちます", A.Pink, new Vector2(-120f, 150f));
            controller.joinButton = BigButton(screen.transform, "JoinButton", "部屋に参加する", "同じLANの部屋をさがします", A.Cyan, new Vector2(-120f, -60f));
            controller.titleButton = Btn(screen.transform, "TitleButton", "タイトルへ", A.Gray, new Vector2(300f, 76f));
            B.Place((RectTransform)controller.titleButton.transform, new Vector2(1f, 0.5f), new Vector2(-120f, -260f), new Vector2(300f, 76f));

            Footer(screen.transform);
            screen.firstSelected = controller.hostButton;
            return screen;
        }

        // ───────── 部屋を選ぶ ─────────

        private static UiScreen BuildJoin()
        {
            var screen = MenuScreen(JoinId, "ロビー：部屋を選ぶ", "部屋をさがす", "同じLANの部屋を自動でさがしています。押すと参加します");
            var controller = screen.gameObject.AddComponent<LobbyJoinScreen>();

            var seat = B.Text(screen.transform, "Seat", "P1", 30, A.Ink, TextAnchor.MiddleRight, true);
            B.Place(seat.rectTransform, new Vector2(1f, 1f), new Vector2(-120f, -70f), new Vector2(700f, 50f));
            var seatText = seat.gameObject.AddComponent<UiBindText>();
            seatText.key = K.LobbySelectedLabel;
            seatText.format = "{0} で参加します";

            // 見つかった部屋の一覧
            var list = Card(screen.transform, "List", A.Card);
            B.Place(list.rectTransform, new Vector2(0.5f, 1f), new Vector2(-260f, -200f), new Vector2(1160f, 600f));
            var rows = B.Rect(list.transform, "Rows");
            B.Stretch(rows, 24f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            controller.listRoot = rows;
            controller.rowTemplate = HostRow(rows);

            var empty = B.Text(list.transform, "Empty", "まだ見つかりません。\nホストが「部屋を作る」を押しているか、同じLANにいるか確かめてください。\n見つからなくても、右の欄にアドレスを入れれば参加できます。", 28, A.SubInk);
            B.Stretch(empty.rectTransform, 60f);
            var emptyVisible = empty.gameObject.AddComponent<UiBindVisible>();
            emptyVisible.key = K.LobbyFoundHosts;
            emptyVisible.condition = UiVisibleCondition.WhenLess;
            emptyVisible.threshold = 0.5f;
            B.AddMotion(empty.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 2.4f);

            // アドレスで参加
            var manual = Card(screen.transform, "Manual", A.CardShade);
            B.Place(manual.rectTransform, new Vector2(0.5f, 1f), new Vector2(610f, -200f), new Vector2(560f, 360f));
            Caption(manual.transform, "Caption", "アドレスで参加", new Vector2(36f, -28f));
            Small(manual.transform, "Hint", "ホストのPCの画面の下に出ている\n「このPCのアドレス」を入れます", new Vector2(36f, -90f));
            controller.addressField = Input(manual.transform, "AddressField", "192.168.1.12", new Vector2(36f, -170f), new Vector2(488f, 76f));
            controller.connectButton = Btn(manual.transform, "ConnectButton", "このアドレスに参加", A.Cyan, new Vector2(488f, 76f));
            TopLeft((RectTransform)controller.connectButton.transform, new Vector2(36f, -264f), new Vector2(488f, 76f));

            controller.backButton = Btn(screen.transform, "BackButton", "もどる", A.Gray, new Vector2(260f, 76f));
            B.Place((RectTransform)controller.backButton.transform, new Vector2(1f, 0f), new Vector2(-120f, 120f), new Vector2(260f, 76f));

            // 接続中の幕（つながるまで、ほかを押せなくする）
            var connecting = Img(screen.transform, "Connecting", A.Panel, new Color(0.12f, 0.12f, 0.25f, 0.82f), false);
            B.Stretch(connecting.rectTransform);
            connecting.raycastTarget = true;
            var connectingVisible = connecting.gameObject.AddComponent<UiBindVisible>();
            connectingVisible.key = K.LobbyConnecting;
            B.AddMotion(connecting.gameObject, UiMotionTrigger.OnShow, UiPreset.FadeIn, 0.2f);
            var spinner = Img(connecting.transform, "Spinner", A.Ready, A.Cyan, false);
            B.Place(spinner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(110f, 110f));
            B.AddMotion(spinner.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 1.2f);
            var connectingText = B.Text(connecting.transform, "Status", "接続しています…", 34, Color.white, TextAnchor.MiddleCenter, true);
            B.Place(connectingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1400f, 60f));
            connectingText.gameObject.AddComponent<UiBindText>().key = K.LobbyStatus;
            controller.cancelButton = Btn(connecting.transform, "CancelButton", "やめる", A.Gray, new Vector2(260f, 76f));
            B.Place((RectTransform)controller.cancelButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(260f, 76f));

            Footer(screen.transform);
            screen.firstSelected = controller.connectButton;
            return screen;
        }

        private static LobbyHostRow HostRow(Transform parent)
        {
            var card = Card(parent, "Row Template", Color.white);
            card.raycastTarget = true;
            card.rectTransform.sizeDelta = new Vector2(0f, 120f);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            var row = card.gameObject.AddComponent<LobbyHostRow>();
            row.button = card.gameObject.AddComponent<Button>();
            ButtonColors(row.button);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnHover, UiPreset.PunchScale, 0.2f);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnClick, UiPreset.Jelly, 0.35f);

            row.title = B.Text(card.transform, "Title", "たろう の部屋", 34, A.Ink, TextAnchor.MiddleLeft, true);
            TopLeft(row.title.rectTransform, new Vector2(30f, -14f), new Vector2(620f, 50f));
            row.info = B.Text(card.transform, "Info", "2/4人・参加できます", 24, A.SubInk, TextAnchor.MiddleLeft);
            TopLeft(row.info.rectTransform, new Vector2(30f, -66f), new Vector2(420f, 40f));
            row.address = B.Text(card.transform, "Address", "192.168.1.12", 22, A.SubInk, TextAnchor.MiddleRight);
            B.Place(row.address.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -20f), new Vector2(300f, 36f));
            row.warning = B.Text(card.transform, "Warning", "", 20, A.Pink, TextAnchor.MiddleLeft, true);
            TopLeft(row.warning.rectTransform, new Vector2(460f, -66f), new Vector2(460f, 40f));
            for (var p = 0; p < 4; p++)
            {
                var mark = Img(card.transform, $"Seat P{p + 1}", A.Dot, FortressColors.PlayerColor(p), false);
                B.Place(mark.rectTransform, new Vector2(1f, 0f), new Vector2(-30f - (3 - p) * 48f, 22f), new Vector2(38f, 38f));
                row.seatMarks[p] = mark;
            }

            return row;
        }

        // ───────── 部屋 ─────────

        private static UiScreen BuildRoom()
        {
            var screen = MenuScreen(RoomId, "ロビー：部屋", "ROOM", "");
            var controller = screen.gameObject.AddComponent<LobbyRoomScreen>();

            var lead = screen.transform.Find("Lead").GetComponent<Text>();
            lead.gameObject.AddComponent<UiBindText>().key = K.LobbyPhase;

            var address = B.Text(screen.transform, "RoomAddress", "部屋のアドレス 192.168.1.12", 28, A.Ink, TextAnchor.MiddleRight, true);
            B.Place(address.rectTransform, new Vector2(1f, 1f), new Vector2(-120f, -70f), new Vector2(900f, 50f));
            var addressText = address.gameObject.AddComponent<UiBindText>();
            addressText.key = K.LobbyHostAddress;
            addressText.format = "部屋のアドレス {0}";
            var addressHint = B.Text(screen.transform, "AddressHint", "ほかの人は「部屋に参加する」で一覧から選ぶか、このアドレスを入れて参加できます", 22, A.SubInk, TextAnchor.MiddleRight);
            B.Place(addressHint.rectTransform, new Vector2(1f, 1f), new Vector2(-120f, -118f), new Vector2(1100f, 40f));

            // 4人の席
            var seats = B.Rect(screen.transform, "Seats");
            B.Place(seats, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1680f, 470f));
            for (var p = 0; p < 4; p++)
            {
                controller.kickButtons[p] = SeatCard(seats, p);
            }

            // 準備OK（握り続けても切り替わる）
            controller.readyButton = Btn(screen.transform, "ReadyButton", "", A.Mint, new Vector2(520f, 110f));
            B.Place((RectTransform)controller.readyButton.transform, new Vector2(0f, 0f), new Vector2(120f, 150f), new Vector2(520f, 110f));
            controller.readyButton.transform.Find("Label").gameObject.SetActive(false);
            var notReady = SwapLabel(controller.readyButton.transform, "NotReady", "準備OKにする", K.LobbyLocalReady, UiVisibleCondition.WhenOff);
            notReady.color = Color.white;
            var ready = SwapLabel(controller.readyButton.transform, "IsReady", "準備OK！（もう一度で取り消し）", K.LobbyLocalReady, UiVisibleCondition.WhenOn);
            ready.color = Color.white;
            ready.fontSize = 32;
            B.AddMotion(ready.gameObject, UiMotionTrigger.OnShow, UiPreset.Tada, 0.5f);

            var gripHint = B.Text(screen.transform, "GripHint", "センサーを握り続けても切り替わります", 22, A.SubInk, TextAnchor.MiddleLeft);
            TopLeftFromBottom(gripHint.rectTransform, new Vector2(120f, 110f), new Vector2(620f, 34f));
            var hold = B.Gauge(screen.transform, "GripHold", K.LobbyGripHold, new Vector2(520f, 18f), A.Mint, false);
            hold.smoothSeconds = 0f;
            TopLeftFromBottom((RectTransform)hold.transform.parent, new Vector2(120f, 80f), new Vector2(520f, 18f));

            // 準備OKの人数（「準備OK 2」と「/ 3人」をまん中で左右に分けて並べる）
            var count = B.Text(screen.transform, "ReadyCount", "準備OK 2", 34, A.Ink, TextAnchor.MiddleRight, true);
            B.Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(-150f, 205f), new Vector2(300f, 50f));
            var countText = count.gameObject.AddComponent<UiBindText>();
            countText.key = K.LobbyReadyCount;
            countText.format = "準備OK {0}";
            var total = B.Text(screen.transform, "PlayerCount", "/ 3人", 34, A.Ink, TextAnchor.MiddleLeft, true);
            B.Place(total.rectTransform, new Vector2(0.5f, 0f), new Vector2(110f, 205f), new Vector2(200f, 50f));
            var totalText = total.gameObject.AddComponent<UiBindText>();
            totalText.key = K.LobbyPlayers;
            totalText.format = "/ {0}人";
            B.AddMotion(count.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.3f, 0f, K.LobbyReadyCount);

            // ホスト: ゲーム開始・待たずに開始・開始をやめる／参加側: 待っています
            var hostArea = B.Rect(screen.transform, "HostOnly");
            B.Place(hostArea, new Vector2(1f, 0f), new Vector2(-120f, 120f), new Vector2(560f, 250f));
            hostArea.gameObject.AddComponent<UiBindVisible>().key = K.LobbyIsHost;
            controller.startButton = Btn(hostArea, "StartButton", "ゲーム開始！", A.Pink, new Vector2(560f, 120f));
            B.Place((RectTransform)controller.startButton.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(560f, 120f));
            controller.startButton.gameObject.AddComponent<UiBindInteractable>().key = K.LobbyCanStart;
            B.AddMotion(controller.startButton.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Tada, 0.6f, 0f, K.LobbyCanStart);
            controller.forceStartButton = Btn(hostArea, "ForceStartButton", "待たずに開始", A.Lavender, new Vector2(270f, 70f));
            B.Place((RectTransform)controller.forceStartButton.transform, new Vector2(0f, 0f), Vector2.zero, new Vector2(270f, 70f));
            controller.cancelButton = Btn(hostArea, "CancelButton", "開始をやめる", A.Gray, new Vector2(270f, 70f));
            B.Place((RectTransform)controller.cancelButton.transform, new Vector2(1f, 0f), Vector2.zero, new Vector2(270f, 70f));
            controller.cancelButton.gameObject.AddComponent<UiBindVisible>().key = K.LobbyCountingDown;

            var waiting = B.Text(screen.transform, "Waiting", "ホストが「ゲーム開始」を押すのを待っています", 28, A.SubInk, TextAnchor.MiddleRight, true);
            B.Place(waiting.rectTransform, new Vector2(1f, 0f), new Vector2(-120f, 200f), new Vector2(760f, 50f));
            var waitingVisible = waiting.gameObject.AddComponent<UiBindVisible>();
            waitingVisible.key = K.LobbyIsHost;
            waitingVisible.condition = UiVisibleCondition.WhenOff;
            B.AddMotion(waiting.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 2f);

            controller.leaveButton = Btn(screen.transform, "LeaveButton", "部屋を出る", A.Gray, new Vector2(260f, 70f));
            B.Place((RectTransform)controller.leaveButton.transform, new Vector2(1f, 0f), new Vector2(-120f, 40f), new Vector2(260f, 70f));

            var status = B.Text(screen.transform, "Status", "", 24, A.SubInk, TextAnchor.MiddleCenter);
            B.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 40f));
            status.gameObject.AddComponent<UiBindText>().key = K.LobbyStatus;

            screen.firstSelected = controller.readyButton;
            return screen;
        }

        /// <summary>部屋の席1つ（色の帯・番号・名前・ホストの印・あなた・準備OK・通信の遅れ・外す）。戻り値は「外す」ボタン。</summary>
        private static Button SeatCard(Transform parent, int p)
        {
            var color = FortressColors.PlayerColor(p);
            var card = Card(parent, $"Seat P{p + 1}", A.Card);
            B.Place(card.rectTransform, new Vector2(0f, 0.5f), new Vector2(p * 425f, 0f), new Vector2(400f, 460f));
            card.rectTransform.pivot = new Vector2(0f, 0.5f);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f, 0.07f * p);

            var stripe = Img(card.transform, "Color", A.Panel, color, true);
            B.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(360f, 18f));
            var number = B.Text(card.transform, "Number", $"P{p + 1}", 64, color, TextAnchor.MiddleLeft, true);
            TopLeft(number.rectTransform, new Vector2(28f, -44f), new Vector2(200f, 80f));

            var crown = Img(card.transform, "Host", A.Host, new Color(1f, 0.78f, 0.25f), false);
            B.Place(crown.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -50f), new Vector2(70f, 70f));
            crown.gameObject.AddComponent<UiBindVisible>().key = K.PlayerIsHost(p);
            B.AddMotion(crown.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f);

            var you = Card(card.transform, "You", color);
            B.Place(you.rectTransform, new Vector2(1f, 1f), new Vector2(-110f, -60f), new Vector2(110f, 44f));
            var youLabel = B.Text(you.transform, "Label", "あなた", 24, Color.white, TextAnchor.MiddleCenter, true);
            B.Stretch(youLabel.rectTransform);
            you.gameObject.AddComponent<UiBindVisible>().key = K.PlayerIsLocal(p);

            // 誰かが座っているとき
            var present = B.Rect(card.transform, "Present");
            B.Stretch(present);
            present.gameObject.AddComponent<UiBindVisible>().key = K.PlayerConnected(p);
            var name = B.Text(present, "Name", "たろう", 46, A.Ink, TextAnchor.MiddleCenter, true);
            B.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(360f, 80f));
            var nameText = name.gameObject.AddComponent<UiBindText>();
            nameText.key = K.PlayerName(p);
            nameText.whenEmpty = $"P{p + 1}";

            var ready = Card(present, "Ready", A.Mint);
            B.Place(ready.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(300f, 76f));
            var readyIcon = Img(ready.transform, "Icon", A.Ready, Color.white, false);
            B.Place(readyIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(54f, 54f));
            var readyLabel = B.Text(ready.transform, "Label", "準備OK", 34, Color.white, TextAnchor.MiddleCenter, true);
            B.Place(readyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(200f, 60f));
            ready.gameObject.AddComponent<UiBindVisible>().key = K.PlayerReady(p);
            B.AddMotion(ready.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            B.AddMotion(ready.gameObject, UiMotionTrigger.OnHide, UiPreset.PopOut, 0.2f);

            var notReady = B.Text(present, "NotReady", "準備中…", 30, A.SubInk, TextAnchor.MiddleCenter, true);
            B.Place(notReady.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(300f, 76f));
            var notReadyVisible = notReady.gameObject.AddComponent<UiBindVisible>();
            notReadyVisible.key = K.PlayerReady(p);
            notReadyVisible.condition = UiVisibleCondition.WhenOff;
            B.AddMotion(notReady.gameObject, UiMotionTrigger.Loop, UiPreset.Blink, 1.6f);

            var ping = B.Text(present, "Ping", "通信 12ms", 20, A.SubInk, TextAnchor.MiddleLeft);
            TopLeftFromBottom(ping.rectTransform, new Vector2(28f, 26f), new Vector2(200f, 34f));
            var pingText = ping.gameObject.AddComponent<UiBindText>();
            pingText.key = K.PlayerPing(p);
            pingText.format = "通信 {0:0}ms";
            var pingVisible = ping.gameObject.AddComponent<UiBindVisible>();
            pingVisible.key = K.PlayerPing(p);
            pingVisible.condition = UiVisibleCondition.WhenGreater;
            pingVisible.threshold = 0f;

            var kick = Btn(present, "KickButton", "外す", A.Gray, new Vector2(110f, 50f));
            B.Place((RectTransform)kick.transform, new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(110f, 50f));

            // 空いているとき
            var empty = B.Text(card.transform, "Empty", "あき", 40, new Color(A.SubInk.r, A.SubInk.g, A.SubInk.b, 0.6f), TextAnchor.MiddleCenter, true);
            B.Place(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(360f, 80f));
            var emptyVisible = empty.gameObject.AddComponent<UiBindVisible>();
            emptyVisible.key = K.PlayerConnected(p);
            emptyVisible.condition = UiVisibleCondition.WhenOff;
            return kick;
        }

        // ───────── 3・2・1 ─────────

        private static UiScreen BuildCountdown()
        {
            var screen = B.Screen(CountdownId, "開始のカウントダウン", "Popup");
            screen.closeOnBack = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.15f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.FadeOut, 0.15f);

            var dim = Img(screen.transform, "Dim", A.Panel, new Color(0.15f, 0.12f, 0.3f, 0.45f), false);
            B.Stretch(dim.rectTransform);
            var number = B.Text(screen.transform, "Number", "3", 360, Color.white, TextAnchor.MiddleCenter, true);
            B.Place(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 460f));
            number.gameObject.AddComponent<Outline>().effectColor = new Color(A.Pink.r, A.Pink.g, A.Pink.b, 0.9f);
            number.gameObject.AddComponent<UiBindText>().key = K.LobbyCountdown;
            B.AddMotion(number.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            B.AddMotion(number.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PopIn, 0.3f, 0f, K.LobbyCountdown);
            var caption = B.Text(screen.transform, "Caption", "まもなく開始！", 54, Color.white, TextAnchor.MiddleCenter, true);
            B.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(1000f, 90f));
            B.AddMotion(caption.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 0.8f);
            return screen;
        }

        // ───────── 一時メニュー・お知らせ・通知 ─────────

        private static UiScreen BuildPause()
        {
            var screen = PopupScreen(PauseId, "一時メニュー", out var window, new Vector2(760f, 620f));
            var controller = screen.gameObject.AddComponent<PauseScreen>();
            Title(window, "一時メニュー", -60f);
            var note = B.Text(window, "Note", "対戦中なので、ゲームは止まりません", 24, A.SubInk);
            B.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(680f, 40f));

            controller.resumeButton = Btn(window, "ResumeButton", "つづける", A.Cyan, new Vector2(520f, 84f));
            B.Place((RectTransform)controller.resumeButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(520f, 84f));
            controller.lobbyButton = Btn(window, "LobbyButton", "全員でロビーに戻る", A.Lavender, new Vector2(520f, 84f));
            B.Place((RectTransform)controller.lobbyButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(520f, 84f));
            controller.lobbyButton.gameObject.AddComponent<UiBindVisible>().key = K.LobbyIsHost;
            controller.leaveButton = Btn(window, "LeaveButton", "試合から抜ける", A.Lavender, new Vector2(520f, 84f));
            B.Place((RectTransform)controller.leaveButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(520f, 84f));
            var leaveVisible = controller.leaveButton.gameObject.AddComponent<UiBindVisible>();
            leaveVisible.key = K.LobbyIsHost;
            leaveVisible.condition = UiVisibleCondition.WhenOff;
            controller.titleButton = Btn(window, "TitleButton", "タイトルへ", A.Gray, new Vector2(520f, 84f));
            B.Place((RectTransform)controller.titleButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(520f, 84f));
            var titleNote = B.Text(window, "TitleNote", "ホストがタイトルへ戻ると、部屋が閉じて全員の試合が終わります", 20, A.SubInk);
            B.Place(titleNote.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -510f), new Vector2(680f, 60f));
            titleNote.gameObject.AddComponent<UiBindVisible>().key = K.LobbyIsHost;

            screen.firstSelected = controller.resumeButton;
            return screen;
        }

        private static UiScreen BuildMessage()
        {
            var screen = PopupScreen(MessageId, "お知らせ", out var window, new Vector2(900f, 460f));
            var title = Title(window, "お知らせ", -64f);
            title.gameObject.AddComponent<UiBindText>().key = K.MessageTitle;
            var body = B.Text(window, "Body", "ここに説明が出ます。", 28, A.SubInk);
            B.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(800f, 170f));
            body.gameObject.AddComponent<UiBindText>().key = K.MessageBody;
            var ok = Btn(window, "OkButton", "OK", A.Cyan, new Vector2(300f, 80f));
            B.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(300f, 80f));
            ok.gameObject.AddComponent<UiButtonAction>().action = UiButtonActionKind.CloseThisScreen;
            B.AddMotion(window.gameObject, UiMotionTrigger.OnShow, UiPreset.Shake, 0.35f, 0.2f);
            screen.firstSelected = ok;
            return screen;
        }

        private static UiScreen BuildToast()
        {
            var screen = B.Screen(ToastId, "通知", "Toast");
            screen.closeOnBack = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.SlideFadeInTop, 0.3f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.SlideFadeOutTop, 0.25f);
            screen.gameObject.AddComponent<ToastScreen>();

            var pill = Card(screen.transform, "Pill", new Color(0.18f, 0.16f, 0.36f, 0.9f));
            B.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(900f, 76f));
            pill.raycastTarget = false;
            var text = B.Text(pill.transform, "Text", "はなこ（P2）が入りました", 30, Color.white, TextAnchor.MiddleCenter, true);
            B.Stretch(text.rectTransform, 12f);
            text.gameObject.AddComponent<UiBindText>().key = K.ToastText;
            var motion = B.AddMotion(pill.gameObject, UiMotionTrigger.Manual, UiPreset.PunchScale, 0.3f);
            motion.entries[motion.entries.Count - 1].name = ToastScreen.BumpMotionName;
            return screen;
        }

        // ───────── 共通の組み立て ─────────

        /// <summary>ロビーのメニューの画面（題・説明つき）。</summary>
        private static UiScreen MenuScreen(string id, string displayName, string title, string lead)
        {
            var screen = B.Screen(id, displayName, "Menu");
            screen.closeOnBack = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.25f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.FadeOut, 0.2f);

            var heading = B.Text(screen.transform, "Title", title, 84, A.Ink, TextAnchor.MiddleLeft, true);
            TopLeft(heading.rectTransform, new Vector2(120f, -40f), new Vector2(1000f, 110f));
            heading.gameObject.AddComponent<Outline>().effectColor = new Color(A.Cyan.r, A.Cyan.g, A.Cyan.b, 0.5f);
            B.AddMotion(heading.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.45f);

            var leadText = B.Text(screen.transform, "Lead", lead, 28, A.SubInk, TextAnchor.MiddleLeft);
            TopLeft(leadText.rectTransform, new Vector2(124f, -150f), new Vector2(1300f, 44f));
            B.AddMotion(leadText.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.45f, 0.08f);
            return screen;
        }

        /// <summary>まん中の小窓（後ろを暗くして押せなくする）。</summary>
        private static UiScreen PopupScreen(string id, string displayName, out Transform window, Vector2 size)
        {
            var screen = B.Screen(id, displayName, "Popup");
            screen.modal = true;
            screen.showMotion = UiMotion.FromPreset(UiPreset.ZoomInFade, 0.25f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.ZoomOutFade, 0.2f);
            var panel = Card(screen.transform, "Window", A.Card);
            B.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            window = panel.transform;
            return screen;
        }

        private static Text Title(Transform window, string text, float y)
        {
            var title = B.Text(window, "Title", text, 44, A.Ink, TextAnchor.MiddleCenter, true);
            B.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(820f, 70f));
            return title;
        }

        private static void Footer(Transform parent)
        {
            var address = B.Text(parent, "LocalAddress", "このPCのアドレス 192.168.1.12", 26, A.Ink, TextAnchor.MiddleLeft, true);
            TopLeftFromBottom(address.rectTransform, new Vector2(120f, 70f), new Vector2(900f, 40f));
            var addressText = address.gameObject.AddComponent<UiBindText>();
            addressText.key = K.LobbyAddress;
            addressText.format = "このPCのアドレス {0}";
            var status = B.Text(parent, "Status", "", 24, A.SubInk, TextAnchor.MiddleLeft);
            TopLeftFromBottom(status.rectTransform, new Vector2(120f, 30f), new Vector2(1400f, 36f));
            status.gameObject.AddComponent<UiBindText>().key = K.LobbyStatus;
        }

        private static void Caption(Transform parent, string name, string text, Vector2 topLeft)
        {
            var caption = B.Text(parent, name, text, 40, A.Ink, TextAnchor.MiddleLeft, true);
            TopLeft(caption.rectTransform, topLeft, new Vector2(800f, 56f));
        }

        private static void Small(Transform parent, string name, string text, Vector2 topLeft)
        {
            var label = B.Text(parent, name, text, 22, A.SubInk, TextAnchor.UpperLeft);
            TopLeft(label.rectTransform, topLeft, new Vector2(900f, 60f));
        }

        private static Button SeatButton(Transform parent, int p)
        {
            var color = FortressColors.PlayerColor(p);
            var button = Btn(parent, $"Seat P{p + 1}", $"P{p + 1}", color, new Vector2(210f, 150f));
            TopLeft((RectTransform)button.transform, new Vector2(40f + p * 228f, -290f), new Vector2(210f, 150f));
            button.transform.Find("Label").GetComponent<Text>().fontSize = 64;

            // 選んでいる番号には、右上にチェックの札を出す（ボタン自体の色は変えない）。
            var selected = Img(button.transform, "Selected", A.Ready, A.Ink, false);
            B.Place(selected.rectTransform, new Vector2(1f, 1f), new Vector2(14f, 14f), new Vector2(58f, 58f));
            selected.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.9f);
            var visible = selected.gameObject.AddComponent<UiBindVisible>();
            visible.key = K.LobbySelectedPlayer;
            visible.condition = UiVisibleCondition.WhenEqual;
            visible.threshold = p;
            B.AddMotion(selected.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.25f);
            B.AddMotion(button.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.25f, 0f, K.LobbySelectedPlayer);
            B.AddMotion(button.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f, 0.06f * p);
            return button;
        }

        private static Button BigButton(Transform parent, string name, string label, string sub, Color color, Vector2 position)
        {
            var button = Btn(parent, name, label, color, new Vector2(620f, 170f));
            B.Place((RectTransform)button.transform, new Vector2(1f, 0.5f), position, new Vector2(620f, 170f));
            var labelText = button.transform.Find("Label").GetComponent<Text>();
            labelText.fontSize = 56;
            B.Place(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(580f, 80f));
            var subText = B.Text(button.transform, "Sub", sub, 24, new Color(1f, 1f, 1f, 0.9f));
            B.Place(subText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(580f, 40f));
            B.AddMotion(button.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.4f, name == "HostButton" ? 0.1f : 0.2f);
            return button;
        }

        private static Text SwapLabel(Transform parent, string name, string text, string key, UiVisibleCondition condition)
        {
            var label = B.Text(parent, name, text, 38, Color.white, TextAnchor.MiddleCenter, true);
            B.Stretch(label.rectTransform, 8f);
            var visible = label.gameObject.AddComponent<UiBindVisible>();
            visible.key = key;
            visible.condition = condition;
            return label;
        }

        /// <summary>ボタン（差し替えられる角丸の絵＋白い太字）。乗せると少し大きく、押すとぷにっと縮む。</summary>
        private static Button Btn(Transform parent, string name, string label, Color color, Vector2 size)
        {
            var image = Card(parent, name, color);
            image.rectTransform.sizeDelta = size;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            ButtonColors(button);
            var text = B.Text(image.transform, "Label", label, Mathf.RoundToInt(Mathf.Min(size.y * 0.4f, 40f)), Color.white, TextAnchor.MiddleCenter, true);
            B.Stretch(text.rectTransform, 6f);
            text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0.2f, 0.25f);
            B.AddMotion(image.gameObject, UiMotionTrigger.OnHover, UiPreset.PunchScale, 0.25f);
            B.AddMotion(image.gameObject, UiMotionTrigger.OnClick, UiPreset.Jelly, 0.35f);
            return button;
        }

        private static void ButtonColors(Button button)
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.88f);
            colors.selectedColor = new Color(1.05f, 1.05f, 1.05f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.8f, 0.45f);
            button.colors = colors;
        }

        private static InputField Input(Transform parent, string name, string placeholder, Vector2 topLeft, Vector2 size)
        {
            var field = Card(parent, name, new Color(0.95f, 0.96f, 1f));
            field.raycastTarget = true;
            TopLeft(field.rectTransform, topLeft, size);
            field.gameObject.AddComponent<Outline>().effectColor = new Color(A.Lavender.r, A.Lavender.g, A.Lavender.b, 0.6f);
            var text = B.Text(field.transform, "Text", "", 34, A.Ink, TextAnchor.MiddleLeft);
            B.Stretch(text.rectTransform, 18f);
            text.supportRichText = false;
            var hint = B.Text(field.transform, "Placeholder", placeholder, 34, new Color(A.SubInk.r, A.SubInk.g, A.SubInk.b, 0.55f), TextAnchor.MiddleLeft);
            B.Stretch(hint.rectTransform, 18f);
            var input = field.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        private static Image Card(Transform parent, string name, Color color) => Img(parent, name, A.Panel, color, true);

        private static Image Img(Transform parent, string name, string spritePath, Color color, bool sliced)
        {
            var rect = B.Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = A.Load(spritePath);
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void TopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void TopLeftFromBottom(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
