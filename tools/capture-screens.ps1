param(
    [Parameter(Mandatory=$true)][string]$Exe,
    [Parameter(Mandatory=$true)][string]$Prefix,   # e.g. tools\shots\avalonia
    [int]$Settle = 9
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, int e);
    [StructLayout(LayoutKind.Sequential)] public struct R { public int Left, Top, Right, Bottom; }
    public const uint DOWN = 0x0002, UP = 0x0004;
}
"@

function Shot($hwnd, $path) {
    [W]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 400
    $r = New-Object W+R; [W]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
    Write-Output "Saved $path (${w}x${h})"
    return $r
}

function ClickRel($r, $fx, $fy) {
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $x = [int]($r.Left + $w * $fx); $y = [int]($r.Top + $h * $fy)
    [W]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 200
    [W]::mouse_event([W]::DOWN, 0,0,0,0); Start-Sleep -Milliseconds 80; [W]::mouse_event([W]::UP, 0,0,0,0)
    Start-Sleep -Milliseconds 1200
}

$proc = Start-Process -FilePath $Exe -PassThru
$hwnd = [IntPtr]::Zero
for ($i=0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; $proc.Refresh(); if ($proc.HasExited) { Write-Output "exited $($proc.ExitCode)"; exit 1 }; if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $proc.MainWindowHandle; break } }
if ($hwnd -eq [IntPtr]::Zero) { Write-Output "no window"; try { $proc.Kill() } catch {}; exit 1 }
Start-Sleep -Seconds $Settle

# Nav sidebar buttons are ~ x fraction 0.045; y fractions by row.
$r = Shot $hwnd "${Prefix}-home.png"
ClickRel $r 0.045 0.215; Shot $hwnd "${Prefix}-gallery.png" | Out-Null
ClickRel $r 0.045 0.34;  Shot $hwnd "${Prefix}-compiler.png" | Out-Null
ClickRel $r 0.045 0.89;  Shot $hwnd "${Prefix}-settings.png" | Out-Null

try { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 800; if (-not $proc.HasExited) { $proc.Kill() } } catch {}
Write-Output "Done."
