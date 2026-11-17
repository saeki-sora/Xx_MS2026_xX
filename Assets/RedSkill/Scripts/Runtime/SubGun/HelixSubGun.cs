using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 赤の副砲「螺旋ツイン」: 細いレーザー2本が、本体のまわりを二重らせんで巻きつく（歯みがき粉のしま模様のイメージ）。
    /// 強く握るほど巻きが細かく速くなり、太くなった本体に合わせて輪も広がる。
    /// 当たり: 本体のふちから外側へ「らせんの幅」までの帯（らせんは速く回るので、帯全体に当たっていると考える）。
    /// 帯のうち本体の外で敵に当たった所には、キャラの色の星形の光を出す（らせんが当てたと分かるように）。
    /// </summary>
    [Serializable]
    public sealed class HelixSubGun : SubGunSettings
    {
        [SkillQuick(1, "らせんのダメージ（毎秒）")]
        [Tooltip("らせんの帯に入った敵に与える、1秒あたりのダメージ（強く握ったとき）。本体は最大 20。")]
        [SkillLabel("ダメージ（毎秒・強く握ったとき）")]
        [Min(0f)]
        public float damagePerSecond = 10f;

        [Tooltip("一番軽く握ったときのダメージの割合（強く握ったときを 1 として）。")]
        [SkillLabel("軽く握ったときのダメージの割合")]
        [Range(0f, 1f)]
        public float weakGripDamageScale = 0.35f;

        [SkillQuick(2, "らせんの幅（本体のふちから）")]
        [Tooltip("本体のレーザーのふちから、らせんの外側までの幅（m）。当たる帯の広さもこれ。")]
        [SkillLabel("らせんの幅（m）")]
        [Min(0f)]
        public float reach = 0.4f;

        [SkillQuick(3, "1周の長さ（m）")]
        [Tooltip("らせんが1周するまでの長さ（m）。小さいほど巻きが細かい。")]
        [SkillLabel("1周の長さ（m）")]
        public GripRange turnLength = new GripRange(5.5f, 3.8f);

        [SkillQuick(4, "回る速さ（1秒に何周）")]
        [Tooltip("らせんが回る速さ（1秒に何周）。")]
        [SkillLabel("回る速さ（1秒に何周）")]
        public GripRange spinSpeed = new GripRange(0.9f, 1.8f);

        [Tooltip("本体が当たっている障害物・壁に与える、1秒あたりのダメージ（らせんのぶん）。ボスの部位には上のダメージが入る。")]
        [SkillLabel("障害物へのダメージ（毎秒）")]
        [Min(0f)]
        public float obstacleDamagePerSecond = 3f;

        [Tooltip("根元から、らせんが一番広くなるまでの長さ（m）。根元ではらせんが細くまとまっている。")]
        [SkillLabel("根元から広がるまでの長さ（m）")]
        [Min(0.01f)]
        public float rampLength = 1.5f;

        [SkillQuick(5, "らせんの線の太さ（m）")]
        [Tooltip("らせんの線の太さ（m）。")]
        [SkillLabel("線の太さ（m）")]
        [Min(0.005f)]
        public float strandWidth = 0.11f;

        [Tooltip("線のまわりのぼんやりした光の太さ（線の太さに掛ける）。0 で光なし。")]
        [SkillLabel("光の太さ（倍）")]
        [Min(0f)]
        public float glowWidthScale = 2.4f;

        [Tooltip("ぼんやりした光の濃さ。")]
        [SkillLabel("光の濃さ")]
        [Range(0f, 1f)]
        public float glowAlpha = 0.3f;

        [Tooltip("本体の奥側を通るところの濃さ（手前側を 1 として）。小さいほど立体に見える。")]
        [SkillLabel("奥側の濃さ")]
        [Range(0f, 1f)]
        public float backAlpha = 0.5f;

        [Tooltip("らせんに沿って流れる、明るい粒の数（1本あたり）。らせんが回っているのが分かりやすくなる。0 で無し。")]
        [SkillLabel("流れる粒の数（1本あたり）")]
        [Range(0, 16)]
        public int beads = 6;

        [Tooltip("流れる粒の大きさ（半径 m）。")]
        [SkillLabel("流れる粒の大きさ（m）")]
        [Min(0f)]
        public float beadRadius = 0.09f;

        [Tooltip("当たった所の光を出す間隔の最小（秒）。小さいほどたくさん出る。")]
        [SkillLabel("当たった所の光の間隔（秒）")]
        [Min(0f)]
        public float flashInterval = 0.06f;

        [Tooltip("1m あたりの点の数（多いほどなめらか。重くなる）。")]
        [SkillLabel("なめらかさ（1mあたりの点）")]
        [Range(2, 20)]
        public int pointsPerMeter = 8;

        public override SubGunRunner AddRunner(GameObject turretObject) => turretObject.AddComponent<HelixSubGunRunner>();
    }

    /// <summary>「螺旋ツイン」を動かす係。</summary>
    public sealed class HelixSubGunRunner : SubGunRunner
    {
        private const int MaxPoints = 220;

        private readonly List<Vector2> _points = new List<Vector2>(MaxPoints);
        private readonly List<float> _depth = new List<float>(MaxPoints);
        private float _phase;
        private float _fade;
        private float _flashCooldown;
        private float _travel;

        private HelixSubGun Settings => (HelixSubGun)BaseSettings;

        protected override bool WantsBeamHits => true;

        protected override void Tick(float dt)
        {
            var settings = Settings;
            _fade = Mathf.MoveTowards(_fade, MainBeamOn ? 1f : 0f, dt * 8f);
            _flashCooldown -= dt;
            var spin = settings.spinSpeed.At(Grip01);
            _phase += Mathf.PI * 2f * spin * dt;
            // 粒はらせんの回りに合わせて先へ進む（1周ぶん回ると、1周の長さだけ進む）。
            _travel = Mathf.Repeat(_travel + spin * settings.turnLength.At(Grip01) * dt, 10000f);
            if (_phase > Mathf.PI * 2f)
            {
                _phase -= Mathf.PI * 2f;
            }

            if (MainBeamOn)
            {
                ApplyDamage(settings, dt);
            }
        }

        private void ApplyDamage(HelixSubGun settings, float dt)
        {
            var dps = settings.damagePerSecond * Mathf.Lerp(settings.weakGripDamageScale, 1f, Grip01);
            var swarm = SwarmSystem.Current;
            if (swarm != null && BeamLength > 0.01f && dps > 0f)
            {
                swarm.QueueBeam(BeamOrigin, BeamEnd, BeamHalfWidth + settings.reach, dps, 0, Owner);
            }

            var impact = BeamImpactCollider;
            if (impact != null)
            {
                SubGunHits.DamageCollider(impact, Turret, dps * dt, settings.obstacleDamagePerSecond * dt);
            }
        }

        /// <summary>自分の当たりのうち、本体の外側（らせんの帯）で当たった所に光を出す。</summary>
        protected override void OnBeamHits(IReadOnlyList<SwarmBeamHit> hits)
        {
            if (!MainBeamOn || _flashCooldown > 0f)
            {
                return;
            }

            var settings = Settings;
            var side = new Vector2(-BeamDirection.y, BeamDirection.x);
            var shown = 0;
            for (var i = 0; i < hits.Count && shown < 2; i++)
            {
                if (hits[i].owner != Owner)
                {
                    continue;
                }

                var p = new Vector2(hits[i].position.x, hits[i].position.y);
                var d = p - BeamOrigin;
                var along = Vector2.Dot(d, BeamDirection);
                var lateral = Mathf.Abs(Vector2.Dot(d, side));
                if (along < 0f || along > BeamLength || lateral < BeamHalfWidth * 0.9f || lateral > BeamHalfWidth + settings.reach + 0.3f)
                {
                    continue;
                }

                AddFlash(p);
                shown++;
            }

            if (shown > 0)
            {
                _flashCooldown = settings.flashInterval;
            }
        }

        protected override void Draw(SubGunLayer back, SubGunLayer front)
        {
            var settings = Settings;
            if (_fade <= 0.001f || BeamLength <= 0.05f)
            {
                return;
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(BeamLength * settings.pointsPerMeter) + 1, 2, MaxPoints);
            var turn = Mathf.Max(0.05f, settings.turnLength.At(Grip01));
            var radius = BeamHalfWidth + settings.reach * 0.85f;
            var side = new Vector2(-BeamDirection.y, BeamDirection.x);
            var frontColor = settings.ColorFor(Owner, _fade);
            var backColor = settings.ColorFor(Owner, _fade * settings.backAlpha);
            for (var strand = 0; strand < 2; strand++)
            {
                _points.Clear();
                _depth.Clear();
                for (var k = 0; k < count; k++)
                {
                    var s = BeamLength * k / (count - 1);
                    var theta = Mathf.PI * 2f * s / turn - _phase + strand * Mathf.PI;
                    var ramp = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(s / settings.rampLength));
                    _points.Add(BeamOrigin + BeamDirection * s + side * (Mathf.Sin(theta) * radius * ramp));
                    _depth.Add(Mathf.Cos(theta));
                }

                DrawRuns(settings, back, front, frontColor, backColor);
                DrawBeads(settings, back, front, strand, turn, radius, side);
            }
        }

        /// <summary>手前（depth≧0）と奥（depth＜0）で続いている所ごとに、上の層・下の層へ分けて描く。</summary>
        private void DrawRuns(HelixSubGun settings, SubGunLayer back, SubGunLayer front, Color frontColor, Color backColor)
        {
            var start = 0;
            for (var k = 1; k <= _points.Count; k++)
            {
                var ended = k == _points.Count || (_depth[k] >= 0f) != (_depth[start] >= 0f);
                if (!ended)
                {
                    continue;
                }

                // 境目の点も含めて描き、すき間を作らない。
                var last = Mathf.Min(k, _points.Count - 1);
                var runCount = last - start + 1;
                var isFront = _depth[start] >= 0f;
                var layer = isFront ? front : back;
                var color = isFront ? frontColor : backColor;
                if (settings.glowWidthScale > 0f && settings.glowAlpha > 0f)
                {
                    var glow = color;
                    glow.a *= settings.glowAlpha;
                    layer.Polyline(_points, start, runCount, settings.strandWidth * settings.glowWidthScale, glow);
                }

                OutlinedPolyline(layer, _points, start, runCount, settings.strandWidth, color);
                start = k;
            }
        }

        /// <summary>らせんに沿って根元から先へ流れる明るい粒（手前を通る粒だけ白く光る）。</summary>
        private void DrawBeads(HelixSubGun settings, SubGunLayer back, SubGunLayer front, int strand, float turn, float radius, Vector2 side)
        {
            if (settings.beads <= 0 || settings.beadRadius <= 0f)
            {
                return;
            }

            var spacing = BeamLength / settings.beads;
            for (var b = 0; b < settings.beads; b++)
            {
                var s = Mathf.Repeat(b * spacing + _travel + strand * spacing * 0.5f, BeamLength);
                var theta = Mathf.PI * 2f * s / turn - _phase + strand * Mathf.PI;
                var ramp = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(s / settings.rampLength));
                var p = BeamOrigin + BeamDirection * s + side * (Mathf.Sin(theta) * radius * ramp);
                var isFront = Mathf.Cos(theta) >= 0f;
                var tail = Mathf.Clamp01(1f - s / BeamLength * 0.5f);
                if (isFront)
                {
                    front.Disc(p, settings.beadRadius * tail, new Color(1f, 1f, 1f, _fade * settings.opacity), 10);
                }
                else
                {
                    back.Disc(p, settings.beadRadius * 0.7f * tail, settings.ColorFor(Owner, _fade * settings.backAlpha), 8);
                }
            }
        }
    }
}
