using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.HowToPlay
{
    // フィールド（上の横長の枠）。本番のステージを真上から撮った絵の上に、コア・4人の砲台（キャラのポッド）・ビーム・敵（スピキ）・泡・スキルを描く。
    // 自分の砲台が上に来るよう、ステージの絵ごと回す（本番のカメラと同じ「全員が赤と同じ向き」）。
    public sealed partial class PracticeScreen
    {
        private const float FieldX = 40f;
        // フィールド（2026-11-19、ユーザーのレイアウト案に合わせて 40,40 から 1230×698）。
        private const float FieldY = 40f;
        private const float FieldW = 1230f;
        private const float FieldH = 698f;
        private const int EnemyPool = 70;
        private const int ParticlePool = 80;
        private const int FloatPool = 8;

        private RectTransform _field;
        private RawImage _plate;
        private RawImage _core;
        private Image _coreGlow;
        private Image _coreHpBack;
        private Image _coreHp;
        private float _coreHp01 = 0.9f;
        private float _coreFlash;
        private float _ppm;
        private float _turretRadius;
        private float _shake;
        private readonly TurretView[] _turrets = new TurretView[PracticeBoard.SeatCount];
        private readonly Enemy[] _enemies = new Enemy[EnemyPool];
        private readonly Particle[] _particles = new Particle[ParticlePool];
        private readonly Floater[] _floaters = new Floater[FloatPool];
        private float _spawnAcc;
        private Image _burnEdge;
        private System.Random _rng = new System.Random(7);

        private sealed class TurretView
        {
            public int Seat;
            public Vector2 Pos;
            public float BaseDeg;
            public float Aim;
            public float Grip;
            public bool Firing;
            public bool LastGripping;
            public RectTransform Root;
            public RawImage Pod;
            public PracticeShape Ring;
            public RawImage Badge;
            public Text You;
            public Text Burn;
            public RawImage Beam;
            public Image BeamGlow;
            public RawImage Thick;

            // 泡
            public bool BubbleOn;
            public float BubbleDeg;
            public float BubbleHp = 1f;
            public float BubbleBorn;
            public RectTransform Bubble;
            public RawImage BubbleImage;
            public PracticeShape BubbleRing;
            public Text BubbleText;

            // スキル
            public float SkillTime = -1f;
            public float SkillDuration;
            public PracticeShape SkillShape;
            public Vector2 SkillTarget;
            public Vector2 ArrowPos;
            public Vector2 ArrowVel;
            public readonly List<Vector2> Trail = new List<Vector2>();
            public Image[] Satellites;
        }

        private sealed class Enemy
        {
            public bool Alive;
            public Vector2 Pos;
            public float Speed;
            public float Phase;
            public float Dying = -1f;
            public RawImage Image;
        }

        private sealed class Particle
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float Max;
            public Image Image;
        }

        private sealed class Floater
        {
            public float Start = -10f;
            public Vector2 Pos;
            public Text Text;
        }

        private float RandomRange(float a, float b) => a + ((float)_rng.NextDouble() * (b - a));

        // ───────── 作る ─────────

        private void BuildField()
        {
            if (_realField)
            {
                BuildRealField();
                return;
            }

            _ppm = _cfg.pixelsPerMeter;
            var plateInfo = _cfg.FindPlate(_stageScene);
            _turretRadius = (plateInfo != null ? plateInfo.turretRadius : 5.9f) * _ppm;

            // 枠（紺の板にピンクとミントのふち）。枠の絵があれば、それを最後に重ねる（BuildFieldFrameImage）。
            if (Art == null || Art.fieldFrame == null)
            {
                var frame = Shape("Field Frame", _root, P(FieldX + (FieldW * 0.5f), FieldY + (FieldH * 0.5f)), new Vector2(FieldW + 40f, FieldH + 40f));
                var c = Vector2.zero;
                frame.Fill(Chamfer(c + new Vector2(8f, -8f), new Vector2(FieldW + 16f, FieldH + 16f), 32f), Pink);
                var outer = Chamfer(c, new Vector2(FieldW + 16f, FieldH + 16f), 32f);
                frame.Fill(outer, Ink);
                frame.Stroke(outer, 4f, Mint, true);
            }

            _field = MakeRect("Field", _root, FieldX, FieldY, FieldW, FieldH);
            _field.gameObject.AddComponent<RectMask2D>();
            var floor = HowToPlayArt.MakeImage("Floor", _field, null, new Color32(0x1b, 0x23, 0x66, 0xff), Vector2.zero, new Vector2(FieldW, FieldH));
            floor.raycastTarget = false;
            if (plateInfo != null && plateInfo.plate != null)
            {
                var size = plateInfo.plateWorldSize * _ppm;
                _plate = Raw("Stage", _field, plateInfo.plate, Vector2.zero, new Vector2(size, size));
                _plate.rectTransform.localEulerAngles = new Vector3(0f, 0f, _local * 90f);
            }

            // コア。
            var coreH = _cfg.coreWorldHeight * _ppm;
            _coreGlow = HowToPlayArt.MakeImage("Core Glow", _field, HowToPlayArt.Soft, new Color(0.6f, 0.95f, 1f, 0.55f), Vector2.zero, new Vector2(coreH * 2.2f, coreH * 2.2f));
            if (_cfg.core != null)
            {
                var aspect = _cfg.core.width / (float)Mathf.Max(1, _cfg.core.height);
                _core = Raw("Core", _field, _cfg.core, Vector2.zero, new Vector2(coreH * aspect, coreH));
            }
            else
            {
                _core = Raw("Core", _field, null, Vector2.zero, new Vector2(coreH * 0.8f, coreH));
            }

            _coreHpBack = HowToPlayArt.MakeImage("Core HP Back", _field, HowToPlayArt.Rounded, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, -(coreH * 0.5f) - 16f), new Vector2(110f, 14f));
            _coreHp = HowToPlayArt.MakeImage("Core HP", _field, HowToPlayArt.Rounded, Mint, new Vector2(-53f, -(coreH * 0.5f) - 16f), new Vector2(106f, 8f));
            HowToPlayArt.PivotLeft(_coreHp.rectTransform);

            var enemySize = _cfg.enemyWorldSize * _ppm;
            var enemyParent = MakeRect("Enemies", _field, FieldX, FieldY, FieldW, FieldH);
            enemyParent.anchoredPosition = Vector2.zero;
            for (var i = 0; i < EnemyPool; i++)
            {
                var image = Raw("Enemy", enemyParent, _cfg.enemy, Vector2.zero, new Vector2(enemySize, enemySize));
                image.enabled = false;
                _enemies[i] = new Enemy { Image = image };
            }

            for (var i = 0; i < PracticeBoard.SeatCount; i++)
            {
                _turrets[i] = BuildTurret(i);
            }

            for (var i = 0; i < ParticlePool; i++)
            {
                var image = HowToPlayArt.MakeImage("Spark", _field, HowToPlayArt.Circle, Color.white, Vector2.zero, new Vector2(8f, 8f));
                image.enabled = false;
                _particles[i] = new Particle { Image = image };
            }

            for (var i = 0; i < FloatPool; i++)
            {
                var text = Heading("Float", _field, string.Empty, 34, Vector2.zero, new Vector2(600f, 90f));
                text.enabled = false;
                _floaters[i] = new Floater { Text = text };
            }

            _burnEdge = HowToPlayArt.MakeImage("Burn Edge", _field, Vignette, new Color(Burn.r, Burn.g, Burn.b, 0f), Vector2.zero, new Vector2(FieldW, FieldH));
            _burnEdge.type = Image.Type.Simple;


            for (var i = 0; i < 30; i++)
            {
                SpawnEnemy(true);
            }

            BuildFieldFrameImage();
        }

        private Image _liveDot;

        private TurretView BuildTurret(int seat)
        {
            var view = new TurretView { Seat = seat };
            view.BaseDeg = Mathf.Repeat((seat - _local) * 90f, 360f);
            var rad = view.BaseDeg * Mathf.Deg2Rad;
            view.Pos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * _turretRadius;
            view.Aim = view.BaseDeg;
            var info = _seats[seat];

            view.BeamGlow = HowToPlayArt.MakeImage("Beam Glow", _field, HowToPlayArt.Soft, new Color(info.Color.r, info.Color.g, info.Color.b, 0.5f), view.Pos, new Vector2(10f, 10f));
            HowToPlayArt.PivotLeft(view.BeamGlow.rectTransform);
            view.Beam = Raw("Beam", _field, BeamTexture, view.Pos, new Vector2(10f, 10f));
            HowToPlayArt.PivotLeft(view.Beam.rectTransform);
            view.Thick = Raw("Skill Beam", _field, BeamTexture, view.Pos, new Vector2(10f, 10f));
            HowToPlayArt.PivotLeft(view.Thick.rectTransform);
            view.Thick.color = Color.Lerp(info.Color, Color.white, 0.25f);
            view.Beam.enabled = view.BeamGlow.enabled = view.Thick.enabled = false;

            view.SkillShape = Shape("Skill", _field, Vector2.zero, new Vector2(FieldW, FieldH));
            view.Satellites = new Image[3];
            for (var k = 0; k < 3; k++)
            {
                view.Satellites[k] = HowToPlayArt.MakeImage("Satellite", _field, HowToPlayArt.Circle, Color.Lerp(info.Color, Color.white, 0.5f), Vector2.zero, new Vector2(34f, 34f));
                HowToPlayArt.AddOutline(view.Satellites[k], Ink, 3f);
                view.Satellites[k].enabled = false;
            }

            // 泡。
            view.Bubble = HowToPlayArt.MakeRect("Bubble", _field, Vector2.zero, new Vector2(96f, 96f));
            var glow = HowToPlayArt.MakeImage("Glow", view.Bubble, HowToPlayArt.Soft, new Color(0.75f, 0.95f, 1f, 0.6f), Vector2.zero, new Vector2(130f, 130f));
            glow.raycastTarget = false;
            view.BubbleImage = Raw("Ball", view.Bubble, _cfg.bubble, Vector2.zero, new Vector2(78f, 78f));
            view.BubbleRing = Shape("Hp", view.Bubble, Vector2.zero, new Vector2(110f, 110f));
            view.BubbleText = Heading("Hint", view.Bubble, Ui.fakeBubbleHint, 26, new Vector2(0f, 66f), new Vector2(240f, 40f));
            view.Bubble.gameObject.SetActive(false);

            // 砲台（本番と同じ、キャラのポッドの絵。下の口がビームの向き）。
            var height = _cfg.turretWorldHeight * _ppm;
            view.Root = HowToPlayArt.MakeRect("Turret " + (seat + 1), _field, view.Pos, new Vector2(height, height));
            view.Ring = Shape("Heat", view.Root, Vector2.zero, new Vector2(height, height));
            var podAspect = info.PodUv.height > 0f && info.Pod != null ? (info.PodUv.width * info.Pod.width) / (info.PodUv.height * info.Pod.height) : 1f;
            view.Pod = Raw("Pod", view.Root, info.Pod, Vector2.zero, new Vector2(height * podAspect * 0.86f, height * 0.86f));
            view.Pod.uvRect = info.PodUv;
            if (info.Pod == null)
            {
                view.Pod.color = info.Color;
            }

            view.Badge = Raw("Badge", _field, Pick(_cfg.seatBadges, seat), view.Pos + new Vector2(height * 0.42f, height * 0.42f), new Vector2(40f, 40f));
            view.You = Heading("You", _field, seat == _local ? _cfg.localMark : string.Empty, 22, view.Pos + new Vector2(-height * 0.45f, height * 0.5f), new Vector2(120f, 30f), TextAnchor.MiddleCenter, Ink);
            view.Burn = Heading("Burn", _field, Ui.burnLabel, seat == _local ? 40 : 26, view.Pos + new Vector2(0f, height * 0.75f), new Vector2(460f, 60f), TextAnchor.MiddleCenter, Burn);
            view.Burn.color = new Color32(0xff, 0xe0, 0xd4, 0xff);
            view.Burn.enabled = false;
            return view;
        }

        private static T Pick<T>(T[] list, int index) where T : class => list != null && index >= 0 && index < list.Length ? list[index] : null;

        private static List<Vector2> Chamfer(Vector2 c, Vector2 size, float cut)
        {
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            return new List<Vector2>
            {
                c + new Vector2(-hx + cut, hy), c + new Vector2(hx, hy), c + new Vector2(hx, -hy + cut),
                c + new Vector2(hx - cut, -hy), c + new Vector2(-hx, -hy), c + new Vector2(-hx, hy - cut)
            };
        }

        // ───────── 動かす ─────────

        private void TickField(float dt)
        {
            if (_realField)
            {
                TickRealField(dt);
                return;
            }

            if (_liveDot != null)
            {
                _liveDot.color = Mathf.Repeat(_time * 1.6f, 1f) < 0.5f ? Pink : new Color32(0xff, 0xc2, 0xdf, 0xff);
            }
            _spawnAcc += dt * 3.2f;
            while (_spawnAcc > 1f)
            {
                _spawnAcc -= 1f;
                SpawnEnemy(false);
            }

            for (var i = 0; i < _turrets.Length; i++)
            {
                TickTurret(_turrets[i], dt);
            }

            TickEnemies(dt);
            TickParticles(dt);
            TickFloaters();

            _coreFlash = Mathf.Max(0f, _coreFlash - (dt * 4f));
            _core.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.6f), _coreFlash);
            _coreGlow.rectTransform.localScale = Vector3.one * (1f + (0.05f * Mathf.Sin(_time * 2f)));
            _core.rectTransform.anchoredPosition = new Vector2(0f, 3f * Mathf.Sin(_time * 2f));
            _coreHp.rectTransform.sizeDelta = new Vector2(106f * Mathf.Clamp01(_coreHp01), 8f);

            var overheated = LocalOverheated;
            var a = overheated ? 0.38f + (0.14f * Mathf.Sin(_time * 14f)) : 0f;
            _burnEdge.color = new Color(Burn.r, Burn.g, Burn.b, Mathf.MoveTowards(_burnEdge.color.a, a, dt * 3f));

            _shake = Mathf.Max(0f, _shake - dt);
            var s = _shake > 0f ? _shake * 22f : 0f;
            _field.anchoredPosition = P(FieldX + (FieldW * 0.5f), FieldY + (FieldH * 0.5f)) + new Vector2(RandomRange(-s, s), RandomRange(-s, s));
        }

        private void TickTurret(TurretView t, float dt)
        {
            var seat = t.Seat;
            var data = _board.seats[seat];
            var info = _seats[seat];
            var present = data.present;
            var isLocal = seat == _local;
            var trainer = isLocal ? Trainer : null;

            t.Root.gameObject.SetActive(present);
            t.Badge.enabled = present;
            t.You.enabled = present && isLocal;

            float grip;
            bool firing;
            float heat;
            bool overheated;
            if (trainer != null)
            {
                grip = trainer.Grip01;
                firing = trainer.Firing;
                heat = trainer.Heat01;
                overheated = trainer.Overheated;
                t.Aim = t.BaseDeg + trainer.TurretDegrees;
            }
            else
            {
                // ほかの人の値は 1 秒に 15 回ほどしか届かないので、なめらかに追いかける。
                var target = data.gripping ? data.Grip01 : 0f;
                t.Grip = Mathf.MoveTowards(t.Grip, target, dt * 6f);
                grip = t.Grip;
                firing = present && data.gripping && !data.overheated && grip > 0.02f;
                heat = data.Heat01;
                overheated = data.overheated;
                var rules = _settings.rules ?? new PracticeRules();
                t.Aim += rules.turretDegreesPerSecond * (firing ? rules.firingTurnScale : 1f) * dt;
            }

            t.Firing = present && firing && t.SkillTime < 0f;
            var aimRad = t.Aim * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(aimRad), Mathf.Cos(aimRad));

            // ポッドの絵: 下（口）がビームの向き。
            var podScale = 1f + (0.06f * (t.Firing ? grip : 0f));
            t.Pod.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f - t.Aim);
            t.Pod.rectTransform.localScale = Vector3.one * podScale;
            t.Pod.color = overheated ? Color.Lerp(Color.white, new Color(1f, 0.45f, 0.35f), 0.5f + (0.5f * Mathf.Sin(_time * 30f))) : (info.Pod == null ? info.Color : Color.white);

            // 熱の輪。
            t.Ring.Clear();
            var r = t.Root.sizeDelta.x * 0.48f;
            t.Ring.Stroke(PracticeShape.Arc(Vector2.zero, r, 0f, 360f, 40), 7f, new Color(1f, 1f, 1f, 0.35f), true);
            if (heat > 0.01f)
            {
                var col = overheated ? (Mathf.Repeat(_time * 8f, 1f) < 0.5f ? Burn : new Color(1f, 0.7f, 0.25f)) : Color.Lerp(new Color(1f, 0.85f, 0.25f), Burn, heat);
                t.Ring.Stroke(PracticeShape.Arc(Vector2.zero, r, 0f, 360f * heat, Mathf.Max(2, Mathf.RoundToInt(40 * heat))), 7f, col, false);
            }

            if (isLocal)
            {
                t.Ring.Stroke(PracticeShape.Arc(Vector2.zero, r + 12f, _time * 40f, (_time * 40f) + 300f, 30), 3f, new Color(1f, 1f, 1f, 0.5f + (0.5f * Mathf.Sin(_time * 5f))), false);
            }

            t.Burn.enabled = present && overheated && Mathf.Repeat(_time * 3f, 1f) < 0.7f;
            t.Burn.rectTransform.localScale = Vector3.one * (1f + (0.06f * Mathf.Sin(_time * 20f)));

            // ビーム。
            var muzzle = t.Pos + (dir * t.Root.sizeDelta.y * 0.38f);
            var length = RayToEdge(muzzle, dir);
            t.Beam.enabled = t.Firing;
            t.BeamGlow.enabled = t.Firing;
            if (t.Firing)
            {
                var width = Mathf.Lerp(9f, 24f, grip) * (1f + (0.06f * Mathf.Sin((_time * 22f) + seat)));
                SetBeam(t.Beam.rectTransform, muzzle, dir, length, width);
                SetBeam(t.BeamGlow.rectTransform, muzzle, dir, length, (width * 2.4f) + 10f);
                var uv = t.Beam.uvRect;
                uv.x = Mathf.Repeat(uv.x - (dt * 1.5f), 1f);
                uv.width = length / 128f;
                t.Beam.uvRect = uv;
                t.Beam.color = Color.white;
                KillAlong(muzzle, dir, length, (width * 0.5f) + 10f, info.Color);
                if (Mathf.Repeat(_time * 30f + seat, 3f) < 1f)
                {
                    Spark(muzzle + (dir * RandomRange(0f, length)), Color.white, 60f, 0.35f, 5f);
                }
            }

            TickBubble(t, dt, trainer, dir, muzzle, length);
            TickSkill(t, dt, trainer, dir, muzzle, length);
        }

        private static void SetBeam(RectTransform rect, Vector2 from, Vector2 dir, float length, float width)
        {
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(length, width);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        /// <summary>from から dir 方向に、フィールドの端までの長さ。</summary>
        private static float RayToEdge(Vector2 from, Vector2 dir)
        {
            var hx = FieldW * 0.5f;
            var hy = FieldH * 0.5f;
            var t = 4000f;
            if (dir.x > 1e-4f) t = Mathf.Min(t, (hx - from.x) / dir.x);
            if (dir.x < -1e-4f) t = Mathf.Min(t, (-hx - from.x) / dir.x);
            if (dir.y > 1e-4f) t = Mathf.Min(t, (hy - from.y) / dir.y);
            if (dir.y < -1e-4f) t = Mathf.Min(t, (-hy - from.y) / dir.y);
            return Mathf.Max(0f, t);
        }

        private static float SegmentDistance(Vector2 p, Vector2 from, Vector2 dir, float length)
        {
            var u = Mathf.Clamp(Vector2.Dot(p - from, dir), 0f, length);
            return (p - (from + (dir * u))).magnitude;
        }

        // ───────── 泡 ─────────

        private void TickBubble(TurretView t, float dt, PracticeTrainer trainer, Vector2 dir, Vector2 muzzle, float length)
        {
            var step = _board.seats[t.Seat].step;
            bool on;
            float hp;
            if (trainer != null)
            {
                on = trainer.BubbleVisible;
                hp = trainer.BubbleHp01;
                if (on && !t.BubbleOn)
                {
                    t.BubbleBorn = _time;
                }

                t.BubbleDeg = t.BaseDeg + trainer.BubbleDegrees;
            }
            else
            {
                on = _board.seats[t.Seat].present && step == PracticeStep.Bubble;
                if (on && !t.BubbleOn)
                {
                    t.BubbleBorn = _time;
                    t.BubbleDeg = t.Aim + ((_settings.rules ?? new PracticeRules()).bubbleAheadDegrees);
                    t.BubbleHp = 1f;
                }

                if (on && t.Firing && Mathf.Abs(Mathf.DeltaAngle(t.Aim, t.BubbleDeg)) < 16f)
                {
                    t.BubbleHp = Mathf.Max(0.15f, t.BubbleHp - (dt * 1.5f));
                }

                hp = t.BubbleHp;
            }

            if (!on && t.BubbleOn)
            {
                // 割れた。
                var p = t.Bubble.anchoredPosition;
                for (var i = 0; i < 18; i++)
                {
                    Spark(p, i % 3 == 0 ? _seats[t.Seat].Color : new Color(0.75f, 0.95f, 1f), 320f, 0.6f, 9f);
                }

                Float(p + new Vector2(0f, 60f), PracticeUiTexts.Format(Ui.skillReadyFloatFormat, t.Seat + 1), _seats[t.Seat].Color);
                if (t.Seat == _local)
                {
                    _shake = 0.25f;
                }
            }

            t.BubbleOn = on;
            t.Bubble.gameObject.SetActive(on);
            if (!on)
            {
                return;
            }

            // 泡は「砲台からその向きに伸ばした線」の上に置く（フィールドの中に収まる所まで）。
            var rad = t.BubbleDeg * Mathf.Deg2Rad;
            var bdir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            var reach = Mathf.Min(170f, RayToEdge(t.Pos, bdir) - 60f);
            var pos = t.Pos + (bdir * Mathf.Max(90f, reach));
            var k = EaseOutBack(Mathf.Clamp01((_time - t.BubbleBorn) / 0.5f));
            t.Bubble.anchoredPosition = pos + new Vector2(0f, 4f * Mathf.Sin(_time * 3f));
            t.Bubble.localScale = Vector3.one * k * (1f + (0.04f * Mathf.Sin(_time * 5f)));
            t.BubbleImage.color = Color.white;
            t.BubbleRing.Clear();
            t.BubbleRing.Stroke(PracticeShape.Arc(Vector2.zero, 50f, 0f, 360f * hp, Mathf.Max(2, Mathf.RoundToInt(40 * hp))), 7f, _seats[t.Seat].Color, false);
            t.BubbleRing.Fill(PracticeShape.Star(Vector2.zero, 17f, 7.5f), _seats[t.Seat].Color);
            t.BubbleRing.Stroke(PracticeShape.Star(Vector2.zero, 17f, 7.5f), 3f, Color.white, true);
            t.BubbleText.enabled = t.Seat == _local && Mathf.Repeat(_time * 2f, 1f) < 0.75f;
        }

        // ───────── スキル（練習用の短い演出） ─────────

        private void OnStepChanged(int seat, PracticeStep before, PracticeStep after)
        {
            var t = _turrets[seat];
            if (t == null)
            {
                // ゲームの中の研修（本物のフィールド）: 砲台は自分では描かないので、文字だけ出す。
                if (seat == _local && PracticeSteps.Lesson(after) > PracticeSteps.Lesson(before))
                {
                    OnLocalLessonCleared(PracticeSteps.Lesson(before));
                }

                if (seat != _local && after == PracticeStep.Done && !_board.finished)
                {
                    Float(new Vector2(0f, (FieldH * 0.5f) - 110f), PracticeUiTexts.Format(Ui.doneFloatFormat, seat + 1, _seats[seat].Name), Ok);
                }

                return;
            }

            var lessonBefore = PracticeSteps.Lesson(before);
            var lessonAfter = PracticeSteps.Lesson(after);
            if (seat != _local && after == PracticeStep.Done && !_board.finished)
            {
                Float(t.Pos + new Vector2(0f, 80f), PracticeUiTexts.Format(Ui.otherDoneFloatFormat, seat + 1), Ok);
            }

            if (seat == _local && lessonAfter > lessonBefore)
            {
                OnLocalLessonCleared(lessonBefore);
            }
        }

        private void StartSkill(TurretView t)
        {
            t.SkillTime = 0f;
            t.SkillDuration = (_settings.rules ?? new PracticeRules()).skillSeconds;
            var rad = t.Aim * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            t.SkillTarget = t.Pos + (dir * Mathf.Min(220f, RayToEdge(t.Pos, dir) - 40f));
            t.ArrowPos = t.Pos;
            t.ArrowVel = dir * 900f;
            t.Trail.Clear();
            if (t.Seat == _local)
            {
                _shake = 0.3f;
            }
        }

        private void TickSkill(TurretView t, float dt, PracticeTrainer trainer, Vector2 dir, Vector2 muzzle, float length)
        {
            if (trainer != null && trainer.SkillActive && t.SkillTime < 0f)
            {
                StartSkill(t);
            }

            // ほかの人: スキルがたまっている間に握り直したら、スキルを出したとみなす（届くのは握力と課題だけなので）。
            var data = _board.seats[t.Seat];
            if (trainer == null && data.step == PracticeStep.Skill && data.gripping && !t.LastGripping && t.SkillTime < 0f)
            {
                StartSkill(t);
            }

            t.LastGripping = data.gripping;

            var look = _seats[t.Seat].Look;
            var kind = look != null ? look.skill : PracticeSkillLook.ThickLaser;
            var color = _seats[t.Seat].Color;
            t.SkillShape.Clear();
            t.Thick.enabled = false;
            foreach (var s in t.Satellites)
            {
                s.enabled = false;
            }

            if (t.SkillTime < 0f)
            {
                return;
            }

            t.SkillTime += dt;
            var k = t.SkillTime / Mathf.Max(0.1f, t.SkillDuration);
            if (k >= 1f)
            {
                t.SkillTime = -1f;
                return;
            }

            var fade = Mathf.Clamp01(Mathf.Min(t.SkillTime * 6f, (1f - k) * 6f));
            switch (kind)
            {
                case PracticeSkillLook.ThickLaser:
                {
                    var width = 90f * fade * (1f + (0.06f * Mathf.Sin(_time * 40f)));
                    t.Thick.enabled = true;
                    SetBeam(t.Thick.rectTransform, muzzle, dir, length, width);
                    var uv = t.Thick.uvRect;
                    uv.x = Mathf.Repeat(uv.x - (dt * 2f), 1f);
                    uv.width = length / 256f;
                    t.Thick.uvRect = uv;
                    KillAlong(muzzle, dir, length, (width * 0.5f) + 14f, color);
                    _shake = Mathf.Max(_shake, t.Seat == _local ? 0.1f : 0f);
                    break;
                }

                case PracticeSkillLook.Vortex:
                {
                    var c = t.SkillTarget;
                    var spin = t.SkillTime * 7f;
                    for (var arm = 0; arm < 4; arm++)
                    {
                        var pts = new List<Vector2>();
                        for (var s = 0; s < 34; s++)
                        {
                            var rr = (8f + (s * 5.2f)) * fade;
                            var a = (s * 0.19f) - spin + (arm * Mathf.PI * 0.5f);
                            pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
                        }

                        t.SkillShape.Stroke(pts, 9f, arm % 2 == 0 ? new Color(color.r, color.g, color.b, 0.85f * fade) : new Color(0.8f, 0.92f, 1f, 0.85f * fade), false);
                    }

                    PullAndKill(c, 190f, dt, color);
                    break;
                }

                case PracticeSkillLook.Ricochet:
                {
                    t.ArrowPos += t.ArrowVel * dt;
                    var hx = (FieldW * 0.5f) - 12f;
                    var hy = (FieldH * 0.5f) - 12f;
                    if (t.ArrowPos.x < -hx || t.ArrowPos.x > hx)
                    {
                        t.ArrowVel.x = -t.ArrowVel.x;
                        t.ArrowPos.x = Mathf.Clamp(t.ArrowPos.x, -hx, hx);
                        Spark(t.ArrowPos, color, 200f, 0.4f, 8f);
                    }

                    if (t.ArrowPos.y < -hy || t.ArrowPos.y > hy)
                    {
                        t.ArrowVel.y = -t.ArrowVel.y;
                        t.ArrowPos.y = Mathf.Clamp(t.ArrowPos.y, -hy, hy);
                        Spark(t.ArrowPos, color, 200f, 0.4f, 8f);
                    }

                    t.Trail.Add(t.ArrowPos);
                    if (t.Trail.Count > 14)
                    {
                        t.Trail.RemoveAt(0);
                    }

                    if (t.Trail.Count > 1)
                    {
                        t.SkillShape.Stroke(t.Trail, 8f, new Color(color.r, color.g, color.b, 0.7f * fade), false);
                    }

                    var d = t.ArrowVel.normalized;
                    var n = new Vector2(-d.y, d.x);
                    var head = new List<Vector2> { t.ArrowPos + (d * 26f), t.ArrowPos - (d * 10f) + (n * 15f), t.ArrowPos - (d * 4f), t.ArrowPos - (d * 10f) - (n * 15f) };
                    t.SkillShape.FillStroke(head, Color.white, PracticeScreen.Ink, 3f);
                    KillNear(t.ArrowPos, 40f, color);
                    break;
                }

                case PracticeSkillLook.Satellite:
                {
                    for (var s = 0; s < 3; s++)
                    {
                        var a = (t.SkillTime * 3f) + (s * 2.094f);
                        var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 120f;
                        var image = t.Satellites[s];
                        image.enabled = true;
                        image.rectTransform.anchoredPosition = p;
                        image.color = new Color(image.color.r, image.color.g, image.color.b, fade);
                        KillNear(p, 34f, color);
                    }

                    t.SkillShape.Stroke(PracticeShape.Arc(Vector2.zero, 120f, 0f, 360f, 48), 3f, new Color(color.r, color.g, color.b, 0.4f * fade), true);
                    _coreHp01 = Mathf.Min(1f, _coreHp01 + (dt * 0.12f));
                    if (Mathf.Repeat(t.SkillTime * 8f, 1f) < dt * 8f)
                    {
                        Float(new Vector2(RandomRange(-50f, 50f), RandomRange(-20f, 30f)), "+", color, 0.9f);
                    }

                    break;
                }
            }
        }

        // ───────── 敵 ─────────

        private void SpawnEnemy(bool anywhere)
        {
            foreach (var e in _enemies)
            {
                if (e.Alive)
                {
                    continue;
                }

                var hx = FieldW * 0.5f;
                var hy = FieldH * 0.5f;
                Vector2 p;
                if (anywhere)
                {
                    var a = RandomRange(0f, Mathf.PI * 2f);
                    var r = RandomRange(_turretRadius * 1.1f, Mathf.Max(hx, hy));
                    p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    p.x = Mathf.Clamp(p.x, -hx + 20f, hx - 20f);
                    p.y = Mathf.Clamp(p.y, -hy + 20f, hy - 20f);
                }
                else
                {
                    var side = _rng.Next(4);
                    p = side switch
                    {
                        0 => new Vector2(RandomRange(-hx, hx), hy + 30f),
                        1 => new Vector2(RandomRange(-hx, hx), -hy - 30f),
                        2 => new Vector2(-hx - 30f, RandomRange(-hy, hy)),
                        _ => new Vector2(hx + 30f, RandomRange(-hy, hy))
                    };
                }

                e.Alive = true;
                e.Dying = -1f;
                e.Pos = p;
                e.Speed = RandomRange(28f, 46f);
                e.Phase = RandomRange(0f, 10f);
                e.Image.enabled = true;
                e.Image.color = Color.white;
                return;
            }
        }

        private void TickEnemies(float dt)
        {
            foreach (var e in _enemies)
            {
                if (!e.Alive)
                {
                    continue;
                }

                if (e.Dying >= 0f)
                {
                    e.Dying += dt;
                    var k = e.Dying / 0.25f;
                    e.Image.rectTransform.localScale = Vector3.one * (1f + (0.6f * k));
                    e.Image.color = new Color(1f, 1f, 1f, 1f - k);
                    if (k >= 1f)
                    {
                        e.Alive = false;
                        e.Image.enabled = false;
                    }

                    continue;
                }

                var toCore = -e.Pos;
                var dist = toCore.magnitude;
                if (dist < 46f)
                {
                    e.Alive = false;
                    e.Image.enabled = false;
                    _coreFlash = 1f;
                    _coreHp01 = Mathf.Max(0.35f, _coreHp01 - 0.004f);
                    continue;
                }

                var v = (toCore / dist * e.Speed) + new Vector2(Mathf.Sin((_time * 2f) + e.Phase), Mathf.Cos((_time * 2.3f) + e.Phase)) * 10f;
                e.Pos += v * dt;
                e.Image.rectTransform.anchoredPosition = e.Pos + new Vector2(0f, 3f * Mathf.Abs(Mathf.Sin((_time * 8f) + e.Phase)));
                e.Image.rectTransform.localEulerAngles = new Vector3(0f, 0f, 6f * Mathf.Sin((_time * 6f) + e.Phase));
                e.Image.rectTransform.localScale = Vector3.one;
            }
        }

        private void Kill(Enemy e, Color color)
        {
            e.Dying = 0f;
            for (var i = 0; i < 4; i++)
            {
                Spark(e.Pos, i % 2 == 0 ? new Color(0.8f, 0.55f, 1f) : color, 220f, 0.4f, 7f);
            }
        }

        private void KillAlong(Vector2 from, Vector2 dir, float length, float radius, Color color)
        {
            foreach (var e in _enemies)
            {
                if (e.Alive && e.Dying < 0f && SegmentDistance(e.Pos, from, dir, length) < radius)
                {
                    Kill(e, color);
                }
            }
        }

        private void KillNear(Vector2 p, float radius, Color color)
        {
            foreach (var e in _enemies)
            {
                if (e.Alive && e.Dying < 0f && (e.Pos - p).sqrMagnitude < radius * radius)
                {
                    Kill(e, color);
                }
            }
        }

        private void PullAndKill(Vector2 c, float radius, float dt, Color color)
        {
            foreach (var e in _enemies)
            {
                if (!e.Alive || e.Dying >= 0f)
                {
                    continue;
                }

                var d = c - e.Pos;
                var m = d.magnitude;
                if (m > radius)
                {
                    continue;
                }

                if (m < 30f)
                {
                    Kill(e, color);
                    continue;
                }

                var n = d / m;
                e.Pos += ((n * 160f) + (new Vector2(-n.y, n.x) * 200f)) * dt;
            }
        }

        // ───────── 火花・浮かぶ文字 ─────────

        private void Spark(Vector2 p, Color color, float speed, float life, float size)
        {
            foreach (var s in _particles)
            {
                if (s.Alive)
                {
                    continue;
                }

                var a = RandomRange(0f, Mathf.PI * 2f);
                s.Alive = true;
                s.Pos = p;
                s.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * RandomRange(0.3f, 1f);
                s.Life = 0f;
                s.Max = life * RandomRange(0.7f, 1.2f);
                s.Image.enabled = true;
                s.Image.color = color;
                s.Image.rectTransform.sizeDelta = new Vector2(size, size);
                return;
            }
        }

        private void TickParticles(float dt)
        {
            foreach (var s in _particles)
            {
                if (!s.Alive)
                {
                    continue;
                }

                s.Life += dt;
                if (s.Life >= s.Max)
                {
                    s.Alive = false;
                    s.Image.enabled = false;
                    continue;
                }

                s.Pos += s.Vel * dt;
                s.Vel *= Mathf.Pow(0.02f, dt);
                var k = s.Life / s.Max;
                s.Image.rectTransform.anchoredPosition = s.Pos;
                var c = s.Image.color;
                c.a = 1f - k;
                s.Image.color = c;
            }
        }

        /// <summary>ビームの模様（Resources/PracticeArtLibrary の「ビームの模様」に入っていればそれ）。</summary>
        private Texture BeamTexture
        {
            get
            {
                var lib = PracticeArtLibrary.Load();
                return lib != null && lib.beam != null ? lib.beam : _settings.beamTexture;
            }
        }

        private void Float(Vector2 p, string text, Color shadow, float scale = 1f)
        {
            Floater oldest = _floaters[0];
            foreach (var f in _floaters)
            {
                if (f.Start < oldest.Start)
                {
                    oldest = f;
                }
            }

            oldest.Start = _time;
            oldest.Pos = p;
            oldest.Text.text = text;
            SetSize(oldest.Text, Mathf.RoundToInt(34 * scale));
            var shadowComponent = oldest.Text.GetComponent<Shadow>();
            if (shadowComponent != null && !(shadowComponent is Outline))
            {
                shadowComponent.effectColor = shadow;
            }

            foreach (var s in oldest.Text.GetComponents<Shadow>())
            {
                if (!(s is Outline))
                {
                    s.effectColor = shadow;
                }
            }
        }

        private void TickFloaters()
        {
            foreach (var f in _floaters)
            {
                var age = _time - f.Start;
                var on = age >= 0f && age < 1.6f;
                f.Text.enabled = on;
                if (!on)
                {
                    continue;
                }

                var k = age / 1.6f;
                f.Text.rectTransform.anchoredPosition = f.Pos + new Vector2(0f, 40f * k);
                f.Text.rectTransform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(age / 0.3f));
                var c = f.Text.color;
                c.a = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
                f.Text.color = c;
            }
        }
    }
}
