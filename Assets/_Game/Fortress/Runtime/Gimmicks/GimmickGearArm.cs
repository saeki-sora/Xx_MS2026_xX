using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 歯車の腕（2026-10-29）。歯車（<see cref="GimmickGear"/>）の回る部分の中に置く、経路の壁（<see cref="NavigationObstacle"/>）。
    /// 大きさは Transform の大きさ（横 = 長さ、縦 = 太さ）。当たり判定は 1×1 の箱で、大きさに合わせて伸びる（ほかの壁と同じ作り）。
    /// 見た目の 3D モデルは腕の中ではなく、回る部分の中に「隣に」置く（腕の大きさで伸びないように）。
    /// </summary>
    [AddComponentMenu("Fortress/仕掛け/歯車の腕（壁）")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(NavigationObstacle))]
    public sealed class GimmickGearArm : MonoBehaviour
    {
        [Tooltip("ON: レーザーは通り抜ける（当たり判定をトリガーにする。敵は今までどおり通れない）。OFF: レーザーも止める。")]
        public bool laserPassesThrough = true;

        /// <summary>腕の長さ（m）。</summary>
        public float Length
        {
            get => transform.localScale.x;
            set => SetSize(value, Thickness);
        }

        /// <summary>腕の太さ（m）。</summary>
        public float Thickness
        {
            get => transform.localScale.y;
            set => SetSize(Length, value);
        }

        public void SetSize(float length, float thickness)
        {
            transform.localScale = new Vector3(Mathf.Max(0.1f, length), Mathf.Max(0.1f, thickness), 1f);
            ApplyCollider();
        }

        /// <summary>当たり判定を 1×1・トリガーの設定に合わせる。</summary>
        public void ApplyCollider()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.size = Vector2.one;
                box.offset = Vector2.zero;
                box.isTrigger = laserPassesThrough;
            }

            var obstacle = GetComponent<NavigationObstacle>();
            if (obstacle != null)
            {
                obstacle.mode = NavigationObstacleMode.Block;
            }
        }

        private void OnValidate()
        {
            ApplyCollider();
        }
    }
}
