# The trailer's phone shot: EQBuddy Mobile, served by the REAL app from a REAL log.
#
#   pwsh -NoProfile -File scripts/trailer/phone.ps1 -SourceLog <eqlog_Name_server.txt> `
#        -CutAt 'yyyy-MM-dd HH:mm:ss' -Out <dir>
#
# The landing's phone picture is the Testchar fixture through mobile-harness.ps1's stubbed
# socket. This one is the other half of the same promise: the built EQBuddy.exe on a
# throwaway profile (IsolatedLaunchPolicy's rule — EQBUDDY_APPDATA pinned, the live profile
# refused), the log staged by scripts/real-log-staging.ps1, the companion server switched on
# with a token this script chooses, and the SHIPPED page loaded over the LAN by headless Edge
# at a phone's viewport (phone.py). Nothing is stubbed; the page draws what the app sends.
#
# The screen: the fixture app puts its widget up on the secondary monitor for the length of
# the capture, so this takes the same screen lock as shoot.ps1 (trap 61). The player's own
# EQBuddy is left running — the single-instance lock is per profile.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceLog,
    [Parameter(Mandatory)][string]$CutAt,
    [Parameter(Mandatory)][string]$Out,
    [int]$Port = 47998,
    [int]$Settle = 45,
    [string]$Theme = 'BlueGrey',
    # A Python with playwright installed (README.md: the trailer's venv).
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$scripts = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $scripts -Parent
. (Join-Path $scripts 'isolated-profile.ps1')
. (Join-Path $scripts 'real-log-staging.ps1')

$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'
if (-not (Test-Path $exe)) { throw "EQBuddy.exe not built at $exe. Run: dotnet build EQBuddy.slnx -c Release" }
$python = $Python
New-Item -ItemType Directory -Force $Out | Out-Null

$lockPath = Join-Path ([IO.Path]::GetTempPath()) 'eqbuddy-screen.lock'
$lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::Read)

$root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-phone-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$profileDir = New-Item -ItemType Directory -Force (Join-Path $root 'profile')
$logsDir = New-Item -ItemType Directory -Force (Join-Path $root 'game/Logs')
$updateDir = New-Item -ItemType Directory -Force (Join-Path $root 'updates')
Assert-EqIsolatedProfile $profileDir.FullName 'trailer/phone.ps1'
$proc = $null
try {
    $staged = Copy-EqRealLogStaged $SourceLog $CutAt $logsDir.FullName
    $token = [Guid]::NewGuid().ToString('N').Substring(0, 16)
    $version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    Add-Type -AssemblyName System.Windows.Forms
    $sec = [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
    $origin = if ($sec) { $sec.WorkingArea } else { [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea }
    @{
        LogFolder = $logsDir.FullName; UpdateFolder = $updateDir.FullName; Theme = $Theme
        WindowLeft = $origin.X + 120; WindowTop = $origin.Y + 120; Minimized = $true
        ShowTutorial = $false; SetupDismissed = $true; LastSeenVersion = $version
        WatchPinsMigrated = $true; WatchChipMasterRetired = $true; DefaultRulesVersion = 1
        TruncateLogs = $false; ArchiveLogs = $false; TrackSpawns = $true
        DisabledBreakouts = @('Damage','Healing','Pet','Watch','Loot','Buffs')
        CompanionEnabled = $true; CompanionPort = $Port; CompanionToken = $token
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $profileDir 'settings.json') -Encoding UTF8

    $psi = New-Object Diagnostics.ProcessStartInfo $exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
    $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
    $proc = [Diagnostics.Process]::Start($psi)
    Write-Host "EQBuddy pid $($proc.Id); settling $Settle s while it folds the log"
    Start-Sleep -Seconds $Settle

    # The server binds LAN addresses only (trap 77), so the page is fetched on one of them.
    $ip = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.|100\.)' -and $_.PrefixOrigin -ne 'WellKnown' } |
        Sort-Object InterfaceMetric | Select-Object -First 1 -ExpandProperty IPAddress
    if (-not $ip) { throw 'No LAN IPv4 address to reach the companion server on.' }
    $url = "http://${ip}:$Port/#$token"
    Write-Host "Companion page at http://${ip}:$Port/ (token in the fragment)"
    & $python (Join-Path $PSScriptRoot 'phone.py') $url $Out
    if ($LASTEXITCODE -ne 0) { throw "phone.py failed ($LASTEXITCODE)" }
}
finally {
    if ($proc -and -not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        if (-not $proc.WaitForExit(10000)) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue
    $lock.Dispose()
}
