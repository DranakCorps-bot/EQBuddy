<#
.SYNOPSIS
    A pull request may not grow an active channel ledger past 64 KiB. The remedy is
    ROTATION into the archive - never deletion.

.DESCRIPTION
    DRA-73 plan rev 2 (approved by David 2026-09-14) set a 30-day / ~64 KB rotation policy
    for the channel ledgers and named a CI size ratchet to enforce the size arm. The
    rotation shipped under DRA-75; the ratchet did not, and DRA-73 then closed, so the
    check had no live owner. This is it (DRA-26 plan rev 3 section 7, card A).

    The gap it fills is measured, not theoretical. On `main` at 275cc215, 9 of the 11
    rostered channel files are over the approved limit - HELM.md by 14x (942 KB),
    DECISIONS.md by 9x (615 KB), BEVEL-FEEDBACK.md by 8x (502 KB) - and nothing in
    `.github/workflows/ci.yml` looked at the size of any of them. `channel-wipe-guard.ps1`
    forbids a ledger getting SMALLER by the wrong mechanism. Nothing forbade it getting
    bigger without limit, which is the failure mode that actually happens every day: an
    agent that conscientiously reads the channel files burns ~300K tokens before writing a
    line of code (DRA-73 section 1.4).

    TWO ARMS, and which one applies is decided by the file's size AT BASE:

      CEILING   base <= 64 KiB. Head may not exceed 64 KiB. No tolerance, no grandfather,
                nothing to argue about - this is the acceptance criterion in one line, and
                it is what governs every file once DRA-144's rotation lands.
      RATCHET   base > 64 KiB. The file is already in debt, so it is judged against its
                recorded row in `channel-size-baseline.psd1` plus 10%. An over-limit file
                with NO row may not grow at all.

    BOTH ARMS FIRE ONLY WHEN THIS PULL REQUEST GREW THE FILE (head > base). That is
    deliberate and it is the word the card uses - "fails any PR that GROWS an active
    channel ledger beyond 64 KB". A pull request that never touched HELM.md must not be
    failed for HELM.md: on a `pull_request` run the checkout is the merge result, so every
    ledger's ambient size is in head whether the author wrote it or not, and blaming an
    author for the state of the branch they merged into is how a guard earns a reputation
    for firing on Tuesday. It also keeps the one move the policy WANTS - rotation, which
    shrinks a file - green by construction rather than by exemption.

    WHAT "GREW" CAN STILL OVER-COUNT, and it is worth knowing before the first surprise.
    On a `pull_request` run head is the merge result against the CURRENT base tip, while
    `-BaseRef` is the base sha the pull request was opened against, so any growth main
    accumulated in between sits inside head and is attributed to this pull request.
    `channel-wipe-guard.ps1` compares the same two points and has the same property; two
    gates on one policy disagreeing about what "base" means would be worse than the
    over-count. In practice the grandfather band is what absorbs it - HELM.md takes ~13 KB
    per append, so the 10% band is about seven of them - and when the band is genuinely
    exhausted, every open pull request going red until the file is rotated is the ratchet
    working rather than a bug in it. That is the whole mechanism: the pressure lands on the
    repository, not on one unlucky author.

    THE ARCHIVE IS NEVER MEASURED. The roster is eleven files at the repo ROOT; nothing
    under `docs/ops/claude-archive/` is a rostered path, so a rotation that moves 900 KB
    out of a ledger and into the archive is a shrink plus an untouched-by-this-guard write.
    That is structural rather than an exemption clause, which matters: an exemption can be
    argued into covering something else, and a path that is simply not in the roster cannot.
    `channel-size-selftest.ps1` asserts it anyway - a 2 MB archive write, and a full
    900 KB -> 40 KB rotation, both green - because "it cannot happen" is how the mojibake
    list stayed green for a month while matching nothing (trap 74).

    IT MEASURES LF-NORMALISED UTF-8 BYTES, NOT FILE LENGTH, and that is not fastidiousness.
    This repo has no `.gitattributes` and the Windows checkout runs `core.autocrlf=true`, so
    every blob is LF in git and CRLF on disk. Comparing a working-tree read against a base
    blob would have reported 3,054 bytes of growth on HELM.md - one per line, written by
    nobody. Normalising both sides is what makes the number the same on a developer's
    machine, in `check.ps1`, and on the runner.

    THE BASELINE TABLE IS CHECKED, NOT TRUSTED. A ratchet whose ceiling the ratcheted party
    can raise is a suggestion. So: a row may not be RAISED, a row may not be ADDED, a row
    for an unrostered path fails, and a row whose file this pull request brought under the
    limit must be DELETED in that same pull request - the rule docs/Architecture.md's
    hotspot table states as "the lift came first, and the baseline came down in the same
    commit". Rows only ever leave.

    AND THE ROSTER IS CHECKED TOO. Every `*-FEEDBACK.md` at the repo root must be rostered.
    A hand-written list stops covering the repo the day the repo grows, and the file it
    stops covering is the one nobody is looking at (trap 30; docs/Architecture.md says the
    same thing about the hotspot list, having learned it from a 5,127-line file that had no
    ratchet at all).

    THERE IS NO -Force AND NO SKIP SWITCH, for the reason `channel-wipe-guard.ps1` has
    none, and there is no parameter that supplies the baseline table either. It is read
    from the tree at head like any other tracked file, so the only way to change what the
    ratchet allows is to edit a committed file that the four rules above are watching -
    which is a reviewable diff rather than a command-line argument. `-Repo`, `-BaseRef` and
    `-HeadRef` exist for one reason: `channel-size-selftest.ps1` uses them to drive every
    arm of this guard into the red on a throwaway repo. Green-only is vacuous coverage
    (trap 34).

    WHAT IT DOES NOT DO: it never trims, moves or edits a ledger, and it does not touch
    `scripts/channel-wipe-guard.ps1` - that guard hard-codes the archive root this policy
    rotates into, and editing it from inside the change it polices is trap 52.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-size-guard.ps1
    pwsh -NoProfile -File scripts/channel-size-guard.ps1 -BaseRef origin/main
    pwsh -NoProfile -File scripts/channel-size-guard.ps1 -BaseRef HEAD~1 -HeadRef HEAD
