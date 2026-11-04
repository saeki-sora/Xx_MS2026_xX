using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>画面のふちの靄の計算のうち、Unity の描画に頼らない部分（テストできるように分けてある）。</summary>
    public static class EdgeMistMath
    {
        /// <summary>コアが画面の端ぎりぎり・外・カメラの後ろにあっても靄の形が崩れないよう、画面の内側に収める余白。</summary>
        public const float CoreMargin = 0.02f;

        /// <summary>
        /// コアの画面上の位置（Camera.WorldToViewportPoint の結果）を、靄の中心に使える 0〜1 の位置にする。
        /// カメラの後ろ（z が0以下）なら画面の真ん中を使う。
        /// </summary>
        public static Vector2 CoreViewport(Vector3 viewport)
        {
            if (viewport.z <= 0f || float.IsNaN(viewport.x) || float.IsNaN(viewport.y))
            {
                return new Vector2(0.5f, 0.5f);
            }

            return new Vector2(
                Mathf.Clamp(viewport.x, CoreMargin, 1f - CoreMargin),
                Mathf.Clamp(viewport.y, CoreMargin, 1f - CoreMargin));
        }

        /// <summary>
        /// シェーダー（FortressEdgeMist.hlsl）と同じ考え方で、画素の位置が「コア(0)〜画面の端(1)」のどこにあるか。
        /// ゆらぎを除いた値。テストと、エディタでの説明用。
        /// </summary>
        public static float EdgeRatio(Vector2 uv, Vector2 core, float roundness)
        {
            var d = uv - core;
            var roomX = d.x >= 0f ? 1f - core.x : core.x;
            var roomY = d.y >= 0f ? 1f - core.y : core.y;
            var qx = Mathf.Abs(d.x) / Mathf.Max(roomX, 1e-4f);
            var qy = Mathf.Abs(d.y) / Mathf.Max(roomY, 1e-4f);
            var p = Mathf.Max(roundness, 1f);
            return Mathf.Pow(Mathf.Pow(qx, p) + Mathf.Pow(qy, p), 1f / p);
        }

        /// <summary>
        /// 靄の中心を、今の値から目標へなめらかに寄せる（カメラの切り替え・揺れで靄がガクッと動かないように）。
        /// <paramref name="sharpness"/> は1秒あたりの寄り方（大きいほどすぐ追いつく）。
        /// </summary>
        public static Vector2 Follow(Vector2 current, Vector2 target, float sharpness, float deltaTime)
        {
            var t = 1f - Mathf.Exp(-Mathf.Max(sharpness, 0f) * Mathf.Max(deltaTime, 0f));
            return Vector2.Lerp(current, target, t);
        }
    }
}
