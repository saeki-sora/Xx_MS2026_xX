# 握力コントローラー（Arduino）が反応しないときの診断。何も変更しない（見るだけ・ポートを数秒開いて読むだけ）。
# 調べること:
#   1. Arduino らしい USB 機器が PC に見えているか・ドライバが入っているか
#   2. COM ポートの一覧と、それぞれ何の機器か（Bluetooth など）
#   3. ポートを横取りしそうなソフト（Arduino IDE・Cura・ゲーム自体 など）が動いていないか
#   4. 実際にポートを開いて、Arduino から届く行を読む（ゲームと同じ 9600bps。DTR なし→あり、だめなら 115200 も試す）
#   5. ゲーム（MS2026.exe）の置き場所・ビルドの中身・前回のログ
#   6. （展示で -grip-sensor を使うときの参考）ネットワークの種類・ファイアウォール
# 結果はデスクトップに「MS2026_握力診断_日時.txt」として保存する（困ったらこのファイルを送ってもらう）。
#
# 使い方: check_grip_sensor.bat をダブルクリック（MS2026.exe と同じフォルダに置くか、MS2026.exe を .bat の上にドラッグ）。
param(
    [string]$ExePath = "",
    [int]$BaudRate = 9600,
    [int]$ReadSeconds = 5
)

$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$report = New-Object System.Collections.Generic.List[string]
$problems = New-Object System.Collections.Generic.List[string]

function Say([string]$text = "", [string]$color = "") {
    $report.Add($text)
    if ($color) { Write-Host $text -ForegroundColor $color } else { Write-Host $text }
}
function Head([string]$text) { Say ""; Say "■ $text" "Cyan" }
function Ok([string]$text) { Say "  [OK] $text" "Green" }
function Ng([string]$text) { Say "  [NG] $text" "Red" }
function Warn([string]$text) { Say "  [注意] $text" "Yellow" }
function Info([string]$text) { Say "  $text" }
function Problem([string]$text) { $problems.Add($text) }

# 見えない文字（\r など）を見える形にする（ゲームの F8 表示と同じ）。
function Visible([string]$line) {
    if ($null -eq $line) { return "(なし)" }
    $b = New-Object System.Text.StringBuilder
    foreach ($c in $line.ToCharArray()) {
        if ($b.Length -ge 60) { [void]$b.Append("…"); break }
        switch ([int]$c) {
            13 { [void]$b.Append("\r") }
            10 { [void]$b.Append("\n") }
            9 { [void]$b.Append("\t") }
            default {
                if ([int]$c -lt 32 -or [int]$c -eq 127) { [void]$b.Append(("\x{0:X2}" -f [int]$c)) } else { [void]$b.Append($c) }
            }
        }
    }
    return $b.ToString()
}

# ゲームが読める1行か（GripSerialProtocol と同じ決まり: 「512」「512,300,100,50」（0〜1023、4つまで）か「G,番号,値,時刻」）。
function Parse-Line([string]$line) {
    if ($null -eq $line) { return $null }
    $t = $line.Trim()
    $parts = $t.Split(',')
    if ($parts.Count -eq 4 -and $parts[0] -eq "G") {
        $p = 0; $v = 0; $ms = [long]0
        if ([int]::TryParse($parts[1], [ref]$p) -and $p -ge 0 -and $p -le 3 -and [int]::TryParse($parts[2], [ref]$v) -and $v -ge 0 -and $v -le 4095 -and [long]::TryParse($parts[3], [ref]$ms)) {
            return ,@(@{ Player = $p; Value = [math]::Round($v / 4095 * 1023) })
        }
        return $null
    }
    if ($parts.Count -lt 1 -or $parts.Count -gt 4) { return $null }
    $values = @()
    for ($i = 0; $i -lt $parts.Count; $i++) {
        $v = 0
        if (-not [int]::TryParse($parts[$i].Trim(), [ref]$v) -or $v -lt 0 -or $v -gt 1023) { return $null }
        $values += @{ Player = $i; Value = $v }
    }
    return ,$values
}

