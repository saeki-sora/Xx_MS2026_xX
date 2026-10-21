using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// スキルを使える間、砲台の上に立つミントタブレットのケース。どのPCの画面でも画面の上向きに立つ。
    /// ・いつも小刻みに震え、ときどき「カシャカシャ」と跳ねて傾き、窓の中のタブレットが跳ね回る。後ろにミント色の光、まわりにキラキラと回復の十字。
    /// ・発動すると蓋がパカッと開き（ちょうつがいは左上）、中身が飛び出していく（飛ぶタブレットは <see cref="GreenMintSkillRunner"/> が出す）。その後しぼんで消える。
    /// </summary>
    internal sealed class GreenMintCase
    {
        private enum Phase
        {
            Hidden,
            Ready,
            Opening,
            Fading
        }

        /// <summary>本体の絵の高さ（幅 1 に対して）。</summary>
        private const float BodyUnits = (float)GreenMintArt.CaseBodyHeightPx / GreenMintArt.CaseWidthPx;

        /// <summary>蓋の絵の高さ（幅 1 に対して）。</summary>
        private const float LidUnits = (float)GreenMintArt.CaseLidHeightPx / GreenMintArt.CaseWidthPx;

        private const float TabletUnits = 0.19f;
        private const float OpenAngle = 125f;

        private readonly GreenMintFx _fx;
        private readonly LaserTurret _turret;
        private readonly Transform _root;
        private readonly Transform _holder;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _windowBack;
        private readonly SpriteRenderer _lid;
        private readonly SpriteRenderer[] _tablets;
        private readonly Vector2[] _tabletPos;
        private readonly Vector2[] _tabletVel;
        private readonly float[] _tabletSpin;
        private readonly MeshRenderer _aura;

        private Phase _phase = Phase.Hidden;
        private float _phaseTime;
        private float _rattleTimer;
        private float _rattleTime = 10f;
        private float _rattleSide = 1f;
        private float _sparkleCarry;
        private float _crossCarry;
        private float _flipTimer;
        private float _flip = 1f;
        private Vector2 _up = Vector2.up;

        public GreenMintCase(GreenMintFx fx, LaserTurret turret)
        {
            _fx = fx;
            _turret = turret;
            var settings = fx.Definition.tabletCase;
            var art = fx.Art;

            _root = new GameObject("MintCase") { hideFlags = HideFlags.DontSave }.transform;
            _root.SetParent(fx.Root, false);
            _holder = new GameObject("Holder") { hideFlags = HideFlags.DontSave }.transform;
            _holder.SetParent(_root, false);

            var order = settings.sortingOrder;
            _windowBack = fx.CreateSprite("WindowBack", art.CaseWindowBack, order, _holder);
            _body = fx.CreateSprite("Body", art.CaseBody, order + 2, _holder);
            _lid = fx.CreateSprite("Lid", art.CaseLid, order + 3, _holder);
            _lid.transform.localPosition = new Vector3(-0.5f, BodyUnits - 0.01f, -0.001f);

            var count = Mathf.Clamp(settings.tabletsInside, 0, 10);
            _tablets = new SpriteRenderer[count];
            _tabletPos = new Vector2[count];
            _tabletVel = new Vector2[count];
            _tabletSpin = new float[count];
            for (var i = 0; i < count; i++)
            {
                _tablets[i] = fx.CreateSprite("Inside" + i, art.Tablet, order + 1, _holder);
            }

            _aura = fx.CreateQuad("CaseAura", fx.RadialMaterial, order - 1);
            Hide();
        }

        public bool IsShown => _phase != Phase.Hidden;

        /// <summary>ケースの口（蓋のすぐ下）の位置。飛び出すタブレットはここから出る。</summary>
        public Vector3 MouthPosition
        {
            get
            {
                var settings = _fx.Definition.tabletCase;
                return _root.position + (Vector3)(_up * settings.height * 0.88f);
            }
        }

        /// <summary>使える状態になった（ポンと弾んで出る）。</summary>
        public void Show()
        {
            if (!_fx.Definition.tabletCase.enabled || _turret == null)
            {
                return;
            }

            _phase = Phase.Ready;
            _phaseTime = 0f;
            _rattleTimer = 0.4f;
            _lid.transform.localRotation = Quaternion.identity;
            ScatterTablets();
            Place(0f);
            var palette = _fx.Definition.palette;
            var center = (Vector2)_root.position + _up * _fx.Definition.tabletCase.height * 0.5f;
            _fx.WaveAt(center, 0.5f, 4.2f, 0.55f, new Color(palette.mint.r, palette.mint.g, palette.mint.b, 0.85f));
            _fx.Sparkles(center, 26, 7f, 0.35f, palette.glow);
            _fx.Leaves(center, 3, 0.5f, 3f);
        }

        /// <summary>発動: 蓋が開いて中身が飛び出し、少しして消える。</summary>
        public void Open()
        {
            if (_phase == Phase.Hidden)
            {
                if (!_fx.Definition.tabletCase.enabled || _turret == null)
                {
                    return;
                }

                // 溜めを見せずに撃った（テストの「今すぐ撃つ」など）ときも、ケースが開くところは見せる。
                Show();
            }

            _phase = Phase.Opening;
            _phaseTime = 0f;
            var palette = _fx.Definition.palette;
            _fx.FlashAt(MouthPosition, 2.4f, 0.35f, palette.glow, 8, 0.5f);
            _fx.Sparkles(MouthPosition, 30, 9f, 0.3f, palette.glow, _up, 70f);
            _fx.Powder(MouthPosition, 6, 0.6f, 3f);
        }

        /// <summary>使える状態が取り消された（すぐしぼんで消える）。</summary>
        public void Dismiss()
        {
            if (_phase == Phase.Ready)
            {
                _phase = Phase.Fading;
                _phaseTime = 0f;
            }
        }

        public void Hide()
        {
            _phase = Phase.Hidden;
            _body.enabled = false;
            _windowBack.enabled = false;
            _lid.enabled = false;
            for (var i = 0; i < _tablets.Length; i++)
            {
                _tablets[i].enabled = false;
            }

            _aura.enabled = false;
        }

        public void Tick(float dt)
        {
            if (_phase == Phase.Hidden)
            {
                return;
            }

            if (_turret == null || !_turret.isActiveAndEnabled)
            {
                Hide();
                return;
            }

            var settings = _fx.Definition.tabletCase;
            _phaseTime += dt;
            var scale = 1f;
            var alpha = 1f;
            switch (_phase)
            {
                case Phase.Ready:
                    scale = GreenMintUtil.EaseOutBack(_phaseTime / settings.appearSeconds, 2.2f);
                    TickRattle(dt, 1f);
                    TickTablets(dt);
                    EmitReadyFx(dt);
                    break;
                case Phase.Opening:
                {
                    var open = GreenMintUtil.EaseOutBack(_phaseTime / settings.lidOpenSeconds, 2.5f);
                    _lid.transform.localRotation = Quaternion.Euler(0f, 0f, OpenAngle * open);
                    TickRattle(dt, 0.4f);
                    var hold = settings.lidOpenSeconds + 0.3f;
                    if (_phaseTime > hold)
                    {
                        _phase = Phase.Fading;
                        _phaseTime = 0f;
                    }

                    break;
                }
                case Phase.Fading:
                {
                    var t = _phaseTime / settings.fadeOutSeconds;
                    if (t >= 1f)
                    {
                        Hide();
                        return;
                    }

                    // 少し縮んでから、すっと消える。
                    scale = Mathf.Lerp(1f, 0.55f, GreenMintUtil.EaseOutCubic(t));
                    alpha = 1f - t * t;
                    break;
                }
            }

            Place(scale);
            Apply(alpha, scale);
        }

        // ───────────── 中身 ─────────────

        /// <summary>ケースを砲台の上に置く（画面の上向き・震え・カシャカシャの跳ねと傾き）。</summary>
        private void Place(float scale)
        {
            var settings = _fx.Definition.tabletCase;
            _up = GreenMintUtil.ScreenUp;
            var side = new Vector2(_up.y, -_up.x);

            // いつもの震え: 速く左右に（ブルブル）。
            _flipTimer -= Time.deltaTime;
            if (_flipTimer <= 0f)
            {
                _flipTimer = 1f / 45f;
                _flip = -_flip;
            }

            var tremble = side * (_flip * settings.trembleAmount * Random.Range(0.6f, 1f));

            // カシャカシャ: 跳ねて、左右に傾く。
            var rattle = Mathf.Clamp01(_rattleTime / 0.22f);
            var jump = rattle < 1f ? Mathf.Sin(rattle * Mathf.PI) * settings.rattleJump.x : 0f;
            var tilt = rattle < 1f ? Mathf.Sin(rattle * Mathf.PI * 2f) * settings.rattleJump.y * _rattleSide * (1f - rattle) : 0f;

            var basePosition = (Vector2)_turret.transform.position + _up * (settings.screenUpOffset - settings.height * 0.5f);
            var position = basePosition + tremble + _up * jump;
            _root.SetPositionAndRotation(new Vector3(position.x, position.y, GreenMintFx.ZOffset - 0.02f),
                GreenMintUtil.FacingUp(_up) * Quaternion.Euler(0f, 0f, tilt));
            var totalUnits = BodyUnits + LidUnits;
            _holder.localScale = new Vector3(settings.width * scale, settings.height / totalUnits * scale, 1f);
        }

        private void Apply(float alpha, float scale)
        {
            var settings = _fx.Definition.tabletCase;
            var color = new Color(1f, 1f, 1f, alpha);
            _body.color = color;
            _windowBack.color = color;
            _lid.color = color;
            _body.enabled = true;
            _windowBack.enabled = true;
            _lid.enabled = true;

            var showInside = _phase == Phase.Ready;
            for (var i = 0; i < _tablets.Length; i++)
            {
                var tablet = _tablets[i];
                tablet.enabled = showInside;
                if (!showInside)
                {
                    continue;
                }

                // 中のタブレットは丸のまま（入れ物の縦横の倍率を打ち消す）。
                var holderScale = _holder.localScale;
                var size = TabletUnits * settings.width;
                tablet.transform.localPosition = new Vector3(_tabletPos[i].x, _tabletPos[i].y, -0.0005f);
                tablet.transform.localRotation = Quaternion.Euler(0f, 0f, _tabletSpin[i]);
                tablet.transform.localScale = new Vector3(size / Mathf.Max(holderScale.x, 1e-4f) * scale, size / Mathf.Max(holderScale.y, 1e-4f) * scale, 1f);
                tablet.color = color;
            }

            // 後ろの光（ゆっくり脈打つ。カシャカシャの瞬間は強く）。
            var palette = _fx.Definition.palette;
            var rattleFlash = Mathf.Clamp01(1f - _rattleTime / 0.3f);
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4.2f) + rattleFlash * 0.5f;
            var auraSize = settings.height * settings.auraScale * scale * (0.92f + 0.08f * pulse);
            var center = (Vector2)_root.position + _up * settings.height * 0.5f * scale;
            _aura.transform.SetPositionAndRotation(new Vector3(center.x, center.y, GreenMintFx.ZOffset), Quaternion.identity);
            _aura.transform.localScale = new Vector3(auraSize, auraSize, 1f);
            _fx.ApplyRadial(_aura, palette.glow * (0.55f * pulse * alpha),
                new Vector4(0.62f, 1.6f, 10f, 0.25f), new Vector4(4f, Time.time * 0.6f, 0.82f, 0.06f), new Vector4(0.45f * pulse, 0f, 3f, 0.6f));
            _aura.enabled = alpha > 0.01f;
        }

        private void TickRattle(float dt, float strength)
        {
            var settings = _fx.Definition.tabletCase;
            _rattleTime += dt;
            _rattleTimer -= dt;
            if (_rattleTimer > 0f)
            {
                return;
            }

            _rattleTimer = Random.Range(settings.rattleIntervalMin, Mathf.Max(settings.rattleIntervalMin, settings.rattleIntervalMax));
            _rattleTime = 0f;
            _rattleSide = Random.value < 0.5f ? -1f : 1f;
            for (var i = 0; i < _tablets.Length; i++)
            {
                _tabletVel[i] += new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(2.2f, 3.6f)) * strength;
            }

            if (strength >= 1f && _phase == Phase.Ready)
            {
                var palette = _fx.Definition.palette;
                var top = (Vector2)_root.position + _up * settings.height;
                _fx.Sparkles(top, 6, 4f, 0.28f, palette.glow, _up, 60f, 0.4f);
            }
        }

        /// <summary>窓の中のタブレット: 下へ落ちて、壁で跳ね返る（ケースの中の座標で計算）。</summary>
        private void TickTablets(float dt)
        {
            var window = GreenMintArt.CaseWindow;
            var radius = TabletUnits * 0.5f;
            var minX = window.xMin - 0.5f + radius;
            var maxX = window.xMax - 0.5f - radius;
            var minY = window.yMin * BodyUnits + radius;
            var maxY = window.yMax * BodyUnits - radius;
            for (var i = 0; i < _tablets.Length; i++)
            {
                var velocity = _tabletVel[i];
                velocity.y -= 9f * dt;
                var position = _tabletPos[i] + velocity * dt;
                if (position.y < minY)
                {
                    position.y = minY;
                    velocity.y = Mathf.Abs(velocity.y) * 0.35f;
                    velocity.x *= 0.7f;
                }
                else if (position.y > maxY)
                {
                    position.y = maxY;
                    velocity.y = -Mathf.Abs(velocity.y) * 0.5f;
                }

                if (position.x < minX || position.x > maxX)
                {
                    position.x = Mathf.Clamp(position.x, minX, maxX);
                    velocity.x = -velocity.x * 0.6f;
                }

                _tabletPos[i] = position;
                _tabletVel[i] = velocity;
                _tabletSpin[i] += velocity.x * 220f * dt;
            }
        }

        private void ScatterTablets()
        {
            var window = GreenMintArt.CaseWindow;
            for (var i = 0; i < _tablets.Length; i++)
            {
                _tabletPos[i] = new Vector2(Random.Range(window.xMin, window.xMax) - 0.5f, Random.Range(window.yMin, window.yMax) * BodyUnits);
                _tabletVel[i] = Vector2.zero;
                _tabletSpin[i] = Random.Range(0f, 360f);
            }
        }

        private void EmitReadyFx(float dt)
        {
            var settings = _fx.Definition.tabletCase;
            var palette = _fx.Definition.palette;
            var center = (Vector2)_root.position + _up * settings.height * 0.5f;
            var sparkles = GreenMintUtil.Accumulate(ref _sparkleCarry, settings.sparkleRate * dt);
            for (var i = 0; i < sparkles; i++)
            {
                var point = center + Random.insideUnitCircle.normalized * settings.height * Random.Range(0.45f, 0.75f);
                _fx.Sparkles(point, 1, 0.8f, 0.3f, palette.glow, _up, 30f, 0.7f);
            }

            var crosses = GreenMintUtil.Accumulate(ref _crossCarry, settings.crossRate * dt);
            if (crosses > 0)
            {
                _fx.Crosses(center + _up * settings.height * 0.3f, crosses, 0.42f, settings.width * 0.6f, _up);
            }
        }
    }
}
