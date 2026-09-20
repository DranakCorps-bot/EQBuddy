<#
.SYNOPSIS
    No commit a pull request ADDS may be authored or committed as anyone but the bot.

.DESCRIPTION
    DRA-226. Every commit on three open DRA-216 delivery branches was authored AND
    committed as `David Edwards <david.edwards08@gmail.com>` - seven commits, 4,589 lines
    of agent-written code, about to land in `git blame` under the name of the one person
    in this company whose signature carries release accountability. A Planner review
    caught it because somebody happened to read the commit authors.

    THE MEASUREMENT THAT SIZED IT. Of the last 600 commits on `main` at the time this
    landed, 126 already carry `david.edwards08@gmail.com` - 121 as `David Edwards`, 5 as
    `DranakCorps-bot`. The three pull requests were not the incident; they were the one
    instance anybody noticed, at about a eighteenth of the real scale.

    THE CAUSE, because a guard should say what it is guarding against. There is more than
    one EQBuddy working tree on the Founder's machine and each carries its own `[user]`
    block, because `~/.gitconfig` has none. `C:\Users\david\source\EQBuddy` - an active
    agent dispatch lane with ~20 linked worktrees under %TEMP% on `claude/*` and
    `sr-exec/*` branches - resolved `git var GIT_AUTHOR_IDENT` to
    `David Edwards <david.edwards08@gmail.com>`. A linked worktree SHARES its clone's
    config, so one wrong `[user]` block mis-attributed every worktree hanging off it. Two
    more trees carried the Founder's email under a different name (`Fable 5`, and
    `DranakCorps-bot` in run-scratch clones), which is why this reads EMAIL and not name.

    WHY A CI CHECK AND NOT JUST THE CONFIG FIX. Fixing the three trees fixes today's
    machine. It does nothing about the next fresh clone, the next run-scratch checkout, or
    the next dispatch lane - all three of which have already happened once. This is the
    copy that does not depend on the environment being right.

    IT JUDGES ONLY WHAT THE PULL REQUEST ADDS. The range is HEAD minus the base AND minus
    `main`, so a commit already on `main` is never judged by it. That is not politeness:
    `main` holds 126 commits this guard would refuse, so a check that read history would
    be permanently, uselessly red and would teach everyone to ignore it (trap 74 - a gate
    that reddens for a reason nobody changed is a gate nobody believes).

    WHAT IT READS IS THE EMAIL, ON BOTH IDENTITIES. GitHub attributes a commit by email,
    so `DranakCorps-bot <david.edwards08@gmail.com>` renders on the pull request page as
    "David Edwards" no matter what the name field says - which is exactly how five of
    those commits got read as the Founder's. Name is REPORTED so a human can see what
    happened, and weighs nothing. Author and committer are checked separately because a
    rebase, a cherry-pick or an `--amend` moves them independently, and half a check is a
    check with a hole the size of the other half.

    THE FOUNDER'S OWN DOOR (-AllowFounder) is deliberate, explicit and narrow, and it
    exists because banning the repo owner from his own repository is not an acceptable
    fix. The honest problem is that an agent running inside the Founder's clone produces a
    commit BYTE-IDENTICAL in identity to one he types himself; no rule read off the commit
    object can separate them. So the separation has to come from outside the object: a
    human puts the `founder-commit` label on the pull request, `ci.yml` passes this switch,
    and the Founder's identity joins the allow-list FOR THAT PULL REQUEST ONLY. Every
    other unknown identity is still refused with the switch on - the door admits one named
    person, it does not stand the guard down. The label is visible on the pull request, it
    is recorded in the event, and an agent cannot apply one.

    FAILING OPEN IS DELIBERATE IN EXACTLY TWO PLACES, and both of them SAY SO on stdout:
    an unresolvable base (a shallow checkout with no merge base), and an empty range (a
    pull request that adds no commit of its own). Both print a loud SKIPPED/EMPTY line,
    because an empty range is precisely how a broken range computation would read as
    green, and a guard that cannot tell "I checked nothing" from "I checked and it was
    fine" is the one that goes quiet the day it breaks.

