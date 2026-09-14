<#
.SYNOPSIS
    The two DECISIONS behind merge-sync, with no I/O in either of them.

.DESCRIPTION
    Dot-sourced by `merge-sync.ps1` (which does the HTTP) and by
    `merge-sync-selftest.ps1` (which proves these fire). The split exists so the
    interesting half — which issue does this PR name, and may that issue be
    closed — is testable without a Paperclip server, a GitHub event, or a secret.

    DRA-77 / M0-4, governing plan DRA-73 rev 2 (SS3.2-3 + SS8.5).
    `exo-experiment: merge-sync`.

    Two rules live here and nowhere else:

    1. THE BRANCH WINS. Every PR body in this repo carries a "Governing plan:
       DRA-73" line, so a scan that treats branch and body as one pool would
       transition the parent plan issue on every single merge. The branch is the
       thing the executor named the work; the body is the fallback for when it
       did not.

    2. AN AMBIGUOUS BODY IS REFUSED, NOT GUESSED. Picking the first of two keys
       is a coin flip whose losing side writes `done` onto somebody else's
       issue. One-way sync means the wrong write has no undo path from here.
#>

Set-StrictMode -Version Latest

# The Paperclip issue status enum, exactly as /api/issues/{id} PATCH declares it.
# Partitioned into the three dispositions below; `Test-MergeSyncStatusCoverage`
# asserts the partition is total and disjoint, so a status Paperclip adds later
# reddens the self-test instead of falling through to a silent guess.
$script:MergeSyncAllStatuses = @('backlog', 'todo', 'in_progress', 'in_review', 'done', 'blocked', 'cancelled')

# Closable: the work landed, so the issue should stop sitting here. `in_review`
# is the observed drift the card was filed for (EXO-HARDEN-A2,
# EQ-V2-HOME-CATCHUP); the other three are included because a merged PR is
# evidence the work happened whatever column the board left it in.
$script:MergeSyncClosable = @('backlog', 'todo', 'in_progress', 'in_review')

# Already closed: a no-op, and it must stay a no-op rather than a re-write.
# A merge can be replayed (re-run of the workflow, a revert-of-a-revert), and
# an idempotent job is the difference between that being free and it being an
# activity-feed spam generator.
$script:MergeSyncAlreadyClosed = @('done')

# Hands off: somebody else owns the next move.
#   blocked   - a first-class blocker is recorded against it; a merged PR does
#               not clear a blocker, and stamping `done` would hide it.
#   cancelled - a human decided this work should not happen. A PR merging is
#               not a reason to reverse that; if the work did land, the human
#               reopens it deliberately.
$script:MergeSyncHandsOff = @('blocked', 'cancelled')

<#
.SYNOPSIS
    Every issue key in one piece of text, normalized and de-duplicated in order.
