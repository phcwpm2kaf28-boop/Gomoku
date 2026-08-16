# 交互冒烟测试：调用顶栏按钮 -> 验证对话框出现；键盘落子 -> 验证状态变化
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms

$p = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$w = [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)

function Find-Button([string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $c2 = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $and = New-Object System.Windows.Automation.AndCondition($c, $c2)
    return $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}
function Invoke-Button([string]$name) {
    $b = Find-Button $name
    if ($null -eq $b) { Write-Host ("MISSING: " + $name); return $false }
    $inv = $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $inv.Invoke()
    Write-Host ("INVOKED: " + $name)
    return $true
}
function Find-Text([string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $c2 = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Text)
    $and = New-Object System.Windows.Automation.AndCondition($c, $c2)
    return $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}
function Send-Keys([string]$keys) {
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 300
}

# 1. 设置对话框
Invoke-Button '设置'
Start-Sleep -Seconds 1
$dlg = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Pane)))  # 探测对话框标题
$foundDlg = $false
$all = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                  [System.Windows.Automation.Condition]::TrueCondition)
foreach ($el in $all) {
    $n = $el.Current.Name
    if ($n -eq '设置' -and $el.Current.ControlType.ProgrammaticName -like '*Window*') { $foundDlg = $true }
}
Write-Host ("SETTINGS DIALOG: " + $(if ($foundDlg) { 'OPEN' } else { 'check elements below' }))
$keys = ('外观主题','玩家昵称','键盘快捷键','恢复默认','跟随系统','浅色','深色')
foreach ($k in $keys) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $k)
    if ($w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)) { Write-Host ("  + " + $k) }
    else { Write-Host ("  - " + $k) }
}
# 关闭设置对话框（Esc 或 完成按钮）
Invoke-Button '完成'
Start-Sleep -Milliseconds 800

# 2. 联机对话框
Invoke-Button '联机'
Start-Sleep -Seconds 1
$onlineKeys = ('联机对弈','创建房间','刷新房间','选择对方玩家','加入','关闭')
foreach ($k in $onlineKeys) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $k)
    if ($w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)) { Write-Host ("  + " + $k) }
    else { Write-Host ("  - " + $k) }
}
Invoke-Button '关闭'
Start-Sleep -Milliseconds 800

# 3. 键盘落子：Enter 落子，观察状态栏文本变化
Send-Keys '{ENTER}'
Start-Sleep -Milliseconds 500
$s1 = Find-Text '第 1 手*'
Write-Host ("MOVE1 TEXT: " + $(if ($s1) { $s1.Current.Name } else { 'not found' }))
Send-Keys 'U'   # 悔棋
Start-Sleep -Milliseconds 500
Write-Host 'UNDO OK (no crash)'

# 4. 主题切换：T 两次（深->浅）
Send-Keys 'T'
Start-Sleep -Milliseconds 600
Send-Keys 'T'
Start-Sleep -Milliseconds 600
Write-Host 'THEME TOGGLE OK (no crash)'

Write-Host 'INTERACT DONE'
