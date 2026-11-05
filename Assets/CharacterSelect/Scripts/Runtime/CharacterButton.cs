using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 画面の下のキャラのボタン1つ。自分のカーソルが乗ると「on」の絵（大きく光る）に替わり、するっと大きくなる。
    /// 絵はキャラのファイルの「ボタン（on）」「ボタン（off）」。空なら入れてある絵のまま。
    /// </summary>
    [AddComponentMenu("キャラ選択/キャラのボタン1つ (CharacterButton)")]
    public sealed class CharacterButton : MonoBehaviour
    {
        [Tooltip("ボタンの絵。")]
        public Image image;

        [Tooltip("自分のカーソルが乗っていないときの大きさ（ピクセル）。")]
        public Vector2 offSize = new Vector2(177f, 177f);

        [Tooltip("自分のカーソルが乗っているときの大きさ（ピクセル。on の絵は光の分だけ大きい）。")]
        public Vector2 onSize = new Vector2(265f, 265f);

        [Tooltip("大きさが替わる速さ（大きいほど速い）。")]
        public float resizeSpeed = 16f;

        private RectTransform _rect;
        private Vector2 _size;
        private bool _sized;
        private float _punch;
        private float _pop;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>今の見た目の左上の角（親の中の位置）。札をここに付ける。</summary>
        public Vector2 TopLeft => Rect.anchoredPosition + new Vector2(-CurrentSize.x * 0.5f, CurrentSize.y * 0.5f);

        /// <summary>今の見た目の右の端の真ん中（親の中の位置）。矢印をここに付ける。</summary>
        public Vector2 RightMiddle => Rect.anchoredPosition + new Vector2(CurrentSize.x * 0.5f, 0f);

        private Vector2 CurrentSize => _sized ? _size : offSize;

        public void Show(CharacterDefinition character, bool on, Color tint)
        {
            if (image == null)
            {
                return;
            }

            var sprite = character != null ? (on ? character.buttonOn : character.buttonOff) : null;
            if (sprite != null && image.sprite != sprite)
            {
                image.sprite = sprite;
            }

            image.color = tint;
            var target = on ? onSize : offSize;
            _size = _sized ? Vector2.Lerp(_size, target, 1f - Mathf.Exp(-resizeSpeed * Time.unscaledDeltaTime)) : target;
            _sized = true;
            var bounce = 1f + Mathf.Sin(_punch * Mathf.PI) * 0.06f + Mathf.Sin(_pop * Mathf.PI) * 0.14f;
            image.rectTransform.sizeDelta = _size * bounce;
        }

        /// <summary>カーソルが来たとき（小さく跳ねる）。</summary>
        public void Punch() => _punch = 1f;

        /// <summary>決定したとき（大きく跳ねる）。</summary>
        public void Pop() => _pop = 1f;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _punch = Mathf.Max(0f, _punch - dt * 5f);
            _pop = Mathf.Max(0f, _pop - dt * 2.5f);
        }
    }
}
