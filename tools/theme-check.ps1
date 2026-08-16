# 主题切换像素验证：采样棋盘木板色，T 键切换，确认颜色实际变化
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null

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
function Find-Wood() {
    $lw = $w.Current.BoundingRectangle
    for ($y = [int]$lw.Y + 60; $y -lt [int]($lw.Y + $lw.Height) - 40; $y += 25) {
        for ($x = [int]$lw.X + 60; $x -lt [int]($lw.X + $lw.Width) - 60; $x += 25) {
            $c = Sample-Color $x $y
            $light = ([Math]::Abs([int]$c.R - 240) -lt 18 -and [Math]::Abs([int]$c.G - 223) -lt 18 -and [Math]::Abs([int]$c.B - 188) -lt 18)
            $dark  = ([Math]::Abs([int]$c.R - 53) -lt 20 -and [Math]::Abs([int]$c.G - 43) -lt 20 -and [Math]::Abs([int]$c.B - 28) -lt 20)
            if ($light -or $dark) { return [PSCustomObject]@{ X = $x; Y = $y; R = $c.R; G = $c.G; B = $c.B; Dark = $dark } }
        }
    }
    return $null
}

$b1 = Find-Wood
if ($null -eq $b1) { Write-Host 'WOOD NOT FOUND (start)'; exit 1 }
Write-Host ("BEFORE: #" + $b1.R.ToString('X2') + $b1.G.ToString('X2') + $b1.B.ToString('X2') + "  " + $(if ($b1.Dark) { 'DARK' } else { 'LIGHT' }))

[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 1300
$b2 = Find-Wood
if ($null -eq $b2) { Write-Host 'WOOD NOT FOUND (after T)'; exit 1 }
Write-Host ("AFTER T: #" + $b2.R.ToString('X2') + $b2.G.ToString('X2') + $b2.B.ToString('X2') + "  " + $(if ($b2.Dark) { 'DARK' } else { 'LIGHT' }))
$changed = ($b1.Dark -ne $b2.Dark)
Write-Host ("THEME-CHANGED: " + $(if ($changed) { 'YES' } else { 'NO' }))

# 恢复原主题
[System.Windows.Forms.SendKeys]::SendWait('T')
Start-Sleep -Milliseconds 900
Write-Host 'THEME CHECK DONE'
