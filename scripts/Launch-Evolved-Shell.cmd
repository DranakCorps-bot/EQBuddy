@echo off
rem ============================================================================
rem  EQBuddy Evolved — open the already-built portable copy, with the shell.
rem
rem  This is the SECOND HALF of the local review door, and it exists because the
rem  first half only fires at build time. `scripts\install-local.ps1 -Evolved`
rem  publishes, signs and launches; coming back to the shell an hour later meant
rem  re-publishing 172 MB, or remembering two environment variables. Neither is a
rem  door anybody opens twice.
rem
rem  It builds NOTHING and installs NOTHING. If dist\publish\EQBuddy.exe is not
rem  there, run install-local.ps1 -Evolved first — this file deliberately does not
rem  do it for you, because a launcher that quietly starts a 172 MB publish is a
rem  launcher that looks hung.
rem
rem  NOT the player-facing door. The Evolved shell has no menu entry yet (see
rem  ShellHost, and DECISIONS.md for the log): its rail draws five rooms of a
rem  planned seven, and a door into a half-built shell is the unexplained-empty the
rem  Phase 2 gate forbids. EQBUDDY_SHELL is the review hook, the same family as
rem  EQBUDDY_PROGRESS and EQBUDDY_QUESTS, and it is set here and in the -Evolved
rem  branch of install-local.ps1 — nowhere that an installed or released build can
rem  reach.
rem
rem  A .cmd rather than a .ps1 on purpose: this one is double-clicked from Explorer,
rem  where a .ps1 opens Notepad. Everything that has to be REASONED about stays in
rem  PowerShell where the repo's other scripts are.
rem ============================================================================
setlocal

rem The Evolved profile, beside v1's and never inside it — the same path
rem install-local.ps1 derives, from the same %APPDATA% root. Two spellings of one
rem directory would be two profiles, and the second one would look like an EQBuddy
rem that had forgotten everything (trap 4). If that script's $evolvedProfile ever
rem moves, this line moves with it.
set "EQBUDDY_APPDATA=%APPDATA%\EQBuddy Evolved"

rem 1 = open on the shell's own default room. Any page:room address works too
rem (EQBUDDY_SHELL=progress:raids), which is the grammar every navigation path in
rem the shell already takes.
set "EQBUDDY_SHELL=1"

set "EXE=%~dp0..\dist\publish\EQBuddy.exe"
if not exist "%EXE%" (
    echo.
    echo   EQBuddy Evolved is not published yet.
    echo   Run this first, from the repo root:
    echo.
    echo       pwsh -NoProfile -File scripts\install-local.ps1 -Evolved
    echo.
    exit /b 1
)

rem Started from its own folder for the same reason install-local.ps1 does it: the
rem single-file host unpacks beside itself, and a working directory of Explorer's
rem choosing is not something to rely on.
start "" /D "%~dp0..\dist\publish" "%EXE%"
endlocal
