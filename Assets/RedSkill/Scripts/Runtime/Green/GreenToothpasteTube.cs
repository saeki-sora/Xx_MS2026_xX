using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 溜め（決定版「溜めB」）: スキルが使える間、緑の砲台の横に歯みがき粉のチューブが転がり、口から煙がもくもく漏れ続ける。
    /// 約1.5秒ごとに「ぼふっ」と大きな煙と光の粒が出る（チューブも少しつぶれる）。
    /// どのPCの画面でも、チューブは画面に対して同じ向き（口が右上）で置く。カットインが始まると、最後に大きくぼふっとして消える。
    /// </summary>
    internal sealed class GreenToothpasteTube
    {
        private readonly GreenMintFx _fx;
        private readonly LaserTurret _turret;
        private readonly SpriteRenderer _sprite;
        private float _appear;
        private float _smokeCarry;
        private float _puffTimer;
        private float _squash;
        private float _leaveTime = -1f;

        public GreenToothpasteTube(GreenMintFx fx, LaserTurret turret)
        {
            _fx = fx;
            _turret = turret;
            _sprite = fx.CreateSprite("ToothpasteTube", null, GreenMintFx.SortingBase - 10);
        }

        public bool IsShown { get; private set; }

        /// <summary>今の口の位置。</summary>
        public Vector2 NozzlePosition { get; private set; }

        public void Show()
        {
            if (!_fx.Definition.tube.enabled || _turret == null)
            {
                return;
            }

            if (!IsShown)
            {
                _appear = 0f;
                _puffTimer = 0.6f;
            }

            IsShown = true;
            _leaveTime = -1f;
            Place(0f);
        }

        /// <summary>消す。puff なら最後に大きくぼふっとしてから縮んで消える。</summary>
        public void Dismiss(bool puff)
        {
            if (!IsShown)
            {
                return;
            }

            if (puff)
            {
                var palette = _fx.Definition.palette;
                _fx.Smoke(NozzlePosition, 14, 1.1f, 3.4f, GreenMintUtil.ScreenUp * 1.5f, 1.3f);
                _fx.Sparkles(NozzlePosition, 14, 6f, 0.35f, palette.glow, default, 180f, 0.6f);
            }

            _leaveTime = 0f;
        }

        public void Hide()
        {
            IsShown = false;
            _leaveTime = -1f;
            _sprite.enabled = false;
        }

        public void Tick(float dt)
        {
            if (!IsShown)
            {
                return;
            }

            if (_turret == null)
            {
                Hide();
                return;
            }

            var settings = _fx.Definition.tube;
            var shrink = 1f;
            if (_leaveTime >= 0f)
            {
                _leaveTime += dt;
                shrink = 1f - Mathf.Clamp01(_leaveTime / 0.22f);
                if (shrink <= 0f)
                {
                    Hide();
                    return;
                }
            }
            else
            {
                _appear = Mathf.Min(1f, _appear + dt / Mathf.Max(0.01f, settings.appearSeconds));
            }

            _squash = Mathf.Max(0f, _squash - dt * 3f);
            Place(shrink);

            if (_leaveTime >= 0f)
            {
                return;
            }

            // 口から漏れ続ける煙（画面の上へ、右へ少し流れる）。
            var up = GreenMintUtil.ScreenUp;
            var right = GreenMintUtil.RightOf(up);
            var count = GreenMintUtil.Accumulate(ref _smokeCarry, settings.smokeRate * dt);
            if (count > 0)
            {
                _fx.Smoke(NozzlePosition, count, 0.6f, 0.3f, right * 0.5f + up * 1.7f, 2.2f);
            }

            _puffTimer -= dt;
            if (_puffTimer <= 0f)
            {
                _puffTimer = settings.puffInterval;
                _squash = 1f;
                _fx.Smoke(NozzlePosition, settings.puffSmoke, 1f, 1.6f, up * 2.2f, 1.6f);
                _fx.Sparkles(NozzlePosition + up * 1f, 8, 3f, 0.35f, _fx.Definition.palette.glow, default, 180f, 0.7f);
                FortressEffectPlayer.PlayOnce(_fx.Definition.sounds.onTubePuff, new Vector3(NozzlePosition.x, NozzlePosition.y, 0f), Quaternion.identity, _turret.playerIndex);
            }
        }

        private void Place(float shrink)
        {
            var settings = _fx.Definition.tube;
            var art = _fx.Definition.art;
            var sprite = art.tube != null ? art.tube : _fx.Art.Tube;
            var nozzle = art.tube != null ? art.tubeNozzle : GreenMintArt.TubeNozzle;
            var up = GreenMintUtil.ScreenUp;
            var right = GreenMintUtil.RightOf(up);
            var center = (Vector2)_turret.transform.position + right * settings.screenOffset.x + up * settings.screenOffset.y;
            var rotation = GreenMintUtil.FacingUp(up) * Quaternion.Euler(0f, 0f, settings.tiltDegrees);
            var width = Mathf.Max(0.01f, sprite.bounds.size.x);
            var scale = settings.length / width * GreenMintUtil.EaseOutBack(_appear) * shrink;
            // ぼふっのとき、少しつぶれて戻る。
            var squashX = 1f - 0.12f * _squash;
            var squashY = 1f + 0.1f * _squash;
            _sprite.sprite = sprite;
            _sprite.transform.SetPositionAndRotation(new Vector3(center.x, center.y, GreenMintFx.ZOffset - 0.03f), rotation);
            _sprite.transform.localScale = new Vector3(scale * squashX, scale * squashY, 1f);
            _sprite.color = Color.white;
            _sprite.enabled = scale > 0.001f;
            var local = new Vector3(nozzle.x * width * scale * squashX, nozzle.y * width * scale, 0f);
            NozzlePosition = center + (Vector2)(rotation * local);
        }
    }
}
