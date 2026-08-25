# Automated stand-in for the hand-done drag/reopen check (David authorized, 2026-08-24).
# Drives the REAL EQBuddy.exe on an isolated profile and measures the window rect with
# Win32 calls between steps. Five assertions:
#   (a) opens at content height (not a pinned first-frame measurement)
#   (b) BEFORE any drag, content change (tab switch) resizes the window  <- premature-ownership catch
#   (c) close+reopen without drag: no WindowHeights entry persisted; still follows
#   (d) external resize (user-take) sticks; content change no longer resizes
#   (e) reopen restores the taken height
#
# -Window picks which pop-out to check. It was hardcoded to Progress, which is why
# HANDOFF.md could name six windows that gained resize on 2026-08-25 and say plainly that
# none of them had been checked by hand: the harness that answers this question existed
# and could only ask it about one window.
#
# Phase B needs a way to CHANGE the content without touching the size, and a tab strip is
# the only one available from outside the app. A window with no tabs reports B and C2 as
# INCONCLUSIVE and still runs A, C1, D and E — which is the persistence-and-ownership half,
# and the half nobody had measured. Saying "inconclusive" is the point: a harness that
# quietly skipped the check would read as a pass (trap 34's shape).
param(
    [Parameter(Mandatory)][string] $Root,
    [ValidateSet('progress', 'quests', 'gearloot', 'drops', 'spawns', 'travel', 'history', 'timeline')]
    [string] $Window = 'progress',
    [string[]] $Tabs
)

$ErrorActionPreference = 'Stop'
# Derived, not hardcoded: this said C:\Users\david\source\EQBuddy and did not resolve on
# any other checkout, so the script could only ever run on one machine.
$repo = Split-Path -Parent $PSScriptRoot

# Per-window: the env var that opens it, its title, its settings key, and the tab labels
# Phase B can click. Keep this table in step with MainWindow's EQBUDDY_* hooks and with
# ResizableWindowTests.Resizable() — a window that gains resize and is not here cannot be
# checked, which is the state this script was written to end.
$Targets = @{
    progress = @{ Env = 'EQBUDDY_PROGRESS'; Title = 'EQBuddy Progress';            Key = 'progress'; Tabs = @('Experience', 'Wealth', 'Faction') }
    quests   = @{ Env = 'EQBUDDY_QUESTS';   Title = 'EQBuddy Quest Tracker';       Key = 'quests';   Tabs = @('Quests', 'Epic 1.0', 'Plane of Sky') }
    gearloot = @{ Env = 'EQBUDDY_GEARLOOT'; Title = 'EQBuddy Gear & Loot';         Key = 'gearloot'; Tabs = @('Loot', 'Items', 'Wishlist', 'Inventory') }
    drops    = @{ Env = 'EQBUDDY_CREATURE'; Title = 'EQBuddy Kills & Drops';       Key = 'drops';    Tabs = @('Kills', 'Drops') }
    spawns   = @{ Env = 'EQBUDDY_SPAWNS';   Title = 'EQBuddy Spawns';              Key = 'spawns';   Tabs = @() }
    travel   = @{ Env = 'EQBUDDY_TRAVEL';   Title = 'Travel route';                Key = 'travel';   Tabs = @() }
    history  = @{ Env = 'EQBUDDY_HISTORY';  Title = 'EQBuddy — Session History';   Key = 'history';  Tabs = @() }
    timeline = @{ Env = 'EQBUDDY_TIMELINE'; Title = 'EQBuddy fight timeline';      Key = 'timeline'; Tabs = @() }
}

$target = $Targets[$Window]
if ($Tabs) { $target.Tabs = $Tabs }
$winTitle = $target.Title
$winKey = $target.Key
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
    $psi.EnvironmentVariables[$target.Env] = '1'
    [Diagnostics.Process]::Start($psi)
}

function Stop-App($p) {
    if ($p -and -not $p.HasExited) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(8000)) { $p.Kill(); $p.WaitForExit(4000) | Out-Null }
    }
    Start-Sleep -Milliseconds 600
}

# Matched on the owning PROCESS as well as the title, which is trap 24's rule: a title is
# not an identity, and a previous run's app that has not finished exiting is a perfect
# match for the next one's request.
function Find-Target([int]$procId) {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $cond = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
        $wins = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children, $cond)
        foreach ($w in $wins) { if ($w.Current.Name -eq $winTitle) { return $w } }
        Start-Sleep -Milliseconds 300
    }
    throw "$winTitle window not found"
}

