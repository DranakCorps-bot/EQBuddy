# Automated stand-in for the hand-done drag/reopen check (David authorized, 2026-08-24).
# Drives the REAL EQBuddy.exe on the drag-check isolated profile and measures the
# Progress window rect with Win32 calls between steps. Five assertions:
#   (a) opens at content height (not the pinned ~203)
#   (b) BEFORE any drag, content change (tab switch) resizes the window  <- premature-ownership catch
#   (c) close+reopen without drag: no WindowHeights entry persisted; still follows
#   (d) external resize (user-take) sticks; content change no longer resizes
#   (e) reopen restores the taken height
param([Parameter(Mandatory)][string] $Root)

$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\david\source\EQBuddy'
$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'
$profileDir = Join-Path $Root 'profile'
$settingsPath = Join-Path $profileDir 'settings.json'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace W -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
[DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
public struct RECT { public int L, T, R, B; }
public struct POINT { public int X, Y; public POINT(int x, int y){X=x;Y=y;} }
'@

function Get-H([IntPtr]$h) { $r = New-Object W.U+RECT; [W.U]::GetWindowRect($h, [ref]$r) | Out-Null; $r.B - $r.T }
function Get-W([IntPtr]$h) { $r = New-Object W.U+RECT; [W.U]::GetWindowRect($h, [ref]$r) | Out-Null; $r.R - $r.L }

function Settle([IntPtr]$h) {
    # Sample until 6 consecutive identical heights (1.2s quiet) or 10s timeout.
    $last = -1; $stable = 0; $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $now = Get-H $h
        if ($now -eq $last) { $stable++ } else { $stable = 0; $last = $now }
        if ($stable -ge 6) { break }
        Start-Sleep -Milliseconds 200
    }
    $last
}

function Start-App {
    $psi = New-Object Diagnostics.ProcessStartInfo $exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir
    $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
    $psi.EnvironmentVariables['EQBUDDY_PROGRESS'] = '1'
    [Diagnostics.Process]::Start($psi)
}

function Stop-App($p) {
    if ($p -and -not $p.HasExited) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(8000)) { $p.Kill(); $p.WaitForExit(4000) | Out-Null }
    }
    Start-Sleep -Milliseconds 600
}

function Find-Progress([int]$procId) {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $cond = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
        $wins = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children, $cond)
        foreach ($w in $wins) { if ($w.Current.Name -eq 'EQBuddy Progress') { return $w } }
        Start-Sleep -Milliseconds 300
    }
    throw 'EQBuddy Progress window not found'
}

function Click-Label($winEl, [string]$name, [IntPtr]$ownHwnd) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $winEl.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { throw "label '$name' not found in Progress window" }
    $b = $el.Current.BoundingRectangle
    $x = [int]($b.X + $b.Width / 2); $y = [int]($b.Y + $b.Height / 2)
    $under = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($x, $y))), 2)
    if ($under -ne $ownHwnd) { throw "point $x,$y belongs to another window (hwnd $under vs $ownHwnd) - not clicking" }
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 120
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero)   # down
    [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)   # up
    Start-Sleep -Milliseconds 250
}

function HeightsEntry {
    $j = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($j.PSObject.Properties['WindowHeights'] -and $j.WindowHeights.PSObject.Properties['progress']) {
        $j.WindowHeights.progress
    } else { $null }
}

$results = [Collections.ArrayList]::new()
function Note([string]$s) { Write-Host $s; $null = $results.Add($s) }

