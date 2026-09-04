<#
.SYNOPSIS
    A 2.x release must tell Linux and macOS players what happened to their build.

.DESCRIPTION
    Charter LEGACY-007 (#275): the first v2 release notes and the README carry a visible
    "Legacy Linux/macOS" section linking to the final v1 release. That is a promise made
    to people who will never see it in the app — the in-app notice only reaches installs
    that took the bridge release, so anyone still on an older 1.x build finds us through
    the release page and the README, and those are the only two places that can reach
    them. A promise whose only enforcement is the memory of the session that made it is
    the shape every stale line in this repo started as.

    Three things are checked. Two hold at EVERY version, because they are cheap and they
    are about a page that already exists:

      1. LEGACY-V1.md names all three non-Windows assets, so the page a legacy user is
         sent to actually tells them what to download.
      2. No LINK on that page (or in the README's Legacy section) targets
         `releases/latest`. That URL becomes the v2 release page the moment v2 ships and
         its most prominent asset is a Windows installer - a legacy page pointing there
         would look correct in every screenshot and hand a Mac user something that cannot
         run. Prose may still discuss `releases/latest`; only link targets are checked.

    The third applies once <Version> reaches 2.x:

      3. The README carries a visible "Legacy Linux/macOS" heading whose section links to
         a pinned v1 release, and - on the FIRST 2.x release only - the What's-new entry
         being shipped carries the same section, because `release.ps1` builds the GitHub
         release body out of those highlights. Later 2.x patches are not asked to repeat
         it; a line written to satisfy a guard every release is a line players stop
         reading.

    Exits non-zero on a violation. Today's tree is 1.99.x, so check 3 does not fire yet;
    that is exactly the state in which a guard quietly stops being able to fail, so it
    takes -AssumeVersion and -Repo purely so it can be PROVEN to fail (traps 34, 39).

    Files are read with [IO.File]::ReadAllText rather than Get-Content: Windows
    PowerShell 5.1 decodes with the ANSI code page and this repo's docs are full of em
    dashes. Git output is read through an explicit UTF-8 [Console]::OutputEncoding for
    the same reason (trap 54) - even though a tag list is ASCII, the next person to add a
    git call here should find the wrapper already in place.

.EXAMPLE
    pwsh -NoProfile -File scripts/legacy-notice-guard.ps1
    pwsh -NoProfile -File scripts/legacy-notice-guard.ps1 -AssumeVersion 2.0.0   # prove it fails
#>
[CmdletBinding()]
param(
    # Verification hooks. Both exist so the guard can be shown to fail on a tree where it
    # would otherwise be permanently silent; neither is used by check.ps1 or release.ps1.
    [string] $AssumeVersion,
    [string] $Repo
)

$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }
$problems = @()

function Read-Utf8([string] $relative) {
    $path = Join-Path $Repo $relative
    if (-not (Test-Path $path)) { return $null }
    [IO.File]::ReadAllText($path)
}

function Invoke-GitUtf8([string[]] $Arguments) {
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $Repo @Arguments 2>$null
    }
    finally { [Console]::OutputEncoding = $prev }
}

# Markdown link targets only: ](...) and <...>. A sentence explaining why we do NOT use
# releases/latest is the whole point of the page and must not trip its own guard.
function Get-LinkTargets([string] $text) {
    $targets = @()
    foreach ($m in [regex]::Matches($text, '\]\(([^)\s]+)')) { $targets += $m.Groups[1].Value }
    foreach ($m in [regex]::Matches($text, '<(https?://[^>\s]+)>')) { $targets += $m.Groups[1].Value }
    $targets
}

$version = $AssumeVersion
if (-not $version) {
    $props = Read-Utf8 'Directory.Build.props'
    if (-not $props) { throw 'No Directory.Build.props at the repo root' }
    if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
    $version = $Matches[1]
}
$major = [int]($version.Split('.')[0])

# ---- 1 and 2: the legacy page itself, at every version -----------------------------

$legacy = Read-Utf8 'LEGACY-V1.md'
if (-not $legacy) {
    $problems += 'LEGACY-V1.md is missing. It is the public support matrix and every other doc links to it.'
}
else {
    foreach ($asset in @('EQBuddy-linux-x64.tar.gz', 'EQBuddy-osx-arm64.zip', 'EQBuddy-osx-x64.zip')) {
        if ($legacy -notmatch [regex]::Escape($asset)) {
            $problems += "LEGACY-V1.md does not name $asset. All three non-Windows assets belong on the page a legacy user is sent to."
        }
    }
    foreach ($t in Get-LinkTargets $legacy) {
        if ($t -match 'releases/latest') {
            $problems += "LEGACY-V1.md links to $t - releases/latest IS the v2 release page once v2 ships. Pin the tag."
        }
    }
}

