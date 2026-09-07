; EQBuddy Evolved installer — the 2.x line, and a SEPARATE PRODUCT from EQBuddy 1.x.
;
; This file was installer\EQBuddy.iss until 2026-09-07. It carried v1's AppId and
; {autopf}\EQBuddy, which is why release.ps1 -EvolvedLocal refused to compile it at all:
; a signed 2.0.0 EQBuddySetup.exe is one double-click from replacing the v1 install in
; place and inheriting its profile. TR-2 (FABLE.md §3, signed #399) replaces that
; mitigation with an identity, so the installer can exist and cannot do that.
;
; Four things ARE the identity, and every one of them is asserted from this file's text
; by scripts\evolved-channel-guard.ps1:
;
;   1. A NEW AppId. Inno keys the install, the uninstall entry and the upgrade-in-place
;      decision on it, so a different AppId is the whole of "this never replaces v1".
;   2. {autopf}\EQBuddy Evolved — beside {autopf}\EQBuddy, never on top of it.
;   3. Its own Start-menu group and its own shortcut names, because two entries reading
;      "EQBuddy" is a dual install the player cannot tell apart.
;   4. OutputBaseFilename=EQBuddyEvolvedSetup. EQBuddySetup.exe is a RESERVED NAME
;      belonging to the v1 line FOREVER: every deployed 1.x updater matches on that
;      name (UpdateChecker.SetupName) and the v1 contract is frozen, so an asset of that
;      name on a 2.x release would be downloaded and run by every Windows v1 install's
;      existing update flow — a major-line replacement with no consent moment. The name
;      IS the identity and only one side of it has a compiler (trap 53).
;
; Dual install is a FEATURE of this decision rather than a defect: the player who tries
; Evolved keeps a working 1.x to fall back to, which is what makes the transition low
; stakes enough to say yes to.
;
; What this file does NOT own: the PROFILE split. An installed Evolved copy reads
; whatever %AppData% directory AppPaths names — that is TR-1 (#403), and this installer
; is only half of the separation until it lands.
#define AppName "EQBuddy Evolved"
; Overridden by scripts\release.ps1 via /DAppVersion=<csproj Version>
#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#define AppPublisher "David Edwards"
#define AppExe "EQBuddy.exe"
; The build being replaced, kept beside the new one so a bad update is recoverable
; without a reinstall (see [Icons] and [Code], discussion #158).
#define PrevExe "EQBuddy.previous.exe"

[Setup]
; NOT v1's {7E1B6A94-3C2D-4B77-9F41-EQBUDDY10000}. The trailing group is spelled out so
; that the two are legible as one deliberate pair rather than as a typo away from each
; other; the leading groups are unrelated on purpose.
AppId={{B3D71F58-6E2A-4C90-A7D4-EQBUDDY20000}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\EQBuddy Evolved
DefaultGroupName=EQBuddy Evolved
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=EQBuddyEvolvedSetup
SetupIconFile=..\src\EQBuddy\Assets\EQBuddy.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
; Stamp the setup exe with the app version so the in-app updater can read it.
VersionInfoVersion={#AppVersion}
; Let silent self-updates close the running widget and relaunch it after. Inno closes
; the applications holding THESE files — the Evolved copy in {app} — and an EQBuddy 1.x
; running out of {autopf}\EQBuddy is not one of them.
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\dist\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; MIT requires the copyright and permission notice to travel with copies.
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Named, not "EQBuddy". A player who installs Evolved beside 1.x gets two Start-menu
; rows, and two rows with one name is a dual install nobody can steer.
Name: "{group}\EQBuddy Evolved"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\EQBuddy Evolved"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; The escape hatch. EQBuddy's updater lives inside the widget, so a build that will
; not open takes the only route to the fix with it — v1.84.0 did exactly that and left
; people reinstalling from GitHub by hand, which a casual player will not find
; (discussion #158, n3cr0nk1tt3n). The previous build is kept beside the new one and
; gets its own shortcut: start it, and you have a working widget whose own updater can
; pull the fix. Only appears once there IS a previous build.
Name: "{group}\EQBuddy Evolved (previous version)"; Filename: "{app}\{#PrevExe}"; \
    Comment: "Starts the build you had before the last update - use this if the current one will not open, then update from inside it."; \
    Check: PreviousBuildKept

[UninstallDelete]
Type: files; Name: "{app}\{#PrevExe}"

[Code]
var
  KeptPrevious: Boolean;

// Runs BEFORE the new files are copied, which is the only moment the outgoing exe
// still exists. A failure here must never block the install: not having a rollback
// shortcut is a far smaller problem than not being able to update at all.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Current: String;
begin
  if CurStep = ssInstall then
  begin
    Current := ExpandConstant('{app}\{#AppExe}');
    if FileExists(Current) then
      KeptPrevious := CopyFile(Current, ExpandConstant('{app}\{#PrevExe}'), False);
  end;
end;

function PreviousBuildKept: Boolean;
begin
  Result := KeptPrevious;
end;

[Run]
; No skipifsilent: silent self-updates must relaunch the widget when done.
Filename: "{app}\{#AppExe}"; Description: "Launch EQBuddy Evolved now"; Flags: nowait postinstall
