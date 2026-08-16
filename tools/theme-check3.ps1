Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cal3 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$T = [string][char]0x4E3B + [string][char]0x9898    # 主题
$Q = [string][char]0x6D45 + [string][char]0x8272    # 浅色
$S = [string][char]0x6DF1 + [string][char]0x8272    # 深色

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
[Cal3]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
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
function Click-ByName([string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    if ($null -eq $el) { Write-Host ("MISSING: " + $name); return $false }
    $r = $el.Current.BoundingRectangle
    $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
    [Cal3]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 120
    [Cal3]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Cal3]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host ("CLICKED(phys): " + $name + "  @ " + $x + "," + $y)
    return $true
}

$b1 = Find-Wood
if ($null -eq $b1) { Write-Host 'WOOD NOT FOUND'; exit 1 }
Write-Host ("BEFORE: #" + $b1.R.ToString('X2') + $b1.G.ToString('X2') + $b1.B.ToString('X2') + "  " + $(if ($b1.Dark) { 'DARK' } else { 'LIGHT' }))

Click-ByName $T
Start-Sleep -Milliseconds 800
Click-ByName $S          # 选深色（当前是浅色，应变深）
Start-Sleep -Milliseconds 1400
$b2 = Find-Wood
if ($null -eq $b2) { Write-Host 'WOOD NOT FOUND after dark'; exit 1 }
Write-Host ("AFTER DARK: #" + $b2.R.ToString('X2') + $b2.G.ToString('X2') + $b2.B.ToString('X2') + "  " + $(if ($b2.Dark) { 'DARK' } else { 'LIGHT' }))

Click-ByName $T
Start-Sleep -Milliseconds 800
Click-ByName $Q          # 恢复浅色
Start-Sleep -Milliseconds 1400
$b3 = Find-Wood
if ($null -eq $b3) { Write-Host 'WOOD NOT FOUND after light'; exit 1 }
Write-Host ("AFTER LIGHT: #" + $b3.R.ToString('X2') + $b3.G.ToString('X2') + $b3.B.ToString('X2') + "  " + $(if ($b3.Dark) { 'DARK' } else { 'LIGHT' }))
Write-Host 'THEME CHECK3 DONE'
