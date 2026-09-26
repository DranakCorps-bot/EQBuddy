<#
.SYNOPSIS
    Experiment A' on EQBuddy: prove the default refuse against every holding
    status, the DRA-<n> claim key, explicit modes, stale recovery.

.DESCRIPTION
    Lab check, not a Corps-standard suite. Uses a throwaway -StoreDir. Never
    touches the machine's live claims.json. Called from scripts/check.ps1 and CI
    so a broken refuse cannot read as coverage.

    Every refuse here is paired with the legitimate spelling right beside it
    (trap 34: a guard that only forbids cannot see a missing thing). The
    DRA-50 block additionally asserts that a REFUSED claim left no row in the
    store — "it printed an error" and "it did not take the seat" are two
    different claims.

    The DRA-76 block runs one card per HOLDING status, so the refusal is proven
    to fire for each element of SoftSeatHolding rather than for the one element
    somebody happened to test (trap 78), and asserts that 'abandoned' still
    releases the card — otherwise a mutex that refuses everything forever would
    pass every row above it.

.EXAMPLE
    pwsh -NoProfile -File scripts/soft-seat-selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$claim = Join-Path $PSScriptRoot 'claim-seat.ps1'
$release = Join-Path $PSScriptRoot 'release-seat.ps1'
# Dot-sourced for ONE assertion — the holding list itself. Everything else here
# goes through the scripts as a player would call them.
. (Join-Path $PSScriptRoot 'soft-seat-store.ps1')
$store = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-soft-seat-selftest-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $store | Out-Null

