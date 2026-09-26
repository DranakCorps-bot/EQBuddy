<#
.SYNOPSIS
    Every gate that must pass before a commit, in one command.

.DESCRIPTION
    The guards and their prove-fails, build, unit tests. (This line said "three guards" and
    had been wrong for some time before the channel size ratchet was added below it — the
    stage list is the answer to "how many", not a number in this paragraph.) Prints one
    summary line per stage and returns a
    non-zero exit code if any of them fail, so it is equally usable by a human and by
    an agent that only reads the tail of the output.

    E2E is deliberately NOT included: it launches the real app and needs a desktop
    session. Run tests/EQBuddy.E2E by hand when touching ingest or the widget's wiring.
    (CI does run it on every push and PR as of 2026-09-04 — this script is the fast
    local pass, not the whole gate.)

    THE `avalonia` STAGE AND THE `-Quick` SWITCH ARE GONE (E-2c, 2026-09-04). The switch
    existed only to skip that stage, so keeping it would have left a flag that does
    nothing — the shape this repo treats as a silent no-op.

.EXAMPLE
    pwsh -NoProfile -File scripts/check.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
$failed = @()

# Every stage's full output is TEED to a file, pass or fail. It used to live only in a
# variable that was filtered to 15 lines on failure and dropped entirely on success —
# so on 2026-08-23 a one-off Avalonia failure (Failed: 1, Passed: 278) could not be
# named, could not be reproduced in seven further runs, and had to be written up as a
# hypothesis. A gate that cannot say WHICH test failed is not much of a gate, and the
# run that matters is the one you cannot repeat.
$logDir = Join-Path $repo 'dist\check-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

function Step([string] $name, [scriptblock] $body) {
    Write-Host "-- $name " -NoNewline
    $slug = ($name.Trim() -replace '\s+', '-')
    $log = Join-Path $logDir "$stamp-$slug.log"
    $output = & $body 2>&1
    $output | Out-File -FilePath $log -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED" -ForegroundColor Red
        # Only the lines that say why — a full MSBuild log buries the one that matters.
        $output | Select-String -Pattern 'error |Failed!|\[FAIL\]|Assert\.|whatsnew-guard|legacy-notice-guard|evolved-channel-guard|channel-wipe-guard|channel-size-guard|channel-size-selftest|soft-seat-selftest|merge-sync|commit-identity|challenge-line-guard|FAIL: ' |
            Select-Object -First 15 | ForEach-Object { Write-Host "   $_" }
        Write-Host "   full log: $log" -ForegroundColor Yellow
        $script:failed += $name
    }
    else {
        $summary = $output | Select-String -Pattern 'Passed!|Build succeeded' |
            Select-Object -Last 1
        Write-Host "ok" -ForegroundColor Green -NoNewline
        if ($summary) { Write-Host "  $($summary -replace '\s+', ' ')" } else { Write-Host '' }
    }
}

