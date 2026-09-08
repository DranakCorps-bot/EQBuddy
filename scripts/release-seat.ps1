<#
.SYNOPSIS
    Release or abandon a Soft seat claim. Recover a dead holder with -ForceStale.

.DESCRIPTION
    Own-seat release is always allowed. Releasing someone else's claim requires
    -ForceStale AND (age ≥ -StaleAfterHours, default 8, OR a recorded pid that is
    no longer running). A missing pid does not count as dead — only age does.

    Does not write HELM-FEEDBACK.md. Does not touch scheduled_tasks.lock or the
    screen lock. Local store only (see scripts/soft-seat-store.ps1).

.EXAMPLE
    pwsh -NoProfile -File scripts/release-seat.ps1 -WorkItem 428 -SeatId opus-isolation
    pwsh -NoProfile -File scripts/release-seat.ps1 -WorkItem 428 -ForceStale
#>
[CmdletBinding()]
param(
    [string] $WorkItem,
    [string] $SeatId,
    [switch] $ForceStale,
    [double] $StaleAfterHours = 8,
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
