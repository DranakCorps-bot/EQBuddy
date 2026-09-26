# DRA-169 prove-fail. Dot-sourced by install-local.ps1 -SelfTest so it calls the
# same close functions the republish uses. Do not run it on its own.
#
# Stages three EQBuddy stand-ins and reads each one's own ProductVersion:
#   169.1.0  outside dist\publish, holding the Evolved profile lock
#   169.2.0  under dist\publish, not holding the lock
#   169.0.1  on the v1 profile's lock
# The pre-fix path filter matches 169.2.0 and leaves 169.1.0 running. The
# profile-lock close matches 169.1.0 and closes it, and never the v1 process.
# Presence alone would pass on the broken filter: both runs end with an
# EQBuddy on screen.

if (-not (Get-Command Get-EqProcessesHoldingProfile -ErrorAction SilentlyContinue)) {
    Write-Host 'FAIL: run pwsh -NoProfile -File scripts/install-local.ps1 -SelfTest'
    exit 1
}

$ErrorActionPreference = 'Stop'
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repo = Split-Path -Parent $scriptDir

$script:Dra169Result = 1

function Fail([string] $Message) {
    throw $Message
}

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("eqbuddy-dra169-" + [guid]::NewGuid().ToString('N'))
$publishDir = Join-Path $root 'repo\dist\publish'
$outsideDir = Join-Path $root 'LocalAppData\EQBuddy Evolved\publish'
$v1Dir = Join-Path $root 'LocalAppData\EQBuddy'
$evolvedProfile = Join-Path $root 'AppData\EQBuddy Evolved'
$v1Profile = Join-Path $root 'AppData\EQBuddy'
$started = @()

function Build-Holder([string] $Version, [string] $OutDir) {
    $csproj = Join-Path $scriptDir 'fixtures\profile-lock-holder\profile-lock-holder.csproj'
    $intermediate = Join-Path $OutDir 'obj'
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    # Uncaptured success output would become part of the return value, and a
    # later .StartsWith on that array is true for any non-empty result.
    & dotnet build $csproj -c Release -o $OutDir --nologo -v q `
        -p:Version=$Version `
        -p:FileVersion=$Version `
        -p:InformationalVersion=$Version `
        -p:IncludeSourceRevisionInInformationalVersion=false `
        "-p:BaseIntermediateOutputPath=$intermediate\" | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "holder build $Version exited $LASTEXITCODE" }
    $exe = Join-Path $OutDir 'EQBuddy.exe'
    if (-not (Test-Path -LiteralPath $exe)) { Fail "holder build $Version produced no EQBuddy.exe" }
    $got = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    if ($got -ne $Version) { Fail "holder $Version stamped ProductVersion '$got'" }
    return $exe
}

function Start-Holder([string] $Exe, [string] $LockPath, [string] $Mode) {
    $ready = Join-Path $root ("ready-" + [guid]::NewGuid().ToString('N') + '.txt')
    # The Evolved profile directory contains a space. Start-Process joins
    # ArgumentList into one command line and the runtime splits it again, so an
    # unquoted lock path never arrives as one argument.
    $proc = Start-Process -FilePath $Exe -ArgumentList "`"$LockPath`"", "`"$ready`"", $Mode -PassThru
    $script:started += $proc
    $deadline = (Get-Date).AddSeconds(20)
    while (-not (Test-Path -LiteralPath $ready) -and -not $proc.HasExited -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 50
    }
    if ($proc.HasExited) { Fail "holder exited before its window was up ($Exe, code $($proc.ExitCode))" }
    if (-not (Test-Path -LiteralPath $ready)) { Fail "holder never became closeable ($Exe)" }
    return (Get-Process -Id $proc.Id)
}

function Get-RunningProductVersion($Proc) {
    $fresh = Get-Process -Id $Proc.Id -ErrorAction Stop
    $ver = $fresh.MainModule.FileVersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($ver)) { Fail "pid $($Proc.Id) has no ProductVersion" }
    return $ver
}

function Test-ExclusiveLockHeld([string] $LockPath) {
    if (-not (Test-Path -LiteralPath $LockPath)) { return $false }
    try {
        $fs = [System.IO.File]::Open($LockPath, 'Open', 'ReadWrite', 'None')
        $fs.Dispose()
        return $false
    }
    catch [System.IO.IOException] {
        return $true
    }
}

