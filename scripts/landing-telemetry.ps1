<#
.SYNOPSIS
  Writes the landing page's LIVE figures file (site/live.json) for the hourly Pages deploy.

.DESCRIPTION
  Founder decision, 2026-09-28 (EQBuddy Evolved 0.1 Beta, tag v2.0.0): the landing shows
  live opt-in telemetry, and the visitor's browser still contacts nobody but the page's own
  origin. So the figures are fetched HERE, by the `pages` workflow, once an hour, and
  written into the Pages ARTIFACT as a same-origin `live.json` that
  `site/assets/js/landing.js` reads. Nothing is committed to `main`.

  The same day the Founder folded the hero into ONE stat strip: Quests in the guide and
  Items cataloged (static, from site/metrics.json), then five live tiles from this file.
  The v1-era "EQBuddy Downloads" tile was dropped with it.

  Founder, 2026-09-29: "Instead of total installs, I would like to show downloads" — and,
  asked which, EVOLVED downloads only. So the file carries TWO halves again, each validated
  and each failing on its own, so a GitHub outage never blanks the telemetry tiles or the
  other way round:
    * telemetry — Hours used, Peak daily users, Peak weekly active, Peak concurrent, from the
      worker. installsAllTime is no longer copied: nothing draws it.
    * downloads — Evolved downloads: the sum of download_count over every release asset
      named exactly EQBuddyEvolvedSetup.exe, walked from the GitHub releases API (with the
      job's read-only token when it has one). The installer only — not the .sha256, not the
      portable zip, and not 1.x's EQBuddySetup.exe. It counts downloads, updates included,
      not people, and the page says so.

  This supersedes the DRA-379 shape this script used to have (a human-run writer of one
  `weeklyActive` figure into the committed `site/metrics.json`, structurally held until a
  tile existed). That hold and the no-cron rule were Helm rulings the Founder's decision
  replaces; the parts that survive are the ones that were about honesty, not timing:

    * It never FREEZES or INVENTS a figure (trap 81). The worker's answer is validated —
      HTTP 200, a JSON object, schema 1, a readable and recent `generatedAt`, and every
      field the page shows present as a non-negative number or null (counts must be whole)
      — and ANY defect makes the whole telemetry half `available: false` with the reason.
      The page then paints "—", never the previous hour's number or a guess.
    * It trims. Only the five figures the page draws, plus the time they were computed,
      reach the published file. A field the worker adds later is never copied, so it cannot
      go public by accident — `installsAllTime` is copied now because the Founder decided to
      show it, not because the worker publishes it.

  A telemetry half that fails does not fail the deploy: the file is still written, with the
  half marked unavailable. If this script cannot run at all, the committed site/live.json —
  which is explicitly unavailable and carries no figure — is what gets published.

  Exit codes: 0 the file was written (whatever the half says) · 1 it could not be written.

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -OutFile site/live.json

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -OutFile out.json -FromFile worker.json

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$Worker = 'https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev',
    [string]$GitHubApi = 'https://api.github.com',
    [string]$Repository = 'DranakCorps-bot/EQBuddy',
    [string]$OutFile,
    [string]$FromFile,
    # A saved releases-API answer (one JSON array) in place of the network walk.
    [string]$ReleasesFromFile,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# The four telemetry figures the landing's stat strip draws, in the order it draws them. Name
# on the page (its data-live key) -> where it lives in the worker's schema-1 answer, and
# whether it must be a whole number. Strict: a field the worker does not publish refuses the
# whole half. installsAllTime left this list 2026-09-29, when Evolved downloads took its tile.
$TelemetryFields = @(
    [pscustomobject]@{ Name = 'usageHoursAllTime'; Path = @('usageHours', 'allTime');  Whole = $false }
    [pscustomobject]@{ Name = 'peakDailyActive';   Path = @('peakDailyActive');        Whole = $true }
    [pscustomobject]@{ Name = 'peakWeeklyActive';  Path = @('peakWeeklyActive');       Whole = $true }
    [pscustomobject]@{ Name = 'peakConcurrent';    Path = @('peakConcurrent');         Whole = $true }
)

# The one asset Evolved downloads counts. Exact name, case-sensitive: the checksum beside it
# and the portable zip are not installer downloads, and 1.x's EQBuddySetup.exe is not Evolved.
$EvolvedInstaller = 'EQBuddyEvolvedSetup.exe'

# Pages of 100 releases the walk reads before it calls the answer suspect. 177 releases on
# 2026-09-29, so two pages; a walk that never ends is a defect, not a big number.
$MaxReleasePages = 20

# An answer older than this is refused as stale. The worker recomputes every 10 minutes,
# so hours of lag mean something upstream stopped, and an old figure presented as live is
# the thing this file exists to prevent. landing.js applies the same window to live.json.
$MaxAgeHours = 6

function New-Unavailable([string]$Reason) {
    [ordered]@{ available = $false; reason = $Reason }
}

function Get-Utc($Stamp) {
    if ($Stamp -is [datetime]) {
        if ($Stamp.Kind -eq [DateTimeKind]::Unspecified) { return [datetime]::SpecifyKind($Stamp, [DateTimeKind]::Utc) }
        return $Stamp.ToUniversalTime()
    }
    if ($Stamp -is [datetimeoffset]) { return $Stamp.UtcDateTime }
    if ($Stamp -is [string]) {
        $parsed = [datetimeoffset]::MinValue
        if ([datetimeoffset]::TryParse($Stamp, [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$parsed)) { return $parsed.UtcDateTime }
    }
    $null
}

function Format-Utc([datetime]$Utc) {
    $Utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture)
}

# ConvertFrom-Json gives a JSON integer as [long] (or [int]/[bigint] at the edges) and any
# number with a fraction or exponent as [double]. A string, a bool or an object is not a
# figure; neither is a negative, a NaN or an infinity.
function Test-Figure($Value, [bool]$Whole) {
    if ($null -eq $Value) { return $true }
    if ($Value -is [bool] -or $Value -is [string]) { return $false }
    if ($Value -is [long] -or $Value -is [int]) { return $Value -ge 0 }
    if ($Value -is [double] -or $Value -is [decimal]) {
        if ($Whole) { return $false }
        $d = [double]$Value
        return -not [double]::IsNaN($d) -and -not [double]::IsInfinity($d) -and $d -ge 0
    }
    $false
}

function ConvertTo-LiveTelemetry([string]$Text, [datetime]$NowUtc) {
    try { $w = $Text | ConvertFrom-Json -AsHashtable }
    catch { return New-Unavailable "the worker's answer is not JSON" }
    if ($w -isnot [System.Collections.IDictionary]) { return New-Unavailable "the worker's answer is not a JSON object" }
    if (-not $w.Contains('schema') -or -not ($w['schema'] -is [long] -or $w['schema'] -is [int]) -or $w['schema'] -ne 1) {
        return New-Unavailable "the worker's schema is not 1; this script reads schema 1 only"
    }
    $utc = if ($w.Contains('generatedAt')) { Get-Utc $w['generatedAt'] } else { $null }
    if ($null -eq $utc) { return New-Unavailable "the worker's generatedAt is missing or unreadable" }
    $age = $NowUtc - $utc
    if ($age.TotalHours -gt $MaxAgeHours) { return New-Unavailable "the worker's answer is $([int]$age.TotalHours) hours old (limit $MaxAgeHours); a stale figure is not shown as live" }
    if ($age.TotalMinutes -lt -10) { return New-Unavailable "the worker's generatedAt is in the future" }

    $out = [ordered]@{ available = $true; asOf = (Format-Utc $utc) }
    foreach ($f in $TelemetryFields) {
        $node = $w
        $label = $f.Path -join '.'
        foreach ($step in $f.Path) {
            if ($node -isnot [System.Collections.IDictionary] -or -not $node.Contains($step)) {
                return New-Unavailable "the worker published no $label"
            }
            $node = $node[$step]
        }
        if (-not (Test-Figure $node $f.Whole)) {
            $kind = if ($f.Whole) { 'a non-negative whole number' } else { 'a non-negative number' }
            return New-Unavailable "the worker's $label is '$node', not $kind or null"
        }
        $out[$f.Name] = if ($null -eq $node) { $null }
            elseif ($f.Whole) { [long]$node }
            else { [long][Math]::Round([double]$node, [MidpointRounding]::AwayFromZero) }
    }
    $out
}

# The downloads half from the releases API's answer, one JSON text per page. Every release
# must be an object whose assets are objects with a string name, and every Evolved installer
# must carry a non-negative whole download_count; anything else refuses the half. So does
# finding NO Evolved installer at all: v2.0.0 ships one, so its absence means the walk read
# the wrong thing, and 0 would be a figure invented out of a failure (trap 81).
function ConvertTo-LiveDownloads([string[]]$Pages, [datetime]$NowUtc) {
    $total = [long]0
    $found = 0
    foreach ($text in $Pages) {
        try { $page = ConvertFrom-Json -InputObject $text -AsHashtable -NoEnumerate }
        catch { return New-Unavailable "the releases API's answer is not JSON" }
        if ($page -isnot [System.Collections.IList]) { return New-Unavailable "the releases API's answer is not a JSON array" }
        foreach ($release in $page) {
            if ($release -isnot [System.Collections.IDictionary] -or -not $release.Contains('assets') -or
                $release['assets'] -isnot [System.Collections.IList]) {
                return New-Unavailable 'a release in the answer has no assets list'
            }
            foreach ($asset in $release['assets']) {
                if ($asset -isnot [System.Collections.IDictionary] -or $asset['name'] -isnot [string]) {
                    return New-Unavailable 'an asset in the answer has no name'
                }
                if ($asset['name'] -cne $EvolvedInstaller) { continue }
                $n = $asset['download_count']
                if (-not (($n -is [long] -or $n -is [int]) -and $n -ge 0)) {
                    return New-Unavailable "an $EvolvedInstaller download_count is '$n', not a non-negative whole number"
                }
                $total += [long]$n
                $found++
            }
        }
    }
    if ($found -eq 0) { return New-Unavailable "no release carries $EvolvedInstaller; a zero would be invented, not counted" }
    [ordered]@{ available = $true; asOf = (Format-Utc $NowUtc); evolvedDownloads = $total }
}

function Get-LiveDownloads([string]$Api, [string]$Repo, [string]$File, [datetime]$NowUtc) {
    if ($File) {
        if (-not (Test-Path -LiteralPath $File)) { return New-Unavailable "no such file: $File" }
        return ConvertTo-LiveDownloads @([IO.File]::ReadAllText($File, $Utf8NoBom)) $NowUtc
    }
    $headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    $token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } elseif ($env:GH_TOKEN) { $env:GH_TOKEN } else { $null }
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    $pages = @()
    for ($n = 1; $n -le $MaxReleasePages; $n++) {
        $read = Read-Url ("$($Api.TrimEnd('/'))/repos/$Repo/releases?per_page=100&page=$n") $headers
        if (-not $read.Ok) { return New-Unavailable $read.Reason }
        $pages += $read.Text
        try { $page = ConvertFrom-Json -InputObject $read.Text -AsHashtable -NoEnumerate }
        catch { return New-Unavailable "the releases API's answer is not JSON" }
        # A short page is the last one; the conversion above re-reads every page and refuses
        # anything that is not an array of releases.
        if ($page -isnot [System.Collections.IList] -or $page.Count -lt 100) { return ConvertTo-LiveDownloads $pages $NowUtc }
    }
    New-Unavailable "the releases walk did not end within $MaxReleasePages pages"
}