#>
[CmdletBinding()]
param(
    [string] $Repo,
    # Left empty, the base is resolved from the environment: an Actions pull_request base,
    # then merge-base with origin/main, then merge-base with main. Same order, same
    # fallbacks and the same loud skip as channel-wipe-guard.ps1, so the two gates in CI
    # never disagree about what "base" meant on a given run.
    [string] $BaseRef,
    # Left empty, head is the WORKING TREE - so an over-limit append fails before it is
    # even committed.
    [string] $HeadRef
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }
$Repo = (Resolve-Path -LiteralPath $Repo).ProviderPath.TrimEnd('\', '/')

# 64 KiB. DRA-73 rev 2 section 4.2 and DRA-26 rev 3 section 0.1 both write it "~64 KB";
# 65,536 is the only number in that neighbourhood a reader can reproduce without being told
# which kilobyte was meant.
$SizeLimit = 65536

# docs/Architecture.md, "Hotspot ratchet": that guard "fails the build if these grow more
# than 10% past their baseline". DRA-73 asked for this check in the same idiom, so the
# tolerance is inherited from a guard that has been calibrated against real work rather
# than picked here. It applies ONLY to grandfathered rows; the ceiling arm has none.
$BaselineTolerance = 1.10

# The same eleven files channel-wipe-guard.ps1 rosters, and deliberately the same eleven:
# one roster the policy can be stated over beats two that disagree about what a channel is
# (DRA-26 rev 3 section 8.4). Tier does not appear here - a 64 KiB limit does not care
# whether a file is a ledger, a state file or an inbox, and inventing a per-tier limit
# would be a number nobody approved.
$Roster = @(
    'HELM.md'
    'HELM-FEEDBACK.md'
    'FABLE.md'
    'FABLE-FEEDBACK.md'
    'BEVEL.md'
    'BEVEL-FEEDBACK.md'
    'SCRIBE.md'
    'SCRIBE-FEEDBACK.md'
    'SCRIBE-TESTING.md'
    'CLAUDE-FEEDBACK.md'
    'DECISIONS.md'
)

# Read at head and at base like any other tracked file, never from a parameter. The
# selftest drives the grandfather arm by COMMITTING a table into its throwaway repo, which
# means every case below exercises the path CI uses rather than a test-only one - and it
# leaves the guard with no argument that could be mistaken for a way to turn it down.
$BaselinePathRelative = 'scripts/channel-size-baseline.psd1'

$problems = @()
$notes    = @()

function Invoke-GitUtf8([string[]] $Arguments) {
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $Repo @Arguments 2>$null
    }
    finally { [Console]::OutputEncoding = $prev }
}

