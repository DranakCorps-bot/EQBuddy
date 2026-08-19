# Artifact Signing (Azure) — the ONE place EQBuddy binaries get signed.
# Dot-source this; release.ps1 is its only caller today.
#
# Replaces the self-signed certificate (scripts\new-cert.ps1, deleted 2026-08-19),
# which bought a consistent publisher NAME and nothing else: any machine that had not
# manually imported dist\EQBuddy-publisher.cer still saw an untrusted root, so
# SmartScreen and Defender read every release as an unknown single-file installer.
# Signatures now chain to Microsoft's public CA as FlossworksCross-Stitch.
#
# Four facts drive the design below. Each one already cost a debugging session:
#
#  1. Artifact Signing certificates live for THREE DAYS. A signature outlives its
#     certificate only because of the countersigned timestamp, so an untimestamped
#     release goes invalid by the weekend — on machines that already installed it.
#     The timestamp is not a flag you may drop to make a command shorter.
#  2. signtool MUST be invoked from PowerShell. Git Bash rewrites the leading slash
#     in `/fd` into a filesystem path, and signtool then reports "No file digest
#     algorithm specified" while `/fd SHA256` sits in plain sight in the command.
#  3. signtool's exit code is NOT proof. It reports success for signatures whose
#     chain will not validate on a player's machine, so every sign here is checked
#     with Get-AuthenticodeSignature before this file calls it done.
#  4. The dlib is gitignored (tools\). A fresh clone has no signing toolchain at all,
#     so this restores it rather than failing — a release must not need a shopping
#     list of manual installs to run unattended.

$ErrorActionPreference = 'Stop'

# Pinned deliberately: a signing toolchain that moves on its own produces output you
# cannot reproduce. Bumping this is an edit someone makes on purpose.
$script:DlibPackageId      = 'Microsoft.ArtifactSigning.Client'
$script:DlibPackageVersion = '1.0.128'

# Artifact Signing's own timestamp authority. See fact 1 — never make this optional.
$script:TimestampUrl = 'http://timestamp.acs.microsoft.com'

$script:SignTool = $null
$script:Dlib     = $null
$script:Metadata = $null

function Get-EqSignToolPath {
    # The Artifact Signing dlib needs a modern Windows SDK signtool; the 20348 SDK
    # ships one it refuses to load, which presents as a bare load failure rather than
    # anything naming the version. Take the newest that isn't 20348.
    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { Test-Path $_ }

    $candidates = @()
    foreach ($root in $roots) {
        foreach ($dir in (Get-ChildItem $root -Directory -ErrorAction SilentlyContinue)) {
            if ($dir.Name -notmatch '^10\.0\.\d+\.\d+$') { continue }
            if ($dir.Name -match '^10\.0\.20348\.')      { continue }
            $exe = Join-Path $dir.FullName 'x64\signtool.exe'
            if (Test-Path $exe) {
                $candidates += [pscustomobject]@{ Version = [version]$dir.Name; Path = $exe }
            }
        }
    }

    $best = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $best) {
        throw @'
No usable signtool.exe found (Windows Kits 10, x64, not the 20348 SDK).
Install it with:
    winget install -e --id Microsoft.Azure.ArtifactSigningClientTools
'@
    }
    return $best.Path
}

function Restore-EqSigningDlib {
    param([Parameter(Mandatory)][string]$Destination)

    # A .nupkg is a zip, so this needs no nuget.exe and no project file — one
    # download, one expand, pinned to $DlibPackageVersion.
    $id  = $script:DlibPackageId.ToLowerInvariant()
    $ver = $script:DlibPackageVersion
    $url = "https://api.nuget.org/v3-flatcontainer/$id/$ver/$id.$ver.nupkg"
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) "$id.$ver.zip"

    Write-Host "Signing dlib missing; restoring $($script:DlibPackageId) $ver from nuget.org"
    Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
    try {
        Expand-Archive -Path $tmp -DestinationPath $Destination -Force
    } finally {
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }
}