# ポートを開いて数秒読む。
function Read-Port([string]$port, [int]$baud, [bool]$dtr, [int]$seconds) {
    $result = [pscustomobject]@{ Opened = $false; Error = $null; Denied = $false; Lines = (New-Object System.Collections.Generic.List[string]) }
    $sp = New-Object System.IO.Ports.SerialPort $port, $baud
    $sp.ReadTimeout = 300
    $sp.NewLine = "`n"
    $sp.DtrEnable = $dtr
    try {
        $sp.Open()
        $result.Opened = $true
    }
    catch {
        $base = $_.Exception.GetBaseException()
        $result.Error = $base.Message
        $result.Denied = $base -is [System.UnauthorizedAccessException]
        $sp.Dispose()
        return $result
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        while ($sw.Elapsed.TotalSeconds -lt $seconds) {
            try {
                $line = $sp.ReadLine()
                if ($result.Lines.Count -lt 2000) { $result.Lines.Add($line) }
            }
            catch {
                $base = $_.Exception.GetBaseException()
                if (-not ($base -is [System.TimeoutException])) {
                    $result.Error = $base.Message
                    break
                }
            }
        }
    }
    finally {
        try { $sp.Close() } catch { }
        $sp.Dispose()
    }
    return $result
}

Say "MS2026 握力コントローラー診断  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  PC: $env:COMPUTERNAME"
Say "（何も変更しません。見るだけです）"

# ───────── 1. USB 機器 ─────────
Head "1. Arduino らしい USB 機器（ドライバが無くても見える）"
$knownVids = [ordered]@{
    "1A86" = "CH340（互換品の Arduino）"; "2341" = "Arduino 純正"; "2A03" = "Arduino 純正（arduino.org）"; "0403" = "FTDI（USB シリアル）";
    "10C4" = "CP210x（USB シリアル）"; "067B" = "PL2303（USB シリアル）"; "1B4F" = "SparkFun"; "239A" = "Adafruit";
    "303A" = "ESP32（Espressif）"; "2E8A" = "Raspberry Pi Pico"
}
$devices = @()
try { $devices = @(Get-CimInstance Win32_PnPEntity -ErrorAction Stop) } catch { Warn "機器の一覧を取れませんでした: $($_.Exception.Message)" }
$usbFound = @()
foreach ($d in $devices) {
    $ids = (@($d.HardwareID) -join " ")
    foreach ($vid in $knownVids.Keys) {
        if ($ids -match "VID_$vid") { $usbFound += [pscustomobject]@{ Device = $d; Kind = $knownVids[$vid] }; break }
    }
}

$driverMissing = $false
if ($usbFound.Count -eq 0) {
    Ng "Arduino らしい USB 機器が見つかりません。"
    Info "  → PC から Arduino がまったく見えていません。次を順に確かめてください:"
    Info "     ・Arduino の電源ランプ（ON / PWR）が点いているか（点いていなければ USB の差し込み・ケーブル・PC の USB 口）"
    Info "     ・USB ケーブルが充電専用でないか（データが流れるケーブルに替える。動いていた PC で使っていたケーブルが確実）"
    Info "     ・USB ハブや延長・変換アダプタを外して、PC 本体の USB 口に直接挿す"
    Problem "PC から Arduino が見えない（ランプ・ケーブル・USB 口を確認）"
}
else {
    foreach ($f in $usbFound) {
        $d = $f.Device
        $code = [int]$d.ConfigManagerErrorCode
        $name = if ($d.Name) { $d.Name } else { "(名前なし)" }
        if ($code -eq 0) {
            Ok "$name  ［$($f.Kind)］"
        }
        elseif ($code -eq 28 -or $code -eq 1 -or $code -eq 18) {
            Ng "$name  ［$($f.Kind)］ … ドライバが入っていません（コード $code）"
            $driverMissing = $true
        }
        elseif ($code -eq 22) {
            Ng "$name  ［$($f.Kind)］ … デバイスマネージャーで「無効」にされています（右クリック →「デバイスを有効にする」）"
            Problem "Arduino が無効になっている（デバイスマネージャーで有効にする）"
        }
        else {
            Ng "$name  ［$($f.Kind)］ … 問題あり（コード $code。ドライバの入れ直しか、USB の挿し直し）"
            Problem "Arduino の機器に問題（コード $code）"
        }
    }
    if ($driverMissing) {
        Info "  → ドライバを入れてください（CH340 なら WCH 公式の CH341SER.EXE、純正なら Arduino IDE のインストール）。入れたら USB を挿し直す。"
        Problem "Arduino のドライバが入っていない"
    }
}

