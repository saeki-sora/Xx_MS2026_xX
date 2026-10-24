using DDrive.Runtime.Ui;
using MS2026.Fortress;
using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using B = MS2026.UI.EditorTools.UiTemplateBuilder;
using H = MS2026.UI.EditorTools.HudKillCounterBuilder;
using W = MS2026.UI.EditorTools.HudWaveBuilder;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// ウェーブの表示の続き（画面の外から来る敵の矢印・コーティング・歯みがきタイム・初めての人向けのヒント）を HUD に入れる。<see cref="HudWaveBuilder"/> から呼ぶ。
    /// </summary>
    internal static class HudWaveExtrasBuilder
    {
        internal const string EdgeName = "WaveEdgeMarkers";
        internal const string BrushName = "BrushTime";
        internal const string CoatingName = "WaveCoatingBadge";
        internal const string NewEnemiesName = "WaveNewEnemies";
        internal const string YouName = "WaveYouMarker";
        internal const string HintName = "WaveFirstHint";

        /// <summary>このビルダーが作る部品の名前（作り直すときに消す）。</summary>
        internal static readonly string[] OwnedNames = { EdgeName, BrushName, CoatingName, NewEnemiesName, YouName, HintName };

        private static readonly Color Navy = new Color(0.06f, 0.08f, 0.2f);
        private static readonly Color StainColor = new Color(0.78f, 0.62f, 0.24f, 0.92f);

        public static void Build(Transform screen)
        {
            W.Remove(screen, OwnedNames);
            BuildEdgeMarkers(screen);
            BuildYouMarker(screen);
            BuildCoatingBadge(screen);
            BuildFirstHint(screen);
            BuildNewEnemies(screen);
            BuildBrushTime(screen);
        }

        // ───────── 初めての人向け ─────────

        // 試合の最初だけ、自分の砲台の上に「YOU」の矢印（場所は各PCのカメラで計算）。
        private static void BuildYouMarker(Transform screen)
        {
            var root = B.Rect(screen, YouName);
            B.Stretch(root);
            var marker = B.Rect(root, "Marker");
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(160f, 150f);
            var arrow = H.Picture(marker, "Arrow", "Wave_Arrow.png", Color.white);
            H.Center(arrow.rectTransform, new Vector2(0f, -30f), new Vector2(80f, 80f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f); // 下向き（砲台を指す）
            var label = H.Number(marker, "Label", 52, Color.white, TextAnchor.MiddleCenter, Navy, 4f, 0f);
            label.text = "YOU";
            H.Center(label.rectTransform, new Vector2(0f, 40f), new Vector2(200f, 64f));
            var you = root.gameObject.AddComponent<WaveYouMarker>();
            you.marker = marker;
            you.tinted = new Graphic[] { arrow, label };
            marker.gameObject.SetActive(false);
        }

        // 試合の最初だけ、画面の下に握り方のヒント。
        private static void BuildFirstHint(Transform screen)
        {
            var root = B.Rect(screen, HintName);
            B.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 120f));
            W.Visible(root, FortressUiKeys.WaveFirstHint, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.4f);
            var back = B.Rect(root, "Back").gameObject.AddComponent<Image>();
            B.Stretch(back.rectTransform);
            back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Bar.png");
            back.type = Image.Type.Sliced;
            back.color = new Color(Navy.r, Navy.g, Navy.b, 0.82f);
            back.raycastTarget = false;
            var hand = B.Rect(root, "Hand").gameObject.AddComponent<RawImage>();
            hand.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{H.ArtFolder}/Wave_Hand.png");
            hand.uvRect = new Rect(0.5f, 0f, 0.5f, 1f); // 握った手
            hand.raycastTarget = false;
            H.Center(hand.rectTransform, new Vector2(-470f, 0f), new Vector2(110f, 110f));
            B.AddMotion(hand.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.8f);
            var line1 = H.Label(root, "Line1", "握ると ビーム！ 強く握るほど 太くなる", 40, Color.white, TextAnchor.MiddleLeft);
            H.Center(line1.rectTransform, new Vector2(-400f, 22f), new Vector2(900f, 54f), new Vector2(0f, 0.5f));
            var line2 = H.Label(root, "Line2", "握りっぱなしは 熱くなって しばらく撃てない（オーバーヒート）", 28, H.Cyan, TextAnchor.MiddleLeft);
            H.Center(line2.rectTransform, new Vector2(-400f, -26f), new Vector2(900f, 40f), new Vector2(0f, 0.5f));
        }

        // 発表のとき、初めて出てくる敵のカード（最大3枚、画面の右）。中身は WaveNewEnemyCards が入れる。
        private static void BuildNewEnemies(Transform screen)
        {
            var root = B.Rect(screen, NewEnemiesName);
            B.Place(root, new Vector2(1f, 0.5f), new Vector2(-40f, -40f), new Vector2(420f, 520f));
            W.Visible(root, FortressUiKeys.WaveAnnouncing, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.4f, 0.3f);
            var cards = new WaveNewEnemyCards.Card[3];
            for (var i = 0; i < cards.Length; i++)
            {
                var card = B.Rect(root, $"Card{i + 1}");
                H.Center(card, new Vector2(0f, 170f - 170f * i), new Vector2(420f, 156f));
                var back = card.gameObject.AddComponent<Image>();
                back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Bar.png");
                back.type = Image.Type.Sliced;
                back.color = new Color(Navy.r, Navy.g, Navy.b, 0.88f);
                back.raycastTarget = false;
                var tag = H.Number(card, "Tag", 26, new Color(1f, 0.86f, 0.4f), TextAnchor.MiddleLeft, Navy, 2f, 0f);
                tag.text = "NEW ENEMY!";
                H.Center(tag.rectTransform, new Vector2(-60f, 54f), new Vector2(220f, 36f), new Vector2(0f, 0.5f));
                var fallback = H.Picture(card, "Dot", "Wave_Ring.png", Color.white);
                H.Center(fallback.rectTransform, new Vector2(-145f, -6f), new Vector2(96f, 96f));
                var picture = B.Rect(card, "Picture").gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
                H.Center(picture.rectTransform, new Vector2(-145f, -6f), new Vector2(116f, 116f));
                B.AddMotion(picture.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 1.6f);
                var title = H.Label(card, "Name", "", 34, Color.white, TextAnchor.MiddleLeft);
                H.Center(title.rectTransform, new Vector2(-75f, 14f), new Vector2(280f, 44f), new Vector2(0f, 0.5f));
                var body = B.Text(card, "Text", "", 24, new Color(0.85f, 0.95f, 1f), TextAnchor.UpperLeft);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
                H.Center(body.rectTransform, new Vector2(-75f, -40f), new Vector2(280f, 64f), new Vector2(0f, 0.5f));
                cards[i] = new WaveNewEnemyCards.Card { root = card.gameObject, picture = picture, fallback = fallback, title = title, body = body };
                card.gameObject.SetActive(false);
            }

            root.gameObject.AddComponent<WaveNewEnemyCards>().cards = cards;
        }

        // 画面の外から来る敵の矢印（赤い線と同じ間だけ、画面のふちに出る）。
        private static void BuildEdgeMarkers(Transform screen)
        {
            var root = B.Rect(screen, EdgeName);
            B.Stretch(root);
            var markers = root.gameObject.AddComponent<WaveEdgeMarkers>();
            markers.arrow = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Arrow.png");
            root.SetAsFirstSibling(); // ほかの表示より奥
        }

        // ───────── コーティング（コアのバーの下） ─────────

        private static void BuildCoatingBadge(Transform screen)
        {
            var root = B.Rect(screen, CoatingName);
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(60f, -168f), new Vector2(360f, 48f));
            W.Visible(root, FortressUiKeys.CoreHasCoating, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnValueDown, UiPreset.Shake, 0.25f, 0f, FortressUiKeys.CoreCoating);
            var back = H.Picture(root, "Back", "Wave_Bar.png", new Color(0.43f, 0.93f, 1f, 0.85f));
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var label = H.Number(root, "Label", 30, Navy, TextAnchor.MiddleCenter, Color.white, 2f, 0f);
            B.Stretch(label.rectTransform);
            H.Bind(label, FortressUiKeys.CoreCoating, "コーティング {0:0}", 0f);
        }

        // ───────── 歯みがきタイム ─────────

        private static void BuildBrushTime(Transform screen)
        {
            var root = B.Rect(screen, BrushName);
            B.Stretch(root);
            W.Visible(root, FortressUiKeys.BrushActive, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.FadeIn, 0.3f);

            var dim = B.Rect(root, "Dim").gameObject.AddComponent<Image>();
            B.Stretch(dim.rectTransform);
            dim.color = new Color(Navy.r, Navy.g, Navy.b, 0.45f);
            dim.raycastTarget = false;

            var title = H.Number(root, "Title", 96, H.Pink, TextAnchor.MiddleCenter, Navy, 5f, 4f);
            title.text = "歯みがきタイム！";
            H.Center(title.rectTransform, new Vector2(0f, 390f), new Vector2(1400f, 130f));
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);

            var sub = H.Label(root, "Sub", "みんなで にぎにぎ して 歯をみがこう！", 40, Color.white, TextAnchor.MiddleCenter);
            H.Center(sub.rectTransform, new Vector2(0f, 300f), new Vector2(1400f, 60f));
            Outline(sub, 3f);

            BuildTooth(root, out var stainGroup, out var shine, out var bubbles, out var brushes);
            var hand = BuildHandAndTimer(root);
            BuildMeters(root);
            BuildSyncAndResult(root);

            var view = root.gameObject.AddComponent<BrushTimeView>();
            view.stains = stainGroup;
            view.hand = hand;
            view.brushes = brushes;
            view.shine = shine;
            view.bubble = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Ring.png");
            view.bubbleArea = bubbles;
        }

        // 歯（コア）と汚れ・光・歯ブラシ・泡。
        private static void BuildTooth(RectTransform root, out CanvasGroup stainGroup, out Image shine, out RectTransform bubbles, out RectTransform[] brushes)
        {
            var toothArea = B.Rect(root, "Tooth");
            H.Center(toothArea, new Vector2(0f, 40f), new Vector2(380f, 388f));
            var tooth = H.Picture(toothArea, "Tooth", "Hud_CoreTooth.png", Color.white);
            B.Stretch(tooth.rectTransform);

            var stains = B.Rect(toothArea, "Stains");
            B.Stretch(stains);
            stainGroup = stains.gameObject.AddComponent<CanvasGroup>();
            var spots = new[]
            {
                new Vector2(-80f, 60f), new Vector2(60f, 90f), new Vector2(-30f, -20f),
                new Vector2(90f, -40f), new Vector2(-95f, -95f), new Vector2(20f, 130f)
            };
            for (var i = 0; i < spots.Length; i++)
            {
                var stain = H.Picture(stains, $"Stain{i + 1}", "Wave_Stain.png", StainColor);
                H.Center(stain.rectTransform, spots[i], Vector2.one * (90f + 18f * (i % 3)));
            }

            shine = H.Picture(toothArea, "Shine", "Wave_Ring.png", new Color(1f, 1f, 1f, 0f));
            H.Center(shine.rectTransform, Vector2.zero, new Vector2(460f, 460f));
            bubbles = B.Rect(toothArea, "Bubbles");
            B.Stretch(bubbles);

            brushes = new RectTransform[4];
            var places = new[] { new Vector2(-250f, 120f), new Vector2(250f, 120f), new Vector2(-250f, -60f), new Vector2(250f, -60f) };
            for (var p = 0; p < 4; p++)
            {
                var holder = B.Rect(toothArea, $"Brush{p + 1}");
                H.Center(holder, places[p], new Vector2(256f, 96f));
                W.Visible(holder, FortressUiKeys.BrushIn(p), UiVisibleCondition.WhenOn, 0.5f);
                var brush = H.Picture(holder, "Brush", "Wave_Brush.png", FortressColors.PlayerColor(p));
                B.Stretch(brush.rectTransform);
                if (p % 2 == 0)
                {
                    brush.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // 左の歯ブラシは毛を右（歯の方）へ
                }

                brushes[p] = brush.rectTransform;
            }
        }

        // 手のアニメ（左）と残り時間（右）。手の絵は「開いた手｜閉じた手」の2コマ。
        private static RawImage BuildHandAndTimer(RectTransform root)
        {
            var handRoot = B.Rect(root, "Hand");
            H.Center(handRoot, new Vector2(-520f, 40f), new Vector2(300f, 330f));
            W.Visible(handRoot, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOff, 0.5f);
            var hand = B.Rect(handRoot, "Picture").gameObject.AddComponent<RawImage>();
            hand.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{H.ArtFolder}/Wave_Hand.png");
            hand.uvRect = new Rect(0f, 0f, 0.5f, 1f);
            hand.raycastTarget = false;
            H.Center(hand.rectTransform, new Vector2(0f, 30f), new Vector2(256f, 256f));
            var handLabel = H.Number(handRoot, "Label", 44, Color.white, TextAnchor.MiddleCenter, Navy, 3f, 0f);
            handLabel.text = "にぎにぎ！";
            H.Center(handLabel.rectTransform, new Vector2(0f, -135f), new Vector2(300f, 60f));
            B.AddMotion(handLabel.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.6f);

            var timer = B.Rect(root, "Timer");
            H.Center(timer, new Vector2(520f, 40f), new Vector2(300f, 260f));
            W.Visible(timer, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOff, 0.5f);
            var caption = H.Label(timer, "Caption", "のこり", 34, H.Cyan, TextAnchor.MiddleCenter);
            H.Center(caption.rectTransform, new Vector2(0f, 85f), new Vector2(300f, 50f));
            var number = H.Number(timer, "Number", 140, Color.white, TextAnchor.MiddleCenter, Navy, 5f, 4f);
            H.Center(number.rectTransform, new Vector2(0f, -15f), new Vector2(300f, 170f));
            H.Bind(number, FortressUiKeys.BrushTimeLeft, "{0:0}", 0f);
            B.AddMotion(number.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.2f, 0f, FortressUiKeys.BrushTimeLeft);
            return hand;
        }

        // きれい度（全員）と、P1〜P4 の頑張り（いる人だけ）。
        private static void BuildMeters(RectTransform root)
        {
            var meter = B.Rect(root, "Meter");
            H.Center(meter, new Vector2(0f, -225f), new Vector2(900f, 90f));
            var label = H.Number(meter, "Label", 38, Color.white, TextAnchor.MiddleCenter, Navy, 3f, 0f);
            H.Center(label.rectTransform, new Vector2(0f, 26f), new Vector2(900f, 50f));
            H.Bind(label, FortressUiKeys.BrushClean01, "きれい度 {0:0%}", 0f);
            Bar(meter, "Bar", new Vector2(0f, -20f), new Vector2(880f, 34f), H.Cyan, FortressUiKeys.BrushClean01, 0.15f);

            var players = B.Rect(root, "Players");
            H.Center(players, new Vector2(0f, -310f), new Vector2(900f, 60f));
            for (var p = 0; p < 4; p++)
            {
                var column = B.Rect(players, $"P{p + 1}");
                H.Center(column, new Vector2(-337.5f + 225f * p, 0f), new Vector2(210f, 60f));
                W.Visible(column, FortressUiKeys.BrushIn(p), UiVisibleCondition.WhenOn, 0.5f);
                var tag = H.Label(column, "Tag", $"{p + 1}P", 28, FortressColors.PlayerColor(p), TextAnchor.MiddleLeft);
                H.Center(tag.rectTransform, new Vector2(-105f, 0f), new Vector2(60f, 40f), new Vector2(0f, 0.5f));
                Outline(tag, 2f);
                Bar(column, "Bar", new Vector2(30f, 0f), new Vector2(140f, 14f), FortressColors.PlayerColor(p), FortressUiKeys.BrushShare(p), 0.1f);
            }
        }

        // シンクロ！（全員がそろって握った）と、結果（HP +12　コーティング +5）。
        private static void BuildSyncAndResult(RectTransform root)
        {
            var sync = B.Rect(root, "Sync");
            H.Center(sync, new Vector2(0f, 220f), new Vector2(700f, 90f));
            W.Visible(sync, FortressUiKeys.BrushSyncShowing, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(sync.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.2f);
            var syncText = H.Number(sync, "Label", 64, new Color(1f, 0.86f, 0.4f), TextAnchor.MiddleCenter, Navy, 4f, 4f);
            syncText.text = "シンクロ！ ボーナス！";
            B.Stretch(syncText.rectTransform);

            var result = B.Rect(root, "Result");
            H.Center(result, new Vector2(0f, 40f), new Vector2(1100f, 210f));
            W.Visible(result, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(result.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.45f);
            var band = H.Picture(result, "Band", "Wave_Banner.png", new Color(1f, 0.86f, 0.4f));
            B.Stretch(band.rectTransform);
            var text = H.Number(result, "Label", 72, Navy, TextAnchor.MiddleCenter, Color.white, 3f, 0f);
            B.Stretch(text.rectTransform);
            H.Bind(text, FortressUiKeys.BrushResultText, "{0}", 0f);
        }

        private static void Bar(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, string key, float smooth)
        {
            var bar = B.Rect(parent, name);
            H.Center(bar, position, size);
            var back = H.Picture(bar, "Back", "Wave_Bar.png", new Color(1f, 1f, 1f, 0.2f));
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var fill = H.Picture(bar, "Fill", "Wave_Bar.png", color);
            H.FillHorizontal(fill);
            var bind = fill.gameObject.AddComponent<UiBindFill>();
            bind.key = key;
            bind.fill = fill;
            bind.smoothSeconds = smooth;
        }

        private static void Outline(Text text, float width)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Navy;
            outline.effectDistance = new Vector2(width, -width);
        }
    }
}
