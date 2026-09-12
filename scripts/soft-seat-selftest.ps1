<#
.SYNOPSIS
    Experiment A' on EQBuddy: prove default refuse, the DRA-<n> claim key,
    explicit modes, stale recovery.

.DESCRIPTION
    Lab check, not a Corps-standard suite. Uses a throwaway -StoreDir. Never
    touches the machine's live claims.json. Called from scripts/check.ps1 and CI
    so a broken refuse cannot read as coverage.

    Every refuse here is paired with the legitimate spelling right beside it
    (trap 34: a guard that only forbids cannot see a missing thing). The
    DRA-50 block additionally asserts that a REFUSED claim left no row in the
    store — "it printed an error" and "it did not take the seat" are two
    different claims.

.EXAMPLE
    pwsh -NoProfile -File scripts/soft-seat-selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$claim = Join-Path $PSScriptRoot 'claim-seat.ps1'
$release = Join-Path $PSScriptRoot 'release-seat.ps1'
$store = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-soft-seat-selftest-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $store | Out-Null

$failed = @()
$script:step = 0

function Invoke-Seat {
    param([string] $Script, [string[]] $SeatArgs)
    $out = & pwsh -NoProfile -File $Script @SeatArgs -StoreDir $store 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $out.Trim() }
}

function Expect-Ok {
    param([string] $Name, $Result, [string] $Needle)
    $script:step++
    $label = "$($script:step). $Name"
    if ($Result.code -ne 0) {
        $script:failed += "$label — expected exit 0, got $($Result.code): $($Result.text)"
        return
    }
    if ($Needle -and ($Result.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label — expected text '$Needle', got: $($Result.text)"
    }
}

function Expect-Fail {
    param([string] $Name, $Result, [string] $Needle)
    $script:step++
    $label = "$($script:step). $Name"
    if ($Result.code -eq 0) {
        $script:failed += "$label — expected refuse, got success: $($Result.text)"
        return
    }
    if ($Needle -and ($Result.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label — expected text '$Needle', got: $($Result.text)"
    }
}

try {
    $a1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-a', '-Branch', 'claude/a')
    Expect-Ok 'first default claim succeeds' $a1 'claimed DRA-428'

    $b1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-b')
    Expect-Fail 'second default claim on same card is blocked' $b1 'already claimed'
    if ($b1.text -notmatch 'seat-a') {
        $script:failed += "$($script:step). second default — refusal must name the holder seat-a: $($b1.text)"
    }

    $b2 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-b', '-Mode', 'challenger')
    Expect-Ok 'challenger is allowed beside an active claim' $b2 'challenger'

    $c1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-c', '-Mode', 'disjoint')
    Expect-Ok 'disjoint is allowed beside an active claim' $c1 'disjoint'

    $d1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-d', '-Mode', 'replacement')
    Expect-Ok 'replacement is allowed and takes the exclusive slot' $d1 'replacement'

    $a2 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-e')
    Expect-Fail 'default still refused after replacement (new exclusive holder)' $a2 'already claimed'

    $e1 = Invoke-Seat $claim @('-WorkItem', 'DRA-429', '-SeatId', 'seat-e')
    Expect-Ok 'different card is a separate claim' $e1 'claimed DRA-429'

    $aAgain = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-d', '-Mode', 'replacement')
    Expect-Ok 'same-seat re-claim is idempotent' $aAgain 'refreshed'

    # --- DRA-50: the claim key is a Paperclip card id, and only that ---------
    # Prove-fail, not green-only (trap 34): each of these MUST refuse, and the
    # legitimate spelling right beside it MUST still work.

    $ghNum = Invoke-Seat $claim @('-WorkItem', '445', '-SeatId', 'seat-gh')
    Expect-Fail 'a bare GitHub issue number is refused as a work item' $ghNum 'GitHub issue number'
    if ($ghNum.text -notmatch 'DRA-') {
        $script:failed += "$($script:step). bare number — the error must name the DRA-<n> form it wants: $($ghNum.text)"
    }

    $ghHash = Invoke-Seat $claim @('-WorkItem', '#445', '-SeatId', 'seat-gh')
    Expect-Fail 'a #-prefixed issue number is refused too' $ghHash 'GitHub issue number'

    $freeText = Invoke-Seat $claim @('-WorkItem', 'options-ia-impl', '-SeatId', 'seat-free')
    Expect-Fail 'a free-text work id is refused' $freeText 'not a Paperclip card id'

    $nearMiss = Invoke-Seat $claim @('-WorkItem', 'DRA-28b', '-SeatId', 'seat-near')
    Expect-Fail 'DRA-<n> means digits only' $nearMiss 'not a Paperclip card id'

    # The prove-pass half: the SAME scope, spelled as its card, claims fine.
    $card = Invoke-Seat $claim @('-WorkItem', 'DRA-28', '-SeatId', 'seat-card')
    Expect-Ok 'the card id for that scope claims successfully' $card 'claimed DRA-28'

    # ...and the refused number did not quietly take a seat on its way out.
    $listAfter = Invoke-Seat $claim @('-List')
    Expect-Ok 'refused claims left no row behind' $listAfter 'DRA-28'
    if ($listAfter.text -match '(?m)^\s+#445\b') {
        $script:failed += "$($script:step). refused bare number still wrote a claim row: $($listAfter.text)"
    }

    # Case is a spelling, not a second key.
    $lowerCase = Invoke-Seat $claim @('-WorkItem', 'dra-28', '-SeatId', 'seat-lower')
    Expect-Fail 'dra-28 is the same key as DRA-28, so a second default is refused' $lowerCase 'already claimed'
    if ($lowerCase.text -notmatch 'seat-card') {
        $script:failed += "$($script:step). case-folded key — refusal must name seat-card: $($lowerCase.text)"
    }

    # One key, not two: -PaperclipIssue may only restate -WorkItem. -Check so
    # no live Paperclip card is written by a selftest.
    $dualKey = Invoke-Seat $claim @('-WorkItem', 'DRA-30', '-SeatId', 'seat-dual', '-PaperclipIssue', 'DRA-31', '-Check')
    Expect-Fail 'a PaperclipIssue that is not the WorkItem is refused' $dualKey 'is not -WorkItem'
    $sameKey = Invoke-Seat $claim @('-WorkItem', 'DRA-30', '-SeatId', 'seat-dual', '-PaperclipIssue', 'DRA-30', '-Check')
    Expect-Ok 'a PaperclipIssue equal to the WorkItem is allowed' $sameKey 'claimable'
    # ------------------------------------------------------------------------

    $relOwn = Invoke-Seat $release @('-WorkItem', 'DRA-428', '-SeatId', 'seat-d')
    Expect-Ok 'own-seat release abandons the claim' $relOwn 'abandoned'

    $f1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-f')
    Expect-Ok 'default claim succeeds after exclusive is released' $f1 'claimed DRA-428'

    $relOther = Invoke-Seat $release @('-WorkItem', 'DRA-428', '-SeatId', 'not-the-holder')
    Expect-Fail 'cannot release another seat without -ForceStale' $relOther 'REFUSED'

    $deadPid = Invoke-Seat $claim @('-WorkItem', 'DRA-430', '-SeatId', 'seat-dead', '-ExecutorPid', '2147483646')
    Expect-Ok 'claim with a dead pid records' $deadPid 'claimed DRA-430'
    $relDeadNo = Invoke-Seat $release @('-WorkItem', 'DRA-430')
    Expect-Fail 'dead-pid recovery still needs -ForceStale' $relDeadNo 'REFUSED'
    $relDead = Invoke-Seat $release @('-WorkItem', 'DRA-430', '-ForceStale')
    Expect-Ok 'dead pid + -ForceStale recovers' $relDead 'abandoned'

    $age = Invoke-Seat $claim @('-WorkItem', 'DRA-431', '-SeatId', 'seat-old')
    Expect-Ok 'age-target claim records' $age 'claimed DRA-431'
    $jsonPath = Join-Path $store 'claims.json'
    if (-not (Test-Path $jsonPath)) {
        $script:failed += 'age plant — claims.json was never written'
        throw 'selftest aborted: store file missing'
    }
    $raw = [IO.File]::ReadAllText($jsonPath)
    $planted = $raw | ConvertFrom-Json
    foreach ($c in @($planted.claims)) {
        if (([string] $c.work_item) -eq 'DRA-431' -and ([string] $c.status) -eq 'active') {
            $c.started_at = [datetime]::UtcNow.AddHours(-9).ToString('yyyy-MM-ddTHH:mm:ssZ')
            $c.pid = $null
        }
    }
    [IO.File]::WriteAllText($jsonPath, ($planted | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    $relAgeNo = Invoke-Seat $release @('-WorkItem', 'DRA-431')
    Expect-Fail 'aged claim still needs -ForceStale' $relAgeNo 'REFUSED'
    $relAge = Invoke-Seat $release @('-WorkItem', 'DRA-431', '-ForceStale')
    Expect-Ok 'age threshold + -ForceStale recovers' $relAge 'abandoned'

    $fresh = Invoke-Seat $claim @('-WorkItem', 'DRA-432', '-SeatId', 'seat-fresh', '-ExecutorPid', "$PID")
    Expect-Ok 'fresh live-pid claim records' $fresh 'claimed DRA-432'
    $relFresh = Invoke-Seat $release @('-WorkItem', 'DRA-432', '-ForceStale')
    Expect-Fail 'ForceStale refuses a fresh live-pid claim that is not ours' $relFresh 'not stale'

    # A row claimed before DRA-50 under a bare number must still be releasable,
    # or this change strands every live claim on the machine.
    $legacy = @{
        work_item  = '445'
        seat_id    = 'seat-legacy'
        branch     = $null
        worktree   = $null
        started_at = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        status     = 'active'
        pid        = $null
    }
    $planted2 = [IO.File]::ReadAllText($jsonPath) | ConvertFrom-Json
    $planted2.claims = @($planted2.claims) + [pscustomobject] $legacy
    [IO.File]::WriteAllText($jsonPath, ($planted2 | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    $relLegacy = Invoke-Seat $release @('-WorkItem', '445', '-SeatId', 'seat-legacy')
    Expect-Ok 'a pre-DRA-50 numeric claim can still be released' $relLegacy 'abandoned'
}
finally {
    Remove-Item -LiteralPath $store -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failed.Count -gt 0) {
    Write-Host 'soft-seat-selftest FAILED:'
    $failed | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "soft-seat-selftest ok ($($script:step) checks)"
exit 0
