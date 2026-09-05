# Build the current tree and silently install it on THIS machine only — the standing
# loop for testing changes before a release (David's rule: everything gets field-tested
# locally first). Touches neither OneDrive nor GitHub; the family keeps whatever
# release.ps1 last shipped. The updater won't fight it: it only ever offers NEWER
# versions than the one running.
#
#   pwsh scripts\install-local.ps1
#
# -Evolved is the 2.x loop, and it does NOT install. EQBuddy Evolved is under
# construction: it builds and signs the same way, then runs PORTABLE out of dist\publish
# against its own profile directory, so David keeps a working v1 install and an untouched
# v1 profile the whole time it is being built. That matters because the installer uses one
# AppId and {autopf}\EQBuddy — installing an Evolved build would REPLACE v1 in place and
# inherit its settings.json, history.db and archives, and #158's EQBuddy.previous.exe
# rollback gives back the binary, not the profile. It is DATA-003's intent arriving before
# there is anything destructive to back up, and it costs one switch.
#
# The heavier version — a second AppId, an "EQBuddy Evolved" install directory and its own
# shortcut — is the right move when Evolved becomes the daily driver. Named here so the
# next session does not re-derive it.
#
# -Evolved also opens the SHELL (EQBUDDY_SHELL=1), because a local Evolved smoke that does
# not show the thing E-3 is building is a smoke of the half that has not changed. It is a
# review door, not a player one, and it is set only on this branch — see the launch block
# below. scripts\Launch-Evolved-Shell.cmd re-opens the same portable copy without rebuilding.
#
#   pwsh scripts\install-local.ps1 -Evolved
param([switch] $Evolved)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\signing.ps1"

$props = Get-Content "$repo\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]
$major = [int]($version.Split('.')[0])

# Both directions, because both are a mistake with a cost. Installing 2.x over v1 is the
# one-way door above; -Evolved on a 1.x tree would run the released product portable on a
# throwaway profile and look like nothing happened.
if ($major -ge 2 -and -not $Evolved) {
    throw "EQBuddy $version is the Evolved line: it must not be INSTALLED over your v1 install (same AppId, same profile). Pass -Evolved to build, sign and run it portable on its own profile."
}
if ($Evolved -and $major -lt 2) {
    throw "-Evolved is for the 2.x line; $version is 1.x. Run this script with no switch to install it normally."
}

# The Evolved profile. Beside the v1 one and never inside it — EQBUDDY_APPDATA is the
# supported way to run against an isolated profile (AppPaths), and it is what keeps the
# two lines' settings, history and archives apart while both exist on this machine.
$evolvedProfile = Join-Path $env:APPDATA 'EQBuddy Evolved'

Write-Host $(if ($Evolved) { "Building EQBuddy $version (Evolved, local-only: portable, own profile, no install)" }
             else { "Installing EQBuddy $version locally (no release)" })

# Same toolchain as release.ps1, resolved BEFORE the build for the same reason: a broken
# signing setup should cost a second, not a 172 MB publish.
#
# This script used to look for a self-signed cert with "EQBuddy" in its subject and
# silently skip signing when it found none. That certificate — and the script that made
# it — were deleted on 2026-08-19, so from that day every local install has been
# UNSIGNED and nothing said so. That is not a shipping-rule violation (nothing here
# reaches OneDrive, GitHub or the update channel), but it is a testing one: an unsigned
# build is exactly what re-triggered Defender's cloud-ML false positive, so the local
# copy has to carry the same publisher identity as the real one or a local test is
# testing a different artifact from the one players get.
Initialize-EqSigning -Repo $repo

