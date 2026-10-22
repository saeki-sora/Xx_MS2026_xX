using System.Collections.Generic;
using MS2026.GripInputBridge.Relay;
using MS2026.GripInputBridge.Transports;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自作コントローラーから届いた「生の値」を画面の左上に出すデバッグ表示。<see cref="ArduinoSerialReader"/> が自分のGameObjectに付ける
/// （手で付けなくてよい）。この部品が有効な間だけ描く（無効なら OnGUI も呼ばれないので負荷は無い）。
/// 出すもの: ポート・状態、届いた1行そのまま（見えない文字は \r などで表示）、1秒あたりの行数、読めなかった行、
/// プレイヤーごとの「届いた値(0-1023)・センサー変換後(raw)・最終の握力(grip)・接続状態」。
/// LAN で握力を配る使い方（起動オプション -grip-sensor N）のときは、自分のセンサー番号・放送の送り/受けの様子・LAN の4人分の値も出す。
/// </summary>
[AddComponentMenu("")]
public sealed class GripRawValueOverlay : MonoBehaviour
{
    /// <summary>値を読む読み取り（付けた <see cref="ArduinoSerialReader"/>）。</summary>
    public ArduinoSerialReader reader;

    /// <summary>見出しに出す、表示を出し入れするキー。</summary>
    public Key toggleKey = Key.F8;

    // 1080p のときの大きさで組んで、画面の高さに合わせて拡大・縮小する。
    private const float ReferenceHeight = 1080f;
    private const float PanelWidth = 1000f;
    private const float LineHeight = 30f;
    private const float Padding = 12f;
    private const float RateIntervalSeconds = 0.5f;

    // 握力入力ブリッジのウィンドウと同じプレイヤー色。
    private static readonly Color[] PlayerColors =
    {
        new Color32(0xE5, 0x48, 0x4D, 0xFF),
        new Color32(0x34, 0x8C, 0xF2, 0xFF),
        new Color32(0x40, 0xB3, 0x73, 0xFF),
        new Color32(0xF2, 0xCC, 0x27, 0xFF),
    };

    private GUIStyle _label;
    private GUIStyle _title;
    private GUIStyle _mono;

    private string _ratePort;
    private long _rateLines;
    private float _rateAt;
    private float _linesPerSecond;

    // LAN の放送の回数（1秒あたり）。
    private long _relaySent;
    private long _relayAccepted;
    private float _relaySentPerSecond;
    private float _relayAcceptedPerSecond;
    private readonly List<(string text, bool warning)> _relayLines = new List<(string, bool)>();
    private GUIStyle _warning;

    private void OnEnable()
    {
        _ratePort = null;
        _linesPerSecond = 0f;
    }

    private void Update()
    {
        if (reader == null || Time.unscaledTime < _rateAt)
        {
            return;
        }

        // 1秒あたりに届いた行の数（ポートが変わったら数え直す）。
        var elapsed = Time.unscaledTime - _rateAt + RateIntervalSeconds;
        _rateAt = Time.unscaledTime + RateIntervalSeconds;
        UpdateRelayRates(elapsed);
        if (!reader.TryGetSerialDiagnostics(out var port, out var diagnostics))
        {
            _ratePort = null;
            _linesPerSecond = 0f;
            return;
        }

        if (_ratePort == port && elapsed > 0f)
        {
            _linesPerSecond = (diagnostics.LinesReceived - _rateLines) / elapsed;
        }

        _ratePort = port;
        _rateLines = diagnostics.LinesReceived;
    }