function Test-GitRef([string] $ref) {
    if (-not $ref) { return $false }
    $null = Invoke-GitUtf8 @('rev-parse', '--verify', '--quiet', "$ref^{commit}")
    return ($LASTEXITCODE -eq 0)
}

# $null means "not there", which is a different answer from 0 bytes and is the difference
# between "this pull request created a 200 KB channel file" and "this pull request deleted
# one". Deletion is channel-wipe-guard.ps1's subject, not this guard's.
function Read-At([string] $ref, [string] $path) {
    if ($ref) {
        $out = Invoke-GitUtf8 @('show', "${ref}:${path}")
        if ($LASTEXITCODE -ne 0) { return $null }
        return (@($out) -join "`n")
    }
    $full = Join-Path $Repo $path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $null }
    return [IO.File]::ReadAllText($full)
}

# THE UNIT. CRLF collapsed to LF, trailing newlines dropped, then UTF-8 bytes. Both
# normalisations are here because a real measurement caught them, and both fail in the
# expensive direction - a red on a pull request that grew nothing.
#
#   CRLF   This repo has no `.gitattributes` and the Windows checkout runs
#          `core.autocrlf=true`, so every blob is LF in git and CRLF on disk. Measured on
#          HELM.md at 6fdfb916: the working-tree read is 780,701 bytes against the blob's
#          777,647. That is 3,054 bytes of "growth", which is exactly the line count.
#   EOL    `Read-At` reconstructs a blob by joining git's output lines, which drops the
#          file's trailing newline; a working-tree read keeps it. So EVERY comparison of a
#          base ref against the working tree read one byte of growth on a file nobody had
#          touched - and the guard's first selftest run duly refused an untouched tree,
#          citing DECISIONS.md growing 296,214 -> 296,215. One byte is a rounding error
#          against 65,536 and a total failure as a ratchet, because the arms fire on
#          `head > base` rather than on a threshold.
function Measure-Bytes($text) {
    if ($null -eq $text) { return $null }
    $normalized = ($text -replace "`r`n", "`n").TrimEnd("`n")
    return [Text.Encoding]::UTF8.GetByteCount($normalized)
}

# Untyped on purpose - `[int] $n` coerces $null to 0, and "absent" and "empty" are not the
# same report. Same lesson as Get-Lines in channel-wipe-guard.ps1, cheaper here.
function Format-Bytes($n) {
    if ($null -eq $n) { return '(absent)' }
    return ('{0:N0} B ({1:N1} KiB)' -f $n, ($n / 1024))
}

