Add-Type -AssemblyName System.Drawing
[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$lw = $w.Current.BoundingRectangle
Write-Host ("WIN: " + [int]$lw.X + "," + [int]$lw.Y + " " + [int]$lw.Width + "x" + [int]$lw.Height)
function Sample-Color([int]$x, [int]$y) {
    $bmp = New-Object System.Drawing.Bitmap(1, 1)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size(1, 1)))
    $c = $bmp.GetPixel(0, 0)
    $g.Dispose(); $bmp.Dispose()
    return $c
}
$midY = [int]($lw.Y + $lw.Height * 0.5)
$x = [int]$lw.X + 60
while ($x -lt [int]$lw.X + $lw.Width - 60) {
    $c = Sample-Color $x $midY
    Write-Host ("  " + $x + "," + $midY + " = #" + $c.R.ToString('X2') + $c.G.ToString('X2') + $c.B.ToString('X2'))
    $x += 100
}
$midX = [int]($lw.X + $lw.Width / 2)
$y = [int]$lw.Y + 60
while ($y -lt [int]$lw.Y + $lw.Height - 40) {
    $c = Sample-Color $midX $y
    Write-Host ("  " + $midX + "," + $y + " = #" + $c.R.ToString('X2') + $c.G.ToString('X2') + $c.B.ToString('X2'))
    $y += 80
}
Write-Host 'PEEK DONE'
