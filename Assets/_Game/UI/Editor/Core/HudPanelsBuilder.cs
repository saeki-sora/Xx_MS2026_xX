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
    /// ・上の中央「CoreBar」: 歯＋バーの枠（1枚の絵 teethBar_frame、2026-11-11 から）＋ ピンク→水色のバー ＋ 歯の中に「100%」。コアの体力（core.hp01）。
    ///   （2026-10-17〜11-11 は、歯のアイコン・紺の枠・バーの中央の「100%」が別々の絵だった。歯だけ揺れていたが、今は歯が枠と1枚なので数字が弾む）
    /// ・左下「PlayerPanel」: 自分のキャラの絵・「1P」の札・しまのゲージ（自分のスキルのゲージ local.skillGauge。濃い赤のしま＝空、ピンクのしま＝溜まった分）。
    ///   （2026-10-31: 前は自分の熱 local.heat だったが、敵を倒して集めた光の粒のゲージに入れ替え。満タンでスキルが使える。ユーザー依頼）
    /// ・その上「OtherPlayers」: ほかの3人の丸いアイコン（other.1〜3。番号の小さい順）。灰色のアイコンの上に、つながっている人だけ番号（その人の色）とオーバーヒートの灯りを出す。
    ///   （2026-10-17: 最初は「アイコン＋熱のバー」の行だったが、バーが邪魔なのでアイコンだけの絵に差し替え。ユーザー依頼）
    /// UIの位置は全プレイヤー共通（ユーザー決定 2026-10-17。カメラの向きは P1〜P4 で違っても、HUD は同じ位置）。
    /// 前の「CoreStatus」（水晶のコアの札）・テンプレートの「Team」（左上の4人の熱）は消さずに隠す（ON にすれば戻る）。何度実行しても作り直すだけ。
    /// 絵は Tools/UIArt/HudSource/ の元画像を Tools/UIArt/make_hud_panel_art.py で切り分けた Assets/_Game/UI/Art/Hud/Hud_*.png
    /// （コアのバーの Hud_CoreBar*.png だけは Tools/UIArt/make_ingame_header_art.py で作る）。
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
        // 絵（576×118、見本と同じ大きさ）の上端。2026-11-11 からは上の中央にウェーブの帯（HudHeaderBuilder）があるので、その下（見本どおり）。前は 26。
        internal const float CoreTop = 104f;
        private static readonly Vector2 CoreSize = new Vector2(576f, 118f);
        private static readonly Color ToothNumber = new Color(0.33f, 0.19f, 0.65f); // 歯の中の数字（見本の紫）
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
            "Hud_CoreBarFrame.png", "Hud_CoreBarFill.png", "Hud_CoreBarTrail.png",
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
                    "ゲーム中HUD に入れました。\n\n・上の中央: コアの体力（歯のアイコン＋バー＋割合）\n・左下: 自分のパネル（しまのゲージ＝スキルのゲージ。敵を倒して集めた光の粒で溜まる）\n・その上: ほかの3人の丸いアイコン（つながっている人だけ番号が出ます）\n\n" +
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

            // 3割を切ったら、枠の後ろが赤くゆっくり脈打つ（強い点滅はしない）。出し入れは外枠、脈打つのは中の絵。
            var danger = B.Rect(core, "Danger");
            B.Stretch(danger);
            danger.gameObject.AddComponent<CanvasGroup>();
            var dangerVisible = danger.gameObject.AddComponent<UiBindVisible>();
            dangerVisible.key = FortressUiKeys.CoreHp01;
            dangerVisible.condition = UiVisibleCondition.WhenLess;
            dangerVisible.threshold = 0.3f;
            var glow = H.Picture(danger, "Glow", "Hud_CoreBarFrame.png", Danger);
            Px(glow.rectTransform, CoreSize, -10f, -8f, 596f, 134f);
            B.AddMotion(glow.gameObject, UiMotionTrigger.Loop, UiPreset.Breathe, 1.2f);

            // 歯の場所の目印（絵は枠と1枚。回復の演出 HudCoreHealFx が、この後ろに光を出す）。
            var tooth = B.Rect(core, "Tooth");
            Px(tooth, CoreSize, 4f, 4f, 96f, 108f);

            // 枠（歯＋バーの枠＋空の溝が1枚の絵）。バーは溝の上に重ねる。
            var frame = H.Picture(core, "Frame", "Hud_CoreBarFrame.png", Color.white);
            Px(frame.rectTransform, CoreSize, 0f, 0f, 576f, 118f);

            // バー（白い残像・グラデーションの中身。両端の丸い絵を、左から右へ塗る）。バーは揺らさず、右から減るだけ（ユーザー決定 2026-10-17）。
            var gauge = B.Rect(core, "Gauge");
            Px(gauge, CoreSize, 111f, 45f, 450f, 25f);
            var trail = H.Picture(gauge, "Trail", "Hud_CoreBarTrail.png", new Color(1f, 1f, 1f, 0.8f));
            H.FillHorizontal(trail);
            var fill = H.Picture(gauge, "Fill", "Hud_CoreBarFill.png", Color.white);
            H.FillHorizontal(fill);
            var bindFill = fill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.CoreHp01;
            bindFill.fill = fill;
            bindFill.trail = trail;
            bindFill.smoothSeconds = 0.12f;

            // 残りの割合（歯の中。見本どおり紫の数字、% だけ小さく）。減ったら弾む（前は歯が揺れていた）。
            var percent = B.Text(core, "Percent", "100%", 34, ToothNumber, TextAnchor.MiddleCenter, true);
            percent.horizontalOverflow = HorizontalWrapMode.Overflow;
            percent.supportRichText = true;
            Px(percent.rectTransform, CoreSize, 15f, 30f, 76f, 40f);
            H.Bind(percent, FortressUiKeys.CoreHp01, "{0:0'<size=18>'%'</size>'}", 0f);
            B.AddMotion(percent.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.25f, 0f, FortressUiKeys.CoreHp01);
        }

        /// <summary>コアのバーだけ作り直す（ほかの部品は触らない）。<see cref="HudHeaderBuilder"/> から呼ぶ。</summary>
        internal static void RebuildCore(Transform screen)
        {
            var old = screen.Find(CoreName);
            var index = old != null ? old.GetSiblingIndex() : -1;
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            BuildCore(screen);
            if (index >= 0)
            {
                screen.Find(CoreName).SetSiblingIndex(index);
            }
        }

        // ───────── 左下: 自分のパネル ─────────

        private static void BuildPanel(Transform screen)
        {
            var root = B.Rect(screen, PanelName);
            B.Place(root, Vector2.zero, PanelPos, PanelSize * PanelScale);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.45f);
            var body = H.Body(root, PanelSize, PanelScale);

            // しまのゲージ（枠の穴の後ろ）。スキルのゲージ: 濃い赤のしま＝空、ピンクのしま＝集めた光の粒（左から伸びる）。
            // 粒が砲台に吸い込まれるのに合わせて、少しゆっくり伸ばす。
            var gaugeBack = H.Picture(body, "GaugeBack", "Hud_PlayerGaugeBack.png", Color.white);
            Px(gaugeBack.rectTransform, PanelSize, 284f, 122f, 666f, 135f);
            var gaugeFill = H.Picture(body, "GaugeFill", "Hud_PlayerGaugeFill.png", Color.white);
            Px(gaugeFill.rectTransform, PanelSize, 284f, 122f, 666f, 135f);
            gaugeFill.type = Image.Type.Filled;
            gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var bindFill = gaugeFill.gameObject.AddComponent<UiBindFill>();
            bindFill.key = FortressUiKeys.LocalSkillGauge;
            bindFill.fill = gaugeFill;
            bindFill.smoothSeconds = 0.25f;

            // 枠・キャラの絵・「1P」の札（1枚の絵）。スキルが使えるようになった瞬間に弾む。
            var frame = H.Picture(body, "Frame", "Hud_PlayerFrame.png", Color.white);
            Px(frame.rectTransform, PanelSize, 0f, 0f, 1000f, 320f);
            B.AddMotion(body.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.35f, 0f, FortressUiKeys.LocalSkillReady);
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

        internal static void ConfigureArt()
        {
            foreach (var file in ArtFiles)
            {
                var path = $"{H.ArtFolder}/{file}";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[UIスタジオ] HUD の絵「{path}」がありません（Tools/UIArt/make_hud_panel_art.py・make_ingame_header_art.py で作れます）。");
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
