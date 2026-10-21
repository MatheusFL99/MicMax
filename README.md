# MicMax — Microphone Volume Lock

A lightweight Windows tray utility that locks your microphone input level at **100% and unmuted**, automatically reverting any change made by other applications, Windows, or audio drivers — within one second.

"Configure and forget." MicMax sits in the system tray and keeps your mic exactly where you left it.

## Why

On Windows 11 the microphone input level likes to drop on its own — the "Communications" automatic level reduction, AGC from apps like Discord/Zoom, or audio drivers. MicMax counteracts those changes continuously without you touching anything.

## Features

- **Lock at 100% unmuted** and keep it there, reverting external changes in under one second (event-driven, no polling).
- **System-tray operation** with a context menu: Lock, Pause, Resume, Auto-Start, Select Microphone, Open Window, Exit.
- **Persisted settings** — selected mic, lock state, and auto-start survive restarts and reboots (per-user Registry, `HKCU\Software\MicMax`).
- **Auto-start with Windows** — launches at sign-in and restores the previous lock state.
- **Device fallback** — follows the Windows default communication microphone when the locked device disconnects, and re-locks the original when it returns.
- **Standard Windows notifications** for lock/pause/resume and device switches; respects Focus Assist / Do Not Disturb.

## Requirements

- Windows 10 (version 1809 or later) or Windows 11
- .NET 8 SDK (to build from source)
- At least one microphone input device
- **No administrator rights** required — everything runs per-user

## Build

From the repository root:

```powershell
# Restore and build the whole solution (Core, app, tests)
dotnet build MicMax.sln

# Run the test suite
dotnet test MicMax.sln
```

## Publish (single self-contained executable)

```powershell
dotnet publish src\MicMax\MicMax.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output:

```
src\MicMax\bin\Release\net8.0-windows\win-x64\publish\MicMax.exe
```

A single file with no external dependencies — copy it anywhere and run.

## Installer (Inno Setup)

The repo includes an Inno Setup script that produces a per-user installer — no administrator rights required.

```powershell
# 1. Publish the app first (see above)
dotnet publish src\MicMax\MicMax.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 2. Compile the installer (requires Inno Setup 6: winget install JRSoftware.InnoSetup)
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\MicMax.iss
```

Output: `dist\MicMax-Setup-<version>.exe`

What the installer does:

- Installs per-user to `%LocalAppData%\Programs\MicMax` (no UAC prompt, no admin rights).
- Creates a Start Menu shortcut (and optionally a desktop icon).
- Offers a **"Iniciar o MicMax automaticamente"** task (checked by default on first install) that registers the same `HKCU\...\Run` value the app's own Auto-Start menu toggle manages.
- Stops a running MicMax before installing/uninstalling, and removes the `HKCU\Software\MicMax` settings on uninstall.

To change the app version, edit `AppVersion`/`VersionInfoVersion` in `installer\MicMax.iss`.

## Usage

1. Launch **MicMax.exe** — it starts in the system tray.
2. Click (or right-click) the tray icon to open the window or the menu.
3. Pick your microphone (or leave it on *Follow Windows default communication device*).
4. Choose **Lock Microphone**. The level is set to 100% unmuted and a notification confirms it.
5. From now on, any external change is reverted automatically. Use **Pause** in the tray menu if you ever need to adjust the level manually.

See [`specs/001-mic-volume-lock/quickstart.md`](specs/001-mic-volume-lock/quickstart.md) for full validation scenarios.

## Download

Grab the latest **`MicMax-Setup-*.exe`** from the [Releases page](../../releases) and run it — no admin rights needed. A portable single-file version (`MicMax-*-portable.exe`) is provided there too.

## Privacy

MicMax **never records, stores, transmits, or processes audio content**. It only reads and sets the device's volume level and mute state through the Windows Core Audio API.

## Project layout

| Path | Purpose |
|------|---------|
| `src/MicMax.Core/` | Core class library — audio interop, lock services, settings, notifications |
| `src/MicMax/` | WinForms tray application (entry point, tray icon, main window) |
| `tests/MicMax.Core.Tests/` | xUnit test suite |
| `specs/001-mic-volume-lock/` | Feature specification, plan, contracts, and task list |
| `installer/` | Inno Setup script for the per-user Windows installer |
| `.github/workflows/` | CI: builds the app + installer and publishes releases on `v*` tags |

## License

Released under the [MIT License](LICENSE).

## Specification

The feature is developed spec-first. See [`specs/001-mic-volume-lock/spec.md`](specs/001-mic-volume-lock/spec.md) for requirements and success criteria.
