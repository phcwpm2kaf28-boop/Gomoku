[System.Reflection.Assembly]::LoadWithPartialName('UIAutomationClient') | Out-Null
$proc = Get-Process Gomoku -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $proc) { Write-Host 'WINDOW NOT FOUND'; exit 1 }
$rootEl = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
$nameSettings = [string][char]0x8BBE + [string][char]0x7F6E
$nameDone = [string][char]0x5B8C + [string][char]0x6210
$nameOnline = [string][char]0x8054 + [string][char]0x673A
$nameClose = [string][char]0x5173 + [string][char]0x95ED

$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nameSettings)
$btnSettings = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
if ($null -eq $btnSettings) { Write-Host 'SETTINGS BTN MISSING'; exit 1 }
$hasInv = $btnSettings.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$null)
if ($hasInv) { $btnSettings.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host 'SETTINGS INVOKED' }
Start-Sleep -Seconds 1
$cond2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nameDone)
$btnDone = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond2)
Write-Host ("SETTINGS DIALOG: " + $(if ($btnDone) { 'OPEN' } else { 'NO' }))
if ($btnDone) {
    $hasInv2 = $btnDone.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$null)
    if ($hasInv2) { $btnDone.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
Start-Sleep -Milliseconds 600

$cond3 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nameOnline)
$btnOnline = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond3)
if ($null -eq $btnOnline) { Write-Host 'ONLINE BTN MISSING'; exit 1 }
$hasInv3 = $btnOnline.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$null)
if ($hasInv3) { $btnOnline.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Host 'ONLINE INVOKED' }
Start-Sleep -Seconds 1
$cond4 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $nameClose)
$btnClose = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond4)
Write-Host ("NETWORK DIALOG: " + $(if ($btnClose) { 'OPEN' } else { 'NO' }))
if ($btnClose) {
    $hasInv4 = $btnClose.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$null)
    if ($hasInv4) { $btnClose.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
Start-Sleep -Milliseconds 600
Write-Host 'SMOKE QUICK DONE'
