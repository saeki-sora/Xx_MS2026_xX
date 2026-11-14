using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】砲台のまわりに出す、やられる・復活の目印（仮の見た目。全PCが自分で描く）。
    /// ・ふつう: 汚れが溜まるほど、くすんだ緑の弧が時計回りに伸びる（7割を超えると点滅）
    /// ・倒れている: 濁った太い輪がゆっくり脈打つ ＋ 復活の進み具合の弧（プレイヤーの色）
    /// ・復活の直後: プレイヤーの色の輪が点滅する（やられない間）
    /// 足すのは輪だけにしてある（色は「菌の色」と「プレイヤーの色」の2つだけ）。
    /// </summary>
    internal sealed class TurretDownView
    {
        private const int Segments = 64;
        private static readonly Color DirtColor = new Color(0.55f, 0.62f, 0.25f, 1f);
        private static readonly Color GooColor = new Color(0.33f, 0.38f, 0.16f, 1f);

        private readonly RingLine _base = new RingLine();
        private readonly RingLine _arc = new RingLine();

        public void Update(Transform parent, LaserTurret turret, TurretDownState state, float bodyRadius, int player)
        {
            if (turret == null)
            {
                Hide();
                return;
            }

            var center = (Vector2)turret.transform.position;
            // カメラは +Z を向いているので、少しだけ手前（-Z）に出して砲台の絵の上に重ねる。
            var z = turret.transform.position.z - 0.05f;
            var radius = bodyRadius + 0.3f;
            var time = Time.unscaledTime;
            var playerColor = FortressColors.PlayerColor(player);

            switch (state.Condition)
            {
                case TurretCondition.Downed:
                {
                    var pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
                    _base.Draw(parent, center, radius, z, 1f, 0.32f + 0.06f * pulse, WithAlpha(GooColor, 0.75f + 0.2f * pulse));
                    _arc.Draw(parent, center, radius + 0.28f, z, state.Revive01, 0.14f, WithAlpha(playerColor, 0.95f));
                    break;
                }

                case TurretCondition.Protected:
                {
                    var blink = Mathf.Repeat(time * 6f, 1f) < 0.5f ? 0.85f : 0.25f;
                    _base.Draw(parent, center, radius, z, 1f, 0.1f, WithAlpha(playerColor, blink));
                    _arc.Hide();
                    break;
                }

                default:
                {
                    _base.Hide();
                    if (state.Dirt01 < 0.02f)
                    {
                        _arc.Hide();
                        break;
                    }

                    var alpha = Mathf.Lerp(0.45f, 0.95f, state.Dirt01);
                    if (state.Dirt01 > 0.7f && Mathf.Repeat(time * 8f, 1f) < 0.35f)
                    {
                        alpha *= 0.35f; // もうすぐ倒れる
                    }

                    _arc.Draw(parent, center, radius, z, state.Dirt01, 0.13f, WithAlpha(DirtColor, alpha));
                    break;
                }
            }
        }

        public void Hide()
        {
            _base.Hide();
            _arc.Hide();
        }

        public void Dispose()
        {
            _base.Dispose();
            _arc.Dispose();
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>円・弧を1本の LineRenderer で描く（弧は真上から時計回り）。</summary>
        private sealed class RingLine
        {
            private LineRenderer _line;
            private Material _material;

            public void Draw(Transform parent, Vector2 center, float radius, float z, float fraction01, float width, Color color)
            {
                fraction01 = Mathf.Clamp01(fraction01);
                if (fraction01 <= 0.001f)
                {
                    Hide();
                    return;
                }

                Ensure(parent);
                _line.enabled = true;
                var full = fraction01 >= 0.999f;
                _line.loop = full;
                var count = full ? Segments : Mathf.Max(2, Mathf.CeilToInt(Segments * fraction01) + 1);
                _line.positionCount = count;
                for (var i = 0; i < count; i++)
                {
                    var t = full ? i / (float)Segments : i / (float)(count - 1) * fraction01;
                    var angle = Mathf.PI * 0.5f - t * Mathf.PI * 2f; // 真上から時計回り
                    _line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, z));
                }

                _line.widthMultiplier = width;
                _line.startColor = color;
                _line.endColor = color;
            }

            public void Hide()
            {
                if (_line != null)
                {
                    _line.enabled = false;
                }
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

            private void Ensure(Transform parent)
            {
                if (_line != null)
                {
                    return;
                }

                var go = new GameObject("[実験] やられる・復活の輪");
                go.transform.SetParent(parent, false);
                _line = go.AddComponent<LineRenderer>();
                _material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                _line.sharedMaterial = _material;
                _line.useWorldSpace = true;
                _line.numCapVertices = 2;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
            }
        }
    }
}
