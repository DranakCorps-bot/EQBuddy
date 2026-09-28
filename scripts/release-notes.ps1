<#
.SYNOPSIS
    The GitHub release body for one version, built from its WhatsNew.json highlights.
.DESCRIPTION
    release.ps1 used to join every highlight into the body. 2.0.0 carries 92 of them —
    everything since v1.99.18 — and the joined text is 135,238 characters, over GitHub's
    125,000-character release-body limit. `gh release create` would have failed AFTER the
    tag was pushed and every artifact signed (2026-09-28, caught before tagging).

    So when the full body will not fit, it is CONDENSED — and only then; a release that
    fits keeps every word:
      * In full: the first three highlights (the release's own headline items), any
        highlight carrying the "Legacy Linux/macOS" section (legacy-notice-guard reads the
        entry as the release body, so that section must survive whole), and every
        highlight that credits a reporter by #number (reporters are credited by name and
        number — CLAUDE.md).
      * Everything else: its opening sentence, under "Also in this release".
    The in-app What's-new window is untouched and always shows every highlight in full.

    Prints the body to stdout. -Check prints only the sizes (for a dry run).
.EXAMPLE
    pwsh -NoProfile -File scripts/release-notes.ps1 -Version 2.0.0 -Check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $Repo,
    [switch] $Check
)
$ErrorActionPreference = 'Stop'
if (-not $Repo) { $Repo = Split-Path $PSScriptRoot -Parent }

# GitHub refuses a body over 125,000 characters; stay well clear of it.
$Budget = 120000

$raw = [IO.File]::ReadAllText((Join-Path $Repo 'src\EQBuddy.Core\Data\WhatsNew.json'))
$entry = ($raw | ConvertFrom-Json) | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $entry) { throw "No What's-new entry for $Version." }
$highlights = @($entry.highlights | ForEach-Object { [string]$_ })

$heading = "## What's new in $Version`n`n"
$full = $heading + (($highlights | ForEach-Object { "- $_" }) -join "`n") + "`n"

function Get-Headline([string] $text) {
    $m = [regex]::Match($text, '[.?!](?=\s)')
    $s = if ($m.Success) { $text.Substring(0, $m.Index + 1) } else { $text }
    if ($s.Length -gt 240) {
        $cut = $s.Substring(0, 240)
        $s = $cut.Substring(0, [Math]::Max($cut.LastIndexOf(' '), 1)) + '…'
    }
    $s
}

if ($full.Length -le $Budget) {
    $body = $full
    $mode = 'full'
}
else {
    $keep = @(); $rest = @()
    for ($i = 0; $i -lt $highlights.Count; $i++) {
        $h = $highlights[$i]
        if ($i -lt 3 -or $h -match 'Legacy\s+Linux\s*/\s*macOS' -or $h -match '#\d+') { $keep += $h }
        else { $rest += (Get-Headline $h) }
    }
    $body = $heading + (($keep | ForEach-Object { "- $_" }) -join "`n") +
        "`n`n### Also in this release`n`n" + (($rest | ForEach-Object { "- $_" }) -join "`n") +
        "`n`n_Every change above is described in full in EQBuddy's own What's new window._`n"
    $mode = "condensed ($($keep.Count) in full, $($rest.Count) as headlines)"
    if ($body.Length -gt $Budget) { throw "Condensed release notes for $Version are still $($body.Length) characters (budget $Budget)." }
}

if ($Check) {
    Write-Host "release-notes: $Version - $($highlights.Count) highlights, full $($full.Length) chars, body $($body.Length) chars, $mode"
    exit 0
}
$body
