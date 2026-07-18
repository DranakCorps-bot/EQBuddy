# One-time: create the self-signed code-signing certificate release.ps1 uses.
# Self-signed = consistent publisher identity on the binaries; it does NOT buy
# SmartScreen reputation (that requires a paid OV/EV certificate).
$existing = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -like '*EQBuddy*' }
if ($existing) { Write-Host "Cert already exists: $($existing.Subject)"; return }

$cert = New-SelfSignedCertificate -Type CodeSigningCert `
    -Subject 'CN=David Edwards - EQBuddy' `
    -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears(10)
Write-Host "Created $($cert.Subject) (thumbprint $($cert.Thumbprint))"

# Export the public part so family PCs can optionally trust it once
# (import into "Trusted People" + "Trusted Root" to remove unknown-publisher prompts).
$repo = Split-Path $PSScriptRoot -Parent
Export-Certificate -Cert $cert -FilePath "$repo\dist\EQBuddy-publisher.cer" | Out-Null
Write-Host "Public cert exported to dist\EQBuddy-publisher.cer"
