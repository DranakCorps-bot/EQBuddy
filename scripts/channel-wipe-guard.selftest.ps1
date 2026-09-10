<#
.SYNOPSIS
    Proves channel-wipe-guard.ps1 actually fails - one throwaway repo per check.

.DESCRIPTION
    A guard that has never been seen to fail has not been shown to guard anything (traps
    34, 39), and this one is silent on every healthy tree by design, so it would sit green
    forever whether or not it worked. #493 and #502 both merged past a repo full of green
    checks; the point of this file is that the NEXT one does not.

    Each case builds a real git repo in the temp directory: an append-only base commit
    carrying HELM.md and HELM-FEEDBACK.md, then a branch that does one specific bad thing,
    then the guard run against that worktree with -Repo and -BaseRef. Real repos rather
    than a mocked `git` because merge-base resolution is half of what is being tested.

    Five cases - the four checks, plus a control that must PASS. The control is the half a
    prove-fail cannot cover on its own: a guard that fails everything is as useless as one
    that fails nothing, and it is the shape that gets a gate switched off.

    Never touches this repo's own ledgers; every path is under a per-run temp folder that
    is removed on the way out.

.EXAMPLE
    pwsh -NoProfile -File scripts/channel-wipe-guard.selftest.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$guard = Join-Path $PSScriptRoot 'channel-wipe-guard.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("eqbuddy-channel-wipe-selftest-" + [guid]::NewGuid().ToString('N'))

$failed = @()
$script:step = 0

# A ledger shaped like the real ones: dated `##` entries, newest first, em dashes and all.
# The em dashes are deliberate - the encoding-churn case below is what forced the guard's
# comparison key to strip non-ASCII in the first place.
function New-Ledger([int] $entries) {
    $sb = [Text.StringBuilder]::new()
    for ($i = $entries; $i -ge 1; $i--) {
        [void]$sb.AppendLine("## 2026-09-0$([math]::Max(1, $i % 9)) ~$($i):15 PM CT $([char]0x2014) entry $i SIGNED")
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("- **Ruling:** entry $i $([char]0x2014) additions-only KEEP. Soft LEAVE rewriting this file.")
        [void]$sb.AppendLine('')
    }
    $sb.ToString()
}

