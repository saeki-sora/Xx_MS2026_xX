using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>材質の1つの値を、項目（ToonItem）の種類に合わせて読み書き・比べる。比べる相手が無いときはシェーダーの初期値と比べる。</summary>
    public static class ToonValues
    {
        private const float FloatEpsilon = 0.0005f;
        private const float ColorEpsilon = 1f / 512f;

        public static bool Has(Material material, ToonItem item)
        {
            return material != null && material.HasProperty(item.Id);
        }

        public static bool IsColor(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 && material.shader.GetPropertyType(i) == ShaderPropertyType.Color;
        }

        public static bool IsHdr(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 && (material.shader.GetPropertyFlags(i) & ShaderPropertyFlags.HDR) != 0;
        }

        public static bool IsInt(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 && System.Array.IndexOf(material.shader.GetPropertyAttributes(i), "IntRange") >= 0;
        }

        /// <summary>スライダーの範囲（Range でなければ 0〜1）。</summary>
        public static Vector2 Range(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 && material.shader.GetPropertyType(i) == ShaderPropertyType.Range
                ? material.shader.GetPropertyRangeLimits(i)
                : new Vector2(0f, 1f);
        }

        public static bool GetToggle(Material material, ToonItem item)
        {
            return item.Kind == ToonKind.TwoSided
                ? Mathf.RoundToInt(material.GetFloat(item.Id)) == (int)CullMode.Off
                : material.GetFloat(item.Id) > 0.5f;
        }

        public static void SetToggle(Material material, ToonItem item, bool on)
        {
            if (item.Kind == ToonKind.TwoSided)
            {
                material.SetFloat(item.Id, on ? (float)CullMode.Off : (float)CullMode.Back);
                return;
            }

            material.SetFloat(item.Id, on ? 1f : 0f);
            if (!string.IsNullOrEmpty(item.Keyword))
            {
                ObjectToonMaterials.SetKeyword(material, item.Keyword, on);
            }
        }

        /// <summary>初期値（baseline があればその値）から変わっているか。物ごとの値は印を付けないので常に false。</summary>
        public static bool IsChanged(Material target, Material baseline, ToonItem item)
        {
            if (item.PerMaterial || !Has(target, item))
            {
                return false;
            }

            if (baseline != null && !Has(baseline, item))
            {
                baseline = null;
            }

            if (IsColor(target, item))
            {
                var a = target.GetColor(item.Id);
                var b = baseline != null ? baseline.GetColor(item.Id) : DefaultColor(target, item);
                return Mathf.Abs(a.r - b.r) > ColorEpsilon || Mathf.Abs(a.g - b.g) > ColorEpsilon ||
                       Mathf.Abs(a.b - b.b) > ColorEpsilon || Mathf.Abs(a.a - b.a) > ColorEpsilon;
            }

            var value = target.GetFloat(item.Id);
            var reference = baseline != null ? baseline.GetFloat(item.Id) : DefaultFloat(target, item);
            return Mathf.Abs(value - reference) > FloatEpsilon;
        }

        /// <summary>初期値（baseline があればその値）に戻す。</summary>
        public static void Reset(Material target, Material baseline, ToonItem item)
        {
            if (!Has(target, item))
            {
                return;
            }

            if (baseline != null && !Has(baseline, item))
            {
                baseline = null;
            }

            if (IsColor(target, item))
            {
                target.SetColor(item.Id, baseline != null ? baseline.GetColor(item.Id) : DefaultColor(target, item));
                return;
            }

            var value = baseline != null ? baseline.GetFloat(item.Id) : DefaultFloat(target, item);
            if (item.Kind == ToonKind.Toggle)
            {
                SetToggle(target, item, value > 0.5f);
            }
            else
            {
                target.SetFloat(item.Id, value);
            }
        }

        private static int Index(Material material, ToonItem item)
        {
            return material != null && material.shader != null ? material.shader.FindPropertyIndex(item.Id) : -1;
        }

        private static float DefaultFloat(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 ? material.shader.GetPropertyDefaultFloatValue(i) : 0f;
        }

        private static Color DefaultColor(Material material, ToonItem item)
        {
            var i = Index(material, item);
            return i >= 0 ? (Color)material.shader.GetPropertyDefaultVectorValue(i) : Color.black;
        }
    }
}
