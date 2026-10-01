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
#
# -Evolved -Install is THE DAILY DRIVER (David, 2026-09-29: "I want my version to always be
# the latest dev build ... I'm always running the latest code, live or incremental dev
# builds"). That is the call the paragraph above left to him, made. It builds and signs
# exactly as -Evolved does, then swaps the signed exe INTO the installed EQBuddy Evolved and
# relaunches it on the real profile:
#   * the exe it replaces becomes EQBuddy.previous.exe - the installer's own convention, so
#     the Start-menu "EQBuddy Evolved (previous version)" entry is a one-click rollback to
#     the build before this one;
#   * nothing else in the install directory is touched, so the uninstaller, the AppId and
#     the updater's installed-copy test (unins000.exe beside the exe) are exactly as the
#     release left them;
#   * no EQBUDDY_APPDATA and no EQBUDDY_SHELL: it runs as the player's app, on the player's
#     profile, which is the whole point of a daily driver.
# It refuses when there is no install to swap into - creating one is the signed installer's
# job - and it builds WHATEVER TREE IT IS RUN FROM, so "the latest code" is decided by the
# checkout: `main` for merged work, a local integration branch for work awaiting a smoke.
# The updater cannot fight it: it only offers a release NEWER than the running version, and
# a dev build carries Directory.Build.props' version, which is never behind the last tag.
#
#   pwsh scripts\install-local.ps1 -Evolved -Install
#
# BUILD FIRST, CLOSE LAST (DRA-705 §3). Both modes publish and sign into dist\publish-staged
# while whatever is running keeps running; only a signed build in hand closes the app. This
# used to close BEFORE `dotnet publish`, which is harmless with a person watching and wrong
# once scripts\auto-roll.ps1 runs it unattended: a failed build would leave the Founder with
# no EQBuddy at all. Under -Install the roll then:
#   * relaunches ONLY IF a copy was running - if he had it closed, the next time he opens it
#     is the new build;
#   * requires the relaunched build to still be alive at 20 s, and on a failed swap or an
#     early death copies EQBuddy.previous.exe back and relaunches THAT ("keep the last good
#     build" as one rule);
#   * writes EQBuddy.build.json beside the exe - the commit, its subject, the merged PR, when,
#     and which loop installed it. auto-roll.ps1 reads `pendingCommits` from it to hold off
#     while a manual PR-smoke install is not yet on main (DRA-705 §7).
#
#   pwsh scripts\install-local.ps1 -Evolved -Install -Source autoroll    (auto-roll.ps1 only)
param([switch] $Evolved, [switch] $Install, [switch] $SelfTest,
      [ValidateSet('manual', 'autoroll')][string] $Source = 'manual')
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

    // strSessionKey is an OUTPUT buffer of CCH_RM_SESSION_KEY + 1 WCHARs that Restart Manager
    // writes the key into. It used to be a 16-char managed string, which the marshaller pins
    // and hands over in place, so every call wrote 66 bytes into a 34-byte string and
    // corrupted the GC heap: the process died later ("Internal CLR error 0x80131506") on
    // whatever ran next in it - found when check.ps1 first ran an in-process step after the
    // DRA-169 selftest (DRA-707).
    const int CCH_RM_SESSION_KEY = 32;

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, System.Text.StringBuilder strSessionKey);

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
        var key = new System.Text.StringBuilder(CCH_RM_SESSION_KEY + 1);
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

# Where EQBuddy Evolved is installed, from its uninstall entry - the directory the signed
# installer chose ({autopf}\EQBuddy Evolved: per-user or per-machine), never a guess. The
# DisplayName is the installer's AppVerName, "EQBuddy Evolved version X". Throws when there
# is none, because -Install swaps an exe inside an install and never creates one.
function Get-EqEvolvedInstallDir {
    $keys = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $entry = Get-ItemProperty $keys -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like 'EQBuddy Evolved version *' -and $_.InstallLocation } |
        Select-Object -First 1
    if (-not $entry -or -not (Test-Path -LiteralPath (Join-Path $entry.InstallLocation 'EQBuddy.exe'))) {
        throw 'No installed EQBuddy Evolved found. Install a release first (EQBuddyEvolvedSetup.exe); -Install swaps the exe inside an existing install and never creates one.'
    }
    return $entry.InstallLocation.TrimEnd('\')
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

# True when the process is still running at the end of the window. A build that dies in its
# first seconds (a missing native, a crash in startup) is exactly what the window is for.
function Wait-EqAlive {
    param([Parameter(Mandatory)][System.Diagnostics.Process] $Process, [int] $Seconds = 20)
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) { return $false }
        Start-Sleep -Milliseconds 250
    }
    $Process.Refresh()
    return -not $Process.HasExited
}

