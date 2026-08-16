[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
$i = 0
foreach ($el in $all) {
    $i++
    $n = $el.Current.Name
    $ct = $el.Current.ControlType.ProgrammaticName.Split('.')[-1]
    if ($n) {
        $r = $el.Current.BoundingRectangle
        Write-Host ($i.ToString().PadLeft(3) + " " + $ct.PadRight(12) + " " + $n + "  " + [int]$r.X + "," + [int]$r.Y + " " + [int]$r.Width + "x" + [int]$r.Height)
    }
}
Write-Host ("TOTAL: " + $i)
