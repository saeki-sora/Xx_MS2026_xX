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
    /// 見た目はプロジェクトの絵（タイトル・キャラ選択・HUD のバー）とコンセプトアートに合わせた
    /// 「パステルの水色・ピンク・ラベンダー、白と紺の太い縁、すりガラスのパネル」（2026-10-25）。絵は Tools/UIArt/make_wave_art.py。
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

        /// <summary>歯についたバイキン（タイトルのバイキンの絵）。</summary>
        private const string GermPath = "Assets/Title/Sprites/Title_Germ.png";

        internal static readonly Color Navy = new Color(0.09f, 0.1f, 0.31f);
        internal static readonly Color Hot = new Color(1f, 0.27f, 0.67f);
        internal static readonly Color Lavender = new Color(0.7f, 0.61f, 1f);

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
            marker.sizeDelta = new Vector2(180f, 170f);
            var arrow = H.Picture(marker, "Arrow", "Wave_Arrow.png", Color.white);
            H.Center(arrow.rectTransform, new Vector2(0f, -36f), new Vector2(84f, 84f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f); // 下向き（砲台を指す）
            var tag = H.Picture(marker, "Tag", "Brush_Chip.png", Color.white);
            tag.type = Image.Type.Sliced;
            H.Center(tag.rectTransform, new Vector2(0f, 42f), new Vector2(170f, 70f));
            var label = W.Glitch(H.Label(tag.rectTransform, "Label", "YOU", 46, Color.white, TextAnchor.MiddleCenter), 3f);
            B.Stretch(label.rectTransform);
            var you = root.gameObject.AddComponent<WaveYouMarker>();
            you.marker = marker;
            you.arrow = arrow.rectTransform;
            you.tag = tag.rectTransform;
            you.tinted = new Graphic[] { arrow, label };
            marker.gameObject.SetActive(false);
        }

        // 試合の最初だけ、画面の下に握り方のヒント（すりガラスのパネル）。
        private static void BuildFirstHint(Transform screen)
        {
            var root = B.Rect(screen, HintName);
            B.Place(root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1060f, 150f));
            W.Visible(root, FortressUiKeys.WaveFirstHint, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInBottom, 0.4f);
            var back = H.Picture(root, "Back", "Wave_Panel.png", Color.white);
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var hand = B.Rect(root, "Hand").gameObject.AddComponent<RawImage>();
            hand.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{H.ArtFolder}/Brush_Hand.png");
            hand.uvRect = new Rect(0.5f, 0f, 0.5f, 1f); // 握った手
            hand.raycastTarget = false;
            H.Center(hand.rectTransform, new Vector2(-440f, 0f), new Vector2(130f, 130f));
            B.AddMotion(hand.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.8f);
            var line1 = W.Glitch(H.Label(root, "Line1", "握ると ビーム！ 強く握るほど 太くなる！", 44, Navy, TextAnchor.MiddleLeft), 2f);
            H.Center(line1.rectTransform, new Vector2(-360f, 24f), new Vector2(860f, 60f), new Vector2(0f, 0.5f));
            var line2 = H.Label(root, "Line2", "握りっぱなしは 熱くなって しばらく撃てない（オーバーヒート）", 28, Hot, TextAnchor.MiddleLeft);
            H.Center(line2.rectTransform, new Vector2(-360f, -32f), new Vector2(860f, 40f), new Vector2(0f, 0.5f));
        }

        // 発表のとき、初めて出てくる敵のカード（最大3枚、画面の右）。中身は WaveNewEnemyCards が入れる。
        private static void BuildNewEnemies(Transform screen)
        {
            var root = B.Rect(screen, NewEnemiesName);
            B.Place(root, new Vector2(1f, 0.5f), new Vector2(-36f, -210f), new Vector2(460f, 560f));
            W.Visible(root, FortressUiKeys.WaveShowNewEnemies, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.4f);
            var cards = new WaveNewEnemyCards.Card[3];
            for (var i = 0; i < cards.Length; i++)
            {
                var card = B.Rect(root, $"Card{i + 1}");
                H.Center(card, new Vector2(0f, 186f - 186f * i), new Vector2(460f, 176f));
                var back = card.gameObject.AddComponent<Image>();
                back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Wave_Panel.png");
                back.type = Image.Type.Sliced;
                back.raycastTarget = false;
                var tagBack = H.Picture(card, "TagBack", "Wave_Bar.png", Hot);
                tagBack.type = Image.Type.Sliced;
                H.Center(tagBack.rectTransform, new Vector2(-48f, 70f), new Vector2(200f, 34f));
                var tag = H.Label(tagBack.rectTransform, "Tag", "NEW ENEMY!", 24, Color.white, TextAnchor.MiddleCenter);
                B.Stretch(tag.rectTransform);
                var fallback = H.Picture(card, "Dot", "Brush_Bubble.png", Color.white);
                H.Center(fallback.rectTransform, new Vector2(-158f, -8f), new Vector2(104f, 104f));
                var picture = B.Rect(card, "Picture").gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
                H.Center(picture.rectTransform, new Vector2(-158f, -8f), new Vector2(124f, 124f));
                B.AddMotion(picture.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 1.6f);
                var title = H.Label(card, "Name", "", 34, Navy, TextAnchor.MiddleLeft);
                title.horizontalOverflow = HorizontalWrapMode.Wrap;
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 20;
                title.resizeTextMaxSize = 34;
                H.Center(title.rectTransform, new Vector2(-86f, 22f), new Vector2(300f, 46f), new Vector2(0f, 0.5f));
                // 紹介文は30文字まで（EnemyTypeDefinition.IntroTextMaxLength）。枠に収まるよう文字を小さくする。
                var body = B.Text(card, "Text", "", 26, new Color(0.25f, 0.27f, 0.5f), TextAnchor.UpperLeft);
                body.fontStyle = FontStyle.Bold;
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.verticalOverflow = VerticalWrapMode.Truncate;
                body.resizeTextForBestFit = true;
                body.resizeTextMinSize = 18;
                body.resizeTextMaxSize = 26;
                H.Center(body.rectTransform, new Vector2(-86f, -36f), new Vector2(300f, 86f), new Vector2(0f, 0.5f));
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
            B.Place(root, new Vector2(0.5f, 1f), new Vector2(60f, -166f), new Vector2(330f, 64f));
            W.Visible(root, FortressUiKeys.CoreHasCoating, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.3f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnValueDown, UiPreset.Shake, 0.25f, 0f, FortressUiKeys.CoreCoating);
            var back = H.Picture(root, "Back", "Brush_Chip.png", Color.white);
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var shine = H.Picture(root, "Sparkle", "Brush_Sparkle.png", Color.white);
            H.Center(shine.rectTransform, new Vector2(-136f, 0f), new Vector2(46f, 46f));
            B.AddMotion(shine.gameObject, UiMotionTrigger.Loop, UiPreset.Pulse, 1f);
            var label = H.Label(root, "Label", "", 28, Navy, TextAnchor.MiddleCenter);
            H.Center(label.rectTransform, new Vector2(16f, 0f), new Vector2(280f, 50f));
            H.Bind(label, FortressUiKeys.CoreCoating, "コーティング {0:0}", 0f);
        }

        // ───────── 歯みがきタイム ─────────
        // 画面（1920×1080）: 上に「歯みがきタイム！」の帯と一言、真ん中に歯（バイキンが1匹ずつ消える）、
        // 左に握り方（手のアニメ）、右に残り時間の輪と「あと○回」、下にきれい度のバー（コアのバーと同じ絵）と「いま HP +○○」。

        private static void BuildBrushTime(Transform screen)
        {
            var root = B.Rect(screen, BrushName);
            root.SetAsLastSibling();
            B.Stretch(root);
            W.Visible(root, FortressUiKeys.BrushActive, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(root.gameObject, UiMotionTrigger.OnShow, UiPreset.FadeIn, 0.3f);

            var dim = B.Rect(root, "Dim").gameObject.AddComponent<Image>();
            B.Stretch(dim.rectTransform);
            dim.color = new Color(Navy.r, Navy.g, Navy.b, 0.55f);
            dim.raycastTarget = false;

            BuildBrushTitle(root);
            BuildBrushStage(root, out var germs, out var shine, out var popArea, out var brushes);
            var hand = BuildGripCard(root);
            BuildTimerCard(root);
            BuildCleanGauge(root);
            BuildSyncAndResult(root);

            var view = root.gameObject.AddComponent<BrushTimeView>();
            view.germs = germs;
            view.hand = hand;
            view.brushes = brushes;
            view.shine = shine;
            view.bubble = AssetDatabase.LoadAssetAtPath<Sprite>($"{H.ArtFolder}/Brush_Bubble.png");
            view.popArea = popArea;
            view.plusColor = Hot;
            // 歯みがきの間は、下の普段の HUD を隠す（コアのバーは回復が見えるように残す）。
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (var name in HiddenWhileBrushing)
            {
                var found = screen.Find(name);
                if (found != null)
                {
                    others.Add(found.gameObject);
                }
            }

            view.hideWhileBrushing = others.ToArray();
        }

        /// <summary>歯みがきの間だけ隠す、普段の HUD の部品の名前。</summary>
        private static readonly string[] HiddenWhileBrushing = { "PlayerPanel", "OtherPlayers", "KillCounter", HudWaveBuilder.PlateName, HudPanelsBuilder.CoreName, CoatingName };

        private static void BuildBrushTitle(RectTransform root)
        {
            var title = B.Rect(root, "Title");
            H.Center(title, new Vector2(0f, 405f), new Vector2(1040f, 184f));
            B.AddMotion(title.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.5f);
            var band = H.Picture(title, "Band", "Brush_Title.png", Color.white);
            B.Stretch(band.rectTransform);
            var text = W.Glitch(H.Label(title, "Label", "歯みがきタイム！", 92, Color.white, TextAnchor.MiddleCenter), 5f);
            W.Outline(text, Navy, 5f);
            B.Stretch(text.rectTransform);

            var sub = B.Rect(root, "Sub");
            H.Center(sub, new Vector2(0f, 292f), new Vector2(940f, 64f));
            var subBack = H.Picture(sub, "Back", "Brush_Chip.png", Color.white);
            subBack.type = Image.Type.Sliced;
            B.Stretch(subBack.rectTransform);
            var subText = H.Label(sub, "Label", "にぎにぎ連打で 歯をピカピカに！ みがいた分だけ コアが回復", 32, Navy, TextAnchor.MiddleCenter);
            B.Stretch(subText.rectTransform);
        }

        // 歯（コア）・バイキン・光・歯ブラシ・「+1」と泡の場所。
        private static void BuildBrushStage(RectTransform root, out RectTransform[] germs, out Image shine, out RectTransform popArea, out RectTransform[] brushes)
        {
            var stage = B.Rect(root, "Stage");
            H.Center(stage, new Vector2(0f, 20f), new Vector2(520f, 520f));
            B.AddMotion(stage.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.4f, 0.15f);

            shine = H.Picture(stage, "Shine", "Brush_Burst.png", new Color(1f, 1f, 1f, 0f));
            H.Center(shine.rectTransform, Vector2.zero, new Vector2(760f, 760f));
            var disk = H.Picture(stage, "Disk", "Brush_Stage.png", Color.white);
            B.Stretch(disk.rectTransform);

            var tooth = H.Picture(stage, "Tooth", "Brush_Tooth.png", Color.white);
            H.Center(tooth.rectTransform, new Vector2(0f, 6f), new Vector2(300f, 314f));
            B.AddMotion(tooth.gameObject, UiMotionTrigger.Loop, UiPreset.Float, 2.4f);

            // バイキン（タイトルの絵）。きれい度が上がるたびに、上から順に1匹ずつ消える。
            var germSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GermPath);
            var places = new[]
            {
                new Vector2(-70f, 90f), new Vector2(64f, 104f), new Vector2(-10f, 34f), new Vector2(84f, 18f),
                new Vector2(-88f, -22f), new Vector2(36f, -50f), new Vector2(-62f, -100f), new Vector2(66f, -108f)
            };
            var germRoot = B.Rect(stage, "Germs");
            B.Stretch(germRoot);
            germs = new RectTransform[places.Length];
            for (var i = 0; i < places.Length; i++)
            {
                var germ = B.Rect(germRoot, $"Germ{i + 1}").gameObject.AddComponent<Image>();
                germ.sprite = germSprite;
                germ.raycastTarget = false;
                H.Center(germ.rectTransform, places[i], Vector2.one * (78f + 14f * (i % 3)));
                germs[i] = germ.rectTransform;
            }

            popArea = B.Rect(stage, "Pops");
            H.Center(popArea, new Vector2(0f, 10f), new Vector2(420f, 420f));

            // 4人の歯ブラシ（いる人だけ）。左右から歯に毛先を向ける。持ち手の線がその人の色。
            brushes = new RectTransform[4];
            var spots = new[] { new Vector2(-205f, 120f), new Vector2(205f, 120f), new Vector2(-205f, -70f), new Vector2(205f, -70f) };
            for (var p = 0; p < 4; p++)
            {
                var holder = B.Rect(stage, $"Brush{p + 1}");
                H.Center(holder, spots[p], new Vector2(240f, 88f));
                W.Visible(holder, FortressUiKeys.BrushIn(p), UiVisibleCondition.WhenOn, 0.5f);
                var body = B.Rect(holder, "Body");
                B.Stretch(body);
                if (p % 2 == 1)
                {
                    body.localScale = new Vector3(-1f, 1f, 1f); // 右の歯ブラシは毛先を左（歯の方）へ
                }

                var brush = H.Picture(body, "Brush", "Brush_Brush.png", Color.white);
                B.Stretch(brush.rectTransform);
                var stripe = H.Picture(body, "Stripe", "Brush_BrushStripe.png", FortressColors.PlayerColor(p));
                B.Stretch(stripe.rectTransform);
                TintByPlayer(stripe, p);
                brushes[p] = body;
            }
        }

        // 左: 握り方（手のアニメ）。
        private static RawImage BuildGripCard(RectTransform root)
        {
            var card = B.Rect(root, "GripCard");
            H.Center(card, new Vector2(-585f, 20f), new Vector2(330f, 390f));
            W.Visible(card, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOff, 0.5f);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInLeft, 0.4f, 0.1f);
            var back = H.Picture(card, "Back", "Brush_Card.png", Color.white);
            B.Stretch(back.rectTransform);
            var hand = B.Rect(card, "Hand").gameObject.AddComponent<RawImage>();
            hand.texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{H.ArtFolder}/Brush_Hand.png");
            hand.uvRect = new Rect(0f, 0f, 0.5f, 1f);
            hand.raycastTarget = false;
            H.Center(hand.rectTransform, new Vector2(0f, 52f), new Vector2(230f, 230f));
            var label = W.Glitch(H.Label(card, "Label", "にぎにぎ！", 58, Hot, TextAnchor.MiddleCenter), 3f);
            W.Outline(label, Color.white, 4f);
            H.Center(label.rectTransform, new Vector2(0f, -100f), new Vector2(320f, 74f));
            B.AddMotion(label.gameObject, UiMotionTrigger.Loop, UiPreset.HeartBeat, 0.6f);
            var how = H.Label(card, "How", "握る → ゆるめる を くり返す", 24, Navy, TextAnchor.MiddleCenter);
            H.Center(how.rectTransform, new Vector2(0f, -150f), new Vector2(320f, 36f));
            return hand;
        }

        // 右: 残り時間の輪と「あと○回」。
        private static void BuildTimerCard(RectTransform root)
        {
            var card = B.Rect(root, "TimerCard");
            H.Center(card, new Vector2(585f, 20f), new Vector2(330f, 390f));
            W.Visible(card, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOff, 0.5f);
            B.AddMotion(card.gameObject, UiMotionTrigger.OnShow, UiPreset.SlideFadeInRight, 0.4f, 0.1f);
            var back = H.Picture(card, "Back", "Brush_Card.png", Color.white);
            B.Stretch(back.rectTransform);

            var ring = B.Rect(card, "Timer");
            H.Center(ring, new Vector2(0f, 70f), new Vector2(200f, 200f));
            var ringBack = H.Picture(ring, "Back", "Brush_TimerBack.png", Color.white);
            B.Stretch(ringBack.rectTransform);
            var ringFill = H.Picture(ring, "Fill", "Brush_TimerFill.png", Color.white);
            B.Stretch(ringFill.rectTransform);
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = false;
            var fill = ringFill.gameObject.AddComponent<UiBindFill>();
            fill.key = FortressUiKeys.BrushTime01;
            fill.fill = ringFill;
            fill.smoothSeconds = 0.1f;
            var number = W.Glitch(H.Label(ring, "Number", "8", 76, Color.white, TextAnchor.MiddleCenter), 3f);
            H.Center(number.rectTransform, new Vector2(0f, 6f), new Vector2(160f, 100f));
            H.Bind(number, FortressUiKeys.BrushTimeLeft, "{0:0}", 0f);
            B.AddMotion(number.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.2f, 0f, FortressUiKeys.BrushTimeLeft);
            var seconds = H.Label(ring, "Caption", "びょう", 20, H.Cyan, TextAnchor.MiddleCenter);
            H.Center(seconds.rectTransform, new Vector2(0f, -46f), new Vector2(120f, 30f));

            var left = H.Label(card, "LeftCaption", "100% まで あと", 26, Navy, TextAnchor.MiddleCenter);
            H.Center(left.rectTransform, new Vector2(0f, -66f), new Vector2(320f, 36f));
            var count = W.Glitch(H.Label(card, "Left", "12回", 64, Hot, TextAnchor.MiddleCenter), 3f);
            W.Outline(count, Color.white, 4f);
            count.supportRichText = true;
            H.Center(count.rectTransform, new Vector2(0f, -124f), new Vector2(320f, 80f));
            H.Bind(count, FortressUiKeys.BrushRemaining, "{0:0}<size=34>回</size>", 0f);
            B.AddMotion(count.gameObject, UiMotionTrigger.OnValueDown, UiPreset.PunchScale, 0.15f, 0f, FortressUiKeys.BrushRemaining);
        }

        // 下: きれい度のバー（コアのバーと同じ絵）・4人の頑張り・「いま HP +○○」。
        private static void BuildCleanGauge(RectTransform root)
        {
            // 結果を見せている間は、下のバー・頑張り・「いま HP」は隠す（結果の帯だけを見せる）。
            var bottom = B.Rect(root, "Bottom");
            B.Stretch(bottom);
            W.Visible(bottom, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOff, 0.5f);
            root = bottom;

            var players = B.Rect(root, "Players");
            H.Center(players, new Vector2(0f, -258f), new Vector2(920f, 50f));
            for (var p = 0; p < 4; p++)
            {
                var chip = B.Rect(players, $"P{p + 1}");
                H.Center(chip, new Vector2(-345f + 230f * p, 0f), new Vector2(214f, 50f));
                W.Visible(chip, FortressUiKeys.BrushIn(p), UiVisibleCondition.WhenOn, 0.5f);
                var back = H.Picture(chip, "Back", "Brush_Chip.png", Color.white);
                back.type = Image.Type.Sliced;
                B.Stretch(back.rectTransform);
                var tag = H.Label(chip, "Tag", $"{p + 1}P", 26, FortressColors.PlayerColor(p), TextAnchor.MiddleLeft);
                W.Outline(tag, Navy, 2f);
                H.Center(tag.rectTransform, new Vector2(-92f, 0f), new Vector2(60f, 40f), new Vector2(0f, 0.5f));
                TintByPlayer(tag, p);
                var share = Bar(chip, "Bar", new Vector2(24f, 0f), new Vector2(118f, 14f), FortressColors.PlayerColor(p), FortressUiKeys.BrushShare(p), 0.1f, new Color(Navy.r, Navy.g, Navy.b, 0.18f));
                TintByPlayer(share, p);
            }

            // コアのバーと同じ絵（チームの絵）で、きれい度。
            var gauge = B.Rect(root, "Gauge");
            H.Center(gauge, new Vector2(0f, -332f), new Vector2(812f, 99f)); // Hud_CoreFrame 478×58 の 1.7 倍
            var fillArea = B.Rect(gauge, "FillArea");
            HudPx(fillArea, new Vector2(478f, 58f), 1.7f, 10f, 12f, 459f, 35f);
            var back2 = B.Rect(fillArea, "Back").gameObject.AddComponent<Image>();
            B.Stretch(back2.rectTransform);
            back2.color = new Color(Navy.r, Navy.g, Navy.b, 0.85f);
            back2.raycastTarget = false;
            var cleanFill = H.Picture(fillArea, "Fill", "Hud_CoreFill.png", Color.white);
            H.FillHorizontal(cleanFill);
            var bind = cleanFill.gameObject.AddComponent<UiBindFill>();
            bind.key = FortressUiKeys.BrushClean01;
            bind.fill = cleanFill;
            bind.smoothSeconds = 0.15f;
            var frame = H.Picture(gauge, "Frame", "Hud_CoreFrame.png", Color.white);
            B.Stretch(frame.rectTransform);
            var percent = W.Glitch(H.Label(gauge, "Percent", "62%", 48, Color.white, TextAnchor.MiddleCenter), 2f);
            W.Outline(percent, Navy, 3f);
            percent.supportRichText = true;
            B.Stretch(percent.rectTransform);
            H.Bind(percent, FortressUiKeys.BrushClean01, "きれい度 {0:0'<size=34>'%'</size>'}", 0f);

            // 「いま HP +120」（大きく）と「100% で +250」（小さく）の2行。
            var preview = B.Rect(root, "Preview");
            H.Center(preview, new Vector2(0f, -432f), new Vector2(760f, 96f));
            var previewBack = H.Picture(preview, "Back", "Brush_Chip.png", Color.white);
            previewBack.type = Image.Type.Sliced;
            B.Stretch(previewBack.rectTransform);
            var now = W.Glitch(H.Label(preview, "Now", "いま HP +120", 40, Hot, TextAnchor.MiddleCenter), 2f);
            now.resizeTextForBestFit = true;
            now.resizeTextMinSize = 26;
            now.resizeTextMaxSize = 40;
            now.horizontalOverflow = HorizontalWrapMode.Wrap;
            H.Center(now.rectTransform, new Vector2(0f, 14f), new Vector2(700f, 54f));
            H.Bind(now, FortressUiKeys.BrushPreviewText, "{0}", 0f);
            B.AddMotion(now.gameObject, UiMotionTrigger.OnValueUp, UiPreset.PunchScale, 0.15f, 0f, FortressUiKeys.BrushClean01);
            var full = H.Label(preview, "Full", "100% で +250", 24, Navy, TextAnchor.MiddleCenter);
            H.Center(full.rectTransform, new Vector2(0f, -26f), new Vector2(700f, 32f));
            H.Bind(full, FortressUiKeys.BrushFullText, "{0}", 0f);
        }

        // シンクロ！（全員がそろって握った）と、結果（ピカピカ！ HP +120　コーティング +50）。
        private static void BuildSyncAndResult(RectTransform root)
        {
            var sync = B.Rect(root, "Sync");
            H.Center(sync, new Vector2(0f, 200f), new Vector2(700f, 110f));
            W.Visible(sync, FortressUiKeys.BrushSyncShowing, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(sync.gameObject, UiMotionTrigger.OnShow, UiPreset.PopIn, 0.2f);
            var syncText = W.Glitch(H.Label(sync, "Label", "シンクロ！ ボーナス！", 66, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleCenter), 4f);
            W.Outline(syncText, Navy, 5f);
            B.Stretch(syncText.rectTransform);

            var result = B.Rect(root, "Result");
            H.Center(result, new Vector2(0f, 20f), new Vector2(1160f, 300f));
            W.Visible(result, FortressUiKeys.BrushResult, UiVisibleCondition.WhenOn, 0.5f);
            B.AddMotion(result.gameObject, UiMotionTrigger.OnShow, UiPreset.BounceIn, 0.45f);
            var burst = H.Picture(result, "Burst", "Brush_Burst.png", new Color(1f, 1f, 1f, 0.55f));
            H.Center(burst.rectTransform, new Vector2(0f, 10f), new Vector2(700f, 700f));
            B.AddMotion(burst.gameObject, UiMotionTrigger.Loop, UiPreset.RotateLoop, 10f);
            var band = H.Picture(result, "Band", "Brush_Title.png", Color.white);
            H.Center(band.rectTransform, new Vector2(0f, -30f), new Vector2(1160f, 205f));
            var heading = W.Glitch(H.Label(result, "Title", "ピカピカ！", 96, Color.white, TextAnchor.MiddleCenter), 5f);
            W.Outline(heading, Navy, 5f);
            H.Center(heading.rectTransform, new Vector2(0f, 98f), new Vector2(1100f, 120f));
            H.Bind(heading, FortressUiKeys.BrushResultTitle, "{0}", 0f);
            var text = W.Glitch(H.Label(result, "Label", "HP +120　コーティング +50", 66, Color.white, TextAnchor.MiddleCenter), 3f);
            W.Outline(text, Navy, 4f);
            H.Center(text.rectTransform, new Vector2(0f, -30f), new Vector2(1100f, 110f));
            H.Bind(text, FortressUiKeys.BrushResultText, "{0}", 0f);
        }

        // HUD の絵の中の px（左上が原点）で置く。scale は絵を何倍で出しているか。
        private static void HudPx(RectTransform rect, Vector2 picture, float scale, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h) * scale;
            rect.anchoredPosition = new Vector2(x + w * 0.5f - picture.x * 0.5f, picture.y * 0.5f - (y + h * 0.5f)) * scale;
        }

        private static Image Bar(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, string key, float smooth, Color backColor)
        {
            var bar = B.Rect(parent, name);
            H.Center(bar, position, size);
            var back = H.Picture(bar, "Back", "Wave_Bar.png", backColor);
            back.type = Image.Type.Sliced;
            B.Stretch(back.rectTransform);
            var fill = H.Picture(bar, "Fill", "Wave_Bar.png", color);
            H.FillHorizontal(fill);
            var bind = fill.gameObject.AddComponent<UiBindFill>();
            bind.key = key;
            bind.fill = fill;
            bind.smoothSeconds = smooth;
            return fill;
        }

        // その人の色を、ゲーム中の値（player.N.color）で塗る。キャラ選択で選んだキャラの色になるように（組み立てたときの席の色のままにしない）。
        private static void TintByPlayer(Graphic graphic, int player)
        {
            var bind = graphic.gameObject.AddComponent<UiBindColor>();
            bind.key = FortressUiKeys.PlayerColor(player);
            bind.target = graphic;
        }
    }
}