# DRA-102: the store registry is MACHINE-level, so a selftest that used the real
# one would register throwaway temp clones in front of every future refusal on
# this box — the live-state rule the repo already applies to the profile. Point
# it at a temp file for the whole run and put it back afterwards. The production
# location is asserted on its own, below, precisely because every other row here
# overrides it (trap 76: a fixture that removes the mechanism verifies nothing).
$script:savedRegistryEnv = [Environment]::GetEnvironmentVariable('EQBUDDY_SOFT_SEAT_REGISTRY')
$registryLab = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-soft-seat-registry-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $registryLab | Out-Null
$env:EQBUDDY_SOFT_SEAT_REGISTRY = Join-Path $registryLab 'stores.json'

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
    Expect-Fail 'second default claim on same card is blocked' $b1 'already held by'
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
    Expect-Fail 'default still refused after replacement (new exclusive holder)' $a2 'already held by'
    # Three seats are live on this card now (challenger, disjoint, replacement);
    # the replacement abandoned seat-a's active row. The refusal COUNTS them, so
    # a refusal that saw only the exclusive one would read '1 live seat'.
    if ($a2.text -notmatch '3 live seat') {
        $script:failed += "$($script:step). refusal must count every live holder (expected 3): $($a2.text)"
    }
    if ($a2.text -match 'seat-a') {
        $script:failed += "$($script:step). refusal named seat-a, whose claim the replacement abandoned: $($a2.text)"
    }

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
    Expect-Fail 'dra-28 is the same key as DRA-28, so a second default is refused' $lowerCase 'already held by'
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

    # --- DRA-76: ANY live seat holds the card against a DEFAULT claim --------
    # One card per holding status, so the refusal is proven to fire for each
    # element of SoftSeatHolding — not for the one element somebody happened to
    # test (trap 78). Before this change, 'challenger' and 'disjoint' were
    # invisible to a default claim and the two rows below went green as
    # successes: two executors on one card, neither refused (PRs #566/#568).
    $holdingModes = @('active', 'challenger', 'disjoint', 'replacement')
    $holdingCard = 760
    foreach ($mode in $holdingModes) {
        $holdingCard++
        $card = "DRA-$holdingCard"
        $holderSeat = "holder-$mode"

        $held = Invoke-Seat $claim @('-WorkItem', $card, '-SeatId', $holderSeat, '-Mode', $mode)
        Expect-Ok "a $mode seat takes $card" $held "as $mode"

        $second = Invoke-Seat $claim @('-WorkItem', $card, '-SeatId', 'second-default')
        Expect-Fail "a default claim is refused by a live $mode seat" $second 'already held by'
        if ($second.text -notmatch [regex]::Escape($holderSeat)) {
            $script:failed += "$($script:step). $mode refusal must name the holder $holderSeat`: $($second.text)"
        }
        if ($second.text -notmatch 'release-seat\.ps1') {
            $script:failed += "$($script:step). $mode refusal must name the recovery: $($second.text)"
        }

        # The prove-pass half of the same rule (trap 34): the SAME seat is
        # admitted the moment it says which kind of second seat it is.
        # -Check so the override writes no row and the next card starts clean.
        $override = Invoke-Seat $claim @('-WorkItem', $card, '-SeatId', 'second-default', '-Mode', 'challenger', '-Check')
        Expect-Ok "an explicit challenger is still admitted beside a $mode seat" $override 'claimable'
    }

    # A refusal that printed and then took the seat anyway is not a refusal.
    $listHold = Invoke-Seat $claim @('-List')
    Expect-Ok 'the holders are on the board' $listHold 'holder-challenger'
    if ($listHold.text -match 'second-default') {
        $script:failed += "$($script:step). a refused default claim still wrote a row: $($listHold.text)"
    }

    # 'abandoned' is the ONE status that does not hold. Without this row, a
    # mutex that refused every claim forever would pass everything above.
    $relHold = Invoke-Seat $release @('-WorkItem', 'DRA-762', '-SeatId', 'holder-challenger')
    Expect-Ok 'the challenger releases its hold' $relHold 'abandoned'
    $afterRelease = Invoke-Seat $claim @('-WorkItem', 'DRA-762', '-SeatId', 'second-default')
    Expect-Ok 'a default claim succeeds once the last holder is abandoned' $afterRelease 'claimed DRA-762'

    # And the LIST itself, not only its behaviour: an empty or drifted detector
    # list matches nothing and reports clean (trap 78 — the mojibake markers).
    $script:step++
    # Where-Object, not a bare @(): @($null) is a one-element array holding
    # $null, which reads as "the list is fine, it has one entry in it".
    $holding = @($script:SoftSeatHolding | Where-Object { $_ })
    $expectedHolding = @('active', 'challenger', 'disjoint', 'replacement')
    if ($holding.Count -eq 0) {
        $script:failed += "$($script:step). SoftSeatHolding is EMPTY — the mutex would hold nothing and refuse nobody."
    }
    elseif (@(Compare-Object $holding $expectedHolding).Count -ne 0) {
        $script:failed += "$($script:step). SoftSeatHolding is [$($holding -join ', ')], expected [$($expectedHolding -join ', ')] — every mode but 'abandoned'. A mode missing from it refuses nobody."
    }
    # ------------------------------------------------------------------------

    # --- DRA-110: the GRANTED MODE is its own field, and lifecycle cannot ----
    # ---           overwrite it                                          ----
    #
    # status was doing two jobs: it carried the mode at grant AND the liveness
    # afterwards, so every transition that ends a seat erased the grant. 150 of
    # 166 rows on this machine no longer state the mode they were granted under,
    # which is why the disputed DRA-106 grant (40 s apart) can be neither ruled
    # in nor out. The demonstration consumed the evidence: one authorized
    # -ForceStale turned the only surviving 'active' into 'abandoned'.
    #
    # Helm's bar, verbatim: the field is written for each of the four admitted
    # modes; a release / -ForceStale / takeover does NOT clear it; plus a
    # reachable negative. Every row below goes through the scripts.
    $grantJson = Join-Path $store 'claims.json'
    function Get-GrantRow {
        param([string] $Card, [string] $Seat)
        if (-not (Test-Path $grantJson)) { return $null }
        $parsed = [IO.File]::ReadAllText($grantJson) | ConvertFrom-Json
        foreach ($c in @($parsed.claims)) {
            if (([string] $c.work_item) -eq $Card -and ([string] $c.seat_id) -ieq $Seat) { return $c }
        }
        return $null
    }
    # Reads the row's granted_mode the way an auditor would: the PROPERTY, not a
    # substring of the file. A row that never grew the field and a row whose
    # field is empty must be indistinguishable here, and both must read as
    # "absent" rather than as a mode.
    function Get-GrantMode {
        param($Row)
        if (-not $Row) { return $null }
        $p = $Row.PSObject.Properties['granted_mode']
        if (-not $p) { return $null }
        if ($p.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($p.Value)) { return $null }
        return ([string] $p.Value)
    }

    # (1) WRITTEN FOR EACH OF THE FOUR ADMITTED MODES. One card per mode, so a
    # field written only on the path somebody happened to test cannot pass
    # (trap 78 — the same reason the DRA-76 block above runs four cards).
    $grantCard = 1100
    foreach ($mode in @('active', 'challenger', 'disjoint', 'replacement')) {
        $grantCard++
        $card = "DRA-$grantCard"
        $seat = "grant-$mode"
        # -Mode replacement on an unheld card is still an explicitly chosen
        # mode; the point here is the field, not the takeover.
        $made = Invoke-Seat $claim @('-WorkItem', $card, '-SeatId', $seat, '-Mode', $mode)
        Expect-Ok "a $mode claim records its granted mode" $made "granted as $mode"

        $script:step++
        $got = Get-GrantMode (Get-GrantRow $card $seat)
        if ($got -ne $mode) {
            $script:failed += "$($script:step). $card/$seat must persist granted_mode '$mode' on the ROW, got '$got'. A mode that lives only in the printed sentence is not evidence."
        }

        # ...and a human reading the board sees it (Helm: surface it in -List).
        $listGrant = Invoke-Seat $claim @('-List')
        Expect-Ok "-List states the granted mode of the $mode seat" $listGrant "granted as $mode"
    }

    # (2) A RELEASE DOES NOT CLEAR IT. This is the exact transition that erased
    # the DRA-106 evidence.
    $relGrant = Invoke-Seat $release @('-WorkItem', 'DRA-1102', '-SeatId', 'grant-challenger')
    Expect-Ok 'the challenger row releases' $relGrant 'abandoned'
    $script:step++
    $afterRel = Get-GrantRow 'DRA-1102' 'grant-challenger'
    if (([string] $afterRel.status) -ne 'abandoned') {
        $script:failed += "$($script:step). release must move status to abandoned, got '$($afterRel.status)'."
    }
    if ((Get-GrantMode $afterRel) -ne 'challenger') {
        $script:failed += "$($script:step). a released row must STILL say it was granted as challenger, got '$(Get-GrantMode $afterRel)'. status moving is the point; the grant moving is the bug."
    }

    # (3) -ForceStale DOES NOT CLEAR IT. Helm authorized exactly this command on
    # DRA-106 and it is what consumed the evidence, so it gets its own row
    # rather than riding on the ordinary release above.
    $staleGrant = Invoke-Seat $claim @('-WorkItem', 'DRA-1110', '-SeatId', 'grant-stale', '-Mode', 'disjoint', '-ExecutorPid', '2147483646')
    Expect-Ok 'a disjoint claim with a dead pid records' $staleGrant 'granted as disjoint'
    $relStale = Invoke-Seat $release @('-WorkItem', 'DRA-1110', '-ForceStale')
    Expect-Ok '-ForceStale recovers the dead holder' $relStale 'abandoned'
    $script:step++
    $afterStale = Get-GrantRow 'DRA-1110' 'grant-stale'
    if (([string] $afterStale.status) -ne 'abandoned' -or (Get-GrantMode $afterStale) -ne 'disjoint') {
        $script:failed += "$($script:step). after -ForceStale the row must read status 'abandoned' AND granted_mode 'disjoint', got status '$($afterStale.status)' granted_mode '$(Get-GrantMode $afterStale)'."
    }

    # (4) A REPLACEMENT TAKEOVER DOES NOT CLEAR THE ROW IT TOOK OVER. The
    # takeover writes 'abandoned' over somebody ELSE's status — the one erasure
    # the seat being erased never ran itself.
    $takenOver = Invoke-Seat $claim @('-WorkItem', 'DRA-1120', '-SeatId', 'grant-victim')
    Expect-Ok 'the victim seat claims by default' $takenOver 'granted as active'
    $takeover = Invoke-Seat $claim @('-WorkItem', 'DRA-1120', '-SeatId', 'grant-taker', '-Mode', 'replacement')
    Expect-Ok 'a replacement takes the exclusive slot' $takeover 'granted as replacement'
    $script:step++
    $victim = Get-GrantRow 'DRA-1120' 'grant-victim'
    if (([string] $victim.status) -ne 'abandoned') {
        $script:failed += "$($script:step). the takeover must abandon the victim's status, got '$($victim.status)'."
    }
    if ((Get-GrantMode $victim) -ne 'active') {
        $script:failed += "$($script:step). a seat somebody ELSE replaced must still say it was granted as active, got '$(Get-GrantMode $victim)'."
    }

    # (5) A SAME-SEAT RE-CLAIM MOVES LIVENESS, NOT THE GRANT. This is the third
    # writer Helm's ACK enumerated, and the subtle one: the row entered as
    # 'active', so escalating it to 'challenger' must not rewrite how it got in.
    # Both facts stay readable — 'challenger since <t>, granted as active'.
    $enter = Invoke-Seat $claim @('-WorkItem', 'DRA-1130', '-SeatId', 'grant-refresh')
    Expect-Ok 'a row enters the store as active' $enter 'granted as active'
    $escalate = Invoke-Seat $claim @('-WorkItem', 'DRA-1130', '-SeatId', 'grant-refresh', '-Mode', 'challenger')
    Expect-Ok 'the same seat re-claims as a challenger' $escalate 'refreshed DRA-1130'
    $script:step++
    $refreshed = Get-GrantRow 'DRA-1130' 'grant-refresh'
    if (([string] $refreshed.status) -ne 'challenger') {
        $script:failed += "$($script:step). a re-claim must move status to challenger, got '$($refreshed.status)'."
    }
    if ((Get-GrantMode $refreshed) -ne 'active') {
        $script:failed += "$($script:step). granted_mode is written ONCE at claim; a re-claim must leave it at 'active', got '$(Get-GrantMode $refreshed)'."
    }
    if ($escalate.text -notmatch [regex]::Escape('granted as active')) {
        $script:failed += "$($script:step). the refreshed row must still PRINT its original grant: $($escalate.text)"
    }

    # (6) THE REACHABLE NEGATIVE, and the forward-only rule in one row.
    # Every row above asserts a mode is present, and every one of them would
    # stay green if the absent case silently rendered as 'active' — which is
    # precisely the reading the card forbids. So plant a row in the pre-DRA-110
    # SHAPE (seven fields, no granted_mode), which is what all 166 rows on this
    # machine look like, and prove it reads as UNRECORDED and never as a mode.
    $legacyGrant = @{
        work_item  = 'DRA-1140'
        seat_id    = 'grant-legacy'
        branch     = $null
        worktree   = $null
        started_at = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        status     = 'active'
        pid        = $null
    }
    $plantedGrant = [IO.File]::ReadAllText($grantJson) | ConvertFrom-Json
    $plantedGrant.claims = @($plantedGrant.claims | ForEach-Object { $_ }) + [pscustomobject] $legacyGrant
    [IO.File]::WriteAllText($grantJson, ($plantedGrant | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

    $script:step++
    if ($null -ne (Get-GrantMode (Get-GrantRow 'DRA-1140' 'grant-legacy'))) {
        $script:failed += "$($script:step). the planted pre-DRA-110 row must carry NO granted_mode, or this negative is testing nothing."
    }

    $listLegacy = Invoke-Seat $claim @('-List')
    Expect-Ok 'a pre-DRA-110 row reads as UNRECORDED, not as a mode' $listLegacy 'granted mode not recorded'
    $script:step++
    # The row is status 'active'. If the readout ever fell back to status, this
    # line would read 'granted as active' — the one wrong answer, because it
    # asserts a default claim about a row that never recorded one.
    $legacyLine = @($listLegacy.text -split "`n" | Where-Object { $_ -match 'grant-legacy' })
    if ($legacyLine.Count -eq 0) {
        $script:failed += "$($script:step). -List did not print the planted legacy row at all: $($listLegacy.text)"
    }
    elseif (($legacyLine -join ' ') -match 'granted as') {
        $script:failed += "$($script:step). a row with no granted_mode was rendered AS a mode — absent must never read as 'default claim': $($legacyLine -join ' ')"
    }
    # ...and the refusal text, the other surface Helm named, says the same thing
    # about the same row. A refusal that omitted it is where a reader infers.
    $legacyRefusal = Invoke-Seat $claim @('-WorkItem', 'DRA-1140', '-SeatId', 'grant-second')
    Expect-Fail 'a refusal over a pre-DRA-110 holder says the grant is unrecorded' $legacyRefusal 'granted mode not recorded'

    # And the sentence itself, not only its behaviour: an empty or mode-shaped
    # "unrecorded" string would make rows (6) match anything or nothing
    # (trap 78 — the detector aimed at nothing).
    $script:step++
    $unrecorded = [string] $script:SoftSeatGrantedModeUnrecorded
    if ([string]::IsNullOrWhiteSpace($unrecorded)) {
        $script:failed += "$($script:step). SoftSeatGrantedModeUnrecorded is EMPTY — every absent grant would print nothing and read as a default claim."
    }
    elseif ($unrecorded -match 'granted as') {
        $script:failed += "$($script:step). the unrecorded sentence '$unrecorded' collides with the recorded one; the two cases must not be mistakable for each other."
    }
    # ------------------------------------------------------------------------

    $relOwn = Invoke-Seat $release @('-WorkItem', 'DRA-428', '-SeatId', 'seat-d')
    Expect-Ok 'own-seat release abandons the claim' $relOwn 'abandoned'

    # DRA-76: releasing the EXCLUSIVE holder is no longer enough. seat-b
    # (challenger) and seat-c (disjoint) are still on this card, and each of
    # them is an executor doing work on it. This exact call SUCCEEDED before.
    $fBlocked = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-f')
    Expect-Fail 'default still refused while a challenger and a disjoint seat hold the card' $fBlocked 'already held by'
    foreach ($needle in @('seat-b', 'seat-c')) {
        if ($fBlocked.text -notmatch $needle) {
            $script:failed += "$($script:step). refusal must name every live holder ($needle): $($fBlocked.text)"
        }
    }
    if ($fBlocked.text -match 'seat-d') {
        $script:failed += "$($script:step). refusal named seat-d, which was just released: $($fBlocked.text)"
    }

    $relB = Invoke-Seat $release @('-WorkItem', 'DRA-428', '-SeatId', 'seat-b')
    Expect-Ok 'the challenger releases' $relB 'abandoned'
    $relC = Invoke-Seat $release @('-WorkItem', 'DRA-428', '-SeatId', 'seat-c')
    Expect-Ok 'the disjoint seat releases' $relC 'abandoned'

    $f1 = Invoke-Seat $claim @('-WorkItem', 'DRA-428', '-SeatId', 'seat-f')
    Expect-Ok 'default claim succeeds once no live seat holds the card' $f1 'claimed DRA-428'

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

    # --- DRA-90: the mutex is only as good as the store BOTH seats read ------
    # Every check above hands the scripts a -StoreDir, so none of them exercise
    # store DISCOVERY — the one thing that decides whether two seats collide at
    # all. That hole is why DRA-90 could be reported as "the store is
    # per-working-copy" (it is not: git rev-parse --git-common-dir sends every
    # linked worktree to the main tree's copy) and why nobody could check.
    # A refusal proven only against a store dir somebody passed in is a mutex
    # proven against itself.
    #
    # This block builds a REAL repo with a REAL linked worktree and calls the
    # scripts with NO -StoreDir, the way a seat actually runs.
    $lab = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-soft-seat-worktree-" + [guid]::NewGuid().ToString('N'))
    try {
        $labMain = Join-Path $lab 'main'
        $labWt = Join-Path $lab 'wt'
        New-Item -ItemType Directory -Force -Path (Join-Path $labMain 'scripts') | Out-Null
        foreach ($f in @('claim-seat.ps1', 'release-seat.ps1', 'soft-seat-store.ps1')) {
            Copy-Item (Join-Path $PSScriptRoot $f) (Join-Path $labMain "scripts/$f") -Force
        }
        # .gitignore mirrors the real repo: the live store is never committed,
        # which is precisely why it is ABSENT in a fresh worktree and why its
        # absence must not be read as "not shared".
        [IO.File]::WriteAllText((Join-Path $labMain '.gitignore'), ".claude/soft-seats/claims.json`n", [Text.UTF8Encoding]::new($false))
        & git -C $labMain init -q . 2>&1 | Out-Null
        & git -C $labMain add -A 2>&1 | Out-Null
        & git -C $labMain -c user.email='seat@lab' -c user.name='seat lab' commit -qm 'seat lab' 2>&1 | Out-Null
        & git -C $labMain worktree add -q $labWt -b seat-lab-wt 2>&1 | Out-Null

        $script:step++
        if (-not (Test-Path (Join-Path $labWt 'scripts/claim-seat.ps1'))) {
            $script:failed += "$($script:step). worktree lab — git worktree add did not produce a working copy at $labWt"
            throw 'selftest aborted: worktree lab not built'
        }

        $labClaim = Join-Path $labMain 'scripts/claim-seat.ps1'
        $wtClaim = Join-Path $labWt 'scripts/claim-seat.ps1'
        # No -StoreDir anywhere in this block. That is the whole point.
        function Invoke-LabSeat {
            param([string] $Script, [string[]] $SeatArgs)
            $out = & pwsh -NoProfile -File $Script @SeatArgs 2>&1 | Out-String
            return @{ code = $LASTEXITCODE; text = $out.Trim() }
        }

        $mainClaim = Invoke-LabSeat $labClaim @('-WorkItem', 'DRA-901', '-SeatId', 'seat-in-main')
        Expect-Ok 'a seat claims in the main working tree' $mainClaim 'claimed DRA-901'

        # THE ROW THIS CARD EXISTS FOR. A second executor, in a different
        # working copy of the same clone, must be refused by the first.
        $wtBlocked = Invoke-LabSeat $wtClaim @('-WorkItem', 'DRA-901', '-SeatId', 'seat-in-worktree')
        Expect-Fail 'a default claim from a LINKED WORKTREE is refused by the main tree''s holder' $wtBlocked 'already held by'
        if ($wtBlocked.text -notmatch 'seat-in-main') {
            $script:failed += "$($script:step). cross-worktree refusal must name the holder seat-in-main: $($wtBlocked.text)"
        }

        # ...and the reverse direction, so this is a shared store rather than
        # a worktree that happens to read the main tree one-way.
        $wtOwn = Invoke-LabSeat $wtClaim @('-WorkItem', 'DRA-902', '-SeatId', 'seat-in-worktree')
        Expect-Ok 'a seat claims from the worktree' $wtOwn 'claimed DRA-902'
        $mainBlocked = Invoke-LabSeat $labClaim @('-WorkItem', 'DRA-902', '-SeatId', 'seat-in-main')
        Expect-Fail 'a default claim in the MAIN tree is refused by the worktree''s holder' $mainBlocked 'already held by'
        if ($mainBlocked.text -notmatch 'seat-in-worktree') {
            $script:failed += "$($script:step). reverse refusal must name the holder seat-in-worktree: $($mainBlocked.text)"
        }

        # Both copies must NAME the same file. -Where is the diagnostic that
        # turns "I looked in my worktree and claims.json was not there" into a
        # question with an answer.
        $whereMain = Invoke-LabSeat $labClaim @('-Where')
        Expect-Ok 'the main tree names its store' $whereMain 'shared by every worktree'
        $whereWt = Invoke-LabSeat $wtClaim @('-Where')
        Expect-Ok 'the worktree names its store' $whereWt 'shared by every worktree'
        $script:step++
        $pathOf = {
            param($text)
            $m = [regex]::Match($text, '(?m)^store:\s*(.+?)\s*\(')
            if ($m.Success) { return $m.Groups[1].Value.Trim() }
            return $null
        }
        $pMain = & $pathOf $whereMain.text
        $pWt = & $pathOf $whereWt.text
        if (-not $pMain -or -not $pWt) {
            $script:failed += "$($script:step). -Where must print a 'store: <path>' line in both copies (main='$($whereMain.text)', wt='$($whereWt.text)')"
        }
        elseif ($pMain -ne $pWt) {
            $script:failed += "$($script:step). the two copies resolve DIFFERENT stores — main '$pMain' vs worktree '$pWt'. Two seats reading two files refuse nobody (DRA-90)."
        }

        # The store lives in the MAIN tree, and the worktree writes no rival
        # copy. The reporter's observation, turned into an assertion: the file
        # is absent there, and that absence is correct rather than the bug.
        $script:step++
        if (-not (Test-Path (Join-Path $labMain '.claude/soft-seats/claims.json'))) {
            $script:failed += "$($script:step). the shared store is not in the main tree at .claude/soft-seats/claims.json"
        }
        if (Test-Path (Join-Path $labWt '.claude/soft-seats/claims.json')) {
            $script:failed += "$($script:step). the worktree grew its OWN claims.json — the store is per-working-copy and the mutex is blind (DRA-90)."
        }

        # THE REACHABLE NEGATIVE (trap 78: a guard aimed at nothing is green).
        # Every row above asserts a refusal, and a refusal can pass for reasons
        # that have nothing to do with sharing. So prove the same claim
        # SUCCEEDS the moment the two seats are pointed at different stores —
        # i.e. that the unshared store really is the bug, and that these rows
        # would go green-and-wrong if discovery ever regressed to per-copy.
        $wtPrivate = Invoke-LabSeat $wtClaim @(
            '-WorkItem', 'DRA-901', '-SeatId', 'seat-in-worktree',
            '-StoreDir', (Join-Path $labWt '.claude/soft-seats'))
        Expect-Ok 'a PRIVATE store admits the duplicate the shared one refused (this is the bug, reproduced)' $wtPrivate 'claimed DRA-901'

        # ...and that private store must SAY it is private, so a seat reading
        # -Where cannot mistake a hand-passed dir for the machine's shared one.
        $wherePrivate = Invoke-LabSeat $wtClaim @('-Where', '-StoreDir', (Join-Path $labWt '.claude/soft-seats'))
        Expect-Ok 'an explicit -StoreDir reports itself as NOT the shared store' $wherePrivate 'NOT the machine'
    }
    finally {
        # Detach the worktree admin record before deleting, or git leaves a
        # stale entry pointing into a temp dir that no longer exists.
        & git -C (Join-Path $lab 'main') worktree remove --force (Join-Path $lab 'wt') 2>&1 | Out-Null
        Remove-Item -LiteralPath $lab -Recurse -Force -ErrorAction SilentlyContinue
    }
    # ------------------------------------------------------------------------

    # --- DRA-102 / DRA-95 A': the refusal crosses independent CLONES ---------
    # The block above proves a shared store across WORKTREES of one clone. Two
    # independent CLONES shared nothing, and on this machine they are the two
    # dispatch lanes: DRA-87's two executors both claimed, 2m29s apart, and
    # neither was refused OR warned.
    #
    # Helm's verification bar, verbatim: build two clones with `git clone` (NOT
    # `git worktree add`), call the scripts with NO -StoreDir, assert clone 2 is
    # refused naming the holder AND the clone it is in, and pair it with the
    # reachable negative — registry absent => clone 2 SUCCEEDS — so rows 1-2
    # cannot pass by accident (trap 78: a guard aimed at nothing is green).
    $clones = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-soft-seat-clones-" + [guid]::NewGuid().ToString('N'))
    $cloneRegistry = Join-Path $clones 'registry/stores.json'
    $registryBefore = $env:EQBUDDY_SOFT_SEAT_REGISTRY
    try {
        $seed = Join-Path $clones 'seed'
        New-Item -ItemType Directory -Force -Path (Join-Path $seed 'scripts') | Out-Null
        foreach ($f in @('claim-seat.ps1', 'release-seat.ps1', 'soft-seat-store.ps1')) {
            Copy-Item (Join-Path $PSScriptRoot $f) (Join-Path $seed "scripts/$f") -Force
        }
        [IO.File]::WriteAllText((Join-Path $seed '.gitignore'), ".claude/soft-seats/claims.json`n", [Text.UTF8Encoding]::new($false))
        & git -C $seed init -q . 2>&1 | Out-Null
        & git -C $seed add -A 2>&1 | Out-Null
        & git -C $seed -c user.email='seat@lab' -c user.name='seat lab' commit -qm 'seat lab seed' 2>&1 | Out-Null

        $c1 = Join-Path $clones 'clone-1'
        $c2 = Join-Path $clones 'clone-2'
        & git clone -q $seed $c1 2>&1 | Out-Null
        & git clone -q $seed $c2 2>&1 | Out-Null

        $c1Claim = Join-Path $c1 'scripts/claim-seat.ps1'
        $c2Claim = Join-Path $c2 'scripts/claim-seat.ps1'
        $c2Release = Join-Path $c2 'scripts/release-seat.ps1'
        $c1StoreJson = [IO.Path]::GetFullPath((Join-Path $c1 '.claude/soft-seats/claims.json'))
        $c2StoreJson = [IO.Path]::GetFullPath((Join-Path $c2 '.claude/soft-seats/claims.json'))
        $c1StoreDir = [IO.Path]::GetFullPath((Join-Path $c1 '.claude/soft-seats'))

        $script:step++
        if (-not (Test-Path $c1Claim) -or -not (Test-Path $c2Claim)) {
            $script:failed += "$($script:step). clone lab — git clone did not produce two working copies ($c1, $c2)"
            throw 'selftest aborted: clone lab not built'
        }
        # Two CLONES, not two worktrees: each must resolve its own store, or the
        # rows below would be testing the worktree case again under a new name.
        $script:step++
        $g1 = (& git -C $c1 rev-parse --path-format=absolute --git-common-dir 2>$null | Out-String).Trim()
        $g2 = (& git -C $c2 rev-parse --path-format=absolute --git-common-dir 2>$null | Out-String).Trim()
        if (-not $g1 -or -not $g2 -or ($g1 -ieq $g2)) {
            $script:failed += "$($script:step). clone lab — the two clones must NOT share a git common dir (got '$g1' and '$g2'); this block would otherwise re-test worktrees."
        }

        # Every call in this block passes NO -StoreDir. The registry is the only
        # thing the two clones share.
        function Invoke-CloneSeat {
            param([string] $Script, [string[]] $SeatArgs, [string] $Registry)
            $saved = $env:EQBUDDY_SOFT_SEAT_REGISTRY
            if ($PSBoundParameters.ContainsKey('Registry')) { $env:EQBUDDY_SOFT_SEAT_REGISTRY = $Registry }
            try {
                $out = & pwsh -NoProfile -File $Script @SeatArgs 2>&1 | Out-String
                return @{ code = $LASTEXITCODE; text = $out.Trim() }
            }
            finally {
                if ($null -eq $saved) { Remove-Item Env:EQBUDDY_SOFT_SEAT_REGISTRY -ErrorAction SilentlyContinue }
                else { $env:EQBUDDY_SOFT_SEAT_REGISTRY = $saved }
            }
        }

        $env:EQBUDDY_SOFT_SEAT_REGISTRY = $cloneRegistry

        $one = Invoke-CloneSeat $c1Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-1')
        Expect-Ok 'a seat claims in clone 1' $one 'claimed DRA-911'

        # The diagnostic half Helm ruled in scope: -Where prints every store
        # CONSULTED, not just the resolved one. "It did not refuse" and "it
        # never looked there" are different claims (DRA-90, one level up).
        $whereTwo = Invoke-CloneSeat $c2Claim @('-Where')
        Expect-Ok 'clone 2 -Where names the registry' $whereTwo 'registry:'
        foreach ($needle in @($c1StoreDir, "clone $c1")) {
            if ($whereTwo.text -notmatch [regex]::Escape($needle)) {
                $script:failed += "$($script:step). -Where must name clone 1's store as a consulted store ('$needle'): $($whereTwo.text)"
            }
        }

        # *** THE ROW THIS CARD EXISTS FOR ***
        $twoBlocked = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2')
        Expect-Fail 'a default claim in clone 2 is refused by clone 1''s holder' $twoBlocked 'already held by'
        foreach ($needle in @('seat-in-clone-1', 'another CLONE', $c1)) {
            if ($twoBlocked.text -notmatch [regex]::Escape($needle)) {
                $script:failed += "$($script:step). the cross-clone refusal must name the holder AND the clone it is in ('$needle'): $($twoBlocked.text)"
            }
        }
        # A recovery that points at THIS tree's release-seat.ps1 answers "no live
        # claim matches" and reads as a spurious refusal. It must point at clone 1.
        if ($twoBlocked.text -notmatch [regex]::Escape((Join-Path $c1 'scripts/release-seat.ps1'))) {
            $script:failed += "$($script:step). the cross-clone refusal must name the recovery IN THE HOLDER'S CLONE: $($twoBlocked.text)"
        }

        # The must-list half (trap 34). A union read that refused everything
        # would pass every row above it.
        $twoOwn = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-912', '-SeatId', 'seat-in-clone-2')
        Expect-Ok 'clone 2 still claims a card nobody holds' $twoOwn 'claimed DRA-912'

        # ...and the reverse direction, so this is a union read rather than one
        # clone happening to look at the other.
        $oneBlocked = Invoke-CloneSeat $c1Claim @('-WorkItem', 'DRA-912', '-SeatId', 'seat-in-clone-1')
        Expect-Fail 'a default claim in clone 1 is refused by clone 2''s holder' $oneBlocked 'already held by'
        foreach ($needle in @('seat-in-clone-2', $c2)) {
            if ($oneBlocked.text -notmatch [regex]::Escape($needle)) {
                $script:failed += "$($script:step). the reverse refusal must name the holder and its clone ('$needle'): $($oneBlocked.text)"
            }
        }

        # The union read must EXCLUDE this clone's own store, and the only way
        # that shows is a same-seat re-claim: a foreign row is not filtered by
        # seat id (deliberately — a seat name is scoped to its store), so a
        # clone that read itself as foreign would be refused BY ITS OWN ROW and
        # every refusal row above would still be green.
        $oneAgain = Invoke-CloneSeat $c1Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-1')
        Expect-Ok 'a same-seat re-claim is still idempotent with the registry on' $oneAgain 'refreshed DRA-911'

        # local-WRITE is the other half of the signed shape. A union read that
        # COPIED the holder's row would satisfy every refusal row above and
        # quietly turn the registry into a claims mailbox (trap 60's shape).
        $script:step++
        $c1Rows = [IO.File]::ReadAllText($c1StoreJson)
        $c2Rows = [IO.File]::ReadAllText($c2StoreJson)
        if ($c1Rows -notmatch 'DRA-911' -or $c2Rows -notmatch 'DRA-912') {
            $script:failed += "$($script:step). each clone must hold its OWN claim (clone1 DRA-911, clone2 DRA-912)."
        }
        if ($c2Rows -match 'DRA-911') {
            $script:failed += "$($script:step). clone 2's store grew a DRA-911 row — the refused claim wrote, or the union read copied clone 1's row."
        }
        if ($c1Rows -match 'DRA-912') {
            $script:failed += "$($script:step). clone 1's store grew a DRA-912 row — the union read is writing to stores it only reads."
        }

        # Helm ruled A' does not trip the Founder door because the registry
        # holds PATHS. If claim data ever leaks into it, that ruling no longer
        # covers what shipped.
        $script:step++
        $registryText = [IO.File]::ReadAllText($cloneRegistry)
        foreach ($leak in @('seat-in-clone-1', 'seat-in-clone-2', 'DRA-911', 'DRA-912', 'work_item', 'claims')) {
            if ($registryText -match [regex]::Escape($leak)) {
                $script:failed += "$($script:step). the store registry must hold PATHS only — it contains '$leak'. That is shape A (claims out of the repo), which Helm did not sign."
            }
        }

        # trap 80, made reachable: an entry whose 'dir' is an ARRAY. `-eq`
        # against an array is a FILTER, not a boolean, so an unchecked field
        # would make a foreign store compare as "this one" and vanish from the
        # union read — a refusal that silently stops refusing. Assert the
        # detector FIRES (trap 78) and that the union read survives it.
        $registrySnapshot = $registryText
        $planted = $registryText | ConvertFrom-Json
        $planted.stores = @($planted.stores | ForEach-Object { $_ }) + @(
            [pscustomobject]@{ dir = @('two', 'paths'); root = 'nonsense'; first_seen = $null }
        )
        [IO.File]::WriteAllText($cloneRegistry, ($planted | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))

        $whereBad = Invoke-CloneSeat $c2Claim @('-Where')
        Expect-Ok 'a registry entry whose dir is not a path is NAMED, not acted on' $whereBad "is not a single path string"
        $stillBlocked = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2')
        Expect-Fail 'a malformed registry entry does not disarm the union read' $stillBlocked 'seat-in-clone-1'

        # A registered store whose directory is gone must be REPORTED, not
        # silently skipped: "we read every store" and "we read the ones that
        # were there" are different claims (trap 81).
        $gone = [IO.Path]::GetFullPath((Join-Path $clones 'clone-gone'))
        $planted2 = [IO.File]::ReadAllText($cloneRegistry) | ConvertFrom-Json
        $planted2.stores = @($planted2.stores | ForEach-Object { $_ }) + @(
            [pscustomobject]@{
                dir        = (Join-Path $gone '.claude/soft-seats')
                root       = $gone
                first_seen = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
            }
        )
        [IO.File]::WriteAllText($cloneRegistry, ($planted2 | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
        $whereGone = Invoke-CloneSeat $c2Claim @('-Where')
        Expect-Ok 'a registered store whose directory is gone reads as MISSING' $whereGone 'MISSING'
        $grantGone = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-913', '-SeatId', 'seat-in-clone-2')
        Expect-Ok 'a grant says which registered stores it could NOT consult' $grantGone 'could not be read'
        [IO.File]::WriteAllText($cloneRegistry, $registrySnapshot, [Text.UTF8Encoding]::new($false))

        # *** THE REACHABLE NEGATIVE — Helm's bar row 3 ***
        # Registry absent => clone 2 SUCCEEDS, which is exactly today's
        # behaviour. Without this, every refusal row above could be green for a
        # reason that has nothing to do with the registry.
        Remove-Item -LiteralPath $cloneRegistry -Force
        $noRegistry = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2')
        Expect-Ok 'registry ABSENT: clone 2 takes the duplicate seat (the pre-DRA-102 bug, reproduced)' $noRegistry 'claimed DRA-911'
        $undo = Invoke-CloneSeat $c2Release @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2')
        Expect-Ok 'the duplicate row is released again' $undo 'abandoned'

        # ...and the explicit door, which is the same degradation somebody can
        # switch on. It must be exactly that string and it must SAY so on the
        # screen that granted the seat (trap 68: a guard that steps aside for
        # the value it exists to override).
        [IO.File]::WriteAllText($cloneRegistry, $registrySnapshot, [Text.UTF8Encoding]::new($false))
        $offGrant = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2') -Registry 'off'
        Expect-Ok 'EQBUDDY_SOFT_SEAT_REGISTRY=off grants the duplicate' $offGrant 'claimed DRA-911'
        if ($offGrant.text -notmatch 'registry is OFF') {
            $script:failed += "$($script:step). a grant decided with the registry OFF must say so, or it is indistinguishable from one that consulted every clone: $($offGrant.text)"
        }
        $undoOff = Invoke-CloneSeat $c2Release @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2') -Registry 'off'
        Expect-Ok 'the off-door duplicate is released again' $undoOff 'abandoned'

        # And the refusal COMES BACK once the registry is restored, so the two
        # negatives above measured the registry rather than drifted state.
        $blockedAgain = Invoke-CloneSeat $c2Claim @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-2')
        Expect-Fail 'the cross-clone refusal returns with the registry restored' $blockedAgain 'seat-in-clone-1'

        # release-seat.ps1 in the WRONG clone used to answer "no live claim
        # matches", which reads as the refusal having been imaginary.
        $wrongClone = Invoke-CloneSeat $c2Release @('-WorkItem', 'DRA-911', '-SeatId', 'seat-in-clone-1')
        Expect-Fail 'releasing a foreign holder from the wrong clone refuses' $wrongClone 'no live claim matches'
        if ($wrongClone.text -notmatch [regex]::Escape((Join-Path $c1 'scripts/release-seat.ps1'))) {
            $script:failed += "$($script:step). release-seat must point a foreign holder at the clone that owns the row: $($wrongClone.text)"
        }

        # The PRODUCTION registry location, asserted once. Every row in this
        # block overrides it, so without this the whole mechanism could point at
        # a temp file on a real machine and the suite would still be green
        # (trap 76: a fixture that removes the mechanism verifies its absence).
        $script:step++
        $probeSaved = $env:EQBUDDY_SOFT_SEAT_REGISTRY
        Remove-Item Env:EQBUDDY_SOFT_SEAT_REGISTRY -ErrorAction SilentlyContinue
        try {
            $defaultLoc = Get-SoftSeatRegistryLocation
            $wantDefault = Join-Path ([Environment]::GetEnvironmentVariable('LOCALAPPDATA')) 'DranakCorps/soft-seats/stores.json'
            if ($defaultLoc.state -ne 'ok' -or (Get-SoftSeatComparablePath $defaultLoc.path) -ne (Get-SoftSeatComparablePath $wantDefault)) {
                $script:failed += "$($script:step). with no override the registry must be '$wantDefault', got state '$($defaultLoc.state)' path '$($defaultLoc.path)'."
            }
            # The door is exactly 'off'. A near miss is a PATH, not a switch.
            $env:EQBUDDY_SOFT_SEAT_REGISTRY = 'OFF'
            if ((Get-SoftSeatRegistryLocation).state -ne 'off') {
                $script:failed += "$($script:step). 'OFF' must be the same door as 'off' — case is a spelling."
            }
            $env:EQBUDDY_SOFT_SEAT_REGISTRY = 'offline'
            if ((Get-SoftSeatRegistryLocation).state -ne 'ok') {
                $script:failed += "$($script:step). only the exact string 'off' is the door; 'offline' is a path."
            }
        }
        finally {
            if ($null -eq $probeSaved) { Remove-Item Env:EQBUDDY_SOFT_SEAT_REGISTRY -ErrorAction SilentlyContinue }
            else { $env:EQBUDDY_SOFT_SEAT_REGISTRY = $probeSaved }
        }
    }
    finally {
        $env:EQBUDDY_SOFT_SEAT_REGISTRY = $registryBefore
        Remove-Item -LiteralPath $clones -Recurse -Force -ErrorAction SilentlyContinue
    }
    # ------------------------------------------------------------------------

    # --- DRA-399: paperclip-card.ps1 DryRun must not auto-assign -----------
    # A standalone seat claim must leave the card's assignee untouched.
    # DryRun only — no live Paperclip card is written.
    $cardNoAssign = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'paperclip-card.ps1') -Issue 'DRA-1' -Status 'in_progress' -DryRun 2>&1 | Out-String
    $script:step++
    if ($LASTEXITCODE -ne 0) {
        $script:failed += "$($script:step). DRA-399a — expected exit 0, got $($LASTEXITCODE): $cardNoAssign"
    }
    if ($cardNoAssign -match [regex]::Escape('--assignee-agent-id')) {
        $script:failed += "$($script:step). DRA-399a — DryRun in_progress without explicit id must NOT emit --assignee-agent-id: $cardNoAssign"
    }

    $cardWithAssign = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'paperclip-card.ps1') -Issue 'DRA-1' -Status 'in_progress' -AssigneeAgentId 'test-agent-123' -DryRun 2>&1 | Out-String
    $script:step++
    if ($LASTEXITCODE -ne 0) {
        $script:failed += "$($script:step). DRA-399b — expected exit 0, got $($LASTEXITCODE): $cardWithAssign"
    }
    if ($cardWithAssign -notmatch [regex]::Escape('--assignee-agent-id test-agent-123')) {
        $script:failed += "$($script:step). DRA-399b — DryRun in_progress with explicit id must emit --assignee-agent-id test-agent-123: $cardWithAssign"
    }
    # ------------------------------------------------------------------------

    # --- DRA-467: a refused card write is a WARNING, not a bare line --------
    # claim-seat resolves paperclip-card.ps1 beside itself, so run a COPY in a
    # temp dir with a stub card script. No live Paperclip card is written. The
    # refusing stub prints the exact text the live API gave on a throwaway
    # unassigned card (2026-09-26); the accepting stub is the paired control.
    $stubDir = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-claim-card-stub-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $stubDir | Out-Null
    try {
        Copy-Item (Join-Path $PSScriptRoot 'claim-seat.ps1'), (Join-Path $PSScriptRoot 'soft-seat-store.ps1') $stubDir
        $stubClaim = Join-Path $stubDir 'claim-seat.ps1'
        $stubCard = Join-Path $stubDir 'paperclip-card.ps1'

        Set-Content -LiteralPath $stubCard -Value "Write-Host 'API error 422: in_progress issues require an assignee'`nexit 1"
        $refused = Invoke-Seat $stubClaim @('-WorkItem', 'DRA-467', '-SeatId', 'seat-card-stub', '-PaperclipIssue', 'DRA-467')
        Expect-Ok 'DRA-467a — a refused card write still grants the seat (best-effort)' $refused 'OK: claimed DRA-467'
        Expect-Ok 'DRA-467a — a refused card write is a WARNING naming the card' $refused 'WARNING: the seat is claimed, but card DRA-467 was NOT moved to in_progress'
        Expect-Ok 'DRA-467a — the WARNING carries the API error' $refused 'API error 422: in_progress issues require an assignee'
        Expect-Ok 'DRA-467a — the WARNING names the expected pre-state' $refused 'assign DRA-467 to the working role BEFORE claiming'

        Set-Content -LiteralPath $stubCard -Value "Write-Host 'DRA-466 → in_progress'`nexit 0"
        $accepted = Invoke-Seat $stubClaim @('-WorkItem', 'DRA-466', '-SeatId', 'seat-card-stub', '-PaperclipIssue', 'DRA-466')
        Expect-Ok 'DRA-467b — an accepted card write prints the card line' $accepted 'DRA-466 → in_progress'
        $script:step++
        if ($accepted.text -match 'WARNING: the seat is claimed') {
            $script:failed += "$($script:step). DRA-467b — an accepted card write must NOT warn: $($accepted.text)"
        }
    }
    finally {
        Remove-Item -LiteralPath $stubDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    # ------------------------------------------------------------------------
}
finally {
    Remove-Item -LiteralPath $store -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $registryLab -Recurse -Force -ErrorAction SilentlyContinue
    if ($null -eq $script:savedRegistryEnv) {
        Remove-Item Env:EQBUDDY_SOFT_SEAT_REGISTRY -ErrorAction SilentlyContinue
    }
    else {
        $env:EQBUDDY_SOFT_SEAT_REGISTRY = $script:savedRegistryEnv
    }
}

if ($failed.Count -gt 0) {
    Write-Host 'soft-seat-selftest FAILED:'
    $failed | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "soft-seat-selftest ok ($($script:step) checks)"
exit 0
