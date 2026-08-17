# ============================================================
#  Gomoku 五子棋 - MSIX 一键安装脚本
#
#  自签名证书首次安装，必须把证书导入"本机（LocalMachine）
#  受信任的根证书颁发机构"。原因是：Add-AppxPackage 的签名
#  信任校验由 AppX 部署服务（SYSTEM 权限）执行，它只认本机
#  根存储，当前用户存储里的证书它不认（会报 0x800B0109）。
#  导入本机根存储需要管理员权限，脚本检测到非管理员时会
#  自动弹出 UAC 请求提升后重新运行。
#
#  用法：双击 安装五子棋.bat（推荐）；或右键本脚本
#        -> "使用 PowerShell 运行"。
# ============================================================
$ErrorActionPreference = 'Stop'

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$msix = Join-Path $dir 'Gomoku_1.1.0.0_x64.msix'
$cer  = Join-Path $dir 'Gomoku.cer'

if (-not (Test-Path $msix)) { Write-Host "[错误] 找不到 $msix"; Read-Host '按回车退出'; exit 1 }
if (-not (Test-Path $cer))  { Write-Host "[错误] 找不到 $cer"; Read-Host '按回车退出'; exit 1 }

# --- 检测管理员权限，非管理员自动提权重启本脚本 ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host '[提示] 导入本机受信任根证书需要管理员权限，正在请求提升（UAC 请点"是"）...'
    $args = "-NoProfile -ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`""
    Start-Process -FilePath 'powershell.exe' -ArgumentList $args -Verb RunAs
    exit 0
}

$thumb = (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cer)).Thumbprint

Write-Host '1/3 导入受信任根证书（本机 -> 受信任的根证书颁发机构）...'
if (Get-ChildItem 'Cert:\LocalMachine\Root' | Where-Object Thumbprint -eq $thumb) {
    Write-Host '   证书已存在，跳过。'
} else {
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Write-Host '   证书已导入。'
}

Write-Host '2/3 安装 MSIX 包（请稍候，包内含自包含运行时约 200MB）...'
# 已安装过旧版时先卸载，避免同版本号覆盖安装报错
$old = Get-AppxPackage -Name 'Gomoku.Game' -ErrorAction SilentlyContinue
if ($old) {
    Write-Host "   检测到已安装旧版本 $($old.Version)，先卸载..."
    Remove-AppxPackage -Package $old.PackageFullName
}
Add-AppxPackage -Path $msix

Write-Host '3/3 验证安装...'
$pkg = Get-AppxPackage -Name 'Gomoku.Game'
if ($pkg) {
    Write-Host ''
    Write-Host "安装成功！版本 $($pkg.Version)"
    Write-Host '从开始菜单搜索「五子棋」即可启动。'
} else {
    Write-Host '[错误] 安装未完成，请检查上方错误信息。'
    exit 1
}
