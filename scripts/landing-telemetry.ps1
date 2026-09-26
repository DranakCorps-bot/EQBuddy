<#
.SYNOPSIS
  The ONE writer of the landing page's opt-in telemetry snapshot (DRA-379 D1, DRA-440).

.DESCRIPTION
  Reads the telemetry worker's public /metrics.json (DRA-369) and rewrites, together:

    site/metrics.json   `weeklyActive` and `scope.weeklyActive`
    site/index.html     the `.n` text of the `data-metric="weeklyActive"` tile

  One writer of both halves, because the `.n` text is the same snapshot the page paints
  when its fetch does not run (trap 4). `landing.js` is not touched: its generic painter
  already paints any flat top-level key a tile names.

  Run BY A HUMAN, per refresh; each refresh is an ordinary reviewed `site/**` PR. Helm's
  SIGN on DRA-379 (EQBuddy PR #912, 2026-09-26) REJECTED the two other shapes: the page
  fetching the worker cross-origin (it would break the page's no-other-origin promise where
  `LandingSiteTests`' scanner cannot see), and a cron committing to `main` (Pages publishes
  on merge, so that is an unattended job publishing numbers under the project's name).
  Nothing in CI calls this script against the network; CI runs `-SelfTest`, which is offline (its only sockets are on loopback).

  THE TILE IS HELD. The SIGN's Q4 holds the first snapshot that paints `weeklyActive`
  into live `site/**` for the Founder's push-wide / public Evolved go. That hold is
  STRUCTURAL here, not a flag: the script refuses (exit 4) when `site/index.html` has no
  `weeklyActive` tile, because writing the JSON alone would publish the number with no
  tile to carry its scope and split the snapshot in two. The held PR that adds the tile is
  what makes this script write; nothing on this side needs to change for it.

  It never FREEZES an absence (trap 81). Anything but HTTP 200 + schema 1 + a non-negative
  integer `weeklyActive` + a non-empty `definitions.weeklyActive` is REFUSED with the reason,
  and a refusal writes neither file.

  Exit codes: 0 written (or already current) · 2 the worker's answer is not one this script
  will publish · 3 the worker could not be read · 4 the page has no single weeklyActive tile.

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -FromFile metrics-snapshot.json

.EXAMPLE
  pwsh -NoProfile -File scripts/landing-telemetry.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$Worker = 'https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev',
    [string]$FromFile,
    [string]$SiteDir,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# The one tile this script owns. A second telemetry figure on the hero is a new SIGN, not
# a new row here — the rest of the figures stay on the worker's own /report.
$Key = 'weeklyActive'

$TilePattern = '(<div\s+class="n"\s+data-metric="' + $Key + '">)([^<]*)(</div>)'

function New-Result([int]$Code, [string]$Message) {
    [pscustomobject]@{ Code = $Code; Message = $Message }
}

# Two-space indent and LF, which is what the committed file already is — so a round trip
# of an unchanged file is byte-identical, and a refresh diff shows only what moved.
function Format-MetricsJson($Doc) {
    (($Doc | ConvertTo-Json -Depth 8) -replace "`r`n", "`n") + "`n"
}

function Read-Worker([string]$Base, [string]$File) {
    if ($File) {
        if (-not (Test-Path -LiteralPath $File)) { return New-Result 3 "no such file: $File" }
        return [pscustomobject]@{ Code = 0; Text = [IO.File]::ReadAllText($File, $Utf8NoBom); Source = $File }
    }
    $url = $Base.TrimEnd('/') + '/metrics.json'
    try {
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 20 -SkipHttpErrorCheck -UseBasicParsing
    }
    catch {
        return New-Result 3 "could not read $url - $($_.Exception.Message)"
    }
    if ($response.StatusCode -ne 200) {
        return New-Result 3 "$url answered HTTP $($response.StatusCode), not 200"
    }
    $content = $response.Content
    if ($content -is [byte[]]) { $content = $Utf8NoBom.GetString($content) }
    [pscustomobject]@{ Code = 0; Text = [string]$content; Source = $url }
}

function Invoke-LandingTelemetry([string]$Base, [string]$File, [string]$Site) {
    $metricsPath = Join-Path $Site 'metrics.json'
    $pagePath = Join-Path $Site 'index.html'
    foreach ($p in @($metricsPath, $pagePath)) {
        if (-not (Test-Path -LiteralPath $p)) { return New-Result 4 "no $p" }
    }

    # The page first: with no tile there is nothing this script may write (the SIGN's hold).
    $page = [IO.File]::ReadAllText($pagePath, $Utf8NoBom)
    $tiles = [regex]::Matches($page, $TilePattern)
    if ($tiles.Count -eq 0) {
        return New-Result 4 ("site/index.html has no data-metric=`"$Key`" tile. The tile is HELD for the Founder's push-wide / public Evolved go (DRA-379 SIGN, Q4); " +
            'writing metrics.json alone would publish the number with no tile to carry its scope. Nothing written.')
    }
    if ($tiles.Count -gt 1) {
        return New-Result 4 "site/index.html has $($tiles.Count) data-metric=`"$Key`" tiles; one snapshot has one tile. Nothing written."
    }

    $read = Read-Worker $Base $File
    if ($read.Code -ne 0) { return New-Result $read.Code "$($read.Message). Nothing written." }

    try { $worker = $read.Text | ConvertFrom-Json -AsHashtable }
    catch { return New-Result 2 "the worker's answer is not JSON. Nothing written." }
    if ($worker -isnot [System.Collections.IDictionary]) { return New-Result 2 "the worker's answer is not a JSON object. Nothing written." }

    if (-not $worker.Contains('schema') -or $worker['schema'] -isnot [long] -or $worker['schema'] -ne 1) {
        return New-Result 2 "the worker's schema is not 1; this script reads schema 1 only. Nothing written."
    }
    if (-not $worker.Contains($Key)) { return New-Result 2 "the worker published no $Key. Nothing written." }
    $value = $worker[$Key]
    # ConvertFrom-Json gives a JSON integer as [long] and 1.5 or 1.0 as [double]; a string,
    # null or a double is not a count of installs.
    if ($value -isnot [long] -or $value -lt 0 -or $value -gt [int]::MaxValue) {
        return New-Result 2 "the worker's $Key is '$value', not a non-negative integer. Nothing written."
    }
    $definitions = $worker['definitions']
    $definition = if ($definitions -is [System.Collections.IDictionary] -and $definitions.Contains($Key)) { [string]$definitions[$Key] } else { '' }
    if ([string]::IsNullOrWhiteSpace($definition)) {
        return New-Result 2 "the worker published no definitions.$Key; a figure is printed beside its own definition (TEL-003) or not at all. Nothing written."
    }
    $generatedAt = [datetimeoffset]::MinValue
    if (-not $worker.Contains('generatedAt') -or
        -not [datetimeoffset]::TryParse([string]$worker['generatedAt'], [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$generatedAt)) {
        return New-Result 2 "the worker's generatedAt is missing or unreadable; a snapshot without its date is not one. Nothing written."
    }
    $date = $generatedAt.UtcDateTime.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)

    $metricsText = [IO.File]::ReadAllText($metricsPath, $Utf8NoBom)
    $metrics = $metricsText | ConvertFrom-Json -AsHashtable
    if (-not $metrics.Contains('scope') -or $metrics['scope'] -isnot [System.Collections.IDictionary]) {
        return New-Result 4 'site/metrics.json has no scope object. Nothing written.'
    }

    $base = $Base.TrimEnd('/')
    $scope = "Playing this week: $($definition.Trim()) Opt-in installs only, so a lower bound on the people playing; " +
        "telemetry is off unless the player turns it on. Snapshot of $base/metrics.json generated $date, " +
        "written by scripts/landing-telemetry.ps1; every other figure is on $base/report."

    $before = if ($metrics.Contains($Key)) { $metrics[$Key] } else { $null }
    $metrics[$Key] = [int]$value
    $metrics['scope'][$Key] = $scope
    $newMetrics = Format-MetricsJson $metrics

    $painted = ([int]$value).ToString('N0', [Globalization.CultureInfo]::InvariantCulture)
    $oldPainted = $tiles[0].Groups[2].Value
    $newPage = $page.Substring(0, $tiles[0].Groups[2].Index) + $painted +
        $page.Substring($tiles[0].Groups[2].Index + $tiles[0].Groups[2].Length)

    # Both texts are built before either file is touched, so a refusal above writes nothing.
    if ($newMetrics -ceq $metricsText -and $newPage -ceq $page) {
        return New-Result 0 "already current: $Key $painted as of $date ($($read.Source))."
    }
    [IO.File]::WriteAllText($metricsPath, $newMetrics, $Utf8NoBom)
    [IO.File]::WriteAllText($pagePath, $newPage, $Utf8NoBom)
    New-Result 0 ("wrote $Key $(if ($null -eq $before) { '(absent)' } else { $before }) -> $value in metrics.json and '$oldPainted' -> '$painted' on the tile, " +
        "snapshot $date, from $($read.Source). Review the diff and open a site/** PR; this script commits nothing.")
}

# ---------------------------------------------------------------------------
# -SelfTest: offline. Fixture sites in a temp dir, fixture worker answers from files or a
# one-shot loopback server, and one socket goes to a port nobody is listening on.
# Every refusal arm is fired here (trap 78), and the live page is COPIED before it is read,
# so the self-test cannot write the repo's site/ even on the day the tile exists.
# ---------------------------------------------------------------------------
function Invoke-SelfTest {
    $script:checks = 0
    $script:failures = 0
    function Check([string]$Label, [bool]$Ok) {
        $script:checks++
        if ($Ok) { Write-Host "  [ok]   $Label" }
        else { $script:failures++; Write-Host "  [FAIL] $Label" }
    }

    $root = Join-Path ([IO.Path]::GetTempPath()) ("landing-telemetry-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $metricsFixture = @'
{
  "asOf": "2026-09-24",
  "questsTracked": 1173,
  "downloads": 37676,
  "scope": {
    "questsTracked": "Count of the quests array.",
    "downloads": "EQBuddy Downloads — all versions."
  }
}

'@.TrimEnd() + "`n"
        $tile = '<div class="kpi"><div class="n" data-metric="weeklyActive">0</div><div class="l">Playing this week</div></div>'
        $pageWithTile = "<div class=`"kpis`" id=`"hero-kpis`">`n  $tile`n</div>`n"
        $pageNoTile = "<div class=`"kpis`" id=`"hero-kpis`">`n</div>`n"

        function New-Site([string]$Name, [string]$Page, [string]$Metrics = $metricsFixture) {
            $dir = Join-Path $root $Name
            New-Item -ItemType Directory -Path $dir | Out-Null
            [IO.File]::WriteAllText((Join-Path $dir 'index.html'), $Page, $Utf8NoBom)
            [IO.File]::WriteAllText((Join-Path $dir 'metrics.json'), $Metrics, $Utf8NoBom)
            $dir
        }
        function New-Answer([string]$Name, [string]$Json) {
            $path = Join-Path $root "$Name.json"
            [IO.File]::WriteAllText($path, $Json, $Utf8NoBom)
            $path
        }
        function Hash([string]$Dir) {
            (Get-ChildItem -LiteralPath $Dir -File | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash }) -join '|'
        }
        function Answer([object]$Weekly, [string]$Definition = 'Distinct opted-in installs in the last 7 complete UTC days.', [object]$Schema = 1) {
            $w = if ($null -eq $Weekly) { 'null' } else { $Weekly }
            $s = if ($null -eq $Schema) { 'null' } else { $Schema }
            $defs = if ($null -eq $Definition) { '{}' } else { '{"weeklyActive":' + (ConvertTo-Json $Definition) + '}' }
            '{"schema":' + $s + ',"generatedAt":"2026-09-26T03:30:55Z","weeklyActive":' + $w + ',"definitions":' + $defs + '}'
        }
        $base = 'https://worker.example'

        # --- the writer: both halves together, and nothing else moves --------------------
        $site = New-Site 'happy' $pageWithTile
        $r = Invoke-LandingTelemetry $base (New-Answer 'happy' (Answer 12345)) $site
        Check 'a good answer is written (exit 0)' ($r.Code -eq 0)
        $m = [IO.File]::ReadAllText((Join-Path $site 'metrics.json'), $Utf8NoBom) | ConvertFrom-Json -AsHashtable
        Check 'metrics.json carries the figure as a flat top-level integer' ($m['weeklyActive'] -is [long] -and $m['weeklyActive'] -eq 12345)
        $sc = [string]$m['scope']['weeklyActive']
        Check 'the scope sentence quotes the worker''s definition verbatim' ($sc.Contains('Distinct opted-in installs in the last 7 complete UTC days.'))
        Check 'the scope sentence says opt-in and lower bound' ($sc.Contains('Opt-in') -and $sc.Contains('lower bound'))
        Check 'the scope sentence carries the snapshot date (generatedAt, UTC)' ($sc.Contains('generated 2026-09-26'))
        Check 'the scope sentence names /report for the other figures' ($sc.Contains("$base/report"))
        Check 'the tile paints the same number, comma-formatted' ([IO.File]::ReadAllText((Join-Path $site 'index.html')).Contains('data-metric="weeklyActive">12,345</div>'))
        Check 'the keys it does not own are untouched' ($m['asOf'] -eq '2026-09-24' -and $m['questsTracked'] -eq 1173 -and $m['downloads'] -eq 37676 -and
            $m['scope']['downloads'] -eq 'EQBuddy Downloads — all versions.')
        $h = Hash $site
        $r = Invoke-LandingTelemetry $base (New-Answer 'happy' (Answer 12345)) $site
        Check 'a second run with the same answer is a no-op' ($r.Code -eq 0 -and $r.Message.StartsWith('already current') -and (Hash $site) -eq $h)

        # The serializer does not churn the committed file: a round trip of the real
        # site/metrics.json through it is byte-identical, so a refresh diff shows only what moved.
        $real = [IO.File]::ReadAllText((Join-Path $RepoRoot 'site/metrics.json'), $Utf8NoBom)
        Check 'the real site/metrics.json round-trips byte-identical' ((Format-MetricsJson ($real | ConvertFrom-Json -AsHashtable)) -ceq $real)

        # --- the hold is structural ----------------------------------------------------------
        $site = New-Site 'notile' $pageNoTile
        $h = Hash $site
        $r = Invoke-LandingTelemetry $base (New-Answer 'good' (Answer 1)) $site
        Check 'no weeklyActive tile: refused (exit 4), nothing written' ($r.Code -eq 4 -and (Hash $site) -eq $h -and $r.Message.Contains('HELD'))
        $site = New-Site 'twotiles' ($pageWithTile + $tile)
        $h = Hash $site
        $r = Invoke-LandingTelemetry $base (New-Answer 'good' (Answer 1)) $site
        Check 'two weeklyActive tiles: refused (exit 4), nothing written' ($r.Code -eq 4 -and (Hash $site) -eq $h)

        # The page as committed. Copied, so this arm cannot write the repo even the day the
        # tile lands; on that day it flips, which is the held PR's to update.
        $site = New-Site 'live' ([IO.File]::ReadAllText((Join-Path $RepoRoot 'site/index.html'), $Utf8NoBom)) $real
        $h = Hash $site
        $r = Invoke-LandingTelemetry $base (New-Answer 'good' (Answer 1)) $site
        Check 'the committed page has no weeklyActive tile, so the writer refuses it today (DRA-379 Q4 hold)' ($r.Code -eq 4 -and (Hash $site) -eq $h)

        # --- never freeze an absence (trap 81) ------------------------------------------------
        $refusals = [ordered]@{
            'schema 2'                         = (Answer 1 -Schema 2)
            'schema missing'                   = '{"generatedAt":"2026-09-26T03:30:55Z","weeklyActive":1,"definitions":{"weeklyActive":"x"}}'
            'schema as a string'               = (Answer 1 -Schema '"1"')
            'weeklyActive null'                = (Answer $null)
            'weeklyActive negative'            = (Answer -1)
            'weeklyActive fractional'          = (Answer 1.5)
            'weeklyActive as a string'         = (Answer '"1"')
            'weeklyActive absent'              = '{"schema":1,"generatedAt":"2026-09-26T03:30:55Z","definitions":{"weeklyActive":"x"}}'
            'no definitions.weeklyActive'      = (Answer 1 -Definition $null)
            'a blank definition'               = (Answer 1 -Definition '  ')
            'no generatedAt'                   = '{"schema":1,"weeklyActive":1,"definitions":{"weeklyActive":"x"}}'
            'not JSON'                         = '<html>502 Bad Gateway</html>'
            'a JSON array'                     = '[1,2]'
        }
        foreach ($name in $refusals.Keys) {
            $site = New-Site ("refuse-" + [guid]::NewGuid().ToString('N')) $pageWithTile
            $h = Hash $site
            $r = Invoke-LandingTelemetry $base (New-Answer ("a-" + [guid]::NewGuid().ToString('N')) $refusals[$name]) $site
            Check "$name`: refused (exit 2), nothing written" ($r.Code -eq 2 -and (Hash $site) -eq $h)
        }

        # A REAL socket to a port nobody is listening on: the unreachable path is exit 3, and
        # it writes nothing — the shape that froze a baseline in trap 81.
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        $site = New-Site 'deadport' $pageWithTile
        $h = Hash $site
        $r = Invoke-LandingTelemetry "http://127.0.0.1:$port" $null $site
        Check 'an unreachable worker: refused (exit 3), nothing written' ($r.Code -eq 3 -and (Hash $site) -eq $h)
        # A loopback server that answers ONE request with a canned response, so the HTTP arms
        # are exercised through Invoke-WebRequest itself: a 503 is exit 3 and writes nothing,
        # and a 200 with a good body is written — the fetch path works end to end.
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
        $srv = Serve-Once '503 Service Unavailable' (Answer 7)
        $site = New-Site 'http503' $pageWithTile
        $h = Hash $site
        $r = Invoke-LandingTelemetry $srv.Url $null $site
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a worker answering HTTP 503 (even with a good body): refused (exit 3), nothing written' ($r.Code -eq 3 -and (Hash $site) -eq $h)
        $srv = Serve-Once '200 OK' (Answer 7)
        $site = New-Site 'http200' $pageWithTile
        $r = Invoke-LandingTelemetry $srv.Url $null $site
        $null = $srv.Job | Wait-Job -Timeout 10; $srv.Job | Remove-Job -Force
        Check 'a worker answering HTTP 200 over the network path is written' ($r.Code -eq 0 -and
            [IO.File]::ReadAllText((Join-Path $site 'index.html')).Contains('data-metric="weeklyActive">7</div>'))

        $site = New-Site 'nofile' $pageWithTile
        $h = Hash $site
        $r = Invoke-LandingTelemetry $base (Join-Path $root 'does-not-exist.json') $site
        Check 'a missing -FromFile: refused (exit 3), nothing written' ($r.Code -eq 3 -and (Hash $site) -eq $h)
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($script:checks -lt 30) { Write-Host "FAIL: only $script:checks landing-telemetry self-test checks ran (trap 78)."; return 1 }
    if ($script:failures -gt 0) {
        Write-Host "FAIL: $script:failures of $script:checks landing-telemetry self-test checks failed."
        return 1
    }
    Write-Host "OK: $script:checks landing-telemetry self-test checks passed (offline)."
    0
}

if ($SelfTest) { exit (Invoke-SelfTest) }

if (-not $SiteDir) { $SiteDir = Join-Path $RepoRoot 'site' }
$result = Invoke-LandingTelemetry $Worker $FromFile $SiteDir
if ($result.Code -eq 0) { Write-Host "OK: $($result.Message)" }
else { Write-Host "REFUSED (exit $($result.Code)): $($result.Message)" }
exit $result.Code
