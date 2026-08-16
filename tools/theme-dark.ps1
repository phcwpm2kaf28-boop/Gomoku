Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal4 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$T = [string][char]0x4E3B + [string][char]0x9898
$Q = [string][char]0x6D45 + [string][char]0x8272
$S = [string][char]0x6DF1 + [string][char]0x8272

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
[Cal4]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400

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
function Phys-Click([int]$x, [int]$y) {
    [Cal4]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 120
    [Cal4]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Cal4]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}
function Find-Rect([string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    if ($null -eq $el) { return $null }
    return $el.Current.BoundingRectangle
}

# 打开主题菜单（最多重试 3 次，确保打开）
$rectT = Find-Rect $T
if ($null -eq $rectT) { Write-Host 'THEME BTN MISSING'; exit 1 }
for ($i = 0; $i -lt 3; $i++) {
    Phys-Click ([int]($rectT.X + $rectT.Width / 2)) ([int]($rectT.Y + $rectT.Height / 2))
    Start-Sleep -Milliseconds 800
    $rs = Find-Rect $S
    if ($null -ne $rs) { Write-Host ("DARK ITEM @ " + [int]$rs.X + "," + [int]$rs.Y + " " + [int]$rs.Width + "x" + [int]$rs.Height); break }
    Write-Host ("try " + ($i + 1) + ": flyout not open, toggle again")
}
if ($null -eq $rs) { Write-Host 'DARK ITEM STILL MISSING'; exit 1 }

Phys-Click ([int]($rs.X + $rs.Width / 2)) ([int]($rs.Y + $rs.Height / 2))
Start-Sleep -Milliseconds 1500
$b = Find-Wood
if ($null -eq $b) { Write-Host 'WOOD NOT FOUND after dark'; exit 1 }
Write-Host ("AFTER DARK CLICK: #" + $b.R.ToString('X2') + $b.G.ToString('X2') + $b.B.ToString('X2') + "  " + $(if ($b.Dark) { 'DARK' } else { 'LIGHT' }))
Write-Host 'THEME-DARK DONE'
