using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 能力の棒グラフ1本（名前＋棒）。プレイヤーの区画の見本（StatTemplate）を、キャラの能力の数だけ複製して使う。
    /// 棒はキャラが変わると、新しい長さまでするっと伸び縮みする。
    /// </summary>
    [AddComponentMenu("キャラ選択/能力の棒グラフ (StatBar)")]
    public sealed class StatBar : MonoBehaviour
    {
        [Tooltip("能力の名前（例: パワー）。キャラのファイルで名前が空なら隠す。")]
        public Text label;

        [Tooltip("伸び縮みする棒（Image の種類を Filled・Horizontal にしておく）。")]
        public Image fill;

        [Tooltip("伸び縮みの速さ（1秒で棒の何本分）。")]
        public float speed = 3f;

        private float _target;

        public void Set(string text, float value)
        {
            if (label != null)
            {
                var hasText = !string.IsNullOrEmpty(text);
                if (label.gameObject.activeSelf != hasText)
                {
                    label.gameObject.SetActive(hasText);
                }

                if (hasText && label.text != text)
                {
                    label.text = text;
                }
            }

            _target = Mathf.Clamp01(value);
        }

        private void Update()
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, _target, Time.unscaledDeltaTime * speed);
            }
        }
    }
}
