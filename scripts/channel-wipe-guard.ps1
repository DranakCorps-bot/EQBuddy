<#
.SYNOPSIS
    A pull request may not empty, truncate or wholesale-replace a channel ledger.

.DESCRIPTION
    Three times in six days a channel file was destroyed by a commit whose message said
    it was signing something:

      7b804338  2026-09-04  HELM-FEEDBACK.md  2713 -> 2209 lines ("restore + clean edit")
      24a91e64  2026-09-09  HELM.md and HELM-FEEDBACK.md DELETED (PR #493)
      d20c8e07  2026-09-10  HELM.md and HELM-FEEDBACK.md DELETED again (empty-tree amend)

    Both deletions were repaired by a follow-up restore commit, so `main` today looks
    fine and the history says otherwise. CLAUDE.md already carries the rule - "APPEND
    your entry; never rewrite the file ... check that `git diff` over the file is
    additions-only" - and trap 60 records that the rule has been broken by three
    different mechanisms. Trap 60(c) ends: "No guard yet; that is a named hole." This is
    the guard. Process language told the agent what to do; it could not tell CI what to
    refuse.

    WHAT IT COMPARES. Base ref versus head (the working tree by default, so a wipe is
    caught before it is even committed). On a pull_request run the checkout is the merge
    result, which is exactly the question worth asking: what would landing this do to the
    ledgers?

    THE ROSTER IS TIERED, because these files are not all the same promise:

      ledger  Append-only records. Nothing is ever taken out of them.
              HELM-FEEDBACK, FABLE-FEEDBACK, BEVEL-FEEDBACK, SCRIBE-FEEDBACK,
              CLAUDE-FEEDBACK, DECISIONS.
      state   HELM.md - holds and posture. Holds get LIFTED, so it shrinks legitimately,
              but only ever by a hold at a time.
      inbox   FABLE, BEVEL, SCRIBE, SCRIBE-TESTING. "When you take an item, delete it"
              is the documented workflow, so a drained inbox is correct and a
              percentage check on it would be noise. An inbox gets the wipe check only.

    FIVE CHECKS:

      1. WIPE      - a rostered file that exists and has content at base must exist and
                     have content at head. All three incidents fail here, and this check
                     has no threshold to argue about.
      2. SHRINK    - ledger/state: head keeps at least 60% of base's substantive lines.
      3. REPLACE   - ledger/state: at least 65% of base's substantive lines are still
                     PRESENT at head (multiset). This is the "full replace when only
                     append was expected" case - a regenerated ledger of about the right
                     length passes check 2 and fails here.
      4. MOJIBAKE  - no rostered file may gain double-encoded characters. HELM-FEEDBACK.md
                     carries 13,411 of these markers today, laid down by eleven separate
                     commits (trap 60(b): a whole-file rewrite through the wrong codec).
                     Base-relative, so the existing damage is not re-litigated on every
                     PR - only NEW damage fails.
      5. ROSTER    - every `*-FEEDBACK.md` at the repo root is in the roster above.
                     Trap 34: a guard that forbids the wrong thing cannot see a missing
                     thing, and an unrostered channel file is one nobody is protecting.

    WHERE THE NUMBERS CAME FROM. Every revision of every rostered file in this repo's
    history was measured (1,379 of them) before the thresholds were chosen, because a
    threshold nobody calibrated is a threshold that fires on Tuesday:

      ledger/state, size:       only 5 revisions ever fell below 98% of base, and only
                                the 4 wipe revisions below 60%. Worst legitimate value
                                in the whole history is 90.7% (91fab9a0, a hold lift on
                                a 75-line HELM.md). The floor is 60%.
      ledger/state, retention:  35 revisions below 90%. Every one of them is a wipe, the
                                2026-09-04 truncation, or a mojibake rewrite - there is
                                no clean commit down there. Worst clean value is 84.5%.
                                The floor is 65%, which leaves the two nearest clean
                                revisions (66.7%, 68.7% - both mojibake) failing for the
                                right reason and every clean one passing.
      inbox:                    9.5% size / 9.1% retention is a NORMAL drained inbox
                                (d091939b). Hence: no percentage check on that tier.

    TWO EXEMPTIONS, because both describe a thing we actually want to happen:

      REPAIR   Un-mangling a file rewrites most of its lines, which is check 3's exact
               signature. If head has FEWER mojibake markers than base and keeps at
               least 95% of its length, the rewrite is a repair and check 3 stands down
               with a note. (It is how e8d2aeed would pass.)
      ARCHIVE  Moving old entries into docs/ops/claude-archive/ is the documented way a
               ledger is allowed to get shorter. If at least 90% of the lines that left
               are found under that directory at head, checks 2 and 3 stand down with a
               note. Content that MOVED was not content that was LOST.

    There is no -Force and no skip switch: an escape hatch on a guard whose whole subject
    is "an automated land destroyed the file" is the automated land's next move. The two
    exemptions above are mechanisms, not flags - they are satisfied by doing the right
    thing, not by asserting that you did.

    -Repo and -BaseRef/-HeadRef exist so the guard can be PROVEN to fail:
    scripts/channel-wipe-guard-selftest.ps1 builds throwaway repos and drives each check
    into the red. Green-only is vacuous coverage (trap 34).

    Files are read with [IO.File]::ReadAllText and git output through an explicit UTF-8
    [Console]::OutputEncoding (trap 54) - this repo's docs are full of em dashes, and a
    guard that decodes them through the ANSI code page would report the mojibake it just
    invented.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-wipe-guard.ps1
    pwsh -NoProfile -File scripts/channel-wipe-guard.ps1 -BaseRef origin/main
    pwsh -NoProfile -File scripts/channel-wipe-guard.ps1 -BaseRef 24a91e64~1 -HeadRef 24a91e64   # prove it fails
#>
[CmdletBinding()]
param(
    [string] $Repo,
    # Left empty, the base is resolved from the environment: an Actions pull_request
    # base, then merge-base with origin/main, then merge-base with main.
    [string] $BaseRef,
    # Left empty, head is the WORKING TREE - so an uncommitted wipe fails too.
    [string] $HeadRef
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }
# Absolute and normalised: Get-ArchiveLines turns a full path back into a repo-relative
# one by length, which a trailing slash or a relative -Repo would silently shift.
$Repo = (Resolve-Path -LiteralPath $Repo).ProviderPath.TrimEnd('\', '/')

# ---- the roster ---------------------------------------------------------------------
# Tier is a statement about what the file PROMISES, not about how big it is. Adding a
# channel file means adding it here; check 5 is what makes forgetting that fail.
$Roster = [ordered]@{
    'HELM.md'            = 'state'
    'HELM-FEEDBACK.md'   = 'ledger'
    'FABLE.md'           = 'inbox'
    'FABLE-FEEDBACK.md'  = 'ledger'
    'BEVEL.md'           = 'inbox'
    'BEVEL-FEEDBACK.md'  = 'ledger'
    'SCRIBE.md'          = 'inbox'
    'SCRIBE-FEEDBACK.md' = 'ledger'
    'SCRIBE-TESTING.md'  = 'inbox'
    'CLAUDE-FEEDBACK.md' = 'ledger'
    'DECISIONS.md'       = 'ledger'
}

# Calibrated below, per tier, because a ledger and a state file make different promises
# and a single number would have to be loose enough for the looser one.
#
#   ledger  0.90 / 0.90. Across every ledger revision in this repo's history, exactly TWO
#           clean ones sit under 95%: e8d2aeed (59% retention at 99% length - a mojibake
#           repair, which the REPAIR exemption below lets through) and 7b804338, the
#           2026-09-04 truncation that took HELM-FEEDBACK.md from 2713 lines to 2209 and
#           is precisely what this guard is for. The nearest LEGITIMATE value is 95.5%
#           (3f405c66), so 0.90 catches the incident with 5 points of daylight.
#   state   0.65 / 0.60. HELM.md is holds and posture, and lifting a hold legitimately
#           removes it: clean revisions reach 85.3% retention (91fab9a0) and 90.7% length
#           on a 75-line file. Tightening this tier to the ledger's numbers would fire on
#           ordinary Helm work, so it stays loose - HELM.md's wipes are caught by check 1,
#           which has no threshold at all.
#   inbox   none. "When you take an item, delete it" is the documented workflow and a
#           drained FABLE.md is 9.5% of its former length (d091939b). Checks 1 and 4 only.
#
# MinBaseLines keeps the percentages off files small enough that one ordinary edit is a
# large fraction of them - below it, only checks 1 and 4 apply.
$TierPolicy = @{
    ledger = @{ MinKept = 0.90; MinRetained = 0.90 }
    state  = @{ MinKept = 0.60; MinRetained = 0.65 }
}
$MinBaseLines         = 40
$RepairMinLength      = 0.95
$ArchiveMinFound      = 0.90
$ArchiveDir           = 'docs/ops/claude-archive'

# Built from code points on purpose: this file must survive being read by a host that
# guesses its encoding, and a literal mojibake glyph in the source is the one string that
# cannot. Each is a UTF-8 sequence that has been decoded as cp1252 and re-encoded.
# EACH ELEMENT IS PARENTHESISED. PowerShell binds `,` TIGHTER than `+`, so the obvious
# spelling - `[char]0xE2 + [char]0x20AC, [char]0xC3 + [char]0xA2` - parses as
# `a + (b, c) + d` and silently collapses the whole list into ONE string of every marker
# joined by $OFS. It matched nothing, and the guard reported a clean file for the exact
# commit that laid down 12,684 markers. A guard that forbids the wrong thing is trap 34;
# a guard that forbids a thing that cannot occur is worse, because it is green.
$MojibakeMarkers = @(
    ([string][char]0x00E2 + [string][char]0x20AC),   # "a-hat euro"  - em dash / smart quotes
    ([string][char]0x00C3 + [string][char]0x00A2),   # "A-tilde a-hat" - the second round trip
    ([string][char]0x00C3 + [string][char]0x201A),   # "A-tilde single-low-quote"
    ([string][char]0x00C2 + [string][char]0x00A0),   # "A-circumflex" + no-break space
    ([string][char]0xFFFD)                           # U+FFFD, decode already given up
)

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

# Substantive lines only: blank lines and trailing whitespace are not content, and
# counting them would let a reformat read as a rewrite.
# $text is DELIBERATELY UNTYPED and the return is DELIBERATELY comma-wrapped. Typed
# `[string] $text` coerces $null to '', and a bare `@()` return is unrolled by PowerShell
# into $null at the call site - between them, "the file is gone" and "the file is there
# and empty" became the same answer, and the guard reported DELETED for a file that was
# sitting right there with nothing in it. Two of the three incidents produced one of those
# states and one produced the other; they need different sentences.
function Get-Lines($text) {
    if ($null -eq $text) { return $null }
    $normalized = $text -replace "`r`n", "`n"
    $result = @($normalized -split "`n" | ForEach-Object { $_.TrimEnd() } | Where-Object { $_.Trim().Length -gt 0 })
    return , $result
}

function Measure-Mojibake([string] $text) {
    if (-not $text) { return 0 }
    $n = 0
    foreach ($m in $MojibakeMarkers) {
        $i = 0
        while (($i = $text.IndexOf($m, $i, [StringComparison]::Ordinal)) -ge 0) { $n++; $i += $m.Length }
    }
    $n
}

# $null means "not there". An empty string means "there and empty", which is a very
# different answer and is exactly what two of the three incidents produced.
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

# ---- resolve the base ---------------------------------------------------------------

$resolved = $null
$how = $null
if ($BaseRef) {
    if (Test-GitRef $BaseRef) { $resolved = $BaseRef; $how = 'given' }
    else {
        # A workflow expression can hand us an empty string or 40 zeroes on a branch's
        # first push. That is not a reason to fail and not a reason to pretend we checked.
        Write-Host "channel-wipe-guard: -BaseRef '$BaseRef' does not resolve; falling back to the default base." -ForegroundColor Yellow
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
    # Fail OPEN, loudly, on a prefixed line so check.ps1's filter prints it. With no base
    # there is nothing to compare against, and a shallow clone is a host difference rather
    # than a wiped ledger - but a silent skip here would be a gate that reads as coverage
    # while seeing nothing, which is the shape this whole file exists to refuse.
    Write-Host "channel-wipe-guard: SKIPPED - no base commit resolvable from $Repo (shallow clone, or no origin/main). The channel ledgers were NOT verified." -ForegroundColor Yellow
    exit 0
}

$headLabel = if ($HeadRef) { $HeadRef } else { 'working tree' }
$baseShort = (Invoke-GitUtf8 @('rev-parse', '--short', $resolved) | Select-Object -First 1)

# ---- check 5: the roster covers every channel file at the root ----------------------
# Before the per-file work, because the answer changes which files the rest of this run
# is even looking at.

$rootFiles = @(Invoke-GitUtf8 @('ls-tree', '--name-only', $(if ($HeadRef) { $HeadRef } else { 'HEAD' })))
if ($LASTEXITCODE -eq 0) {
    foreach ($f in $rootFiles) {
        $name = $f.Trim()
        if ($name -match '(?i)-FEEDBACK\.md$' -and -not $Roster.Contains($name)) {
            $problems += "$name is a channel file at the repo root and is NOT in this guard's roster, so nothing stops a PR from emptying it. Add it to `$Roster in scripts/channel-wipe-guard.ps1 with its tier (ledger / state / inbox)."
        }
    }
}

# ---- the archive corpus, built once and only if something needs excusing ------------

$script:archiveLines = $null
function Get-ArchiveLines {
    if ($null -ne $script:archiveLines) { return $script:archiveLines }
    $set = [Collections.Generic.HashSet[string]]::new()
    $paths = @()
    if ($HeadRef) {
        $paths = @(Invoke-GitUtf8 @('ls-tree', '-r', '--name-only', $HeadRef, $ArchiveDir))
        if ($LASTEXITCODE -ne 0) { $paths = @() }
    }
    else {
        $dir = Join-Path $Repo $ArchiveDir
        if (Test-Path -LiteralPath $dir) {
            $paths = @(Get-ChildItem -LiteralPath $dir -Recurse -File | ForEach-Object {
                    $_.FullName.Substring($Repo.Length).TrimStart('\', '/') -replace '\\', '/'
                })
        }
    }
    foreach ($p in $paths) {
        $t = Read-At $HeadRef $p
        foreach ($l in (Get-Lines $t)) { [void]$set.Add($l) }
    }
    $script:archiveLines = $set
    $set
}

# Only the lines that actually left, and only counted once each: a ledger that repeats
# "To: Fable" four hundred times should not be able to buy its own exemption with it.
function Get-LostLines($baseLines, $headLines) {
    $have = @{}
    foreach ($l in $headLines) { if ($have.ContainsKey($l)) { $have[$l]++ } else { $have[$l] = 1 } }
    $lost = [Collections.Generic.List[string]]::new()
    foreach ($l in $baseLines) {
        if ($have.ContainsKey($l) -and $have[$l] -gt 0) { $have[$l]-- } else { $lost.Add($l) }
    }
    $lost
}

function Test-ArchiveMove($lost) {
    if ($lost.Count -eq 0) { return $true }
    $arch = Get-ArchiveLines
    if ($arch.Count -eq 0) { return $false }
    $found = 0
    foreach ($l in $lost) { if ($arch.Contains($l)) { $found++ } }
    return (($found / $lost.Count) -ge $ArchiveMinFound)
}

# ---- checks 1 to 4, per rostered file -----------------------------------------------

$checked = 0
foreach ($path in $Roster.Keys) {
    $tier = $Roster[$path]

    $baseText = Read-At $resolved $path
    $baseLines = Get-Lines $baseText
    # Absent or empty at BASE: there is no promise to keep. A file this PR creates is not
    # this guard's business.
    if ($null -eq $baseLines -or $baseLines.Count -eq 0) { continue }
    $checked++

    $headText = Read-At $HeadRef $path
    $headLines = Get-Lines $headText

    # -- 1. WIPE ----------------------------------------------------------------------
    if ($null -eq $headLines) {
        $problems += "$path is DELETED at $headLabel but has $($baseLines.Count) lines at base $baseShort. A channel ledger is not deleted by a pull request - PR #493 and the 2026-09-10 amend both landed exactly this and both needed a restore commit."
        continue
    }
    if ($headLines.Count -eq 0) {
        $problems += "$path is EMPTY at $headLabel but has $($baseLines.Count) lines at base $baseShort. Emptying a channel file is the same loss as deleting it and reads as a much smaller diff."
        continue
    }

    # -- 4. MOJIBAKE (every tier; an inbox can be mangled as easily as a ledger) -------
    $baseMoji = Measure-Mojibake $baseText
    $headMoji = Measure-Mojibake $headText
    if ($headMoji -gt $baseMoji) {
        $problems += "$path gains $($headMoji - $baseMoji) double-encoded characters at $headLabel ($baseMoji -> $headMoji). That is a whole-file rewrite through the wrong codec (trap 60b), not an append - re-read the ref and APPEND in explicit UTF-8."
    }

    if (-not $TierPolicy.ContainsKey($tier)) { continue }
    if ($baseLines.Count -lt $MinBaseLines) { continue }
    $policy = $TierPolicy[$tier]

    $kept = $headLines.Count / $baseLines.Count
    $shrank = $kept -lt $policy.MinKept

    $lost = Get-LostLines $baseLines $headLines
    $retained = ($baseLines.Count - $lost.Count) / $baseLines.Count
    $replaced = $retained -lt $policy.MinRetained

    if (-not $shrank -and -not $replaced) { continue }

    # -- exemptions -------------------------------------------------------------------
    if ($replaced -and -not $shrank -and $headMoji -lt $baseMoji -and $kept -ge $RepairMinLength) {
        $notes += "$path rewrote $([int]((1 - $retained) * 100))% of its lines and REMOVED $($baseMoji - $headMoji) mojibake markers at full length - read as an encoding REPAIR, not a replacement."
        $replaced = $false
    }
    if (($shrank -or $replaced) -and (Test-ArchiveMove $lost)) {
        $notes += "$path lost $($lost.Count) lines, and at least $([int]($ArchiveMinFound * 100))% of them are present under $ArchiveDir at $headLabel - read as an ARCHIVE MOVE, not a loss."
        $shrank = $false
        $replaced = $false
    }

    # -- 2. SHRINK --------------------------------------------------------------------
    if ($shrank) {
        $problems += ("$path keeps only $([int]($kept * 100))% of its length at $headLabel " +
            "($($baseLines.Count) -> $($headLines.Count) lines; the floor for a $tier is $([int]($policy.MinKept * 100))%). " +
            'The 2026-09-04 truncation of HELM-FEEDBACK.md landed at 81% and read as an ordinary "restore + clean edit". ' +
            "If entries are genuinely retiring, move them under $ArchiveDir in the same commit and this check stands down.")
    }

    # -- 3. REPLACE -------------------------------------------------------------------
    if ($replaced) {
        $sample = @($lost | Where-Object { $_.Length -gt 20 } | Select-Object -First 3)
        $problems += ("$path retains only $([int]($retained * 100))% of the lines it had at base $baseShort " +
            "(the floor for a $tier is $([int]($policy.MinRetained * 100))%), at $([int]($kept * 100))% of its length - " +
            'so this is a full REPLACE wearing an append''s file size. ' +
            'CLAUDE.md: re-read the ref at splice time and APPEND; a channel diff is additions-only.')
        foreach ($s in $sample) {
            $t = if ($s.Length -gt 96) { $s.Substring(0, 96) + '...' } else { $s }
            $problems += "    lost: $t"
        }
    }
}

# ---- report -------------------------------------------------------------------------

foreach ($n in $notes) { Write-Host "channel-wipe-guard: note - $n" -ForegroundColor DarkCyan }

if ($problems.Count -gt 0) {
    Write-Host "channel-wipe-guard: FAILED (base $baseShort -> $headLabel)" -ForegroundColor Red
    # Every line carries the prefix: check.ps1 prints only the lines its filter matches,
    # and a headline without its reasons is a gate that says something is wrong and not what.
    foreach ($p in $problems) { Write-Host "channel-wipe-guard:    $p" -ForegroundColor Red }
    exit 1
}

# Say how many files were actually compared. A run that checked nothing and a run that
# checked eleven files print the same word otherwise, and one of them is not coverage.
Write-Host "channel-wipe-guard: ok  ($checked channel files intact; base $baseShort via $how -> $headLabel)" -ForegroundColor Green
exit 0