function New-LiveDocument($Telemetry, $Downloads, [datetime]$NowUtc) {
    [ordered]@{ schema = 1; generatedAt = (Format-Utc $NowUtc); telemetry = $Telemetry; downloads = $Downloads }
}

function Format-LiveJson($Doc) { (($Doc | ConvertTo-Json -Depth 8) -replace "`r`n", "`n") + "`n" }

function Read-Url([string]$Url, [hashtable]$Headers) {
    try {
        $r = Invoke-WebRequest -Uri $Url -Headers $Headers -TimeoutSec 20 -SkipHttpErrorCheck -UseBasicParsing
    }
    catch { return [pscustomobject]@{ Ok = $false; Text = $null; Reason = "could not read $Url - $($_.Exception.Message)" } }
    if ($r.StatusCode -ne 200) { return [pscustomobject]@{ Ok = $false; Text = $null; Reason = "$Url answered HTTP $($r.StatusCode), not 200" } }
    $content = $r.Content
    if ($content -is [byte[]]) { $content = $Utf8NoBom.GetString($content) }
    [pscustomobject]@{ Ok = $true; Text = [string]$content; Reason = $null }
}

function Get-LiveTelemetry([string]$Base, [string]$File, [datetime]$NowUtc) {
    if ($File) {
        if (-not (Test-Path -LiteralPath $File)) { return New-Unavailable "no such file: $File" }
        return ConvertTo-LiveTelemetry ([IO.File]::ReadAllText($File, $Utf8NoBom)) $NowUtc
    }
    $read = Read-Url ($Base.TrimEnd('/') + '/metrics.json') @{}
    if (-not $read.Ok) { return New-Unavailable $read.Reason }
    ConvertTo-LiveTelemetry $read.Text $NowUtc
}