# Import-PowerShellDataFile takes a path, and the base copy of the table lives in a git
# object rather than on disk - so it goes through a temp file. It parses DATA, not script:
# a baseline table that could execute would be a guard handing its own bypass to the pull
# request it is judging.
# $text is UNTYPED for the reason Get-Lines in channel-wipe-guard.ps1 is: `[string] $text`
# coerces $null to '', and "there is no table at this ref" then becomes "there is a table
# and it is empty" - which reaches Import-PowerShellDataFile as a parse error and reports a
# corrupt baseline file for a commit that predates the file existing. This guard's own
# introducing commit was the first thing it got wrong that way.
function Read-BaselineText($text, [string] $label) {
    if ($null -eq $text) { return $null }
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("channel-size-baseline-" + [guid]::NewGuid().ToString('N') + '.psd1')
    try {
        [IO.File]::WriteAllText($tmp, $text, [Text.UTF8Encoding]::new($false))
        return Import-PowerShellDataFile -LiteralPath $tmp
    }
    catch {
        $script:problems += "$label is not a readable PowerShell data file: $($_.Exception.Message). It must be a single @{ 'File.md' = <bytes> } hashtable and nothing else."
        return @{}
    }
    finally { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
}

# ---- resolve the base ---------------------------------------------------------------

$resolved = $null
$how = $null
if ($BaseRef) {
    if (Test-GitRef $BaseRef) { $resolved = $BaseRef; $how = 'given' }
    else {
        Write-Host "channel-size-guard: -BaseRef '$BaseRef' does not resolve; falling back to the default base." -ForegroundColor Yellow
    }
}
if (-not $resolved -and $env:GITHUB_BASE_REF) {
    foreach ($c in @("origin/$($env:GITHUB_BASE_REF)", $env:GITHUB_BASE_REF)) {
        if (Test-GitRef $c) { $resolved = $c; $how = 'GITHUB_BASE_REF'; break }
    }
}
if (-not $resolved) {
    foreach ($c in @('origin/main', 'main')) {
        if (Test-GitRef $c) {
            $mb = Invoke-GitUtf8 @('merge-base', 'HEAD', $c)
            if ($LASTEXITCODE -eq 0 -and $mb) { $resolved = ($mb | Select-Object -First 1).Trim(); $how = "merge-base with $c"; break }
        }
    }
}

if (-not $resolved) {
    # Fail OPEN, loudly, on a prefixed line so check.ps1's filter prints it. Every arm of
    # this guard is base-relative, so with no base there is genuinely nothing to compare -
    # and a silent exit 0 there would be a gate that reads as coverage while measuring
    # nothing, which is the shape this file exists to close.
    Write-Host "channel-size-guard: SKIPPED - no base commit resolvable from $Repo (shallow clone, or no origin/main). Channel ledger sizes were NOT verified." -ForegroundColor Yellow
    exit 0
}

$headLabel = if ($HeadRef) { $HeadRef } else { 'working tree' }
$baseShort = (Invoke-GitUtf8 @('rev-parse', '--short', $resolved) | Select-Object -First 1)

# ---- the grandfather table, at head and at base -------------------------------------

$baseline = Read-BaselineText (Read-At $HeadRef $BaselinePathRelative) 'The baseline table'
if ($null -eq $baseline) {
    $baseline = @{}
    $notes += "no $BaselinePathRelative at $headLabel - every rostered file is judged by the 64 KiB ceiling with no grandfathering, which is the strictest reading and not an error."
}

# ---- check A: the roster covers every channel file at the root ----------------------

$rootFiles = @(Invoke-GitUtf8 @('ls-tree', '--name-only', $(if ($HeadRef) { $HeadRef } else { 'HEAD' })))
if ($LASTEXITCODE -eq 0) {
    foreach ($f in $rootFiles) {
        $name = $f.Trim()
        if ($name -match '(?i)-FEEDBACK\.md$' -and $Roster -notcontains $name) {
            $problems += "$name is a channel file at the repo root and is NOT in this guard's roster, so nothing caps its size. Add it to `$Roster in scripts/channel-size-guard.ps1."
        }
    }
}

# ---- check B: the grandfather table may only shrink ---------------------------------
# A row is a debt, and a debt register the debtor can write to is a wish. These four rules
# are what keep the ceiling falling: raising a number, inventing a key, or keeping a row
# past the rotation that discharged it are all the same move in different clothes.

$baselineBaseText = Read-At $resolved $BaselinePathRelative
$baselineBase = Read-BaselineText $baselineBaseText 'The baseline table at base'

if ($null -eq $baselineBase) {
    $notes += "$BaselinePathRelative does not exist at base $baseShort - this pull request introduces it, so its rows are taken as given rather than checked for growth."
}
else {
    foreach ($key in $baseline.Keys) {
        if (-not $baselineBase.ContainsKey($key)) {
            $problems += "$BaselinePathRelative ADDS a grandfather row for $key at $headLabel. Rows only ever leave this table: a file that was under 64 KiB at base is governed by the ceiling, and writing itself an exemption in the same pull request that needs one is the move the ratchet exists to refuse."
            continue
        }
        if ([int]$baseline[$key] -gt [int]$baselineBase[$key]) {
            $problems += "$BaselinePathRelative RAISES $key from $($baselineBase[$key]) to $($baseline[$key]). A ratchet's ceiling only falls. If $key genuinely needs room, rotate it into docs/ops/claude-archive/channels/ and lower this row - the lift comes first and the baseline comes down with it (docs/Architecture.md, hotspot ratchet)."
        }
    }
}

foreach ($key in $baseline.Keys) {
    if ($Roster -notcontains $key) {
        $problems += "$BaselinePathRelative carries a grandfather row for '$key', which is not in this guard's roster. A row for a file nobody measures is dead weight that reads as coverage."
    }
}

# ---- the per-file arms ---------------------------------------------------------------

$measured = 0
$overLimit = 0
$grandfathered = 0
foreach ($path in $Roster) {
    $baseBytes = Measure-Bytes (Read-At $resolved $path)
    $headBytes = Measure-Bytes (Read-At $HeadRef $path)

    # Deleted at head. Not this guard's subject - channel-wipe-guard.ps1 check 1 is what
    # refuses that, and it refuses it with three incidents' worth of history behind the
    # message. Two guards reporting the same deletion in different words is two places to
    # fix when the wording is wrong.
    if ($null -eq $headBytes) { continue }
    $measured++

    # Absent at base means this pull request CREATED the file, and a channel file born over
    # the limit is exactly what the ceiling is for. 0 is the honest base size for it.
    $effectiveBase = if ($null -eq $baseBytes) { 0 } else { $baseBytes }

    if ($headBytes -gt $SizeLimit) { $overLimit++ }
    if ($baseline.ContainsKey($path)) { $grandfathered++ }

    # Not grown by this pull request: nothing to refuse. A shrink is the policy's own
    # remedy, and a file left alone is not this author's to answer for.
    if ($headBytes -le $effectiveBase) {
        if ($headBytes -gt $SizeLimit -and $headBytes -lt $effectiveBase) {
            $notes += "$path shrank $('{0:N0}' -f ($effectiveBase - $headBytes)) bytes at $headLabel and is still $('{0:N1}' -f ($headBytes / $SizeLimit))x over the 64 KiB limit - rotation in progress, not finished."
        }
        continue
    }

    if ($effectiveBase -le $SizeLimit) {
        # -- CEILING: no tolerance, no grandfather ------------------------------------
        if ($headBytes -gt $SizeLimit) {
            # ${headLabel} is braced because PowerShell reads `$headLabel:` as a
            # drive-qualified variable and refuses to parse the file at all.
            $problems += ("$path crosses the 64 KiB channel limit at ${headLabel}: $(Format-Bytes $effectiveBase) at base $baseShort -> $(Format-Bytes $headBytes). " +
                'ROTATE it - move the oldest entries verbatim into docs/ops/claude-archive/channels/<YYYY-Qn>/ and leave a pointer line in the live file. ' +
                'Do NOT delete entries to get under the limit: history moves, it is never destroyed (DRA-26 plan rev 3 principle 3), and channel-wipe-guard.ps1 will refuse a deletion anyway. ' +
                'Rotation is not an Executor edit - it is the standing EXO-CHANNEL-ROTATE card, held by a non-Executor Soft seat.')
        }
        continue
    }

    # -- RATCHET: the file was already in debt at base ---------------------------------
    if (-not $baseline.ContainsKey($path)) {
        $problems += ("$path is $(Format-Bytes $effectiveBase) at base $baseShort - already over the 64 KiB limit - and this pull request grows it to $(Format-Bytes $headBytes). " +
            "It carries no row in $BaselinePathRelative, so it has no headroom at all. " +
            'ROTATE it into docs/ops/claude-archive/channels/<YYYY-Qn>/ rather than appending to it; rotation, not deletion, is the remedy.')
        continue
    }

    $row = [int]$baseline[$path]
    $cap = [int][Math]::Floor($row * $BaselineTolerance)
    if ($headBytes -gt $cap) {
        $problems += ("$path has spent its grandfather band. Its recorded baseline is $(Format-Bytes $row) and the ratchet allows $([int](($BaselineTolerance - 1) * 100))% over it " +
            "($(Format-Bytes $cap)); this pull request takes it from $(Format-Bytes $effectiveBase) to $(Format-Bytes $headBytes). " +
            "That band was a transition allowance for a file that was already over policy on the day the ratchet shipped - it is not a new limit, and RAISING the row is not the fix. " +
            'ROTATE the file into docs/ops/claude-archive/channels/<YYYY-Qn>/ and lower its row in the same commit; the entries move, nothing is deleted. ' +
            'Rotation belongs to the standing EXO-CHANNEL-ROTATE card, not to the Executor seat that tripped this.')
    }
}

# ---- check C: a discharged row must leave in the commit that discharged it ----------
# Only when THIS pull request is what brought the file under the limit. A row left behind
# by some earlier rotation is inert - the ceiling arm governs anything at or under 64 KiB
# and never consults the table - so it gets a note rather than somebody else's red.

foreach ($key in $baseline.Keys) {
    if ($Roster -notcontains $key) { continue }
    $b = Measure-Bytes (Read-At $resolved $key)
    $h = Measure-Bytes (Read-At $HeadRef $key)
    if ($null -eq $h) { continue }
    if ($h -le $SizeLimit) {
        if ($null -ne $b -and $b -gt $SizeLimit) {
            $problems += ("$key is rotated - $(Format-Bytes $b) at base $baseShort -> $(Format-Bytes $h), under the 64 KiB limit. Its grandfather row in $BaselinePathRelative is now discharged and must be DELETED in this same pull request. " +
                'The hotspot table states the rule this follows: the lift comes first and the baseline comes down with it, because a re-baseline without a lift is a raised ceiling wearing a cleanup commit.')
        }
        else {
            $notes += "$key is $(Format-Bytes $h), under the limit, but still carries a grandfather row in $BaselinePathRelative. The row is inert - the ceiling arm governs this file now - but it should be deleted."
        }
    }
}

# ---- report -------------------------------------------------------------------------

foreach ($n in $notes) { Write-Host "channel-size-guard: note - $n" -ForegroundColor DarkCyan }

if ($problems.Count -gt 0) {
    Write-Host "channel-size-guard: FAILED (base $baseShort -> $headLabel)" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "channel-size-guard:    $p" -ForegroundColor Red }
    exit 1
}

# Print the COUNTS, not just the word. A run that measured eleven files and a run whose
# roster silently stopped matching anything both print "ok" otherwise, and one of them is
# not coverage - the mojibake list in channel-wipe-guard.ps1 was green for a month while
# matching nothing, and a printed count is what exposed it (trap 74). The over-limit number
# is the second half of the same honesty: while it is not zero, this guard is holding a line
# rather than enforcing a policy, and it should say so on every green run.
Write-Host "channel-size-guard: ok  ($measured channel files measured, $overLimit over the 64 KiB limit, $grandfathered grandfathered; base $baseShort via $how -> $headLabel)" -ForegroundColor Green
exit 0
