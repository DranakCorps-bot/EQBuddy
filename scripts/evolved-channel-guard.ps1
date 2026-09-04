<#
.SYNOPSIS
    EQBuddy Evolved (2.x) develops LOCAL-ONLY, and that has to be a mechanism.

.DESCRIPTION
    The owner's GO for Evolved (2026-09-04) says: no public channel, no auto-publish,
    until he says ready. Today that promise has a hole big enough to drive a release
    through, and it is not the one anybody watches:

      `release.ps1` copies EQBuddySetup.exe, its .sha256 and the portable zip into
      C:\Users\david\OneDrive\EQBuddyDownload on EVERY run - before the `if ($Tag)`
      block, with or without a tag, with or without -Prerelease. `UpdateChecker` is
      local-first BY DESIGN: `Check(folder)` reads that exe's FileVersionInfo,
      `IsNewer` compares it to the running build, `FindBestAsync` returns it as "a
      local file ready to install as-is", and the widget asks at startup and every six
      hours. So one release.ps1 run on a 2.x tree auto-updates every family v1 install
      to a Windows-only Evolved build inside six hours - no tag, no GitHub release, no
      prerelease flag anywhere in the story.

    `-Prerelease` (#279) closed the OTHER hole, the GitHub one. release.ps1's own
    comment says so in as many words: *"The OneDrive copy above is a SEPARATE channel
    ... so a prerelease still reaches the family's widgets."* Correct and deliberate for
    v1. It is the leak for Evolved.

    THE POSTURE: local-only is enforced structurally or it is not enforced. This repo
    already knows the shape - release.ps1 has no -SkipSign on purpose, because a
    protection you can pass a flag to opt out of is a protection nobody has. So with the
    script as written you cannot publish a 2.x build at all; opening the channel is a
    deliberate future EDIT, made when the owner gives the channel go.

    Three checks, all at major >= 2 only (this is a no-op on the 1.x line, which is
    finished and lives on `legacy-v1`):

      1. Every statement in release.ps1 that reaches the family - the OneDrive copy, the
         local /SILENT install, `gh release create` - lives inside ONE region opened by
         `if (-not $EvolvedLocal) {`, and the 2.x refusal (throw unless -EvolvedLocal)
         is positioned BEFORE that region and before the 172 MB publish. A future edit
         that re-adds a copy outside the region fails the build rather than the family's
         widgets. In LogJanitorPolicyTests' shape: the guard reads the script text.
      2. `gh release create` is unreachable on a 2.x tree, by two independent locks -
         it is inside the region above, and -EvolvedLocal refuses -Tag and -Prerelease
         outright. There is deliberately NO switch that re-enables it.
      3. THE POSITIVE ONE, and the only one that is about the world: the live update
         folder contains no EQBuddySetup.exe stamped 2.x. Checks 1 and 2 prove a script;
         trap 43's lesson is that proving the producer is not proving the effect. If a
         2.x setup is sitting in that folder, every family widget is six hours from
         installing it and no amount of correct script text helps.

    RESIDUAL, named rather than papered over: `.github/workflows/release-assets.yml`
    attaches non-Windows assets on a `release: published` event. It cannot fire without
    a release, and creating one is what checks 1 and 2 block - but a release made by
    hand in the GitHub UI would still trigger it. E-2 deletes that workflow from the
    Evolved mainline (legacy tags keep their own copy and can be re-published forever,
    which is LEGACY-004). It is not checked here because failing on it today would
    fail a gate for work that is already scheduled and owned.

    Exits non-zero on a violation. The tree is 1.99.x until E-1's third commit lands, so
    -AssumeVersion exists purely so this can be PROVEN to fail before it can ever fire
    (traps 34, 39: a guard that has never failed has not been shown to guard anything).
    -Repo points it at another worktree; -AssumeUpdateFolder is check 3's own prove-fail
    hook, since the real folder is - and had better stay - clean.

    Files are read with [IO.File]::ReadAllText rather than Get-Content: Windows
    PowerShell 5.1 decodes with the ANSI code page and this repo's scripts are full of
    em dashes (trap 54, and the same reason legacy-notice-guard.ps1 does it).

.EXAMPLE
    pwsh -NoProfile -File scripts/evolved-channel-guard.ps1
    pwsh -NoProfile -File scripts/evolved-channel-guard.ps1 -AssumeVersion 2.0.0        # prove 1 and 2 fail
    pwsh -NoProfile -File scripts/evolved-channel-guard.ps1 -AssumeVersion 2.0.0 -AssumeUpdateFolder C:\tmp\fake  # prove 3 fails
#>
[CmdletBinding()]
param(
    # Verification hooks. None of the three is used by check.ps1 or release.ps1; they
    # exist so a guard that is silent on today's tree can still be shown to bite.
    [string] $AssumeVersion,
    [string] $Repo,
    [string] $AssumeUpdateFolder
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }
$problems = @()

function Read-Utf8([string] $relative) {
    $path = Join-Path $Repo $relative
    if (-not (Test-Path $path)) { return $null }
    [IO.File]::ReadAllText($path)
}

$version = $AssumeVersion
if (-not $version) {
    $props = Read-Utf8 'Directory.Build.props'
    if (-not $props) { throw 'No Directory.Build.props at the repo root' }
    if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
    $version = $Matches[1]
}
$major = [int]($version.Split('.')[0])

if ($major -lt 2) {
    Write-Host "evolved-channel-guard: ok  (version $version; 1.x - the local-only checks arm at 2.0.0)" -ForegroundColor Green
    exit 0
}

# ---- the script under inspection ---------------------------------------------------

$releaseText = Read-Utf8 'scripts/release.ps1'
if (-not $releaseText) {
    Write-Host "evolved-channel-guard: FAILED (version $version)" -ForegroundColor Red
    Write-Host 'evolved-channel-guard:    scripts/release.ps1 is missing. It is the only script that can reach the family; a guard that cannot find it has not checked anything.' -ForegroundColor Red
    exit 1
}
$lines = $releaseText -split "`r?`n"

# Brace depth, with ${...} stripped first: `${env:ProgramFiles(x86)}` is a real line in
# this script and a naive counter reads its closing brace as the end of a block.
function Get-Depth([string] $line) {
    $bare = [regex]::Replace($line, '\$\{[^}]*\}', '')
    ([regex]::Matches($bare, '\{')).Count - ([regex]::Matches($bare, '\}')).Count
}

# ---- 1: one region, and the refusal in front of it ---------------------------------

$regionStart = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*if\s*\(\s*-not\s+\$EvolvedLocal\s*\)\s*\{') { $regionStart = $i; break }
}

$regionEnd = -1
if ($regionStart -ge 0) {
    $depth = 0
    for ($i = $regionStart; $i -lt $lines.Count; $i++) {
        $depth += Get-Depth $lines[$i]
        if ($depth -le 0) { $regionEnd = $i; break }
    }
    if ($regionEnd -lt 0) {
        $problems += 'scripts/release.ps1 opens `if (-not $EvolvedLocal) {` and never closes it. Unbalanced braces mean this guard cannot say what is inside the region, so it refuses to say anything is.'
    }
}
else {
    $problems += 'scripts/release.ps1 has no `if (-not $EvolvedLocal) {` region. At 2.x every statement that reaches the family - the OneDrive copy, the /SILENT install, `gh release create` - belongs inside one, so that skipping it is a single decision rather than three.'
}

# The refusal itself. Asserted as three separate facts because they fail for three
# different reasons and a merged message would name the wrong one.
$refusalLine = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '\$major\s*-ge\s*2' -and $lines[$i] -notmatch '^\s*#') { $refusalLine = $i; break }
}
if ($refusalLine -lt 0) {
    $problems += 'scripts/release.ps1 does not test `$major -ge 2`. The 2.x refusal is the whole mechanism: without it, -EvolvedLocal is an opt-IN to safety, which is the posture this repo rejected when it refused to add -SkipSign.'
}
elseif (-not (($lines[$refusalLine..([Math]::Min($refusalLine + 12, $lines.Count - 1))] -join "`n") -match 'throw')) {
    $problems += 'scripts/release.ps1 tests `$major -ge 2` but no `throw` follows it. A 2.x release must STOP, not warn - a warning on a run that goes on to build, sign and copy is exactly how the old self-signed path shipped an unsigned installer while reporting success.'
}
if ($releaseText -notmatch '(?m)^\s*param\(.*\$EvolvedLocal') {
    $problems += 'scripts/release.ps1 has no -EvolvedLocal switch in its param block.'
}

