<#
.SYNOPSIS
    Claim a Soft / Claude seat for one work item, or refuse it because another
    live seat already holds that item.

.DESCRIPTION
    Experiment A' on EQBuddy (the lab), 2026-09-08 — not a Corps standard.
    Soft max ≤3 is a count, not a mutex. This is the smallest mechanical refuse
    so we can measure it: one JSON store on the machine, shared by every worktree
    of this clone, gitignored so it cannot become a mailbox rebase war.

    Since DRA-102 (DRA-95 A', Helm-signed 2026-09-16) the refusal crosses
    independent CLONES too, by union-READ / local-WRITE. A machine-level
    registry of store PATHS (%LOCALAPPDATA%\DranakCorps\soft-seats\stores.json,
    no claim data) records each clone's resolved store the first time it runs
    these scripts; a claim reads every registered store and writes only its own.
    Registry absent, unreadable or EQBUDDY_SOFT_SEAT_REGISTRY=off degrades to
    the pre-DRA-102 behaviour, and a grant decided that way says so.
    -Where prints every store consulted, not just the resolved one.

    INVOKE THIS SCRIPT RESOLVED, through the clone's MAIN checkout (DRA-107,
    Helm-signed 2026-09-16):

        pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" ...

    A linked worktree shares its clone's STORE (correct, DRA-90) but carries its
    OWN checkout of this file. Invoked by the bare relative path from a stale
    worktree, a pre-DRA-102 copy runs, consults no registry, and GRANTS the
    cross-clone duplicate DRA-102 closed — measured on the same card in the same
    second: local copy "OK: claimable", resolved copy REFUSED naming the foreign
    holder. --git-common-dir answers the clone's .git from a linked worktree and
    from the main checkout alike, so one invocation is right everywhere. Needs
    git >= 2.31 for --path-format=absolute. The bare relative form still works
    and is DEMOTED, not removed: it is correct from a main checkout, wrong from a
    stale worktree, and cannot tell you which one you are in.

    -WorkItem IS the Paperclip card id, DRA-<n>, and nothing else (EXO-HARDEN-A2
    / DRA-50, 2026-09-10). A bare GitHub issue number is refused with the reason,
    never silently mapped onto a card: #445 and DRA-28 are two names for one
    scope, and the mutex only refuses a second seat that spells the key the same
    way. See scripts/soft-seat-store.ps1.

    Default (-Mode active) fails when ANY other live seat holds the work item —
    active, challenger, disjoint or replacement (DRA-76 / DRA-73 plan SS2.2,
    2026-09-14). Only an abandoned claim releases the item. It used to refuse
    only against an EXCLUSIVE holder, which let a default executor start beside
    a live challenger or disjoint seat: two executors, one card, neither refused.
    That is what PRs #566/#568 cost.

    -Mode challenger|disjoint|replacement is the explicit override and is never
    refused — a second seat is a choice somebody made, not a default that
    happened. Same-seat re-claim is idempotent. The recovery for a holder that
    is gone is release-seat.ps1 -ForceStale, and the refusal names it (plus
    every holder, and which of them look stale).

    Optional -PaperclipIssue: opts in to writing the card to in_progress on a
    successful claim (Phase 0 EXO-HARDEN-AGENTS / DRA-18). It must EQUAL
    -WorkItem — there is one key, not two. Omit it to claim without touching
    Paperclip.

    scheduled_tasks.lock and %TEMP%\eqbuddy-screen.lock are NOT Soft seat claims.
    Not a scheduler, not a control plane, never writes HELM-FEEDBACK.md.
    Formal proposal lands in the control-plane repo; this file only verifies.

.EXAMPLE
    pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -WorkItem DRA-28 -SeatId opus-isolation
    pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -WorkItem DRA-7 -SeatId x -PaperclipIssue DRA-7
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
    [switch] $Where,
    [switch] $Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'soft-seat-store.ps1')

$origin = Get-SoftSeatStoreOrigin -StoreDir $StoreDir -Repo $Repo
$dir = $origin.dir

