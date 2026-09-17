<#
.SYNOPSIS
    Drive scripts/channel-size-guard.ps1 into the red once per arm, and prove it stays
    green on the things it must not fire on.

.DESCRIPTION
    A ratchet nobody has watched REFUSE is a green light with no bulb behind it (trap 34).
    This builds a throwaway git repository in TEMP - never this repo, never the real
    channel files - seeds it with fixture ledgers and a fixture baseline table, and then
    does each forbidden thing on purpose.

    Twenty cases, and eleven of them are "must PASS". That ratio is not padding. A size
    ratchet's whole risk is the false positive: it sits in front of files that 40% of this
    repo's commits touch, and the single most likely way for it to be wrong is to refuse an
    append that policy allows. So the passes carry the same weight as the refusals, and each
    one corresponds to a real shape this repo produces - an ordinary append under the limit,
    a rotation, a partly-finished rotation, a 2 MB archive write, a pull request that never
    touched the file at all.

    THE ONE THAT MATTERS MOST IS CASE 7: a pull request that does not touch HELM.md, on a
    tree where HELM.md is already 14x over the limit. On a `pull_request` run the checkout
    is the merge result, so every oversized ledger in the repo is sitting in head whether
    this author wrote a byte of it or not. A guard that read head alone would fail every
    pull request in the repository on its first day and call it enforcement.

    CASE 2 IS THE TRAP-74 CASE. It asserts the exact counts the guard prints, because every
    refusal below would still "pass" if the roster silently stopped matching any file: a
    guard measuring nothing refuses nothing. The mojibake list in channel-wipe-guard.ps1 was
    green for a month while matching nothing, and a printed count is what exposed it.

    CASE 1 EARNED ITS KEEP IMMEDIATELY. Every arm fires on `head > base`, so any asymmetry
    in how the two sides are read is indistinguishable from growth - and on this suite's
    first run the guard refused an untouched tree, because reconstructing a blob from git's
    output drops the trailing newline that a working-tree read keeps. One byte, on every
    file, forever. That bug does not survive a threshold-free comparison and it would not
    have been found by any of the nineteen cases aimed at the arms themselves.

    CASES 16-19 are the baseline table's own integrity - raise a row, add a row, keep a
    discharged row, roster an unknown one. A ratchet whose ceiling the ratcheted party can
    edit is a suggestion, and those four are what make the table's numbers only ever fall.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-size-selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'channel-size-guard.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-channel-size-selftest-" + [guid]::NewGuid().ToString('N'))

$failed = @()
$script:step = 0

$LIMIT = 65536

function New-Utf8File([string] $path, [string] $text) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

# Ledger text of a requested SIZE rather than a requested entry count, because size is this
# guard's whole subject and "about 70 KB" is the fixture every case here is written against.
function Get-LedgerText([int] $bytes, [string] $tag) {
    $sb = [Text.StringBuilder]::new()
    [void]$sb.AppendLine("# $tag")
    $i = 0
    while ($sb.Length -lt $bytes) {
        $i++
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("## 2026-09-0$([int]($i % 9) + 1) ~$i" + ':00 PM CT ' + [char]0x2014 + " $tag entry $i")
        [void]$sb.AppendLine('To: Claude')
        [void]$sb.AppendLine("- **Reinforcing:** entry $i of $tag says a specific thing worth keeping.")
        [void]$sb.AppendLine("- **Corrective:** entry $i names the evidence it rests on ($tag/$i).")
    }
    $sb.ToString()
}

function Get-BaselineText([hashtable] $rows) {
    $sb = [Text.StringBuilder]::new()
    [void]$sb.AppendLine('@{')
    foreach ($k in ($rows.Keys | Sort-Object)) {
        [void]$sb.AppendLine("    '$k' = $($rows[$k])")
    }
    [void]$sb.AppendLine('}')
    $sb.ToString()
}

function Invoke-Git([string[]] $Arguments) {
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $root @Arguments 2>&1 | Out-Null
    }
    finally { [Console]::OutputEncoding = $prev }
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed in the fixture repo" }
}

function Invoke-Guard {
    param([string[]] $GuardArgs)
    $out = & pwsh -NoProfile -File $guard -Repo $root @GuardArgs 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $out.Trim() }
}

