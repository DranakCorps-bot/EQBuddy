# signing.ps1's identity resolver (DRA-679 D1), offline: no Azure call, no signtool.
#
# One battery of assertions, run against the shipped functions (must be all green)
# and then against each MUTANT of them (each must turn at least one row red) — a
# battery that a broken resolver also passes is not a test (trap 34 / trap 78).
#
# The mutants are the three ways this could quietly stop protecting a release:
#   - a resolver that answers $null / "SkipSign" instead of throwing when nothing can sign
#   - an expiry check that is ignored, so an expired certificate is "usable"
#   - an exclude list left empty, so any credential on the machine can answer
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\signing.ps1"

$now   = [datetime]'2026-10-01T12:00:00'
$id    = [pscustomobject]@{ TenantId = 't'; ClientId = 'b34a21ff-0000'; CertificateThumbprint = 'AB' }
$cli   = [pscustomobject]@{ user = [pscustomobject]@{ name = 'founder@example.com' } }

function New-TestCert([datetime]$NotAfter, [bool]$WithKey = $true) {
    # A real X509Certificate2, made in memory and never stored.
    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    $req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=signing-selftest', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $cert = $req.CreateSelfSigned($NotAfter.AddYears(-1), $NotAfter)
    if ($WithKey) { return $cert }
    return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export('Cert'))
}

$certs = @{
    Good     = New-TestCert $now.AddMonths(12)
    Soon     = New-TestCert $now.AddDays(10)
    Expired  = New-TestCert $now.AddDays(-1)
    NoKey    = New-TestCert $now.AddMonths(12) $false
}

function Invoke-Battery {
    $fails = [System.Collections.Generic.List[string]]::new()
    function Row([string]$name, [scriptblock]$test) {
        $ok = $false
        try { $ok = [bool](& $test) } catch { $ok = $false }
        if (-not $ok) { $fails.Add($name) }
    }
    function Throws([scriptblock]$body) { try { $null = & $body; return $false } catch { return $true } }
    $state = { param($c) Get-EqSignerCertificateState -Certificate $c -Now $now }

    Row 'a valid certificate is Usable'              { (& $state $certs.Good).State -eq 'Usable' }
    Row 'under 30 days is ExpiringSoon'              { (& $state $certs.Soon).State -eq 'ExpiringSoon' }
    Row 'an expired certificate is Expired'          { (& $state $certs.Expired).State -eq 'Expired' }
    Row 'no private key is NoPrivateKey'             { (& $state $certs.NoKey).State -eq 'NoPrivateKey' }
    Row 'no certificate is Missing'                  { (Get-EqSignerCertificateState -Thumbprint ('0' * 40) -Now $now).State -eq 'Missing' }

    Row 'identity + valid cert -> ServicePrincipal'  {
        $r = Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Good) -CliAccount $cli
        $r.Kind -eq 'ServicePrincipal' -and $r.Credential -eq 'AzurePowerShellCredential' -and -not $r.Warning }
    Row 'expiring cert still signs, and WARNs'       {
        $r = Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Soon) -CliAccount $null
        $r.Kind -eq 'ServicePrincipal' -and $r.Warning -match 'expires' }
    Row 'expired cert -> AzureCli fallback'          {
        $r = Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Expired) -CliAccount $cli
        $r.Kind -eq 'AzureCli' -and $r.Credential -eq 'AzureCliCredential' -and $r.SkippedBecause -match 'expired' }
    Row 'no private key -> AzureCli fallback'        {
        (Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.NoKey) -CliAccount $cli).Kind -eq 'AzureCli' }
    Row 'no identity file -> AzureCli fallback'      {
        (Resolve-EqSigningIdentity -Identity $null -CertState $null -CliAccount $cli).Kind -eq 'AzureCli' }
    Row 'SP refused at sign-in -> AzureCli fallback' {
        $r = Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Good) -SpFailure 'AADSTS700027' -CliAccount $cli
        $r.Kind -eq 'AzureCli' -and $r.SkippedBecause -match 'AADSTS700027' }
    Row 'neither -> THROWS (never unsigned)'         {
        Throws { Resolve-EqSigningIdentity -Identity $null -CertState $null -CliAccount $null } }
    Row 'expired cert + no session -> THROWS'        {
        Throws { Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Expired) -CliAccount $null } }
    Row 'SP refused + no session -> THROWS'          {
        Throws { Resolve-EqSigningIdentity -Identity $id -CertState (& $state $certs.Good) -SpFailure 'x' -CliAccount $null } }

    foreach ($cred in 'AzurePowerShellCredential', 'AzureCliCredential') {
        $other = if ($cred -eq 'AzureCliCredential') { 'AzurePowerShellCredential' } else { 'AzureCliCredential' }
        Row "excludes for $cred leave only it"       {
            $ex = @(Get-EqExcludedCredentials -Credential $cred)
            $ex.Count -eq ($script:AllAzureCredentials.Count - 1) -and $ex -notcontains $cred -and $ex -contains $other }
    }
    Row 'per-run metadata carries the excludes'      {
        $src = Join-Path ([IO.Path]::GetTempPath()) "signing-selftest-$([guid]::NewGuid().ToString('N')).json"
        '{"Endpoint":"https://x","CodeSigningAccountName":"a","CertificateProfileName":"p"}' | Set-Content $src
        try {
            $p = New-EqSigningMetadata -Source $src -Credential 'AzurePowerShellCredential'
            $m = Get-Content $p -Raw | ConvertFrom-Json
            Remove-Item $p
            $m.CertificateProfileName -eq 'p' -and @($m.ExcludeCredentials) -contains 'AzureCliCredential' -and
                @($m.ExcludeCredentials) -notcontains 'AzurePowerShellCredential' -and @($m.ExcludeCredentials).Count -ge 9
        } finally { Remove-Item $src -ErrorAction SilentlyContinue }
    }
    Row 'an unknown credential name is refused'      { Throws { Get-EqExcludedCredentials -Credential 'SkipSign' } }
    return , $fails
}

