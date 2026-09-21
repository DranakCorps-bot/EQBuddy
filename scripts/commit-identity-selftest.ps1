<#
.SYNOPSIS
    The prove-fail for `commit-identity-guard.ps1`: every refusal driven RED at least
    once, and every shape the policy ALLOWS driven green.

.DESCRIPTION
    DRA-226. A detector nobody has watched fire is a detector aimed at nothing - trap 34
    (a guard that forbids the wrong thing cannot see a missing thing) and trap 78 (a
    detector whose pattern list silently collapsed matched nothing and reported clean for
    a month). This is the copy of that lesson for the commit-identity guard, and it runs
    in CI beside the guard itself.

    It builds a THROWAWAY repository under the run's temp directory - never the live one -
    and drives the guard over commits it authored on purpose. The green half matters as
    much as the red half here: a guard that refuses the Founder's email is one bad
    allow-list row away from refusing the bot's too, and every pull request in the
    repository would then be red for a reason nobody changed.

    The two properties worth naming, because they are the ones that would rot quietly:

    ALL SIX REFUSALS FIRE. Author, committer, the right-name/wrong-email shape that
    started DRA-226, a stranger, a stranger under the Founder's own door, and a single bad
    commit buried in a run of good ones.

    ALL OF `main` STAYS UNJUDGED. `main` holds 126 commits the guard would refuse, so
    "it only judges what the pull request adds" is not a nicety - it is the difference
    between a gate and a permanent red light. Case 8 commits the Founder's identity onto
    `main` and asserts the guard says so out loud rather than passing silently.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

$guard = Join-Path $PSScriptRoot 'commit-identity-guard.ps1'
if (-not (Test-Path $guard)) { throw "commit-identity-selftest: cannot find $guard" }

$BOT      = @{ Name = 'DranakCorps-bot'; Email = 'dranakcorps@gmail.com' }
$FOUNDER  = @{ Name = 'David Edwards';   Email = 'david.edwards08@gmail.com' }
$STRANGER = @{ Name = 'Some Contractor'; Email = 'contractor@example.com' }
# The shape that actually started this card: the NAME is the bot's and only the email is
# the Founder's. GitHub renders it as "David Edwards", so it reads as him on the pull
# request page while passing any check that looks at the name.
$DISGUISED = @{ Name = 'DranakCorps-bot'; Email = 'david.edwards08@gmail.com' }

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("eqb-identity-selftest-" + [guid]::NewGuid().ToString('n').Substring(0, 8))
New-Item -ItemType Directory -Path $root -Force | Out-Null

$failures = 0
$checks = 0

function Write-Result {
    param([bool]$Ok, [string]$Label, [string]$Detail = '')
    $script:checks++
    if ($Ok) {
        Write-Host "  [ok]   $Label"
    } else {
        $script:failures++
        Write-Host "  [FAIL] $Label"
        if ($Detail) { foreach ($l in ($Detail -split "`n")) { Write-Host "         $l" } }
    }
}

# One array parameter rather than ValueFromRemainingArguments: `Invoke-Git add -A` binds
# `-A` to the automatic `$Args` parameter and dies before git ever runs.
function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$GitArgs)
    $out = & git -C $repo @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArgs -join ' ') failed: $out" }
    return $out
}

function New-Commit {
    param(
        [Parameter(Mandatory)][string]$Message,
        [Parameter(Mandatory)][hashtable]$Author,
        [hashtable]$Committer
    )
    if (-not $Committer) { $Committer = $Author }
    $file = Join-Path $repo ('f-' + [guid]::NewGuid().ToString('n').Substring(0, 6) + '.txt')
    Set-Content -Path $file -Value $Message -Encoding utf8
    Invoke-Git @('add','-A') | Out-Null

    # Committer through `-c`, author through `--author`, and no environment variables at
    # all. The obvious version sets GIT_AUTHOR_*/GIT_COMMITTER_* and restores them in a
    # `finally` - but restoring a variable that was never set leaves it as an EMPTY string
    # rather than absent, and the next git command in the fixture dies on
    # "empty ident name (for <>) not allowed". Flags carry the identity exactly as far as
    # the one command that needs it and leave nothing behind to restore.
    Invoke-Git @(
        '-c', "user.name=$($Committer.Name)"
        '-c', "user.email=$($Committer.Email)"
        'commit', '-q', '-m', $Message
        "--author=$($Author.Name) <$($Author.Email)>"
    ) | Out-Null
    return (Invoke-Git @('rev-parse','HEAD')).Trim()
}

