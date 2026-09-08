<#
.SYNOPSIS
    Experiment A' on EQBuddy: prove default refuse, explicit modes, stale recovery.

.DESCRIPTION
    Lab check, not a Corps-standard suite. Uses a throwaway -StoreDir. Never
    touches the machine's live claims.json. Called from scripts/check.ps1 and CI
    so a broken refuse cannot read as coverage.

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
    $a1 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-a', '-Branch', 'claude/a')
    Expect-Ok 'first default claim succeeds' $a1 'claimed #428'

    $b1 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-b')
    Expect-Fail 'second default claim on same issue is blocked' $b1 'already claimed'
    if ($b1.text -notmatch 'seat-a') {
        $script:failed += "$($script:step). second default — refusal must name the holder seat-a: $($b1.text)"
    }

    $b2 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-b', '-Mode', 'challenger')
    Expect-Ok 'challenger is allowed beside an active claim' $b2 'challenger'

    $c1 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-c', '-Mode', 'disjoint')
    Expect-Ok 'disjoint is allowed beside an active claim' $c1 'disjoint'

    $d1 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-d', '-Mode', 'replacement')
    Expect-Ok 'replacement is allowed and takes the exclusive slot' $d1 'replacement'

    $a2 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-e')
    Expect-Fail 'default still refused after replacement (new exclusive holder)' $a2 'already claimed'

    $e1 = Invoke-Seat $claim @('-WorkItem', '429', '-SeatId', 'seat-e')
    Expect-Ok 'different work item is a separate claim' $e1 'claimed #429'

    $aAgain = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-d', '-Mode', 'replacement')
    Expect-Ok 'same-seat re-claim is idempotent' $aAgain 'refreshed'

    $relOwn = Invoke-Seat $release @('-WorkItem', '428', '-SeatId', 'seat-d')
    Expect-Ok 'own-seat release abandons the claim' $relOwn 'abandoned'

    $f1 = Invoke-Seat $claim @('-WorkItem', '428', '-SeatId', 'seat-f')
    Expect-Ok 'default claim succeeds after exclusive is released' $f1 'claimed #428'

    $relOther = Invoke-Seat $release @('-WorkItem', '428', '-SeatId', 'not-the-holder')
    Expect-Fail 'cannot release another seat without -ForceStale' $relOther 'REFUSED'

    $deadPid = Invoke-Seat $claim @('-WorkItem', '430', '-SeatId', 'seat-dead', '-ExecutorPid', '2147483646')
    Expect-Ok 'claim with a dead pid records' $deadPid 'claimed #430'
    $relDeadNo = Invoke-Seat $release @('-WorkItem', '430')
    Expect-Fail 'dead-pid recovery still needs -ForceStale' $relDeadNo 'REFUSED'
    $relDead = Invoke-Seat $release @('-WorkItem', '430', '-ForceStale')
    Expect-Ok 'dead pid + -ForceStale recovers' $relDead 'abandoned'

    $age = Invoke-Seat $claim @('-WorkItem', '431', '-SeatId', 'seat-old')
    Expect-Ok 'age-target claim records' $age 'claimed #431'
    $jsonPath = Join-Path $store 'claims.json'
    if (-not (Test-Path $jsonPath)) {
        $script:failed += 'age plant — claims.json was never written'
        throw 'selftest aborted: store file missing'
    }
    $raw = [IO.File]::ReadAllText($jsonPath)
    $planted = $raw | ConvertFrom-Json
    foreach ($c in @($planted.claims)) {
        if (([string] $c.work_item) -eq '431' -and ([string] $c.status) -eq 'active') {
            $c.started_at = [datetime]::UtcNow.AddHours(-9).ToString('yyyy-MM-ddTHH:mm:ssZ')
            $c.pid = $null
        }
    }
    [IO.File]::WriteAllText($jsonPath, ($planted | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    $relAgeNo = Invoke-Seat $release @('-WorkItem', '431')
    Expect-Fail 'aged claim still needs -ForceStale' $relAgeNo 'REFUSED'
    $relAge = Invoke-Seat $release @('-WorkItem', '431', '-ForceStale')
    Expect-Ok 'age threshold + -ForceStale recovers' $relAge 'abandoned'

    $fresh = Invoke-Seat $claim @('-WorkItem', '432', '-SeatId', 'seat-fresh', '-ExecutorPid', "$PID")
    Expect-Ok 'fresh live-pid claim records' $fresh 'claimed #432'
    $relFresh = Invoke-Seat $release @('-WorkItem', '432', '-ForceStale')
    Expect-Fail 'ForceStale refuses a fresh live-pid claim that is not ours' $relFresh 'not stale'
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
