using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Billboards;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Red
{
    /// <summary>
    /// スキルを使える間、砲台の上に乗る「強力そうな歯磨き粉」（<see cref="RedLaserVisuals"/> が1つ持つ）。
    /// 口（チューブの先）は砲台の向きを向き、スキルのレーザーはこの口から出る（<see cref="NozzlePosition"/>）。
    /// 動き: 使えるようになると「ポンッ」と出てきて、ふくらんだりしぼんだり・ブルブル震えて今にも出そうな状態 →
    /// ため中は真ん中を握られてくびれ、ほかがふくらむ → 撃った瞬間に反動で下がり、撃っている間はくびれが深くなりながら、
    /// 中身が出てやせていく（タイトルの冒頭の歯磨き粉と同じ。絵を横に細長く切った帯ごとに幅を変える）→ 撃ち終わると消える。
    /// 絵は設定ファイルの「歯磨き粉の絵」（口が上向きの絵）。空ならコードで描いた仮の絵（赤いチューブ）を使う。
    /// </summary>
    internal sealed class RedLaserTube
    {
        private const int PlaceholderWidth = 112;
        private const int PlaceholderHeight = 352;

        private static Sprite _placeholder;

        private readonly RedLaserSkill _def;
        private readonly LaserTurret _turret;
        private readonly GameObject _root;
        private readonly List<SpriteRenderer> _slices = new List<SpriteRenderer>();
        private readonly List<Sprite> _sliceSprites = new List<Sprite>();
        private Sprite _slicedFrom;
        private Vector3 _spriteSize = Vector3.one;
        private float _pinch;
        private float _swell;
        private float _sideShrink;

        private float _alpha;
        private float _appearTime;
        private float _recoil;
        private float _time;
        private bool _wanted;
        private float _tremblePhase;
        private float _trembleSign = 1f;
        private Vector2 _trembleOffset;
        private float _trembleTilt;

        public RedLaserTube(RedLaserSkill definition, LaserTurret turret, Transform parent)
        {
            _def = definition;
            _turret = turret;
            _root = new GameObject("PasteTube") { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(parent, false);

            // 床に寝かせたまま描く（ビルボードで起こさない。レーザーと同じ平面に置くため）。
            _root.AddComponent<BillboardIgnore>();
        }

        /// <summary>今、画面に出ているか（消えていく途中も含む）。</summary>
        public bool IsShown => _alpha > 0.001f;

        /// <summary>チューブの口（レーザーが出る所）。出ていないときは砲台の発射口。</summary>
        public Vector3 NozzlePosition { get; private set; }

        /// <summary>チューブの口が向いている向き（砲台の向き）。</summary>
        public Vector2 NozzleDirection { get; private set; } = Vector2.up;

        /// <summary>チューブの横幅の中心線から、絵の横幅（メートル）。稲妻をチューブのまわりに出すときに使う。</summary>
        public float Width => _def.tube.width;

        /// <summary>チューブの長さ（つぶれ具合を含む、メートル）。</summary>
        public float CurrentLength { get; private set; }

        /// <summary>撃ち始めの反動（チューブが後ろへ下がる）。</summary>
        public void Kick()
        {
            _recoil = 1f;
        }

        /// <summary>
        /// 毎フレーム呼ぶ。<paramref name="shown"/> = 出しておきたいか（使える・ため・撃っている間）。
        /// <paramref name="windup01"/> = ため（0〜1、ため中でなければ負）、<paramref name="fire01"/> = 撃っている進み具合（0〜1、撃っていなければ負）。
        /// </summary>
        public void Tick(float dt, bool shown, float windup01, float fire01)
        {
            var settings = _def.tube;
            _time += dt;
            if (!settings.enabled)
            {
                shown = false;
            }

            if (shown && !_wanted)
            {
                _appearTime = 0f;
            }

            _wanted = shown;
            _appearTime += dt;
            var fadeSpeed = 1f / Mathf.Max(0.01f, shown ? settings.appearSeconds : settings.fadeOutSeconds);
            _alpha = Mathf.MoveTowards(_alpha, shown ? 1f : 0f, dt * fadeSpeed);
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 4f);

            var aim = _turret.AimDirection;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.up;
            }

            aim.Normalize();
            NozzleDirection = aim;
            var side = new Vector2(aim.y, -aim.x);

            if (_alpha <= 0.001f)
            {
                SetSlicesVisible(false);
                NozzlePosition = _turret.MuzzlePosition;
                CurrentLength = 0f;
                return;
            }

            ApplySprite();

            // 出てくるときは「ポンッ」と少し大きくなってから落ち着く。消えるときは縮む。
            var appear = shown ? OverShoot(Mathf.Clamp01(_appearTime / Mathf.Max(0.01f, settings.appearSeconds))) : _alpha;

            // 踏ん張り: いつも少し押しつぶされて（短く太く）力んでいて、ときどき「ぐっ」と強く力む（力みの波）。
            // その上に、今にも出そうな「ふくらみ・しぼみ」。
            var windup = Mathf.Max(0f, windup01);
            var surge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * settings.strainSurgeFrequency * Mathf.PI * 2f)), 6f);
            var strain = settings.strainAmount * (1f + surge * 1.5f);
            var pulse = Mathf.Sin(_time * settings.pulseFrequency * Mathf.PI * 2f) * settings.pulseAmount;
            var lengthScale = (1f + pulse) * (1f - strain);
            var widthScale = (1f - pulse * 0.6f) * (1f + strain * 0.7f);

            // タイトルの冒頭と同じしぼり方（長さは変えない）: ため中に真ん中を握り始め（くびれ・ほかがふくらむ）、
            // 撃っている間はくびれが深くなりながら、中身が出てだんだんやせていく。消えていく間は最後の形のまま。
            if (fire01 >= 0f)
            {
                var p = Mathf.Clamp01(fire01);
                var smooth = p * p * (3f - 2f * p);

                // くびれは撃っている間じゅう、徐々に徐々に深くなる（撃ち終わりで一番深い）。
                var pinchGrip = Mathf.Lerp(settings.windupGrip, 1f, smooth);
                _pinch = settings.pinchAmount * pinchGrip;

                // ほかの部分は撃ち始めに中身が押されてふくらみ、そのあと中身が出てやせていく。
                var bulgeGrip = Mathf.Lerp(settings.windupGrip, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(p * 3f), 3f));
                _swell = Mathf.Lerp(settings.swellAmount * bulgeGrip, -settings.deflateAmount, smooth);

                // 横からも縮む: 中身が出るにつれて、チューブ全体が左右から細くなる（おしりの帯・キャップは少しだけ）。
                _sideShrink = settings.sideShrinkWhileFiring * (1f - (1f - p) * (1f - p));
            }
            else if (windup01 >= 0f)
            {
                var grip = settings.windupGrip * windup * windup;
                _pinch = settings.pinchAmount * grip;
                _swell = settings.swellAmount * grip;
                _sideShrink = 0f;
            }
            else if (shown)
            {
                _pinch = 0f;
                _swell = 0f;
                _sideShrink = 0f;
            }

            var length = settings.length * lengthScale * appear;
            var width = settings.width * widthScale * appear;
            CurrentLength = length;

            // ブルブル震える: 左右に行ったり来たりを速く繰り返す（1秒に「震えの速さ」回。画面の更新より速い分は毎フレーム反対へ）。
            // 力みの波・ため中・撃っている間はもっと強く。
            var shakeAmount = settings.shakeAmount * (1f + surge) * (1f + windup * (settings.windupShakeMultiplier - 1f))
                              * (fire01 >= 0f ? settings.windupShakeMultiplier : 1f);
            _tremblePhase += dt * settings.shakeFrequency * 2f;
            if (_tremblePhase >= 1f)
            {
                _tremblePhase -= Mathf.Floor(_tremblePhase);
                _trembleSign = -_trembleSign;
                _trembleOffset = new Vector2(_trembleSign * Random.Range(0.6f, 1f), Random.Range(-0.35f, 0.35f));
                _trembleTilt = -_trembleSign * Random.Range(0.4f, 1f);
            }

            var shake = (side * _trembleOffset.x + aim * _trembleOffset.y) * shakeAmount;
            var tilt = _trembleTilt * settings.shakeAngle * (1f + surge + windup);

            // 口の位置を基準にする（おしりがつぶれても口は動かない）。撃ち始めの反動で後ろへ下がる。
            var turretPosition = _turret.transform.position;
            var nozzle = (Vector2)turretPosition + aim * (settings.forwardOffset + settings.length * 0.5f)
                         + side * settings.sideOffset + shake - aim * (settings.recoilDistance * _recoil * _recoil);
            var center = nozzle - aim * (length * 0.5f);
            var z = turretPosition.z - 0.06f;
            var rotation = Quaternion.LookRotation(Vector3.forward, aim) * Quaternion.Euler(0f, 0f, tilt);

            _root.transform.SetPositionAndRotation(new Vector3(center.x, center.y, z), rotation);
            _root.transform.localScale = new Vector3(width / Mathf.Max(0.0001f, _spriteSize.x), length / Mathf.Max(0.0001f, _spriteSize.y), 1f);

            var color = settings.tint;
            color.a *= _alpha;
            ApplySliceShape(color, settings.sortingOrder);
            NozzlePosition = new Vector3(nozzle.x, nozzle.y, _turret.MuzzlePosition.z);
        }

        /// <summary>チューブの表面のどこか（稲妻・火花を出す場所）。along = -1 おしり〜+1 口、across = -1〜+1。</summary>
        public Vector2 SurfacePoint(float along, float across)
        {
            var aim = NozzleDirection;
            var side = new Vector2(aim.y, -aim.x);
            var center = (Vector2)NozzlePosition - aim * (CurrentLength * 0.5f);
            return center + aim * (along * CurrentLength * 0.5f) + side * (across * _def.tube.width * 0.5f);
        }

        /// <summary>切り分けた帯の絵を片付ける（見た目一式を消すとき）。</summary>
        public void Dispose()
        {
            ClearSlices();
        }

        public void Hide()
        {
            _alpha = 0f;
            _wanted = false;
            SetSlicesVisible(false);
        }

        /// <summary>
        /// 絵を横に細長い帯（「しぼりの帯の数」本）に切り分けて、帯ごとの SpriteRenderer を作る（絵が変わったときだけ作り直す）。
        /// どの絵でも同じように切れる（タイトルの冒頭の歯磨き粉と同じやり方）。
        /// </summary>
        private void ApplySprite()
        {
            var sprite = _def.tube.sprite != null ? _def.tube.sprite : Placeholder;
            var count = Mathf.Clamp(_def.tube.sliceCount, 4, 96);
            if (_slicedFrom == sprite && _slices.Count == count)
            {
                return;
            }

            ClearSlices();
            _slicedFrom = sprite;
            _spriteSize = sprite.bounds.size;
            var rect = sprite.rect;
            for (var i = 0; i < count; i++)
            {
                var sliceRect = new Rect(rect.x, rect.y + rect.height * i / count, rect.width, rect.height / count);
                var slice = Sprite.Create(sprite.texture, sliceRect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                slice.hideFlags = HideFlags.HideAndDontSave;
                _sliceSprites.Add(slice);

                var go = new GameObject("Slice" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = new Vector3(0f, _spriteSize.y * ((i + 0.5f) / count - 0.5f), 0f);
                go.AddComponent<BillboardIgnore>();
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = slice;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                _slices.Add(renderer);
            }
        }

        /// <summary>
        /// 帯ごとの幅: 1 + ふくらみ × 胴のふくらみ方 × (1 − くびれ) − くびれの深さ × くびれ（タイトルの冒頭と同じ式）。
        /// くびれ = exp(-((高さ − くびれる高さ)/くびれの幅)²)、胴のふくらみ方 = sin(π × 胴の中の高さ)^0.6（おしりの帯・キャップはふくらまない）。
        /// </summary>
        private void ApplySliceShape(Color color, int sortingOrder)
        {
            var settings = _def.tube;
            var count = _slices.Count;
            var range = Mathf.Max(0.001f, settings.swellRange.y - settings.swellRange.x);
            for (var i = 0; i < count; i++)
            {
                var v = (i + 0.5f) / count;
                var body = Mathf.Clamp01((v - settings.swellRange.x) / range);
                var bulge = v < settings.swellRange.x || v > settings.swellRange.y ? 0f : Mathf.Pow(Mathf.Sin(body * Mathf.PI), 0.6f);
                var d = (v - settings.pinchHeight) / Mathf.Max(0.001f, settings.pinchWidth);
                var pinchShape = Mathf.Exp(-d * d);
                var inBody = v >= settings.swellRange.x && v <= settings.swellRange.y;
                var width = (1f + _swell * bulge * (1f - pinchShape) - _pinch * pinchShape) * (1f - _sideShrink * (inBody ? 1f : 0.3f));
                var renderer = _slices[i];
                // となりの帯との間にすき間が見えないよう、少しだけ縦に重ねる。
                renderer.transform.localScale = new Vector3(Mathf.Max(0.05f, width), 1.04f, 1f);
                renderer.color = color;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = true;
            }
        }

        private void SetSlicesVisible(bool visible)
        {
            for (var i = 0; i < _slices.Count; i++)
            {
                _slices[i].enabled = visible;
            }
        }

        private void ClearSlices()
        {
            for (var i = 0; i < _slices.Count; i++)
            {
                if (_slices[i] != null)
                {
                    DestroySafe(_slices[i].gameObject);
                }
            }

            for (var i = 0; i < _sliceSprites.Count; i++)
            {
                DestroySafe(_sliceSprites[i]);
            }

            _slices.Clear();
            _sliceSprites.Clear();
            _slicedFrom = null;
        }

        private static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static float OverShoot(float t)
        {
            // 0→1 を少し行き過ぎてから戻る。
            const float S = 2.2f;
            var u = t - 1f;
            return 1f + u * u * ((S + 1f) * u + S);
        }

        /// <summary>
        /// 仮の絵（トゥーン調、タイトルの冒頭の歯磨き粉がお手本）: 縦長で、おしり（下）が太く口（上）へ向かって細くなるチューブ。
        /// おしりに太い帯、口は楕円のふち、その上にギザギザのすじのキャップ。太いふち線に、左にマゼンタ・右にシアンのずれた線（タイトルと同じ色ずれ）。
        /// 本体は薄いピンクに赤とピンクの縦じま2本、真ん中に赤い稲妻マーク。右側にべた塗りの影。
        /// </summary>
        private static Sprite Placeholder
        {
            get
            {
                if (_placeholder != null)
                {
                    return _placeholder;
                }

                var texture = new Texture2D(PlaceholderWidth, PlaceholderHeight, TextureFormat.RGBA32, false)
                {
                    name = "RedSkillPasteTube",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[PlaceholderWidth * PlaceholderHeight];
                for (var y = 0; y < PlaceholderHeight; y++)
                {
                    for (var x = 0; x < PlaceholderWidth; x++)
                    {
                        var u = (x + 0.5f) / PlaceholderWidth * 2f - 1f;
                        var t = (y + 0.5f) / PlaceholderHeight;
                        pixels[y * PlaceholderWidth + x] = PaintTube(u, t);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false);
                _placeholder = Sprite.Create(texture, new Rect(0f, 0f, PlaceholderWidth, PlaceholderHeight), new Vector2(0.5f, 0.5f), PlaceholderHeight);
                _placeholder.name = "RedSkillPasteTube";
                _placeholder.hideFlags = HideFlags.HideAndDontSave;
                return _placeholder;
            }
        }

        // 縦の区切り（t: 0 おしり 〜 1 口）
        private const float BarBottom = 0.02f;
        private const float BarTop = 0.07f;
        private const float MouthCenter = 0.835f;
        private const float MouthRadiusT = 0.022f;
        private const float CapBottom = 0.855f;
        private const float CapTop = 0.985f;
        private const float CapHalfWidth = 0.4f;
        private const float OffsetU = 0.07f;

        private static readonly Vector2[] BoltShape =
        {
            new Vector2(0.25f, 1f), new Vector2(-0.65f, -0.08f), new Vector2(-0.02f, -0.08f),
            new Vector2(-0.3f, -1f), new Vector2(0.7f, 0.16f), new Vector2(0.06f, 0.16f)
        };

        /// <summary>縦 t でのチューブの半分の幅（-1〜+1 の横 u で）。0 なら何も無い。</summary>
        private static float HalfWidthAt(float t)
        {
            if (t < BarBottom || t > CapTop)
            {
                return 0f;
            }

            if (t < BarTop)
            {
                return 0.9f;
            }

            if (t < MouthCenter)
            {
                return Mathf.Lerp(0.86f, 0.58f, (t - BarTop) / (MouthCenter - BarTop));
            }

            if (t < CapBottom)
            {
                // 口の楕円の上半分
                var k = (t - MouthCenter) / MouthRadiusT;
                return k < 1f ? 0.58f * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)) : 0f;
            }

            return CapHalfWidth;
        }

        private static bool Inside(float u, float t)
        {
            var hw = HalfWidthAt(t);
            return hw > 0f && Mathf.Abs(u) <= hw;
        }

        /// <summary>横 u（-1〜+1）・縦 t（0 おしり〜1 口）の1点の色。</summary>
        private static Color32 PaintTube(float u, float t)
        {
            const float OutlinePixels = 5.5f;
            var ink = new Color(0.62f, 0.02f, 0.1f);
            var magenta = new Color(1f, 0.25f, 0.75f);
            var cyan = new Color(0.35f, 0.9f, 1f);
            var body = new Color(1f, 0.93f, 0.94f);
            var red = new Color(0.95f, 0.13f, 0.18f);
            var pink = new Color(1f, 0.55f, 0.7f);

            var hw = HalfWidthAt(t);
            var au = Mathf.Abs(u);
            if (hw <= 0f || au > hw)
            {
                // 外側: 左にずれた形の内側ならマゼンタ、右にずれた形の内側ならシアン（タイトルと同じ色ずれ）。
                if (Inside(u + OffsetU, t) && u < 0f)
                {
                    return magenta;
                }

                if (Inside(u - OffsetU, t) && u > 0f)
                {
                    return cyan;
                }

                return new Color32(0, 0, 0, 0);
            }

            Color fill;
            if (t < BarTop)
            {
                // おしりの太い帯
                fill = ink;
            }
            else if (t < CapBottom)
            {
                fill = body;
                var x = u / hw;

                // 縦じま2本（赤とピンク）
                if (t > 0.17f && t < 0.73f)
                {
                    if (u > -0.46f && u < -0.32f)
                    {
                        fill = pink;
                    }
                    else if (u > 0.26f && u < 0.4f)
                    {
                        fill = red;
                    }
                }

                // 真ん中の稲妻マーク（ふち付き）
                var bolt = new Vector2((u + 0.03f) / 0.42f, (t - 0.45f) / 0.19f);
                if (InPolygon(bolt, BoltShape))
                {
                    fill = red;
                }
                else if (InPolygon(bolt * 0.86f, BoltShape))
                {
                    fill = ink;
                }

                // べた塗りの影（右側）と、左の細いツヤ
                if (x > 0.62f)
                {
                    fill *= 0.86f;
                }
                else if (x > -0.8f && x < -0.68f && t > 0.12f && t < 0.78f)
                {
                    fill = Color.white;
                }

                // 口の楕円のふち（下半分も線で描く）
                var ex = u / 0.58f;
                var ey = (t - MouthCenter) / MouthRadiusT;
                var ring = Mathf.Abs(Mathf.Sqrt(ex * ex + ey * ey) - 1f);
                if (ring < 0.16f && ey < 0.1f)
                {
                    fill = ink;
                }
            }
            else
            {
                // キャップ: 赤に、明るい縦すじ
                fill = Mathf.Repeat((u + 1f) * 3.2f, 1f) < 0.32f && au < CapHalfWidth - 0.1f && t > CapBottom + 0.015f && t < CapTop - 0.015f
                    ? new Color(1f, 0.6f, 0.65f)
                    : new Color(0.85f, 0.1f, 0.16f);
            }

            // 太いふち線（本体・キャップ・帯とも）。キャップと口の境目にも線。
            var edgePixels = (hw - au) * PlaceholderWidth * 0.5f;
            var endPixels = Mathf.Min(t - BarBottom, CapTop - t) * PlaceholderHeight;
            var capLine = Mathf.Abs(t - CapBottom) * PlaceholderHeight < 2f && au < CapHalfWidth;
            if (edgePixels < OutlinePixels || endPixels < OutlinePixels || capLine)
            {
                fill = ink;
            }

            fill.a = 1f;
            return fill;
        }

        private static bool InPolygon(Vector2 p, Vector2[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
