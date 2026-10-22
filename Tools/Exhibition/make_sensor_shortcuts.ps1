# 展示用: デスクトップに「MS2026 センサー1で起動」〜「センサー4で起動」の4つのアイコン（ショートカット）を作る。
# 中身は MS2026.exe -grip-sensor N（N = そのPCの前の人のセンサー番号）。予備のPCに切り替えるときは、
# 壊れたPCと同じ番号のアイコンをダブルクリックするだけ。
#
# 使い方: make_sensor_shortcuts.bat をダブルクリック（MS2026.exe を .bat の上にドラッグしてもよい）。
#   -ExePath  MS2026.exe の場所（省略時: このファイルと同じフォルダ → <プロジェクト>\Builds\TestBuild → ダウンロード\TestBuild の順にさがす）
#   -OutDir   アイコンを作る場所（省略時: デスクトップ）
#   -ExtraArgs 足したい起動オプション（例: "-gripdebug"）
param(
    [string]$ExePath = "",
    [string]$OutDir = "",
    [string]$ExtraArgs = ""
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $ExePath) {
    $candidates = @(
        (Join-Path $here "MS2026.exe"),
        (Join-Path $here "..\..\Builds\TestBuild\MS2026.exe"),
        (Join-Path $env:USERPROFILE "Downloads\TestBuild\MS2026.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            $ExePath = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
    }
}

if (-not $ExePath -or -not (Test-Path -LiteralPath $ExePath)) {
    Write-Host "MS2026.exe が見つかりませんでした。"
    Write-Host "このファイル（make_sensor_shortcuts.bat と .ps1）を MS2026.exe と同じフォルダに置いて実行するか、"
    Write-Host "MS2026.exe を make_sensor_shortcuts.bat の上にドラッグしてください。"
    exit 1
}

$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if (-not $OutDir) {
    $OutDir = [Environment]::GetFolderPath("Desktop")
}

if (-not (Test-Path -LiteralPath $OutDir)) {
    Write-Host "アイコンを作る場所がありません: $OutDir"
    exit 1
}

$shell = New-Object -ComObject WScript.Shell
for ($n = 1; $n -le 4; $n++) {
    $path = Join-Path $OutDir ("MS2026 センサー{0}で起動.lnk" -f $n)
    $shortcut = $shell.CreateShortcut($path)
    $shortcut.TargetPath = $ExePath
    $shortcut.Arguments = ("-grip-sensor {0} {1}" -f $n, $ExtraArgs).Trim()
    $shortcut.WorkingDirectory = Split-Path -Parent $ExePath
    $shortcut.IconLocation = "$ExePath,0"
    $shortcut.Description = "センサー${n}番の席で起動（Arduino の${n}番のセンサーを、このPCの前の人の握力として使う）"
    $shortcut.Save()
    Write-Host ("作りました: {0}  （{1} {2}）" -f $path, $ExePath, $shortcut.Arguments)
}

Write-Host ""
Write-Host "このPCの席の番号のアイコンから起動してください（Arduino の配線の番号 = 席の番号）。"
