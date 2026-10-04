using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Transports;
using UnityEngine;

/// <summary>
/// Arduino(Uno)からのシリアル入力を握力入力ブリッジの入力元にするコンポーネント。
/// シーン内の任意のGameObjectに付けてPlayすると、<see cref="MS2026.GripInputBridge.GripInputBridge.Provider"/>
/// の値が実機の値に切り替わり、同じ playerIndex の LaserTurret のレーザーの太さ・熱に反映される。
/// 入力元は「実機＋キーボード」の合成になるため、実機を挿したままキーボードでも操作できる。
/// COMポートを開けなかったとき(デバイス未接続など)は何も差し替えず、キーボードシミュレータのまま動く。
///
/// Arduino側の出力は次のどちらでもよい（どちらも1行1サンプル、Serial.println で送る）:
/// <list type="bullet">
/// <item><c>512</c> … analogRead の値をそのまま（0-1023）。プレイヤー0(P1)として扱う。</item>
/// <item><c>512,300</c> … カンマ区切りで最大4つ。先頭から順にP1, P2, P3, P4として扱う。</item>
/// <item><c>G,0,2048,183920</c> … 設計書 6.2 の正式フォーマット（0-4095、複数プレイヤー対応）。</item>
/// </list>
///
/// 前提: Player Settings の Api Compatibility Level を「.NET Framework」にし、
/// Scripting Define Symbols に「MS2026_GRIP_SERIAL_ENABLED」を追加していること。
/// </summary>
public sealed class ArduinoSerialReader : MonoBehaviour
{
    [Tooltip("設定アセット。設定した場合は、下のポート名〜センサー特性の項目は使わずアセットの値を使う。")]
    public GripDeviceConfig deviceConfig;

    [Tooltip("ArduinoのCOMポート名。Arduino IDEの「ツール > ポート」で確認できる（例: COM3）。")]
    public string portName = "COM3";

    [Tooltip("Arduinoスケッチの Serial.begin(...) と同じ値にする。")]
    public int baudRate = 9600;

    [Tooltip("この時間(ミリ秒)以上データが来なければ「切断」表示にする。Arduino側の送信間隔より長くする。")]
    [Min(1)]
    public int heartbeatTimeoutMs = 1000;

    [Header("センサー特性")]
    [Tooltip("ONなら「中心値からどれだけ離れたか」を握力とする。何もしていないときに約512を出し、" +
             "磁石のN極/S極で上下に振れるリニアホールセンサー(49E等)向け。どちらの極を近づけても反応する。")]
    public bool centeredSensor = true;

    [Tooltip("centeredSensorがONのとき、何もしていない状態の値(0-1)。Unoの512なら0.5。")]
    [Range(0.01f, 0.99f)]
    public float sensorCenter01 = 0.5f;

    [Header("デバッグ表示")]
    [Tooltip("ONなら受信している値を一定間隔でConsoleに出す。")]
    public bool logValues = true;

    [Tooltip("Consoleに出す間隔(秒)。")]
    [Min(0.01f)]
    public float logIntervalSeconds = 0.3f;

    [Tooltip("Consoleに出すプレイヤー数。センサーの数に合わせる(A0,A5の2つなら2)。")]
    [Range(1, 4)]
    public int logPlayerCount = 2;

    private float _nextLogTime;

    private void Update()
    {
        if (!logValues || Time.unscaledTime < _nextLogTime)
        {
            return;
        }

        _nextLogTime = Time.unscaledTime + logIntervalSeconds;

        // arduino: Arduinoから届いたままの値をUnoのanalogRead換算(0-1023)にしたもの。IDEのシリアルモニタの値と比べられる。
        // raw: センサー特性の変換後の値(0-1)。磁石なしで0付近になっていれば正しい。
        // grip: フィルタ・キャリブレーション後の値(0-1)。これがレーザーの太さ・熱に使われる。
        var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
        var message = new System.Text.StringBuilder("[ArduinoSerialReader]");
        for (var i = 0; i < logPlayerCount; i++)
        {
            var arduinoValue = Mathf.RoundToInt(GetReceivedValue(i) * GripSerialProtocol.SimpleFormatRawValueMax);
            message.Append($"  P{i + 1}: arduino={arduinoValue} raw={provider.GetRawValue(i):F3} grip={provider.GetGripValue(i):F3} {provider.GetStatus(i)}");
        }

        Debug.Log(message.ToString());
    }

#if MS2026_GRIP_SERIAL_ENABLED
    private SerialGripTransport _transport;
    private CompositeGripTransport _composite;

    private void Start()
    {
        var config = deviceConfig != null ? deviceConfig : CreateInlineConfig();
        var transport = new SerialGripTransport(config);
        if (!transport.IsPortOpen)
        {
            // ポートが無い(デバイス未接続)・使用中のときは入力元を差し替えない。差し替えると、
            // 何も届かない実機が入力元になってキーボードまで効かなくなる。
            transport.Dispose();
            Debug.LogWarning($"[ArduinoSerialReader] {config.serialPortName} を開けなかったため、キーボードシミュレータのまま動かします。");
            return;
        }

        // 実機とキーボードを合成する。実機が繋がっていれば大きい方、途切れたらキーボードの値になる。
        _transport = transport;
        _composite = new CompositeGripTransport(_transport, MS2026.GripInputBridge.GripInputBridge.Simulator);
        MS2026.GripInputBridge.GripInputBridge.SetTransport(_composite);
        Debug.Log($"[ArduinoSerialReader] {config.serialPortName} ({config.baudRate}bps) を入力元にしました(キーボードも併用できます)。");
    }

    private void OnDestroy()
    {
        if (_transport == null)
        {
            return;
        }

        // 自分が設定した入力元がまだ使われているときだけシミュレータへ戻す(ウィンドウ等が別の入力元に切り替えていたら触らない)。
        if (ReferenceEquals(MS2026.GripInputBridge.GripInputBridge.CurrentTransport, _composite))
        {
            MS2026.GripInputBridge.GripInputBridge.SetTransport(MS2026.GripInputBridge.GripInputBridge.Simulator);
        }

        // COMポートを掴んだままだと次のPlayで開けなくなるため、必ず解放する。
        _transport.Dispose();
        _transport = null;
        _composite = null;
    }

    private float GetReceivedValue(int playerIndex)
    {
        return _transport != null
            ? _transport.GetReceivedValue(playerIndex)
            : MS2026.GripInputBridge.GripInputBridge.Provider.GetRawValue(playerIndex);
    }

    private GripDeviceConfig CreateInlineConfig()
    {
        var config = ScriptableObject.CreateInstance<GripDeviceConfig>();
        config.serialPortName = portName;
        config.baudRate = baudRate;
        config.heartbeatTimeoutMs = heartbeatTimeoutMs;
        config.centeredSensor = centeredSensor;
        config.sensorCenter01 = sensorCenter01;
        return config;
    }
#else
    private void Start()
    {
        Debug.LogWarning(
            "[ArduinoSerialReader] 実機通信が無効のため、キーボードシミュレータのままです。\n" +
            "1. Edit > Project Settings > Player > Other Settings > Api Compatibility Level を「.NET Framework」に変更\n" +
            "2. 同じ画面の Scripting Define Symbols に「MS2026_GRIP_SERIAL_ENABLED」を追加");
    }

    private float GetReceivedValue(int playerIndex)
    {
        return MS2026.GripInputBridge.GripInputBridge.Provider.GetRawValue(playerIndex);
    }
#endif
}
