using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// コアの周りを回る葉の輪（決定版「葉C 寄せては返す輪」）。8枚の葉が回りながら、輪の半径がコアのすぐそば（2.6m）と 8.4m の間を
    /// 約3.2秒ごとに行き来する（1枚おきに少し遅れて、波のように見える）。葉は進む向きに先を向け、ゆらゆら傾き、後ろに光の尾を引く。
    /// 握ると寄せては返すのが速く（約2.2秒）、回転も速くなり、葉が大きく光る。
    /// 出るとき（着地の後）は1枚ずつコアから弾むように広がる。終わりは渦になって速く回りながらコアへ吸い込まれる。
    /// 当たり判定は <see cref="GreenLeafContacts"/> がこの位置と大きさを使う。
    /// </summary>
    internal sealed class GreenLeafRing
    {
        public const int MaxLeaves = 16;
        private const int PathPoints = 72;

        private sealed class Leaf
        {
            public SpriteRenderer Sprite;
            public TrailRenderer Trail;
            public MeshRenderer Glow;
            public float Angle;
            public float Phase;
            public float Hit;
            public Vector2 Position;
            public float Length;
            public float Alpha;
            public bool Visible;
        }

        private readonly GreenMintFx _fx;
        private readonly Leaf[] _leaves = new Leaf[MaxLeaves];
        private readonly LineRenderer _path;
        private readonly Vector3[] _pathPoints = new Vector3[PathPoints];
        private float _pulse;

        public GreenLeafRing(GreenMintFx fx)
        {
            _fx = fx;
            const int order = GreenMintFx.SortingBase - 6;
            for (var i = 0; i < MaxLeaves; i++)
            {
                var leaf = new Leaf { Sprite = fx.CreateSprite("Leaf" + i, null, order + 2) };
                leaf.Glow = fx.CreateQuad("LeafGlow" + i, fx.RadialMaterial, order + 1);
                var trailObject = new GameObject("LeafTrail" + i) { hideFlags = HideFlags.DontSave };
                trailObject.transform.SetParent(fx.Root, false);
                var trail = trailObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = fx.TrailMaterial;
                trail.minVertexDistance = 0.08f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
                trail.sortingOrder = order;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;
                trail.numCapVertices = 2;
                leaf.Trail = trail;
                _leaves[i] = leaf;
            }

            var pathObject = new GameObject("LeafPath") { hideFlags = HideFlags.DontSave };
            pathObject.transform.SetParent(fx.Root, false);
            _path = pathObject.AddComponent<LineRenderer>();
            _path.sharedMaterial = fx.TrailMaterial;
            _path.loop = true;
            _path.useWorldSpace = true;
            _path.positionCount = PathPoints;
            _path.sortingOrder = order - 1;
            _path.shadowCastingMode = ShadowCastingMode.Off;
            _path.receiveShadows = false;
            _path.enabled = false;
            ResetLeaves();
        }

        /// <summary>今使っている葉の数。</summary>
        public int Count => Mathf.Clamp(_fx.Definition.leaves.count, 1, MaxLeaves);

        /// <summary>今フレームの葉の位置。</summary>
        public Vector2 PositionOf(int index) => _leaves[index].Position;

        /// <summary>葉が見えていて、当たってよい状態か。</summary>
        public bool IsTouchable(int index) => _leaves[index].Visible && _leaves[index].Alpha > 0.5f;

        /// <summary>今の葉の大きさ（当たり判定の倍率。握ると大きい）。</summary>
        public float SizeScale { get; private set; } = 1f;

        /// <summary>葉の向き（度、地面の上で）。当たったときの白い線に使う。</summary>
        public float RotationOf(int index) => _leaves[index].Sprite.transform.eulerAngles.z;

        /// <summary>葉が敵に当たった（一瞬光る）。</summary>
        public void MarkHit(int index)
        {
            _leaves[index].Hit = 1f;
        }

        /// <summary>葉をばらばらの向きから始め直す（発動のたび）。</summary>
        public void ResetLeaves()
        {
            _pulse = 0f;
            var n = Count;
            for (var i = 0; i < MaxLeaves; i++)
            {
                var leaf = _leaves[i];
                leaf.Angle = i / (float)n * Mathf.PI * 2f;
                leaf.Phase = Random.Range(0f, Mathf.PI * 2f);
                leaf.Hit = 0f;
                leaf.Visible = false;
                leaf.Alpha = 0f;
            }
        }

        public void Hide()
        {
            for (var i = 0; i < MaxLeaves; i++)
            {
                HideLeaf(_leaves[i]);
            }

            _path.enabled = false;
        }

        /// <param name="core">コアの位置。</param>
        /// <param name="appear">出てくる進み具合（0 = まだ、1 = 広がりきった）。</param>
        /// <param name="suck">吸い込まれる進み具合（負なら吸い込み中ではない、0〜1）。</param>
        /// <param name="boost">握る強さ（0〜1、なめらか）。</param>
        public void Tick(Vector2 core, float appear, float suck, float boost, float time, float dt)
        {
            if (appear <= 0f)
            {
                Hide();
                return;
            }

            var settings = _fx.Definition.leaves;
            var boostSettings = _fx.Definition.boost;
            var palette = _fx.Definition.palette;
            var n = Count;
            var sucking = suck >= 0f;
            var suckEase = sucking ? GreenMintUtil.EaseInCubic(suck) : 0f;
            var spinBoost = sucking ? 1f + 4f * suck : 1f;
            var period = Mathf.Lerp(settings.pulseSeconds, boostSettings.pulseSeconds, boost);
            _pulse += dt * Mathf.PI * 2f / Mathf.Max(0.2f, period);
            var spin = Mathf.Lerp(settings.spinDegreesPerSecond, boostSettings.spinDegreesPerSecond, boost) * Mathf.Deg2Rad * spinBoost;
            SizeScale = Mathf.Lerp(1f, boostSettings.sizeScale, boost);
            var lit = Mathf.Clamp01(boost);
            var leafSprite = _fx.Definition.art.leaf != null ? _fx.Definition.art.leaf : _fx.Art.Leaf;
            var spriteWidth = Mathf.Max(0.01f, leafSprite.bounds.size.x);
            var delay = settings.alternateDelay * Mathf.PI * 2f;
            var z = GreenMintFx.ZOffset - 0.06f;

            for (var i = 0; i < MaxLeaves; i++)
            {
                var leaf = _leaves[i];
                if (i >= n)
                {
                    HideLeaf(leaf);
                    continue;
                }

                leaf.Angle += spin * dt;
                leaf.Hit = Mathf.Max(0f, leaf.Hit - dt * 4f);

                // 1枚ずつ少し遅れて、コアから弾むように広がる。
                var stagger = Mathf.Clamp01((appear - i / (float)n * 0.35f) / 0.65f);
                var radius = PulseRadius(i % 2 == 1 ? delay : 0f) * GreenMintUtil.EaseOutBack(stagger);
                var length = settings.length * SizeScale * Mathf.Min(1f, stagger * 3f);
                var alpha = 1f;
                var offset = new Vector2(Mathf.Cos(leaf.Angle), Mathf.Sin(leaf.Angle)) * radius;
                if (sucking)
                {
                    offset *= 1f - suckEase;
                    length *= 1f - 0.6f * suckEase;
                    alpha = 1f - GreenMintUtil.Segment(suck, 0.85f, 1f);
                }

                leaf.Position = core + offset;
                leaf.Length = length;
                leaf.Alpha = alpha;
                leaf.Visible = alpha > 0.01f && length > 0.02f;
                if (!leaf.Visible)
                {
                    HideLeaf(leaf);
                    continue;
                }

                // 進む向きに先を向けて、ゆらゆら傾く。
                var rotation = leaf.Angle * Mathf.Rad2Deg + 90f + settings.wobbleDegrees * Mathf.Sin(time * 5f + leaf.Phase);
                var punch = 1f + 0.15f * leaf.Hit;
                var scale = length / spriteWidth * punch;
                var position = new Vector3(leaf.Position.x, leaf.Position.y, z);
                leaf.Sprite.sprite = leafSprite;
                leaf.Sprite.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
                leaf.Sprite.transform.localScale = new Vector3(scale, scale, 1f);
                leaf.Sprite.color = new Color(1f, 1f, 1f, alpha);
                leaf.Sprite.enabled = true;

                var glowStrength = (0.35f * lit + 0.55f * leaf.Hit) * alpha;
                if (glowStrength > 0.01f)
                {
                    var glowSize = length * 1.8f;
                    leaf.Glow.transform.SetPositionAndRotation(position + new Vector3(0f, 0f, 0.004f), Quaternion.identity);
                    leaf.Glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
                    _fx.ApplySoftGlow(leaf.Glow, palette.glow * glowStrength);
                    leaf.Glow.enabled = true;
                }
                else
                {
                    leaf.Glow.enabled = false;
                }

                var trail = leaf.Trail;
                trail.transform.position = position + new Vector3(0f, 0f, 0.006f);
                if (!trail.emitting)
                {
                    trail.Clear();
                }

                trail.time = settings.trailSeconds;
                trail.widthMultiplier = length * settings.trailWidth;
                var trailColor = palette.leafLight;
                trailColor.a = Mathf.Lerp(0.3f, 0.45f, lit) * alpha;
                trail.startColor = trailColor;
                trailColor.a = 0f;
                trail.endColor = trailColor;
                trail.emitting = settings.trailSeconds > 0f;
            }

            UpdatePath(core, appear, sucking, lit);
        }

        /// <summary>寄せては返す輪の今の半径（lag = 遅れの角度）。</summary>
        private float PulseRadius(float lag)
        {
            var settings = _fx.Definition.leaves;
            var t = 0.5f - 0.5f * Mathf.Cos(_pulse + lag);
            return Mathf.Lerp(settings.minRadius, Mathf.Max(settings.minRadius, settings.maxRadius), t);
        }

        private void UpdatePath(Vector2 core, float appear, bool sucking, float lit)
        {
            if (!_fx.Definition.leaves.showPathRing || sucking)
            {
                _path.enabled = false;
                return;
            }

            var radius = PulseRadius(0f) * GreenMintUtil.EaseOutCubic(Mathf.Clamp01(appear * 1.4f));
            var z = GreenMintFx.ZOffset - 0.02f;
            for (var i = 0; i < PathPoints; i++)
            {
                var a = i / (float)PathPoints * Mathf.PI * 2f;
                _pathPoints[i] = new Vector3(core.x + Mathf.Cos(a) * radius, core.y + Mathf.Sin(a) * radius, z);
            }

            _path.SetPositions(_pathPoints);
            _path.widthMultiplier = 1.1f;
            var color = _fx.Definition.palette.leafLight;
            color.a = Mathf.Lerp(0.13f, 0.2f, lit) * Mathf.Min(1f, appear * 2f);
            _path.startColor = color;
            _path.endColor = color;
            _path.enabled = true;
        }

        private static void HideLeaf(Leaf leaf)
        {
            leaf.Visible = false;
            leaf.Sprite.enabled = false;
            leaf.Glow.enabled = false;
            if (leaf.Trail.emitting)
            {
                leaf.Trail.emitting = false;
                leaf.Trail.Clear();
            }
        }
    }
}