# Gracefully, not Stop-Process -Force. EQBuddy finalizes its session into history.db on
# exit, and the cost of a test build must never be someone's session record — the same
# reason shoot.ps1 stands the app down with CloseMainWindow. Force is the fallback only.
#
# Under -Evolved, only the PORTABLE copy is closed — the one running out of dist\publish,
# which has to go because the publish below overwrites its exe. The installed v1 widget is
# left alone: it is a different binary on a different profile, and closing it would cost a
# session for a build that never touches it. Filtering by path is the whole difference, and
# it is why this asks Path rather than name (both processes are called EQBuddy.exe).
$publishDir = "$repo\dist\publish"
$running = @(Get-Process EQBuddy -ErrorAction SilentlyContinue | Where-Object {
    (-not $Evolved) -or ($_.Path -and $_.Path.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase))
})
if ($running) {
    Write-Host $(if ($Evolved) { 'Closing the running portable Evolved copy (gracefully, so it finalizes its session)' }
                 else { 'Closing the running EQBuddy (gracefully, so it finalizes its session)' })
    foreach ($p in $running) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(15000)) { $p | Stop-Process -Force }
    }
    Start-Sleep -Seconds 1
}

dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$repo\dist\publish"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Sign the app before Inno Setup packages it, so the installer carries a signed payload
# as well as being signed itself. Invoke-EqSign throws on anything short of a verified,
# timestamped signature — no warn-and-continue here either, because "it installed but
# quietly unsigned" is the state this script sat in for a day without anyone noticing.
Invoke-EqSign "$repo\dist\publish\EQBuddy.exe"

# ---- the Evolved loop stops here: run it, do not install it ------------------------
if ($Evolved) {
    New-Item -ItemType Directory -Force $evolvedProfile | Out-Null

    # Set on THIS process so the child inherits them; restored afterwards so nothing else
    # this shell does inherits a redirected profile — or an auto-opening shell — by accident.
    #
    # EQBUDDY_SHELL=1 is THE REVIEW DOOR, and it is set HERE and nowhere else on purpose.
    # The Evolved shell has no player-facing entry point yet (ShellHost says why, and
    # DECISIONS.md logs it): its rail draws five rooms of a planned seven, so a menu entry
    # into it would be the unexplained-empty the Phase 2 gate forbids. But a surface nobody
    # can reach reads as reviewed anyway (trap 22), and this script is the ONLY way David
    # runs an Evolved build — so a local -Evolved smoke that does not open the shell is a
    # local smoke of the half of Evolved that has not changed.
    #
    # It rides the local-only switch that already exists rather than becoming a new one:
    # the branch below is the same one that refuses to install, refuses to touch OneDrive
    # and refuses to touch the v1 profile. The installed and released builds go through
    # neither this branch nor this variable and are untouched by it.
    $previousProfile = $env:EQBUDDY_APPDATA
    $previousShell = $env:EQBUDDY_SHELL
    try {
        $env:EQBUDDY_APPDATA = $evolvedProfile
        $env:EQBUDDY_SHELL = '1'
        Start-Process "$repo\dist\publish\EQBuddy.exe" -WorkingDirectory "$repo\dist\publish"
    }
    finally {
        $env:EQBUDDY_APPDATA = $previousProfile
        $env:EQBUDDY_SHELL = $previousShell
    }

    Write-Host ''
    Write-Host "EQBuddy Evolved $version is running, PORTABLE, from $repo\dist\publish" -ForegroundColor Cyan
    Write-Host "  profile:   $evolvedProfile   (v1's %AppData%\EQBuddy is untouched)" -ForegroundColor Cyan
    Write-Host '  shell:     open — EQBUDDY_SHELL=1, the local review door. Installed and released builds never set it.' -ForegroundColor Cyan
    Write-Host '  installed: nothing. Your v1 install, its shortcut and its profile are exactly as they were.' -ForegroundColor Cyan
    Write-Host '  signed:    yes — same certificate, same verification as a release build.' -ForegroundColor Cyan
    # No unins000.exe beside a portable exe, so UpdateChecker.IsInstalledCopy is false and
    # the update banner offers the release page rather than installing over anything (#119).
    Write-Host '  updates:   portable copies are never auto-installed over; the banner links out instead.' -ForegroundColor Cyan
    return
}

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "/DAppVersion=$version" "$repo\installer\EQBuddy.iss" | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { throw 'installer compile failed' }
Invoke-EqSign "$repo\dist\EQBuddySetup.exe"

Start-Process "$repo\dist\EQBuddySetup.exe" -ArgumentList '/SILENT'
Write-Host "Installer launched (/SILENT); EQBuddy relaunches itself when it finishes."