# Build the whole text first, then one write through a temp file, so a crash midway leaves
# whatever was there (in CI: the committed, explicitly-unavailable copy) rather than half a file.
function Write-LiveFile([string]$Path, $Doc) {
    $text = Format-LiveJson $Doc
    $full = [IO.Path]::GetFullPath($Path)
    $dir = Split-Path -Parent $full
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $tmp = "$full.tmp"
    [IO.File]::WriteAllText($tmp, $text, $Utf8NoBom)
    Move-Item -LiteralPath $tmp -Destination $full -Force
}

# ---------------------------------------------------------------------------
# -SelfTest: offline. Fixture answers as strings and temp files, one loopback server per HTTP
# arm, and one socket to a port nobody is listening on. Every refusal arm fires (trap 78).
# ---------------------------------------------------------------------------
function Invoke-SelfTest {
    $script:checks = 0
    $script:failures = 0
    function Check([string]$Label, [bool]$Ok) {
        $script:checks++
        if ($Ok) { Write-Host "  [ok]   $Label" }
        else { $script:failures++; Write-Host "  [FAIL] $Label" }
    }

    $now = [datetime]::SpecifyKind([datetime]'2026-09-28T19:30:00', [DateTimeKind]::Utc)
    # Every figure distinct, so a check can tell which field was copied where.
    $good = [ordered]@{
        schema = 1; generatedAt = '2026-09-28T19:00:55Z'; concurrentNow = 4; peakConcurrent = 10
        peakConcurrentBucket = '2026-09-24T22:00:00Z'; uniqueUsers30d = 42; installsAllTime = 99
        peakDailyActive = 12; peakWeeklyActive = 13
        versionMix7d = [ordered]@{ denominator = 1; versions = @() }; dailyActive = 3; weeklyActive = 7
        usageHours = [ordered]@{ yesterday = 0; last7d = 3.17; last30d = 3.17; allTime = 1234.5; todaySoFar = 0.5 }
        definitions = [ordered]@{ weeklyActive = 'x' }
    }
    $pageOrder = 'available,asOf,usageHoursAllTime,peakDailyActive,peakWeeklyActive,peakConcurrent'
    function Answer([scriptblock]$Mutate) {
        $copy = ($good | ConvertTo-Json -Depth 8) | ConvertFrom-Json -AsHashtable
        if ($Mutate) { & $Mutate $copy }
        $copy | ConvertTo-Json -Depth 8
    }

    # --- the telemetry half: what is kept, and what is trimmed ---------------------------
    $t = ConvertTo-LiveTelemetry (Answer $null) $now
    Check 'a good answer is available' ($t.available -eq $true)
    Check 'asOf is the worker''s generatedAt, UTC' ($t.asOf -eq '2026-09-28T19:00:55Z')
    Check 'the four figures are kept, in page order' ((@($t.Keys) -join ',') -eq $pageOrder)
    Check 'counts are copied exactly, each from its own field' ($t.peakDailyActive -eq 12 -and
        $t.peakWeeklyActive -eq 13 -and $t.peakConcurrent -eq 10)
    Check 'usage hours are rounded to a whole hour (1234.5 -> 1235)' ($t.usageHoursAllTime -eq 1235)
    $json = Format-LiveJson (New-LiveDocument $t (New-Unavailable 'x') $now)
    # Founder, 2026-09-29: Evolved downloads took the Total installs tile, so the install count
    # is trimmed again. It flipped to a positive on 2026-09-28 and back to a negative now.
    Check 'installsAllTime is not copied (its tile became Evolved downloads, 2026-09-29)' (
        -not $json.Contains('installsAllTime'))
    $published = @(($json | ConvertFrom-Json -AsHashtable)['telemetry'].Keys) -join ','
    Check 'nothing else the worker publishes is copied (uniqueUsers30d, dailyActive, weeklyActive, concurrentNow, versionMix7d, definitions, last30d)' (
        $published -eq $pageOrder -and -not $json.Contains('"uniqueUsers30d"') -and -not $json.Contains('"dailyActive"') -and
        -not $json.Contains('"weeklyActive"') -and -not $json.Contains('concurrentNow') -and -not $json.Contains('versionMix7d') -and
        -not $json.Contains('definitions') -and -not $json.Contains('last30d') -and -not $json.Contains('peakConcurrentBucket'))
    $s = ConvertTo-LiveTelemetry (Answer { param($a) $a['activeLast24h'] = 5; $a['someFutureFigure'] = 77 }) $now
    Check 'a field the worker adds later is not copied' ($s.available -and (@($s.Keys) -join ',') -eq $pageOrder)
    $n = ConvertTo-LiveTelemetry (Answer { param($a) $a['peakDailyActive'] = $null; $a['usageHours']['allTime'] = $null }) $now
    Check 'a null figure stays null (its tile paints a dash), and the rest are kept' ($n.available -and $null -eq $n.peakDailyActive -and
        $null -eq $n.usageHoursAllTime -and $n.peakWeeklyActive -eq 13)
    $z = ConvertTo-LiveTelemetry (Answer { param($a) $a['usageHours']['allTime'] = 3 }) $now
    Check 'a whole-number usage figure is accepted' ($z.available -and $z.usageHoursAllTime -eq 3)

    # --- every defect refuses the WHOLE half (trap 81: never freeze, never guess) ----------
    $refusals = [ordered]@{
        'schema 2'                               = (Answer { param($a) $a['schema'] = 2 })
        'schema as a string'                     = (Answer { param($a) $a['schema'] = '1' })
        'schema missing'                         = (Answer { param($a) $a.Remove('schema') })
        'generatedAt missing'                    = (Answer { param($a) $a.Remove('generatedAt') })
        'generatedAt unreadable'                 = (Answer { param($a) $a['generatedAt'] = 'yesterday' })
        'generatedAt seven hours old (stale)'    = (Answer { param($a) $a['generatedAt'] = '2026-09-28T12:29:00Z' })
        'generatedAt an hour in the future'      = (Answer { param($a) $a['generatedAt'] = '2026-09-28T20:30:00Z' })
        'peakDailyActive negative'               = (Answer { param($a) $a['peakDailyActive'] = -1 })
        'peakDailyActive absent (a worker before the companion deploy)' = (Answer { param($a) $a.Remove('peakDailyActive') })
        'peakWeeklyActive fractional'            = (Answer { param($a) $a['peakWeeklyActive'] = 1.5 })
        'peakWeeklyActive absent'                = (Answer { param($a) $a.Remove('peakWeeklyActive') })
        'peakConcurrent a boolean'               = (Answer { param($a) $a['peakConcurrent'] = $true })
        'peakConcurrent absent'                  = (Answer { param($a) $a.Remove('peakConcurrent') })
        'usageHours.allTime as a string'         = (Answer { param($a) $a['usageHours']['allTime'] = 'lots' })
        'usageHours.allTime negative'            = (Answer { param($a) $a['usageHours']['allTime'] = -0.5 })
        'usageHours not an object'               = (Answer { param($a) $a['usageHours'] = 4 })
        'usageHours absent'                      = (Answer { param($a) $a.Remove('usageHours') })
        'not JSON (a 502 page)'                  = '<html>502 Bad Gateway</html>'
        'a JSON array'                           = '[1,2]'
    }
    foreach ($name in $refusals.Keys) {
        $r = ConvertTo-LiveTelemetry $refusals[$name] $now
        Check "$name`: unavailable, with a reason, and no figure" ($r.available -eq $false -and $r.reason -and
            (@($r.Keys) -join ',') -eq 'available,reason')
    }

    # --- the downloads half: the installer only, every page, and never an invented zero ---
    function Release([string]$Tag, [object[]]$Assets) { [ordered]@{ tag_name = $Tag; assets = @($Assets) } }
    function Asset([string]$Name, $Count) { [ordered]@{ name = $Name; download_count = $Count } }
    # -InputObject of an array already writes a JSON array; adding -AsArray nests it one level
    # deeper (trap 80's shape), and the converter rightly refuses that as "no assets list".
    function Page([object[]]$Releases) { ConvertTo-Json -InputObject @($Releases) -Depth 6 }
    $p1 = Page @(
        (Release 'v2.1.0' @((Asset 'EQBuddyEvolvedSetup.exe' 300), (Asset 'EQBuddyEvolvedSetup.exe.sha256' 9), (Asset 'EQBuddyEvolved-portable.zip' 7))),
        (Release 'v2.0.0' @((Asset 'EQBuddyEvolvedSetup.exe' 419), (Asset 'EQBuddyEvolvedSetup.exe.sha256' 4))))
    $p2 = Page @(
        (Release 'v1.99.18' @((Asset 'EQBuddySetup.exe' 1692), (Asset 'EQBuddy-portable.zip' 900))),
        (Release 'v1.0.0' @()))
    $d = ConvertTo-LiveDownloads @($p1, $p2) $now
    Check 'downloads: the Evolved installer summed across releases and pages (300 + 419)' ($d.available -and $d.evolvedDownloads -eq 719)
    Check 'downloads: the half is available, asOf, evolvedDownloads, in that order' ((@($d.Keys) -join ',') -eq 'available,asOf,evolvedDownloads')
    Check 'downloads: the checksum, the portable zip and 1.x''s installer are not counted' ($d.evolvedDownloads -eq 719)
    $dRefusals = [ordered]@{
        'no Evolved installer on any release (a zero would be invented)' = @($p2)
        'an installer download_count as a string'   = @((Page @((Release 'v2.0.0' @((Asset 'EQBuddyEvolvedSetup.exe' '419'))))))
        'an installer download_count negative'      = @((Page @((Release 'v2.0.0' @((Asset 'EQBuddyEvolvedSetup.exe' -1))))))
        'an installer download_count fractional'    = @((Page @((Release 'v2.0.0' @((Asset 'EQBuddyEvolvedSetup.exe' 1.5))))))
        'a release with no assets list'             = @('[{"tag_name":"v2.0.0"}]')
        'an asset with no name'                     = @('[{"tag_name":"v2.0.0","assets":[{"download_count":3}]}]')
        'a JSON object, not an array'               = @('{"message":"Bad credentials"}')
        'not JSON (an HTML error page)'             = @('<html>rate limited</html>')
        'a case-folded installer name only'         = @((Page @((Release 'v2.0.0' @((Asset 'eqbuddyevolvedsetup.exe' 5))))))
    }
    foreach ($name in $dRefusals.Keys) {
        $r = ConvertTo-LiveDownloads $dRefusals[$name] $now
        Check "downloads, $name`: unavailable, with a reason, and no figure" ($r.available -eq $false -and $r.reason -and
            (@($r.Keys) -join ',') -eq 'available,reason')
    }

    # --- the file: written whole, and always written ------------------------------------
    $root = Join-Path ([IO.Path]::GetTempPath()) ("landing-live-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $wf = Join-Path $root 'worker.json'; [IO.File]::WriteAllText($wf, (Answer $null), $Utf8NoBom)
        $rf = Join-Path $root 'releases.json'; [IO.File]::WriteAllText($rf, $p1, $Utf8NoBom)
        $out = Join-Path $root 'site/live.json'
        Write-LiveFile $out (New-LiveDocument (Get-LiveTelemetry 'unused' $wf $now) (Get-LiveDownloads 'unused' 'x/y' $rf $now) $now)
        $back = [IO.File]::ReadAllText($out) | ConvertFrom-Json -AsHashtable
        Check 'the written file is schema 1 with the telemetry half available' ($back['schema'] -eq 1 -and $back['telemetry']['available'])
        Check 'the written file carries both halves: schema, generatedAt, telemetry, downloads' (
            (@($back.Keys) -join ',') -eq 'schema,generatedAt,telemetry,downloads')
        Check 'the written downloads half carries the figure (-ReleasesFromFile: 300 + 419)' (
            $back['downloads']['available'] -and $back['downloads']['evolvedDownloads'] -eq 719)
        Check 'the written file carries generatedAt' ([IO.File]::ReadAllText($out).Contains('"generatedAt": "2026-09-28T19:30:00Z"'))
        Check 'no temp file is left behind' (-not (Test-Path -LiteralPath "$out.tmp"))

        # A REAL socket to a port nobody listens on: the telemetry half is unavailable and the
        # file is still written — the deploy does not depend on the worker being up.
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $t = Get-LiveTelemetry "http://127.0.0.1:$port" $null $now
        Check 'an unreachable worker: telemetry unavailable, with the reason' ($t.available -eq $false -and $t.reason.Contains('could not read'))
        $dl = Get-LiveDownloads "http://127.0.0.1:$port" 'x/y' $null $now
        Check 'an unreachable releases API: downloads unavailable, with the reason' ($dl.available -eq $false -and $dl.reason.Contains('could not read'))
        Write-LiveFile $out (New-LiveDocument $t (Get-LiveDownloads 'unused' 'x/y' $rf $now) $now)
        $back = [IO.File]::ReadAllText($out) | ConvertFrom-Json -AsHashtable
        Check 'and the file is still written, telemetry marked unavailable' ($back['telemetry']['available'] -eq $false)
        Check 'one half failing leaves the other half standing (downloads still 719)' (
            $back['downloads']['available'] -and $back['downloads']['evolvedDownloads'] -eq 719)

        function Serve-Once([string]$Status, [string]$Body) {
            $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $l.Start()
            $job = Start-ThreadJob -ArgumentList $l, $Status, $Body -ScriptBlock {
                param($l, $Status, $Body)
                try {
                    $c = $l.AcceptTcpClient()
                    $s = $c.GetStream()
                    $buf = [byte[]]::new(8192); $null = $s.Read($buf, 0, $buf.Length)
                    $b = [Text.Encoding]::UTF8.GetBytes($Body)
                    $head = [Text.Encoding]::ASCII.GetBytes("HTTP/1.1 $Status`r`nContent-Type: application/json`r`nContent-Length: $($b.Length)`r`nConnection: close`r`n`r`n")
                    $s.Write($head, 0, $head.Length); $s.Write($b, 0, $b.Length); $s.Flush(); $c.Close()
                }
                finally { $l.Stop() }
            }
            [pscustomobject]@{ Url = "http://127.0.0.1:$($l.LocalEndpoint.Port)"; Job = $job }
        }
        $srv = Serve-Once '503 Service Unavailable' (Answer $null)
        $t = Get-LiveTelemetry $srv.Url $null $now
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a worker answering HTTP 503 (even with a good body): unavailable' ($t.available -eq $false -and $t.reason.Contains('503'))
        $srv = Serve-Once '200 OK' (Answer $null)
        $t = Get-LiveTelemetry $srv.Url $null $now
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a worker answering HTTP 200 over the network path is available' ($t.available -and $t.peakWeeklyActive -eq 13)

        $srv = Serve-Once '403 Forbidden' '{"message":"API rate limit exceeded"}'
        $dl = Get-LiveDownloads $srv.Url 'x/y' $null $now
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a releases API answering HTTP 403 (rate limited): downloads unavailable' ($dl.available -eq $false -and $dl.reason.Contains('403'))
        $srv = Serve-Once '200 OK' $p1
        $dl = Get-LiveDownloads $srv.Url 'x/y' $null $now
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a releases API answering one short page over the network path: available, summed' ($dl.available -and $dl.evolvedDownloads -eq 719)

        $t = Get-LiveTelemetry 'unused' (Join-Path $root 'nope.json') $now
        Check 'a missing -FromFile: unavailable' ($t.available -eq $false)
        $dl = Get-LiveDownloads 'unused' 'x/y' (Join-Path $root 'nope.json') $now
        Check 'a missing -ReleasesFromFile: unavailable' ($dl.available -eq $false)
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }

    # --- the committed copy carries no figure -------------------------------------------
    $committed = [IO.File]::ReadAllText((Join-Path $RepoRoot 'site/live.json'), $Utf8NoBom) | ConvertFrom-Json -AsHashtable
    Check 'the committed site/live.json is explicitly unavailable in both halves' (
        $committed['schema'] -eq 1 -and $committed['telemetry']['available'] -eq $false -and
        $committed.Contains('downloads') -and $committed['downloads']['available'] -eq $false)
    Check 'the committed site/live.json names no figure' ((@($committed['telemetry'].Keys) -join ',') -eq 'available,reason' -and
        (@($committed['downloads'].Keys) -join ',') -eq 'available,reason')

    if ($script:checks -lt 58) { Write-Host "FAIL: only $script:checks landing-telemetry self-test checks ran (trap 78)."; return 1 }
    if ($script:failures -gt 0) {
        Write-Host "FAIL: $script:failures of $script:checks landing-telemetry self-test checks failed."
        return 1
    }
    Write-Host "OK: $script:checks landing-telemetry self-test checks passed (offline)."
    0
}

