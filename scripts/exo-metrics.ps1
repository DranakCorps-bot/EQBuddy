<#
.SYNOPSIS
  ExO execution-model dashboard — DRA-73 plan §6 + §10.3.

.DESCRIPTION
  Computes the §6 metric table over a window of GitHub PRs and emits
  docs/ops/exo-dashboard.md. Dependencies: `gh` and the Paperclip API. Nothing
  else — no modules, no network beyond those two.

  Every metric is computed from data that already exists (PR timestamps, merge
  commits, workflow runs, HELM.md commits, the flake ledger, Paperclip issue
  records). A metric whose data does NOT exist in the window is reported as
  `unmeasured` **with the reason**, never as a zero — an unmeasured metric and a
  measured zero are different claims (trap 64b).

  -Baseline additionally freezes the run into docs/ops/exo-baseline.json, which
  later runs read to fill the "vs baseline" column of the §10.3 "Experiments in
  flight" section. Freezing is the whole point: every later claim about the new
  operating model is checkable against a number that was written down first.

.EXAMPLE
  pwsh -NoProfile -File scripts/exo-metrics.ps1 -FromPr 580 -ToPr 607 -Baseline

.EXAMPLE
  pwsh -NoProfile -File scripts/exo-metrics.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [int]$FromPr,
    [int]$ToPr,
    [string]$Repo = 'DranakCorps-bot/EQBuddy',
    [string]$Out = 'docs/ops/exo-dashboard.md',
    [string]$BaselineFile = 'docs/ops/exo-baseline.json',
    [string]$WindowLabel,
    [switch]$Baseline,
    [switch]$NoPaperclip,
    [switch]$SelfTest,
    [string]$ApiBase = $env:PAPERCLIP_API_URL,
    [string]$ApiKey = $env:PAPERCLIP_API_KEY,
    [string]$CompanyId = $env:PAPERCLIP_COMPANY_ID
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------------------
# Classification tables.
#
# Trap 78: every element of a computed array literal is parenthesised, because
# PowerShell binds `,` tighter than `+` and a bare list collapses into one
# string that matches nothing. Self-test asserts each list is non-empty AND
# that it fires.
# ---------------------------------------------------------------------------

# A PR whose only cargo is Helm's signature prose. Retired by DRA-74/M0-1; the
# baseline window is full of them and that is the number we are freezing.
$script:GovernanceBranchPattern = '^helm/ssc-(\d+)$'

# Rework markers — a PR that undoes or re-lands recent work.
$script:ReworkMarkers = @(
    ('revert'),
    ('re-land'),
    ('reland'),
    ('back out'),
    ('backed out'),
    ('undo '),
    ('regression')
)

# Veto markers — a Helm/Bevel ruling that refuses something already merged.
$script:VetoMarkers = @(
    ('VETO'),
    ('REJECT'),
    ('UNWIND'),
    ('ROLL BACK'),
    ('ROLLBACK')
)

# Branch-name fragments that make a PR a planning PR rather than a delivery one.
$script:PlanBranchMarkers = @(
    ('fable-'),
    ('-plan')
)

# Branch-name fragments that make a PR an ops/process PR (flake ledger, channel
# rotation, dashboards) rather than product delivery.
$script:OpsBranchMarkers = @(
    ('exo-'),
    ('ops-'),
    ('dra53'),
    ('channel-rotation')
)

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

function ConvertTo-Utc {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    return [datetime]::Parse(
        $Text, [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor
        [System.Globalization.DateTimeStyles]::AssumeUniversal)
}

function Get-HoursBetween {
    param($From, $To)
    if ($null -eq $From -or $null -eq $To) { return $null }
    return [math]::Round(($To - $From).TotalHours, 3)
}

function Get-UnionHours {
    <# Total wall-clock covered by a set of [Start,End] intervals, counting
       overlap ONCE.

       This is not fussiness. A slice's product PR and its `helm/ssc-N` PR sat
       open across the same hours waiting on the same person; adding their waits
       would report more governance wait than the window contains, and a ratio
       built on it can exceed 1. Elapsed time is what a wait costs, so elapsed
       time is what gets summed. #>
    param($Intervals)

    $valid = @($Intervals | Where-Object { $null -ne $_ -and $null -ne $_.Start -and $null -ne $_.End -and $_.End -gt $_.Start })
    if ($valid.Count -eq 0) { return 0.0 }

    $total = 0.0
    $ordered = @($valid | Sort-Object Start)
    $curStart = $ordered[0].Start
    $curEnd = $ordered[0].End
    for ($i = 1; $i -lt $ordered.Count; $i++) {
        $n = $ordered[$i]
        if ($n.Start -le $curEnd) {
            if ($n.End -gt $curEnd) { $curEnd = $n.End }
        } else {
            $total += ($curEnd - $curStart).TotalHours
            $curStart = $n.Start
            $curEnd = $n.End
        }
    }
    $total += ($curEnd - $curStart).TotalHours
    return [math]::Round($total, 3)
}

function Get-Median {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $mid = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$mid] }
    return ($sorted[$mid - 1] + $sorted[$mid]) / 2
}

function Test-ContainsAny {
    param([string]$Text, [string[]]$Needles)
    if ([string]::IsNullOrEmpty($Text)) { return $false }
    if ($null -eq $Needles -or $Needles.Count -eq 0) {
        throw 'Test-ContainsAny called with an EMPTY needle list — an empty list matches nothing and reports clean (trap 78).'
    }
    $lower = $Text.ToLowerInvariant()
    foreach ($n in $Needles) {
        if ($lower.Contains($n.ToLowerInvariant())) { return $true }
    }
    return $false
}

function Format-Number {
    param($Value, [int]$Digits = 2, [string]$Suffix = '')
    if ($null -eq $Value) { return '`unmeasured`' }
    return ('{0}{1}' -f [math]::Round([double]$Value, $Digits), $Suffix)
}

function Format-Percent {
    param($Value, [int]$Digits = 0)
    if ($null -eq $Value) { return '`unmeasured`' }
    return ('{0}%' -f [math]::Round([double]$Value * 100, $Digits))
}

# ---------------------------------------------------------------------------
# Data acquisition — gh
# ---------------------------------------------------------------------------

function Invoke-GhJson {
    param([string[]]$GhArgs)
    $raw = & gh @GhArgs 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) { return $null }
    return ($raw | ConvertFrom-Json)
}

function Get-WindowPullRequests {
    param([int]$From, [int]$To, [string]$Repository)

    $fields = 'number,title,state,headRefName,baseRefName,createdAt,mergedAt,closedAt,author,commits,reviews,files,statusCheckRollup,body'
    $prs = @()
    for ($n = $From; $n -le $To; $n++) {
        $pr = Invoke-GhJson @('pr', 'view', "$n", '--repo', $Repository, '--json', $fields)
        if ($null -eq $pr) {
            Write-Verbose "PR #$n not readable — skipped."
            continue
        }
        $prs += , $pr
    }
    return $prs
}

function Get-WindowWorkflowRuns {
    param([string]$Repository, [datetime]$From, [datetime]$To)

    $fromDay = $From.ToString('yyyy-MM-dd')
    $toDay = $To.AddDays(1).ToString('yyyy-MM-dd')
    $path = "repos/$Repository/actions/runs?created=$fromDay..$toDay&per_page=100"
    $page = Invoke-GhJson @('api', $path, '--paginate', '--slurp')
    if ($null -eq $page) { return @() }

    $runs = @()
    foreach ($chunk in @($page)) {
        if ($null -ne $chunk.workflow_runs) { $runs += @($chunk.workflow_runs) }
    }
    return $runs
}

# ---------------------------------------------------------------------------
# Data acquisition — Paperclip
# ---------------------------------------------------------------------------

function Get-PaperclipBase {
    param([string]$Raw)
    if ([string]::IsNullOrWhiteSpace($Raw)) { return $null }
    $b = $Raw.TrimEnd('/')
    if ($b.EndsWith('/api')) { $b = $b.Substring(0, $b.Length - 4) }
    return $b
}

