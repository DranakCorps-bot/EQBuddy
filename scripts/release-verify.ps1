<#
.SYNOPSIS
    After a release: prove it shipped, row by row, instead of trusting release.ps1's exit code.

.DESCRIPTION
    DRA-675 D1 (docs/plans/DRA-675.md §3.1). release.ps1 pushes the same artifacts down three
    channels — the OneDrive folder every family widget polls, the git tag, the GitHub release —
    and "a silent failure is not proof nothing happened" (CLAUDE.md, Commands). This script is
    the observation that replaces the exit code. Each assertion prints as its own row:

      tag        the tag exists on origin and points at -Commit (the reviewed commit the go named)
      release    `gh release view` finds it, it is not a draft, and it carries the four assets
      onedrive   each artifact is in the OneDrive folder, written at or after -Since
      sha256     OneDrive == GitHub asset digest == dist\ == the published .sha256 sidecar
      signature  every shipped executable is Authenticode Valid, TIMESTAMPED, and signed by
                 -ExpectedSigner — the setup exe and the EQBuddy.exe inside the portable zip,
                 from OneDrive (what players get) and from dist\ (what was built)

    Exit 0 only when every row is OK; exit 1 on any [FAIL]. A failed release is a HARD STOP for
    the release seat (docs/ops/release-seat.md): this script tells you what DID happen, and it
    never re-runs anything. It writes nothing except temp files (the zip extraction and the
    downloaded sidecars), removed on exit, and — when -Since is omitted — a fetch of the one tag
    ref into this clone so the tagged commit's date can be read.

    Every git and gh call names this script's repo (`git -C`, `gh --repo`), so the result does
    not depend on the directory it is run from.

    -SelfTest is offline: every row is driven red by a named mutant over synthetic observations,
    and the hash and signature readers are driven against real files (an unsigned temp file, and
    PowerShell's own Authenticode-signed, timestamped pwsh.dll). The tag reader runs against real
    temp git remotes — one holding the tag, one without it, and one that cannot answer — because
    "could not ask origin" and "not on origin" must never print the same row (DRA-699). Wired
    into check.ps1 and CI.

.EXAMPLE
    pwsh -NoProfile -File scripts/release-verify.ps1 -Tag v2.0.3 -Commit <reviewed-sha> -Since 2026-10-02T14:00:00Z

.EXAMPLE
    pwsh -NoProfile -File scripts/release-verify.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$Tag,
    # The commit the Founder's go named (the latest Reviewer PASS). Full or abbreviated SHA.
    [string]$Commit,
    # When the release run began. Absent: the tagged commit's committer date, the earliest
    # moment a build of that commit could exist — weaker, and the row says which bound it used.
    [Nullable[datetime]]$Since,
    # The dist\ folder release.ps1 built into. Default: this checkout's.
    [string]$Dist,
    [string]$OneDrive = 'C:\Users\david\OneDrive\EQBuddyDownload',
    [string]$ExpectedSigner = 'CN=FlossworksCross-Stitch',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent

# What release.ps1 publishes, per channel — read off the script, not assumed symmetric. The
# GitHub release carries all four (release.ps1's $ghArgs); OneDrive gets three (its
# Copy-Item: the installer, the installer's sidecar the updater checks (UPDATE-003), and the
# zip — NOT the zip's sidecar). The first live run of this script assumed four on OneDrive
# and reddened v2.0.2 for a file release.ps1 never copies. If release.ps1 changes what it
# copies, change these two lines with it.
$script:Artifacts = @('EQBuddyEvolvedSetup.exe', 'EQBuddyEvolved-portable.zip')
$script:Assets = @($script:Artifacts | ForEach-Object { $_; "$_.sha256" })
$script:OneDriveFiles = @('EQBuddyEvolvedSetup.exe', 'EQBuddyEvolvedSetup.exe.sha256', 'EQBuddyEvolved-portable.zip')

function New-Row([string]$Check, [bool]$Ok, [string]$Detail) {
    [pscustomobject]@{ Check = $Check; Ok = $Ok; Detail = $Detail }
}

# ---- the checks: pure functions over observations, so -SelfTest can drive each one -----

# An ASK is { Asked; Exit; Why } plus what it read. Asked = the remote answered; an unanswered
# read is never reported as an absence (DRA-699, trap 81's shape): "could not ask origin" and
# "the tag is not on origin" are different facts, and only the second is evidence about a release.
function New-TagRead([bool]$asked, [int]$exit, $commit) {
    [pscustomobject]@{ Asked = $asked; Exit = $exit; Commit = $commit }
}

function New-ReleaseRead([bool]$asked, [int]$exit, $release, [string]$why) {
    [pscustomobject]@{ Asked = $asked; Exit = $exit; Release = $release; Why = $why }
}

function Test-TagRow($tag, $read, $expectedCommit) {
    if (-not $read.Asked) { return New-Row 'tag' $false "could not ask origin (exit $($read.Exit)) - says NOTHING about whether $tag exists" }
    $tagCommit = $read.Commit
    if (-not $tagCommit) { return New-Row 'tag' $false "$tag is not on origin" }
    if (-not $expectedCommit) { return New-Row 'tag' $false "$tag -> $tagCommit, but no -Commit was given to compare it with (the go names one)" }
    $ok = $tagCommit.StartsWith($expectedCommit, [StringComparison]::OrdinalIgnoreCase)
    $why = if ($ok) { 'the reviewed commit' } else { "NOT the reviewed commit $expectedCommit" }
    New-Row 'tag' $ok "$tag -> $tagCommit ($why)"
}

function Test-ReleaseRow($tag, $read, [string[]]$expected) {
    if (-not $read.Asked) { return New-Row 'release' $false "could not ask GitHub (exit $($read.Exit): $($read.Why)) - says NOTHING about whether $tag is published" }
    $release = $read.Release
    if (-not $release) { return New-Row 'release' $false "no GitHub release for $tag" }
    if ($release.isDraft) { return New-Row 'release' $false "$tag is a DRAFT (players cannot see it)" }
    $names = @($release.assets | ForEach-Object { $_.name })
    $missing = @($expected | Where-Object { $_ -notin $names })
    if ($missing.Count) { return New-Row 'release' $false "$tag is missing asset(s): $($missing -join ', ')" }
    New-Row 'release' $true "$tag published, not a draft, $($expected.Count) assets present"
}

# $noBound: why there is no $since, when there is none. The default is the honest one only
# when nothing went unanswered; a bound that could not be derived says what was not answered.
function Test-OneDriveRow([string]$name, $file, $since, [string]$sinceSource, [string]$noBound = 'there is no time bound to call it fresh against') {
    if (-not $file) { return New-Row "onedrive  $name" $false 'not in the OneDrive folder' }
    if (-not $since) { return New-Row "onedrive  $name" $false "present (written $($file.LastWriteTimeUtc.ToString('u'))), but $noBound" }
    $ok = $file.LastWriteTimeUtc -ge $since.ToUniversalTime()
    $rel = if ($ok) { 'at/after' } else { 'BEFORE' }
    New-Row "onedrive  $name" $ok "written $($file.LastWriteTimeUtc.ToString('u')), $rel $($since.ToUniversalTime().ToString('u')) ($sinceSource)"
}

# The OneDrive rows' time bound: -Since when given, else the tagged commit's date, read by
# $dateTagged (tag, commit -> { Iso; FetchExit }). It is NEVER derived off an UNANSWERED tag
# read — that turned one transient remote failure into four red rows that all looked like a
# missing release (DRA-699) — and $dateTagged is not even called then.
function Get-FreshnessBound($given, [string]$tag, $tagRead, [scriptblock]$dateTagged) {
    $out = [pscustomobject]@{ Since = $given; Source = 'given'; NoBound = 'there is no time bound to call it fresh against' }
    if ($given) { return $out }
    if (-not $tagRead.Asked) {
        $out.NoBound = "the time bound is UNKNOWN: origin could not be asked for $tag (exit $($tagRead.Exit)), so there is no tagged commit to date. Pass -Since, or re-run release-verify (never release.ps1)"
        return $out
    }
    if (-not $tagRead.Commit) { return $out }
    $d = & $dateTagged $tag $tagRead.Commit
    if ($d.Iso) {
        $out.Since = [datetimeoffset]::Parse("$($d.Iso)".Trim()).UtcDateTime
        $out.Source = 'the tagged commit''s date; pass -Since for the run''s start'
    }
    else { $out.NoBound = "the time bound is UNKNOWN: the tagged commit $($tagRead.Commit) could not be read in this clone (fetch exit $($d.FetchExit)). Pass -Since" }
    $out
}

# $hashes: ordered map of source -> sha256 (or $null when that source could not be read).
function Test-ShaRow([string]$name, $hashes) {
    $absent = @($hashes.Keys | Where-Object { -not $hashes[$_] })
    if ($absent.Count) { return New-Row "sha256    $name" $false "no hash from: $($absent -join ', ')" }
    $distinct = @($hashes.Values | ForEach-Object { $_.ToLowerInvariant() } | Select-Object -Unique)
    if ($distinct.Count -ne 1) {
        $pairs = ($hashes.Keys | ForEach-Object { "$_=$($hashes[$_].Substring(0, 12))" }) -join ' '
        return New-Row "sha256    $name" $false "MISMATCH: $pairs"
    }
    New-Row "sha256    $name" $true "$($distinct[0].Substring(0, 16))... equal across $(@($hashes.Keys) -join ', ')"
}

# $sig: { Status; SignatureType; HasTimestamp; Signer } — or $null when the file was not there.
function Test-SignatureRow([string]$label, $sig, [string]$expectedSigner) {
    $check = "signature $label"
    if (-not $sig) { return New-Row $check $false 'file not found' }
    if ("$($sig.Status)" -ne 'Valid') { return New-Row $check $false "status $($sig.Status), not Valid" }
    if ("$($sig.SignatureType)" -ne 'Authenticode') { return New-Row $check $false "a $($sig.SignatureType) signature, not an embedded Authenticode one" }
    if (-not $sig.HasTimestamp) { return New-Row $check $false 'Valid but NOT timestamped: it dies with the 3-day certificate' }
    if ($sig.Signer -ne $expectedSigner) { return New-Row $check $false "signed by '$($sig.Signer)', not '$expectedSigner'" }
    New-Row $check $true "Valid, Authenticode, timestamped, $($sig.Signer)"
}

# ---- the readers: the only code here that touches the world -----------------------------

# ls-remote asks the REMOTE, not this checkout's tag list. An annotated tag lists a peeled
# ^{} line pointing at the commit; a lightweight one points at it directly. A clean exit with
# no matching line is "not there"; a non-zero exit is "not asked" (DRA-699: one transient
# failure here used to read as a missing tag and take the freshness bound down with it).
function Read-OriginTag([string]$repoDir, [string]$remote, [string]$tag) {
    $ls = @(git -C $repoDir ls-remote --tags $remote "refs/tags/$tag" "refs/tags/$tag^{}" 2>$null)
    $exit = $LASTEXITCODE
    if ($exit -ne 0) { return New-TagRead $false $exit $null }
    $peeled = $ls | Where-Object { $_ -match '\^\{\}$' } | Select-Object -First 1
    $line = if ($peeled) { $peeled } else { $ls | Select-Object -First 1 }
    New-TagRead $true 0 $(if ($line) { ($line -split '\s+')[0] } else { $null })
}

# gh exits 1 for a missing release AND for an unreachable host, a wrong repo or a lapsed login,
# so the exit code alone cannot tell them apart — the words can. Only gh's own "release not
# found" is an answer; anything else non-zero is "could not ask GitHub" (measured 2026-10-01:
# missing tag -> "release not found"; bad host -> "error connecting to ..."; bad repo ->
# "GraphQL: Could not resolve to a Repository ...", all exit 1).
function Resolve-GhRelease([int]$exit, $out) {
    $err = @($out | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    $std = (@($out | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n").Trim()
    if ($exit -eq 0) {
        try { if ($std) { return New-ReleaseRead $true 0 ($std | ConvertFrom-Json) '' } } catch { }
        return New-ReleaseRead $false 0 $null 'gh answered, but not with JSON this script can read'
    }
    if ($err -match '^release not found') { return New-ReleaseRead $true $exit $null '' }
    $why = if ($err.Count) { $err[0] } else { 'no message' }
    New-ReleaseRead $false $exit $null $why
}

function Read-GhRelease([string]$tag, [string]$ghRepo) {
    try { $out = @(gh release view $tag --repo $ghRepo --json isDraft,tagName,assets 2>&1); $exit = $LASTEXITCODE }
    catch { return New-ReleaseRead $false -1 $null "gh did not run: $($_.Exception.Message)" }
    Resolve-GhRelease $exit $out
}

function Get-Sha([string]$path) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) { return $null }
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Sidecar([string]$path) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) { return $null }
    $text = (Get-Content -LiteralPath $path -Raw).Trim()
    if ($text -match '^[0-9a-fA-F]{64}') { return $Matches[0].ToLowerInvariant() }
    $null
}

function Get-Sig([string]$path) {
    if (-not $path -or -not (Test-Path -LiteralPath $path)) { return $null }
    $s = Get-AuthenticodeSignature -LiteralPath $path
    [pscustomobject]@{
        Status        = "$($s.Status)"
        SignatureType = "$($s.SignatureType)"
        HasTimestamp  = [bool]$s.TimeStamperCertificate
        Signer        = if ($s.SignerCertificate) { ($s.SignerCertificate.Subject -split ',')[0].Trim() } else { $null }
    }
}

function Write-Rows($rows) {
    foreach ($r in $rows) {
        if ($r.Ok) { Write-Host "[ OK ] $($r.Check): $($r.Detail)" -ForegroundColor Green }
        else { Write-Host "[FAIL] $($r.Check): $($r.Detail)" -ForegroundColor Red }
    }
}

# ---- -SelfTest ---------------------------------------------------------------------------

if ($SelfTest) {
    $fail = 0
    function Expect([string]$name, $row, [bool]$wantOk) {
        if ($row.Ok -eq $wantOk) { Write-Host "  ok   $name" }
        else {
            Write-Host "  FAIL: $name -> expected $(if ($wantOk) {'OK'} else {'red'}), got $(if ($row.Ok) {'OK'} else {'red'}): $($row.Detail)" -ForegroundColor Red
            $script:fail++
        }
    }
    # A red row is not enough where two reds mean different things: assert what it SAYS.
    function ExpectSays([string]$name, $row, [string]$pattern) {
        if (-not $row.Ok -and $row.Detail -match $pattern) { Write-Host "  ok   $name" }
        else {
            Write-Host "  FAIL: $name -> expected a red row matching /$pattern/, got $(if ($row.Ok) {'OK'} else {'red'}): $($row.Detail)" -ForegroundColor Red
            $script:fail++
        }
    }

    $sha = 'aa' * 32
    # UTC kind, like a real LastWriteTimeUtc: DateTime comparison ignores Kind, so a Local
    # fixture would compare wall-clock numbers and redden the green baseline (it did).
    $since = ([datetime]'2026-10-01T12:00:00Z').ToUniversalTime()
    $good = [pscustomobject]@{ Status = 'Valid'; SignatureType = 'Authenticode'; HasTimestamp = $true; Signer = 'CN=FlossworksCross-Stitch' }
    $release = [pscustomobject]@{ isDraft = $false; assets = @($script:Assets | ForEach-Object { [pscustomobject]@{ name = $_ } }) }

    Write-Host 'release-verify selftest: the green baseline (a check that cannot pass is not a check)'
    Expect 'tag on origin at the reviewed commit'      (Test-TagRow 'v9.9.9' (New-TagRead $true 0 'f9e266a6abc') 'f9e266a6') $true
    Expect 'release published with four assets'        (Test-ReleaseRow 'v9.9.9' (New-ReleaseRead $true 0 $release '') $script:Assets) $true
    Expect 'OneDrive file written after the run began' (Test-OneDriveRow 'x' ([pscustomobject]@{ LastWriteTimeUtc = $since.AddMinutes(5) }) $since 'given') $true
    Expect 'four equal hashes'                         (Test-ShaRow 'x' ([ordered]@{ onedrive = $sha; github = $sha; dist = $sha; sidecar = $sha.ToUpperInvariant() })) $true
    Expect 'Valid, timestamped, right signer'          (Test-SignatureRow 'x' $good 'CN=FlossworksCross-Stitch') $true

    Write-Host 'release-verify selftest: every mutant reddens its row'
    ExpectSays 'MUTANT missing tag'                    (Test-TagRow 'v9.9.9' (New-TagRead $true 0 $null) 'f9e266a6') 'is not on origin'
    ExpectSays 'MUTANT origin not asked'               (Test-TagRow 'v9.9.9' (New-TagRead $false 128 $null) 'f9e266a6') '^could not ask origin \(exit 128\)'
    Expect 'MUTANT tag at another commit'              (Test-TagRow 'v9.9.9' (New-TagRead $true 0 '0123456789ab') 'f9e266a6') $false
    Expect 'MUTANT no -Commit to compare with'         (Test-TagRow 'v9.9.9' (New-TagRead $true 0 'f9e266a6abc') $null) $false
    ExpectSays 'MUTANT no GitHub release'              (Test-ReleaseRow 'v9.9.9' (New-ReleaseRead $true 1 $null '') $script:Assets) '^no GitHub release'
    ExpectSays 'MUTANT GitHub not asked'               (Test-ReleaseRow 'v9.9.9' (New-ReleaseRead $false 1 $null 'error connecting to api.github.com') $script:Assets) '^could not ask GitHub \(exit 1'
    Expect 'MUTANT draft release'                      (Test-ReleaseRow 'v9.9.9' (New-ReleaseRead $true 0 ([pscustomobject]@{ isDraft = $true; assets = $release.assets }) '') $script:Assets) $false
    Expect 'MUTANT asset missing from the release'     (Test-ReleaseRow 'v9.9.9' (New-ReleaseRead $true 0 ([pscustomobject]@{ isDraft = $false; assets = @($release.assets | Select-Object -Skip 1) }) '') $script:Assets) $false
    Expect 'MUTANT OneDrive file absent'               (Test-OneDriveRow 'x' $null $since 'given') $false
    Expect 'MUTANT OneDrive file stale'                (Test-OneDriveRow 'x' ([pscustomobject]@{ LastWriteTimeUtc = $since.AddDays(-1) }) $since 'given') $false
    Expect 'MUTANT no freshness bound'                 (Test-OneDriveRow 'x' ([pscustomobject]@{ LastWriteTimeUtc = $since }) $null 'none') $false
    $script:dated = 0
    $dater = { param($t, $c) $script:dated++; [pscustomobject]@{ Iso = '2026-10-01T11:00:00+00:00'; FetchExit = 0 } }
    $odFile = [pscustomobject]@{ LastWriteTimeUtc = $since }
    $b = Get-FreshnessBound $null 'v9.9.9' (New-TagRead $true 0 'f9e266a6abc') $dater
    Expect 'bound from an ANSWERED tag read is its date' (Test-OneDriveRow 'x' $odFile $b.Since $b.Source $b.NoBound) $true
    $script:dated = 0
    $b = Get-FreshnessBound $null 'v9.9.9' (New-TagRead $false 128 $null) $dater
    ExpectSays 'MUTANT bound off an UNASKED tag read says origin was not asked' (Test-OneDriveRow 'x' $odFile $b.Since $b.Source $b.NoBound) 'UNKNOWN: origin could not be asked for v9\.9\.9 \(exit 128\)'
    Expect 'an UNASKED tag read never reaches the dater' ([pscustomobject]@{ Ok = ($script:dated -eq 0); Detail = "dater called $($script:dated) time(s)" }) $true
    Expect 'MUTANT sha mismatch (GitHub differs)'      (Test-ShaRow 'x' ([ordered]@{ onedrive = $sha; github = 'bb' * 32; dist = $sha; sidecar = $sha })) $false
    Expect 'MUTANT sha mismatch (sidecar differs)'     (Test-ShaRow 'x' ([ordered]@{ onedrive = $sha; github = $sha; dist = $sha; sidecar = 'cc' * 32 })) $false
    Expect 'MUTANT sha unreadable (dist absent)'       (Test-ShaRow 'x' ([ordered]@{ onedrive = $sha; github = $sha; dist = $null; sidecar = $sha })) $false
    Expect 'MUTANT unsigned'                           (Test-SignatureRow 'x' ([pscustomobject]@{ Status = 'NotSigned'; SignatureType = 'None'; HasTimestamp = $false; Signer = $null }) 'CN=FlossworksCross-Stitch') $false
    Expect 'MUTANT signature does not verify'          (Test-SignatureRow 'x' ([pscustomobject]@{ Status = 'HashMismatch'; SignatureType = 'Authenticode'; HasTimestamp = $true; Signer = 'CN=FlossworksCross-Stitch' }) 'CN=FlossworksCross-Stitch') $false
    Expect 'MUTANT untimestamped'                      (Test-SignatureRow 'x' ([pscustomobject]@{ Status = 'Valid'; SignatureType = 'Authenticode'; HasTimestamp = $false; Signer = 'CN=FlossworksCross-Stitch' }) 'CN=FlossworksCross-Stitch') $false
    Expect 'MUTANT catalog, not embedded'              (Test-SignatureRow 'x' ([pscustomobject]@{ Status = 'Valid'; SignatureType = 'Catalog'; HasTimestamp = $true; Signer = 'CN=FlossworksCross-Stitch' }) 'CN=FlossworksCross-Stitch') $false
    Expect 'MUTANT wrong signer'                       (Test-SignatureRow 'x' ([pscustomobject]@{ Status = 'Valid'; SignatureType = 'Authenticode'; HasTimestamp = $true; Signer = 'CN=Somebody Else' }) 'CN=FlossworksCross-Stitch') $false
    Expect 'MUTANT signed file absent'                 (Test-SignatureRow 'x' $null 'CN=FlossworksCross-Stitch') $false

    Write-Host 'release-verify selftest: gh''s words, as measured (exit 1 covers four different worlds)'
    $ghErr = { param($m) [System.Management.Automation.ErrorRecord]::new([Exception]::new($m), 'gh', 'NotSpecified', $null) }
    ExpectSays 'gh "release not found" is an ANSWER'   (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 1 @(& $ghErr 'release not found')) $script:Assets) '^no GitHub release'
    ExpectSays 'gh "error connecting" is NOT asked'    (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 1 @((& $ghErr 'error connecting to api.github.com'), (& $ghErr 'check your internet connection'))) $script:Assets) '^could not ask GitHub \(exit 1: error connecting'
    ExpectSays 'gh unknown repo is NOT asked'          (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 1 @(& $ghErr "GraphQL: Could not resolve to a Repository with the name 'a/b'. (repository)")) $script:Assets) '^could not ask GitHub'
    ExpectSays 'gh silent failure is NOT asked'        (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 4 @()) $script:Assets) '^could not ask GitHub \(exit 4'
    Expect 'gh JSON answer parses to the release'      (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 0 @(($release | ConvertTo-Json -Depth 4))) $script:Assets) $true
    ExpectSays 'gh exit 0 without JSON is NOT asked'   (Test-ReleaseRow 'v9.9.9' (Resolve-GhRelease 0 @('not json')) $script:Assets) '^could not ask GitHub \(exit 0'

    Write-Host 'release-verify selftest: the tag reader, against real git remotes'
    $gtmp = Join-Path ([IO.Path]::GetTempPath()) ("release-verify-git-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $gtmp | Out-Null
    try {
        $src = Join-Path $gtmp 'src'; $empty = Join-Path $gtmp 'empty.git'
        git init -q $src 2>$null | Out-Null
        git -C $src -c user.name=selftest -c user.email=selftest@invalid commit -q --allow-empty -m one 2>$null | Out-Null
        git -C $src -c user.name=selftest -c user.email=selftest@invalid tag -a v9.9.9 -m v9.9.9 2>$null | Out-Null
        $head = (git -C $src rev-parse HEAD).Trim()
        git init -q --bare $empty 2>$null | Out-Null
        # An origin that cannot answer: a path that is not a repository. Offline and instant,
        # and git exits 128 exactly as it does for a dropped connection.
        $gone = 'file:///' + ((Join-Path $gtmp 'no-such-remote') -replace '\\', '/')
        Expect 'a real remote holding the tag answers the peeled commit' (Test-TagRow 'v9.9.9' (Read-OriginTag $src $src 'v9.9.9') $head) $true
        ExpectSays 'a real remote WITHOUT the tag says "not on origin"'  (Test-TagRow 'v9.9.9' (Read-OriginTag $src $empty 'v9.9.9') $head) 'is not on origin'
        ExpectSays 'an UNREACHABLE remote says "could not ask origin"'   (Test-TagRow 'v9.9.9' (Read-OriginTag $src $gone 'v9.9.9') $head) '^could not ask origin \(exit [1-9]'
    }
    finally { Remove-Item -LiteralPath $gtmp -Recurse -Force -ErrorAction SilentlyContinue }

    Write-Host 'release-verify selftest: the readers, against real files'
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("release-verify-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        $a = Join-Path $tmp 'a.exe'; $b = Join-Path $tmp 'b.exe'; $side = Join-Path $tmp 'a.exe.sha256'
        [IO.File]::WriteAllBytes($a, [byte[]](1..64)); [IO.File]::WriteAllBytes($b, [byte[]](2..65))
        Set-Content -LiteralPath $side -Value (Get-Sha $a).ToUpperInvariant() -NoNewline
        Expect 'real hashes of one file and its sidecar agree' (Test-ShaRow 'a' ([ordered]@{ one = (Get-Sha $a); sidecar = (Get-Sidecar $side) })) $true
        Expect 'real hashes of two different files disagree'   (Test-ShaRow 'a' ([ordered]@{ one = (Get-Sha $a); two = (Get-Sha $b) })) $false
        Expect 'a real unsigned file is refused'                (Test-SignatureRow 'a' (Get-Sig $a) 'CN=FlossworksCross-Stitch') $false
        Expect 'a missing file reads as no signature'           (Test-SignatureRow 'a' (Get-Sig (Join-Path $tmp 'absent.exe')) 'CN=FlossworksCross-Stitch') $false
        # pwsh.dll ships Authenticode-signed and timestamped (measured on the desk and the
        # runner image alike). Its signer is Microsoft, so it passes only when told to expect
        # that — which proves the green path is reachable through the REAL reader, and the
        # default signer refuses it on the same file.
        $real = Get-Sig (Join-Path $PSHOME 'pwsh.dll')
        Expect 'a real signed + timestamped file passes'        (Test-SignatureRow 'pwsh.dll' $real $real.Signer) $true
        Expect "the same file is refused for not being $ExpectedSigner" (Test-SignatureRow 'pwsh.dll' $real $ExpectedSigner) $false
    }
    finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }

    if ($fail) { Write-Host "release-verify selftest: FAIL: $fail check(s)" -ForegroundColor Red; exit 1 }
    Write-Host 'release-verify selftest: all checks passed' -ForegroundColor Green
    exit 0
}

