using System;
using System.Collections.Generic;

namespace MS2026.Spectator
{
    /// <summary>観客の菌が消えた理由。</summary>
    public enum SpectatorRemoval
    {
        Other,

        /// <summary>プレイヤーに倒された。</summary>
        Killed,

        /// <summary>コアにかじりついた（コアに着いて消えた）。</summary>
        ReachedCore
    }

    /// <summary>画面に出す1つの群れ（全員の PC に配る中身）。</summary>
    public struct SpectatorGroupState
    {
        public int Number;
        public int ColorIndex;
        public bool Boss;
        public int Alive;
        public float X;
        public float Y;

        /// <summary>群れの広がり（ワールドの長さ）。</summary>
        public float Radius;
    }

    /// <summary>観客1人の、この試合の成績（M4 でスマホに出す）。</summary>
    public struct SpectatorScore
    {
        public int Sends;
        public int Germs;
        public int Killed;
        public int Bites;
    }

    /// <summary>
    /// Host だけが持つ「観客の群れの帳簿」。どの通信番号の菌がどの群れか、群れの中心・広がり・残りの数、観客ごとの成績。
    /// 群衆の係から届く「番号が決まった」「消えた」と、1 秒に数回の「いまの位置」で更新する。メインスレッドだけで使う。Unity に頼らない。
    /// </summary>
    public sealed class SpectatorGroupBook
    {
        private sealed class Group
        {
            public int Tag;
            public int Owner;
            public int Number;
            public int ColorIndex;
            public bool Boss;
            public int Assigned;
            public int Alive;
            public double CreatedAt;

            public double SumX;
            public double SumY;
            public double SumSq;
            public int Seen;

            public bool HasPosition;
            public float X;
            public float Y;
            public float Radius;
        }

        /// <summary>番号が決まらないまま、この秒数たった群れは捨てる（全消去などで出られなかった）。</summary>
        public const double UnassignedTimeout = 3.0;

        private readonly Dictionary<int, Group> _byTag = new Dictionary<int, Group>();
        private readonly Dictionary<int, Group> _byNetId = new Dictionary<int, Group>();
        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<int, SpectatorScore> _scores = new Dictionary<int, SpectatorScore>();
        private int _nextTag = 1;

        public int GroupCount => _groups.Count;

        /// <summary>いま追いかけている菌の数（位置を読む必要があるか）。</summary>
        public int TrackedAgents => _byNetId.Count;

        /// <summary>新しい群れの印（0 以外、重ならない）。</summary>
        public int NewTag()
        {
            var tag = _nextTag++;
            if (_nextTag >= int.MaxValue - 1)
            {
                _nextTag = 1;
            }

            return tag;
        }

        /// <summary>
        /// 群れを覚える。owner は成績をまとめる見分け（受付の PC が違えば同じ番号でも別の人。省くと番号そのもの）。
        /// </summary>
        public void AddGroup(int tag, int number, int colorIndex, bool boss, double now, int owner = -1)
        {
            if (tag == 0 || _byTag.ContainsKey(tag))
            {
                return;
            }

            var group = new Group { Tag = tag, Owner = owner >= 0 ? owner : number, Number = number, ColorIndex = colorIndex, Boss = boss, CreatedAt = now };
            _byTag.Add(tag, group);
            _groups.Add(group);
        }

        /// <summary>出した数を成績に足す（ボスは数えない）。owner は <see cref="AddGroup"/> と同じ見分け。</summary>
        public void RecordSend(int owner, int germs)
        {
            var score = Score(owner);
            score.Sends++;
            score.Germs += germs;
            _scores[owner] = score;
        }

        /// <summary>印つきの菌が配列に入り、通信の番号が決まった。</summary>
        public void OnAssigned(int tag, int netId)
        {
            if (!_byTag.TryGetValue(tag, out var group))
            {
                return;
            }

            // 番号は使い回されるので、前の持ち主が残っていたら外す（消えた知らせを取りこぼした場合）。
            if (_byNetId.TryGetValue(netId, out var old) && old != group)
            {
                old.Alive = Math.Max(0, old.Alive - 1);
            }

            _byNetId[netId] = group;
            group.Assigned++;
            group.Alive++;
        }

        /// <summary>菌が消えた（観客の菌でなければ何もしない）。</summary>
        public void OnRemoved(int netId, SpectatorRemoval reason)
        {
            if (!_byNetId.TryGetValue(netId, out var group))
            {
                return;
            }

            _byNetId.Remove(netId);
            group.Alive = Math.Max(0, group.Alive - 1);
            if (group.Boss || reason == SpectatorRemoval.Other)
            {
                return;
            }

            var score = Score(group.Owner);
            if (reason == SpectatorRemoval.Killed)
            {
                score.Killed++;
            }
            else
            {
                score.Bites++;
            }

            _scores[group.Owner] = score;
        }

        public void BeginScan()
        {
            foreach (var group in _groups)
            {
                group.SumX = 0;
                group.SumY = 0;
                group.SumSq = 0;
                group.Seen = 0;
            }
        }

        /// <summary>配列の1体分（観客の菌でなければ何もしない）。</summary>
        public void Accumulate(int netId, float x, float y)
        {
            if (!_byNetId.TryGetValue(netId, out var group))
            {
                return;
            }

            group.SumX += x;
            group.SumY += y;
            group.SumSq += (double)x * x + (double)y * y;
            group.Seen++;
        }

