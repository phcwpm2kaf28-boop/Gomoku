Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$D = [string][char]0x7b2c
$S = [string][char]0x624b

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Sample-Color([int]$x, [int]$y) {
    $bmp = New-Object System.Drawing.Bitmap(1, 1)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size(1, 1)))
    $c = $bmp.GetPixel(0, 0)
    $g.Dispose(); $bmp.Dispose()
    return $c
}

$lw = $w.Current.BoundingRectangle
Write-Host ("LOGIC WIN: " + [int]$lw.X + "," + [int]$lw.Y + " " + [int]$lw.Width + "x" + [int]$lw.Height)

# 在 UIA 窗口矩形内扫描木板色（浅/深两套），找到棋盘物理范围
$minX = 99999; $minY = 99999; $maxX = -1; $maxY = -1; $found = 0; $foundColor = ''
$step = 20
for ($y = [int]$lw.Y + 60; $y -lt [int]($lw.Y + $lw.Height) - 40; $y += $step) {
    for ($x = [int]$lw.X + 60; $x -lt [int]($lw.X + $lw.Width) - 60; $x += $step) {
        $c = Sample-Color $x $y
        $light = ([Math]::Abs([int]$c.R - 240) -lt 18 -and [Math]::Abs([int]$c.G - 223) -lt 18 -and [Math]::Abs([int]$c.B - 188) -lt 18)
        $dark  = ([Math]::Abs([int]$c.R - 53) -lt 20 -and [Math]::Abs([int]$c.G - 43) -lt 20 -and [Math]::Abs([int]$c.B - 28) -lt 20)
        if ($light -or $dark) {
            $found++
            $foundColor = $(if ($light) { 'LIGHT' } else { 'DARK' })
            if ($x -lt $minX) { $minX = $x }; if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
if ($found -eq 0) { Write-Host 'BOARD WOOD NOT FOUND'; exit 1 }
Write-Host ("BOARD PHYS: " + $minX + "," + $minY + " - " + $maxX + "," + $maxY + "  n=" + $found + "  color=" + $foundColor)
$bx = [int](($minX + $maxX) / 2); $by = [int](($minY + $maxY) / 2)

[Cal]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 500
[Cal]::SetCursorPos($bx, $by) | Out-Null
Start-Sleep -Milliseconds 150
[Cal]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[Cal]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900

$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$seen = $false
foreach ($el in $all) {
    if ($el.Current.Name -like "*$D*$S*") { Write-Host ("STATUS: " + $el.Current.Name); $seen = $true }
}
Write-Host ("MOVE-PLACED: " + $(if ($seen) { 'YES' } else { 'NO' }))
Write-Host 'DONE'
