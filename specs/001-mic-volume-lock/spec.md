# Feature Specification: MicMax — Microphone Volume Lock

**Feature Branch**: `001-mic-volume-lock`

**Created**: 2026-10-18

**Status**: Draft

**Input**: User description: "Criar um utilitário para Windows chamado MicMax que mantém o volume do microfone fixo em 100%, restaurando automaticamente qualquer alteração feita por outros aplicativos ou pelo sistema. No Windows 11 o volume de entrada do microfone abaixa sozinho (ajuste de 'Comunicações', AGC de apps como Discord/Zoom ou drivers de áudio). O usuário quer 'configurar e esquecer'."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Lock the microphone at 100% and keep it there (Priority: P1)

A user selects their microphone in MicMax (or accepts the Windows default communication microphone) and activates the lock. The microphone input level is immediately set to 100% and unmuted. From then on, whenever any other application, Windows itself, or an audio driver changes that microphone's input level or mute state, MicMax reverts the change within one second — automatically, with no user intervention. The app sits in the system tray and does not block normal computer use.

**Why this priority**: This is the core value proposition. Without an always-on, fast-correcting lock, the product solves nothing. This story alone is a viable MVP.

**Independent Test**: Activate the lock, then manually drag the microphone level slider down in Windows Sound Settings (or in Discord). Verify the level returns to 100% in under one second, repeatedly, for as long as the lock is active.

**Acceptance Scenarios**:

1. **Given** the lock is inactive, **When** the user selects a microphone and activates the lock, **Then** the microphone input level is set to 100% and the mute state is cleared within one second of the action.
2. **Given** the lock is active, **When** any external application or the operating system lowers the microphone input level below 100%, **Then** MicMax restores the level to 100% in under one second.
3. **Given** the lock is active, **When** any external application or the operating system mutes the microphone, **Then** MicMax unmutes the microphone in under one second.
4. **Given** the lock is active, **When** the microphone level is already at 100% and unmuted, **Then** MicMax performs no visible or audible action and consumes negligible resources.
5. **Given** the lock is active, **When** an application repeatedly tries to lower the level (e.g., AGC adjusting several times per second), **Then** the level remains effectively at 100% for the entire duration of normal use.

---

### User Story 2 - Background operation with tray control (Priority: P2)

MicMax runs in the background from the Windows system tray. The user opens the app's window by clicking (or double-clicking) the tray icon. From the tray icon's menu, the user can pause (temporarily disable) and resume the lock, and from the window the user can select the target microphone, toggle the lock, and toggle "start automatically with Windows".

**Why this priority**: "Configure and forget" requires the app to be unobtrusive yet always controllable. Tray presence and pause/resume are needed for real daily use, but the P1 lock behavior is the essential deliverable.

**Independent Test**: Install and launch the app, close the main window, and verify the tray icon remains. Pause the lock from the tray menu, confirm external changes are no longer reverted, then resume and confirm reversion resumes.

**Acceptance Scenarios**:

1. **Given** the app is running, **When** the user closes the main window, **Then** the app keeps running in the tray and the lock keeps working.
2. **Given** the lock is active, **When** the user chooses "Pause" from the tray menu, **Then** the lock stops reverting changes and a notification confirms the pause.
3. **Given** the lock is paused, **When** the user chooses "Resume" from the tray menu, **Then** the microphone is immediately returned to 100% unmuted and a notification confirms the lock is active.
4. **Given** the app is running in the tray, **When** the user clicks the tray icon, **Then** the main window opens (or is brought to the foreground if already open).

---

### User Story 3 - Persist settings and auto-start with Windows (Priority: P2)

The user's choices — selected microphone, whether the lock was active, and the auto-start preference — survive application restarts and computer reboots. When auto-start is enabled, MicMax launches automatically after the user logs into Windows and restores the lock to its previous state without any action.

**Why this priority**: Persistence and auto-start are required for the "configure and forget" promise across reboots. They are secondary only to the lock itself because the app is still useful in a single session without them.

**Independent Test**: Enable the lock and auto-start, then reboot the computer. After login, verify MicMax is running and the lock is active with the microphone at 100%.

**Acceptance Scenarios**:

1. **Given** the lock is active and a microphone is selected, **When** the user closes and reopens the app, **Then** the same microphone is selected and the lock is active again.
2. **Given** auto-start is enabled, **When** the user logs into Windows, **Then** MicMax launches automatically and restores the lock to its previous state.
3. **Given** auto-start is disabled, **When** the user logs into Windows, **Then** MicMax does not launch automatically.
4. **Given** the user changes settings, **When** the app is restarted, **Then** all settings (device, lock state, auto-start) match the last saved state.

