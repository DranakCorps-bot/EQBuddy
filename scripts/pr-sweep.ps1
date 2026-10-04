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
    every line of the title/body/comments that carries hold wording. Then it decides:

      EXCEPTION  a hold line names a release (vX.Y.Z) and a tag at or above it is published,
                 and no comment dated AFTER that tag says `HOLD LIFTED` or `STILL HELD: <why>`
                 (or `RELEASE-EXCLUDE vA.B.C: <why>`, which is a still-held reason too).
                 Signed-off and held past its release is the #992 shape and says so.
      READY      signed off, both required checks green, mergeable, not a draft, no live
                 hold. Nothing is wrong with it except that it is sitting.
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
    [switch]$NoMergeResolve,
    [string[]]$RequiredChecks = @('build-and-test', 'e2e-windows'),
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

# Each array element parenthesised: PowerShell binds `,` tighter than `+` (trap 78).
# DRA-769 (DRA-723 follow-up). Every pattern is an EXPLICIT hold marker anchored to the start
# of the line after optional leading markdown/bullets (^\W*): free prose that merely
# describes a held PR no longer matches -- #1013's own body quoted #992's "DO NOT MERGE
# before v2.0.2" line verbatim and the old word-boundary hold/held patterns flagged that PR
# as its own EXCEPTION. A real hold is a marker: DO NOT MERGE / don't merge, an anchored
# HOLD for|until|before vX.Y.Z, or not before vX.Y.Z -- the wording a seat actually writes,
# of which #992's title line 'DO NOT MERGE before v2.0.2 is tagged' is one.
$script:HoldPatterns = @(
    ("(?i)^\W*(?:do\s+not|don'?t)\s+merge\b"),
    ("(?i)^[Hh][Oo][Ll][Dd]\s+(?:for|until|before)\s+v?\d+\.\d+\.\d+"),
    ('(?i)^\W*not\s+before\s+v?\d+\.\d+\.\d+')
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
    # exclusion marker is the cure, not the hold.
    $sources = @(@{ Where = 'title'; At = $null; Text = [string]$pr.title },
                 @{ Where = 'body'; At = $null; Text = [string]$pr.body })
    foreach ($c in @($pr.comments | ForEach-Object { $_ })) {
        $sources += @{ Where = 'comment'; At = [datetime]$c.createdAt; Text = [string]$c.body }
    }
    $out = @()
    foreach ($s in $sources) {
        foreach ($line in ($s.Text -split "`r?`n")) {
            if ([regex]::IsMatch($line, $script:LiftPattern)) { continue }
            $hit = $false
            foreach ($p in $script:HoldPatterns) { if ([regex]::IsMatch($line, $p)) { $hit = $true; break } }
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

    # DRA-769: UNKNOWN after the per-PR resolve is READY-pending-mergeability -- the sitting
    # PR is real and the only thing undecided is the head merge state.
    if ([string]$pr.mergeable -eq 'UNKNOWN') { $mergeable = 'READY-pending-mergeability' } else { $mergeable = [string]$pr.mergeable }
    $align = Get-AlignmentVerdicts $pr
    $alignment = if ($align.Count -eq 0) { 'none' } else { ($align -join ', ') }
    $alignWhy = Get-AlignmentException $pr $align
    # DRA-769: READY also fires when mergeability is computed-on-demand but UNKNOWN remains --
    # the sitting PR is signed off, green and not draft; only the head merge state lags.
    $readyMerge = if ($mergeable -in 'MERGEABLE', 'READY-pending-mergeability') { $true } else { $false }
    $verdict = if ($outrun.Count -gt 0 -or $alignWhy.Count -gt 0) { 'EXCEPTION' }
               elseif ($liveHold) { 'HELD' }
               elseif ($signOff -eq 'signed-off' -and $checks.State -eq 'green' -and $readyMerge -and -not $pr.isDraft) { 'READY' }
               else { 'OPEN' }
    # DRA-769: one row per released TAG, not per (hold line, version) pair. Two hold lines
    # that each outrun to v2.0.3, or one line naming v2.0.2 and v2.0.3 both outrun, used to
    # print the same tag repeatedly (#1013's own self-flag read v2.0.2 x2 + v2.0.3 x2).
    $why = @()
    $tagSeen = @{}
    foreach ($o in @($outrun | Group-Object { [string]$_.Tag.Tag } | Sort-Object Name)) {
        if ($tagSeen.ContainsKey($o.Name)) { continue }
        $tagSeen[$o.Name] = $true
        $lead = if ($signOff -eq 'signed-off') { 'SIGNED OFF and held past its release' } else { 'held past its release' }
        $cited = @($o.Group | ForEach-Object { "$($_.Hold.Text) ($($_.Hold.Where))" } | Select-Object -Unique | ForEach-Object { "$_".Trim() })
        $why += "${lead}: $(($cited | ForEach-Object { "'" + ($_ -replace "'", "''") + "'" }) -join '; ') - $($o.Name) published $($o.Group[0].Tag.Published.ToString('yyyy-MM-dd HH:mm'))Z with no HOLD LIFTED / STILL HELD: <reason> dated after it"
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
    [void]$sb.AppendLine("Run $($now.ToString('yyyy-MM-dd HH:mm'))Z by ``scripts/pr-sweep.ps1`` (DRA-723, DRA-733). $($rows.Count) open PR(s).")
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
    Check 'hold pattern list is non-empty and every element is its own pattern (trap 78)' ($script:HoldPatterns.Count -eq 3 -and @($script:HoldPatterns | Where-Object { $_ -isnot [string] }).Count -eq 0)
    $holdSamples = @(
        '**DO NOT MERGE before `v2.0.2` is tagged.** The Founder moved it to 2.0.3.',
        "- don't merge this until the smoke passes",
        '[HOLD for 2.0.3] watch picker',
        'HOLD until v2.0.4 lands',
        '- not before v2.0.4'
    )
    foreach ($p in $script:HoldPatterns) {
        $hits = @($holdSamples | Where-Object { $_ -match $p })
        Check "hold pattern fires on a held-PR marker, not on its descriptive prose" ($hits.Count -ge 1)
    }
    # DRA-769: prose that describes a held PR (#1013's wording, which also quotes #992's
    # marker) carries no hold of its own, and a bare version mention is not a hold.
    $proseLine = 'PR #992 was signed off, then held as a draft with *"DO NOT MERGE before `v2.0.2` is tagged***. v2.0.2 was tagged.'
    Check 'DRA-769: prose quoting a DO NOT MERGE line is not itself a hold' (-not @($script:HoldPatterns | Where-Object { $proseLine -match $_ }))
    Check 'DRA-769: a version mention alone is not a hold' (-not @($script:HoldPatterns | Where-Object { 'shipped in v2.0.3, then v2.0.4' -match $_ }))

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
    Check 'DRA-769: the single #992 line is ONE hold line naming both releases' (@($v.Holds).Count -eq 1 -and ($v.Holds[0].Text -match 'v2\.0\.2') -and ($v.Holds[0].Text -match 'v2\.0\.3'))
    Check 'DRA-769: #992 yields exactly two EXCEPTION rows, one per tag, not one per (line, version) pair' (@($v.Why).Count -eq 2 -and (@([regex]::Matches(($v.Why -join ' | '), 'v2\.0\.2')).Count -eq 1) -and (@([regex]::Matches(($v.Why -join ' | '), 'v2\.0\.3')).Count -eq 1))

    # DRA-769 fix 1: hold markers stay holds; descriptive prose describing them does not.
    $tTitle = & $mk 1013 $false 'ops: sweep' '**DO NOT MERGE before `v2.0.2` is tagged.** The Founder moved it to 2.0.3.' @($signed) $green 'MERGEABLE'
    $v = Get-PrVerdict $tTitle $tags $RequiredChecks $null $ex
    Check 'DRA-769: a titled DO NOT MERGE hold is still EXCEPTION, from one hold line' ($v.Verdict -eq 'EXCEPTION' -and (@($v.Holds)).Count -eq 1)
    $tProse = & $mk 1014 $false 'ops: sweep' 'PR #992 was signed off, then held as a draft with *"DO NOT MERGE before v2.0.2 is tagged***. v2.0.2 was tagged, then v2.0.3, and nobody lifted the hold.' @($signed) $green 'MERGEABLE'
    $v = Get-PrVerdict $tProse $tags $RequiredChecks $null $ex
    Check 'DRA-769: #1013-style prose quoting a hold marker is NOT an EXCEPTION' ($v.Verdict -ne 'EXCEPTION' -and (@($v.Holds)).Count -eq 0)
    $tHoldMarker = & $mk 1015 $false 'watch' 'HOLD until v2.0.4 lands' @() $green 'MERGEABLE'
    $v = Get-PrVerdict $tHoldMarker $tags $RequiredChecks $null $ex
    Check 'DRA-769: an anchored HOLD until vX.Y.Z line is a live hold' ($v.Verdict -eq 'HELD')
    $tBare = & $mk 1016 $false 'notes' 'shipped in v2.0.2; the move went to v2.0.3' @() $green 'MERGEABLE'
    $v = Get-PrVerdict $tBare $tags $RequiredChecks $null $ex
    Check 'DRA-769: prose naming versions without a hold marker is not a hold' ($v.Verdict -ne 'HELD' -and (@($v.Holds)).Count -eq 0)
    $cProse = @{ createdAt = '2026-10-01T02:30:00Z'; body = 'Heads up: PR #992 was held for the v2.0.2 tag and v2.0.3 shipped without it.' }
    $tCmt = & $mk 1017 $false 'sweep' '' @($signed, $cProse) $green 'MERGEABLE'
    $v = Get-PrVerdict $tCmt $tags $RequiredChecks $null $ex
    Check 'DRA-769: a comment describing a held PR is not a hold line' (@($v.Holds | Where-Object { $_.Where -eq 'comment' }).Count -eq 0)
    # DRA-769 fix 2: the same released tag, cited by two hold lines, reads ONCE.
    $dupBody = '**DO NOT MERGE before `v2.0.2` is tagged.** The Founder moved it to 2.0.3.'
    $tDup = & $mk 1018 $false 'ops: sweep' $dupBody @(@{ createdAt = '2026-10-01T02:00:00Z'; body = $dupBody }) $green 'MERGEABLE'
    $v = Get-PrVerdict $tDup $tags $RequiredChecks $null $ex
    $whyTxt = $v.Why -join ' | '
    Check 'DRA-769: two hold lines both outrun to v2.0.2/v2.0.3 read ONE row per tag' ($v.Verdict -eq 'EXCEPTION' -and (@([regex]::Matches($whyTxt, 'v2\.0\.2')).Count -eq 1) -and (@([regex]::Matches($whyTxt, 'v2\.0\.3')).Count -eq 1) -and (@([regex]::Matches($whyTxt, '\bv2\.0\.2 published')).Count -eq 1))

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

    $changes = @{ createdAt = '2026-10-01T05:00:00Z'; body = 'Reviewer: REQUEST CHANGES on the values line' }
    $v = Get-PrVerdict (& $mk 3 $false 'x' '' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'SIGNED OFF' }, $changes) $green 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a later REQUEST CHANGES voids the sign-off' ($v.SignOff -eq 'changes-requested' -and $v.Verdict -eq 'OPEN')

    $red = @(@{ name = 'build-and-test'; status = 'COMPLETED'; conclusion = 'FAILURE' }, @{ name = 'e2e-windows'; status = 'IN_PROGRESS'; conclusion = '' })
    $v = Get-PrVerdict (& $mk 4 $false 'x' '' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'SIGNED OFF' }) $red 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'a red required check keeps a signed PR off READY' ($v.Checks.State -eq 'red' -and $v.Verdict -eq 'OPEN')
    $v = Get-PrVerdict (& $mk 5 $false 'x' '' @() @() 'MERGEABLE') $tags $RequiredChecks $null $ex
    Check 'no checks at all reads as none, never green' ($v.Checks.State -eq 'none')

    # DRA-769 fix 3: a PR that is signed off, green, not draft, and whose mergeable is
    # UNKNOWN (gh pr list has not computed it yet) reads READY, not OPEN. Reverting the
    # $readyMerge branch back to -eq 'MERGEABLE' reddens this row.
    $v = Get-PrVerdict (& $mk 9 $false 'ready-pending' 'nothing to see' @(@{ createdAt = '2026-10-01T01:00:00Z'; body = 'Reviewer: SIGNED OFF' }) $green 'UNKNOWN') $tags $RequiredChecks $null $ex
    Check 'DRA-769: signed+green+not-draft+UNKNOWN-mergeable reads READY, mergeability shown as READY-pending-mergeability' ($v.Verdict -eq 'READY' -and $v.Mergeable -eq 'READY-pending-mergeability')
    $v2 = Get-PrVerdict (& $mk 10 $false 'unknown-not-qualified' 'nothing to see' @() $green 'UNKNOWN') $tags $RequiredChecks $null $ex
    Check 'DRA-769: UNKNOWN-mergeable WITHOUT sign-off stays OPEN' ($v2.Verdict -eq 'OPEN')

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

# DRA-769: gh pr list reports mergeable UNKNOWN until GitHub lazily computes it, so a signed
# green PR read OPEN in every live run. Ask once per PR -- gh pr view forces the computation
# -- and fall back to the list value. A PR that is still UNKNOWN after that is READY in the
# only other sense (signed, green, not draft, no hold): READY-pending-mergeability, so the
# sitting PR is visible instead of disappearing into OPEN.
function Get-ResolvedMergeable([psobject]$pr, [string]$repo) {
    if ([string]$pr.mergeable -ne 'UNKNOWN') { return [string]$pr.mergeable }
    $view = gh pr view ([int]$pr.number) --repo $repo --json mergeable,mergeStateStatus 2>&1
    if ($LASTEXITCODE -eq 0) {
        $m = ([string]$view | ConvertFrom-Json).mergeable
        if ($m -and $m -ne 'UNKNOWN') { return [string]$m }
    }
    'UNKNOWN'
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

$rows = @($prs | ForEach-Object {
    $pr = $_
    if (-not $NoMergeResolve) { $pr = [pscustomobject]@{ number = $pr.number; isDraft = $pr.isDraft; title = $pr.title; body = $pr.body; comments = $pr.comments; reviews = $pr.reviews; statusCheckRollup = $pr.statusCheckRollup; headRefName = $pr.headRefName; files = $pr.files; mergeable = (Get-ResolvedMergeable $pr $Repo) } }
    Get-PrVerdict $pr $tags $RequiredChecks $Release $excludeTable })
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
