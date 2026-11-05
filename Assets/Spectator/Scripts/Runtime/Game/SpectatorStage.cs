using System.Collections.Generic;
using System.Linq;
using System.Text;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Spectator
{
    /// <summary>
    /// いまのステージの「観客が選べる出口」と「観客が出すふつうの菌」を調べ、菌を出す係（メインスレッドだけで使う）。
    /// 出口 = ステージの湧き位置（EnemySpawnPoint）のうち、ウェーブで実際に使っている所（無ければ全部）。並びは名前順で決まる。
    /// ふつうの菌 = このステージのウェーブでいちばん多く出てくる群衆の種類（通信の番号が付いている種類なので、Client にも同じ菌が届く）。
    /// </summary>
    internal sealed class SpectatorStage
    {
        private readonly List<EnemySpawnPoint> _exits = new List<EnemySpawnPoint>();
        private readonly List<Vector2> _mapPoints = new List<Vector2>();
        private EnemySpawnDirector _director;
        private EnemyTypeDefinition _germ;
        private EnemyTypeDefinition _boss;

        /// <summary>このステージの見分け（シーン名・出口の数・読み込みごとに変わる番号）。</summary>
        public string Key { get; private set; } = "";

        public bool IsReady => _director != null && _germ != null && _exits.Count > 0;

        public int ExitCount => _exits.Count;

        public string GermName => _germ != null ? _germ.name : "";

        /// <summary>ボスに使う種類（このステージのウェーブでいちばん HP が多い群衆の種類）。ふつうの菌と同じなら、菌をたくさん出してボスの代わりにする。</summary>
        public bool HasDistinctBoss => _boss != null && _boss != _germ;

        /// <summary>湧き位置の係を探し直す（見つかっていて、まだあれば何もしない）。</summary>
        public void Refresh()
        {
            if (_director != null && _exits.TrueForAll(e => e != null))
            {
                return;
            }

            _director = Object.FindFirstObjectByType<EnemySpawnDirector>();
            Rebuild();
        }

        private void Rebuild()
        {
            _exits.Clear();
            _mapPoints.Clear();
            _germ = null;
            _boss = null;
            Key = "";
            if (_director == null)
            {
                return;
            }

            _director.RefreshSpawnPoints();
            var all = (_director.spawnPoints ?? new EnemySpawnPoint[0])
                .Where(p => p != null)
                .GroupBy(p => p.ResolvedId)
                .Select(g => g.First())
                .ToList();

            var entries = _director.AllEntries().ToList();
            var used = new HashSet<string>(entries.Select(e => e.spawnPointId).Where(id => !string.IsNullOrEmpty(id)));
            var chosen = all.Where(p => used.Contains(p.ResolvedId)).ToList();
            if (chosen.Count == 0)
            {
                chosen = all;
            }

            chosen.Sort((a, b) => string.CompareOrdinal(a.ResolvedId, b.ResolvedId));
            _exits.AddRange(chosen);

            _germ = entries
                .Where(e => e.enemyType != null && e.enemyType.simulationMode == EnemySimulationMode.Swarm)
                .GroupBy(e => e.enemyType)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            _boss = entries
                .Where(e => e.enemyType != null && e.enemyType.simulationMode == EnemySimulationMode.Swarm)
                .Select(e => e.enemyType)
                .Distinct()
                .OrderByDescending(t => t.maxHealth)
                .FirstOrDefault();

            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            Vector2 center;
            if (core != null)
            {
                center = core.transform.position;
            }
            else
            {
                center = Vector2.zero;
                foreach (var exit in _exits)
                {
                    center += (Vector2)exit.transform.position;
                }

                if (_exits.Count > 0)
                {
                    center /= _exits.Count;
                }
            }

            var xs = _exits.Select(e => e.transform.position.x).ToArray();
            var ys = _exits.Select(e => e.transform.position.y).ToArray();
            var outX = new float[xs.Length];
            var outY = new float[ys.Length];
            SpectatorMapProjection.Project(xs, ys, center.x, center.y, outX, outY);
            for (var i = 0; i < outX.Length; i++)
            {
                _mapPoints.Add(new Vector2(outX[i], outY[i]));
            }

            Key = _director.gameObject.scene.name + ":" + _exits.Count + ":" + _director.GetInstanceID();
        }

        /// <summary>[[x,y],…]（-1〜1、上が +）。</summary>
        public void AppendExitsJson(StringBuilder sb)
        {
            sb.Append('[');
            for (var i = 0; i < _mapPoints.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('[').Num(_mapPoints[i].x).Append(',').Num(_mapPoints[i].y).Append(']');
            }

            sb.Append(']');
        }

        /// <summary>出口の名前から並びの番号（無ければ -1）。Host への取り次ぎは名前で届く。</summary>
        public int IndexOfExit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (var i = 0; i < _exits.Count; i++)
            {
                if (_exits[i] != null && _exits[i].ResolvedId == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public string ExitId(int index) => index >= 0 && index < _exits.Count && _exits[index] != null ? _exits[index].ResolvedId : "";

        /// <summary>その出口から count 体を出す（Host・オフラインだけ）。印 tag を付ける（0 なら付けない）。出せた数を返す。</summary>
        public int Spawn(int exitIndex, int count, int tag)
        {
            if (!IsReady || exitIndex < 0 || exitIndex >= _exits.Count || _exits[exitIndex] == null)
            {
                return 0;
            }

            var swarm = SwarmSystem.Current;
            if (swarm == null)
            {
                return 0;
            }

            return swarm.SpawnBurst(_germ, _exits[exitIndex].transform.position, 0.5f, count, tag);
        }

        /// <summary>ボスを出す（別の種類が無ければ、ふつうの菌を fallbackCount 体）。出せた数を返す。</summary>
        public int SpawnBoss(int exitIndex, int fallbackCount, int tag)
        {
            if (!IsReady || exitIndex < 0 || exitIndex >= _exits.Count || _exits[exitIndex] == null)
            {
                return 0;
            }

            var swarm = SwarmSystem.Current;
            if (swarm == null)
            {
                return 0;
            }

            var at = _exits[exitIndex].transform.position;
            return HasDistinctBoss ? (swarm.Spawn(_boss, at, tag) ? 1 : 0) : swarm.SpawnBurst(_germ, at, 0.8f, fallbackCount, tag);
        }
    }
}
