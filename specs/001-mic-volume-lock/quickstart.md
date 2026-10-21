# Quickstart: MicMax — Microphone Volume Lock

## Prerequisites

- Windows 10 (version 1809 or later) or Windows 11
- .NET 8 SDK installed
- A microphone input device available on the system
- Administrative rights are **not** required for normal operation

## Build & Publish

```powershell
# From the repository root
cd .  # stay at the repository root (MicMax.sln lives here)

# Restore dependencies and build everything (Core, app, tests)
dotnet build MicMax.sln

# Run the test suite
dotnet test MicMax.sln

# Publish as a single executable (platform-specific)
dotnet publish src\MicMax\MicMax.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published executable will be located at:
`src\MicMax\bin\Release\net8.0-windows\win-x64\publish\MicMax.exe`

The executable is a single file with no external dependencies (self-contained), and can be placed in any directory.

## First-Time Setup

1. **Launch MicMax.exe** — The application will start in the system tray (notification area).
2. **Click the tray icon** (or double-click) to open the main window (if closed).
3. **The app automatically detects** the Windows default communication recording device.
4. **Select "Lock Microphone"** from the tray menu (or click the "Lock" button in the main window).
   - The microphone input level is immediately set to 100% and unmuted.
   - A notification confirms: *"MicMax: Microphone locked at 100%"*.
5. **Monitoring begins** — Any external changes to the microphone's level or mute state will be reverted to 100% unmuted within 1 second.

## Validation Scenarios

### Scenario 1: Lock Activation

**Prerequisites**: MicMax is running in the tray, a microphone is selected/default.

**Steps**:
1. Open the main window (click tray icon).
2. Click "Lock Microphone" in the tray menu, or press the lock button.
3. Verify the notification appears: *"MicMax: Microphone locked at 100%"*.
4. Open Windows Sound Settings and manually drag the microphone level slider down to 50%.
5. **Wait ≤ 1 second** — the level should automatically return to 100% and unmute.

**Expected Outcome**: Microphone level returns to 100% and unmuted within 1 second. Notification may appear if the change triggers a correction event.

### Scenario 2: Pause/Resume Lock

**Prerequisites**: Lock is active (microphone at 100% unmuted).

**Steps**:
1. From the tray menu, choose **"Pause"**.
2. **Notification**: *"MicMax: Microphone lock paused"*.
3. While paused, drag the microphone level slider down in Windows Sound Settings.
4. **Verify**: The level stays at the external change (e.g., 50%) — MicMax is no longer reverting it.
5. From the tray menu, choose **"Resume"**.
6. **Notification**: *"MicMax: Microphone lock resumed"*.
7. **Verify**: Microphone level returns to 100% and unmuted within 1 second.

**Expected Outcome**: While paused, external changes are not reverted. On resume, microphone is immediately returned to 100% unmuted.

### Scenario 3: Auto-Start with Windows

**Prerequisites**: MicMax is running and configured.

**Steps**:
1. From the tray menu, choose **"Auto-Start"** to enable it (toggle on).
2. **Notification** may confirm the change.
3. Close MicMax completely (right-click tray icon → "Exit", or just close the main window).
4. Restart the computer and log into Windows.
5. **Verify**: MicMax launches automatically and the lock is active (microphone at 100% unmuted).

**Expected Outcome**: Within 60 seconds of Windows sign-in, MicMax is running with the lock active.

### Scenario 4: Device Fallback (Disconnected Microphone)

**Prerequisites**: Lock is active on a specific microphone.

**Steps**:
1. With the lock active, physically disconnect the locked microphone (or disable it in Windows).
2. **Wait ≤ 10 seconds** — MicMax should automatically switch to the new Windows default communication recording device, lock it at 100% unmuted, and send a notification naming the new device.
3. **Notification**: *"MicMax: Switched to {device name} — microphone locked at 100%"*.

**Expected Outcome**: Lock continues on the new default device without user intervention.

### Scenario 5: Manual Microphone Selection

**Prerequisites**: MicMax is running, default device is locked.

**Steps**:
1. From the tray menu, choose **"Select Microphone"**.
2. The main window shows a list of available input devices.
3. Select a different microphone from the list.
4. **Verify**: The app locks the newly selected device at 100% unmuted, and the previous device is released.

**Expected Outcome**: User can explicitly choose which microphone to lock; the selection is persisted.

## Troubleshooting

| Issue | Possible Cause | Resolution |
|-------|---------------|-----------|
| No microphone detected at lock time | No input devices available | Wait for a device to appear; the app will keep trying and notify when one is available |
| Lock not reverting changes | App not running with sufficient audio permissions | Verify the app is running (tray icon present); on standard user accounts, Core Audio API access is allowed per-user |
| Auto-start not working | Registry key not configured correctly | Check `HKCU\Software\MicMax\AutoStart` value; ensure the app is not blocked by Group Policy |
| Notifications not appearing | Focus Assist / Do Not Disturb enabled | This is by design; notifications respect Focus Assist. Check the action center manually. |

## Next Steps

- Full implementation details (source code, P/Invoke interop, UI layout) are in the implementation phase.
- For the data model and internal contracts, see `data-model.md` and `contracts/` directory.
- For research findings and decisions, see `research.md`.
- To track tasks and implementation progress, see the tasks.md file (generated during implementation).