# The -Install roll, in the order DRA-705 §3 fixes: BUILD, then close, then swap, then
# relaunch only if it was running, then the liveness check with restore. The steps are
# scriptblocks so install-local-selftest.ps1 can drive this exact function with stand-ins,
# and the two `# roll:` regions are how it builds the pre-fix order (close before build)
# as a mutant and watches it kill the running copy - a test of the order that cannot
# see the order is trap 78. Returns what happened; the caller throws on a Failure.
#
# A throw from $Build propagates before anything is closed. That is the property.
function Invoke-EqInstallRoll {
    param(
        [Parameter(Mandatory)][scriptblock] $Build,
        [Parameter(Mandatory)][scriptblock] $FindRunning,
        [Parameter(Mandatory)][scriptblock] $Close,
        [Parameter(Mandatory)][scriptblock] $Launch,
        [Parameter(Mandatory)][string] $StagedExe,
        [Parameter(Mandatory)][string] $InstalledExe,
        [int] $LivenessSeconds = 20
    )
    $r = [ordered]@{ WasRunning = $false; Relaunched = $false; Restored = $false; Failure = $null; RestoreFailed = $null }

    # roll:build
    Write-Host '[install-local] stage=build'
    & $Build
    if (-not (Test-Path -LiteralPath $StagedExe)) { throw "the build produced no $StagedExe" }
    # roll:build-end

    # roll:close
    Write-Host '[install-local] stage=close'
    $running = @(& $FindRunning)
    $r.WasRunning = $running.Count -gt 0
    if ($running) { & $Close $running; Start-Sleep -Seconds 1 }
    # roll:close-end

    $previous = Join-Path (Split-Path -Parent $InstalledExe) 'EQBuddy.previous.exe'
    $saved = $false
    try {
        Write-Host '[install-local] stage=swap'
        Copy-Item -LiteralPath $InstalledExe -Destination $previous -Force
        $saved = $true
        Copy-Item -LiteralPath $StagedExe -Destination $InstalledExe -Force
        # Read back, not believed: the swap is the one step here with no exit code of its own.
        if ((Get-FileHash -LiteralPath $InstalledExe).Hash -ne (Get-FileHash -LiteralPath $StagedExe).Hash) {
            throw 'the installed EQBuddy.exe is not the build just signed - the swap did not take'
        }
        if ($r.WasRunning) {
            Write-Host '[install-local] stage=liveness'
            $proc = & $Launch $InstalledExe
            $r.Relaunched = $true
            if (-not (Wait-EqAlive -Process $proc -Seconds $LivenessSeconds)) {
                throw "the new build exited within $LivenessSeconds s of launch (exit code $($proc.ExitCode))"
            }
        }
    }
    catch {
        $r.Failure = $_.Exception.Message
        Write-Host "[install-local] stage=restore ($($r.Failure))"
        # The restore can throw too (a file still held, a launch that fails). It must not take
        # $r with it: the caller reads Failure, and RestoreFailed says the exe on disk is unknown.
        try {
            if ($saved) {
                Copy-Item -LiteralPath $previous -Destination $InstalledExe -Force
                $r.Restored = $true
            }
            # Whatever is installed now is the last good build; bring it back if he had it open.
            if ($r.WasRunning) { & $Launch $InstalledExe | Out-Null; $r.Relaunched = $true }
        }
        catch {
            $r.RestoreFailed = $_.Exception.Message
            Write-Host "[install-local] stage=restore-failed ($($r.RestoreFailed))"
        }
    }
    return [pscustomobject]$r
}

