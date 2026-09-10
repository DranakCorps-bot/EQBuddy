<#
.SYNOPSIS
    Drive scripts/channel-wipe-guard.ps1 into the red once per check, and prove it stays
    green on the things it must not fire on.

.DESCRIPTION
    A guard is only as good as its worst false negative, and a guard nobody has watched
    FAIL is a green light with no bulb behind it (trap 34). This builds a throwaway git
    repository in TEMP - never this repo, never the real channel files - seeds it with
    fixture ledgers, and then does each destructive thing on purpose.

    Fifteen cases. Six of them are "must PASS" on purpose: the useful half of a guard
    like this is the workflows it does NOT interrupt, and every one of the passes below
    corresponds to a real commit in this repo's history that must keep landing (a drained
    inbox, a lifted hold, an encoding repair, an archive move).

    The marker-array bug this suite would have caught, and did: the mojibake check was
    written as `@([char]0xE2 + [char]0x20AC, [char]0xC3 + [char]0xA2)`, and PowerShell
    binds `,` TIGHTER than `+`, so the list collapsed into one string that matches
    nothing. The guard reported a clean file for the commit that quadrupled the
    corruption. It was green, and it was measuring nothing.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-wipe-guard-selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'channel-wipe-guard.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-channel-guard-selftest-" + [guid]::NewGuid().ToString('N'))

$failed = @()
$script:step = 0

# Built from code points for the same reason the guard is: a literal mojibake glyph in
# this file is the one string a host that guesses the encoding cannot be trusted with.
$MojiEmDash = [string][char]0x00E2 + [string][char]0x20AC + [string][char]0x201D

function New-Utf8File([string] $path, [string] $text) {
    $dir = Split-Path $path -Parent
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

function Get-LedgerText([int] $entries, [string] $tag) {
    $sb = [Text.StringBuilder]::new()
    [void]$sb.AppendLine("# $tag")
    for ($i = 1; $i -le $entries; $i++) {
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("## 2026-09-0$([int]($i % 9) + 1) ~$i" + ":00 PM CT " + [char]0x2014 + " $tag entry $i")
        [void]$sb.AppendLine("To: Claude")
        [void]$sb.AppendLine("- **Reinforcing:** entry $i of $tag says a specific thing worth keeping.")
        [void]$sb.AppendLine("- **Corrective:** entry $i names the evidence it rests on ($tag/$i).")
    }
    $sb.ToString()
}

function Invoke-Git([string[]] $Arguments) {
    $prev = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        & git -C $root @Arguments 2>&1 | Out-Null
    }
    finally { [Console]::OutputEncoding = $prev }
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed in the fixture repo" }
}

function Invoke-Guard {
    param([string[]] $GuardArgs)
    $out = & pwsh -NoProfile -File $guard -Repo $root @GuardArgs 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $out.Trim() }
}

