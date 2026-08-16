# 验证：棋盘定位 + 点击落子 + 主题切换像素级验证
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

$r = New-Object Win32+RECT
[Win32]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null

function Sample-Color([int]$x, [int]$y) {
    $bmp = New-Object System.Drawing.Bitmap(1, 1)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size(1, 1)))
    $c = $bmp.GetPixel(0, 0)
    $g.Dispose(); $bmp.Dispose()
    return $c
}

# 在窗口内扫描，找到木板色区域（浅色 #F0DFBC=(240,223,188) / 暗色 #352B1C=(53,43,28)）
function Find-Board() {
    $step = 30
    for ($y = $r.T + 90; $y -lt $r.B - 60; $y += $step) {
        for ($x = $r.L + 90; $x -lt $r.R - 90; $x += $step) {
            $c = Sample-Color $x $y
            $light = ([Math]::Abs([int]$c.R - 240) -lt 18 -and [Math]::Abs([int]$c.G - 223) -lt 18 -and [Math]::Abs([int]$c.B - 188) -lt 18)
            $dark  = ([Math]::Abs([int]$c.R - 53) -lt 20 -and [Math]::Abs([int]$c.G - 43) -lt 20 -and [Math]::Abs([int]$c.B - 28) -lt 20)
            if ($light -or $dark) { return [PSCustomObject]@{ X = $x; Y = $y; R = $c.R; G = $c.G; B = $c.B } }
        }
    }
    return $null
}

$board = Find-Board
if ($null -eq $board) { Write-Host 'BOARD NOT FOUND (scan)'; exit 1 }
Write-Host ("BOARD at " + $board.X + "," + $board.Y + " color #" + $board.R.ToString('X2') + $board.G.ToString('X2') + $board.B.ToString('X2'))

# 点击棋盘（落子）
[Win32]::SetCursorPos($board.X, $board.Y) | Out-Null
[Win32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[Win32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900

# 状态栏应出现手数
$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$moveSeen = $false
foreach ($el in $all) {
    $n = $el.Current.Name
    if ($n -match '第 .* 手') { Write-Host ("STATUS: " + $n); $moveSeen = $true }
}
Write-Host ("MOVE PLACED: " + $(if ($moveSeen) { 'YES' } else { 'NO' }))

# 主题切换 T -> 棋盘应变色
[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1200
$board2 = Find-Board
if ($null -ne $board2) {
    Write-Host ("BOARD after T:  #" + $board2.R.ToString('X2') + $board2.G.ToString('X2') + $board2.B.ToString('X2'))
    $diff = [Math]::Abs([int]$board.R - [int]$board2.R) + [Math]::Abs([int]$board.G - [int]$board2.G) + [Math]::Abs([int]$board.B - [int]$board2.B)
    Write-Host ("COLOR DIFF: " + $diff + $(if ($diff -gt 80) { ' -> THEME CHANGED OK' } else { ' -> no visible change?' }))
} else { Write-Host 'BOARD NOT FOUND after T' }

# 再切回
[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 800
Write-Host 'VERIFY DONE'
