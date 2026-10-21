---

description: "Task list template for feature implementation"
---

# Tasks: MicMax — Microphone Volume Lock

**Input**: Design documents from `/specs/001-mic-volume-lock/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are included because plan.md (Constitution Check GC-03) mandates a TDD approach: xUnit tests are written and failing before implementation of core logic (audio endpoint volume control, settings management, tray menu handling).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single solution at repository root**: executable app in `src/MicMax/`, testable core class library in `src/MicMax.Core/` (per plan.md gate GC-01 Library-First), tests in `tests/MicMax.Core.Tests/`
- Published executable output: `bin\Release\net8.0\win-x64\publish\MicMax.exe` (per quickstart.md)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution and project structure (MicMax.sln at repository root, src/MicMax.Core/, src/MicMax/, tests/MicMax.Core.Tests/) per Library-First gate GC-01 in specs/001-mic-volume-lock/plan.md
- [X] T002 Initialize .NET 8 projects targeting net8.0-windows with WindowsForms enabled for the executable and xunit + Microsoft.NET.Test.Sdk for tests in src/MicMax.Core/MicMax.Core.csproj, src/MicMax/MicMax.csproj, tests/MicMax.Core.Tests/MicMax.Core.Tests.csproj
- [X] T003 [P] Configure linting and formatting rules in .editorconfig
- [X] T004 Finalize pending research decisions (Core Audio interop strategy: direct P/Invoke vs NAUI; tray framework: WinForms vs WPF; notification mechanism) in specs/001-mic-volume-lock/research.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 [P] Create AudioInputDevice model (endpoint GUID, DisplayName, InputLevel, MuteState, IsAvailable, IsDefaultCommunication, DeviceCategory) in src/MicMax.Core/Models/AudioInputDevice.cs
- [X] T006 [P] Create LockState model (IsActive, WasActiveBeforePause, LastActivationTime, LastDeactivationTime) in src/MicMax.Core/Models/LockState.cs
- [X] T007 [P] Create DeviceSelection model (SelectionMode, SelectedDeviceGUID, FallbackDeviceGUID, OriginalDeviceGUID) in src/MicMax.Core/Models/DeviceSelection.cs
- [X] T008 [P] Create AppSettings model (SelectedDevice, LockState, AutoStartEnabled, LastSavedTime) in src/MicMax.Core/Models/AppSettings.cs
- [X] T009 Implement Windows Core Audio P/Invoke interop layer (IMMDeviceEnumerator, IAudioEndpointVolume, endpoint GUID identification) in src/MicMax.Core/Audio/CoreAudioInterop.cs
- [X] T010 Implement IAudioEndpointVolumeCallback bridge for event-driven change notifications with no polling in src/MicMax.Core/Audio/AudioVolumeCallback.cs (depends on T009)
- [X] T011 Implement audio device enumeration (default communication device lookup, device list, availability checks) in src/MicMax.Core/Audio/AudioDeviceEnumerator.cs (depends on T009)
- [X] T012 [P] Configure structured logging and Windows Event Log writer in src/MicMax.Core/Logging/EventLogger.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Lock the microphone at 100% and keep it there (Priority: P1) 🎯 MVP

**Goal**: Select a microphone (or accept the Windows default communication microphone), set it to 100% unmuted, and automatically revert any external change to level or mute state within one second.

**Independent Test**: Activate the lock, then manually drag the microphone level slider down in Windows Sound Settings (or Discord). Verify the level returns to 100% and unmuted in under one second, repeatedly, for as long as the lock is active.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (per plan.md GC-03 TDD mandate)

- [X] T013 [P] [US1] Contract test for Core Audio Control contract operations (SetVolumeTo100Percent, GetCurrentVolume, GetCurrentMuteState, SetMuteToUnmuted, MonitorVolumeChanges, StopMonitoring) with a mocked endpoint in tests/MicMax.Core.Tests/Contracts/CoreAudioControlContractTests.cs
- [X] T014 [P] [US1] Unit test for reverting external level/mute changes within one second, including no-op when already at 100% unmuted, in tests/MicMax.Core.Tests/VolumeLockServiceTests.cs

### Implementation for User Story 1

- [X] T015 [US1] Implement VolumeLockService implementing the Core Audio Control contract (set 100%, unmute, event-driven correction within 1 second, negligible cost when already locked) in src/MicMax.Core/Services/VolumeLockService.cs (depends on T013, T014)
- [X] T016 [US1] Create minimal verification harness in src/MicMax/Program.cs that locks the default communication device and keeps running for manual validation of acceptance scenarios 1-5
- [X] T017 [P] [US1] Add lock activation, deactivation, and correction event entries to structured log and Windows Event Log in src/MicMax.Core/Logging/EventLogger.cs

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently — external changes are reverted to 100% unmuted within one second

---

## Phase 4: User Story 2 - Background operation with tray control (Priority: P2)

**Goal**: Run unobtrusively in the Windows system tray with a context menu (Lock, Pause, Resume, Select Microphone, Open Window, Exit) and a main window opened from the tray icon; the lock keeps working with the window closed.

**Independent Test**: Launch the app, close the main window, and verify the tray icon remains and the lock still corrects changes. Pause from the tray menu, confirm external changes are no longer reverted, then resume and confirm reversion resumes.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (per plan.md GC-03 TDD mandate)

- [X] T018 [P] [US2] Contract test for Tray Menu Command contract (Lock, Pause, Resume, Auto-Start, Select Microphone, Open Window, Exit) in tests/MicMax.Core.Tests/Contracts/TrayMenuContractTests.cs
- [X] T019 [P] [US2] Unit test for LockState transitions per the state transition table (Inactive→Active, Active→Paused, Paused→Active, →Inactive) in tests/MicMax.Core.Tests/LockStateManagerTests.cs

### Implementation for User Story 2

- [X] T020 [US2] Implement LockStateManager enforcing the data-model.md state transitions with WasActiveBeforePause semantics in src/MicMax.Core/Services/LockStateManager.cs (depends on T019)
- [X] T021 [US2] Implement NotifyIcon tray host with context menu wiring Lock/Pause/Resume/Select Microphone/Open Window/Exit commands in src/MicMax/Tray/TrayIcon.cs (depends on T020)
- [X] T022 [US2] Implement main window with available microphone list, lock toggle, and auto-start toggle placeholder in src/MicMax/Windows/MainWindow.cs
- [X] T023 [US2] Configure close-to-tray behavior so the app keeps running and the lock works without the window open in src/MicMax/Program.cs (depends on T016, T021)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently — tray pause/resume controls the lock

---

## Phase 5: User Story 3 - Persist settings and auto-start with Windows (Priority: P2)

**Goal**: Persist selected microphone, lock state, and auto-start preference in the per-user Windows Registry (HKCU\Software\MicMax) so they survive restarts and reboots; launch automatically at sign-in and restore the previous lock state.

**Independent Test**: Enable the lock and auto-start, then reboot the computer. After login, verify MicMax is running and the lock is active with the microphone at 100% unmuted (quickstart.md Scenario 3).

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (per plan.md GC-03 TDD mandate)

- [X] T024 [P] [US3] Contract test for Settings Persistence contract (round-trip of DeviceGUID, DeviceMode, LockActive, AutoStart under HKCU\Software\MicMax) in tests/MicMax.Core.Tests/Contracts/SettingsPersistenceContractTests.cs
- [X] T025 [P] [US3] Unit test for auto-start enable/disable toggle in tests/MicMax.Core.Tests/AutoStartManagerTests.cs

### Implementation for User Story 3

- [X] T026 [US3] Implement RegistrySettingsStore with atomic per-user hive writes for DeviceGUID, DeviceMode, LockActive, AutoStart in src/MicMax.Core/Settings/RegistrySettingsStore.cs (depends on T024)
- [X] T027 [US3] Implement settings load on startup and save on change and app exit in src/MicMax/Program.cs (depends on T026)
- [X] T028 [US3] Implement AutoStartManager toggling the HKCU Run key value for MicMax at user sign-in in src/MicMax.Core/Settings/AutoStartManager.cs (depends on T025)
- [X] T029 [US3] Wire auto-start toggle and device selection persistence to the main window in src/MicMax/Windows/MainWindow.cs (depends on T022, T026, T028)

**Checkpoint**: At this point, User Stories 1, 2, AND 3 work independently — settings and auto-start survive reboot

---

## Phase 6: User Story 4 - Follow the default microphone when the locked device disappears (Priority: P3)

**Goal**: When the locked microphone is disconnected, keep the lock active by switching to the Windows default communication microphone (100% unmuted); re-lock the original device when it returns; hold a pending lock when no device exists. Handle rapid device churn without crashing or notification spam.

**Independent Test**: With the lock active, unplug the locked headset and verify MicMax locks the new default microphone within 10 seconds (SC-005). Replug the headset and verify the lock returns to it.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (per plan.md GC-03 TDD mandate)

- [X] T030 [P] [US4] Unit test for fallback switch on disconnect, original-device re-lock on return, and pending-lock behavior when no device is available in tests/MicMax.Core.Tests/DeviceFallbackTests.cs

### Implementation for User Story 4

- [X] T031 [P] [US4] Implement IMMNotificationClient device change listener (device added, removed, state changed) in src/MicMax.Core/Audio/DeviceNotificationListener.cs
- [X] T032 [US4] Implement DeviceFallbackManager (switch to default communication device on disconnect, re-select original GUID on return, hold pending lock until a device appears) in src/MicMax.Core/Services/DeviceFallbackManager.cs (depends on T030)
- [X] T033 [US4] Integrate DeviceFallbackManager with VolumeLockService and LockStateManager so the lock follows device switches in src/MicMax.Core/Services/VolumeLockService.cs (depends on T032)
- [X] T034 [US4] Handle rapid device churn by settling on the current default communication device without crashing or notification spam in src/MicMax.Core/Services/DeviceFallbackManager.cs (depends on T032)
- [X] T035 [US4] Re-apply the lock after Windows audio service restarts or a device is disabled/re-enabled in src/MicMax.Core/Audio/DeviceNotificationListener.cs (depends on T031)

**Checkpoint**: All user stories 1-4 should now be independently functional

---

## Phase 7: User Story 5 - Clear feedback via notifications (Priority: P3)

**Goal**: Send standard Windows notifications on lock activation, deactivation/pause/resume, and device switches, respecting Focus Assist / Do Not Disturb, without bypass or spam.

**Independent Test**: Activate and deactivate the lock and verify a notification appears on each transition; trigger a device switch and verify the notification names the newly locked device.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (per plan.md GC-03 TDD mandate)

- [X] T036 [P] [US5] Contract test for Notification contract message formats ("locked at 100%", "unlocked", "paused", "resumed", "Switched to {device name}", "Restored lock to {device name}") in tests/MicMax.Core.Tests/Contracts/NotificationContractTests.cs

### Implementation for User Story 5

- [X] T037 [US5] Implement NotificationService (Windows toast/balloon, respects Focus Assist, throttling to avoid spam) in src/MicMax.Core/Notifications/NotificationService.cs (depends on T036)
- [X] T038 [US5] Wire notifications to lock activate/deactivate/pause/resume events in src/MicMax/Tray/TrayIcon.cs (depends on T037, T021)
- [X] T039 [US5] Wire device switch and device restored notifications in src/MicMax.Core/Services/DeviceFallbackManager.cs (depends on T037, T032)

**Checkpoint**: All user stories should now be independently functional with full user feedback

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T040 [P] Create repository README with build, publish, and usage instructions in README.md
- [X] T041 [P] Update quickstart build paths to match solution layout at repository root in specs/001-mic-volume-lock/quickstart.md
- [X] T042 [P] Code cleanup and refactoring across src/MicMax.Core/ and src/MicMax/
- [X] T043 Optimize idle resource usage to meet SC-003 (less than 1% CPU averaged over one minute, less than 100 MB memory) in src/MicMax.Core/Services/VolumeLockService.cs
- [X] T044 [P] Configure single-file self-contained win-x64 publish (PublishSingleFile=true) in src/MicMax/MicMax.csproj
- [X] T045 Verify FR-015 (no audio capture, record, transmit, or processing) and FR-016 (standard user account, no admin privileges) compliance across src/MicMax.Core/ and src/MicMax/
- [X] T046 Run quickstart.md validation scenarios 1-5 in specs/001-mic-volume-lock/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories — this is the MVP
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Builds on US1's VolumeLockService for Pause/Resume but is independently testable once US1 exists; if run in parallel with US1, integrate at T023
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Builds on US2's MainWindow for T029 wiring; the settings store itself (T026-T028) is independent of US1/US2
- **User Story 4 (P3)**: Can start after Foundational (Phase 2) - Integrates with US1's VolumeLockService (T033) and US2's tray state (T021) for full behavior; fallback logic (T030-T032) is independently testable
- **User Story 5 (P3)**: Can start after Foundational (Phase 2) - Wires into US2's tray events (T038) and US4's fallback manager (T039); NotificationService (T037) is independent

### Within Each User Story

- Tests MUST be written and FAIL before implementation (per plan.md GC-03)
- Models before services
- Services before endpoints/UI
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T003)
- All Foundational model tasks marked [P] can run in parallel (T005, T006, T007, T008, T012)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel (T013/T014, T018/T019, T024/T025)
- Cross-story parallel pairs: T030 [US4] with T031 [US4]; T036 [US5] with any other story's tasks
- Different user stories can be worked on in parallel by different team members
- Polish tasks T040, T041, T042, T044 can run in parallel

---

## Parallel Examples per User Story

### User Story 1

```bash
# Launch both tests together first (TDD):
Task: "T013 Contract test for Core Audio Control contract in tests/MicMax.Core.Tests/Contracts/CoreAudioControlContractTests.cs"
Task: "T014 Unit test for reverting external changes in tests/MicMax.Core.Tests/VolumeLockServiceTests.cs"

