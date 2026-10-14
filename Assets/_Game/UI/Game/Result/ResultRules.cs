using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// リザルト画面の決まり（Unity の画面を使わないのでテストできる）。
    /// ・ランク: コアの耐久（残りHPの割合）で決める。S 90%以上 / A 70%以上 / B 40%以上 / C それ未満
    /// ・出てくる順番（段階）: 数字が1つずつ数え上がり、最後にランクのハンコ（ユーザー決定 2026-10-13「数字が一気に数え上がる」）
    /// </summary>
    public static class ResultRules
    {
        public const int StepNone = 0;
        public const int StepBanner = 1;
        public const int StepPanel = 2;
        public const int StepDurability = 3;
        public const int StepBrightness = 4;
        public const int StepPlayers = 5;
        public const int StepCombos = 6;
        public const int StepRank = 7;
        public const int StepNext = 8;

        // クリア: 各段階が始まる秒数（StepBanner から StepNext まで）。
        private static readonly float[] ClearTimes = { 0f, 2.6f, 3.2f, 4.2f, 5.2f, 6.7f, 7.7f, 8.7f };

        // ゲームオーバー（仮）: 「GAME OVER」→ パネル → すぐ「タイトルへ」。
        private const float GameOverPanelTime = 2.6f;
        private const float GameOverNextTime = 3.4f;

        /// <summary>結果が出てから elapsed 秒たったときの段階。</summary>
        public static int StepAt(bool clear, float elapsed)
        {
            if (elapsed < 0f)
            {
                return StepNone;
            }

            if (!clear)
            {
                return elapsed >= GameOverNextTime ? StepNext : elapsed >= GameOverPanelTime ? StepPanel : StepBanner;
            }

            var step = StepNone;
            for (var i = 0; i < ClearTimes.Length; i++)
            {
                if (elapsed >= ClearTimes[i])
                {
                    step = StepBanner + i;
                }
            }

            return step;
        }

        /// <summary>パネルが出る秒数（自動でタイトルへ戻るまでの時間は、ここから数える）。</summary>
        public static float PanelTime(bool clear) => clear ? ClearTimes[StepPanel - StepBanner] : GameOverPanelTime;

        public static string Rank(float durability01)
        {
            return durability01 >= 0.9f ? "S" : durability01 >= 0.7f ? "A" : durability01 >= 0.4f ? "B" : "C";
        }

        /// <summary>ランクの色（意味のある色: S=金、A=水色、B=藤色、C=赤みのピンク）。</summary>
        public static Color RankColor(string rank)
        {
            switch (rank)
            {
                case "S": return new Color(1f, 0.84f, 0.35f);
                case "A": return new Color(0.43f, 0.93f, 1f);
                case "B": return new Color(0.72f, 0.64f, 1f);
                default: return new Color(1f, 0.5f, 0.6f);
            }
        }

        /// <summary>ランクのひとこと（このゲームの言葉で短く）。</summary>
        public static string RankComment(string rank)
        {
            switch (rank)
            {
                case "S": return "コアはピカピカ！ 完ぺきな掃除";
                case "A": return "きれいに守りきった！";
                case "B": return "なんとか守りきった";
                default: return "ギリギリ… 次はもっとピカピカに";
            }
        }
    }
}