# DRA-102: this clone registers its own store path once, then the refusal
# decision below reads every registered store and writes only this one.
# Registration is attempted before anything else so that a -Where run — the
# first thing a confused seat is told to do — is itself the "first use" that
# puts this clone on the board.
$registration = Register-SoftSeatStore -Origin $origin
$registry = Read-SoftSeatRegistry
$foreignStores = @(Get-SoftSeatForeignStores -Origin $origin -Registry $registry)

# "Which file is this seat reading?" — the question DRA-90 was reported without
# an answer to. A mutex is only as good as the store BOTH seats read, and the
# store is gitignored, so it is invisible in a fresh worktree by design; the
# only way to tell "shared" from "my own private copy" is to print it.
function Write-SoftSeatOrigin {
    param($Origin)
    switch ($Origin.kind) {
        'explicit' { Write-Host "store: $($Origin.dir) (-StoreDir, this call only — NOT the machine's shared store)" }
        'git-common-dir' { Write-Host "store: $($Origin.dir) (shared by every worktree of $($Origin.root))" }
        default {
            Write-Host "store: $($Origin.dir)"
            Write-Host 'WARNING: git did not resolve a common dir, so this is a PRIVATE store for this copy. A seat in another checkout cannot see it and will not be refused.'
        }
    }
}

# Helm ruled this the diagnostic half of the A' slice: -Where prints EVERY store
# consulted, not just the resolved one. A union read nobody can enumerate is the
# DRA-90 reporting problem again one level up — "it did not refuse" and "it
# never looked there" are different claims and only this tells them apart.
function Write-SoftSeatRegistryReadout {
    param($Origin, $Registry, $ForeignStores)
    switch ($Registry.state) {
        'ok' { Write-Host "registry: $($Registry.path) ($(@($Registry.stores).Count) registered store(s))" }
        'absent' { Write-Host "registry: $($Registry.path) (absent — no clone has registered yet)" }
        'off' { Write-Host "registry: OFF ($($Registry.why)) — cross-clone refusal is disabled for this call; this is the pre-DRA-102 behaviour." }
        'unavailable' { Write-Host "registry: unavailable ($($Registry.why)) — cross-clone refusal is not possible on this machine." }
        'unreadable' { Write-Host "registry: $($Registry.path) UNREADABLE ($($Registry.why)) — cross-clone refusal is standing down, which is today's behaviour, not a refusal." }
        default { Write-Host "registry: $($Registry.state)" }
    }
    foreach ($m in @($Registry.malformed)) {
        Write-Host "  registry WARNING: dropped $m"
    }
    if ($Origin.kind -eq 'explicit') {
        Write-Host 'consulted: this call passed -StoreDir, so ONLY that store is read. A seat in another clone is invisible to it.'
        return
    }
    Write-Host 'consulted:'
    Write-Host "  * $($Origin.dir)  (this clone — the only one this call WRITES)"
    foreach ($s in @($ForeignStores)) {
        $where = if ($s.root) { $s.root } else { $s.dir }
        if ($s.exists) { Write-Host "    $($s.dir)  (clone $where)" }
        else { Write-Host "    $($s.dir)  (clone $where) — MISSING: that directory no longer exists, so its seats cannot be read" }
    }
    if (@($ForeignStores).Count -eq 0 -and $Registry.state -eq 'ok') {
        Write-Host '    (no other clone is registered — a claim in one that has never run these scripts is still invisible)'
    }
}

if ($Where) {
    if ($Json) {
        [pscustomobject]@{
            origin       = $origin
            registration = $registration
            registry     = $registry
            consulted    = $foreignStores
        } | ConvertTo-Json -Depth 6
        exit 0
    }
    Write-SoftSeatOrigin $origin
    Write-SoftSeatRegistryReadout -Origin $origin -Registry $registry -ForeignStores $foreignStores
    exit 0
}

