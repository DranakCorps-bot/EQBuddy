<#
.SYNOPSIS
  Writes the landing page's LIVE figures file (site/live.json) for the hourly Pages deploy.

.DESCRIPTION
  Founder decision, 2026-09-28 (EQBuddy Evolved 0.1 Beta, tag v2.0.0): the landing shows
  live opt-in telemetry and a live installer-download total, and the visitor's browser
  still contacts nobody but the page's own origin. So the figures are fetched HERE, by the
  `pages` workflow, once an hour, and written into the Pages ARTIFACT as a same-origin
  `live.json` that `site/assets/js/landing.js` reads. Nothing is committed to `main`.

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
      reach the published file. A field the worker adds later (e.g. `installsAllTime`) is
      never copied, so it cannot go public by accident.
    * The downloads half re-measures the hero's "EQBuddy Downloads" the way site/metrics.json
      defines it: the sum of `download_count` over every GitHub release asset named in
      $InstallerAssets (1.x's EQBuddySetup.exe and Evolved's EQBuddyEvolvedSetup.exe), all
      releases, paginated. A failure there leaves the committed, dated snapshot on the page.

  A half that fails does not fail the deploy: the file is still written, with that half
  marked unavailable. If this script cannot run at all, the committed site/live.json —
  which is explicitly unavailable and carries no figure — is what gets published.

  Exit codes: 0 the file was written (whatever each half says) · 1 it could not be written.

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -OutFile site/live.json

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -OutFile out.json -FromFile worker.json -ReleasesFromFile releases.json

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$Worker = 'https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev',
    [string]$Repository = 'DranakCorps-bot/EQBuddy',
    [string]$OutFile,
    [string]$FromFile,
    [string]$ReleasesFromFile,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# The five figures the landing draws, in the order it draws them. Name on the page -> where
# it lives in the worker's schema-1 answer, and whether it must be a whole number.
$TelemetryFields = @(
    [pscustomobject]@{ Name = 'uniqueUsers30d';    Path = @('uniqueUsers30d');         Whole = $true }
    [pscustomobject]@{ Name = 'usageHoursAllTime'; Path = @('usageHours', 'allTime');  Whole = $false }
    [pscustomobject]@{ Name = 'dailyActive';       Path = @('dailyActive');            Whole = $true }
    [pscustomobject]@{ Name = 'weeklyActive';      Path = @('weeklyActive');           Whole = $true }
    [pscustomobject]@{ Name = 'peakConcurrent';    Path = @('peakConcurrent');         Whole = $true }
)

# The installers the "EQBuddy Downloads" tile counts. site/metrics.json's scope sentence
# names the same two; LandingSourceClaimsTests holds them together.
$InstallerAssets = @('EQBuddySetup.exe', 'EQBuddyEvolvedSetup.exe')

# An answer older than this is refused as stale. The worker recomputes every 10 minutes,
# so hours of lag mean something upstream stopped, and an old figure presented as live is
# the thing this file exists to prevent. landing.js applies the same window to live.json.
$MaxAgeHours = 6

# Release pages are 100 each; 176 releases on 2026-09-28. A bound so a broken paginator
# cannot loop forever — reaching it is a refusal, not a partial sum.
$MaxReleasePages = 30

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

