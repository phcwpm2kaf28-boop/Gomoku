# 五子棋 Gomoku（WinUI 3）

![Windows](https://img.shields.io/badge/Windows-11%20%7C%2010%201809%2B-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![WinUI](https://img.shields.io/badge/WinUI-3-0b66c3) ![Tests](https://img.shields.io/badge/Tests-20%20passed-brightgreen) ![License](https://img.shields.io/badge/License-MIT%20%2B%20Additional-green)

基于 **WinUI 3 / Windows App SDK** 构建的五子棋游戏，完整遵循 Windows 11 Fluent Design 设计语言：
Mica 材质背景、圆角卡片、明暗主题、触摸 / 键盘 / 鼠标全支持。仅需核显轻薄本即可流畅运行。

## ✨ 功能

| 功能 | 说明 |
|---|---|
| 双人对弈 | 本机双人轮流落子 |
| 人机对弈 | 5 档难度（简单 / 普通 / 困难 / 专家 / 大师），大师采用迭代加深搜索 |
| 局域网联机 | 同一 WiFi / 局域网内自动发现房间，也可手动输入 IP；主机执黑先行 |
| 悔棋 | 撤销一回合（联机模式下可向对手发起悔棋请求） |
| 提示系统 | 一键显示建议落点（人机模式，可在设置中关闭） |
| 键位自定义 | 全部快捷键可重新绑定（光标移动 / 落子 / 悔棋 / 提示 / 新开局 / 主题） |
| 明暗主题 | 明 / 暗 / 跟随系统三档，一键切换（快捷键 T），标题栏与窗口按钮颜色同步 |
| 立体棋子 | 径向渐变高光 + 底部暗边 + 反光点 + 落子弹入动画，纯 XAML 渲染，性能开销极低 |
| 完整输入 | 鼠标点击 / 键盘光标（方向键或 WASD）+ 空格落子 / 触摸屏手指落子 |

## 📦 获取与安装

发布产物位于 `release/` 目录，提供两种形式：

### 方式一：MSIX 安装包（推荐）

文件：`Gomoku_1.1.0.0_x64.msix` + `Gomoku.cer` + `install-msix.ps1` + `安装五子棋.bat`
（GitHub Release 中该脚本名为 `install-gomoku.bat`，内容与本地一致）

1. **双击 `安装五子棋.bat`** 即可自动完成全部安装（自动请求 UAC 提权 → 导入证书到本机受信任根 → 安装应用）；
2. 或右键 `install-msix.ps1` → **使用 PowerShell 运行**（注意：`.ps1` 双击默认不运行，这是 Windows 安全策略，属正常现象）；
3. 或手动操作：右键 `Gomoku.cer` → 安装证书 → **本地计算机** → 受信任的根证书颁发机构（需管理员）；
   然后双击 `Gomoku_1.1.0.0_x64.msix` 完成安装；
4. 安装后在开始菜单搜索 **五子棋** 启动。

> ⚠️ **证书必须导入"本机"受信任根（LocalMachine\Root）**：Add-AppxPackage 的签名校验由
> AppX 部署服务（SYSTEM 权限）执行，只认本机根存储；只导入当前用户存储会报
> `0x800B0109 根证书不受信任`。安装脚本已自动处理（UAC 提权），手动安装时请勿选错存储。

> 应用为**自签名**（证书 CN=Gomoku，有效期 10 年），首次安装需信任证书，这是旁加载应用的正常步骤。
> 升级时重新运行脚本即可（同版本号覆盖安装）。

### 方式二：免安装绿色版

文件：`Gomoku-Portable-Win64.zip`

解压后直接双击 `Gomoku.exe` 运行，无需安装、无需 Windows App Runtime、不含任何注册表修改。
设置保存在 `%LOCALAPPDATA%\Gomoku\settings.json`。

> 两种版本要求 Windows 10 1809（Build 17763）及以上，推荐 Windows 11。

## 🎮 操作说明

### 界面布局（Windows 11 系统应用风格）

- **标题栏**：标准 32px 高，仅显示应用名，右侧为系统窗口按钮（最小化 / 最大化 / 关闭）；
  与窗口 Mica 一体、随主题变色，可拖拽移动窗口；
- **工具栏**（标题栏下方独立一行，系统 CommandBar 风格）：左侧 模式（双人 / 人机 / 联机）、
  难度、执子颜色 图标+文字按钮；中部 悔棋 / 提示 / 新开局；右侧 主题（跟随系统 / 浅色 / 深色）、联机、设置；
- **底部状态栏**：居中显示双方昵称与当前状态（含手数）；
- **设置**：点工具栏「设置」弹出应用内对话框（主题三选、昵称、提示开关、键位重绑、关于）；
- **联机**：点工具栏「联机」弹出引导对话框，可创建房间、刷新搜索同网玩家、从列表选择或手动输入 IP 加入。

### 快捷键

| 动作 | 默认按键 |
|---|---|
| 移动光标 | `↑↓←→` 或 `WASD` |
| 落子 | `Enter` / `空格` |
| 悔棋 | `U` |
| 提示 | `H` |
| 新开局 | `R` |
| 切换主题 | `T` |
| 打开设置 | `O` |

鼠标/触摸直接点击棋盘交叉点落子；设置对话框可重新绑定所有键位。

### 联机对弈提示

- 两台设备连**同一 WiFi / 局域网**，一方点「创建房间」，另一方点「刷新房间」自动发现并加入；
- 路由器开了 **AP 隔离**（访客网络常见）时无法发现，请用「IP 手动加入」；
- 首次运行请允许防火墙放行（TCP 45679 + UDP 45680，见下）；
- 对局中可点「悔棋」向对方发起请求，对方确认后双方各撤回一步。

## 🔨 从源码构建

环境要求：.NET SDK 8.0、Windows 10 SDK（makeappx / signtool 打包用）、Windows 11（推荐）。

```bash
# 1. 常规构建（开发调试）
dotnet build Gomoku/Gomoku.csproj -c Release

# 2. 绿色版发布（自包含，输出到 publish/portable）
dotnet publish Gomoku/Gomoku.csproj -c Release -r win-x64 --self-contained true \
    -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -o publish/portable

# 3. MSIX 安装包（输出到 Gomoku/AppPackages/）
dotnet build Gomoku/Gomoku.csproj -c Release -r win-x64 -p:AppxPackage=true

# 4. 签名（自签名证书 Gomoku.pfx，密码见 tools 说明）
signtool sign /f release/Gomoku.pfx /p <密码> /fd SHA256 Gomoku_1.1.0.0_x64.msix
```

图标生成脚本：`tools/make-icon.ps1`（PowerShell 5.1 + System.Drawing）。
单元测试：`tests/LogicTests/`（GameCore + AiEngine 纯逻辑层，20 项断言）。

## 🏗️ 技术架构

```
Gomoku/
├── Game/        GameCore.cs        纯逻辑核心（棋盘状态/胜局判定/悔棋），可独立单测
│                GameController.cs  模式状态机（双人/人机/联机统一协调）
├── AI/          AiEngine.cs        negamax + alpha-beta，预编译模式表，5 档难度
├── Net/         NetSession.cs      TCP 45679 对局 + UDP 45680 房间发现，JSON 行协议
├── Controls/    BoardView.*        纯 XAML 棋盘：共享静态径向渐变画刷，核显友好
│                BoardVisuals.cs    棋子立体画刷（高光/暗边/反光）主题化重建
├── Pages/       GamePage.*         对局页（顶栏一体化标题栏/棋盘/底部状态栏/联机对话框）
│                SettingsDialog.*   设置对话框（主题三选/键位捕获/提示开关/昵称/关于）
├── Services/    ThemeService.cs    明暗主题 + 标题栏按钮配色（窗口创建后用根元素主题，避免 WinUI 限制）
│                SettingsService.cs JSON 持久化到 %LOCALAPPDATA%\Gomoku
│                KeyUtil.cs         键位字符串 ↔ 按键事件转换匹配
└── App.xaml     主题资源（明暗两套棋盘/卡片画刷）
```

### 性能设计

- 棋盘用 **Viewbox 缩放 800×800 逻辑画布**，实际渲染元素只有 30 条网格线 + 数百个子圆，
  静态共享画刷（一次创建、全盘复用），无逐帧重绘；
- 大师难度 AI 在后台线程运行，可随时取消，UI 不卡顿；
- 落子动画仅用 150ms 依赖动画，结束后自动释放，长时间对局内存稳定。

## 📄 常见问题

- **安装 MSIX 提示"无法验证发布者"** → 先安装 `Gomoku.cer` 到受信任根，再装包。
- **联机搜不到房间** → 检查同一网络、关闭 AP 隔离、检查防火墙（TCP 45679 / UDP 45680）。
- **绿色版被 SmartScreen 拦截** → 无签名程序首次运行属正常，点「更多信息 → 仍要运行」。
- **主题切换后 Mica 背景不变** → Mica 是系统背景材质，明暗由系统控制；应用内棋盘、控件与标题栏按钮颜色会立即切换。

## 📝 许可证

[LICENSE](LICENSE)：**MIT License + 附加条款**

**附加条款**：
1. **商业使用必须开源**：任何商业用途（销售、集成到商业产品或服务等）须将修改后的与本软件有关部分的完整源码开源。
    **Mandatory Open-Sourcing for Commercial Use**：All commercial use (sales, integration into commercial products/services, etc.) requires full open-source disclosure of the complete source code for all project-related modifications.

2. **功能免费**：本软件及其所有相关功能不得在被引用的软件中就涉及本软件的部分向最终用户收费。
    **Free Functionality Requirement**：After referencing this software and its related functionalities, you may not charge end users for any parts related to this software.
