using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「にぎにぎ」（小刻みに握る）を1回ずつ数える（Unity に頼らない計算。テストあり）。
    /// 握りの値が「押した」の線を越えたら1回。もう一度数えるには「離した」の線まで下がる必要がある（値のゆれで数えすぎない）。
    /// </summary>
    public sealed class GripPulseCounter
    {
        public float pressThreshold = 0.55f;
        public float releaseThreshold = 0.35f;

        /// <summary>これより短い間隔の「1回」は数えない（センサーの跳ね返り対策）。</summary>
        public float minIntervalSeconds = 0.06f;

        private bool _down;
        private float _lastPulse = float.NegativeInfinity;

        public int Count { get; private set; }

        public void Reset()
        {
            _down = false;
            _lastPulse = float.NegativeInfinity;
            Count = 0;
        }

        /// <summary>今の握りの値（0〜1）と時刻を入れる。新しく1回数えたら true。</summary>
        public bool Feed(float grip, float time)
        {
            if (_down)
            {
                if (grip <= releaseThreshold)
                {
                    _down = false;
                }

                return false;
            }

            if (grip < pressThreshold)
            {
                return false;
            }

            _down = true;
            if (time - _lastPulse < minIntervalSeconds)
            {
                return false;
            }

            _lastPulse = time;
            Count++;
            return true;
        }
    }

    /// <summary>歯みがきタイムの計算（Unity に頼らない計算。テストあり）。</summary>
    public static class BrushingMath
    {
        /// <summary>きれい度（0〜1）。</summary>
        public static float Clean01(int pulses, int required) => required <= 0 ? 1f : Mathf.Clamp01((float)pulses / required);

        /// <summary>
        /// ごほうび: きれい度 × healFraction × 最大HP をまず回復に使い、満タンからあふれた分をコーティング（最大 coatingFraction × 最大HP）にする。
        /// </summary>
        public static void Reward(float clean01, float maxHealth, float currentHealth, float healFraction, float coatingFraction,
            out float heal, out float coating)
        {
            var total = Mathf.Clamp01(clean01) * Mathf.Max(0f, healFraction) * Mathf.Max(0f, maxHealth);
            heal = Mathf.Clamp(total, 0f, Mathf.Max(0f, maxHealth - currentHealth));
            coating = Mathf.Min(total - heal, Mathf.Max(0f, coatingFraction) * Mathf.Max(0f, maxHealth));
        }

        /// <summary>
        /// 「シンクロ」: mask のプレイヤー（2人以上）が全員、now から window 秒以内に1回ずつ握ったか。
        /// lastPulseTime[p] は p が最後に1回数えられた時刻（まだなら −∞）。
        /// </summary>
        public static bool IsSync(float[] lastPulseTime, int mask, float now, float window)
        {
            var members = 0;
            for (var p = 0; p < lastPulseTime.Length; p++)
            {
                if ((mask & (1 << p)) == 0)
                {
                    continue;
                }

                members++;
                if (now - lastPulseTime[p] > window)
                {
                    return false;
                }
            }

            return members >= 2;
        }
    }
}
