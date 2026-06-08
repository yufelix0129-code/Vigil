# Vigil for Windows

这是 Vigil 的 Windows MVP 版本，基于 C#、WPF 和 .NET 8。

当前版本仍是非正式稳定版，目标是先跑通 Windows 桌面端的核心流程：

- 输入本次专注目标和专注时长
- 定时截取主屏幕
- 调用 OpenAI-compatible 视觉模型判断专注状态
- 专注时显示 Windows WPF 模拟的 Dynamic Island 风格顶部悬浮窗
- 分心时显示顶部提醒和可选全屏半透明遮罩
- 使用 SQLite 保存本地历史记录

## UI 状态

Windows 版界面正在朝 macOS 原版 Vigil 的质感靠近：

- 主窗口使用浅色 Liquid Glass 风格卡片、柔和阴影和轻量打开动画。
- 设置页采用 AI Provider / Focus Monitor / Interface 三段式偏好设置布局。
- 历史页使用圆角 session 卡片和统计卡片，避免默认 WPF 蓝色选中样式。
- Floating Reminder 是轻量顶部 toast；Overlay 是可关闭的 glass modal。

## 从源码运行

需要：

- Windows
- .NET 8 SDK，或 Visual Studio 2022

在仓库根目录运行：

```powershell
dotnet build Windows\VigilWin\VigilWin.csproj
dotnet run --project Windows\VigilWin\VigilWin.csproj
```

也可以在 Visual Studio 2022 中打开 `Windows/VigilWin/VigilWin.csproj`，然后 Build 和 Run。

## 发布 exe

在 PowerShell 中从仓库根目录运行：

```powershell
Windows\VigilWin\scripts\publish-win-x64.ps1
```

脚本会优先使用 PATH 中的 `dotnet`；如果没有加入 PATH，可以设置 `DOTNET_ROOT` 指向 .NET SDK 目录。

发布产物位置：

```text
Windows/VigilWin/publish/win-x64/
```

脚本会执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

完成后运行 `Windows/VigilWin/publish/win-x64/VigilWin.exe`。

## 配置 AI

打开主界面的“设置”按钮，填写：

- Base URL
- API Key
- Model

接口格式为 OpenAI-compatible chat completions endpoint，例如：

- `https://api.openai.com/v1`
- 本地或第三方兼容服务的 `/v1` 地址

程序会调用 `{BaseUrl}/chat/completions`。如果没有配置 API Key，程序仍可开始专注会话，但不会上传截图到 AI Provider，分析记录会显示“AI 配置不完整”。

## Dynamic Island

Windows 版支持一个 Dynamic Island 风格的顶部悬浮窗：

- 这是 WPF 模拟效果，不是 macOS 系统级灵动岛。
- 专注开始后会在主屏幕顶部居中显示。
- Compact 模式显示当前状态、专注目标和实时计时。
- Compact 底部有极细进度线，时间每秒更新但不触发整窗尺寸动画。
- 点击灵动岛可在 Compact 和 Expanded 之间切换，切换使用 WPF 原生宽高、透明度和阴影动画。
- Expanded / Alert 底部有专注进度条，参考 macOS 原版的 FocusTimelineBar 思路，用于避免展开态出现空白区域。
- Distracted 时会展开成 Alert，显示目标和 AI 判断原因，并在一段时间后自动收回 Compact。
- Session completed / stopped 时会短暂显示完成状态，然后自动淡出并关闭窗口。
- 设置页可以通过 `Enable Dynamic Island` 开启或关闭。

主界面和 Dynamic Island 的计时显示每秒更新；AI 截屏分析仍按设置页里的 `Capture Interval Seconds` 执行，不会因为计时每秒刷新而每秒调用 AI。

如果某台机器上动画明显卡顿，可以在设置页关闭 `Enable Dynamic Island`，主窗口计时、截屏、AI 分析和历史记录仍会照常工作。

## 本地数据

Windows 版数据保存在：

- 设置文件：`%APPDATA%/VigilWin/settings.json`
- 数据库：`%APPDATA%/VigilWin/vigil.db`
- 日志：`%APPDATA%/VigilWin/logs/vigil.log`
- 截图目录：`%APPDATA%/VigilWin/Screenshots/`
- 测试截屏：`%APPDATA%/VigilWin/test-screenshot.jpg`

默认不保存会话截图。只有在设置里开启 `Save Screenshots` 时，截图才会保存到本地 `Screenshots` 文件夹；数据库只保存截图路径，不保存图片二进制。

## API Key 安全

- API Key 使用 Windows 当前用户 DPAPI 加密后保存到 `settings.json` 的 `EncryptedApiKey` 字段。
- API Key 不写死在代码里。
- API Key 不写入日志。
- 如果更换 Windows 用户或重装系统，DPAPI 可能无法解密旧 Key，需要重新填写。

## 隐私说明

- 不配置 API Key 时不会上传截图。
- 开始专注并配置 AI 后，截图会发送到用户设置的 AI Provider。
- 如果设置本地 OpenAI-compatible 模型，截图可以只在本机分析。
- 日志不会记录 API Key，也不会记录截图 base64。

## 排错

遇到问题时优先查看：

```text
%APPDATA%/VigilWin/logs/vigil.log
```

常见检查：

- 设置页里 Base URL、Model、API Key 是否填写。
- 模型是否支持视觉输入。
- `settings.json` 是否能被当前 Windows 用户 DPAPI 解密。
- 数据库 `vigil.db` 是否被其他程序占用或损坏。

## 当前限制

- 只支持主屏幕截图。
- 不支持多显示器选择。
- 不支持系统托盘。
- 不支持自动更新。
- UI 仍是 MVP。
- AI 判断质量取决于模型。
