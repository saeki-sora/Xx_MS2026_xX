using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 4人それぞれのスキルのゲージ（敵を倒して光の粒を砲台に集めると溜まる。2026-10-31）。画面（左下のゲージ）が読む掲示板。
    /// ・溜める・満タンでスキルを使える状態にするのは、砲台のスキル欄（MS2026.Skills.PlayerSkillSlot）。ここには結果だけを書く
    /// ・溜まる量は <see cref="EnemyKillCounter.ByPlayer"/>（Host が数えて全員に配る数）から決めるので、どのPCでも同じになる
    ///   （光の粒そのものは見た目だけで、出す数に上限もあるので数えない）
    /// </summary>
    public static class PlayerSkillGauge
    {
        public const int PlayerCount = 4;

        private static readonly float[] FillValues = new float[PlayerCount];
        private static readonly bool[] ReadyValues = new bool[PlayerCount];
        private static readonly bool[] HasValues = new bool[PlayerCount];

        /// <summary>このプレイヤー(0〜3)のゲージ（0=空、1=満タン）。</summary>
        public static float Fill01(int player)
        {
            return Valid(player) ? FillValues[player] : 0f;
        }

        /// <summary>このプレイヤーのスキルが使える状態か（満タン、またはスマッシュボールで溜まった）。</summary>
        public static bool IsReady(int player)
        {
            return Valid(player) && ReadyValues[player];
        }

        /// <summary>このプレイヤーの砲台にスキルがあるか。</summary>
        public static bool HasSkill(int player)
        {
            return Valid(player) && HasValues[player];
        }

        public static void Set(int player, float fill01, bool ready)
        {
            if (!Valid(player))
            {
                return;
            }

            FillValues[player] = Mathf.Clamp01(fill01);
            ReadyValues[player] = ready;
            HasValues[player] = true;
        }

        public static void Clear(int player)
        {
            if (!Valid(player))
            {
                return;
            }

            FillValues[player] = 0f;
            ReadyValues[player] = false;
            HasValues[player] = false;
        }

        private static bool Valid(int player) => player >= 0 && player < PlayerCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                Clear(p);
            }
        }
    }
}
