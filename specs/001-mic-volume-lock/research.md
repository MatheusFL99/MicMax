# Research: MicMax — Microphone Volume Lock

## Phase 0: Research Tasks

### Task 1: Windows Core Audio API Interop Strategy for .NET 8

**Context**: The application must use Windows Core Audio API via `IAudioEndpointVolume`, `IAudioEndpointVolumeCallback`, and `IMMNotificationClient` for event-driven volume monitoring without polling. Must work on standard user accounts without administrator privileges.

**Objective**: Determine the best approach for P/Invoke interop with Windows Core Audio API in .NET 8 C#.

**Options Considered**:

1. **Direct P/Invoke** — Directly invoke the Windows Core Audio APIs (`audioendpointvolume.h`, `mmdeviceapi.h`) using `DllImport` and custom structs. Provides full control but requires significant interop boilerplate, especially for `IAudioEndpointVolumeCallback` which requires a custom class implementing the interface with STA threading.

2. **NAudio library** — Popular .NET audio library. Need to evaluate whether NAudio exposes `IAudioEndpointVolume` and supports event-driven callbacks, or if it wraps the APIs in a way that limits event-driven capabilities.

3. **Windows API Code Pack for .NET** — Microsoft's maintained library for Windows API interop. Provides higher-level wrappers around Core Audio APIs, potentially reducing interop complexity. Need to check licensing and .NET 8 compatibility.

**Decision**: Research needed to determine the optimal approach balancing interop complexity, event-driven callback support, and .NET 8 compatibility.

### Task 2: Tray Application Pattern for .NET 8 (WinForms vs WPF Lightweight)

**Context**: The application must run in the Windows system tray with a context menu for pause/resume, auto-start toggle, and window activation. Must be a single executable publishable output.

**Objective**: Determine the best UI framework (WinForms vs lightweight WPF) for the tray application.

**Options Considered**:

1. **WinForms** — Mature, lightweight, minimal footprint. Native tray icon support via `NotifyIcon` component. Smaller assembly size. Less modern UI capabilities but sufficient for a tray app.

2. **WPF (lightweight)** — More modern UI, data binding, and styling capabilities. Requires `WpfApp` project type but can be published as single executable with .NET 8. Slightly larger footprint but offers better customization for the context menu and window.

**Decision**: Research needed to evaluate trade-offs between WinForms simplicity vs WPF modernity for this specific use case.

### Task 3: xUnit Testing Strategy for Windows Tray App

**Context**: The application must be testable with xUnit, the industry standard for .NET. Need to design testable core logic that can be unit tested while encapsulating Windows-specific interop.

**Objective**: Define the testing strategy that enables xUnit unit tests for the core audio control logic and tray app functionality.

**Key Testing Areas**:
- Audio endpoint volume control (set level, get level, get mute state)
- Settings persistence/reading from Windows Registry
- Tray menu command handling (pause, resume, auto-start toggle)
- Notification sending via Windows Toaster or WPF notifications

**Approach**: Extract core logic into a class library project that can be tested independently of the UI framework. Use mocking for Windows API calls where possible. Integration tests will verify the full tray app behavior.

### Task 4: Windows Registry Persistence for App Settings

**Context**: App settings (selected microphone, lock state, auto-start preference) must persist across application restarts and system reboots, stored per-user via Windows Registry.

**Objective**: Determine the Registry hive/keys pattern for persisting MicMax settings.

**Areas to Research**:
- Per-user Registry hive (HKEY_CURRENT_USER vs HKEY_LOCAL_MACHINE)
- Recommended key structure under `Software\MicMax` or `%APPDATA%\MicMax`
- Values for: selected device GUID, lock state (active/paused/off), auto-start preference (bool)
- Atomic write patterns to avoid corruption
- Reading settings on app startup

### Task 5: Windows Notifications System for Tray App

**Context**: The user must receive standard Windows notifications for lock activation, deactivation/pause, and device switches.

**Objective**: Determine the best approach for sending Windows notifications from a .NET 8 tray app.

**Options Considered**:
1. **WPF Toasts** — Using `ToastNotifications` library or built-in WPF toast support.
2. **WinForms Balloon Tips** — `NotifyIcon.ShowBalloonTip()` for simple notifications.
3. **Windows Toaster** — .NET library for Windows 10+ toast notifications with rich formatting.
4. **Shell NotifyIcon** — Basic balloon tips with limited formatting.

**Decision**: Research needed to choose the appropriate notification mechanism that works on both Windows 10 and 11, respects Focus Assist/Do Not Disturb, and provides adequate information.

---

## Consolidated Decisions (to be filled after research)

### Decision: Windows Core Audio API Interop Strategy
- **Rationale**: [To be determined after research]
- **Alternatives Considered**: 
  - Direct P/Invoke (full control, high complexity)
  - NAudio library (may not support event-driven callback pattern)
  - Windows API Code Pack (Microsoft maintained, may have licensing/dependency requirements)

### Decision: Tray Application Framework
- **Rationale**: [To be determined after research]
- **Alternatives Considered**:
  - WinForms (simpler, smaller footprint)
  - WPF (more modern, better styling, slightly larger footprint)

### Decision: xUnit Testing Strategy
- **Rationale**: [To be determined after research]
- **Approach**: Extract core logic into class library, use mocking for API calls, unit test core logic, integration test full app behavior

### Decision: Registry Persistence Pattern
- **Rationale**: [To be determined after research]
- **Pattern**: [To be determined after research]

### Decision: Notification Mechanism
- **Rationale**: [To be determined after research]
- **Approach**: [To be determined after research]