$failed = 0
$real = Invoke-Battery
if ($real.Count) {
    $real | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    $failed++
} else {
    Write-Host 'signing selftest: every row green against the shipped resolver'
}

# Each mutant is a TEXT EDIT of one shipped function — the real body with exactly one
# thing broken — installed for the length of one battery and then restored. A mutant
# whose edit matches nothing is refused (it would be aimed at nothing, trap 78), and one
# that leaves every row green is a hole in the battery.
$mutants = @(
    @{ Label = 'resolver answers $null when nothing can sign'; Name = 'Resolve-EqSigningIdentity'
       From = 'throw @"';                                   To = 'return $null; @"' }
    @{ Label = 'resolver answers SkipSign when nothing can sign'; Name = 'Resolve-EqSigningIdentity'
       From = 'throw @"';                                   To = 'return [pscustomobject]@{ Kind = ''SkipSign'' }; @"' }
    @{ Label = 'expiry check ignored'; Name = 'Get-EqSignerCertificateState'
       From = 'if ($notAfter -le $Now)';                    To = 'if ($false)' }
    @{ Label = 'exclude list left empty'; Name = 'Get-EqExcludedCredentials'
       From = 'return @($script:AllAzureCredentials | Where-Object { $_ -ne $Credential })'; To = 'return @()' }
)

foreach ($m in $mutants) {
    $original = (Get-Item "function:$($m.Name)").ScriptBlock
    $text = $original.ToString()
    if (-not $text.Contains($m.From)) {
        Write-Host "FAIL: mutant '$($m.Label)' matches nothing in $($m.Name) — it tests nothing" -ForegroundColor Red
        $failed++; continue
    }
    Set-Item "function:$($m.Name)" ([scriptblock]::Create($text.Replace($m.From, $m.To)))
    try { $red = Invoke-Battery } finally { Set-Item "function:$($m.Name)" $original }
    if ($red.Count) {
        Write-Host "  mutant '$($m.Label)' reddens: $($red -join '; ')"
    } else {
        Write-Host "FAIL: mutant '$($m.Label)' left every row green" -ForegroundColor Red
        $failed++
    }
}

if ($failed) { exit 1 }
Write-Host 'signing selftest: every mutant reddens the battery'
