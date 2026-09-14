<#
.SYNOPSIS
    Every gate that must pass before a commit, in one command.

.DESCRIPTION
    Three guards, build, unit tests. Prints one summary line per stage and returns a
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
        $output | Select-String -Pattern 'error |Failed!|\[FAIL\]|Assert\.|whatsnew-guard|legacy-notice-guard|evolved-channel-guard|channel-wipe-guard|soft-seat-selftest' |
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
# Experiment A′ self-test (trap 70, EQBuddy lab): a second default seat on the
# same work item must refuse. Throwaway StoreDir; not the machine's live claims.
Step 'soft seats  ' { & "$PSScriptRoot\soft-seat-selftest.ps1" 6>&1 }
# The two generated catalogs against their generators. Neither script fetches — both read
# the committed cache — so this is free and it is the only thing that makes a weekly
# refresh PR's diff reviewable.
#
# FAILS OPEN, loudly, when there is no python: this is the fast local pass and the repo
# does not ask a WPF contributor to install a toolchain for a data gate. CI pins python
# 3.12 and runs the same two commands as a hard gate, so what is optional here is the
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
