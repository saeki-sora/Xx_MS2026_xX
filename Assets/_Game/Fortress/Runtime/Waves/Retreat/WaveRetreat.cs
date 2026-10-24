using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの目標を倒した後の「撤退」（2026-10-24、ユーザー: 「その時点でフィールドにいる敵は画面外に速攻で帰っていく。全てのプレイヤーの視界からいなくなるまで」）。
    /// ・全員のPC（<see cref="SetRetreatLook"/>）: ビームが当たらない・コアに何もしない（<see cref="SwarmSystem.SetRetreating"/>）
    /// ・Host（<see cref="Begin"/>〜<see cref="Finish"/>）: だれの画面にも映っていない場所（と湧き位置）へ向かう道を計算して速く逃がし、
    ///   4人全員の画面から出た敵・湧き位置にたどり着いた敵を消す（撃破数に入らない）。まだ映っている数が <see cref="VisibleCount"/>
    /// 画面は各プレイヤーの視点（ステージの視点プリセット P1〜P4）で判定する（<see cref="WaveViewCoverage"/>）。
    /// Actor の敵（今のプロジェクトには無い）は、撤退が始まった時点で消す。
    /// </summary>
    public static class WaveRetreat
    {
        /// <summary>画面の外にこれだけ出たら「映っていない」（体の大きい敵がはみ出して見えないように）。</summary>
        public const float VisibleMarginMeters = 1.5f;

        /// <summary>湧き位置のこの距離まで来たら、そこへ帰ったことにして消す（湧き位置が画面の中にあるステージ用）。</summary>
        public const float SpawnVanishRadius = 1.2f;

        /// <summary>撤退を始めてから、湧き位置で消え始めるまでの秒数（湧いた直後の敵がすぐ消えないように）。</summary>
        public const float SpawnVanishDelay = 0.6f;

        /// <summary>出口を探すマス目の間引き（2 なら 2 マスに1つ）。</summary>
        private const int ExitStride = 2;

        private static readonly WaveViewCoverage Coverage = new WaveViewCoverage();
        private static readonly List<Vector2> SpawnPositions = new List<Vector2>();
        private static readonly List<Vector2> Exits = new List<Vector2>();
        private static readonly List<int> ToRemove = new List<int>();
        private static SwarmSystem _swarm;
        private static float _startTime;

        /// <summary>撤退中か（Host）。</summary>
        public static bool IsRetreating { get; private set; }

        /// <summary>撤退中、まだだれかの画面に映っている敵の数（Host）。</summary>
        public static int VisibleCount { get; private set; }

        /// <summary>Host: 残りの敵を逃がし始める。</summary>
        public static void Begin(float speedMultiplier)
        {
            Unsubscribe();
            IsRetreating = true;
            _startTime = Time.time;
            _swarm = SwarmSystem.Current;
            VisibleCount = _swarm != null ? Mathf.Max(1, _swarm.Storage.Count) : 0;

            var rig = FortressCameraRig.Active;
            Coverage.Set(rig != null ? rig.preset : null, rig != null ? rig.guides.ReferenceAspect : 16f / 9f);
            SpawnPositions.Clear();
            foreach (var point in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                SpawnPositions.Add(point.transform.position);
            }

            RemoveActors();
            if (_swarm == null)
            {
                VisibleCount = 0;
                return;
            }

            _swarm.SetRetreatField(SolveExitField(), speedMultiplier);
            _swarm.StorageReadable += Scan;
        }

        /// <summary>Host: 撤退を終える。まだ残っている敵は消す（撃破数には入らない）。</summary>
        public static void Finish()
        {
            Unsubscribe();
            IsRetreating = false;
            VisibleCount = 0;
            if (FortressNet.HasSimulationAuthority)
            {
                if (_swarm != null)
                {
                    _swarm.SetRetreatField(null, 1f);
                }

                SwarmSystem.Current?.ClearAll();
                RemoveActors();
            }

            _swarm = null;
        }

        /// <summary>試合の外へ出たとき・Client になったとき。</summary>
        public static void Reset()
        {
            Unsubscribe();
            IsRetreating = false;
            VisibleCount = 0;
            _swarm = null;
            var current = SwarmSystem.Current;
            if (current != null)
            {
                current.SetRetreating(false);
            }
        }

        /// <summary>全員のPC: 撤退中の扱い（ビームが当たらない・コアに何もしない）にするか。</summary>
        public static void SetRetreatLook(bool on)
        {
            var current = SwarmSystem.Current;
            if (current != null && current.Retreating != on)
            {
                current.SetRetreating(on);
            }
        }

        // 逃げる道: だれの画面にも映っていないマス（と湧き位置）を出口にした経路。経路のマップが無ければ null（コアの反対へまっすぐ）。
        private static FlowFieldResult SolveExitField()
        {
            if (!(EnemyNavigation.Current is NavigationField field))
            {
                return null;
            }

            field.EnsureBuilt();
            var data = field.Data;
            Exits.Clear();
            if (Coverage.ViewCount > 0)
            {
                for (var y = 0; y < data.Height; y += ExitStride)
                {
                    for (var x = 0; x < data.Width; x += ExitStride)
                    {
                        var center = data.CellCenter(x, y);
                        if (!data.Blocked[data.Index(x, y)] && !Coverage.IsVisible(center, VisibleMarginMeters))
                        {
                            Exits.Add(center);
                        }
                    }
                }
            }

            Exits.AddRange(SpawnPositions);
            return Exits.Count > 0 ? field.SolveTo(Exits) : null;
        }

        // 毎フレーム（群衆の配列を読める瞬間）: 画面の外へ出た敵・湧き位置に着いた敵を消し、まだ映っている数を数える。
        private static void Scan()
        {
            var swarm = _swarm;
            if (swarm == null || !IsRetreating)
            {
                return;
            }

            var storage = swarm.Storage;
            var nearSpawnAllowed = Time.time - _startTime >= SpawnVanishDelay;
            var radiusSq = SpawnVanishRadius * SpawnVanishRadius;
            var visible = 0;
            ToRemove.Clear();
            for (var i = 0; i < storage.Count; i++)
            {
                var p = (Vector2)storage.pos[i];
                var gone = !Coverage.IsVisible(p, VisibleMarginMeters);
                if (!gone && nearSpawnAllowed)
                {
                    foreach (var spawn in SpawnPositions)
                    {
                        if ((spawn - p).sqrMagnitude <= radiusSq)
                        {
                            gone = true;
                            break;
                        }
                    }
                }

                if (gone)
                {
                    ToRemove.Add(storage.netId[i]);
                }
                else
                {
                    visible++;
                }
            }

            swarm.RequestRemove(ToRemove);
            VisibleCount = visible;
        }

        private static void RemoveActors()
        {
            if (!FortressNet.HasSimulationAuthority)
            {
                return;
            }

            for (var i = EnemyController.Active.Count - 1; i >= 0; i--)
            {
                var enemy = EnemyController.Active[i];
                if (enemy != null && !enemy.IsReplica)
                {
                    Object.Destroy(enemy.gameObject);
                }
            }
        }

        private static void Unsubscribe()
        {
            if (_swarm != null)
            {
                _swarm.StorageReadable -= Scan;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsRetreating = false;
            VisibleCount = 0;
            _swarm = null;
        }
    }
}