# Returns $false rather than throwing when the label is absent: a chip carries its badge in
# its name ("Epic 1.0  3 / 486"), so an exact match is not enough, and a window whose strip
# we cannot drive should report INCONCLUSIVE rather than fail the whole run.
function Click-Label($winEl, [string]$name, [IntPtr]$ownHwnd) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $winEl.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) {
        # Prefix match, for a chip whose label carries a count after it.
        $all = $winEl.FindAll([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::IsOffscreenProperty, $false)))
        foreach ($c in $all) {
            if ($c.Current.Name -and $c.Current.Name.StartsWith($name)) { $el = $c; break }
        }
    }
    if (-not $el) { Write-Host "  (label '$name' not found in $winTitle)"; return $false }
    $b = $el.Current.BoundingRectangle
    $x = [int]($b.X + $b.Width / 2); $y = [int]($b.Y + $b.Height / 2)
    $under = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($x, $y))), 2)
    if ($under -ne $ownHwnd) { throw "point $x,$y belongs to another window (hwnd $under vs $ownHwnd) - not clicking" }
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 120
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero)   # down
    [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)   # up
    Start-Sleep -Milliseconds 250
    return $true
}

function HeightsEntry {
    $j = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($j.PSObject.Properties['WindowHeights'] -and $j.WindowHeights.PSObject.Properties[$winKey]) {
        $j.WindowHeights.$winKey
    } else { $null }
}

