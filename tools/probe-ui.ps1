# 冒烟测试：枚举五子棋窗口的控件树（按钮/文本/列表项 + 屏幕矩形）
Add-Type -AssemblyName UIAutomationClient

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }

$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
Write-Host ("FOUND: " + $w.Current.Name + " rect=" + $w.Current.BoundingRectangle)

$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
foreach ($el in $all) {
    $n = $el.Current.Name
    $ct = $el.Current.ControlType.ProgrammaticName
    $r = $el.Current.BoundingRectangle
    if ($n) {
        $type = $ct.Split('.')[-1]
        if ($type -match 'Button|Text|ListItem|TitleBar|ComboBox|Edit|RadioButton|CheckBox|TabItem') {
            Write-Host ("  " + $type.PadRight(12) + " | " + $n.PadRight(24) +
                        " | X=" + [int]$r.X + " Y=" + [int]$r.Y +
                        " W=" + [int]$r.Width + " H=" + [int]$r.Height)
        }
    }
}
Write-Host 'PROBE DONE'