# ───────── 2. COM ポート ─────────
Head "2. COM ポートの一覧"
$portNames = @()
try { $portNames = @([System.IO.Ports.SerialPort]::GetPortNames() | ForEach-Object { $_.Trim() } | Sort-Object -Unique) } catch { }
$devicePaths = @{}
try {
    $key = Get-Item 'HKLM:\HARDWARE\DEVICEMAP\SERIALCOMM' -ErrorAction Stop
    foreach ($n in $key.GetValueNames()) { $devicePaths[[string]$key.GetValue($n)] = $n }
}
catch { }
$friendly = @{}
foreach ($d in $devices) {
    if ($d.Name -match '\((COM\d+)\)') { $friendly[$Matches[1]] = $d.Name }
}

$readCandidates = @()
if ($portNames.Count -eq 0) {
    Ng "COM ポートが1つもありません。"
}
foreach ($p in $portNames) {
    $path = $devicePaths[$p]
    $label = if ($friendly[$p]) { $friendly[$p] } else { "(名前不明)" }
    $isBluetooth = $path -and $path -match 'BthModem'
    $isOnboard = $path -and $path -match '^\\Device\\Serial'
    if ($isBluetooth) { Info "$p  $label  … Bluetooth（Arduino ではない）" }
    elseif ($isOnboard) { Info "$p  $label  … PC 本体のシリアル（Arduino ではない）" }
    else { Info "$p  $label  $path"; $readCandidates += $p }
}
if ($portNames.Count -gt 0 -and $readCandidates.Count -eq 0) {
    Ng "Arduino かもしれない COM ポートがありません（Bluetooth などだけ）。"
    if (-not $driverMissing -and $usbFound.Count -gt 0) { Problem "Arduino は見えているが COM ポートになっていない（ドライバの入れ直し）" }
}

# ───────── 3. ポートを横取りしそうなソフト ─────────
Head "3. ポートを使いそうなソフト"
$grabbers = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -match '^(Arduino IDE|arduino|arduino-cli|serial-monitor|serial-discovery|UltiMaker-Cura|Cura|putty|ttermpro|CoolTerm|processing|java)$'
    })
$games = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^MS2026' })
if ($grabbers.Count -eq 0) { Ok "ポートを横取りしそうなソフトは動いていません。" }
foreach ($g in $grabbers) {
    Warn "$($g.ProcessName) が動いています。シリアルモニタ／プロッタや 3D プリンタのソフトはポートを掴むので、閉じてからゲームを起動してください。"
}
if ($games.Count -gt 1) {
    Warn "ゲームが $($games.Count) 個起動しています。同じ Arduino を読めるのは1つだけです。"
    Problem "ゲームを2つ以上起動している"
}

