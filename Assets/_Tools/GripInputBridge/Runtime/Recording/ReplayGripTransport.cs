using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MS2026.GripInputBridge.Recording
{
    /// <summary>
    /// <see cref="GripSessionRecorder"/> で記録したJSON Linesログを、記録時と同じタイミングで
    /// 再生するトランスポート。設計書 10.2 参照。バグ再現・回帰テストに使う。
    ///
    /// フィルタ・キャリブレーションを一切通していない生値(raw)を返す点に注意。
    /// これにより、記録後にフィルタやキャリブレーションの設定を変えて
    /// 「同じ生データに対して結果がどう変わるか」を確認できる。
    /// </summary>
    public sealed class ReplayGripTransport : IGripTransport
    {
        private readonly List<GripLogEntry>[] _samplesByPlayer = new List<GripLogEntry>[GripInputBridgeConstants.PlayerCount];
        private readonly int[] _nextSampleIndex = new int[GripInputBridgeConstants.PlayerCount];
        private readonly float[] _currentValues = new float[GripInputBridgeConstants.PlayerCount];
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        public event Action<int> OnConnected;
        public event Action<int> OnDisconnected;

        public ReplayGripTransport(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            for (var i = 0; i < _samplesByPlayer.Length; i++)
            {
                _samplesByPlayer[i] = new List<GripLogEntry>();
            }

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; // 空行はスキップ(ファイル末尾等)。
                }

                var entry = JsonUtility.FromJson<GripLogEntry>(line);
                if (entry.p >= 0 && entry.p < _samplesByPlayer.Length)
                {
                    _samplesByPlayer[entry.p].Add(entry);
                }
            }
        }

        public bool IsConnected(int playerIndex)
        {
            return IsValidIndex(playerIndex) && _samplesByPlayer[playerIndex].Count > 0;
        }

        public float GetRawValue(int playerIndex)
        {
            if (!IsValidIndex(playerIndex))
            {
                return 0f;
            }

            AdvancePlayback(playerIndex);
            return _currentValues[playerIndex];
        }

        /// <summary>再生位置を先頭に戻す。</summary>
        public void Restart()
        {
            _clock.Restart();
            Array.Clear(_nextSampleIndex, 0, _nextSampleIndex.Length);
            Array.Clear(_currentValues, 0, _currentValues.Length);
        }

        private void AdvancePlayback(int playerIndex)
        {
            var samples = _samplesByPlayer[playerIndex];
            var elapsedMs = _clock.ElapsedMilliseconds;
            var nextIndex = _nextSampleIndex[playerIndex];

            while (nextIndex < samples.Count && samples[nextIndex].t <= elapsedMs)
            {
                _currentValues[playerIndex] = samples[nextIndex].raw;
                nextIndex++;
            }

            _nextSampleIndex[playerIndex] = nextIndex;
        }

        private static bool IsValidIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < GripInputBridgeConstants.PlayerCount;
        }
    }
}