.PARAMETER BaseRef
    The commit the pull request is based on. Defaults to `origin/main`, which makes the
    local invocation argument-free: from a feature branch it judges exactly your own
    commits. CI passes the pull request's base sha.

.PARAMETER HeadRef
    The tip to judge. Defaults to HEAD. On a `pull_request` run CI passes the pull
    request's HEAD sha rather than HEAD, because `actions/checkout` leaves the MERGE
    RESULT checked out and that merge commit is authored by GitHub, not by the author.

.PARAMETER AllowFounder
    Add the Founder's own identity to the allow-list for this run. See above - this is
    the `founder-commit` label's door, not a general off switch.

.EXAMPLE
    pwsh -NoProfile -File scripts/commit-identity-guard.ps1
    Judge every commit on the current branch that is not already on origin/main.
#>
[CmdletBinding()]
param(
    [string]$BaseRef = 'origin/main',
    [string]$HeadRef = 'HEAD',
    [switch]$AllowFounder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Trap 54: read git's bytes, not the host's decode of them. Author names carry non-ASCII
# and a mojibaked name in a refusal message is a refusal nobody can act on.
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

# The identities a commit in this repository may carry. One per line and each its own
# quoted element: trap 78 is a detector whose pattern list collapsed to a single joined
# string because PowerShell binds `,` tighter than `+`, and it then matched nothing and
# reported clean for a month. The non-empty assertion below is the other half of that
# lesson - a list that CAN be empty must be checked, not assumed.
$AllowedEmails = @(
    'dranakcorps@gmail.com'                                  # the bot, as configured in every correct checkout
    'DranakCorps-bot@users.noreply.github.com'               # the bot committing through the GitHub web UI
    '280514144+DranakCorps-bot@users.noreply.github.com'     # the same, in GitHub's numbered noreply form
    'noreply@github.com'                                     # GitHub's own merge / "Update branch" commits
    '49699333+dependabot[bot]@users.noreply.github.com'      # dependabot, which opens pull requests here
)

# Named rather than inlined so the refusal can say WHOSE name is at stake, and so the
# door below cannot drift from the thing it is a door for.
$FounderEmail = 'david.edwards08@gmail.com'

if ($AllowedEmails.Count -eq 0) {
    throw 'commit-identity-guard: the allowed-identity list is EMPTY, so this guard would pass everything. Refusing to report a result (trap 78).'
}

if ($AllowFounder) {
    $AllowedEmails += $FounderEmail
}

function Resolve-Commit {
    param([string]$Ref)
    if ([string]::IsNullOrWhiteSpace($Ref)) { return $null }
    $sha = & git rev-parse --verify --quiet "$Ref^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) { return $null }
    return $sha.Trim()
}

$head = Resolve-Commit $HeadRef
if (-not $head) {
    Write-Host "SKIPPED: commit-identity-guard could not resolve HeadRef '$HeadRef'. Nothing judged."
    exit 0
}

$base = Resolve-Commit $BaseRef
if (-not $base) {
    Write-Host "SKIPPED: commit-identity-guard could not resolve BaseRef '$BaseRef' (a shallow checkout has no merge base). Nothing judged."
    exit 0
}

# Exclude `main` as well as the base. The base sha alone is very nearly right, but a
# branch that merged a newer main than the sha the pull request was opened against would
# drag main's own commits into the range - and main holds 126 commits this guard refuses.
# Naming main explicitly makes "never judge history already on main" a property of the
# range rather than a hope about how the base was computed.
$exclusions = @($base)
foreach ($candidate in @('origin/main', 'main')) {
    $sha = Resolve-Commit $candidate
    if ($sha -and $exclusions -notcontains $sha) { $exclusions += $sha }
}

$SEP = [char]0x1F   # unit separator: cannot occur in a name, an email or a subject line
$format = "%H$SEP%an$SEP%ae$SEP%cn$SEP%ce$SEP%s"

# Two steps rather than one pipeline: `git log --no-walk` with an EMPTY revision list does
# not print nothing, it falls back to reading stdin and hangs a runner. Ask for the shas
# first, and decide about emptiness before anything is handed a revision argument.
$revs = @(& git rev-list $head --not @exclusions 2>$null | ForEach-Object { $_ } | Where-Object { $_ })
if ($LASTEXITCODE -ne 0) {
    Write-Host "SKIPPED: commit-identity-guard could not walk $base..$head. Nothing judged."
    exit 0
}

$lines = @()
if ($revs.Count -gt 0) {
    # Revisions first, then `--` to close the revision list. Without the terminator a sha
    # that also names a file would be read as a path and the commit would go unjudged.
    $lines = @(& git log --no-walk=unsorted --format=$format @revs -- 2>$null)
    $lines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

if ($lines.Count -eq 0) {
    Write-Host "EMPTY: commit-identity-guard found no commit that this branch adds to $($BaseRef). Nothing judged."
    Write-Host "       (This is a pass, but it is a pass over zero commits - said out loud so it cannot be mistaken for one over many.)"
    exit 0
}

$violations = @()
foreach ($line in $lines) {
    $parts = $line -split $SEP
    if ($parts.Count -lt 6) { continue }

    $entry = [pscustomobject]@{
        Sha            = $parts[0]
        AuthorName     = $parts[1]
        AuthorEmail    = $parts[2]
        CommitterName  = $parts[3]
        CommitterEmail = $parts[4]
        Subject        = $parts[5]
        Fields         = @()
    }

    # Both identities, separately. A rebase rewrites the committer and leaves the author
    # alone; an `--amend` in the wrong tree rewrites the committer only. Checking one of
    # them is a check with a hole exactly the size of the other.
    if ($AllowedEmails -notcontains $entry.AuthorEmail)    { $entry.Fields += 'author' }
    if ($AllowedEmails -notcontains $entry.CommitterEmail) { $entry.Fields += 'committer' }

    if ($entry.Fields.Count -gt 0) { $violations += $entry }
}

$scope = "$($lines.Count) commit(s) this branch adds"
if ($violations.Count -eq 0) {
    Write-Host "OK: $scope are all authored and committed by an allowed identity."
    exit 0
}

Write-Host ''
Write-Host "FAIL: $($violations.Count) of $scope carry an identity this repository does not allow."
Write-Host ''
foreach ($v in $violations) {
    $short = $v.Sha.Substring(0, 8)
    Write-Host "  $short  $($v.Subject)"
    Write-Host "      author    : $($v.AuthorName) <$($v.AuthorEmail)>$(if ($v.Fields -contains 'author') { '   <-- refused' })"
    Write-Host "      committer : $($v.CommitterName) <$($v.CommitterEmail)>$(if ($v.Fields -contains 'committer') { '   <-- refused' })"
    if ($v.AuthorEmail -eq $FounderEmail -or $v.CommitterEmail -eq $FounderEmail) {
        Write-Host "      This is the FOUNDER's email. If an agent run produced this commit, the run's"
        Write-Host "      working tree has the wrong [user] block - a linked worktree shares its clone's"
        Write-Host "      config, so check the CLONE and not just this checkout:"
        Write-Host "          git var GIT_AUTHOR_IDENT"
        Write-Host "          git config --local user.name 'DranakCorps-bot'"
        Write-Host "          git config --local user.email 'dranakcorps@gmail.com'"
        Write-Host "      Then rewrite the branch (it is impossible once merged):"
        Write-Host "          git rebase --exec 'git commit --amend --no-edit --reset-author' $BaseRef"
        Write-Host "          git push --force-with-lease origin HEAD"
    }
    Write-Host ''
}
Write-Host 'Allowed identities:'
foreach ($e in $AllowedEmails) { Write-Host "  $e" }
Write-Host ''
if (-not $AllowFounder) {
    Write-Host "If these commits really are the Founder's OWN work, add the `founder-commit` label to"
    Write-Host 'the pull request and re-run. That admits his identity for this pull request only; every'
    Write-Host 'other unknown identity above stays refused.'
}
exit 1