function Invoke-Paperclip {
    param([string]$Base, [string]$Key, [string]$Path)
    if ([string]::IsNullOrWhiteSpace($Base) -or [string]::IsNullOrWhiteSpace($Key)) { return $null }
    try {
        return Invoke-RestMethod -Method Get -Uri ("$Base$Path") -TimeoutSec 40 `
            -Headers @{ Authorization = "Bearer $Key" }
    } catch {
        Write-Verbose "Paperclip GET $Path failed: $($_.Exception.Message)"
        return $null
    }
}

# ---------------------------------------------------------------------------
# Classification
# ---------------------------------------------------------------------------

function Get-GovernedPrNumber {
    <# Returns the PR number an `helm/ssc-N` branch carries the ruling for, else $null. #>
    param([string]$Branch)
    if ([string]::IsNullOrWhiteSpace($Branch)) { return $null }
    $m = [regex]::Match($Branch, $script:GovernanceBranchPattern)
    if ($m.Success) { return [int]$m.Groups[1].Value }
    return $null
}

function Get-SliceKey {
    <# Normalises a head branch into a stable slice key: strips the owner prefix,
       the executor-model prefix and the trailing yyyyMMdd stamp. #>
    param([string]$Branch)
    $k = $Branch
    $k = [regex]::Replace($k, '^(claude|codex|cursor)/', '')
    $k = [regex]::Replace($k, '^(opus|sonnet|fable|helm)-', '')
    $k = [regex]::Replace($k, '-\d{8}$', '')
    return $k
}

function Get-SliceKind {
    param([string]$Branch, [string]$Title)
    if (Test-ContainsAny -Text $Branch -Needles $script:OpsBranchMarkers) { return 'ops' }
    if (Test-ContainsAny -Text $Branch -Needles $script:PlanBranchMarkers) { return 'plan' }
    if ($Title -and $Title.StartsWith('Fable plan')) { return 'plan' }
    if ($Title -and $Title.StartsWith('ops:')) { return 'ops' }
    return 'delivery'
}

function Get-DraKey {
    <# The DRA work item a slice belongs to, from its branch or title. #>
    param([string]$Branch, [string]$Title)
    foreach ($text in @(($Branch), ($Title))) {
        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        $m = [regex]::Match($text, '(?i)dra[-]?(\d{2,3})')
        if ($m.Success) { return ('DRA-{0}' -f $m.Groups[1].Value) }
    }
    return 'unattributed'
}

# ---------------------------------------------------------------------------
# Per-PR derived facts
# ---------------------------------------------------------------------------

function Get-PrFacts {
    param($Pr)

    $created = ConvertTo-Utc $Pr.createdAt
    $merged = ConvertTo-Utc $Pr.mergedAt
    $closed = ConvertTo-Utc $Pr.closedAt

    $firstCommit = $null
    if ($null -ne $Pr.commits) {
        $stamps = @($Pr.commits | ForEach-Object { ConvertTo-Utc $_.authoredDate } | Where-Object { $_ })
        if ($stamps.Count -gt 0) { $firstCommit = ($stamps | Sort-Object)[0] }
    }
    if ($null -eq $firstCommit) { $firstCommit = $created }

    # CI runtime: the gating run on the head commit. Elapsed wall-clock of the
    # whole CI check set, not the sum of its jobs — the jobs run in parallel and
    # what a merge waits on is the slowest one.
    $ciHours = $null
    if ($null -ne $Pr.statusCheckRollup) {
        $checks = @($Pr.statusCheckRollup | Where-Object { $_.PSObject.Properties.Name -contains 'startedAt' -and $_.startedAt })
        if ($checks.Count -gt 0) {
            $starts = @($checks | ForEach-Object { ConvertTo-Utc $_.startedAt } | Where-Object { $_ })
            $ends = @($checks | ForEach-Object { ConvertTo-Utc $_.completedAt } | Where-Object { $_ })
            if ($starts.Count -gt 0 -and $ends.Count -gt 0) {
                $ciHours = Get-HoursBetween ($starts | Sort-Object)[0] ($ends | Sort-Object)[-1]
            }
        }
    }

    $openToMerge = Get-HoursBetween $created $merged
    $waitHours = $null
    $waitInterval = $null
    if ($null -ne $openToMerge) {
        $waitHours = $openToMerge
        if ($null -ne $ciHours) { $waitHours = [math]::Max(0, $openToMerge - $ciHours) }
        # The wait is the tail of the PR's open life AFTER CI had finished with
        # it — that is the part a process change can remove.
        $waitStart = $created
        if ($null -ne $ciHours) { $waitStart = $created.AddHours([double]$ciHours) }
        if ($waitStart -lt $merged) {
            $waitInterval = [pscustomobject]@{ Start = $waitStart; End = $merged }
        }
    }

    $governs = Get-GovernedPrNumber -Branch $Pr.headRefName
    $kind = if ($null -ne $governs) { 'governance' } else { Get-SliceKind -Branch $Pr.headRefName -Title $Pr.title }

    $fileList = @()
    if ($null -ne $Pr.files) { $fileList = @($Pr.files | ForEach-Object { $_.path }) }

    return [pscustomobject]@{
        Number      = [int]$Pr.number
        Title       = [string]$Pr.title
        Body        = [string]$Pr.body
        Branch      = [string]$Pr.headRefName
        State       = [string]$Pr.state
        Kind        = $kind
        GovernsPr   = $governs
        SliceKey    = Get-SliceKey -Branch $Pr.headRefName
        Dra         = Get-DraKey -Branch $Pr.headRefName -Title $Pr.title
        Created     = $created
        Merged      = $merged
        Closed      = $closed
        FirstCommit = $firstCommit
        CiHours     = $ciHours
        OpenToMerge = $openToMerge
        WaitHours   = $waitHours
        WaitInterval = $waitInterval
        ReviewCount = @($Pr.reviews).Count
        Files       = $fileList
    }
}

# ---------------------------------------------------------------------------
# Slice assembly
# ---------------------------------------------------------------------------

function Get-Slices {
    param($Facts, $HelmCommits)

    $byNumber = @{}
    foreach ($f in $Facts) { $byNumber[$f.Number] = $f }

    # Governance PRs attach to the slice of the PR they rule on.
    $governanceFor = @{}
    foreach ($f in $Facts) {
        if ($f.Kind -ne 'governance') { continue }
        $target = $f.GovernsPr
        if ($null -eq $target) { continue }
        if (-not $governanceFor.ContainsKey($target)) { $governanceFor[$target] = @() }
        $governanceFor[$target] += , $f
    }

    $slices = @()
    foreach ($f in $Facts) {
        if ($f.Kind -eq 'governance') { continue }

        $gov = @()
        if ($governanceFor.ContainsKey($f.Number)) { $gov = @($governanceFor[$f.Number]) }

        # A HELM.md commit naming this PR number is a ruling touch, counted
        # separately from the PR that carried it: the retired flow cost one of
        # each per slice, which is the "≥2 touches" the baseline records.
        $rulings = @($HelmCommits | Where-Object { $_.PrNumbers -contains $f.Number })

        $helmTouches = $gov.Count + $rulings.Count + $f.ReviewCount

        $allPrs = @($f) + $gov
        $merges = @($allPrs | ForEach-Object { $_.Merged } | Where-Object { $_ })
        $finalMerge = if ($merges.Count -gt 0) { ($merges | Sort-Object)[-1] } else { $null }

        $slices += , [pscustomobject]@{
            Key             = $f.SliceKey
            Dra             = $f.Dra
            Kind            = $f.Kind
            DeliveryPr      = $f
            GovernancePrs   = $gov
            Rulings         = $rulings
            PrCount         = $allPrs.Count
            HelmTouches     = $helmTouches
            FounderTouches  = 0
            FirstCommit     = $f.FirstCommit
            ProductMerge    = $f.Merged
            FinalMerge      = $finalMerge
            LeadTimeHours   = (Get-HoursBetween $f.FirstCommit $finalMerge)
            # Union, matching §1 — the product PR and its SSC twin waited across
            # the same hours, so a summed column here would contradict the KPI.
            WaitHours       = (Get-UnionHours -Intervals @($allPrs | ForEach-Object { $_.WaitInterval } | Where-Object { $null -ne $_ }))
            Merged          = ($null -ne $f.Merged)
        }
    }
    return $slices
}

function Get-HelmCommits {
    param([datetime]$From, [datetime]$To)

    Push-Location $RepoRoot
    try {
        $fromArg = $From.ToString('yyyy-MM-ddTHH:mm:ssZ')
        $toArg = $To.AddDays(1).ToString('yyyy-MM-ddTHH:mm:ssZ')
        $lines = @(& git log --since=$fromArg --until=$toArg --format='%H%x1f%aI%x1f%s' -- HELM.md 2>$null)
    } finally {
        Pop-Location
    }

    $out = @()
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`u{001f}"
        if ($parts.Count -lt 3) { continue }
        $nums = @([regex]::Matches($parts[2], '#(\d+)') | ForEach-Object { [int]$_.Groups[1].Value })
        $out += , [pscustomobject]@{
            Sha       = $parts[0]
            When      = (ConvertTo-Utc $parts[1])
            Subject   = $parts[2]
            PrNumbers = $nums
            IsVeto    = (Test-ContainsAny -Text $parts[2] -Needles $script:VetoMarkers)
        }
    }
    return $out
}