---

### User Story 4 - Follow the default microphone when the locked device disappears (Priority: P3)

If the locked microphone is disconnected (unplugged, headset removed), MicMax keeps the lock active and switches to the new Windows default communication microphone, bringing it to 100% unmuted. When the original device returns, MicMax re-locks it. The user is informed of the switch via a notification.

**Why this priority**: This makes the lock robust for laptop/dock/headset users, but the core product still works for users whose device never disconnects.

**Independent Test**: With the lock active, unplug the locked headset and verify MicMax locks the new default microphone within a few seconds. Replug the headset and verify the lock returns to it.

**Acceptance Scenarios**:

1. **Given** the lock is active on a specific microphone, **When** that microphone is disconnected, **Then** MicMax automatically selects the current Windows default communication microphone, locks it at 100% unmuted, and notifies the user.
2. **Given** the lock followed the default microphone after a disconnect, **When** the originally selected microphone is reconnected, **Then** MicMax re-selects it and keeps it locked, with a notification.
3. **Given** the lock is active and the locked device disappears, **When** no input device is available at all, **Then** MicMax keeps the lock state active, waits for an available device, and locks it as soon as one appears.

---

### User Story 5 - Clear feedback via notifications (Priority: P3)

The user receives a standard Windows notification whenever the lock is activated, deactivated (including via pause), or when the app switches to a different microphone because the previous one disappeared.

**Why this priority**: Notifications confirm the app is working and explain automatic device switches, but the lock functions without them.

**Independent Test**: Activate and deactivate the lock and verify a notification appears on each transition.

**Acceptance Scenarios**:

1. **Given** the lock is inactive, **When** the user activates the lock, **Then** a notification confirms the microphone is now locked at 100%.
2. **Given** the lock is active, **When** the user deactivates or pauses the lock, **Then** a notification confirms the lock is off/paused.
3. **Given** the lock is active, **When** the locked device disconnects and MicMax follows the default device, **Then** a notification names the newly locked device.

---

### Edge Cases

- No microphone exists at lock time (no input devices): the app must inform the user and keep trying to lock a device once one appears.
- The locked microphone is removed while another app is actively trying to change levels: MicMax locks the fallback device and corrects it as usual.
- Multiple devices appear/disappear rapidly (docking station scenario): MicMax must settle on the current default communication microphone without crashing or spamming notifications.
- Another application sets the level to a value that the audio driver rounds or clamps: MicMax treats any steady-state value below 100% as a violation and re-corrects.
- The user manually changes the microphone level while the lock is active: this is treated as an external change and is reverted (the user must pause the lock to adjust the level manually).
- Windows audio service restarts or the device is disabled/re-enabled in system settings: MicMax detects the device state change and re-applies the lock when the device is usable again.
- The selected microphone no longer exists after reboot (renamed/removed): MicMax falls back to the default communication microphone and keeps the lock.
- User is not an administrator / standard user account: the app must work for the current user's audio devices without requiring elevated privileges.

## Clarifications

**Session 2026-10-18**