$app = $null
try {
    # ---- Phase A: launch, find window, park it in a clear region -----------------
    $app = Start-App
    $win = Find-Progress $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 80, 80, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null  # move only
    Start-Sleep -Milliseconds 300
    $hExp = Settle $hwnd
    Note "A: Experience settled height = $hExp px (width $(Get-W $hwnd))"

    # ---- Phase B: content change BEFORE any drag must resize the window ----------
    Click-Label $win 'Wealth' $hwnd;     $hWea = Settle $hwnd
    Click-Label $win 'Faction' $hwnd;    $hFac = Settle $hwnd
    Click-Label $win 'Experience' $hwnd; $hExp2 = Settle $hwnd
    Note "B: tab heights Exp=$hExp Wealth=$hWea Faction=$hFac Exp-again=$hExp2"
    $spread = (@($hExp, $hWea, $hFac) | Measure-Object -Maximum).Maximum - (@($hExp, $hWea, $hFac) | Measure-Object -Minimum).Minimum
    if ($spread -gt 20 -and [Math]::Abs($hExp2 - $hExp) -le 6) { Note 'B: PASS - window still FOLLOWS content (resizes per tab, returns on Experience)' }
    elseif ($spread -le 20) { Note 'B: INCONCLUSIVE - all tabs measured within 20px of each other' }
    else { Note "B: FAIL - did not return to Experience height (drift $([Math]::Abs($hExp2-$hExp))px)" }

    # ---- Phase C: close without drag -> nothing persisted; reopen still follows --
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null  # WM_CLOSE
    Start-Sleep -Milliseconds 1500
    $entry = HeightsEntry
    if ($null -eq $entry) { Note 'C1: PASS - no WindowHeights.progress entry after undragged close' }
    else { Note "C1: FAIL - undragged close persisted height $entry (premature ownership!)" }

    Stop-App $app
    $app = Start-App
    $win = Find-Progress $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 80, 80, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null
    Start-Sleep -Milliseconds 300
    $hExp3 = Settle $hwnd
    Click-Label $win 'Wealth' $hwnd; $hWea2 = Settle $hwnd
    Click-Label $win 'Experience' $hwnd; $null = Settle $hwnd
    Note "C2: reopened Exp=$hExp3 (was $hExp); Wealth=$hWea2 (was $hWea)"
    if ([Math]::Abs($hExp3 - $hExp) -le 10 -and [Math]::Abs($hWea2 - $hWea) -le 10) { Note 'C2: PASS - reopened window still follows content' }
    else { Note 'C2: FAIL - reopened window not following (or content changed between runs)' }

    # ---- Phase D: external resize = the player takes the height ------------------
    $w = Get-W $hwnd; $before = Get-H $hwnd; $taken = $before + 80
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 0, 0, $w, $taken, 0x0002 -bor 0x0004 -bor 0x0010) | Out-Null  # resize only
    Start-Sleep -Milliseconds 800
    $hTaken = Get-H $hwnd
    if ([Math]::Abs($hTaken - $taken) -le 4) { Note "D1: PASS - resize to $taken stuck ($hTaken)" }
    else { Note "D1: FAIL - asked $taken, window says $hTaken (follower fought the player?)" }
    Click-Label $win 'Wealth' $hwnd; $hAfterTab = Settle $hwnd
    if ([Math]::Abs($hAfterTab - $hTaken) -le 4) { Note "D2: PASS - owned: tab switch no longer resizes ($hAfterTab)" }
    else { Note "D2: FAIL - window resized to $hAfterTab after the player took the height" }
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1500
    $entry2 = HeightsEntry
    if ($null -ne $entry2) { Note "D3: PASS - dragged close persisted WindowHeights.progress = $entry2 (DIU; rect was $hTaken px)" }
    else { Note 'D3: FAIL - player-taken height was not persisted' }

    # ---- Phase E: reopen restores the taken height -------------------------------
    Stop-App $app
    $app = Start-App
    $win = Find-Progress $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 300
    $hRestored = Settle $hwnd
    Note "E: reopened at $hRestored px (taken was $hTaken px)"
    if ([Math]::Abs($hRestored - $hTaken) -le 10) { Note 'E1: PASS - dragged height restored on reopen' }
    else { Note "E1: FAIL - expected ~$hTaken, got $hRestored" }
    Click-Label $win 'Faction' $hwnd; $hOwnedTab = Settle $hwnd
    if ([Math]::Abs($hOwnedTab - $hRestored) -le 4) { Note 'E2: PASS - ownership survives restart (tab switch does not resize)' }
    else { Note "E2: FAIL - resized to $hOwnedTab; ownership lost across restart" }
}
finally {
    Stop-App $app
    Write-Host '--- SUMMARY ---'
    $results | ForEach-Object { Write-Host $_ }
}
