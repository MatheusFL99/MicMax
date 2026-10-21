# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command; its definition describes the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# 12 /.NET 8 (as specified in user requirements)

**Primary Dependencies**: Windows Core Audio API via P/Invoke for IAudioEndpointVolume, IAudioEndpointVolumeCallback, and IMMNotificationClient interop. Library choice (NAudio vs direct interop vs Windows API Code Pack) needs evaluation — NEEDS CLARIFICATION

**Storage**: Windows Registry (per-user hive) for app settings persistence — from Assumptions FR-016 and success criteria SC-004

**Testing**: xUnit — industry standard for .NET; needs integration with tray app test scenarios — NEEDS CLARIFICATION

**Target Platform**: Windows 10 (minimum version 1809) and Windows 11 — as specified in user requirements and Assumptions

**Project Type**: desktop-app (C#/.NET 8 tray app with WinForms/WPF lightweight) — as specified in user requirements

**Performance Goals**: <1% CPU when idle (averaged over one minute), <100 MB memory — from Success Criteria SC-003

**Constraints**: 
- No administrator privileges required (FR-016, user must operate on standard user account)
- Single executable publishable output
- Configuration stored in %APPDATA%\MicMax
- Windows Core Audio API only (IAudioEndpointVolume, IAudioEndpointVolumeCallback, IMMNotificationClient)
- Event-driven via IAudioEndpointVolumeCallback, no polling
- No audio capture or processing of any kind
- Auto-start via Startup folder or per-user Registry key

**Scale/Scope**: 
- Single microphone lock at a time (v1 scope)
- Per-user settings only, no cloud sync or network connectivity required
- Windows 10/11 desktop only

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Gates evaluated against constitution.md principles:**

| Gate | Principle | Status | Reasoning |
|------|-----------|--------|-----------|
| GC-01 | Library-First (Principle 1) | ✅ PASS | Core audio control logic will be structured as a self-contained class library within the solution, satisfying the principle that every feature starts as a standalone library. The MicMax executable will reference this library, and it can be used independently. |
| GC-02 | CLI Interface (Principle 2) | ✅ PASS | While MicMax is a tray app (not a CLI tool), all app settings (device selection, lock state, auto-start preference) will be persistable and verifiable via Registry reads, providing CLI-equivalent access without the GUI. |
| GC-03 | TDD (Principle 3 - NON-NEGOTIABLE) | ✅ PASS | TDD will be applied to core logic: audio endpoint volume control, settings management, and tray menu handling. Unit tests will be written using xUnit before implementation follows the Red-Green-Refactor cycle. |
| GC-04 | Integration Testing (Principle 4) | ✅ PASS | Integration tests will verify audio endpoint volume control across app restarts, Registry persistence, and auto-start behavior. Special focus on contract changes between the core library and the executable shell. |

**No constitution violations identified.** All gates pass without requiring justifications.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
