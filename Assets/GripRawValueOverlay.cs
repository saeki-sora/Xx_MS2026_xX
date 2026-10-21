using MS2026.GripInputBridge.Transports;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 自作コントローラーから届いた「生の値」を画面の左上に出すデバッグ表示。<see cref="ArduinoSerialReader"/> が自分のGameObjectに付ける
/// （手で付けなくてよい）。この部品が有効な間だけ描く（無効なら OnGUI も呼ばれないので負荷は無い）。
/// 出すもの: ポート・状態、届いた1行そのまま（見えない文字は \r などで表示）、1秒あたりの行数、読めなかった行、
/// プレイヤーごとの「届いた値(0-1023)・センサー変換後(raw)・最終の握力(grip)・接続状態」。
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
    private const float PanelWidth = 760f;
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
        var lineCount = 4 + (hasSerial ? 3 : 0) + (hasSerial && diagnostics.LastRejectedLine != null ? 1 : 0) + MS2026.GripInputBridge.GripInputBridgeConstants.PlayerCount;
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
