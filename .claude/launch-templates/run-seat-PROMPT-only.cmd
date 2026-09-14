@echo off
rem ============================================================================
rem  Experiment A' on EQBuddy (the lab) — PROMPT.txt only. Not a Corps standard.
rem
rem  Claim the work item FIRST (CLAUDE.md trap 70). A second default seat on the
rem  same card is refused here, before anyone starts claude.exe.
rem
rem  <work-item> is the PAPERCLIP CARD ID, DRA-<n>, and only that. A bare GitHub
rem  issue number is refused by claim-seat.ps1 with the reason: #445 and DRA-28
rem  are two names for one scope, and two claims under two spellings collide
rem  with neither. Nothing here maps a number onto a card.
rem
rem  NEVER writes HELM-FEEDBACK.md / HELM.md / *-FEEDBACK.md. Kick text goes in
rem  the seat-local PROMPT.txt. Helm #428 / trap 60: a launcher that rewrites a
rem  mailbox is a silent history wipe.
rem
rem  Usage:
rem    run-seat-PROMPT-only.cmd DRA-<n> <seat-id> [mode]
rem    mode = active (default) | challenger | disjoint | replacement
rem
rem  Example:
rem    run-seat-PROMPT-only.cmd DRA-28 opus-isolation
rem ============================================================================
setlocal

if "%~1"=="" goto :usage
if "%~2"=="" goto :usage

set "WORK_ITEM=%~1"
set "SEAT_ID=%~2"
set "MODE=%~3"
if "%MODE%"=="" set "MODE=active"

set "REPO=%~dp0..\.."
for %%I in ("%REPO%") do set "REPO=%%~fI"

set "WORKTREE=%REPO%\.claude\worktrees\%SEAT_ID%"
set "PROMPT=%WORKTREE%\PROMPT.txt"

pwsh -NoProfile -File "%REPO%\scripts\claim-seat.ps1" -WorkItem "%WORK_ITEM%" -SeatId "%SEAT_ID%" -Mode "%MODE%" -Worktree "%WORKTREE%"
if errorlevel 1 exit /b 1

if not exist "%WORKTREE%" mkdir "%WORKTREE%"

echo.
echo   Claim recorded for %WORK_ITEM% / %SEAT_ID% (%MODE%).
echo   Kick text goes ONLY in:
echo       %PROMPT%
echo   Do not write HELM-FEEDBACK.md or any mailbox from this launcher.
echo.
if not exist "%PROMPT%" (
    echo   PROMPT.txt is not there yet — create it, then start the executor
    echo   from that worktree. This file will not invent one.
    echo.
)
exit /b 0

:usage
echo Usage: run-seat-PROMPT-only.cmd DRA-^<n^> ^<seat-id^> [active^|challenger^|disjoint^|replacement]
echo        The work item is the Paperclip card id (e.g. DRA-28), not a GitHub issue number.
exit /b 2
