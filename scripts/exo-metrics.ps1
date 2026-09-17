<#
.SYNOPSIS
  ExO execution-model dashboard — DRA-73 plan §6 + §10.3.

.DESCRIPTION
  Computes the §6 metric table over a window of GitHub PRs and emits
  docs/ops/exo-dashboard.md. Dependencies: `gh` and the Paperclip API. Nothing
  else — no modules, no network beyond those two.

  Every metric is computed from data that already exists (PR timestamps, merge
  commits, workflow runs, HELM.md commits, the flake ledger, Paperclip issue
  records, the player-report feed). A metric whose data does NOT exist in the
  window is reported as `unmeasured` **with the reason**, never as a zero — an
  unmeasured metric and a measured zero are different claims (trap 64b).

  And the REASON is computed too (DRA-127). A reason printed beside an
  `unmeasured` row is a claim about the window it is printed for; a constant
  sentence is true of at most the window it was written for, and stays printed
  for all the others. Every such sentence here is derived from what this run
  actually read, and the run REFUSES to write a document whose text names a
  precondition the measured window satisfies.

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
    [switch]$NoDefectFeed,
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
# The player-report feed — escaped defect rate (DRA-127).
#
# The feed is GitHub Discussions on the product repo. That is where player
# reports actually land — 166 of them at the time of writing — and it is the
# only channel this instrument can read without inventing one.
#
# It is also, as read, ILLEGIBLE to a defect count, and the three discussions
# the card named show why: #234 ("Named Monsters in Guk do not appear in
# session") reads as a defect and sits in Q&A, while #237 and #239 are feature
# requests and sit in Ideas. The repo's ten labels are never applied to
# discussions, and the category is the author's guess, not a triage verdict.
#
# A discussion becomes an ESCAPED DEFECT READING only when BOTH halves hold:
#
#   1. it is MARKED as a defect report, and
#   2. the marker NAMES the merge the defect escaped through.
#
# Neither half is decoration. Without (1) the feed is a mixed channel — feature
# requests, quest data, "how do I", and genuine regressions, in one list with no
# labels — and counting it would report ideas as defects. Without (2) a report
# cannot be attributed to a window or a tier at all: an escaped defect is
# reported LATER than the merge that caused it, usually in some other window, so
# "discussions opened during the window" is not the population. The population
# is "reports naming a merge IN the window", and only the marker can say that.
#
# This is why the row is `unmeasured` while the marker is unused, and why that
# is a STRUCTURAL absence rather than a checkpoint that has not arrived: no
# M-milestone marks a discussion. The moment reports carry the marker, the code
# path below computes — including a legitimate measured ZERO, because once the
# feed is legible "no defect escaped this window's merges" is a finding.
#
# DRA-133 / DRA-135 amend the marker and the read in four ways, and each one
# exists to stop the SAME failure: silence printed as a measured zero.
#
#   - The marker is posted as a COMMENT by triage (ruling 1), not written into
#     the player's body, so the read below matches the body OR any comment.
#     Until this landed, a triage sweep would have run, marked, and produced
#     nothing the instrument could see — worse than no sweep, because it would
#     manufacture the belief that the feed is marked.
#   - `unattributed` is an accepted form (ruling 3): "marked, but nobody can
#     name the culprit merge" is a real state, and refusing to represent it
#     means it is recorded as NOT MARKED, which is an undercount.
#   - A window whose merges predate the convention could not have been marked
#     (ruling 4), and a window younger than the report lag floor has a
#     population that has not arrived yet (ruling 6). Both are `unmeasured`
#     with a named reason, never `0`.
$script:DefectMarkerPattern = '(?im)^\s*exo-defect:\s*escaped\s+(?:#(\d+)|(unattributed))\s*$'

# One page is normally the whole feed. The cap exists so that a feed which has
# outgrown it reports TRUNCATED rather than a count that silently omits its own
# tail — an undercount of escaped defects is the one direction that flatters the
# experiment this metric is the stated cost of.
$script:DefectFeedPageSize = 100
$script:DefectFeedPageCap = 20

# The marker now lives in comments, so the comment fetch inherits the same
# discipline: a discussion with an unread comment tail AND no marker in what was
# read is not "unmarked", it is UNREAD, and the whole reading reports Truncated
# rather than a count missing that discussion (ruling 5).
$script:DefectCommentPageSize = 100

# ---------------------------------------------------------------------------
# The two guards that stop adoption turning an honest blank into a zero
# (DRA-133 rulings 4 and 6).
#
# `Get-EscapedDefectReading` used to leave `NoConvention` the moment ANY marked
# report existed ANYWHERE in the feed, then divide in-window reports by delivery
# slices. So the first marker ever posted — on any discussion, naming any merge,
# however old — would have flipped every other window from an honest
# `unmeasured` to a measured `0.000`, sourced from silence and printed as
# stated-net-of quality credit for `whole-sequence-auth`. These two constants
# are what make that impossible.

# The date the marking sweep begins. A window whose last merge predates it is
# PERMANENTLY unmeasurable and says so: no backfill (ruling 4), because
# retro-bisecting a three-week-old report is guesswork presented as measurement.
#
# $null is the PLACEHOLDER state and it is deliberately the strictest one: until
# Scribe's first sweep sets a date, NO window is after the convention, so no
# window computes. The alternative — leaving it unset and permissive — is
# exactly the trap above, reachable by one marker comment posted before the
# convention has a start date to be measured against.
$script:DefectConventionStart = $null

# Report-lag floor, in days. An escaped defect is reported LATER than the merge
# that caused it, so a window read the day it closes has a population that has
# not arrived. `0/N` in that state is a statement about the calendar.
#
# L = 14 is judgement, not data — there is no marked data yet to set it from.
# Which is why every marked report's observed lag is recorded and reported
# below: L is re-set from that distribution at five marked reports or M2,
# whichever comes first (ruling 6).
$script:DefectReportLagFloorDays = 14