function Assert-EqAzureSignIn {
    # DefaultAzureCredential inside the dlib picks up the Azure CLI session. Checking
    # it here turns "403 Forbidden" three minutes into a release into one clear line
    # before the build starts.
    $json = & az account show 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $json) {
        throw @'
Not signed in to Azure, so the installer cannot be signed.
Sign in and re-run the release:
    az login
The session lasts for weeks; this is the only step signing ever asks a human for.
'@
    }
    $account = $json | ConvertFrom-Json
    Write-Host "Azure sign-in: $($account.user.name) ($($account.name))"
}

function Initialize-EqSigning {
    param([Parameter(Mandatory)][string]$Repo)

    # Everything is resolved BEFORE the 172 MB publish, so a misconfigured toolchain
    # costs a second rather than a full build.
    #
    # Repo root, not dist\ — dist is build output and gets wiped (trap 18's rebuild
    # advice deletes bin/obj, and a stale-artifact hunt eventually reaches dist too).
    # Config that signing cannot run without does not belong somewhere disposable.
    # Gitignored: it names Azure resources, and this repo is public.
    $script:Metadata = Join-Path $Repo 'artifact-signing.json'
    if (-not (Test-Path $script:Metadata)) {
        throw @"
Missing $($script:Metadata) — signing has no account to talk to.
It should contain the Artifact Signing endpoint, account and certificate profile:
    {
      "Endpoint": "https://cus.codesigning.azure.net",
      "CodeSigningAccountName": "EQBuddy",
      "CertificateProfileName": "EQBuddyPublicTrust"
    }
The Endpoint region MUST match the account's region or signing fails with 403.
"@
    }

    $tools = Join-Path $Repo 'tools'
    $script:Dlib = Join-Path $tools "$($script:DlibPackageId)\bin\x64\Azure.CodeSigning.Dlib.dll"
    if (-not (Test-Path $script:Dlib)) {
        New-Item -ItemType Directory -Force $tools | Out-Null
        Restore-EqSigningDlib -Destination (Join-Path $tools $script:DlibPackageId)
    }
    if (-not (Test-Path $script:Dlib)) {
        throw "Restored $($script:DlibPackageId) but $($script:Dlib) is still missing."
    }

    $script:SignTool = Get-EqSignToolPath
    Assert-EqAzureSignIn

    Write-Host "Signing ready: $(Split-Path $script:SignTool -Leaf) + Azure Artifact Signing"
}

function Invoke-EqSign {
    param([Parameter(Mandatory)][string]$Path)

    if (-not $script:SignTool) { throw 'Initialize-EqSigning must run before Invoke-EqSign.' }
    if (-not (Test-Path $Path)) { throw "Cannot sign missing file: $Path" }

    $name = Split-Path $Path -Leaf

    # /v for a readable log, /fd + /td SHA256 for the file and timestamp digests.
    # See fact 2: this only works because it is PowerShell invoking it.
    & $script:SignTool sign /v /fd SHA256 /tr $script:TimestampUrl /td SHA256 `
        /dlib $script:Dlib /dmdf $script:Metadata $Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed on $name (exit $LASTEXITCODE) — refusing to ship an unsigned build."
    }

    # Fact 3: verify rather than trust. A release that ships a signature Windows will
    # reject is worse than one that fails here, because players find that one.
    $sig = Get-AuthenticodeSignature -FilePath $Path
    if ($sig.Status -ne 'Valid') {
        throw "$name signed but does not verify: $($sig.Status) — $($sig.StatusMessage)"
    }
    if (-not $sig.TimeStamperCertificate) {
        throw "$name has no timestamp; the signature would expire with the 3-day certificate."
    }

    $subject = ($sig.SignerCertificate.Subject -split ',')[0]
    Write-Host "Signed $name — $subject (valid, timestamped)"
}
