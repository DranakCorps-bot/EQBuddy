<#
.SYNOPSIS
    What's-new entries that have already shipped are HISTORY. This proves nobody edited one.

.DESCRIPTION
    Twice in three releases a session wrote its work into the What's-new entry of a version
    that was ALREADY TAGGED (v1.99.14 was the second; the repair had to be verified
    byte-exact against the tag). Both times everything else was green: the file is valid
    JSON, `release.ps1` found an entry for the version it was releasing, and every unit test
    passed — because the defect is not IN the file, it is a disagreement between the file and
    a git tag, which no unit test can see. Fable filed the guard as a V1 follow-up on the
    v1.99.15 review for exactly that reason: it needs git-tag knowledge, so it belongs here.

    Two costs when it happens. The words a player saw in the popup for 1.99.14 stop matching
    what the repo says 1.99.14 said. And worse in the other direction: work that belongs to
    the release being prepared gets filed under a version that already shipped, so it never
    appears in ANY popup — the "every player-noticeable change needs an entry in the release
    that ships it" rule, defeated silently.

    The check is one `git show` rather than one per tag: the newest tag's copy of the file
    already contains every older entry, so asserting that the whole of it survives unchanged
    in the working copy covers the entire history at once. New entries may be prepended;
    nothing that shipped may move, change or disappear.

    Exits non-zero on a violation. Skips (exit 0, with a note) when there is no git, no tag,
    or no reachable copy of the file at the tag — a fresh or shallow clone must not fail.

.EXAMPLE
    pwsh -NoProfile -File scripts/whatsnew-guard.ps1
    pwsh -NoProfile -File scripts/whatsnew-guard.ps1 -Releasing   # also: the tag must not exist yet
#>
[CmdletBinding()]
param([switch] $Releasing)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$rel = 'src/EQBuddy.Core/Data/WhatsNew.json'
$problems = @()

$props = Get-Content (Join-Path $repo 'Directory.Build.props') -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]

$current = Get-Content (Join-Path $repo $rel) -Raw | ConvertFrom-Json
if ($current.Count -lt 1) { throw "$rel has no entries" }

# The entry a release ships is the one at the top, and `release.ps1` searches by version
# rather than position — so an entry written under the wrong heading still satisfies it.
if ($current[0].version -ne $version) {
    $problems += "The top What's-new entry is $($current[0].version) but Directory.Build.props says $version. " +
                 "Newest entry goes first, and it belongs to the version being built."
}

# The entries carry em dashes, arrows and ✦. PowerShell decodes a native command's stdout
# with [Console]::OutputEncoding, which is the OEM code page here — so every non-ASCII
# character came back mangled and the FIRST version of this guard reported 111 of 129
# shipped entries as edited, on a tree `git diff` says is byte-identical to the tag. A guard
# that cries wolf is worth less than no guard, so the comparison reads the blob as UTF-8.
function Invoke-GitUtf8 {
    param([string[]] $Arguments)
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $repo @Arguments 2>$null
    }
    finally { [Console]::OutputEncoding = $prev }
}

function Get-Tags {
    $out = Invoke-GitUtf8 @('tag', '--list', 'v*')
    if ($LASTEXITCODE -ne 0) { return @() }
    @($out) | Where-Object { $_ -match '^v[\d.]+$' }
}

$tags = Get-Tags
if ($tags.Count -eq 0) {
    Write-Host "whatsnew-guard: no v* tags reachable - nothing shipped to compare against, skipping." -ForegroundColor DarkGray
    exit 0
}

if ($Releasing -and ($tags -contains "v$version")) {
    Write-Host "whatsnew-guard: v$version is ALREADY TAGGED." -ForegroundColor Red
    Write-Host "   Bump <Version> in Directory.Build.props and add a NEW What's-new entry;" -ForegroundColor Red
    Write-Host "   do not reuse a shipped version's heading." -ForegroundColor Red
    exit 1
}

# Newest by version order, not by string order: v1.99.9 must not beat v1.99.16.
$newest = ($tags | Sort-Object { [version]($_.Substring(1)) } | Select-Object -Last 1)

$shippedRaw = Invoke-GitUtf8 @('show', "${newest}:$rel")
if ($LASTEXITCODE -ne 0 -or -not $shippedRaw) {
    Write-Host "whatsnew-guard: cannot read $rel at $newest - skipping." -ForegroundColor DarkGray
    exit 0
}
$shipped = ($shippedRaw -join "`n") | ConvertFrom-Json

$byVersion = @{}
foreach ($e in $current) { $byVersion[$e.version] = $e }

foreach ($old in $shipped) {
    $now = $byVersion[$old.version]
    if (-not $now) {
        $problems += "$($old.version) shipped in $newest and its entry is GONE from $rel."
        continue
    }
    if ($now.date -ne $old.date) {
        $problems += "$($old.version): date changed since $newest ('$($old.date)' -> '$($now.date)')."
    }
    $a = @($old.highlights); $b = @($now.highlights)
    if ($a.Count -ne $b.Count) {
        $problems += "$($old.version): $($a.Count) highlight(s) in $newest, $($b.Count) now. A shipped entry is a record of what players were told."
        continue
    }
    for ($i = 0; $i -lt $a.Count; $i++) {
        if ($a[$i] -cne $b[$i]) {
            $problems += "$($old.version): highlight $($i + 1) was edited after it shipped. Restore it with: git show ${newest}:$rel"
            break
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host "whatsnew-guard: FAILED (compared against $newest)" -ForegroundColor Red
    # Every line carries the prefix, because check.ps1 shows only the lines its filter
    # matches — a headline with the reasons filtered out is the shape of a gate that says
    # something is wrong and not what.
    foreach ($p in $problems) { Write-Host "whatsnew-guard:    $p" -ForegroundColor Red }
    exit 1
}

Write-Host "whatsnew-guard: ok  ($($shipped.Count) shipped entries unchanged since $newest; top entry $version)" -ForegroundColor Green
exit 0
