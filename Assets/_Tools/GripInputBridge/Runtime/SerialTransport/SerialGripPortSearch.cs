// SerialGripTransport と同じく、実機通信を有効にしたとき（MS2026_GRIP_SERIAL_ENABLED）だけコンパイルする。
#if MS2026_GRIP_SERIAL_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MS2026.GripInputBridge.Data;
using Object = UnityEngine.Object;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 握力デバイスがつながっているCOMポートをさがす（PCごとにポート番号が違っても動くように）。
    /// ・候補のポートを全部同時に開く（開くのに時間がかかるポートがあっても画面が止まらないよう、別のスレッドで開く）
    /// ・正しい形の値が最初に届いたポートを選ぶ（同時なら候補の順番が前の方。設定したポートが先頭）
    /// ・timeoutMs たっても値が届かなければ、先頭のポート（設定したポート）が開けていればそれを選ぶ（後から値を送り始める機器のため）。それも無ければ見つからない
    /// 選ばなかったポートはすべて閉じる。メインスレッドから毎フレーム <see cref="Poll"/> を呼ぶ。
    /// </summary>
    public sealed class SerialGripPortSearch : IDisposable
    {
        private sealed class Candidate
        {
            public string port;
            public GripDeviceConfig config;
            public Task<SerialGripTransport> opening;
            public bool present;
            public string devicePath;
        }

        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly int _timeoutMs;
        private bool _disposed;

        // さがし終わったときの各ポートの様子（終わった後は、閉じたポートを読まずにこれを返す）。
        private List<GripPortReport> _finalReports;

        /// <param name="config">ポート名以外の設定（速さ・センサー特性など）。ポートごとに写しを作って使う。メインスレッドで呼ぶこと。</param>
        /// <param name="ports">試すポート（先頭ほど優先。<see cref="GripPortCandidates.Build"/>）。</param>
        /// <param name="timeoutMs">値が届くのを待つ時間（Arduino Uno はポートを開くと再起動するので 2000 以上）。</param>
        /// <param name="logFirstPortError">先頭のポートを開けなかったときにエラーを出すか。</param>
        /// <param name="availablePorts">このPCに今あるポート（表示で「このPCに無い」を見分けるため。null なら全部あるとみなす）。</param>
        public SerialGripPortSearch(GripDeviceConfig config, IReadOnlyList<string> ports, int timeoutMs, bool logFirstPortError,
            IReadOnlyCollection<string> availablePorts = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _timeoutMs = Math.Max(0, timeoutMs);
            var devicePaths = SerialPortDevicePaths.Read();
            for (var i = 0; i < ports.Count; i++)
            {
                var portConfig = Object.Instantiate(config);
                portConfig.serialPortName = ports[i];
                var logError = i == 0 && logFirstPortError;
                _candidates.Add(new Candidate
                {
                    port = ports[i],
                    config = portConfig,
                    opening = Task.Run(() => new SerialGripTransport(portConfig, logError)),
                    present = availablePorts == null || ContainsPort(availablePorts, ports[i]),
                    devicePath = devicePaths.TryGetValue(ports[i], out var path) ? path : null
                });
            }

            IsDone = _candidates.Count == 0;
        }

        /// <summary>さがし終わったか（見つかった・見つからなかった）。</summary>
        public bool IsDone { get; private set; }

        /// <summary>選んだポートのトランスポート（見つからなければ null）。受け取った側が Dispose する。</summary>
        public SerialGripTransport Result { get; private set; }

        /// <summary>選んだポートから値が届いていたか（false なら、時間切れで設定したポートを選んだ）。</summary>
        public bool ResultHasData { get; private set; }

        /// <summary>試したポートの名前（知らせる文用）。</summary>
        public string DescribePorts()
        {
            var names = new List<string>();
            foreach (var candidate in _candidates)
            {
                names.Add(candidate.port);
            }

            return names.Count > 0 ? string.Join(", ", names) : "（COMポートがありません）";
        }

        /// <summary>試しているポートごとの今の様子（さがし終わった後は、終わったときの様子）。F8 の表示・ログ用。</summary>
        public List<GripPortReport> GetReports()
        {
            if (_finalReports != null)
            {
                return _finalReports;
            }

            var reports = new List<GripPortReport>(_candidates.Count);
            foreach (var candidate in _candidates)
            {
                reports.Add(Report(candidate));
            }

            return reports;
        }

        private static GripPortReport Report(Candidate candidate)
        {
            if (!candidate.present)
            {
                return new GripPortReport(candidate.port, GripPortState.NotPresent, candidate.devicePath);
            }

            var task = candidate.opening;
            if (task.Status != TaskStatus.RanToCompletion)
            {
                return task.IsCompleted
                    ? new GripPortReport(candidate.port, GripPortState.OpenFailed, candidate.devicePath, task.Exception?.GetBaseException().Message)
                    : new GripPortReport(candidate.port, GripPortState.Opening, candidate.devicePath);
            }

            var transport = task.Result;
            if (!transport.IsPortOpen)
            {
                return new GripPortReport(candidate.port, GripPortState.OpenFailed, candidate.devicePath, transport.OpenError,
                    transport.OpenErrorWasAccessDenied);
            }

            if (transport.HasReceivedData)
            {
                return new GripPortReport(candidate.port, GripPortState.Readable, candidate.devicePath);
            }

            var diagnostics = transport.GetDiagnostics();
            return diagnostics.LinesReceived > 0
                ? new GripPortReport(candidate.port, GripPortState.Unreadable, candidate.devicePath, linesReceived: diagnostics.LinesReceived,
                    lastRejectedLine: diagnostics.LastRejectedLine ?? diagnostics.LastLine)
                : new GripPortReport(candidate.port, GripPortState.Silent, candidate.devicePath);
        }

        private static bool ContainsPort(IReadOnlyCollection<string> ports, string port)
        {
            foreach (var candidate in ports)
            {
                if (string.Equals(candidate?.Trim(), port, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>メインスレッドから毎フレーム呼ぶ。</summary>
        public void Poll()
        {
            if (IsDone || _disposed)
            {
                return;
            }

            foreach (var candidate in _candidates)
            {
                var transport = OpenedTransport(candidate);
                if (transport != null && transport.HasReceivedData)
                {
                    Finish(candidate, true);
                    return;
                }
            }

            if (_clock.ElapsedMilliseconds < _timeoutMs)
            {
                return;
            }

            // 時間切れ: 設定したポート（先頭）が開けていれば、値はまだでもそれを使う（今までと同じ動き）。
            var first = _candidates[0];
            Finish(OpenedTransport(first) != null ? first : null, false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!IsDone)
            {
                Finish(null, false);
            }
        }

        private static SerialGripTransport OpenedTransport(Candidate candidate)
        {
            if (candidate.opening.Status != TaskStatus.RanToCompletion)
            {
                return null;
            }

            var transport = candidate.opening.Result;
            return transport.IsPortOpen ? transport : null;
        }

        private void Finish(Candidate chosen, bool hasData)
        {
            // 閉じる前に、各ポートの様子を控えておく（見つからなかったときの表示・ログ用）。
            _finalReports = GetReports();
            IsDone = true;
            Result = chosen != null ? OpenedTransport(chosen) : null;
            ResultHasData = Result != null && hasData;
            foreach (var candidate in _candidates)
            {
                if (candidate == chosen)
                {
                    continue;
                }

                // 写しの設定を消す（さがし直すたびに増えないように）。閉じる途中のスレッドが読むのは数値だけなので、消しても問題ない。
                Object.Destroy(candidate.config);

                // まだ開いている途中のポートも、開き終わったら閉じる。
                candidate.opening.ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result.Dispose();
                    }
                });
            }
        }
    }
}

#endif // MS2026_GRIP_SERIAL_ENABLED
