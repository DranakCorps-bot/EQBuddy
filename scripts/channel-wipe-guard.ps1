<#
.SYNOPSIS
    A channel ledger is append-only history. This proves a branch has not wiped one.

.DESCRIPTION
    On 2026-09-09 PR #493 merged with `HELM.md` and `HELM-FEEDBACK.md` DELETED - 12,208
    lines, the entire Helm channel and its whole reply history, gone in a merge whose own
    commit message said "additions-only KEEP". It was restored by hand in `4cb742d5`. On
    2026-09-10 it happened AGAIN (#502) and was restored again in `04b2b7aa`: another
    12,444 lines. Twice in two days, on the file that carries every ruling Helm has ever
    signed, and both times everything else was green - the build does not read markdown,
    no unit test opens these files, and `git diff` on a branch nobody diffed says nothing.

    CLAUDE.md trap 60 already names this exact hazard ("a channel diff is additions-only",
    and 60(c) on the silently truncated append) and ends with the line this script exists
    to delete: **"No guard yet; that is a named hole."** Process language had two chances
    and lost both. The rule is now a mechanism.

    THE COMPARISON IS AGAINST THE MERGE-BASE, not against HEAD~1. A channel branch is
    routinely rebased and merged, so the previous commit that touched the file is often on
    another line of development; measured against a raw first parent, real history shows
    "losses" of 63% that are nothing of the kind. The merge-base with the base branch IS
    what a PR proposes to change, which is the question being asked.

    Four checks, per file, and only for files that EXIST at the merge-base - a channel a
    role has not opened yet is not a violation, and a NEW file is what check 1 is not
    about:

      1. DELETED - present at base, absent now. The #493/#502 shape exactly, and the one
         that cost two hand restorations.
      2. EMPTIED - present but zero bytes, or nothing but whitespace. A truncate-to-zero
         leaves the path in the tree, so check 1 never sees it; this is the same
         catastrophe wearing a file that still exists.
      3. EXTREME SHRINK - the file lost more than -MaxShrinkPercent of its BYTES. Measured
         with `git cat-file -s` against the blob and `.Length` on disk, so no text decoding
         sits between the guard and the number.
      4. ENTRIES LOST - fewer than -MinEntryRetentionPercent of the `##` entry headings
         that exist at base still exist now. This is the full-replace case: a whole-file
         rewrite can be BIGGER than what it replaced, so checks 2 and 3 both pass it while
         every ruling in the ledger is gone. It is also trap 60(c)'s silently truncated
         append, which loses the tail and grows the head at the same time.

    WHY HEADINGS AND NOT LINES. The first version of check 4 compared every non-blank
    line, and on real history it reported that `ff6853ba` - a routine "merge main into
    helm/ssc-487 (additions-only KEEP)" - had destroyed 63% of HELM.md. It had not. That
    commit re-encoded the file: `E2 80 94`, a correct UTF-8 em dash, became
    `C3 A2 E2 82 AC E2 80 9D`, the same dash decoded as Latin-1 and re-encoded (trap 54's
    shape, and 60(c)'s). Every one of the hundreds of lines carrying an em dash compared
    unequal while saying exactly what it said before. A guard that fails a correct merge
    is worth less than no guard (whatsnew-guard's lesson, in as many words), so the
    comparison key is the ENTRY HEADING with every non-ASCII character stripped,
    whitespace collapsed and case folded. Encoding churn cannot move it, re-indentation
    cannot move it, and reordering cannot move it - only deleting the entry can.

    THE THRESHOLDS ARE MEASURED, NOT GUESSED. Over the last 18 commits to each of
    HELM/FABLE/BEVEL/SCRIBE and their -FEEDBACK siblings, heading retention against the
    previous state is 1.00 on the large majority of commits and never once below 0.937;
    the lost headings in that tail are single superseded entries. A wipe scores 0.00 and a
    full replace scores near it. The defaults sit at 50% - roughly eight times the largest
    real loss ever observed - because this is a CATASTROPHE stop and nothing else. It is
    deliberately not an additions-only enforcer: losing three entries out of two hundred
    is a review question, and a gate that argued about those would be turned off inside a
    week, at which point it would not be there for the next #493 either.

    Exits non-zero on a violation. Fails OPEN, loudly, on a prefixed line, when there is
    no git or no reachable base - a shallow clone is a host difference, not a wiped
    ledger - because a silent skip is the shape of a gate that reads as coverage while
    seeing nothing (traps 34, 39). CI checks this out at fetch-depth 0 for that reason.

    -BaseRef and -Repo exist so a guard that is silent on a healthy tree can still be
    PROVEN to bite; see scripts/channel-wipe-guard.selftest.ps1, which builds four
    throwaway repos, one per check, and asserts this script fails on each and passes on an
    append-only control.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-wipe-guard.ps1
    pwsh -NoProfile -File scripts/channel-wipe-guard.ps1 -BaseRef origin/main
    pwsh -NoProfile -File scripts/channel-wipe-guard.selftest.ps1   # prove all four fail
#>
[CmdletBinding()]
param(
    # The branch this work is proposed against. CI passes the PR's own base; locally the
    # fallback chain below finds origin/main without anyone typing anything.
    [string] $BaseRef,

    # Verification hook, same as the sibling guards: point the checks at another worktree
    # so they can be shown to fail. Not used by check.ps1 or by CI.
    [string] $Repo,

    # Catastrophe thresholds. See "THE THRESHOLDS ARE MEASURED" above before moving these:
    # the largest real loss in this repo's history is ~6%, and these sit at 50%.
    [int] $MaxShrinkPercent = 50,
    [int] $MinEntryRetentionPercent = 50
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }
$problems = @()

# The ledgers. One row per role, both halves of each channel: `<ROLE>.md` is what the role
# is told, `<ROLE>-FEEDBACK.md` is what it says back, and #493 took both at once. CLAUDE.md
# is here because it is loaded at the start of every session - wiping it is not a lost
# conversation, it is a bot with no operating rules - and its -FEEDBACK sibling for the same
# reason as the rest. Adding a role is one word.
$roles = @('HELM', 'FABLE', 'BEVEL', 'SCRIBE', 'CLAUDE')
$channelFiles = @()
foreach ($role in $roles) { $channelFiles += "$role.md"; $channelFiles += "$role-FEEDBACK.md" }

# Native git output is decoded with [Console]::OutputEncoding, which is the OEM code page
# on this box - the same trap that made whatsnew-guard's first version report 111 of 129
# unchanged entries as edited (trap 54). Every git call in this file goes through here.
function Invoke-GitUtf8([string[]] $Arguments) {
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $Repo @Arguments 2>$null
    }
    finally { [Console]::OutputEncoding = $prev }
}

function Test-GitOk {
    try { Invoke-GitUtf8 @('rev-parse', '--git-dir') | Out-Null; return ($LASTEXITCODE -eq 0) }
    catch { return $false }
}

# The comparison key (see "WHY HEADINGS AND NOT LINES"). Non-ASCII is stripped rather than
# normalised: this file must not care whether the tree is UTF-8, mojibake, or half of each,
# because it is the ledger's SURVIVAL being asserted and not its encoding.
function Get-EntryKeys([string] $text) {
    if (-not $text) { return @() }
    $keys = @()
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -notmatch '^\s{0,3}#{1,6}\s') { continue }
        $k = [regex]::Replace($line, '[^\x20-\x7E]', '')
        $k = [regex]::Replace($k, '\s+', ' ').Trim().ToLowerInvariant()
        if ($k) { $keys += $k }
    }
    @($keys | Sort-Object -Unique)
}

