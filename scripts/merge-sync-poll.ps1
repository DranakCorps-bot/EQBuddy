<#
.SYNOPSIS
    Pull merged PRs with `gh` (read-only) and hand each one to merge-sync.ps1,
    on the Founder's machine, against a loopback/tailnet Paperclip API.

.DESCRIPTION
    DRA-528. This replaces `.github/workflows/merge-sync.yml`, which ran
    merge-sync on a GitHub-hosted runner. That needed the board key in this
    public repo's Actions secrets and the control-plane API reachable from
    GitHub's cloud. Pulling from here needs neither: GitHub is only READ
    (`gh pr list`), and the only write is merge-sync.ps1's own PATCH to a
    private address, which it now refuses to make to a public one.

    ONE WAY still holds: nothing here writes to GitHub.
    `merge-sync-selftest.ps1` scans this file as well as merge-sync.ps1.

    THE WATERMARK IS PERSISTED (trap 85). Every pass re-reads the same merged
    list, so "what I have handled since I started" would re-close, on every
    restart, a card a human reopened after its PR merged. State is the newest
    `mergedAt` handled plus the PR numbers handled at that exact instant, and it
    advances one PR at a time, only after merge-sync.ps1 exits 0.

    A FAILED PR STOPS THE PASS and the watermark stays behind it, so a transient
    outage retries next pass and nothing is skipped silently. A PR that can never
    succeed (its branch names an issue that does not exist) therefore holds the
    queue, loudly, until a human passes `-Skip <number>`, which is logged.

    FIRST RUN: with no state file this SEEDS the watermark at now (or at
    `-Since`) and handles nothing. It never replays history on its own: every
    older merge already had its chance under the workflow this replaces, and
    replaying them would re-close any card a human has reopened since.

    One pass at a time, per machine (a named mutex). Scheduling it is the
    Founder's step; the recipe is in docs/ops/merge-sync.md.

.EXAMPLE
    pwsh -NoProfile -File scripts/merge-sync-poll.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string] $Repo = 'DranakCorps-bot/EQBuddy',

    # Where the watermark lives. Machine-local on purpose: it describes what
    # THIS machine has written to the board.
    [string] $StatePath = (Join-Path $env:LOCALAPPDATA 'DranakCorps\merge-sync\state.json'),

    # Seed the watermark here instead of now, on a first run only.
    [string] $Since = '',

    # Advance past one PR by hand (it is logged, never silent).
    [int] $Skip = 0,

    # The most PRs one `gh` read may return. If the read comes back FULL, the
    # pass refuses rather than handle a list it cannot prove is complete.
    [int] $Limit = 100,

    # Test seam: a JSON file standing in for `gh pr list`'s output.
    [string] $PrsJson = '',

    # Decide and print; merge-sync makes no request and the watermark stays put.
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'merge-sync-linkage.ps1')
$syncScript = Join-Path $PSScriptRoot 'merge-sync.ps1'

$mutex = [System.Threading.Mutex]::new($false, 'Global\DranakCorps-merge-sync-poll')
$held = $false
try { $held = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $held = $true }
if (-not $held) {
    Write-Host 'SKIPPED: another merge-sync pass is running on this machine.'
    exit 0
}

