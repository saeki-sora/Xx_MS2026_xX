using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ】倒れた砲台の見た目（全員の画面。各PCが自分で描く・通信しない。仮の見た目）。
    /// ・菌のべったりした汚れ: 砲台にかぶさる、ふちがでこぼこした濁った緑の塊。倒れた瞬間にべちゃっと広がり、ゆっくりうごめく。復活でしぼんで消える。
    /// ・煙: くすんだ緑灰色の煙が、もくもくと画面の上（カメラの方）へ立ちのぼる。復活すると出なくなり、出ていた分は消えていく。
    /// 色は「菌の色」だけ（プレイヤーの色は使わない。倒れた人の色あせは <see cref="TurretFade"/>）。絵はコードで作る（外の画像は使わない）。
    /// </summary>
    internal sealed class TurretDownedFx
    {
        private const float PopSeconds = 0.28f;
        private const float ShrinkSeconds = 0.3f;
        private static readonly Color GooColor = new Color(0.30f, 0.36f, 0.13f, 0.92f);
        private static readonly Color SmokeA = new Color(0.36f, 0.40f, 0.30f, 0.85f);
        private static readonly Color SmokeB = new Color(0.22f, 0.25f, 0.17f, 0.85f);

        private static Sprite _gooSprite;
        private static Texture2D _smokeTexture;

        private SpriteRenderer _goo;
        private ParticleSystem _smoke;
        private Material _smokeMaterial;
        private float _gooAmount; // 0 = 出ていない、1 = 出きった
        private bool _wasDowned;
        private float _sinceDowned;
        private float _seed;

        public void Update(Transform parent, LaserTurret turret, bool downed, float bodyRadius, float dt)
        {
            var settings = TurretMoveSettings.Load();
            if (turret == null)
            {
                Hide();
                return;
            }

            if (downed && !_wasDowned)
            {
                _sinceDowned = 0f;
                _seed = Random.value * 100f;
            }

            _wasDowned = downed;
            _sinceDowned += dt;

            UpdateGoo(parent, turret, downed && settings.downedGoo, bodyRadius, dt);
            UpdateSmoke(parent, turret, downed && settings.downedSmoke, bodyRadius);
        }

        public void Dispose()
        {
            if (_goo != null)
            {
                Object.Destroy(_goo.gameObject);
            }

            if (_smoke != null)
            {
                Object.Destroy(_smoke.gameObject);
            }

            if (_smokeMaterial != null)
            {
                Object.Destroy(_smokeMaterial);
            }

            _goo = null;
            _smoke = null;
            _smokeMaterial = null;
            _gooAmount = 0f;
            _wasDowned = false;
        }

        private void Hide()
        {
            _wasDowned = false;
            _gooAmount = 0f;
            if (_goo != null)
            {
                _goo.enabled = false;
            }

            if (_smoke != null && _smoke.isEmitting)
            {
                _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ───────── 菌の汚れ ─────────

        private void UpdateGoo(Transform parent, LaserTurret turret, bool show, float bodyRadius, float dt)
        {
            _gooAmount = show
                ? Mathf.MoveTowards(_gooAmount, 1f, dt / PopSeconds)
                : Mathf.MoveTowards(_gooAmount, 0f, dt / ShrinkSeconds);
            if (_gooAmount <= 0f)
            {
                if (_goo != null)
                {
                    _goo.enabled = false;
                }

                return;
            }

            EnsureGoo(parent);
            _goo.enabled = true;

            // べちゃっと広がる（行き過ぎてから戻る）→ ゆっくりうごめく。
            var pop = show ? EaseOutBack(_gooAmount) : _gooAmount * _gooAmount;
            var time = _sinceDowned + _seed;
            var wobbleX = 1f + 0.06f * Mathf.Sin(time * 2.3f);
            var wobbleY = 1f + 0.06f * Mathf.Sin(time * 1.7f + 1.3f);
            var size = bodyRadius * 3.1f * pop;
            var t = _goo.transform;
            var position = turret.transform.position;
            // カメラは +Z を向いているので、少し手前（-Z）に出して砲台の絵の上にかぶせる。
            t.position = new Vector3(position.x, position.y, position.z - 0.08f);
            t.rotation = Quaternion.Euler(0f, 0f, _seed * 37f + time * 6f);
            t.localScale = new Vector3(size * wobbleX, size * wobbleY, 1f);

            var color = GooColor;
            color.a *= Mathf.Clamp01(_gooAmount * 1.5f);
            _goo.color = color;
        }

        private void EnsureGoo(Transform parent)
        {
            if (_goo != null)
            {
                return;
            }

            var go = new GameObject("[実験] 倒れた砲台の菌の汚れ");
            go.transform.SetParent(parent, false);
            _goo = go.AddComponent<SpriteRenderer>();
            _goo.sprite = GooSprite();
            _goo.sortingOrder = 50;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.9f;
            const float c3 = c1 + 1f;
            var t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        // ふちがでこぼこした塊（白。色は SpriteRenderer で付ける）。中ほどは少し暗く、左上につやを入れて、べったり感を出す。
        private static Sprite GooSprite()
        {
            if (_gooSprite != null)
            {
                return _gooSprite;
            }

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    var d = Mathf.Sqrt(u * u + v * v);
                    var a = Mathf.Atan2(v, u);
                    var edge = 0.74f + 0.09f * Mathf.Sin(3f * a + 0.7f) + 0.06f * Mathf.Sin(5f * a + 2.1f) + 0.04f * Mathf.Sin(9f * a + 4.0f);
                    var alpha = Mathf.Clamp01((edge - d) / 0.06f);

                    // 小さな飛び散り（ふちの外の粒）
                    for (var k = 0; k < 5; k++)
                    {
                        var ka = k * 1.257f + 0.4f;
                        var kr = 0.86f + 0.05f * (k % 2);
                        var dx = u - Mathf.Cos(ka) * kr;
                        var dy = v - Mathf.Sin(ka) * kr;
                        var dot = Mathf.Clamp01((0.075f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.03f);
                        alpha = Mathf.Max(alpha, dot);
                    }

                    var shade = Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(1f - d / Mathf.Max(0.01f, edge)));
                    var hx = u + 0.3f;
                    var hy = v - 0.32f;
                    var gloss = Mathf.Clamp01((0.22f - Mathf.Sqrt(hx * hx + hy * hy)) / 0.12f) * 0.55f;
                    var c = Mathf.Clamp01(shade + gloss);
                    pixels[y * size + x] = new Color(c, c, c, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _gooSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _gooSprite.hideFlags = HideFlags.HideAndDontSave;
            return _gooSprite;
        }

        // ───────── 煙 ─────────

        private void UpdateSmoke(Transform parent, LaserTurret turret, bool show, float bodyRadius)
        {
            if (!show)
            {
                if (_smoke != null && _smoke.isEmitting)
                {
                    _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                return;
            }

            EnsureSmoke(parent, bodyRadius);
            var position = turret.transform.position;
            _smoke.transform.position = new Vector3(position.x, position.y, position.z - 0.3f);

            // 自分の画面の上へ流す（視点が回っている PC でも、煙は画面の上へのぼる）。
            var up = FortressSeatMover.ScreenUp() * 0.9f;
            var velocity = _smoke.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(up.x);
            velocity.y = new ParticleSystem.MinMaxCurve(up.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.3f);

            if (!_smoke.isEmitting)
            {
                _smoke.Play(true);
            }
        }

        private void EnsureSmoke(Transform parent, float bodyRadius)
        {
            if (_smoke != null)
            {
                return;
            }

            var go = new GameObject("[実験] 倒れた砲台の煙");
            go.transform.SetParent(parent, false);
            // 円錐の向き（+Z）をカメラの方（-Z）へ向ける。
            go.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            _smoke = go.AddComponent<ParticleSystem>();
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(bodyRadius * 0.9f, bodyRadius * 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(SmokeA, SmokeB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = _smoke.emission;
            emission.rateOverTime = 12f;

            var shape = _smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = bodyRadius * 0.6f;

            var velocity = _smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var sizeOverLife = _smoke.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));

            var colorOverLife = _smoke.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.15f), new GradientAlphaKey(0.45f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            var rotationOverLife = _smoke.rotationOverLifetime;
            rotationOverLife.enabled = true;
            rotationOverLife.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            _smokeMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave, mainTexture = SmokeTexture() };
            renderer.sharedMaterial = _smokeMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 60;
        }

        // ふわっとした丸い煙のかたまり（白。色は粒の色で付ける）。
        private static Texture2D SmokeTexture()
        {
            if (_smokeTexture != null)
            {
                return _smokeTexture;
            }

            const int size = 64;
            _smokeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size * 2f - 1f;
                    var v = (y + 0.5f) / size * 2f - 1f;
                    var d = Mathf.Sqrt(u * u + v * v);
                    var a = Mathf.Atan2(v, u);
                    var lumpy = 0.85f + 0.08f * Mathf.Sin(4f * a + 1.1f) + 0.05f * Mathf.Sin(7f * a + 0.3f);
                    var alpha = Mathf.Clamp01(1f - d / lumpy);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _smokeTexture.SetPixels32(pixels);
            _smokeTexture.Apply(false, true);
            return _smokeTexture;
        }
    }
}