# ───────── 4. 実際に読む ─────────
Head "4. ポートを開いて、Arduino から届く行を読む（$BaudRate bps）"
if ($games.Count -gt 0) {
    Warn "ゲーム（MS2026）が起動中です。ゲームがポートを使っていると、ここでは読めません。"
    $answer = Read-Host "  ゲームを閉じてから Enter を押してください（閉じずに続けるなら s を入力して Enter）"
    if ($answer -ne "s") { $games = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^MS2026' }) }
}
if ($readCandidates.Count -eq 0) {
    Info "読むポートがありません（上の 1・2 を先に直してください）。"
}
else {
    Info "読んでいる間（1ポート最大で約15秒）に、1本目のセンサーを何回か握ったり離したりしてください。"
}

foreach ($p in $readCandidates) {
    Say ""
    Say "  ── $p ──"
    $attempts = @(
        @{ Baud = $BaudRate; Dtr = $false; Label = "$BaudRate bps・DTR なし（いま配っているビルドと同じ読み方）" },
        @{ Baud = $BaudRate; Dtr = $true; Label = "$BaudRate bps・DTR あり（新しいビルドの読み方）" }
    )
    $success = $null
    $anyLines = $false
    foreach ($a in $attempts) {
        $r = Read-Port $p $a.Baud $a.Dtr $ReadSeconds
        if (-not $r.Opened) {
            if ($r.Denied) {
                Ng "開けません: ほかのソフトが使用中（Arduino IDE のシリアルモニタ・ゲーム など）。"
                Problem "$p をほかのソフトが使っている"
            }
            else {
                Ng "開けません: $($r.Error)"
                Problem "$p を開けない（$($r.Error)）"
            }
            break
        }
        $readable = @($r.Lines | Where-Object { $null -ne (Parse-Line $_) })
        Info ("{0}: {1} 行届いた（読める行 {2}）" -f $a.Label, $r.Lines.Count, $readable.Count)
        if ($r.Lines.Count -gt 0) { $anyLines = $true }
        if ($readable.Count -gt 0) { $success = @{ Attempt = $a; Result = $r; Readable = $readable }; break }
        if ($r.Lines.Count -gt 0) {
            Info ("    届いた行の例: " + ((@($r.Lines | Select-Object -Last 3) | ForEach-Object { Visible $_ }) -join "  ｜  "))
        }
    }

    if ($null -eq $success -and $anyLines) {
        # 届くが読めない: 通信速度が違う可能性。115200 も試す。
        $r = Read-Port $p 115200 $true 3
        $readable = @($r.Lines | Where-Object { $null -ne (Parse-Line $_) })
        if ($readable.Count -gt 0) {
            Ng "115200 bps なら読めました。Arduino のスケッチが Serial.begin(115200) になっています。ゲームは $BaudRate bps なので、スケッチを Serial.begin($BaudRate) にして書き込み直してください。"
            Problem "$p の通信速度がゲーム（$BaudRate）と違う（Arduino は 115200）"
        }
        else {
            Ng "行は届きますが、ゲームが読める形ではありません。1行は「512」か「512,300,100,50」（数字だけ・0〜1023・4つまで・最後にカンマを付けない）にしてください。"
            Problem "$p から届く行の形が違う"
        }
        continue
    }

    if ($null -eq $success) {
        Ng "開けましたが、1行も届きません。"
        Info "  → Arduino にスケッチが書き込まれているか（別の PC で動いていたなら入っているはず）、USB の挿し直し、Arduino のリセットボタン。"
        Problem "$p は開けるが何も届かない"
        continue
    }

    $a = $success.Attempt
    $r = $success.Result
    $rate = [math]::Round($r.Lines.Count / [double]$ReadSeconds, 1)
    Ok ("読めました（{0}、1秒に約 {1} 行）。" -f $a.Label, $rate)
    Info ("    届いた行の例: " + ((@($success.Readable | Select-Object -Last 3) | ForEach-Object { Visible $_ }) -join "  ｜  "))
    if ($a.Dtr) {
        Warn "DTR ありでないと届きませんでした（Leonardo・Pro Micro など）。いま配っているビルドでは読めません。DTR を ON にした新しいビルドを使ってください。"
        Problem "$p は DTR ありでないと届かない（新しいビルドが必要）"
    }

    # センサーごとの最小〜最大（握ったときに動くか）。
    $min = @{}; $max = @{}
    foreach ($line in $success.Readable) {
        foreach ($s in (Parse-Line $line)) {
            $i = $s.Player
            if (-not $min.ContainsKey($i) -or $s.Value -lt $min[$i]) { $min[$i] = $s.Value }
            if (-not $max.ContainsKey($i) -or $s.Value -gt $max[$i]) { $max[$i] = $s.Value }
        }
    }
    foreach ($i in ($min.Keys | Sort-Object)) {
        $range = $max[$i] - $min[$i]
        $note = if ($range -lt 30) { "（ほとんど動いていません。握っていなければ正常）" } else { "（動いています）" }
        Info ("    センサー{0}: {1} 〜 {2} {3}" -f ($i + 1), $min[$i], $max[$i], $note)
    }
    Info "    ※ -grip-sensor を付けずに起動すると、ゲームが使うのはセンサー1（行の先頭の値）だけです。"
}

# ───────── 5. ゲーム ─────────
Head "5. ゲーム（MS2026.exe）"
if (-not $ExePath) {
    foreach ($c in @((Join-Path $here "MS2026.exe"), (Join-Path $here "..\..\Builds\TestBuild\MS2026.exe"), (Join-Path $env:USERPROFILE "Downloads\TestBuild\MS2026.exe"))) {
        if (Test-Path -LiteralPath $c) { $ExePath = (Resolve-Path -LiteralPath $c).Path; break }
    }
}
if ($ExePath -and (Test-Path -LiteralPath $ExePath)) {
    $ExePath = (Resolve-Path -LiteralPath $ExePath).Path
    Info "場所: $ExePath"
    if ($ExePath -match '\\AppData\\Local\\Temp\\') {
        Warn "zip の中から直接起動しているようです。zip を「すべて展開」してから、展開したフォルダの MS2026.exe を起動してください。"
        Problem "zip を展開せずに起動している"
    }
    $serialDll = Join-Path (Split-Path -Parent $ExePath) "MS2026_Data\Managed\MS2026.GripInputBridge.SerialTransport.dll"
    if (Test-Path -LiteralPath $serialDll) {
        $text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($serialDll))
        $hasSerial = $text.Contains("SerialGripTransport")
        $isNew = $text.Contains("SerialPortDevicePaths")
        if (-not $hasSerial) {
            Ng "このビルドは実機通信が無効です（Scripting Define Symbols に MS2026_GRIP_SERIAL_ENABLED が無いままビルドされた）。"
            Problem "ビルドの実機通信が無効"
        }
        elseif ($isNew) { Ok "実機通信あり・新しいビルド（DTR ON・F8 にポートごとの様子が出る版）。" }
        else { Ok "実機通信あり（DTR OFF の前の版）。" }
    }
    else {
        Warn "MS2026_Data\Managed\MS2026.GripInputBridge.SerialTransport.dll がありません（フォルダごとコピーされていない？）。"
        Problem "ビルドのファイルが足りない"
    }
}
else {
    Info "MS2026.exe が見つかりませんでした（このファイルを MS2026.exe と同じフォルダに置くか、MS2026.exe を .bat の上にドラッグすると調べます）。"
}