# EQBuddy.build.json beside the installed exe: what this build IS, written only once the roll
# succeeded, so after a failure it still names the build he is running. `pendingCommits` is
# the tree's non-merge commits that origin/main does not have yet - empty for a build of main,
# the PR's commits for a local PR-smoke integration branch. A local merge commit is never on
# main, so the commits it carries are the fact auto-roll.ps1 can check (DRA-705 §7).
function Write-EqBuildStamp {
    param([Parameter(Mandatory)][string] $Repo, [Parameter(Mandatory)][string] $InstallDir,
          [Parameter(Mandatory)][string] $Source, [Parameter(Mandatory)][string] $Version)
    $sha = (& git -C $Repo rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $sha) { $sha = 'unknown' }
    $subject = (& git -C $Repo log -1 --format=%s 2>$null)
    $dirty = [bool](& git -C $Repo status --porcelain --untracked-files=no 2>$null)
    $pending = @(& git -C $Repo rev-list --no-merges --max-count=200 HEAD --not origin/main 2>$null)
    if ($LASTEXITCODE -ne 0) { $pending = @($sha) }
    $pr = if ($subject -match '^Merge pull request #(\d+)') { [int]$Matches[1] } else { $null }
    $now = Get-Date
    $stamp = [ordered]@{
        sha            = "$sha".Trim()
        subject        = "$subject"
        mergedPr       = $pr
        version        = $Version
        builtAtLocal   = $now.ToString('yyyy-MM-ddTHH:mm:sszzz')
        builtAtUtc     = $now.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        source         = $Source
        dirty          = $dirty
        pendingCommits = @($pending | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
        result         = 'installed'
    }
    $path = Join-Path $InstallDir 'EQBuddy.build.json'
    $tmp = "$path.tmp"
    [System.IO.File]::WriteAllText($tmp, ($stamp | ConvertTo-Json -Depth 4), [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tmp -Destination $path -Force
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
if ($Install -and -not $Evolved) {
    throw '-Install is the Evolved daily-driver loop: pass -Evolved -Install.'
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
#
# THE ORDER IS BUILD FIRST (DRA-705 §3): the publish goes to dist\publish-staged, which no
# process runs from, so the copy he has open keeps running through the whole 172 MB build and
# the signature. Only a signed build in hand closes anything.
$publishDir = "$repo\dist\publish"
$stageDir = "$repo\dist\publish-staged"
$stagedExe = Join-Path $stageDir 'EQBuddy.exe'

$build = {
    if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
    dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $stageDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

    # Sign the app before Inno Setup packages it, so the installer carries a signed payload
    # as well as being signed itself. Invoke-EqSign throws on anything short of a verified,
    # timestamped signature — no warn-and-continue here either, because "it installed but
    # quietly unsigned" is the state this script sat in for a day without anyone noticing.
    Invoke-EqSign $stagedExe
}

$findRunning = {
    if ($Evolved) {
        $running = @(Get-EqProcessesHoldingProfile -ProfileDir $evolvedProfile)
    } else {
        $running = @(Get-Process EQBuddy -ErrorAction SilentlyContinue)
    }
    if ($running) {
        Write-Host $(if ($Evolved) { 'Closing the EQBuddy holding the Evolved profile (gracefully, so it finalizes its session)' }
                     else { 'Closing the running EQBuddy (gracefully, so it finalizes its session)' })
    }
    $running
}
$close = { param($procs) Close-EqBuddyGracefully -Processes $procs }

# ---- -Install: the daily driver -------------------------------------------------------
if ($Evolved -and $Install) {
    # Resolved before the build, so a PC with no install refuses in a second, not after it.
    $installDir = Get-EqEvolvedInstallDir
    $installed = Join-Path $installDir 'EQBuddy.exe'
    $launch = { param($exe) Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -PassThru }

    $roll = Invoke-EqInstallRoll -Build $build -FindRunning $findRunning -Close $close -Launch $launch `
        -StagedExe $stagedExe -InstalledExe $installed
    if ($roll.Failure) {
        $what = if ($roll.Restored) { 'EQBuddy.previous.exe was copied back, so the build that was there is installed again' }
                else { 'the installed EQBuddy.exe was not changed' }
        $again = if ($roll.Relaunched) { ' and relaunched' } else { '' }
        $restore = if ($roll.RestoreFailed) { " The restore itself then failed ($($roll.RestoreFailed)) - check EQBuddy.exe against EQBuddy.previous.exe by hand." } else { '' }
        throw "Install FAILED: $($roll.Failure). $what$again.$restore"
    }
    Write-EqBuildStamp -Repo $repo -InstallDir $installDir -Source $Source -Version $version

    $built = (Get-Item -LiteralPath $installed).VersionInfo.ProductVersion
    $state = if ($roll.Relaunched) { 'INSTALLED and running' }
             else { 'INSTALLED (it was not running, so it was left closed - the next launch is this build)' }
    Write-Host ''
    Write-Host "EQBuddy Evolved $built is $state from $installDir" -ForegroundColor Cyan
    Write-Host "  profile:   your own ($evolvedProfile)" -ForegroundColor Cyan
    Write-Host '  rollback:  Start menu -> "EQBuddy Evolved (previous version)" runs the build this replaced' -ForegroundColor Cyan
    Write-Host '  stamp:     EQBuddy.build.json beside the exe names the commit' -ForegroundColor Cyan
    Write-Host '  signed:    yes - same certificate, same verification as a release build.' -ForegroundColor Cyan
    Write-Host '  released:  nothing. GitHub, OneDrive and the update channel are untouched.' -ForegroundColor Cyan
    return
}

# ---- the Evolved loop stops here: run it, do not install it ------------------------
if ($Evolved) {
    # Same order as the roll: build and sign while the old copy runs, then close it and
    # move the build into dist\publish (the copy Launch-Evolved-Shell.cmd re-opens).
    & $build
    $running = @(& $findRunning)
    if ($running) { & $close $running; Start-Sleep -Seconds 1 }
    New-Item -ItemType Directory -Force $publishDir | Out-Null
    Copy-Item -Path (Join-Path $stageDir '*') -Destination $publishDir -Recurse -Force

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