try {
    # -----------------------------------------------------------------------
    # 1. The watermark.
    # -----------------------------------------------------------------------
    $state = $null
    if (Test-Path -LiteralPath $StatePath) {
        $state = Get-Content -Raw -Encoding utf8 -LiteralPath $StatePath | ConvertFrom-Json
    }

    function Save-State {
        param($Watermark, [int[]] $DoneAtWatermark)
        if ($DryRun) { return }
        $dir = Split-Path -Parent $StatePath
        if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        $json = [ordered]@{
            watermark       = (ConvertTo-MergeSyncUtc $Watermark).ToString('o')
            doneAtWatermark = @($DoneAtWatermark)
            repo            = $Repo
        } | ConvertTo-Json -Compress
        $tmp = "$StatePath.tmp"
        [System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $tmp -Destination $StatePath -Force
    }

    if ($null -eq $state) {
        $seed = if ($Since) { ConvertTo-MergeSyncUtc $Since } else { [System.DateTimeOffset]::UtcNow }
        Save-State -Watermark $seed -DoneAtWatermark @()
        Write-Host "SEEDED: no state at $StatePath; watermark set to $($seed.ToString('o')). Merges before it are not replayed."
        exit 0
    }

    $mark = ConvertTo-MergeSyncUtc $state.watermark
    $doneAt = @($state.doneAtWatermark | ForEach-Object { [int] $_ })

    # -----------------------------------------------------------------------
    # 2. The merged PRs since the watermark. READ only.
    # -----------------------------------------------------------------------
    $fields = 'number,title,body,url,headRefName,mergedAt'
    if ($PrsJson) {
        $raw = Get-Content -Raw -Encoding utf8 -LiteralPath $PrsJson
    } else {
        # The date qualifier is a coarse filter only (a day, so no instant is
        # lost to its granularity); the watermark does the exact cut.
        $day = $mark.AddDays(-1).ToString('yyyy-MM-dd')
        $raw = & gh pr list --repo $Repo --state merged --search "merged:>=$day" --limit $Limit --json $fields
        if ($LASTEXITCODE -ne 0) {
            Write-Error "gh pr list failed (exit $LASTEXITCODE) — is gh authenticated on this machine?"
            exit 1
        }
    }
    $prs = @($raw | ConvertFrom-Json | ForEach-Object { $_ })
    if (-not $PrsJson -and $prs.Count -ge $Limit) {
        Write-Error "gh returned $($prs.Count) PRs, the -Limit; the list may be truncated, so nothing is handled. Re-run with a larger -Limit."
        exit 1
    }

    $pending = @(Select-MergeSyncPending -Prs $prs -Watermark $mark -DoneAtWatermark $doneAt)
    if ($pending.Count -eq 0) {
        Write-Host "OK: nothing merged since $($mark.ToString('o'))."
        exit 0
    }
    Write-Host "Pending: $($pending.Count) merged PR(s) since $($mark.ToString('o'))."

    # -----------------------------------------------------------------------
    # 3. Hand each to merge-sync.ps1, oldest first, advancing as we go.
    # -----------------------------------------------------------------------
    foreach ($p in $pending) {
        $pr = $p.Pr
        if ($Skip -and $p.Number -eq $Skip) {
            Write-Host "SKIPPED BY HAND: PR #$($p.Number) (-Skip). merge-sync was not run for it."
        } else {
            $syncArgs = @(
                '-Branch', [string] $pr.headRefName,
                '-PrBody', [string] $pr.body,
                '-PrNumber', [string] $p.Number,
                '-PrUrl', [string] $pr.url,
                '-PrTitle', [string] $pr.title,
                '-Merged', 'true'
            )
            if ($DryRun) { $syncArgs += '-DryRun' }
            # A child process, so merge-sync's own `exit` and Write-Error stay its
            # own. The key travels in the inherited ENVIRONMENT, never on a
            # command line another process can read.
            & pwsh -NoProfile -File $syncScript @syncArgs
            if ($LASTEXITCODE -ne 0) {
                Write-Error "merge-sync failed for PR #$($p.Number) (exit $LASTEXITCODE). The watermark stays before it; the next pass retries. If it can never succeed, re-run with -Skip $($p.Number)."
                exit 1
            }
        }
        $next = Step-MergeSyncWatermark -Watermark $mark -DoneAtWatermark $doneAt -MergedAt $p.MergedAt -Number $p.Number
        $mark = $next.Watermark
        $doneAt = @($next.DoneAtWatermark)
        Save-State -Watermark $mark -DoneAtWatermark $doneAt
    }

    $verb = if ($DryRun) { 'decided (dry run, watermark unchanged)' } else { 'handled' }
    Write-Host "OK: $($pending.Count) PR(s) $verb."
    exit 0
} finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
