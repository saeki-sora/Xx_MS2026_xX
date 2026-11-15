using System;
using System.Collections.Generic;
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

        /// <summary>結果が入った・消えたとき（途中から入った人に、ホストから結果が届いたときも。スキルの配り直しなどに使う）。</summary>
        public static event Action Changed;

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
            Changed?.Invoke();
        }

        public static void Clear()
        {
            for (var i = 0; i < Chosen.Length; i++)
            {
                Chosen[i] = null;
            }

            HasResult = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// 空いている席（null）に、まだ誰も選んでいないキャラを pool の順に割り当てた並びを返す（CPU が使う。2026-11-15）。
        /// 割り当てるキャラが足りなければ null のまま。全員の PC が同じ盤面から同じ結果になる（順番が決まっているので）。
        /// </summary>
        public static CharacterDefinition[] FillAbsent(CharacterDefinition[] chosen, IReadOnlyList<CharacterDefinition> pool)
        {
            var result = new CharacterDefinition[chosen != null ? chosen.Length : 0];
            if (chosen == null)
            {
                return result;
            }

            var used = new HashSet<CharacterDefinition>();
            for (var i = 0; i < chosen.Length; i++)
            {
                result[i] = chosen[i];
                if (chosen[i] != null)
                {
                    used.Add(chosen[i]);
                }
            }

            var next = 0;
            for (var i = 0; i < result.Length; i++)
            {
                if (result[i] != null || pool == null)
                {
                    continue;
                }

                while (next < pool.Count && (pool[next] == null || used.Contains(pool[next])))
                {
                    next++;
                }

                if (next >= pool.Count)
                {
                    break;
                }

                result[i] = pool[next];
                used.Add(pool[next]);
                next++;
            }

            return result;
        }

        // 「ドメインの再読み込みなしで再生」の設定でも、再生のたびに前回の結果が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Changed = null;
            Clear();
        }
    }
}
