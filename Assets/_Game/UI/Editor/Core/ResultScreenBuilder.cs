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
using H = MS2026.UI.EditorTools.HudKillCounterBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// リザルト画面（UIスタジオの画面 <see cref="MatchResultPresenter.ResultScreenId"/>）を作る（2026-10-13、ユーザー依頼）。
    /// 中身は <see cref="MatchResultPresenter"/> が段階ごとに書く値（result.step など）で出てくる。作りはHUDの撃破数・コアのバーと同じ絵・色。
    /// ・クリア: 「討伐完了！」→ パネル（全員の撃破数）→ コアの耐久 → コアの輝き（試合前と試合後のコアの色を並べる）→ 各プレイヤーの撃破数
    ///   → 最大コンボと MVP の王冠 → 評価のランクのハンコ → 「握り続けてタイトルへ」。数字はそれぞれ出た瞬間に数え上がる
    /// ・ゲームオーバー（仮）: 「GAME OVER」→ 倒した数 → 「握り続けてタイトルへ」
    /// デザインの決まり（Docs/Reference/UI_Design_References.md [ANTI-AI]）: 色に意味を持たせる（コアの色・プレイヤー色・ランク色）、
    /// 見せ場はランクのハンコ1か所、動きは「出てきた・増えた」ときだけ、意味のない飾り（番号・英字の小見出し）を付けない。
    /// 何度実行しても作り直すだけ（Screens/Result.prefab を上書き。手で直した物は戻るので、直した後は押さない）。
    /// </summary>
    public static class ResultScreenBuilder
    {
        public const string MenuPath = "Tools/UIスタジオ/リザルト画面を作る";
        public const string PrefabPath = "Assets/_Game/UI/Screens/Result.prefab";
        private const string CrownPath = "Assets/_Game/UI/Art/Session/Session_Host.png";

        // 不透明にする（このプロジェクトは色をリニアで混ぜ、ステージの床などは光らせるために白より明るく描くので、少しでも透けると灰色に浮いて見える）。
        private static readonly Color Dim = new Color(0.04f, 0.05f, 0.14f, 1f);
        private static readonly Color Sub = new Color(0.7f, 0.74f, 0.86f);
        private static readonly Color Danger = new Color(1f, 0.36f, 0.46f);

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        public static bool Build(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Notify(interactive, "再生中は作れません。再生を止めてから実行してください。");
                return false;
            }

            H.ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);

            var scratch = EditorSceneManager.NewPreviewScene();
            UiScreen prefab;
            try
            {
                var screen = BuildScreen();
                SceneManager.MoveGameObjectToScene(screen.gameObject, scratch);
                StudioAssets.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                prefab = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, PrefabPath).GetComponent<UiScreen>();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scratch);
            }

            var catalog = UiStudioSetup.Catalog;
            if (catalog.Find(MatchResultPresenter.ResultScreenId) == null)
            {
                Undo.RecordObject(catalog, "画面を一覧に追加");
                catalog.screens.Add(prefab);
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] リザルト画面「{PrefabPath}」を作りました。");
            if (interactive)
            {
                EditorUtility.DisplayDialog("リザルト画面を作る",
                    "リザルト画面（画面 Result）を作りました。試合が終わると自動で開きます。\n\n" +
                    "UIスタジオの「値」ページで「サンプル値で表示」をONにし、「リザルト」の分類の値（リザルトの段階など）を動かすと、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        private static UiScreen BuildScreen()
        {
            var screen = B.Screen(MatchResultPresenter.ResultScreenId, "リザルト", "Popup");
            screen.closeOnBack = false;
            screen.modal = false;
            screen.showMotion = UiMotion.FromPreset(UiPreset.FadeIn, 0.4f);
            screen.hideMotion = UiMotion.FromPreset(UiPreset.FadeOut, 0.3f);
            screen.memo = "試合が終わると MatchResultPresenter が自動で開く。中身は result.step（段階）の値で順に出てくる。";

            var dim = B.Rect(screen.transform, "Dim").gameObject.AddComponent<Image>();
            B.Stretch(dim.rectTransform);
            dim.color = Dim;
            dim.raycastTarget = false;

            var clear = Group(screen.transform, "Clear", FortressUiKeys.ResultClear, UiVisibleCondition.WhenOn, 0.5f);
            B.Stretch(clear);
            BuildClearBanner(clear);
            BuildClearPanel(clear);

            var over = Group(screen.transform, "GameOver", FortressUiKeys.ResultClear, UiVisibleCondition.WhenOff, 0.5f);
            B.Stretch(over);
            BuildGameOver(over);

            BuildNext(screen.transform);
            return screen;
        }

        // ───────── クリア: 「討伐完了！」 ─────────

        private static void BuildClearBanner(RectTransform parent)
        {
            var banner = Group(parent, "Banner", FortressUiKeys.ResultStep, UiVisibleCondition.WhenEqual, ResultRules.StepBanner);
            B.Stretch(banner);
            B.AddMotion(banner.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.55f);
            B.AddMotion(banner.gameObject, UiMotionTrigger.OnHide, UiPreset.ScaleOut, 0.3f);

            var burst = H.Picture(banner, "Burst", "Kill_Burst.png", H.Gold);
            H.Center(burst.rectTransform, new Vector2(0f, 40f), new Vector2(1000f, 1000f));
            B.AddMotion(burst.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 10f);

            var title = H.Number(banner, "Title", 180, Color.white, TextAnchor.MiddleCenter, H.Navy, 6f, 7f);
            title.text = "討伐完了！";
            H.Center(title.rectTransform, new Vector2(0f, 70f), new Vector2(1500f, 220f));

            var total = H.Number(banner, "Total", 60, H.Cyan, TextAnchor.MiddleCenter, H.Navy, 4f, 0f);
            H.Center(total.rectTransform, new Vector2(0f, -90f), new Vector2(1200f, 90f));
            H.Bind(total, FortressUiKeys.ResultTotalKills, "全員で {0:N0} 体を倒した", 0f);
        }

        // ───────── クリア: パネル ─────────

        private static void BuildClearPanel(RectTransform parent)
        {
            var panel = Group(parent, "Panel", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepPanel - 0.5f);
            B.Stretch(panel);
            B.AddMotion(panel.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.4f);

            // 見出し（左上）。ここだけ大きく、あとは段階ごとに出す。
            var head = H.Number(panel, "Head", 76, Color.white, TextAnchor.MiddleLeft, H.Navy, 4f, 4f);
            head.text = "討伐完了";
            H.Center(head.rectTransform, new Vector2(-880f, 440f), new Vector2(600f, 100f), new Vector2(0f, 0.5f), -880f);
            var line = B.Rect(panel, "HeadLine").gameObject.AddComponent<Image>();
            line.color = H.Pink;
            line.raycastTarget = false;
            H.Center(line.rectTransform, new Vector2(-880f, 384f), new Vector2(520f, 6f), new Vector2(0f, 0.5f), -880f);
            var total = H.Label(panel, "Total", "", 34, H.Cyan, TextAnchor.MiddleLeft);
            H.Center(total.rectTransform, new Vector2(-880f, 344f), new Vector2(700f, 50f), new Vector2(0f, 0.5f), -880f);
            H.Bind(total, FortressUiKeys.ResultTotalKills, "全員で {0:N0} 体", 0f);

            BuildCore(panel);
            BuildPlayers(panel);
            BuildRank(panel);
        }

        // コア: 試合前と試合後のコアを並べ（色の変わり方が一目で分かる）、耐久と輝きを数え上げる。
        private static void BuildCore(RectTransform panel)
        {
            var core = B.Rect(panel, "Core");
            H.Center(core, new Vector2(-470f, 0f), new Vector2(820f, 600f));

            var before = Crystal(core, "Before", new Vector2(-230f, 150f), 170f, FortressUiKeys.ResultCoreColorBefore, "試合前");
            before.GetComponent<Image>().color = new Color(0.75f, 0.95f, 1f);
            var after = Crystal(core, "After", new Vector2(80f, 130f), 270f, FortressUiKeys.ResultCoreColorAfter, "試合後");
            B.AddMotion(after.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 2.4f);

            var durability = Stat(core, "Durability", new Vector2(0f, -120f), "コアの耐久", FortressUiKeys.ResultDurability01, ResultRules.StepDurability);
            var durabilityFill = durability.Find("Gauge/Fill").GetComponent<Image>();
            var durabilityColor = durabilityFill.gameObject.AddComponent<UiBindColor>();
            durabilityColor.key = FortressUiKeys.ResultDurability01;
            durabilityColor.target = durabilityFill;
            durabilityColor.gradient = H.HealthGradient();

            var brightness = Stat(core, "Brightness", new Vector2(0f, -230f), "コアの輝き", FortressUiKeys.ResultBrightness01, ResultRules.StepBrightness);
            var brightnessFill = brightness.Find("Gauge/Fill").GetComponent<Image>();
            var brightnessColor = brightnessFill.gameObject.AddComponent<UiBindColor>();
            brightnessColor.key = FortressUiKeys.ResultCoreColorAfter;
            brightnessColor.target = brightnessFill;
        }

        private static RectTransform Crystal(RectTransform parent, string name, Vector2 position, float size, string colorKey, string caption)
        {
            var image = H.Picture(parent, name, "Result_Crystal.png", Color.white);
            image.preserveAspect = true;
            H.Center(image.rectTransform, position, new Vector2(size, size));
            var tint = image.gameObject.AddComponent<UiBindColor>();
            tint.key = colorKey;
            tint.target = image;
            var label = H.Label(image.transform, "Caption", caption, 26, Sub, TextAnchor.UpperCenter);
            H.Center(label.rectTransform, new Vector2(0f, -size * 0.5f - 22f), new Vector2(240f, 36f));
            return image.rectTransform;
        }

        // 1行: 見出し・バー・数字（出た瞬間に 0 → 値 へ数え上がる）。
        private static RectTransform Stat(RectTransform parent, string name, Vector2 position, string caption, string key, int step)
        {
            var row = Group(parent, name, FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, step - 0.5f);
            H.Center(row, position, new Vector2(760f, 96f));
            B.AddMotion(row.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.35f);

            var label = H.Label(row, "Caption", caption, 30, Color.white, TextAnchor.MiddleLeft);
            H.Center(label.rectTransform, new Vector2(-370f, 22f), new Vector2(300f, 44f), new Vector2(0f, 0.5f), -370f);

            var gauge = B.Rect(row, "Gauge");
            H.Center(gauge, new Vector2(-90f, -22f), new Vector2(540f, 28f));
            var back = H.Picture(gauge, "Back", "Core_Bar.png", new Color(H.Navy.r, H.Navy.g, H.Navy.b, 0.85f));
            B.Stretch(back.rectTransform);
            var fill = H.Picture(gauge, "Fill", "Core_Bar.png", H.Cyan);
            H.FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = key;
            bindFill.fill = fill;
            bindFill.smoothSeconds = 0.9f;
            var ticks = H.Picture(gauge, "Ticks", "Core_Ticks.png", Color.white);
            B.Stretch(ticks.rectTransform);

            var value = H.Number(row, "Value", 64, Color.white, TextAnchor.MiddleRight, H.Navy, 3f, 3f);
            H.Center(value.rectTransform, new Vector2(380f, 0f), new Vector2(200f, 90f), new Vector2(1f, 0.5f), 380f);
            H.Bind(value, key, "{0:0%}", 0.9f);
            B.AddMotion(value.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.25f, 0.9f, key);
            return row;
        }

        // プレイヤー: 自分の色の札・撃破数（数え上がる）・最大コンボ・MVP の王冠。
        private static void BuildPlayers(RectTransform panel)
        {
            var players = Group(panel, "Players", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepPlayers - 0.5f);
            H.Center(players, new Vector2(470f, 90f), new Vector2(820f, 600f));
            var caption = H.Label(players, "Caption", "だれが何体倒した？", 30, Color.white, TextAnchor.MiddleLeft);
            H.Center(caption.rectTransform, new Vector2(-380f, 268f), new Vector2(500f, 44f), new Vector2(0f, 0.5f), -380f);

            var crown = AssetDatabase.LoadAssetAtPath<Sprite>(CrownPath);
            for (var p = 0; p < EnemyKillCounter.PlayerCount; p++)
            {
                var row = Group(players, $"P{p + 1}", FortressUiKeys.PlayerInResult(p), UiVisibleCondition.WhenOn, 0.5f);
                H.Center(row, new Vector2(0f, 180f - p * 118f), new Vector2(780f, 104f));
                B.AddMotion(row.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.35f, 0.08f * p);

                var plate = H.Picture(row, "Plate", "Core_Plate.png", Color.white);
                H.Center(plate.rectTransform, Vector2.zero, new Vector2(780f, 102f));

                var tag = H.Picture(row, "Tag", "Kill_YouTag.png", FortressColors.PlayerColor(p));
                H.Center(tag.rectTransform, new Vector2(-290f, 0f), new Vector2(150f, 60f));
                // 札の色はゲーム中の値（キャラ選択で選んだキャラの色）で塗る。
                var tagColor = tag.gameObject.AddComponent<UiBindColor>();
                tagColor.key = FortressUiKeys.PlayerColor(p);
                tagColor.target = tag;
                var tagLabel = H.Label(tag.transform, "Label", $"{p + 1}P", 34, H.Navy, TextAnchor.MiddleCenter);
                H.Center(tagLabel.rectTransform, Vector2.zero, new Vector2(150f, 60f));

                var mvp = Group(row, "Mvp", FortressUiKeys.PlayerIsMvp(p), UiVisibleCondition.WhenOn, 0.5f);
                H.Center(mvp, new Vector2(-340f, 46f), new Vector2(64f, 64f));
                B.AddMotion(mvp.gameObject, UiMotionTrigger.OnShow, UiPreset.Tada, 0.6f);
                if (crown != null)
                {
                    var crownImage = mvp.gameObject.AddComponent<Image>();
                    crownImage.sprite = crown;
                    crownImage.color = H.Gold;
                    crownImage.raycastTarget = false;
                    crownImage.preserveAspect = true;
                }

                var mvpLabel = H.Label(mvp, "Label", "MVP", 22, H.Gold, TextAnchor.MiddleLeft);
                H.Center(mvpLabel.rectTransform, new Vector2(40f, 0f), new Vector2(80f, 30f), new Vector2(0f, 0.5f), 40f);

                var kills = H.Number(row, "Kills", 60, Color.white, TextAnchor.MiddleRight, H.Navy, 3f, 3f);
                H.Center(kills.rectTransform, new Vector2(150f, 6f), new Vector2(300f, 80f), new Vector2(1f, 0.5f), 150f);
                H.Bind(kills, FortressUiKeys.PlayerResultKills(p), "{0:N0}", 1.2f);
                var unit = H.Label(row, "Unit", "体", 28, Sub, TextAnchor.MiddleLeft);
                H.Center(unit.rectTransform, new Vector2(158f, 0f), new Vector2(60f, 40f), new Vector2(0f, 0.5f), 158f);

                var combo = Group(row, "Combo", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepCombos - 0.5f);
                H.Center(combo, new Vector2(300f, 0f), new Vector2(180f, 80f));
                B.AddMotion(combo.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
                var comboCaption = H.Label(combo, "Caption", "最大コンボ", 20, Sub, TextAnchor.MiddleCenter);
                H.Center(comboCaption.rectTransform, new Vector2(0f, 20f), new Vector2(180f, 30f));
                var comboValue = H.Number(combo, "Value", 38, H.Pink, TextAnchor.MiddleCenter, H.Navy, 3f, 0f);
                H.Center(comboValue.rectTransform, new Vector2(0f, -14f), new Vector2(180f, 50f));
                H.Bind(comboValue, FortressUiKeys.PlayerMaxCombo(p), "x{0:0}", 0.5f);
            }
        }

        // 評価のランク: 最後に右下へハンコを押す（この画面の見せ場はここ1か所）。
        private static void BuildRank(RectTransform panel)
        {
            var rank = Group(panel, "Rank", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepRank - 0.5f);
            H.Center(rank, new Vector2(650f, -290f), new Vector2(420f, 420f));
            B.AddMotion(rank.gameObject, UiMotionTrigger.OnShow, UiPreset.ZoomInFade, 0.35f);
            B.AddMotion(rank.gameObject, UiMotionTrigger.OnShow, UiPreset.ShakeHard, 0.3f, 0.35f);

            var burst = H.Picture(rank, "Burst", "Kill_Burst.png", Color.white);
            H.Center(burst.rectTransform, Vector2.zero, new Vector2(480f, 480f));
            var burstColor = burst.gameObject.AddComponent<UiBindColor>();
            burstColor.key = FortressUiKeys.ResultRankColor;
            burstColor.target = burst;
            burstColor.keepAlpha = false;
            B.AddMotion(burst.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 12f);

            var tilt = B.Rect(rank, "Tilt");
            H.Center(tilt, Vector2.zero, new Vector2(360f, 360f));
            tilt.localEulerAngles = new Vector3(0f, 0f, 12f);
            var stamp = H.Picture(tilt, "Stamp", "Result_Stamp.png", Color.white);
            H.Center(stamp.rectTransform, Vector2.zero, new Vector2(330f, 330f));
            var stampColor = stamp.gameObject.AddComponent<UiBindColor>();
            stampColor.key = FortressUiKeys.ResultRankColor;
            stampColor.target = stamp;
            var letter = H.Number(tilt, "Letter", 220, Color.white, TextAnchor.MiddleCenter, H.Navy, 6f, 6f);
            H.Center(letter.rectTransform, new Vector2(0f, 6f), new Vector2(330f, 300f));
            H.Bind(letter, FortressUiKeys.ResultRank, "{0}", 0f);
            var letterColor = letter.gameObject.AddComponent<UiBindColor>();
            letterColor.key = FortressUiKeys.ResultRankColor;
            letterColor.target = letter;

            var comment = H.Label(rank, "Comment", "", 32, Color.white, TextAnchor.MiddleCenter);
            H.Center(comment.rectTransform, new Vector2(-20f, -190f), new Vector2(620f, 50f));
            H.Bind(comment, FortressUiKeys.ResultRankComment, "{0}", 0f);
        }

        /// <summary>
        /// ゲームオーバーの一言を、終わった理由（コアが壊れた／時間切れ・行けたウェーブ）の値 result.subtitle にする。
        /// 値が空（ウェーブの計画が無いステージの古い試合など）なら、今までどおり「コアが壊されてしまった…」。
        /// </summary>
        internal static void BindSubtitle(Text message)
        {
            if (message.GetComponent<UiBindText>() != null)
            {
                return;
            }

            var bind = message.gameObject.AddComponent<UiBindText>();
            bind.key = FortressUiKeys.ResultSubtitle;
            bind.format = "{0}";
            bind.whenEmpty = "コアが壊されてしまった…";
        }

        // ───────── ゲームオーバー（仮） ─────────

        private static void BuildGameOver(RectTransform parent)
        {
            var title = H.Number(parent, "Title", 170, Danger, TextAnchor.MiddleCenter, H.Navy, 6f, 0f);
            title.text = "GAME OVER";
            H.Center(title.rectTransform, new Vector2(0f, 160f), new Vector2(1500f, 220f));
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.ShakeHard, 0.5f);

            var message = H.Label(parent, "Message", "コアが壊されてしまった…", 44, Color.white, TextAnchor.MiddleCenter);
            H.Center(message.rectTransform, new Vector2(0f, 20f), new Vector2(1200f, 70f));
            BindSubtitle(message);

            var panel = Group(parent, "Panel", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepPanel - 0.5f);
            H.Center(panel, new Vector2(0f, -110f), new Vector2(1200f, 120f));
            B.AddMotion(panel.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.35f);
            var total = H.Number(panel, "Total", 54, H.Cyan, TextAnchor.MiddleCenter, H.Navy, 3f, 0f);
            B.Stretch(total.rectTransform);
            H.Bind(total, FortressUiKeys.ResultTotalKills, "倒した数  {0:N0} 体", 0f);

            var note = H.Label(parent, "Note", "（仮の画面）", 22, Sub, TextAnchor.LowerRight);
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = new Vector2(1f, 0f);
            note.rectTransform.pivot = new Vector2(1f, 0f);
            note.rectTransform.anchoredPosition = new Vector2(-40f, 30f);
            note.rectTransform.sizeDelta = new Vector2(300f, 40f);
        }

        // ───────── 共通: 「握り続けてタイトルへ」 ─────────

        private static void BuildNext(Transform screen)
        {
            var next = Group(screen, "Next", FortressUiKeys.ResultStep, UiVisibleCondition.WhenGreater, ResultRules.StepNext - 0.5f);
            next.anchorMin = next.anchorMax = new Vector2(0.5f, 0f);
            next.pivot = new Vector2(0.5f, 0f);
            next.anchoredPosition = new Vector2(0f, 40f);
            next.sizeDelta = new Vector2(760f, 120f);
            B.AddMotion(next.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.35f);

            var label = H.Label(next, "Label", "握り続けてタイトルへ", 36, Color.white, TextAnchor.MiddleCenter);
            H.Center(label.rectTransform, new Vector2(0f, 30f), new Vector2(760f, 50f));
            B.AddMotion(label.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.6f);

            var gauge = B.Rect(next, "Hold");
            H.Center(gauge, new Vector2(0f, -10f), new Vector2(440f, 18f));
            var back = H.Picture(gauge, "Back", "Kill_Bar.png", new Color(1f, 1f, 1f, 0.2f));
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var fill = H.Picture(gauge, "Fill", "Kill_Bar.png", H.Cyan);
            H.FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.ResultNextHold01;
            bindFill.fill = fill;
            bindFill.smoothSeconds = 0.03f;

            var auto = H.Label(next, "Auto", "", 22, Sub, TextAnchor.MiddleCenter);
            H.Center(auto.rectTransform, new Vector2(0f, -42f), new Vector2(760f, 32f));
            H.Bind(auto, FortressUiKeys.ResultAutoSeconds, "{0:0} 秒たつと自動でタイトルへ", 0f);
        }

        // ───────── 部品 ─────────

        /// <summary>値で出し入れするまとまり（CanvasGroup ＋ 値で出す／隠す）。出る・消える動きは、このまとまりに付ける。</summary>
        private static RectTransform Group(Transform parent, string name, string key, UiVisibleCondition condition, float threshold)
        {
            var rect = B.Rect(parent, name);
            rect.gameObject.AddComponent<CanvasGroup>();
            var visible = rect.gameObject.AddComponent<UiBindVisible>();
            visible.key = key;
            visible.condition = condition;
            visible.threshold = threshold;
            return rect;
        }

        private static void Notify(bool interactive, string message)
        {
            if (interactive)
            {
                EditorUtility.DisplayDialog("リザルト画面を作る", message, "OK");
            }
            else
            {
                Debug.LogWarning("[UIスタジオ] " + message);
            }
        }
    }
}