# ---- the live run ------------------------------------------------------------------------

if (-not $Tag) { throw 'release-verify needs -Tag vX.Y.Z (or -SelfTest).' }
if (-not $Dist) { $Dist = Join-Path $repo 'dist' }

Write-Host "release-verify $Tag  (commit $(if ($Commit) { $Commit } else { '<none given>' }), dist $Dist, OneDrive $OneDrive)"
$rows = @()

# tag: asked of ORIGIN (Read-OriginTag).
$ghRepo = git -C $repo remote get-url origin 2>$null
$tagRead = Read-OriginTag $repo 'origin' $Tag
$tagCommit = $tagRead.Commit
$rows += Test-TagRow $Tag $tagRead $Commit

# release: one gh call. `digest` is GitHub's own sha256 of each asset, so nothing downloads.
$releaseRead = Read-GhRelease $Tag $ghRepo
$release = $releaseRead.Release
$rows += Test-ReleaseRow $Tag $releaseRead $script:Assets

# the freshness bound (Get-FreshnessBound)
$bound = Get-FreshnessBound $Since $Tag $tagRead {
    param($t, $c)
    git -C $repo fetch -q origin "refs/tags/${t}:refs/tags/${t}" 2>$null | Out-Null
    $fetchExit = $LASTEXITCODE
    $iso = git -C $repo log -1 --format=%cI $c 2>$null
    [pscustomobject]@{ Iso = $(if ($LASTEXITCODE -eq 0) { $iso } else { $null }); FetchExit = $fetchExit }
}

