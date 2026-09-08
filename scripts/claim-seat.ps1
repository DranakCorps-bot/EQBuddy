<#
.SYNOPSIS
    Claim a Soft / Claude seat for one work item, or refuse a duplicate default claim.

.DESCRIPTION
    Experiment A' on EQBuddy (the lab), 2026-09-08 — not a Corps standard.
    Soft max ≤3 is a count, not a mutex. This is the smallest mechanical refuse
    so we can measure it: one JSON store on the machine, shared by every worktree
    of this clone, gitignored so it cannot become a mailbox rebase war.

    Default (-Mode active) fails when another live exclusive claim (active or
    replacement) already holds the work item. -Mode challenger|disjoint|replacement
    is the explicit override. Same-seat re-claim is idempotent.

    Optional -PaperclipIssue DRA-n: on successful claim, Soft worker updates the
    Paperclip card to in_progress (Phase 0 EXO-HARDEN-AGENTS / DRA-18).

    scheduled_tasks.lock and %TEMP%\eqbuddy-screen.lock are NOT Soft seat claims.
    Not a scheduler, not a control plane, never writes HELM-FEEDBACK.md.
    Formal proposal lands in the control-plane repo; this file only verifies.

.EXAMPLE
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem 428 -SeatId opus-isolation
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem options-ia-impl -SeatId x -PaperclipIssue DRA-7
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
    [string] $PaperclipIssue,
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
    Write-Error 'Usage: claim-seat.ps1 -WorkItem <issue# or id> -SeatId <name> [-Mode active|challenger|disjoint|replacement] [-Branch <ref>] [-Worktree <path>] [-ExecutorPid <n>] [-PaperclipIssue DRA-n]'
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

if ($result.ok -and $PaperclipIssue -and -not $Check) {
    $cardScript = Join-Path $PSScriptRoot 'paperclip-card.ps1'
    if (Test-Path $cardScript) {
        try {
            & $cardScript -Issue $PaperclipIssue -Status in_progress -Comment "claim-seat: $SeatId on $WorkItem"
        } catch {
            Write-Host "paperclip-card warn: $($_.Exception.Message)"
        }
    }
}

if ($result.ok) { exit 0 } else { exit 1 }
