# Development Setup

StoryLoom targets .NET 10 on Windows.

## Requirements

- Windows 10/11
- .NET SDK 10.0.203 or newer compatible .NET 10 SDK
- Visual Studio 2022 or Windows SDK support for `net10.0-windows10.0.26100.0`

## Local SDK

This checkout can use a local SDK installed under `.dotnet/`. The directory is ignored by Git.

To install the SDK locally:

```powershell
New-Item -ItemType Directory -Force -Path .tools | Out-Null
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile .tools\dotnet-install.ps1
& .tools\dotnet-install.ps1 -Version 10.0.203 -InstallDir .dotnet -Architecture x64 -NoPath
```

## Restore And Build

```powershell
.\.dotnet\dotnet.exe restore .\StoryLoom.sln
.\.dotnet\dotnet.exe build .\StoryLoom.sln --no-restore
```

If you have a matching SDK installed globally, `dotnet build .\StoryLoom.sln` also works.

## Repository Hygiene

Do not commit local runtime data, API keys, SDK folders, or verification build outputs. In particular:

- `.dotnet/`
- `.tools/`
- `artifacts/`
- `Config/config.json`
- `Saves/`
