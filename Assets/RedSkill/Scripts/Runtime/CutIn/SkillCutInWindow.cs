using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Skills
{
    /// <summary>
    /// カットイン1回分（窓1つ）。部品（絵・白い光・稲妻2本・技名）を作り、<see cref="Tick"/> で動かす。消え終わったら自分で部品を消す。
    /// 形と動きの計算は <see cref="SkillCutInMath"/>、置き場所（大きい窓か、どの欄のそばの小さい窓か）は <see cref="SkillCutInDirector"/> が決める。
    /// </summary>
    internal sealed class SkillCutInWindow
    {
        private readonly SkillCutInSettings _settings;
        private readonly SkillCutInEntry _entry;
        private readonly bool _big;
        private readonly GameObject _root;
        private readonly CutInQuadGraphic _picture;
        private readonly CutInQuadGraphic _flash;
        private readonly CutInBoltGraphic _edge;
        private readonly CutInBoltGraphic _line;
        private readonly Text _text;
        private readonly RectTransform _textRect;
        private Shadow[] _textEffects;

        private SkillCutInMath.Shapes _shapes;
        private Rect _pictureRect;
        private Vector2 _anchor;
        private float _time;
        private float _closeAt = -1f;
        private float _nextJitter;
        private bool _closeSoundPlayed;

        public SkillCutInWindow(SkillCutInSettings settings, SkillCutInEntry entry, bool big, RectTransform parent)
        {
            _settings = settings;
            _entry = entry;
            _big = big;

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
        /// </summary>
        public void Place(SkillCutInMath.Shapes shapes, Vector2 anchor, float pictureHeight)
        {
            _shapes = shapes;
            _anchor = anchor;
            var sprite = _entry.picture;
            var size = sprite != null ? sprite.rect.size : Vector2.one;
            _pictureRect = SkillCutInMath.PictureRect(size, _entry.face, anchor, pictureHeight * _entry.faceScale, SkillCutInMath.Bounds(shapes.Open));
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
            var pose = SkillCutInMath.Evaluate(_settings.motion, _shapes, _time, _closeAt);
            Apply(pose);

            if (pose.Closing && !_closeSoundPlayed)
            {
                _closeSoundPlayed = true;
                IsClosing = true;
                if (_big || _settings.sounds.smallPlaysSe)
                {
                    SkillCutInDirector.PlaySound(_settings.sounds.close);
                }
            }

            if (pose.Done)
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
            _picture.SetCorners(pose.BottomLeft, pose.TopLeft, pose.TopRight, pose.BottomRight);
            _picture.PictureRect = SkillCutInMath.ScaleAround(_pictureRect, _anchor, pose.Zoom);
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
                var basePos = _big
                    ? Vector2.Scale(_settings.text.bigPosition, ((RectTransform)_root.transform).rect.size)
                    : new Vector2(_shapes.Open[0].x + 12f, _shapes.Open[0].y + 6f);
                _textRect.anchoredPosition = basePos + new Vector2(-pose.TextSlide * _text.fontSize, 0f);
                _text.color = new Color(1f, 1f, 1f, pose.TextAlpha);
                SetEffectAlpha(pose.TextAlpha);
            }
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
