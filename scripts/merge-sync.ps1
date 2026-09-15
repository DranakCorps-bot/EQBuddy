<#
.SYNOPSIS
    When a PR merges, move the Paperclip issue it names to `done`. One way only.

.DESCRIPTION
    DRA-77 / M0-4, governing plan DRA-73 rev 2 (SS3.2-3 + SS8.5).
    `exo-experiment: merge-sync`.

    The problem this exists for: issues sat `in_review` for days after the work
    had landed on `main` (EXO-HARDEN-A2, EQ-V2-HOME-CATCHUP), because closing
    the card is a separate act from merging the PR and nothing tied them
    together. The board then reads as busier than the work is.

    ONE WAY, GitHub -> Paperclip, for M0. This script never writes to GitHub:
    no `gh pr`, no label, no comment on the PR, no status check. That is a scope
    lock, not an oversight — a two-way sync has a loop to design and M0 does not
    need one to stop the drift. `merge-sync-selftest.ps1` scans this file for
    GitHub writes and reddens if one appears.

    WHAT IT DOES NOT DO, and why each is deliberate:
      - It does not open, reopen or create issues.
      - It does not touch an issue that is `blocked` or `cancelled`.
      - It does not guess between two candidate keys.
      - It does not fail the build when the secrets are absent. A repo that has
        not been given the key yet is not a broken repo; it prints SKIPPED and
        exits 0, the same shape ci.yml's other fail-open guards use. A
        CONFIGURED job that cannot reach Paperclip, or names an issue that does
        not exist, IS red — silence there would recreate the drift it fixes.

.PARAMETER Merged
    Taken as a STRING on purpose. GitHub hands this over as the text "true" or
    "false", and in PowerShell `[bool]"false"` is $true — a non-empty string is
    truthy. Typing this parameter [bool] would close an issue every time a PR
    was closed WITHOUT merging, which is the worst single bug this job could
    have. The self-test pins it.

.EXAMPLE
    pwsh -NoProfile -File scripts/merge-sync.ps1 -Branch claude/dra77-merge-sync-20260914 -Merged true -DryRun
#>
[CmdletBinding()]
param(
    # The PR's head branch (github.event.pull_request.head.ref).
    [string] $Branch = '',

    # The PR body (github.event.pull_request.body). May be empty.
    [string] $PrBody = '',

    [string] $PrNumber = '',
    [string] $PrUrl = '',
    [string] $PrTitle = '',

    # "true" only when the PR actually merged. See the .PARAMETER note above.
    [string] $Merged = 'false',

    # Paperclip. Defaults read the environment so a developer with the Paperclip
    # variables already exported can dry-run this without repeating them.
    [string] $ApiBase = $env:PAPERCLIP_API_URL,
    [string] $ApiKey = $env:PAPERCLIP_API_KEY,
    [string] $CompanyId = $env:PAPERCLIP_COMPANY_ID,

    # The issue-key prefix. One project today; a parameter so a second repo
    # pointing at a different Paperclip project is a flag and not a fork.
    [string] $Prefix = 'DRA',

    # Decide and print, touch nothing. What CI runs on a pull_request.
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'merge-sync-linkage.ps1')

function Write-Skip { param([string] $Message) Write-Host "SKIPPED: $Message" }
function Write-Did { param([string] $Message) Write-Host "OK: $Message" }

$prLabel = if ($PrNumber) { "PR #$PrNumber" } else { 'this PR' }

# ---------------------------------------------------------------------------
# 1. Did it actually merge?
# ---------------------------------------------------------------------------
if ($Merged.Trim().ToLowerInvariant() -ne 'true') {
    Write-Skip "$prLabel was closed without merging (merged='$Merged') — nothing landed, so nothing closes."
    exit 0
}

# ---------------------------------------------------------------------------
# 2. Which issue does it name?
# ---------------------------------------------------------------------------
$link = Resolve-MergeSyncLink -Branch $Branch -Body $PrBody -Prefix $Prefix

switch ($link.Outcome) {
    'none' {
        Write-Skip "$prLabel names no Paperclip issue — $($link.Reason). Put the key in the branch name (e.g. claude/dra77-...) to link it."
        exit 0
    }
    'ambiguous' {
        # Loud, but not red: the branch-wins rule means this can only be reached
        # by a PR whose branch named nothing, and going red there would paint
        # dependabot's queue with a failure nobody can act on.
        Write-Host "REFUSED: $prLabel — $($link.Reason)"
        exit 0
    }
}

$key = $link.Key
Write-Host "Linked: $prLabel -> $key ($($link.Reason))"

# ---------------------------------------------------------------------------
# 3. Are we configured to talk to Paperclip at all?
# ---------------------------------------------------------------------------
$missing = @()
if ([string]::IsNullOrWhiteSpace($ApiBase)) { $missing += 'PAPERCLIP_API_URL' }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $missing += 'PAPERCLIP_API_KEY' }
if ([string]::IsNullOrWhiteSpace($CompanyId)) { $missing += 'PAPERCLIP_COMPANY_ID' }
if ($missing.Count -gt 0 -and -not $DryRun) {
    Write-Skip ("not configured — " + ($missing -join ', ') + " absent from this repo's Actions secrets. Add them (Settings -> Secrets and variables -> Actions) and this job starts working on the next merge; until then it is inert by design.")
    exit 0
}

