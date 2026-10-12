// 実機（マイコン）を接続する準備ができ、Player Settings の Api Compatibility Level を
// 「.NET Framework」に変更したら、Project Settings > Player > Other Settings >
// Configuration > Scripting Define Symbols に "MS2026_GRIP_SERIAL_ENABLED" を追加して
// このファイルを有効化すること。それまではSystem.IO.Portsに依存するこのファイル全体を
// コンパイル対象から外し、実機が無い開発中でも他の部分のビルド・テストを止めないようにしている。
#if MS2026_GRIP_SERIAL_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using MS2026.GripInputBridge.Data;
using Debug = UnityEngine.Debug;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 実機（マイコン + 握力センサー）からのUSBシリアル入力を扱うトランスポート。設計書 6章 参照。
    ///
    /// <para>
    /// <b>前提条件（重要）:</b> このクラスは <see cref="System.IO.Ports.SerialPort"/> に依存する。
    /// Unity の Player Settings で Api Compatibility Level が「.NET Standard 2.1」になっていると
    /// このAPIが正しく動作しない/コンパイルできない場合がある。「.NET Framework」への変更、または
    /// System.IO.Ports 互換パッケージの追加が必要。
    /// ProjectSettings の変更はAIではなく人間が行うこと（設計書 0.1・MCP運用ルール参照）。
    /// </para>
    ///
    /// <para>
    /// 受信はバックグラウンドスレッドで行い、メインスレッドをブロックしない。
    /// スレッド間の値の受け渡しは <see cref="Volatile"/> のみで行い、ロックを使わずに単純化している
    /// （書き込み手はスレッド1つだけのため、この単純な方式で安全）。
    /// </para>
    /// </summary>
    public sealed class SerialGripTransport : IGripTransport, IDisposable
    {
        private readonly GripDeviceConfig _config;
        private readonly SerialPort _serialPort;
        private readonly Thread _readThread;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private readonly float[] _rawValues = new float[GripInputBridgeConstants.PlayerCount];
        private readonly float[] _receivedValues = new float[GripInputBridgeConstants.PlayerCount];
        private readonly long[] _lastReceivedAtMs = new long[GripInputBridgeConstants.PlayerCount];
        private readonly bool[] _everConnected = new bool[GripInputBridgeConstants.PlayerCount];

        // 受信スレッド専用。1行に複数プレイヤー分の値が入る簡易フォーマット用に使い回す。
        private readonly List<GripSerialProtocol.ParsedSample> _parsedSamples = new List<GripSerialProtocol.ParsedSample>();

        private volatile bool _isRunning;

        // USBが抜けた等でポートが使えなくなった（受信スレッドは止まっている）。
        private volatile bool _portLost;

        // 受信スレッド専用。続けて起きた受信エラーの数（正しく1行読めたら 0 に戻す）。
        private int _consecutiveErrors;

        // 受信エラーがこの回数続いたら、ポートが使えなくなったとみなす（約0.5秒）。
        private const int LostAfterErrors = 5;

        public event Action<int> OnConnected;
#pragma warning disable CS0067 // 未使用でもインターフェースの都合で必要なイベント
        public event Action<int> OnDisconnected;
#pragma warning restore CS0067

        private readonly bool _logOpenError;

        public SerialGripTransport(GripDeviceConfig config) : this(config, true)
        {
        }

        /// <param name="logOpenError">false なら、ポートを開けなかったときにエラーを出さない（ポートを自動でさがすとき用）。</param>
        public SerialGripTransport(GripDeviceConfig config, bool logOpenError)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logOpenError = logOpenError;

            for (var i = 0; i < _lastReceivedAtMs.Length; i++)
            {
                _lastReceivedAtMs[i] = long.MinValue;
            }

            _serialPort = new SerialPort(_config.serialPortName, _config.baudRate)
            {
                ReadTimeout = 500,
                NewLine = "\n"
            };

            var openSucceeded = TryOpenPort();
            _isRunning = openSucceeded;

            if (openSucceeded)
            {
                _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "GripInputBridge.SerialRead" };
                _readThread.Start();
            }
        }

        /// <summary>COMポートを開けて受信を始められたか。falseなら(ポートが無い・使用中など)この入力元からは何も届かない。</summary>
        public bool IsPortOpen => _isRunning;

        /// <summary>このトランスポートが開いているポートの名前。</summary>
        public string PortName => _config.serialPortName;

        /// <summary>正しい形の値を一度でも受け取ったか（そのポートの先に握力デバイスがつながっている目印）。</summary>
        public bool HasReceivedData
        {
            get
            {
                for (var i = 0; i < _lastReceivedAtMs.Length; i++)
                {
                    if (Volatile.Read(ref _lastReceivedAtMs[i]) != long.MinValue)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// USBが抜けた等でポートが使えなくなったか（受信エラーが続いた・ポートが閉じられた）。true になったら受信は止まっているので、
        /// Dispose して、さがし直す（<see cref="SerialGripPortSearch"/>）。
        /// </summary>
        public bool IsPortLost => _portLost;

        /// <summary>最後に値が届いてからの秒数（どのプレイヤーの値でもよい。一度も届いていなければ +∞）。</summary>
        public double SecondsSinceLastData
        {
            get
            {
                var latest = long.MinValue;
                for (var i = 0; i < _lastReceivedAtMs.Length; i++)
                {
                    latest = Math.Max(latest, Volatile.Read(ref _lastReceivedAtMs[i]));
                }

                return latest == long.MinValue ? double.PositiveInfinity : (_clock.ElapsedMilliseconds - latest) / 1000.0;
            }
        }

        /// <summary>このPCにあるCOMポートの名前（取れなければ空）。</summary>
        public static string[] GetAvailablePortNames()
        {
            try
            {
                return SerialPort.GetPortNames();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GripInputBridge] COMポートの一覧を取れませんでした: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public bool IsConnected(int playerIndex)
        {
            if (!IsValidIndex(playerIndex) || !_isRunning)
            {
                return false;
            }

            var lastReceived = Volatile.Read(ref _lastReceivedAtMs[playerIndex]);
            if (lastReceived == long.MinValue)
            {
                return false; // まだ一度も受信していない
            }

            var elapsedSinceLastSample = _clock.ElapsedMilliseconds - lastReceived;
            return elapsedSinceLastSample <= _config.heartbeatTimeoutMs;
        }

        public float GetRawValue(int playerIndex)
        {
            return IsValidIndex(playerIndex) ? Volatile.Read(ref _rawValues[playerIndex]) : 0f;
        }

        /// <summary>
        /// マイコンから届いた値(0-1)を、<see cref="GripDeviceConfig.ToGripRawValue"/> で変換する前のまま返す。
        /// センサーの中心値の確認など、診断表示用。
        /// </summary>
        public float GetReceivedValue(int playerIndex)
        {
            return IsValidIndex(playerIndex) ? Volatile.Read(ref _receivedValues[playerIndex]) : 0f;
        }

        /// <summary>スレッドの停止とCOMポートの解放を行う。トランスポートを差し替える際は必ず呼ぶこと。</summary>
        public void Dispose()
        {
            _isRunning = false;

            try
            {
                _readThread?.Join(millisecondsTimeout: 1000);
            }
            catch (ThreadStateException)
            {
                // スレッドが開始していない等、想定内のため無視してよい。
            }

            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GripInputBridge] シリアルポートのクローズ中にエラー: {ex.Message}");
            }

            _serialPort.Dispose();
        }

        private bool TryOpenPort()
        {
            try
            {
                _serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                // ここで例外を握りつぶさずログに残す(設計書13.2セルフチェック項目)。自動でさがすときは、さがす側がまとめて知らせる。
                if (_logOpenError)
                {
                    Debug.LogError($"[GripInputBridge] シリアルポート '{_config.serialPortName}' を開けませんでした: {ex.Message}");
                }

                return false;
            }
        }

        private void ReadLoop()
        {
            while (_isRunning)
            {
                string line;
                try
                {
                    line = _serialPort.ReadLine();
                }
                catch (TimeoutException)
                {
                    // データが来ていないだけ。ハートビート判定は IsConnected 側の経過時間比較で行う。
                    continue;
                }
                catch (Exception ex)
                {
                    if (!_isRunning)
                    {
                        // Dispose中に発生した例外(ポートクローズによる中断等)は正常終了として扱う。
                        break;
                    }

                    // ケーブル抜け等。一時的なエラーなら続ける(通信全体を落とさない、設計書6.3)。
                    // 続けて起きる・ポートが閉じられたときは「使えなくなった」として受信を止める（USBを挿し直したら、使う側がさがし直す）。
                    _consecutiveErrors++;
                    if (_consecutiveErrors == 1)
                    {
                        Debug.LogWarning($"[GripInputBridge] シリアル受信エラー（{_config.serialPortName}）: {ex.Message}");
                    }

                    if (_consecutiveErrors >= LostAfterErrors || !_serialPort.IsOpen)
                    {
                        _portLost = true;
                        Debug.LogWarning($"[GripInputBridge] {_config.serialPortName} が使えなくなりました（USBが抜けた など）。受信を止めます。");
                        break;
                    }

                    Thread.Sleep(100);
                    continue;
                }

                _consecutiveErrors = 0;
                if (!GripSerialProtocol.TryParseSamples(line, _parsedSamples))
                {
                    continue; // 化けた1行はスキップする(設計書6.3)。
                }

                foreach (var sample in _parsedSamples)
                {
                    Volatile.Write(ref _receivedValues[sample.PlayerIndex], sample.NormalizedRawValue);
                    Volatile.Write(ref _rawValues[sample.PlayerIndex], _config.ToGripRawValue(sample.NormalizedRawValue));
                    Volatile.Write(ref _lastReceivedAtMs[sample.PlayerIndex], _clock.ElapsedMilliseconds);

                    if (!_everConnected[sample.PlayerIndex])
                    {
                        _everConnected[sample.PlayerIndex] = true;
                        OnConnected?.Invoke(sample.PlayerIndex);
                    }
                }
            }
        }

        private static bool IsValidIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < GripInputBridgeConstants.PlayerCount;
        }
    }
}

#endif // MS2026_GRIP_SERIAL_ENABLED
