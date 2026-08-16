# click board center -> verify status shows move count
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32c {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
}
"@
$D = [string][char]0x7b2c      # di
$S = [string][char]0x624b      # shou

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Click([int]$x, [int]$y) {
    [Win32c]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 120
    [Win32c]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [Win32c]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$hintRect = $null
foreach ($el in $all) {
    $n = $el.Current.Name
    if ($n -match 'WASD' -and $null -eq $hintRect) { $hintRect = $el.Current.BoundingRectangle }
}
if ($null -eq $hintRect) { Write-Host 'HINT TEXT NOT FOUND'; exit 1 }
Write-Host ("HINT: " + $hintRect.X + "," + $hintRect.Y + " " + $hintRect.Width + "x" + $hintRect.Height)
$cx = [int]($hintRect.X + $hintRect.Width / 2)
$cy = [int]($hintRect.Y - 250)
Click $cx $cy
Write-Host ("CLICKED: " + $cx + "," + $cy)

Start-Sleep -Milliseconds 600
$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$seen = $false
foreach ($el in $all) {
    $n = $el.Current.Name
    if ($n -like "*$D*$S*") { Write-Host ("STATUS: " + $n); $seen = $true }
}
Write-Host ("MOVE-PLACED: " + $(if ($seen) { 'YES' } else { 'NO' }))
Write-Host 'CLICK-MOVE DONE'
