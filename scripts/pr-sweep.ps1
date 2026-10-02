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
$script:HoldPatterns = @(
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
    $verdict = if ($outrun.Count -gt 0) { 'EXCEPTION' }
               elseif ($liveHold) { 'HELD' }
               elseif ($signOff -eq 'signed-off' -and $checks.State -eq 'green' -and $mergeable -eq 'MERGEABLE' -and -not $pr.isDraft) { 'READY' }
               else { 'OPEN' }
    $why = @()
    foreach ($o in $outrun) {
        $lead = if ($signOff -eq 'signed-off') { 'SIGNED OFF and held past its release' } else { 'held past its release' }
        $why += "${lead}: '$($o.Hold.Text)' ($($o.Hold.Where)) - $($o.Tag.Tag) published $($o.Tag.Published.ToString('yyyy-MM-dd HH:mm'))Z with no HOLD LIFTED / STILL HELD: <reason> dated after it"
    }
    [pscustomobject]@{
        Number    = [int]$pr.number
        Title     = [string]$pr.title
        Draft     = [bool]$pr.isDraft
        SignOff   = $signOff
        Checks    = $checks
        Mergeable = $mergeable
        Holds     = $holds
        Verdict   = $verdict
        Why       = $why
        Excluded  = Get-ExclusionReason $pr $release $excludeArg
    }
}

function Format-Sweep($rows, [string]$release, [datetime]$now) {
    $sb = [System.Text.StringBuilder]::new()
    $head = if ($release) { "## Pre-release PR gate: $release" } else { '## EQBuddy open-PR sweep' }
    [void]$sb.AppendLine($head)
    [void]$sb.AppendLine("Run $($now.ToString('yyyy-MM-dd HH:mm'))Z by ``scripts/pr-sweep.ps1`` (DRA-723). $($rows.Count) open PR(s).")
    [void]$sb.AppendLine()
    $cols = '| PR | Verdict | Draft | Signed-off | Checks | Mergeable |' + $(if ($release) { " Excluded from $release |" } else { '' })
    [void]$sb.AppendLine($cols)
    [void]$sb.AppendLine('|---|---|---|---|---|---|' + $(if ($release) { '---|' } else { '' }))
    foreach ($r in ($rows | Sort-Object Number)) {
        $t = $r.Title -replace '\|', '/'
        if ($t.Length -gt 70) { $t = $t.Substring(0, 67) + '...' }
        $line = "| #$($r.Number) $t | **$($r.Verdict)** | $(if ($r.Draft) { 'yes' } else { 'no' }) | $($r.SignOff) | $($r.Checks.State) ($($r.Checks.Detail)) | $($r.Mergeable) |"
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
        [void]$sb.AppendLine(); [void]$sb.AppendLine("**EXCEPTIONS ($($ex.Count)):** each needs a dated ``HOLD LIFTED`` (then merge) or ``STILL HELD: <reason>`` comment on the PR.")
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
        if ($r.Verdict -eq 'EXCEPTION') { $fails += "#$($r.Number) is an EXCEPTION (held past a published release)" }
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
    Check 'hold pattern list is non-empty and every element is its own pattern (trap 78)' ($script:HoldPatterns.Count -eq 5 -and @($script:HoldPatterns | Where-Object { $_ -isnot [string] }).Count -eq 0)
    foreach ($p in $script:HoldPatterns) { Check "hold pattern '$p' fires on something" ((@('DO NOT MERGE before v2.0.2', "don't merge yet", 'HOLD for 2.0.3', 'held for Helm', 'not before v2.0.4') | Where-Object { $_ -match $p }).Count -gt 0) }

    $t = { param($tag, $at) [pscustomobject]@{ Tag = $tag; Version = (ConvertTo-Version $tag); Published = [datetime]$at } }
    $tags = @((& $t 'v2.0.2' '2026-10-01T03:00:00Z'), (& $t 'v2.0.3' '2026-10-01T20:00:00Z'))
    $green = @(@{ name = 'build-and-test'; status = 'COMPLETED'; conclusion = 'SUCCESS' }, @{ name = 'e2e-windows'; status = 'COMPLETED'; conclusion = 'SUCCESS' })
    $signed = @{ createdAt = '2026-10-01T01:51:11Z'; body = '## Reviewer: PR #992 code + tests SIGNED OFF; merge HELD until v2.0.2 is tagged' }
    $mk = { param($n, $draft, $title, $body, $comments, $checks, $mergeable)
        [pscustomobject]@{ number = $n; isDraft = $draft; title = $title; body = $body; comments = $comments; reviews = @(); statusCheckRollup = $checks; mergeable = $mergeable } }
    $ex = @{}

    # The #992 shape, verbatim wording.
    $pr992 = & $mk 992 $true 'DRA-638: Watch picker' '**DO NOT MERGE before `v2.0.2` is tagged.** The Founder moved it to 2.0.3.' @($signed) $green 'MERGEABLE'
    $v = Get-PrVerdict $pr992 $tags $RequiredChecks $null $ex
    Check '#992 shape: signed off, held past v2.0.2 and v2.0.3 -> EXCEPTION' ($v.Verdict -eq 'EXCEPTION')
    Check '#992 shape: the reason says SIGNED OFF and names the tag' ($v.Why[0] -match 'SIGNED OFF and held past its release' -and $v.Why[0] -match 'v2\.0\.2')
    Check '#992 shape: signed-off is read from the Reviewer comment' ($v.SignOff -eq 'signed-off')

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

    if ($script:fails) { Write-Host "pr-sweep selftest: FAIL ($($script:fails))"; exit 1 }
    Write-Host 'pr-sweep selftest: all checks passed'
    exit 0
}

# --- live run ---
$excludeTable = ConvertTo-ExcludeTable $Exclude
if ($Release -and -not (ConvertTo-Version $Release)) { throw "-Release '$Release' is not vX.Y.Z" }

$fields = 'number,title,isDraft,mergeable,body,comments,reviews,statusCheckRollup'
$prJson = gh pr list --repo $Repo --state open --limit 200 --json $fields 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: could not ask GitHub for open PRs (gh exit $LASTEXITCODE): $prJson"; exit 3 }
$relJson = gh release list --repo $Repo --limit 100 --json tagName,publishedAt,isDraft 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: could not ask GitHub for releases (gh exit $LASTEXITCODE): $relJson"; exit 3 }

# Enumerate through the pipeline so a JSON array unrolls (trap 80).
$prs = @(($prJson | ConvertFrom-Json) | ForEach-Object { $_ })
$tags = @(($relJson | ConvertFrom-Json) | ForEach-Object { $_ } | Where-Object { -not $_.isDraft -and (ConvertTo-Version $_.tagName) } |
    ForEach-Object { [pscustomobject]@{ Tag = $_.tagName; Version = (ConvertTo-Version $_.tagName); Published = ([datetime]$_.publishedAt).ToUniversalTime() } })

$rows = @($prs | ForEach-Object { Get-PrVerdict $_ $tags $RequiredChecks $Release $excludeTable })
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