$publishLine = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'dotnet publish' -and $lines[$i] -notmatch '^\s*#') { $publishLine = $i; break }
}
if ($refusalLine -ge 0 -and $publishLine -ge 0 -and $refusalLine -gt $publishLine) {
    $problems += "scripts/release.ps1 refuses 2.x at line $($refusalLine + 1), AFTER the publish at line $($publishLine + 1). Every other refusal in that script fires before the 172 MB publish; this one guards a one-way door and has less excuse than the rest."
}
if ($refusalLine -ge 0 -and $regionStart -ge 0 -and $refusalLine -gt $regionStart) {
    $problems += "scripts/release.ps1 refuses 2.x at line $($refusalLine + 1), after the -EvolvedLocal region opens at line $($regionStart + 1). The refusal has to be in front of the thing it refuses."
}

# ---- 1 and 2: nothing that reaches the family sits outside the region ---------------

# Comment lines are exempt on purpose: a comment cannot copy a file, and the region
# needs the prose that explains what the channel IS. Every executable line is read.
$channelTokens = @(
    @{ Rx = 'EQBuddyDownload|\$oneDrive'; What = 'names the family update folder' },
    @{ Rx = 'gh\s+release\s+create';     What = 'creates a GitHub release' },
    @{ Rx = '/SILENT';                   What = 'installs over this machine''s v1 install' }
)

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*#') { continue }
    if ($regionStart -ge 0 -and $regionEnd -ge $regionStart -and $i -ge $regionStart -and $i -le $regionEnd) { continue }
    foreach ($t in $channelTokens) {
        if ($line -match $t.Rx) {
            $problems += "scripts/release.ps1 line $($i + 1) $($t.What) outside the -EvolvedLocal region: $($line.Trim()). At 2.x that line is reachable, and reachable means the family's widgets take an Evolved build within six hours."
        }
    }
}

