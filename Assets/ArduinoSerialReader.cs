using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Transports;
using UnityEngine;

/// <summary>
/// Arduino(Uno)からのシリアル入力を握力入力ブリッジの入力元にするコンポーネント。
/// シーン内の任意のGameObjectに付けてPlayすると、<see cref="MS2026.GripInputBridge.GripInputBridge.Provider"/>
/// の値が実機の値に切り替わり、同じ playerIndex の LaserTurret のレーザーの太さ・熱に反映される。
/// 入力元は「実機＋キーボード」の合成になるため、実機を挿したままキーボードでも操作できる。
/// COMポートを開けなかったとき(デバイス未接続など)は何も差し替えず、キーボードシミュレータのまま動く。
/// <see cref="autoFindPort"/> がONなら、設定したポートで値が届かないとき、PCにあるほかのCOMポートも試して値が届いたポートを使う
/// (展示ではPCごとにポート番号が違ってもよいように。見つからなければ時々さがし直す)。
/// <see cref="keepAcrossScenes"/> がONの物(タイトルに置く)はシーンが変わっても残り、ほかのシーンの同じ部品は何もしない
/// (シーンごとにポートを開け直すと、Arduino Uno は開くたびに約2秒再起動して値が途切れるため)。
/// 使っているポートから <see cref="deviceLostSeconds"/> 秒値が届かない・ポートが使えなくなったら(USBが抜けた等)、キーボードに切り替えてさがし直す。
/// USBを挿し直せば自動で実機に戻る。
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

    [Tooltip("ArduinoのCOMポート名。Arduino IDEの「ツール > ポート」で確認できる（例: COM3）。下の「ポートの自動さがし」がONなら、ここで値が届かなくてもほかのポートを試す。")]
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

    [Header("ポートの自動さがし")]
    [Tooltip("ONなら、上のポートで値が届かないとき、PCにあるほかのCOMポートも全部試して、値が届いたポートを使う（PCごとにポート番号が違ってもよい）。見つからなければ、しばらくしてからさがし直す。")]
    public bool autoFindPort = true;

    [Tooltip("ポートを開いてから、最初の値が届くのを待つ秒数。Arduino Uno はポートを開くと再起動するので2秒以上にする。")]
    [Min(0.5f)]
    public float portSearchSeconds = 4f;

    [Tooltip("どのポートからも値が届かなかったとき、さがし直すまでの秒数（0 = さがし直さない）。展示中にUSBを挿し直したときのため。")]
    [Min(0f)]
    public float portRetrySeconds = 5f;

    [Tooltip("使っているポートから、この秒数のあいだ値が届かなければ（USBが抜けた・デバイスが止まった など）、キーボードに切り替えて、ポートをさがし直す。" +
             "USBを挿し直せば、上の「さがし直す秒数」ごとにさがして自動で戻る（ポートの自動さがしが OFF なら、上のポートだけをさがし直す）。Arduino の送信間隔より長くする。")]
    [Min(0.5f)]
    public float deviceLostSeconds = 3f;

    [Header("シーンをまたぐ")]
    [Tooltip("ONなら、シーンが変わっても消えずに実機を使い続ける（最初のシーン＝タイトルに置く物をON）。これがあると、ほかのシーンの同じ部品は何もしない（ポートを開け直さない）。")]
    public bool keepAcrossScenes;

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

    // シーンをまたいで残っている読み取り（タイトルに置いた物）。あれば、ほかのシーンの同じ部品は何もしない。
    private static ArduinoSerialReader s_persistent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_persistent = null;
    }

    private void Awake()
    {
        if (!keepAcrossScenes)
        {
            return;
        }

        if (s_persistent != null && s_persistent != this)
        {
            // タイトルに戻ってきた: 前から残っている物をそのまま使う。
            Destroy(gameObject);
            return;
        }

        s_persistent = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>シーンをまたいで残っている別の読み取りがあり、こちらは何もしないか。</summary>
    private bool DeferToPersistent()
    {
        if (s_persistent == null || s_persistent == this)
        {
            return false;
        }

        Debug.Log($"[ArduinoSerialReader] 前のシーンから残っている読み取り（{s_persistent.name}）を使うので、ここではポートを開きません。");
        enabled = false;
        return true;
    }

    private void Update()
    {
        TickDeviceHealth();
        TickPortSearch();

        // 実機につながっていないとき(ポートを開けなかった・実機通信が無効)は出さない。
        // 意味の無い値を0.3秒ごとにログへ書き続けると、ビルドでは文字列の生成とログ出力が負荷(GC)になるため(2026-10-04)。
        if (!logValues || !IsDeviceActive || Time.unscaledTime < _nextLogTime)
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
    private GripDeviceConfig _config;
    private SerialGripPortSearch _search;
    private float _retryAt = -1f;
    private bool _warnedNotFound;
    private float _installedAt;
    private bool _warnedSilentPort;

    // 使えなくなったポートを閉じてから、さがし直すまでの秒数（閉じ終わる前に同じポートを開こうとして失敗しないように）。
    private const float ReopenDelaySeconds = 1f;

    /// <summary>実機のCOMポートを開けて、入力元として使っているか。</summary>
    public bool IsDeviceActive => _transport != null;

    private void Start()
    {
        if (DeferToPersistent())
        {
            return;
        }

        _config = deviceConfig != null ? deviceConfig : CreateInlineConfig();
        if (autoFindPort)
        {
            BeginPortSearch(true);
            return;
        }

        var transport = new SerialGripTransport(_config);
        if (!transport.IsPortOpen)
        {
            // ポートが無い(デバイス未接続)・使用中のときは入力元を差し替えない。差し替えると、
            // 何も届かない実機が入力元になってキーボードまで効かなくなる。
            transport.Dispose();
            Debug.LogWarning($"[ArduinoSerialReader] {_config.serialPortName} を開けなかったため、キーボードシミュレータのまま動かします。");
            return;
        }

        Install(transport, true);
    }

    // 設定したポートを先頭に、PCにあるCOMポートを全部同時に試す（値が届くまでの間もキーボードで動く）。
    private void BeginPortSearch(bool firstTime)
    {
        // 自動さがしが OFF のとき（挿し直しを待つ間）は、設定したポートだけを試す。
        var ports = GripPortCandidates.Build(_config.serialPortName, SerialGripTransport.GetAvailablePortNames(), autoFindPort);
        _search = new SerialGripPortSearch(_config, ports, Mathf.RoundToInt(portSearchSeconds * 1000f), false);
        _retryAt = -1f;
        if (firstTime)
        {
            Debug.Log($"[ArduinoSerialReader] 握力デバイスのポートをさがしています（{_search.DescribePorts()}）。");
        }
    }

    private void TickPortSearch()
    {
        if (_search == null)
        {
            if (_retryAt >= 0f && Time.unscaledTime >= _retryAt && _transport == null)
            {
                BeginPortSearch(false);
            }

            return;
        }

        _search.Poll();
        if (!_search.IsDone)
        {
            return;
        }

        var found = _search.Result;
        var hasData = _search.ResultHasData;
        var tried = _search.DescribePorts();
        _search.Dispose();
        _search = null;
        if (found != null)
        {
            Install(found, hasData);
            return;
        }

        if (!_warnedNotFound)
        {
            _warnedNotFound = true;
            Debug.LogWarning($"[ArduinoSerialReader] 握力デバイスから値が届くポートがありませんでした（試したポート: {tried}）。キーボードシミュレータのまま動かします。" +
                             (portRetrySeconds > 0f ? $"{portRetrySeconds:0.#}秒ごとにさがし直します。" : string.Empty));
        }

        _retryAt = portRetrySeconds > 0f ? Time.unscaledTime + portRetrySeconds : -1f;
    }

    private void Install(SerialGripTransport transport, bool hasData)
    {
        // 実機とキーボードを合成する。実機が繋がっていれば大きい方、途切れたらキーボードの値になる。
        _transport = transport;
        _composite = new CompositeGripTransport(_transport, MS2026.GripInputBridge.GripInputBridge.Simulator);
        MS2026.GripInputBridge.GripInputBridge.SetTransport(_composite);
        _installedAt = Time.unscaledTime;
        _warnedNotFound = false;
        Debug.Log($"[ArduinoSerialReader] {transport.PortName} ({_config.baudRate}bps) を入力元にしました(キーボードも併用できます)。" +
                  (hasData ? string.Empty : "まだ値は届いていません。"));
    }

    // 使っているポートから値が届かなくなったら（USBが抜けた・デバイスが止まった）、キーボードに戻してさがし直す。
    private void TickDeviceHealth()
    {
        if (_transport == null)
        {
            return;
        }

        // 一度も値が届いていない（時間切れで選んだ）ポートは、選んでからの秒数で見る。
        var silentSeconds = _transport.HasReceivedData ? _transport.SecondsSinceLastData : Time.unscaledTime - _installedAt;
        if (!_transport.IsPortLost && silentSeconds < deviceLostSeconds)
        {
            return;
        }

        if (_transport.HasReceivedData || _transport.IsPortLost)
        {
            _warnedSilentPort = false;
            Debug.LogWarning($"[ArduinoSerialReader] {_transport.PortName} から値が届かなくなりました（USBが抜けた・デバイスが止まった など）。" +
                             "キーボードに切り替えて、ポートをさがし直します（USBを挿し直せば自動で戻ります）。");
        }
        else if (!_warnedSilentPort)
        {
            // 開けたが一度も値が届かないポート。さがし直しを繰り返すので、知らせるのは1回だけ。
            _warnedSilentPort = true;
            Debug.LogWarning($"[ArduinoSerialReader] {_transport.PortName} は開けましたが値が届きません。ときどきポートをさがし直します。");
        }

        Uninstall();
        _retryAt = Time.unscaledTime + ReopenDelaySeconds;
    }

    private void Uninstall()
    {
        // 自分が設定した入力元がまだ使われているときだけシミュレータへ戻す(ウィンドウ等が別の入力元に切り替えていたら触らない)。
        if (ReferenceEquals(MS2026.GripInputBridge.GripInputBridge.CurrentTransport, _composite))
        {
            MS2026.GripInputBridge.GripInputBridge.SetTransport(MS2026.GripInputBridge.GripInputBridge.Simulator);
        }

        // 閉じるのに最大1秒ほどかかる（受信スレッドの終了待ち）ので、画面が止まらないよう別のスレッドで閉じる。
        var transport = _transport;
        _transport = null;
        _composite = null;
        System.Threading.Tasks.Task.Run(() => transport.Dispose());
    }

    private void OnDestroy()
    {
        if (s_persistent == this)
        {
            s_persistent = null;
        }

        _search?.Dispose();
        _search = null;
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
    /// <summary>実機通信が無効なので、常にfalse。</summary>
    public bool IsDeviceActive => false;

    private void TickPortSearch()
    {
    }

    private void TickDeviceHealth()
    {
    }

    private void OnDestroy()
    {
        if (s_persistent == this)
        {
            s_persistent = null;
        }
    }

    private void Start()
    {
        if (DeferToPersistent())
        {
            return;
        }

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
