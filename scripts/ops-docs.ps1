<#
.SYNOPSIS
    Light ops-doc report: CLAUDE.md size vs archive, flake-ledger shape.

.DESCRIPTION
    Not a CI gate and not part of check.ps1. Soft uses it to see whether the
    live/archive split is still doing its job. A missing file fails the
    script; a large live CLAUDE.md only prints. There is no word-count ratchet.

.EXAMPLE
    pwsh -NoProfile -File scripts/ops-docs.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$claude = Join-Path $repo 'CLAUDE.md'
$archive = Join-Path $repo 'docs/ops/claude-archive/claude-2026-09-08.md'
$traps = Join-Path $repo 'docs/ops/claude-archive/traps.md'
$ladder = Join-Path $repo 'docs/ops/verification-ladder.md'
$ledger = Join-Path $repo 'docs/ops/flake-ledger.md'

foreach ($p in @($claude, $archive, $traps, $ladder, $ledger)) {
    if (-not (Test-Path -LiteralPath $p)) {
        Write-Error "missing $p"
        exit 1
    }
}

function Get-ByteCount([string] $path) {
    (Get-Item -LiteralPath $path).Length
}

$live = Get-ByteCount $claude
$snap = Get-ByteCount $archive
Write-Host ("CLAUDE.md (live)     {0,8:N0} bytes" -f $live)
Write-Host ("archive snapshot     {0,8:N0} bytes" -f $snap)
Write-Host ("traps novels         {0,8:N0} bytes" -f (Get-ByteCount $traps))
if ($snap -gt 0) {
    $pct = [math]::Round(100.0 * $live / $snap, 1)
    Write-Host ("live / snapshot      {0}%" -f $pct)
}

$text = Get-Content -LiteralPath $ledger -Raw
foreach ($col in @('| Signature |', '| Occurrences |', '| Affected test |', '| Environment |', '| Disposition |')) {
    if ($text.IndexOf($col) -lt 0) {
        Write-Error "flake-ledger.md missing column header $col"
        exit 1
    }
}
if ($text -notmatch 'Passed on rerun') {
    Write-Error "flake-ledger.md no longer states the rerun rule"
    exit 1
}

Write-Host 'flake-ledger.md      columns + rerun rule ok'
Write-Host 'not a CI gate'