# $Pages: the text of each /releases page, in order. The sum is only returned when the walk
# reached a page shorter than 100 — a walk that ran out of budget is refused, not summed.
function ConvertTo-LiveDownloads([string[]]$Pages, [datetime]$NowUtc) {
    if ($null -eq $Pages -or $Pages.Count -eq 0) { return New-Unavailable 'no release pages were read' }
    $total = [long]0
    $releases = 0
    $complete = $false
    foreach ($text in $Pages) {
        try { $page = ConvertFrom-Json -InputObject $text -AsHashtable -NoEnumerate }
        catch { return New-Unavailable 'a release page is not JSON' }
        if ($page -isnot [System.Collections.IList]) { return New-Unavailable 'a release page is not a JSON array' }
        foreach ($release in $page) {
            if ($release -isnot [System.Collections.IDictionary]) { return New-Unavailable 'a release is not a JSON object' }
            $releases++
            # Assigned directly, never through `if` as an expression: that pipes the array,
            # and a one-asset release would arrive unrolled as a bare object.
            $assets = $null
            if ($release.Contains('assets')) { $assets = $release['assets'] }
            if ($assets -isnot [System.Collections.IList]) { return New-Unavailable "release $releases has no assets list" }
            foreach ($asset in $assets) {
                if ($asset -isnot [System.Collections.IDictionary] -or -not $asset.Contains('name')) { continue }
                if ($InstallerAssets -cnotcontains [string]$asset['name']) { continue }
                $count = if ($asset.Contains('download_count')) { $asset['download_count'] } else { $null }
                if ($null -eq $count -or -not (Test-Figure $count $true)) {
                    return New-Unavailable "$($asset['name']) has download_count '$count', not a non-negative whole number"
                }
                $total += [long]$count
            }
        }
        if ($page.Count -lt 100) { $complete = $true; break }
    }
    if (-not $complete) { return New-Unavailable "the release list did not end within $($Pages.Count) pages; a partial sum is not the total" }
    [ordered]@{ available = $true; asOf = (Format-Utc $NowUtc); total = $total; releases = $releases; assets = $InstallerAssets }
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

function Get-LiveDownloads([string]$Repo, [string]$File, [datetime]$NowUtc) {
    if ($File) {
        if (-not (Test-Path -LiteralPath $File)) { return New-Unavailable "no such file: $File" }
        return ConvertTo-LiveDownloads @([IO.File]::ReadAllText($File, $Utf8NoBom)) $NowUtc
    }
    $headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    $token = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } elseif ($env:GH_TOKEN) { $env:GH_TOKEN } else { $null }
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    $pages = [System.Collections.Generic.List[string]]::new()
    for ($n = 1; $n -le $MaxReleasePages; $n++) {
        $read = Read-Url "https://api.github.com/repos/$Repo/releases?per_page=100&page=$n" $headers
        if (-not $read.Ok) { return New-Unavailable $read.Reason }
        $pages.Add($read.Text)
        # Stop at the first page shorter than 100 (or one that is not an array at all, which
        # ConvertTo-LiveDownloads then refuses by name).
        try { $arr = ConvertFrom-Json -InputObject $read.Text -NoEnumerate } catch { break }
        if ($arr -isnot [System.Collections.IList] -or $arr.Count -lt 100) { break }
    }
    ConvertTo-LiveDownloads $pages.ToArray() $NowUtc
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
    $good = [ordered]@{
        schema = 1; generatedAt = '2026-09-28T19:00:55Z'; concurrentNow = 0; peakConcurrent = 1
        peakConcurrentBucket = '2026-09-24T22:00:00Z'; uniqueUsers30d = 12; installsAllTime = 99
        versionMix7d = [ordered]@{ denominator = 1; versions = @() }; dailyActive = 3; weeklyActive = 7
        usageHours = [ordered]@{ yesterday = 0; last7d = 3.17; last30d = 3.17; allTime = 1234.5; todaySoFar = 0.5 }
        definitions = [ordered]@{ weeklyActive = 'x' }
    }
    function Answer([scriptblock]$Mutate) {
        $copy = ($good | ConvertTo-Json -Depth 8) | ConvertFrom-Json -AsHashtable
        if ($Mutate) { & $Mutate $copy }
        $copy | ConvertTo-Json -Depth 8
    }

    # --- the telemetry half: what is kept, and what is trimmed ---------------------------
    $t = ConvertTo-LiveTelemetry (Answer $null) $now
    Check 'a good answer is available' ($t.available -eq $true)
    Check 'asOf is the worker''s generatedAt, UTC' ($t.asOf -eq '2026-09-28T19:00:55Z')
    Check 'the five figures are kept, in page order' ((@($t.Keys) -join ',') -eq 'available,asOf,uniqueUsers30d,usageHoursAllTime,dailyActive,weeklyActive,peakConcurrent')
    Check 'counts are copied exactly' ($t.uniqueUsers30d -eq 12 -and $t.dailyActive -eq 3 -and $t.weeklyActive -eq 7 -and $t.peakConcurrent -eq 1)
    Check 'usage hours are rounded to a whole hour (1234.5 -> 1235)' ($t.usageHoursAllTime -eq 1235)
    $json = Format-LiveJson (New-LiveDocument $t (New-Unavailable 'x') $now)
    Check 'installsAllTime never reaches the published file' (-not $json.Contains('installsAllTime') -and -not $json.Contains('99'))
    Check 'nothing else the worker publishes is copied (concurrentNow, versionMix7d, definitions, last30d)' (
        -not $json.Contains('concurrentNow') -and -not $json.Contains('versionMix7d') -and -not $json.Contains('definitions') -and -not $json.Contains('last30d'))
    $n = ConvertTo-LiveTelemetry (Answer { param($a) $a['dailyActive'] = $null; $a['usageHours']['allTime'] = $null }) $now
    Check 'a null figure stays null (its tile paints a dash), and the rest are kept' ($n.available -and $null -eq $n.dailyActive -and $null -eq $n.usageHoursAllTime -and $n.weeklyActive -eq 7)
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
        'uniqueUsers30d as a string'             = (Answer { param($a) $a['uniqueUsers30d'] = '12' })
        'dailyActive negative'                   = (Answer { param($a) $a['dailyActive'] = -1 })
        'weeklyActive fractional'                = (Answer { param($a) $a['weeklyActive'] = 1.5 })
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

    # --- the downloads half ---------------------------------------------------------------
    function Release([string]$Tag, [hashtable]$Counts) {
        $assets = foreach ($k in $Counts.Keys) { [ordered]@{ name = $k; download_count = $Counts[$k] } }
        [ordered]@{ tag_name = $Tag; assets = @($assets) }
    }
    function Page([object[]]$Releases) { ConvertTo-Json -InputObject @($Releases) -Depth 6 }
    $p = Page @(
        (Release 'v2.0.0' @{ 'EQBuddyEvolvedSetup.exe' = 5; 'EQBuddyEvolvedSetup.exe.sha256' = 1000 }),
        (Release 'v1.99.19' @{ 'EQBuddySetup.exe' = 100; 'EQBuddy-portable.zip' = 1000; 'EQBuddySetup.exe.sha256' = 1000 }),
        (Release 'v1.99.18' @{ 'EQBuddySetup.exe' = 37 })
    )
    $d = ConvertTo-LiveDownloads @($p) $now
    Check 'downloads sum BOTH installers across releases, and nothing else (5 + 100 + 37)' ($d.available -and $d.total -eq 142 -and $d.releases -eq 3)
    Check 'the downloads half names the installers it counted' ((@($d.assets) -join ',') -eq 'EQBuddySetup.exe,EQBuddyEvolvedSetup.exe')
    $full = Page @(1..100 | ForEach-Object { Release "v1.0.$_" @{ 'EQBuddySetup.exe' = 1 } })
    $d = ConvertTo-LiveDownloads @($full, (Page @((Release 'v0.9' @{ 'EQBuddySetup.exe' = 2 })))) $now
    Check 'pagination: a full page of 100 continues to the next (100 + 2)' ($d.available -and $d.total -eq 102 -and $d.releases -eq 101)
    $d = ConvertTo-LiveDownloads @($full) $now
    Check 'a walk that ends on a full page is refused, not summed' ($d.available -eq $false)
    $d = ConvertTo-LiveDownloads @('[]') $now
    Check 'a repo with no releases totals 0' ($d.available -and $d.total -eq 0)
    $badPages = [ordered]@{
        'a download_count as a string'     = (Page @((Release 'v2.0.0' @{ 'EQBuddyEvolvedSetup.exe' = '5' })))
        'a negative download_count'        = (Page @((Release 'v2.0.0' @{ 'EQBuddyEvolvedSetup.exe' = -5 })))
        'a release page that is an object' = '{"message":"API rate limit exceeded"}'
        'a release page that is not JSON'  = '<html>'
    }
    foreach ($bad in $badPages.GetEnumerator()) {
        $d = ConvertTo-LiveDownloads @($bad.Value) $now
        Check "$($bad.Key): downloads unavailable" ($d.available -eq $false -and $d.reason)
    }

    # --- the file: written whole, and always written ------------------------------------
    $root = Join-Path ([IO.Path]::GetTempPath()) ("landing-live-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $wf = Join-Path $root 'worker.json'; [IO.File]::WriteAllText($wf, (Answer $null), $Utf8NoBom)
        $rf = Join-Path $root 'releases.json'; [IO.File]::WriteAllText($rf, $p, $Utf8NoBom)
        $out = Join-Path $root 'site/live.json'
        $doc = New-LiveDocument (Get-LiveTelemetry 'unused' $wf $now) (Get-LiveDownloads 'unused' $rf $now) $now
        Write-LiveFile $out $doc
        $back = [IO.File]::ReadAllText($out) | ConvertFrom-Json -AsHashtable
        Check 'the written file is schema 1 with both halves available' ($back['schema'] -eq 1 -and $back['telemetry']['available'] -and $back['downloads']['available'])
        Check 'the written file carries generatedAt' ([IO.File]::ReadAllText($out).Contains('"generatedAt": "2026-09-28T19:30:00Z"'))
        Check 'no temp file is left behind' (-not (Test-Path -LiteralPath "$out.tmp"))

        # A REAL socket to a port nobody listens on: the telemetry half is unavailable and the
        # file is still written — the deploy does not depend on the worker being up.
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $t = Get-LiveTelemetry "http://127.0.0.1:$port" $null $now
        Check 'an unreachable worker: telemetry unavailable, with the reason' ($t.available -eq $false -and $t.reason.Contains('could not read'))
        Write-LiveFile $out (New-LiveDocument $t (New-Unavailable 'x') $now)
        $back = [IO.File]::ReadAllText($out) | ConvertFrom-Json -AsHashtable
        Check 'and the file is still written, marked unavailable' ($back['telemetry']['available'] -eq $false)

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
        Check 'a worker answering HTTP 200 over the network path is available' ($t.available -and $t.weeklyActive -eq 7)

        $t = Get-LiveTelemetry 'unused' (Join-Path $root 'nope.json') $now
        Check 'a missing -FromFile: unavailable' ($t.available -eq $false)
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }

    # --- the committed copy carries no figure -------------------------------------------
    $committed = [IO.File]::ReadAllText((Join-Path $RepoRoot 'site/live.json'), $Utf8NoBom) | ConvertFrom-Json -AsHashtable
    Check 'the committed site/live.json is explicitly unavailable in both halves' (
        $committed['schema'] -eq 1 -and $committed['telemetry']['available'] -eq $false -and $committed['downloads']['available'] -eq $false)
    Check 'the committed site/live.json names no figure' (@($committed['telemetry'].Keys) -join ',' -eq 'available,reason' -and
        (@($committed['downloads'].Keys) -join ',') -eq 'available,reason')

    if ($script:checks -lt 45) { Write-Host "FAIL: only $script:checks landing-telemetry self-test checks ran (trap 78)."; return 1 }
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
$downloads = Get-LiveDownloads $Repository $ReleasesFromFile $now
try { Write-LiveFile $OutFile (New-LiveDocument $telemetry $downloads $now) }
catch { Write-Host "FAILED to write ${OutFile}: $($_.Exception.Message)"; exit 1 }

foreach ($half in @(@('telemetry', $telemetry), @('downloads', $downloads))) {
    $name = $half[0]; $v = $half[1]
    if ($v.available) {
        $figures = ($v.Keys | Where-Object { $_ -notin 'available', 'asOf', 'assets' } | ForEach-Object { "$_=$($v[$_])" }) -join ' '
        Write-Host "OK: $name as of $($v.asOf): $figures"
    }
    else {
        Write-Host "UNAVAILABLE: $name - $($v.reason). The page paints it as unavailable."
        if ($env:GITHUB_ACTIONS) { Write-Host "::warning title=landing live $name unavailable::$($v.reason)" }
    }
}
Write-Host "Wrote $OutFile (nothing is committed)."
exit 0
