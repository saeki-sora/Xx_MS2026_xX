using System;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// 群衆の敵の位置の「写し」（CPU 全員で1つ）。群衆の配列を読める瞬間（<see cref="SwarmSystem.StorageReadable"/>）に、
    /// 決めた間隔ごとに位置だけを写し取る。CPU の目はこの写しを読む（群衆の配列やジョブには触らない）。
    /// 結果を決める PC（Host・オフライン）で使う。Client の群衆は Host の写真を映しているだけなので、CPU は使わない。
    /// </summary>
    public sealed class CpuSwarmSampler : IDisposable
    {
        private SwarmSystem _swarm;
        private Vector2[] _positions = new Vector2[256];
        private float _nextSampleTime;

        /// <summary>写し取る間隔（秒、実時間）。</summary>
        public float Interval { get; set; } = 0.1f;

        /// <summary>写した敵の数。</summary>
        public int Count { get; private set; }

        /// <summary>写した位置（0〜<see cref="Count"/>-1 だけ有効）。</summary>
        public Vector2[] Positions => _positions;

        /// <summary>最後に写した時刻（ゲームの時間）。</summary>
        public float SampleTime { get; private set; } = float.NegativeInfinity;

        /// <summary>写すたびに増える番号（新しい写しが来たか見分ける用）。</summary>
        public int Version { get; private set; }

        /// <summary>今の群衆を見張る（群衆が作り直されたら付け替える）。毎フレーム呼んでよい。</summary>
        public void Track()
        {
            var current = SwarmSystem.Current;
            if (ReferenceEquals(current, _swarm))
            {
                return;
            }

            Detach();
            _swarm = current;
            Count = 0;
            if (_swarm != null)
            {
                _swarm.StorageReadable += OnReadable;
            }
        }

        public void Dispose()
        {
            Detach();
            Count = 0;
        }

        private void Detach()
        {
            if (_swarm != null)
            {
                _swarm.StorageReadable -= OnReadable;
            }

            _swarm = null;
        }

        private void OnReadable()
        {
            var now = Time.unscaledTime;
            if (now < _nextSampleTime || _swarm == null || _swarm.Storage == null)
            {
                return;
            }

            _nextSampleTime = now + Mathf.Max(0.02f, Interval);

            var storage = _swarm.Storage;
            var count = Mathf.Max(0, storage.Count);
            if (_positions.Length < count)
            {
                Array.Resize(ref _positions, Mathf.NextPowerOfTwo(count));
            }

            var pos = storage.pos;
            for (var i = 0; i < count; i++)
            {
                var p = pos[i];
                _positions[i] = new Vector2(p.x, p.y);
            }

            Count = count;
            SampleTime = Time.time;
            Version++;
        }
    }
}