function Write-Utf8([string] $path, [string] $text) {
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

function Invoke-Git([string] $dir, [string[]] $Arguments) {
    & git -C $dir @Arguments 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed in $dir" }
}

# Base commit on `main`, then a branch checked out and ready to be damaged.
function New-Fixture([string] $name) {
    $dir = Join-Path $root $name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Invoke-Git $dir @('init', '--initial-branch=main')
    Invoke-Git $dir @('config', 'user.email', 'selftest@example.invalid')
    Invoke-Git $dir @('config', 'user.name', 'channel-wipe selftest')
    Invoke-Git $dir @('config', 'commit.gpgsign', 'false')

    Write-Utf8 (Join-Path $dir 'HELM.md') (New-Ledger 40)
    Write-Utf8 (Join-Path $dir 'HELM-FEEDBACK.md') (New-Ledger 60)
    Invoke-Git $dir @('add', '-A')
    Invoke-Git $dir @('commit', '-m', 'base: channel ledgers')
    Invoke-Git $dir @('checkout', '-b', 'work')
    return $dir
}

function Invoke-Guard([string] $dir) {
    $out = & pwsh -NoProfile -File $guard -Repo $dir -BaseRef 'main' 2>&1 | Out-String
    return @{ code = $LASTEXITCODE; text = $out.Trim() }
}

function Expect-Fail {
    param([string] $Name, $Result, [string] $Needle)
    $script:step++
    $label = "$($script:step). $Name"
    if ($Result.code -eq 0) {
        $script:failed += "$label - expected the guard to FAIL, got exit 0: $($Result.text)"
        return
    }
    if ($Needle -and ($Result.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label - failed for the wrong reason; expected text '$Needle', got: $($Result.text)"
        return
    }
    Write-Host "  $label - guard failed as required" -ForegroundColor Green
}

function Expect-Ok {
    param([string] $Name, $Result, [string] $Needle)
    $script:step++
    $label = "$($script:step). $Name"
    if ($Result.code -ne 0) {
        $script:failed += "$label - expected exit 0, got $($Result.code): $($Result.text)"
        return
    }
    if ($Needle -and ($Result.text -notmatch [regex]::Escape($Needle))) {
        $script:failed += "$label - expected text '$Needle', got: $($Result.text)"
        return
    }
    Write-Host "  $label - guard passed as required" -ForegroundColor Green
}

try {
    New-Item -ItemType Directory -Force -Path $root | Out-Null

    # ---- 1: DELETED - the #493 / #502 shape, both files at once --------------------
    $d = New-Fixture 'deleted'
    Invoke-Git $d @('rm', '-q', 'HELM.md', 'HELM-FEEDBACK.md')
    Invoke-Git $d @('commit', '-m', 'channel: SSC land (additions-only KEEP)')
    Expect-Fail 'a branch that deletes both HELM ledgers' (Invoke-Guard $d) 'EXISTS at the base and is GONE'

    # ---- 2: EMPTIED - truncate to zero, path still in the tree ----------------------
    $e = New-Fixture 'emptied'
    Write-Utf8 (Join-Path $e 'HELM.md') ''
    Invoke-Git $e @('commit', '-am', 'channel: land')
    Expect-Fail 'a branch that truncates a ledger to zero bytes' (Invoke-Guard $e) 'is EMPTY in this tree'

    # ---- 3: EXTREME SHRINK - keeps the newest entries, drops the history ------------
    # 40 entries down to 4: a shrink far past the threshold. Deliberately NOT zero, so
    # this can only be caught by the byte comparison and not by checks 1 or 2.
    $s = New-Fixture 'shrunk'
    Write-Utf8 (Join-Path $s 'HELM.md') (New-Ledger 4)
    Invoke-Git $s @('commit', '-am', 'channel: land')
    Expect-Fail 'a branch that keeps only the newest entries' (Invoke-Guard $s) 'of its bytes against the base'

    # ---- 4: FULL REPLACE - and the file GROWS ---------------------------------------
    # The case checks 2 and 3 are both blind to: every original entry is gone, replaced by
    # a larger set of new ones. Bytes go UP, so only entry retention can see it.
    $r = New-Fixture 'replaced'
    $sb = [Text.StringBuilder]::new()
    for ($i = 1; $i -le 80; $i++) {
        [void]$sb.AppendLine("## 2026-09-10 ~$($i):00 PM CT $([char]0x2014) rewritten note $i")
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("- Whole-file rewrite: none of the base entries survive this. Padding to grow the file well past what it replaced.")
        [void]$sb.AppendLine('')
    }
    Write-Utf8 (Join-Path $r 'HELM.md') $sb.ToString()
    Invoke-Git $r @('commit', '-am', 'channel: rewrite HELM.md')
    $replacedResult = Invoke-Guard $r
    Expect-Fail 'a whole-file rewrite that is BIGGER than what it replaced' $replacedResult 'entries it has at the base'
    # ...and prove it really was bigger, so this case cannot silently degrade into case 3.
    $script:step++
    if ($replacedResult.text -match 'of its bytes against the base') {
        $script:failed += "$($script:step). the rewrite fixture must not also trip the SHRINK check, or it is not testing full-replace: $($replacedResult.text)"
    }
    else { Write-Host "  $($script:step). the rewrite was caught by entry retention, not by size" -ForegroundColor Green }

    # ---- 5: THE CONTROL - append-only, plus the encoding churn that broke v1 ---------
    # Prepends a new entry (the sanctioned shape) AND re-encodes the whole file to the
    # mojibake form real history produced in ff6853ba. Both must pass: the ledger is
    # intact, and a guard that failed here would fail correct merges.
    $c = New-Fixture 'control'
    $helm = Join-Path $c 'HELM.md'
    $appended = "## 2026-09-10 ~11:00 PM CT $([char]0x2014) newest entry, prepended`r`n`r`n- **Ruling:** SIGNED.`r`n`r`n" + [IO.File]::ReadAllText($helm)
    # Decode the UTF-8 BYTES as CP1252 and let Write-Utf8 re-encode them: exactly how
    # `E2 80 94` became `C3 A2 E2 82 AC E2 80 9D` on a commit whose message said
    # "additions-only KEEP". CP1252 and not Latin-1, and the difference is the whole
    # fixture - Latin-1 has no mapping for the bytes an em dash is made of, so it yields
    # `?`, which is ASCII, survives the guard's key normalisation and would make this case
    # pass for a reason that has nothing to do with the bug. 0x80 -> U+20AC and
    # 0x94 -> U+201D are CP1252's, and they reproduce the real commit's bytes exactly.
    $cp1252 = [Text.Encoding]::GetEncoding(1252)
    $mojibake = $cp1252.GetString([Text.Encoding]::UTF8.GetBytes($appended))
    Write-Utf8 $helm $mojibake
    Invoke-Git $c @('commit', '-am', 'channel: prepend entry (additions-only KEEP)')
    Expect-Ok 'an append-only land whose text was re-encoded on the way in' (Invoke-Guard $c) 'channel ledgers intact'
}
finally {
    if (Test-Path $root) { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }
}

if ($failed.Count -gt 0) {
    Write-Host "channel-wipe-guard-selftest: FAILED" -ForegroundColor Red
    foreach ($f in $failed) { Write-Host "channel-wipe-guard-selftest:    $f" -ForegroundColor Red }
    exit 1
}

Write-Host "channel-wipe-guard-selftest: ok  ($($script:step) cases - delete, empty, shrink, full-replace all refused; append-only control passes)" -ForegroundColor Green
exit 0
