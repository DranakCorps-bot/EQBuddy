# Auto-roll `main` onto the Founder's PC (DRA-705, plan docs/plans/DRA-705.md). LOCAL ONLY.
#
# The scheduled task `\Dranak - EQBuddy Auto-roll` runs this every 10 minutes and at logon,
# under the Founder's own user, "only when logged on", from the DEDICATED clone
# C:\Users\david\source\EQBuddy-autoroll. Each run:
#
#   1. local pause file (%LOCALAPPDATA%\EQBuddy Evolved\autoroll\PAUSED, the desktop
#      shortcuts) -> stop, and say "paused" once;
#   2. `git ls-remote` for main (cheap). Same SHA as the last good roll, or as the build
#      installed now -> stop. Five merges in ten minutes are one roll of the newest;
#   3. a SHA that FAILED is not retried until main moves - except once, when the failure
#      was in the build step and reads as a network fault (the signing toolchain restore);
#   4. fetch; `docs/ops/autoroll.pause` on main (Helm's switch, no PC access) -> stop;
#   5. a manual PR-smoke install (EQBuddy.build.json source=manual) whose commits main does
#      not contain yet -> stop, for at most 48 h. When the PR merges, main contains them;
#   6. detach to the SHA, clean, and run install-local.ps1 -Evolved -Install, which builds
#      and signs FIRST, closes last, relaunches only what was running, and restores the
#      previous build if the new one dies (its own header says how);
#   7. read EQBuddy.build.json back - the roll succeeded only if it names this SHA - then
#      record state, keep the log, and toast the result.
#
# What it NEVER does is the point of the file, and scripts\autoroll-guard.ps1 holds it to
# that in check.ps1 and CI: no release script, no tag, no push, no GitHub release, no
# OneDrive, no writing web call. The clone's push URL is DISABLED and this script refuses
# to run in any clone where it is not - a `git clean -fdx` in a working clone would be a
# disaster, and that refusal is what keeps the robot inside its own tree.
#
#   pwsh -NoProfile -File scripts\auto-roll.ps1              (the scheduled task)
#   pwsh -NoProfile -File scripts\auto-roll.ps1 -SelfTest    (offline; check.ps1 and CI)
param(
    [switch] $SelfTest,
    [string] $StateDir = (Join-Path $env:LOCALAPPDATA 'EQBuddy Evolved\autoroll')
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent

$script:HoldHours = 48
$script:KeepLogs = 20
# What a network fault in the build step looks like: NuGet restore, a toolchain download,
# DNS. Anything else that fails is a real failure and waits for main to move.
$script:TransientPattern = 'Unable to load the service index|NU1301|No such host is known|could not resolve host|The remote name could not be resolved|timed out|A connection attempt failed|Failed to download'

# ---- pure decisions (the -SelfTest drives these) ------------------------------------

# The one answer to "roll this SHA now?". Every skip names its reason, because the log is
# how anyone learns why the PC did not update.
function Get-AutoRollDecision {
    param(
        [bool] $LocalPaused, [string] $RemoteSha, [string] $InstalledSha,
        $State, [bool] $RepoPaused, [string] $Held
    )
    if ($LocalPaused) { return @{ Action = 'skip'; Reason = 'paused-local' } }
    if (-not $RemoteSha) { return @{ Action = 'skip'; Reason = 'no-remote' } }
    if ($RemoteSha -eq $State.lastGoodSha -or $RemoteSha -eq $InstalledSha) {
        return @{ Action = 'skip'; Reason = 'current' }
    }
    $retry = $false
    if ($RemoteSha -eq $State.lastFailedSha) {
        if (-not ($State.lastFailedTransient -and -not $State.retryUsed)) {
            return @{ Action = 'skip'; Reason = 'failed-sha' }
        }
        $retry = $true
    }
    if ($RepoPaused) { return @{ Action = 'skip'; Reason = 'paused-repo' } }
    if ($Held) { return @{ Action = 'skip'; Reason = "held-manual: $Held" } }
    return @{ Action = 'roll'; Reason = $(if ($retry) { 'retry-transient' } else { 'new-sha' }); Retry = $retry }
}

# A manual install wins over the robot while the commits it carries are not on main. The
# stamp names those commits (install-local.ps1's pendingCommits), never the local merge
# commit, which main will never contain. $IsContained answers one SHA.
function Get-AutoRollHold {
    param($Stamp, [datetime] $Now, [scriptblock] $IsContained)
    if (-not $Stamp -or $Stamp.source -ne 'manual') { return $null }
    # ConvertFrom-Json hands back an ISO stamp as a [datetime] already; a string is parsed
    # invariantly. Either way the comparison is in UTC.
    $built = if ($Stamp.builtAtUtc -is [datetime]) { $Stamp.builtAtUtc.ToUniversalTime() }
             else { [datetimeoffset]::Parse("$($Stamp.builtAtUtc)", [cultureinfo]::InvariantCulture).UtcDateTime }
    $age = $Now - $built
    if ($age.TotalHours -ge $script:HoldHours) { return $null }
    if ($Stamp.dirty) { return "manual install of a dirty tree at $($Stamp.sha), $([int]$age.TotalHours) h old" }
    $missing = @(@($Stamp.pendingCommits) | Where-Object { $_ -and -not (& $IsContained $_) })
    if ($missing.Count) { return "manual install $($Stamp.sha) carries $($missing.Count) commit(s) main does not have yet" }
    return $null
}

# Worth the one retry only when it failed BUILDING (or before the build, which is where the
# signing toolchain restores) and the log reads as a network fault.
function Test-AutoRollTransient {
    param([string] $LogText)
    $stages = [regex]::Matches($LogText, '\[install-local\] stage=(\w+)')
    $last = if ($stages.Count) { $stages[$stages.Count - 1].Groups[1].Value } else { $null }
    if ($last -and $last -ne 'build') { return $false }
    return [bool]($LogText -match $script:TransientPattern)
}

# The robot runs `git clean -fdx`. It refuses any clone whose push URL is not DISABLED.
function Assert-AutoRollClone {
    param([string] $Repo)
    $push = (& git -C $Repo remote get-url --push origin 2>$null)
    if ($LASTEXITCODE -ne 0 -or "$push".Trim() -ne 'DISABLED') {
        throw "auto-roll refuses to run in $Repo - its push URL is '$push', not DISABLED. It runs only in the dedicated auto-roll clone (DRA-705 plan section 2)."
    }
}

# ---- state, logs, toast --------------------------------------------------------------

function Read-AutoRollJson([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Save-AutoRollState($State) {
    $path = Join-Path $StateDir 'state.json'
    $tmp = "$path.tmp"
    [System.IO.File]::WriteAllText($tmp, ($State | ConvertTo-Json -Depth 4), [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tmp -Destination $path -Force
}

function Write-AutoRollEvent([string] $Message) {
    $log = Join-Path $StateDir 'autoroll.log'
    Add-Content -LiteralPath $log -Value ("{0}  {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message) -Encoding utf8
    $lines = @(Get-Content -LiteralPath $log)
    if ($lines.Count -gt 1000) { $lines[-500..-1] | Set-Content -LiteralPath $log -Encoding utf8 }
}

# A plain WinRT toast. pwsh 7 has no WinRT projection, so Windows PowerShell draws it; the
# text travels in environment variables so the script it runs is a constant. A toast that
# fails is logged and never fails the roll.
function Show-AutoRollToast([string] $Title, [string] $Body) {
    $toast = @'
$ErrorActionPreference = 'Stop'
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$x = New-Object Windows.Data.Xml.Dom.XmlDocument
$x.LoadXml('<toast><visual><binding template="ToastGeneric"><text></text><text></text></binding></visual></toast>')
$t = $x.GetElementsByTagName('text')
$t.Item(0).AppendChild($x.CreateTextNode($env:EQB_AUTOROLL_TITLE)) | Out-Null
$t.Item(1).AppendChild($x.CreateTextNode($env:EQB_AUTOROLL_BODY)) | Out-Null
$app = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($app).Show([Windows.UI.Notifications.ToastNotification]::new($x))
'@
    $env:EQB_AUTOROLL_TITLE = $Title
    $env:EQB_AUTOROLL_BODY = $Body
    try {
        $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($toast))
        & powershell.exe -NoProfile -NonInteractive -EncodedCommand $encoded 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-AutoRollEvent "toast failed (exit $LASTEXITCODE): $Title" }
    }
    catch { Write-AutoRollEvent "toast failed: $($_.Exception.Message)" }
    finally {
        Remove-Item Env:\EQB_AUTOROLL_TITLE, Env:\EQB_AUTOROLL_BODY -ErrorAction SilentlyContinue
    }
}

function Get-AutoRollInstallDir {
    $keys = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    $entry = Get-ItemProperty $keys -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like 'EQBuddy Evolved version *' -and $_.InstallLocation } |
        Select-Object -First 1
    if ($entry) { return $entry.InstallLocation.TrimEnd('\') }
    return $null
}

function Get-Short([string] $Sha) { if ($Sha -and $Sha.Length -ge 7) { $Sha.Substring(0, 7) } else { "$Sha" } }

# ---- -SelfTest: offline, touches no clone but a throwaway one -------------------------
if ($SelfTest) {
    $fails = @()
    function Check([string] $Name, [bool] $Ok) { if (-not $Ok) { $script:fails += $Name; Write-Host "FAIL: $Name" } }
    $s = [pscustomobject]@{ lastGoodSha = 'aaa'; lastFailedSha = 'bbb'; lastFailedTransient = $false; retryUsed = $false }
    $t = [pscustomobject]@{ lastGoodSha = 'aaa'; lastFailedSha = 'bbb'; lastFailedTransient = $true; retryUsed = $false }
    $u = [pscustomobject]@{ lastGoodSha = 'aaa'; lastFailedSha = 'bbb'; lastFailedTransient = $true; retryUsed = $true }
    $d = { param($p) Get-AutoRollDecision @p }
    Check 'local pause stops a new SHA' ((& $d @{ LocalPaused = $true; RemoteSha = 'ccc'; State = $s }).Reason -eq 'paused-local')
    Check 'no remote SHA is a skip' ((& $d @{ RemoteSha = ''; State = $s }).Reason -eq 'no-remote')
    Check 'last good SHA is current' ((& $d @{ RemoteSha = 'aaa'; State = $s }).Reason -eq 'current')
    Check 'installed SHA is current' ((& $d @{ RemoteSha = 'ddd'; InstalledSha = 'ddd'; State = $s }).Reason -eq 'current')
    Check 'a failed SHA is not retried' ((& $d @{ RemoteSha = 'bbb'; State = $s }).Reason -eq 'failed-sha')
    $r = & $d @{ RemoteSha = 'bbb'; State = $t }
    Check 'a transient build failure gets ONE retry' ($r.Action -eq 'roll' -and $r.Retry)
    Check 'the retry is spent once' ((& $d @{ RemoteSha = 'bbb'; State = $u }).Reason -eq 'failed-sha')
    Check 'repo pause stops a new SHA' ((& $d @{ RemoteSha = 'ccc'; State = $s; RepoPaused = $true }).Reason -eq 'paused-repo')
    Check 'a manual hold stops a new SHA' ((& $d @{ RemoteSha = 'ccc'; State = $s; Held = 'x' }).Reason -like 'held-manual*')
    $r = & $d @{ RemoteSha = 'ccc'; State = $s }
    Check 'a new SHA rolls' ($r.Action -eq 'roll' -and -not $r.Retry)
    Check 'a new SHA rolls on first run (no state)' ((& $d @{ RemoteSha = 'ccc'; State = [pscustomobject]@{} }).Action -eq 'roll')

    $now = [datetime]::UtcNow
    $stamp = { param($src, $hours, $pending, $dirty)
        [pscustomobject]@{ source = $src; sha = 'm1'; builtAtUtc = $now.AddHours(-$hours).ToString('yyyy-MM-ddTHH:mm:ssZ'); pendingCommits = $pending; dirty = $dirty } }
    $onMain = { param($sha) $sha -eq 'p1' }
    Check 'an autoroll stamp never holds' ($null -eq (Get-AutoRollHold -Stamp (& $stamp 'autoroll' 1 @('zz') $false) -Now $now -IsContained $onMain))
    Check 'a manual stamp whose commits are on main does not hold' ($null -eq (Get-AutoRollHold -Stamp (& $stamp 'manual' 1 @('p1') $false) -Now $now -IsContained $onMain))
    Check 'a manual stamp with a commit main lacks HOLDS' ($null -ne (Get-AutoRollHold -Stamp (& $stamp 'manual' 1 @('p1', 'p2') $false) -Now $now -IsContained $onMain))
    Check 'the hold expires at 48 h' ($null -eq (Get-AutoRollHold -Stamp (& $stamp 'manual' 49 @('p2') $false) -Now $now -IsContained $onMain))
    Check 'a dirty manual install holds' ($null -ne (Get-AutoRollHold -Stamp (& $stamp 'manual' 1 @() $true) -Now $now -IsContained $onMain))
    Check 'a clean manual install of main does not hold' ($null -eq (Get-AutoRollHold -Stamp (& $stamp 'manual' 1 @() $false) -Now $now -IsContained $onMain))
    Check 'no stamp does not hold' ($null -eq (Get-AutoRollHold -Stamp $null -Now $now -IsContained $onMain))

    Check 'a NuGet fault in the build is transient' (Test-AutoRollTransient "[install-local] stage=build`nerror NU1301: Unable to load the service index")
    Check 'a network fault before the build (toolchain restore) is transient' (Test-AutoRollTransient 'Failed to download the signing toolchain')
    Check 'a network-looking line after the build is NOT transient' (-not (Test-AutoRollTransient "[install-local] stage=build`n[install-local] stage=liveness`ntimed out"))
    Check 'a compile error is not transient' (-not (Test-AutoRollTransient "[install-local] stage=build`nerror CS1002: ; expected"))

    # The clone refusal, in a REAL throwaway repo: refused with a push URL, allowed with DISABLED.
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('eqbuddy-autoroll-' + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null
        # `git config`, not `git remote`: autoroll-guard.ps1 allows a push URL to be written
        # only as DISABLED, and this file is what it scans.
        & git -C $tmp init -q 2>&1 | Out-Null
        & git -C $tmp config remote.origin.url https://example.invalid/repo.git 2>&1 | Out-Null
        $refused = $false
        try { Assert-AutoRollClone -Repo $tmp } catch { $refused = $true }
        Check 'a clone that can push is refused' $refused
        & git -C $tmp config remote.origin.pushurl DISABLED 2>&1 | Out-Null
        $ok = $true
        try { Assert-AutoRollClone -Repo $tmp } catch { $ok = $false }
        Check 'the push-disabled clone is accepted' $ok
    }
    finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }

    if ($fails.Count) { Write-Host "auto-roll selftest: $($fails.Count) FAILED"; exit 1 }
    Write-Host 'auto-roll selftest: decision table, manual hold, transient retry and clone refusal all hold'
    exit 0
}

# ---- the run ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
$mutex = [System.Threading.Mutex]::new($false, 'Local\EQBuddyAutoRoll')
if (-not $mutex.WaitOne(0)) { exit 0 }
try {
    Assert-AutoRollClone -Repo $repo
    $state = Read-AutoRollJson (Join-Path $StateDir 'state.json')
    if (-not $state) { $state = [pscustomobject]@{} }
    foreach ($k in 'lastGoodSha', 'lastFailedSha', 'lastFailedTransient', 'retryUsed', 'lastAttemptSha',
                   'lastAttemptAt', 'lastResult', 'lastLog', 'pauseToasted', 'holdToastedSha') {
        if (-not ($state.PSObject.Properties.Name -contains $k)) { $state | Add-Member -NotePropertyName $k -NotePropertyValue $null }
    }

    $installDir = Get-AutoRollInstallDir
    $stampPath = if ($installDir) { Join-Path $installDir 'EQBuddy.build.json' } else { $null }
    $stamp = if ($stampPath) { Read-AutoRollJson $stampPath } else { $null }

    $localPaused = Test-Path -LiteralPath (Join-Path $StateDir 'PAUSED')
    if (-not $localPaused -and $state.pauseToasted -eq 'local') { $state.pauseToasted = $null; Save-AutoRollState $state }

    $remote = $null
    if (-not $localPaused) {
        $line = (& git -C $repo ls-remote origin refs/heads/main 2>$null)
        if ($LASTEXITCODE -ne 0) { Write-AutoRollEvent 'ls-remote failed; will try again next run'; exit 0 }
        $remote = ("$line" -split '\s+')[0]
    }

    $decision = Get-AutoRollDecision -LocalPaused $localPaused -RemoteSha $remote -InstalledSha $stamp.sha -State $state
    if ($decision.Action -eq 'roll') {
        # Only now is a fetch worth it: the repo pause and the manual hold both need main.
        & git -C $repo fetch --quiet origin main 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-AutoRollEvent 'fetch failed; will try again next run'; exit 0 }
        & git -C $repo cat-file -e "${remote}:docs/ops/autoroll.pause" 2>$null
        $repoPaused = $LASTEXITCODE -eq 0
        $contained = { param($sha) & git -C $repo merge-base --is-ancestor $sha $remote 2>$null; $LASTEXITCODE -eq 0 }
        $held = Get-AutoRollHold -Stamp $stamp -Now ([datetime]::UtcNow) -IsContained $contained
        $decision = Get-AutoRollDecision -LocalPaused $false -RemoteSha $remote -InstalledSha $stamp.sha -State $state `
            -RepoPaused $repoPaused -Held $held
    }

    if ($decision.Action -ne 'roll') {
        switch -Wildcard ($decision.Reason) {
            'paused-*' {
                $kind = $decision.Reason.Substring(7)
                if ($state.pauseToasted -ne $kind) {
                    $why = if ($kind -eq 'local') { 'the desktop Pause shortcut is on' } else { 'docs/ops/autoroll.pause is on main' }
                    Write-AutoRollEvent "paused ($why)"
                    Show-AutoRollToast 'EQBuddy auto-update is paused' "Nothing will be installed until it is resumed: $why."
                    $state.pauseToasted = $kind
                    Save-AutoRollState $state
                }
            }
            'held-manual*' {
                if ($state.holdToastedSha -ne $stamp.sha) {
                    Write-AutoRollEvent $decision.Reason
                    Show-AutoRollToast 'EQBuddy auto-update is holding' "Your hand-installed build $(Get-Short $stamp.sha) is not on main yet; it stays until it is (48 h at most)."
                    $state.holdToastedSha = $stamp.sha
                    Save-AutoRollState $state
                }
            }
        }
        exit 0
    }
    if ($state.pauseToasted) { $state.pauseToasted = $null }

    # ---- the roll ----
    $sha7 = Get-Short $remote
    $log = Join-Path $StateDir ("roll-{0}-{1}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $sha7)
    Write-AutoRollEvent "rolling $remote ($($decision.Reason)); log $log"
    $state.lastAttemptSha = $remote
    $state.lastAttemptAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    $state.lastLog = $log
    Save-AutoRollState $state

    & git -C $repo checkout --quiet --detach --force $remote *>> $log
    $checkout = $LASTEXITCODE
    # Keep only what a fresh clone needs and git does not carry: the signing config and the
    # auto-restored toolchain. Everything else untracked is gone, so nothing stray rides in.
    & git -C $repo clean -fdxq -e tools/ -e artifact-signing.json -e artifact-signing-identity.json *>> $log
    $code = 1
    if ($checkout -eq 0) {
        $pwsh = (Get-Process -Id $PID).Path
        & $pwsh -NoProfile -File (Join-Path $repo 'scripts\install-local.ps1') -Evolved -Install -Source autoroll *>> $log
        $code = $LASTEXITCODE
    }
    $after = if ($stampPath) { Read-AutoRollJson $stampPath } else { $null }
    $ok = $code -eq 0 -and $after -and $after.sha -eq $remote

    $subject = (& git -C $repo log -1 --format=%s $remote 2>$null)
    if ($subject -match '^Merge pull request (#\d+)') {
        $title = (& git -C $repo log -1 --format=%b $remote 2>$null | Where-Object { $_.Trim() } | Select-Object -First 1)
        $subject = "$($Matches[1]) $title".Trim()
    }

    if ($ok) {
        $state.lastGoodSha = $remote
        $state.lastFailedSha = $null; $state.lastFailedTransient = $null; $state.retryUsed = $null
        $state.lastResult = 'installed'
        Write-AutoRollEvent "installed $remote"
        Show-AutoRollToast "EQBuddy updated to main $sha7" "$subject"
    }
    else {
        $text = Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue
        $transient = Test-AutoRollTransient "$text"
        $state.retryUsed = [bool]($decision.Retry)
        $state.lastFailedSha = $remote
        $state.lastFailedTransient = $transient
        $state.lastResult = "failed (exit $code)"
        $running = if ($after) { Get-Short $after.sha } else { 'the previous build' }
        Write-AutoRollEvent "FAILED at $remote (exit $code, transient=$transient); still on $running"
        Show-AutoRollToast "EQBuddy auto-update FAILED at $sha7" "Still running $running. Log: $log"
    }
    Save-AutoRollState $state

    Get-ChildItem -LiteralPath $StateDir -Filter 'roll-*.log' | Sort-Object LastWriteTime -Descending |
        Select-Object -Skip $script:KeepLogs | Remove-Item -Force
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
