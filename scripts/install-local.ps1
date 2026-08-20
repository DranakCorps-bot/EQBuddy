# Build the current tree and silently install it on THIS machine only — the standing
# loop for testing changes before a release (David's rule: everything gets field-tested
# locally first). Touches neither OneDrive nor GitHub; the family keeps whatever
# release.ps1 last shipped. The updater won't fight it: it only ever offers NEWER
# versions than the one running.
#
#   pwsh scripts\install-local.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\signing.ps1"

$props = Get-Content "$repo\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]
Write-Host "Installing EQBuddy $version locally (no release)"

# Same toolchain as release.ps1, resolved BEFORE the build for the same reason: a broken
# signing setup should cost a second, not a 172 MB publish.
#
# This script used to look for a self-signed cert with "EQBuddy" in its subject and
# silently skip signing when it found none. That certificate — and the script that made
# it — were deleted on 2026-08-19, so from that day every local install has been
# UNSIGNED and nothing said so. That is not a shipping-rule violation (nothing here
# reaches OneDrive, GitHub or the update channel), but it is a testing one: an unsigned
# build is exactly what re-triggered Defender's cloud-ML false positive, so the local
# copy has to carry the same publisher identity as the real one or a local test is
# testing a different artifact from the one players get.
Initialize-EqSigning -Repo $repo

# Gracefully, not Stop-Process -Force. EQBuddy finalizes its session into history.db on
# exit, and the cost of a test build must never be someone's session record — the same
# reason shoot.ps1 stands the app down with CloseMainWindow. Force is the fallback only.
$running = Get-Process EQBuddy -ErrorAction SilentlyContinue
if ($running) {
    Write-Host 'Closing the running EQBuddy (gracefully, so it finalizes its session)'
    foreach ($p in $running) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(15000)) { $p | Stop-Process -Force }
    }
    Start-Sleep -Seconds 1
}

dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$repo\dist\publish"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Sign the app before Inno Setup packages it, so the installer carries a signed payload
# as well as being signed itself. Invoke-EqSign throws on anything short of a verified,
# timestamped signature — no warn-and-continue here either, because "it installed but
# quietly unsigned" is the state this script sat in for a day without anyone noticing.
Invoke-EqSign "$repo\dist\publish\EQBuddy.exe"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "/DAppVersion=$version" "$repo\installer\EQBuddy.iss" | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { throw 'installer compile failed' }
Invoke-EqSign "$repo\dist\EQBuddySetup.exe"

Start-Process "$repo\dist\EQBuddySetup.exe" -ArgumentList '/SILENT'
Write-Host "Installer launched (/SILENT); EQBuddy relaunches itself when it finishes."
