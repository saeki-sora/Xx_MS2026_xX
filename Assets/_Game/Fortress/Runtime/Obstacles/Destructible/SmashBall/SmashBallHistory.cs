using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「直近誰がスマッシュボールを割ったか」の履歴。デバッグ表示やHUDから参照できるよう、直近N件だけ保持する。
    /// Play開始のたびにリセットされる。
    /// </summary>
    public static class SmashBallHistory
    {
        private const int MaxRecords = 30;

        private static readonly List<SmashBallBreakInfo> RecordsList = new List<SmashBallBreakInfo>();

        /// <summary>新しい順。</summary>
        public static IReadOnlyList<SmashBallBreakInfo> Records => RecordsList;

        /// <summary>記録が1件増えるたびに発火する。将来、能力発動などのフックとしても使える。</summary>
        public static event Action<SmashBallBreakInfo> Recorded;

        public static void Add(SmashBallBreakInfo info)
        {
            RecordsList.Insert(0, info);
            if (RecordsList.Count > MaxRecords)
            {
                RecordsList.RemoveAt(RecordsList.Count - 1);
            }

            Recorded?.Invoke(info);
        }

        public static void Clear()
        {
            RecordsList.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            RecordsList.Clear();
            Recorded = null;
        }
    }
}