        /// <summary>
        /// 中心と広がりを決め、いなくなった群れを捨てる。広がり = 中心からの距離の 2 乗平均の平方根 × 1.5
        /// （ほぼ全員が入る大きさ。1 回で数えられる）。
        /// </summary>
        public void EndScan(double now)
        {
            foreach (var group in _groups)
            {
                if (group.Seen <= 0)
                {
                    continue;
                }

                var cx = group.SumX / group.Seen;
                var cy = group.SumY / group.Seen;
                var variance = Math.Max(0, group.SumSq / group.Seen - cx * cx - cy * cy);
                group.X = (float)cx;
                group.Y = (float)cy;
                group.Radius = (float)(Math.Sqrt(variance) * 1.5);
                group.HasPosition = true;
            }

            Prune(now);
        }

        public void Prune(double now)
        {
            for (var i = _groups.Count - 1; i >= 0; i--)
            {
                var group = _groups[i];
                var gone = group.Assigned > 0 ? group.Alive <= 0 : now - group.CreatedAt > UnassignedTimeout;
                if (gone)
                {
                    _groups.RemoveAt(i);
                    _byTag.Remove(group.Tag);
                }
            }
        }

        /// <summary>画面に出す群れ（ボスが先、あとは新しい順）を into に入れる。最大 max 個。</summary>
        public void Snapshot(List<SpectatorGroupState> into, int max)
        {
            into.Clear();
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = _groups.Count - 1; i >= 0 && into.Count < max; i--)
                {
                    var group = _groups[i];
                    if (group.Boss != (pass == 0) || !group.HasPosition || group.Alive <= 0)
                    {
                        continue;
                    }

                    into.Add(new SpectatorGroupState
                    {
                        Number = group.Number,
                        ColorIndex = group.ColorIndex,
                        Boss = group.Boss,
                        Alive = group.Alive,
                        X = group.X,
                        Y = group.Y,
                        Radius = group.Radius
                    });
                }
            }
        }

        public SpectatorScore Score(int owner) => _scores.TryGetValue(owner, out var score) ? score : default;

        /// <summary>この試合の成績（owner → 成績）。</summary>
        public IEnumerable<KeyValuePair<int, SpectatorScore>> Scores => _scores;

        /// <summary>受付の PC（0 = Host 自身、1〜 = Client の番号 + 1）と観客の番号から、成績の見分けを作る。</summary>
        public static int OwnerKey(int source, int number) => source * 65536 + (number & 0xFFFF);

        /// <summary>新しい試合・ステージ。群れも成績も忘れる。</summary>
        public void Clear()
        {
            _byTag.Clear();
            _byNetId.Clear();
            _groups.Clear();
            _scores.Clear();
        }
    }

    /// <summary>
    /// 観客が出せる量の上限と、ボスのたまり（Host だけ・メインスレッド）。Unity に頼らない。
    /// 上限 = はじめの持ち分 + ウェーブで出た菌の数 × 割合 − 観客が出した数（試合ごと）。
    /// </summary>
    public sealed class SpectatorBudget
    {
        /// <summary>ウェーブの菌に対する観客の菌の割合（0.15 = 15% 上乗せまで）。</summary>
        public float Ratio = 0.15f;

        /// <summary>試合の始めから出せる分（ウェーブがまだ少ないうちも送れるように）。</summary>
        public int StartAllowance = 60;

        /// <summary>ボスが来るまでの「送った回数」の合計。</summary>
        public int PoolMax = 15;

        /// <summary>1 試合で出るボスの数の上限。</summary>
        public int MaxBosses = 2;

        public int WaveSpawned { get; private set; }

        public int SpectatorSpawned { get; private set; }

        public int Pool { get; private set; }

        public int BossesSpawned { get; private set; }

        public int BossesLeft => Math.Max(0, MaxBosses - BossesSpawned);

        public int Allowance => Math.Max(0, StartAllowance + (int)(Ratio * WaveSpawned) - SpectatorSpawned);

        /// <summary>あと count 体出してよいか。</summary>
        public bool CanSpawn(int count) => Allowance >= count;

        public void Reset()
        {
            WaveSpawned = 0;
            SpectatorSpawned = 0;
            Pool = 0;
            BossesSpawned = 0;
        }

        public void SetWaveSpawned(int count) => WaveSpawned = Math.Max(0, count);

        public void AddSpectatorSpawned(int count) => SpectatorSpawned += Math.Max(0, count);

        /// <summary>送った1回分をためる。ボスを出す番になったら true（たまりは 0 に戻る）。ボスを出し切ったらたまらない。</summary>
        public bool AddToPool()
        {
            if (BossesLeft <= 0)
            {
                return false;
            }

            Pool++;
            if (Pool < PoolMax)
            {
                return false;
            }

            Pool = 0;
            BossesSpawned++;
            return true;
        }

        /// <summary>ボスを出せなかったとき、たまりを満タンに戻す（次に送られたときにもう一度試す）。</summary>
        public void RefundBoss()
        {
            BossesSpawned = Math.Max(0, BossesSpawned - 1);
            Pool = Math.Max(0, PoolMax - 1);
        }
    }
}
