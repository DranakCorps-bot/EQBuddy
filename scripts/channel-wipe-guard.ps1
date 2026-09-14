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
      3. REPLACE   - ledger/state, and it has TWO ARMS, because the two ask different
                     questions about the same rewrite:

                     3a. LINE retention - at least 65% of base's substantive lines are
                         still PRESENT at head (multiset). Sensitive to any rewrite,
                         which is its strength and also why it needs the REPAIR
                         exemption below: re-encoding a file moves every line that
                         carries a dash without losing a word of it.
                     3b. ENTRY retention - at least 85% of base's ENTRY HEADINGS are
                         still present at head, compared through a key with all
                         non-ASCII STRIPPED, whitespace collapsed and case FOLDED.

                     3b is the key PR #507 was built around, and the reason it is worth
                     carrying as well as 3a rather than instead of it:

                       - It cannot be moved by encoding churn. `ff6853ba`, an honest
                         "merge main (additions-only KEEP)", re-encoded HELM.md so that
                         E2 80 94 (a UTF-8 em dash) became C3 A2 E2 82 AC E2 80 9D (the
                         same dash through cp1252 - trap 54's shape). Every line
                         carrying a dash compared unequal while saying exactly what it
                         said before. Strip non-ASCII and the entry is the same entry.
                       - Because of that it is NOT excused by the REPAIR exemption, and
                         that closes a real hole in arm 3a alone: a rewrite that removes
                         mojibake at full length is waved through by REPAIR, so a commit
                         that repaired the encoding AND quietly dropped forty entries
                         passed every check. An encoding repair does not move an entry
                         key, so a repair that also loses entries now has nothing left
                         to hide behind. `channel-wipe-guard-selftest.ps1` case 13 is
                         exactly that commit, and it is green on arm 3a.
                       - A ledger's unit is the ENTRY - "APPEND your entry" - so losing
                         one is the harm being named, and a percentage of entries is a
                         number a human can argue with in review.

                     Entries are found ANYWHERE, not only at the start of a line, and
                     the key is capped at 80 characters. Both are forced by the state of
                     the file this guard exists for: `c7a597a8` collapsed
                     HELM-FEEDBACK.md's 8,677 lines into 2, so `origin/main` today has
                     EIGHT line-start headings standing for 1,051 entries. A line-start
                     reading would have given this arm no coverage at all on the one
                     ledger that has been destroyed twice - a detector aimed at nothing
                     (trap 74). Scanning mid-line recovers 3,523 of them. The cap keeps
                     a recovered key from swallowing its entry's whole body, so an edit
                     inside an entry is not read as the loss of it.

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
      ENTRY retention:          measured the same way, over all 1,143 ledger/state
                                revision pairs with at least four entries at base. The
                                separation is cleaner than any line measure in this file:

                                  ledger  n=909. Three revisions below 90%, and all
                                          three are the incidents - 24a91e64 (0.000,
                                          PR #493), 7b804338 (0.485, the truncation),
                                          c7a597a8 (0.594, the line collapse). The
                                          worst CLEAN value in the whole history is
                                          0.941 (de05c512, 17 entries).
                                  state   n=234. One revision below 90%: the deletion,
                                          at 0.000. Worst clean is 0.944 (3e68e2a0, a
                                          shipped-and-closed loop on an 18-entry file).
                                  inbox   n=314, min 0.125, fifteen clean revisions
                                          below 90% - a drained inbox again. No entry
                                          check on that tier either, for the same
                                          reason there is no line check.

                                The floor is 85% for both checked tiers: about six
                                points under the worst clean revision ever recorded and
                                thirty-six above the truncation it has to catch.
      inbox:                    9.5% size / 9.1% retention is a NORMAL drained inbox
                                (d091939b). Hence: no percentage check on that tier.

    TWO EXEMPTIONS, because both describe a thing we actually want to happen:

      REPAIR   Un-mangling a file rewrites most of its lines, which is check 3a's exact
               signature. If head has FEWER mojibake markers than base and keeps at
               least 95% of its length, the rewrite is a repair and check 3a stands down
               with a note. (It is how e8d2aeed would pass.)

               IT DOES NOT TOUCH 3b, and that asymmetry is the point. The entry key has
               no non-ASCII in it, so a genuine repair moves no entry at all and needs
               no excusing; anything that DID lose entries was not only repairing.
               Waiving 3b here would hand every future rewrite a one-line cover story.
      ARCHIVE  Moving old entries into docs/ops/claude-archive/ is the documented way a
               ledger is allowed to get shorter. If at least 90% of the lines that left
               are found under that directory at head, checks 2 and 3a stand down with a
               note; 3b stands down on the same test applied to the entry keys that
               left. Content that MOVED was not content that was LOST.

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
    ledger = @{ MinKept = 0.90; MinRetained = 0.90; MinEntries = 0.85 }
    state  = @{ MinKept = 0.60; MinRetained = 0.65; MinEntries = 0.85 }
}
$MinBaseLines         = 40
$RepairMinLength      = 0.95
$ArchiveMinFound      = 0.90
$ArchiveDir           = 'docs/ops/claude-archive'

# Check 3b, the #507 key. An ENTRY is a markdown heading of two to six hashes; one hash is
# excluded on purpose because `# comment` is the prefix of every PowerShell line quoted in
# these files and would invent entries out of pasted scripts.
#
# It is matched at the start of a line OR after any whitespace inside one. That is not
# tidiness - it is the difference between coverage and none. `c7a597a8` collapsed
# HELM-FEEDBACK.md's 8,677 lines into 2, so at origin/main today a line-start reading finds
# EIGHT headings in the file that has been deleted twice, and the mid-line reading finds
# 3,523. A percentage over eight things is not a measurement.
#
# The key is capped, because a recovered mid-line entry has no end: it runs to the next
# entry, which on a collapsed line is the whole body. Capped at 80 ASCII characters the key
# is the heading and a little of what follows - distinctive enough that two entries do not
# collide (1,009 distinct keys over HELM-FEEDBACK.md's 3,523), and short enough that
# editing a typo inside an entry does not read as deleting it.
$EntryPattern         = [regex] '(?:(?<=\A)|(?<=\n)|(?<=\s))#{2,6}[ \t]+\S'
$EntryKeyCap          = 80
$MinBaseEntries       = 20

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

# The comparison key of check 3b: an entry heading with all non-ASCII STRIPPED, whitespace
# collapsed and case FOLDED. Stripping is what makes it survive an encoding round trip -
# `ff6853ba` turned every em dash in HELM.md into mojibake without changing a word, and a
# guard that fails a correct merge is worth less than no guard at all.
# Same $null-vs-empty contract as Get-Lines, and the same comma-wrapped return: an unrolled
# empty array would make "no entries" and "file is gone" the same answer.
function Get-EntryKeys($text) {
    if ($null -eq $text) { return $null }
    $t = $text -replace "`r`n", "`n"
    $starts = @($EntryPattern.Matches($t) | ForEach-Object { $_.Index })
    $keys = [Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $starts.Count; $i++) {
        $s = $starts[$i]
        # An entry ends at the next entry or at end of line, whichever comes first. The
        # end-of-line half is what keeps an ordinary heading's key from running on into the
        # paragraph under it; the next-entry half is what bounds one recovered from a
        # collapsed line, where there is no newline to stop at.
        $end = if ($i + 1 -lt $starts.Count) { $starts[$i + 1] } else { $t.Length }
        $nl = $t.IndexOf("`n", $s)
        if ($nl -ge 0 -and $nl -lt $end) { $end = $nl }
        $seg = $t.Substring($s, $end - $s)
        $seg = [regex]::Replace($seg, '[^\x20-\x7E]', '')
        $seg = ([regex]::Replace($seg, '\s+', ' ')).Trim().ToLowerInvariant()
        if ($seg.Length -gt $EntryKeyCap) { $seg = $seg.Substring(0, $EntryKeyCap) }
        if ($seg.Length -gt 0) { [void]$keys.Add($seg) }
    }
    return , $keys.ToArray()
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
$script:archiveKeys  = $null
function Build-ArchiveCorpus {
    if ($null -ne $script:archiveLines) { return }
    $set = [Collections.Generic.HashSet[string]]::new()
    $keys = [Collections.Generic.HashSet[string]]::new()
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
        foreach ($k in (Get-EntryKeys $t)) { [void]$keys.Add($k) }
    }
    $script:archiveLines = $set
    $script:archiveKeys = $keys
}

function Get-ArchiveLines { Build-ArchiveCorpus; $script:archiveLines }
function Get-ArchiveKeys { Build-ArchiveCorpus; $script:archiveKeys }

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

# The same question as Test-ArchiveMove, asked of entries. It gets its own corpus rather
# than reusing the line set because an archived entry's heading has been through the same
# key - stripped and folded - and would not match the raw line it came from.
function Test-ArchiveMoveEntries($lostKeys) {
    if ($lostKeys.Count -eq 0) { return $true }
    $arch = Get-ArchiveKeys
    if ($arch.Count -eq 0) { return $false }
    $found = 0
    foreach ($k in $lostKeys) { if ($arch.Contains($k)) { $found++ } }
    return (($found / $lostKeys.Count) -ge $ArchiveMinFound)
}

# ---- checks 1 to 4, per rostered file -----------------------------------------------

$checked = 0
$entriesCompared = 0
$filesWithEntries = 0
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

    # -- 3b. ENTRY retention, the #507 key --------------------------------------------
    # Its own floor and its own lost-list, because the two arms get different exemptions
    # further down: an encoding repair excuses 3a and must never excuse this.
    $baseKeys = Get-EntryKeys $baseText
    $headKeys = Get-EntryKeys $headText
    $lostKeys = @()
    $entryRetained = $null
    $entriesLost = $false
    if ($baseKeys.Count -ge $MinBaseEntries) {
        $lostKeys = Get-LostLines $baseKeys $headKeys
        $entryRetained = ($baseKeys.Count - $lostKeys.Count) / $baseKeys.Count
        $entriesLost = $entryRetained -lt $policy.MinEntries
        $entriesCompared += $baseKeys.Count
        $filesWithEntries++
    }
    else {
        # Say it out loud. A file under the floor gets checks 1, 2, 3a and 4 and no entry
        # arm, and a guard that silently applies four checks where the docs promise five
        # is the shape trap 74 is about. CLAUDE-FEEDBACK.md (13 entries) is legitimately
        # down here; HELM-FEEDBACK.md would be too if entries were read line-start only.
        $notes += "$path has $($baseKeys.Count) entries at base, under the $MinBaseEntries needed for a percentage to mean anything - checked for wipe, shrink, line-replace and mojibake, but NOT for entry loss."
    }

    if (-not $shrank -and -not $replaced -and -not $entriesLost) { continue }

    # -- exemptions -------------------------------------------------------------------
    # REPAIR stands down 3a ONLY. Stripping non-ASCII is what the entry key does, so a
    # real repair does not move one and has nothing here to ask for; a rewrite that
    # removed mojibake AND lost entries is not the thing this exemption is named after.
    if ($replaced -and -not $shrank -and $headMoji -lt $baseMoji -and $kept -ge $RepairMinLength) {
        $notes += "$path rewrote $([int]((1 - $retained) * 100))% of its lines and REMOVED $($baseMoji - $headMoji) mojibake markers at full length - read as an encoding REPAIR, not a replacement. (Entry retention is judged separately and is not excused by this.)"
        $replaced = $false
    }
    if (($shrank -or $replaced) -and (Test-ArchiveMove $lost)) {
        $notes += "$path lost $($lost.Count) lines, and at least $([int]($ArchiveMinFound * 100))% of them are present under $ArchiveDir at $headLabel - read as an ARCHIVE MOVE, not a loss."
        $shrank = $false
        $replaced = $false
    }
    if ($entriesLost -and (Test-ArchiveMoveEntries $lostKeys)) {
        $notes += "$path lost $($lostKeys.Count) entries, and at least $([int]($ArchiveMinFound * 100))% of them are present under $ArchiveDir at $headLabel - read as an ARCHIVE MOVE, not a loss."
        $entriesLost = $false
    }

    if (-not $shrank -and -not $replaced -and -not $entriesLost) { continue }

    # -- 2. SHRINK --------------------------------------------------------------------
    if ($shrank) {
        $problems += ("$path keeps only $([int]($kept * 100))% of its length at $headLabel " +
            "($($baseLines.Count) -> $($headLines.Count) lines; the floor for a $tier is $([int]($policy.MinKept * 100))%). " +
            'The 2026-09-04 truncation of HELM-FEEDBACK.md landed at 81% and read as an ordinary "restore + clean edit". ' +
            "If entries are genuinely retiring, move them under $ArchiveDir in the same commit and this check stands down.")
    }

    # -- 3a. REPLACE (lines) ----------------------------------------------------------
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

    # -- 3b. REPLACE (entries) --------------------------------------------------------
    # Named separately from 3a even when both fire, because they are different evidence:
    # 3a says the TEXT moved, 3b says an ENTRY is gone. Only the second survives an
    # argument about encoding.
    if ($entriesLost) {
        $sample = @($lostKeys | Select-Object -First 3)
        $problems += ("$path retains only $([int]($entryRetained * 100))% of the ENTRIES it had at base $baseShort " +
            "($($baseKeys.Count) -> $($headKeys.Count); $($lostKeys.Count) gone; the floor for a $tier is $([int]($policy.MinEntries * 100))%). " +
            'Entries are compared with non-ASCII stripped and case folded, so re-encoding, re-indenting and reordering CANNOT cause this - ' +
            'only removing the entry can. ' +
            "If they are genuinely retiring, move them under $ArchiveDir in the same commit and this check stands down.")
        foreach ($s in $sample) {
            $problems += "    lost entry: $s"
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

# Say how many files were actually compared, AND how many entries the 3b arm weighed. A run
# that checked nothing and a run that checked eleven files print the same word otherwise,
# and one of them is not coverage. The entry number is there for the same reason: the
# mojibake list was green for a month while matching nothing, and it was a printed COUNT
# that exposed it (trap 74). A sudden zero here means the pattern stopped finding entries,
# not that the ledgers got safer.
Write-Host "channel-wipe-guard: ok  ($checked channel files intact, $entriesCompared entries compared across $filesWithEntries of them; base $baseShort via $how -> $headLabel)" -ForegroundColor Green
exit 0
