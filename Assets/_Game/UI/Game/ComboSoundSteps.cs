namespace MS2026.UI.Game
{
    /// <summary>
    /// コンボの音を鳴らす区切り（サウンドスタジオ N10、2026-11-22）: 10・50・100、その後は 100 ごと。
    /// 1フレームに何体も倒して区切りを飛び越えても、1回だけ鳴らす。Unity を使わないのでテストできる。
    /// </summary>
    public static class ComboSoundSteps
    {
        private static readonly int[] FirstSteps = { 10, 50, 100 };
        private const int StepAfterLast = 100;

        /// <summary>コンボが <paramref name="before"/> から <paramref name="after"/> に増えたとき、区切りを1つ以上越えたか。</summary>
        public static bool Reached(int before, int after)
        {
            return after > before && StepIndex(after) > StepIndex(before);
        }

        // その数までに越えた区切りの数。
        private static int StepIndex(int combo)
        {
            var index = 0;
            foreach (var step in FirstSteps)
            {
                if (combo >= step)
                {
                    index++;
                }
            }

            var last = FirstSteps[FirstSteps.Length - 1];
            if (combo > last)
            {
                index += (combo - last) / StepAfterLast;
            }

            return index;
        }
    }
}
