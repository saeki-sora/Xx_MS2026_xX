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
        }

        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly int _timeoutMs;
        private bool _disposed;

        /// <param name="config">ポート名以外の設定（速さ・センサー特性など）。ポートごとに写しを作って使う。メインスレッドで呼ぶこと。</param>
        /// <param name="ports">試すポート（先頭ほど優先。<see cref="GripPortCandidates.Build"/>）。</param>
        /// <param name="timeoutMs">値が届くのを待つ時間（Arduino Uno はポートを開くと再起動するので 2000 以上）。</param>
        /// <param name="logFirstPortError">先頭のポートを開けなかったときにエラーを出すか。</param>
        public SerialGripPortSearch(GripDeviceConfig config, IReadOnlyList<string> ports, int timeoutMs, bool logFirstPortError)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _timeoutMs = Math.Max(0, timeoutMs);
            for (var i = 0; i < ports.Count; i++)
            {
                var portConfig = Object.Instantiate(config);
                portConfig.serialPortName = ports[i];
                var logError = i == 0 && logFirstPortError;
                _candidates.Add(new Candidate
                {
                    port = ports[i],
                    config = portConfig,
                    opening = Task.Run(() => new SerialGripTransport(portConfig, logError))
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
