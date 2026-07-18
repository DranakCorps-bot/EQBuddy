# EQBuddy release: publish exe, sign, compile installer, sign it, refresh zip,
# push to OneDrive (the family's install + auto-update channel).
# Commit + `git push` your source changes too; git is the source-code backup.
param([string]$Tag)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$oneDrive = 'C:\Users\david\OneDrive\EQBuddyDownload'

# Version comes from the csproj so the app, installer, and updater always agree.
$csproj = Get-Content "$repo\src\EQBuddy\EQBuddy.csproj" -Raw
if ($csproj -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in csproj' }
$version = $Matches[1]
Write-Host "Releasing EQBuddy $version"

Get-Process EQBuddy -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$repo\dist\publish"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Sign with the self-signed EQBuddy cert if present (create once with scripts\new-cert.ps1).
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -like '*EQBuddy*' } | Select-Object -First 1
function Sign-File($path) {
    if (-not $cert) { Write-Warning "No EQBuddy code-signing cert found; skipping signature for $path"; return }
    $r = Set-AuthenticodeSignature -FilePath $path -Certificate $cert `
        -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
    Write-Host "Signed $(Split-Path $path -Leaf): $($r.Status)"
}
Sign-File "$repo\dist\publish\EQBuddy.exe"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "/DAppVersion=$version" "$repo\installer\EQBuddy.iss"
if ($LASTEXITCODE -ne 0) { throw 'installer compile failed' }
Sign-File "$repo\dist\EQBuddySetup.exe"

Compress-Archive -Path "$repo\dist\publish\EQBuddy.exe", "$repo\README.md" `
    -DestinationPath "$repo\dist\EQBuddy-portable.zip" -Force

New-Item -ItemType Directory -Force $oneDrive | Out-Null
Copy-Item "$repo\dist\EQBuddySetup.exe", "$repo\dist\EQBuddy-portable.zip" $oneDrive -Force
Write-Host "Released $version to $oneDrive (family widgets will offer the update within 6 h)"

if ($Tag) {
    gh release create $Tag "$repo\dist\EQBuddySetup.exe" "$repo\dist\EQBuddy-portable.zip" `
        --title "EQBuddy $Tag" --generate-notes
    if ($LASTEXITCODE -ne 0) { throw 'gh release failed' }
    Write-Host "GitHub release $Tag published"
}