if ($SelfTest) { exit (Invoke-SelfTest) }

if (-not $OutFile) {
    Write-Host 'Usage: landing-telemetry.ps1 -OutFile <path> [-FromFile worker.json] [-ReleasesFromFile releases.json] | -SelfTest'
    exit 1
}

$now = [datetime]::UtcNow
$telemetry = Get-LiveTelemetry $Worker $FromFile $now
$downloads = Get-LiveDownloads $GitHubApi $Repository $ReleasesFromFile $now
try { Write-LiveFile $OutFile (New-LiveDocument $telemetry $downloads $now) }
catch { Write-Host "FAILED to write ${OutFile}: $($_.Exception.Message)"; exit 1 }

if ($telemetry.available) {
    $figures = ($telemetry.Keys | Where-Object { $_ -notin 'available', 'asOf' } | ForEach-Object { "$_=$($telemetry[$_])" }) -join ' '
    Write-Host "OK: telemetry as of $($telemetry.asOf): $figures"
}
else {
    Write-Host "UNAVAILABLE: telemetry - $($telemetry.reason). The page paints it as unavailable."
    if ($env:GITHUB_ACTIONS) { Write-Host "::warning title=landing live telemetry unavailable::$($telemetry.reason)" }
}
if ($downloads.available) { Write-Host "OK: downloads as of $($downloads.asOf): evolvedDownloads=$($downloads.evolvedDownloads)" }
else {
    Write-Host "UNAVAILABLE: downloads - $($downloads.reason). The page paints it as unavailable."
    if ($env:GITHUB_ACTIONS) { Write-Host "::warning title=landing live downloads unavailable::$($downloads.reason)" }
}
Write-Host "Wrote $OutFile (nothing is committed)."
exit 0