    private void OnGUI()
    {
        if (reader == null)
        {
            return;
        }

        EnsureStyles();
        var scale = Mathf.Max(0.5f, Screen.height / ReferenceHeight);
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var hasSerial = reader.TryGetSerialDiagnostics(out var port, out var diagnostics);
        CollectRelayLines();
        var lineCount = 4 + (hasSerial ? 3 : 0) + (hasSerial && diagnostics.LastRejectedLine != null ? 1 : 0) + _relayLines.Count +
                        MS2026.GripInputBridge.GripInputBridgeConstants.PlayerCount;
        var panel = new Rect(Padding, Padding, PanelWidth, Padding * 2f + LineHeight * lineCount);
        DrawRect(panel, new Color(0f, 0f, 0f, 0.75f));

        var y = panel.y + Padding;
        var x = panel.x + Padding;
        var width = panel.width - Padding * 2f;
        GUI.Label(new Rect(x, y, width, LineHeight), $"握力コントローラー（生の値）   {toggleKey} で閉じる", _title);
        y += LineHeight;
        GUI.Label(new Rect(x, y, width, LineHeight), $"状態: {reader.DeviceStatusText}", _label);
        y += LineHeight;

        if (hasSerial)
        {
            GUI.Label(new Rect(x, y, width, LineHeight), $"ポート: {port}  {reader.BaudRateInUse}bps", _label);
            y += LineHeight;
            var since = double.IsInfinity(diagnostics.SecondsSinceLastLine) ? "—" : $"{diagnostics.SecondsSinceLastLine:0.00}秒前";
            GUI.Label(new Rect(x, y, width, LineHeight),
                $"受信: {_linesPerSecond:0.0} 行/秒   最後の行: {since}   届いた行: {diagnostics.LinesReceived}   読めなかった行: {diagnostics.LinesRejected}", _label);
            y += LineHeight;
            GUI.Label(new Rect(x, y, width, LineHeight), $"届いた1行: {GripSerialDiagnostics.ToVisibleText(diagnostics.LastLine)}", _mono);
            y += LineHeight;
            if (diagnostics.LastRejectedLine != null)
            {
                GUI.Label(new Rect(x, y, width, LineHeight), $"読めなかった行: {GripSerialDiagnostics.ToVisibleText(diagnostics.LastRejectedLine)}", _mono);
                y += LineHeight;
            }
        }

        foreach (var (text, warning) in _relayLines)
        {
            GUI.Label(new Rect(x, y, width, LineHeight), text, warning ? _warning : _label);
            y += LineHeight;
        }

        y += LineHeight * 0.25f;
        DrawRow(x, y, "", "届いた値(0-1023)", "raw", "grip", "状態", _label);
        y += LineHeight;

        var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
        for (var i = 0; i < MS2026.GripInputBridge.GripInputBridgeConstants.PlayerCount; i++)
        {
            var received = reader.GetReceivedValue(i);
            var receivedText = hasSerial && !reader.HasReceivedDataFor(i)
                ? "—"
                : Mathf.RoundToInt(received * GripSerialProtocol.SimpleFormatRawValueMax).ToString();

            // 届いた値の大きさを、プレイヤー色の帯で後ろに描く。
            var bar = new Rect(x, y + 3f, width * Mathf.Clamp01(received), LineHeight - 6f);
            var color = PlayerColors[i];
            color.a = 0.35f;
            DrawRect(bar, color);
            // 実機の値を使わないプレイヤー（ArduinoSerialReader の devicePlayerCount より後）は、届いていてもレーザーには使わない。
            var status = provider.GetStatus(i).ToString();
            if (hasSerial && i >= reader.devicePlayerCount)
            {
                status += " 実機OFF";
            }

            DrawRow(x, y, $"P{i + 1}", receivedText, provider.GetRawValue(i).ToString("0.000"), provider.GetGripValue(i).ToString("0.000"),
                status, _mono);
            y += LineHeight;
        }

        GUI.matrix = oldMatrix;
    }

    private void UpdateRelayRates(float elapsed)
    {
        var status = reader.RelayStatus;
        if (status == null || elapsed <= 0f)
        {
            _relaySentPerSecond = 0f;
            _relayAcceptedPerSecond = 0f;
            return;
        }

        var sent = status.Value.PacketsSent;
        var accepted = status.Value.Receiver.PacketsAccepted;
        _relaySentPerSecond = Mathf.Max(0f, (sent - _relaySent) / elapsed);
        _relayAcceptedPerSecond = Mathf.Max(0f, (accepted - _relayAccepted) / elapsed);
        _relaySent = sent;
        _relayAccepted = accepted;
    }

