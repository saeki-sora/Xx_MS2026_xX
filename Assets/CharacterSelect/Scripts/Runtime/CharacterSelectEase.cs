using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>キャラ選択の画面の動きで使う、動きの曲線（0〜1 → 0〜1）。</summary>
    internal static class CharacterSelectEase
    {
        /// <summary>少し行き過ぎて戻る。</summary>
        public static float BackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>速く始まって、ゆっくり止まる。</summary>
        public static float CubicOut(float t)
        {
            var u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        /// <summary>ゆっくり始まって、速く終わる。</summary>
        public static float CubicIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }
    }
}
