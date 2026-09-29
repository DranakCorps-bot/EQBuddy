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
  Items cataloged (static, from site/metrics.json), then five live tiles from this file —
  Total installs, Hours used, Peak daily users, Peak weekly active, Peak concurrent. The
  all-time install count, kept private that morning, is now public by his decision. The
  v1-era "EQBuddy Downloads" tile was dropped, and with it the downloads half this file
  used to carry (an hourly walk of the GitHub releases API whose answer nothing drew).

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
    [string]$OutFile,
    [string]$FromFile,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# The five live figures the landing's stat strip draws, in the order it draws them. Name on
# the page (its data-live key) -> where it lives in the worker's schema-1 answer, and whether
# it must be a whole number. Strict: a field the worker does not publish refuses the whole
# half, so peakDailyActive / peakWeeklyActive (the most distinct installs in any single UTC
# day / any 7-day window, today included) need the companion worker deploy before this ships.
$TelemetryFields = @(
    [pscustomobject]@{ Name = 'installsAllTime';   Path = @('installsAllTime');        Whole = $true }
    [pscustomobject]@{ Name = 'usageHoursAllTime'; Path = @('usageHours', 'allTime');  Whole = $false }
    [pscustomobject]@{ Name = 'peakDailyActive';   Path = @('peakDailyActive');        Whole = $true }
    [pscustomobject]@{ Name = 'peakWeeklyActive';  Path = @('peakWeeklyActive');       Whole = $true }
    [pscustomobject]@{ Name = 'peakConcurrent';    Path = @('peakConcurrent');         Whole = $true }
)

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

function New-LiveDocument($Telemetry, [datetime]$NowUtc) {
    [ordered]@{ schema = 1; generatedAt = (Format-Utc $NowUtc); telemetry = $Telemetry }
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
    $pageOrder = 'available,asOf,installsAllTime,usageHoursAllTime,peakDailyActive,peakWeeklyActive,peakConcurrent'
    function Answer([scriptblock]$Mutate) {
        $copy = ($good | ConvertTo-Json -Depth 8) | ConvertFrom-Json -AsHashtable
        if ($Mutate) { & $Mutate $copy }
        $copy | ConvertTo-Json -Depth 8
    }

    # --- the telemetry half: what is kept, and what is trimmed ---------------------------
    $t = ConvertTo-LiveTelemetry (Answer $null) $now
    Check 'a good answer is available' ($t.available -eq $true)
    Check 'asOf is the worker''s generatedAt, UTC' ($t.asOf -eq '2026-09-28T19:00:55Z')
    Check 'the five figures are kept, in page order' ((@($t.Keys) -join ',') -eq $pageOrder)
    Check 'counts are copied exactly, each from its own field' ($t.installsAllTime -eq 99 -and $t.peakDailyActive -eq 12 -and
        $t.peakWeeklyActive -eq 13 -and $t.peakConcurrent -eq 10)
    Check 'usage hours are rounded to a whole hour (1234.5 -> 1235)' ($t.usageHoursAllTime -eq 1235)
    $json = Format-LiveJson (New-LiveDocument $t $now)
    # Founder, 2026-09-28: the all-time install count IS public now. This check used to assert
    # the opposite; it flips to a positive so a trim that drops it again goes red.
    Check 'installsAllTime reaches the published file (Founder decision 2026-09-28: it is public)' (
        $json.Contains('"installsAllTime": 99'))
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
        'installsAllTime as a string'            = (Answer { param($a) $a['installsAllTime'] = '99' })
        'installsAllTime absent'                 = (Answer { param($a) $a.Remove('installsAllTime') })
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

    # --- the file: written whole, and always written ------------------------------------
    $root = Join-Path ([IO.Path]::GetTempPath()) ("landing-live-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $wf = Join-Path $root 'worker.json'; [IO.File]::WriteAllText($wf, (Answer $null), $Utf8NoBom)
        $out = Join-Path $root 'site/live.json'
        Write-LiveFile $out (New-LiveDocument (Get-LiveTelemetry 'unused' $wf $now) $now)
        $back = [IO.File]::ReadAllText($out) | ConvertFrom-Json -AsHashtable
        Check 'the written file is schema 1 with the telemetry half available' ($back['schema'] -eq 1 -and $back['telemetry']['available'])
        Check 'the written file carries no downloads half (retired 2026-09-28; nothing draws it)' (
            (@($back.Keys) -join ',') -eq 'schema,generatedAt,telemetry')
        Check 'the written file carries generatedAt' ([IO.File]::ReadAllText($out).Contains('"generatedAt": "2026-09-28T19:30:00Z"'))
        Check 'no temp file is left behind' (-not (Test-Path -LiteralPath "$out.tmp"))

        # A REAL socket to a port nobody listens on: the telemetry half is unavailable and the
        # file is still written — the deploy does not depend on the worker being up.
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $t = Get-LiveTelemetry "http://127.0.0.1:$port" $null $now
        Check 'an unreachable worker: telemetry unavailable, with the reason' ($t.available -eq $false -and $t.reason.Contains('could not read'))
        Write-LiveFile $out (New-LiveDocument $t $now)
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
        Check 'a worker answering HTTP 200 over the network path is available' ($t.available -and $t.peakWeeklyActive -eq 13)

        $t = Get-LiveTelemetry 'unused' (Join-Path $root 'nope.json') $now
        Check 'a missing -FromFile: unavailable' ($t.available -eq $false)
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }

    # --- the committed copy carries no figure -------------------------------------------
    $committed = [IO.File]::ReadAllText((Join-Path $RepoRoot 'site/live.json'), $Utf8NoBom) | ConvertFrom-Json -AsHashtable
    Check 'the committed site/live.json is explicitly unavailable, with no downloads half' (
        $committed['schema'] -eq 1 -and $committed['telemetry']['available'] -eq $false -and -not $committed.Contains('downloads'))
    Check 'the committed site/live.json names no figure' ((@($committed['telemetry'].Keys) -join ',') -eq 'available,reason')

    if ($script:checks -lt 42) { Write-Host "FAIL: only $script:checks landing-telemetry self-test checks ran (trap 78)."; return 1 }
    if ($script:failures -gt 0) {
        Write-Host "FAIL: $script:failures of $script:checks landing-telemetry self-test checks failed."
        return 1
    }
    Write-Host "OK: $script:checks landing-telemetry self-test checks passed (offline)."
    0
}

if ($SelfTest) { exit (Invoke-SelfTest) }

if (-not $OutFile) {
    Write-Host 'Usage: landing-telemetry.ps1 -OutFile <path> [-FromFile worker.json] | -SelfTest'
    exit 1
}

$now = [datetime]::UtcNow
$telemetry = Get-LiveTelemetry $Worker $FromFile $now
try { Write-LiveFile $OutFile (New-LiveDocument $telemetry $now) }
catch { Write-Host "FAILED to write ${OutFile}: $($_.Exception.Message)"; exit 1 }

if ($telemetry.available) {
    $figures = ($telemetry.Keys | Where-Object { $_ -notin 'available', 'asOf' } | ForEach-Object { "$_=$($telemetry[$_])" }) -join ' '
    Write-Host "OK: telemetry as of $($telemetry.asOf): $figures"
}
else {
    Write-Host "UNAVAILABLE: telemetry - $($telemetry.reason). The page paints it as unavailable."
    if ($env:GITHUB_ACTIONS) { Write-Host "::warning title=landing live telemetry unavailable::$($telemetry.reason)" }
}
Write-Host "Wrote $OutFile (nothing is committed)."
exit 0