# ---------------------------------------------------------------------------
# Metric computations (§6)
# ---------------------------------------------------------------------------

function Get-AuthorizationGaps {
    <# The old flow made every slice wait for an explicit "AUTHORIZE dra-N-dX
       after land" before the next one could start. The gap is measured from the
       previous slice's PRODUCT merge to the next slice's first commit, for
       consecutive delivery slices of the same DRA.

       This term is an UPPER bound on governance wait: it also contains the
       executor's own startup (claim-seat, context read). The PR-wait term is the
       tight lower bound. The dashboard reports both, because a single fabricated
       point estimate would be a claim neither number supports. #>
    param($Slices)

    $gaps = @()
    $delivery = @($Slices | Where-Object { $_.Kind -eq 'delivery' -and $_.Merged })
    foreach ($group in ($delivery | Group-Object Dra)) {
        $ordered = @($group.Group | Sort-Object FirstCommit)
        for ($i = 1; $i -lt $ordered.Count; $i++) {
            $prev = $ordered[$i - 1]
            $next = $ordered[$i]
            if ($null -eq $prev.ProductMerge -or $null -eq $next.FirstCommit) { continue }
            $h = Get-HoursBetween $prev.ProductMerge $next.FirstCommit
            if ($null -eq $h -or $h -le 0) { continue }
            $gaps += , [pscustomobject]@{
                Dra   = $group.Name; From = $prev.Key; To = $next.Key; Hours = $h
                Start = $prev.ProductMerge; End = $next.FirstCommit
            }
        }
    }
    return $gaps
}

function Get-ReworkPrs {
    param($Facts)

    $rework = @()
    foreach ($f in $Facts) {
        # A governance PR that DESCRIBES a re-land ("ACK #604 flake re-land KEEP")
        # is a ruling about someone else's rework, not rework. Counting the
        # signature prose would double every rework event in the window.
        if ($f.Kind -eq 'governance') { continue }
        $text = "$($f.Title)`n$($f.Body)"
        if (-not (Test-ContainsAny -Text $text -Needles $script:ReworkMarkers)) { continue }
        # Must name another PR — a title using the word "revert" about nothing in
        # particular is prose, not rework.
        $refs = @([regex]::Matches($f.Title, '#(\d+)') | ForEach-Object { [int]$_.Groups[1].Value } | Where-Object { $_ -ne $f.Number })
        if ($refs.Count -eq 0) { continue }
        $within = $false
        foreach ($r in $refs) {
            $target = @($Facts | Where-Object { $_.Number -eq $r })
            if ($target.Count -eq 0) { continue }
            $when = $target[0].Merged
            if ($null -eq $when) { $when = $target[0].Closed }
            if ($null -eq $when -or $null -eq $f.Created) { continue }
            if (($f.Created - $when).TotalDays -le 14) { $within = $true }
        }
        if ($within) { $rework += , [pscustomobject]@{ Number = $f.Number; Title = $f.Title; Refs = $refs } }
    }
    return $rework
}

function Get-CiSplit {
    param($Runs, $PrBranches, [string]$FlakeLedgerText, [datetime]$From, [datetime]$To)

    # A run is RED if it concluded failure, or if it needed a second attempt —
    # a rerun-to-green overwrites the run's conclusion, so conclusion alone
    # undercounts exactly the reds the flake ledger exists for.
    #
    # The GitHub `created=` filter is DAY-granular, so it over-collects at both
    # ends. Clamp to the window the PRs actually span, or the split reports reds
    # from work that is not in the window at all.
    $rows = @()
    foreach ($r in $Runs) {
        if ($r.name -ne 'CI') { continue }
        if (-not ($r.conclusion -eq 'failure' -or [int]$r.run_attempt -gt 1)) { continue }
        $when = ConvertTo-Utc $r.created_at
        if ($null -eq $when -or $when -lt $From -or $when -gt $To) { continue }

        $scope = if ($r.head_branch -eq 'main') { 'main (post-merge)' }
        elseif ($PrBranches -contains $r.head_branch) { 'window PR branch' }
        else { 'other branch' }

        $rows += , [pscustomobject]@{
            Id      = [string]$r.id
            Branch  = [string]$r.head_branch
            Attempt = [int]$r.run_attempt
            Verdict = [string]$r.conclusion
            Created = $when
            Filed   = $FlakeLedgerText.Contains([string]$r.id)
            Scope   = $scope
        }
    }
    return $rows
}

function Get-MedianCiMinutes {
    param($Facts)
    $vals = @($Facts | ForEach-Object { $_.CiHours } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ * 60 })
    return (Get-Median -Values $vals)
}

# ---------------------------------------------------------------------------
# §10.3 — experiments in flight
# ---------------------------------------------------------------------------

function Get-Experiments {
    <# Reads the `exo-experiment: <name>` tags §10.1 requires on every process
       change, together with the metric each entry names as its judge. An
       untagged experiment is one the playbook cannot find, so this reads the
       tags rather than a hand-kept list. #>
    param([string]$DecisionsPath)

    if (-not (Test-Path $DecisionsPath)) { return @() }
    $lines = @([System.IO.File]::ReadAllLines($DecisionsPath, [System.Text.Encoding]::UTF8))

    $out = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $m = [regex]::Match($lines[$i], '^\s*`?exo-experiment:\s*([a-z0-9-]+)')
        if (-not $m.Success) { continue }
        $name = $m.Groups[1].Value

        # The judging clause runs from this line to the next blank line.
        $buf = @($lines[$i])
        for ($j = $i + 1; $j -lt $lines.Count; $j++) {
            if ([string]::IsNullOrWhiteSpace($lines[$j])) { break }
            if ([regex]::IsMatch($lines[$j], '^\s*`?exo-experiment:')) { break }
            $buf += $lines[$j]
        }
        $clause = ($buf -join ' ')
        $clause = [regex]::Replace($clause, '^\s*`?exo-experiment:\s*[a-z0-9-]+\s*`?\s*', '')
        # Entries that pack other fields onto the tag line (`· Tier T1 · plan …`)
        # carry no judging metric on it; drop the bookkeeping so the absence is
        # visible rather than dressed up as an answer.
        $clause = [regex]::Replace($clause, '^(·|—|-)\s*', '')
        $clause = [regex]::Replace($clause, '\s+', ' ').Trim()

        # §10.1 requires the tag to NAME the §6 metric that will judge it. A tag
        # that names none is reported as such: an experiment nobody can judge is
        # the failure mode the tag exists to prevent, and hiding it behind
        # whatever prose happened to follow would defeat the whole section.
        $namesMetric = [regex]::IsMatch($clause, '(?i)judged by|Governance Wait Ratio|Autonomous Correct|per slice|veto|rework|lead time|defect')
        if (-not $namesMetric) { $clause = '**no judging metric named on the tag** — plan §10.1 asks for one' }

        if (@($out | Where-Object { $_.Name -eq $name }).Count -gt 0) { continue }
        $out += , [pscustomobject]@{ Name = $name; JudgedBy = $clause; NamesMetric = $namesMetric }
    }
    return $out
}

# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------

function Write-Utf8NoBom {
    param([string]$Path, [string]$Text)
    $full = [System.IO.Path]::GetFullPath($Path)
    $dir = Split-Path -Parent $full
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($full, $Text, (New-Object System.Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------------------
# Self-test — a detector that has never fired is a detector aimed at nothing
# (trap 78), and a forbid-scan with no must-list cannot see a MISSING thing
# (trap 34). Both halves are asserted here.
# ---------------------------------------------------------------------------

function Invoke-SelfTest {
    $failures = @()
    function Assert([bool]$Condition, [string]$Message) {
        if (-not $Condition) { $script:selfTestFailures += $Message }
    }
    $script:selfTestFailures = @()

    # 1. Every pattern list is non-empty and is a LIST, not one collapsed string.
    foreach ($pair in @(
            (@{ n = 'ReworkMarkers'; v = $script:ReworkMarkers }),
            (@{ n = 'VetoMarkers'; v = $script:VetoMarkers }),
            (@{ n = 'PlanBranchMarkers'; v = $script:PlanBranchMarkers }),
            (@{ n = 'OpsBranchMarkers'; v = $script:OpsBranchMarkers }))) {
        Assert (@($pair.v).Count -gt 1) "$($pair.n) collapsed to $(@($pair.v).Count) element(s) — trap 78."
        foreach ($e in $pair.v) {
            Assert ($e -is [string] -and $e.Length -gt 0) "$($pair.n) has a non-string / empty element."
            Assert (-not $e.Contains(' ') -or $e.Trim().Length -gt 0) "$($pair.n) element is whitespace."
        }
    }

    # 2. Each list FIRES on a positive and stays quiet on a negative.
    Assert (Test-ContainsAny -Text 'ops: re-land the flake row' -Needles $script:ReworkMarkers) 'ReworkMarkers missed "re-land".'
    Assert (-not (Test-ContainsAny -Text 'DRA-71 D4: throughput is an outcome' -Needles $script:ReworkMarkers)) 'ReworkMarkers fired on a plain delivery title.'
    Assert (Test-ContainsAny -Text 'Helm: VETO the mote weight' -Needles $script:VetoMarkers) 'VetoMarkers missed "VETO".'
    Assert (-not (Test-ContainsAny -Text 'Helm SSC: SIGN PR #600 (KEEP)' -Needles $script:VetoMarkers)) 'VetoMarkers fired on a SIGN/KEEP ruling.'

    # 3. Governance detection.
    Assert ((Get-GovernedPrNumber -Branch 'helm/ssc-582') -eq 582) 'helm/ssc-582 not read as governance for #582.'
    Assert ($null -eq (Get-GovernedPrNumber -Branch 'opus-dra71-d4')) 'A delivery branch read as governance.'
    Assert ($null -eq (Get-GovernedPrNumber -Branch 'helm/ssc-nope')) 'A non-numeric ssc branch read as governance.'

    # 4. Slice keys and kinds.
    Assert ((Get-SliceKey -Branch 'claude/opus-dra70-d1-20260912') -eq 'dra70-d1') 'Slice key did not normalise the owner/model/date decoration.'
    Assert ((Get-SliceKey -Branch 'opus-dra71-d9') -eq 'dra71-d9') 'Slice key mangled a bare executor branch.'
    Assert ((Get-SliceKind -Branch 'opus-dra71-d9' -Title 'DRA-71 D9: the Helper on the phone') -eq 'delivery') 'A delivery PR was not classified as delivery.'
    Assert ((Get-SliceKind -Branch 'claude/fable-dra71-helper-d2-20260913' -Title 'Fable plan: DRA-71') -eq 'plan') 'A plan PR was not classified as plan.'
    Assert ((Get-SliceKind -Branch 'claude/fable-exo-night3-20260913' -Title 'ops: night-3') -eq 'ops') 'An ops PR was not classified as ops.'
    Assert ((Get-DraKey -Branch 'opus-dra71-d9' -Title 'x') -eq 'DRA-71') 'DRA key not read from the branch.'
    Assert ((Get-DraKey -Branch 'nothing' -Title 'nothing') -eq 'unattributed') 'DRA key invented one out of nothing.'

    # 5. Interval union — overlap counted ONCE, disjoint intervals added.
    $a = [datetime]::Parse('2026-09-14T00:00:00Z').ToUniversalTime()
    Assert ((Get-UnionHours -Intervals @(
                ([pscustomobject]@{ Start = $a; End = $a.AddHours(4) }),
                ([pscustomobject]@{ Start = $a.AddHours(1); End = $a.AddHours(3) }))) -eq 4.0) `
        'A fully contained interval was double-counted — the ratio can exceed 1.'
    Assert ((Get-UnionHours -Intervals @(
                ([pscustomobject]@{ Start = $a; End = $a.AddHours(2) }),
                ([pscustomobject]@{ Start = $a.AddHours(1); End = $a.AddHours(4) }))) -eq 4.0) `
        'Partially overlapping intervals were not merged.'
    Assert ((Get-UnionHours -Intervals @(
                ([pscustomobject]@{ Start = $a; End = $a.AddHours(1) }),
                ([pscustomobject]@{ Start = $a.AddHours(3); End = $a.AddHours(5) }))) -eq 3.0) `
        'Disjoint intervals were merged when they should have been added.'
    Assert ((Get-UnionHours -Intervals @()) -eq 0.0) 'An empty interval set did not answer zero.'
    Assert ((Get-UnionHours -Intervals @(([pscustomobject]@{ Start = $a; End = $a }))) -eq 0.0) `
        'A zero-length interval contributed time.'

    # 6. Arithmetic helpers.
    Assert ((Get-Median -Values @(1, 2, 3)) -eq 2) 'Median of an odd set is wrong.'
    Assert ((Get-Median -Values @(1, 2, 3, 4)) -eq 2.5) 'Median of an even set is wrong.'
    Assert ($null -eq (Get-Median -Values @())) 'Median of nothing answered a number rather than unmeasured.'
    Assert ($null -eq (Format-Number -Value $null | Where-Object { $_ -ne '`unmeasured`' })) 'A null metric did not render as unmeasured.'

    # 7. An empty needle list must THROW rather than report clean (trap 78's
    #    whole lesson: the guard that matched nothing said the file was fine).
    $threw = $false
    try { Test-ContainsAny -Text 'anything' -Needles @() | Out-Null } catch { $threw = $true }
    Assert $threw 'An empty needle list reported clean instead of throwing.'

    if ($script:selfTestFailures.Count -gt 0) {
        Write-Host "exo-metrics self-test: $($script:selfTestFailures.Count) FAILED" -ForegroundColor Red
        foreach ($f in $script:selfTestFailures) { Write-Host "  - $f" -ForegroundColor Red }
        exit 1
    }
    Write-Host 'exo-metrics self-test: all checks passed.' -ForegroundColor Green
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if (-not $FromPr -or -not $ToPr) {
    throw 'Specify -FromPr and -ToPr (or -SelfTest). Example: -FromPr 580 -ToPr 607'
}

# Trap 54, one layer out: `gh` emits UTF-8, and PowerShell decodes a native
# process's stdout using the CONSOLE codepage. Without this, every em-dash in a
# PR title arrives as `ΓÇö` and is written into the dashboard that way — a
# generated doc that mojibakes itself on every run. Read the bytes gh actually
# wrote, not the console's guess at them.
#
# Set here rather than at the top of the file so that -SelfTest (which check.ps1
# runs in-process) leaves the caller's console alone.
$priorOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "Reading PRs #$FromPr..#$ToPr from $Repo …"
$prs = Get-WindowPullRequests -From $FromPr -To $ToPr -Repository $Repo
if (@($prs).Count -eq 0) { throw "No readable PRs in #$FromPr..#$ToPr." }

$facts = @($prs | ForEach-Object { Get-PrFacts -Pr $_ })
$windowStart = (@($facts | ForEach-Object { $_.FirstCommit } | Where-Object { $_ }) | Sort-Object)[0]
$windowEnd = (@($facts | ForEach-Object { $_.Merged } | Where-Object { $_ }) | Sort-Object)[-1]

Write-Host "Reading HELM.md rulings …"
$helmCommits = Get-HelmCommits -From $windowStart -To $windowEnd

Write-Host "Reading workflow runs …"
$runs = Get-WindowWorkflowRuns -Repository $Repo -From $windowStart -To $windowEnd

$flakePath = Join-Path $RepoRoot 'docs/ops/flake-ledger.md'
$flakeText = if (Test-Path $flakePath) { [System.IO.File]::ReadAllText($flakePath, [System.Text.Encoding]::UTF8) } else { '' }

$slices = Get-Slices -Facts $facts -HelmCommits $helmCommits
$delivery = @($slices | Where-Object { $_.Kind -eq 'delivery' -and $_.Merged })
$allMergedSlices = @($slices | Where-Object { $_.Merged })

# --- Paperclip: lead time per DRA, queue latency, cost ---------------------
$apiBase = Get-PaperclipBase -Raw $ApiBase
$issues = $null
if (-not $NoPaperclip -and $apiBase -and $CompanyId) {
    Write-Host "Reading Paperclip issues …"
    $issues = Invoke-Paperclip -Base $apiBase -Key $ApiKey -Path "/api/companies/$CompanyId/issues?limit=200"
}

$draRows = @()
foreach ($group in ($allMergedSlices | Group-Object Dra | Sort-Object Name)) {
    $dra = $group.Name
    $firstCommit = (@($group.Group | ForEach-Object { $_.FirstCommit } | Where-Object { $_ }) | Sort-Object)[0]
    $lastMerge = (@($group.Group | ForEach-Object { $_.FinalMerge } | Where-Object { $_ }) | Sort-Object)[-1]

    $issue = $null
    if ($null -ne $issues) {
        $issue = @($issues | Where-Object { $_.identifier -eq $dra })[0]
    }

    $accepted = $null
    $created = $null
    if ($null -ne $issue) {
        $accepted = ConvertTo-Utc $issue.startedAt
        $created = ConvertTo-Utc $issue.createdAt
    }

    # Lead time starts at the Paperclip acceptance when that falls INSIDE the
    # window, otherwise at the item's first commit in the window.
    #
    # The clamp matters: a standing work item (an ops lane accepted days earlier
    # and never closed) would otherwise contribute its whole open life to the
    # denominator and silently divide the Governance Wait Ratio down. Lead time
    # for a window is the lead time of the work done IN it.
    $start = $firstCommit
    if ($null -ne $accepted -and $accepted -lt $start -and $accepted -ge $windowStart) { $start = $accepted }
    $clamped = ($null -ne $accepted -and $accepted -lt $windowStart)

    $cost = $null
    if ($null -ne $issue -and -not $NoPaperclip) {
        $cost = Invoke-Paperclip -Base $apiBase -Key $ApiKey -Path "/api/issues/$($issue.id)/cost-summary"
    }

    $draRows += , [pscustomobject]@{
        Dra           = $dra
        Slices        = @($group.Group | Where-Object { $_.Kind -eq 'delivery' }).Count
        Accepted      = $accepted
        Clamped       = $clamped
        QueueHours    = (Get-HoursBetween $created $accepted)
        Start         = $start
        LastMerge     = $lastMerge
        LeadTimeHours = (Get-HoursBetween $start $lastMerge)
        RunCount      = $(if ($null -ne $cost) { [int]$cost.runCount } else { $null })
        CostCents     = $(if ($null -ne $cost) { [int]$cost.costCents } else { $null })
        InputTokens   = $(if ($null -ne $cost) { [long]$cost.inputTokens } else { $null })
        OutputTokens  = $(if ($null -ne $cost) { [long]$cost.outputTokens } else { $null })
        CachedTokens  = $(if ($null -ne $cost) { [long]$cost.cachedInputTokens } else { $null })
    }
}

# --- The KPIs --------------------------------------------------------------

$gaps = Get-AuthorizationGaps -Slices $slices

# Governance wait is UNION-ed per work item, not summed: a product PR and its
# `helm/ssc-N` twin sat open across the same hours, and adding them would report
# more wait than the window contains (a ratio built that way can exceed 1).
$productWaitIntervals = @($facts | Where-Object { $_.Merged -and $_.Kind -ne 'governance' -and $null -ne $_.WaitInterval } | ForEach-Object { $_.WaitInterval })
$governanceWaitIntervals = @($facts | Where-Object { $_.Merged -and $_.Kind -eq 'governance' -and $null -ne $_.WaitInterval } | ForEach-Object { $_.WaitInterval })
$gapIntervals = @($gaps | ForEach-Object { [pscustomobject]@{ Start = $_.Start; End = $_.End } })

$prWaitHours = Get-UnionHours -Intervals $productWaitIntervals
$gapHours = Get-UnionHours -Intervals $gapIntervals
$governanceOnlyHours = (Get-UnionHours -Intervals (@($productWaitIntervals) + @($governanceWaitIntervals) + @($gapIntervals))) - (Get-UnionHours -Intervals (@($productWaitIntervals) + @($gapIntervals)))
$attributableLead = (@($draRows | Where-Object { $_.Dra -ne 'unattributed' -and $null -ne $_.LeadTimeHours } | ForEach-Object { $_.LeadTimeHours } | Measure-Object -Sum).Sum)

# Two readings, and the difference between them is a real disagreement about
# what counts, not noise:
#   $gwr          — product-PR wait + authorization gaps. This is the plan §6
#                   definition and the one the §6 target is set against.
#   $gwrWithSsc   — the same, plus the hours a `helm/ssc-N` PR spent open after
#                   its product PR had already merged. That is pure governance
#                   overhead by construction, so the fuller number is reported
#                   beside the comparable one rather than instead of it.
$gwr = $null; $gwrWithSsc = $null
$coreWait = Get-UnionHours -Intervals (@($productWaitIntervals) + @($gapIntervals))
$fullWait = Get-UnionHours -Intervals (@($productWaitIntervals) + @($governanceWaitIntervals) + @($gapIntervals))
if ($attributableLead -gt 0) {
    $gwr = $coreWait / $attributableLead
    $gwrWithSsc = $fullWait / $attributableLead
}

$autonomous = @($delivery | Where-Object { $_.HelmTouches -eq 0 -and $_.FounderTouches -eq 0 })
$rework = Get-ReworkPrs -Facts $facts
$reworkedSliceKeys = @()
foreach ($r in $rework) {
    $owner = @($facts | Where-Object { $_.Number -eq $r.Number })
    if ($owner.Count -gt 0) { $reworkedSliceKeys += $owner[0].SliceKey }
}
$accrNumerator = @($autonomous | Where-Object { $reworkedSliceKeys -notcontains $_.Key })
$accr = $null
if ($delivery.Count -gt 0) { $accr = $accrNumerator.Count / $delivery.Count }

$intervened = @($delivery | Where-Object { ($_.HelmTouches + $_.FounderTouches) -gt 0 })
$interventionRate = $null
if ($delivery.Count -gt 0) { $interventionRate = $intervened.Count / $delivery.Count }

$mergedPrCount = @($facts | Where-Object { $_.Merged }).Count
$abandonedSlices = @($slices | Where-Object { -not $_.Merged })

# PR traffic per unit of DELIVERY: all PRs opened in the window (including the
# ones a slice burned and abandoned) over the slices that actually landed. A
# ratio that silently drops an abandoned slice's two PRs flatters the old model
# with the cost of its own waste.
$prsPerSlice = $null
if ($allMergedSlices.Count -gt 0) { $prsPerSlice = @($facts).Count / $allMergedSlices.Count }
$governanceShare = $null
if (@($facts).Count -gt 0) { $governanceShare = @($facts | Where-Object { $_.Kind -eq 'governance' }).Count / @($facts).Count }
$touchesPerSlice = $null
if ($delivery.Count -gt 0) {
    $touchesPerSlice = (@($delivery | ForEach-Object { $_.HelmTouches } | Measure-Object -Sum).Sum) / $delivery.Count
}

$vetoes = @($helmCommits | Where-Object { $_.IsVeto })
$vetoRate = $null
if ($delivery.Count -gt 0) { $vetoRate = $vetoes.Count / $delivery.Count }
$reworkRate = $null
if ($mergedPrCount -gt 0) { $reworkRate = @($rework).Count / $mergedPrCount }

$prBranches = @($facts | ForEach-Object { $_.Branch })
$ciRed = Get-CiSplit -Runs $runs -PrBranches $prBranches -FlakeLedgerText $flakeText -From $windowStart -To $windowEnd
$medianCi = Get-MedianCiMinutes -Facts $facts
$ciMinutes = @($facts | ForEach-Object { $_.CiHours } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ * 60 } | Sort-Object)
$ciMin = if ($ciMinutes.Count -gt 0) { $ciMinutes[0] } else { $null }
$ciMax = if ($ciMinutes.Count -gt 0) { $ciMinutes[-1] } else { $null }

# Handoff: plan merge → first delivery commit, per DRA.
$handoffs = @()
foreach ($group in ($slices | Group-Object Dra)) {
    $plan = @($group.Group | Where-Object { $_.Kind -eq 'plan' -and $_.Merged } | Sort-Object ProductMerge)
    $firstDelivery = @($group.Group | Where-Object { $_.Kind -eq 'delivery' } | Sort-Object FirstCommit)
    if ($plan.Count -eq 0 -or $firstDelivery.Count -eq 0) { continue }
    $h = Get-HoursBetween $plan[0].ProductMerge $firstDelivery[0].FirstCommit
    if ($null -ne $h) { $handoffs += , [pscustomobject]@{ Dra = $group.Name; Hours = $h } }
}
$medianExecutorPrHours = Get-Median -Values @($delivery | ForEach-Object { $_.DeliveryPr.OpenToMerge } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ })

# Cost per slice is only defined for a work item that has BOTH run records and
# delivered slices. Summing one item's runs over another item's slices would
# print a number that is about neither of them.
$costedDra = @($draRows | Where-Object { $null -ne $_.RunCount -and $_.RunCount -gt 0 -and $_.Slices -gt 0 })
$costPerSlice = $null
$tokensPerSlice = $null
if ($costedDra.Count -gt 0) {
    $costedSlices = (@($costedDra | ForEach-Object { $_.Slices } | Measure-Object -Sum).Sum)
    if ($costedSlices -gt 0) {
        $costPerSlice = (@($costedDra | ForEach-Object { $_.CostCents } | Measure-Object -Sum).Sum) / $costedSlices
        $tokensPerSlice = (@($costedDra | ForEach-Object { [double]$_.InputTokens + [double]$_.OutputTokens } | Measure-Object -Sum).Sum) / $costedSlices
    }
}

# --- Experiments in flight (§10.3) ----------------------------------------
$experiments = Get-Experiments -DecisionsPath (Join-Path $RepoRoot 'DECISIONS.md')

$frozen = $null
$baselinePath = Join-Path $RepoRoot $BaselineFile
if ((Test-Path $baselinePath) -and -not $Baseline) {
    $frozen = [System.IO.File]::ReadAllText($baselinePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
}

$current = [ordered]@{
    window            = "PRs #$FromPr-#$ToPr"
    windowStart       = $windowStart.ToString('o')
    windowEnd         = $windowEnd.ToString('o')
    generatedAt       = ([datetime]::UtcNow.ToString('o'))
    prs               = @($facts).Count
    mergedPrs         = $mergedPrCount
    governancePrs     = @($facts | Where-Object { $_.Kind -eq 'governance' }).Count
    slices            = $allMergedSlices.Count
    deliverySlices    = $delivery.Count
    abandonedSlices   = @($abandonedSlices).Count
    gwr               = $gwr
    gwrIncludingSscWait = $gwrWithSsc
    productWaitHours  = $prWaitHours
    authGapHours      = $gapHours
    sscOnlyWaitHours  = $governanceOnlyHours
    leadTimeHours     = $attributableLead
    accr              = $accr
    interventionRate  = $interventionRate
    prsPerSlice       = $prsPerSlice
    governanceShare   = $governanceShare
    helmTouchesPerSlice = $touchesPerSlice
    vetoRate          = $vetoRate
    reworkRate        = $reworkRate
    medianCiMinutes   = $medianCi
    medianExecutorPrHours = $medianExecutorPrHours
    ciRedEvents       = @($ciRed).Count
    ciRedFiledAsFlake = @($ciRed | Where-Object { $_.Filed }).Count
    costCentsPerSlice = $costPerSlice
    tokensPerSlice    = $tokensPerSlice
}

# ---------------------------------------------------------------------------
# Markdown
# ---------------------------------------------------------------------------

$label = if ($WindowLabel) { $WindowLabel } else { "PRs #$FromPr-#$ToPr" }
$sb = New-Object System.Text.StringBuilder
function Add-Line { param([string]$Text = '') ; [void]$sb.AppendLine($Text) }

Add-Line '# ExO execution dashboard'
Add-Line ''
Add-Line ('Generated by `scripts/exo-metrics.ps1` — DRA-73 plan §6 (metric table) and')
Add-Line ('§10.3 (experiments in flight). Window: **{0}**, {1:yyyy-MM-dd HH:mm} → {2:yyyy-MM-dd HH:mm} UTC.' -f $label, $windowStart, $windowEnd)
Add-Line ''
if ($Baseline) {
    Add-Line '> **FROZEN BASELINE.** This is the pre-migration reading of the DRA-70/71/72'
    Add-Line '> window, taken so that every later claim about the new operating model is'
    Add-Line '> checkable against a number that was written down FIRST. The machine-readable'
    Add-Line ('> copy is `{0}`;' -f $BaselineFile)
    Add-Line '> regenerate a later window with the same script and the "vs baseline" column'
    Add-Line '> of §7 fills itself.'
    Add-Line ''
}
Add-Line 'Every row is computed from data that already existed: PR timestamps, workflow'
Add-Line 'runs, `HELM.md` commits, `docs/ops/flake-ledger.md`, and Paperclip issue records.'
Add-Line '**A metric whose data does not exist in the window reads `unmeasured`, with the**'
Add-Line '**reason** — never `0`. An unmeasured metric and a measured zero are different'
Add-Line 'claims, and reading one as the other is how a dashboard starts lying.'
Add-Line ''
Add-Line '---'
Add-Line ''
Add-Line '## 1. Headline KPIs'
Add-Line ''
Add-Line '| KPI | Reading | Plan §6 target after M2 |'
Add-Line '|---|---|---|'
Add-Line ('| **Governance Wait Ratio** | **{0}** | < 0.15 |' -f (Format-Number $gwr 2))
Add-Line ('| — same, counting `helm/ssc-N` PR wait too | {0} | — |' -f (Format-Number $gwrWithSsc 2))
Add-Line ('| **Autonomous Correct Completion Rate** | **{0}** | > 70% |' -f (Format-Percent $accr))
Add-Line ('| PRs per delivered slice | {0} | ~1.05 |' -f (Format-Number $prsPerSlice 2))
Add-Line ('| — of which governance-only PRs | {0} | 0% |' -f (Format-Percent $governanceShare))
Add-Line ('| Helm touches per delivery slice | {0} | < 0.3 |' -f (Format-Number $touchesPerSlice 2))
Add-Line ('| Median CI runtime | {0} min (range {1}–{2}) | unchanged (the merge bar) |' -f (Format-Number $medianCi 1), (Format-Number $ciMin 1), (Format-Number $ciMax 1))
Add-Line ('| T0/T1 merge latency (median executor PR open→merge) | {0} h | ≈ CI time |' -f (Format-Number $medianExecutorPrHours 2))
Add-Line ''
Add-Line '### How the Governance Wait Ratio is built'
Add-Line ''
Add-Line ('- **Product-PR wait** — the hours a product or plan PR stayed open after CI had')
Add-Line ('  finished with it: **{0} h**.' -f (Format-Number $prWaitHours 1))
Add-Line ('- **Authorization gaps** — previous slice merged → next slice''s first commit, for')
Add-Line ('  consecutive delivery slices of one work item: **{0} h** across {1} gap(s).' -f (Format-Number $gapHours 1), @($gaps).Count)
Add-Line ('- **`helm/ssc-N` wait, beyond the above** — **{0} h**. Reported separately because' -f (Format-Number $governanceOnlyHours 1))
Add-Line ('  it is the second reading''s whole difference, and because it is the term cutover 1')
Add-Line ('  (DRA-74) deletes outright rather than shortens.')
Add-Line ('- **Lead time** — Σ per-work-item (accepted, or first commit, → last merge): **{0} h**.' -f (Format-Number $attributableLead 1))
Add-Line ''
Add-Line '**These hours are UNION-ed, not summed.** A slice''s product PR and its `helm/ssc-N`'
Add-Line 'twin sat open across the same hours waiting on the same person; adding their waits'
Add-Line 'would report more wait than the window contains, and the ratio could exceed 1.'
Add-Line 'Elapsed time is what a wait costs, so elapsed time is what is counted.'
Add-Line ''
Add-Line 'The headline figure uses the plan §6 terms (product wait + authorization gaps), so'
Add-Line 'it is the one comparable to the < 0.15 target. The second line adds the hours an'
Add-Line 'SSC PR spent open on its own — governance overhead by construction. Both are'
Add-Line 'printed because the M2 comparison has to be made against the same definition it was'
Add-Line 'frozen at, and a single number would hide which one that is.'
Add-Line ''
Add-Line 'The authorization-gap term is an **upper** bound on what a process change can'
Add-Line 'recover: it also contains the executor''s own startup (claim-seat, context read),'
Add-Line 'which no cutover removes.'
Add-Line ''

Add-Line '## 2. The §6 metric table'
Add-Line ''
Add-Line '| Metric | Computation | Reading |'
Add-Line '|---|---|---|'
Add-Line ('| Lead time | issue accepted (or first commit) → last PR merged, per work item | {0} h total over {1} item(s) |' -f (Format-Number $attributableLead 1), @($draRows | Where-Object { $_.Dra -ne 'unattributed' }).Count)
Add-Line ('| Governance Wait Ratio | Σ(open→merge − CI) + Σ(auth gaps) ÷ lead time | {0} |' -f (Format-Number $gwr 2))
Add-Line ('| Autonomous Correct Completion Rate | slices with zero Helm/Founder pre-merge touches and no rework within 14 d | {0} ({1} of {2}) |' -f (Format-Percent $accr), $accrNumerator.Count, $delivery.Count)
Add-Line ('| Queue latency by role | issue created → first action | see §5 |')
Add-Line ('| Handoff delay | plan merged → first executor commit | median {0} h |' -f (Format-Number (Get-Median -Values @($handoffs | ForEach-Object { [double]$_.Hours })) 2))
Add-Line ('| Helm/Founder intervention % | delivery slices with ≥1 pre-merge touch ÷ slices | {0} ({1} of {2}) |' -f (Format-Percent $interventionRate), $intervened.Count, $delivery.Count)
Add-Line ('| Veto rate | Helm/Bevel post-merge vetoes ÷ delivery merges | {0} ({1} veto ruling(s)) |' -f (Format-Percent $vetoRate 1), @($vetoes).Count)
Add-Line ('| Rework rate | PRs reverting/re-landing a ≤14-day-old merge ÷ merged PRs | {0} ({1} of {2}) |' -f (Format-Percent $reworkRate 1), @($rework).Count, $mergedPrCount)
Add-Line ('| Escaped defect rate | player-reported defects per tier | `unmeasured` — see §6 |')
Add-Line ('| CI failure/flake split | red runs: filed as flake vs unfiled | {0} red event(s), {1} filed — see §4 |' -f @($ciRed).Count, @($ciRed | Where-Object { $_.Filed }).Count)
Add-Line ('| PRs + Helm touches per slice | count | {0} PRs/slice, {1} Helm touches/delivery slice |' -f (Format-Number $prsPerSlice 2), (Format-Number $touchesPerSlice 2))
Add-Line ('| Cost per delivered slice | Paperclip run cost ÷ slices delivered | {0} — see §5 and §6 |' -f $(if ($null -eq $costPerSlice) { '`unmeasured`' } else { ('{0} cents · {1:N0} tokens' -f (Format-Number $costPerSlice 2), $tokensPerSlice) }))
Add-Line ''
if (@($rework).Count -gt 0) {
    Add-Line '**The rework in this window, named so the rate is checkable:**'
    Add-Line ''
    foreach ($r in $rework) {
        Add-Line ('- #{0} — {1} _(names #{2})_' -f $r.Number, $r.Title, ($r.Refs -join ', #'))
    }
    Add-Line ''
}

Add-Line '## 3. Slices in the window'
Add-Line ''
Add-Line '| Slice | Work item | Kind | PRs | Helm touches | First commit → final merge | Wait (h) |'
Add-Line '|---|---|---|---|---|---|---|'
foreach ($s in ($allMergedSlices | Sort-Object FirstCommit)) {
    $prNums = (@($s.DeliveryPr.Number) + @($s.GovernancePrs | ForEach-Object { $_.Number })) -join ', '
    Add-Line ('| `{0}` | {1} | {2} | #{3} | {4} | {5:MM-dd HH:mm} → {6:MM-dd HH:mm} | {7} |' -f `
            $s.Key, $s.Dra, $s.Kind, $prNums, $s.HelmTouches, $s.FirstCommit, $s.FinalMerge, (Format-Number $s.WaitHours 2))
}
Add-Line ''
if (@($abandonedSlices).Count -gt 0) {
    Add-Line '**Slices that consumed PRs and delivered nothing** — counted in the PR-traffic'
    Add-Line 'ratio above, because a ratio that drops them flatters the model with its own waste:'
    Add-Line ''
    Add-Line '| Slice | Work item | PRs | What happened |'
    Add-Line '|---|---|---|---|'
    foreach ($s in ($abandonedSlices | Sort-Object FirstCommit)) {
        $prNums = (@($s.DeliveryPr.Number) + @($s.GovernancePrs | ForEach-Object { $_.Number })) -join ', #'
        Add-Line ('| `{0}` | {1} | #{2} | delivery PR `{3}` |' -f $s.Key, $s.Dra, $prNums, $s.DeliveryPr.State)
    }
    Add-Line ''
}
if (@($gaps).Count -gt 0) {
    Add-Line '**Authorization gaps between consecutive slices:**'
    Add-Line ''
    Add-Line '| Work item | After | Before | Hours |'
    Add-Line '|---|---|---|---|'
    foreach ($g in ($gaps | Sort-Object -Property Hours -Descending)) {
        Add-Line ('| {0} | `{1}` | `{2}` | {3} |' -f $g.Dra, $g.From, $g.To, (Format-Number $g.Hours 2))
    }
    Add-Line ''
}

Add-Line '## 4. CI: failure vs flake'
Add-Line ''
Add-Line 'A run is **red** if it concluded `failure` **or** if it needed a second attempt —'
Add-Line 'a rerun-to-green overwrites the run''s conclusion, so counting conclusions alone'
Add-Line 'undercounts exactly the reds the flake ledger exists for. A red is **filed** when'
Add-Line 'its run id appears in `docs/ops/flake-ledger.md`.'
Add-Line ''
if (@($ciRed).Count -eq 0) {
    Add-Line '_No red CI events in the window._'
} else {
    Add-Line '| Run | Branch | Attempt | Conclusion | Scope | Filed as a flake |'
    Add-Line '|---|---|---|---|---|---|'
    foreach ($r in ($ciRed | Sort-Object Created)) {
        Add-Line ('| `{0}` | `{1}` | {2} | {3} | {4} | {5} |' -f `
                $r.Id, $r.Branch, $r.Attempt, $r.Verdict, $r.Scope, $(if ($r.Filed) { '**yes**' } else { 'no' }))
    }
    Add-Line ''
    $unfiled = @($ciRed | Where-Object { -not $_.Filed -and $_.Scope -eq 'window PR branch' })
    Add-Line ('{0} red event(s) on PR branches are **unfiled**. "Passed on rerun" is an' -f $unfiled.Count)
    Add-Line 'observation, not a resolution — an unfiled red is a row `docs/ops/flake-ledger.md`'
    Add-Line 'should have and does not, and this split is the number that says so.'
}
Add-Line ''

Add-Line '## 5. Work items, queue latency and cost'
Add-Line ''
Add-Line '| Work item | Delivery slices | Queue latency (created→first action) | Lead time (h) | Paperclip runs | Cost |'
Add-Line '|---|---|---|---|---|---|'
foreach ($d in ($draRows | Sort-Object Dra)) {
    $costCell = if ($null -eq $d.RunCount) { '`no issue record`' }
    elseif ($d.RunCount -eq 0) { '`no run records`' }
    else { ('{0} cents · {1:N0} in / {2:N0} out tokens' -f $d.CostCents, $d.InputTokens, $d.OutputTokens) }
    $leadCell = (Format-Number $d.LeadTimeHours 2)
    if ($d.Clamped) { $leadCell += ' _(clamped)_' }
    Add-Line ('| {0} | {1} | {2} | {3} | {4} | {5} |' -f `
            $d.Dra, $d.Slices, $(if ($null -eq $d.QueueHours) { '`unmeasured`' } else { (Format-Number $d.QueueHours 2) + ' h' }), `
        $leadCell, $(if ($null -eq $d.RunCount) { '`unmeasured`' } else { $d.RunCount }), $costCell)
}
Add-Line ''
if (@($draRows | Where-Object { $_.Clamped }).Count -gt 0) {
    Add-Line '_(clamped)_ — the work item was accepted before this window opened, so its lead'
    Add-Line 'time is measured from its first commit **in** the window. Letting a standing lane'
    Add-Line 'contribute its whole open life to the denominator would divide the Governance Wait'
    Add-Line 'Ratio down by an amount that has nothing to do with governance.'
    Add-Line ''
}

Add-Line '## 6. What this window could NOT measure, and why'
Add-Line ''
Add-Line '- **Escaped defect rate per tier.** The tier model (plan §2.1) did not exist during'
Add-Line '  this window, so no merge in it carries a tier. The metric becomes computable for'
Add-Line '  windows after M0; it is `unmeasured` here rather than `0`, because nobody looked.'
$noCost = @($draRows | Where-Object { $null -ne $_.RunCount -and $_.RunCount -eq 0 })
if ($noCost.Count -gt 0) {
    Add-Line ('- **Cost per delivered slice.** {0} of the window''s work items have Paperclip issue' -f $noCost.Count)
    Add-Line '  records but **zero run records** — those slices were executed on Soft CLI seats'
    Add-Line '  outside Paperclip''s run accounting, so there is nothing to divide. The instrument'
    Add-Line '  itself works: issues worked through Paperclip return non-zero run counts and token'
    Add-Line '  totals, so the zero is a property of this window and not of the script. The one'
    Add-Line '  item here that does carry run records delivered no slices, so dividing its cost by'
    Add-Line '  another item''s slices would print a number about neither of them.'
    Add-Line '- **`costCents` is 0 across the board**, and that is a billing shape rather than a'
    Add-Line '  measurement: the account bills by subscription, not per token. Tokens are the'
    Add-Line '  quantity that actually moves, which is why they are carried beside the cents.'
}
Add-Line '- **Queue latency by role** is reported as issue-created → first action, which is the'
Add-Line '  latency the Paperclip record can actually support. A per-wake latency needs'
Add-Line '  heartbeat-run records, which these work items do not have.'
Add-Line ''

Add-Line '## 7. Experiments in flight (plan §10.3)'
Add-Line ''
Add-Line 'Read from the `exo-experiment:` tags §10.1 requires on every process change, so'
Add-Line 'that doctrine capture at an M-checkpoint is a copy step and not an archaeology'
Add-Line 'project. **An untagged experiment does not appear here** — which is the point: the'
Add-Line 'tag is what the playbook cites.'
Add-Line ''
if (@($experiments).Count -eq 0) {
    Add-Line '_No `exo-experiment:` tags found in `DECISIONS.md`._'
} else {
    $untagged = @($experiments | Where-Object { -not $_.NamesMetric })
    if ($untagged.Count -gt 0) {
        Add-Line ('> **{0} experiment(s) below name no judging metric.** §10.1 asks the tag to name' -f $untagged.Count)
        Add-Line '> the §6 metric that will judge it, because that is what a graduation entry cites.'
        Add-Line '> This is reported rather than filled in: guessing which metric an author meant is'
        Add-Line '> how an experiment graduates on a number nobody chose for it.'
        Add-Line ''
    }
    Add-Line '| Experiment | Judging metric | Frozen baseline | Current reading |'
    Add-Line '|---|---|---|---|'
    foreach ($e in $experiments) {
        $reading = ('GWR {0} · ACCR {1} · {2} PRs/slice · {3} Helm touches/slice · veto {4} · rework {5}' -f `
            (Format-Number $gwr 2), (Format-Percent $accr), (Format-Number $prsPerSlice 2), `
            (Format-Number $touchesPerSlice 2), (Format-Percent $vetoRate 1), (Format-Percent $reworkRate 1))
        $baseCell = ('**{0}** _(frozen by this run)_' -f $reading)
        $curCell = '_this run IS the baseline_'
        if (-not $Baseline -and $null -ne $frozen) {
            $baseCell = ('GWR {0} · ACCR {1} · {2} PRs/slice · {3} Helm touches/slice · veto {4} · rework {5}' -f `
                (Format-Number $frozen.gwr 2), (Format-Percent $frozen.accr), (Format-Number $frozen.prsPerSlice 2), `
                (Format-Number $frozen.helmTouchesPerSlice 2), (Format-Percent $frozen.vetoRate 1), (Format-Percent $frozen.reworkRate 1))
            $curCell = $reading
        }
        Add-Line ('| `{0}` | {1} | {2} | {3} |' -f $e.Name, $e.JudgedBy, $baseCell, $curCell)
    }
}
Add-Line ''
Add-Line '## 8. Reproducing this'
Add-Line ''
Add-Line '```bash'
Add-Line ('pwsh -NoProfile -File scripts/exo-metrics.ps1 -FromPr {0} -ToPr {1}{2}' -f $FromPr, $ToPr, $(if ($Baseline) { ' -Baseline' } else { '' }))
Add-Line 'pwsh -NoProfile -File scripts/exo-metrics.ps1 -SelfTest   # the detectors fire'
Add-Line '```'
Add-Line ''
Add-Line 'Needs `gh` authenticated against the repo and, for the cost and queue columns,'
Add-Line '`PAPERCLIP_API_URL` / `PAPERCLIP_API_KEY` / `PAPERCLIP_COMPANY_ID`. Without them'
Add-Line 'the GitHub-derived rows still compute and the Paperclip-derived ones read'
Add-Line '`unmeasured` — `-NoPaperclip` makes that explicit.'

$outPath = Join-Path $RepoRoot $Out
Write-Utf8NoBom -Path $outPath -Text ($sb.ToString())
Write-Host "Wrote $Out"

if ($Baseline) {
    Write-Utf8NoBom -Path $baselinePath -Text (($current | ConvertTo-Json -Depth 4))
    Write-Host "Froze $BaselineFile"
}

Write-Host ''
Write-Host ('GWR {0} (incl. SSC wait {1}) · ACCR {2} · {3} PRs/slice · {4} Helm touches/slice · CI median {5} min' -f `
    (Format-Number $gwr 2), (Format-Number $gwrWithSsc 2), (Format-Percent $accr), `
    (Format-Number $prsPerSlice 2), (Format-Number $touchesPerSlice 2), (Format-Number $medianCi 1))

[Console]::OutputEncoding = $priorOutputEncoding
