; ============================================================================
; MicMax — Inno Setup script
;
; Produces a per-user installer (no administrator rights required) from the
; single-file self-contained publish produced by:
;
;   dotnet publish src\MicMax\MicMax.csproj -c Release -r win-x64 `
;       --self-contained true -p:PublishSingleFile=true
;
; Compile with:
;
;   ISCC.exe installer\MicMax.iss
;
; Output: dist\MicMax-Setup-<version>.exe
; ============================================================================

#define MyAppName "MicMax"
; Version can be overridden from the command line (CI sets it from the tag):
;   ISCC.exe /DMyAppVersion=1.2.0 installer\MicMax.iss
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "MicMax Project"
#define MyAppExeName "MicMax.exe"
; Must match AutoStartManager.RunValueName in src\MicMax.Core\Settings\AutoStartManager.cs
#define AutoStartValueName "MicMax"
; Must match the app's settings registry path (HKCU\Software\MicMax).
#define SettingsKeyPath "Software\MicMax"

; Fixed GUID — do not change, it identifies the app across upgrades/uninstalls.
#define MyAppId "{{A7C4E1D3-5B8F-4C2A-9E6D-1F0B7A3C5E94}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} — Microphone Volume Lock installer
VersionInfoProductName={#MyAppName}

; Per-user install: with PrivilegesRequired=lowest, {autopf} resolves to
; {localappdata}\Programs, so no UAC prompt is ever shown.
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; The app is a single self-contained .exe — nothing else to install.
OutputDir=..\dist
OutputBaseFilename=MicMax-Setup-{#MyAppVersion}
SetupIconFile=..\src\MicMax\Assets\MicMax.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
DisableReadyMemo=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; \
    Description: "Iniciar o MicMax automaticamente ao entrar no Windows"; \
    GroupDescription: "Opções adicionais:"; \
    Flags: checkedonce
Name: "desktopicon"; \
    Description: "{cm:CreateDesktopIcon}"; \
    GroupDescription: "{cm:AdditionalIcons}:"; \
    Flags: unchecked

[Files]
; Single-file self-contained publish (includes MicMax.Core and the .NET runtime).
Source: "..\src\MicMax\bin\Release\net8.0-windows\win-x64\publish\MicMax.exe"; \
    DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start under the per-user Run key — the same value the app's own
; "Auto-Start" menu toggle manages (AutoStartManager.RunKeyPath/RunValueName).
; uninsdeletevalue keeps the key clean after uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AutoStartValueName}"; \
    ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

; Per-user app settings (selected mic, lock state). Created here so the
; uninstaller can remove the whole key (uninsdeletekey) even if the app
; never wrote to it.
Root: HKCU; Subkey: "{#SettingsKeyPath}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; \
    Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent

[Code]
// Stop a running MicMax before files are replaced (install) or removed
// (uninstall). taskkill without /F asks the app to close gracefully; the
// /F pass is the fallback because a tray app with no visible window may
// never process WM_CLOSE.
procedure StopMicMax();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#MyAppExeName}',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName}',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopMicMax();  // before any files are copied over
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopMicMax();  // close the tray app before deleting it
end;
