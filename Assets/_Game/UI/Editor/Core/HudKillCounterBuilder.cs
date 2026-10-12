using DDrive.Runtime.Ui;
using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ゲーム中HUD（画面 Hud）に撃破数の表示を入れる（2026-10-13、ユーザー依頼「熱ゲージがあった場所に、めっちゃおしゃれな撃破数」）。
    /// ・下の中央（自分の熱ゲージ LocalHeat の場所。熱ゲージは消さずに隠す＝ON にすれば戻る）: 全員の撃破数（大きな数字）＋ 自分の撃破数（自分の色の札）
    /// ・その上: 「x25 COMBO!」と、途切れるまでの残りのバー（自分の撃破が続いている間だけ）
    /// ・画面の中央: 区切りの数（100・250・500・1000…）に届いた瞬間の「1,000 DEFEATED!」（光の放射つき、少しの間だけ）
    /// ・上の中央: コアの体力バー（2026-10-13 追加。古い CoreHp は消さずに隠す）。撃破数と同じ見た目の斜めの札に、水晶のアイコン・残りの割合・体力で色が変わるバー
    /// 数字は倒すたびに弾む（値が増えたときの動き）。世界観（電脳世界のクリーニング）に合わせ、水色・ピンク・紺、色ずれ文字。
    /// 大きさ（2026-10-13 ユーザー「撃破数をもうちょい小さく」）: いちばん大事なコアの体力を目立たせ、撃破数は一回り小さくする（Docs/Reference/UI_Design_References.md の
    /// 「大事な物を1つだけ大きく」）。部品は 1920×1080 基準の元の大きさで組み、中の「Body」を縮めて大きさを決める（外側の出る・消える動きと縮小がぶつからないように）。
    /// 何度実行しても作り直すだけ（前の部品は消して作る）。絵は Assets/_Game/UI/Art/Hud/ の PNG を上書きすれば差し替わる。
    /// </summary>
    public static class HudKillCounterBuilder
    {
        public const string MenuPath = "Tools/UIスタジオ/撃破数とコアの体力バーをHUDに入れる";
        public const string HudScreenId = "Hud";
        public const string ArtFolder = "Assets/_Game/UI/Art/Hud";

        private const string CounterName = "KillCounter";
        private const string ComboName = "KillCombo";
        private const string MilestoneName = "KillMilestone";
        private const string CoreName = "CoreStatus";
        private const string OldCoreName = "CoreHp";
        private const string HeatName = "LocalHeat";
        private const string OverheatName = "Overheat";
        private const string SparklePath = "Assets/CharacterSelect/Art/CS_Sparkle.png";

        private static readonly Color Cyan = new Color(0.43f, 0.93f, 1f);
        private static readonly Color Pink = new Color(1f, 0.5f, 0.84f);
        private static readonly Color Navy = new Color(0.06f, 0.08f, 0.2f);
        private static readonly Color Magenta = new Color(1f, 0.24f, 0.78f, 0.8f);
        private static readonly Color CyanShadow = new Color(0.24f, 0.9f, 1f, 0.8f);
        private static readonly Color Gold = new Color(1f, 0.86f, 0.5f, 0.9f);

        // 大きさ（1 = 元の大きさ）。
        private const float CounterScale = 0.72f;
        private const float ComboScale = 0.72f;
        private const float MilestoneScale = 0.85f;
        private const float CoreScale = 0.8f;

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        /// <summary>HUD に撃破数を入れる。できたら true。</summary>
        public static bool Build(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Notify(interactive, "再生中は作れません。再生を止めてから実行してください。");
                return false;
            }

            var hud = UiStudioSetup.Catalog.Find(HudScreenId);
            var path = hud != null ? AssetDatabase.GetAssetPath(hud) : null;
            if (string.IsNullOrEmpty(path))
            {
                Notify(interactive, $"画面「{HudScreenId}」（ゲーム中HUD）がありません。UIスタジオの「画面」ページで「ゲーム中HUD」を作ってから実行してください。");
                return false;
            }

            ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.transform;
                foreach (var old in new[] { CounterName, ComboName, MilestoneName, CoreName })
                {
                    var found = screen.Find(old);
                    if (found != null)
                    {
                        Object.DestroyImmediate(found.gameObject);
                    }
                }

                // 熱ゲージは消さずに隠す（ON にすれば元に戻る）。
                var heat = screen.Find(HeatName);
                if (heat != null)
                {
                    heat.gameObject.SetActive(false);
                }

                // OVERHEAT の表示は、コンボと重ならないように少し上へ（雛形の位置のときだけ動かす）。
                var overheat = screen.Find(OverheatName) as RectTransform;
                if (overheat != null && Mathf.Approximately(overheat.anchoredPosition.y, 170f))
                {
                    overheat.anchoredPosition = new Vector2(overheat.anchoredPosition.x, 330f);
                }

                // 古いコアの体力バーは消さずに隠す（ON にすれば元に戻る）。
                var oldCore = screen.Find(OldCoreName);
                if (oldCore != null)
                {
                    oldCore.gameObject.SetActive(false);
                }

                BuildCounter(screen);
                BuildCombo(screen);
                BuildMilestone(screen);
                BuildCore(screen);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] 「{path}」に撃破数とコアの体力バーを入れました（熱ゲージ {HeatName} と古いコアのバー {OldCoreName} は隠しました。ON にすれば戻ります）。");
            if (interactive)
            {
                EditorUtility.DisplayDialog("撃破数とコアの体力バーをHUDに入れる",
                    "ゲーム中HUD に撃破数とコアの体力バーを入れました。\n\n・上の中央: コアの体力（古いバーは隠しました）\n・下の中央: 全員の撃破数 ＋ 自分の撃破数（熱ゲージは隠しました）\n・その上: コンボ\n・画面の中央: 区切りの数のお祝い\n\n" +
                    "UIスタジオの「値」ページで「サンプル値で表示」をONにすると、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        // ───────── 下の中央: 全員の撃破数 ＋ 自分の撃破数 ─────────

        private static void BuildCounter(Transform screen)
        {
            var root = B.Rect(screen, CounterName);
            B.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(900f, 176f) * CounterScale);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.45f);
            var counter = Body(root, new Vector2(900f, 176f), CounterScale);

            var plate = Picture(counter, "Plate", "Kill_Plate.png", Color.white);
            B.Stretch(plate.rectTransform);

            // ばいきんのアイコン（ゆらゆら揺れる）。
            var icon = Picture(counter, "Icon", "Kill_GermIcon.png", Color.white);
            Center(icon.rectTransform, new Vector2(-352f, 4f), new Vector2(150f, 150f));
            B.AddMotion(icon.gameObject, UiMotionTrigger.Loop, UiPreset.WobbleLoop, 2.4f);

            var caption = Label(counter, "Caption", "TEAM DEFEATED", 24, Cyan, TextAnchor.MiddleLeft);
            Center(caption.rectTransform, new Vector2(-100f, 52f), new Vector2(330f, 34f), new Vector2(0f, 0.5f), -262f);

            // 全員の撃破数（倒すたびに弾む）。弾む動きは外枠、数字は中の文字（文字の大きさを変える動きと数え上げがぶつからないように）。
            var totalPop = B.Rect(counter, "TotalPop");
            Center(totalPop, new Vector2(-268f, -10f), new Vector2(430f, 112f), new Vector2(0f, 0.5f), -268f);
            B.AddMotion(totalPop.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.22f, 0f, FortressUiKeys.KillsTotal);
            var total = Number(totalPop, "Total", 92, Color.white, TextAnchor.MiddleLeft, Navy, 3f, 4f);
            B.Stretch(total.rectTransform);
            Bind(total, FortressUiKeys.KillsTotal, "{0:N0}", 0.25f);

            // 自分の撃破数（自分の色の札）。
            var tag = Picture(counter, "You", "Kill_YouTag.png", Color.white);
            Center(tag.rectTransform, new Vector2(262f, -4f), new Vector2(260f, 72f));
            var tagColor = tag.gameObject.AddComponent<UiBindColor>();
            tagColor.key = FortressUiKeys.LocalColor;
            tagColor.target = tag;
            B.AddMotion(tag.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Jelly, 0.3f, 0f, FortressUiKeys.KillsLocal);
            var you = Label(tag.transform, "Label", "YOU", 26, Navy, TextAnchor.MiddleLeft);
            Center(you.rectTransform, new Vector2(-100f, 0f), new Vector2(90f, 40f), new Vector2(0f, 0.5f), -100f);
            var own = Number(tag.transform, "Count", 44, Color.white, TextAnchor.MiddleRight, Navy, 3f, 0f);
            Center(own.rectTransform, new Vector2(105f, 0f), new Vector2(190f, 60f), new Vector2(1f, 0.5f), 105f);
            Bind(own, FortressUiKeys.KillsLocal, "{0:N0}", 0.2f);
        }

        // ───────── その上: コンボ ─────────

        private static void BuildCombo(Transform screen)
        {
            var comboRoot = B.Rect(screen, ComboName);
            B.Place(comboRoot, new Vector2(0.5f, 0f), new Vector2(300f * CounterScale, 18f + 176f * CounterScale + 8f), new Vector2(320f, 92f) * ComboScale);
            comboRoot.gameObject.AddComponent<CanvasGroup>();
            var visible = comboRoot.gameObject.AddComponent<UiBindVisible>();
            visible.key = FortressUiKeys.KillsCombo;
            visible.condition = UiVisibleCondition.WhenGreater;
            visible.threshold = 2.5f; // 3コンボから出す
            B.AddMotion(comboRoot.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            B.AddMotion(comboRoot.gameObject, UiMotionTrigger.OnHide, UiPreset.PopOut, 0.2f);
            var combo = Body(comboRoot, new Vector2(320f, 92f), ComboScale);

            // コンボの数（増えるたびに弾む）。
            // 数字は右寄せで左へ伸ばし、「COMBO!」はその右に置く（x138 のように桁が増えても重ならない）。
            var pop = B.Rect(combo, "Pop");
            Center(pop, new Vector2(-6f, 14f), new Vector2(240f, 74f), new Vector2(1f, 0.5f), -6f);
            B.AddMotion(pop.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.18f, 0f, FortressUiKeys.KillsCombo);
            var count = Number(pop, "Count", 64, Color.white, TextAnchor.MiddleRight, new Color(0.55f, 0.1f, 0.45f), 3f, 3f);
            B.Stretch(count.rectTransform);
            Bind(count, FortressUiKeys.KillsCombo, "x{0}", 0f);

            var word = Number(combo, "Word", 32, Pink, TextAnchor.MiddleLeft, Navy, 3f, 0f);
            word.text = "COMBO!";
            Center(word.rectTransform, new Vector2(6f, 8f), new Vector2(170f, 44f), new Vector2(0f, 0.5f), 6f);
            B.AddMotion(word.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.9f);

            // 途切れるまでの残り。
            var back = Picture(combo, "Timer", "Kill_Bar.png", new Color(Navy.r, Navy.g, Navy.b, 0.65f));
            back.type = UnityEngine.UI.Image.Type.Sliced;
            Center(back.rectTransform, new Vector2(0f, -36f), new Vector2(290f, 14f));
            var fill = Picture(back.transform, "Fill", "Kill_Bar.png", Pink);
            B.Stretch(fill.rectTransform, 2f);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.KillsComboTime01;
            bindFill.fill = fill;
            bindFill.smoothSeconds = 0.05f;
        }

        // ───────── 画面の中央: 区切りの数のお祝い ─────────

        private static void BuildMilestone(Transform screen)
        {
            var milestoneRoot = B.Rect(screen, MilestoneName);
            B.Place(milestoneRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1000f, 320f) * MilestoneScale);
            milestoneRoot.gameObject.AddComponent<CanvasGroup>();
            var visible = milestoneRoot.gameObject.AddComponent<UiBindVisible>();
            visible.key = FortressUiKeys.KillsMilestoneShowing;
            visible.condition = UiVisibleCondition.WhenOn;
            B.AddMotion(milestoneRoot.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);
            B.AddMotion(milestoneRoot.gameObject, UiMotionTrigger.OnHide, UiPreset.ScaleOut, 0.3f);
            var milestone = Body(milestoneRoot, new Vector2(1000f, 320f), MilestoneScale);

            // 後ろの光の放射（ゆっくり回る）と輪。
            var burst = Picture(milestone, "Burst", "Kill_Burst.png", Gold);
            Center(burst.rectTransform, Vector2.zero, new Vector2(640f, 640f));
            B.AddMotion(burst.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 8f);
            var burst2 = Picture(milestone, "Burst2", "Kill_Burst.png", new Color(Pink.r, Pink.g, Pink.b, 0.55f));
            Center(burst2.rectTransform, Vector2.zero, new Vector2(520f, 520f));
            burst2.rectTransform.localEulerAngles = new Vector3(0f, 0f, 10f);
            B.AddMotion(burst2.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.2f);
            var ring = Picture(milestone, "Ring", "Kill_Ring.png", new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
            Center(ring.rectTransform, Vector2.zero, new Vector2(340f, 340f));
            B.AddMotion(ring.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 0.8f);

            var sparkle = AssetDatabase.LoadAssetAtPath<Sprite>(SparklePath);
            if (sparkle != null)
            {
                var spots = new[] { new Vector2(-330f, 90f), new Vector2(320f, 110f), new Vector2(-280f, -110f), new Vector2(350f, -80f) };
                for (var i = 0; i < spots.Length; i++)
                {
                    var star = B.Rect(milestone, $"Sparkle{i + 1}").gameObject.AddComponent<Image>();
                    star.sprite = sparkle;
                    star.raycastTarget = false;
                    star.color = i % 2 == 0 ? Color.white : Cyan;
                    Center(star.rectTransform, spots[i], Vector2.one * (i % 2 == 0 ? 64f : 46f));
                    B.AddMotion(star.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 0.6f + 0.15f * i);
                }
            }

            var number = Number(milestone, "Number", 150, Color.white, TextAnchor.MiddleCenter, Navy, 5f, 6f);
            Center(number.rectTransform, new Vector2(0f, 24f), new Vector2(1000f, 170f));
            Bind(number, FortressUiKeys.KillsMilestone, "{0:N0}", 0f);
            B.AddMotion(number.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Tada, 0.6f, 0.1f, FortressUiKeys.KillsMilestone);

            var word = Number(milestone, "Word", 58, Cyan, TextAnchor.MiddleCenter, Navy, 4f, 3f);
            word.text = "DEFEATED!";
            Center(word.rectTransform, new Vector2(0f, -84f), new Vector2(800f, 76f));
        }

        // ───────── 上の中央: コアの体力 ─────────

        // いちばん大事な情報（0 になると負け）。残りの割合を大きな数字とバーで出し、バーの色に意味を持たせる
        // （水色 = 元気 → 黄色 → 赤みのピンク = 危ない）。減ったときは揺れて、白い残像が遅れて追いかける。
        // 3割を切ったら、札の後ろが赤くゆっくり脈打つ（強い点滅はしない。展示会場で誰でも見られるように）。
        private static void BuildCore(Transform screen)
        {
            var root = B.Rect(screen, CoreName);
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(860f, 112f) * CoreScale);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.45f);
            var core = Body(root, new Vector2(860f, 112f), CoreScale);

            // 危ないとき（3割未満）だけ出る、赤い光。出し入れは外枠、脈打つのは中の絵（透明度がぶつからないように）。
            var danger = B.Rect(core, "Danger");
            B.Stretch(danger);
            danger.gameObject.AddComponent<CanvasGroup>();
            var dangerVisible = danger.gameObject.AddComponent<UiBindVisible>();
            dangerVisible.key = FortressUiKeys.CoreHp01;
            dangerVisible.condition = UiVisibleCondition.WhenLess;
            dangerVisible.threshold = 0.3f;
            var glow = Picture(danger, "Glow", "Core_Plate.png", new Color(1f, 0.3f, 0.42f, 0.55f));
            Center(glow.rectTransform, Vector2.zero, new Vector2(900f, 136f));
            B.AddMotion(glow.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.2f);

            var plate = Picture(core, "Plate", "Core_Plate.png", Color.white);
            B.Stretch(plate.rectTransform);

            var icon = Picture(core, "Icon", "Core_Icon.png", Color.white);
            Center(icon.rectTransform, new Vector2(-352f, 2f), new Vector2(104f, 104f));
            B.AddMotion(icon.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 2.2f);
            B.AddMotion(icon.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchRotation, 0.3f, 0f, FortressUiKeys.CoreHp01);

            var caption = Label(core, "Caption", "CORE", 22, Cyan, TextAnchor.MiddleLeft);
            Center(caption.rectTransform, new Vector2(-292f, 30f), new Vector2(120f, 30f), new Vector2(0f, 0.5f), -292f);

            // バー（背景・残像・中身・10%ごとの目盛り）。減ったときに揺れる。
            var gauge = B.Rect(core, "Gauge");
            Center(gauge, new Vector2(-36f, -12f), new Vector2(520f, 32f));
            B.AddMotion(gauge.gameObject, UiMotionTrigger.OnValueDown, UiPreset.Shake, 0.3f, 0f, FortressUiKeys.CoreHp01);
            var back = Picture(gauge, "Back", "Core_Bar.png", new Color(Navy.r, Navy.g, Navy.b, 0.75f));
            B.Stretch(back.rectTransform);
            var trail = Picture(gauge, "Trail", "Core_Bar.png", new Color(1f, 1f, 1f, 0.8f));
            FillHorizontal(trail);
            var fill = Picture(gauge, "Fill", "Core_Bar.png", Cyan);
            FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.CoreHp01;
            bindFill.fill = fill;
            bindFill.trail = trail;
            bindFill.smoothSeconds = 0.12f;
            var bindColor = fill.gameObject.AddComponent<UiBindColor>();
            bindColor.key = FortressUiKeys.CoreHp01;
            bindColor.target = fill;
            bindColor.gradient = HealthGradient();
            var ticks = Picture(gauge, "Ticks", "Core_Ticks.png", Color.white);
            B.Stretch(ticks.rectTransform);

            // 残りの割合（大きな数字）。
            var percent = Number(core, "Percent", 46, Color.white, TextAnchor.MiddleRight, Navy, 3f, 3f);
            Center(percent.rectTransform, new Vector2(372f, 0f), new Vector2(150f, 70f), new Vector2(1f, 0.5f), 372f);
            Bind(percent, FortressUiKeys.CoreHp01, "{0:0%}", 0f);
        }

        // 体力の色: 0 = 赤みのピンク（危ない）、0.35 = 黄色、1 = 水色（元気）。
        private static Gradient HealthGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.3f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.82f, 0.32f), 0.35f),
                    new GradientColorKey(new Color(0.55f, 0.95f, 0.75f), 0.65f),
                    new GradientColorKey(Cyan, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static void FillHorizontal(Image image)
        {
            B.Stretch(image.rectTransform);
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        }

        /// <summary>外枠（出る・消える動きの係）の中に、元の大きさで組んで scale で縮める「Body」を作る。</summary>
        private static RectTransform Body(RectTransform root, Vector2 designSize, float scale)
        {
            var body = B.Rect(root, "Body");
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = designSize;
            body.localScale = new Vector3(scale, scale, 1f);
            return body;
        }

        // ───────── 部品 ─────────

        private static Image Picture(Transform parent, string name, string file, Color color)
        {
            var image = B.Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{file}");
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            var label = B.Text(parent, name, text, size, color, anchor, true);
            label.fontStyle = FontStyle.BoldAndItalic;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        /// <summary>太い斜体の数字。縁取り（紺）と、左右にずらした赤紫・水色の影（色ずれ）。</summary>
        private static Text Number(Transform parent, string name, int size, Color color, TextAnchor anchor, Color outline, float outlineWidth, float split)
        {
            var text = Label(parent, name, "0", size, color, anchor);
            if (split > 0f)
            {
                var a = text.gameObject.AddComponent<Shadow>();
                a.effectColor = Magenta;
                a.effectDistance = new Vector2(-split, 0f);
                var b = text.gameObject.AddComponent<Shadow>();
                b.effectColor = CyanShadow;
                b.effectDistance = new Vector2(split, 0f);
            }

            var line = text.gameObject.AddComponent<Outline>();
            line.effectColor = outline;
            line.effectDistance = new Vector2(outlineWidth, -outlineWidth);
            return text;
        }

        private static void Bind(Text text, string key, string format, float countUp)
        {
            var bind = text.gameObject.AddComponent<UiBindText>();
            bind.key = key;
            bind.format = format;
            bind.countUpSeconds = countUp;
        }

        /// <summary>親の中央を基準に置く。pivot を左右の端にしたときは、その端の x を edgeX で渡す。</summary>
        private static void Center(RectTransform rect, Vector2 position, Vector2 size, Vector2? pivot = null, float? edgeX = null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(edgeX ?? position.x, position.y);
        }

        // 絵を「UIの絵（Sprite）」として読み込む。バーは角丸を保ったまま伸ばせるように、9スライスの枠を付ける。
        private static void ConfigureArt()
        {
            foreach (var file in new[] { "Kill_Plate.png", "Kill_YouTag.png", "Kill_GermIcon.png", "Kill_Bar.png", "Kill_Burst.png", "Kill_Ring.png",
                         "Core_Plate.png", "Core_Bar.png", "Core_Ticks.png", "Core_Icon.png" })
            {
                var path = $"{ArtFolder}/{file}";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[UIスタジオ] 撃破数の絵「{path}」がありません。");
                    continue;
                }

                var border = file == "Kill_Bar.png" ? new Vector4(12f, 11f, 12f, 11f) : Vector4.zero;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    !importer.mipmapEnabled && importer.alphaIsTransparency && importer.spriteBorder == border)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }

        private static void Notify(bool interactive, string message)
        {
            if (interactive)
            {
                EditorUtility.DisplayDialog("撃破数とコアの体力バーをHUDに入れる", message, "OK");
            }
            else
            {
                Debug.LogWarning("[UIスタジオ] " + message);
            }
        }
    }
}
