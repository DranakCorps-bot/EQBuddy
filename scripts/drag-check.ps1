# Hand-done drag/reopen check for the window-height follower.
# Stages the same isolated profile shoot.ps1 uses, then LEAVES the app running so the
# bottom edge can actually be dragged — the one acceptance criterion that cannot be
# shot or unit-tested.
param([string] $Root)

$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\david\source\EQBuddy'
$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'

if (-not $Root) {
    $Root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-drag-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
    $profileDir = New-Item -ItemType Directory -Force (Join-Path $Root 'profile')
    $logsDir = New-Item -ItemType Directory -Force (Join-Path $Root 'game/Logs')
    New-Item -ItemType Directory -Force (Join-Path $Root 'updates') | Out-Null
    & (Join-Path $repo 'scripts/make-test-session.ps1') -Out $logsDir.FullName | Out-Null

    $version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    $settings = @{
        LogFolder      = $logsDir.FullName
        ShowTutorial   = $false
        LastSeenVersion = $version
        TruncateLogs   = $false
        UpdateFolder   = (Join-Path $Root 'updates')
        Theme          = 'Midnight'
        ExpandedCards  = @('progress')
    }
    $settings | ConvertTo-Json -Depth 6 |
        Set-Content (Join-Path $profileDir 'settings.json') -Encoding utf8
} else {
    $profileDir = Get-Item (Join-Path $Root 'profile')
}

Write-Host "ROOT      $Root"
Write-Host "PROFILE   $profileDir"

$psi = New-Object Diagnostics.ProcessStartInfo $exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
$psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
$proc = [Diagnostics.Process]::Start($psi)
$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline -and $proc.MainWindowHandle -eq 0) {
    Start-Sleep -Milliseconds 400
    if ($proc.HasExited) { break }
    $proc.Refresh()
}
Write-Host "PID       $($proc.Id)"
Write-Host "Running. Drag the Progress window's bottom edge, then close it."
