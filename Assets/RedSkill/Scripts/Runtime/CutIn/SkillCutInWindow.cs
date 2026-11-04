using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// カットイン1回分（窓1つ）。部品（絵・白い光・稲妻2本・技名）を作り、<see cref="Tick"/> で動かす。消え終わったら自分で部品を消す。
    /// 小さい窓では、欄の丸いアイコンの上に出す顔（色の輪・光つき）も持ち、窓はそのアイコンに吸い込まれて消える（設定による）。
    /// 形と動きの計算は <see cref="SkillCutInMath"/>、置き場所（大きい窓か、どの欄のそばの小さい窓か）は <see cref="SkillCutInDirector"/> が決める。
    /// </summary>
    internal sealed class SkillCutInWindow
    {
        // 窓が吸い込まれたあと、アイコンの顔が光って消えるまでの秒数。
        private const float IconTail = 0.3f;
        private const float IconRingWidth = 6f;
        private const float IconWhiteWidth = 4f;
        private const float IconGlowWidth = 24f;

        private readonly SkillCutInSettings _settings;
        private readonly SkillCutInEntry _entry;
        private readonly bool _big;
        private readonly bool _shrink;
        private readonly GameObject _root;
        private readonly CutInQuadGraphic _picture;
        private readonly CutInQuadGraphic _flash;
        private readonly CutInBoltGraphic _edge;
        private readonly CutInBoltGraphic _line;
        private readonly Text _text;
        private readonly RectTransform _textRect;
        private readonly CutInDiscGraphic _iconGlow;
        private readonly CutInDiscGraphic _iconWhite;
        private readonly CutInDiscGraphic _iconRing;
        private readonly CutInDiscGraphic _iconFace;
        private readonly CutInDiscGraphic _iconBurst;
        private Shadow[] _textEffects;

        private SkillCutInMath.Shapes _shapes;
        private Rect _pictureRect;
        private Vector2 _anchor;
        private Vector2 _iconCenter;
        private float _iconRadius;
        private Rect _iconPictureRect;
        private float _time;
        private float _closeAt = -1f;
        private float _sinceDone;
        private float _nextJitter;
        private bool _closeSoundPlayed;

        public SkillCutInWindow(SkillCutInSettings settings, SkillCutInEntry entry, bool big, RectTransform parent)
        {
            _settings = settings;
            _entry = entry;
            _big = big;
            _shrink = !big && settings.small.end == SkillCutInSmallEnd.ShrinkToIcon;

            _root = new GameObject(big ? "CutIn (Big)" : "CutIn (Small)", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            rect.SetParent(parent, false);
            Stretch(rect);

            _picture = Add<CutInQuadGraphic>("Picture");
            _picture.Sprite = entry.picture;
            _flash = Add<CutInQuadGraphic>("Flash");
            _edge = Add<CutInBoltGraphic>("Edge");
            _line = Add<CutInBoltGraphic>("Line");

            var edge = settings.edge;
            var scale = big ? 1f : edge.smallScale;
            var glow = entry.color;
            glow.a = 0.85f;
            _edge.SetStyle(edge.coreWidth * scale, edge.glowWidth * scale, glow);
            _line.SetStyle(edge.coreWidth * scale, edge.glowWidth * scale, glow);

            var label = entry.SkillNameOrDefault;
            if (settings.text.show && !string.IsNullOrWhiteSpace(label))
            {
                _text = Add<Text>("SkillName");
                _text.font = settings.text.font != null ? settings.text.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _text.text = label;
                _text.fontSize = big ? settings.text.bigSize : settings.text.smallSize;
                _text.fontStyle = FontStyle.BoldAndItalic;
                _text.color = Color.white;
                _text.alignment = TextAnchor.LowerLeft;
                _text.horizontalOverflow = HorizontalWrapMode.Overflow;
                _text.verticalOverflow = VerticalWrapMode.Overflow;
                var width = settings.text.outline * scale;
                if (width > 0f)
                {
                    var outline = _text.gameObject.AddComponent<Outline>();
                    outline.effectColor = entry.color;
                    outline.effectDistance = new Vector2(width, -width);
                    var shadow = _text.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0.05f, 0.04f, 0.15f, 0.8f);
                    shadow.effectDistance = new Vector2(width * 1.5f, -width * 1.5f);
                }

                _textRect = _text.rectTransform;
                _textRect.anchorMin = _textRect.anchorMax = Vector2.zero;
                _textRect.pivot = Vector2.zero;
                _textRect.sizeDelta = new Vector2(10f, 10f);
            }

            // 欄の丸いアイコンの上の顔（窓より手前。窓はここへ吸い込まれる）。
            if (!big && settings.small.iconFace)
            {
                _iconGlow = Add<CutInDiscGraphic>("IconGlow");
                _iconWhite = Add<CutInDiscGraphic>("IconWhite");
                _iconRing = Add<CutInDiscGraphic>("IconRing");
                _iconFace = Add<CutInDiscGraphic>("IconFace");
                _iconFace.Sprite = entry.picture;
                _iconBurst = Add<CutInDiscGraphic>("IconBurst");
            }
        }

        /// <summary>消え終わったか。</summary>
        public bool Done { get; private set; }

        /// <summary>大きい窓か。</summary>
        public bool IsBig => _big;

        /// <summary>閉じ始めたか。</summary>
        public bool IsClosing { get; private set; }

        /// <summary>
        /// 置き場所を決める（毎フレーム呼んでよい。画面の大きさや HUD の欄が動いても追いかける）。
        /// <paramref name="shapes"/> は窓の形、<paramref name="anchor"/> は顔を置く場所、<paramref name="pictureHeight"/> は絵の高さ。
        /// 小さい窓では <paramref name="iconRadius"/> に欄の丸いアイコンの半径を渡す（中心は <see cref="SkillCutInMath.Shapes.Pivot"/>）。
        /// </summary>
        public void Place(SkillCutInMath.Shapes shapes, Vector2 anchor, float pictureHeight, float iconRadius = 0f)
        {
            _shapes = shapes;
            _anchor = anchor;
            var sprite = _entry.picture;
            var size = sprite != null ? sprite.rect.size : Vector2.one;
            _pictureRect = SkillCutInMath.PictureRect(size, _entry.face, anchor, pictureHeight * _entry.faceScale, SkillCutInMath.Bounds(shapes.Open));

            if (_iconFace != null)
            {
                var small = _settings.small;
                _iconCenter = shapes.Pivot;
                _iconRadius = iconRadius * small.iconFaceSize;
                var d = _iconRadius * 2f;
                var cover = new Rect(_iconCenter.x - _iconRadius, _iconCenter.y - _iconRadius, d, d);
                _iconPictureRect = SkillCutInMath.PictureRect(size, _entry.face, _iconCenter, d * small.iconPictureHeight * _entry.faceScale, cover);
            }
        }

        /// <summary>次のカットインに切り替えるので閉じる（開いている途中なら、開ききってから閉じる）。</summary>
        public void RequestClose()
        {
            if (_closeAt < 0f)
            {
                _closeAt = Mathf.Max(_time, _settings.motion.openSeconds);
            }
        }

        public void Tick(float dt)
        {
            if (Done)
            {
                return;
            }

            _time += dt;
            var pose = SkillCutInMath.Evaluate(_settings.motion, _shapes, _time, _closeAt, _shrink);
            if (pose.Done)
            {
                _sinceDone += dt;
            }

            Apply(pose);
            ApplyIcon(pose);

            if (pose.Closing && !_closeSoundPlayed)
            {
                _closeSoundPlayed = true;
                IsClosing = true;
                if (_big || _settings.sounds.smallPlaysSe)
                {
                    SkillCutInDirector.PlaySound(_settings.sounds.close);
                }
            }

            // アイコンの顔があるときは、窓が吸い込まれたあと顔が光って消えるまで待つ。
            if (pose.Done && (_iconFace == null || _sinceDone >= IconTail))
            {
                Destroy();
            }
        }

        public void Destroy()
        {
            Done = true;
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        private void Apply(SkillCutInMath.Pose pose)
        {
            // 吸い込まれるときは、窓全体（角・絵・技名）を行き先を中心に縮める。
            var pivot = _shapes.Pivot;
            if (!Mathf.Approximately(pose.Scale, 1f))
            {
                pose.BottomLeft = pivot + (pose.BottomLeft - pivot) * pose.Scale;
                pose.TopLeft = pivot + (pose.TopLeft - pivot) * pose.Scale;
                pose.TopRight = pivot + (pose.TopRight - pivot) * pose.Scale;
                pose.BottomRight = pivot + (pose.BottomRight - pivot) * pose.Scale;
            }

            _picture.SetCorners(pose.BottomLeft, pose.TopLeft, pose.TopRight, pose.BottomRight);
            _picture.PictureRect = SkillCutInMath.ScaleAround(SkillCutInMath.ScaleAround(_pictureRect, _anchor, pose.Zoom), pivot, pose.Scale);
            _picture.Sprite = pose.Later && _entry.laterPicture != null ? _entry.laterPicture : _entry.picture;
            _picture.color = new Color(1f, 1f, 1f, pose.Alpha);

            _flash.SetCorners(pose.BottomLeft, pose.TopLeft, pose.TopRight, pose.BottomRight);
            _flash.color = new Color(1f, 1f, 1f, pose.Flash * pose.Alpha);

            // ふちの稲妻は窓の右のふち（右上 → 右下）。消えるときの線は帯の真ん中を左から右へ。
            var edgeA = pose.TopRight;
            var edgeB = pose.BottomRight;
            var lineA = (pose.BottomLeft + pose.TopLeft) * 0.5f;
            var lineB = (pose.BottomRight + pose.TopRight) * 0.5f;
            var edge = _settings.edge;
            var scale = _big ? 1f : edge.smallScale;
            if (_time >= _nextJitter)
            {
                _nextJitter = _time + edge.refreshSeconds;
                _edge.Rebuild(edgeA, edgeB, edge.segments, edge.jag * scale);
                _line.Rebuild(lineA, lineB, edge.segments + 4, edge.jag * scale * 0.9f);
            }
            else
            {
                _edge.Refit(edgeA, edgeB);
                _line.Refit(lineA, lineB);
            }

            _edge.color = new Color(1f, 1f, 1f, pose.EdgeAlpha);
            _line.color = new Color(1f, 1f, 1f, pose.LineAlpha);

            if (_text != null)
            {
                var height = _shapes.Open[1].y - _shapes.Open[0].y;
                var basePos = _big
                    ? Vector2.Scale(_settings.text.bigPosition, ((RectTransform)_root.transform).rect.size)
                    : _shapes.Open[0] + new Vector2(height * 0.12f, height * 0.1f);
                var pos = basePos + new Vector2(-pose.TextSlide * _text.fontSize, 0f);
                _textRect.anchoredPosition = pivot + (pos - pivot) * pose.Scale;
                _textRect.localScale = new Vector3(pose.Scale, pose.Scale, 1f);
                _text.color = new Color(1f, 1f, 1f, pose.TextAlpha);
                SetEffectAlpha(pose.TextAlpha);
            }
        }

        // 欄のアイコンの上の顔: 出た瞬間に弾み、窓が吸い込まれたら少し膨らんで光の輪を出し、消える。
        private void ApplyIcon(SkillCutInMath.Pose pose)
        {
            if (_iconFace == null)
            {
                return;
            }

            SkillCutInMath.IconFace(_time, pose, _sinceDone, IconTail, out var scale, out var alpha);
            var r = _iconRadius * scale;
            var c = _iconCenter;
            var col = _entry.color;
            var glow = col;
            glow.a = 0.8f;
            var clear = col;
            clear.a = 0f;
            var inner = Mathf.Max(0.5f, r - IconRingWidth);
            _iconGlow.SetRing(c, r + IconWhiteWidth, r + IconWhiteWidth + IconGlowWidth, glow, clear);
            _iconWhite.SetRing(c, r, r + IconWhiteWidth, Color.white, Color.white);
            _iconRing.SetRing(c, inner, r, col, col);
            _iconFace.SetDisc(c, inner);
            _iconFace.PictureRect = SkillCutInMath.ScaleAround(_iconPictureRect, c, scale);
            var tint = new Color(1f, 1f, 1f, alpha);
            _iconGlow.color = tint;
            _iconWhite.color = tint;
            _iconRing.color = tint;
            _iconFace.color = tint;

            // 窓が吸い込まれた瞬間に、キャラの色の輪が広がって消える。
            var u = pose.Done ? Mathf.Clamp01(_sinceDone / IconTail) : 0f;
            var burst = _iconRadius * Mathf.Lerp(0.9f, 2.4f, SkillCutInMath.EaseOut(u));
            _iconBurst.SetRing(c, burst, burst + IconRingWidth, col, col);
            _iconBurst.color = new Color(1f, 1f, 1f, pose.Done ? 1f - u : 0f);
        }

        private void SetEffectAlpha(float alpha)
        {
            _textEffects ??= _text.GetComponents<Shadow>();
            foreach (var effect in _textEffects)
            {
                var c = effect.effectColor;
                c.a = effect is Outline ? alpha : 0.8f * alpha;
                effect.effectColor = c;
            }
        }

        private T Add<T>(string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_root.transform, false);
            Stretch(rect);
            // 自作の Graphic（CutInQuadGraphic など）は、AddComponent しても CanvasRenderer が自動で付かない
            // （RequireComponent が効かない）ので先に付ける。付いていないと何も描かれない。
            go.AddComponent<CanvasRenderer>();
            var graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        // 親いっぱいに広げ、ピボットを左下にする（中の座標 = 左下からの位置）。
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
