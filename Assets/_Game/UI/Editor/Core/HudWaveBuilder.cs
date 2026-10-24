using DDrive.Runtime.Ui;
using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;
using H = MS2026.UI.EditorTools.HudKillCounterBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ゲーム中HUD（画面 Hud）に、ウェーブの表示を入れる（2026-10-24、ユーザー依頼「コアの体力バーの左隣に、後から素材を差し替える前提で目標敵数と制限時間のUI」）。
    /// ・「WavePlate」: コアのバー（歯）の左。WAVE 2/5 ・ あと23体（進み具合の棒）・ TIME 02:48。ウェーブの計画が無いステージでは隠れる（wave.active）
    /// ・「WaveAnnounce」: 発表「WAVE 2 / 目標撃破数 60！」＋一言＋「赤い線＝敵の通り道」。最終ウェーブは帯が赤い
    /// ・「WaveCountdown」「WaveGo」: 3・2・1・GO!
    /// ・「WaveClear」: 「WAVE 2 CLEAR! 敵が逃げていく！」
    /// ・「WaveTimeUp」: 「TIME UP!」
    /// あわせて、リザルトの「GAME OVER」の一言を、終わった理由（時間切れ・行けたウェーブ）にする（result.subtitle）。
    /// 絵は Tools/UIArt/make_wave_art.py の仮の絵（Assets/_Game/UI/Art/Hud/Wave_*.png）。同じ名前で上書きすれば差し替わる。
    /// 何度実行しても作り直すだけ（手で直した分は戻るので、直した後は押さない）。
    /// </summary>
    public static class HudWaveBuilder
    {
        public const string MenuPath = "Tools/UIスタジオ/ウェーブの表示をHUDに入れる";

        internal const string PlateName = "WavePlate";
        internal const string AnnounceName = "WaveAnnounce";
        internal const string CountdownName = "WaveCountdown";
        internal const string GoName = "WaveGo";
        internal const string ClearName = "WaveClear";
        internal const string TimeUpName = "WaveTimeUp";

        /// <summary>このビルダーが作る部品の名前（作り直すときに消す）。ほかのウェーブの部品のビルダーも、ここに足す。</summary>
        internal static readonly string[] OwnedNames = { PlateName, AnnounceName, CountdownName, GoName, ClearName, TimeUpName };

        // コアのバー（幅 640、上から 26）の左に、少し離して置く。
        private static readonly Vector2 PlateDesign = new Vector2(300f, 190f);
        private const float PlateScale = 0.8f;
        private const float PlateTop = 24f;
        private const float PlateGap = 18f;
        private const float CoreHalfWidth = 320f;

        private static readonly Color Navy = new Color(0.06f, 0.08f, 0.2f);
        private static readonly Color DeepPink = new Color(0.86f, 0.18f, 0.55f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.4f);

        internal static readonly string[] ArtFiles =
        {
            "Wave_Plate.png", "Wave_Bar.png", "Wave_Banner.png", "Wave_Ring.png", "Wave_Hand.png", "Wave_Brush.png", "Wave_Stain.png", "Wave_Arrow.png"
        };

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        /// <summary>HUD にウェーブの表示を入れる。できたら true。</summary>
        public static bool Build(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Notify(interactive, "再生中は作れません。再生を止めてから実行してください。");
                return false;
            }

            var hud = UiStudioSetup.Catalog.Find(H.HudScreenId);
            var path = hud != null ? AssetDatabase.GetAssetPath(hud) : null;
            if (string.IsNullOrEmpty(path))
            {
                Notify(interactive, $"画面「{H.HudScreenId}」（ゲーム中HUD）がありません。UIスタジオの「画面」ページで「ゲーム中HUD」を作ってから実行してください。");
                return false;
            }

            ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.transform;
                Remove(screen, OwnedNames);
                BuildPlate(screen);
                BuildAnnounce(screen);
                BuildCountdown(screen);
                BuildGo(screen);
                BuildClear(screen);
                BuildTimeUp(screen);
                HudWaveExtrasBuilder.Build(screen);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var resultPatched = PatchResult();
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] 「{path}」にウェーブの表示を入れました（WAVE の札・発表・3・2・1・GO・クリア・時間切れ・歯みがきタイム・ヒント）。" +
                      (resultPatched ? " リザルトの GAME OVER の一言を、終わった理由にしました。" : ""));
            if (interactive)
            {
                EditorUtility.DisplayDialog("ウェーブの表示",
                    "ゲーム中HUD に入れました。\n\n・コアのバーの左: WAVE 2/5 ・ あと○体 ・ TIME\n・画面の中央: 「WAVE n／目標撃破数」の発表、3・2・1・GO!、WAVE CLEAR!、TIME UP!\n" +
                    "・歯みがきタイムの表示、初めての人向けのヒント（あなたの砲台・握り方・敵が来る方向）\n\n" +
                    "絵は仮の物です（Assets/_Game/UI/Art/Hud/Wave_*.png を同じ名前で上書きすると差し替わります）。\n" +
                    "UIスタジオの「値」ページで「サンプル値で表示」をONにすると、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        // ───────── WAVE の札（コアのバーの左） ─────────

        private static void BuildPlate(Transform screen)
        {
            var root = B.Rect(screen, PlateName);
            var size = PlateDesign * PlateScale;
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(-(CoreHalfWidth + PlateGap + size.x * 0.5f), -PlateTop), size);
            Visible(root, FortressUiKeys.WaveActive, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.45f);
            var body = H.Body(root, PlateDesign, PlateScale);

            var plate = H.Picture(body, "Plate", "Wave_Plate.png", Color.white);
            B.Stretch(plate.rectTransform);

            // 1段目: WAVE 2/5（ウェーブが進んだらはねる）
            Caption(body, "WaveCaption", "WAVE", 54f);
            var wave = Value(body, "Wave", 46, 54f);
            H.Bind(wave, FortressUiKeys.WaveNumberText, "{0}", 0f);
            B.AddMotion(wave.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Tada, 0.6f, 0f, FortressUiKeys.WaveNumber);

            // 2段目: あと 23体（1体倒すごとにぽんと縮む）＋ 進み具合の棒
            Caption(body, "GoalCaption", "あと", 4f);
            var remaining = Value(body, "Remaining", 46, 4f);
            remaining.supportRichText = true;
            H.Bind(remaining, FortressUiKeys.WaveRemaining, "{0:0}<size=24>体</size>", 0f);
            B.AddMotion(remaining.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.18f, 0f, FortressUiKeys.WaveRemaining);
            var bar = B.Rect(body, "Progress");
            H.Center(bar, new Vector2(0f, -20f), new Vector2(236f, 9f));
            var back = H.Picture(bar, "Back", "Wave_Bar.png", new Color(1f, 1f, 1f, 0.18f));
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var fill = H.Picture(bar, "Fill", "Wave_Bar.png", H.Cyan);
            H.FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.WaveProgress01;
            bindFill.fill = fill;
            bindFill.smoothSeconds = 0.12f;

            // 3段目: TIME 02:48（残り10秒からは赤い数字が重なって脈打つ）
            Caption(body, "TimeCaption", "TIME", -54f);
            var time = Value(body, "Time", 46, -54f);
            H.Bind(time, FortressUiKeys.WaveTimeText, "{0}", 0f);
            var warn = B.Rect(body, "TimeWarning");
            B.Stretch(warn);
            Visible(warn, FortressUiKeys.WaveTimeWarning, UiVisibleCondition.WhenOn, 0.5f);
            var warnText = Value(warn, "Time", 46, -54f);
            warnText.color = Red;
            H.Bind(warnText, FortressUiKeys.WaveTimeText, "{0}", 0f);
            B.AddMotion(warnText.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 0.5f);

            // 札の下: 「あと少し！」（あと5体から）
            var near = B.Rect(root, "NearGoal");
            near.anchorMin = near.anchorMax = new Vector2(0.5f, 0f);
            near.pivot = new Vector2(0.5f, 1f);
            near.anchoredPosition = new Vector2(0f, -6f);
            near.sizeDelta = new Vector2(260f, 44f);
            Visible(near, FortressUiKeys.WaveNearGoal, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(near.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            var nearText = H.Number(near, "Label", 34, H.Pink, TextAnchor.MiddleCenter, Navy, 3f, 0f);
            nearText.text = "あと少し！";
            B.Stretch(nearText.rectTransform);
            B.AddMotion(nearText.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.8f);
        }

        private static Text Caption(Transform parent, string name, string text, float y)
        {
            var caption = H.Label(parent, name, text, 22, H.Cyan, TextAnchor.MiddleLeft);
            H.Center(caption.rectTransform, new Vector2(-122f, y), new Vector2(110f, 40f), new Vector2(0f, 0.5f));
            return caption;
        }

        private static Text Value(Transform parent, string name, int size, float y)
        {
            var value = H.Number(parent, name, size, Color.white, TextAnchor.MiddleRight, Navy, 3f, 0f);
            H.Center(value.rectTransform, new Vector2(124f, y), new Vector2(190f, 56f), new Vector2(1f, 0.5f));
            return value;
        }

        // ───────── 発表（WAVE 2／目標撃破数 60！） ─────────

        private static void BuildAnnounce(Transform screen)
        {
            var root = B.Rect(screen, AnnounceName);
            B.Stretch(root);
            Visible(root, FortressUiKeys.WaveAnnouncing, UiVisibleCondition.WhenOn, 0.5f);

            var body = B.Rect(root, "Body");
            H.Center(body, new Vector2(0f, 170f), new Vector2(1400f, 260f));
            B.AddMotion(body.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);
            B.AddMotion(body.gameObject, UiMotionTrigger.OnHide, UiPreset.ScaleOut, 0.25f);

            var band = H.Picture(body, "Band", "Wave_Banner.png", Color.white);
            B.Stretch(band.rectTransform);

            // 最終ウェーブだけ、帯の上に赤い帯を重ねる。
            var final = B.Rect(body, "Final");
            B.Stretch(final);
            Visible(final, FortressUiKeys.WaveIsFinal, UiVisibleCondition.WhenOn, 0.5f);
            var finalBand = H.Picture(final, "Band", "Wave_Banner.png", new Color(1f, 0.55f, 0.6f));
            B.Stretch(finalBand.rectTransform);
            B.AddMotion(finalBand.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 0.9f);

            var title = H.Number(body, "Title", 128, Navy, TextAnchor.MiddleCenter, Color.white, 4f, 5f);
            H.Center(title.rectTransform, new Vector2(0f, 40f), new Vector2(1300f, 150f));
            H.Bind(title, FortressUiKeys.WaveAnnounceTitle, "{0}", 0f);

            var goal = H.Number(body, "Goal", 60, DeepPink, TextAnchor.MiddleCenter, Color.white, 3f, 0f);
            H.Center(goal.rectTransform, new Vector2(0f, -70f), new Vector2(1300f, 80f));
            H.Bind(goal, FortressUiKeys.WaveAnnounceGoal, "{0}", 0f);
            B.AddMotion(goal.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f, 0.25f);

            var line = H.Label(root, "Line", "", 40, Color.white, TextAnchor.MiddleCenter);
            H.Center(line.rectTransform, new Vector2(0f, 10f), new Vector2(1400f, 60f));
            var lineOutline = line.gameObject.AddComponent<Outline>();
            lineOutline.effectColor = Navy;
            lineOutline.effectDistance = new Vector2(3f, -3f);
            H.Bind(line, FortressUiKeys.WaveAnnounceText, "{0}", 0f);

            // 初めての人向け: 赤い線が何かを言葉で添える。
            var hint = H.Label(root, "RouteHint", "赤い線 ＝ 敵の通り道", 30, new Color(1f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
            H.Center(hint.rectTransform, new Vector2(0f, -40f), new Vector2(900f, 44f));
            var hintOutline = hint.gameObject.AddComponent<Outline>();
            hintOutline.effectColor = Navy;
            hintOutline.effectDistance = new Vector2(2f, -2f);
            B.AddMotion(hint.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.4f);
        }

        // ───────── 3・2・1・GO! ─────────

        private static void BuildCountdown(Transform screen)
        {
            var root = B.Rect(screen, CountdownName);
            H.Center(root, new Vector2(0f, -150f), new Vector2(260f, 260f));
            Visible(root, FortressUiKeys.WaveCountdown, UiVisibleCondition.WhenGreater, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.25f);

            var ring = H.Picture(root, "Ring", "Wave_Ring.png", H.Cyan);
            B.Stretch(ring.rectTransform);
            B.AddMotion(ring.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 3f);

            var number = H.Number(root, "Number", 170, Color.white, TextAnchor.MiddleCenter, Navy, 5f, 6f);
            B.Stretch(number.rectTransform);
            H.Bind(number, FortressUiKeys.WaveCountdown, "{0:0}", 0f);
            B.AddMotion(number.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.3f, 0f, FortressUiKeys.WaveCountdown);
        }

        private static void BuildGo(Transform screen)
        {
            var root = B.Rect(screen, GoName);
            H.Center(root, new Vector2(0f, -110f), new Vector2(900f, 300f));
            Visible(root, FortressUiKeys.WaveGo, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.ZoomInFade, 0.3f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnHide, UiPreset.ZoomOutFade, 0.25f);
            var go = H.Number(root, "Label", 220, H.Cyan, TextAnchor.MiddleCenter, Navy, 6f, 8f);
            go.text = "GO!";
            B.Stretch(go.rectTransform);
        }

        // ───────── WAVE CLEAR!／TIME UP! ─────────

        private static void BuildClear(Transform screen)
        {
            var root = B.Rect(screen, ClearName);
            B.Stretch(root);
            Visible(root, FortressUiKeys.WaveClearShowing, UiVisibleCondition.WhenOn, 0.5f);

            var body = B.Rect(root, "Body");
            H.Center(body, new Vector2(0f, 150f), new Vector2(1300f, 230f));
            B.AddMotion(body.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);
            B.AddMotion(body.gameObject, UiMotionTrigger.OnHide, UiPreset.FadeOut, 0.3f);
            var band = H.Picture(body, "Band", "Wave_Banner.png", Gold);
            B.Stretch(band.rectTransform);
            var text = H.Number(body, "Label", 116, Navy, TextAnchor.MiddleCenter, Color.white, 4f, 5f);
            H.Center(text.rectTransform, new Vector2(0f, 10f), new Vector2(1250f, 150f));
            H.Bind(text, FortressUiKeys.WaveClearText, "{0}", 0f);

            var sub = H.Label(root, "Sub", "敵が逃げていく！", 40, Color.white, TextAnchor.MiddleCenter);
            H.Center(sub.rectTransform, new Vector2(0f, 0f), new Vector2(900f, 60f));
            var outline = sub.gameObject.AddComponent<Outline>();
            outline.effectColor = Navy;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private static void BuildTimeUp(Transform screen)
        {
            var root = B.Rect(screen, TimeUpName);
            H.Center(root, new Vector2(0f, 120f), new Vector2(1400f, 260f));
            Visible(root, FortressUiKeys.WaveTimeUp, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.ZoomInFade, 0.35f);
            var text = H.Number(root, "Label", 190, Red, TextAnchor.MiddleCenter, Navy, 6f, 6f);
            text.text = "TIME UP!";
            B.Stretch(text.rectTransform);
            B.AddMotion(text.gameObject, UiMotionTrigger.OnShow, UiPreset.ShakeHard, 0.5f, 0.3f);
        }

        // ───────── リザルト ─────────

        // リザルトの「GAME OVER」の一言（「コアが壊されてしまった…」）を、終わった理由の値に付け替える（画面は作り直さない）。
        private static bool PatchResult()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ResultScreenBuilder.PrefabPath) == null)
            {
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(ResultScreenBuilder.PrefabPath);
            try
            {
                var message = root.transform.Find("GameOver/Message");
                var text = message != null ? message.GetComponent<Text>() : null;
                if (text == null || text.GetComponent<UiBindText>() != null)
                {
                    return false;
                }

                ResultScreenBuilder.BindSubtitle(text);
                PrefabUtility.SaveAsPrefabAsset(root, ResultScreenBuilder.PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ───────── 部品 ─────────

        internal static void Remove(Transform screen, string[] names)
        {
            foreach (var name in names)
            {
                var found = screen.Find(name);
                if (found != null)
                {
                    Object.DestroyImmediate(found.gameObject);
                }
            }
        }

        /// <summary>値で出し入れする（CanvasGroup ＋ UiBindVisible）。</summary>
        internal static UiBindVisible Visible(RectTransform rect, string key, UiVisibleCondition condition, float threshold)
        {
            if (rect.GetComponent<CanvasGroup>() == null)
            {
                rect.gameObject.AddComponent<CanvasGroup>();
            }

            var visible = rect.gameObject.AddComponent<UiBindVisible>();
            visible.key = key;
            visible.condition = condition;
            visible.threshold = threshold;
            return visible;
        }

        internal static void ConfigureArt()
        {
            foreach (var file in ArtFiles)
            {
                var path = $"{H.ArtFolder}/{file}";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[UIスタジオ] ウェーブの絵「{path}」がありません（Tools/UIArt/make_wave_art.py で作れます）。");
                    continue;
                }

                var border = file == "Wave_Bar.png" ? new Vector4(10f, 9f, 10f, 9f) : Vector4.zero;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    !importer.mipmapEnabled && importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None &&
                    importer.spriteBorder == border)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }

        private static void Notify(bool interactive, string message)
        {
            if (interactive)
            {
                EditorUtility.DisplayDialog("ウェーブの表示", message, "OK");
            }
            else
            {
                Debug.LogWarning("[UIスタジオ] " + message);
            }
        }
    }
}