try {
    # These arms used to run before this try. Fail throws, and check.ps1 invokes
    # the selftest in-process, so a throw outside the catch aborts the later
    # stages and the FAILED summary instead of recording the failure (DRA-457).
    $install = Get-Content (Join-Path $scriptDir 'install-local.ps1') -Raw
    if ($install -notmatch 'Get-EqProcessesHoldingProfile -ProfileDir \$evolvedProfile') {
        Fail 'Evolved close does not ask who holds the Evolved profile lock'
    }
    if ($install -match 'StartsWith\(\$publishDir') {
        Fail 'install-local.ps1 still selects the copy to close by publish path'
    }
    if ($install -notmatch '\} else \{\s+\$running = @\(Get-Process EQBuddy -ErrorAction SilentlyContinue\)') {
        Fail 'the non-Evolved close arm is no longer every EQBuddy process'
    }
    if ($install -notmatch 'CloseMainWindow\(\)' -or $install -notmatch 'WaitForExit\(15000\)') {
        Fail 'graceful close order is gone (CloseMainWindow then WaitForExit 15000)'
    }
    $closeAt = $install.IndexOf('function Close-EqBuddyGracefully')
    $waitAt = $install.IndexOf('WaitForExit(15000)', $closeAt)
    $forceAt = $install.IndexOf('Stop-Process -Force', $waitAt)
    if ($closeAt -lt 0 -or $waitAt -lt 0 -or $forceAt -lt $waitAt) {
        Fail 'Stop-Process is not the fallback after WaitForExit'
    }

    $single = Get-Content (Join-Path $repo 'src\EQBuddy.UI.Shared\SingleInstance.cs') -Raw
    if ($single -notmatch 'public const string LockFileName = "([^"]+)"') {
        Fail 'could not read SingleInstance.LockFileName'
    }
    $lockName = $Matches[1]
    if ($script:EqInstanceLockName -ne $lockName) {
        Fail "lock file name '$script:EqInstanceLockName' is not SingleInstance.LockFileName '$lockName'"
    }

    # The negative is reachable only when the outside copy is genuinely outside
    # the publish directory. A nested path would make the pre-fix filter match
    # it, and the "leaves it running" arm would be aimed at nothing (trap 78).
    if ($outsideDir.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase)) {
        Fail "outside dir is under publish dir ($outsideDir)"
    }
    New-Item -ItemType Directory -Force -Path $evolvedProfile, $v1Profile | Out-Null

    $oldExe = Build-Holder '169.1.0' $outsideDir
    $newExe = Build-Holder '169.2.0' $publishDir
    $v1Exe = Build-Holder '169.0.1' $v1Dir
    if ($oldExe.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase)) {
        Fail "old exe is under publish dir ($oldExe)"
    }
    if (-not $newExe.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase)) {
        Fail "new exe is not under publish dir ($newExe)"
    }

    $evolvedLock = Join-Path $evolvedProfile $lockName
    $v1Lock = Join-Path $v1Profile $lockName
    $old = Start-Holder $oldExe $evolvedLock 'hold'
    $decoy = Start-Holder $newExe $evolvedLock 'free'
    $v1 = Start-Holder $v1Exe $v1Lock 'hold'

    if (-not (Test-ExclusiveLockHeld $evolvedLock)) {
        Fail 'Evolved instance.lock is not held exclusively (not the SingleInstance share mode)'
    }

    # The pre-fix selector, copied here so this file can show what it does.
    # install-local.ps1 must not use it; the source check above refuses that.
    $preFix = @(Get-Process EQBuddy -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($publishDir, [StringComparison]::OrdinalIgnoreCase)
    })
    $preIds = @($preFix | ForEach-Object { $_.Id })
    if ($preIds -notcontains $decoy.Id) {
        Fail "pre-fix path filter did not match the publish-dir copy (pid $($decoy.Id)); the negative is vacuous"
    }
    if ($preIds -contains $old.Id) {
        Fail "pre-fix path filter matched the outside copy (pid $($old.Id)); the staged path is not outside"
    }
    if ($preIds -contains $v1.Id) {
        Fail "pre-fix path filter matched the v1-profile copy (pid $($v1.Id))"
    }
    if ($old.HasExited) { Fail 'outside copy was not left running by the pre-fix filter' }
    $leftOnScreen = Get-RunningProductVersion $old
    if ($leftOnScreen -ne '169.1.0') {
        Fail "pre-fix left ProductVersion '$leftOnScreen' holding the Evolved profile, expected 169.1.0"
    }

    $holders = @(Get-EqProcessesHoldingProfile -ProfileDir $evolvedProfile)
    if ($holders.Count -ne 1) {
        Fail "profile lock matched $($holders.Count) processes, expected the one outside copy"
    }
    if ($holders[0].Id -ne $old.Id) {
        Fail "profile lock matched pid $($holders[0].Id) ($($holders[0].Path)), expected outside pid $($old.Id)"
    }
    $heldVersion = Get-RunningProductVersion $holders[0]
    if ($heldVersion -ne '169.1.0') {
        Fail "process holding the Evolved profile reports ProductVersion '$heldVersion', expected 169.1.0"
    }

    $v1Holders = @(Get-EqProcessesHoldingProfile -ProfileDir $v1Profile)
    if ($v1Holders.Count -ne 1 -or $v1Holders[0].Id -ne $v1.Id) {
        Fail 'v1 profile lock was not attributed to the v1 stand-in (the detector matched nothing)'
    }

    Close-EqBuddyGracefully -Processes $holders
    $old.Refresh()
    if (-not $old.HasExited) { Fail 'profile-lock close left the outside copy running' }
    if (Test-ExclusiveLockHeld $evolvedLock) {
        Fail 'Evolved instance.lock is still held after the close'
    }
    $decoy.Refresh()
    $v1.Refresh()
    if ($decoy.HasExited) { Fail 'profile-lock close stopped the publish-dir copy that was not holding the lock' }
    if ($v1.HasExited) { Fail 'profile-lock close stopped the copy on the v1 profile' }
    $decoyVersion = Get-RunningProductVersion $decoy
    $v1Version = Get-RunningProductVersion $v1
    if ($decoyVersion -ne '169.2.0') { Fail "publish-dir copy ProductVersion drifted to '$decoyVersion'" }
    if ($v1Version -ne '169.0.1') { Fail "v1 copy ProductVersion drifted to '$v1Version'" }

    # The lock file stays on disk after the holder exits (TryClaim does not delete it).
    # A stale file is not a live copy.
    if (-not (Test-Path -LiteralPath $evolvedLock)) { Fail 'expected the lock file to remain after the holder exited' }
    $stale = @(Get-EqProcessesHoldingProfile -ProfileDir $evolvedProfile)
    if ($stale.Count -ne 0) { Fail "stale instance.lock still matched $($stale.Count) processes" }

    $fresh = Start-Holder $newExe $evolvedLock 'hold'
    $now = @(Get-EqProcessesHoldingProfile -ProfileDir $evolvedProfile)
    if ($now.Count -ne 1 -or $now[0].Id -ne $fresh.Id) {
        Fail 'after the close, the profile lock did not follow the publish-dir copy'
    }
    $nowVersion = Get-RunningProductVersion $now[0]
    if ($nowVersion -ne '169.2.0') {
        Fail "profile holder ProductVersion is '$nowVersion' after the close, expected the publish-dir build 169.2.0"
    }
    $v1.Refresh()
    if ($v1.HasExited) { Fail 'starting the new Evolved holder stopped the v1 copy' }
    if ((Get-RunningProductVersion $v1) -ne '169.0.1') {
        Fail 'v1 ProductVersion changed while the Evolved profile was taken over'
    }

    Write-Host 'profile-lock selftest: pre-fix left 169.1.0 holding the profile; post-fix closed it; holder is now 169.2.0; v1 169.0.1 untouched'
    $script:Dra169Result = 0
}
catch {
    Write-Host "FAIL: $($_.Exception.Message)"
    $script:Dra169Result = 1
}
finally {
    foreach ($proc in @($started)) {
        if ($null -eq $proc) { continue }
        try {
            if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
        }
        catch { }
    }
    if (Test-Path -LiteralPath $root) {
        Start-Sleep -Milliseconds 200
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
