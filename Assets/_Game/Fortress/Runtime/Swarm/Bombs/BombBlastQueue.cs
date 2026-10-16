using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>爆弾の爆発がまわりに与えるダメージ1件分（爆発した瞬間の設定の値を写して持つ）。</summary>
    public struct BombBlast
    {
        /// <summary>ダメージを届ける時刻（Time.time）。爆発した時刻＋連鎖の間隔。</summary>
        public float dueTime;
        public Vector2 center;
        public float radius;
        public float enemyDamage;
        public float obstacleDamage;
        /// <summary>コアへのダメージ。コアで自爆したとき（到達のダメージが別に入る）は 0。</summary>
        public float coreDamage;
        /// <summary>爆弾を倒した人（0〜3）。不明なら -1。巻き込んで倒した敵・壊した物もこの人の手柄になる。</summary>
        public int owner;
    }

    /// <summary>
    /// 届ける時刻を待っている爆発ダメージの列（Host・オフラインだけで使う）。
    /// 「爆発→少し待って→まわりにダメージ→巻き込まれた爆弾が爆発→…」の待ち時間を受け持つ。状態だけを持つ純粋なクラス。
    /// </summary>
    public sealed class BombBlastQueue
    {
        private readonly List<BombBlast> _waiting = new List<BombBlast>();

        public int Count => _waiting.Count;

        public void Add(BombBlast blast)
        {
            _waiting.Add(blast);
        }

        public void Clear()
        {
            _waiting.Clear();
        }

        /// <summary>
        /// 時刻 now までに届けるべきものを、入れた順に最大 max 件 output へ移す（output は先に空にする）。
        /// 上限を超えた分は列に残り、次の呼び出しで取り出される。取り出した数を返す。
        /// </summary>
        public int TakeDue(float now, int max, List<BombBlast> output)
        {
            output.Clear();
            var write = 0;
            for (var i = 0; i < _waiting.Count; i++)
            {
                var blast = _waiting[i];
                if (blast.dueTime <= now && output.Count < max)
                {
                    output.Add(blast);
                    continue;
                }

                _waiting[write++] = blast;
            }

            _waiting.RemoveRange(write, _waiting.Count - write);
            return output.Count;
        }
    }
}
