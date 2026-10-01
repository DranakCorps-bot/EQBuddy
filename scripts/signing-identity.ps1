# The release signing LOGIN (DRA-679): a service principal whose credential is a
# NON-EXPORTABLE certificate in Cert:\CurrentUser\My, so a release signs without a
# human refreshing `az login`. scripts\signing.ps1 prefers it; `az login` stays the
# signed fallback. Plan and threat model: docs/plans/DRA-679.md.
#
#   -Create   make the cert, the app + SP, the one role assignment; write the
#             gitignored artifact-signing-identity.json. Idempotent.
#   -Check    re-assert the one-row role listing (C-4 / K2) and the cert's state.
#   -Rotate   new cert, appended; prove the SP can get a token with it; then remove
#             the old key credential and the old local key.
#   -Revoke   section 7 steps 1-2: delete the role assignment, then the app (which
#             deletes the SP). -RemoveKey also drops the local key (step 4).
#
# Every Azure call here runs under the caller's own `az` session (the Founder's): the
# SP is given no rights over identity, so it can never manage itself.
#
# What is on disk afterwards is three identifiers (tenant, client id, thumbprint) —
# none of them signs anything. The key can be USED by this Windows user and copied by
# nobody; with the TPM provider it cannot leave the machine at all.
[CmdletBinding(DefaultParameterSetName = 'Check')]
param(
    [Parameter(ParameterSetName = 'Create', Mandatory)][switch]$Create,
    [Parameter(ParameterSetName = 'Check')][switch]$Check,
    [Parameter(ParameterSetName = 'Rotate', Mandatory)][switch]$Rotate,
    [Parameter(ParameterSetName = 'Revoke', Mandatory)][switch]$Revoke,
    [Parameter(ParameterSetName = 'Revoke')][switch]$RemoveKey,
    # The checkout that holds artifact-signing.json (gitignored, so a linked worktree
    # does not have one). Defaults to the clone's main checkout.
    [string]$Repo
)

$ErrorActionPreference = 'Stop'

if (-not $Repo) {
    $common = & git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir
    $Repo = (Resolve-Path (Join-Path $common '..')).Path
}

$DisplayName  = 'EQBuddy Release Signer'
$Months       = 12
# The same name scripts\signing.ps1 reads. Gitignored beside artifact-signing.json.
$IdentityFile = Join-Path $Repo 'artifact-signing-identity.json'

function Invoke-Az {
    # az writes JSON to stdout; a refusal is a non-zero exit and a stderr line. The
    # plan's stop rule (section 6 step 1): a refused call STOPS — no retry, no
    # workaround — so this throws with az's own words.
    $out = & az @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($args -join ' ') was REFUSED (exit $LASTEXITCODE):`n$($out -join "`n")`nSTOP: file the DRA-679 section 8 Founder card. No workaround."
    }
    $text = ($out | Where-Object { $_ -is [string] }) -join "`n"
    if ($text.Trim()) { return $text | ConvertFrom-Json }
}

