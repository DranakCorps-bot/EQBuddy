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

# --- Who signs (DRA-679) ---------------------------------------------------------------
#
# The dlib authenticates with a DefaultAzureCredential and reads `ExcludeCredentials`
# from the metadata file. Two identities are allowed to answer it, in this order:
#
#   ServicePrincipal  the release signing login: an app whose credential is a
#                     NON-EXPORTABLE certificate in Cert:\CurrentUser\My, reached
#                     through Az PowerShell (Connect-AzAccount -CertificateThumbprint).
#                     Made by scripts\signing-identity.ps1. Needs no human.
#   AzureCli          the Founder's `az login` session. The FALLBACK — still a signed
#                     path, never an unsigned one.
#
# Neither available -> throw, before the build. There is no third answer.
#
# Every sign writes its own copy of the metadata with every OTHER credential excluded,
# so the identity this file printed is the only one that can answer. Measured on
# 2026-10-01 (DRA-695) against dlib 1.0.128 after `az logout`: the excludes bind (the
# dlib tries only the one left), a wrong thumbprint and a deleted key both FAIL the
# sign, and the persisted Az context carries the thumbprint and no token, so a context
# whose key is gone cannot sign either.

# Pinned for the same reason as the dlib, and restored into tools\ the same way: the
# user module folder sits under Documents, which Controlled Folder Access refuses to
# write (measured), and a release must not need a manual install.
$script:AzAccountsVersion = '5.5.3'

$script:IdentityFileName = 'artifact-signing-identity.json'
$script:ExpiryWarnDays   = 30

# Every credential DefaultAzureCredential can try, by the names the dlib's
# ExcludeCredentials reads (its Exclude*Credential setters, 1.0.128).
$script:AllAzureCredentials = @(
    'EnvironmentCredential', 'WorkloadIdentityCredential', 'ManagedIdentityCredential',
    'SharedTokenCacheCredential', 'VisualStudioCredential', 'VisualStudioCodeCredential',
    'AzureCliCredential', 'AzurePowerShellCredential', 'AzureDeveloperCliCredential',
    'InteractiveBrowserCredential'
)

$script:SigningIdentity = $null

function Read-EqSigningIdentityFile {
    # TenantId, ClientId, CertificateThumbprint — identifiers only; none of them signs.
    param([Parameter(Mandatory)][string]$Repo)
    $path = Join-Path $Repo $script:IdentityFileName
    if (-not (Test-Path $path)) { return $null }
    $id = Get-Content $path -Raw | ConvertFrom-Json
    foreach ($field in 'TenantId', 'ClientId', 'CertificateThumbprint') {
        if (-not $id.$field) { throw "$path has no $field. Re-run scripts\signing-identity.ps1 -Create." }
    }
    return $id
}

function Get-EqSignerCertificateState {
    # A VALUE for every world (trap 81): Usable, ExpiringSoon (signs, and warns),
    # Missing, Expired, NoPrivateKey. Only the first two may sign.
    param(
        [string]$Thumbprint,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [datetime]$Now = (Get-Date)
    )
    if (-not $Certificate -and $Thumbprint) {
        $Certificate = Get-Item "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
    }
    if (-not $Certificate) {
        return [pscustomobject]@{ State = 'Missing'; Reason = "certificate $Thumbprint is not in Cert:\CurrentUser\My"; NotAfter = $null }
    }
    $notAfter = $Certificate.NotAfter
    if ($notAfter -le $Now) {
        return [pscustomobject]@{ State = 'Expired'; Reason = "certificate expired $($notAfter.ToString('yyyy-MM-dd'))"; NotAfter = $notAfter }
    }
    if (-not $Certificate.HasPrivateKey) {
        return [pscustomobject]@{ State = 'NoPrivateKey'; Reason = 'certificate has no private key on this machine'; NotAfter = $notAfter }
    }
    $days = [int][math]::Floor(($notAfter - $Now).TotalDays)
    if ($days -lt $script:ExpiryWarnDays) {
        return [pscustomobject]@{ State = 'ExpiringSoon'; Reason = "certificate expires in $days day(s), $($notAfter.ToString('yyyy-MM-dd'))"; NotAfter = $notAfter }
    }
    return [pscustomobject]@{ State = 'Usable'; Reason = "certificate valid until $($notAfter.ToString('yyyy-MM-dd'))"; NotAfter = $notAfter }
}

