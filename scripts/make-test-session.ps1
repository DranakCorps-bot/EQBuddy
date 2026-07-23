# Turns a fixture log into live-looking test data (cross-platform pwsh).
# Rewrites timestamps so the log ends one minute ago, with idle gaps >45 min
# compressed to 3 min — one rich "live" session for testing any UI against.
# See docs/FeatureGuide.md ("Testing without playing") for the full recipe.
#
#   pwsh scripts/make-test-session.ps1 -Out /tmp/eqbuddy-test/logs
#   EQBUDDY_APPDATA=/tmp/eqbuddy-test/appdata <app> (point LogFolder at -Out)
param(
    [string]$Fixture = '',
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Server = 'test'
)
$ErrorActionPreference = 'Stop'
if ($Fixture -eq '') {
    $Fixture = Join-Path (Split-Path $PSScriptRoot -Parent) 'tests/fixtures/eqlog_Testchar_fixture.txt'
}
if (-not (Test-Path $Fixture)) { throw "Fixture log not found: $Fixture" }
$fmt = 'ddd MMM dd HH:mm:ss yyyy'
$ci = [Globalization.CultureInfo]::InvariantCulture

$events = foreach ($line in Get-Content $Fixture) {
    if ($line -match '^\[(?<ts>[^\]]+)\] (?<msg>.*)$') {
        [pscustomobject]@{ T = [DateTime]::ParseExact($Matches.ts, $fmt, $ci); M = $Matches.msg }
    }
}

$shift = [TimeSpan]::Zero
$shifted = foreach ($i in 0..($events.Count - 1)) {
    if ($i -gt 0) {
        $gap = $events[$i].T - $events[$i - 1].T
        if ($gap.TotalMinutes -gt 45) { $shift += $gap - [TimeSpan]::FromMinutes(3) }
    }
    [pscustomobject]@{ T = $events[$i].T - $shift; M = $events[$i].M }
}

$delta = (Get-Date).AddMinutes(-1) - $shifted[-1].T
$character = ([IO.Path]::GetFileNameWithoutExtension($Fixture) -split '_')[1]
New-Item -ItemType Directory -Force $Out | Out-Null
$target = Join-Path $Out "eqlog_${character}_$Server.txt"
$shifted | ForEach-Object { "[$(($_.T + $delta).ToString($fmt, $ci))] $($_.M)" } |
    Set-Content $target -Encoding UTF8
Write-Host "Wrote $target ($($shifted.Count) lines, session ends 1 minute ago)"
Write-Host "Append lines to it while the app runs to simulate live play."