#>
function Get-MergeSyncIssueKeys {
    param(
        [string] $Text,
        [string] $Prefix = 'DRA'
    )

    if ([string]::IsNullOrWhiteSpace($Text)) { return @() }

    # Why each piece:
    #   (?<![a-z0-9])  a key is a WHOLE token. Without it `hydra77` and
    #                  `EQ-DRA12` read as DRA-77 and DRA-12. Trap 66 is the same
    #                  shape from the other side: a rule written against one
    #                  position is really a rule about the fact.
    #   [-_ ]?         the branch spelling is `dra77`, the body spelling is
    #                  `DRA-77`, and both are the same card.
    #   (?![0-9])      stops `dra7` from eating `dra77`. It is also what keeps
    #                  the branch suffix `-20260914` out of the answer, together
    #                  with the lookbehind: there is no `dra` in front of it.
    $pattern = '(?<![a-z0-9])' + [regex]::Escape($Prefix) + '[-_ ]?([0-9]{1,6})(?![0-9])'

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $keys = [System.Collections.Generic.List[string]]::new()
    foreach ($m in [regex]::Matches($Text, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        # Normalize on the way in: the store speaks one spelling, and trap 70's
        # "a mutex over free text refuses neither spelling" is the same lesson.
        $key = '{0}-{1}' -f $Prefix.ToUpperInvariant(), [int]$m.Groups[1].Value
        if ($seen.Add($key)) { $keys.Add($key) }
    }

    return @($keys)
}

<#
.SYNOPSIS
    Which issue, if any, this merged PR is allowed to close. Branch beats body.
#>
function Resolve-MergeSyncLink {
    param(
        [string] $Branch,
        [string] $Body,
        [string] $Prefix = 'DRA'
    )

    $branchKeys = @(Get-MergeSyncIssueKeys -Text $Branch -Prefix $Prefix)
    if ($branchKeys.Count -eq 1) {
        return @{
            Outcome    = 'linked'
            Key        = $branchKeys[0]
            Source     = 'branch'
            Candidates = $branchKeys
            Reason     = "branch '$Branch' names $($branchKeys[0])"
        }
    }
    if ($branchKeys.Count -gt 1) {
        return @{
            Outcome    = 'ambiguous'
            Key        = $null
            Source     = 'branch'
            Candidates = $branchKeys
            Reason     = "branch '$Branch' names $($branchKeys.Count) keys ($($branchKeys -join ', ')) — refusing rather than picking one"
        }
    }

    # Only now does the body get a vote. This ordering IS the feature: without
    # it the "Governing plan: DRA-73" line every PR in this repo carries would
    # close the parent plan issue on every merge.
    $bodyKeys = @(Get-MergeSyncIssueKeys -Text $Body -Prefix $Prefix)
    if ($bodyKeys.Count -eq 1) {
        return @{
            Outcome    = 'linked'
            Key        = $bodyKeys[0]
            Source     = 'body'
            Candidates = $bodyKeys
            Reason     = "branch names no key; PR body names $($bodyKeys[0])"
        }
    }
    if ($bodyKeys.Count -gt 1) {
        return @{
            Outcome    = 'ambiguous'
            Key        = $null
            Source     = 'body'
            Candidates = $bodyKeys
            Reason     = "branch names no key and the PR body names $($bodyKeys.Count) ($($bodyKeys -join ', ')) — refusing rather than picking one. Put the key in the branch name."
        }
    }

    return @{
        Outcome    = 'none'
        Key        = $null
        Source     = 'none'
        Candidates = @()
        Reason     = "no $Prefix-<n> key in branch '$Branch' or in the PR body"
    }
}

<#
.SYNOPSIS
    What to do to an issue in this status now that its PR has merged.
#>
function Get-MergeSyncTransition {
    param([string] $CurrentStatus)

    $status = ([string]$CurrentStatus).Trim().ToLowerInvariant()

    if ($script:MergeSyncClosable -contains $status) {
        return @{ Action = 'transition'; To = 'done'; Reason = "status '$status' is open work and the PR merged" }
    }
    if ($script:MergeSyncAlreadyClosed -contains $status) {
        return @{ Action = 'noop'; To = $null; Reason = "already '$status' — nothing to do (replaying a merge must stay free)" }
    }
    if ($script:MergeSyncHandsOff -contains $status) {
        return @{ Action = 'refuse'; To = $null; Reason = "status '$status' is somebody else's decision; a merged PR does not reverse it" }
    }

    # Unknown status: refuse, loudly. This is the arm the coverage assertion
    # below exists to keep unreachable — if it ever fires in production, the
    # enum moved and nobody told the partition.
    return @{ Action = 'refuse'; To = $null; Reason = "status '$status' is not one this job knows about — refusing rather than guessing" }
}

<#
.SYNOPSIS
    Exactly one issue with this identifier, or a named refusal. Never "all of them".

.DESCRIPTION
    This exists because the obvious one-liner
    `$issues | Where-Object { $_.identifier -eq $key } | Select-Object -First 1`
    silently returned the ENTIRE 80-issue collection on the first live run, and
    then reported the concatenation of all eighty statuses as one issue's status.

    Two PowerShell behaviours compose into that:

      1. `@(Invoke-RestMethod ...)` does NOT normalize an array. The cmdlet emits
         the whole JSON array as ONE object, so `@(...)` wraps it a second time:
         a 1-element array whose single element is the 80-item array. Measured,
         not guessed — `@(fn).Count` is 1 where `(fn).Count` is 80.

      2. `-eq` against an array is a FILTER, not a comparison. `$_.identifier`
         on that nested element member-enumerates to eighty identifiers, and
         `eighty-identifiers -eq 'DRA-78'` returns the matching ones — a
         non-empty array, which `Where-Object` reads as $true. So the tautology
         passed every issue through, and the not-found case still looked correct
         because an empty result is falsy.

    The second half is what makes this worth a function rather than a fix: the
    bug had a CORRECT-LOOKING negative. `DRA-9999` was refused properly while
    `DRA-78` matched everything, so the refusal path "proved" the lookup worked.

    Returns @{ Outcome = 'found'|'missing'|'malformed'; Issue; Reason }.
#>
function Select-MergeSyncIssue {
    param(
        $Issues,
        [string] $Key
    )

    # THE FIX, and it is this one line. Enumeration through the PIPELINE unrolls
    # a nested array; `@()` around the value does not. So this normalizes both
    # the shape the API returns and the shape a careless `@(Invoke-RestMethod)`
    # would hand over, and every comparison below is against real issue objects.
    #
    # Deliberately NOT paired with a "refuse a nested list" check: after this
    # line no reachable input is still nested, and a branch nothing can enter is
    # a guard aimed at nothing (trap 78's other half). The scalar-field
    # assertion further down is the reachable one.
    $flat = @($Issues | ForEach-Object { $_ })

    if ($flat.Count -eq 0) {
        return @{ Outcome = 'malformed'; Issue = $null; Reason = 'the issue list is empty — refusing to conclude anything from it' }
    }

    $hits = @($flat | Where-Object { $_.identifier -eq $Key })

    if ($hits.Count -eq 0) {
        return @{ Outcome = 'missing'; Issue = $null; Reason = "no issue with identifier $Key in a list of $($flat.Count)" }
    }
    if ($hits.Count -gt 1) {
        return @{ Outcome = 'malformed'; Issue = $null; Reason = "$($hits.Count) issues claim identifier $Key — refusing to pick one" }
    }

    $issue = $hits[0]

    # Belt and braces: the symptom that made the original bug visible was a
    # status field holding eighty values. Assert the two fields this job acts on
    # are scalars, so the NEXT way this shape breaks is caught at the source
    # rather than reported as a bizarre status string.
    foreach ($field in @('id', 'status', 'identifier')) {
        $value = $issue.$field
        if (@($value).Count -ne 1) {
            return @{
                Outcome = 'malformed'
                Issue   = $null
                Reason  = "the matched issue's '$field' holds $(@($value).Count) values, not one — the list shape is wrong"
            }
        }
    }

    return @{ Outcome = 'found'; Issue = $issue; Reason = "matched $Key in a list of $($flat.Count)" }
}

<#
.SYNOPSIS
    The three disposition lists partition the status enum, totally and disjointly.

.DESCRIPTION
    Returns a list of complaints; empty means the partition holds. Called by the
    self-test. This is the trap 78 half: a disposition list that silently
    collapsed to nothing would make `Get-MergeSyncTransition` answer 'refuse'
    for everything, and a job that refuses everything looks exactly like a job
    with nothing to do.
#>
function Test-MergeSyncStatusCoverage {
    $problems = @()

    foreach ($pair in @(
            @{ Name = 'MergeSyncClosable'; Values = $script:MergeSyncClosable },
            @{ Name = 'MergeSyncAlreadyClosed'; Values = $script:MergeSyncAlreadyClosed },
            @{ Name = 'MergeSyncHandsOff'; Values = $script:MergeSyncHandsOff })) {
        if (@($pair.Values).Count -lt 1) {
            $problems += "$($pair.Name) is EMPTY — an empty disposition list matches nothing and reports clean (trap 78)."
        }
    }

    $union = @($script:MergeSyncClosable) + @($script:MergeSyncAlreadyClosed) + @($script:MergeSyncHandsOff)

    $dupes = @($union | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
    if ($dupes.Count -gt 0) {
        $problems += "these statuses appear in more than one disposition list, so the answer depends on check order: $($dupes -join ', ')"
    }

    $missing = @($script:MergeSyncAllStatuses | Where-Object { $union -notcontains $_ })
    if ($missing.Count -gt 0) {
        $problems += "these Paperclip statuses have no disposition: $($missing -join ', '). Decide each one explicitly in merge-sync-linkage.ps1 rather than letting it fall through to the unknown-status arm."
    }

    $extra = @($union | Where-Object { $script:MergeSyncAllStatuses -notcontains $_ })
    if ($extra.Count -gt 0) {
        $problems += "these are dispositioned but are not Paperclip statuses: $($extra -join ', ')"
    }

    return @($problems)
}
