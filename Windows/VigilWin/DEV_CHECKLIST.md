# Windows Vigil Developer Checklist

## Build

```powershell
dotnet build Windows\VigilWin\VigilWin.csproj
```

## Run

```powershell
dotnet run --project Windows\VigilWin\VigilWin.csproj
```

## Feature Checks

- 主界面可以打开。
- 主界面左侧 Start Focus / Stop Focus 按钮完整显示，不被卡片底部裁切。
- Live Status 底部 Latest AI reason 区域有占位文字且不被裁切。
- “测试截屏”可以生成 `%APPDATA%/VigilWin/test-screenshot.jpg`。
- 设置页可以保存并重新读取 Base URL、Model、Capture Interval、Idle Threshold。
- 开始专注成功。
- 主界面计时每秒连续更新，不跟随 AI 分析 tick 跳变。
- Enable Dynamic Island 开启时，专注开始后顶部灵动岛出现并每秒更新时间。
- 灵动岛点击展开/收起有平滑动画，不出现多个窗口。
- 灵动岛 Expanded / Alert 底部显示专注进度条或状态线，不留空白。
- Stop Focus 后灵动岛显示 stopped/completed 状态，并在 3 秒内自动淡出消失。
- Enable Dynamic Island 关闭时，专注开始后不显示灵动岛。
- 停止专注成功。
- 没有 API Key 时不崩溃，也不发起 AI 请求。
- Idle 检测不崩溃。
- 历史记录窗口可以打开。
- 日志文件生成在 `%APPDATA%/VigilWin/logs/vigil.log`。
- 发布脚本可以生成 exe。

## Privacy Checks

- API Key 不进入日志。
- 截图 base64 不进入日志。
- `settings.json` 不保存明文 API Key，只保存 DPAPI 加密后的 `EncryptedApiKey`。