if ($DryRun) {
    Write-Host "DRY RUN: would look up $key and, if it is open work, set it to 'done'. No request made."
    exit 0
}

# Normalize the base the same way every Paperclip caller in this org does:
# accept it with or without a trailing /api.
$base = $ApiBase.TrimEnd('/')
if ($base.EndsWith('/api')) { $base = $base.Substring(0, $base.Length - 4) }

$headers = @{ Authorization = "Bearer $ApiKey" }

function Invoke-Paperclip {
    param(
        [string] $Method,
        [string] $Path,
        $Body
    )
    $uri = "$base$Path"
    $args = @{ Method = $Method; Uri = $uri; Headers = $headers; ErrorAction = 'Stop' }
    if ($null -ne $Body) {
        # ConvertTo-Json, never string interpolation. A PR title with a quote or
        # a backslash in it builds invalid JSON by hand and the API answers 500,
        # which reads as an outage rather than as our bad request.
        $args.Body = ($Body | ConvertTo-Json -Depth 6 -Compress)
        $args.ContentType = 'application/json; charset=utf-8'
    }
    return Invoke-RestMethod @args
}

# ---------------------------------------------------------------------------
# 4. Resolve the human key to the issue id.
# ---------------------------------------------------------------------------
# There is no lookup-by-identifier route, so this lists the company's issues and
# filters. ~190 KB for this board today; if that ever stops being cheap the fix
# is a route, not a cache.
try {
    # NOT `@(Invoke-Paperclip ...)`. That wraps the returned array a second time
    # instead of normalizing it, and the nested form makes every identifier
    # comparison a tautology — the first live run matched all eighty issues and
    # reported eighty concatenated statuses as one. `Select-MergeSyncIssue`
    # carries the measurement and refuses the shape.
    $response = Invoke-Paperclip -Method GET -Path "/api/companies/$CompanyId/issues?view=compact"
} catch {
    Write-Error "could not list Paperclip issues for company $CompanyId : $($_.Exception.Message)"
    exit 1
}

$found = Select-MergeSyncIssue -Issues $response -Key $key

if ($found.Outcome -eq 'malformed') {
    Write-Error "cannot read Paperclip's issue list for company $CompanyId — $($found.Reason)."
    exit 1
}
if ($found.Outcome -eq 'missing') {
    # RED on purpose. Somebody named a key; it does not exist. Staying quiet here
    # is how a mislabelled branch silently stops syncing forever, which is the
    # original complaint wearing a different hat.
    Write-Error "$prLabel names $key but no issue with that identifier exists in company $CompanyId ($($found.Reason)). Fix the branch name or the PR body."
    exit 1
}

$issue = $found.Issue

# ---------------------------------------------------------------------------
# 5. May that issue be closed?
# ---------------------------------------------------------------------------
$decision = Get-MergeSyncTransition -CurrentStatus $issue.status

if ($decision.Action -eq 'noop') {
    Write-Skip "$key — $($decision.Reason)."
    exit 0
}
if ($decision.Action -eq 'refuse') {
    Write-Host "REFUSED: $key — $($decision.Reason)."
    exit 0
}

# ---------------------------------------------------------------------------
# 6. Do it, then say so on the issue.
# ---------------------------------------------------------------------------
try {
    Invoke-Paperclip -Method PATCH -Path "/api/issues/$($issue.id)" -Body @{ status = $decision.To } | Out-Null
} catch {
    Write-Error "could not set $key to '$($decision.To)': $($_.Exception.Message)"
    exit 1
}
Write-Did "$key '$($issue.status)' -> '$($decision.To)' ($($decision.Reason))."

$where = if ($PrUrl) { $PrUrl } else { $prLabel }
$what = if ($PrTitle) { ": $PrTitle" } else { '' }
$note = "merge-sync: $prLabel merged$what, so this issue moved from ``$($issue.status)`` to ``$($decision.To)``.`n`n$where`n`nLinked by $($link.Source) ($($link.Reason)). One-way GitHub -> Paperclip; nothing was written back to the PR."

try {
    Invoke-Paperclip -Method POST -Path "/api/issues/$($issue.id)/comments" -Body @{ body = $note } | Out-Null
    Write-Did "left a comment on $key naming $prLabel."
} catch {
    # Deliberately NOT fatal. The transition is the deliverable and it already
    # happened; failing the job here would report a sync that did occur as one
    # that did not, and the next re-run would find the issue already `done` and
    # no-op — so the audit line would never be written anyway.
    Write-Warning "set $key to '$($decision.To)' but could not leave the audit comment: $($_.Exception.Message)"
}

exit 0
