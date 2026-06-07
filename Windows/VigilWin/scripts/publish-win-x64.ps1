$ErrorActionPreference = "Stop"

function Resolve-DotNet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    if ($env:DOTNET_ROOT) {
        $candidate = Join-Path $env:DOTNET_ROOT "dotnet.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $candidates = @(
        "C:\Program Files\dotnet\dotnet.exe",
        "C:\Program Files (x86)\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "未找到 dotnet。请安装 .NET 8 SDK，或设置 DOTNET_ROOT 指向 SDK 目录。"
}

$dotnet = Resolve-DotNet
$projectDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputDir = Join-Path $projectDir "publish\win-x64"

Push-Location $projectDir
try {
    & $dotnet publish .\VigilWin.csproj -c Release -r win-x64 --self-contained true -o $outputDir
}
finally {
    Pop-Location
}

$exePath = Join-Path $outputDir "VigilWin.exe"
Write-Host "发布完成，exe 位于：$exePath"