function Assert-Result {
    param([string] $Name, [bool] $WantFail, [string] $Needle, [string[]] $GuardArgs = @())
    $script:step++
    $r = Invoke-Guard $GuardArgs
    $label = "$($script:step). $Name"
    $wanted = if ($WantFail) { 'refuse' } else { 'pass' }
    if ($WantFail -and $r.code -eq 0) {
        $script:failed += "$label - expected the guard to REFUSE, got exit 0: $($r.text)"
        return
    }
    if (-not $WantFail -and $r.code -ne 0) {
        $script:failed += "$label - expected the guard to PASS, got exit $($r.code): $($r.text)"
        return
    }
    if ($Needle -and ($r.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label - $wanted was correct but the reason is missing '$Needle': $($r.text)"
        return
    }
    Write-Host ("  {0,-58} {1}" -f $label, $wanted) -ForegroundColor DarkGray
}

# Every case starts from the committed base, so one case cannot leak into the next
# (trap 51: when staging is cumulative, reset is the contract).
function Reset-Tree {
    Invoke-Git @('reset', '--hard', '--quiet', 'HEAD')
    Invoke-Git @('clean', '-fdq')
}

$LEDGER = 'HELM-FEEDBACK.md'
$STATE = 'HELM.md'
$INBOX = 'FABLE.md'

try {
    New-Item -ItemType Directory -Force -Path $root | Out-Null
    Invoke-Git @('init', '--quiet', '-b', 'fixture')
    Invoke-Git @('config', 'user.email', 'selftest@example.invalid')
    Invoke-Git @('config', 'user.name', 'Channel Guard Selftest')
    Invoke-Git @('config', 'commit.gpgsign', 'false')

    # The full roster, so the guard walks the same shape it walks in the real repo.
    foreach ($f in @('HELM-FEEDBACK.md', 'FABLE-FEEDBACK.md', 'BEVEL-FEEDBACK.md',
            'SCRIBE-FEEDBACK.md', 'CLAUDE-FEEDBACK.md', 'DECISIONS.md')) {
        New-Utf8File (Join-Path $root $f) (Get-LedgerText 60 $f)
    }
    New-Utf8File (Join-Path $root $STATE) (Get-LedgerText 60 'HELM.md holds')
    foreach ($f in @('FABLE.md', 'BEVEL.md', 'SCRIBE.md', 'SCRIBE-TESTING.md')) {
        New-Utf8File (Join-Path $root $f) (Get-LedgerText 40 $f)
    }
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '--quiet', '-m', 'fixture base')
    $base = (& git -C $root rev-parse HEAD).Trim()
    $B = @('-BaseRef', $base)

    $ledgerPath = Join-Path $root $LEDGER
    $baseLedger = [IO.File]::ReadAllText($ledgerPath)

    Write-Host 'channel-wipe-guard selftest' -ForegroundColor Cyan

    # -- must PASS: the ordinary case, and the four legitimate shapes -----------------

    Assert-Result 'an untouched tree passes' $false 'channel files intact' $B

    Reset-Tree
    New-Utf8File $ledgerPath ($baseLedger + (Get-LedgerText 3 'a new signed entry'))
    Assert-Result 'appending to a ledger passes' $false 'channel files intact' $B

    Reset-Tree
    # "When you take an item, delete it" - a drained inbox is the documented workflow.
    New-Utf8File (Join-Path $root $INBOX) "# FABLE.md`n`nNothing outstanding.`n"
    Assert-Result 'draining an inbox to three lines passes' $false 'channel files intact' $B

    Reset-Tree
    # HELM.md is state: lifting holds legitimately removes them, so its tier is looser.
    $stateText = [IO.File]::ReadAllText((Join-Path $root $STATE))
    $keepChars = [int]($stateText.Length * 0.78)
    New-Utf8File (Join-Path $root $STATE) $stateText.Substring(0, $keepChars)
    Assert-Result 'lifting holds from HELM.md (state tier) passes' $false 'channel files intact' $B

    Reset-Tree
    # An encoding REPAIR rewrites most lines and must not read as a replacement.
    $mangled = $baseLedger.Replace([string][char]0x2014, $MojiEmDash)
    New-Utf8File $ledgerPath $mangled
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '--quiet', '-m', 'fixture: mangled ledger')
    $mangledSha = (& git -C $root rev-parse HEAD).Trim()
    New-Utf8File $ledgerPath $baseLedger
    Assert-Result 'un-mangling a ledger reads as a REPAIR, not a replace' $false 'encoding REPAIR' @('-BaseRef', $mangledSha)
    Invoke-Git @('reset', '--hard', '--quiet', $base)

    Reset-Tree
    # Content that MOVED to the archive was not content that was LOST.
    $lines = $baseLedger -split "`n"
    $head = ($lines[0..([int]($lines.Count * 0.4))] -join "`n")
    New-Utf8File $ledgerPath $head
    New-Utf8File (Join-Path $root 'docs/ops/claude-archive/helm-feedback-2026-09.md') $baseLedger
    Assert-Result 'moving old entries into the archive passes' $false 'ARCHIVE MOVE' $B

    # -- must FAIL: one case per check ------------------------------------------------

    Reset-Tree
    Remove-Item -LiteralPath $ledgerPath -Force
    Assert-Result 'check 1 - deleting a ledger REFUSES' $true 'is DELETED' $B

    Reset-Tree
    New-Utf8File $ledgerPath ''
    Assert-Result 'check 1 - emptying a ledger REFUSES' $true 'is EMPTY' $B

    Reset-Tree
    New-Utf8File $ledgerPath "# HELM-FEEDBACK.md`n`nsee git history`n"
    Assert-Result 'check 1 - replacing a ledger with a stub REFUSES' $true 'keeps only' $B

    Reset-Tree
    Remove-Item -LiteralPath (Join-Path $root $INBOX) -Force
    Assert-Result 'check 1 - deleting an INBOX REFUSES too' $true 'is DELETED' $B

    Reset-Tree
    New-Utf8File $ledgerPath ($lines[0..([int]($lines.Count * 0.75))] -join "`n")
    Assert-Result 'check 2 - truncating a ledger by a quarter REFUSES' $true 'keeps only' $B

    Reset-Tree
    # Same length, different content: passes any size check, and is exactly the
    # "SSC full-replace" the card names.
    New-Utf8File $ledgerPath (Get-LedgerText 60 'regenerated from scratch')
    Assert-Result 'check 3 - full-replacing at the same length REFUSES' $true 'retains only' $B

    Reset-Tree
    New-Utf8File $ledgerPath ($baseLedger.Replace([string][char]0x2014, $MojiEmDash))
    Assert-Result 'check 4 - introducing mojibake REFUSES' $true 'double-encoded' $B

    Reset-Tree
    New-Utf8File (Join-Path $root $INBOX) ((Get-LedgerText 40 'FABLE.md').Replace([string][char]0x2014, $MojiEmDash))
    Assert-Result 'check 4 - mojibake in an INBOX REFUSES too' $true 'double-encoded' $B

    Reset-Tree
    New-Utf8File (Join-Path $root 'ORACLE-FEEDBACK.md') (Get-LedgerText 20 'a new channel nobody rostered')
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '--quiet', '-m', 'fixture: a new channel file')
    Assert-Result 'check 5 - an unrostered channel file REFUSES' $true 'NOT in this guard' $B
    Invoke-Git @('reset', '--hard', '--quiet', $base)

    # -- and the honest skip ----------------------------------------------------------

    Reset-Tree
    # No -BaseRef, no origin/main, no main: there is nothing to compare against. It must
    # say so out loud rather than exit 0 looking like coverage.
    Assert-Result 'no resolvable base SKIPS loudly rather than passing quietly' $false 'were NOT verified' @()
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "channel-wipe-guard-selftest: FAILED" -ForegroundColor Red
    foreach ($f in $failed) { Write-Host "channel-wipe-guard-selftest:    $f" -ForegroundColor Red }
    exit 1
}
Write-Host "channel-wipe-guard-selftest: ok  ($script:step cases; every check driven into the red at least once)" -ForegroundColor Green
exit 0
