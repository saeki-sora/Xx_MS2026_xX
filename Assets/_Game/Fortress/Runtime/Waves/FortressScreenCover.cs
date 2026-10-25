using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面を覆う幕（歯磨き粉の幕・UIスタジオの切り替えの幕）が今出ているか（覆う途中・覆ったまま・流れ落ちる途中のどれでも）。
    /// 幕を持つ物が起動時に「出ているか」を知らせる関数を足し、試合の始まりの演出（ステージの物が落ちてくる）などが
    /// 「幕が全部消えて、盤面が全部見えてから」始めるのに使う。
    /// <see cref="FortressWaves.AddReadyBlocker"/> との違い: あちらは「覆っている間だけ」（流れ落ち始めたら準備OK）。
    /// </summary>
    public static class FortressScreenCover
    {
        private static readonly List<Func<bool>> Checks = new List<Func<bool>>();

        /// <summary>「幕が出ているか」を知らせる関数を足す。起動時に1回だけ足す（消さない）。</summary>
        public static void Add(Func<bool> isShowing)
        {
            if (isShowing != null && !Checks.Contains(isShowing))
            {
                Checks.Add(isShowing);
            }
        }

        /// <summary>どれかの幕が出ているか。</summary>
        public static bool IsShowing
        {
            get
            {
                foreach (var check in Checks)
                {
                    try
                    {
                        if (check())
                        {
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        // 壊れた関数があっても、演出が始まらなくならないように無視する。
                        Debug.LogException(e);
                    }
                }

                return false;
            }
        }
    }
}