# A REAL border drag: press the bottom edge and move. SetWindowPos is not this — the app
# now records a height as the player's only on WM_EXITSIZEMOVE after a resize hit code,
# which is the native size loop and nothing else. A harness that resized programmatically
# and then asserted the height was kept would be testing a path no player can take, and
# would report FAIL for correct behaviour.
#
# Returns the new height, or $null when the bottom edge cannot be aimed at.
function Drag-BottomEdge([IntPtr]$hwnd, [int]$delta) {
    $rect = New-Object W.U+RECT
    [W.U]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $midX = [int]($rect.L + ($rect.R - $rect.L) / 2)
    $edgeY = -1
    foreach ($off in 1..14) {
        $probe = $rect.B - $off
        $owner = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($midX, $probe))), 2)
        if ($owner -eq $hwnd) { $edgeY = $probe; break }
    }
    if ($edgeY -lt 0) { return $null }
    [W.U]::SetCursorPos($midX, $edgeY) | Out-Null; Start-Sleep -Milliseconds 200
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    $steps = 8
    foreach ($i in 1..$steps) {
        [W.U]::SetCursorPos($midX, $edgeY + [int]($delta * $i / $steps)) | Out-Null
        [W.U]::mouse_event(1, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 60
    }
    [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    Settle $hwnd
}

$results = [Collections.ArrayList]::new()
function Note([string]$s) { Write-Host $s; $null = $results.Add($s) }

$app = $null
try {
    # ---- Phase A: launch, find window, park it in a clear region -----------------
    $app = Start-App
    $win = Find-Target $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 80, 80, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null  # move only
    Start-Sleep -Milliseconds 300
    $hExp = Settle $hwnd
    Note "A: $Window settled height = $hExp px (width $(Get-W $hwnd))"

    # ---- Phase B: content change BEFORE any drag must resize the window ----------
    $tabs = @($target.Tabs)
    $hWea = $hExp; $hExp2 = $hExp
    if ($tabs.Count -lt 2) {
        Note "B: INCONCLUSIVE - $Window has no tab strip, so there is no way to change its content from outside the app"
    } else {
        $heights = @($hExp)
        $ok = $true
        foreach ($t in $tabs[1..($tabs.Count - 1)]) {
            if (Click-Label $win $t $hwnd) { $heights += Settle $hwnd } else { $ok = $false }
        }
        if ($ok -and (Click-Label $win $tabs[0] $hwnd)) { $hExp2 = Settle $hwnd } else { $ok = $false }
        if ($heights.Count -gt 1) { $hWea = $heights[1] }
        Note "B: tab heights $($tabs -join '/') = $($heights -join '/') back-on-first=$hExp2"
        $spread = ($heights | Measure-Object -Maximum).Maximum - ($heights | Measure-Object -Minimum).Minimum
        if (-not $ok) { Note "B: INCONCLUSIVE - could not click every tab of $Window" }
        elseif ($spread -gt 20 -and [Math]::Abs($hExp2 - $hExp) -le 6) { Note 'B: PASS - window still FOLLOWS content (resizes per tab, returns on the first)' }
        elseif ($spread -le 20) { Note 'B: INCONCLUSIVE - all tabs measured within 20px of each other' }
        else { Note "B: FAIL - did not return to the first tab's height (drift $([Math]::Abs($hExp2-$hExp))px)" }
    }

    # ---- Phase C: close without drag -> nothing persisted; reopen still follows --
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null  # WM_CLOSE
    Start-Sleep -Milliseconds 1500
    $entry = HeightsEntry
    if ($null -eq $entry) { Note "C1: PASS - no WindowHeights.$winKey entry after undragged close" }
    else { Note "C1: FAIL - undragged close persisted height $entry (premature ownership!)" }

    Stop-App $app
    $app = Start-App
    $win = Find-Target $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 80, 80, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null
    Start-Sleep -Milliseconds 300
    $hExp3 = Settle $hwnd
    if ($tabs.Count -ge 2) {
        $clicked = Click-Label $win $tabs[1] $hwnd
        if ($clicked) { $hWea2 = Settle $hwnd } else { $hWea2 = $hWea }
        $null = Click-Label $win $tabs[0] $hwnd; $null = Settle $hwnd
        Note "C2: reopened first=$hExp3 (was $hExp); second=$hWea2 (was $hWea)"
        if ([Math]::Abs($hExp3 - $hExp) -le 10 -and [Math]::Abs($hWea2 - $hWea) -le 10) { Note 'C2: PASS - reopened window still follows content' }
        else { Note 'C2: FAIL - reopened window not following (or content changed between runs)' }
    } else {
        Note "C2: reopened at $hExp3 px (was $hExp)"
        if ([Math]::Abs($hExp3 - $hExp) -le 10) { Note 'C2: PASS (partial) - reopened at the same content height; no tabs to prove it still FOLLOWS' }
        else { Note "C2: FAIL - reopened at $hExp3, expected ~$hExp" }
    }

    # ---- Phase D: external resize = the player takes the height ------------------
    # SHRINK, not grow. A window that is already sized to its content can be at the
    # monitor work-area cap — the Quest Tracker opens at 1822px on a tall screen — and
    # asking it to grow then fails for a reason that has nothing to do with ownership,
    # which reads as a defect in exactly the thing being measured. Shrinking is always
    # available, and it is also the operation Hateborne actually asked for on 2026-08-21:
    # making a tab short enough to scroll.
    # Park it fully on screen: a window restored at a saved position can have its bottom
    # edge off the monitor, which probes as "no border" and reads like a defect.
    [W.U]::SetWindowPos($hwnd, [IntPtr]::Zero, 80, 40, 0, 0, 0x0001 -bor 0x0004 -bor 0x0010) | Out-Null
    Start-Sleep -Milliseconds 400
    $before = Get-H $hwnd
    # Shrink if there is room above the floor, otherwise grow. A window already at its
    # MinHeight cannot shrink, and asserting that it did would fail for a reason that has
    # nothing to do with what is being measured.
    $delta = if ($before -gt 520) { -200 } else { 200 }
    $hTaken = Drag-BottomEdge $hwnd $delta
    if ($null -eq $hTaken) {
        Note "D1: INCONCLUSIVE - could not aim at the bottom edge of $Window"
        $hTaken = $before
    } elseif ([Math]::Abs($hTaken - $before) -ge 40) {
        Note "D1: PASS - a real bottom-edge drag moved the height $before -> $hTaken"
    } else {
        Note "D1: FAIL - a real bottom-edge drag moved the height only $before -> $hTaken. CanResize is set and the chrome offers no border to grab; FramelessResize's WM_NCHITTEST hook is what provides one."
    }
    if ($tabs.Count -ge 2 -and (Click-Label $win $tabs[1] $hwnd)) {
        $hAfterTab = Settle $hwnd
        if ([Math]::Abs($hAfterTab - $hTaken) -le 4) { Note "D2: PASS - owned: tab switch no longer resizes ($hAfterTab)" }
        else { Note "D2: FAIL - window resized to $hAfterTab after the player took the height" }
    } else {
        Note "D2: INCONCLUSIVE - no tab strip on $Window to change the content with"
    }
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1500
    $entry2 = HeightsEntry
    if ($null -ne $entry2) { Note "D3: PASS - dragged close persisted WindowHeights.$winKey = $entry2 (DIU; rect was $hTaken px)" }
    else { Note 'D3: FAIL - player-taken height was not persisted' }

    # ---- Phase E: reopen restores the taken height -------------------------------
    Stop-App $app
    $app = Start-App
    $win = Find-Target $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 300
    $hRestored = Settle $hwnd
    Note "E: reopened at $hRestored px (taken was $hTaken px)"
    if ([Math]::Abs($hRestored - $hTaken) -le 10) { Note 'E1: PASS - dragged height restored on reopen' }
    else { Note "E1: FAIL - expected ~$hTaken, got $hRestored" }
    if ($tabs.Count -ge 3 -and (Click-Label $win $tabs[2] $hwnd)) {
        $hOwnedTab = Settle $hwnd
        if ([Math]::Abs($hOwnedTab - $hRestored) -le 4) { Note 'E2: PASS - ownership survives restart (tab switch does not resize)' }
        else { Note "E2: FAIL - resized to $hOwnedTab; ownership lost across restart" }
    } else {
        Note "E2: INCONCLUSIVE - no third tab on $Window to change the content with"
    }

    # (Phase F retired: it asked the same question as D, which now drags for real.)
}
finally {
    Stop-App $app
    Write-Host '--- SUMMARY ---'
    $results | ForEach-Object { Write-Host $_ }
}