$log = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\MS2026\Player.log"
if (Test-Path -LiteralPath $log) {
    Info "前回のゲームのログ: $log （$((Get-Item -LiteralPath $log).LastWriteTime)）"
    $lines = @(Get-Content -LiteralPath $log -Encoding UTF8 -ErrorAction SilentlyContinue | Where-Object { $_ -match '\[(ArduinoSerialReader|GripInputBridge|GripRelay)\]' } | Select-Object -Last 12)
    if ($lines.Count -eq 0) { Info "  （握力に関する行がありません。別のゲームのログかもしれません）" }
    foreach ($l in $lines) { Info ("  " + $l) }
}
else {
    Info "ゲームのログはまだありません（一度起動すると $log にできます）。"
}

# ───────── 6. LAN（展示で -grip-sensor を使うときだけ関係） ─────────
Head "6. （展示で4台をつなぐときの参考）ネットワーク・ファイアウォール"
try {
    foreach ($n in @(Get-NetConnectionProfile -ErrorAction Stop)) {
        if ($n.NetworkCategory -eq "Public") {
            Warn "ネットワーク「$($n.Name)」が「パブリック」です。4台をつなぐときは「プライベート」にしてください（1台で遊ぶだけなら関係ありません）。"
        }
        else { Ok "ネットワーク「$($n.Name)」: $($n.NetworkCategory)" }
    }
}
catch { Info "ネットワークの種類を取れませんでした。" }
if ($ExePath -and (Test-Path -LiteralPath $ExePath)) {
    try {
        $rules = @(Get-NetFirewallApplicationFilter -Program $ExePath -ErrorAction Stop | Get-NetFirewallRule -ErrorAction Stop | Where-Object { $_.Enabled -eq "True" -and $_.Direction -eq "Inbound" })
        $allow = @($rules | Where-Object { $_.Action -eq "Allow" })
        $block = @($rules | Where-Object { $_.Action -eq "Block" })
        if ($block.Count -gt 0) { Warn "ファイアウォールで MS2026.exe が「ブロック」されています（4台をつなぐときは許可が必要）。" }
        elseif ($allow.Count -gt 0) { Ok "ファイアウォールで MS2026.exe は許可されています。" }
        else { Info "ファイアウォールの規則はまだありません（最初の起動で出る画面で「アクセスを許可する」を押す）。" }
    }
    catch { Info "ファイアウォールの規則を調べられませんでした。" }
}

# ───────── まとめ ─────────
Head "まとめ"
if ($problems.Count -eq 0) {
    Ok "見つかった問題はありません。ゲームを起動して F8 を押し、状態が「受信中」になるか確かめてください。"
}
else {
    $i = 1
    foreach ($p in $problems) { Ng ("{0}. {1}" -f $i, $p); $i++ }
    Info "上から順に直してください（1番目が直ると、あとの問題が消えることがあります）。"
}

$out = Join-Path ([Environment]::GetFolderPath("Desktop")) ("MS2026_握力診断_{0}.txt" -f (Get-Date -Format "yyyyMMdd_HHmm"))
try {
    [System.IO.File]::WriteAllLines($out, $report, (New-Object System.Text.UTF8Encoding $true))
    Say ""
    Say "結果を保存しました: $out" "Cyan"
}
catch { Say "結果を保存できませんでした: $($_.Exception.Message)" "Yellow" }
