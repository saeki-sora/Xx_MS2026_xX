using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Fortress
{
    /// <summary>
    /// ボス戦の画面の表示（仮。HUD の段階で UIスタジオの部品に置き換える。2026-11-15）。
    /// 「WARNING」の帯・右上のボスの名前と部位の数の印（◆◆◇◇。2026-11-15 ユーザー決定: HP バーではなく部位の印）・
    /// 「コアに届いた！」の帯・とどめ／撃破の文字。各PCが自分で描く（通信なし）。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(131)]
    internal sealed class BossOverlay : MonoBehaviour
    {
        private const int SortingOrder = 85;
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color Band = new Color(0.1f, 0.03f, 0.05f, 0.86f);
        private static readonly Color Warn = new Color(1f, 0.3f, 0.33f, 1f);
        private static readonly Color MarkAlive = new Color(0.77f, 0.48f, 0.88f, 1f);
        private static readonly Color MarkBroken = new Color(0.95f, 0.9f, 0.97f, 0.35f);

        private readonly List<Image> _marks = new List<Image>();
        private readonly List<Image> _markHoles = new List<Image>();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _warning;
        private Text _warningTitle;
        private Text _warningSub;
        private RectTransform _plate;
        private Text _plateName;
        private RectTransform _reached;
        private Text _reachedTitle;
        private Text _reachedSub;
        private Text _bigText;
        private float _defeatedAt = -100f;

        private static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void OnEnable()
        {
            FortressBoss.Defeated += OnDefeated;
        }

        private void OnDisable()
        {
            FortressBoss.Defeated -= OnDefeated;
        }

        private void OnDefeated()
        {
            _defeatedAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            var phase = FortressWaves.Phase;
            var active = WaveFlow.IsBossPhase(phase) || (phase == WavePhase.Finished && FortressBoss.Definition != null);
            if (!active)
            {
                if (_canvas != null)
                {
                    _canvas.gameObject.SetActive(false);
                }

                return;
            }

            EnsureCanvas();
            _canvas.gameObject.SetActive(true);
            var def = FortressBoss.Definition;
            var elapsed = FortressWaves.PhaseElapsed;
            UpdateWarning(phase, def, elapsed);
            UpdatePlate(phase, def);
            UpdateReached(phase, def, elapsed);
            UpdateBigText(phase, elapsed);
        }

        private void UpdateWarning(WavePhase phase, BossDefinition def, float elapsed)
        {
            var seconds = def != null ? def.warningSeconds : 2.4f;
            var show = phase == WavePhase.BossWarning && elapsed < seconds;
            _warning.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var appear = Mathf.Clamp01(elapsed / 0.18f);
            var leave = Mathf.Clamp01((seconds - elapsed) / 0.25f);
            _warning.localScale = new Vector3(1f, appear * leave, 1f);
            var blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(elapsed * Mathf.PI * 2.2f));
            _warningTitle.color = new Color(Warn.r, Warn.g, Warn.b, blink);
            _warningSub.text = def != null ? $"{def.displayName}が 近づいてくる！" : "ボスが 近づいてくる！";
        }

        private void UpdatePlate(WavePhase phase, BossDefinition def)
        {
            var show = def != null && FortressBoss.IsVisible && (phase == WavePhase.BossWarning || phase == WavePhase.BossCombat || phase == WavePhase.BossFinale);
            _plate.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            _plateName.text = def.displayName;
            var count = Mathf.Max(0, FortressBoss.PartCount);
            while (_marks.Count < count)
            {
                AddMark();
            }

            for (var i = 0; i < _marks.Count; i++)
            {
                var visible = i < count;
                _marks[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var alive = FortressBoss.IsPartAlive(i);
                _marks[i].color = alive ? MarkAlive : MarkBroken;
                _markHoles[i].enabled = !alive;
                _marks[i].rectTransform.anchoredPosition = new Vector2(-40f - (count - 1 - i) * 52f, 0f);
            }

            _plate.sizeDelta = new Vector2(300f + count * 52f, 76f);
        }

        private void UpdateReached(WavePhase phase, BossDefinition def, float elapsed)
        {
            var show = phase == WavePhase.BossReached && elapsed < 2.4f;
            _reached.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            var percent = Mathf.RoundToInt((def != null ? def.reachCoreDamage01 : 0.45f) * 100f);
            _reachedSub.text = $"コアの体力 −{percent}%（まだ残っている！）";
            _reached.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.2f - elapsed) * 1.5f);
        }

        private void UpdateBigText(WavePhase phase, float elapsed)
        {
            var sinceDefeat = Time.unscaledTime - _defeatedAt;
            if (sinceDefeat < 2.5f)
            {
                _bigText.gameObject.SetActive(true);
                _bigText.text = "撃破！";
                _bigText.color = new Color(1f, 0.82f, 0.4f, Mathf.Clamp01((2.5f - sinceDefeat) / 0.4f));
                _bigText.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.25f - sinceDefeat) * 2f);
                return;
            }

            if (phase == WavePhase.BossFinale)
            {
                // （仮）とどめの「全員で握れ！」は次の段階で作る。
                _bigText.gameObject.SetActive(true);
                _bigText.text = "部位を全部こわした！";
                _bigText.color = new Color(1f, 1f, 1f, Mathf.Clamp01(elapsed / 0.2f));
                _bigText.rectTransform.localScale = Vector3.one;
                return;
            }

            _bigText.gameObject.SetActive(false);
        }

        // ───────── 部品 ─────────

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }

            _marks.Clear();
            _markHoles.Clear();
            var go = new GameObject("[Boss] Overlay", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)go.transform;

            // WARNING の帯（画面の真ん中、横いっぱい）。
            _warning = NewRect("Warning", _canvasRect);
            _warning.anchorMin = new Vector2(0f, 0.5f);
            _warning.anchorMax = new Vector2(1f, 0.5f);
            _warning.sizeDelta = new Vector2(0f, 230f);
            var bg = NewImage("Band", _warning, null, Band);
            Stretch(bg.rectTransform);
            foreach (var top in new[] { true, false })
            {
                var stripe = NewImage(top ? "StripeTop" : "StripeBottom", _warning, BossArt.Stripe, Color.white);
                stripe.type = Image.Type.Tiled;
                var rt = stripe.rectTransform;
                rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
                rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
                rt.pivot = new Vector2(0.5f, top ? 0f : 1f);
                rt.sizeDelta = new Vector2(0f, 22f);
                rt.anchoredPosition = Vector2.zero;
            }

            _warningTitle = NewText("Title", _warning, "WARNING", 150, Warn, new Vector2(0f, 26f));
            _warningSub = NewText("Sub", _warning, "", 44, Color.white, new Vector2(0f, -78f));

            // 右上の名前と部位の印。
            _plate = NewRect("Plate", _canvasRect);
            _plate.anchorMin = _plate.anchorMax = new Vector2(1f, 1f);
            _plate.pivot = new Vector2(1f, 1f);
            _plate.anchoredPosition = new Vector2(-36f, -28f);
            var plateBg = NewImage("Bg", _plate, null, new Color(0.15f, 0.05f, 0.2f, 0.85f));
            Stretch(plateBg.rectTransform);
            _plateName = NewText("Name", _plate, "", 40, new Color(0.95f, 0.9f, 0.97f), Vector2.zero);
            _plateName.alignment = TextAnchor.MiddleLeft;
            var nameRect = _plateName.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(24f, 0f);
            nameRect.offsetMax = Vector2.zero;

            // 「コアに届いた！」の帯。
            _reached = NewRect("Reached", _canvasRect);
            _reached.anchorMin = _reached.anchorMax = new Vector2(0.5f, 0.5f);
            _reached.sizeDelta = new Vector2(1000f, 200f);
            var reachedBg = NewImage("Bg", _reached, null, new Color(0.32f, 0.04f, 0.08f, 0.88f));
            Stretch(reachedBg.rectTransform);
            _reachedTitle = NewText("Title", _reached, "コアに届いた！", 84, new Color(1f, 0.42f, 0.42f), new Vector2(0f, 26f));
            _reachedSub = NewText("Sub", _reached, "", 36, Color.white, new Vector2(0f, -56f));

            _bigText = NewText("Big", _canvasRect, "", 120, Color.white, new Vector2(0f, 120f));
            _bigText.rectTransform.sizeDelta = new Vector2(1600f, 200f);
            _bigText.gameObject.SetActive(false);
        }

        private void AddMark()
        {
            var mark = NewImage("Mark", _plate, null, MarkAlive);
            var rt = mark.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var hole = NewImage("Hole", rt, null, new Color(0.15f, 0.05f, 0.2f, 1f));
            hole.rectTransform.sizeDelta = new Vector2(18f, 18f);
            hole.enabled = false;
            _marks.Add(mark);
            _markHoles.Add(hole);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, string value, int size, Color color, Vector2 position)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1800f, size * 1.4f);
            rt.anchoredPosition = position;
            var text = rt.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