# ---- the base to compare against ---------------------------------------------------

if (-not (Test-GitOk)) {
    Write-Host "channel-wipe-guard: SKIPPED - $Repo is not a git worktree, so there is no base to compare the ledgers against. NOTHING was checked." -ForegroundColor Yellow
    exit 0
}

# Explicit wins; then the PR's own base as GitHub gives it; then the two ordinary spellings
# of the trunk. `origin/main` before `main` because a CI checkout has the remote-tracking
# ref and often no local branch at all.
$candidates = @()
if ($BaseRef) { $candidates += $BaseRef }
elseif ($env:GITHUB_BASE_REF) { $candidates += "origin/$($env:GITHUB_BASE_REF)"; $candidates += $env:GITHUB_BASE_REF }
$candidates += @('origin/main', 'main')

$base = $null
foreach ($c in $candidates) {
    $resolved = Invoke-GitUtf8 @('rev-parse', '--verify', '--quiet', "$c^{commit}")
    if ($LASTEXITCODE -eq 0 -and $resolved) { $base = ($resolved | Select-Object -First 1).Trim(); $baseName = $c; break }
}

if (-not $base) {
    Write-Host "channel-wipe-guard: SKIPPED - none of [$($candidates -join ', ')] resolves in $Repo (shallow or single-branch clone?). The ledgers were NOT checked against a base." -ForegroundColor Yellow
    exit 0
}

# The fork point, not the tip. Comparing a branch against a moved trunk tip reports every
# commit main took while the branch was open as a "loss" on the branch.
$mergeBase = Invoke-GitUtf8 @('merge-base', $base, 'HEAD')
if ($LASTEXITCODE -eq 0 -and $mergeBase) { $compare = ($mergeBase | Select-Object -First 1).Trim() }
else {
    # No shared ancestor: an unrelated-history checkout. Fall back to the base commit
    # itself and SAY SO, rather than skipping - the ledgers still have to survive.
    $compare = $base
    Write-Host "channel-wipe-guard: no merge-base with $baseName; comparing against $baseName directly." -ForegroundColor Yellow
}

# ---- the four checks, per ledger ----------------------------------------------------

