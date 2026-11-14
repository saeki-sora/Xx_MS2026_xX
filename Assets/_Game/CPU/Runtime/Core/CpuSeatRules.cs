namespace MS2026.Cpu
{
    /// <summary>どの席を CPU にするかを決めるための材料。</summary>
    public struct CpuSeatInputs
    {
        /// <summary>設定で CPU を使うか。</summary>
        public bool Enabled;

        /// <summary>このPCが結果を決めるか（Host・オフライン）。Client では CPU を動かさない。</summary>
        public bool HasAuthority;

        /// <summary>通信中か。</summary>
        public bool Networked;

        /// <summary>このPCの人のプレイヤー番号（0〜3）。</summary>
        public int LocalPlayer;

        /// <summary>ほかのPCから入っている人の席（bit p = P(p+1)。自分は含めない）。</summary>
        public int RemoteHumanMask;

        /// <summary>砲台がある席。</summary>
        public int TurretMask;

        /// <summary>通信していないとき、自分以外の席を CPU にするか。</summary>
        public bool FillOtherSeatsOffline;

        /// <summary>起動オプションの席。</summary>
        public CpuSeatMode Mode;

        /// <summary><see cref="CpuSeatMode.List"/> のときの席。</summary>
        public int ListMask;
    }

    /// <summary>
    /// どの席を CPU にするか（決まりだけ。テストできるように Unity に頼らない）。
    /// ・CPU は結果を決める PC（Host・オフライン）だけで動く。
    /// ・ほかのPCから人が入っている席は、いつでも人が優先（人が来たら CPU は止まる）。
    /// ・起動オプションが無ければ: 通信していないときは「自分以外の席」（設定で切れる）、通信中は無し（キャラ選択の空いた席は段9）。
    /// ・-cpu 2,3: その席だけ（自分の席でも、書いてあれば CPU）。-cpu all: 全部の席（自分の席も。自動プレイ用）。-cpu none: 無し。
    /// </summary>
    public static class CpuSeatRules
    {
        public const int PlayerCount = 4;
        public const int AllMask = (1 << PlayerCount) - 1;

        public static int ComputeCpuMask(in CpuSeatInputs inputs)
        {
            if (!inputs.Enabled || !inputs.HasAuthority)
            {
                return 0;
            }

            int wanted;
            switch (inputs.Mode)
            {
                case CpuSeatMode.None:
                    wanted = 0;
                    break;
                case CpuSeatMode.All:
                    wanted = AllMask;
                    break;
                case CpuSeatMode.List:
                    wanted = inputs.ListMask & AllMask;
                    break;
                default:
                    wanted = !inputs.Networked && inputs.FillOtherSeatsOffline ? AllMask & ~LocalBit(inputs.LocalPlayer) : 0;
                    break;
            }

            var remoteHumans = inputs.Networked ? inputs.RemoteHumanMask & ~LocalBit(inputs.LocalPlayer) : 0;
            return wanted & ~remoteHumans & inputs.TurretMask & AllMask;
        }

        private static int LocalBit(int player) => player >= 0 && player < PlayerCount ? 1 << player : 0;

        /// <summary>「P2, P3」のような書き方（記録用）。</summary>
        public static string Describe(int mask)
        {
            if (mask == 0)
            {
                return "なし";
            }

            var text = string.Empty;
            for (var p = 0; p < PlayerCount; p++)
            {
                if ((mask & (1 << p)) == 0)
                {
                    continue;
                }

                text += text.Length > 0 ? ", " : string.Empty;
                text += "P" + (p + 1);
            }

            return text;
        }

        /// <summary>CPU ごとの乱数の種（seed が 0 ならばらばら）。同じ seed・同じ席なら毎回同じ。</summary>
        public static int SeedFor(int seed, int player, int matchCount, int randomBase)
        {
            unchecked
            {
                var baseSeed = seed != 0 ? seed : randomBase;
                return (baseSeed * 486187739) ^ (player * 92821 + 17) ^ (matchCount * 1103515245);
            }
        }
    }
}
