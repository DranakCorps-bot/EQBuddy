<#
.SYNOPSIS
    Drive every merge-sync refusal into the red at least once, plus the
    legitimate spelling beside it.

.DESCRIPTION
    DRA-77 / M0-4. `exo-experiment: merge-sync`.

    Called from scripts/check.ps1 and from CI. A sync job that quietly decides
    nothing looks exactly like a sync job with nothing to decide, which is the
    whole reason this file exists (trap 34: pair every "no X may do Y" with a
    curated must-list; trap 78: assert the detector's list is non-empty AND
    that it fires, in the same commit that adds it).

    Needs no network, no secret and no GitHub event: everything asserted here is
    in `merge-sync-linkage.ps1`, plus the argument handling of `merge-sync.ps1`
    exercised through -DryRun and a deliberately unreachable API base.

.EXAMPLE
    pwsh -NoProfile -File scripts/merge-sync-selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'merge-sync-linkage.ps1')

$syncScript = Join-Path $PSScriptRoot 'merge-sync.ps1'
$failed = @()
$script:step = 0

function Check {
    param([string] $Name, [scriptblock] $Body)
    $script:step++
    $label = "$($script:step). $Name"
    try {
        $complaint = & $Body
        if ($complaint) { $script:failed += "$label — $complaint" }
        else { Write-Host "  ok  $label" }
    } catch {
        $script:failed += "$label — threw: $($_.Exception.Message)"
    }
}

function Expect-Link {
    param([string] $Branch, [string] $Body, [string] $Outcome, [string] $Key, [string] $Source)
    $r = Resolve-MergeSyncLink -Branch $Branch -Body $Body
    if ($r.Outcome -ne $Outcome) { return "expected outcome '$Outcome', got '$($r.Outcome)' ($($r.Reason))" }
    if ($Key -and $r.Key -ne $Key) { return "expected key '$Key', got '$($r.Key)'" }
    if ($Source -and $r.Source -ne $Source) { return "expected source '$Source', got '$($r.Source)'" }
    return $null
}

Write-Host 'merge-sync self-test'
Write-Host '--- key extraction ---'

Check 'the branch spelling `dra77` is DRA-77' {
    Expect-Link -Branch 'claude/dra77-merge-sync-20260914' -Body '' -Outcome 'linked' -Key 'DRA-77' -Source 'branch'
}

Check 'the trailing DATE in a branch is not an issue key' {
    # `claude/dra75-channel-rotation-20260914` must be DRA-75 and nothing else.
    # A pattern without the lookbehind reads 20260914 as a key the moment
    # somebody writes a branch as `dra-20260914`.
    $keys = @(Get-MergeSyncIssueKeys -Text 'claude/dra75-channel-rotation-20260914')
    if ($keys.Count -ne 1) { return "expected exactly one key, got $($keys.Count): $($keys -join ', ')" }
    if ($keys[0] -ne 'DRA-75') { return "expected DRA-75, got $($keys[0])" }
    return $null
}

Check 'every separator spelling normalizes to one key' {
    foreach ($spelling in @('DRA-77', 'dra77', 'Dra_77', 'DRA 77', 'dra-77')) {
        $keys = @(Get-MergeSyncIssueKeys -Text "see $spelling for detail")
        if ($keys.Count -ne 1 -or $keys[0] -ne 'DRA-77') {
            return "'$spelling' read as [$($keys -join ', ')], expected DRA-77"
        }
    }
    return $null
}

Check 'a key must be a whole token (hydra77 / EQDRA12 are not keys)' {
    foreach ($text in @('hydra77 is a mob', 'EQDRA12', 'xdra-9')) {
        $keys = @(Get-MergeSyncIssueKeys -Text $text)
        if ($keys.Count -ne 0) { return "'$text' read as [$($keys -join ', ')], expected nothing" }
    }
    return $null
}

Check 'dra7 does not eat dra77' {
    $keys = @(Get-MergeSyncIssueKeys -Text 'dra77')
    if ($keys.Count -ne 1 -or $keys[0] -ne 'DRA-77') { return "got [$($keys -join ', ')]" }
    return $null
}

Write-Host '--- linkage precedence ---'

