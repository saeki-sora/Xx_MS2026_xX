using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// ルーレットの動きの計算（Unity の画面に頼らないのでテストできる）。
    /// 位置は「カード何枚ぶん進んだか」で表す（0 = 一覧の 1 枚目が真ん中、1 = 2 枚目が真ん中…、一覧の数で 1 周）。
    /// </summary>
    public static class StageRouletteMath
    {
        /// <summary>止まる位置（カード何枚ぶん）。少なくとも minLaps 周してから、選ばれたカード（列の中の番号）で止まる。</summary>
        public static float FinalPosition(int cardCount, int chosenSlot, int minLaps)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            chosenSlot = Mathf.Clamp(chosenSlot, 0, cardCount - 1);
            return Mathf.Max(1, minLaps) * cardCount + chosenSlot;
        }

        /// <summary>
        /// 回り始めてから t 秒後の位置。最初は速く、だんだんゆっくり（5 乗の減速）。終わり近くで overshoot 枚ぶん行き過ぎて戻る。
        /// t が spinSeconds 以上なら、ぴったり finalPosition。
        /// </summary>
        public static float PositionAt(float t, float spinSeconds, float finalPosition, float overshoot)
        {
            if (spinSeconds <= 0f || t >= spinSeconds)
            {
                return finalPosition;
            }

            var x = Mathf.Clamp01(t / spinSeconds);
            var inv = 1f - x;
            var eased = 1f - inv * inv * inv * inv * inv;
            const float BumpStart = 0.8f;
            var bump = x > BumpStart ? Mathf.Sin((x - BumpStart) / (1f - BumpStart) * Mathf.PI) * overshoot : 0f;
            return finalPosition * eased + bump;
        }

        /// <summary>位置 p のとき、真ん中にいるカードの列の番号（0〜cardCount-1）。</summary>
        public static int CenterSlot(float position, int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0;
            }

            var slot = Mathf.FloorToInt(position + 0.5f) % cardCount;
            return slot < 0 ? slot + cardCount : slot;
        }

        /// <summary>真ん中から offset 枚目に見えるカードの列の番号（位置 p のとき）。</summary>
        public static int SlotAt(float position, int offset, int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0;
            }

            var slot = (Mathf.FloorToInt(position) + offset) % cardCount;
            return slot < 0 ? slot + cardCount : slot;
        }
    }
}