# ---------------------------------------------------------------------------
# Reason honesty (DRA-127).
#
# The defect this exists for: the escaped-defect row printed a hardcoded
# sentence — "the tier model (plan §2.1) did not exist during this window, so no
# merge in it carries a tier. The metric becomes computable for windows after
# M0." That was true of the baseline window #580–#607 and FALSE of every window
# after DRA-74 landed the tier model (PR #608, merge stamp below). It printed
# again unchanged over #619–#643, a window whose PRs do carry tiers, and it
# promised a fix — "computable after M0" — that nothing was going to deliver,
# because the missing thing was never the tier model. A wrong VALUE gets caught
# by the next reader who checks it; a wrong reason for an absent value tells
# that reader not to bother.
#
# A constant sentence may state a STRUCTURAL fact ("no marking convention
# exists"). It may not name a precondition a window can satisfy, because
# nothing re-checks a constant. Each check below is a phrase plus the window
# condition that makes printing it dishonest; the scan runs over the rendered
# document before it is written, so it catches a hardcoded reason anywhere in
# the output, not only the rows this script currently knows about.
$script:TierModelLandedUtc = [datetime]::Parse(
    '2026-09-14T14:25:13Z', [System.Globalization.CultureInfo]::InvariantCulture,
    [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor
    [System.Globalization.DateTimeStyles]::AssumeUniversal)

$script:WindowClaimChecks = @(
    (@{
            Name    = 'tier-model-absent'
            # `[\s\S]` and `\s+`, not `.` and ' ': Add-Line wraps the rendered
            # document, so the retired sentence reaches this scan split across
            # two lines with the indent in the middle. A pattern that only
            # matches the one-line form would pass on the committed defect.
            Pattern = '(?i)tier model[\s\S]{0,140}?did\s+not\s+exist'
            # Dishonest as soon as the window reaches the merge that landed the
            # tier model: from there on, merges in it DO carry tiers.
            Satisfied = { param($Start, $End) $End -ge $script:TierModelLandedUtc }
            Why     = 'the window extends past DRA-74 / PR #608, so merges in it do carry tiers'
        }),
    (@{
            Name    = 'computable-after-checkpoint'
            Pattern = '(?i)becomes\s+computable\s+for\s+windows\s+after\s+M\d'
            # The promise itself is the defect, and it is dishonest for every
            # window at or past the checkpoint it names: the checkpoint passed
            # and the metric did not become computable, because a checkpoint
            # wires no feed. Before M0 it is a forecast; after, it is a lie.
            Satisfied = { param($Start, $End) $End -ge $script:TierModelLandedUtc }
            Why     = 'the named checkpoint has passed and the metric did not become computable — a checkpoint wires no feed'
        })
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
# Data acquisition — the player-report feed
# ---------------------------------------------------------------------------

$script:DefectFeedFailures = @()

function Get-DefectFeedPage {
    <# One page of Discussions, newest first. Separated from the loop below so
       the loop is testable against a fake page source: the live query needs a
       network and -SelfTest must not. #>
    param([string]$Repository, [string]$Cursor)

    $parts = $Repository -split '/'
    if ($parts.Count -ne 2) { throw "Repository '$Repository' is not owner/name." }

    # `after: null` is the first page; gh -F sends an empty string as an empty
    # string, which GraphQL rejects as a cursor, so the argument is omitted
    # rather than blanked.
    # `comments` is the DRA-133 ruling-5 widening: the marker is a triage
    # COMMENT, so a query that reads bodies only reads a feed that is marked and
    # reports it unmarked. `totalCount` and the comments' own `hasNextPage` are
    # both fetched because the caller has to be able to tell "this discussion
    # has no marker" from "this discussion has more comments than I read".
    $query = @'
query($owner:String!,$name:String!,$size:Int!,$csize:Int!,$after:String){
  repository(owner:$owner,name:$name){
    discussions(first:$size, after:$after, orderBy:{field:CREATED_AT,direction:DESC}){
      pageInfo{ hasNextPage endCursor }
      nodes{
        number title url createdAt body category{ name }
        comments(first:$csize){
          totalCount
          pageInfo{ hasNextPage }
          nodes{ body createdAt url author{ login } }
        }
      }
    }
  }
}
'@
    $ghArgs = @('api', 'graphql', '-f', "query=$query",
        '-F', "owner=$($parts[0])", '-F', "name=$($parts[1])",
        '-F', "size=$script:DefectFeedPageSize",
        '-F', "csize=$script:DefectCommentPageSize")
    if (-not [string]::IsNullOrWhiteSpace($Cursor)) { $ghArgs += @('-F', "after=$Cursor") }

    $raw = & gh @ghArgs 2>&1
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
        throw ("gh api graphql (discussions) failed: {0}" -f (($raw | Out-String).Trim()))
    }
    $json = $raw | ConvertFrom-Json
    if ($null -ne $json.PSObject.Properties['errors'] -and $null -ne $json.errors) {
        throw ("gh api graphql (discussions) returned errors: {0}" -f (($json.errors | ForEach-Object { $_.message }) -join '; '))
    }
    return $json.data.repository.discussions
}

function Get-NodeProperty {
    <# StrictMode makes a missing property a terminating error, and the fake
       nodes -SelfTest builds deliberately omit `comments` (a discussion with no
       comment thread is the common case and must not be a crash). #>
    param($Node, [string]$Name)
    if ($null -eq $Node) { return $null }
    if ($null -eq $Node.PSObject.Properties[$Name]) { return $null }
    return $Node.$Name
}

function Get-DefectMarkerFromNode {
    <# The marker read, over ONE discussion: body first, then comments in the
       order GitHub returns them.

       Returns a marker record, or a truncation flag, or neither — and the
       difference between the last two is the whole of ruling 5. A discussion
       whose comment tail was not read and which carries no marker in what WAS
       read is not unmarked; it is unread, and reporting it as unmarked is the
       silent undercount `Truncated` exists to refuse.

       A discussion whose marker was already found needs no tail: one discussion
       is one report, so the only thing the tail could add is a second marker on
       the same report. Counting it once is the reading. #>
    param($Node)

    $number = [int](Get-NodeProperty $Node 'number')
    $created = ConvertTo-Utc (Get-NodeProperty $Node 'createdAt')

    $bodyMatch = [regex]::Match([string](Get-NodeProperty $Node 'body'), $script:DefectMarkerPattern)
    if ($bodyMatch.Success) {
        return [pscustomobject]@{
            Marked = $true; Truncated = $false
            Report = (New-DefectReport -Node $Node -Match $bodyMatch -Source 'body' `
                    -MarkedAt $created -MarkedBy ([string](Get-NodeProperty $Node 'authorLogin')))
        }
    }

    $comments = Get-NodeProperty $Node 'comments'
    $commentNodes = @()
    $tailUnread = $false
    if ($null -ne $comments) {
        $commentNodes = @(Get-NodeProperty $comments 'nodes')
        $pi = Get-NodeProperty $comments 'pageInfo'
        if ($null -ne $pi -and [bool](Get-NodeProperty $pi 'hasNextPage')) { $tailUnread = $true }
    }

    foreach ($c in $commentNodes) {
        if ($null -eq $c) { continue }
        $m = [regex]::Match([string](Get-NodeProperty $c 'body'), $script:DefectMarkerPattern)
        if (-not $m.Success) { continue }
        $author = Get-NodeProperty $c 'author'
        $login = if ($null -ne $author) { [string](Get-NodeProperty $author 'login') } else { '' }
        return [pscustomobject]@{
            Marked = $true; Truncated = $false
            Report = (New-DefectReport -Node $Node -Match $m -Source 'comment' `
                    -MarkedAt (ConvertTo-Utc (Get-NodeProperty $c 'createdAt')) -MarkedBy $login)
        }
    }

    if ($tailUnread) {
        return [pscustomobject]@{ Marked = $false; Truncated = $true; Report = $null; Number = $number }
    }
    return [pscustomobject]@{ Marked = $false; Truncated = $false; Report = $null; Number = $number }
}

function New-DefectReport {
    <# One marked report. `MergedPr` is $null for the `unattributed` form, and
       that $null is load-bearing: an unattributed report is COUNTED (it is a
       real escaped defect somebody triaged) but belongs to no window, so it
       enters neither the numerator nor the denominator of any rate (ruling 3).

       `Created` is the PLAYER's report time and `MarkedAt` is the TRIAGE time.
       They are different facts and ruling 6's observed lag is built from the
       first: how long after a merge the report arrives is what sets L. The
       second says how long triage took, which is our latency, not the feed's. #>
    param($Node, $Match, [string]$Source, $MarkedAt, [string]$MarkedBy)

    $attributed = $Match.Groups[1].Success
    return [pscustomobject]@{
        Number   = [int](Get-NodeProperty $Node 'number')
        Title    = [string](Get-NodeProperty $Node 'title')
        Url      = [string](Get-NodeProperty $Node 'url')
        Created  = (ConvertTo-Utc (Get-NodeProperty $Node 'createdAt'))
        MergedPr = $(if ($attributed) { [int]$Match.Groups[1].Value } else { $null })
        MarkerSource = $Source
        MarkedAt = $MarkedAt
        MarkedBy = $MarkedBy
    }
}

function Get-DefectReports {
    <# Walks the whole feed and returns the MARKED reports as a status value, in
       the same three-worlds shape the Paperclip reads use: the failure is a
       value, not a $null that every caller re-guesses at (DRA-80).

       The whole feed, not a date slice: an escaped defect is reported after the
       merge that caused it, so a report about this window can be younger than
       any bound the window has. The marker names the merge; the marker is the
       bound. #>
    param(
        [string]$Repository,
        [bool]$Enabled = $true,
        [scriptblock]$PageSource = $null
    )

    if (-not $Enabled) {
        return [pscustomobject]@{
            Status = 'NotConfigured'; Reports = @(); Scanned = 0; Newest = $null
            Reason = '-NoDefectFeed: the player-report feed was not read'
        }
    }

    $fetch = $PageSource
    if ($null -eq $fetch) { $fetch = { param($Cursor) Get-DefectFeedPage -Repository $Repository -Cursor $Cursor }.GetNewClosure() }

    $reports = @()
    $scanned = 0
    $cursor = $null
    # Newest discussion createdAt across the whole walk — ruling 6's feed
    # liveness. A window can only be read against a feed that was alive after it
    # closed; a dead feed reading as zero escaped defects is the same lie as an
    # unread one.
    $newest = $null
    # Discussions whose comment tail went unread AND which carried no marker in
    # what was read. Named, not counted, so the truncation reason can say which
    # discussions to go look at.
    $commentTruncated = @()
    for ($page = 0; $page -lt $script:DefectFeedPageCap; $page++) {
        try { $d = & $fetch $cursor }
        catch {
            $reason = ('player-report feed read failed: {0}' -f $_.Exception.Message)
            $script:DefectFeedFailures += , $reason
            return [pscustomobject]@{ Status = 'Unreachable'; Reports = @(); Scanned = $scanned; Newest = $newest; Reason = $reason }
        }
        if ($null -eq $d) {
            $reason = 'player-report feed returned no payload'
            $script:DefectFeedFailures += , $reason
            return [pscustomobject]@{ Status = 'Unreachable'; Reports = @(); Scanned = $scanned; Newest = $newest; Reason = $reason }
        }

        foreach ($n in @($d.nodes)) {
            $scanned++
            $created = ConvertTo-Utc (Get-NodeProperty $n 'createdAt')
            if ($null -ne $created -and ($null -eq $newest -or $created -gt $newest)) { $newest = $created }

            $hit = Get-DefectMarkerFromNode -Node $n
            if ($hit.Marked) { $reports += , $hit.Report; continue }
            if ($hit.Truncated) { $commentTruncated += , $hit.Number }
        }

        if (-not $d.pageInfo.hasNextPage) {
            if (@($commentTruncated).Count -gt 0) { return (New-CommentTruncatedResult $reports $scanned $newest $commentTruncated) }
            return [pscustomobject]@{ Status = 'Ok'; Reports = $reports; Scanned = $scanned; Newest = $newest; Reason = '' }
        }
        $cursor = [string]$d.pageInfo.endCursor
    }

    # Cap reached with pages left. Reporting the partial count would undercount
    # escaped defects, which is the direction that flatters the experiment this
    # metric is the stated cost of, so it reports the truncation instead.
    $reason = ('player-report feed exceeded {0} pages of {1}; the tail was not read' -f
        $script:DefectFeedPageCap, $script:DefectFeedPageSize)
    if (@($commentTruncated).Count -gt 0) {
        $reason += ('; and {0} discussion(s) had unread comment tails (#{1})' -f
            @($commentTruncated).Count, (@($commentTruncated) -join ', #'))
    }
    $script:DefectFeedFailures += , $reason
    return [pscustomobject]@{ Status = 'Truncated'; Reports = $reports; Scanned = $scanned; Newest = $newest; Reason = $reason }
}

function New-CommentTruncatedResult {
    <# The feed pages were all read, but some discussion's COMMENTS were not,
       and the marker now lives in comments — so this is the same claim the page
       cap makes and gets the same status. The counted reports are a lower bound
       and are not reported as a rate. #>
    param($Reports, [int]$Scanned, $Newest, $Numbers)
    $reason = ('{0} discussion(s) carry more than {1} comment(s) and no marker in the page read (#{2}); the marker now lives in comments, so those tails are unread rather than unmarked' -f
        @($Numbers).Count, $script:DefectCommentPageSize, (@($Numbers) -join ', #'))
    $script:DefectFeedFailures += , $reason
    return [pscustomobject]@{ Status = 'Truncated'; Reports = $Reports; Scanned = $Scanned; Newest = $Newest; Reason = $reason }
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

# The three worlds this call can land in are DIFFERENT CLAIMS, and $null told
# them apart from nothing (DRA-80). "The API refused the connection" and "the
# API answered, and has no record of DRA-71" produce the same absent cost row,
# and a -Baseline run froze that absence as a measurement. That is trap 11
# (evidence only one side can produce) wearing trap 64b's clothes (a $null proxy
# standing in for a fact nobody named), and it cost a wrong frozen GWR: this
# box's PAPERCLIP_API_URL says localhost, the API binds a tailnet address, every
# GET was refused, and the run printed its success line.
#
# So the failure is a VALUE. `Status` is one of:
#   Ok            — the API answered. `Data` is what it said (possibly null).
#   NotConfigured — no base or no key. The operator never asked for this.
#   Unreachable   — the call was made and threw. We know NOTHING about the data.
# `NoRecord` is the callers' to declare: only they know what they were looking
# for in an Ok payload.
$script:PaperclipFailures = @()

function New-PaperclipResult {
    param(
        [ValidateSet('Ok', 'NoRecord', 'NotConfigured', 'Unreachable')][string]$Status,
        $Data,
        [string]$Reason
    )
    return [pscustomobject]@{
        Status      = $Status
        Data        = $Data
        Reason      = $Reason
        Ok          = ($Status -eq 'Ok')
        Unreachable = ($Status -eq 'Unreachable')
    }
}

function Invoke-Paperclip {
    param([string]$Base, [string]$Key, [string]$Path, [int]$TimeoutSec = 40)
    if ([string]::IsNullOrWhiteSpace($Base) -or [string]::IsNullOrWhiteSpace($Key)) {
        return (New-PaperclipResult -Status 'NotConfigured' -Data $null `
                -Reason 'PAPERCLIP_API_URL / PAPERCLIP_API_KEY not set')
    }
    try {
        $data = Invoke-RestMethod -Method Get -Uri ("$Base$Path") -TimeoutSec $TimeoutSec `
            -Headers @{ Authorization = "Bearer $Key" }
        return (New-PaperclipResult -Status 'Ok' -Data $data -Reason '')
    } catch {
        # Registered here rather than at the call sites, because a caller that
        # forgets to register turns the refusal below back into the silence this
        # whole change exists to delete.
        $reason = ('GET {0}{1} failed: {2}' -f $Base, $Path, $_.Exception.Message)
        $script:PaperclipFailures += , $reason
        Write-Verbose "Paperclip $reason"
        return (New-PaperclipResult -Status 'Unreachable' -Data $null -Reason $reason)
    }
}

function Get-PaperclipFailureSummary {
    <# The one sentence a row or banner cites. Exception messages carry the URL
       and a stack-ish tail; a table cell gets the first line only. #>
    param([string[]]$Failures = $script:PaperclipFailures)
    if (@($Failures).Count -eq 0) { return '' }
    $first = ([string]$Failures[0] -split "`r?`n")[0].Trim()
    if ($first.Length -gt 160) { $first = $first.Substring(0, 157) + '…' }
    if (@($Failures).Count -gt 1) { $first += (' (+{0} more)' -f (@($Failures).Count - 1)) }
    return $first
}

function Format-PaperclipUnmeasured {
    <# Never a bare `unmeasured`, and never a `0`: the cell says which of the
       three worlds it is reporting. The dashboard header promises exactly this. #>
    param([string]$Fallback = 'no Paperclip record')
    if (@($script:PaperclipFailures).Count -gt 0) { return '`unmeasured` — API unreachable' }
    return ('`unmeasured` — {0}' -f $Fallback)
}

function Test-BaselineRefused {
    <# A -Baseline run exists to write a number down FIRST so later claims are
       checkable against it. Freezing an absence we never measured poisons every
       later comparison, so an Unreachable input REFUSES the freeze outright.
       -NoPaperclip is the explicit door: it says "I know these are unmeasured",
       and a run through it never calls the API, so it has no failures to weigh. #>
    param([bool]$Baseline, [bool]$NoPaperclip, [string[]]$Failures)
    if (-not $Baseline) { return $false }
    if ($NoPaperclip) { return $false }
    return (@($Failures).Count -gt 0)
}

function Write-BaselineFreeze {
    <# The guard lives INSIDE the writer, so there is no path to the file that
       does not pass it (trap 47: never let two code paths decide one question). #>
    param([string]$Path, $Current, [bool]$NoPaperclip, [string[]]$Failures)
    if (Test-BaselineRefused -Baseline $true -NoPaperclip $NoPaperclip -Failures $Failures) {
        throw ("Refusing to freeze a baseline: {0} Paperclip read(s) were UNREACHABLE, so the " +
            "Paperclip-derived rows are unknown rather than zero. First failure: {1}. " +
            "Fix the endpoint, or re-run with -NoPaperclip to freeze them as explicitly unmeasured." `
                -f @($Failures).Count, (Get-PaperclipFailureSummary -Failures $Failures))
    }
    Write-Utf8NoBom -Path $Path -Text (($Current | ConvertTo-Json -Depth 4))
}

function Get-ReproduceCommand {
    <# The printed recipe must regenerate THIS file. -WindowLabel feeds the
       header, so a command that drops it regenerates a file that differs from
       the committed one for a reason that says nothing about the metrics — and
       the lesson people learn from that gate is that the gate is noise
       (trap 74). Same argument for -NoPaperclip and -NoDefectFeed, which decide
       which rows are measured at all. #>
    param([int]$FromPr, [int]$ToPr, [string]$WindowLabel, [bool]$Baseline, [bool]$NoPaperclip, [bool]$NoDefectFeed)
    $cmd = ('pwsh -NoProfile -File scripts/exo-metrics.ps1 -FromPr {0} -ToPr {1}' -f $FromPr, $ToPr)
    if (-not [string]::IsNullOrWhiteSpace($WindowLabel)) {
        $cmd += (" -WindowLabel '{0}'" -f $WindowLabel.Replace("'", "''"))
    }
    if ($Baseline) { $cmd += ' -Baseline' }
    if ($NoPaperclip) { $cmd += ' -NoPaperclip' }
    if ($NoDefectFeed) { $cmd += ' -NoDefectFeed' }
    return $cmd
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

function Get-TierMap {
    <# Work item → tier, read from the `Tier T…` line each DECISIONS.md entry
       carries. Read rather than kept by hand for the same reason §10.3 reads the
       `exo-experiment:` tags: a hand-kept list is a second copy of a fact, and
       the two copies disagree silently.

       Sparse on purpose — most entries carry no tier line, and an item with no
       tier reads `untiered` rather than being assigned a plausible one. A tier
       this instrument invented would be a tier no ruling ever set. #>
    param([string]$DecisionsPath)

    if (-not (Test-Path $DecisionsPath)) { return @{} }
    $lines = @([System.IO.File]::ReadAllLines($DecisionsPath, [System.Text.Encoding]::UTF8))

    $map = @{}
    $current = @()
    foreach ($line in $lines) {
        if ($line -match '^##\s') {
            $current = @([regex]::Matches($line, '(?i)dra[-\s]?(\d{2,3})') | ForEach-Object { 'DRA-{0}' -f $_.Groups[1].Value })
            continue
        }
        if ($current.Count -eq 0) { continue }
        $m = [regex]::Match($line, '(?i)\bTier\s+(T\d(?:/T\d)?)\b')
        if (-not $m.Success) { continue }
        foreach ($dra in $current) {
            # First tier line in an entry wins: later mentions in the same entry
            # are prose about other work ("a T2 ruling would be needed").
            if (-not $map.ContainsKey($dra)) { $map[$dra] = $m.Groups[1].Value }
        }
    }
    return $map
}

function Get-EscapedDefectReading {
    <# The §6 escaped-defect row, as a value that carries its own reason.

       EIGHT worlds now, and they are DIFFERENT CLAIMS — which is the whole of
       DRA-127. `Ok` with a rate of 0 is a measurement ("the feed is legible and
       no marked report names a merge in this window"); `NoConvention` is the
       absence of one ("nothing in the feed says which discussions are defect
       reports, so a count of them would be a count of ideas"). Collapsing those
       two into a bare `unmeasured` with a stock sentence is what let one
       sentence, true of the window it was written for, print unchanged over the
       next one.

       DRA-133 adds three more, and they are the reason this function is not
       just a division. Before them, the reading left `NoConvention` the moment
       ANY marked report existed ANYWHERE in the feed. One marker on one old
       discussion naming one old merge would have turned every other window's
       row into a measured `0.000` — an assertion about merges nobody had
       looked at, printed as quality credit.

         PreConvention — the window's merges predate the marking sweep, so
                         nothing in it COULD have been marked (ruling 4).
         LagFloor      — the window is younger than L days, so its reports have
                         not arrived yet; `0/N` here is about the calendar
                         rather than the code (ruling 6).
         FeedSilent    — no discussion at all has been created since the window
                         closed. A dead feed reading as zero escaped defects is
                         the same lie as an unread one (ruling 6).

       Precedence is deliberate and is ordered by how PERMANENT the reason is,
       not by how the checks happen to be written:

         feed read failure  (nothing about the window is knowable)
         → PreConvention    (permanent; no sweep will ever fix this window)
         → NoConvention     (feed-wide; true until the first marker exists)
         → LagFloor         (window-specific; fixes itself on a known date)
         → FeedSilent       (window-specific; fixes itself if anyone posts)
         → Ok

       NoConvention precedes LagFloor on purpose: when NOTHING in the feed is
       marked, "this window is too young to read" is the less informative of two
       true sentences, and it implies the instrument would otherwise have read
       it. #>
    param($Feed, $Facts, $Delivery, $TierMap, $Now = $null)

    if ($null -eq $Now) { $Now = [datetime]::UtcNow }

    $windowPrs = @($Facts | ForEach-Object { $_.Number })

    # Ruling 3's partition, and it happens BEFORE anything is counted. An
    # unattributed report names no merge, so it is in no window — but it is a
    # real marked defect and vanishing it would undercount, which is the one
    # direction that flatters the experiment this metric is the cost of.
    $attributed = @($Feed.Reports | Where-Object { $null -ne (Get-NodeProperty $_ 'MergedPr') })
    $unattributed = @($Feed.Reports | Where-Object { $null -eq (Get-NodeProperty $_ 'MergedPr') })
    $inWindow = @($attributed | Where-Object { $windowPrs -contains $_.MergedPr })
    $elsewhere = @($attributed | Where-Object { $windowPrs -notcontains $_.MergedPr })

    # The window's last merge — the date both guards are measured from.
    $mergeStamps = @($Facts | ForEach-Object { $_.Merged } | Where-Object { $null -ne $_ })
    $lastMerge = $null
    if ($mergeStamps.Count -gt 0) { $lastMerge = ($mergeStamps | Sort-Object)[-1] }

    # Observed report lag, per in-window marked report (ruling 6). L = 14 was set
    # by judgement because there was no marked data to set it from; this is the
    # data. Only in-window reports have a merge date to subtract — a report
    # naming a merge outside the window names a PR this run never fetched, so
    # its lag is unknown and is reported as unknown rather than guessed.
    $lags = @()
    foreach ($r in $inWindow) {
        $owner = @($Facts | Where-Object { $_.Number -eq $r.MergedPr -and $null -ne $_.Merged })
        if ($owner.Count -eq 0) { continue }
        $lags += , [pscustomobject]@{
            Number   = $r.Number
            MergedPr = $r.MergedPr
            Days     = [math]::Round(($r.Created - $owner[0].Merged).TotalDays, 2)
        }
    }

    $daysSince = $null
    if ($null -ne $lastMerge) { $daysSince = [math]::Round(($Now - $lastMerge).TotalDays, 2) }

    # A feed value built before DRA-135 (or by a caller that only needs the
    # statuses) has no `Newest`. Absent is not "the feed is dead": an absent
    # liveness fact must not be read as the strongest liveness claim, which is
    # why FeedSilent below requires a zero reading as well.
    $feedNewest = Get-NodeProperty $Feed 'Newest'

    # --- status, in precedence order -------------------------------------
    $status = $Feed.Status
    $conventionCase = ''
    if ($status -eq 'Ok') {
        if ($null -eq $lastMerge) {
            # No merged PR in the window at all, so there is no date to measure
            # either guard from. The window is not SHOWN to be inside the
            # convention, and "not shown" resolves to unmeasured, never to 0.
            $status = 'PreConvention'; $conventionCase = 'no-merge'
        } elseif ($null -eq $script:DefectConventionStart) {
            # The placeholder state. Strict on purpose: with no adopted date,
            # no window is after the convention, so a marker posted before
            # Scribe's sweep cannot promote any window to a measured zero.
            $status = 'PreConvention'; $conventionCase = 'unset'
        } elseif ($lastMerge -lt $script:DefectConventionStart) {
            $status = 'PreConvention'; $conventionCase = 'predates'
        } elseif (@($Feed.Reports).Count -eq 0) {
            $status = 'NoConvention'
        } elseif (@($attributed).Count -eq 0) {
            # Marked, but not one marked report names a merge. The rate would be
            # 0/N with a "lower bound" label on it — and "at least 0 per
            # delivered slice" is true of every window that has ever existed, so
            # it is not a reading, it is a shape. The done bar is explicit that a
            # zero needs a marked, ATTRIBUTED report behind it; this is the arm
            # where marking is demonstrably happening and attribution is not.
            $status = 'Unattributable'
        } elseif ($daysSince -lt $script:DefectReportLagFloorDays) {
            $status = 'LagFloor'
        } elseif (@($inWindow).Count -eq 0 -and ($null -eq $feedNewest -or $feedNewest -le $lastMerge)) {
            # Nothing has been posted since this window closed. An in-window
            # report is still theoretically reachable (the marker is a comment,
            # and a comment can land on an old discussion), so this check runs
            # only when the reading would otherwise have been a zero.
            $status = 'FeedSilent'
        }
    }

    $rate = $null
    $perTier = @()
    if ($status -eq 'Ok' -and @($Delivery).Count -gt 0) {
        $rate = @($inWindow).Count / @($Delivery).Count
        $buckets = @{}
        foreach ($r in $inWindow) {
            $owner = @($Facts | Where-Object { $_.Number -eq $r.MergedPr })
            $dra = if ($owner.Count -gt 0) { $owner[0].Dra } else { 'unattributed' }
            $tier = if ($TierMap.ContainsKey($dra)) { $TierMap[$dra] } else { 'untiered' }
            if (-not $buckets.ContainsKey($tier)) { $buckets[$tier] = @() }
            $buckets[$tier] += , $r
        }
        foreach ($k in ($buckets.Keys | Sort-Object)) {
            $perTier += , [pscustomobject]@{ Tier = $k; Count = @($buckets[$k]).Count }
        }
    }

    # Ruling 3's lower-bound rule: when as many reports failed attribution as
    # succeeded, the rate is a floor and must be printed as one, so attribution
    # difficulty shows up as visible uncertainty rather than as a smaller number.
    #
    # The ruling's literal test is `unattributed >= inWindow`, which fires at
    # 0 >= 0. Taken literally it would stamp "lower bound" on every honest
    # measured zero, including one read from a feed where attribution never
    # failed — collapsing the measured-zero-is-a-finding distinction DRA-127
    # exists to protect. So the degenerate arm is excluded: with no unattributed
    # report there is no attribution difficulty for the label to be about. This
    # is the one place this implementation reads the ruling narrowly, and it is
    # called out in the PR for Planner to overrule if that reading is wrong.
    $lowerBound = ($status -eq 'Ok' -and @($unattributed).Count -gt 0 -and
        @($unattributed).Count -ge @($inWindow).Count)

    return [pscustomobject]@{
        Status          = $status
        FeedStatus      = $Feed.Status
        Rate            = $rate
        Scanned         = [int]$Feed.Scanned
        MarkedTotal     = @($Feed.Reports).Count
        InWindow        = $inWindow
        Elsewhere       = @($elsewhere).Count
        Unattributed    = @($unattributed)
        LowerBound      = $lowerBound
        DeliverySlices  = @($Delivery).Count
        PerTier         = $perTier
        FeedReason      = [string]$Feed.Reason
        ConventionCase  = $conventionCase
        ConventionStart = $script:DefectConventionStart
        LastMerge       = $lastMerge
        DaysSinceLastMerge = $daysSince
        LagFloorDays    = $script:DefectReportLagFloorDays
        FeedNewest      = $Feed.Newest
        ObservedLags    = $lags
        CommentMarked   = @($Feed.Reports | Where-Object { $_.MarkerSource -eq 'comment' }).Count
    }
}

function Format-EscapedDefectCell {
    <# The §2 cell. One function, so the cell and the §6 sentence below cannot
       drift apart: two code paths deciding one question is trap 47, and the
       table saying "see §6" while §6 says something else is how it shows up. #>
    param($Reading)
    switch ($Reading.Status) {
        'Ok' {
            # "at least", not a bare rate, when attribution failed at least as
            # often as it succeeded (ruling 3). The number is the same; the
            # claim it supports is not, and the cell is where a reader forms it.
            $lead = if ($Reading.LowerBound) { 'at least {0}' } else { '{0}' }
            $cell = (($lead + ' per delivered slice ({1} report(s) / {2} slice(s))') -f
                (Format-Number $Reading.Rate 3), @($Reading.InWindow).Count, $Reading.DeliverySlices)
            if (@($Reading.PerTier).Count -gt 0) {
                $cell += (' — ' + ((@($Reading.PerTier) | ForEach-Object { '{0} {1}' -f $_.Tier, $_.Count }) -join ', '))
            }
            if ($Reading.LowerBound) {
                $cell += (' — **lower bound**: {0} marked report(s) name no merge, see §6' -f @($Reading.Unattributed).Count)
            }
            return $cell
        }
        'NoConvention' { return '`unmeasured` — feed unmarked, see §6' }
        'NotConfigured' { return '`unmeasured` — feed not read (`-NoDefectFeed`)' }
        'Truncated' { return '`unmeasured` — feed read truncated, see §6' }
        'PreConvention' {
            # Three sub-forms of one claim — "this window is not established to
            # be inside the marking convention" — and they are distinguished
            # because a reader who cannot tell "adopted, and this window is
            # older" from "never adopted" cannot tell which one somebody has to
            # go fix.
            switch ($Reading.ConventionCase) {
                'unset' { return '`unmeasured` — the marking convention has no adopted date yet, see §6' }
                'no-merge' { return '`unmeasured` — no merged PR in the window to date the convention against, see §6' }
                default {
                    return ('`unmeasured` — window predates the marking convention (adopted {0})' -f
                        (Format-DefectDate $Reading.ConventionStart))
                }
            }
        }
        'LagFloor' { return '`unmeasured` — window younger than the report lag floor, see §6' }
        'Unattributable' {
            return ('`unmeasured` — {0} marked report(s), none naming a merge, see §6' -f @($Reading.Unattributed).Count)
        }
        'FeedSilent' { return '`unmeasured` — no player report filed since the window closed, see §6' }
        default { return '`unmeasured` — feed unreachable, see §6' }
    }
}

function Format-DefectDate {
    param($Value)
    if ($null -eq $Value) { return 'not set' }
    return ([datetime]$Value).ToString('yyyy-MM-dd')
}

function Get-DefectEvidenceLines {
    <# The facts every branch that actually READ the feed owes the reader,
       whatever its status: what was marked but unattributable (ruling 3), the
       observed report lag that L is supposed to be re-set from (ruling 6), and
       whether the feed has produced anything at all since the window closed.

       These are appended rather than folded into each status sentence because
       they are true independently of which guard fired, and a fact that is
       re-stated per branch is a fact that drifts per branch. #>
    param($Reading)

    $lines = @()

    if (@($Reading.Unattributed).Count -gt 0) {
        $head = '  - **{0} marked report(s) name no merge and are in no window.** They are counted ' +
            'here and excluded from every rate, numerator and denominator alike: a report nobody ' +
            'could bisect is a real escaped defect with an unknown address, and folding it into a ' +
            'window would attribute it to merges that may not have caused it.'
        $lines += ($head -f @($Reading.Unattributed).Count)
        foreach ($u in @($Reading.Unattributed)) {
            $lines += ('    - [#{0}]({1}) — {2} _(unattributed)_' -f $u.Number, $u.Url, $u.Title)
        }
    }

    if (@($Reading.ObservedLags).Count -gt 0) {
        $days = @($Reading.ObservedLags | ForEach-Object { [double]$_.Days })
        $head = '  - **Observed report lag: {0} sample(s), median {1} d, max {2} d.** The floor is ' +
            'currently {3} d, set by judgement because there was no marked data to set it from. ' +
            'These are that data; the floor is re-set from them once five marked reports exist.'
        $lines += ($head -f @($days).Count, (Format-Number (Get-Median -Values $days) 2),
            (Format-Number ($days | Sort-Object)[-1] 2), $Reading.LagFloorDays)
        foreach ($l in @($Reading.ObservedLags)) {
            $lines += ('    - [#{0}] reported {1} d after #{2} merged' -f $l.Number, (Format-Number $l.Days 2), $l.MergedPr)
        }
    }

    if ($Reading.CommentMarked -gt 0) {
        $lines += ('  - {0} of the marked report(s) carry the marker in a **comment** rather than the body — the form triage posts it in.' -f
            $Reading.CommentMarked)
    }

    return $lines
}

function Get-DefectLivenessLine {
    <# Ruling 6's feed-liveness statement, as one sentence with the dates in it.
       A reader who is told a window is unmeasurable is owed the two timestamps
       that decide it, because those are what make the claim checkable. #>
    param($Reading)
    if ($null -eq $Reading.LastMerge) { return @() }
    $newest = if ($null -eq $Reading.FeedNewest) { 'never' } else { (Format-DefectDate $Reading.FeedNewest) }
    return @('  - Window last merged **{0}** ({1} d ago); newest discussion in the feed: **{2}**.' -f
        (Format-DefectDate $Reading.LastMerge), (Format-Number $Reading.DaysSinceLastMerge 1), $newest)
}

function Get-EscapedDefectReason {
    <# The §6 sentence. Every branch states what THIS run read, and none of them
       promises that a checkpoint will fix it, because no checkpoint marks a
       discussion. The row that used to live here promised exactly that. #>
    param($Reading, [string]$Repository)

    switch ($Reading.Status) {
        'Ok' {
            # Each template is built, THEN formatted. `'a' + 'b {0}' -f $x` binds
            # -f to the last segment alone and ships the other segments' braces
            # as literal text — which is what the first run of this function did.
            $head = '- **Escaped defect rate per tier — no longer unmeasured.** The reading is in §2. ' +
                'The player-report feed (GitHub Discussions on `{0}`) carries {1} marked report(s) ' +
                'across {2} discussion(s); {3} name a merge in this window, over {4} delivered ' +
                'delivery slice(s).'
            $lines = @($head -f $Repository, $Reading.MarkedTotal, $Reading.Scanned,
                @($Reading.InWindow).Count, $Reading.DeliverySlices)
            if (@($Reading.InWindow).Count -gt 0) {
                foreach ($r in @($Reading.InWindow)) {
                    $lines += ('  - [#{0}]({1}) — {2} _(escaped through #{3})_' -f $r.Number, $r.Url, $r.Title, $r.MergedPr)
                }
            } else {
                # The conditions under which this sentence is allowed to be
                # printed are now four, not one, and they are named here because
                # a measured zero is the single most misreadable cell on the
                # page: it is the one an approving reader wants to see.
                $zeroNote = '  A rate of 0 here is a **measurement**, not a blank: the feed is legible, ' +
                    'it was read in full, the window''s merges are inside the marking convention, ' +
                    'the window is older than the {0}-day report lag floor, and the feed has ' +
                    'produced discussions since it closed. No marked report names a merge in this ' +
                    'window; {1} marked report(s) name merges outside it.'
                $lines += ($zeroNote -f $Reading.LagFloorDays, $Reading.Elsewhere)
            }
            if ($Reading.LowerBound) {
                $lb = '  This rate is a **lower bound**, not a rate: {0} marked report(s) could not be ' +
                    'attributed to any merge, which is at least as many as the {1} that could. ' +
                    'Attribution difficulty belongs in the uncertainty, not in a smaller number.'
                $lines += ($lb -f @($Reading.Unattributed).Count, @($Reading.InWindow).Count)
            }
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            $lines += (Get-DefectLivenessLine -Reading $Reading)
            return $lines
        }
        'PreConvention' {
            $lines = @()
            switch ($Reading.ConventionCase) {
                'unset' {
                    $head = '- **Escaped defect rate per tier.** `unmeasured` — the marking convention ' +
                        'has **no adopted date**. Triage marks player reports from the day the sweep ' +
                        'starts; until that date is recorded here, no window can be shown to lie ' +
                        'inside the convention, and a window that cannot be shown to lie inside it ' +
                        'is not measured against it.'
                    $lines += $head
                    $lines += ''
                    $why = '  This is deliberately the strict end of the placeholder. The alternative — ' +
                        'compute anyway, and let the date arrive later — is the failure this guard ' +
                        'exists for: one marker comment posted on one old discussion would promote ' +
                        'every window in the archive to a measured `0.000`, an assertion about ' +
                        'merges nobody triaged, printed as quality credit.'
                    $lines += $why
                }
                'no-merge' {
                    $head = '- **Escaped defect rate per tier.** `unmeasured` — **no PR in this window ' +
                        'merged**, so the window has no date to measure the marking convention or ' +
                        'the report lag floor from. Both guards are anchored on the last merge; ' +
                        'with no merge there is nothing to anchor, and an unanchored window reads ' +
                        'unmeasured rather than zero.'
                    $lines += $head
                }
                default {
                    $head = '- **Escaped defect rate per tier.** `unmeasured`, **permanently**, and the ' +
                        'reason is structural. This window''s last merge is {0}; the marking ' +
                        'convention was adopted {1}. Every merge in it shipped before anyone was ' +
                        'triaging player reports against merges, so nothing in it **could** have ' +
                        'been marked.'
                    $lines += ($head -f (Format-DefectDate $Reading.LastMerge), (Format-DefectDate $Reading.ConventionStart))
                    $lines += ''
                    $why = '  There is no backfill and there will not be one: nobody can bisect a ' +
                        'weeks-old report to a merge now, and the result would be a history of our ' +
                        'own guesses presented as measurement. `unmeasured` with this reason is the ' +
                        'honest end state for this window, not a gap waiting to be filled.'
                    $lines += $why
                }
            }
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            $lines += (Get-DefectLivenessLine -Reading $Reading)
            return $lines
        }
        'LagFloor' {
            $head = '- **Escaped defect rate per tier.** `unmeasured` — this window is **younger than ' +
                'the {0}-day report lag floor**. Its last merge was {1} ({2} d ago), so the ' +
                'population of reports about it has not arrived yet. A count of escaped defects ' +
                'taken now would be a statement about the calendar rather than about the code, and ' +
                '`0` is the specific wrong answer it would give.'
            $lines = @($head -f $Reading.LagFloorDays, (Format-DefectDate $Reading.LastMerge),
                (Format-Number $Reading.DaysSinceLastMerge 1))
            $lines += ''
            $lines += ('  Re-run this window on or after **{0}** and the row computes.' -f
                (Format-DefectDate ($Reading.LastMerge).AddDays($Reading.LagFloorDays)))
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            $lines += (Get-DefectLivenessLine -Reading $Reading)
            return $lines
        }
        'Unattributable' {
            $head = '- **Escaped defect rate per tier.** `unmeasured` — the feed **is** marked and ' +
                '**nothing in it names a merge**: all {0} marked report(s) are `unattributed`. ' +
                'Triage is running, so this is not the unmarked-feed blank; but with no report ' +
                'attributable to any merge, no window has a numerator, and the rate for this one ' +
                'would be `at least 0 per delivered slice` — a sentence true of every window that ' +
                'has ever existed.'
            $lines = @($head -f @($Reading.Unattributed).Count)
            $lines += ''
            $why = '  This is the attribution half of the marker failing rather than the marking ' +
                'half, and it is a different thing to go fix: the reports exist and somebody read ' +
                'them, but nobody could identify the merge. Executor can be asked to bisect them ' +
                '(ruling 1); until one is attributed, this row has nothing to divide.'
            $lines += $why
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            $lines += (Get-DefectLivenessLine -Reading $Reading)
            return $lines
        }
        'FeedSilent' {
            $head = '- **Escaped defect rate per tier.** `unmeasured` — **no discussion has been ' +
                'created in the feed since this window closed.** The window''s last merge was {0}, ' +
                'and the newest of the {1} discussion(s) read is {2}. The feed is legible and past ' +
                'both guards, but it has been silent over the entire period in which a report about ' +
                'these merges would have been filed.'
            $newest = if ($null -eq $Reading.FeedNewest) { 'older still — the feed is empty' } else { (Format-DefectDate $Reading.FeedNewest) }
            $lines = @($head -f (Format-DefectDate $Reading.LastMerge), $Reading.Scanned, $newest)
            $lines += ''
            $why = '  Silence is not a finding. A dead feed reading as zero escaped defects is the ' +
                'same false statement as an unread one, and it is the statement this instrument was ' +
                'built to stop making. The row computes as soon as the feed shows any activity after ' +
                'the window''s last merge.'
            $lines += $why
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            return $lines
        }
        'NoConvention' {
            $head = '- **Escaped defect rate per tier.** `unmeasured`, and the reason is structural ' +
                'rather than a checkpoint that has not arrived. The raw feed exists and was read: ' +
                '**{0} discussion(s)** on `{1}`, which is where player reports actually land — ' +
                'bodies **and** comments, which is where triage posts the marker. **None of them ' +
                'carries the `exo-defect: escaped #<pr>` or `exo-defect: escaped unattributed` ' +
                'marker** this row computes from, and nothing else in a discussion says which merge ' +
                'a defect escaped through.'
            $lines = @(
                ($head -f $Reading.Scanned, $Repository),
                '',
                ('  Both halves of the marker are load-bearing. Unmarked, the feed is a mixed ' +
                    'channel — feature requests, quest data, "how do I", and real regressions in ' +
                    'one unlabelled list — so a count of it would report ideas as defects. ' +
                    'Unattributed, a report cannot be placed in a window or a tier at all: an ' +
                    'escaped defect is reported LATER than the merge that caused it, usually in ' +
                    'some other window, so "discussions opened during the window" is the wrong ' +
                    'population.'),
                '',
                ('  **No M-checkpoint changes this.** A checkpoint marks no discussion. The row ' +
                    'becomes readable when triage starts marking reports — the code path is live, ' +
                    'reads bodies and comments, and runs on every run — **and** when the window ' +
                    'also clears the marking-convention start date and the report lag floor. From ' +
                    'that point a rate of 0 is a finding rather than a blank; before it, a 0 would ' +
                    'be silence wearing the costume of a measurement.'),
                '',
                ('  Until then `whole-sequence-auth`, which `DECISIONS.md` defines as judged by GWR ' +
                    'and ACCR *stated net of escaped defect rate*, has no measurable quality cost ' +
                    'to be stated net of. That bounds what any graduation ruling on it can honestly ' +
                    'claim, and it is stated here so the bound is not rediscovered.')
            )
            $lines += (Get-DefectLivenessLine -Reading $Reading)
            return $lines
        }
        'NotConfigured' {
            return @(
                ('- **Escaped defect rate per tier.** `unmeasured` — this run was given ' +
                    '`-NoDefectFeed`, so the player-report feed was **not read at all**. Nothing ' +
                    'may be inferred from the blank; re-run without the switch to read it.')
            )
        }
        'Truncated' {
            $head = '- **Escaped defect rate per tier.** `unmeasured` — the feed read did **not ' +
                'complete** ({0} discussion(s) scanned): either the page cap was reached with pages ' +
                'left, or a discussion carried more comments than were fetched and no marker in the ' +
                'part that was read. Either way the marked reports found so far are a **lower ' +
                'bound** and are not reported as a rate. An unread tail is not an unmarked one, and ' +
                'an undercount of escaped defects is the one direction that flatters the experiment ' +
                'this metric is the stated cost of. Reason: `{1}`'
            $lines = @($head -f $Reading.Scanned, $Reading.FeedReason)
            $lines += (Get-DefectEvidenceLines -Reading $Reading)
            return $lines
        }
        default {
            $head = '- **Escaped defect rate per tier.** `unmeasured` — the player-report feed read ' +
                '**failed**, so this row is *unknown* rather than zero, and it is unknown for a ' +
                'different reason than the one below it. Reason: `{0}`'
            return @($head -f $Reading.FeedReason)
        }
    }
}

function Get-WindowClaimViolations {
    <# DRA-127's guard. Scans text that is about to be published for a sentence
       naming a precondition the measured window SATISFIES — the shape of the
       retired escaped-defect reason, which said the tier model "did not exist
       during this window" and kept saying it after the tier model landed.

       Deliberately a scan of the rendered document rather than of the rows this
       script knows about today: the next hardcoded reason will be written by
       someone who has not read this function, and a must-list of known rows
       cannot see a sentence nobody registered (trap 34). #>
    param([string]$Text, $WindowStart, $WindowEnd)

    if (@($script:WindowClaimChecks).Count -eq 0) {
        throw 'Get-WindowClaimViolations called with an EMPTY check list — a scan with no patterns reports clean (trap 78).'
    }
    if ([string]::IsNullOrEmpty($Text)) { return @() }

    $out = @()
    foreach ($check in $script:WindowClaimChecks) {
        $m = [regex]::Match($Text, $check.Pattern)
        if (-not $m.Success) { continue }
        if (-not (& $check.Satisfied $WindowStart $WindowEnd)) { continue }
        $out += , [pscustomobject]@{
            Name     = [string]$check.Name
            Why      = [string]$check.Why
            Sentence = $m.Value.Trim()
        }
    }
    return $out
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

function Resolve-OutPath {
    <# `Join-Path $RepoRoot $absolute` silently produces `C:\repo\C:\temp\…`, which
       fails with a syntax error rather than writing where it was told. The defaults
       are repo-relative; anything a caller passes may not be. #>
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path $RepoRoot $Path)
}

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

    # 7. Output paths. `Join-Path $RepoRoot $absolute` produces `C:\repo\C:\temp\…`
    #    and dies with a path-syntax error instead of writing where it was told.
    $abs = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), 'exo-out.md')
    Assert ((Resolve-OutPath $abs) -eq $abs) 'An absolute -Out path was joined onto the repo root.'
    Assert ((Resolve-OutPath 'docs/ops/exo-dashboard.md') -ne 'docs/ops/exo-dashboard.md') `
        'A relative -Out path was not made repo-relative.'

    # 8. An empty needle list must THROW rather than report clean (trap 78's
    #    whole lesson: the guard that matched nothing said the file was fine).
    $threw = $false
    try { Test-ContainsAny -Text 'anything' -Needles @() | Out-Null } catch { $threw = $true }
    Assert $threw 'An empty needle list reported clean instead of throwing.'

    # 9. Paperclip results are a VALUE, and the three worlds are told apart.
    #    The Unreachable arm is a REAL socket to a closed port, not a mock: the
    #    defect was that a refused connection rendered as a measurement, and a
    #    stubbed exception would not have proved the live path produces one.
    $savedFailures = $script:PaperclipFailures
    $script:PaperclipFailures = @()

    # The null checks are the point, not defensive noise: returning $null IS the
    # pre-fix behaviour, so this arm has to report it as a named failure rather
    # than die on a property lookup.
    $noKey = Invoke-Paperclip -Base 'http://127.0.0.1:9' -Key '' -Path '/api/x'
    Assert ($null -ne $noKey) 'A missing key answered $null — absence again, not a value.'
    Assert ($null -ne $noKey -and $noKey.Status -eq 'NotConfigured') 'A missing key did not read as NotConfigured.'
    Assert (@($script:PaperclipFailures).Count -eq 0) 'NotConfigured registered a failure — it is not one.'

    $dead = Invoke-Paperclip -Base 'http://127.0.0.1:9' -Key 'k' -Path '/api/x' -TimeoutSec 5
    Assert ($null -ne $dead) 'An unreachable GET answered $null — the defect DRA-80 exists for.'
    Assert ($null -ne $dead -and $dead.Status -eq 'Unreachable') 'A dead base did not read as Unreachable.'
    Assert ($null -ne $dead -and $dead.Unreachable) 'An Unreachable result did not say so.'
    Assert ($null -eq $dead -or $null -eq $dead.Data) 'An Unreachable result carried data.'
    Assert (@($script:PaperclipFailures).Count -eq 1) 'An unreachable GET was not registered as a failure.'
    Assert ((Get-PaperclipFailureSummary).Length -gt 0) 'An unreachable run produced no reason to print.'
    Assert ((Format-PaperclipUnmeasured) -notmatch '^\s*`?0`?\s*$') 'An unmeasured cell rendered as a zero.'
    Assert ((Format-PaperclipUnmeasured) -match 'unreachable') 'An unreachable cell did not name the reason.'
    Assert ((Format-PaperclipUnmeasured -Fallback 'no Paperclip record') -match 'unreachable') `
        'An unreachable run rendered a row as "no record" — the two are different claims.'

    $deadFailures = $script:PaperclipFailures
    $script:PaperclipFailures = @()
    Assert ((Format-PaperclipUnmeasured -Fallback 'no Paperclip record') -match 'no Paperclip record') `
        'A reachable run with no record did not say "no record".'

    # 10. The refusal predicate — a -Baseline run with an unreachable input does
    #     not freeze. All four corners, because a guard that only ever says yes
    #     is indistinguishable from a guard that is broken open (trap 34).
    Assert (Test-BaselineRefused -Baseline $true -NoPaperclip $false -Failures $deadFailures) `
        'A -Baseline run with an UNREACHABLE Paperclip read was allowed to freeze.'
    Assert (-not (Test-BaselineRefused -Baseline $true -NoPaperclip $false -Failures @())) `
        'A clean -Baseline run was refused.'
    Assert (-not (Test-BaselineRefused -Baseline $true -NoPaperclip $true -Failures $deadFailures)) `
        '-NoPaperclip, the explicit "these are unmeasured" door, was refused.'
    Assert (-not (Test-BaselineRefused -Baseline $false -NoPaperclip $false -Failures $deadFailures)) `
        'A non-baseline run was refused — it freezes nothing, so it has nothing to poison.'

    # 11. And the WRITER honours it: the file must not appear on disk. Asserting
    #     the predicate alone would pass on a writer that never calls it.
    $probe = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(),
        ('exo-baseline-selftest-{0}.json' -f [guid]::NewGuid().ToString('N')))
    $refused = $false
    try { Write-BaselineFreeze -Path $probe -Current @{ gwr = 0.51 } -NoPaperclip $false -Failures $deadFailures }
    catch { $refused = $true }
    Assert $refused 'Write-BaselineFreeze did not throw on an unreachable input.'
    Assert (-not (Test-Path $probe)) 'Write-BaselineFreeze REFUSED and wrote the file anyway.'

    try {
        Write-BaselineFreeze -Path $probe -Current @{ gwr = 0.49 } -NoPaperclip $false -Failures @()
        Assert (Test-Path $probe) 'Write-BaselineFreeze refused a clean run — the guard is stuck shut.'
    } catch { Assert $false "Write-BaselineFreeze threw on a clean run: $($_.Exception.Message)" }
    finally { if (Test-Path $probe) { Remove-Item $probe -Force } }

    $script:PaperclipFailures = $savedFailures

    # 12. The printed recipe reproduces the file it is printed in (trap 74): a
    #     dashboard headed "DRA-70/71/72" whose command omits -WindowLabel
    #     regenerates without the work-item names, and the diff that catches it
    #     says nothing about metrics.
    $withLabel = Get-ReproduceCommand -FromPr 580 -ToPr 607 -WindowLabel 'DRA-70/71/72' -Baseline $true -NoPaperclip $false
    Assert ($withLabel -match "-WindowLabel 'DRA-70/71/72'") 'The reproduce command dropped -WindowLabel.'
    Assert ($withLabel -match '-Baseline') 'The reproduce command dropped -Baseline.'
    Assert ($withLabel -notmatch '-NoPaperclip') 'The reproduce command invented -NoPaperclip.'
    $bare = Get-ReproduceCommand -FromPr 580 -ToPr 607 -WindowLabel '' -Baseline $false -NoPaperclip $true
    Assert ($bare -notmatch '-WindowLabel') 'The reproduce command emitted an empty -WindowLabel.'
    Assert ($bare -match '-NoPaperclip') 'The reproduce command dropped -NoPaperclip.'
    Assert ((Get-ReproduceCommand -FromPr 1 -ToPr 2 -WindowLabel "it's a window" -Baseline $false -NoPaperclip $false) `
            -match "-WindowLabel 'it''s a window'") 'A label containing a quote was not escaped for the shell.'
    Assert ((Get-ReproduceCommand -FromPr 1 -ToPr 2 -WindowLabel '' -Baseline $false -NoPaperclip $false -NoDefectFeed $true) `
            -match '-NoDefectFeed') 'The reproduce command dropped -NoDefectFeed, which decides whether the defect row is read at all.'
    Assert ((Get-ReproduceCommand -FromPr 1 -ToPr 2 -WindowLabel '' -Baseline $false -NoPaperclip $false -NoDefectFeed $false) `
            -notmatch '-NoDefectFeed') 'The reproduce command invented -NoDefectFeed.'

    # 13. Tiers are READ, never invented. A tier this script guessed would be a
    #     tier no ruling ever set, and the per-tier breakdown would be fiction.
    $decoy = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(),
        ('exo-decisions-selftest-{0}.md' -f [guid]::NewGuid().ToString('N')))
    try {
        Write-Utf8NoBom -Path $decoy -Text (@(
                '## 2026-09-14 — DRA-77 / M0-4 (merge-sync): eight calls',
                '',
                'Tier T1 · one workflow + three scripts · governing plan: DRA-73.',
                'A T2 ruling would be needed to undo this.',
                '',
                '## 2026-09-15 — DRA-87: the last three "log-only" labels',
                '',
                'No tier line in this entry at all.',
                ''
            ) -join "`n")
        $tiers = Get-TierMap -DecisionsPath $decoy
        Assert ($tiers['DRA-77'] -eq 'T1') 'Get-TierMap did not read the tier off a DECISIONS entry.'
        Assert (-not $tiers.ContainsKey('DRA-87')) 'Get-TierMap invented a tier for an entry that carries none.'
        Assert ((Get-TierMap -DecisionsPath ($decoy + '.missing')).Count -eq 0) 'A missing DECISIONS.md produced tiers anyway.'
    } finally { if (Test-Path $decoy) { Remove-Item $decoy -Force } }

    # 14. The defect marker fires on a marked report and stays QUIET on a real
    #     unmarked one. The negative is a live discussion title from the feed —
    #     an unlabelled channel full of genuine bug reports is exactly why the
    #     marker, and not the word "bug", is what this row counts.
    $marked = "Kills stopped counting after 1.9.2.`n`nexo-defect: escaped #630`n"
    Assert ([regex]::IsMatch($marked, $script:DefectMarkerPattern)) 'The defect marker missed a marked report.'
    Assert ([regex]::Match($marked, $script:DefectMarkerPattern).Groups[1].Value -eq '630') 'The defect marker did not capture the merge it names.'
    Assert (-not [regex]::IsMatch(
            "Bug: Wizard kill in Lower Guk hall triggers an arch magi respawn chip`n`nSeen twice tonight.",
            $script:DefectMarkerPattern)) 'The defect marker fired on an unmarked bug report — the feed is full of those.'
    Assert (-not [regex]::IsMatch("exo-defect: escaped (no pr)", $script:DefectMarkerPattern)) `
        'Free text after the marker was accepted as a marker — the form has to be exact or the count is a guess.'

    # 14b. DRA-133 ruling 3: `unattributed` is an ACCEPTED form. Before this,
    #      "marked but not bisectable" was unrepresentable, so triage had one
    #      option for it — leave it unmarked — and an unmarked defect is an
    #      undercount, the one direction that flatters the experiment.
    $unattr = "Zoning into Sebilis drops the client.`n`nexo-defect: escaped unattributed`n"
    Assert ([regex]::IsMatch($unattr, $script:DefectMarkerPattern)) `
        'The `unattributed` marker form was rejected — a defect nobody can bisect would go unrecorded.'
    Assert (-not [regex]::Match($unattr, $script:DefectMarkerPattern).Groups[1].Success) `
        'The `unattributed` form captured a merge number — it names no merge and must belong to no window.'
    Assert ([regex]::Match($unattr, $script:DefectMarkerPattern).Groups[2].Success) `
        'The `unattributed` form did not identify itself as unattributed.'
    Assert (-not [regex]::IsMatch("exo-defect: escaped unattributed #630", $script:DefectMarkerPattern)) `
        'A marker that is BOTH unattributed and attributed was accepted — that is two claims in one line.'

    # 15. The feed walk, against a FAKE page source: the loop is what has to be
    #     right, and -SelfTest must not need a network for it.
    $script:DefectFeedFailures = @()
    function New-FeedNode([int]$Number, [string]$Body, $Comments = $null, [bool]$MoreComments = $false, [string]$Created = '2026-09-15T00:00:00Z') {
        $node = [pscustomobject]@{
            number = $Number; title = "d$Number"; url = "https://x/$Number"
            createdAt = $Created; body = $Body; category = [pscustomobject]@{ name = 'Q&A' }
        }
        # `comments` is added only when the case is about comments, so the
        # common shape — a discussion the query returned without them — stays
        # covered. StrictMode turns a missing property into a crash, and the
        # feed is mostly comment-less discussions.
        if ($null -ne $Comments -or $MoreComments) {
            $cnodes = @()
            foreach ($c in @($Comments)) {
                if ($null -eq $c) { continue }
                $cnodes += , [pscustomobject]@{
                    body = [string]$c; createdAt = '2026-09-20T00:00:00Z'
                    url = "https://x/$Number#c"; author = [pscustomobject]@{ login = 'scribe' }
                }
            }
            $node | Add-Member -NotePropertyName comments -NotePropertyValue ([pscustomobject]@{
                    totalCount = $cnodes.Count
                    pageInfo = [pscustomobject]@{ hasNextPage = $MoreComments }
                    nodes = $cnodes
                })
        }
        return $node
    }
    function New-FeedPage($Nodes, [bool]$More, [string]$Cursor = 'c1') {
        return [pscustomobject]@{
            nodes = @($Nodes)
            pageInfo = [pscustomobject]@{ hasNextPage = $More; endCursor = $Cursor }
        }
    }
    $twoPages = {
        param($Cursor)
        if ([string]::IsNullOrEmpty($Cursor)) {
            return (New-FeedPage @((New-FeedNode 1 'no marker here'), (New-FeedNode 2 "exo-defect: escaped #630")) $true 'PAGE2')
        }
        if ($Cursor -ne 'PAGE2') { throw "the walk did not carry the cursor forward (got '$Cursor')." }
        return (New-FeedPage @((New-FeedNode 3 "exo-defect: escaped #999")) $false)
    }
    $walked = Get-DefectReports -Repository 'o/n' -PageSource $twoPages
    Assert ($walked.Status -eq 'Ok') 'A complete two-page feed walk did not report Ok.'
    Assert ($walked.Scanned -eq 3) "The feed walk scanned $($walked.Scanned) node(s) rather than 3 — page 2 was dropped."
    Assert (@($walked.Reports).Count -eq 2) 'The feed walk did not find both marked reports.'
    Assert (@($walked.Reports | Where-Object { $_.MergedPr -eq 630 }).Count -eq 1) 'A marked report lost the merge it names.'

    $blowsUp = { param($Cursor) throw 'no network' }
    $dead2 = Get-DefectReports -Repository 'o/n' -PageSource $blowsUp
    Assert ($dead2.Status -eq 'Unreachable') 'A failed feed read did not report Unreachable.'
    Assert (@($dead2.Reports).Count -eq 0) 'A failed feed read carried reports.'
    Assert (@($script:DefectFeedFailures).Count -eq 1) 'A failed feed read was not registered as a failure.'

    $never = { param($Cursor) return (New-FeedPage @((New-FeedNode 9 'x')) $true 'again') }
    $capped = Get-DefectReports -Repository 'o/n' -PageSource $never
    Assert ($capped.Status -eq 'Truncated') 'A feed longer than the page cap did not report Truncated.'
    Assert ($capped.Scanned -eq $script:DefectFeedPageCap) 'The page cap did not bound the walk.'

    $off = Get-DefectReports -Repository 'o/n' -Enabled $false
    Assert ($off.Status -eq 'NotConfigured') '-NoDefectFeed did not read as NotConfigured.'
    $script:DefectFeedFailures = @()

    # 15b. Ruling 5: the marker is a triage COMMENT. A read that matches bodies
    #      only turns a marked feed into an unmarked one, which is the worst of
    #      the failure modes here — the sweep runs, the instrument reports
    #      nothing, and nothing reports that anything is missing.
    $commentFeed = {
        param($Cursor)
        return (New-FeedPage @(
                (New-FeedNode 11 'Kills stopped counting.' @('triaged', "exo-defect: escaped #630")),
                (New-FeedNode 12 'Zoning crash.' @("exo-defect: escaped unattributed")),
                (New-FeedNode 13 'A feature request.' @('nice idea'))
            ) $false)
    }
    $cmt = Get-DefectReports -Repository 'o/n' -PageSource $commentFeed
    Assert ($cmt.Status -eq 'Ok') 'A complete feed whose markers live in comments did not report Ok.'
    Assert (@($cmt.Reports).Count -eq 2) `
        'A marker posted as a COMMENT was invisible — the triage ritual would run and produce nothing readable.'
    Assert (@($cmt.Reports | Where-Object { $_.MergedPr -eq 630 }).Count -eq 1) 'A comment marker lost the merge it names.'
    Assert (@($cmt.Reports | Where-Object { $null -eq $_.MergedPr }).Count -eq 1) 'The unattributed comment marker was dropped or given a merge.'
    Assert (@($cmt.Reports | Where-Object { $_.MarkerSource -eq 'comment' }).Count -eq 2) 'A comment marker was not recorded as coming from a comment.'
    # Filtered, not indexed: when this regresses, Reports is EMPTY, and an
    # index into an empty array crashes the suite instead of reporting which
    # assertion failed. A test that cannot survive its own failure case is a
    # test that reports a stack trace where a sentence was needed.
    Assert (@($cmt.Reports | Where-Object { $_.Number -eq 11 -and $_.MarkedBy -eq 'scribe' }).Count -eq 1) `
        'The comment marker lost its author — attribution of the TRIAGE is half of why ruling 1 chose a comment.'
    Assert ($null -ne $cmt.Newest) 'The feed walk did not record the newest discussion — feed liveness has nothing to read.'

    # 15c. A body marker still wins, and a discussion with an unread comment
    #      tail AND no marker in what was read is TRUNCATED, not unmarked.
    $mixed = {
        param($Cursor)
        return (New-FeedPage @(
                (New-FeedNode 21 "exo-defect: escaped #631" @('chatter') $true),
                (New-FeedNode 22 'no marker in the part we read' @('chatter') $true)
            ) $false)
    }
    $mix = Get-DefectReports -Repository 'o/n' -PageSource $mixed
    Assert ($mix.Status -eq 'Truncated') `
        'A discussion with an unread comment tail and no marker read as unmarked — an unread tail is not an unmarked one.'
    Assert (@($mix.Reports).Count -eq 1) 'The body-marked discussion was lost when another discussion truncated.'
    Assert ($mix.Reason -match '22') 'The truncation did not name the discussion whose tail went unread.'
    Assert ($mix.Reason -notmatch '#21[^0-9]') `
        'A discussion whose marker was already found was reported as truncated — its tail could only repeat the report it already carries.'
    $script:DefectFeedFailures = @()

    # 16. The reading, and the distinction DRA-127 is about: a LEGIBLE feed with
    #     no in-window report is a measured 0; an ILLEGIBLE feed is not a 0 at
    #     all. Collapsing those two is what let one sentence stand in for both.
    #
    #     Every case below states the four things a measured reading depends on
    #     — convention start, window merge dates, the clock, and feed liveness —
    #     because after DRA-133 a rate that does not state them is not a
    #     reading, it is a coincidence. The fixture dates: the window merges
    #     2026-09-10/12, the convention started 2026-09-01, the feed last spoke
    #     2026-09-20, and "now" is 2026-10-20, five weeks past the merges.
    $conventionStartSaved = $script:DefectConventionStart
    $fixtureConvention = ConvertTo-Utc '2026-09-01T00:00:00Z'
    $fixtureNow = ConvertTo-Utc '2026-10-20T00:00:00Z'
    $fixtureAlive = ConvertTo-Utc '2026-09-20T00:00:00Z'
    $script:DefectConventionStart = $fixtureConvention

    $fakeFacts = @(
        ([pscustomobject]@{ Number = 630; Dra = 'DRA-77'; Merged = (ConvertTo-Utc '2026-09-10T00:00:00Z') }),
        ([pscustomobject]@{ Number = 631; Dra = 'DRA-87'; Merged = (ConvertTo-Utc '2026-09-12T00:00:00Z') })
    )
    $fakeDelivery = @(1, 2, 3, 4)   # four delivered slices
    $tierMap = @{ 'DRA-77' = 'T1' }

    function New-FakeFeed($Reports, $Newest = $null, [int]$Scanned = 166, [string]$Status = 'Ok') {
        return [pscustomobject]@{
            Status = $Status; Scanned = $Scanned; Reason = ''
            Newest = $Newest; Reports = @($Reports)
        }
    }
    function New-FakeReport([int]$Number, $MergedPr, $Created = $null) {
        if ($null -eq $Created) { $Created = (ConvertTo-Utc '2026-09-20T00:00:00Z') }
        return [pscustomobject]@{
            Number = $Number; Title = "t$Number"; Url = "u$Number"; Created = $Created
            MergedPr = $MergedPr; MarkerSource = 'comment'; MarkedAt = $Created; MarkedBy = 'scribe'
        }
    }
    function Read-Fixture($Feed) {
        return (Get-EscapedDefectReading -Feed $Feed -Facts $fakeFacts -Delivery $fakeDelivery `
                -TierMap $tierMap -Now $fixtureNow)
    }

    $noConv = Read-Fixture (New-FakeFeed @() $fixtureAlive)
    Assert ($noConv.Status -eq 'NoConvention') 'A readable feed with NO marked report read as a measured rate.'
    Assert ($null -eq $noConv.Rate) 'An unmarked feed produced a RATE — that is the zero this row must never print.'
    Assert ((Format-EscapedDefectCell -Reading $noConv) -notmatch '^\s*`?0') 'An unmeasurable escaped-defect cell rendered as a zero.'
    Assert ((Format-EscapedDefectCell -Reading $noConv) -match 'unmeasured') 'An unmeasurable escaped-defect cell did not say unmeasured.'

    $zero = Read-Fixture (New-FakeFeed @((New-FakeReport 501 4242)) $fixtureAlive)
    Assert ($zero.Status -eq 'Ok') 'A legible feed whose reports name other windows was not treated as measured.'
    Assert ($zero.Rate -eq 0) 'A legible feed with no in-window report did not measure 0 — a finding was reported as a blank.'
    Assert ($zero.Elsewhere -eq 1) 'A report naming a merge outside the window was not counted as such.'
    Assert ((((Get-EscapedDefectReason -Reading $zero -Repository 'o/n') -join ' ') -match 'measurement')) `
        'A measured zero did not say it was a measurement rather than a blank.'
    Assert (-not $zero.LowerBound) `
        'A measured zero from a feed where attribution never failed was labelled a lower bound — that erases the measured-zero finding DRA-127 built.'

    $hit = Read-Fixture (New-FakeFeed @((New-FakeReport 501 630), (New-FakeReport 502 631)) $fixtureAlive)
    Assert ($hit.Rate -eq 0.5) "Two in-window reports over four slices read $($hit.Rate) rather than 0.5."
    Assert ((@($hit.PerTier) | Where-Object { $_.Tier -eq 'T1' }).Count -eq 1) 'The per-tier split lost the tiered report.'
    Assert ((@($hit.PerTier) | Where-Object { $_.Tier -eq 'untiered' }).Count -eq 1) `
        'A report against an untiered work item was given a tier, or dropped.'
    Assert ((Format-EscapedDefectCell -Reading $hit) -notmatch 'unmeasured') 'A measured escaped-defect cell still said unmeasured.'

    # 16a. THE TEST THIS CARD EXISTS FOR (DRA-133, the trap).
    #
    #      One marked report, on one discussion, naming one merge that is not in
    #      this window. Under the pre-DRA-135 reading that single report cleared
    #      `NoConvention` for EVERY window, and this window — whose merges
    #      predate the convention start, and which nobody has triaged — computed
    #      a measured `0.000`. Silence, printed as quality credit for
    #      `whole-sequence-auth`, which `DECISIONS.md` judges net of this term.
    $oldFacts = @(
        ([pscustomobject]@{ Number = 585; Dra = 'DRA-10'; Merged = (ConvertTo-Utc '2026-08-05T00:00:00Z') }),
        ([pscustomobject]@{ Number = 586; Dra = 'DRA-11'; Merged = (ConvertTo-Utc '2026-08-07T00:00:00Z') })
    )
    $oneReportElsewhere = New-FakeFeed @((New-FakeReport 501 4242)) $fixtureAlive
    $unrelated = Get-EscapedDefectReading -Feed $oneReportElsewhere -Facts $oldFacts `
        -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow
    Assert ($unrelated.Status -eq 'PreConvention') `
        'A single marked report naming an out-of-window merge promoted a PRE-CONVENTION window to a measured reading — the DRA-133 trap, re-opened.'
    Assert ($null -eq $unrelated.Rate) `
        'A window whose merges predate the marking convention produced a RATE. No sweep could have marked those merges; the number would be about nobody having looked.'
    Assert ((Format-EscapedDefectCell -Reading $unrelated) -notmatch '(^|[^.\d])0([^.\d]|$)') `
        'The pre-convention cell printed a zero.'
    Assert ((Format-EscapedDefectCell -Reading $unrelated) -match 'predates the marking convention') `
        'The pre-convention cell did not name its reason — an unmeasured row without a reason is the DRA-127 defect.'
    Assert ((Format-EscapedDefectCell -Reading $unrelated) -match '2026-09-01') `
        'The pre-convention cell did not carry the adopted date it is measuring against.'

    # 16b. Ruling 4's placeholder state. Until Scribe's sweep sets a date, NO
    #      window is inside the convention — including one that would otherwise
    #      compute cleanly. This is the arm that holds on the day this lands.
    $script:DefectConventionStart = $null
    $unsetReading = Read-Fixture (New-FakeFeed @((New-FakeReport 501 4242)) $fixtureAlive)
    Assert ($unsetReading.Status -eq 'PreConvention') 'With no adopted convention date, a window still computed.'
    Assert ($unsetReading.ConventionCase -eq 'unset') 'The unset-convention case was not distinguished from a window that predates a set one.'
    Assert ($null -eq $unsetReading.Rate) 'A window computed a rate against a convention that has no start date.'
    Assert ((Format-EscapedDefectCell -Reading $unsetReading) -match 'no adopted date') `
        'The unset-convention cell did not say the convention has no adopted date — the reader cannot tell who has to go fix it.'
    $script:DefectConventionStart = $fixtureConvention

    # 16c. Ruling 6, the lag floor. Same feed, same convention, window closed
    #      two days ago. An escaped defect is reported LATER than the merge that
    #      caused it, so `0/N` today is a fact about the calendar.
    $freshFacts = @(
        ([pscustomobject]@{ Number = 700; Dra = 'DRA-90'; Merged = $fixtureNow.AddDays(-2) })
    )
    $fresh = Get-EscapedDefectReading -Feed (New-FakeFeed @((New-FakeReport 501 4242)) $fixtureNow.AddDays(-1)) `
        -Facts $freshFacts -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow
    Assert ($fresh.Status -eq 'LagFloor') 'A window younger than the report lag floor computed anyway.'
    Assert ($null -eq $fresh.Rate) 'A window younger than the lag floor produced a rate — its reports have not arrived yet.'
    Assert ((Format-EscapedDefectCell -Reading $fresh) -match 'younger than the report lag floor') `
        'The lag-floor cell did not name the lag floor as its reason.'
    Assert ((((Get-EscapedDefectReason -Reading $fresh -Repository 'o/n') -join ' ') -match '14')) `
        'The lag-floor reason did not carry the floor it is applying.'

    # And the floor lets go on its own: the same window, read after L days.
    $aged = Get-EscapedDefectReading -Feed (New-FakeFeed @((New-FakeReport 501 4242)) $fixtureNow.AddDays(20)) `
        -Facts $freshFacts -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow.AddDays(21)
    Assert ($aged.Status -eq 'Ok') 'A window past the lag floor stayed unmeasured — the guard must expire, not latch.'

    # 16d. Ruling 6's second half: feed liveness. Past the convention, past the
    #      floor, feed legible — and not one discussion created since the window
    #      closed. A dead feed reading as zero escaped defects is the same lie
    #      as an unread one.
    $silent = Get-EscapedDefectReading -Feed (New-FakeFeed @((New-FakeReport 501 4242)) (ConvertTo-Utc '2026-09-05T00:00:00Z')) `
        -Facts $fakeFacts -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow
    Assert ($silent.Status -eq 'FeedSilent') `
        'A feed that has produced nothing since the window closed still printed a rate for it.'
    Assert ($null -eq $silent.Rate) 'A silent feed produced a rate.'
    Assert ((Format-EscapedDefectCell -Reading $silent) -match 'no player report filed since') `
        'The feed-silent cell did not name its reason.'
    # A silent feed with a REAL in-window report is a reading, not silence.
    $silentButHit = Get-EscapedDefectReading -Feed (New-FakeFeed @((New-FakeReport 501 630)) (ConvertTo-Utc '2026-09-05T00:00:00Z')) `
        -Facts $fakeFacts -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow
    Assert ($silentButHit.Status -eq 'Ok') `
        'A window WITH a marked in-window report was suppressed by the liveness guard — the guard is against a zero, not against data.'

    # 16e. Ruling 3: unattributed reports are counted, belong to no window, and
    #      turn the cell into a lower bound when they outnumber the attributed.
    $unattrFeed = New-FakeFeed @(
        (New-FakeReport 501 630),
        (New-FakeReport 502 $null),
        (New-FakeReport 503 $null)
    ) $fixtureAlive
    $lb = Read-Fixture $unattrFeed
    Assert ($lb.Status -eq 'Ok') 'A feed with attributed and unattributed reports did not read.'
    Assert (@($lb.Unattributed).Count -eq 2) 'The unattributed reports were dropped — an uncounted defect is an undercount.'
    Assert (@($lb.InWindow).Count -eq 1) 'An unattributed report was placed in a window it names no merge in.'
    Assert ($lb.Rate -eq 0.25) "Unattributed reports entered the rate: 1 in-window over 4 slices read $($lb.Rate) rather than 0.25."
    Assert ($lb.LowerBound) 'Two unattributed against one attributed did not make the cell a lower bound.'
    Assert ((Format-EscapedDefectCell -Reading $lb) -match 'lower bound') 'The lower-bound cell did not say lower bound.'
    Assert ((Format-EscapedDefectCell -Reading $lb) -match 'at least') `
        'The lower-bound cell stated a bare rate — attribution difficulty has to show as uncertainty, not as a smaller number.'
    Assert ((((Get-EscapedDefectReason -Reading $lb -Repository 'o/n') -join ' ') -match 'name no merge and are in no window')) `
        'The unattributed reports got no §6 line of their own (ruling 3).'

    # 16e-ii. And the degenerate lower bound, which is not a reading at all:
    #      every marked report is unattributed, so the cell would have been
    #      "at least 0 per delivered slice" — a sentence true of every window
    #      that has ever existed. The done bar requires a marked ATTRIBUTED
    #      report behind any zero, and this is the arm where there is none.
    $allUnattr = Read-Fixture (New-FakeFeed @((New-FakeReport 502 $null), (New-FakeReport 503 $null)) $fixtureAlive)
    Assert ($allUnattr.Status -eq 'Unattributable') `
        'A feed whose every marked report is unattributed produced a rate — "at least 0" is a shape, not a measurement.'
    Assert ($null -eq $allUnattr.Rate) 'An unattributable feed produced a rate.'
    Assert ((Format-EscapedDefectCell -Reading $allUnattr) -match 'none naming a merge') `
        'The unattributable cell did not distinguish itself from the unmarked-feed blank — triage IS running here.'
    Assert ((Format-EscapedDefectCell -Reading $allUnattr) -notmatch '(^|[^.\d])0([^.\d]|$)') 'The unattributable cell printed a zero.'
    Assert ((Format-EscapedDefectCell -Reading $allUnattr) -ne (Format-EscapedDefectCell -Reading $noConv)) `
        'The unattributable cell and the unmarked-feed cell read the same — they are different things to go fix.'

    # 16f. Ruling 6's observed lag, which is how L stops being a guess.
    $lagFeed = New-FakeFeed @((New-FakeReport 501 630 (ConvertTo-Utc '2026-09-17T00:00:00Z'))) $fixtureAlive
    $lagRead = Read-Fixture $lagFeed
    Assert (@($lagRead.ObservedLags).Count -eq 1) 'No observed report lag was recorded — L can never be re-set from data.'
    Assert (@($lagRead.ObservedLags | Where-Object { $_.Days -eq 7 }).Count -eq 1) `
        "A report filed 2026-09-17 against a merge on 2026-09-10 did not measure 7 d of lag (got: $(@($lagRead.ObservedLags | ForEach-Object { $_.Days }) -join ', '))."
    Assert ((((Get-EscapedDefectReason -Reading $lagRead -Repository 'o/n') -join ' ') -match 'Observed report lag')) `
        'The observed lag was recorded and not reported — a number nobody prints cannot re-set the floor.'

    foreach ($st in @('NoConvention', 'NotConfigured', 'Truncated', 'Unreachable', 'PreConvention', 'LagFloor', 'FeedSilent', 'Unattributable')) {
        $r = [pscustomobject]@{
            Status = $st; FeedStatus = $st; Rate = $null; Scanned = 166; MarkedTotal = 0
            InWindow = @(); Elsewhere = 0; DeliverySlices = 4; PerTier = @(); FeedReason = 'because'
            Unattributed = @(([pscustomobject]@{ Number = 901; Title = 'u1'; Url = 'uu1' }))
            LowerBound = $false; ConventionCase = 'predates'
            ConventionStart = $fixtureConvention; LastMerge = (ConvertTo-Utc '2026-09-12T00:00:00Z')
            DaysSinceLastMerge = 38.0; LagFloorDays = 14; FeedNewest = $fixtureAlive
            ObservedLags = @(); CommentMarked = 0
        }
        Assert ((Format-EscapedDefectCell -Reading $r) -match 'unmeasured') "Status $st did not render as unmeasured."
        Assert ((Format-EscapedDefectCell -Reading $r) -notmatch '(^|[^.\d])0([^.\d]|$)') "Status $st rendered a zero into the §2 cell."
        Assert (@(Get-EscapedDefectReason -Reading $r -Repository 'o/n').Count -gt 0) "Status $st produced no reason to print."
    }
    # The three sub-forms of PreConvention are three different things to go fix,
    # so they are three different sentences.
    $preCells = @('predates', 'unset', 'no-merge') | ForEach-Object {
        $case = $_
        $r = [pscustomobject]@{
            Status = 'PreConvention'; FeedStatus = 'Ok'; Rate = $null; Scanned = 166; MarkedTotal = 0
            InWindow = @(); Elsewhere = 0; DeliverySlices = 4; PerTier = @(); FeedReason = ''
            Unattributed = @(); LowerBound = $false; ConventionCase = $case
            ConventionStart = $(if ($case -eq 'unset') { $null } else { $fixtureConvention })
            LastMerge = $(if ($case -eq 'no-merge') { $null } else { (ConvertTo-Utc '2026-08-12T00:00:00Z') })
            DaysSinceLastMerge = 38.0; LagFloorDays = 14; FeedNewest = $fixtureAlive
            ObservedLags = @(); CommentMarked = 0
        }
        Format-EscapedDefectCell -Reading $r
    }
    Assert ((@($preCells | Sort-Object -Unique)).Count -eq 3) `
        'Two PreConvention sub-forms rendered the same cell — a reader cannot tell which one somebody has to go fix.'

    # Every emitted string is fully substituted. `'a' + 'b {0}' -f $x` binds -f to
    # the last segment only and publishes the rest of the braces verbatim: the
    # first live run of this row printed "**{0} discussion(s)** on `{1}`" into the
    # document, and every assertion above still passed, because they all checked
    # for phrases rather than for the numbers the phrases were supposed to carry.
    foreach ($case in @($noConv, $zero, $hit, $unrelated, $unsetReading, $fresh, $silent, $lb, $lagRead, $allUnattr)) {
        $rendered = ((Get-EscapedDefectReason -Reading $case -Repository 'o/n') -join "`n") + "`n" +
            (Format-EscapedDefectCell -Reading $case)
        Assert ($rendered -notmatch '\{\d+\}') `
            "The escaped-defect text for status $($case.Status) shipped an unsubstituted format placeholder."
    }
    Assert ((Format-EscapedDefectCell -Reading $hit) -match '0\.5') 'The measured cell did not carry the rate it measured.'
    Assert ((((Get-EscapedDefectReason -Reading $noConv -Repository 'o/n') -join ' ') -match '166 discussion')) `
        'The unmeasurable reason did not carry the number of discussions actually read — the count is what makes it a reading rather than an assertion.'

    # 16g. The done bar for the whole family, asserted as a PROPERTY over the
    #      input space rather than as a list of cases: no input — empty feed,
    #      dead feed, old window, fresh window, unset convention, failed read —
    #      may print a `0` into the §2 cell without marked, attributed reports
    #      behind it and every guard cleared.
    #
    #      The permission is re-derived here from the FIXTURE inputs, not read
    #      off the reading's own fields. Asking the implementation whether the
    #      implementation agreed with itself is not a test; this restates the
    #      five conditions in the test's own terms and compares.
    $zeroProbes = @()
    foreach ($convention in @($null, $fixtureConvention)) {
        $script:DefectConventionStart = $convention
        foreach ($factSet in @($fakeFacts, $oldFacts, $freshFacts, @())) {
            $probeLastMerge = $null
            $probeStamps = @($factSet | ForEach-Object { $_.Merged } | Where-Object { $null -ne $_ })
            if ($probeStamps.Count -gt 0) { $probeLastMerge = ($probeStamps | Sort-Object)[-1] }
            $probeNumbers = @($factSet | ForEach-Object { $_.Number })
            foreach ($newest in @($null, (ConvertTo-Utc '2026-09-05T00:00:00Z'), $fixtureAlive)) {
                foreach ($reportSet in @(@(), @((New-FakeReport 501 4242)), @((New-FakeReport 502 $null)), @((New-FakeReport 503 630)))) {
                    $probeAttributed = @($reportSet | Where-Object { $null -ne $_.MergedPr })
                    $probeInWindow = @($probeAttributed | Where-Object { $probeNumbers -contains $_.MergedPr })
                    foreach ($feedStatus in @('Ok', 'Truncated', 'Unreachable', 'NotConfigured')) {
                        # The five conditions, restated. A zero is a claim that
                        # nothing escaped these merges, and it is only honest
                        # when all five hold.
                        $mayPrintZero = (
                            $feedStatus -eq 'Ok' -and                                 # the feed was read in full
                            $null -ne $convention -and                                # the convention has a start date
                            $null -ne $probeLastMerge -and                            # the window has a merge to date
                            $probeLastMerge -ge $convention -and                      # ... inside the convention
                            ($fixtureNow - $probeLastMerge).TotalDays -ge 14 -and     # ... past the report lag floor
                            @($probeAttributed).Count -gt 0 -and                      # somebody is actually marking
                            $null -ne $newest -and $newest -gt $probeLastMerge        # the feed spoke after it closed
                        )
                        $probe = Get-EscapedDefectReading `
                            -Feed (New-FakeFeed $reportSet $newest 166 $feedStatus) `
                            -Facts $factSet -Delivery $fakeDelivery -TierMap $tierMap -Now $fixtureNow
                        $zeroProbes += , [pscustomobject]@{
                            Cell = (Format-EscapedDefectCell -Reading $probe)
                            Permitted = $mayPrintZero
                            HasReport = (@($probeInWindow).Count -gt 0)
                        }
                    }
                }
            }
        }
    }
    $script:DefectConventionStart = $conventionStartSaved
    Assert (@($zeroProbes).Count -gt 300) 'The zero-probe sweep collapsed — a sweep with no cases reports clean (trap 78).'
    # A cell "prints a zero" when a 0 stands as a whole number: `0.250` does not
    # count, `(0 report(s)` does, and a date like 2026-09-01 does not.
    $printsZero = { param($Text) return [bool]([regex]::IsMatch($Text, '(^|[^.\d])0([^.\d]|$)')) }
    $badZero = @($zeroProbes | Where-Object { -not $_.Permitted -and (& $printsZero $_.Cell) })
    Assert (@($badZero).Count -eq 0) `
        ("{0} input combination(s) printed a zero into the escaped-defect cell without all five conditions holding. First: '{1}'" -f
            @($badZero).Count, $(if (@($badZero).Count -gt 0) { @($badZero)[0].Cell } else { '' }))
    # And the sweep has to actually reach both sides, or it is a scan with no
    # patterns: some probes are permitted a zero, and at least one prints one.
    Assert (@($zeroProbes | Where-Object { $_.Permitted }).Count -gt 0) `
        'No probe in the sweep was permitted a zero, so the sweep never exercised the case it discriminates against (trap 78).'
    Assert (@($zeroProbes | Where-Object { $_.Permitted -and -not $_.HasReport -and (& $printsZero $_.Cell) }).Count -gt 0) `
        'No probe printed a legitimate measured zero — the guards have latched shut, and a measured 0 is a finding this row must still be able to report.'

    # 17. DRA-127's own guard. The positive fixture is the RETIRED sentence, word
    #     for word, because a guard that has never fired on the defect it was
    #     built for is a guard aimed at nothing (trap 78).
    $retired = 'The tier model (plan §2.1) did not exist during this window, so no merge in it ' +
        'carries a tier. The metric becomes computable for windows after M0; it is `unmeasured` ' +
        'here rather than `0`, because nobody looked.'
    $postM0Start = $script:TierModelLandedUtc.AddHours(6)
    $postM0End = $script:TierModelLandedUtc.AddDays(2)
    $preM0Start = $script:TierModelLandedUtc.AddDays(-20)
    $preM0End = $script:TierModelLandedUtc.AddHours(-1)

    $fired = Get-WindowClaimViolations -Text $retired -WindowStart $postM0Start -WindowEnd $postM0End
    Assert (@($fired).Count -eq 2) `
        "The retired escaped-defect reason produced $(@($fired).Count) violation(s) over a post-M0 window rather than 2 — the guard does not see the defect it exists for."
    Assert (@($fired | Where-Object { $_.Name -eq 'tier-model-absent' }).Count -eq 1) 'The "tier model did not exist" claim was not caught for a post-M0 window.'
    Assert (@($fired | Where-Object { $_.Name -eq 'computable-after-checkpoint' }).Count -eq 1) 'The "becomes computable after M0" promise was not caught after M0.'
    Assert (@($fired | Where-Object { [string]::IsNullOrWhiteSpace($_.Why) }).Count -eq 0) 'A violation was reported without saying why it is false here.'

    # The shape the scan actually meets: Add-Line wrapped that sentence across
    # three lines in every dashboard that shipped it, so the wrapped form is the
    # committed defect and the one-line form is only a convenience.
    $retiredWrapped = @(
        '- **Escaped defect rate per tier.** The tier model (plan §2.1) did not exist during',
        '  this window, so no merge in it carries a tier. The metric becomes computable for',
        '  windows after M0; it is `unmeasured` here rather than `0`, because nobody looked.'
    ) -join "`n"
    Assert (@(Get-WindowClaimViolations -Text $retiredWrapped -WindowStart $postM0Start -WindowEnd $postM0End).Count -eq 2) `
        'The guard missed the WRAPPED retired reason — which is the only form that ever shipped.'

    Assert (@(Get-WindowClaimViolations -Text $retired -WindowStart $preM0Start -WindowEnd $preM0End).Count -eq 0) `
        'The guard fired on the BASELINE window, where that sentence was true — a guard that flags a true statement is a guard people disable.'
    Assert (@(Get-WindowClaimViolations -Text 'Nothing here names a precondition.' -WindowStart $postM0Start -WindowEnd $postM0End).Count -eq 0) `
        'The guard fired on innocent prose.'
    Assert (@(Get-WindowClaimViolations -Text '' -WindowStart $postM0Start -WindowEnd $postM0End).Count -eq 0) 'The guard fired on empty text.'
    Assert (@($script:WindowClaimChecks).Count -gt 1) 'The window-claim check list collapsed — a scan with no patterns reports clean (trap 78).'

    # And the sentences this script ACTUALLY emits survive their own guard, for
    # every status, over a window past the checkpoint the retired text named.
    foreach ($st in @('Ok', 'NoConvention', 'NotConfigured', 'Truncated', 'Unreachable', 'PreConvention', 'LagFloor', 'FeedSilent', 'Unattributable')) {
        $r = [pscustomobject]@{
            Status = $st; FeedStatus = $st; Rate = $(if ($st -eq 'Ok') { 0 } else { $null })
            Scanned = 166; MarkedTotal = $(if ($st -eq 'Ok') { 1 } else { 0 })
            InWindow = @(); Elsewhere = 1; DeliverySlices = 4; PerTier = @(); FeedReason = 'because'
            Unattributed = @(([pscustomobject]@{ Number = 901; Title = 'u1'; Url = 'uu1' }))
            LowerBound = $false; ConventionCase = 'predates'
            ConventionStart = (ConvertTo-Utc '2026-09-01T00:00:00Z')
            LastMerge = (ConvertTo-Utc '2026-09-12T00:00:00Z'); DaysSinceLastMerge = 38.0
            LagFloorDays = 14; FeedNewest = (ConvertTo-Utc '2026-09-20T00:00:00Z')
            ObservedLags = @(); CommentMarked = 0
        }
        $text = (Get-EscapedDefectReason -Reading $r -Repository 'o/n') -join "`n"
        Assert (@(Get-WindowClaimViolations -Text $text -WindowStart $postM0Start -WindowEnd $postM0End).Count -eq 0) `
            "The reason this script prints for status $st names a precondition the window satisfies — the DRA-127 defect, re-introduced."
    }

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

# --- The player-report feed: escaped defect rate ----------------------------
# Read unconditionally (bar -NoDefectFeed) because the point of the row is that
# the instrument LOOKS. The retired version asserted an absence it had never
# checked, which is how its reason outlived the fact it was about.
$decisionsPath = Join-Path $RepoRoot 'DECISIONS.md'
if ($NoDefectFeed) {
    Write-Host 'Skipping the player-report feed (-NoDefectFeed) …'
} else {
    Write-Host 'Reading the player-report feed (Discussions) …'
}
$defectFeed = Get-DefectReports -Repository $Repo -Enabled (-not $NoDefectFeed.IsPresent)
if ($defectFeed.Status -notin @('Ok', 'NotConfigured')) {
    Write-Host ("  player-report feed {0}: {1}" -f $defectFeed.Status, $defectFeed.Reason) -ForegroundColor Yellow
}
$defects = Get-EscapedDefectReading -Feed $defectFeed -Facts $facts -Delivery $delivery `
    -TierMap (Get-TierMap -DecisionsPath $decisionsPath)

# --- Paperclip: lead time per DRA, queue latency, cost ---------------------
$apiBase = Get-PaperclipBase -Raw $ApiBase
$issues = $null
if (-not $NoPaperclip -and $apiBase -and $CompanyId) {
    Write-Host "Reading Paperclip issues …"
    $issuesResult = Invoke-Paperclip -Base $apiBase -Key $ApiKey -Path "/api/companies/$CompanyId/issues?limit=200"
    if ($issuesResult.Ok) { $issues = $issuesResult.Data }
    else {
        Write-Host ("  Paperclip {0}: {1}" -f $issuesResult.Status, $issuesResult.Reason) -ForegroundColor Yellow
    }
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

    # Which of the three worlds this row's cost cell is reporting. Without it,
    # "the API refused us" and "Paperclip has never heard of DRA-71" render the
    # same, and the reader cannot tell a measurement from a silence.
    $rowStatus = 'NoRecord'
    if ($NoPaperclip) { $rowStatus = 'NotConfigured' }
    elseif (-not $apiBase -or -not $CompanyId) { $rowStatus = 'NotConfigured' }
    elseif ($null -eq $issues -and @($script:PaperclipFailures).Count -gt 0) { $rowStatus = 'Unreachable' }

    $cost = $null
    if ($null -ne $issue -and -not $NoPaperclip) {
        $costResult = Invoke-Paperclip -Base $apiBase -Key $ApiKey -Path "/api/issues/$($issue.id)/cost-summary"
        if ($costResult.Ok) { $cost = $costResult.Data; $rowStatus = 'Ok' }
        else { $rowStatus = $costResult.Status }
    }

    $draRows += , [pscustomobject]@{
        Dra           = $dra
        PaperclipStatus = $rowStatus
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
$experiments = Get-Experiments -DecisionsPath $decisionsPath

$frozen = $null
$baselinePath = Resolve-OutPath $BaselineFile
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
    # The rate AND the name of the world it came from. A later window comparing
    # against this file has to be able to tell a measured 0 from an unreadable
    # feed, and a lone `null` cannot: that is the same absent-proxy-for-a-fact
    # shape (trap 64b) the Paperclip rows were fixed for in DRA-80.
    escapedDefectRate = $defects.Rate
    escapedDefectStatus = $defects.Status
    escapedDefectsMarked = $defects.MarkedTotal
    escapedDefectFeedScanned = $defects.Scanned
    # DRA-135: a later window comparing against this file has to be able to tell
    # a rate from a lower bound, and to see the guard state the rate was taken
    # under. A frozen `0` whose guards nobody recorded is un-auditable later.
    escapedDefectUnattributed = @($defects.Unattributed).Count
    escapedDefectLowerBound = $defects.LowerBound
    escapedDefectConventionStart = $(if ($null -eq $defects.ConventionStart) { $null } else { (Format-DefectDate $defects.ConventionStart) })
    escapedDefectLagFloorDays = $defects.LagFloorDays
    escapedDefectWindowLastMerge = $(if ($null -eq $defects.LastMerge) { $null } else { $defects.LastMerge.ToString('o') })
    escapedDefectFeedNewest = $(if ($null -eq $defects.FeedNewest) { $null } else { $defects.FeedNewest.ToString('o') })
    escapedDefectObservedLagDays = @($defects.ObservedLags | ForEach-Object { $_.Days })
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
if (@($script:PaperclipFailures).Count -gt 0) {
    Add-Line ('> **{0} Paperclip read(s) in this run were UNREACHABLE.** Every Paperclip-derived' -f @($script:PaperclipFailures).Count)
    Add-Line '> row below (queue latency, cost, and the acceptance half of lead time) is'
    Add-Line '> **unknown** rather than zero, and this run is NOT eligible to freeze a baseline.'
    Add-Line ('> First failure: `{0}`' -f (Get-PaperclipFailureSummary))
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
Add-Line ('| Escaped defect rate | marked player reports naming a window merge ÷ delivery slices, per tier | {0} |' -f (Format-EscapedDefectCell -Reading $defects))
Add-Line ('| CI failure/flake split | red runs: filed as flake vs unfiled | {0} red event(s), {1} filed — see §4 |' -f @($ciRed).Count, @($ciRed | Where-Object { $_.Filed }).Count)
Add-Line ('| PRs + Helm touches per slice | count | {0} PRs/slice, {1} Helm touches/delivery slice |' -f (Format-Number $prsPerSlice 2), (Format-Number $touchesPerSlice 2))
Add-Line ('| Cost per delivered slice | Paperclip run cost ÷ slices delivered | {0} — see §5 and §6 |' -f $(if ($null -eq $costPerSlice) { (Format-PaperclipUnmeasured -Fallback 'no run records in the window') } else { ('{0} cents · {1:N0} tokens' -f (Format-Number $costPerSlice 2), $tokensPerSlice) }))
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
    # Three absences, three sentences. `no issue record` is a measurement (we
    # asked, and Paperclip does not know this item); `API unreachable` is the
    # absence of one.
    $absent = switch ($d.PaperclipStatus) {
        'Unreachable' { '`unmeasured` — API unreachable' }
        'NotConfigured' { '`unmeasured` — no Paperclip credentials' }
        default { '`no issue record`' }
    }
    $costCell = if ($null -eq $d.RunCount) { $absent }
    elseif ($d.RunCount -eq 0) { '`no run records`' }
    else { ('{0} cents · {1:N0} in / {2:N0} out tokens' -f $d.CostCents, $d.InputTokens, $d.OutputTokens) }
    $leadCell = (Format-Number $d.LeadTimeHours 2)
    if ($d.Clamped) { $leadCell += ' _(clamped)_' }
    Add-Line ('| {0} | {1} | {2} | {3} | {4} | {5} |' -f `
            $d.Dra, $d.Slices, $(if ($null -eq $d.QueueHours) { $absent } else { (Format-Number $d.QueueHours 2) + ' h' }), `
        $leadCell, $(if ($null -eq $d.RunCount) { $absent } else { $d.RunCount }), $costCell)
}
Add-Line ''
if (@($draRows | Where-Object { $_.PaperclipStatus -eq 'Unreachable' }).Count -gt 0) {
    Add-Line ('_`API unreachable`_ — the Paperclip GET threw, so these rows are **unknown**, not')
    Add-Line 'zero. Nothing was measured and nothing may be inferred from the blank. Reason:'
    Add-Line ('`{0}`' -f (Get-PaperclipFailureSummary))
    Add-Line ''
}
if (@($draRows | Where-Object { $_.Clamped }).Count -gt 0) {
    Add-Line '_(clamped)_ — the work item was accepted before this window opened, so its lead'
    Add-Line 'time is measured from its first commit **in** the window. Letting a standing lane'
    Add-Line 'contribute its whole open life to the denominator would divide the Governance Wait'
    Add-Line 'Ratio down by an amount that has nothing to do with governance.'
    Add-Line ''
}

Add-Line '## 6. What this window could NOT measure, and why'
Add-Line ''
foreach ($line in (Get-EscapedDefectReason -Reading $defects -Repository $Repo)) { Add-Line $line }
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
Add-Line (Get-ReproduceCommand -FromPr $FromPr -ToPr $ToPr -WindowLabel $WindowLabel `
        -Baseline $Baseline.IsPresent -NoPaperclip $NoPaperclip.IsPresent -NoDefectFeed $NoDefectFeed.IsPresent)
Add-Line 'pwsh -NoProfile -File scripts/exo-metrics.ps1 -SelfTest   # the detectors fire'
Add-Line '```'
Add-Line ''
Add-Line 'Needs `gh` authenticated against the repo and, for the cost and queue columns,'
Add-Line '`PAPERCLIP_API_URL` / `PAPERCLIP_API_KEY` / `PAPERCLIP_COMPANY_ID`. Without them'
Add-Line 'the GitHub-derived rows still compute and the Paperclip-derived ones read'
Add-Line '`unmeasured` **with which kind of absence it is** — `-NoPaperclip` makes that'
Add-Line 'explicit, and is the only door through which a `-Baseline` run may freeze them.'
Add-Line 'A run whose Paperclip reads were *unreachable* refuses to freeze and exits 3:'
Add-Line 'the point of a baseline is that later claims are checkable against it, and an'
Add-Line 'absence nobody measured is not a number to check anything against.'
Add-Line ''
Add-Line 'The escaped-defect row reads the player-report feed (Discussions) on every run;'
Add-Line '`-NoDefectFeed` skips it and says so in the row. A run that would print a reason'
Add-Line 'naming a precondition **this** window satisfies refuses to write and exits 4 —'
Add-Line 'the escaped-defect reason was a hardcoded sentence true of one window and printed'
Add-Line 'for three (DRA-127), and a wrong reason for a missing value tells the next reader'
Add-Line 'not to bother looking.'

# --- Refuse before writing anything -----------------------------------------
# Both files, not just the JSON: a -Baseline dashboard carries a "FROZEN
# BASELINE" banner, and writing that beside a freeze that did not happen is the
# same lie one layer out.
if (Test-BaselineRefused -Baseline $Baseline.IsPresent -NoPaperclip $NoPaperclip.IsPresent -Failures $script:PaperclipFailures) {
    [Console]::OutputEncoding = $priorOutputEncoding
    Write-Host ''
    Write-Host ('REFUSED: {0} Paperclip read(s) were UNREACHABLE, so the Paperclip-derived inputs' -f @($script:PaperclipFailures).Count) -ForegroundColor Red
    Write-Host 'to this baseline are UNKNOWN, not zero. Nothing was written.' -ForegroundColor Red
    Write-Host ''
    foreach ($f in $script:PaperclipFailures) { Write-Host "  - $f" -ForegroundColor Red }
    Write-Host ''
    Write-Host 'Fix the endpoint (check that PAPERCLIP_API_URL is the address the API actually' -ForegroundColor Yellow
    Write-Host 'binds — a localhost URL against a tailnet-bound server is refused, which is the' -ForegroundColor Yellow
    Write-Host 'bug this refusal exists for), or re-run with -NoPaperclip to freeze those rows' -ForegroundColor Yellow
    Write-Host 'as explicitly unmeasured.' -ForegroundColor Yellow
    exit 3
}

# Same door, one layer further in: the document must not carry a reason that is
# false of the window it is about. Scanned here rather than at each row, because
# the next hardcoded sentence will be added by someone who has not read the row
# code, and it still must not ship (DRA-127).
$claimViolations = Get-WindowClaimViolations -Text ($sb.ToString()) -WindowStart $windowStart -WindowEnd $windowEnd
if (@($claimViolations).Count -gt 0) {
    [Console]::OutputEncoding = $priorOutputEncoding
    Write-Host ''
    Write-Host ('REFUSED: {0} sentence(s) in this document name a precondition THIS window' -f @($claimViolations).Count) -ForegroundColor Red
    Write-Host 'satisfies, so they are false of the reading they are printed beside. Nothing was written.' -ForegroundColor Red
    Write-Host ''
    foreach ($v in $claimViolations) {
        Write-Host ("  - [{0}] `"{1}`"" -f $v.Name, $v.Sentence) -ForegroundColor Red
        Write-Host ("      why it is false here: {0}" -f $v.Why) -ForegroundColor Yellow
    }
    Write-Host ''
    Write-Host 'A reason beside an `unmeasured` row is a claim about the window it is printed' -ForegroundColor Yellow
    Write-Host 'for. Compute it from what the run read, or state a structural fact that no' -ForegroundColor Yellow
    Write-Host 'window can falsify — do not hardcode a precondition nobody re-checks.' -ForegroundColor Yellow
    exit 4
}

$outPath = Resolve-OutPath $Out
Write-Utf8NoBom -Path $outPath -Text ($sb.ToString())
Write-Host "Wrote $Out"

if ($Baseline) {
    Write-BaselineFreeze -Path $baselinePath -Current $current `
        -NoPaperclip $NoPaperclip.IsPresent -Failures $script:PaperclipFailures
    Write-Host "Froze $BaselineFile"
}
if (@($script:PaperclipFailures).Count -gt 0) {
    Write-Host ''
    Write-Host ('WARNING: {0} Paperclip read(s) were unreachable — the Paperclip-derived rows read' -f @($script:PaperclipFailures).Count) -ForegroundColor Yellow
    Write-Host 'unmeasured, not zero. This run may not be used to freeze a baseline.' -ForegroundColor Yellow
}
if (@($script:DefectFeedFailures).Count -gt 0) {
    Write-Host ''
    Write-Host 'WARNING: the player-report feed read did not complete, so escaped defect rate is' -ForegroundColor Yellow
    Write-Host 'UNKNOWN rather than zero for this run.' -ForegroundColor Yellow
    foreach ($f in $script:DefectFeedFailures) { Write-Host "  - $f" -ForegroundColor Yellow }
}

Write-Host ''
Write-Host ('GWR {0} (incl. SSC wait {1}) · ACCR {2} · {3} PRs/slice · {4} Helm touches/slice · CI median {5} min' -f `
    (Format-Number $gwr 2), (Format-Number $gwrWithSsc 2), (Format-Percent $accr), `
    (Format-Number $prsPerSlice 2), (Format-Number $touchesPerSlice 2), (Format-Number $medianCi 1))
Write-Host ('escaped defect rate: {0} ({1}; {2} marked report(s) in {3} discussion(s) read)' -f `
    (Format-Number $defects.Rate 3), $defects.Status, $defects.MarkedTotal, $defects.Scanned)

[Console]::OutputEncoding = $priorOutputEncoding
