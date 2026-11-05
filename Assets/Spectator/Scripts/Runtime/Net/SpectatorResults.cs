using System;
using System.Collections.Generic;

namespace MS2026.Spectator
{
    /// <summary>試合のあとにスマホへ出す、観客 1 人の戦績。</summary>
    public struct SpectatorResultCard
    {
        /// <summary>試合の見分け（Host が試合ごとに決める）。スマホが「この結果はもう閉じた」を覚えるのに使う。</summary>
        public int MatchId;
        public int Number;
        public int Sends;
        public int Germs;
        public int Killed;
        public int Bites;

        /// <summary>かじりついた回数の順位（1 から。同じ数なら同じ順位）。</summary>
        public int Rank;

        /// <summary>この試合で送った観客の人数。</summary>
        public int Of;

        /// <summary>いちばんかじりついた観客の番号と回数（0 回なら番号も 0）。</summary>
        public int TopNumber;
        public int TopBites;

        /// <summary>試合が終わったとき、まだ生きていた数（倒されず、かじりつきもしなかった）。</summary>
        public int Survived => Math.Max(0, Germs - Killed - Bites);
    }

    /// <summary>Host の帳簿から戦績のカードを作る（Unity に頼らない）。</summary>
    public static class SpectatorResults
    {
        /// <summary>
        /// owner（<see cref="SpectatorGroupBook.OwnerKey"/>）ごとの成績から、送ったことのある人全員のカードを作る。
        /// 順位はかじりついた回数（多い順）、同じなら同じ順位。
        /// </summary>
        public static List<KeyValuePair<int, SpectatorResultCard>> Build(IEnumerable<KeyValuePair<int, SpectatorScore>> scores, int matchId)
        {
            var list = new List<KeyValuePair<int, SpectatorScore>>();
            foreach (var pair in scores)
            {
                if (pair.Value.Sends > 0)
                {
                    list.Add(pair);
                }
            }

            list.Sort((a, b) =>
            {
                var byBites = b.Value.Bites.CompareTo(a.Value.Bites);
                return byBites != 0 ? byBites : b.Value.Killed.CompareTo(a.Value.Killed);
            });

            var topNumber = 0;
            var topBites = 0;
            if (list.Count > 0 && list[0].Value.Bites > 0)
            {
                topNumber = NumberOf(list[0].Key);
                topBites = list[0].Value.Bites;
            }

            var cards = new List<KeyValuePair<int, SpectatorResultCard>>(list.Count);
            var rank = 0;
            var previousBites = -1;
            for (var i = 0; i < list.Count; i++)
            {
                var score = list[i].Value;
                if (score.Bites != previousBites)
                {
                    rank = i + 1;
                    previousBites = score.Bites;
                }

                cards.Add(new KeyValuePair<int, SpectatorResultCard>(list[i].Key, new SpectatorResultCard
                {
                    MatchId = matchId,
                    Number = NumberOf(list[i].Key),
                    Sends = score.Sends,
                    Germs = score.Germs,
                    Killed = score.Killed,
                    Bites = score.Bites,
                    Rank = rank,
                    Of = list.Count,
                    TopNumber = topNumber,
                    TopBites = topBites
                }));
            }

            return cards;
        }

        /// <summary>owner から受付の PC（0 = Host 自身、1〜 = Client の通信番号 + 1）。</summary>
        public static int SourceOf(int owner) => owner / 65536;

        public static int NumberOf(int owner) => owner & 0xFFFF;
    }

    /// <summary>
    /// 受付の PC が持つ「最後の試合の戦績」（観客の番号 → カード）。メインスレッドが入れ、Web サーバーのスレッドが読む。
    /// 次の試合の戦績が届くまで残す（試合が終わってすぐ次が始まっても、スマホで見られるように）。
    /// </summary>
    public sealed class SpectatorResultShelf
    {
        private readonly object _lock = new object();
        private Dictionary<int, SpectatorResultCard> _cards = new Dictionary<int, SpectatorResultCard>();

        public void Replace(IEnumerable<SpectatorResultCard> cards)
        {
            var next = new Dictionary<int, SpectatorResultCard>();
            foreach (var card in cards)
            {
                next[card.Number] = card;
            }

            lock (_lock)
            {
                _cards = next;
            }
        }

        public bool TryGet(int number, out SpectatorResultCard card)
        {
            lock (_lock)
            {
                return _cards.TryGetValue(number, out card);
            }
        }
    }
}