# First, because it costs a second and it is the one gate that can see a defect the whole
# suite is blind to: a What's-new entry edited after its version shipped. 6>&1 folds the
# guard's Write-Host into the captured output so its reasons reach the log like any other
# stage's.
Step "what's-new  " { & "$PSScriptRoot\whatsnew-guard.ps1" 6>&1 }
# Same shape, same second: a promise about a release, checkable from the tree. It is a
# no-op while <Version> is 1.x and arms itself at 2.0.0 (LEGACY-007, #275).
Step 'legacy notice' { & "$PSScriptRoot\legacy-notice-guard.ps1" 6>&1 }
# And the third of the same family: EQBuddy Evolved develops local-only, which is a
# promise about what release.ps1 is ALLOWED to do. Also a no-op at 1.x, also armed by
# <Version> reaching 2.0.0 — and the one that reads the world as well as the tree, since
# the family's update folder is where the promise is actually kept or broken.
Step 'evolved     ' { & "$PSScriptRoot\evolved-channel-guard.ps1" 6>&1 }
# A channel ledger may not be emptied, truncated or wholesale-replaced by the work in
# flight. Three times in six days a commit that said it was signing something destroyed
# HELM.md or HELM-FEEDBACK.md instead (trap 60). Compares the WORKING TREE against the
# merge-base with origin/main, so a wipe fails here before it is ever committed.
Step 'channel     ' { & "$PSScriptRoot\channel-wipe-guard.ps1" 6>&1 }
# …and its prove-fail. Every check above is driven into the red once in a throwaway repo
# under TEMP; a wipe guard nobody has watched refuse is trap 34 with the stakes raised.
Step 'channel test' { & "$PSScriptRoot\channel-wipe-guard-selftest.ps1" 6>&1 }
# The size half of the same policy (DRA-73's 30-day / ~64 KB rotation trigger, enforced at
# last by DRA-26 rev 3 card A). The guard above refuses a ledger shrinking by the wrong
# mechanism; this one refuses one GROWING past 64 KiB, and the remedy it names is rotation
# into docs/ops/claude-archive/ — never deletion, which the guard above would refuse anyway.
# Working tree against the merge-base, so an over-limit append fails before it is committed.
Step 'channel size' { & "$PSScriptRoot\channel-size-guard.ps1" 6>&1 }
# …and its prove-fail, for the reason the one above has one.
Step 'size test   ' { & "$PSScriptRoot\channel-size-selftest.ps1" 6>&1 }
# No commit this branch ADDS may be authored or committed as the Founder (DRA-226). Seven
# commits and 4,589 lines of agent-written code were about to land in `git blame` under
# his name, from a dispatch clone whose [user] block said `David Edwards`; a linked
# worktree shares its clone's config, so one wrong block mis-attributed ~20 worktrees and
# 126 of the last 600 commits on main. Locally this judges HEAD against origin/main, so a
# mis-authored commit fails here while it is still rewritable — which is the whole window,
# because after the merge it is permanent.
Step 'commit id   ' { & "$PSScriptRoot\commit-identity-guard.ps1" 6>&1 }
# …and its prove-fail, for the reason the two above have one: six refusals driven red in a
# throwaway repo, every allow-list row exercised, and both fail-open paths asserted to SAY
# they judged nothing. Prove-failed against six mutants of the guard.
Step 'commit test ' { & "$PSScriptRoot\commit-identity-selftest.ps1" 6>&1 }
# The Challenger gate's keyed line (DRA-309 S3, off the DRA-305 SPEC). Every plan that
# reached the C-test carries `challenge: dra-<n>-… -> …` at the TOP of its body, and ZERO
# plans carry one inside a slice sequence — the gate fires once at plan SIGN and never per
# slice (§3.2), so a keyed line on a D(n+1) hand-off is the gate drifting into the slice
# sequence. The must-list is the half that can see a plan which quietly skipped the gate
# (trap 34); the forbid-scan alone is green on a repo that stopped walking it entirely.
Step 'gate line   ' { & "$PSScriptRoot\challenge-line-guard.ps1" 6>&1 }
# …and its prove-fail. Ten refusals driven red over throwaway corpora, five allowed shapes
# driven green (a guard that refused those would re-impose the every-card tax DRA-306's
# condition removed), and BOTH curated lists proven load-bearing by mutation rather than by
# inspection. Prove-failed against six mutants of the guard.
Step 'gate test   ' { & "$PSScriptRoot\challenge-line-selftest.ps1" 6>&1 }
# Experiment A′ self-test (trap 70, EQBuddy lab): a second default seat on the
# same work item must refuse. Throwaway StoreDir; not the machine's live claims.
Step 'soft seats  ' { & "$PSScriptRoot\soft-seat-selftest.ps1" 6>&1 }
# Paperclip merge-sync (DRA-77). Offline: no secret, no network, no GitHub event —
# it drives the linkage precedence and every status disposition, plus the
# one-way scope lock. This job's own trigger only fires AFTER a merge to the
# default branch, so a PR that changes it cannot otherwise test it; the
# self-test is the only thing a pull request can actually see.
Step 'merge sync  ' { & "$PSScriptRoot\merge-sync-selftest.ps1" 6>&1 }
# The ExO dashboard's own detectors (DRA-78). Offline: it exercises the classifiers
# and the interval arithmetic against fixtures, touching neither gh nor Paperclip.
# A metrics script nobody has watched misclassify is a dashboard that reports
# whatever it was already going to report — trap 78 with a number on it.
Step 'exo metrics ' { & "$PSScriptRoot\exo-metrics.ps1" -SelfTest 6>&1 }
# The landing telemetry snapshot writer (DRA-379 D1, DRA-440). Offline: fixture sites,
# fixture worker answers and loopback sockets only. Every refusal arm fires, including the
# one that keeps the held weeklyActive tile off the live page.
Step 'landing tel ' { & "$PSScriptRoot\landing-telemetry.ps1" -SelfTest 6>&1 }
# The three generated catalogs against their generators. None of the scripts fetches — they
# read the committed cache — so this is free and it is the only thing that makes a weekly
# refresh PR's diff reviewable.
#
# FAILS OPEN, loudly, when there is no python: this is the fast local pass and the repo
# does not ask a WPF contributor to install a toolchain for a data gate. CI pins python
# 3.12 and runs the same three commands as a hard gate, so what is optional here is the
# convenience, not the guard (same shape as the evolved-channel-guard's third check).
Step 'generated   ' {
    $py = (Get-Command python -ErrorAction SilentlyContinue) ??
          (Get-Command python3 -ErrorAction SilentlyContinue)
    if (-not $py) {
        Write-Host 'SKIPPED (no python on PATH) — CI runs this as a hard gate' -ForegroundColor Yellow
        $global:LASTEXITCODE = 0
        return
    }
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\guides-transform.py" --check
    if ($LASTEXITCODE -ne 0) { return }
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\epic-guides-build.py" --check
    if ($LASTEXITCODE -ne 0) { return }
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\zonelevels-transform.py" --check
    if ($LASTEXITCODE -ne 0) { return }
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\merchants-transform.py" --check
    if ($LASTEXITCODE -ne 0) { return }
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\zone-eras-transform.py" --check
    if ($LASTEXITCODE -ne 0) { return }
    # …and the parser's own arms. Both era refusals are unreachable in the committed corpus
    # (0 off-ladder words, 0 pages with two eras), so `--check` green says nothing about
    # whether they fire — a refusal that has never fired on anything is a guard aimed at
    # nothing (trap 78). `--selftest` runs them over synthetic wikitext.
    & $py.Source "$PSScriptRoot\harvests\eqlwiki\zone-eras-transform.py" --selftest
}
Step 'build      ' { dotnet build "$repo\EQBuddy.slnx" -c Release --nologo -v q }
Step 'unit tests  ' { dotnet test "$repo\tests\EQBuddy.Tests\EQBuddy.Tests.csproj" -c Release --nologo }

Write-Host ''
Write-Host "logs: $logDir" -ForegroundColor DarkGray
if ($failed.Count -gt 0) {
    Write-Host "FAILED: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host 'All gates green.' -ForegroundColor Green