function Assert-Result {
    param(
        [string] $Name,
        [bool] $WantFail,
        [string] $Needle,
        [string[]] $GuardArgs = @(),
        # Asserted ABSENT. The card's bar is that the remedy named is rotation and not
        # deletion, and the only way to hold a guard to a word it must never print is to
        # look for it.
        [string] $Forbidden
    )
    $script:step++
    $r = Invoke-Guard $GuardArgs
    $label = "$($script:step). $Name"
    $wanted = if ($WantFail) { 'refuse' } else { 'pass' }
    if ($WantFail -and $r.code -eq 0) {
        $script:failed += "$label - expected the guard to REFUSE, got exit 0: $($r.text)"
        return
    }
    if (-not $WantFail -and $r.code -ne 0) {
        $script:failed += "$label - expected the guard to PASS, got exit $($r.code): $($r.text)"
        return
    }
    if ($Needle -and ($r.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label - $wanted was correct but the reason is missing '$Needle': $($r.text)"
        return
    }
    if ($Forbidden -and ($r.text -match $Forbidden)) {
        $script:failed += "$label - $wanted was correct but the message says '$Forbidden', which it must never say: $($r.text)"
        return
    }
    Write-Host ("  {0,-64} {1}" -f $label, $wanted) -ForegroundColor DarkGray
}

function Reset-Tree {
    Invoke-Git @('reset', '--hard', '--quiet', 'HEAD')
    Invoke-Git @('clean', '-fdq')
}

$BASELINE = 'scripts/channel-size-baseline.psd1'
$OVER = 'HELM.md'            # the grandfathered one: over the limit at base, with a row
$UNDER = 'CLAUDE-FEEDBACK.md' # the compliant one: under the limit, governed by the ceiling
$NAKED = 'DECISIONS.md'       # over the limit at base and deliberately NOT in the table

try {
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    Invoke-Git @('init', '--quiet', '-b', 'fixture')
    Invoke-Git @('config', 'user.email', 'selftest@example.invalid')
    Invoke-Git @('config', 'user.name', 'Channel Size Selftest')
    Invoke-Git @('config', 'commit.gpgsign', 'false')

    # The full roster, so the guard walks the same shape it walks in the real repo: three
    # files over the limit (one grandfathered, one not, one comfortably inside its band) and
    # the rest well under it.
    New-Utf8File (Join-Path $root $OVER) (Get-LedgerText 900000 'HELM.md holds')
    New-Utf8File (Join-Path $root $NAKED) (Get-LedgerText 300000 'DECISIONS.md')
    New-Utf8File (Join-Path $root 'BEVEL-FEEDBACK.md') (Get-LedgerText 120000 'BEVEL-FEEDBACK.md')
    foreach ($f in @('HELM-FEEDBACK.md', 'FABLE-FEEDBACK.md', 'SCRIBE-FEEDBACK.md', $UNDER,
            'FABLE.md', 'BEVEL.md', 'SCRIBE.md', 'SCRIBE-TESTING.md')) {
        New-Utf8File (Join-Path $root $f) (Get-LedgerText 20000 $f)
    }

    $overBytes = [Text.Encoding]::UTF8.GetByteCount(((Get-Content -Raw -LiteralPath (Join-Path $root $OVER)) -replace "`r`n", "`n"))
    $bevelBytes = [Text.Encoding]::UTF8.GetByteCount(((Get-Content -Raw -LiteralPath (Join-Path $root 'BEVEL-FEEDBACK.md')) -replace "`r`n", "`n"))
    New-Utf8File (Join-Path $root $BASELINE) (Get-BaselineText @{
            'HELM.md'           = $overBytes
            'BEVEL-FEEDBACK.md' = $bevelBytes
        })

    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '--quiet', '-m', 'fixture base')
    $base = (& git -C $root rev-parse HEAD).Trim()
    $B = @('-BaseRef', $base)

    $overPath = Join-Path $root $OVER
    $underPath = Join-Path $root $UNDER
    $nakedPath = Join-Path $root $NAKED
    $baselinePath = Join-Path $root $BASELINE
    $baseOverText = [IO.File]::ReadAllText($overPath)
    $baseUnderText = [IO.File]::ReadAllText($underPath)

    Write-Host 'channel-size-guard selftest' -ForegroundColor Cyan

    # -- must PASS ---------------------------------------------------------------------

    # Worth more than it looks. Every arm fires on `head > base`, so ANY systematic
    # asymmetry between how the two sides are read shows up here as growth nobody wrote -
    # and on the first run of this suite it did: `Read-At` reconstructs a blob by joining
    # git's output, which drops the trailing newline a working-tree read keeps, and the
    # guard refused this tree over DECISIONS.md "growing" by one byte.
    Assert-Result 'an untouched tree passes' $false 'channel files measured' $B

    # Trap 74, asserted rather than hoped for. Eleven rostered files exist in the fixture,
    # three are over the limit, two are grandfathered. If the roster ever stops matching,
    # this number goes to zero and EVERY refusal below still "passes", because a guard that
    # measures nothing refuses nothing.
    Assert-Result 'the guard reports the counts it actually measured' $false '11 channel files measured, 3 over the 64 KiB limit, 2 grandfathered' $B

    Reset-Tree
    New-Utf8File $underPath ($baseUnderText + (Get-LedgerText 8000 'an ordinary append'))
    Assert-Result 'appending to a ledger that stays under 64 KiB passes' $false 'channel files measured' $B

    Reset-Tree
    # Rotation: the move the whole policy is asking for. It must be green by construction,
    # not by exemption - the file SHRANK, so no arm even applies.
    New-Utf8File $overPath (Get-LedgerText 40000 'HELM.md holds')
    New-Utf8File (Join-Path $root 'docs/ops/claude-archive/channels/2026-Q3/HELM.md') $baseOverText
    New-Utf8File $baselinePath (Get-BaselineText @{ 'BEVEL-FEEDBACK.md' = $bevelBytes })
    Assert-Result 'rotating an oversized ledger into the archive passes' $false 'channel files measured' $B

    Reset-Tree
    # A 2 MB write under the archive root, and nothing else. The archive is where rotation
    # PUTS the bytes; a size guard that fired on it would forbid its own remedy.
    New-Utf8File (Join-Path $root 'docs/ops/claude-archive/channels/2026-Q3/BEVEL-FEEDBACK.md') (Get-LedgerText 2000000 'two megabytes of archived entries')
    Assert-Result 'a 2 MB write under docs/ops/claude-archive/ passes' $false 'channel files measured' $B

    Reset-Tree
    # Half a rotation. Still 7x over the limit, but smaller than it was, so the guard says
    # so in a note and does not refuse - DRA-144 rotates seven files and will land some of
    # them in stages.
    New-Utf8File $overPath (Get-LedgerText 460000 'HELM.md holds')
    Assert-Result 'a partial rotation that is still over the limit passes with a note' $false 'rotation in progress, not finished' $B

    Reset-Tree
    # THE CASE THAT DECIDES WHETHER THIS GUARD IS USABLE AT ALL. HELM.md is 14x over the
    # limit and this pull request does not touch it; on a real `pull_request` run it is
    # sitting in the merge-result checkout regardless. Failing here would fail every pull
    # request in the repository.
    New-Utf8File (Join-Path $root 'README.md') "# unrelated work`n"
    Assert-Result 'a pull request that never touched an oversized ledger passes' $false 'channel files measured' $B

    Reset-Tree
    # Inside the 10% band: the grandfather row is a transition allowance and it has to
    # actually allow the transition, or the ratchet is a stall with a different name.
    New-Utf8File $overPath ($baseOverText + (Get-LedgerText 20000 'a signed entry inside the band'))
    Assert-Result 'growing a grandfathered ledger inside its 10% band passes' $false 'channel files measured' $B

    Reset-Tree
    # Lowering a row is always allowed - it is the direction the ratchet turns.
    New-Utf8File $baselinePath (Get-BaselineText @{
            'HELM.md'           = ($overBytes - 50000)
            'BEVEL-FEEDBACK.md' = $bevelBytes
        })
    Assert-Result 'LOWERING a grandfather row passes' $false 'channel files measured' $B

    Reset-Tree
    # Deleting a row for a file that is still over the limit makes the guard STRICTER: the
    # file now has no headroom at all. It must not be mistaken for tampering.
    New-Utf8File $baselinePath (Get-BaselineText @{ 'BEVEL-FEEDBACK.md' = $bevelBytes })
    Assert-Result 'DELETING a grandfather row passes (it only ever tightens)' $false 'channel files measured' $B

    # -- must FAIL ---------------------------------------------------------------------

    Reset-Tree
    # THE ACCEPTANCE CRITERION, in one case: a compliant file is grown past 64 KiB.
    New-Utf8File $underPath (Get-LedgerText 70000 'CLAUDE-FEEDBACK.md')
    Assert-Result 'CEILING - growing a compliant ledger past 64 KiB REFUSES' $true 'crosses the 64 KiB channel limit' $B

    Reset-Tree
    # The remedy, held to the word DRA-73 used. `Forbidden` is the real assertion here: a
    # message that says "delete" would teach exactly the behaviour trap 60 is about, and
    # channel-wipe-guard.ps1 would refuse the deletion it invited.
    New-Utf8File $underPath (Get-LedgerText 70000 'CLAUDE-FEEDBACK.md')
    Assert-Result '  ...and it names ROTATION as the remedy, never deletion' $true 'ROTATE it' $B '(?i)(?<!do not )\bdelete (entries|old entries|the file|it)\b'

    Reset-Tree
    # A channel file BORN over the limit. Absent at base is base 0, so the ceiling arm is
    # what catches it - there is no "it was always like this" for a file that was not.
    New-Utf8File (Join-Path $root 'ORACLE-FEEDBACK.md') (Get-LedgerText 200000 'a new channel born oversized')
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '--quiet', '-m', 'fixture: a new oversized channel file')
    Assert-Result 'ROSTER - a new unrostered channel file REFUSES' $true 'NOT in this guard' $B
    Invoke-Git @('reset', '--hard', '--quiet', $base)

    Reset-Tree
    # Over the limit at base, no row, grown by one entry. No headroom, and the message has
    # to say that rather than pointing at a band it does not have.
    New-Utf8File $nakedPath ((Get-LedgerText 300000 'DECISIONS.md') + (Get-LedgerText 5000 'one more decision'))
    Assert-Result 'RATCHET - growing an over-limit ledger with no row REFUSES' $true 'no headroom at all' $B

    Reset-Tree
    # Past the band. 900 KB + 20% is well over 900 KB + 10%.
    New-Utf8File $overPath ($baseOverText + (Get-LedgerText 180000 'far past the band'))
    Assert-Result 'RATCHET - growing a grandfathered ledger past its 10% band REFUSES' $true 'has spent its grandfather band' $B

    Reset-Tree
    # Raising the number instead of rotating the file: the self-granted exemption, which is
    # the one move that would turn this whole guard into a formality.
    New-Utf8File $baselinePath (Get-BaselineText @{
            'HELM.md'           = ($overBytes * 4)
            'BEVEL-FEEDBACK.md' = $bevelBytes
        })
    New-Utf8File $overPath ($baseOverText + (Get-LedgerText 180000 'far past the band'))
    Assert-Result 'TABLE - RAISING a grandfather row REFUSES' $true 'A ratchet''s ceiling only falls' $B

    Reset-Tree
    # Writing yourself a row for a file the ceiling was governing.
    New-Utf8File $underPath (Get-LedgerText 70000 'CLAUDE-FEEDBACK.md')
    New-Utf8File $baselinePath (Get-BaselineText @{
            'HELM.md'           = $overBytes
            'BEVEL-FEEDBACK.md' = $bevelBytes
            'CLAUDE-FEEDBACK.md' = 70000
        })
    Assert-Result 'TABLE - ADDING a grandfather row REFUSES' $true 'ADDS a grandfather row' $B

    Reset-Tree
    # The rotation that forgot its row. Same rule the hotspot table states: the lift and the
    # re-baseline land together, or the next pull request inherits a ceiling nobody argued
    # for.
    New-Utf8File $overPath (Get-LedgerText 40000 'HELM.md holds')
    New-Utf8File (Join-Path $root 'docs/ops/claude-archive/channels/2026-Q3/HELM.md') $baseOverText
    Assert-Result 'TABLE - rotating without DELETING the discharged row REFUSES' $true 'must be DELETED in this same pull request' $B

    Reset-Tree
    New-Utf8File $baselinePath (Get-BaselineText @{
            'HELM.md'           = $overBytes
            'BEVEL-FEEDBACK.md' = $bevelBytes
            'HANDOFF.md'        = 248286
        })
    Assert-Result 'TABLE - a row for an unrostered path REFUSES' $true 'not in this guard''s roster' $B

    # -- and the honest skip -----------------------------------------------------------

    Reset-Tree
    # No -BaseRef, no origin/main, no main. Every arm here is base-relative, so there is
    # nothing to compare against - and it must say so out loud rather than exit 0 looking
    # like coverage.
    Assert-Result 'no resolvable base SKIPS loudly rather than passing quietly' $false 'were NOT verified' @()
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "channel-size-selftest: FAILED" -ForegroundColor Red
    foreach ($f in $failed) { Write-Host "channel-size-selftest:    $f" -ForegroundColor Red }
    exit 1
}
Write-Host "channel-size-selftest: ok  ($script:step cases; every arm driven into the red at least once)" -ForegroundColor Green
exit 0
