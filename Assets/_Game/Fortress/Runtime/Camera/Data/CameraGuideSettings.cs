using System;
using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>
    /// 画面構図の確認用ガイド（基準解像度・安全域・中心線・三分割線）の設定。
    /// エディタのプレビューと、Development Buildのデバッグ表示の両方が同じ設定を使う。
    /// </summary>
    [Serializable]
    public sealed class CameraGuideSettings
    {
        [Tooltip("本番の画面解像度。エディタのプレビューはこの縦横比で描く。")]
        public Vector2Int referenceResolution = new Vector2Int(1920, 1080);

        [Tooltip("安全域の大きさ(画面に対する割合)。モニターの縁や額縁で欠けても困らない範囲。自分の砲台などはこの内側に入れたい。")]
        [Range(0.5f, 1f)]
        public float safeAreaRatio = 0.9f;

        [Tooltip("画面中心の十字を表示する。")]
        public bool showCenterCross = true;

        [Tooltip("三分割線を表示する。")]
        public bool showThirds;

        public float ReferenceAspect => referenceResolution.y > 0
            ? Mathf.Max(0.1f, referenceResolution.x / (float)referenceResolution.y)
            : 16f / 9f;

        /// <summary>画面(またはプレビュー枠)の矩形から、安全域の矩形を求める。</summary>
        public Rect GetSafeRect(Rect screen)
        {
            var ratio = Mathf.Clamp(safeAreaRatio, 0.01f, 1f);
            var size = screen.size * ratio;
            return new Rect(screen.center - size * 0.5f, size);
        }

        /// <summary>ビューポート座標(0-1)が安全域に入っているか。</summary>
        public bool IsInSafeArea(Vector2 viewport01)
        {
            return GetSafeRect(new Rect(0f, 0f, 1f, 1f)).Contains(viewport01);
        }
    }
}
