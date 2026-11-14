using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// プレイヤー（0〜3）の「操縦者」の差し替え口（2026-11-15、CPU プレイヤー用）。
    /// ここに入っているプレイヤーは、人のセンサー・キーボード・Client から届いた握力の代わりに、この操縦者の握力で遊ぶ。
    /// ・読むのは砲台（<see cref="LaserTurret.ReadGrip"/>）・歯みがきタイム（<see cref="BrushingRunner"/>）など、握力を使う所。
    /// ・入れるのは結果を決める PC（Host・オフライン）の係だけ（CPU なら MS2026.Cpu の CpuDirector）。Client の砲台は Host の状態を映すだけなので、ここは読まない。
    /// ・番号はプレイヤーの番号（席替えの「席」ではない）。
    /// </summary>
    public static class PlayerPilots
    {
        public const int PlayerCount = 4;

        private static readonly ITurretGripSource[] Pilots = new ITurretGripSource[PlayerCount];

        /// <summary>操縦者が入っているプレイヤー（bit p = P(p+1)）。</summary>
        public static int Mask { get; private set; }

        /// <summary>操縦者が入った・外れたとき。</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Array.Clear(Pilots, 0, Pilots.Length);
            Mask = 0;
            Changed = null;
        }

        /// <summary>プレイヤー player の操縦者を入れる（null で外す）。</summary>
        public static void Set(int player, ITurretGripSource pilot)
        {
            if (player < 0 || player >= PlayerCount || ReferenceEquals(Pilots[player], pilot))
            {
                return;
            }

            Pilots[player] = pilot;
            Mask = pilot != null ? Mask | (1 << player) : Mask & ~(1 << player);
            Changed?.Invoke();
        }

        /// <summary>プレイヤー player の操縦者を外す。</summary>
        public static void Clear(int player) => Set(player, null);

        /// <summary>全員の操縦者を外す。</summary>
        public static void ClearAll()
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                Set(p, null);
            }
        }

        public static bool IsPiloted(int player) => player >= 0 && player < PlayerCount && Pilots[player] != null;

        public static bool TryGet(int player, out ITurretGripSource pilot)
        {
            pilot = player >= 0 && player < PlayerCount ? Pilots[player] : null;
            return pilot != null;
        }

        /// <summary>操縦者がいれば、その握力を読んで true。いなければ false（呼んだ側が今まで通りに読む）。</summary>
        public static bool TryReadGrip(int player, out bool isGripping, out float grip01)
        {
            if (TryGet(player, out var pilot))
            {
                pilot.GetGrip(player, out isGripping, out grip01);
                grip01 = Mathf.Clamp01(grip01);
                return true;
            }

            isGripping = false;
            grip01 = 0f;
            return false;
        }
    }
}