$checked = @()
foreach ($rel in $channelFiles) {
    # Present at base? If not, this is a new or not-yet-opened channel and there is
    # nothing to have lost.
    Invoke-GitUtf8 @('cat-file', '-e', "${compare}:$rel") | Out-Null
    if ($LASTEXITCODE -ne 0) { continue }

    $checked += $rel
    $path = Join-Path $Repo $rel

    # ---- 1: DELETED ----------------------------------------------------------------
    if (-not (Test-Path $path)) {
        $problems += "$rel EXISTS at the base and is GONE from this tree. That is exactly what PR #493 and #502 did to HELM.md and HELM-FEEDBACK.md - 12,208 and 12,444 lines, both restored by hand. Restore it with: git checkout $compare -- $rel"
        continue
    }

    $sizeNow = (Get-Item $path).Length

    # ---- 2: EMPTIED ----------------------------------------------------------------
    $textNow = [IO.File]::ReadAllText($path)
    if ($sizeNow -eq 0 -or -not $textNow.Trim()) {
        $problems += "$rel is EMPTY in this tree ($sizeNow bytes) and is not empty at the base. A truncate-to-zero keeps the path in the tree, so it is a wipe that the deleted-file check never sees. Restore it with: git checkout $compare -- $rel"
        continue
    }

    # ---- 3: EXTREME SHRINK ---------------------------------------------------------
    # Blob size straight out of the object database: no decoding, so nothing here can be
    # an artefact of how this shell reads text.
    $sizeBaseRaw = Invoke-GitUtf8 @('cat-file', '-s', "${compare}:$rel")
    $sizeBase = 0
    if ($LASTEXITCODE -eq 0 -and $sizeBaseRaw) { [void][long]::TryParse((($sizeBaseRaw | Select-Object -First 1).Trim()), [ref] $sizeBase) }

    if ($sizeBase -gt 0) {
        $lostPct = [math]::Round((($sizeBase - $sizeNow) * 100.0 / $sizeBase), 1)
        if ($lostPct -gt $MaxShrinkPercent) {
            $problems += "$rel lost $lostPct% of its bytes against the base ($sizeBase -> $sizeNow). The threshold is $MaxShrinkPercent%; the largest legitimate shrink in this repo's channel history is about 6%. A ledger this much smaller has had history removed, not edited."
        }
    }

    # ---- 4: ENTRIES LOST (full replace / truncated append) -------------------------
    $baseTextRaw = Invoke-GitUtf8 @('show', "${compare}:$rel")
    if ($LASTEXITCODE -ne 0) {
        Write-Host "channel-wipe-guard: cannot read $rel at $compare - its entry check was SKIPPED." -ForegroundColor Yellow
        continue
    }
    $baseKeys = Get-EntryKeys (@($baseTextRaw) -join "`n")
    if ($baseKeys.Count -eq 0) { continue }   # no headings at base: nothing to retain

    $nowKeys = Get-EntryKeys $textNow
    $nowSet = @{}
    foreach ($k in $nowKeys) { $nowSet[$k] = $true }

    $lost = @($baseKeys | Where-Object { -not $nowSet.ContainsKey($_) })
    $retained = $baseKeys.Count - $lost.Count
    $retainedPct = [math]::Round(($retained * 100.0 / $baseKeys.Count), 1)

    if ($retainedPct -lt $MinEntryRetentionPercent) {
        # Name a few of the dead. A count alone says a ledger is wrong and not which
        # history left it - and these headings are dated, so they locate the loss.
        $sample = @($lost | Select-Object -First 3 | ForEach-Object { "`"$($_.Substring(0, [Math]::Min(90, $_.Length)))`"" })
        $problems += "$rel keeps only $retainedPct% of the $($baseKeys.Count) entries it has at the base ($($lost.Count) gone). The floor is $MinEntryRetentionPercent%. A channel ledger is APPEND-ONLY (CLAUDE.md trap 60): prepend your entry, never rewrite the file. Note this fires even when the file GREW, which is the full-replace case checks 2 and 3 cannot see. Gone, for example: $($sample -join '; ')."
    }
}

# ---- verdict -------------------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host "channel-wipe-guard: FAILED (against $baseName @ $($compare.Substring(0, 8)))" -ForegroundColor Red
    # Every line carries the prefix: check.ps1 prints only the lines its filter matches,
    # and a headline without its reasons is a gate that says something is wrong and not what.
    foreach ($p in $problems) { Write-Host "channel-wipe-guard:    $p" -ForegroundColor Red }
    exit 1
}

if ($checked.Count -eq 0) {
    # Not a pass. A run that found no ledger at all has proven nothing, and saying "ok"
    # here is how this guard would report green on a tree where every channel was already
    # gone before the base.
    Write-Host "channel-wipe-guard: SKIPPED - none of the $($channelFiles.Count) channel ledgers exists at $baseName. NOTHING was checked." -ForegroundColor Yellow
    exit 0
}

# The summary names what was ACTUALLY read, for the same reason evolved-channel-guard's
# does: a reassuring line over a check that saw nothing is the failure being guarded here.
Write-Host "channel-wipe-guard: ok  ($($checked.Count) channel ledgers intact vs $baseName @ $($compare.Substring(0, 8)); $($checked -join ', '))" -ForegroundColor Green
exit 0
