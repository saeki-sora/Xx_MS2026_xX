using UnityEngine;

namespace MS2026.Title
{
    /// <summary>
    /// 画像を差し替えても、画面上の大きさが変わらないように自動で拡大縮小する。
    /// SpriteRenderer と同じオブジェクトに付ける（動き担当の <see cref="TitleElementMotion"/> は親に付けて、大きさの担当を分ける）。
    /// 編集中も効くので、Sprite を差し替えるとシーン上ですぐ大きさが合う。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TitleSpriteFit : MonoBehaviour
    {
        public enum FitMode
        {
            [InspectorName("合わせない（画像の大きさのまま）")] None,
            [InspectorName("高さを指定")] Height,
            [InspectorName("幅を指定")] Width,
            [InspectorName("画面全体を覆う（背景用）")] CoverCamera,
        }

        [Tooltip("大きさの合わせ方。")]
        public FitMode mode = FitMode.Height;
        [Tooltip("「高さを指定」「幅を指定」のときの大きさ（ワールドの単位。カメラに映る画面の高さは約10.8）。")]
        public float size = 3f;
        [Tooltip("「画面全体を覆う」のとき、カメラの揺れや寄りで端が見えないよう少し大きくする割合（1.1 = 10%増し）。")]
        public float coverOverscan = 1.12f;
        [Tooltip("「画面全体を覆う」で使うカメラ。空ならメインカメラ。")]
        public Camera targetCamera;

        private SpriteRenderer spriteRenderer;

        private void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Fit();
        }

        private void OnValidate()
        {
            Fit();
        }

        private void LateUpdate()
        {
            // 編集中は毎フレーム合わせる（差し替え・カメラの調整にすぐついていく）。
            // 再生中は画像か画面の縦横比が変わったときだけ合わせる（カメラの寄り・揺れの演出で背景が伸び縮みしないように）。
            if (Application.isPlaying)
            {
                var cam = targetCamera != null ? targetCamera : Camera.main;
                var aspect = cam != null ? cam.aspect : 0f;
                var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
                if (sprite == fittedSprite && Mathf.Approximately(aspect, fittedAspect)) return;
                fittedSprite = sprite;
                fittedAspect = aspect;
            }
            Fit();
        }

        private Sprite fittedSprite;
        private float fittedAspect;

        public void Fit()
        {
            if (mode == FitMode.None) return;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            var sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (sprite == null) return;

            var native = sprite.bounds.size;
            if (native.x <= 0f || native.y <= 0f) return;

            float scale;
            switch (mode)
            {
                case FitMode.Height:
                    scale = size / native.y;
                    break;
                case FitMode.Width:
                    scale = size / native.x;
                    break;
                case FitMode.CoverCamera:
                {
                    var cam = targetCamera != null ? targetCamera : Camera.main;
                    if (cam == null || !cam.orthographic) return;
                    var viewHeight = cam.orthographicSize * 2f;
                    var viewWidth = viewHeight * cam.aspect;
                    scale = Mathf.Max(viewWidth / native.x, viewHeight / native.y) * Mathf.Max(1f, coverOverscan);
                    break;
                }
                default:
                    return;
            }

            var target = new Vector3(scale, scale, 1f);
            if (transform.localScale != target) transform.localScale = target;
        }
    }
}
