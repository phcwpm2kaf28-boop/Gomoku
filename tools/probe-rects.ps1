# 打印窗口内所有非空名元素 + 大矩形元素（定位棋盘实际显示区域）
Add-Type -AssemblyName UIAutomationClient
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32f {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'NO WINDOW'; exit 1 }
$r = New-Object Win32f+RECT
[Win32f]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
Write-Host ("WIN PHY: " + $r.L + "," + $r.T + " " + ($r.R - $r.L) + "x" + ($r.B - $r.T))

$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$idx = 0
foreach ($el in $all) {
    $idx++
    $rct = $el.Current.BoundingRectangle
    $n = $el.Current.Name
    $ct = $el.Current.ControlType.ProgrammaticName.Split('.')[-1]
    $big = ($rct.Width -gt 300 -and $rct.Height -gt 300)
    if ($n -or $big) {
        Write-Host ("  " + $idx + " " + $ct.PadRight(10) + " " +
                    [int]$rct.X + "," + [int]$rct.Y + " " + [int]$rct.Width + "x" + [int]$rct.Height +
                    $(if ($n) { "  [" + $n.Substring(0, [Math]::Min(20, $n.Length)) + "]" } else { "" }))
    }
}
Write-Host 'DONE'
