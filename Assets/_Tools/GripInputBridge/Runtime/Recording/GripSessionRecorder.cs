using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace MS2026.GripInputBridge.Recording
{
    /// <summary>
    /// 握力入力のセッションログをJSON Lines形式で記録する。設計書 10.1 参照。
    /// 記録したログは <see cref="ReplayGripTransport"/> で再生でき、本番中の不具合調査や
    /// バランス調整データの収集に使う。
    /// </summary>
    public sealed class GripSessionRecorder : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _isDisposed;

        /// <summary>実際に書き出しているファイルの絶対パス。</summary>
        public string FilePath { get; }

        /// <param name="directoryPath">
        /// 保存先ディレクトリ。省略時はプロジェクト直下(Assets外)の "GripLogs" フォルダを使う
        /// （設計書10.1: Unityがインポート対象にしないようAssets外に置く）。
        /// </param>
        public GripSessionRecorder(string directoryPath = null)
        {
            var directory = string.IsNullOrEmpty(directoryPath) ? DefaultLogDirectory() : directoryPath;
            Directory.CreateDirectory(directory);

            var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_session.jsonl";
            FilePath = Path.Combine(directory, fileName);

            // 1サンプルごとにディスクへ同期書き込みすると本番の連続稼働で処理落ちの原因になるため、
            // AutoFlushはオフにして内部バッファに溜め、Flush/Dispose時にまとめて書き出す。
            _writer = new StreamWriter(FilePath, append: false) { AutoFlush = false };
        }

        /// <summary>1プレイヤー・1サンプル分をログに追記する。</summary>
        public void RecordSample(int playerIndex, float raw, float filtered, float normalized)
        {
            if (_isDisposed)
            {
                return;
            }

            var entry = new GripLogEntry
            {
                t = _clock.ElapsedMilliseconds,
                p = playerIndex,
                raw = raw,
                filtered = filtered,
                normalized = normalized
            };

            _writer.WriteLine(JsonUtility.ToJson(entry));
        }

        /// <summary>バッファをディスクへ書き出す。長時間の連続稼働中は定期的に呼ぶとよい。</summary>
        public void Flush()
        {
            if (!_isDisposed)
            {
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _writer.Flush();
            _writer.Dispose();
        }

        private static string DefaultLogDirectory()
        {
            return Path.Combine(Application.dataPath, "..", "GripLogs");
        }
    }
}
