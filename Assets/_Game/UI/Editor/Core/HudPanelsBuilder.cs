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
    /// ゲーム中HUD（画面 Hud）に、チームの絵を使った3つの部品を入れる（2026-10-17、ユーザーの見本画像どおりの配置）。
    /// ・上の中央「CoreBar」: 歯のアイコン ＋ 紺の枠のバー（ピンク→水色）＋ 中央に「100%」。コアの体力（core.hp01）。
    /// ・左下「PlayerPanel」: 自分のキャラの絵・「1P」の札・しまのゲージ（自分の熱 local.heat。濃い赤のしま＝空、ピンクのしま＝溜まった分）。
    /// ・その上「OtherPlayers」: ほかの3人の丸いアイコン（other.1〜3。番号の小さい順）。灰色のアイコンの上に、つながっている人だけ番号（その人の色）とオーバーヒートの灯りを出す。
    ///   （2026-10-17: 最初は「アイコン＋熱のバー」の行だったが、バーが邪魔なのでアイコンだけの絵に差し替え。ユーザー依頼）
    /// UIの位置は全プレイヤー共通（ユーザー決定 2026-10-17。カメラの向きは P1〜P4 で違っても、HUD は同じ位置）。
    /// 前の「CoreStatus」（水晶のコアの札）・テンプレートの「Team」（左上の4人の熱）は消さずに隠す（ON にすれば戻る）。何度実行しても作り直すだけ。
    /// 絵は Tools/UIArt/HudSource/ の元画像を Tools/UIArt/make_hud_panel_art.py で切り分けた Assets/_Game/UI/Art/Hud/Hud_*.png。
    /// </summary>
    public static class HudPanelsBuilder
    {
        public const string MenuPath = "Tools/UIスタジオ/プレイヤーのパネルとコアのバー（歯）をHUDに入れる";

        public const string CoreName = "CoreBar";
        private const string PanelName = "PlayerPanel";
        private const string OthersName = "OtherPlayers";
        private const string OldCoreName = "CoreStatus";
        private const string TemplateCoreName = "CoreHp";
        private const string TeamName = "Team";
        private const string HeatName = "LocalHeat";
        private const string DotPath = "Assets/_Game/UI/Art/Session/Session_Dot.png";

        // 見本（1920×1080）から測った置き場所。
        private const float CoreTop = 26f;                                 // 絵（640×163）の上端
        private static readonly Vector2 CoreSize = new Vector2(640f, 163f);
        private static readonly Vector2 PanelPos = new Vector2(39f, 25f);  // 左下からの位置
        private static readonly Vector2 PanelSize = new Vector2(1000f, 320f);
        private const float PanelScale = 0.615f;
        private const float OthersLeft = 44f;
        private const float OthersFirstBottom = 408f;                      // 1段目の下端（画面の下から）
        private const float OthersPitch = 90f;
        private static readonly Vector2 RowSize = new Vector2(89f, 81f);   // アイコンの絵の大きさ

        private static readonly Color Navy = new Color(0.05f, 0.06f, 0.4f);
        private static readonly Color Danger = new Color(1f, 0.3f, 0.45f, 0.6f);

        private static readonly string[] ArtFiles =
        {
            "Hud_CoreTooth.png", "Hud_CoreFrame.png", "Hud_CoreFill.png",
            "Hud_PlayerFrame.png", "Hud_PlayerGaugeBack.png", "Hud_PlayerGaugeFill.png", "Hud_OtherIcon.png"
        };

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build(true);
        }

        /// <summary>HUD にプレイヤーのパネルとコアのバーを入れる。できたら true。</summary>
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
                foreach (var old in new[] { CoreName, PanelName, OthersName })
                {
                    var found = screen.Find(old);
                    if (found != null)
                    {
                        Object.DestroyImmediate(found.gameObject);
                    }
                }

                // 前の部品は消さずに隠す（ON にすれば戻る）。
                foreach (var hide in new[] { OldCoreName, TemplateCoreName, TeamName, HeatName })
                {
                    var found = screen.Find(hide);
                    if (found != null)
                    {
                        found.gameObject.SetActive(false);
                    }
                }

                BuildCore(screen);
                BuildOthers(screen);
                BuildPanel(screen);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UIスタジオ] 「{path}」にプレイヤーのパネルとコアのバー（歯）を入れました（前のコアの札 {OldCoreName} と左上の {TeamName} は隠しました。ON にすれば戻ります）。");
            if (interactive)
            {
                EditorUtility.DisplayDialog("プレイヤーのパネルとコアのバー",
                    "ゲーム中HUD に入れました。\n\n・上の中央: コアの体力（歯のアイコン＋バー＋割合）\n・左下: 自分のパネル（しまのゲージ＝自分の熱）\n・その上: ほかの3人の丸いアイコン（つながっている人だけ番号が出ます）\n\n" +
                    "前のコアの札と左上の4人の熱は隠しました（消してはいません）。\nUIスタジオの「値」ページで「サンプル値で表示」をONにすると、再生しなくても見た目を確かめられます。", "OK");
            }

            return true;
        }

        // ───────── 上の中央: コアの体力（歯＋バー） ─────────

        private static void BuildCore(Transform screen)
        {
            var root = B.Rect(screen, CoreName);
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -CoreTop), CoreSize);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInTop, 0.45f);
            var core = H.Body(root, CoreSize, 1f); // 大きさは Body の Scale で変える（外側の出る動きとぶつからないように）

            // 3割を切ったら、バーの後ろが赤くゆっくり脈打つ（強い点滅はしない）。出し入れは外枠、脈打つのは中の絵。
            var danger = B.Rect(core, "Danger");
            B.Stretch(danger);
            danger.gameObject.AddComponent<CanvasGroup>();
            var dangerVisible = danger.gameObject.AddComponent<UiBindVisible>();
            dangerVisible.key = FortressUiKeys.CoreHp01;
            dangerVisible.condition = UiVisibleCondition.WhenLess;
            dangerVisible.threshold = 0.3f;
            var glow = H.Picture(danger, "Glow", "Hud_CoreFrame.png", Danger);
            Px(glow.rectTransform, CoreSize, 152f, 38f, 498f, 78f);
            B.AddMotion(glow.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.2f);

            var tooth = H.Picture(core, "Tooth", "Hud_CoreTooth.png", Color.white);
            Px(tooth.rectTransform, CoreSize, 0f, 0f, 160f, 163f);
            B.AddMotion(tooth.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 2.2f);
            B.AddMotion(tooth.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchRotation, 0.3f, 0f, FortressUiKeys.CoreHp01);

            // バー（減った所の紺・白い残像・グラデーションの中身）。バーは揺らさず、右から減るだけ（ユーザー決定 2026-10-17。揺れるのは歯だけ）。
            var gauge = B.Rect(core, "Gauge");
            Px(gauge, CoreSize, 172f, 60f, 459f, 35f);
            var back = B.Rect(gauge, "Back").gameObject.AddComponent<Image>();
            B.Stretch(back.rectTransform);
            back.color = new Color(Navy.r, Navy.g, Navy.b, 0.85f);
            back.raycastTarget = false;
            var trail = B.Rect(gauge, "Trail").gameObject.AddComponent<Image>();
            B.Stretch(trail.rectTransform);
            trail.color = new Color(1f, 1f, 1f, 0.8f);
            trail.raycastTarget = false;
            var fill = H.Picture(gauge, "Fill", "Hud_CoreFill.png", Color.white);
            H.FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.CoreHp01;
            bindFill.fill = fill;
            bindFill.trail = trail;
            bindFill.smoothSeconds = 0.12f;

            var frame = H.Picture(core, "Frame", "Hud_CoreFrame.png", Color.white);
            Px(frame.rectTransform, CoreSize, 162f, 48f, 478f, 58f);

            // 残りの割合（「100%」の % だけ小さく。見本どおり）。
            var percent = B.Text(core, "Percent", "100%", 50, Color.white, TextAnchor.MiddleCenter, true);
            percent.horizontalOverflow = HorizontalWrapMode.Overflow;
            percent.supportRichText = true;
            Px(percent.rectTransform, CoreSize, 172f, 44f, 459f, 66f);
            var outline = percent.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.22f, 0.62f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);
            H.Bind(percent, FortressUiKeys.CoreHp01, "{0:0'<size=34>'%'</size>'}", 0f);
        }

        // ───────── 左下: 自分のパネル ─────────

        private static void BuildPanel(Transform screen)
        {
            var root = B.Rect(screen, PanelName);
            B.Place(root, Vector2.zero, PanelPos, PanelSize * PanelScale);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.45f);
            var body = H.Body(root, PanelSize, PanelScale);

            // しまのゲージ（枠の穴の後ろ）。濃い赤のしま＝空、ピンクのしま＝溜まった分（左から伸びる）。
            var gaugeBack = H.Picture(body, "GaugeBack", "Hud_PlayerGaugeBack.png", Color.white);
            Px(gaugeBack.rectTransform, PanelSize, 284f, 122f, 666f, 135f);
            var gaugeFill = H.Picture(body, "GaugeFill", "Hud_PlayerGaugeFill.png", Color.white);
            Px(gaugeFill.rectTransform, PanelSize, 284f, 122f, 666f, 135f);
            gaugeFill.type = Image.Type.Filled;
            gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var bindFill = gaugeFill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.LocalHeat;
            bindFill.fill = gaugeFill;
            bindFill.smoothSeconds = 0.08f;

            // 枠・キャラの絵・「1P」の札（1枚の絵）。オーバーヒートした瞬間に揺れる。
            var frame = H.Picture(body, "Frame", "Hud_PlayerFrame.png", Color.white);
            Px(frame.rectTransform, PanelSize, 0f, 0f, 1000f, 320f);
            B.AddMotion(body.gameObject, UiMotionTrigger.OnValueUp, UiPreset.ShakeHard, 0.35f, 0f, FortressUiKeys.LocalOverheated);
        }

        // ───────── その上: ほかの3人 ─────────

        private static void BuildOthers(Transform screen)
        {
            var root = B.Rect(screen, OthersName);
            B.Stretch(root);
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(DotPath);
            for (var s = 0; s < FortressUiKeys.OtherCount; s++)
            {
                var row = B.Rect(root, $"Other{s + 1}");
                B.Place(row, Vector2.zero, new Vector2(OthersLeft, OthersFirstBottom - s * OthersPitch), RowSize);
                B.AddMotion(row.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.4f, 0.08f * (s + 1));
                var body = H.Body(row, RowSize, 1f);

                var back = H.Picture(body, "Back", "Hud_OtherIcon.png", Color.white);
                B.Stretch(back.rectTransform);

                // つながっている人だけ、番号・オーバーヒートの灯りを出す（いない人は灰色のアイコンのまま）。
                var live = B.Rect(body, "Live");
                B.Stretch(live);
                live.gameObject.AddComponent<CanvasGroup>();
                var liveVisible = live.gameObject.AddComponent<UiBindVisible>();
                liveVisible.key = FortressUiKeys.OtherConnected(s);
                liveVisible.condition = UiVisibleCondition.WhenOn;

                var label = B.Text(live, "Label", $"{s + 2}P", 30, Color.white, TextAnchor.MiddleCenter, true);
                label.fontStyle = FontStyle.BoldAndItalic;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Px(label.rectTransform, RowSize, 2f, 4f, 76f, 72f);
                var labelLine = label.gameObject.AddComponent<Outline>();
                labelLine.effectColor = Navy;
                labelLine.effectDistance = new Vector2(2f, -2f);
                var labelColor = label.gameObject.AddComponent<UiBindColor>();
                labelColor.key = FortressUiKeys.OtherColor(s);
                labelColor.target = label;
                H.Bind(label, FortressUiKeys.OtherLabel(s), "{0}", 0f);

                // アイコンの右上: オーバーヒート中だけ赤く光る。
                var lamp = B.Rect(live, "Overheat");
                Px(lamp, RowSize, 60f, 0f, 22f, 22f);
                lamp.gameObject.AddComponent<CanvasGroup>();
                var lampVisible = lamp.gameObject.AddComponent<UiBindVisible>();
                lampVisible.key = FortressUiKeys.OtherOverheated(s);
                lampVisible.condition = UiVisibleCondition.WhenOn;
                var light = B.Rect(lamp, "Light").gameObject.AddComponent<Image>();
                B.Stretch(light.rectTransform);
                light.sprite = dot;
                light.color = new Color(1f, 0.3f, 0.4f);
                light.raycastTarget = false;
                B.AddMotion(light.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 0.6f);
            }
        }

        // ───────── 部品 ─────────

        /// <summary>絵の左上を原点にした px（x, y, 幅, 高さ）で、親（大きさ parentSize）の中央基準に置く。</summary>
        private static void Px(RectTransform rect, Vector2 parentSize, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * 0.5f - parentSize.x * 0.5f, parentSize.y * 0.5f - (y + h * 0.5f));
        }

        private static void ConfigureArt()
        {
            foreach (var file in ArtFiles)
            {
                var path = $"{H.ArtFolder}/{file}";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[UIスタジオ] HUD の絵「{path}」がありません（Tools/UIArt/make_hud_panel_art.py で作れます）。");
                    continue;
                }

                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
                    !importer.mipmapEnabled && importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        private static void Notify(bool interactive, string message)
        {
            if (interactive)
            {
                EditorUtility.DisplayDialog("プレイヤーのパネルとコアのバー", message, "OK");
            }
            else
            {
                Debug.LogWarning("[UIスタジオ] " + message);
            }
        }
    }
}
