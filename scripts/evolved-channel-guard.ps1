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

    Four checks, all at major >= 2 only (this is a no-op on the 1.x line, which is
    finished and lives on `legacy-v1`):

      1. Every statement in release.ps1 that reaches the family - the OneDrive copy, the
         local /SILENT install, `gh release create` - lives inside ONE region opened by
         `if (-not $EvolvedLocal) {`, and the 2.x refusal (throw unless -EvolvedLocal)
         is positioned BEFORE that region and before the 172 MB publish. A future edit
         that re-adds a copy outside the region fails the build rather than the family's
         widgets. In LogJanitorPolicyTests' shape: the guard reads the script text.

         Since 2026-09-05 that list had a fourth member which reached NOBODY and was on it
         anyway: compiling, signing or hashing EQBuddySetup.exe. The other three are about
         a file leaving the machine; the installer only had to EXIST. It carried v1's
         AppId and {autopf}\EQBuddy, so a signed 2.0.0 one sitting in dist\ was one
         double-click from replacing the v1 install and inheriting its profile - and
         check 3 watches the family's update folder, never dist\.

         ON 2026-09-07 (TR-2, FABLE.md §3, signed #399) THAT MEMBER FLIPPED: from "never
         build the installer" to "only ever build it under the NEW IDENTITY". The
         mitigation was never the point - the AppId was. installer\EQBuddyEvolved.iss has
         its own AppId, installs into {autopf}\EQBuddy Evolved with its own Start-menu
         entry, and is named EQBuddyEvolvedSetup.exe, so the artifact cannot do the thing
         "build none" was avoiding. Check 5 below is the flipped row, and it is TWO facts
         that must both hold: the reserved name is never produced anywhere on a 2.x tree,
         and the script that IS compiled carries the Evolved identity. A negative alone
         would pass on a tree that builds no installer at all, which is trap 34's shape -
         so the positive is asserted beside it.
      2. `gh release create` is unreachable on a 2.x tree, by two independent locks -
         it is inside the region above, and -EvolvedLocal refuses -Tag and -Prerelease
         outright. There is deliberately NO switch that re-enables it.
      3. THE POSITIVE ONE, and the only one that is about the world: the live update
         folder contains no EQBuddySetup.exe stamped 2.x. Checks 1 and 2 prove a script;
         trap 43's lesson is that proving the producer is not proving the effect. If a
         2.x setup is sitting in that folder, every family widget is six hours from
         installing it and no amount of correct script text helps.

      4. CLOSED 2026-09-04 (E-2c), and it used to be this file's named RESIDUAL: no
         workflow under .github/workflows/ fires on a `release:` event. The residual was
         `release-assets.yml`, which attached non-Windows assets on `release: published` -
         unreachable through release.ps1, since checks 1 and 2 make the release itself
         unreachable, but a release made BY HAND in the GitHub UI would still have
         triggered it, and then the first Evolved release ever published would have
         carried Linux and macOS artifacts of a Windows-only product.

         E-2c deletes that workflow from the Evolved mainline; this check is what keeps
         it deleted. Legacy tags keep their own copy of it in their own tree and can be
         re-published forever, which is LEGACY-004 - `release-assets.yml` runs from the
         TAG's tree, not from main, which is the observation that made deleting it safe.

         Same shape as check 1's fourth token: deleting the thing without guarding the
         shape leaves the mechanism exactly as blind as it was.

      5. THE INSTALLER IDENTITY (TR-2, 2026-09-07), which is check 1's fourth member after
         the flip described above, and is stated as its own check because it reads two
         files rather than one region:

           * EQBuddySetup.exe is a RESERVED NAME belonging to the v1 line FOREVER. Every
             deployed 1.x updater matches on it (UpdateChecker.SetupName) and the v1
             contract is frozen - an asset of that name on a 2.x release would be
             downloaded and run by every Windows v1 install's existing update flow, a
             major-line replacement with no consent moment. So no 2.x path may PRODUCE
             one: not release.ps1, not install-local.ps1, and not a .iss sitting in the
             tree that anyone could hand-compile. Reading the name is fine and is the
             point - release.ps1 still warns about a pre-TR-2 leftover in dist\.
           * The installer script release.ps1 compiles is the Evolved one, and carries all
             four halves of the identity: the new AppId (and NOT v1's), the
             {autopf}\EQBuddy Evolved directory, its own Start-menu group, and the
             EQBuddyEvolvedSetup output name.

         The AppId literals are written HERE as well as in the .iss on purpose: this is
         the ScreenLockTests shape - two files that must agree, neither with a compiler
         that can see the other. The v1 literal is carried as the thing that must never
         come back.

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
    pwsh -NoProfile -File scripts/evolved-channel-guard.ps1 -AssumeVersion 2.0.0 -Repo <pre-E-2c worktree>        # prove 4 fails
    pwsh -NoProfile -File scripts/evolved-channel-guard.ps1 -AssumeVersion 2.0.0 -Repo <pre-TR-2 worktree>        # prove 5 fails
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

# ALL of them, not the first. The script skips more than one thing under -EvolvedLocal -
# the publish channel, and separately the Stop-Process that only exists because a /SILENT
# install is coming - and a guard that locked on to the first `if (-not $EvolvedLocal) {`
# it found would declare everything after it "outside the region" and fail a correct tree.
$regions = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -notmatch '^\s*if\s*\(\s*-not\s+\$EvolvedLocal\s*\)\s*\{') { continue }
    $depth = 0
    $end = -1
    for ($j = $i; $j -lt $lines.Count; $j++) {
        $depth += Get-Depth $lines[$j]
        if ($depth -le 0) { $end = $j; break }
    }
    if ($end -lt 0) {
        $problems += "scripts/release.ps1 opens ``if (-not `$EvolvedLocal) {`` at line $($i + 1) and never closes it. Unbalanced braces mean this guard cannot say what is inside the region, so it refuses to say anything is."
        continue
    }
    $regions += , @($i, $end)
}

