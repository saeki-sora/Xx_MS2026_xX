using UnityEngine;
using UnityEngine.UI;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 柄（RawImage の絵）をゆっくり流し続ける（区画の斜めのしま模様など）。
    /// 絵は「繰り返し（Wrap Mode = Repeat）」の設定にしておく。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [AddComponentMenu("キャラ選択/演出/流れる柄 (CharacterSelectScroll)")]
    public sealed class CharacterSelectScroll : MonoBehaviour
    {
        [Tooltip("流れる速さ（1秒に絵の何枚分。x = 横、y = 縦）。")]
        public Vector2 speed = new Vector2(0.15f, 0.15f);

        [Tooltip("絵を何枚分並べるか（大きいほど柄が細かい）。空のときは区画の大きさ÷128 にする。")]
        public Vector2 tiles = Vector2.zero;

        private RawImage _image;
        private Vector2 _offset;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            if (tiles == Vector2.zero)
            {
                var size = ((RectTransform)transform).rect.size;
                tiles = new Vector2(Mathf.Max(1f, size.x / 128f), Mathf.Max(1f, size.y / 128f));
            }
        }

        private void Update()
        {
            _offset += speed * Time.unscaledDeltaTime;
            _offset.x = Mathf.Repeat(_offset.x, 1f);
            _offset.y = Mathf.Repeat(_offset.y, 1f);
            _image.uvRect = new Rect(_offset, tiles);
        }
    }
}