# After tests fail, implement sequentially: T015 → T016, with T017 in parallel:
Task: "T017 Add lock/correction events to src/MicMax.Core/Logging/EventLogger.cs"
```

### User Story 2

```bash
# Launch both tests together first:
Task: "T018 Contract test for Tray Menu Command contract in tests/MicMax.Core.Tests/Contracts/TrayMenuContractTests.cs"
Task: "T019 Unit test for LockState transitions in tests/MicMax.Core.Tests/LockStateManagerTests.cs"

# Then sequentially: T020 → T021, with T022 in parallel (different files):
Task: "T021 Implement tray host in src/MicMax/Tray/TrayIcon.cs"
Task: "T022 Implement main window in src/MicMax/Windows/MainWindow.cs"
```

### User Story 3

```bash
# Launch both tests together first:
Task: "T024 Contract test for Settings Persistence contract in tests/MicMax.Core.Tests/Contracts/SettingsPersistenceContractTests.cs"
Task: "T025 Unit test for auto-start toggle in tests/MicMax.Core.Tests/AutoStartManagerTests.cs"

# Then in parallel (different files):
Task: "T026 Implement RegistrySettingsStore in src/MicMax.Core/Settings/RegistrySettingsStore.cs"
Task: "T028 Implement AutoStartManager in src/MicMax.Core/Settings/AutoStartManager.cs"
```

### User Story 4

```bash
# Test and listener implementation are independent files - launch together:
Task: "T030 Unit test for fallback behavior in tests/MicMax.Core.Tests/DeviceFallbackTests.cs"
Task: "T031 Implement IMMNotificationClient listener in src/MicMax.Core/Audio/DeviceNotificationListener.cs"

# Then sequentially: T032 → T033, with T034/T035 following their dependencies
```

### User Story 5

```bash
# Test first:
Task: "T036 Contract test for Notification contract in tests/MicMax.Core.Tests/Contracts/NotificationContractTests.cs"

# Then implement:
Task: "T037 Implement NotificationService in src/MicMax.Core/Notifications/NotificationService.cs"
# T038 and T039 depend on T037 and wire into different files (TrayIcon.cs vs DeviceFallbackManager.cs)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently (quickstart.md Scenario 1)
5. Publish/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo (configure-and-forget promise fulfilled)
5. Add User Story 4 → Test independently → Deploy/Demo
6. Add User Story 5 → Test independently → Deploy/Demo
7. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (MVP - must land first)
   - Developer B: User Story 3 settings store (T024-T028 are independent of US1)
   - Developer C: User Story 5 NotificationService (T036-T037 are independent)
3. Stories complete and integrate at their documented integration points (T023, T029, T033, T038, T039)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing (plan.md GC-03 TDD)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- No audio content may ever be captured, recorded, stored, or transmitted (FR-015) — only volume level and mute state
- No administrator privileges required; all Registry writes target HKCU (FR-016)