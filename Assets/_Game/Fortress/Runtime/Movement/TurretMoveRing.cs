using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】行ける範囲の端に近づいたときだけ、地面に薄く出す輪（自分の色）。
    /// 自分の PC の自分の砲台にだけ出す（通信しない）。離れると消える。
    /// </summary>
    internal sealed class TurretMoveRing
    {
        private const int Segments = 96;
        private const float Width = 0.07f;
        private const float MaxAlpha = 0.5f;
        private const float FadeSharpness = 10f;

        private LineRenderer _line;
        private Material _material;
        private float _alpha;

        public void Update(Transform parent, bool visible, Vector2 center, float radius, float z, float closeness01, int player, float dt)
        {
            var target = visible ? MaxAlpha * closeness01 * closeness01 : 0f;
            _alpha = Mathf.Lerp(_alpha, target, TurretMoveMath.Blend(FadeSharpness, dt));
            if (_alpha < 0.005f)
            {
                if (_line != null)
                {
                    _line.enabled = false;
                }

                return;
            }

            EnsureLine(parent);
            _line.enabled = true;
            // カメラは +Z を向いているので、少しだけ手前（-Z）に出して床とちらつかないようにする。
            z -= 0.02f;
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, z));
            }

            var color = FortressColors.PlayerColor(player);
            color.a = _alpha;
            _line.startColor = color;
            _line.endColor = color;
        }

        public void Dispose()
        {
            if (_line != null)
            {
                Object.Destroy(_line.gameObject);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }

            _line = null;
            _material = null;
        }

        private void EnsureLine(Transform parent)
        {
            if (_line != null)
            {
                return;
            }

            var go = new GameObject("[実験] 移動できる範囲の輪");
            go.transform.SetParent(parent, false);
            _line = go.AddComponent<LineRenderer>();
            _material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            _line.sharedMaterial = _material;
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.widthMultiplier = Width;
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
        }
    }
}
