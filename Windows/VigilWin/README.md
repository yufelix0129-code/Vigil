# Vigil for Windows

这是 Vigil 的 Windows MVP 版本，基于 C#、WPF 和 .NET 8。

当前版本支持：

- 输入本次专注目标和专注时长
- 定时截取主屏幕
- 调用 OpenAI-compatible 视觉模型判断专注状态
- 分心时显示顶部提醒和可选全屏半透明遮罩
- 使用 SQLite 保存本地历史记录

## 运行方式

需要：

- Windows
- .NET 8 SDK，或 Visual Studio 2022

运行：

```powershell
dotnet build Windows\VigilWin\VigilWin.csproj
dotnet run --project Windows\VigilWin\VigilWin.csproj
```

也可以在 Visual Studio 2022 中打开 `Windows/VigilWin/VigilWin.csproj`，然后 Build 和 Run。

## 配置 AI

打开主界面的“设置”按钮，填写：

- Base URL
- API Key
- Model

接口格式为 OpenAI-compatible chat completions endpoint，例如：

- `https://api.openai.com/v1`
- 本地或第三方兼容服务的 `/v1` 地址

程序会调用 `{BaseUrl}/chat/completions`。如果没有配置 API Key，程序仍可开始专注会话，但不会上传截图到 AI Provider，分析记录会显示“AI 配置不完整”。

## 本地数据

Windows 版数据保存在：

- `%APPDATA%/VigilWin/settings.json`
- `%APPDATA%/VigilWin/vigil.db`
- `%APPDATA%/VigilWin/Screenshots/`

默认不保存截图。只有在设置里开启 `Save Screenshots` 时，截图才会保存到本地 `Screenshots` 文件夹；数据库只保存截图路径，不保存图片二进制。

## 隐私说明

- 如果使用云端 AI，截图会发送给用户配置的 AI Provider。
- 如果不配置 AI，不会上传截图。
- 如果使用本地 OpenAI-compatible 服务，例如本地视觉模型，则截图可以只在本机处理。
- API Key 目前集中保存在 `settings.json`。后续应迁移到 Windows DPAPI 或 Credential Manager。

## 当前限制

- 只截取主屏幕。
- AI 分析依赖支持视觉输入的模型。
- UI 是 MVP 版本，不追求精美动效。
- 没有自动更新。
- 没有多屏幕选择。
- 没有系统托盘、开机自启动、账号登录或云同步。
