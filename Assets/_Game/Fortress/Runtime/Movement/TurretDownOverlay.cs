using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】自分の画面にだけ出す表示（仮の見た目。通信しない）。
    /// ・右上: 自分の体力バー（体力 = 1 − 汚れ。少ないと赤くなる。倒れている間は「ダウン中」）
    /// ・倒れている間だけ、下の方に「にぎにぎ連打で抜け出せ！」＋ キーボードのキー ＋ 復活の進み具合のバー。握るたびに少し跳ねる。
    /// </summary>
    internal sealed class TurretDownOverlay
    {
        private const int SortingOrder = 900;
        private const float BarWidth = 640f;
        private const float BarHeight = 26f;
        private const float HealthWidth = 360f;
        private const float HealthHeight = 22f;
        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.04f, 0.78f);
        private static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color LowHealthColor = new Color(1f, 0.32f, 0.28f, 1f);

        private Canvas _canvas;

        // 抜け出しの案内
        private RectTransform _panel;
        private Text _hint;
        private Image _fill;
        private float _pulse;
        private float _shownRevive;

        // 体力バー
        private RectTransform _healthRoot;
        private Text _healthLabel;
        private Image _healthFill;
        private float _shownHealth = 1f;
        private float _hitFlash;

        private static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>
        /// 毎フレーム呼ぶ。
        /// showHealth = 体力バーを出すか、downed = 自分が倒れているか、pressed = 今フレームに握った（案内を跳ねさせる）。
        /// </summary>
        public void Update(bool showHealth, float health01, bool downed, float revive01, int player, string keyName, bool pressed, float dt)
        {
            if (!showHealth && !downed)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf)
                {
                    _canvas.gameObject.SetActive(false);
                }

                _shownRevive = 0f;
                return;
            }

            Ensure();
            if (!_canvas.gameObject.activeSelf)
            {
                _canvas.gameObject.SetActive(true);
            }

            UpdateHealth(showHealth, health01, downed, player, dt);
            UpdateStruggle(downed, revive01, player, keyName, pressed, dt);
        }

        public void Dispose()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas.gameObject);
            }

            _canvas = null;
        }

        private void UpdateHealth(bool visible, float health01, bool downed, int player, float dt)
        {
            if (_healthRoot.gameObject.activeSelf != visible)
            {
                _healthRoot.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            health01 = Mathf.Clamp01(health01);
            if (health01 < _shownHealth - 0.002f)
            {
                _hitFlash = 1f; // 減った瞬間に少し光らせる
            }

            _shownHealth = Mathf.Lerp(_shownHealth, health01, TurretMoveMath.Blend(14f, dt));
            _hitFlash = Mathf.Max(0f, _hitFlash - dt * 3f);

            var color = health01 <= 0.3f ? LowHealthColor : FortressColors.PlayerColor(player);
            _healthFill.color = Color.Lerp(color, Color.white, 0.5f * _hitFlash);
            _healthFill.rectTransform.sizeDelta = new Vector2(HealthWidth * _shownHealth, HealthHeight);
            _healthLabel.text = downed ? "体力　ダウン中" : "体力";
        }

        private void UpdateStruggle(bool visible, float revive01, int player, string keyName, bool pressed, float dt)
        {
            if (_panel.gameObject.activeSelf != visible)
            {
                _panel.gameObject.SetActive(visible);
                _pulse = 0f;
            }

            if (!visible)
            {
                _shownRevive = 0f;
                return;
            }

            if (pressed)
            {
                _pulse = 1f;
            }

            _pulse = Mathf.Max(0f, _pulse - dt * 6f);
            _shownRevive = Mathf.Lerp(_shownRevive, Mathf.Clamp01(revive01), TurretMoveMath.Blend(18f, dt));

            var scale = 1f + 0.08f * _pulse * _pulse;
            _panel.localScale = new Vector3(scale, scale, 1f);
            _hint.text = $"キーボードは {keyName} を連打 ／ 仲間のビームを当ててもらっても復活";
            _fill.color = FortressColors.PlayerColor(player);
            _fill.rectTransform.sizeDelta = new Vector2(BarWidth * _shownRevive, BarHeight);
        }

        // 今のシーンの普通のオブジェクトにする（DontSave の係の子にすると、Play を止めても残ってしまう）。シーンが閉じて消えたら作り直す。
        private void Ensure()
        {
            if (_canvas != null)
            {
                return;
            }

            var go = new GameObject("[実験] 体力・やられた人の案内", typeof(RectTransform));
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;

            BuildHealth(go.transform);
            BuildStruggle(go.transform);
        }

        private void BuildHealth(Transform parent)
        {
            var root = NewImage("Health", parent);
            root.color = PanelColor;
            _healthRoot = root.rectTransform;
            _healthRoot.anchorMin = new Vector2(1f, 1f);
            _healthRoot.anchorMax = new Vector2(1f, 1f);
            _healthRoot.pivot = new Vector2(1f, 1f);
            _healthRoot.anchoredPosition = new Vector2(-40f, -40f);
            _healthRoot.sizeDelta = new Vector2(HealthWidth + 40f, 84f);

            _healthLabel = NewText("Label", _healthRoot, 26, FontStyle.Bold, Color.white, HealthWidth);
            _healthLabel.alignment = TextAnchor.MiddleLeft;
            _healthLabel.rectTransform.anchoredPosition = new Vector2(0f, 18f);

            var back = NewImage("BarBack", _healthRoot);
            back.color = BarBackColor;
            var backRect = back.rectTransform;
            backRect.anchoredPosition = new Vector2(0f, -16f);
            backRect.sizeDelta = new Vector2(HealthWidth, HealthHeight);

            _healthFill = NewImage("BarFill", backRect);
            LeftAnchored(_healthFill.rectTransform, HealthHeight);
        }

        private void BuildStruggle(Transform parent)
        {
            var panel = NewImage("Struggle", parent);
            panel.color = PanelColor;
            _panel = panel.rectTransform;
            _panel.anchorMin = new Vector2(0.5f, 0f);
            _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, 150f);
            _panel.sizeDelta = new Vector2(BarWidth + 120f, 210f);

            var title = NewText("Title", _panel, 64, FontStyle.Bold, Color.white, BarWidth + 100f);
            title.text = "にぎにぎ連打で抜け出せ！";
            title.rectTransform.anchoredPosition = new Vector2(0f, 52f);

            _hint = NewText("Hint", _panel, 28, FontStyle.Bold, new Color(1f, 1f, 1f, 0.8f), BarWidth + 100f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            var back = NewImage("BarBack", _panel);
            back.color = BarBackColor;
            var backRect = back.rectTransform;
            backRect.anchoredPosition = new Vector2(0f, -62f);
            backRect.sizeDelta = new Vector2(BarWidth, BarHeight);

            _fill = NewImage("BarFill", backRect);
            LeftAnchored(_fill.rectTransform, BarHeight);
            _panel.gameObject.SetActive(false);
        }

        // 左端から右へ伸びるバー（幅は sizeDelta.x で決める）。
        private static void LeftAnchored(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, int size, FontStyle style, Color color, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(width, size + 20f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }
    }
}