- Q1: What attribute should serve as the primary persistent identifier for microphone devices across application restarts and system reboots? → A: Windows audio endpoint GUID
- Q2: What is the preferred mechanism for persisting app settings across application restarts and system reboots? → A: Windows Registry (per-user hive)
- Q3: Should MicMax emit structured logs, Windows Event Log entries, both, or neither for lock events and correction actions? → A: Both structured logs + Windows Event Log
- Q4: What is the minimum supported Windows version for MicMax? → A: Windows 10 (minimum version 1809)
- Q5: When the locked device is unavailable and no suitable fallback device can be found, what should MicMax do? → A: Lock the Windows default communication recording device

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow the user to select which microphone input device is locked; by default it MUST target the Windows default communication recording device.
- **FR-002**: When the lock is activated, the system MUST immediately (within one second) set the selected device's input level to 100% and clear its mute state.
- **FR-003**: While the lock is active, the system MUST detect any change to the locked device's input level or mute state made by any other application, the operating system, or a driver, and MUST revert it to 100% unmuted within one second.
- **FR-004**: While the lock is active, the system MUST NOT permit a sustained input level below 100% or a sustained muted state on the locked device.
- **FR-005**: The application MUST run in the background with a presence in the Windows system tray, and MUST NOT require its window to be open for the lock to function.
- **FR-006**: The user MUST be able to open the main window from the tray icon.
- **FR-007**: The user MUST be able to pause (temporarily disable) and resume the lock from the tray icon's context menu.
- **FR-008**: The user MUST be able to enable or disable automatic launch with Windows sign-in (auto-start).
- **FR-009**: The system MUST persist the selected device, the lock state (active vs. paused/off), and the auto-start preference across application restarts and system reboots.
- **FR-010**: When auto-start is enabled and the user signs in, the application MUST launch automatically and restore the persisted lock state without user intervention.
- **FR-011**: The system MUST display a Windows notification when the lock is activated and when it is deactivated or paused.
- **FR-012**: When the locked device becomes unavailable while the lock is active, the system MUST automatically switch to the current Windows default communication recording device, lock it at 100% unmuted, keep the lock active, and notify the user.
- **FR-013**: When the originally selected device becomes available again after a fallback, the system MUST re-select it and keep it locked.
- **FR-014**: If no input device is available while the lock is requested/active, the system MUST keep the lock state pending and apply it to the first available suitable device.
- **FR-015**: The system MUST NOT record, store, transmit, or process audio content; it MUST ONLY read and set the device's volume level and mute state.
- **FR-016**: The application MUST operate on a standard user account without requiring administrator privileges.
- **FR-017**: The system MUST work correctly when external applications change the level or mute state many times per second, without instability or notification spam.

### Key Entities

- **Audio Input Device (Microphone)**: A recording endpoint available to the system; identified persistently across sessions using the **Windows audio endpoint GUID** so the selection survives reboots and reconnects; has attributes: identity (GUID), display name, input level, mute state, availability. The GUID provides stable identification across renames, driver updates, and dock/undock scenarios.
- **Lock State**: The user's intent for a session: active or paused/off; persisted across restarts.
- **Device Selection**: Which device the lock applies to — either an explicitly chosen device or the "follow default communication device" mode; persisted across restarts.
- **App Settings**: The combination of device selection, lock state, and auto-start preference; persisted locally per user via the Windows Registry.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the lock active, the microphone input level remains at 100% and unmuted for at least 24 hours of typical use (video calls, chat apps, games) with zero manual interventions.
- **SC-002**: Any external change to the locked microphone's level or mute state is reverted in less than 1 second in at least 99% of observed correction events.
- **SC-003**: While idle with the lock active and no external changes occurring, the application consumes less than 1% CPU (averaged over one minute) and less than 100 MB of memory.
- **SC-004**: With auto-start enabled, the application is running with the lock active within 60 seconds of Windows sign-in, with no user action.
- **SC-005**: When the locked microphone is unplugged while the lock is active, a different default microphone is at 100% unmuted within 10 seconds, and the lock continues without user intervention.
- **SC-006**: 95% of users can select a microphone and activate the lock in under 30 seconds on first use, without consulting documentation.
- **SC-007**: The application never records, retains, or transmits any audio data (verified by design review and by observing that the app requests no audio content access).

## Assumptions

- Target platform is **Windows 10 (minimum version 1809)** desktop with at least one audio input device; Windows 11 is also supported; other operating systems are out of scope.
- "100%" means the maximum value of the device's input level as exposed by the system (e.g., 100 on the system's input volume scale).
- "Revert within 1 second" is measured from the moment the external change settles to the moment the level/mute is corrected.
- The default communication recording device is whatever Windows currently designates as the default device for communication use; MicMax follows it in the default mode.
- Only one microphone is locked at a time (single-device lock); multi-device locking is out of scope for v1.
- Notifications use the standard Windows notification system and respect Focus Assist / Do Not Disturb (no bypass required).
- "Start with Windows" means launch at user sign-in for the current user only, not as a system service.
- **App settings (selected device, lock state, auto-start preference) are persisted via the Windows Registry (per-user hive).**
- Persisted settings are stored locally per user account; no cloud sync, accounts, or network connectivity are required.
- The app does not modify system-wide settings such as the Windows "Communications" automatic level-reduction feature itself; it only counteracts its effects on the locked device.
- Pausing the lock means the app stops correcting changes entirely until resumed; it does not restore any previous level when resumed (it sets 100% again on resume).
- **MicMax emits structured logs and Windows Event Log entries for lock events and correction actions** (both log types for operational visibility, debugging, and compliance with SC-003 performance monitoring).
