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
    /// ・「WaveHeader」: 上の中央の帯（チームの絵、2026-11-11 から。作るのは <see cref="HudHeaderBuilder"/>）。WAVE 1/3 ・ 進み具合の棒 ・ あと何体 ・ 残り時間。
    ///   ウェーブの計画が無いステージでは隠れる（wave.active）。前はコアのバーの左の箱「WavePlate」（作り直すときに消す）
    /// ・「WaveOpening」: 試合の始まりの「コアを守れ！」（物が落ちてきた後、WAVE 1 の発表の前。2026-11-11）
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

        /// <summary>「コアを守れ！」だけを入れる（ほかの部品は作り直さないので、手で直した分も残る）。</summary>
        public const string OpeningMenuPath = "Tools/UIスタジオ/「コアを守れ！」をHUDに入れる";

        internal const string OpeningName = "WaveOpening";

        internal const string PlateName = "WavePlate";
        internal const string AnnounceName = "WaveAnnounce";
        internal const string CountdownName = "WaveCountdown";
        internal const string GoName = "WaveGo";
        internal const string ClearName = "WaveClear";
        internal const string TimeUpName = "WaveTimeUp";

        /// <summary>このビルダーが作る部品の名前（作り直すときに消す）。ほかのウェーブの部品のビルダーも、ここに足す。</summary>
        internal static readonly string[] OwnedNames = { PlateName, HudHeaderBuilder.HeaderName, OpeningName, AnnounceName, CountdownName, GoName, ClearName, TimeUpName };

        private static readonly Color Navy = new Color(0.06f, 0.08f, 0.2f);
        private static readonly Color DeepPink = new Color(0.86f, 0.18f, 0.55f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.4f);

        internal static readonly string[] ArtFiles =
        {
            "Wave_Bar.png", "Wave_Banner.png", "Wave_Ring.png", "Wave_Arrow.png", "Wave_Panel.png",
            "Brush_Title.png", "Brush_Card.png", "Brush_Chip.png", "Brush_Stage.png", "Brush_Tooth.png", "Brush_Brush.png", "Brush_BrushStripe.png",
            "Brush_Hand.png", "Brush_TimerBack.png", "Brush_TimerFill.png", "Brush_Burst.png", "Brush_Bubble.png", "Brush_Sparkle.png"
        };

        // 9 スライスで伸ばす絵の枠（px）。
        private static Vector4 BorderOf(string file)
        {
            switch (file)
            {
                case "Wave_Bar.png": return new Vector4(10f, 9f, 10f, 9f);
                case "Wave_Panel.png": return new Vector4(44f, 44f, 44f, 44f);
                case "Brush_Chip.png": return new Vector4(44f, 40f, 44f, 40f);
                default: return Vector4.zero;
            }
        }

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
            HudHeaderBuilder.ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.transform;
                Remove(screen, OwnedNames);
                HudHeaderBuilder.BuildHeader(screen);
                BuildOpening(screen);
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
                    "ゲーム中HUD に入れました。\n\n・上の中央の帯: WAVE 1/3 ・ 進み具合の棒 ・ あと何体 ・ 残り時間\n・画面の中央: 「WAVE n／目標撃破数」の発表、3・2・1・GO!、WAVE CLEAR!、TIME UP!\n" +
                    "・歯みがきタイムの表示、初めての人向けのヒント（あなたの砲台・握り方・敵が来る方向）\n\n" +
                    "絵は仮の物です（Assets/_Game/UI/Art/Hud/Wave_*.png を同じ名前で上書きすると差し替わります）。\n" +
                    "UIスタジオの「値」ページで「サンプル値で表示」をONにすると、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        [MenuItem(OpeningMenuPath)]
        private static void BuildOpeningFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Notify(true, "再生中は作れません。再生を止めてから実行してください。");
                return;
            }

            var hud = UiStudioSetup.Catalog.Find(H.HudScreenId);
            var path = hud != null ? AssetDatabase.GetAssetPath(hud) : null;
            if (string.IsNullOrEmpty(path))
            {
                Notify(true, $"画面「{H.HudScreenId}」（ゲーム中HUD）がありません。UIスタジオの「画面」ページで「ゲーム中HUD」を作ってから実行してください。");
                return;
            }

            ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.transform;
                Remove(screen, new[] { OpeningName });
                var opening = BuildOpening(screen);
                // 発表のすぐ手前に並べる（重なったときは発表が上）。
                var announce = screen.Find(AnnounceName);
                if (announce != null)
                {
                    opening.SetSiblingIndex(announce.GetSiblingIndex());
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] 「{path}」に「コアを守れ！」を入れました（ほかの部品はそのまま）。");
            EditorUtility.DisplayDialog("「コアを守れ！」",
                "ゲーム中HUD に「コアを守れ！」を入れました。\n\n試合の始まり、物が落ちてきた後・WAVE 1 の発表の前に出ます。\n" +
                "秒数と文字は要塞デザイナーの「敵ウェーブ」→「全体」→「表示のタイミング」で変えられます。", "OK");
        }

        // ───────── 試合の始まりの「コアを守れ！」 ─────────

        private static RectTransform BuildOpening(Transform screen)
        {
            var root = B.Rect(screen, OpeningName);
            B.Stretch(root);
            Visible(root, FortressUiKeys.WaveOpening, UiVisibleCondition.WhenOn, 0.5f);

            // 発表と同じ帯・同じ場所（続けて出るので、帯が残ったまま文字だけ変わって見える）。
            var body = B.Rect(root, "Body");
            H.Center(body, new Vector2(0f, 170f), new Vector2(1400f, 260f));
            B.AddMotion(body.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);
            B.AddMotion(body.gameObject, UiMotionTrigger.OnHide, UiPreset.ScaleOut, 0.25f);

            var band = H.Picture(body, "Band", "Wave_Banner.png", Color.white);
            B.Stretch(band.rectTransform);

            var title = H.Number(body, "Title", 132, Color.white, TextAnchor.MiddleCenter, Navy, 6f, 6f);
            H.Center(title.rectTransform, new Vector2(0f, 10f), new Vector2(1300f, 170f));
            H.Bind(title, FortressUiKeys.WaveOpeningText, "{0}", 0f);
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.PunchScale, 0.35f, 0.3f);
            return root;
        }

        // ───────── 発表（WAVE 2／目標撃破数 60！） ─────────

        private static void BuildAnnounce(Transform screen)
        {
            var root = B.Rect(screen, AnnounceName);
            B.Stretch(root);
            Visible(root, FortressUiKeys.WaveShowBanner, UiVisibleCondition.WhenOn, 0.5f);

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

            var title = H.Number(body, "Title", 128, Color.white, TextAnchor.MiddleCenter, Navy, 6f, 6f);
            H.Center(title.rectTransform, new Vector2(0f, 40f), new Vector2(1300f, 150f));
            H.Bind(title, FortressUiKeys.WaveAnnounceTitle, "{0}", 0f);

            var goal = H.Number(body, "Goal", 60, Color.white, TextAnchor.MiddleCenter, DeepPink, 4f, 3f);
            H.Center(goal.rectTransform, new Vector2(0f, -70f), new Vector2(1300f, 80f));
            H.Bind(goal, FortressUiKeys.WaveAnnounceGoal, "{0}", 0f);
            B.AddMotion(goal.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.35f, 0.25f);

            var line = H.Label(root, "Line", "", 40, Color.white, TextAnchor.MiddleCenter);
            H.Center(line.rectTransform, new Vector2(0f, 10f), new Vector2(1400f, 60f));
            var lineOutline = line.gameObject.AddComponent<Outline>();
            lineOutline.effectColor = Navy;
            lineOutline.effectDistance = new Vector2(3f, -3f);
            H.Bind(line, FortressUiKeys.WaveAnnounceText, "{0}", 0f);

            // 初めての人向け: 赤い線が何かを言葉で添える（表示の設定で ON のときだけ）。
            var hintRoot = B.Rect(root, "RouteHint");
            B.Stretch(hintRoot);
            Visible(hintRoot, FortressUiKeys.WaveShowRouteHint, UiVisibleCondition.WhenOn, 0.5f);
            var hint = H.Label(hintRoot, "Label", "赤い線 ＝ 敵の通り道", 30, new Color(1f, 0.75f, 0.75f), TextAnchor.MiddleCenter);
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
            var band = H.Picture(body, "Band", "Wave_Banner.png", Color.white);
            B.Stretch(band.rectTransform);
            var text = H.Number(body, "Label", 116, Color.white, TextAnchor.MiddleCenter, Navy, 6f, 6f);
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

        /// <summary>文字に、左へ赤紫・右へ水色の影（タイトルロゴと同じ色ずれ）を付ける。</summary>
        internal static Text Glitch(Text text, float split)
        {
            var a = text.gameObject.AddComponent<Shadow>();
            a.effectColor = new Color(1f, 0.16f, 0.78f, 0.85f);
            a.effectDistance = new Vector2(-split, 0f);
            var b = text.gameObject.AddComponent<Shadow>();
            b.effectColor = new Color(0f, 0.86f, 1f, 0.85f);
            b.effectDistance = new Vector2(split, 0f);
            return text;
        }

        /// <summary>文字に縁取りを付ける。</summary>
        internal static Text Outline(Text text, Color color, float width)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            return text;
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

                var border = BorderOf(file);
                // 設定ファイル（.meta）が最小限だと、Unity が「立方体のテクスチャ」として読み込み、画面で白い四角になる（2026-10-25 に起きた）。
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.textureShape == TextureImporterShape.Texture2D &&
                    !importer.mipmapEnabled && importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None &&
                    importer.spriteBorder == border)
                {
                    continue;
                }

                importer.textureShape = TextureImporterShape.Texture2D;
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