    // LAN で握力を配る仕組みの行（使っていなければ、起動オプションの書き間違いだけ）。
    private void CollectRelayLines()
    {
        _relayLines.Clear();
        var maybeStatus = reader.RelayStatus;
        if (maybeStatus == null)
        {
            if (reader.RelayOptionsError != null)
            {
                _relayLines.Add(("LAN: 起動オプションの間違いで使っていません: " + reader.RelayOptionsError, true));
            }

            return;
        }

        var status = maybeStatus.Value;
        var source = status.Source switch
        {
            GripRelayInputSource.LocalDevice => "このPCの Arduino",
            GripRelayInputSource.Relay => "LAN の放送",
            _ => "なし（キーボードだけ）"
        };
        _relayLines.Add(($"LAN: このPCはセンサー{status.SensorNumber}番（UDP {status.ListenPort}）   握力の出どころ: {source}", false));

        string sending;
        if (status.IsSending)
        {
            sending = (status.HasLocalDevice ? "このPCの Arduino の4人分を放送中" : "キーボードを放送中（テスト用）") + $"  {_relaySentPerSecond:0} 回/秒";
        }
        else
        {
            sending = status.HasLocalDevice ? "放送の準備中" : "放送していない（このPCに Arduino が無い）";
        }

        _relayLines.Add(("放送: " + sending, false));

        var receiver = status.Receiver;
        if (receiver.IsFresh)
        {
            _relayLines.Add(($"受信: {receiver.SenderName}{(receiver.SenderIsTestSender ? "（テスト）" : string.Empty)} から  {_relayAcceptedPerSecond:0} 回/秒  " +
                             $"{receiver.SecondsSinceLastPacket:0.00}秒前", false));
        }
        else if (status.Source != GripRelayInputSource.LocalDevice)
        {
            var since = receiver.HasSender ? $"（{receiver.SenderName} から最後に届いたのは {receiver.SecondsSinceLastPacket:0.0}秒前）" : string.Empty;
            _relayLines.Add(($"受信: 放送が届いていません{since}。Arduino のPC・LAN・ファイアウォールを確認", true));
        }

        if (receiver.HasSender)
        {
            var values = new System.Text.StringBuilder("LAN の4人分（raw）:");
            for (var i = 0; i < receiver.Sensors.Length; i++)
            {
                var sensor = receiver.Sensors[i];
                values.Append(receiver.IsFresh && sensor.Connected ? $"  {i + 1}:{sensor.Raw:0.00}" : $"  {i + 1}:—");
            }

            _relayLines.Add((values.ToString(), false));
        }

        if (status.ListenError != null)
        {
            _relayLines.Add(("受け取れません: " + status.ListenError, true));
        }

        if (receiver.OtherSenderName != null)
        {
            _relayLines.Add(($"放送が2か所から来ています（使っていない方: {receiver.OtherSenderName}）。Arduino のPCは1台だけにする", true));
        }

        if (receiver.WrongVersionPackets > 0)
        {
            _relayLines.Add(("形の違う放送が届いています。全部のPCを同じビルドにする", true));
        }

        if (status.OptionsError != null)
        {
            _relayLines.Add(("起動オプション: " + status.OptionsError, true));
        }
    }

    // 列の位置をそろえて1行描く（プレイヤー名・届いた値・raw・grip・状態）。
    private static void DrawRow(float x, float y, string player, string received, string raw, string grip, string status, GUIStyle style)
    {
        GUI.Label(new Rect(x, y, 50f, LineHeight), player, style);
        GUI.Label(new Rect(x + 50f, y, 200f, LineHeight), received, style);
        GUI.Label(new Rect(x + 260f, y, 110f, LineHeight), raw, style);
        GUI.Label(new Rect(x + 380f, y, 110f, LineHeight), grip, style);
        GUI.Label(new Rect(x + 500f, y, 230f, LineHeight), status, style);
    }

    private void EnsureStyles()
    {
        if (_label != null)
        {
            return;
        }

        _label = new GUIStyle(GUI.skin.label) { fontSize = 20, clipping = TextClipping.Clip };
        _label.normal.textColor = Color.white;
        _title = new GUIStyle(_label) { fontStyle = FontStyle.Bold, fontSize = 22 };
        _warning = new GUIStyle(_label);
        _warning.normal.textColor = new Color(1f, 0.55f, 0.35f);
        _mono = new GUIStyle(_label);
        var monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "MS Gothic", "Courier New" }, 20);
        if (monoFont != null)
        {
            _mono.font = monoFont;
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
