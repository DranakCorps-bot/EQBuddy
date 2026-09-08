# Fail-closed helpers for automated EQBuddy launches.
#
# Mirrors UI.Shared/IsolatedLaunchPolicy: refuse %AppData%\EQBuddy and
# %AppData%\EQBuddy Evolved, and strip harness overrides before relaunching
# a player's app. Product launches (install-local.ps1 -Evolved,
# Launch-Evolved-Shell.cmd) do not dot-source this — they are how the owner
# runs Evolved.
#
# The two directory names are AppPaths.LegacyDirName / EvolvedDirName.
# IsolatedLaunchScriptTests fails this file if either spelling moves.

function Get-EqFullDir([string] $Path) {
    if (-not $Path) { return '' }
    try {
        return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    }
    catch {
        return ''
    }
}

function Test-EqLivePlayerProfile([string] $Path) {
    $full = Get-EqFullDir $Path
    if (-not $full) { return $false }
    $roaming = [Environment]::GetFolderPath('ApplicationData')
    foreach ($name in @('EQBuddy', 'EQBuddy Evolved')) {
        $live = Get-EqFullDir (Join-Path $roaming $name)
        if ($live -and [string]::Equals($full, $live, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Assert-EqIsolatedProfile([string] $Path, [string] $Label) {
    if (Test-EqLivePlayerProfile $Path) {
        throw "$Label refused to use live player profile $Path"
    }
}

function Clear-EqHarnessProfileOverrides([System.Diagnostics.ProcessStartInfo] $Psi) {
    foreach ($k in @('EQBUDDY_APPDATA', 'EQBUDDY_V1_APPDATA', 'EQBUDDY_ALLOW_LIVE_APPDATA')) {
        if ($Psi.EnvironmentVariables.ContainsKey($k)) {
            $Psi.EnvironmentVariables.Remove($k)
        }
    }
}
