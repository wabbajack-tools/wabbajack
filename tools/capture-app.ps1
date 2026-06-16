param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string]$Out,
    [int]$WaitSeconds = 12
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinApi {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$proc = Start-Process -FilePath $Exe -PassThru
Write-Output "Launched PID $($proc.Id), waiting for window..."

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
    if ($proc.HasExited) { Write-Output "Process exited early (code $($proc.ExitCode))"; break }
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $proc.MainWindowHandle; break }
}

if ($hwnd -eq [IntPtr]::Zero) {
    Write-Output "No window handle obtained."
    try { $proc.Kill() } catch {}
    exit 1
}

# Let the UI settle/render
Start-Sleep -Seconds $WaitSeconds
[WinApi]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$r = New-Object WinApi+RECT
[WinApi]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
Write-Output "Window rect: ${w}x${h} at ($($r.Left),$($r.Top))"

if ($w -le 0 -or $h -le 0) { Write-Output "Bad window size."; try { $proc.Kill() } catch {}; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "Saved $Out"

try { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 800; if (-not $proc.HasExited) { $proc.Kill() } } catch {}
Write-Output "Done."
