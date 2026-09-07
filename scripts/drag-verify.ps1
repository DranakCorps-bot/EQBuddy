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
#
# -Mode park is OE-8's half, and it asks a DIFFERENT question of DIFFERENT windows. The
# phases above are about a window taking ownership of its HEIGHT from its content; free
# placement is about a companion window taking ownership of its POSITION from the widget it
# follows. Same five-phase shape, same reason for existing — the automated stand-in for a
# hand-done drag/reopen check, on the only two windows in the app that are placed by a
# follower rather than by a saved point:
#   (P0) untouched profile -> slaved, and NOTHING in settings.json    <- the default is SA-2
#   (P1) a real drag parks it, at drag end                            <- the one writer
#   (P2) close+reopen -> the parked point comes back exactly          <- #152 inverted
#   (P3) "Follow the HUD again" clears it back to slaved              <- the way back
# The MISSING-MONITOR half stays a unit test (`HudParkTests`): a harness cannot detach a
# display, and pretending it can would be a guess about the machine.
param(
    # Omit to get a throwaway root with a seeded profile (the 2026-08-25 rewrite's
    # self-contained setup, kept through the #238 merge so one command answers the
    # question). Pass an existing root to re-enter a previous run's profile.
    [string] $Root,
    [ValidateSet('height', 'park')]
    [string] $Mode = 'height',
    [ValidateSet('progress', 'quests', 'gearloot', 'drops', 'spawns', 'travel', 'history', 'timeline',
                 'hudrow', 'hudpanel')]
    [string] $Window = 'progress',
    [string[]] $Tabs
)

$ErrorActionPreference = 'Stop'
# Derived, not hardcoded: this said C:\Users\david\source\EQBuddy and did not resolve on
# any other checkout, so the script could only ever run on one machine.
$repo = Split-Path -Parent $PSScriptRoot

if (-not $Root) {
    $Root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-drag-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
    $newProfile = New-Item -ItemType Directory -Force (Join-Path $Root 'profile')
    $logsDir = New-Item -ItemType Directory -Force (Join-Path $Root 'game/Logs')
    New-Item -ItemType Directory -Force (Join-Path $Root 'updates') | Out-Null
    & (Join-Path $repo 'scripts/make-test-session.ps1') -Out $logsDir.FullName | Out-Null
    $ver = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    @{
        LogFolder = $logsDir.FullName; ShowTutorial = $false; LastSeenVersion = $ver
        TruncateLogs = $false; UpdateFolder = (Join-Path $Root 'updates'); Theme = 'Midnight'
        # The one-time junk-heights clear must not fire mid-run and eat phase D's entry.
        WindowHeightsReset = $true
        # -Mode park: the chip row is up exactly while a timer is running, and the under-bar
        # panel exists only on the COLLAPSED bar. Neither is a state this harness can reach
        # from outside the app, so both are seeded — trap 22's rule (a surface with no
        # fixture state cannot be reviewed, and reads as reviewed anyway).
        TrackSpawns = $true
        Minimized = ($Mode -eq 'park')
        # Deliberately NOT seeded: HudRowPark*/HudPanelPark*/HudPanelWidth. Phase P0's whole
        # assertion is that an untouched profile has no park in it AT ALL, and a key written
        # here — even a null one — would be the thing it was looking for.
    } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $newProfile 'settings.json') -Encoding utf8
    if ($Mode -eq 'park') {
        # One long countdown, so the row is up for the whole run and never flips to DUE and
        # gets clicked away by a phase aiming at the window behind it.
        @(@{
            Server = 'test'; Zone = 'Runnyeye Citadel'; Name = 'Kizdean Gix'
            KilledAt = (Get-Date).AddSeconds(-60); DurationSeconds = 1800
        }) | ConvertTo-Json -Depth 6 -AsArray |
            Set-Content (Join-Path $newProfile 'spawn-timers.json') -Encoding utf8
    }
    Write-Host "ROOT $Root"
}

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

# OE-8's two companion windows. Left/Top are the settings keys the park pair lives under;
# Dump is the EFFECT key in the EQBUDDY_EXPAND dump, which is a different claim from the
# setting (trap 42) and the reason both are read below. The TITLES are identities the window
# itself owns — change one in the source and this table is what goes stale, silently, which
# is exactly how three shot rows went dark for six days (trap 53). Grep scripts/ before
# renaming either window.
$ParkTargets = @{
    hudrow   = @{ Title = 'EQBuddy HUD Chips'; Left = 'HudRowParkLeft';   Top = 'HudRowParkTop';   Dump = 'hudRowPark' }
    hudpanel = @{ Title = 'EQBuddy HUD Panel'; Left = 'HudPanelParkLeft'; Top = 'HudPanelParkTop'; Dump = 'hudPanelPark' }
}

