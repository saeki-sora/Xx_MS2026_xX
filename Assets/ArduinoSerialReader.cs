using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Relay;
using MS2026.GripInputBridge.Transports;
using UnityEngine;
using UnityEngine.InputSystem;

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
/// <see cref="rawValuesToggleKey"/>（F8）で、コントローラーから届いた生の値（届いた1行そのまま・数値）を画面に出せる（<see cref="GripRawValueOverlay"/>）。
///
/// 展示（1台の Arduino に4人分のセンサー・4台のPC）: 起動オプション <c>-grip-sensor N</c>（N = そのPCの前の人のセンサー番号 1〜4）を付けると、
/// Arduino をつないだPCが4人分の値を LAN へ放送し、どのPCも自分の番号の値を「このPCの前の人（P1）」の握力として使う（<see cref="GripRelaySession"/>）。
/// 付けなければ今まで通り（通信しない）。
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

    [Tooltip("実機（マイコン）の値を使うプレイヤー数。P1から数える。1 = P1のレーザーだけ実機で出す（1人プレイ・通信なし／通信ありでも各PCの1人目のセンサーだけ使う）。" +
             "マイコンがカンマ区切りで2つ以上の値を送ってきても、ここより後のプレイヤーは実機の値を使わず、キーボードだけで動く。1台のPCで2人以上が実機で遊ぶときに増やす。" +
             "起動オプション -grip-sensor N（LAN で配る使い方）のときは使わない（4人分を放送し、P1 にはセンサー N を使う）。")]
    [Range(0, 4)]
    public int devicePlayerCount = 1;

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

    [Header("LAN で握力を配る（1台の Arduino に4人分・展示）")]
    [Tooltip("センサー1の放送を受け取る UDP の番号。センサー n は「この番号 + n - 1」。全部のPCで同じにする（起動オプション -grip-relay-port でも変えられる）。" +
             "起動オプション -grip-sensor N を付けたときだけ使う。")]
    public int relayBasePort = GripRelayOptions.DefaultBasePort;

    [Tooltip("この秒数 LAN の放送が届かなければ、その人の握力は「届いていない」（0）にする（撃ちっぱなしを防ぐ）。")]
    [Min(0.1f)]
    public float relayTimeoutSeconds = 0.5f;

    [Tooltip("Arduino をつないだPCが、1秒に放送する回数。")]
    [Range(10f, 120f)]
    public float relaySendRate = 60f;

    [Tooltip("エディタで試すとき用: このPCの前の人のセンサー番号（1〜4）。0 = LAN で配らない（今まで通り）。" +
             "ビルドしたゲームでは使わない（起動オプション -grip-sensor N で決める。デスクトップの「センサーNで起動」アイコン）。")]
    [Range(0, 4)]
    public int editorRelaySensorNumber;

    [Tooltip("エディタで試すとき用: Arduino が無くても、キーボード（Q/W/O/P = センサー1〜4）の値を LAN へ放送する（起動オプション -grip-relay-send と同じ）。")]
    public bool editorRelayForceSend;

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

    [Header("画面に生の値を出す（デバッグ）")]
    [Tooltip("ONなら、始めから画面の左上に、コントローラーから届いた生の値（届いた1行そのまま・数値・届く頻度）を出す。" +
             "ビルドしたゲームでは、起動オプション -debug-ui を付けたときだけ始めから出す（会場で見せないため）。起動オプション -gripdebug なら、ここがOFFでも始めから出す。")]
    public bool showRawValues;

    [Tooltip("生の値の表示を出す/消すキー（エディタでもビルドでも効く）。F1〜F7 はカメラのデバッグに使っているので避ける。")]
    public Key rawValuesToggleKey = Key.F8;

    /// <summary>ビルドでも始めから生の値を出す起動オプション。</summary>
    public const string ShowRawValuesArgument = "-gripdebug";

    private float _nextLogTime;
    private GripRawValueOverlay _overlay;

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

    // 生の値の表示を付ける（使う読み取りだけ。ほかのシーンの何もしない読み取りには付けない）。最初は出すかどうかを決めて、隠しておく。
    private void SetUpRawValueOverlay()
    {
        _overlay = gameObject.AddComponent<GripRawValueOverlay>();
        _overlay.reader = this;
        _overlay.toggleKey = rawValuesToggleKey;
        _overlay.enabled = (showRawValues && MS2026.Fortress.Hud.DebugUi.Visible) || HasArgument(ShowRawValuesArgument);
    }

    private void TickRawValueOverlay()
    {
        var keyboard = Keyboard.current;
        if (_overlay != null && keyboard != null && rawValuesToggleKey != Key.None && keyboard[rawValuesToggleKey].wasPressedThisFrame)
        {
            _overlay.enabled = !_overlay.enabled;
        }
    }

    private static bool HasArgument(string argument)
    {
        foreach (var arg in System.Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, argument, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ───────── LAN で握力を配る（-grip-sensor N のときだけ）─────────

    private GripRelaySession _relay;
    private string _relayOptionsError;

    /// <summary>LAN で握力を配る使い方（-grip-sensor N）で動いているか。</summary>
    public bool IsRelayMode => _relay != null;

    /// <summary>LAN で配る仕組みの今の様子（生の値の表示用）。使っていなければ null。</summary>
    public GripRelayStatus? RelayStatus => _relay?.GetStatus();

    /// <summary>LAN で配る起動オプションの書き方が間違っていて、使わずに動いているときの説明（無ければ null）。</summary>
    public string RelayOptionsError => _relayOptionsError;

    // 起動オプション -grip-sensor N（エディタでは editorRelaySensorNumber）があれば、LAN で配る仕組みを動かして入力元にする。
    private void StartRelay()
    {
        var options = Application.isEditor && editorRelaySensorNumber > 0
            ? GripRelayOptions.Create(editorRelaySensorNumber, editorRelayForceSend, relayBasePort)
            : GripRelayOptions.Parse(System.Environment.GetCommandLineArgs(), relayBasePort);
        if (options.Error != null)
        {
            Debug.LogError("[ArduinoSerialReader] 起動オプションの書き方が違います: " + options.Error);
        }

        if (!options.Enabled)
        {
            _relayOptionsError = options.Error;
            return;
        }

        try
        {
            _relay = new GripRelaySession(options, relayTimeoutSeconds, relaySendRate);
        }
        catch (System.Exception e)
        {
            // 放送の準備に失敗しても、このPCの Arduino・キーボードでは動くようにする（ポートのさがし始めを止めない）。
            _relayOptionsError = "LAN で配る仕組みを始められませんでした: " + e.Message;
            Debug.LogError("[ArduinoSerialReader] " + _relayOptionsError);
            return;
        }

        MS2026.GripInputBridge.GripInputBridge.SetTransport(_relay.Transport);
    }

    private void StopRelay()
    {
        if (_relay == null)
        {
            return;
        }

        // 自分が設定した入力元がまだ使われているときだけシミュレータへ戻す(ウィンドウ等が別の入力元に切り替えていたら触らない)。
        if (ReferenceEquals(MS2026.GripInputBridge.GripInputBridge.CurrentTransport, _relay.Transport))
        {
            MS2026.GripInputBridge.GripInputBridge.SetTransport(MS2026.GripInputBridge.GripInputBridge.Simulator);
        }

        _relay.Dispose();
        _relay = null;
    }

    private void Update()
    {
        _relay?.Tick();
        TickRawValueOverlay();
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

        SetUpRawValueOverlay();
        StartRelay();
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
            Debug.LogWarning($"[ArduinoSerialReader] {_config.serialPortName} を開けなかったため、{FallbackDescription}。");
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
            Debug.LogWarning($"[ArduinoSerialReader] 握力デバイスから値が届くポートがありませんでした（試したポート: {tried}）。{FallbackDescription}。" +
                             (portRetrySeconds > 0f ? $"{portRetrySeconds:0.#}秒ごとにさがし直します。" : string.Empty));
        }

        _retryAt = portRetrySeconds > 0f ? Time.unscaledTime + portRetrySeconds : -1f;
    }

    private void Install(SerialGripTransport transport, bool hasData)
    {
        _transport = transport;
        _installedAt = Time.unscaledTime;
        _warnedNotFound = false;
        if (_relay != null)
        {
            // LAN で配る使い方: 入力元は最初に設定した物のまま。この Arduino の4人分を LAN へ放送し、自分の分（センサー N）もここから取る。
            _composite = null;
            _relay.SetLocalDevice(transport);
            Debug.Log($"[ArduinoSerialReader] {transport.PortName} ({_config.baudRate}bps) の Arduino を使います。4人分の値を LAN へ放送し、" +
                      $"このPCの前の人はセンサー{_relay.Options.SensorNumber}番の値で動きます（キーボードも併用できます）。" +
                      (hasData ? string.Empty : "まだ値は届いていません。"));
            return;
        }

        // 実機とキーボードを合成する。実機が繋がっていれば大きい方、途切れたらキーボードの値になる。
        _composite = new CompositeGripTransport(_transport, MS2026.GripInputBridge.GripInputBridge.Simulator, devicePlayerCount);
        MS2026.GripInputBridge.GripInputBridge.SetTransport(_composite);
        Debug.Log($"[ArduinoSerialReader] {transport.PortName} ({_config.baudRate}bps) を入力元にしました（実機の値を使うのは {DescribeDevicePlayers(_composite.DevicePlayerCount)}。キーボードも併用できます）。" +
                  (hasData ? string.Empty : "まだ値は届いていません。"));
    }

    // このPCに Arduino が無いときに、何で動くか（ログ用）。
    private string FallbackDescription => _relay != null
        ? $"LAN の放送（センサー{_relay.Options.SensorNumber}番。届かなければキーボード）で動かします"
        : "キーボードシミュレータのまま動かします";

    private static string DescribeDevicePlayers(int count)
    {
        return count <= 0 ? "だれもいない" : count == 1 ? "P1 だけ" : $"P1〜P{count}";
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
        // LAN で配る使い方: 放送をやめ、自分の分は LAN の放送（ほかのPCに Arduino を挿し替えた場合など）かキーボードで続ける。
        _relay?.SetLocalDevice(null);

        // 自分が設定した入力元がまだ使われているときだけシミュレータへ戻す(ウィンドウ等が別の入力元に切り替えていたら触らない)。
        if (_composite != null && ReferenceEquals(MS2026.GripInputBridge.GripInputBridge.CurrentTransport, _composite))
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

        // 放送を先に止める（放送のスレッドが、閉じた後のポートを読まないように）。
        _relay?.SetLocalDevice(null);
        StopRelay();
        if (_transport == null)
        {
            return;
        }

        // 自分が設定した入力元がまだ使われているときだけシミュレータへ戻す(ウィンドウ等が別の入力元に切り替えていたら触らない)。
        if (_composite != null && ReferenceEquals(MS2026.GripInputBridge.GripInputBridge.CurrentTransport, _composite))
        {
            MS2026.GripInputBridge.GripInputBridge.SetTransport(MS2026.GripInputBridge.GripInputBridge.Simulator);
        }

        // COMポートを掴んだままだと次のPlayで開けなくなるため、必ず解放する。
        _transport.Dispose();
        _transport = null;
        _composite = null;
    }

    /// <summary>マイコンから届いた値(0-1、センサー特性の変換前)。実機を使っていなければ入力元の値。</summary>
    public float GetReceivedValue(int playerIndex)
    {
        return _transport != null
            ? _transport.GetReceivedValue(playerIndex)
            : MS2026.GripInputBridge.GripInputBridge.Provider.GetRawValue(playerIndex);
    }

    /// <summary>実機からこのプレイヤーの値を一度でも受け取ったか（生の値の表示用）。</summary>
    public bool HasReceivedDataFor(int playerIndex)
    {
        return _transport != null && _transport.HasReceivedDataFor(playerIndex);
    }

    /// <summary>使っているポートの名前と、届いた行そのものなどの受信の様子（生の値の表示用）。実機を使っていなければ false。</summary>
    public bool TryGetSerialDiagnostics(out string usedPortName, out GripSerialDiagnostics diagnostics)
    {
        if (_transport == null)
        {
            usedPortName = null;
            diagnostics = default;
            return false;
        }

        usedPortName = _transport.PortName;
        diagnostics = _transport.GetDiagnostics();
        return true;
    }

    /// <summary>実機の読み取りの今の状態（生の値の表示用の短い文）。</summary>
    public string DeviceStatusText
    {
        get
        {
            if (_transport != null)
            {
                return _transport.HasReceivedData ? "受信中" : "ポートは開けたが、値はまだ届いていない";
            }

            if (_search != null)
            {
                return $"ポートをさがしています（{_search.DescribePorts()}）";
            }

            if (_retryAt >= 0f)
            {
                return $"値が届くポートが無い。{Mathf.Max(0f, _retryAt - Time.unscaledTime):0.0}秒後にさがし直す";
            }

            return "ポートを開けていない（キーボードで動いています）";
        }
    }

    /// <summary>使っている通信速度(bps)。</summary>
    public int BaudRateInUse => _config != null ? _config.baudRate : baudRate;

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

        StopRelay();
    }

    private void Start()
    {
        if (DeferToPersistent())
        {
            return;
        }

        SetUpRawValueOverlay();

        // 実機通信が無効でも、LAN の放送は受け取れる（Arduino はほかのPCにある）。
        StartRelay();
        Debug.LogWarning(
            "[ArduinoSerialReader] 実機通信が無効のため、キーボードシミュレータのままです。\n" +
            "1. Edit > Project Settings > Player > Other Settings > Api Compatibility Level を「.NET Framework」に変更\n" +
            "2. 同じ画面の Scripting Define Symbols に「MS2026_GRIP_SERIAL_ENABLED」を追加");
    }

    public float GetReceivedValue(int playerIndex)
    {
        return MS2026.GripInputBridge.GripInputBridge.Provider.GetRawValue(playerIndex);
    }

    public bool HasReceivedDataFor(int playerIndex)
    {
        return false;
    }

    public bool TryGetSerialDiagnostics(out string usedPortName, out GripSerialDiagnostics diagnostics)
    {
        usedPortName = null;
        diagnostics = default;
        return false;
    }

    public string DeviceStatusText => "実機通信が無効（Scripting Define Symbols に MS2026_GRIP_SERIAL_ENABLED が無い）";

    public int BaudRateInUse => baudRate;
#endif
}