if ($List) {
    if ($Json) {
        $store = Read-SoftSeatStoreFile $dir
        $store | ConvertTo-Json -Depth 6
        exit 0
    }
    Write-SoftSeatOrigin $origin
    $listCode = Write-SoftSeatList $dir
    # -List stays a LOCAL list — it is this clone's board. But a claim-rate
    # measurement swept from one clone is not the machine's rate (the store
    # README says so), so say how many other stores exist and where to see them.
    if (@($foreignStores).Count -gt 0) {
        Write-Host "($(@($foreignStores).Count) other registered store(s) on this machine are NOT listed above — `$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1 -Where names them.)"
    }
    exit $listCode
}

if (-not $WorkItem -or -not $SeatId) {
    Write-Error 'Usage: pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -WorkItem DRA-<n> -SeatId <name> [-Mode active|challenger|disjoint|replacement] [-Branch <ref>] [-Worktree <path>] [-ExecutorPid <n>] [-PaperclipIssue DRA-<n>, same card] | -List | -Where  (that long form runs the clone''s MAIN checkout — a linked worktree''s own copy may be stale; DRA-107)'
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
        -Mode $Mode -Branch $Branch -Worktree $Worktree -ExecutorPid $pidArg `
        -ForeignStores $foreignStores -Check:$Check
}
catch {
    Write-Host $_.Exception.Message
    exit 1
}

if ($Json) { $result | ConvertTo-Json -Depth 6 }
else { Write-Host $result.message }

# A GRANT is the dangerous half. "No live holder" from a store no other seat
# writes to is the same sentence as "no live holder" from the shared one, and
# only this line tells them apart (DRA-90 / trap 82). Refusals need it less —
# a refusal already found somebody — but an unshared store makes a grant a lie.
if ($result.ok -and $origin.kind -eq 'fallback' -and -not $Json) {
    Write-Host 'WARNING: this claim was granted from a PRIVATE store (git did not resolve a common dir). A seat in another checkout is invisible to it. Run: pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/claim-seat.ps1" -Where'
}

# The same sentence, one level up (DRA-102). A grant decided WITHOUT the union
# read is the pre-registry answer, and it is indistinguishable from a grant that
# consulted every clone and found nothing — unless it says so. Degrading quietly
# is how an env var somebody exported in one shell turns the cross-clone refusal
# off for a week (trap 68).
if ($result.ok -and -not $Json -and $origin.kind -ne 'explicit') {
    switch ($registry.state) {
        'ok' { }
        'absent' { Write-Host "WARNING: the store registry at $($registry.path) is absent, so no other clone was consulted. A seat in another clone was not checked." }
        'off' { Write-Host "WARNING: the store registry is OFF ($($registry.why)), so NO other clone was consulted. Unset that variable to restore cross-clone refusal." }
        'unavailable' { Write-Host "WARNING: no store registry is possible here ($($registry.why)), so no other clone was consulted." }
        'unreadable' { Write-Host "WARNING: the store registry at $($registry.path) could not be read ($($registry.why)), so NO other clone was consulted." }
    }
    if ($registration.action -eq 'failed') {
        Write-Host "WARNING: this clone could not register its store ($($registration.why)). Another clone will not be refused by this seat."
    }
    foreach ($m in @($registry.malformed)) {
        Write-Host "WARNING: the store registry holds $m — that entry names no store and was skipped."
    }
}

# A read that THREW is a store we did not consult, and silence about it is the
# frozen-absence failure (trap 81): the refusal arithmetic would look identical.
if (-not $Json) {
    foreach ($u in @($result.foreign_unreadable)) {
        Write-Host "WARNING: registered store $($u.dir) could not be read ($($u.reason)) — its seats were NOT consulted."
    }
}

# union-READ / local-WRITE: an explicit second seat was admitted beside rows
# this script has no way to touch. "Replaced the holder" would be a lie.
if ($result.ok -and -not $Json -and -not $Check -and @($result.foreign_holders).Count -gt 0) {
    Write-Host "NOTE: $(@($result.foreign_holders).Count) live seat(s) on this card are in ANOTHER CLONE and were NOT changed by this call (this script writes only its own store):"
    foreach ($h in @($result.foreign_holders)) {
        Write-Host "  - $(Format-SoftSeatHolder $h)"
    }
}

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
