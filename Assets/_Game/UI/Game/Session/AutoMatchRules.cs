using System.Collections.Generic;

namespace MS2026.UI.Game
{
    /// <summary>展示用の自動接続で、見つかった部屋をどうするかの答え。</summary>
    public enum AutoMatchChoice
    {
        /// <summary>入れる部屋も、もうすぐ入れる部屋も無い（さがす時間が過ぎたら自分がホストになる）。</summary>
        None,

        /// <summary>部屋はあるが今は入れない（読み込み中・満員）。ホストにならずに待つ。</summary>
        Wait,

        /// <summary>この部屋に入る。</summary>
        Join
    }

    /// <summary>
    /// 展示用の自動接続（<see cref="GameSession"/> の autoConnect）の決まり。Unity を使わないのでテストできる。
    /// ・入る部屋: 同じビルドで、席が空いていて、読み込み中でない部屋。人が多い部屋 → 部屋の番号が小さい部屋の順に選ぶ
    /// ・席: 空いている一番小さい番号（来た順に P1, P2, …）
    /// ・ほぼ同時に2台がホストになったら、人が少ない部屋（同じなら番号が大きい部屋）のホストが、もう片方の部屋に入り直す
    /// </summary>
    public static class AutoMatchRules
    {
        /// <summary>見つかった部屋の中から入る部屋を選ぶ。ignoreRoomId（自分が前に作った部屋など）は無視する。</summary>
        public static AutoMatchChoice Choose(IReadOnlyList<LanDiscovery.FoundHost> hosts, int ignoreRoomId, out int index)
        {
            index = -1;
            var wait = false;
            if (hosts == null)
            {
                return AutoMatchChoice.None;
            }

            for (var i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                if (!host.sameBuild || (ignoreRoomId != 0 && host.reply.roomId == ignoreRoomId))
                {
                    continue;
                }

                if (!IsJoinable(host))
                {
                    wait = true;
                    continue;
                }

                if (index < 0 || IsBetter(host, hosts[index]))
                {
                    index = i;
                }
            }

            return index >= 0 ? AutoMatchChoice.Join : wait ? AutoMatchChoice.Wait : AutoMatchChoice.None;
        }

        /// <summary>今すぐ入れる部屋か（席が空いていて、試合の読み込み中・ロビーへ戻る途中でない）。</summary>
        public static bool IsJoinable(LanDiscovery.FoundHost host)
        {
            return FirstFreeSeat(host.reply.takenMask) >= 0 && host.reply.phase != LobbyPhase.Loading && host.reply.phase != LobbyPhase.Returning;
        }

        /// <summary>空いている一番小さい席（無ければ -1）。</summary>
        public static int FirstFreeSeat(int takenMask)
        {
            for (var seat = 0; seat < LobbyRoster.SeatCount; seat++)
            {
                if ((takenMask & (1 << seat)) == 0)
                {
                    return seat;
                }
            }

            return -1;
        }

        /// <summary>
        /// ホストの自分が、見つかったほかの部屋に譲って入り直すべきか（試合が始まる前だけ呼ぶ）。
        /// 人が多い部屋が残る。同じ人数なら部屋の番号が小さい方が残る（両方のPCで同じ答えになるので、譲るのは必ず片方だけ）。
        /// </summary>
        public static bool ShouldYield(int ownPlayers, int ownRoomId, LanDiscovery.FoundHost other)
        {
            if (!other.sameBuild || other.reply.roomId == 0 || other.reply.roomId == ownRoomId || !IsJoinable(other))
            {
                return false;
            }

            return other.reply.players > ownPlayers || (other.reply.players == ownPlayers && other.reply.roomId < ownRoomId);
        }

        private static bool IsBetter(LanDiscovery.FoundHost a, LanDiscovery.FoundHost b)
        {
            if (a.reply.players != b.reply.players)
            {
                return a.reply.players > b.reply.players;
            }

            return a.reply.roomId < b.reply.roomId;
        }
    }
}
