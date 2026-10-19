# Prints the primary monitor's real resolution as "<width> <height>" (used by run_instance.bat for fullscreen runs).
# The process is made DPI aware first, so 125%/150% display scaling does not shrink the numbers.
# Falls back to the first video controller's current resolution. Prints nothing if both fail.
try {
    Add-Type -Namespace MS2026 -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
    [MS2026.Dpi]::SetProcessDPIAware() | Out-Null
    Add-Type -AssemblyName System.Windows.Forms
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    if ($bounds.Width -gt 0 -and $bounds.Height -gt 0) {
        Write-Output "$($bounds.Width) $($bounds.Height)"
        exit 0
    }
} catch {
}

try {
    $video = Get-CimInstance Win32_VideoController | Where-Object { $_.CurrentHorizontalResolution -gt 0 } | Select-Object -First 1
    if ($video) {
        Write-Output "$($video.CurrentHorizontalResolution) $($video.CurrentVerticalResolution)"
    }
} catch {
}
