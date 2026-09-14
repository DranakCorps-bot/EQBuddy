<#
.SYNOPSIS
    Claim a Soft / Claude seat for one work item, or refuse a duplicate default claim.

.DESCRIPTION
    Experiment A' on EQBuddy (the lab), 2026-09-08 — not a Corps standard.
    Soft max ≤3 is a count, not a mutex. This is the smallest mechanical refuse
    so we can measure it: one JSON store on the machine, shared by every worktree
    of this clone, gitignored so it cannot become a mailbox rebase war.

    -WorkItem IS the Paperclip card id, DRA-<n>, and nothing else (EXO-HARDEN-A2
    / DRA-50, 2026-09-10). A bare GitHub issue number is refused with the reason,
    never silently mapped onto a card: #445 and DRA-28 are two names for one
    scope, and the mutex only refuses a second seat that spells the key the same
    way. See scripts/soft-seat-store.ps1.

    Default (-Mode active) fails when another live exclusive claim (active or
    replacement) already holds the work item. -Mode challenger|disjoint|replacement
    is the explicit override. Same-seat re-claim is idempotent.

    Optional -PaperclipIssue: opts in to writing the card to in_progress on a
    successful claim (Phase 0 EXO-HARDEN-AGENTS / DRA-18). It must EQUAL
    -WorkItem — there is one key, not two. Omit it to claim without touching
    Paperclip.

    scheduled_tasks.lock and %TEMP%\eqbuddy-screen.lock are NOT Soft seat claims.
    Not a scheduler, not a control plane, never writes HELM-FEEDBACK.md.
    Formal proposal lands in the control-plane repo; this file only verifies.

.EXAMPLE
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem DRA-28 -SeatId opus-isolation
    pwsh -NoProfile -File scripts/claim-seat.ps1 -WorkItem DRA-7 -SeatId x -PaperclipIssue DRA-7
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
    Write-Error 'Usage: claim-seat.ps1 -WorkItem DRA-<n> -SeatId <name> [-Mode active|challenger|disjoint|replacement] [-Branch <ref>] [-Worktree <path>] [-ExecutorPid <n>] [-PaperclipIssue DRA-<n>, same card]'
    exit 1
}

# One key, not two. -PaperclipIssue is an opt-in to the card write, not a
# second name for the work — a claim under DRA-28 that reports to DRA-29 is
# the dual-key collision this card exists to close. Checked BEFORE the claim,
# so a mismatched call does not take the seat on its way to the error.
if ($PaperclipIssue) {
    $wantCard = Normalize-SoftSeatWorkItem $PaperclipIssue
    $wantItem = Normalize-SoftSeatWorkItem $WorkItem
    if ($wantCard -ne $wantItem) {
        Write-Host @"
REFUSED: -PaperclipIssue '$PaperclipIssue' is not -WorkItem '$WorkItem'.
A seat has ONE key. Two of them means the mutex guards one name while the card reports the other, and a second seat under the other spelling is refused by neither (CLAUDE.md trap 70).
Decide which card this seat is: pass that one as BOTH (-WorkItem $wantItem -PaperclipIssue $wantItem), or drop -PaperclipIssue and claim without writing the card.
"@.Trim()
        exit 1
    }
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
            $card = Normalize-SoftSeatWorkItem $WorkItem
            & $cardScript -Issue $card -Status in_progress -Comment "claim-seat: $SeatId on $card"
        } catch {
            Write-Host "paperclip-card warn: $($_.Exception.Message)"
        }
    }
}

if ($result.ok) { exit 0 } else { exit 1 }
