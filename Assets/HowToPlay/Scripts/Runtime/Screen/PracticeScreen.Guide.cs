using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // 分かりやすくするための目印（2026-11-13）。
    // ・フィールドの左上に「ためしてみてね」の札（2026-11-19、レイアウト案に合わせて小さく。研修の名前とやることは右上の欄に出す）
    // ・研修1の間、敵（ばい菌）の絵と説明の札を出す。倒すたびに「ばい菌 1/3」と大きく出す
    public sealed partial class PracticeScreen
    {
        private PracticeShape _nowPlate;
        private Text _nowTag;
        private int _nowLesson = -2;
        private float _nowChangedAt = -10f;

        private RectTransform _enemyCard;
        private PracticeShape _enemyCardShape;
        private float _enemyCardAlpha;
        private int _lastKills;

        private void BuildGuide()
        {
            // フィールドの左上の札「ためしてみてね」。
            _nowPlate = Shape("Field Tag", _root, P(FieldX + 121f, FieldY + 41f), new Vector2(240f, 70f));
            _nowTag = Label("Text", _nowPlate.transform, Ui.fieldTag, 24, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(200f, 40f));
            HowToPlayArt.AddOutline(_nowTag, Ink, 1.5f);

            // 枠の絵を使うときは、札も絵の中にあるので出さない。
            _nowPlate.gameObject.SetActive(Art == null || Art.fieldFrame == null);

            // 敵（ばい菌）の説明の札。
            _enemyCard = HowToPlayArt.MakeRect("Enemy Card", _root, P(FieldX + FieldW - 200f, FieldY + 140f), new Vector2(380f, 110f));
            _enemyCardShape = Shape("Body", _enemyCard, Vector2.zero, new Vector2(380f, 110f));
            var icon = Raw("Enemy", _enemyCard, _cfg.enemy, new Vector2(-130f, 0f), new Vector2(86f, 86f));
            icon.raycastTarget = false;
            Label("Name", _enemyCard, Ui.enemyCardName, 26, Ink, TextAnchor.MiddleLeft, new Vector2(60f, 22f), new Vector2(260f, 32f));
            Label("Hint", _enemyCard, Ui.enemyCardHint, 17, Ink, TextAnchor.MiddleLeft, new Vector2(60f, -20f), new Vector2(260f, 50f));
            SetCardAlpha(0f);
            BuildHeatGauge();
        }

        private void SetCardAlpha(float a)
        {
            _enemyCardAlpha = a;
            foreach (var g in _enemyCard.GetComponentsInChildren<Graphic>(true))
            {
                if (g == _enemyCardShape)
                {
                    continue;
                }

                var c = g.color;
                c.a = a;
                g.color = c;
            }

            _enemyCardShape.Clear();
            if (a > 0.01f)
            {
                var body = PracticeShape.RoundRect(Vector2.zero, new Vector2(370f, 100f), 18f);
                _enemyCardShape.Fill(PracticeShape.RoundRect(new Vector2(5f, -6f), new Vector2(370f, 100f), 18f), new Color(Ink.r, Ink.g, Ink.b, 0.25f * a));
                _enemyCardShape.Fill(body, new Color(1f, 1f, 1f, 0.95f * a));
                _enemyCardShape.Stroke(body, 4f, new Color(0.62f, 0.3f, 0.9f, a), true);
            }
        }

        private void TickGuide()
        {
            var info = _seats[_local];
            var step = LocalStep;
            var lesson = PracticeSteps.Lesson(step);

            // ───── フィールドの左上の札（研修が替わると少し弾む） ─────
            if (lesson != _nowLesson)
            {
                _nowLesson = lesson;
                _nowChangedAt = _time;
            }

            // 前の「いまの研修」の札と同じ見た目（紺の斜めの札・白いふち・左にキャラの色の帯、後ろにキャラの色の影）。
            var accent = lesson >= PracticeSteps.LessonCount ? Ok : info.Color;
            _nowPlate.Clear();
            var body = PracticeShape.Parallelogram(Vector2.zero, new Vector2(202f, 46f), 10f);
            _nowPlate.Fill(PracticeShape.Parallelogram(new Vector2(5f, -5f), new Vector2(202f, 46f), 10f), accent);
            _nowPlate.Fill(body, new Color(Ink.r * 0.8f, Ink.g * 0.8f, Ink.b * 0.8f, 0.94f));
            _nowPlate.Stroke(body, 3f, Color.white, true);
            _nowPlate.Fill(PracticeShape.Parallelogram(new Vector2(-94f, 0f), new Vector2(9f, 46f), 10f), accent);
            var pop = EaseOutBack(Mathf.Clamp01((_time - _nowChangedAt) / 0.35f));
            _nowPlate.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, pop);

            // ───── 敵（ばい菌）の説明と、倒した数 ─────
            var showCard = lesson == 0 && _time > 1.5f;
            var target = showCard ? 1f : 0f;
            if (!Mathf.Approximately(_enemyCardAlpha, target))
            {
                SetCardAlpha(Mathf.MoveTowards(_enemyCardAlpha, target, Time.unscaledDeltaTime * 3f));
            }

            var trainer = PracticeTrainer.Local;
            if (trainer != null && PracticeTrainer.World != null && step == PracticeStep.Hold)
            {
                var kills = trainer.KillsInLesson1;
                if (kills > _lastKills)
                {
                    var need = Mathf.Max(1, (_settings.rules ?? new PracticeRules()).lesson1Kills);
                    Float(new Vector2(0f, 120f), PracticeUiTexts.Format(Ui.killFloatFormat, kills, need), new Color(0.62f, 0.3f, 0.9f), 1.4f);
                }

                _lastKills = kills;
            }

            // ───── 泡: だれの泡か（持ち主の色の輪と名前） ─────
            TickBallMarkers();
            TickHeatGauge();
        }

        // 泡の目印（2026-11-13）。泡は1人に1つで、持ち主しか割れない。自分の泡は太い輪・「あなたの泡」・下向きの矢印、ほかの人の泡は細い輪と「2P」。
        private readonly List<BallMarker> _ballMarkers = new List<BallMarker>();

        private sealed class BallMarker
        {
            public PracticeShape Shape;
            public Text Name;
        }

        private void TickBallMarkers()
        {
            var driver = _sceneMode ? PracticeSceneDriver.Current : null;
            var cam = driver != null ? driver.ViewCamera : null;
            var used = 0;
            if (driver != null && cam != null && !_sceneUnloadRequested)
            {
                for (var i = 0; i < driver.BallCount; i++)
                {
                    if (!driver.TryGetBall(i, out var world, out var owner) || owner < 0 || owner >= _seats.Length || _seats[owner] == null)
                    {
                        continue;
                    }

                    var screen = cam.WorldToScreenPoint(world);
                    if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_field, screen, null, out var local))
                    {
                        continue;
                    }

                    if (Mathf.Abs(local.x) > FieldW * 0.5f || Mathf.Abs(local.y) > FieldH * 0.5f)
                    {
                        continue;
                    }

                    DrawBallMarker(Marker(used++), local, owner);
                }
            }

            for (var i = used; i < _ballMarkers.Count; i++)
            {
                _ballMarkers[i].Shape.Clear();
                _ballMarkers[i].Name.enabled = false;
            }
        }

        private BallMarker Marker(int index)
        {
            while (_ballMarkers.Count <= index)
            {
                var shape = Shape("Ball Marker", _field, Vector2.zero, new Vector2(FieldW, FieldH));
                var name = Heading("Owner", _field, string.Empty, 22, Vector2.zero, new Vector2(260f, 32f), TextAnchor.MiddleCenter, Ink);
                _ballMarkers.Add(new BallMarker { Shape = shape, Name = name });
            }

            return _ballMarkers[index];
        }

        private void DrawBallMarker(BallMarker m, Vector2 p, int owner)
        {
            var info = _seats[owner];
            var mine = owner == _local;
            var c = info.Color;
            m.Shape.Clear();
            m.Name.enabled = true;

            // 札がフィールドの枠からはみ出さないように: 上に入らなければ泡の下に出し、左右は枠の中に寄せる。
            var hx = (FieldW * 0.5f) - 10f;
            var hy = (FieldH * 0.5f) - 10f;
            if (mine)
            {
                // 自分の泡: 持ち主の色の太い輪（脈打つ）と、泡を指す矢印と「あなたの泡」の札。
                const float plateW = 170f;
                const float plateH = 36f;
                const float plateGap = 120f;
                var pulse = Mathf.Repeat(_time * 1.2f, 1f);
                m.Shape.Stroke(PracticeShape.Arc(p, 50f + (pulse * 34f), 0f, 360f, 40), (5f * (1f - pulse)) + 1f, new Color(c.r, c.g, c.b, 1f - pulse), true);
                m.Shape.Stroke(PracticeShape.Arc(p, 50f, 0f, 360f, 40), 9f, Ink, true);
                m.Shape.Stroke(PracticeShape.Arc(p, 50f, 0f, 360f, 40), 6f, c, true);
                var bob = 6f * Mathf.Sin(_time * 6f);
                var s = p.y + plateGap + 6f + (plateH * 0.5f) <= hy ? 1f : -1f;
                var tip = p + new Vector2(0f, s * (58f + bob));
                var arrow = new List<Vector2>
                {
                    tip, tip + new Vector2(-16f, s * 22f), tip + new Vector2(-6f, s * 22f), tip + new Vector2(-6f, s * 42f),
                    tip + new Vector2(6f, s * 42f), tip + new Vector2(6f, s * 22f), tip + new Vector2(16f, s * 22f)
                };
                m.Shape.FillStroke(arrow, c, Ink, 3f);
                var platePos = p + new Vector2(0f, s * (plateGap + bob));
                platePos.x = Mathf.Clamp(platePos.x, -hx + (plateW * 0.5f), hx - (plateW * 0.5f));
                platePos.y = Mathf.Clamp(platePos.y, -hy + (plateH * 0.5f), hy - (plateH * 0.5f));
                m.Shape.FillStroke(PracticeShape.RoundRect(platePos, new Vector2(plateW, plateH), 12f), c, Ink, 3f);
                m.Name.text = Ui.myBubble;
                m.Name.color = Color.white;
                SetSize(m.Name, 24);
                m.Name.rectTransform.anchoredPosition = platePos;
                return;
            }

            // ほかの人の泡: 持ち主の色の細い輪と「2P」。
            m.Shape.Stroke(PracticeShape.Arc(p, 46f, 0f, 360f, 40), 4f, new Color(c.r, c.g, c.b, 0.85f), true);
            m.Name.text = $"{owner + 1}P";
            m.Name.color = c;
            SetSize(m.Name, 20);
            var namePos = p + new Vector2(0f, p.y + 66f + 14f <= hy ? 66f : -66f);
            namePos.x = Mathf.Clamp(namePos.x, -hx + 24f, hx - 24f);
            m.Name.rectTransform.anchoredPosition = namePos;
        }
    }
}
