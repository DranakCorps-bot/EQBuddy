<#
.SYNOPSIS
    The open-PR queue sweep: one row per open PR, and an EXCEPTION for any hold a release
    has already outrun. With -Release, the pre-release gate the release seat runs.

.DESCRIPTION
    DRA-723. PR #992 was SIGNED OFF by Reviewer and left as a draft titled "DO NOT MERGE
    before v2.0.2 is tagged". v2.0.2 was tagged, then v2.0.3, and nobody lifted the hold, so
    2.0.3 shipped without it. The sweep that read the queue (DRA-636) was a person reading
    `gh pr list`, and a hold whose condition has come true reads exactly like a hold that is
    still live. This script makes that difference a row.

    For every open PR it prints: draft, signed-off, the two required checks, mergeable, and
    every hold line. The title keeps the broad wording (`[HOLD]`, `do not merge`,
    `held`). Body and comment lines take an explicit marker only (DRA-769): a line
    that describes a hold ("held as a draft", "lifted the hold", a quoted marker
    mid-sentence) is not one. Then it decides:

      EXCEPTION  a hold line names a release (vX.Y.Z) and a tag at or above it is published,
                 and no comment dated AFTER that tag says `HOLD LIFTED` or `STILL HELD: <why>`
                 (or `RELEASE-EXCLUDE vA.B.C: <why>`, which is a still-held reason too).
                 Signed-off and held past its release is the #992 shape and says so.
      READY      signed off, both required checks green, mergeable, not a draft, no live
                 hold. Nothing is wrong with it except that it is sitting. `gh pr list`
                 often leaves mergeable UNKNOWN; each of those is re-read with
                 `gh pr view`, which makes GitHub compute it. Still UNKNOWN after that
                 is shown as READY-pending-mergeability and still counts as READY when
                 the rest of this row is met (DRA-769). One EXCEPTION reason per
                 (PR, released tag): a line that names the same version twice is one row.
      HELD       a hold that has not been outrun (its release is not tagged yet, or the
                 latest dated marker says STILL HELD).
      OPEN       everything else (unreviewed, red, conflicting, draft without a hold).

    DRA-733. A PR is request-driven when its head branch starts with scribe/, or its
    body links github.com/DranakCorps-bot/EQBuddy/(discussions|issues)/N, or a
    reddit.com / discord.com URL. The verdict is every body or comment line matching
    ^\W*ALIGNMENT:\s*(aligned|not-aligned|unclear). It is printed on every row.
    A request-driven PR with no such line is an EXCEPTION ("request-driven, no
    alignment verdict"). A non-intake PR (any changed file other than SCRIBE.md)
    whose verdicts include not-aligned or unclear is an EXCEPTION ("alignment
    <verdict>: Founder ask, do not merge"). A SCRIBE.md-only intake may carry those
    lines; they are filing records. -Release inherits this through the EXCEPTION
    path above. There is no second gate.

    Signed-off is read from what Reviewer actually writes on this shared account (a
    `--approve` review is refused on our own PRs): a review or comment saying SIGNED OFF /
    APPROVED / Reviewer PASS, voided by a LATER one saying REQUEST(ED) CHANGES.

    -Release vX.Y.Z is the gate (docs/ops/release-seat.md step 1b). It passes only when there
    is no EXCEPTION and every open PR carries an exclusion reason for THAT version: a PR
    comment `RELEASE-EXCLUDE vX.Y.Z: <reason>` or a `-Exclude '<n>=<reason>'` argument. The
    output is the markdown the seat posts on the release card. A READY PR is not excluded by
    being READY: merge it first, or say why it waits.

    Exit codes: sweep 0 clean / 2 at least one EXCEPTION; -Release 0 PASS / 1 FAIL;
    3 when GitHub could not be asked (never read as an empty queue). Reads only.

.EXAMPLE
    pwsh -NoProfile -File scripts/pr-sweep.ps1

.EXAMPLE
    pwsh -NoProfile -File scripts/pr-sweep.ps1 -Release v2.0.4 -Exclude '978=Scribe intake, no player effect'

.EXAMPLE
    pwsh -NoProfile -File scripts/pr-sweep.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$Release,
    [string[]]$Exclude = @(),
    [string]$Repo = 'DranakCorps-bot/EQBuddy',
    [string[]]$RequiredChecks = @('build-and-test', 'e2e-windows'),
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

# Each array element parenthesised: PowerShell binds `,` tighter than `+` (trap 78).
# DRA-769. Body and comment lines are explicit markers only. `\bhold\b` / `\bheld\b` matched
# #1013's own body, which describes #992 ("held as a draft", "lifted the hold") beside a
# version and flagged itself. `do not merge` is line-anchored so a mid-sentence quotation
# of that marker is prose; a real one is its own line, which is #992's body
# (`**DO NOT MERGE before v2.0.2**`). The versioned `hold|held for|until|before vX.Y.Z`
# phrase is not anchored, so it still matches inside a sentence that is itself the marker.
# The TITLE is not narrowed (Reviewer on #1034). It keeps the pre-DRA-769 word-boundary
# set in TitleHoldPatterns, so `[HOLD]`, `WIP - do not merge` and `Held pending Helm SIGN`
# stay holds. Applying this body list to the title made a signed-off green PR read READY.
$script:HoldPatterns = @(
    ("(?i)^\W*(?:do\s+not|don'?t)\s+merge\b"),
    ('(?i)\bhold(?:s|ing)?\s+(?:for|until|before)\s+v?\d+\.\d+\.\d+'),
    ('(?i)\bheld\s+(?:for|until|before)\s+v?\d+\.\d+\.\d+'),
    ('(?i)\bnot\s+before\s+v?\d+\.\d+\.\d+')
)
$script:TitleHoldPatterns = @(
    ('(?i)\bdo\s+not\s+merge\b'),
    ("(?i)\bdon'?t\s+merge\b"),
    ('(?i)\bhold(s|ing)?\b'),
    ('(?i)\bheld\b'),
    ('(?i)\bnot\s+before\s+v?\d+\.\d+\.\d+')
)
$script:SignOffPattern = '(?i)\bsigned[\s-]*off\b|\bAPPROVED?\b|\bReviewer\b[^\r\n]{0,60}\bPASS\b'
$script:ChangesPattern = '(?i)\brequest(ed|ing)?\s+changes\b'
$script:LiftPattern = '(?im)^\W*(HOLD\s+LIFTED\b|STILL\s+HELD\s*:\s*\S|RELEASE-EXCLUDE\s+v?\d+\.\d+\.\d+\s*:\s*\S)'
$script:VersionPattern = '\bv?(\d+\.\d+\.\d+)\b'
# Each element parenthesised: PowerShell binds `,` tighter than `+` (trap 78).
# Order is the selftest's sample order: discussions, issues, reddit, discord.
$script:RequestLinkPatterns = @(
    ('(?i)(?:https?://)?github\.com/DranakCorps-bot/EQBuddy/discussions/\d+'),
    ('(?i)(?:https?://)?github\.com/DranakCorps-bot/EQBuddy/issues/\d+'),
    ('(?i)(?:https?://)?(?:[\w-]+\.)?reddit\.com/\S+'),
    ('(?i)(?:https?://)?(?:[\w-]+\.)?discord\.com/\S+')
)
# not-aligned is captured whole: the group is tried at the first word, so `aligned`
# does not eat the suffix of `not-aligned`. The selftest asserts the capture.
$script:AlignmentLinePattern = '(?im)^\W*ALIGNMENT:\s*(aligned|not-aligned|unclear)\b'

function ConvertTo-Version([string]$s) {
    $m = [regex]::Match($s, '(\d+\.\d+\.\d+)')
    if ($m.Success) { [version]$m.Groups[1].Value } else { $null }
}

function Get-HoldLines($pr) {
    # Title, body and comment lines carrying hold wording. A line that is itself a lift or
    # exclusion marker is the cure, not the hold. The title uses the broad set; body and
    # comments use the explicit markers (DRA-769, Reviewer on #1034).
    $sources = @(@{ Where = 'title'; At = $null; Text = [string]$pr.title },
                 @{ Where = 'body'; At = $null; Text = [string]$pr.body })
    foreach ($c in @($pr.comments | ForEach-Object { $_ })) {
        $sources += @{ Where = 'comment'; At = [datetime]$c.createdAt; Text = [string]$c.body }
    }
    $out = @()
    foreach ($s in $sources) {
        $patterns = if ($s.Where -eq 'title') { $script:TitleHoldPatterns } else { $script:HoldPatterns }
        foreach ($line in ($s.Text -split "`r?`n")) {
            if ([regex]::IsMatch($line, $script:LiftPattern)) { continue }
            $hit = $false
            foreach ($p in $patterns) { if ([regex]::IsMatch($line, $p)) { $hit = $true; break } }
            if (-not $hit) { continue }
            $versions = @([regex]::Matches($line, $script:VersionPattern) | ForEach-Object { [version]$_.Groups[1].Value })
            $text = $line.Trim()
            if ($text.Length -gt 140) { $text = $text.Substring(0, 137) + '...' }
            $out += [pscustomobject]@{ Where = $s.Where; At = $s.At; Text = $text; Versions = $versions }
        }
    }
    , $out
}

function Get-SignOff($pr) {
    # The latest sign-off-or-changes statement wins. Reviews and comments are one timeline.
    $events = @()
    foreach ($r in @($pr.reviews | ForEach-Object { $_ })) {
        $events += [pscustomobject]@{ At = [datetime]$r.submittedAt; State = [string]$r.state; Body = [string]$r.body }
    }
    foreach ($c in @($pr.comments | ForEach-Object { $_ })) {
        $events += [pscustomobject]@{ At = [datetime]$c.createdAt; State = ''; Body = [string]$c.body }
    }
    $verdict = 'none'
    foreach ($e in ($events | Sort-Object At)) {
        if ($e.State -eq 'CHANGES_REQUESTED' -or [regex]::IsMatch($e.Body, $script:ChangesPattern)) { $verdict = 'changes-requested' }
        elseif ($e.State -eq 'APPROVED' -or [regex]::IsMatch($e.Body, $script:SignOffPattern)) { $verdict = 'signed-off' }
    }
    $verdict
}

function Get-ChecksState($pr, [string[]]$required) {
    $rollup = @($pr.statusCheckRollup | ForEach-Object { $_ })
    $parts = @()
    $worst = 'green'
    foreach ($name in $required) {
        $runs = @($rollup | Where-Object { $_.name -eq $name -or $_.context -eq $name })
        if ($runs.Count -eq 0) { $parts += "${name}:none"; if ($worst -eq 'green') { $worst = 'none' }; continue }
        $r = $runs[-1]
        $concl = [string]($r.conclusion ?? $r.state)
        $status = [string]$r.status
        $s = if ($concl -in 'SUCCESS', 'NEUTRAL', 'SKIPPED') { 'ok' }
             elseif ($concl -in 'FAILURE', 'ERROR', 'CANCELLED', 'TIMED_OUT', 'ACTION_REQUIRED', 'STARTUP_FAILURE') { 'red' }
             elseif ($status -and $status -ne 'COMPLETED') { 'pending' }
             elseif ($concl -eq 'PENDING') { 'pending' }
             else { 'red' }
        $parts += "${name}:$s"
        if ($s -eq 'red') { $worst = 'red' } elseif ($s -eq 'pending' -and $worst -ne 'red') { $worst = 'pending' }
    }
    [pscustomobject]@{ State = $worst; Detail = ($parts -join ' ') }
}

function Get-ExclusionReason($pr, [string]$release, [hashtable]$excludeArg) {
    if (-not $release) { return $null }
    $n = [int]$pr.number
    if ($excludeArg.ContainsKey($n)) { return "$($excludeArg[$n]) (-Exclude)" }
    $want = ConvertTo-Version $release
    foreach ($c in (@($pr.comments | ForEach-Object { $_ }) | Sort-Object { [datetime]$_.createdAt } -Descending)) {
        foreach ($m in [regex]::Matches([string]$c.body, '(?im)^\W*RELEASE-EXCLUDE\s+v?(\d+\.\d+\.\d+)\s*:\s*(.+)$')) {
            if ([version]$m.Groups[1].Value -eq $want) { return "$($m.Groups[2].Value.Trim()) (PR comment $(([datetime]$c.createdAt).ToString('yyyy-MM-dd HH:mm'))Z)" }
        }
    }
    $null
}

function Test-RequestDriven($pr) {
    # Branch prefix is case-sensitive (`scribe/`, not `Scribe/`). Links are the
    # BODY only: a URL that exists solely in a comment is not a request.
    $head = [string]$pr.headRefName
    if ($head.StartsWith('scribe/', [System.StringComparison]::Ordinal)) { return $true }
    $body = [string]$pr.body
    foreach ($p in $script:RequestLinkPatterns) {
        if ([regex]::IsMatch($body, $p)) { return $true }
    }
    $false
}

function Test-ScribeIntakeOnly($pr) {
    # Intake is SCRIBE.md and nothing else. An empty file list is not an intake.
    $files = @($pr.files | ForEach-Object { $_ })
    if ($files.Count -eq 0) { return $false }
    foreach ($f in $files) {
        $path = if ($f -is [string]) { $f } else { [string]$f.path }
        if ($path -ne 'SCRIBE.md') { return $false }
    }
    $true
}

function Get-AlignmentVerdicts($pr) {
    $texts = @([string]$pr.body)
    foreach ($c in @($pr.comments | ForEach-Object { $_ })) { $texts += [string]$c.body }
    $found = [System.Collections.Generic.List[string]]::new()
    foreach ($text in $texts) {
        foreach ($m in [regex]::Matches($text, $script:AlignmentLinePattern)) {
            $v = $m.Groups[1].Value
            if (-not $found.Contains($v)) { [void]$found.Add($v) }
        }
    }
    , $found.ToArray()
}

function Get-AlignmentException($pr, $verdicts) {
    # One reason, and the missing-verdict arm does not also fire the bad-verdict arm.
    $reasons = [System.Collections.Generic.List[string]]::new()
    if ((Test-RequestDriven $pr) -and $verdicts.Count -eq 0) {
        [void]$reasons.Add('request-driven, no alignment verdict')
    } elseif (-not (Test-ScribeIntakeOnly $pr)) {
        $bad = @($verdicts | Where-Object { $_ -eq 'not-aligned' -or $_ -eq 'unclear' })
        if ($bad.Count -gt 0) {
            $named = if ($bad -contains 'not-aligned') { 'not-aligned' } else { 'unclear' }
            [void]$reasons.Add("alignment ${named}: Founder ask, do not merge")
        }
    }
    , $reasons.ToArray()
}

function Get-PrVerdict($pr, $tags, [string[]]$required, [string]$release, [hashtable]$excludeArg) {
    $holds = Get-HoldLines $pr
    $signOff = Get-SignOff $pr
    $checks = Get-ChecksState $pr $required
    $markers = @(@($pr.comments | ForEach-Object { $_ }) | Where-Object { [regex]::IsMatch([string]$_.body, $script:LiftPattern) } |
        ForEach-Object { [pscustomobject]@{ At = [datetime]$_.createdAt; Text = ([regex]::Match([string]$_.body, $script:LiftPattern).Value.Trim()) } })

    # A hold naming vX.Y.Z is outrun by the EARLIEST published tag at or above it: "hold until
    # v2.0.2 is tagged" and "hold for 2.0.3" both stopped being true the moment that tag shipped.
    $outrun = @()
    foreach ($h in $holds) {
        foreach ($v in $h.Versions) {
            $tag = @($tags | Where-Object { $_.Version -ge $v } | Sort-Object Published)[0]
            if (-not $tag) { continue }
            $cure = @($markers | Where-Object { $_.At -gt $tag.Published } | Sort-Object At)[-1]
            if (-not $cure) { $outrun += [pscustomobject]@{ Hold = $h; Tag = $tag } }
        }
    }

    $liveHold = $holds.Count -gt 0
    if ($liveHold -and $outrun.Count -eq 0) {
        # Every versioned hold is either not yet due or answered after its tag. An unversioned
        # hold, or a STILL HELD answer, keeps it HELD; only a HOLD LIFTED as the newest marker
        # clears it.
        $latest = @($markers | Sort-Object At)[-1]
        if ($latest -and $latest.Text -match '(?i)^\W*HOLD\s+LIFTED') { $liveHold = $false }
    }

    $mergeable = [string]$pr.mergeable
    # READY-pending-mergeability is the label Resolve-ListedMergeable writes when a per-PR
    # `gh pr view` still returns UNKNOWN. Raw UNKNOWN (the list value, before that view)
    # is not READY: the resolve step is what makes a sitting PR visible.
    $readyMerge = $mergeable -eq 'MERGEABLE' -or $mergeable -eq 'READY-pending-mergeability'
    $align = Get-AlignmentVerdicts $pr
    $alignment = if ($align.Count -eq 0) { 'none' } else { ($align -join ', ') }
    $alignWhy = Get-AlignmentException $pr $align
    $verdict = if ($outrun.Count -gt 0 -or $alignWhy.Count -gt 0) { 'EXCEPTION' }
               elseif ($liveHold) { 'HELD' }
               elseif ($signOff -eq 'signed-off' -and $checks.State -eq 'green' -and $readyMerge -and -not $pr.isDraft) { 'READY' }
               else { 'OPEN' }
    # One reason per released tag. #1013's prose line named v2.0.2 twice and v2.0.3 twice
    # and the sweep printed four rows; a real hold line that repeats a version does the same.
    $why = @()
    $tagSeen = @{}
    foreach ($o in $outrun) {
        $key = [string]$o.Tag.Tag
        if ($tagSeen.ContainsKey($key)) { continue }
        $tagSeen[$key] = $true
        $lead = if ($signOff -eq 'signed-off') { 'SIGNED OFF and held past its release' } else { 'held past its release' }
        $why += "${lead}: '$($o.Hold.Text)' ($($o.Hold.Where)) - $($o.Tag.Tag) published $($o.Tag.Published.ToString('yyyy-MM-dd HH:mm'))Z with no HOLD LIFTED / STILL HELD: <reason> dated after it"
    }
    foreach ($w in $alignWhy) { $why += [string]$w }
    [pscustomobject]@{
        Number    = [int]$pr.number
        Title     = [string]$pr.title
        Draft     = [bool]$pr.isDraft
        SignOff   = $signOff
        Checks    = $checks
        Mergeable = $mergeable
        Holds     = $holds
        Alignment = $alignment
        Verdict   = $verdict
        Why       = $why
        Excluded  = Get-ExclusionReason $pr $release $excludeArg
    }
}

function Format-Sweep($rows, [string]$release, [datetime]$now) {
    $sb = [System.Text.StringBuilder]::new()
    $head = if ($release) { "## Pre-release PR gate: $release" } else { '## EQBuddy open-PR sweep' }
    [void]$sb.AppendLine($head)
    [void]$sb.AppendLine("Run $($now.ToString('yyyy-MM-dd HH:mm'))Z by ``scripts/pr-sweep.ps1`` (DRA-723, DRA-733, DRA-769). $($rows.Count) open PR(s).")
    [void]$sb.AppendLine()
    $cols = '| PR | Verdict | Alignment | Draft | Signed-off | Checks | Mergeable |' + $(if ($release) { " Excluded from $release |" } else { '' })
    [void]$sb.AppendLine($cols)
    [void]$sb.AppendLine('|---|---|---|---|---|---|---|' + $(if ($release) { '---|' } else { '' }))
    foreach ($r in ($rows | Sort-Object Number)) {
        $t = $r.Title -replace '\|', '/'
        if ($t.Length -gt 70) { $t = $t.Substring(0, 67) + '...' }
        $alignCol = ([string]$r.Alignment) -replace '\|', '/'
        $line = "| #$($r.Number) $t | **$($r.Verdict)** | $alignCol | $(if ($r.Draft) { 'yes' } else { 'no' }) | $($r.SignOff) | $($r.Checks.State) ($($r.Checks.Detail)) | $($r.Mergeable) |"
        if ($release) { $line += " $(if ($r.Excluded) { $r.Excluded -replace '\|', '/' } else { '**NO REASON**' }) |" }
        [void]$sb.AppendLine($line)
    }
    $withHolds = @($rows | Where-Object { $_.Holds.Count -gt 0 })
    if ($withHolds.Count) {
        [void]$sb.AppendLine(); [void]$sb.AppendLine('**Hold wording found:**')
        foreach ($r in ($withHolds | Sort-Object Number)) {
            foreach ($h in $r.Holds) { [void]$sb.AppendLine("- #$($r.Number) ($($h.Where)): $($h.Text -replace '`', "'")") }
        }
    }
    $ex = @($rows | Where-Object Verdict -eq 'EXCEPTION')
    if ($ex.Count) {
        [void]$sb.AppendLine(); [void]$sb.AppendLine("**EXCEPTIONS ($($ex.Count)):** a hold a release has outrun needs a dated ``HOLD LIFTED`` or ``STILL HELD: <reason>`` comment. A request-driven PR with no ``ALIGNMENT:`` line, or a non-intake PR whose verdict is ``not-aligned`` or ``unclear``, stays an exception until that is answered (Founder ask; do not merge).")
        foreach ($r in $ex) { foreach ($w in $r.Why) { [void]$sb.AppendLine("- #$($r.Number): $($w -replace '`', "'")") } }
    }
    $ready = @($rows | Where-Object Verdict -eq 'READY')
    if ($ready.Count) {
        [void]$sb.AppendLine(); [void]$sb.AppendLine("**READY and sitting ($($ready.Count)):** signed off, green, mergeable, no hold: " + (($ready | ForEach-Object { "#$($_.Number)" }) -join ', ') + '. Merge, or write why it waits.')
    }
    $sb.ToString()
}

function Test-ReleaseGate($rows) {
    $fails = @()
    foreach ($r in $rows) {
        if ($r.Verdict -eq 'EXCEPTION') {
            $detail = if (@($r.Why).Count) { @($r.Why) -join '; ' } else { 'unspecified' }
            $fails += "#$($r.Number) is an EXCEPTION: $detail"
        }
        elseif (-not $r.Excluded) { $fails += "#$($r.Number) is open with no exclusion reason for this release" }
    }
    , $fails
}

function Resolve-ListedMergeable([string]$Current, [scriptblock]$Fetch) {
    # gh pr list reports mergeable UNKNOWN until GitHub lazily computes it. $Fetch is
    # `gh pr view <n> --json mergeable,mergeStateStatus`, which triggers that computation.
    # A value that is still UNKNOWN afterwards is READY-pending-mergeability: the sitting
    # PR is real, and only the head merge state has not landed. A known value is not re-fetched.
    if ($Current -ne 'UNKNOWN') { return $Current }
    $got = & $Fetch
    if ($got -and [string]$got -ne 'UNKNOWN') { return [string]$got }
    'READY-pending-mergeability'
}

function Get-GhMergeable([int]$Number, [string]$Repo) {
    $view = & gh pr view $Number --repo $Repo --json mergeable,mergeStateStatus 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $view) { return 'UNKNOWN' }
    $text = if ($view -is [array]) { $view -join "`n" } else { [string]$view }
    try { $m = ($text | ConvertFrom-Json).mergeable } catch { return 'UNKNOWN' }
    if (-not $m) { return 'UNKNOWN' }
    [string]$m
}

function Get-SweepRows($prs, $tags, [string[]]$required, [string]$release, [hashtable]$excludeArg, [string]$repo) {
    # One producer of the live rows. UNKNOWN is resolved here, before the verdict,
    # so a selftest that builds a PR by hand still decides on the value it was given.
    @($prs | ForEach-Object {
        $pr = $_
        $number = [int]$pr.number
        $mergeable = Resolve-ListedMergeable ([string]$pr.mergeable) { Get-GhMergeable $number $repo }.GetNewClosure()
        $pr | Add-Member -NotePropertyName mergeable -NotePropertyValue $mergeable -Force
        Get-PrVerdict $pr $tags $required $release $excludeArg
    })
}

function ConvertTo-ExcludeTable([string[]]$items) {
    $t = @{}
    foreach ($i in $items) {
        $m = [regex]::Match($i, '^\s*#?(\d+)\s*=\s*(\S.*)$')
        if (-not $m.Success) { throw "-Exclude '$i' is not '<pr number>=<reason>'. A reason is required." }
        $t[[int]$m.Groups[1].Value] = $m.Groups[2].Value.Trim()
    }
    $t
}

if ($SelfTest) {
    $fails = 0
    function Check([string]$name, [bool]$ok) {
        if ($ok) { Write-Host "  [ OK ] $name" } else { Write-Host "  [FAIL] $name"; $script:fails++ }
    }
    $script:fails = 0
    Check 'hold pattern list is non-empty and every element is its own pattern (trap 78)' ($script:HoldPatterns.Count -eq 4 -and @($script:HoldPatterns | Where-Object { $_ -isnot [string] }).Count -eq 0)
    # One named row per pattern. A shared name hides which pattern went dead (trap 78).
    $bodyPatternRows = @(
        @{ Label = 'line-start do not merge'; Sample = '**DO NOT MERGE before `v2.0.2` is tagged.**' },
        @{ Label = 'hold for a version'; Sample = '[HOLD for 2.0.3] watch picker' },
        @{ Label = 'held until a version'; Sample = 'merge HELD until v2.0.2 is tagged' },
        @{ Label = 'not before a version'; Sample = 'not before v2.0.4' }
    )
    for ($i = 0; $i -lt $script:HoldPatterns.Count; $i++) {
        $p = $script:HoldPatterns[$i]
        $row = $bodyPatternRows[$i]
        Check "body hold pattern '$($row.Label)' fires on its sample" (($row.Sample -match $p) -and ($p -is [string]))
    }
    Check "body hold pattern 'line-start don`'t merge' fires on its sample" ("- don't merge this until the smoke passes" -match $script:HoldPatterns[0])
    Check 'DRA-769: bare "held" with no versioned marker is not a body hold' (-not @($script:HoldPatterns | Where-Object { 'held for Helm' -match $_ }))
    Check 'title hold pattern list is non-empty and every element is its own pattern (trap 78)' ($script:TitleHoldPatterns.Count -eq 5 -and @($script:TitleHoldPatterns | Where-Object { $_ -isnot [string] }).Count -eq 0)
    $titlePatternRows = @(
        @{ Label = 'do not merge'; Sample = 'WIP - do not merge' },
        @{ Label = 'don''t merge'; Sample = "please don't merge" },
        @{ Label = 'hold'; Sample = '[HOLD] waiting on Founder smoke' },
        @{ Label = 'held'; Sample = 'Held pending Helm SIGN' },
        @{ Label = 'not before a version'; Sample = 'not before v2.0.4' }
    )
    for ($i = 0; $i -lt $script:TitleHoldPatterns.Count; $i++) {
        $p = $script:TitleHoldPatterns[$i]
        $row = $titlePatternRows[$i]
        Check "title hold pattern '$($row.Label)' fires on its sample" (($row.Sample -match $p) -and ($p -is [string]))
    }

    # Sample order matches $script:RequestLinkPatterns. Each pattern fires on its own sample only.
    $linkSamples = @(
        'https://github.com/DranakCorps-bot/EQBuddy/discussions/710',
        'https://github.com/DranakCorps-bot/EQBuddy/issues/12',
        'https://www.reddit.com/r/everquest/comments/abc',
        'https://discord.com/channels/1/2/3'
    )
    Check 'request-link pattern list is non-empty and every element is its own pattern (trap 78)' ($script:RequestLinkPatterns.Count -eq $linkSamples.Count -and $script:RequestLinkPatterns.Count -gt 0 -and @($script:RequestLinkPatterns | Where-Object { $_ -isnot [string] }).Count -eq 0)
    for ($i = 0; $i -lt $script:RequestLinkPatterns.Count; $i++) {
        $p = $script:RequestLinkPatterns[$i]
        $hits = @($linkSamples | Where-Object { $_ -match $p })
        Check "request-link pattern $i fires on its sample and not the others" ($hits.Count -eq 1 -and $hits[0] -eq $linkSamples[$i])
    }
    $alignSamples = @(
        'ALIGNMENT: aligned: PRODUCT.md: the chain',
        'ALIGNMENT: not-aligned: PRODUCT.md Platform support: Windows only',
        '> ALIGNMENT: unclear: ROADMAP.md: new surface'
    )
    foreach ($s in $alignSamples) { Check "alignment pattern fires on '$s'" ([regex]::IsMatch($s, $script:AlignmentLinePattern)) }
    Check 'alignment pattern captures not-aligned whole, not the aligned suffix' (([regex]::Match('ALIGNMENT: not-aligned: x', $script:AlignmentLinePattern).Groups[1].Value) -eq 'not-aligned')
    Check 'alignment pattern does not fire on prose that merely says aligned' (-not [regex]::IsMatch('we are aligned with the vision', $script:AlignmentLinePattern))

    $t = { param($tag, $at) [pscustomobject]@{ Tag = $tag; Version = (ConvertTo-Version $tag); Published = [datetime]$at } }
    $tags = @((& $t 'v2.0.2' '2026-10-01T03:00:00Z'), (& $t 'v2.0.3' '2026-10-01T20:00:00Z'))
    $green = @(@{ name = 'build-and-test'; status = 'COMPLETED'; conclusion = 'SUCCESS' }, @{ name = 'e2e-windows'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
    $signed = @{ createdAt = '2026-10-01T01:51:11Z'; body = '## Reviewer: PR #992 code + tests SIGNED OFF; merge HELD until v2.0.2 is tagged' }
    $mk = { param($n, $draft, $title, $body, $comments, $checks, $mergeable, $head, $files)
        if (-not $head) { $head = 'cursor-exec/local' }
        if ($null -eq $files) { $files = @(@{ path = 'src/App.cs' }) }
        [pscustomobject]@{ number = $n; isDraft = $draft; title = $title; body = $body; comments = $comments; reviews = @(); statusCheckRollup = $checks; mergeable = $mergeable; headRefName = $head; files = $files } }
    $ex = @{}

    # The #992 shape, verbatim wording.
    $pr992 = & $mk 992 $true 'DRA-638: Watch picker' '**DO NOT MERGE before `v2.0.2` is tagged.** The Founder moved it to 2.0.3.' @($signed) $green 'MERGEABLE'
    $v = Get-PrVerdict $pr992 $tags $RequiredChecks $null $ex
    Check '#992 shape: signed off, held past v2.0.2 and v2.0.3 -> EXCEPTION' ($v.Verdict -eq 'EXCEPTION')
    Check '#992 shape: the reason says SIGNED OFF and names the tag' ($v.Why[0] -match 'SIGNED OFF and held past its release' -and $v.Why[0] -match 'v2\.0\.2')
    Check '#992 shape: signed-off is read from the Reviewer comment' ($v.SignOff -eq 'signed-off')

    # DRA-769 fix 1. #1013's body describes #992 and quotes its marker mid-sentence.
    # Restoring `\bhold\b` / `\bheld\b`, or un-anchoring `do not merge`, reddens this row.
    $prose1013 = '**What went wrong:** PR #992 was signed off by Reviewer, then held as a draft with *"DO NOT MERGE before `v2.0.2` is tagged"*. v2.0.2 was tagged, then v2.0.3, and nobody lifted the hold, so 2.0.3 shipped without it.'
    $v = Get-PrVerdict (& $mk 1013 $false 'sweep follow-up' $prose1013 @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'DRA-769: #1013 prose that describes a hold is not itself a hold' ($v.Verdict -eq 'READY' -and @($v.Holds).Count -eq 0)
    $v = Get-PrVerdict (& $mk 992 $true 'hold for 2.0.3' '' @() $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'DRA-769: a draft title "hold for 2.0.3" stays an EXCEPTION once that tag exists' ($v.Verdict -eq 'EXCEPTION' -and @($v.Holds).Count -eq 1)
    # Reviewer on #1034: the title keeps the broad match. Dropping TitleHoldPatterns
    # (so the title uses the body list) reddens these. The same words in the body stay prose.
    $signOnly = @{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }
    foreach ($title in @('[HOLD] waiting on Founder smoke', 'WIP - do not merge', 'Held pending Helm SIGN')) {
        $v = Get-PrVerdict (& $mk 1100 $false $title 'nothing to see' @($signOnly) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
        Check "DRA-769: title '$title' stays HELD on a signed green mergeable PR" ($v.Verdict -eq 'HELD' -and @($v.Holds).Count -eq 1 -and $v.Holds[0].Where -eq 'title')
    }
    $bodySame = "[HOLD] waiting on Founder smoke`nWIP - do not merge`nHeld pending Helm SIGN"
    $v = Get-PrVerdict (& $mk 1101 $false 'sweep follow-up' $bodySame @($signOnly) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'DRA-769: those title wordings in the body are not holds' ($v.Verdict -eq 'READY' -and @($v.Holds).Count -eq 0)

    # DRA-769 fix 2. One line, four version mentions (v2.0.2 x2, v2.0.3 x2). Dropping the
    # per-tag skip in the why loop reddens this row.
    $dupLine = '**DO NOT MERGE before `v2.0.2` is tagged.** v2.0.2 was tagged, then v2.0.3, and 2.0.3 shipped without it.'
    $v = Get-PrVerdict (& $mk 1018 $false 'dup' $dupLine @() $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    $whyTxt = $v.Why -join ' | '
    Check 'DRA-769: one EXCEPTION row per (PR, version), not per version mention' ($v.Verdict -eq 'EXCEPTION' -and @($v.Why).Count -eq 2 -and ([regex]::Matches($whyTxt, 'v2\.0\.2 published')).Count -eq 1 -and ([regex]::Matches($whyTxt, 'v2\.0\.3 published')).Count -eq 1)

    $lateStill = @{ createdAt = '2026-10-01T21:00:00Z'; body = 'STILL HELD: Founder wants it after the 2.0.4 smoke' }
    $v = Get-PrVerdict (& $mk 992 $true 't' $pr992.body @($signed, $lateStill) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a STILL HELD reason dated after the tag clears the EXCEPTION and keeps it HELD' ($v.Verdict -eq 'HELD')

    $earlyLift = @{ createdAt = '2026-10-01T02:00:00Z'; body = 'HOLD LIFTED' }
    $v = Get-PrVerdict (& $mk 992 $true 't' $pr992.body @($signed, $earlyLift) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a marker dated BEFORE the tag does not answer it' ($v.Verdict -eq 'EXCEPTION')

    $lateLift = @{ createdAt = '2026-10-01T21:00:00Z'; body = 'HOLD LIFTED - v2.0.3 is out, merge it' }
    $v = Get-PrVerdict (& $mk 992 $false 't' $pr992.body @($signed, $lateLift) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a HOLD LIFTED after the tag on a signed green PR -> READY' ($v.Verdict -eq 'READY')

    $v = Get-PrVerdict (& $mk 1 $true '[HOLD for 2.0.4] thing' '' @() $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a hold for an untagged release -> HELD, not an exception' ($v.Verdict -eq 'HELD')

    $v = Get-PrVerdict (& $mk 2 $false 'x' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'signed off + green + mergeable + no hold -> READY' ($v.Verdict -eq 'READY')

    # DRA-769 fix 3. The list value UNKNOWN is not READY by itself. The fetch (gh pr view)
    # has to run, and only a still-UNKNOWN result becomes READY-pending-mergeability.
    # Skipping the fetch, or treating that label as OPEN, reddens these rows.
    $probe = @{ Called = $false }
    $resolved = Resolve-ListedMergeable 'UNKNOWN' { $probe.Called = $true; 'MERGEABLE' }
    $v = Get-PrVerdict (& $mk 943 $false 'sitting' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green $resolved) $tags $RequiredChecks $null $ex
    Check 'DRA-769: UNKNOWN that gh pr view resolves to MERGEABLE reads READY' ($probe.Called -and $resolved -eq 'MERGEABLE' -and $v.Verdict -eq 'READY' -and $v.Mergeable -eq 'MERGEABLE')
    $still = Resolve-ListedMergeable 'UNKNOWN' { 'UNKNOWN' }
    $v = Get-PrVerdict (& $mk 943 $false 'sitting' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green $still) $tags $RequiredChecks $null $ex
    Check 'DRA-769: UNKNOWN that stays UNKNOWN is READY-pending-mergeability, not OPEN' ($still -eq 'READY-pending-mergeability' -and $v.Verdict -eq 'READY' -and $v.Mergeable -eq 'READY-pending-mergeability')
    $again = @{ Called = $false }
    $kept = Resolve-ListedMergeable 'CONFLICTING' { $again.Called = $true; 'MERGEABLE' }
    Check 'DRA-769: a known mergeable value is not sent through the per-PR view' ($kept -eq 'CONFLICTING' -and -not $again.Called)
    $unsigned = Resolve-ListedMergeable 'UNKNOWN' { 'UNKNOWN' }
    $v = Get-PrVerdict (& $mk 944 $false 'unsigned' '' @() $green $unsigned) $tags $RequiredChecks $null $ex
    Check 'DRA-769: READY-pending-mergeability without a sign-off stays OPEN' ($v.Verdict -eq 'OPEN' -and $v.Mergeable -eq 'READY-pending-mergeability')
    $ghSrc = ${function:Get-GhMergeable}.ToString()
    $rowSrc = ${function:Get-SweepRows}.ToString()
    Check 'DRA-769: the live rows resolve UNKNOWN with gh pr view mergeable,mergeStateStatus' ($ghSrc -match 'gh pr view' -and $ghSrc -match 'mergeStateStatus' -and $rowSrc -match 'Get-GhMergeable' -and $rowSrc -match 'Resolve-ListedMergeable')

    $changes = @{ createdAt = '2026-10-01T05:00:00Z'; body = 'Reviewer: REQUEST CHANGES on the values line' }
    $v = Get-PrVerdict (& $mk 3 $false 'x' '' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'SIGNED OFF' }, $changes) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a later REQUEST CHANGES voids the sign-off' ($v.SignOff -eq 'changes-requested' -and $v.Verdict -eq 'OPEN')

    $red = @(@{ name = 'build-and-test'; status = 'COMPLETED'; conclusion = 'FAILURE' }, @{ name = 'e2e-windows'; status = 'IN_PROGRESS'; conclusion = '' })
    $v = Get-PrVerdict (& $mk 4 $false 'x' '' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'SIGNED OFF' }) $red 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a red required check keeps a signed PR off READY' ($v.Checks.State -eq 'red' -and $v.Verdict -eq 'OPEN')
    $v = Get-PrVerdict (& $mk 5 $false 'x' '' @() @() 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'no checks at all reads as none, never green' ($v.Checks.State -eq 'none')

    # The gate.
    $rel = 'v2.0.4'
    $plain = & $mk 6 $false 'Scribe intake' '' @() $green 'MERGEABLE'
    $rows = @(Get-PrVerdict $plain $tags $RequiredChecks $rel @{})
    Check 'gate: an open PR with no exclusion reason FAILS' ((Test-ReleaseGate $rows).Count -eq 1)
    $rows = @(Get-PrVerdict $plain $tags $RequiredChecks $rel (ConvertTo-ExcludeTable @('6=Scribe intake, no player effect')))
    Check 'gate: -Exclude with a reason passes it' ((Test-ReleaseGate $rows).Count -eq 0)
    $byComment = & $mk 7 $false 'x' '' @(@{ createdAt = '2026-10-02T01:00:00Z'; body = 'RELEASE-EXCLUDE v2.0.4: waits on DRA-999' }) $green 'MERGEABLE'
    Check 'gate: a RELEASE-EXCLUDE comment for this version passes it' ((Test-ReleaseGate @(Get-PrVerdict $byComment $tags $RequiredChecks $rel @{})).Count -eq 0)
    Check 'gate: a RELEASE-EXCLUDE for a DIFFERENT version does not' ((Test-ReleaseGate @(Get-PrVerdict $byComment $tags $RequiredChecks 'v2.0.5' @{})).Count -eq 1)
    $rows = @(Get-PrVerdict $pr992 $tags $RequiredChecks $rel (ConvertTo-ExcludeTable @('992=later')))
    Check 'gate: an EXCEPTION fails even with an -Exclude argument (the cure is a dated PR comment)' ((Test-ReleaseGate $rows).Count -eq 1)
    $cured = & $mk 992 $true 't' $pr992.body @($signed, @{ createdAt = '2026-10-02T01:00:00Z'; body = 'RELEASE-EXCLUDE v2.0.4: Founder holds it for the macOS smoke' }) $green 'MERGEABLE'
    Check 'gate: a dated RELEASE-EXCLUDE comment answers the old hold AND excludes it' ((Test-ReleaseGate @(Get-PrVerdict $cured $tags $RequiredChecks $rel @{})).Count -eq 0)
    $threw = $false; try { ConvertTo-ExcludeTable @('978') | Out-Null } catch { $threw = $true }
    Check '-Exclude without a reason is refused' $threw

    $md = Format-Sweep @(Get-PrVerdict $pr992 $tags $RequiredChecks $rel @{}) $rel ([datetime]'2026-10-02T00:00:00Z')
    Check 'the card post names the EXCEPTION and the missing reason' ($md -match 'EXCEPTIONS \(1\)' -and $md -match 'NO REASON' -and $md -match '#992')

    # DRA-733 alignment arms. Reverting any one of these branches reddens its check.
    $scribeBare = & $mk 40 $false 'scribe note' 'files the ask' @() $green 'MERGEABLE' 'scribe/dra-733' @(@{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $scribeBare $tags $RequiredChecks $null $ex
    Check 'a scribe/* PR with no verdict gives EXCEPTION' ($v.Verdict -eq 'EXCEPTION' -and (($v.Why -join ' ') -eq 'request-driven, no alignment verdict'))
    $scribeAligned = & $mk 40 $false 'scribe note' 'ALIGNMENT: aligned: PRODUCT.md: the chain' @() $green 'MERGEABLE' 'scribe/dra-733' @(@{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $scribeAligned $tags $RequiredChecks $null $ex
    Check 'the same PR with an aligned line is not an exception' ($v.Verdict -ne 'EXCEPTION' -and $v.Alignment -eq 'aligned')
    $byComment = & $mk 41 $false 'scribe note' 'files the ask' @(@{ createdAt = '2026-10-01T04:00:00Z'; body = 'ALIGNMENT: aligned: PRODUCT.md: the chain' }) $green 'MERGEABLE' 'scribe/dra-733' @(@{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $byComment $tags $RequiredChecks $null $ex
    Check 'an ALIGNMENT line in a comment counts the same as the body' ($v.Verdict -ne 'EXCEPTION' -and $v.Alignment -eq 'aligned')
    $codeBody = "See https://github.com/DranakCorps-bot/EQBuddy/discussions/710`nALIGNMENT: unclear: ROADMAP.md Where a feature goes: this adds a surface"
    $codeUnclear = & $mk 42 $false 'watch list' $codeBody @() $green 'MERGEABLE' 'fix/watch' @(@{ path = 'src/Foo.cs' })
    $v = Get-PrVerdict $codeUnclear $tags $RequiredChecks $null $ex
    Check 'a code PR linking a discussion with an unclear line gives EXCEPTION' ($v.Verdict -eq 'EXCEPTION' -and (($v.Why -join ' ') -match 'alignment unclear: Founder ask, do not merge'))
    Check 'gate: that alignment EXCEPTION fails -Release even with -Exclude' ((Test-ReleaseGate @(Get-PrVerdict $codeUnclear $tags $RequiredChecks $rel (ConvertTo-ExcludeTable @('42=later')))).Count -eq 1)
    $intakeBody = "https://github.com/DranakCorps-bot/EQBuddy/discussions/710`nALIGNMENT: unclear: PRODUCT.md Platform support: needs a Founder answer"
    $intake = & $mk 43 $false 'intake' $intakeBody @() $green 'MERGEABLE' 'scribe/intake' @(@{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $intake $tags $RequiredChecks $null $ex
    Check 'an intake PR with an unclear line is not an exception' ($v.Verdict -ne 'EXCEPTION' -and $v.Alignment -eq 'unclear')
    Check 'gate: an intake unclear line is not an alignment EXCEPTION once excluded' ((Test-ReleaseGate @(Get-PrVerdict $intake $tags $RequiredChecks $rel (ConvertTo-ExcludeTable @('43=scribe intake filing')))).Count -eq 0)
    $plain = & $mk 44 $false 'x' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green 'MERGEABLE'
    $v = Get-PrVerdict $plain $tags $RequiredChecks $null $ex
    Check 'a PR with no request link and no verdict is unaffected' ($v.Verdict -eq 'READY' -and $v.Alignment -eq 'none')
    $commentOnly = & $mk 45 $false 'x' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = "Reviewer: SIGNED OFF`nhttps://github.com/DranakCorps-bot/EQBuddy/discussions/710" }) $green 'MERGEABLE'
    $v = Get-PrVerdict $commentOnly $tags $RequiredChecks $null $ex
    Check 'a discussion link that exists only in a comment does not make the PR request-driven' ($v.Verdict -eq 'READY' -and $v.Alignment -eq 'none')
    $issueLink = & $mk 46 $false 'from an issue' 'See https://github.com/DranakCorps-bot/EQBuddy/issues/12' @() $green 'MERGEABLE' 'fix/issue-ask' @(@{ path = 'src/Foo.cs' })
    $v = Get-PrVerdict $issueLink $tags $RequiredChecks $null $ex
    Check 'an issue link in the body with no verdict gives EXCEPTION' ($v.Verdict -eq 'EXCEPTION' -and (($v.Why -join ' ') -eq 'request-driven, no alignment verdict'))
    $reddit = & $mk 47 $false 'from reddit' 'https://old.reddit.com/r/everquest/comments/abc/title' @() $green 'MERGEABLE' 'feat/reddit' @(@{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $reddit $tags $RequiredChecks $null $ex
    Check 'a reddit.com URL in the body with no verdict gives EXCEPTION' ($v.Verdict -eq 'EXCEPTION')
    $notAligned = & $mk 48 $false 'port' 'ALIGNMENT: not-aligned: PRODUCT.md Platform support: Windows only' @() $green 'MERGEABLE' 'feat/linux' @(@{ path = 'src/Foo.cs' }, @{ path = 'SCRIBE.md' })
    $v = Get-PrVerdict $notAligned $tags $RequiredChecks $null $ex
    Check 'a non-intake not-aligned line is an EXCEPTION even with no request link' ($v.Verdict -eq 'EXCEPTION' -and $v.Alignment -eq 'not-aligned' -and (($v.Why -join ' ') -match 'alignment not-aligned: Founder ask, do not merge'))
    $mdAlign = Format-Sweep @(Get-PrVerdict $scribeAligned $tags $RequiredChecks $null $ex) $null ([datetime]'2026-10-02T00:00:00Z')
    Check 'the sweep prints an Alignment column' ($mdAlign -match '\| Alignment \|' -and $mdAlign -match '\| aligned \|')
    $mdMiss = Format-Sweep @(Get-PrVerdict $scribeBare $tags $RequiredChecks $null $ex) $null ([datetime]'2026-10-02T00:00:00Z')
    Check 'a missing verdict is printed and named' ($mdMiss -match '\| none \|' -and $mdMiss -match 'request-driven, no alignment verdict')

    if ($script:fails) { Write-Host "pr-sweep selftest: FAIL ($($script:fails))"; exit 1 }
    Write-Host 'pr-sweep selftest: all checks passed'
    exit 0
}

# --- live run ---
$excludeTable = ConvertTo-ExcludeTable $Exclude
if ($Release -and -not (ConvertTo-Version $Release)) { throw "-Release '$Release' is not vX.Y.Z" }

$fields = 'number,title,isDraft,mergeable,body,comments,reviews,statusCheckRollup,headRefName,files'
$prJson = gh pr list --repo $Repo --state open --limit 200 --json $fields 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: could not ask GitHub for open PRs (gh exit $LASTEXITCODE): $prJson"; exit 3 }
$relJson = gh release list --repo $Repo --limit 100 --json tagName,publishedAt,isDraft 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: could not ask GitHub for releases (gh exit $LASTEXITCODE): $relJson"; exit 3 }

# Enumerate through the pipeline so a JSON array unrolls (trap 80).
$prs = @(($prJson | ConvertFrom-Json) | ForEach-Object { $_ })
$tags = @(($relJson | ConvertFrom-Json) | ForEach-Object { $_ } | Where-Object { -not $_.isDraft -and (ConvertTo-Version $_.tagName) } |
    ForEach-Object { [pscustomobject]@{ Tag = $_.tagName; Version = (ConvertTo-Version $_.tagName); Published = ([datetime]$_.publishedAt).ToUniversalTime() } })

$rows = @(Get-SweepRows $prs $tags $RequiredChecks $Release $excludeTable $Repo)
Write-Output (Format-Sweep $rows $Release ([datetime]::UtcNow))

if ($Release) {
    $gate = Test-ReleaseGate $rows
    if ($gate.Count) {
        Write-Output "**GATE: FAIL** - $Release does not proceed:"
        $gate | ForEach-Object { Write-Output "- $_" }
        exit 1
    }
    Write-Output "**GATE: PASS** - every open PR is excluded from $Release with a reason, and no hold has outrun a release."
    exit 0
}
if (@($rows | Where-Object Verdict -eq 'EXCEPTION').Count) { exit 2 }
exit 0