function Invoke-Guard {
    param([string]$Base = 'main', [string]$Head = 'HEAD', [switch]$AllowFounder)
    # Not `$args`: that is an automatic variable, and assigning to it under StrictMode is
    # the same class of collision as `-A` binding to `$Args` above.
    $psArgs = @('-NoProfile', '-File', $guard, '-BaseRef', $Base, '-HeadRef', $Head)
    if ($AllowFounder) { $psArgs += '-AllowFounder' }
    Push-Location $repo
    try {
        $output = & pwsh @psArgs 2>&1 | Out-String
        return [pscustomobject]@{ Code = $LASTEXITCODE; Output = $output }
    } finally { Pop-Location }
}

function Reset-Feature {
    # Every case starts from the same clean `main`, so a case cannot pass on a commit an
    # earlier case happened to leave behind (trap 51 - when staging is cumulative and the
    # fixture is shared, reset is the contract).
    Invoke-Git @('checkout','-q','-B','feature','main') | Out-Null
}

try {
    $repo = Join-Path $root 'repo'
    New-Item -ItemType Directory -Path $repo -Force | Out-Null
    Invoke-Git @('init','-q','-b','main') | Out-Null
    Invoke-Git @('config','user.name',$BOT.Name) | Out-Null
    Invoke-Git @('config','user.email',$BOT.Email) | Out-Null
    Invoke-Git @('config','commit.gpgsign','false') | Out-Null
    $mainBase = New-Commit -Message 'base commit on main' -Author $BOT

    Write-Host ''
    Write-Host 'commit-identity guard - prove-fail'
    Write-Host ''
    Write-Host 'REFUSALS (each must go RED):'

    # 1. The author half.
    Reset-Feature
    New-Commit -Message 'agent work authored as the Founder' -Author $FOUNDER -Committer $BOT | Out-Null
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 1) -and ($r.Output -match 'author' )) '1. author email is the Founder' $r.Output

    # 2. The committer half, with a clean author. A rebase moves these independently, so
    #    checking one is a hole the size of the other.
    Reset-Feature
    New-Commit -Message 'authored by the bot, committed as the Founder' -Author $BOT -Committer $FOUNDER | Out-Null
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 1) -and ($r.Output -match 'committer')) '2. committer email is the Founder (author clean)' $r.Output

    # 3. The shape that started the card. If the guard ever regresses to reading names,
    #    this is the case that catches it.
    Reset-Feature
    New-Commit -Message 'bot name, Founder email' -Author $DISGUISED | Out-Null
    $r = Invoke-Guard
    Write-Result ($r.Code -eq 1) '3. bot NAME with the Founder email is still refused' $r.Output

    # 4. Anyone else at all.
    Reset-Feature
    New-Commit -Message 'a third party' -Author $STRANGER | Out-Null
    $r = Invoke-Guard
    Write-Result ($r.Code -eq 1) '4. an unknown third-party identity' $r.Output

    # 5. The Founder's door is a door for ONE person, not an off switch.
    $r = Invoke-Guard -AllowFounder
    Write-Result ($r.Code -eq 1) '5. -AllowFounder still refuses a third party' $r.Output

    # 6. A bad commit that is not the tip. A guard that only reads HEAD passes this.
    Reset-Feature
    New-Commit -Message 'good one'              -Author $BOT | Out-Null
    $bad = New-Commit -Message 'bad one buried' -Author $FOUNDER
    New-Commit -Message 'another good one'      -Author $BOT | Out-Null
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 1) -and ($r.Output -match $bad.Substring(0, 8))) '6. a bad commit buried mid-branch, named by sha' $r.Output

    Write-Host ''
    Write-Host 'ALLOWED (each must stay GREEN):'

    # 7. The ordinary case, and the one whose breakage would redden every pull request.
    Reset-Feature
    New-Commit -Message 'ordinary bot work' -Author $BOT | Out-Null
    New-Commit -Message 'more bot work'     -Author $BOT | Out-Null
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 0) -and ($r.Output -match 'OK:')) '7. an all-bot branch passes' $r.Output

    # 8. History already on main is never judged - the property that keeps this a gate
    #    rather than a permanent red light, asserted against a real Founder commit.
    Invoke-Git @('checkout','-q','main') | Out-Null
    New-Commit -Message 'a Founder-authored commit that is already on main' -Author $FOUNDER | Out-Null
    Invoke-Git @('checkout','-q','-B','feature','main') | Out-Null
    New-Commit -Message 'clean work on top of it' -Author $BOT | Out-Null
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 0) -and ($r.Output -match 'OK:')) '8. a Founder commit already on main is not judged' $r.Output

    # 8b. The case that actually exercises the `--not main` term, and the reason it exists.
    #     Case 8 above passes on the BASE term alone - `main..feature` never contained
    #     main's own commits - so deleting the main exclusion left it green. This is the
    #     shape that catches it: a branch cut from an OLD base which then merges a main
    #     that has moved on. Without the exclusion, main's Founder commit is inside
    #     `oldBase..feature` and every such pull request goes red for history it did not
    #     write. Measured as a live mutant: removing the term reddens THIS row and nothing
    #     else, which is precisely why the row is here.
    Invoke-Git @('checkout', '-q', '-B', 'feature', $mainBase) | Out-Null
    New-Commit -Message 'branch work cut from the old base' -Author $BOT | Out-Null
    Invoke-Git @('merge', '--no-ff', '-q', '-m', 'merge main into the branch', 'main') | Out-Null
    $r = Invoke-Guard -Base $mainBase
    Write-Result (($r.Code -eq 0) -and ($r.Output -match 'OK:')) "8b. main's history merged into a branch is still not judged" $r.Output

    # 9. The door itself.
    Reset-Feature
    New-Commit -Message "the Founder's own commit" -Author $FOUNDER | Out-Null
    $r = Invoke-Guard -AllowFounder
    Write-Result ($r.Code -eq 0) '9. -AllowFounder admits the Founder' $r.Output
    $r = Invoke-Guard
    Write-Result ($r.Code -eq 1) '9b. ...and without it, the same commit is refused' $r.Output

    # 10. Every row of the allow-list, one at a time. A row that matches nothing has no
    #     symptom (trap 78), so an allow-list is only honest if each entry is exercised.
    $allowed = @(
        'dranakcorps@gmail.com'
        'DranakCorps-bot@users.noreply.github.com'
        '280514144+DranakCorps-bot@users.noreply.github.com'
        'noreply@github.com'
        '49699333+dependabot[bot]@users.noreply.github.com'
    )
    foreach ($email in $allowed) {
        Reset-Feature
        New-Commit -Message "commit as $email" -Author @{ Name = 'Whoever'; Email = $email } | Out-Null
        $r = Invoke-Guard
        Write-Result ($r.Code -eq 0) "10. allow-list row is live: $email" $r.Output
    }

    Write-Host ''
    Write-Host 'FAILING OPEN, OUT LOUD (each must exit 0 and SAY it judged nothing):'

    # 11. No commits of its own.
    Reset-Feature
    $r = Invoke-Guard
    Write-Result (($r.Code -eq 0) -and ($r.Output -match 'EMPTY:')) '11. an empty range says EMPTY rather than OK' $r.Output

    # 12. A base that does not resolve - a shallow checkout with no merge base. It must
    #     not throw, and it must not read as a pass over real commits.
    Reset-Feature
    New-Commit -Message 'work' -Author $FOUNDER | Out-Null
    $r = Invoke-Guard -Base 'refs/heads/no-such-base-ref'
    Write-Result (($r.Code -eq 0) -and ($r.Output -match 'SKIPPED:')) '12. an unresolvable base says SKIPPED' $r.Output

    Write-Host ''
    if ($failures -eq 0) {
        Write-Host "commit-identity-selftest: $checks checks, all green."
        exit 0
    }
    Write-Host "commit-identity-selftest: $failures of $checks checks FAILED."
    exit 1
} finally {
    try { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue } catch { }
}