Check 'THE BRANCH WINS over a body naming the governing plan' {
    # This is the row that matters most: every PR body in this repo carries
    # "Governing plan: DRA-73". Without branch-precedence this job would close
    # the parent plan issue on every single merge.
    Expect-Link `
        -Branch 'claude/dra77-merge-sync-20260914' `
        -Body "Per DRA-73 plan SS3.2-3. Closes DRA-77." `
        -Outcome 'linked' -Key 'DRA-77' -Source 'branch'
}

Check 'a body-only PR with ONE key links by body' {
    Expect-Link -Branch 'fix/typo' -Body 'Closes DRA-42.' -Outcome 'linked' -Key 'DRA-42' -Source 'body'
}

Check 'a body-only PR with TWO keys is REFUSED, not guessed' {
    Expect-Link -Branch 'fix/typo' -Body 'Governing plan: DRA-73. Also fixes DRA-42.' -Outcome 'ambiguous' -Source 'body'
}

Check 'a branch naming two keys is REFUSED, not guessed' {
    Expect-Link -Branch 'claude/dra77-and-dra78-combined' -Body '' -Outcome 'ambiguous' -Source 'branch'
}

Check 'a dependabot branch links nothing' {
    Expect-Link -Branch 'dependabot/nuget/src/EQBuddy.Core/nuget-minor-and-patch-7aadc11dc3' -Body 'Bumps two packages.' -Outcome 'none'
}

Check 'an ambiguous link never carries a key' {
    # Belt and braces: 'ambiguous' with a non-null Key would let a careless
    # caller read the key and act on it anyway.
    $r = Resolve-MergeSyncLink -Branch 'fix/x' -Body 'DRA-1 DRA-2'
    if ($r.Outcome -ne 'ambiguous') { return "expected ambiguous, got $($r.Outcome)" }
    if ($null -ne $r.Key) { return "ambiguous result carried key '$($r.Key)'" }
    return $null
}

Write-Host '--- transition dispositions ---'

Check 'the three disposition lists partition the Paperclip status enum' {
    $problems = @(Test-MergeSyncStatusCoverage)
    if ($problems.Count -gt 0) { return ($problems -join ' / ') }
    return $null
}

Check 'every CLOSABLE status transitions to done — each one, not just one' {
    # Per-element, because a list that happens to contain the one status
    # somebody tested is not a list that was checked (trap 78).
    foreach ($status in @('backlog', 'todo', 'in_progress', 'in_review')) {
        $d = Get-MergeSyncTransition -CurrentStatus $status
        if ($d.Action -ne 'transition') { return "'$status' gave '$($d.Action)', expected transition ($($d.Reason))" }
        if ($d.To -ne 'done') { return "'$status' would go to '$($d.To)', expected done" }
    }
    return $null
}

Check 'in_review is closable — the drift the card was filed for' {
    $d = Get-MergeSyncTransition -CurrentStatus 'in_review'
    if ($d.Action -ne 'transition' -or $d.To -ne 'done') { return "in_review gave $($d.Action)/$($d.To)" }
    return $null
}

Check 'done is an idempotent no-op, not a re-write' {
    $d = Get-MergeSyncTransition -CurrentStatus 'done'
    if ($d.Action -ne 'noop') { return "expected noop, got '$($d.Action)'" }
    return $null
}

Check 'every HANDS-OFF status is refused — each one' {
    foreach ($status in @('blocked', 'cancelled')) {
        $d = Get-MergeSyncTransition -CurrentStatus $status
        if ($d.Action -ne 'refuse') { return "'$status' gave '$($d.Action)', expected refuse" }
        if ($null -ne $d.To) { return "'$status' refused but still named a target '$($d.To)'" }
    }
    return $null
}

Check 'an unknown status is refused rather than guessed' {
    $d = Get-MergeSyncTransition -CurrentStatus 'shipped_to_production'
    if ($d.Action -ne 'refuse') { return "expected refuse, got '$($d.Action)'" }
    return $null
}

Check 'status matching is case- and whitespace-insensitive' {
    $d = Get-MergeSyncTransition -CurrentStatus '  In_Review '
    if ($d.Action -ne 'transition') { return "got '$($d.Action)' for '  In_Review '" }
    return $null
}

Write-Host '--- resolving the key to ONE issue ---'

# The fixture is the shape the API actually returns: a flat list of objects each
# carrying a scalar identifier/status/id.
$board = @(
    ([pscustomobject]@{ id = 'u-75'; identifier = 'DRA-75'; status = 'blocked' }),
    ([pscustomobject]@{ id = 'u-77'; identifier = 'DRA-77'; status = 'in_review' }),
    ([pscustomobject]@{ id = 'u-78'; identifier = 'DRA-78'; status = 'done' })
)

Check 'a flat list resolves to exactly one issue' {
    $r = Select-MergeSyncIssue -Issues $board -Key 'DRA-77'
    if ($r.Outcome -ne 'found') { return "expected found, got '$($r.Outcome)': $($r.Reason)" }
    if ($r.Issue.id -ne 'u-77') { return "matched the wrong issue: $($r.Issue.id)" }
    if (@($r.Issue.status).Count -ne 1) { return "status came back as $(@($r.Issue.status).Count) values" }
    return $null
}

Check 'an absent key is missing, not a match' {
    $r = Select-MergeSyncIssue -Issues $board -Key 'DRA-9999'
    if ($r.Outcome -ne 'missing') { return "expected missing, got '$($r.Outcome)'" }
    return $null
}

Check 'a NESTED list is normalized, not matched as a tautology' {
    # THE row this function exists for, and the one the live run needed.
    # `@(Invoke-RestMethod ...)` produces exactly this shape: one element which
    # is itself the whole array. The pipeline flatten unrolls it, so the answer
    # is the ONE right issue. Asserting the status is scalar is what separates
    # "found the issue" from the live symptom, which also reported 'found'.
    $nested = , $board
    $r = Select-MergeSyncIssue -Issues $nested -Key 'DRA-78'
    if ($r.Outcome -ne 'found') { return "expected found, got '$($r.Outcome)': $($r.Reason)" }
    if ($r.Issue.id -ne 'u-78') { return "matched '$($r.Issue.id)', expected u-78" }
    if (@($r.Issue.status).Count -ne 1) {
        return "status holds $(@($r.Issue.status).Count) values — the tautology is back"
    }
    if ($r.Issue.status -ne 'done') { return "status is '$($r.Issue.status)', expected 'done'" }
    return $null
}

Check 'an issue whose status is not scalar is refused' {
    # The reachable shape guard. Nothing flattens this one away: the LIST is
    # fine, the field inside one object is not. This is the assertion that would
    # have named the live bug in one line instead of printing eighty statuses.
    $bad = @(([pscustomobject]@{ id = 'x'; identifier = 'DRA-5'; status = @('todo', 'done') }))
    $r = Select-MergeSyncIssue -Issues $bad -Key 'DRA-5'
    if ($r.Outcome -ne 'malformed') { return "expected malformed, got '$($r.Outcome)'" }
    if ($r.Reason -notmatch 'status') { return "the refusal should name the field; got: $($r.Reason)" }
    return $null
}

Check 'the tautology is real (prove the bug the guard prevents)' {
    # Run the ORIGINAL one-liner over the nested shape and show it matches
    # everything. If PowerShell ever stops behaving this way, this row goes
    # green-by-accident and tells us the guard above is now theatre.
    $nested = , $board
    $naive = @($nested | Where-Object { $_.identifier -eq 'DRA-78' })
    if ($naive.Count -eq 0) {
        return 'the naive filter found nothing on the nested shape — the premise behind Select-MergeSyncIssue no longer holds; re-read its note'
    }
    $statuses = @(($naive | Select-Object -First 1).status)
    if ($statuses.Count -lt 2) {
        return "expected the naive match to carry several statuses (the live symptom), got $($statuses.Count)"
    }
    return $null
}

Check 'two issues claiming one identifier are refused' {
    $dupes = @(
        ([pscustomobject]@{ id = 'a'; identifier = 'DRA-1'; status = 'todo' }),
        ([pscustomobject]@{ id = 'b'; identifier = 'DRA-1'; status = 'done' })
    )
    $r = Select-MergeSyncIssue -Issues $dupes -Key 'DRA-1'
    if ($r.Outcome -ne 'malformed') { return "expected malformed, got '$($r.Outcome)'" }
    return $null
}

Check 'an empty list is malformed, not "issue missing"' {
    # An empty board means the request went wrong; reporting it as "your key is
    # bad" would send the reader to fix a branch name that is fine.
    $r = Select-MergeSyncIssue -Issues @() -Key 'DRA-77'
    if ($r.Outcome -ne 'malformed') { return "expected malformed, got '$($r.Outcome)'" }
    return $null
}

Write-Host '--- the script''s own argument handling ---'

function Run-Sync {
    param([string[]] $SyncArgs)
    $out = & pwsh -NoProfile -File $syncScript @SyncArgs 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $out.Trim() }
}

Check 'a CLOSED-not-merged PR does nothing, even with a perfect branch key' {
    # THE row. `[bool]"false"` is $true in PowerShell, so typing -Merged as
    # [bool] would close an issue every time a PR was abandoned. -ApiBase etc.
    # are deliberately supplied and unreachable: if the merged check were wrong
    # this would get as far as the network and say so.
    $r = Run-Sync @('-Branch', 'claude/dra77-merge-sync-20260914', '-Merged', 'false',
        '-ApiBase', 'http://127.0.0.1:9', '-ApiKey', 'x', '-CompanyId', 'y')
    if ($r.code -ne 0) { return "expected exit 0, got $($r.code): $($r.text)" }
    if ($r.text -notmatch 'closed without merging') { return "expected the not-merged line, got: $($r.text)" }
    return $null
}

Check 'the string "False" is also not a merge' {
    $r = Run-Sync @('-Branch', 'claude/dra77-x', '-Merged', 'False')
    if ($r.code -ne 0 -or $r.text -notmatch 'closed without merging') { return "got $($r.code): $($r.text)" }
    return $null
}

Check 'a merged PR with no key exits 0 and says which door to use' {
    $r = Run-Sync @('-Branch', 'fix/typo', '-Merged', 'true')
    if ($r.code -ne 0) { return "expected exit 0, got $($r.code): $($r.text)" }
    if ($r.text -notmatch 'names no Paperclip issue') { return "got: $($r.text)" }
    return $null
}

Check 'an ambiguous merged PR prints REFUSED and touches nothing' {
    $r = Run-Sync @('-Branch', 'fix/typo', '-PrBody', 'DRA-73 and DRA-42', '-Merged', 'true',
        '-ApiBase', 'http://127.0.0.1:9', '-ApiKey', 'x', '-CompanyId', 'y')
    if ($r.code -ne 0) { return "expected exit 0, got $($r.code): $($r.text)" }
    if ($r.text -notmatch 'REFUSED') { return "expected REFUSED, got: $($r.text)" }
    return $null
}

Check 'missing secrets are SKIPPED loudly, not a red build' {
    $r = Run-Sync @('-Branch', 'claude/dra77-merge-sync-20260914', '-Merged', 'true',
        '-ApiBase', '', '-ApiKey', '', '-CompanyId', '')
    if ($r.code -ne 0) { return "expected exit 0, got $($r.code): $($r.text)" }
    if ($r.text -notmatch 'not configured') { return "expected the not-configured line, got: $($r.text)" }
    if ($r.text -notmatch 'PAPERCLIP_API_URL') { return "the skip line must NAME the missing secrets; got: $($r.text)" }
    return $null
}

Check '-DryRun reaches the decision and still makes no request' {
    $r = Run-Sync @('-Branch', 'claude/dra77-merge-sync-20260914', '-Merged', 'true', '-DryRun')
    if ($r.code -ne 0) { return "expected exit 0, got $($r.code): $($r.text)" }
    if ($r.text -notmatch 'DRY RUN') { return "got: $($r.text)" }
    if ($r.text -notmatch 'DRA-77') { return "dry run should name the resolved key; got: $($r.text)" }
    return $null
}

Check 'a CONFIGURED job that cannot reach Paperclip is RED' {
    # The other side of the fail-open rule: absent secrets are fine, a broken
    # sync is not. Port 9 (discard) refuses fast on every platform.
    $r = Run-Sync @('-Branch', 'claude/dra77-merge-sync-20260914', '-Merged', 'true',
        '-ApiBase', 'http://127.0.0.1:9', '-ApiKey', 'x', '-CompanyId', 'y')
    if ($r.code -eq 0) { return "expected a non-zero exit for an unreachable API, got 0: $($r.text)" }
    return $null
}

Write-Host '--- the one-way scope lock ---'

Check 'merge-sync.ps1 contains no GitHub write' {
    $text = Get-Content -Raw -Encoding utf8 $syncScript
    # Forbid half. Each pattern is its own parenthesised element: trap 78 was a
    # detector list that collapsed to one useless string because ',' binds
    # tighter than '+'.
    $forbidden = @(
        ('gh pr '),
        ('gh issue '),
        ('gh api --method POST'),
        ('gh release '),
        ('git push')
    )
    if ($forbidden.Count -lt 5) { return "the forbidden-pattern list collapsed to $($forbidden.Count) entries" }
    $hits = @($forbidden | Where-Object { $text -match [regex]::Escape($_) })
    if ($hits.Count -gt 0) { return "merge-sync.ps1 writes to GitHub, which M0 forbids: $($hits -join ', ')" }
    return $null
}

Check 'and the forbid scan can actually see a GitHub write (prove-fail)' {
    # Must-list half (trap 34): a scan that matches nothing would pass the row
    # above forever. Run the same scan over a string that DOES contain a write.
    $sample = "Invoke-Paperclip ...`ngh pr comment 1 --body hi`n"
    $forbidden = @(('gh pr '), ('gh issue '))
    $hits = @($forbidden | Where-Object { $sample -match [regex]::Escape($_) })
    if ($hits.Count -eq 0) { return 'the forbid scan did not fire on a sample that contains `gh pr comment` — it is aimed at nothing' }
    return $null
}

Check 'merge-sync.ps1 does call the Paperclip PATCH (must-list)' {
    $text = Get-Content -Raw -Encoding utf8 $syncScript
    foreach ($needle in @('PATCH', '/api/issues/', 'status = $decision.To')) {
        if ($text -notmatch [regex]::Escape($needle)) {
            return "expected merge-sync.ps1 to contain '$needle' — the job that transitions nothing passes every forbid check"
        }
    }
    return $null
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "merge-sync self-test FAILED ($($failed.Count) of $script:step):" -ForegroundColor Red
    foreach ($f in $failed) { Write-Host "  [FAIL] $f" -ForegroundColor Red }
    exit 1
}

Write-Host "merge-sync self-test: $script:step checks, all green."
exit 0
