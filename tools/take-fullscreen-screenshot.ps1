param(
    [string]$Path = "D:\UI-win\docs\previews\fullscreen-dpi-aware-check.png",
    [ValidateSet("png", "jpg", "jpeg", "bmp")]
    [string]$Format = "png"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class DpiAwarenessBootstrap {
    private static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    public static void Enable() {
        if (!SetProcessDpiAwarenessContext(PerMonitorAwareV2)) {
            SetProcessDPIAware();
        }
    }
}
"@

[DpiAwarenessBootstrap]::Enable()

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$fullPath = [System.IO.Path]::GetFullPath($Path)
$parent = Split-Path -Parent $fullPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$imageFormat = switch ($Format.ToLowerInvariant()) {
    "png" { [System.Drawing.Imaging.ImageFormat]::Png }
    "jpg" { [System.Drawing.Imaging.ImageFormat]::Jpeg }
    "jpeg" { [System.Drawing.Imaging.ImageFormat]::Jpeg }
    "bmp" { [System.Drawing.Imaging.ImageFormat]::Bmp }
}

$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

try {
    $graphics.CopyFromScreen(
        (New-Object System.Drawing.Point($bounds.Left, $bounds.Top)),
        [System.Drawing.Point]::Empty,
        (New-Object System.Drawing.Size($bounds.Width, $bounds.Height)))
    $bitmap.Save($fullPath, $imageFormat)
} finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output $fullPath
Write-Output "Captured physical pixels: $($bounds.Width)x$($bounds.Height)"