# ---- 2: the second lock - -EvolvedLocal refuses a tag ------------------------------

$tagRefusal = $false
foreach ($line in $lines) {
    if ($line -match '^\s*#') { continue }
    if ($line -match '\$EvolvedLocal' -and $line -match '\$Tag' -and $line -match 'throw') { $tagRefusal = $true }
}
if (-not $tagRefusal) {
    $problems += 'scripts/release.ps1 does not refuse -Tag under -EvolvedLocal. The region check makes `gh release create` unreachable; this is the second, independent lock, and two locks is the point - the first one is a claim about braces.'
}
if ($releaseText -match '(?m)^\s*param\(.*\$EvolvedLocal' -and $releaseText -notmatch 'EvolvedLocal[^\r\n]*Prerelease|Prerelease[^\r\n]*EvolvedLocal') {
    $problems += 'scripts/release.ps1 does not refuse -Prerelease under -EvolvedLocal. -Prerelease is a flag on a GitHub release, so under a switch that cannot make one it is a switch that silently does nothing - the exact defect the -Prerelease/-Tag refusal at the top of that script was written for.'
}

# ---- 3: the live channel is clean --------------------------------------------------

# UpdateChecker.FindUpdateFolder's rule, re-implemented rather than invoked: an explicit
# setting wins, then the known family path, then a shallow scan of this PC's OneDrive
# roots. The literal path is READ OUT OF release.ps1 rather than hardcoded here, so the
# guard follows the script if the channel ever moves.
$candidates = @()
if ($AssumeUpdateFolder) { $candidates = @($AssumeUpdateFolder) }
else {
    # Single-quoted pattern, so $oneDrive is a literal here and not an expansion to "".
    if ($releaseText -match '(?m)^\s*\$oneDrive\s*=\s*(.+)$') {
        $candidates += $Matches[1].Trim().Trim("'").Trim('"')
    }
    foreach ($envName in @('OneDrive', 'OneDriveConsumer', 'OneDriveCommercial')) {
        $root = [Environment]::GetEnvironmentVariable($envName)
        if (-not $root -or -not (Test-Path $root)) { continue }
        $candidates += (Join-Path $root 'EQBuddyDownload')
        try {
            foreach ($sub in Get-ChildItem -Path $root -Directory -ErrorAction Stop) {
                $candidates += (Join-Path $sub.FullName 'EQBuddyDownload')
            }
        }
        catch { <# an inaccessible root is not a violation #> }
    }
}

$looked = @($candidates | Sort-Object -Unique | Where-Object { Test-Path $_ })
if ($looked.Count -eq 0) {
    # Fail OPEN, loudly, on a prefixed line so check.ps1's filter prints it. CI has no
    # OneDrive and neither does a fresh clone; a silent skip here would be a gate that
    # reads as coverage while seeing nothing, which is the thing this file is against.
    Write-Host 'evolved-channel-guard: check 3 SKIPPED - no update folder on this machine (CI, or a clone without OneDrive). The live channel was NOT inspected.' -ForegroundColor Yellow
}
else {
    foreach ($folder in $looked) {
        $setup = Join-Path $folder 'EQBuddySetup.exe'
        if (-not (Test-Path $setup)) { continue }
        $info = (Get-Item $setup).VersionInfo
        $stamped = $info.FileVersion

        # TWO readings, because they can disagree and only one of them is the app's.
        #
        # UpdateChecker.Check runs Version.TryParse over the FileVersion STRING, so that
        # parse is what actually decides whether a family widget offers this file. But a
        # string reading is fragile in a way an int is not: the first attempt here stripped
        # non-digits and fed "10.0.26100.9278 (WinBuild.160101.0800)" in as five parts,
        # TryParse said no, and the check passed on a fixture written to make it fail.
        # A check that cannot fail reads as coverage (traps 34, 39), so FileMajorPart -
        # the numeric field out of VS_FIXEDFILEINFO, which needs no parser - is read
        # alongside it. Either one at 2.x is a hazard worth stopping for.
        $parsed = $null
        $stringMajor = if ($stamped -and [Version]::TryParse($stamped, [ref] $parsed)) { $parsed.Major } else { -1 }
        if ($stringMajor -ge 2 -or $info.FileMajorPart -ge 2) {
            $problems += "$setup is stamped $stamped (FileMajorPart $($info.FileMajorPart)). That folder IS the family's auto-update channel - UpdateChecker.Check reads this exe's FileVersionInfo at startup and every six hours and returns it as a local file ready to install as-is - so an Evolved build sitting there is already on its way to every v1 install. Remove it and restore the final v1 installer."
        }
    }
}

# ---- verdict -----------------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host "evolved-channel-guard: FAILED (version $version)" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "evolved-channel-guard:    $p" -ForegroundColor Red }
    exit 1
}

$scope = if ($looked.Count -gt 0) { "script + live channel ($($looked -join ', '))" } else { 'script only - live channel not inspected' }
Write-Host "evolved-channel-guard: ok  (version $version; $scope)" -ForegroundColor Green
exit 0
