Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal6 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
[Cal6]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500

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

# 点击棋盘空白处获取窗口焦点
$lw = $w.Current.BoundingRectangle
$bx = [int]($lw.X + $lw.Width / 2); $by = [int]($lw.Y + $lw.Height * 0.45)
[Cal6]::SetCursorPos($bx, $by) | Out-Null
Start-Sleep -Milliseconds 150
[Cal6]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[Cal6]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 700

$b1 = Find-Wood
if ($null -eq $b1) { Write-Host 'WOOD NOT FOUND'; exit 1 }
Write-Host ("BEFORE: #" + $b1.R.ToString('X2') + $b1.G.ToString('X2') + $b1.B.ToString('X2') + "  " + $(if ($b1.Dark) { 'DARK' } else { 'LIGHT' }))

# 固定采样点（木板区域中心附近）跟踪变化
$sampleX = $bx; $sampleY = $by
[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1500
$c1 = Sample-Color $sampleX $sampleY
$b2 = Find-Wood
Write-Host ("AFTER T1: sample #" + $c1.R.ToString('X2') + $c1.G.ToString('X2') + $c1.B.ToString('X2') +
            $(if ($b2) { "  board " + $(if ($b2.Dark) { 'DARK' } else { 'LIGHT' }) } else { '  board gone?' }))

[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1500
$b3 = Find-Wood
Write-Host ("AFTER T2: " + $(if ($b3) { $(if ($b3.Dark) { 'DARK' } else { 'LIGHT' }) } else { 'board gone?' }))
Write-Host 'THEME-T DONE'
