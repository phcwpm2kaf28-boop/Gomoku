# 物理像素定位棋盘（扫描木板色）-> 点击中心 -> 验证状态栏手数
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32c2 {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
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

# 屏幕坐标探测：先在 UIA 窗口矩形附近采样找木板色
$winR = $w.Current.BoundingRectangle
Write-Host ("UIA WIN: " + [int]$winR.X + "," + [int]$winR.Y + " " + [int]$winR.Width + "x" + [int]$winR.Height)

# 扫描 UIA 窗口矩形放大 1.5 倍区域（覆盖坐标缩放差异）
$xs = [int]($winR.X * 0.6); $ys = [int]($winR.Y * 0.6)
$xw = [int](($winR.X + $winR.Width) * 1.4); $yw = [int](($winR.Y + $winR.Height) * 1.4)
$step = 24
$minX = 99999; $minY = 99999; $maxX = -1; $maxY = -1; $found = 0
for ($y = $ys; $y -lt $yw; $y += $step) {
    for ($x = $xs; $x -lt $xw; $x += $step) {
        $c = Sample-Color $x $y
        if ([Math]::Abs([int]$c.R - 240) -lt 18 -and [Math]::Abs([int]$c.G - 223) -lt 18 -and [Math]::Abs([int]$c.B - 188) -lt 18) {
            $found++
            if ($x -lt $minX) { $minX = $x }; if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
if ($found -eq 0) { Write-Host 'BOARD WOOD NOT FOUND IN SCAN'; exit 1 }
$bx = [int](($minX + $maxX) / 2); $by = [int](($minY + $maxY) / 2)
Write-Host ("BOARD RECT: " + $minX + "," + $minY + " - " + $maxX + "," + $maxY + "  found=" + $found)

# 点击棋盘中心
[Win32c2]::SetCursorPos($bx, $by) | Out-Null
Start-Sleep -Milliseconds 150
$cp = New-Object Win32c2+POINT
[Win32c2]::GetCursorPos([ref]$cp) | Out-Null
Write-Host ("CURSOR NOW: " + $cp.X + "," + $cp.Y)
[Win32c2]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[Win32c2]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 800

$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$seen = $false
foreach ($el in $all) {
    if ($el.Current.Name -like "*$D*$S*") { Write-Host ("STATUS: " + $el.Current.Name); $seen = $true }
}
Write-Host ("MOVE-PLACED: " + $(if ($seen) { 'YES' } else { 'NO' }))
Write-Host 'DONE'
