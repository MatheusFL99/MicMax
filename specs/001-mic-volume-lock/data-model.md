# Data Model: MicMax — Microphone Volume Lock

## Entities

### 1. AudioInputDevice

Represents a microphone input device available to the Windows audio system. Identified persistently across sessions using the **Windows audio endpoint GUID**.

| Field | Type | Description |
|-------|------|-------------|
| `GUID` | `guid` | Unique identifier for the audio endpoint. Provides stable identification across renames, driver updates, and dock/undock scenarios. Primary persistent identifier. |
| `DisplayName` | `string` | Human-readable name of the microphone device as exposed by Windows. |
| `InputLevel` | `int` | Current input level percentage (0-100). Set to 100 when lock is active. |
| `MuteState` | `bool` | Current mute state. `false` = unmuted, `true` = muted. Set to `false` when lock is active. |
| `IsAvailable` | `bool` | Whether the device is currently available/active on the system. |
| `IsDefaultCommunication` | `bool` | Whether this device is the Windows default communication recording device. |
| `DeviceCategory` | `enum` | `DefaultCommunication`, `ExplicitlySelected`, `Fallback`. Tracks how the device was selected. |

### 2. LockState

Represents the user's intent regarding the microphone lock for a session.

| Field | Type | Description |
|-------|------|-------------|
| `IsActive` | `bool` | Whether the lock is currently active. `true` = locking enabled, `false` = locking disabled/paused. |
| `WasActiveBeforePause` | `bool` | Saves the active state before a pause, so resume restores to 100% unmuted (per Assumptions in spec.md). |
| `LastActivationTime` | `datetime` | Timestamp of when the lock was last activated. |
| `LastDeactivationTime` | `datetime` | Timestamp of when the lock was last deactivated or paused. |

### 3. DeviceSelection

Tracks which device the lock applies to and the selection mode.

| Field | Type | Description |
|-------|------|-------------|
| `SelectionMode` | `enum` | `ExplicitDevice`, `FollowDefaultCommunication`. Determines how the locked device is determined. |
| `SelectedDeviceGUID` | `guid` | The GUID of the explicitly selected device, if `SelectionMode` is `ExplicitDevice`. |
| `FallbackDeviceGUID` | `guid` | The GUID of the fallback device (default communication device), if `SelectionMode` is `FollowDefaultCommunication`. |
| `OriginalDeviceGUID` | `guid` | The GUID of the originally locked device, for re-selection after fallback. |

### 4. AppSettings

The combination of device selection, lock state, and auto-start preference; persisted locally per user via the Windows Registry.

| Field | Type | Description |
|-------|------|-------------|
| `SelectedDevice` | `DeviceSelection` | Which device the lock applies to. |
| `LockState` | `LockState` | The user's current lock intent. |
| `AutoStartEnabled` | `bool` | Whether MicMax should launch automatically at Windows sign-in. |
| `LastSavedTime` | `datetime` | Timestamp of when settings were last persisted. |

## Relationships

- **AppSettings** `1:1` **DeviceSelection** — Each app settings instance has exactly one device selection.
- **AppSettings** `1:1` **LockState** — Each app settings instance has exactly one lock state.
- **DeviceSelection** `1:1` **AudioInputDevice** — When a device is explicitly selected, it references the corresponding audio input device.
- **LockState** has no direct relationship to **AudioInputDevice** — the lock state is independent of the specific device's current level/mute state until the lock acts on it.

## State Transitions

### LockState Transitions

| From | To | Trigger | Action |
|------|-----|---------|--------|
| Inactive | Active | User activates lock via tray menu or main window | Set microphone input level to 100%, clear mute state, send notification |
| Active | Paused | User chooses "Pause" from tray menu | Stop reverting external changes; send notification confirming pause |
| Paused | Active | User chooses "Resume" from tray menu | Immediately set microphone to 100% unmuted; send notification confirming lock resumes |
| Active | Inactive | User deactivates lock via tray menu or main window | Stop reverting external changes; send notification confirming deactivation |
| Paused | Inactive | User disables lock completely | Same as Active → Inactive |

### DeviceSelection Transitions

| From | To | Trigger | Action |
|------|-----|---------|--------|
| ExplicitDevice | FollowDefaultCommunication | User changes setting to "follow default communication device" | Save the original device GUID for potential re-lock; start following default communication device |
| FollowDefaultCommunication | ExplicitDevice | User explicitly selects a different microphone | Lock the newly selected device at 100% unmuted; notify user |
| FollowDefaultCommunication | FollowDefaultCommunication | Default communication device changes (device disconnected/reconnected) | Automatically switch to new default; notify user if device changed |
| ExplicitDevice | ExplicitDevice | User keeps same selected device | No change; setting persists |

### AppSettings Transitions

| From | To | Trigger | Action |
|------|-----|---------|--------|
| Default | WithAutoStart | User enables auto-start preference | Persist setting to Registry; on next app launch, auto-start will trigger |
| WithAutoStart | Default | User disables auto-start preference | Remove auto-start Registry key; app will no longer launch at sign-in |
| Default | WithDifferentDevice | User changes selected microphone | Update `SelectedDevice` in Registry; current session retains previous device until restart |
| WithDifferentDevice | Default | App restart | Load updated device selection from Registry |

## Validation Rules

| Rule | Entity | Description |
|------|--------|-------------|
| VR-001 | AudioInputDevice | `GUID` must be a valid Windows audio endpoint GUID format |
| VR-002 | AudioInputDevice | `InputLevel` must be in range 0-100 |
| VR-003 | AudioInputDevice | `MuteState` must be a boolean value |
| VR-004 | LockState | `IsActive` transitions must follow the state transition table |
| VR-005 | DeviceSelection | `SelectedDeviceGUID` must correspond to an available audio input device |
| VR-006 | AppSettings | `AutoStartEnabled` must be persisted in per-user Registry hive |
| VR-007 | AppSettings | At least one of `SelectedDeviceGUID` or `FollowDefaultCommunication` mode must be set |

## Summary

The data model consists of 4 primary entities (`AudioInputDevice`, `LockState`, `DeviceSelection`, `AppSettings`) that together capture all persistent state for the MicMax application. The model is designed to be technology-agnostic (describing what data needs to be stored and managed) rather than prescribing specific implementation details like Registry key paths or database schemas.