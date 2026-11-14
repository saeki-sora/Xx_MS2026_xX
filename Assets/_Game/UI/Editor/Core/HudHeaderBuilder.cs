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
    /// ゲーム中HUD（画面 Hud）に、チームの絵の「ウェーブの帯」と「撃破数の札（クリーン完了）」を入れる（2026-11-11、ユーザー依頼・見本画像どおりの配置）。
    /// ・上の中央「WaveHeader」: WAVE 1/3 ・ ばいきん＋進み具合の棒 ・ あと何体 ・ 時計＋残り時間。
    ///   コアのバー（歯）はその下に、新しい枠の絵（teethBar_frame）で作り直す（作るのは HudPanelsBuilder）。
    ///   前の「WavePlate」（コアのバーの左の箱）の代わり。あと5体からの「あと少し！」（帯の右）と、残り10秒からの赤い時間も引き継ぐ。
    /// ・右下「KillMeter」: 「クリーン完了」の札。左のカード＝全員の撃破数、右のカード＝自分の撃破数。前の「KillCounter」の代わり。コンボはその上へ。
    /// 数字の字体は前と同じ（Unity 標準の LegacyRuntime、太字）。
    /// 絵は Tools/UIArt/HudSource/ingame_header.png・ingame_killmeter.png を Tools/UIArt/make_ingame_header_art.py で
    /// Assets/_Game/UI/Art/Hud/Hud_WaveHeader.png・Hud_WaveBarFill.png・Hud_KillMeter.png にした物（棒の中身は動くように抜いてある）。
    /// 何度実行しても作り直すだけ。
    /// </summary>
    public static class HudHeaderBuilder
    {
        public const string MenuPath = "Tools/UIスタジオ/ウェーブの帯と撃破数の札（クリーン完了）をHUDに入れる";

        internal const string HeaderName = "WaveHeader";
        internal const string MeterName = "KillMeter";
        private const string OldPlateName = "WavePlate";
        private const string OldCounterName = "KillCounter";
        private const string ComboName = "KillCombo";

        // 見本（1920×1080）から測った置き場所。帯は絵と同じ大きさ、札は半分。
        private static readonly Vector2 HeaderSize = new Vector2(808f, 70f);
        private const float HeaderTop = 24f;
        private static readonly Vector2 MeterSize = new Vector2(620f, 220f);
        private const float MeterScale = 0.5f;
        private static readonly Vector2 MeterMargin = new Vector2(44f, 42f); // 右下の角からの隙間
        private const float ComboGap = 8f;

        private static readonly Color Outline = new Color(0.22f, 0.12f, 0.55f, 0.85f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.4f);

        private static readonly string[] ArtFiles = { "Hud_WaveHeader.png", "Hud_WaveBarFill.png", "Hud_KillMeter.png" };

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        /// <summary>HUD に帯と札を入れる（前の箱は消す・コアのバーを下げる）。できたら true。</summary>
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
            HudPanelsBuilder.ConfigureArt();
            UiStudioSetup.RegisterGameValues(UiStudioSetup.ValueCatalog);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.transform;
                HudWaveBuilder.Remove(screen, new[] { OldPlateName, OldCounterName });
                BuildHeader(screen);
                BuildMeter(screen);
                RebuildCoreBar(screen);
                PlaceCombo(screen);
                HudWaveExtrasBuilder.RefreshHiddenWhileBrushing(screen);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] 「{path}」にウェーブの帯（上の中央）と撃破数の札（右下）を入れました。前の {OldPlateName}・{OldCounterName} は消し、コアのバーは新しい枠で帯の下に作り直しました。");
            if (interactive)
            {
                EditorUtility.DisplayDialog("ウェーブの帯と撃破数の札",
                    "ゲーム中HUD に入れました。\n\n・上の中央: WAVE 1/3 ・ 進み具合の棒 ・ あと何体 ・ 残り時間（コアのバーはその下に新しい枠で）\n・右下: クリーン完了（左＝全員、右＝自分の撃破数）\n\n" +
                    "前の左上の箱と右下の撃破数は消しました。\nUIスタジオの「値」ページで「サンプル値で表示」をONにすると、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        // ───────── 上の中央: ウェーブの帯 ─────────

        /// <summary>帯を作る（同じ名前の前の帯は消す）。<see cref="HudWaveBuilder"/> からも呼ぶ。</summary>
        internal static void BuildHeader(Transform screen)
        {
            HudWaveBuilder.Remove(screen, new[] { HeaderName });
            var root = B.Rect(screen, HeaderName);
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -HeaderTop), HeaderSize);
            HudWaveBuilder.Visible(root, FortressUiKeys.WaveActive, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.45f);
            var body = H.Body(root, HeaderSize, 1f);

            var plate = H.Picture(body, "Plate", "Hud_WaveHeader.png", Color.white);
            B.Stretch(plate.rectTransform);

            // 進み具合の棒（絵の紺の溝の内側）。丸い端を保ったまま、横幅で伸びる。
            var bar = B.Rect(body, "Progress");
            Px(bar, HeaderSize, 230f, 30f, 297f, 15f);
            var fill = H.Picture(bar, "Fill", "Hud_WaveBarFill.png", Color.white);
            fill.type = Image.Type.Sliced;
            B.Stretch(fill.rectTransform);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.WaveProgress01;
            bindFill.fill = fill;
            bindFill.smoothSeconds = 0.12f;

            // WAVE 1/3（ウェーブが進んだらはねる）。
            var wave = Number(body, "Wave", TextAnchor.MiddleLeft);
            Px(wave.rectTransform, HeaderSize, 68f, 4f, 108f, 62f);
            H.Bind(wave, FortressUiKeys.WaveNumberText, "{0}", 0f);
            B.AddMotion(wave.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Tada, 0.6f, 0f, FortressUiKeys.WaveNumber);

            // 棒の右: 目標まであと何体（1体倒すごとにぽんと縮む）。
            var remaining = Number(body, "Remaining", TextAnchor.MiddleCenter);
            Px(remaining.rectTransform, HeaderSize, 536f, 4f, 100f, 62f);
            H.Bind(remaining, FortressUiKeys.WaveRemaining, "{0:00}", 0f);
            B.AddMotion(remaining.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.18f, 0f, FortressUiKeys.WaveRemaining);

            // 時計の右: 残り時間（残り10秒からは赤い数字が重なって脈打つ）。
            var time = Number(body, "Time", TextAnchor.MiddleCenter);
            Px(time.rectTransform, HeaderSize, 676f, 4f, 116f, 62f);
            H.Bind(time, FortressUiKeys.WaveTimeText, "{0}", 0f);
            var warn = B.Rect(body, "TimeWarning");
            B.Stretch(warn);
            HudWaveBuilder.Visible(warn, FortressUiKeys.WaveTimeWarning, UiVisibleCondition.WhenOn, 0.5f);
            var warnText = Number(warn, "Time", TextAnchor.MiddleCenter);
            warnText.color = Red;
            Px(warnText.rectTransform, HeaderSize, 676f, 4f, 116f, 62f);
            H.Bind(warnText, FortressUiKeys.WaveTimeText, "{0}", 0f);
            B.AddMotion(warnText.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 0.5f);

            // 帯の右: 「あと少し！」（あと5体から。下はコアのバーなので横に出す）。
            var near = B.Rect(root, "NearGoal");
            near.anchorMin = near.anchorMax = new Vector2(1f, 0.5f);
            near.pivot = new Vector2(0f, 0.5f);
            near.anchoredPosition = new Vector2(10f, 0f);
            near.sizeDelta = new Vector2(220f, 50f);
            HudWaveBuilder.Visible(near, FortressUiKeys.WaveNearGoal, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(near.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            var nearText = H.Number(near, "Label", 34, H.Pink, TextAnchor.MiddleLeft, H.Navy, 3f, 0f);
            nearText.text = "あと少し！";
            B.Stretch(nearText.rectTransform);
            B.AddMotion(nearText.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.8f);
        }

        // ───────── 右下: 撃破数の札（クリーン完了） ─────────

        /// <summary>札を作る（同じ名前の前の札は消す）。<see cref="HudKillCounterBuilder"/> からも呼ぶ。</summary>
        internal static void BuildMeter(Transform screen)
        {
            HudWaveBuilder.Remove(screen, new[] { MeterName });
            var root = B.Rect(screen, MeterName);
            B.Place(root, new Vector2(1f, 0f), new Vector2(-MeterMargin.x, MeterMargin.y), MeterSize * MeterScale);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.45f);
            var body = H.Body(root, MeterSize, MeterScale);

            var plate = H.Picture(body, "Plate", "Hud_KillMeter.png", Color.white);
            B.Stretch(plate.rectTransform);

            // 左のカード（みんなのアイコン）: 全員の撃破数。弾む動きは外枠、数字は中の文字（数え上げとぶつからないように）。
            var teamPop = B.Rect(body, "TeamPop");
            Px(teamPop, MeterSize, 140f, 60f, 156f, 120f);
            B.AddMotion(teamPop.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.22f, 0f, FortressUiKeys.KillsTotal);
            var team = CardNumber(teamPop, "Team");
            H.Bind(team, FortressUiKeys.KillsTotal, "{0:N0}", 0.25f);

            // 右のカード（ひとりのアイコン）: 自分の撃破数。
            var youPop = B.Rect(body, "YouPop");
            Px(youPop, MeterSize, 396f, 60f, 184f, 120f);
            B.AddMotion(youPop.gameObject, UiMotionTrigger.OnValueUp, UiPreset.Jelly, 0.3f, 0f, FortressUiKeys.KillsLocal);
            var you = CardNumber(youPop, "You");
            H.Bind(you, FortressUiKeys.KillsLocal, "{0:N0}", 0.2f);
        }

        // ───────── 周りの部品の位置 ─────────

        // コアのバー（歯）を新しい枠の絵で作り直し、帯の下へ置く。下にあるコーティングの札は、バーの下端が動いた分だけ動かす。
        private static void RebuildCoreBar(Transform screen)
        {
            if (!(screen.Find(HudPanelsBuilder.CoreName) is RectTransform old))
            {
                return;
            }

            var oldBottom = old.anchoredPosition.y - old.sizeDelta.y;
            HudPanelsBuilder.RebuildCore(screen);
            var core = (RectTransform)screen.Find(HudPanelsBuilder.CoreName);
            var delta = core.anchoredPosition.y - core.sizeDelta.y - oldBottom;
            if (screen.Find(HudWaveExtrasBuilder.CoatingName) is RectTransform coating)
            {
                coating.anchoredPosition += new Vector2(0f, delta);
            }
        }

        /// <summary>コンボ（x25 COMBO!）を札のすぐ上に置く。<see cref="HudKillCounterBuilder"/> からも呼ぶ。</summary>
        internal static void PlaceCombo(Transform screen)
        {
            if (!(screen.Find(ComboName) is RectTransform combo))
            {
                return;
            }

            // 札と同じく右下を基準にする（画面の縦横比が変わっても札の真上に来る）。
            combo.anchorMin = combo.anchorMax = new Vector2(1f, 0f);
            combo.pivot = new Vector2(0.5f, 0f);
            combo.anchoredPosition = new Vector2(-(MeterMargin.x + MeterSize.x * MeterScale * 0.5f), MeterMargin.y + MeterSize.y * MeterScale + ComboGap);
        }

        // ───────── 部品 ─────────

        // 白い太字の数字（字体は前の HUD と同じ）。見本どおり、斜体にせず細い紫の縁だけ。
        private static Text Number(Transform parent, string name, TextAnchor anchor)
        {
            var text = B.Text(parent, name, "0", 36, Color.white, anchor, true);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var line = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            line.effectColor = Outline;
            line.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        // カードの数字。桁が増えたらカードからはみ出さないように自動で小さくする。
        private static Text CardNumber(RectTransform parent, string name)
        {
            var text = B.Text(parent, name, "0", 68, Color.white, TextAnchor.MiddleCenter, true);
            B.Stretch(text.rectTransform);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 30;
            text.resizeTextMaxSize = 68;
            var line = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            line.effectColor = Outline;
            line.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        /// <summary>絵の左上を原点にした px（x, y, 幅, 高さ）で、親（大きさ parentSize）の中央基準に置く。</summary>
        private static void Px(RectTransform rect, Vector2 parentSize, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * 0.5f - parentSize.x * 0.5f, parentSize.y * 0.5f - (y + h * 0.5f));
        }

        internal static void ConfigureArt()
        {
            foreach (var file in ArtFiles)
            {
                var path = $"{H.ArtFolder}/{file}";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[UIスタジオ] HUD の絵「{path}」がありません（Tools/UIArt/make_ingame_header_art.py で作れます）。");
                    continue;
                }

                var border = file == "Hud_WaveBarFill.png" ? new Vector4(8f, 0f, 8f, 0f) : Vector4.zero;
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.textureShape == TextureImporterShape.Texture2D && !importer.mipmapEnabled && importer.alphaIsTransparency &&
                    importer.npotScale == TextureImporterNPOTScale.None && importer.spriteBorder == border)
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
                EditorUtility.DisplayDialog("ウェーブの帯と撃破数の札", message, "OK");
            }
            else
            {
                Debug.LogWarning("[UIスタジオ] " + message);
            }
        }
    }
}
