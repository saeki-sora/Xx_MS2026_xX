using MS2026.StudioKit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 光ページ: 太陽が「どこから照らすか」を円の上で決める。円はゲーム画面を真上から見た形（上=画面の上）で、
    /// 中央=真上から、ふちに近いほど低い（影が長い）。点をドラッグすると「照らす向き」と「高さ」が変わる。
    /// </summary>
    public sealed class SunDirectionPad : VisualElement
    {
        private const string DirectionPath = "lighting.sunDirection";
        private const string ElevationPath = "lighting.sunElevation";
        private const string ColorPath = "lighting.sunColor";
        private const float Size = 168f;
        private const float Margin = 14f;

        private readonly SerializedObject _so;
        private readonly SerializedProperty _direction;
        private readonly SerializedProperty _elevation;
        private readonly SerializedProperty _color;
        private readonly Label _caption;
        private int _undoGroup = -1;

        private SunDirectionPad(SerializedObject so, Label caption)
        {
            _so = so;
            _direction = so.FindProperty(DirectionPath);
            _elevation = so.FindProperty(ElevationPath);
            _color = so.FindProperty(ColorPath);
            _caption = caption;

            style.width = Size;
            style.height = Size;
            style.alignSelf = Align.Center;
            style.marginTop = 4;
            tooltip = "太陽の点をドラッグして、照らす向きと高さを決めます。円の上=ゲーム画面の上。中央=真上から（影が短い）、ふち=低い所から（影が長い）。";

            AddCompassLabel("画面の上", 0f, -1f);
            AddCompassLabel("下", 0f, 1f);
            AddCompassLabel("左", -1f, 0f);
            AddCompassLabel("右", 1f, 0f);

            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>円と説明の一式を作る（項目が見つからなければ空の要素）。</summary>
        public static VisualElement Create(SerializedObject so)
        {
            var box = new VisualElement();
            if (so.FindProperty(DirectionPath) == null || so.FindProperty(ElevationPath) == null)
            {
                return box;
            }

            var caption = StudioUi.Styled(new Label(), "sk-card-body");
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            var pad = new SunDirectionPad(so, caption);
            box.Add(pad);
            box.Add(caption);
            pad.UpdateCaption();

            // 数値の欄や Ctrl+Z で値が変わったら描き直す。
            pad.TrackPropertyValue(pad._direction, _ => pad.Refresh());
            pad.TrackPropertyValue(pad._elevation, _ => pad.Refresh());
            if (pad._color != null)
            {
                pad.TrackPropertyValue(pad._color, _ => pad.MarkDirtyRepaint());
            }

            return box;
        }

        private float Radius => Size * 0.5f - Margin;

        private Vector2 Center => new Vector2(Size * 0.5f, Size * 0.5f);

        private void Refresh()
        {
            UpdateCaption();
            MarkDirtyRepaint();
        }

        private void UpdateCaption()
        {
            _caption.text = $"照らす向き {Mathf.RoundToInt(_direction.floatValue)}°（画面の上から時計回り）・高さ {Mathf.RoundToInt(_elevation.floatValue)}°";
        }

        private void AddCompassLabel(string text, float x, float y)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.fontSize = 9;
            label.style.color = new Color(1f, 1f, 1f, 0.55f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.width = 60;
            label.style.height = 12;
            var r = Size * 0.5f - Margin * 0.5f;
            label.style.left = Size * 0.5f + x * r - 30f;
            label.style.top = Size * 0.5f + y * r - 6f;
            Add(label);
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var center = Center;
            var radius = Radius;

            painter.fillColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();

            painter.lineWidth = 1f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
            foreach (var ringElevation in new[] { 30f, 60f })
            {
                painter.BeginPath();
                painter.Arc(center, radius * SunPadMath.RadiusFor(ringElevation), 0f, 360f);
                painter.Stroke();
            }

            painter.BeginPath();
            painter.MoveTo(center + new Vector2(-radius, 0f));
            painter.LineTo(center + new Vector2(radius, 0f));
            painter.MoveTo(center + new Vector2(0f, -radius));
            painter.LineTo(center + new Vector2(0f, radius));
            painter.Stroke();

            painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            var sun = center + SunPadMath.ToPad(_direction.floatValue, _elevation.floatValue) * radius;

            // 影が伸びる向き（太陽の反対側）。
            var shadow = center - (sun - center) * 0.6f;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.75f);
            painter.lineWidth = 4f;
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(shadow);
            painter.Stroke();

            // 光の進む向き（太陽 → 中央）。
            var sunColor = _color != null ? _color.colorValue : Color.white;
            sunColor.a = 1f;
            painter.strokeColor = new Color(sunColor.r, sunColor.g, sunColor.b, 0.7f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(sun);
            painter.LineTo(center);
            painter.Stroke();

            painter.fillColor = sunColor;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.6f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.Arc(sun, 7f, 0f, 360f);
            painter.Fill();
            painter.Stroke();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !enabledInHierarchy)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("太陽の向きを変える");
            _undoGroup = Undo.GetCurrentGroup();
            this.CapturePointer(evt.pointerId);
            SetFrom(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId))
            {
                SetFrom(evt.localPosition);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            this.ReleasePointer(evt.pointerId);
            if (_undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_undoGroup);
                _undoGroup = -1;
            }
        }

        private void SetFrom(Vector2 local)
        {
            var pad = (local - Center) / Radius;
            _so.Update();
            var (direction, elevation) = SunPadMath.FromPad(pad, _direction.floatValue);
            _direction.floatValue = direction;
            _elevation.floatValue = elevation;
            _so.ApplyModifiedProperties();
            Refresh();
        }
    }

    /// <summary>太陽の円の計算（画面上の点 ↔ 照らす向き・高さ）。点は円の半径を1とした値で、y は下が正（UIの向き）。</summary>
    public static class SunPadMath
    {
        public const float MinElevation = 5f;
        public const float MaxElevation = 90f;

        /// <summary>高さ（度）→ 円の中心からの距離（0=中央=真上、1=ふち=水平）。</summary>
        public static float RadiusFor(float elevation) => Mathf.Cos(Mathf.Clamp(elevation, 0f, 90f) * Mathf.Deg2Rad);

        public static Vector2 ToPad(float direction, float elevation)
        {
            var r = RadiusFor(elevation);
            var a = direction * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * r, -Mathf.Cos(a) * r);
        }

        /// <summary>円の上の点 → (照らす向き -180〜180, 高さ 5〜90)。中央付近は向きが決まらないので前の向きを使う。整数度に丸める。</summary>
        public static (float direction, float elevation) FromPad(Vector2 pad, float previousDirection)
        {
            var r = Mathf.Min(pad.magnitude, RadiusFor(MinElevation));
            var elevation = Mathf.Clamp(Mathf.Round(Mathf.Acos(r) * Mathf.Rad2Deg), MinElevation, MaxElevation);
            var direction = pad.sqrMagnitude < 1e-4f ? previousDirection : Mathf.Round(Mathf.Atan2(pad.x, -pad.y) * Mathf.Rad2Deg);
            if (direction > 180f)
            {
                direction -= 360f;
            }

            return (direction, elevation);
        }
    }
}
