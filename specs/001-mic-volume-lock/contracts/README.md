# Contracts: MicMax — Microphone Volume Lock

## Overview

This project is a standalone desktop tray application (C#/.NET 8) that does not expose external APIs, endpoints, or services to other systems. The "contracts" in this context document the internal interfaces that govern the application's behavior, state management, and interactions between its components.

Contracts are documented here for clarity and maintainability, not for external consumption.

---

## 1. Core Audio Control Contract

**Purpose**: Defines the operations the application performs on a Windows audio input device using the Core Audio API.

### Operations

| Operation | Description | API Mapping |
|-----------|-------------|-------------|
| `SetVolumeTo100Percent()` | Sets the selected microphone's input level to 100% and unmutes it. | `IAudioEndpointVolume.SetMasterVolumeLevelScalar(1.0)` |
| `GetCurrentVolume()` | Returns the current input level (0.0 - 1.0) of the locked device. | `IAudioEndpointVolume.GetMasterVolumeLevelScalar()` |
| `GetCurrentMuteState()` | Returns whether the locked device is currently muted. | `IAudioEndpointVolume.GetMute()` |
| `SetMuteToUnmuted()` | Unmutes the locked device without changing the level. | `IAudioEndpointVolume.SetMute(False, None)` |
| `MonitorVolumeChanges()` | Starts event-driven monitoring of the locked device's level/mute state via `IAudioEndpointVolumeCallback`. | `IAudioEndpointVolume.RegisterForNotifications()` (or equivalent P/Invoke) |
| `StopMonitoring()` | Stops event-driven monitoring. | Deregister notifications |

### Constraints

- No audio capture or processing of any kind — only read and set volume/mute state
- Must operate on standard user accounts without administrator privileges
- Event-driven (via `IAudioEndpointVolumeCallback`), no polling
- Corrections must occur within 1 second of external changes settling

---

## 2. Tray Menu Command Contract

**Purpose**: Defines the commands available in the system tray context menu and their effects.

| Command | Parameter | Effect |
|---------|-----------|--------|
| `Lock` | None | Activates the microphone lock: sets level to 100%, unmutes, starts monitoring. Sends notification. |
| `Pause` | None | Temporarily disables the lock: stops reverting external changes. Sends notification confirming pause. |
| `Resume` | None | Re-enables the lock: immediately sets microphone to 100% unmuted, resumes monitoring. Sends notification confirming resume. |
| `Auto-Start` | Toggle state | Enables/disables automatic launch at Windows sign-in. Persists preference to per-user Registry. |
| `Select Microphone` | Opens device selection UI | Allows the user to choose which microphone input device to lock (or follow default communication device). |
| `Open Window` | None | Opens/brings to foreground the main application window. |
| `Exit` | None | Closes the application completely. |

---

## 3. Settings Persistence Contract

**Purpose**: Defines how application settings are persisted across application restarts and system reboots.

### Persisted Data

| Setting | Type | Persistence Mechanism | Registry/Path Example |
|---------|------|----------------------|----------------------|
| Selected microphone device | `guid` + selection mode | Per-user Windows Registry (default hive) | `HKCU\Software\MicMax\DeviceGUID` |
| Lock state (active/paused/off) | `bool` | Per-user Windows Registry | `HKCU\Software\MicMax\LockActive` |
| Auto-start preference | `bool` | Per-user Windows Registry `Run` key or custom key | `HKCU\Software\MicMax\AutoStart` |
| Selected device mode | `enum` (ExplicitDevice/FollowDefault) | Per-user Windows Registry | `HKCU\Software\MicMax\DeviceMode` |

### Read Operations (on app startup)

1. Read `DeviceGUID` from Registry — if present and device is available, select it; otherwise fall back to default communication device.
2. Read `LockActive` from Registry — if `true`, activate the lock on the selected device.
3. Read `AutoStart` from Registry — configure auto-start shortcut/Registry value accordingly.
4. Read `DeviceMode` from Registry — determine whether to follow default communication device or use explicit selection.

### Write Operations (on setting changes)

1. Write `DeviceGUID`, `LockActive`, `AutoStart`, `DeviceMode` to per-user Registry on every change.
2. Atomic writes to avoid corruption on crash during write.
3. On app exit, persist current state to Registry.

---

## 4. Notification Contract

**Purpose**: Defines the Windows notifications the application sends for user feedback.

| Event | Notification Content | Behavior |
|-------|---------------------|----------|
| `Lock activated` | "MicMax: Microphone locked at 100%" | Standard Windows toast/balloon notification, respects Focus Assist/DND |
| `Lock deactivated` | "MicMax: Microphone unlocked" | Standard Windows toast/balloon notification |
| `Lock paused` | "MicMax: Microphone lock paused" | Standard Windows toast/balloon notification |
| `Lock resumed` | "MicMax: Microphone lock resumed" | Standard Windows toast/balloon notification |
| `Device switch (fallback)` | "MicMax: Switched to {device name} — microphone locked at 100%" | Standard Windows toast/balloon notification, triggered when locked device becomes unavailable and default communication device is locked instead |
| `Device restored` | "MicMax: Restored lock to {device name}" | Standard Windows toast/balloon notification, triggered when originally locked device returns |

### Constraints

- Notifications must respect Windows Focus Assist / Do Not Disturb settings
- No notification bypass or priority override intended
- Notifications should be throttled/spammed avoided (per FR-016 and SC-016 edge cases)

---

## Summary

The contracts documented here define the essential interfaces between MicMax's components and its interactions with the Windows operating system. These contracts are internal to the application and support maintainability, testing, and future refactoring. No external APIs or service contracts are exposed by this project.