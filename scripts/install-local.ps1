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
# v1 profile the whole time it is being built.
#
# The reason for that changed on 2026-09-07 and the behaviour deliberately did not. It
# used to be that installing an Evolved build would REPLACE v1 in place — one AppId, one
# {autopf}\EQBuddy — and inherit its settings.json, history.db and archives. TR-2 closed
# that: installer\EQBuddyEvolved.iss has its own AppId, its own install directory and its
# own shortcut, so the "heavier version" this comment used to name as a future move now
# exists and installs BESIDE v1. What keeps this loop portable is what it is FOR: a fast
# build-and-run against an isolated profile with the shell's review door open, which is a
# smoke rather than a deployment. Switching David's Evolved testing to an installed copy
# is the daily-driver call, and it is his; nothing here presumes it.
#
# -Evolved also opens the SHELL (EQBUDDY_SHELL=1), because a local Evolved smoke that does
# not show the thing E-3 is building is a smoke of the half that has not changed. It is a
# review door, not a player one, and it is set only on this branch — see the launch block
# below. scripts\Launch-Evolved-Shell.cmd re-opens the same portable copy without rebuilding.
#
#   pwsh scripts\install-local.ps1 -Evolved
param([switch] $Evolved, [switch] $SelfTest)
$ErrorActionPreference = 'Stop'

# The single-instance key. SingleInstance.LockFileName is the one spelling in
# C#; install-local-selftest.ps1 reads that constant and refuses a drift.
$script:EqInstanceLockName = 'instance.lock'

function Initialize-EqProfileLock {
    if ('EqProfileLock' -as [type]) { return }
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class EqProfileLock
{
    const int CCH_RM_MAX_APP_NAME = 255;
    const int CCH_RM_MAX_SVC_NAME = 63;
    const int ERROR_MORE_DATA = 234;

    public enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, IntPtr rgApplications, uint nServices, IntPtr rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
        ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint lpdwRebootReasons);

    public static int[] PidsHolding(string path)
    {
        uint handle;
        string key = Guid.NewGuid().ToString("N").Substring(0, 16);
        int err = RmStartSession(out handle, 0, key);
        if (err != 0) throw new InvalidOperationException("RmStartSession " + err);
        try
        {
            err = RmRegisterResources(handle, 1, new[] { path }, 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (err != 0) throw new InvalidOperationException("RmRegisterResources " + err);
            uint needed = 0, count = 0, reasons = 0;
            err = RmGetList(handle, out needed, ref count, null, ref reasons);
            if (err == ERROR_MORE_DATA)
            {
                var arr = new RM_PROCESS_INFO[needed];
                count = needed;
                err = RmGetList(handle, out needed, ref count, arr, ref reasons);
                if (err != 0) throw new InvalidOperationException("RmGetList " + err);
                var pids = new int[count];
                for (int i = 0; i < count; i++) pids[i] = arr[i].Process.dwProcessId;
                return pids;
            }
            if (err != 0) throw new InvalidOperationException("RmGetList " + err);
            return new int[0];
        }
        finally
        {
            RmEndSession(handle);
        }
    }
}
'@
}

# Who is holding this profile's instance.lock. That file is the key
# SingleInstance.TryClaim takes with FileShare.None; Restart Manager is how
# this script turns the file into the process to close. A stale lock file
# left after a crash names no process, which is the same answer as a free lock.
function Get-EqProcessesHoldingProfile {
    param([Parameter(Mandatory)][string] $ProfileDir)
    $lock = Join-Path $ProfileDir $script:EqInstanceLockName
    if (-not (Test-Path -LiteralPath $lock)) { return @() }
    Initialize-EqProfileLock
    $ids = @([EqProfileLock]::PidsHolding($lock))
    $found = @()
    foreach ($procId in $ids) {
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($null -eq $proc -or $proc.HasExited) { continue }
        if ($proc.ProcessName -ne 'EQBuddy') { continue }
        $found += $proc
    }
    return $found
}

# CloseMainWindow, then wait, then force. EQBuddy finalizes its session into
# history.db on exit; force is only the fallback when the window did not close.
function Close-EqBuddyGracefully {
    param([Parameter(Mandatory)][System.Diagnostics.Process[]] $Processes)
    foreach ($p in @($Processes)) {
        if ($null -eq $p -or $p.HasExited) { continue }
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(15000)) { $p | Stop-Process -Force }
    }
}

# Prove-fail for the close above. Does not build, sign, or launch the product.
# The sourced test sets $script:Dra169Result; an exit inside it returns here.
if ($SelfTest) {
    $script:Dra169Result = 1
    . "$PSScriptRoot\install-local-selftest.ps1"
    exit $script:Dra169Result
}

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
# Under -Evolved, close whichever EQBuddy holds this profile's instance.lock.
# The lock is what the next launch also keys on (SingleInstance): a copy running
# from any other directory on the Evolved profile used to survive a path check
# against dist\publish, keep the lock, and make the freshly published process
# exit after asking the old one to surface — while this script still printed
# that the new build was running. The installed v1 widget is a different profile,
# so it is not holding this lock and an -Evolved run does not close it.
# Without -Evolved the close set is still every EQBuddy, as before.
# Nothing here copies the build to %LOCALAPPDATA%\EQBuddy Evolved\publish; the
# launch below is still the portable exe in dist\publish.
$publishDir = "$repo\dist\publish"
if ($Evolved) {
    $running = @(Get-EqProcessesHoldingProfile -ProfileDir $evolvedProfile)
} else {
    $running = @(Get-Process EQBuddy -ErrorAction SilentlyContinue)
}
if ($running) {
    Write-Host $(if ($Evolved) { 'Closing the EQBuddy holding the Evolved profile (gracefully, so it finalizes its session)' }
                 else { 'Closing the running EQBuddy (gracefully, so it finalizes its session)' })
    Close-EqBuddyGracefully -Processes $running
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

# ---- the 1.x install loop is not on this tree --------------------------------------
#
# Everything above returns or throws: 2.x needs -Evolved (and -Evolved returns), and
# -Evolved on a 1.x tree is refused. What used to be here was the v1 loop - compile
# installer\EQBuddy.iss, sign EQBuddySetup.exe, /SILENT it - and both of those names are
# the v1 line's, which is finished and lives on `legacy-v1` with its own copy of this
# script and its own installer (the same reasoning that let E-2c delete release-assets.yml
# from the mainline: a tag builds from the tag's tree). Leaving the block here after TR-2
# renamed the .iss would have left a script pointing at a file that does not exist, which
# is trap 53 exactly - a stale name in a harness, with no compiler to catch it.
#
# The throw is unreachable on this tree and is here anyway, because falling off the end of
# a script that says it installs is a silent no-op.
throw "EQBuddy $version is 1.x and this is the Evolved mainline: the v1 install loop lives on the ``legacy-v1`` branch, with the v1 installer. Nothing was installed."
