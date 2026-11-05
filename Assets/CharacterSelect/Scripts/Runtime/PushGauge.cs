using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の右下の「長くにぎって確定!!」のゲージ（自分の分）。
    /// ・長く握っている間、ゲージが左から伸びる（枠に描いてある文字は隠れてよい。ユーザー決定 2026-11-05）
    /// ・決定したら満タンのまま「他の社員を待ってね…」を出す。そのまま握ると取り消しに向かってゲージが減る
    /// </summary>
    [AddComponentMenu("キャラ選択/決定のゲージ (PushGauge)")]
    public sealed class PushGauge : MonoBehaviour
    {
        [Tooltip("伸びるゲージ（Image の種類を Filled・Horizontal にしておく）。")]
        public Image fill;

        [Tooltip("決定した後に出す「他の社員を待ってね…」。")]
        public Graphic waitingText;

        [Tooltip("「他の社員を待ってね…」が明るくなったり暗くなったりする速さ（1秒に何回。0 で動かない）。")]
        public float waitingPulseSpeed = 0.8f;

        [Tooltip("決定したとき・取り消したときにぽんと跳ねる部分（空ならこの物）。")]
        public RectTransform body;

        private bool _confirmed;
        private bool _hasState;
        private float _pop;
        private float _waiting;

        /// <summary>状態を表示する（毎フレーム呼ばれる）。hold = 長く握っている進み具合（0〜1）。</summary>
        public void Show(float hold, bool confirmed)
        {
            if (_hasState && confirmed != _confirmed)
            {
                _pop = 1f;
            }

            _hasState = true;
            _confirmed = confirmed;
            var dt = Time.unscaledDeltaTime;
            if (fill != null)
            {
                fill.fillAmount = confirmed ? 1f - hold : hold;
            }

            _waiting = Mathf.MoveTowards(_waiting, confirmed ? 1f : 0f, dt * 5f);
            if (waitingText != null)
            {
                var pulse = waitingPulseSpeed > 0f ? 0.8f + 0.2f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f * waitingPulseSpeed) : 1f;
                var c = waitingText.color;
                c.a = _waiting * pulse;
                waitingText.color = c;
            }

            _pop = Mathf.Max(0f, _pop - dt * 3f);
            var target = body != null ? body : (RectTransform)transform;
            target.localScale = Vector3.one * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.04f);
        }
    }
}