function Get-ProfileScope {
    $meta = Get-Content (Join-Path $Repo 'artifact-signing.json') -Raw | ConvertFrom-Json
    $acct = Invoke-Az resource list --resource-type Microsoft.CodeSigning/codeSigningAccounts `
        --name $meta.CodeSigningAccountName -o json
    if (@($acct).Count -ne 1) { throw "Expected one code signing account named $($meta.CodeSigningAccountName); found $(@($acct).Count)." }
    return "$(@($acct)[0].id)/certificateProfiles/$($meta.CertificateProfileName)"
}

function Get-SignerRoleId {
    # Recorded by GUID so the role's rename (Trusted Signing -> Artifact Signing) cannot
    # matter. Measured 2026-10-01: 2837e146-70d7-4cfd-ad55-7efa6464f958.
    $r = Invoke-Az role definition list --name 'Artifact Signing Certificate Profile Signer' -o json
    if (@($r).Count -ne 1) { throw 'The Artifact Signing Certificate Profile Signer role was not found exactly once.' }
    return @($r)[0].name
}

function Assert-OneRoleRow {
    # C-4 / K2: exactly one row — that role, that scope. Anything else revokes and stops.
    param([string]$AppId, [string]$Scope, [string]$RoleId)
    $rows = @(Invoke-Az role assignment list --assignee $AppId --all -o json)
    $rows | ForEach-Object { Write-Host "  role row: $($_.roleDefinitionName) [$(($_.roleDefinitionId -split '/')[-1])] at $($_.scope)" }
    $ok = $rows.Count -eq 1 -and
          (($rows[0].roleDefinitionId -split '/')[-1] -eq $RoleId) -and
          ($rows[0].scope -ieq $Scope)
    if (-not $ok) {
        Write-Host "K2 FIRED: $($rows.Count) role row(s), expected exactly one ($RoleId at $Scope). Revoking." -ForegroundColor Red
        Revoke-Identity -AppId $AppId
        throw 'Role listing was not exactly one row; the identity has been revoked. STOP and hand to Planner.'
    }
    Write-Host "OK: exactly one role row (Artifact Signing Certificate Profile Signer at the certificate profile)."
}

function Revoke-Identity {
    param([string]$AppId)
    # Step 2 first, while the SP still exists to name: once the app is gone the
    # assignment is an orphan nobody can list by appId.
    $rows = @(& az role assignment list --assignee $AppId --all -o json 2>$null | ConvertFrom-Json)
    foreach ($row in $rows) {
        Invoke-Az role assignment delete --ids $row.id | Out-Null
        Write-Host "Deleted role assignment $($row.id)"
    }
    $app = @(Invoke-Az ad app list --filter "appId eq '$AppId'" -o json)
    if ($app.Count -eq 1) {
        Invoke-Az ad app delete --id $AppId | Out-Null
        Write-Host "Deleted app registration $AppId (its service principal goes with it)."
    }
}

function New-SignerCertificate {
    # TPM first: a key the TPM holds cannot be copied even by an administrator.
    $common = @{
        Subject           = "CN=$DisplayName"
        CertStoreLocation = 'Cert:\CurrentUser\My'
        KeyExportPolicy   = 'NonExportable'
        # No -KeySpec: it is a legacy-CSP field, and both providers below are KSPs —
        # passing it fails CertEnroll with NTE_PROV_TYPE_NOT_DEF (measured).
        KeyAlgorithm      = 'RSA'
        KeyLength         = 2048
        HashAlgorithm     = 'SHA256'
        NotAfter          = (Get-Date).AddMonths($Months)
        KeyUsage          = 'DigitalSignature'
    }
    foreach ($provider in 'Microsoft Platform Crypto Provider', 'Microsoft Software Key Storage Provider') {
        try {
            $cert = New-SelfSignedCertificate @common -Provider $provider
            Write-Host "Certificate $($cert.Thumbprint) created with $provider, NotAfter $($cert.NotAfter.ToUniversalTime().ToString('u'))"
            return $cert
        } catch {
            Write-Host "  $provider unavailable: $($_.Exception.Message)"
        }
    }
    throw 'Could not create a non-exportable certificate with either key provider.'
}

function Add-CertificateCredential {
    param([string]$AppId, $Cert)
    # The PUBLIC half only, and only for as long as az needs to read it.
    $cer = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-signer-$($Cert.Thumbprint).cer"
    try {
        Export-Certificate -Cert $Cert -FilePath $cer -Type CERT | Out-Null
        Invoke-Az ad app credential reset --id $AppId --cert "@$cer" --append -o none | Out-Null
    } finally {
        Remove-Item $cer -ErrorAction SilentlyContinue
    }
}

function Write-Identity {
    param([string]$TenantId, [string]$ClientId, [string]$Thumbprint)
    [ordered]@{ TenantId = $TenantId; ClientId = $ClientId; CertificateThumbprint = $Thumbprint } |
        ConvertTo-Json | Set-Content -Path $IdentityFile -Encoding utf8
    Write-Host "Wrote $IdentityFile"
}

$identity = if (Test-Path $IdentityFile) { Get-Content $IdentityFile -Raw | ConvertFrom-Json } else { $null }

switch ($PSCmdlet.ParameterSetName) {
    'Create' {
        $scope  = Get-ProfileScope
        $roleId = Get-SignerRoleId
        $tenant = (Invoke-Az account show -o json).tenantId

        $apps = @(Invoke-Az ad app list --display-name $DisplayName -o json)
        if ($apps.Count -gt 1) { throw "$($apps.Count) app registrations are named '$DisplayName'; resolve by hand before creating another." }
        if ($identity -and $apps.Count -eq 1 -and $apps[0].appId -eq $identity.ClientId -and
            (Test-Path "Cert:\CurrentUser\My\$($identity.CertificateThumbprint)")) {
            Write-Host "Identity already exists: app $($identity.ClientId), certificate $($identity.CertificateThumbprint)."
            Assert-OneRoleRow -AppId $identity.ClientId -Scope $scope -RoleId $roleId
            return
        }
        if ($apps.Count -eq 1) { throw "An app named '$DisplayName' exists but this checkout has no matching identity file and key. Run -Revoke against $($apps[0].appId) first, or restore the identity file." }

        $cert = New-SignerCertificate
        $app  = Invoke-Az ad app create --display-name $DisplayName --sign-in-audience AzureADMyOrg -o json
        Write-Host "App registration $($app.appId) created."
        $sp   = Invoke-Az ad sp create --id $app.appId -o json
        Write-Host "Service principal $($sp.id) created."
        Add-CertificateCredential -AppId $app.appId -Cert $cert
        # A brand-new SP takes a few seconds to replicate; --assignee-object-id with the
        # principal type skips the Graph lookup that fails during that window.
        Invoke-Az role assignment create --assignee-object-id $sp.id --assignee-principal-type ServicePrincipal `
            --role $roleId --scope $scope -o none | Out-Null
        Write-Identity -TenantId $tenant -ClientId $app.appId -Thumbprint $cert.Thumbprint
        Assert-OneRoleRow -AppId $app.appId -Scope $scope -RoleId $roleId
    }
    'Check' {
        if (-not $identity) { throw "No $IdentityFile — run -Create." }
        Assert-OneRoleRow -AppId $identity.ClientId -Scope (Get-ProfileScope) -RoleId (Get-SignerRoleId)
        . "$PSScriptRoot\signing.ps1"
        $state = Get-EqSignerCertificateState -Thumbprint $identity.CertificateThumbprint
        Write-Host "Certificate: $($state.State) - $($state.Reason)"
    }
    'Rotate' {
        if (-not $identity) { throw "No $IdentityFile — run -Create." }
        $old  = $identity.CertificateThumbprint
        $cert = New-SignerCertificate
        Add-CertificateCredential -AppId $identity.ClientId -Cert $cert
        # Prove the NEW key works before anything names it or the old one goes, so a
        # failed rotation leaves the identity file, the old key and the old credential
        # exactly as they were (DRA-697: the file used to be written first).
        #
        # The proof is a TOKEN, not a signature: Connect-AzAccount with the new
        # certificate succeeding means Entra accepts the key for this SP. The role
        # assignment belongs to the SP, not the key, so a token is what a rotation can
        # change. The first real signature with the new key is the next release's.
        $newIdentity = [pscustomobject]@{
            TenantId = $identity.TenantId; ClientId = $identity.ClientId; CertificateThumbprint = $cert.Thumbprint }
        . "$PSScriptRoot\signing.ps1"
        Import-EqAzAccounts -Repo $Repo
        $ok = $false
        foreach ($i in 1..6) {
            try {
                Connect-EqSigningServicePrincipal -Identity $newIdentity
                $ok = $true; break
            } catch { Start-Sleep -Seconds 10 }
        }
        if (-not $ok) {
            throw @"
The new certificate $($cert.Thumbprint) could not get a token. The identity file, the old key ($old) and its credential are untouched, so releases still sign with the old one.
Left behind: the new key credential appended to app $($identity.ClientId), and the new local key Cert:\CurrentUser\My\$($cert.Thumbprint). Neither is named by anything; remove them before retrying.
"@
        }
        Write-Identity -TenantId $identity.TenantId -ClientId $identity.ClientId -Thumbprint $cert.Thumbprint

        $creds = @(Invoke-Az ad app credential list --id $identity.ClientId --cert -o json)
        $match = @(Select-EqKeyCredential -Credentials $creds -Thumbprint $old)
        if ($match.Count -ne 1) {
            # The customKeyIdentifier form was never measured (DRA-697), so a zero here
            # is most likely a format miss — and silently keeping a credential the old
            # key can still use is the failure. Keep the old local key too: the app
            # still trusts it, and deleting it would hide that, not fix it.
            Write-Host "WARN: expected exactly ONE key credential for the old thumbprint $old, found $($match.Count). Removed NOTHING." -ForegroundColor Red
            Write-Host "  The new key is proven and the identity file names it; the OLD key still works against the app." -ForegroundColor Red
            Write-Host "  customKeyIdentifier values az returned (record this on the rotation card):" -ForegroundColor Red
            $creds | ForEach-Object { Write-Host "    keyId $($_.keyId)  customKeyIdentifier '$($_.customKeyIdentifier)'  end $($_.endDateTime)" -ForegroundColor Red }
            Write-Host "  Remove the old credential by keyId (az ad app credential delete --id $($identity.ClientId) --key-id <keyId> --cert), then the old local key Cert:\CurrentUser\My\$old." -ForegroundColor Red
            exit 2
        }
        Invoke-Az ad app credential delete --id $identity.ClientId --key-id $match[0].keyId --cert | Out-Null
        Write-Host "Removed the old key credential ($old, keyId $($match[0].keyId)) from the app."
        if (Test-Path "Cert:\CurrentUser\My\$old") { Remove-Item "Cert:\CurrentUser\My\$old" -DeleteKey; Write-Host "Removed the old local key $old." }
    }
    'Revoke' {
        if (-not $identity) { throw "No $IdentityFile — revoke by hand: Entra ID -> App registrations -> '$DisplayName' -> Delete." }
        Revoke-Identity -AppId $identity.ClientId
        if ($RemoveKey -and (Test-Path "Cert:\CurrentUser\My\$($identity.CertificateThumbprint)")) {
            Remove-Item "Cert:\CurrentUser\My\$($identity.CertificateThumbprint)" -DeleteKey
            Remove-Item $IdentityFile
            Write-Host 'Removed the local key and the identity file.'
        }
    }
}
