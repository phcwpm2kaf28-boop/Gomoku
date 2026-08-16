Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal5 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$T = [string][char]0x4E3B + [string][char]0x9898   # 主题
$Q = [string][char]0x6D45 + [string][char]0x8272   # 浅色
$S = [string][char]0x6DF1 + [string][char]0x8272   # 深色

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
[Cal5]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
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
function Find-Elem([string]$needle) {
    $all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                      [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) {
        if ($el.Current.Name -eq $needle) { return $el }
    }
    return $null
}
function Phys-Click-Elem($el) {
    $r = $el.Current.BoundingRectangle
    $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
    [Cal5]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Cal5]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Cal5]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host ("CLICKED @ " + $x + "," + $y)
}

$b1 = Find-Wood
if ($null -eq $b1) { Write-Host 'WOOD NOT FOUND'; exit 1 }
Write-Host ("BEFORE: #" + $b1.R.ToString('X2') + $b1.G.ToString('X2') + $b1.B.ToString('X2') + "  " + $(if ($b1.Dark) { 'DARK' } else { 'LIGHT' }))

# 打开主题菜单
$btn = Find-Elem $T
if ($null -eq $btn) { Write-Host 'THEME BTN MISSING'; exit 1 }
Phys-Click-Elem $btn
Start-Sleep -Milliseconds 900
$sItem = Find-Elem $S
if ($null -eq $sItem) { Write-Host 'DARK ITEM MISSING'; exit 1 }
Write-Host ("DARK ITEM: " + $sItem.Current.ControlType.ProgrammaticName)
Phys-Click-Elem $sItem
Start-Sleep -Milliseconds 1500

$b2 = Find-Wood
if ($null -eq $b2) { Write-Host 'WOOD NOT FOUND after'; exit 1 }
Write-Host ("AFTER: #" + $b2.R.ToString('X2') + $b2.G.ToString('X2') + $b2.B.ToString('X2') + "  " + $(if ($b2.Dark) { 'DARK' } else { 'LIGHT' }))
Write-Host ("CHANGED: " + $(if ($b1.Dark -ne $b2.Dark) { 'YES' } else { 'NO' }))
Write-Host 'THEME-DARK2 DONE'
