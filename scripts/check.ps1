<#
.SYNOPSIS
    Every gate that must pass before a commit, in one command.

.DESCRIPTION
    Build, unit tests, Avalonia tests. Prints one summary line per stage and returns a
    non-zero exit code if any of them fail, so it is equally usable by a human and by
    an agent that only reads the tail of the output.

    E2E is deliberately NOT included: it launches the real app and needs a desktop
    session. Run tests/EQBuddy.E2E by hand when touching ingest or the widget's wiring.

.EXAMPLE
    pwsh -NoProfile -File scripts/check.ps1
    pwsh -NoProfile -File scripts/check.ps1 -Quick   # skip the Avalonia suite
#>
[CmdletBinding()]
param([switch] $Quick)

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
        $output | Select-String -Pattern 'error |Failed!|\[FAIL\]|Assert\.|whatsnew-guard|legacy-notice-guard' |
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
Step 'build      ' { dotnet build "$repo\EQBuddy.slnx" -c Release --nologo -v q }
Step 'unit tests  ' { dotnet test "$repo\tests\EQBuddy.Tests\EQBuddy.Tests.csproj" -c Release --nologo }
if (-not $Quick) {
    Step 'avalonia    ' { dotnet test "$repo\tests\EQBuddy.Avalonia.Tests\EQBuddy.Avalonia.Tests.csproj" -c Release --nologo }
}

Write-Host ''
Write-Host "logs: $logDir" -ForegroundColor DarkGray
if ($failed.Count -gt 0) {
    Write-Host "FAILED: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host 'All gates green.' -ForegroundColor Green
