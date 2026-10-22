using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 歯磨き粉の幕を連番の絵で出すときの、何コマ目を出すかの計算（<see cref="PasteCurtain"/> が使う）。
    /// 連番は「垂れてくる（0 → 覆いきるコマ）」「覆ったまま（→ 抜け始めるコマの1つ前で止まる）」「抜ける（→ 最後）」の3つに分かれる。
    /// </summary>
    public readonly struct PasteCurtainFrameTiming
    {
        public readonly int Count;
        public readonly float FrameRate;
        public readonly int CoveredFrame;
        public readonly int RevealFrame;

        public PasteCurtainFrameTiming(int count, float frameRate, int coveredFrame, int revealFrame)
        {
            Count = Mathf.Max(0, count);
            FrameRate = Mathf.Max(1f, frameRate);
            var last = Mathf.Max(0, Count - 1);
            CoveredFrame = Mathf.Clamp(coveredFrame, 0, last);
            // 抜け始めは覆いきった後。最後のコマより後ろなら、覆ったら最後のコマで終わる。
            RevealFrame = Mathf.Clamp(revealFrame, CoveredFrame, Mathf.Max(CoveredFrame, Count));
        }

        /// <summary>2枚以上あれば連番で出せる。</summary>
        public bool IsUsable => Count >= 2;

        /// <summary>垂れてきて覆いきるまでの秒数。</summary>
        public float CoverSeconds => CoveredFrame / FrameRate;

        /// <summary>覆ったまま待つ間のコマを1回出し終えるまでの秒数（その後は止めて待つ）。</summary>
        public float HoldSeconds => Mathf.Max(0, RevealFrame - 1 - CoveredFrame) / FrameRate;

        /// <summary>抜け始めてから最後のコマを出し終えるまでの秒数。</summary>
        public float RevealSeconds => Mathf.Max(0, Count - RevealFrame) / FrameRate;

        /// <summary>垂れてくる途中のコマ（seconds = 垂れ始めてから）。</summary>
        public int CoverFrameAt(float seconds) => Mathf.Clamp(Mathf.FloorToInt(seconds * FrameRate), 0, CoveredFrame);

        /// <summary>覆ったまま待つ間のコマ（seconds = 覆いきってから）。抜け始めるコマの1つ前で止まる。</summary>
        public int HoldFrameAt(float seconds)
        {
            var end = Mathf.Max(CoveredFrame, RevealFrame - 1);
            return Mathf.Clamp(CoveredFrame + Mathf.FloorToInt(seconds * FrameRate), CoveredFrame, end);
        }

        /// <summary>抜けていく途中のコマ（seconds = 抜け始めてから）。最後まで出したら -1（幕を消す）。</summary>
        public int RevealFrameAt(float seconds)
        {
            var frame = RevealFrame + Mathf.FloorToInt(seconds * FrameRate);
            return frame >= Count ? -1 : frame;
        }
    }
}