foreach ($name in $script:OneDriveFiles) {
    $p = Join-Path $OneDrive $name
    $file = if (Test-Path -LiteralPath $p) { Get-Item -LiteralPath $p } else { $null }
    $rows += Test-OneDriveRow $name $file $bound.Since $bound.Source $bound.NoBound
}

# The published .sha256 sidecars are 64 bytes each; they are the hash a player (or the
# updater) is told to expect, so they are compared too, not just the files.
$ghSide = Join-Path ([IO.Path]::GetTempPath()) ("release-verify-side-" + [guid]::NewGuid().ToString('N'))
try {
    if ($release) {
        New-Item -ItemType Directory -Path $ghSide | Out-Null
        gh release download $Tag --repo $ghRepo --dir $ghSide --pattern '*.sha256' 2>$null | Out-Null
    }
    foreach ($name in $script:Artifacts) {
        $gh = $null
        if ($release) {
            $asset = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
            if ($asset -and "$($asset.digest)" -match '^sha256:([0-9a-f]{64})$') { $gh = $Matches[1] }
        }
        $hashes = [ordered]@{
            onedrive         = Get-Sha (Join-Path $OneDrive $name)
            github           = $gh
            dist             = Get-Sha (Join-Path $Dist $name)
            'github sidecar' = Get-Sidecar (Join-Path $ghSide "$name.sha256")
        }
        if ($script:OneDriveFiles -contains "$name.sha256") { $hashes['onedrive sidecar'] = Get-Sidecar (Join-Path $OneDrive "$name.sha256") }
        $rows += Test-ShaRow $name $hashes
    }
}
finally { Remove-Item -LiteralPath $ghSide -Recurse -Force -ErrorAction SilentlyContinue }

