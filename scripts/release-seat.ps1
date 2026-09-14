<#
.SYNOPSIS
    Release or abandon a Soft seat claim. Recover a dead holder with -ForceStale.

.DESCRIPTION
    Experiment A' on EQBuddy (the lab) — not a Corps standard. Own-seat release
    is always allowed. Releasing someone else's claim requires -ForceStale AND
    (age ≥ -StaleAfterHours, default SoftSeatStaleAfterHours = 8 h, OR a recorded
    pid that is no longer running). A missing pid does not count as dead — only
    age does.

    This is the ONLY recovery from the DRA-76 refusal, and the widened rule made
    it load-bearing: a challenger or disjoint seat now holds the item too, so a
    default claim waits on someone releasing — or on -ForceStale proving the
    holder is gone.

    Does not write HELM-FEEDBACK.md. Does not touch scheduled_tasks.lock or the
    screen lock. Local store only (see scripts/soft-seat-store.ps1).

    -WorkItem is NOT key-checked here, unlike claim-seat.ps1. Claims recorded
    before DRA-50 carry bare issue numbers, and refusing to release them would
    strand every one of them behind a rule they predate.

.EXAMPLE
    pwsh -NoProfile -File scripts/release-seat.ps1 -WorkItem DRA-28 -SeatId opus-isolation
    pwsh -NoProfile -File scripts/release-seat.ps1 -WorkItem DRA-28 -ForceStale
#>
[CmdletBinding()]
param(
    [string] $WorkItem,
    [string] $SeatId,
    [switch] $ForceStale,
    # 0 = "unset": resolved to the store's one SoftSeatStaleAfterHours after
    # the dot-source below, so the refusal text and this default cannot drift.
    [double] $StaleAfterHours = 0,
    [string] $StoreDir,
    [string] $Repo,
    [switch] $Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'soft-seat-store.ps1')

$dir = Get-SoftSeatStoreDir -StoreDir $StoreDir -Repo $Repo

try {
    $result = Invoke-SoftSeatRelease -StoreDir $dir -WorkItem $WorkItem -SeatId $SeatId `
        -ForceStale:$ForceStale -StaleAfterHours $StaleAfterHours
}
catch {
    Write-Host $_.Exception.Message
    exit 1
}

if ($Json) { $result | ConvertTo-Json -Depth 6 }
else { Write-Host $result.message }

if ($result.ok) { exit 0 } else { exit 1 }