function Resolve-EqSigningIdentity {
    # Pure: every input is handed in, so -selftest can drive each world without Azure.
    #   $Identity     the identity file's contents, or $null
    #   $CertState    Get-EqSignerCertificateState's answer, or $null when there is no identity
    #   $SpFailure    why Connect-AzAccount refused the SP this run, or $null
    #   $CliAccount   `az account show`'s answer, or $null when there is no session
    param($Identity, $CertState, [string]$SpFailure, $CliAccount)

    # Every skip arm answers a non-empty reason: an empty one reads as "nothing to skip"
    # below and hands the release to an SP whose certificate nobody looked at (DRA-697).
    $spReason = if (-not $Identity) { "no $($script:IdentityFileName)" }
                elseif (-not $CertState) { 'the signing certificate was never checked' }
                elseif ($CertState.State -notin 'Usable', 'ExpiringSoon') {
                    if ($CertState.Reason) { $CertState.Reason } else { "signing certificate is $($CertState.State)" } }
                elseif ($SpFailure) { "service principal sign-in failed: $SpFailure" }
                else { $null }

    if (-not $spReason) {
        return [pscustomobject]@{
            Kind = 'ServicePrincipal'; Credential = 'AzurePowerShellCredential'
            Who  = "service principal $($Identity.ClientId)"; Warning = $(if ($CertState.State -eq 'ExpiringSoon') { $CertState.Reason })
            SkippedBecause = $null
        }
    }
    if ($CliAccount) {
        return [pscustomobject]@{
            Kind = 'AzureCli'; Credential = 'AzureCliCredential'
            Who  = "$($CliAccount.user.name) via az login"; Warning = $null
            SkippedBecause = $spReason
        }
    }
    throw @"
Cannot sign: neither signing login is available, so the release stops here (never unsigned).
  Service principal: $spReason
  az login:          no session
Fix ONE of them and re-run:
  - the release login: pwsh -NoProfile -File scripts\signing-identity.ps1 -Check   (or -Create / -Rotate)
  - the fallback:      az login
"@
}

function Get-EqExcludedCredentials {
    # Everything except the one credential the resolved identity uses.
    param([Parameter(Mandatory)][string]$Credential)
    if ($Credential -notin $script:AllAzureCredentials) { throw "Unknown credential '$Credential'." }
    return @($script:AllAzureCredentials | Where-Object { $_ -ne $Credential })
}

function New-EqSigningMetadata {
    # A per-sign copy of artifact-signing.json plus ExcludeCredentials. Holds no secret;
    # it is per-run only so the shared file never carries one run's choice into the next.
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Credential)
    $meta = Get-Content $Source -Raw | ConvertFrom-Json
    $meta | Add-Member -NotePropertyName ExcludeCredentials -NotePropertyValue (Get-EqExcludedCredentials -Credential $Credential) -Force
    $path = Join-Path ([System.IO.Path]::GetTempPath()) "eqbuddy-signing-$([guid]::NewGuid().ToString('N')).json"
    $meta | ConvertTo-Json | Set-Content -Path $path -Encoding utf8
    return $path
}

function Import-EqAzAccounts {
    param([Parameter(Mandatory)][string]$Repo)
    $root = Join-Path $Repo 'tools\psmodules'
    $psd1 = Join-Path $root "Az.Accounts\$($script:AzAccountsVersion)\Az.Accounts.psd1"
    if (-not (Test-Path $psd1)) {
        Write-Host "Az.Accounts missing; restoring $($script:AzAccountsVersion) from the PowerShell Gallery"
        New-Item -ItemType Directory -Force $root | Out-Null
        Save-PSResource -Name Az.Accounts -Version $script:AzAccountsVersion -Path $root -Repository PSGallery -TrustRepository
    }
    if (-not (Test-Path $psd1)) { throw "Restored Az.Accounts but $psd1 is still missing." }
    # Process-wide on purpose: the dlib's AzurePowerShellCredential starts its OWN pwsh,
    # which inherits this and must find the same pinned module.
    if (($env:PSModulePath -split ';') -notcontains $root) { $env:PSModulePath = "$root;$env:PSModulePath" }
    Import-Module $psd1 -ErrorAction Stop
}

