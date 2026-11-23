using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // 研修2（オーバーヒート）の間、フィールドに出す「熱のゲージ」（2026-11-19）。
    // どれだけ握り続けるとオーバーヒートするかが見えるように、自分の砲台のすぐ下（コア側）に横長のゲージを出す。
    // 中身は自分のキャラの色。8 割を超えると点滅し、オーバーヒートしたら満タンのまま点滅して「オーバーヒート！」。
    // 練習のシーンでは本物の砲台の場所に付いていく。絵で描いたフィールドでは、フィールドの上の真ん中に出す。
    public sealed partial class PracticeScreen
    {
        private const float HeatGaugeW = 280f;
        private const float HeatGaugeH = 26f;

        private PracticeShape _heatGauge;
        private Text _heatLabel;
        private float _heatAlpha;

        private void BuildHeatGauge()
        {
            _heatGauge = Shape("Heat Gauge", _field, Vector2.zero, new Vector2(FieldW, FieldH));
            _heatLabel = Heading("Heat Label", _field, string.Empty, 20, Vector2.zero, new Vector2(HeatGaugeW + 120f, 28f), TextAnchor.MiddleCenter, Ink);
        }

        private void TickHeatGauge()
        {
            var step = LocalStep;
            var show = PracticeSteps.Lesson(step) == 1 && !_sceneUnloadRequested;
            _heatAlpha = Mathf.MoveTowards(_heatAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            _heatGauge.Clear();
            if (_heatAlpha <= 0.01f)
            {
                _heatLabel.enabled = false;
                return;
            }

            var info = _seats[_local];
            var heat = Mathf.Clamp01(Source != null ? Source.Heat01 : _board.seats[_local].Heat01);
            var overheated = LocalOverheated;
            var center = HeatGaugePosition();
            var a = _heatAlpha;

            // 枠（暗い地に白いふち）。
            var body = PracticeShape.RoundRect(center, new Vector2(HeatGaugeW, HeatGaugeH), HeatGaugeH * 0.5f);
            _heatGauge.Fill(PracticeShape.RoundRect(center + new Vector2(3f, -4f), new Vector2(HeatGaugeW, HeatGaugeH), HeatGaugeH * 0.5f), new Color(Ink.r, Ink.g, Ink.b, 0.35f * a));
            _heatGauge.Fill(body, new Color(Ink.r * 0.7f, Ink.g * 0.7f, Ink.b * 0.7f, 0.85f * a));

            // 中身（キャラの色）。8 割を超えたら点滅、オーバーヒート中は満タンで点滅。
            var fill = overheated ? 1f : heat;
            var blink = overheated ? 0.5f + (0.5f * Mathf.Sin(_time * 16f)) : fill >= 0.8f ? 0.5f + (0.5f * Mathf.Sin(_time * 10f)) : 0f;
            var color = Color.Lerp(info.Color, Color.white, 0.45f * blink);
            color.a = a;
            var inner = HeatGaugeW - 8f;
            var w = inner * fill;
            if (w > 2f)
            {
                var left = center.x - (inner * 0.5f);
                _heatGauge.Fill(PracticeShape.RoundRect(new Vector2(left + (w * 0.5f), center.y), new Vector2(w, HeatGaugeH - 8f), (HeatGaugeH - 8f) * 0.5f), color);
            }

            // 目盛り（4 等分）と、右端の「ここでオーバーヒート」の線。
            for (var i = 1; i < 4; i++)
            {
                var x = center.x - (inner * 0.5f) + (inner * i / 4f);
                _heatGauge.Fill(PracticeShape.RoundRect(new Vector2(x, center.y), new Vector2(2f, HeatGaugeH - 12f), 1f), new Color(1f, 1f, 1f, 0.35f * a));
            }

            _heatGauge.Stroke(body, 3f, new Color(1f, 1f, 1f, a), true);
            var endX = center.x + (HeatGaugeW * 0.5f) + 6f;
            _heatGauge.Fill(PracticeShape.RoundRect(new Vector2(endX, center.y), new Vector2(5f, HeatGaugeH + 14f), 2.5f), new Color(Burn.r, Burn.g, Burn.b, a));

            // 文字（ゲージの上）。
            _heatLabel.enabled = true;
            _heatLabel.color = new Color(1f, 1f, 1f, a);
            var percent = Mathf.RoundToInt(heat * 100f);
            _heatLabel.text = overheated ? Ui.heatOverheated
                : step == PracticeStep.Cool ? PracticeUiTexts.Format(Ui.heatCoolFormat, percent)
                : heat >= 0.8f ? PracticeUiTexts.Format(Ui.heatNearFormat, percent)
                : PracticeUiTexts.Format(Ui.heatNormalFormat, percent);
            _heatLabel.rectTransform.anchoredPosition = center + new Vector2(0f, HeatGaugeH + 4f);
        }

        /// <summary>ゲージの場所（フィールドの中の位置）。練習のシーンでは自分の砲台の少し下、無ければフィールドの上の真ん中。</summary>
        private Vector2 HeatGaugePosition()
        {
            var hx = (FieldW * 0.5f) - (HeatGaugeW * 0.5f) - 16f;
            var hy = (FieldH * 0.5f) - 40f;
            var fallback = new Vector2(0f, hy - 150f);
            var driver = _sceneMode ? PracticeSceneDriver.Current : null;
            var cam = driver != null ? driver.ViewCamera : null;
            var turret = driver != null ? driver.LocalTurret : null;
            if (cam == null || turret == null)
            {
                return fallback;
            }

            var screen = cam.WorldToScreenPoint(turret.transform.position);
            if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_field, screen, null, out var p))
            {
                return fallback;
            }

            // 砲台の下（コアの側）。枠の中に収める。
            var pos = p + new Vector2(0f, -95f);
            return new Vector2(Mathf.Clamp(pos.x, -hx, hx), Mathf.Clamp(pos.y, -hy + 20f, hy - 20f));
        }
    }
}