if ($Mode -eq 'park' -and -not $ParkTargets.ContainsKey($Window)) {
    throw "-Mode park takes -Window hudrow or hudpanel; '$Window' is a height-mode window."
}
if ($Mode -ne 'park' -and $ParkTargets.ContainsKey($Window)) {
    throw "-Window $Window is a companion window with no height of its own - use -Mode park."
}

$target = if ($Mode -eq 'park') { $ParkTargets[$Window] } else { $Targets[$Window] }
if ($Tabs -and $Mode -ne 'park') { $target.Tabs = $Tabs }
$winTitle = $target.Title
$winKey = if ($Mode -eq 'park') { $Window } else { $target.Key }
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
    if ($Mode -eq 'park') {
        # The two companion windows have no env hook of their own and never will: they are
        # not doors, they are windows the app puts up when it has something to say. The row
        # comes up because a seeded timer is running; the panel because EQBUDDY_HUDEXPAND
        # PINS it (`dps`, not `dps:peek` — a peek collapses the moment the pointer leaves,
        # and every phase below moves the pointer).
        $psi.EnvironmentVariables['EQBUDDY_EXPAND'] = '1'
        if ($Window -eq 'hudpanel') { $psi.EnvironmentVariables['EQBUDDY_HUDEXPAND'] = 'dps' }
    } else {
        $psi.EnvironmentVariables[$target.Env] = '1'
    }
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

# ---- -Mode park helpers ------------------------------------------------------------------

# The park pair AS THE PROFILE HOLDS IT, or $null when there is none. A key that is absent
# and a key that is present-and-null are the same answer here (slaved), and both are what an
# untouched profile looks like — the app writes the pair only at drag end.
function ParkEntry {
    $j = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $l = $target.Left; $t = $target.Top
    if (-not $j.PSObject.Properties[$l] -or $null -eq $j.$l) { return $null }
    if (-not $j.PSObject.Properties[$t] -or $null -eq $j.$t) { return $null }
    # System.Text.Json writes NaN as the string "NaN"; ConvertFrom-Json gives it back as one.
    if ("$($j.$l)" -eq 'NaN' -or "$($j.$t)" -eq 'NaN') { return $null }
    @{ Left = [double]$j.$l; Top = [double]$j.$t }
}

# The EFFECT, off the running app's own dump — "slaved" or "left,top". Read BESIDE the
# setting above because they are different claims (trap 42) and OE-8's unreachable rule is
# precisely a disagreement between them.
function DumpKey([string]$key) {
    $path = Join-Path $profileDir 'debug.txt'
    if (-not (Test-Path $path)) { return '(no dump)' }
    $text = Get-Content $path -Raw
    if ($text -match "(?<![A-Za-z])$key=(\S+)") { $Matches[1] } else { '(key absent)' }
}

function ParkDump { DumpKey $target.Dump }

# The GRIP's own "presses,drags" for this window. Printed on every phase because the three
# ways a park phase can fail are indistinguishable without it: "0,0" means the synthetic
# pointer never reached the window at all, "1,0" means the press arrived and never became a
# drag, and "1,1" means the gesture completed and the write is what is wrong.
function GripDump { DumpKey "$(if ($Window -eq 'hudrow') { 'hudRowGrip' } else { 'hudPanelGrip' })" }

function WindowOrigin([IntPtr]$h) {
    $r = New-Object W.U+RECT; [W.U]::GetWindowRect($h, [ref]$r) | Out-Null
    @{ X = $r.L; Y = $r.T }
}

