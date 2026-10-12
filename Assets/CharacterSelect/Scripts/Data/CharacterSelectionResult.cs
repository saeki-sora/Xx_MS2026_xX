using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// キャラクター選択の結果（P1〜P4 がどのキャラを選んだか）。選択画面が終わるときに入り、ゲームのシーンから読める。
    /// 例: <c>var chara = CharacterSelectionResult.Get(playerIndex);</c>（選んでいなければ null）。
    /// ネット対戦では各PCが、ホストから届いた結果を自分で入れる。
    /// </summary>
    public static class CharacterSelectionResult
    {
        private static readonly CharacterDefinition[] Chosen = new CharacterDefinition[CharacterSelectBoard.SeatCount];

        /// <summary>選択画面を通ってきたか（このシーンだけで遊んでいるときは false）。</summary>
        public static bool HasResult { get; private set; }

        /// <summary>その席（P1〜P4 = 0〜3）が選んだキャラ（選んでいない・参加していない・結果が無いときは null）。</summary>
        public static CharacterDefinition Get(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < Chosen.Length ? Chosen[playerIndex] : null;
        }

        public static void Store(CharacterDefinition[] chosen)
        {
            for (var i = 0; i < Chosen.Length; i++)
            {
                Chosen[i] = chosen != null && i < chosen.Length ? chosen[i] : null;
            }

            HasResult = true;
        }

        public static void Clear()
        {
            for (var i = 0; i < Chosen.Length; i++)
            {
                Chosen[i] = null;
            }

            HasResult = false;
        }

        // 「ドメインの再読み込みなしで再生」の設定でも、再生のたびに前回の結果が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
