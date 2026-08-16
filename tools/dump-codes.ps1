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
    if ($n) {
        $codes = ''
        foreach ($ch in $n.ToCharArray()) { $codes += ('U+' + ([int]$ch).ToString('X4') + ' ') }
        $ct = $el.Current.ControlType.ProgrammaticName.Split('.')[-1]
        Write-Host ($i.ToString().PadLeft(3) + " " + $ct.PadRight(10) + " " + $codes)
    }
}
Write-Host ("TOTAL: " + $i)