# A REAL move: press somewhere in the BODY of the companion window and drag. Not
# SetWindowPos — the app writes a park only at the end of a pointer gesture that crossed the
# system drag threshold, which is the whole of trap 49's fix, so a harness that moved the
# window programmatically and then asserted a park would be testing a path no player can take
# and would report FAIL for correct behaviour (the same sentence Drag-BottomEdge carries).
#
# The press lands one third of the way in rather than at the centre: the panel's centre is
# its rows and its edges are the resize zones, and a third across the top strip is body on
# both windows.
function Drag-Body([IntPtr]$hwnd, [int]$dx, [int]$dy) {
    $rect = New-Object W.U+RECT
    [W.U]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    # SEARCH for a point that belongs to this window rather than assuming its middle does.
    # Both of these are per-pixel-alpha layered windows stacked among other Topmost ones, so
    # "the centre of the rect" is a guess about compositing: the panel's own centre came back
    # owned by a different hwnd (its body is a ScaleTransform over a rounded Border, and the
    # chip row is Topmost beside it). A scan is a fact. Same move Drag-BottomEdge already
    # makes when it probes inward for a border it can actually grab.
    $x = -1; $y = -1
    foreach ($fy in 0.25, 0.15, 0.5, 0.75) {
        foreach ($fx in 0.5, 0.35, 0.65, 0.2) {
            $px = [int]($rect.L + ($rect.R - $rect.L) * $fx)
            $py = [int]($rect.T + ($rect.B - $rect.T) * $fy)
            $owner = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($px, $py))), 2)
            if ($owner -eq $hwnd) { $x = $px; $y = $py; break }
        }
        if ($x -ge 0) { break }
    }
    if ($x -lt 0) {
        Write-Host "  (no point inside $($rect.L),$($rect.T)-$($rect.R),$($rect.B) belongs to hwnd $hwnd)"
        return $null
    }
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 200
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero)   # down
    Start-Sleep -Milliseconds 150
    foreach ($i in 1..8) {
        [W.U]::SetCursorPos($x + [int]($dx * $i / 8), $y + [int]($dy * $i / 8)) | Out-Null
        [W.U]::mouse_event(1, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 60
    }
    [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)   # up — THE DRAG END, and the one write
    Start-Sleep -Milliseconds 800
    WindowOrigin $hwnd
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
if ($Mode -eq 'park') {
    # ===================== -Mode park: OE-8 free placement ==========================
    # Four phases, and the ORDER is the argument: P0 has to run on a profile no phase has
    # touched yet, so it goes first and asserts an ABSENCE — which is only meaningful
    # because P1 immediately afterwards produces the presence (trap 62: every "did not
    # write" needs a positive on the far side of the same decision, or it is satisfied by
    # the state that was already there).

    # ---- P0: an untouched profile is SLAVED, and nothing is in the file -----------
    $app = Start-App
    $win = Find-Target $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 1200
    $before = WindowOrigin $hwnd
    $entry0 = ParkEntry
    $dump0 = ParkDump
    Note "P0: $Window opened at $($before.X),$($before.Y); dump $($target.Dump)=$dump0 grip=$(GripDump); settings pair = $(if ($null -eq $entry0) { '(none)' } else { "$($entry0.Left),$($entry0.Top)" })"
    if ($null -eq $entry0 -and $dump0 -eq 'slaved') {
        Note "P0: PASS - untouched profile follows the widget and holds no park (NaN is slaved)"
    } elseif ($null -ne $entry0) {
        Note "P0: FAIL - a launch with no drag wrote a park ($($entry0.Left),$($entry0.Top)). Something other than a drag end can reach the setting."
    } else {
        Note "P0: FAIL - the dump says '$dump0', expected 'slaved'"
    }

    # ---- P1: a real drag parks it, AT DRAG END ------------------------------------
    $moved = Drag-Body $hwnd 220 -140
    if ($null -eq $moved) {
        Note "P1: INCONCLUSIVE - could not aim at the body of $winTitle"
    } else {
        $entry1 = ParkEntry
        $dump1 = ParkDump
        Note "P1: dragged to $($moved.X),$($moved.Y); dump $($target.Dump)=$dump1 grip=$(GripDump); settings pair = $(if ($null -eq $entry1) { '(none)' } else { "$($entry1.Left),$($entry1.Top)" })"
        if ($null -ne $entry1 -and $dump1 -ne 'slaved') {
            Note 'P1: PASS - the drag END wrote the park and the window is running parked'
        } elseif ($null -eq $entry1) {
            Note "P1: FAIL - a real body drag persisted nothing"
        } else {
            Note "P1: FAIL - the park is in the file but the dump still says '$dump1'"
        }
    }

    # ---- P2: close + reopen restores the parked point exactly (#152, inverted) ----
    # #152 was chips that WALKED up the screen one row per reopen off a saved position.
    # The same assertion with the sign flipped: a point the player chose comes back, to the
    # pixel, and does not drift.
    Stop-App $app
    $app = Start-App
    $win = Find-Target $app.Id
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 1500
    $reopened = WindowOrigin $hwnd
    $entry2 = ParkEntry
    if ($null -eq $entry2) {
        Note 'P2: INCONCLUSIVE - no park in the file to restore (see P1)'
    } else {
        Note "P2: reopened at $($reopened.X),$($reopened.Y) (parked pair $($entry2.Left),$($entry2.Top))"
        if ([Math]::Abs($reopened.X - $entry2.Left) -le 4 -and [Math]::Abs($reopened.Y - $entry2.Top) -le 4) {
            Note 'P2: PASS - the parked point restored, and did not walk'
        } else {
            Note "P2: FAIL - expected ~$($entry2.Left),$($entry2.Top)"
        }
    }

    # ---- P3: "Follow the HUD again" clears it back to slaved ----------------------
    # Edit HUD is reached by a right-click menu row on the WIDGET, which this harness has no
    # way to drive — EQBUDDY_HUDEDIT opens the mode but there is no hook that clicks a
    # control inside it. Reported as INCONCLUSIVE rather than skipped: a harness that quietly
    # left a phase out reads as a pass (trap 34's shape). The un-park is covered by
    # HudParkTests' un-park assertion and by hand at review.
    Note 'P3: INCONCLUSIVE - "Follow the HUD again" is a click inside Edit HUD, and no hook drives a control in that mode'
    return
}
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
