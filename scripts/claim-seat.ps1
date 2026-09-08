<#
.SYNOPSIS
    Claim a Soft / Claude seat for one work item, or refuse a duplicate default claim.

.DESCRIPTION
    Operating-model experiment A' (2026-09-08): Soft max ≤3 is a count, not a mutex.
    This is the smallest mechanical refuse — one JSON store on the machine, shared by
    every worktree of this clone, gitignored so it cannot become a mailbox rebase war.

    Default (-Mode active) fails when another live exclusive claim (active or
    replacement) already holds the work item. -Mode challenger|disjoint|replacement
    is the explicit override. Same-seat re-claim is idempotent.

    scheduled_tasks.lock and %TEMP%\eqbuddy-screen.lock are NOT Soft seat claims.
    This file is not a scheduler, not a control plane, and never writes HELM-FEEDBACK.md.

.EXAMPLE
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem 428 -SeatId opus-isolation
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem 428 -SeatId docs-ssc -Mode disjoint
    pwsh -NoProfile -File scripts/claim-seat.ps1 -List
#>
[CmdletBinding()]
param(
    [string] $WorkItem,
    [string] $SeatId,
    [ValidateSet('active', 'challenger', 'disjoint', 'replacement')]
    [string] $Mode = 'active',
    [string] $Branch,
    [string] $Worktree,
    [int] $ExecutorPid,
    [string] $StoreDir,
    [string] $Repo,
    [switch] $List,
    [switch] $Check,
    [switch] $Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'soft-seat-store.ps1')

$dir = Get-SoftSeatStoreDir -StoreDir $StoreDir -Repo $Repo

if ($List) {
    if ($Json) {
        $store = Read-SoftSeatStoreFile $dir
        $store | ConvertTo-Json -Depth 6
        exit 0
    }
    exit (Write-SoftSeatList $dir)
}

if (-not $WorkItem -or -not $SeatId) {
    Write-Error 'Usage: claim-seat.ps1 -WorkItem <issue# or id> -SeatId <name> [-Mode active|challenger|disjoint|replacement] [-Branch <ref>] [-Worktree <path>] [-ExecutorPid <n>]'
    exit 1
}

$pidArg = $null
if ($PSBoundParameters.ContainsKey('ExecutorPid')) { $pidArg = $ExecutorPid }

try {
    $result = Invoke-SoftSeatClaim -StoreDir $dir -WorkItem $WorkItem -SeatId $SeatId `
        -Mode $Mode -Branch $Branch -Worktree $Worktree -ExecutorPid $pidArg -Check:$Check
}
catch {
    Write-Host $_.Exception.Message
    exit 1
}

if ($Json) { $result | ConvertTo-Json -Depth 6 }
else { Write-Host $result.message }

if ($result.ok) { exit 0 } else { exit 1 }