$regionStart = if ($regions.Count -gt 0) { ($regions | ForEach-Object { $_[0] } | Measure-Object -Minimum).Minimum } else { -1 }
if ($regions.Count -eq 0) {
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
#
# THE FOURTH TOKEN IS GONE, and its absence is the TR-2 flip rather than a relaxation.
# It matched the ACTS - compile, sign, hash - because at the time the installer's mere
# existence was the hazard: it carried v1's AppId, so a signed 2.0.0 EQBuddySetup.exe in
# dist\ was one double-click from replacing the v1 install in place. That is now false by
# construction (installer\EQBuddyEvolved.iss), and the compile deliberately sits OUTSIDE
# the region so that -EvolvedLocal builds one. Check 5 replaces it, and it is strictly
# stronger: the acts were a proxy for an identity, and check 5 asserts the identity
# (trap 64 - name the fact, not the thing that stood in for it).
$channelReach = 'At 2.x that line is reachable, and reachable means the family''s widgets take an Evolved build within six hours.'
$channelTokens = @(
    @{ Rx = 'EQBuddyDownload|\$oneDrive'; What = 'names the family update folder';                Why = $channelReach },
    @{ Rx = 'gh\s+release\s+create';      What = 'creates a GitHub release';                      Why = $channelReach },
    @{ Rx = '/SILENT';                    What = 'installs this build on this machine';           Why = $channelReach }
)

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*#') { continue }
    $inside = $false
    foreach ($r in $regions) { if ($i -ge $r[0] -and $i -le $r[1]) { $inside = $true; break } }
    if ($inside) { continue }
    foreach ($t in $channelTokens) {
        if ($line -match $t.Rx) {
            $problems += "scripts/release.ps1 line $($i + 1) $($t.What) outside the -EvolvedLocal region: $($line.Trim()). $($t.Why)"
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
$prereleaseRefusal = $false
foreach ($line in $lines) {
    if ($line -match '^\s*#') { continue }
    if ($line -match '\$EvolvedLocal' -and $line -match '\$Prerelease' -and $line -match 'throw') { $prereleaseRefusal = $true }
}
if (-not $prereleaseRefusal) {
    $problems += 'scripts/release.ps1 does not refuse -Prerelease under -EvolvedLocal. -Prerelease is a flag on a GitHub release, so under a switch that cannot make one it is a switch that silently does nothing - the exact defect the -Prerelease/-Tag refusal at the top of that script was written for.'
}

# ---- 5: the installer identity (check 1's fourth member, flipped by TR-2) -----------

# The two literals this check exists to keep apart. They are written HERE as well as in
# the .iss because that is the only way two files with no compiler between them can be
# held to one fact - ScreenLockTests' shape. The v1 one is carried as the thing that must
# never come back, not as something to match.
$v1AppId      = '{7E1B6A94-3C2D-4B77-9F41-EQBUDDY10000}'
$evolvedAppId = '{B3D71F58-6E2A-4C90-A7D4-EQBUDDY20000}'
$evolvedIss   = 'installer/EQBuddyEvolved.iss'

# (a) THE NEGATIVE - nothing on a 2.x tree PRODUCES the reserved name.
#
# Matched on the acts, and the list is every verb that can put a file of that name on disk
# or hand one to somebody. A READ is deliberately absent: release.ps1's -EvolvedLocal
# summary uses Test-Path/Get-Item to warn about a pre-TR-2 leftover in dist\, which is this
# rule said out loud, and a token that fired on it would be the guard arguing with its own
# reason for existing. "EQBuddyEvolvedSetup" does not contain "EQBuddySetup", so the new
# name needs no exemption - that is half of why it was chosen.
$producers = 'Invoke-EqSign|Get-FileHash|Set-Content|Out-File|Copy-Item|Move-Item|Rename-Item|Compress-Archive|Start-Process|gh\s+release\s+create'
foreach ($rel in @('scripts/release.ps1', 'scripts/install-local.ps1')) {
    $text = Read-Utf8 $rel
    if (-not $text) { continue }
    $n = 0
    foreach ($line in ($text -split "`r?`n")) {
        $n++
        if ($line -match '^\s*#') { continue }
        if ($line -match 'EQBuddySetup' -and $line -match $producers) {
            $problems += "$rel line $n produces or hands on an artifact named EQBuddySetup.exe: $($line.Trim()). That name is RESERVED to the v1 line forever - every deployed 1.x updater matches on it (UpdateChecker.SetupName) and the v1 contract is frozen, so a 2.x artifact wearing it is downloaded and run by every Windows v1 install's existing update flow: a major-line replacement with no consent moment. The 2.x installer is $evolvedIss and it is called EQBuddyEvolvedSetup.exe."
        }
    }
}

# ...and the same negative about the TREE rather than about a script. A .iss whose output
# is the reserved name is one hand-run ISCC from the hazard, and nothing else in this repo
# would notice; the same argument that put the compile on check 1's list in the first
# place. It also catches v1's AppId coming back under any filename, which is the identity
# half stated as a negative.
$installerDir = Join-Path $Repo 'installer'
if (Test-Path $installerDir) {
    foreach ($iss in Get-ChildItem -Path $installerDir -Filter '*.iss' -File) {
        # Inno comments start with ';', and they are exempt for the same reason PowerShell
        # comments are above: the Evolved .iss NAMES v1's AppId in a comment, to say what
        # it is deliberately not. A guard that read that as a violation would forbid the
        # file from explaining itself.
        $text = (([IO.File]::ReadAllText($iss.FullName) -split "`r?`n") |
                 Where-Object { $_ -notmatch '^\s*;' }) -join "`n"
        if ($text -match '(?m)^\s*OutputBaseFilename\s*=\s*EQBuddySetup\s*$') {
            $problems += "installer/$($iss.Name) produces EQBuddySetup.exe. On a 2.x tree that file is one hand-run ISCC from the reserved name, whatever release.ps1 does or does not compile - the v1 installer belongs to the v1 tree (it is on ``legacy-v1``, which builds from its own tree, exactly as LEGACY-004 keeps release-assets.yml alive there)."
        }
        if ($text -match [regex]::Escape($v1AppId)) {
            $problems += "installer/$($iss.Name) carries EQBuddy 1.x's AppId $v1AppId. An installer with that AppId replaces a v1 install IN PLACE and inherits its profile - settings.json, history.db, archives - and #158's rollback gives back the binary, not the profile. The Evolved identity is $evolvedAppId."
        }
    }
}

# (b) THE POSITIVE - and it is the half a negative alone cannot cover (trap 34). "No .iss
# makes the reserved name" is equally true of a tree that has no installer at all, which is
# where this repo was yesterday; the row flipped precisely because -EvolvedLocal now has to
# BUILD one. So: release.ps1 compiles an .iss, every .iss it names is the Evolved one, and
# that file carries all four halves of the identity.
$issLines = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*#') { continue }
    if ($lines[$i] -match '\.iss\b') { $issLines += , @($i, $lines[$i]) }
}
if ($issLines.Count -eq 0) {
    $problems += "scripts/release.ps1 compiles no installer script. Since TR-2 the Evolved line HAS an installer story - $evolvedIss, its own AppId, {autopf}\EQBuddy Evolved - and -EvolvedLocal builds it. A tree that builds none passes every negative in this check while shipping nothing a player can install, which is why this row is asserted from both ends."
}
foreach ($entry in $issLines) {
    if ($entry[1] -notmatch 'EQBuddyEvolved\.iss') {
        $problems += "scripts/release.ps1 line $($entry[0] + 1) names an installer script that is not $($evolvedIss): $($entry[1].Trim()). At 2.x there is exactly one, and it is the one carrying the Evolved AppId."
    }
}

$issText = Read-Utf8 $evolvedIss
if (-not $issText) {
    $problems += "$evolvedIss is missing. It is the 2.x installer and the only place the Evolved AppId, install directory and output name are written; a guard that cannot find it has not checked the identity."
}
else {
    # Comments stripped here too, and for the opposite reason to the negative above: a
    # comment naming the Evolved AppId must not SATISFY the check either. Every assertion
    # below is anchored to a real directive line for the same reason.
    $issText = (($issText -split "`r?`n") | Where-Object { $_ -notmatch '^\s*;' }) -join "`n"

    # Four facts, four messages. They fail for four different reasons and a merged one
    # would name the wrong half of the identity.
    if ($issText -notmatch ('(?m)^\s*AppId\s*=\s*\{*' + [regex]::Escape($evolvedAppId))) {
        $problems += "$evolvedIss does not carry the Evolved AppId $evolvedAppId. Inno keys the install, the uninstall entry and the upgrade-in-place decision on AppId, so that literal IS 'this never replaces an EQBuddy 1.x install'."
    }
    if ($issText -notmatch '(?m)^\s*DefaultDirName\s*=\s*\{autopf\}\\EQBuddy Evolved\s*$') {
        $problems += "$evolvedIss does not install into {autopf}\EQBuddy Evolved. Beside {autopf}\EQBuddy, never on top of it: dual install is the feature that makes trying Evolved low-stakes, and it is the directory that makes falling back to 1.x possible."
    }
    if ($issText -notmatch '(?m)^\s*OutputBaseFilename\s*=\s*EQBuddyEvolvedSetup\s*$') {
        $problems += "$evolvedIss does not output EQBuddyEvolvedSetup. The artifact name is the other half of the reserved-name rule: the negative above stops EQBuddySetup.exe being produced, and this is what says which name IS produced."
    }
    if ($issText -notmatch '(?m)^\s*DefaultGroupName\s*=\s*EQBuddy Evolved\s*$' -or
        $issText -notmatch '(?m)^\s*Name:\s*"\{group\}\\EQBuddy Evolved"') {
        $problems += "$evolvedIss does not give Evolved its own Start-menu group and shortcut named 'EQBuddy Evolved'. Two Start-menu rows both reading 'EQBuddy' is a dual install the player cannot steer, which turns the fallback this design is built on into a coin toss."
    }
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

# ---- 4: no workflow answers a `release:` event -------------------------------------

# Checks 1 and 2 are about the script that MAKES a release. This one is about what would
# answer if a release appeared anyway - by hand in the GitHub UI, which no amount of
# correct PowerShell can prevent. `release-assets.yml` was that answer until E-2c, and it
# published Linux and macOS artifacts for a product that is Windows-only.
#
# Matched on the TRIGGER, not on a filename: a re-added workflow under any name is the
# same hazard, and this repo has learnt (E-2c's own sibling token) that a filename token
# guards a filename. Anchored to a line that is only `release:` at YAML trigger depth, so
# `-c Release` and prose cannot trip it.
$workflowDir = Join-Path $Repo '.github/workflows'
if (Test-Path $workflowDir) {
    foreach ($wf in Get-ChildItem -Path $workflowDir -Filter '*.yml' -File) {
        $text = [IO.File]::ReadAllText($wf.FullName)
        if ($text -match '(?m)^\s{0,4}release:\s*$') {
            $problems += ".github/workflows/$($wf.Name) fires on a ``release:`` event. At 2.x nothing may answer a release: checks 1 and 2 stop release.ps1 from making one, but a release created by hand in the GitHub UI still fires workflows, and the one this replaced attached Linux and macOS artifacts to it. Evolved is Windows-only; legacy tags keep their own copy of that workflow in their own tree (LEGACY-004) and lose nothing by its absence here."
        }
    }
}

# ---- verdict -----------------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host "evolved-channel-guard: FAILED (version $version)" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "evolved-channel-guard:    $p" -ForegroundColor Red }
    exit 1
}

# The scope line names what was ACTUALLY read, because a reassuring summary over a check
# that saw nothing is how this guard could have been green on the installer hole (#297).
# Workflows are always readable, so they are always in scope; the live channel is not.
$scope = if ($looked.Count -gt 0) { "scripts + installer identity + workflows + live channel ($($looked -join ', '))" } else { 'scripts + installer identity + workflows only - live channel not inspected' }
Write-Host "evolved-channel-guard: ok  (version $version; $scope)" -ForegroundColor Green
exit 0