# ---- 3: the README section and the release notes, from 2.x on ----------------------

$readme = Read-Utf8 'README.md'
if (-not $readme) { $problems += 'README.md is missing.' }

$headingRx = '(?m)^#{1,3}\s+.*Legacy\s+Linux\s*/\s*macOS.*$'
$section = $null
if ($readme) {
    $m = [regex]::Match($readme, $headingRx)
    if ($m.Success) {
        # The section runs to the next heading of the same or higher level.
        $rest = $readme.Substring($m.Index + $m.Length)
        $next = [regex]::Match($rest, '(?m)^#{1,3}\s')
        $section = if ($next.Success) { $rest.Substring(0, $next.Index) } else { $rest }
    }
}

if ($section) {
    foreach ($t in Get-LinkTargets $section) {
        if ($t -match 'releases/latest') {
            $problems += "The README's Legacy Linux/macOS section links to $t. Pin the final v1 tag instead."
        }
    }
}

if ($major -ge 2) {
    if (-not $section) {
        $problems += 'README.md has no "Legacy Linux/macOS" heading. LEGACY-007: a 2.x release says in the README what happened to the Linux and macOS builds.'
    }
    elseif ($section -notmatch 'releases/(tag|download)/v1\.') {
        $problems += 'The README''s Legacy Linux/macOS section does not link to a pinned v1 release (releases/tag/v1.x or releases/download/v1.x).'
    }

    # The GitHub release body is the shipping What's-new entry's highlights (release.ps1).
    # No git, or a git that cannot answer, means STRICT rather than skip: the cost of
    # asking for the section twice is one line of notes, and the cost of skipping it is
    # the promise going unenforced on the one release it was written for.
    $gitOk = $true
    $tagOut = @()
    try { $tagOut = @(Invoke-GitUtf8 @('tag', '--list', 'v2*')); $gitOk = ($LASTEXITCODE -eq 0) }
    catch { $gitOk = $false }
    $v2Tags = @($tagOut | Where-Object { $_ -match '^v2($|[\d.])' })
    $firstV2 = (-not $gitOk) -or ($v2Tags.Count -eq 0)
    $why = if (-not $gitOk) { 'git could not be read, so this is treated as the first 2.x release' }
           else { 'this is the first 2.x release' }

    if ($firstV2) {
        $whatsNewRaw = Read-Utf8 'src/EQBuddy.Core/Data/WhatsNew.json'
        if (-not $whatsNewRaw) { $problems += 'src/EQBuddy.Core/Data/WhatsNew.json is missing.' }
        else {
            $entry = ($whatsNewRaw | ConvertFrom-Json) | Where-Object { $_.version -eq $version } | Select-Object -First 1
            if (-not $entry) {
                $problems += "No What's-new entry for $version, so the GitHub release notes would carry nothing at all."
            }
            else {
                $notes = ($entry.highlights -join "`n")
                if ($notes -notmatch 'Legacy\s+Linux\s*/\s*macOS') {
                    $problems += "The $version What's-new entry carries no ""Legacy Linux/macOS"" section, and $why. " +
                                 'Those highlights ARE the GitHub release body (release.ps1 --notes-file).'
                }
                if ($notes -notmatch 'releases/(tag|download)/v1\.') {
                    $problems += "The $version What's-new entry does not link to the final v1 release (releases/tag/v1.x)."
                }
                foreach ($t in Get-LinkTargets $notes) {
                    if ($t -match 'releases/latest') {
                        $problems += "The $version What's-new entry links to $t. That is the v2 page; pin the final v1 tag."
                    }
                }
            }
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host "legacy-notice-guard: FAILED (version $version)" -ForegroundColor Red
    # Every line carries the prefix: check.ps1 prints only the lines its filter matches,
    # and a headline without its reasons is a gate that says something is wrong and not what.
    foreach ($p in $problems) { Write-Host "legacy-notice-guard:    $p" -ForegroundColor Red }
    exit 1
}

$scope = if ($major -ge 2) { 'README section + release notes checked' } else { "1.x - LEGACY-007 notes check arms at 2.x" }
Write-Host "legacy-notice-guard: ok  (version $version; $scope)" -ForegroundColor Green
exit 0