# signatures: the installer from both places, and the app inside the zip players unpack.
$rows += Test-SignatureRow 'onedrive EQBuddyEvolvedSetup.exe' (Get-Sig (Join-Path $OneDrive 'EQBuddyEvolvedSetup.exe')) $ExpectedSigner
$rows += Test-SignatureRow 'dist EQBuddyEvolvedSetup.exe' (Get-Sig (Join-Path $Dist 'EQBuddyEvolvedSetup.exe')) $ExpectedSigner
$rows += Test-SignatureRow 'dist publish\EQBuddy.exe' (Get-Sig (Join-Path $Dist 'publish\EQBuddy.exe')) $ExpectedSigner

$zip = Join-Path $OneDrive 'EQBuddyEvolved-portable.zip'
$unz = Join-Path ([IO.Path]::GetTempPath()) ("release-verify-" + [guid]::NewGuid().ToString('N'))
try {
    $inner = $null
    if (Test-Path -LiteralPath $zip) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $entry = $archive.Entries | Where-Object { $_.Name -eq 'EQBuddy.exe' } | Select-Object -First 1
            if ($entry) {
                New-Item -ItemType Directory -Path $unz | Out-Null
                $inner = Join-Path $unz 'EQBuddy.exe'
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $inner)
            }
        }
        finally { $archive.Dispose() }
    }
    $rows += Test-SignatureRow 'onedrive zip\EQBuddy.exe' (Get-Sig $inner) $ExpectedSigner
}
finally { Remove-Item -LiteralPath $unz -Recurse -Force -ErrorAction SilentlyContinue }

Write-Rows $rows
$bad = @($rows | Where-Object { -not $_.Ok })
if ($bad.Count) {
    Write-Host "release-verify: FAIL: $($bad.Count) of $($rows.Count) row(s) red for $Tag. Do NOT re-run release.ps1 — hand the rows to Planner (docs/ops/release-seat.md)." -ForegroundColor Red
    exit 1
}
Write-Host "release-verify: all $($rows.Count) rows OK for $Tag" -ForegroundColor Green
exit 0
