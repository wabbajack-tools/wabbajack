param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string]$Out
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinApi {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
    public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$proc = Start-Process -FilePath $Exe -PassThru
Write-Output "Launched PID $($proc.Id)"

$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
    if ($proc.HasExited) { Write-Output "Exited early ($($proc.ExitCode))"; break }
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "No window"; try { $proc.Kill() } catch {}; exit 1 }

# Let Home settle
Start-Sleep -Seconds 8
[WinApi]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$r = New-Object WinApi+RECT
[WinApi]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
Write-Output "Window ${w}x${h} at ($($r.Left),$($r.Top))"

# Click "Browse lists" nav button: ~5% across, ~27% down the window
$cx = $r.Left + [int]($w * 0.05)
$cy = $r.Top + [int]($h * 0.27)
Write-Output "Clicking Browse lists at ($cx,$cy)"
[WinApi]::SetCursorPos($cx, $cy) | Out-Null
Start-Sleep -Milliseconds 300
[WinApi]::mouse_event([WinApi]::LEFTDOWN, 0, 0, 0, 0)
Start-Sleep -Milliseconds 80
[WinApi]::mouse_event([WinApi]::LEFTUP, 0, 0, 0, 0)

# Wait for gallery to load modlists from network
Start-Sleep -Seconds 14

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "Saved $Out"
try { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 800; if (-not $proc.HasExited) { $proc.Kill() } } catch {}
