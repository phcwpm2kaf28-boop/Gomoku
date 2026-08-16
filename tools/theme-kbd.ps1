Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal8 {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@
$TOPMOST = [IntPtr]-1
$HWND_NOTOPMOST = [IntPtr]-2
$FLAGS = 0x0040 -bor 0x0001

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
[Cal8]::SetWindowPos($p.MainWindowHandle, $TOPMOST, 0, 0, 0, 0, $FLAGS) | Out-Null
[Cal8]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 700

function Sample-Color([int]$x, [int]$y) {
    $bmp = New-Object System.Drawing.Bitmap(1, 1)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size(1, 1)))
    $c = $bmp.GetPixel(0, 0)
    $g.Dispose(); $bmp.Dispose()
    return $c
}
function Find-Wood() {
    $lw = $w.Current.BoundingRectangle
    for ($y = [int]$lw.Y + 140; $y -lt [int]($lw.Y + $lw.Height) - 90; $y += 22) {
        for ($x = [int]$lw.X + 80; $x -lt [int]($lw.X + $lw.Width) - 80; $x += 22) {
            $c = Sample-Color $x $y
            $light = ([Math]::Abs([int]$c.R - 240) -lt 8 -and [Math]::Abs([int]$c.G - 223) -lt 8 -and [Math]::Abs([int]$c.B - 188) -lt 8)
            $dark  = ([Math]::Abs([int]$c.R - 53) -lt 8 -and [Math]::Abs([int]$c.G - 43) -lt 8 -and [Math]::Abs([int]$c.B - 28) -lt 8)
            if ($light -or $dark) { return [PSCustomObject]@{ X = $x; Y = $y; R = $c.R; G = $c.G; B = $c.B; Dark = $dark } }
        }
    }
    return $null
}

$b1 = Find-Wood
if ($null -eq $b1) { Write-Host 'WOOD NOT FOUND'; exit 1 }
Write-Host ("BEFORE: #" + $b1.R.ToString('X2') + $b1.G.ToString('X2') + $b1.B.ToString('X2') + "  " + $(if ($b1.Dark) { 'DARK' } else { 'LIGHT' }))

[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1800
$b2 = Find-Wood
if ($null -eq $b2) { Write-Host 'WOOD NOT FOUND after T1'; [Cal8]::SetWindowPos($p.MainWindowHandle, $HWND_NOTOPMOST, 0, 0, 0, 0, $FLAGS) | Out-Null; exit 1 }
Write-Host ("AFTER T1: #" + $b2.R.ToString('X2') + $b2.G.ToString('X2') + $b2.B.ToString('X2') + "  " + $(if ($b2.Dark) { 'DARK' } else { 'LIGHT' }))
Write-Host ("T1 CHANGED: " + $(if ($b1.Dark -ne $b2.Dark) { 'YES' } else { 'NO' }))

[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1800
$b3 = Find-Wood
if ($null -eq $b3) { Write-Host 'WOOD NOT FOUND after T2'; [Cal8]::SetWindowPos($p.MainWindowHandle, $HWND_NOTOPMOST, 0, 0, 0, 0, $FLAGS) | Out-Null; exit 1 }
Write-Host ("AFTER T2: #" + $b3.R.ToString('X2') + $b3.G.ToString('X2') + $b3.B.ToString('X2') + "  " + $(if ($b3.Dark) { 'DARK' } else { 'LIGHT' }))

# 标题栏区域采样（顶栏 Mica 背景 + 按钮区）：主题切换应改变整体明暗
$lw = $w.Current.BoundingRectangle
$tb = Sample-Color ([int]($lw.X + 300)) ([int]($lw.Y + 24))
Write-Host ("TITLEBAR SAMPLE: #" + $tb.R.ToString('X2') + $tb.G.ToString('X2') + $tb.B.ToString('X2'))

[Cal8]::SetWindowPos($p.MainWindowHandle, $HWND_NOTOPMOST, 0, 0, 0, 0, $FLAGS) | Out-Null
Write-Host 'THEME-KBD DONE'
