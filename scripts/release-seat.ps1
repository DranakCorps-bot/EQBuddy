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
    screen lock. Local store only (see scripts/soft-seat-store.ps1) — and since
    DRA-102 that is a deliberate half of union-READ / local-WRITE: this script
    never abandons a row in another clone's store, it NAMES it and the command
    that would, in the clone that owns it.

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

$origin = Get-SoftSeatStoreOrigin -StoreDir $StoreDir -Repo $Repo
$dir = $origin.dir
# Register on first use here too (DRA-102): a clone that only ever releases is
# still a clone whose claims another seat must be refused by.
$null = Register-SoftSeatStore -Origin $origin

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

# The union read changes what this script must SAY, not what it may write.
# claim-seat.ps1 can now refuse over a holder in another clone; without this,
# the reader's next move — run release-seat here — answers "no live claim
# matches", which reads as the refusal having been spurious. Read-only: the
# recovery is named, and it runs in the clone that owns the row.
if (-not $result.ok -and -not $Json -and $result.message -match 'no live claim matches') {
    $registry = Read-SoftSeatRegistry
    $foreign = Read-SoftSeatForeignHolders -Stores (Get-SoftSeatForeignStores -Origin $origin -Registry $registry) -WorkItem $WorkItem
    if (@($foreign.holders).Count -gt 0) {
        Write-Host "...but $(@($foreign.holders).Count) live seat(s) on this card are registered in ANOTHER CLONE. This script writes only its own store, so release them where they are:"
        foreach ($h in @($foreign.holders)) {
            Write-Host "  - $(Format-SoftSeatHolder $h)"
            $r = if ($h.store_root) { $h.store_root } else { (Split-Path (Split-Path $h.store_dir -Parent) -Parent) }
            Write-Host "    pwsh -NoProfile -File `"$(Join-Path $r 'scripts/release-seat.ps1')`" -WorkItem $(Format-SoftSeatWorkItem $h.work_item) -SeatId $($h.seat_id)"
        }
    }
    foreach ($u in @($foreign.unreadable)) {
        Write-Host "WARNING: registered store $($u.dir) could not be read ($($u.reason)) — its seats were NOT consulted."
    }
}

if ($result.ok) { exit 0 } else { exit 1 }
