using System;
using System.Globalization;
using UnityEngine;

namespace MS2026.UI
{
    public enum UiValueKind
    {
        None,
        Number,
        Bool,
        Text,
        Color
    }

    /// <summary>
    /// 画面に渡す値1つ（数・ON/OFF・文字・色のどれか）。数とON/OFFは割り当てなしで受け渡せる。
    /// </summary>
    public readonly struct UiValue : IEquatable<UiValue>
    {
        public readonly UiValueKind Kind;
        public readonly float Number;
        public readonly string Text;
        public readonly Color Color;

        private UiValue(UiValueKind kind, float number, string text, Color color)
        {
            Kind = kind;
            Number = number;
            Text = text;
            Color = color;
        }

        public static UiValue Of(float number) => new UiValue(UiValueKind.Number, number, null, default);
        public static UiValue Of(bool value) => new UiValue(UiValueKind.Bool, value ? 1f : 0f, null, default);
        public static UiValue Of(string text) => new UiValue(UiValueKind.Text, 0f, text ?? string.Empty, default);
        public static UiValue Of(Color color) => new UiValue(UiValueKind.Color, 0f, null, color);

        public bool HasValue => Kind != UiValueKind.None;

        /// <summary>ON/OFFとして見たとき（数なら0.5より大きければON、文字なら空でなければON）。</summary>
        public bool AsBool => Kind switch
        {
            UiValueKind.Text => !string.IsNullOrEmpty(Text),
            UiValueKind.Color => Color.a > 0f,
            _ => Number > 0.5f
        };

        /// <summary>数として見たとき（文字は数に読めれば数、読めなければ0）。</summary>
        public float AsNumber => Kind == UiValueKind.Text
            ? float.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0f
            : Number;

        /// <summary>文字にする。format は string.Format の書式（例: "{0:0}%"、"残り{0:0.0}秒"）。空なら値そのまま。</summary>
        public string Format(string format)
        {
            object boxed = Kind switch
            {
                UiValueKind.Text => Text,
                UiValueKind.Bool => AsBool ? "ON" : "OFF",
                UiValueKind.Color => "#" + ColorUtility.ToHtmlStringRGBA(Color),
                UiValueKind.None => string.Empty,
                _ => Number
            };

            if (string.IsNullOrEmpty(format))
            {
                return boxed is float f ? f.ToString("0.##", CultureInfo.InvariantCulture) : boxed.ToString();
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, boxed);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public bool Equals(UiValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }

            return Kind switch
            {
                UiValueKind.Text => Text == other.Text,
                UiValueKind.Color => Color == other.Color,
                UiValueKind.None => true,
                _ => Mathf.Abs(Number - other.Number) < 1e-5f
            };
        }

        public override bool Equals(object obj) => obj is UiValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((((int)Kind * 397) ^ Number.GetHashCode()) * 397 ^ (Text?.GetHashCode() ?? 0)) * 397 ^ Color.GetHashCode();
            }
        }

        public override string ToString() => Format(null);
    }
}