function Connect-EqSigningServicePrincipal {
    param([Parameter(Mandatory)]$Identity)
    Connect-AzAccount -ServicePrincipal -Tenant $Identity.TenantId -ApplicationId $Identity.ClientId `
        -CertificateThumbprint $Identity.CertificateThumbprint -SkipContextPopulation -WarningAction SilentlyContinue | Out-Null
}

function Select-EqKeyCredential {
    # Pure (DRA-697): which of `az ad app credential list --cert`'s rows is the key
    # credential for $Thumbprint. Graph types customKeyIdentifier as binary, and the
    # form az hands back was never measured, so both readings are accepted: the hex
    # thumbprint itself, or the base64 of its bytes. The caller asserts the COUNT —
    # a match of zero must never read as "removed".
    param($Credentials, [Parameter(Mandatory)][string]$Thumbprint)
    $hex = $Thumbprint.Trim()
    $b64 = try { [Convert]::ToBase64String([Convert]::FromHexString($hex)) } catch { $null }
    return @(@($Credentials | ForEach-Object { $_ }) | Where-Object {
        $_ -and $_.customKeyIdentifier -and
        ($_.customKeyIdentifier -ieq $hex -or ($b64 -and $_.customKeyIdentifier -ceq $b64)) })
}

function Initialize-EqSigningIdentity {
    param([Parameter(Mandatory)][string]$Repo)

    $identity  = Read-EqSigningIdentityFile -Repo $Repo
    $certState = if ($identity) { Get-EqSignerCertificateState -Thumbprint $identity.CertificateThumbprint }
    $spFailure = $null
    if ($identity -and $certState.State -in 'Usable', 'ExpiringSoon') {
        try {
            Import-EqAzAccounts -Repo $Repo
            Connect-EqSigningServicePrincipal -Identity $identity
        } catch {
            $spFailure = ($_.Exception.Message -split "`r?`n")[0]
        }
    }
    $cli = $null
    $resolvedSp = $identity -and -not $spFailure -and $certState.State -in 'Usable', 'ExpiringSoon'
    if (-not $resolvedSp) {
        # Only asked when the SP cannot answer: a live session must not be a reason the
        # SP is skipped, and az is slow to start.
        $json = & az account show 2>$null
        if ($LASTEXITCODE -eq 0 -and $json) { $cli = $json | ConvertFrom-Json }
    }

    $resolved = Resolve-EqSigningIdentity -Identity $identity -CertState $certState -SpFailure $spFailure -CliAccount $cli
    if ($resolved.SkippedBecause) {
        Write-Host "Signing login skipped: $($resolved.SkippedBecause) — falling back to az login." -ForegroundColor Yellow
    }
    if ($resolved.Warning) {
        Write-Host "WARN: signing $($resolved.Warning). Rotate it: scripts\signing-identity.ps1 -Rotate (release seat: file a Planner rotation card)." -ForegroundColor Yellow
    }
    Write-Host "Signing identity: $($resolved.Who)"
    return $resolved
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
    $script:SigningIdentity = Initialize-EqSigningIdentity -Repo $Repo

    Write-Host "Signing ready: $(Split-Path $script:SignTool -Leaf) + Azure Artifact Signing"
}

function Invoke-EqSign {
    param([Parameter(Mandatory)][string]$Path)

    if (-not $script:SignTool -or -not $script:SigningIdentity) { throw 'Initialize-EqSigning must run before Invoke-EqSign.' }
    if (-not (Test-Path $Path)) { throw "Cannot sign missing file: $Path" }

    $name = Split-Path $Path -Leaf

    # /v for a readable log, /fd + /td SHA256 for the file and timestamp digests.
    # See fact 2: this only works because it is PowerShell invoking it.
    $runMetadata = New-EqSigningMetadata -Source $script:Metadata -Credential $script:SigningIdentity.Credential
    try {
        & $script:SignTool sign /v /fd SHA256 /tr $script:TimestampUrl /td SHA256 `
            /dlib $script:Dlib /dmdf $runMetadata $Path
        $code = $LASTEXITCODE
    } finally {
        Remove-Item $runMetadata -ErrorAction SilentlyContinue
    }
    if ($code -ne 0) {
        throw "signtool failed on $name (exit $code) — refusing to ship an unsigned build."
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
