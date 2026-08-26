# Window-resize acceptance harness (supersedes the 2026-08-24 follower-era version, which
# tested a reverted design and whose SetWindowPos phase cannot exercise the NC-grab path).
# Drives the REAL exe on an isolated profile: UIA-located clicks, REAL border drags via
# relative mouse injection through the modal resize loop, Win32 rect measurements, and
# settings.json assertions. Progress = the full follow/own/persist acceptance (9 phases);
# History = new-AllowResize-caller drag/persist spot check. First green run: 2026-08-25,
# v1.99.11 review (P-A..P-E2, H-D1..H-E1).
$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\david\source\EQBuddy'
$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'

$Root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-drag2-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$profileDir = New-Item -ItemType Directory -Force (Join-Path $Root 'profile')
$logsDir = New-Item -ItemType Directory -Force (Join-Path $Root 'game/Logs')
New-Item -ItemType Directory -Force (Join-Path $Root 'updates') | Out-Null
& (Join-Path $repo 'scripts/make-test-session.ps1') -Out $logsDir.FullName | Out-Null
$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
@{
    LogFolder = $logsDir.FullName; ShowTutorial = $false; LastSeenVersion = $version
    TruncateLogs = $false; UpdateFolder = (Join-Path $Root 'updates'); Theme = 'Midnight'
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $profileDir 'settings.json') -Encoding utf8
$settingsPath = Join-Path $profileDir 'settings.json'
Write-Host "ROOT $Root"

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
function Get-RectOf([IntPtr]$h) { $r = New-Object W.U+RECT; [W.U]::GetWindowRect($h, [ref]$r) | Out-Null; $r }
function Settle([IntPtr]$h) {
    $last = -1; $stable = 0; $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $now = Get-H $h
        if ($now -eq $last) { $stable++ } else { $stable = 0; $last = $now }
        if ($stable -ge 6) { break }
        Start-Sleep -Milliseconds 200
    }
    $last
}
function Start-App([hashtable]$extra) {
    $psi = New-Object Diagnostics.ProcessStartInfo $exe
    $psi.UseShellExecute = $false
    $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
    $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
    foreach ($k in $extra.Keys) { $psi.EnvironmentVariables[$k] = $extra[$k] }
    [Diagnostics.Process]::Start($psi)
}
function Stop-App($p) {
    if ($p -and -not $p.HasExited) {
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(8000)) { $p.Kill(); $p.WaitForExit(4000) | Out-Null }
    }
    Start-Sleep -Milliseconds 700
}
function Find-Win([int]$procId, [string]$title) {
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $cond = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
        $wins = [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children, $cond)
        foreach ($w in $wins) { if ($w.Current.Name -like $title) { return $w } }
        Start-Sleep -Milliseconds 300
    }
    throw "window '$title' not found"
}
function Click-Label($winEl, [string]$name, [IntPtr]$ownHwnd) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::NameProperty, $name)
    $el = $winEl.FindFirst([Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { throw "label '$name' not found" }
    $b = $el.Current.BoundingRectangle
    $x = [int]($b.X + $b.Width / 2); $y = [int]($b.Y + $b.Height / 2)
    $ok = $false
    for ($try = 0; $try -lt 5; $try++) {
        [W.U]::SetWindowPos($ownHwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010) | Out-Null
        Start-Sleep -Milliseconds 150
        $under = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($x, $y))), 2)
        if ($under -eq $ownHwnd) { $ok = $true; break }
        Start-Sleep -Milliseconds 300
    }
    if (-not $ok) { throw "point $x,$y is another window's - not clicking" }
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 120
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}
# REAL border drag: press on the bottom edge, relative moves through the modal loop, release.
function Drag-BottomEdge([IntPtr]$h, [int]$dy) {
    $r = Get-RectOf $h
    $x = [int](($r.L + $r.R) / 2); $y = $r.B - 3
    $ok = $false
    for ($try = 0; $try -lt 5; $try++) {
        [W.U]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0010) | Out-Null
        Start-Sleep -Milliseconds 150
        $under = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($x, $y))), 2)
        if ($under -eq $h) { $ok = $true; break }
        Start-Sleep -Milliseconds 300
    }
    if (-not $ok) { throw "edge point $x,$y belongs to another window - not dragging" }
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 150
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero)      # left down
    Start-Sleep -Milliseconds 120
    $steps = 10
    for ($i = 0; $i -lt $steps; $i++) {
        [W.U]::mouse_event(1, 0, [int]($dy / $steps), 0, [IntPtr]::Zero)  # relative move
        Start-Sleep -Milliseconds 40
    }
    Start-Sleep -Milliseconds 120
    [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)      # left up
    Start-Sleep -Milliseconds 400
}
function HeightsEntry([string]$key) {
    $j = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($j.PSObject.Properties['WindowHeights'] -and $j.WindowHeights.PSObject.Properties[$key]) {
        $j.WindowHeights.$key
    } else { $null }
}
function Park([IntPtr]$h) {
    foreach ($spot in @(@(80,80), @(640,80), @(80,560), @(1250,300), @(640,560), @(1250,700))) {
        [W.U]::SetWindowPos($h, [IntPtr](-1), $spot[0], $spot[1], 0, 0, 0x0001 -bor 0x0010) | Out-Null
        Start-Sleep -Milliseconds 250
        $r = Get-RectOf $h
        $cx = [int](($r.L + $r.R) / 2); $cy = [int](($r.T + $r.B) / 2)
        $ok = $true
        foreach ($pt in @(@($cx,$cy), @($cx, ($r.T + 40)), @($cx, ($r.B - 3)))) {
            $u = [W.U]::GetAncestor([W.U]::WindowFromPoint((New-Object W.U+POINT($pt[0], $pt[1]))), 2)
            if ($u -ne $h) { $ok = $false; break }
        }
        if ($ok) { return }
    }
    throw 'no unobstructed parking spot found'
}
$results = [Collections.ArrayList]::new()
function Note([string]$s) { Write-Host $s; $null = $results.Add($s) }

$app = $null
try {
    # ================= PROGRESS: full V2 acceptance =================
    $app = Start-App @{ EQBUDDY_PROGRESS = '1' }
    $win = Find-Win $app.Id 'EQBuddy Progress'
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Park $hwnd
    $hExp = Settle $hwnd
    Note "P-A: Experience opened/settled at $hExp px"
    Click-Label $win 'Wealth' $hwnd;     $hWea = Settle $hwnd
    Click-Label $win 'Experience' $hwnd; $hExp2 = Settle $hwnd
    if ([Math]::Abs($hWea - $hExp) -gt 20 -and [Math]::Abs($hExp2 - $hExp) -le 6) {
        Note "P-B: PASS - still follows content before any drag (Exp=$hExp Wea=$hWea Exp2=$hExp2)"
    } else { Note "P-B: FAIL/INCONCLUSIVE (Exp=$hExp Wea=$hWea Exp2=$hExp2)" }
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1500
    if ($null -eq (HeightsEntry 'progress')) { Note 'P-C1: PASS - undragged close persisted nothing' }
    else { Note "P-C1: FAIL - undragged close persisted $(HeightsEntry 'progress')" }
    Stop-App $app

    $app = Start-App @{ EQBUDDY_PROGRESS = '1' }
    $win = Find-Win $app.Id 'EQBuddy Progress'
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Park $hwnd
    $hExp3 = Settle $hwnd
    Click-Label $win 'Wealth' $hwnd; $hWea2 = Settle $hwnd
    Click-Label $win 'Experience' $hwnd; $null = Settle $hwnd
    if ([Math]::Abs($hExp3 - $hExp) -le 10 -and [Math]::Abs($hWea2 - $hWea) -le 10) {
        Note 'P-C2: PASS - reopen still follows'
    } else { Note "P-C2: FAIL (Exp=$hExp3 was $hExp; Wea=$hWea2 was $hWea)" }

    $before = Get-H $hwnd
    Drag-BottomEdge $hwnd 80
    $hDrag = Get-H $hwnd
    if ($hDrag -gt $before + 40) { Note "P-D1: PASS - real border drag took ($before -> $hDrag)" }
    else { Note "P-D1: FAIL - drag did not resize ($before -> $hDrag)" }
    Click-Label $win 'Wealth' $hwnd; $hAfter = Settle $hwnd
    if ([Math]::Abs($hAfter - $hDrag) -le 4) { Note "P-D2: PASS - owned after grab; tab switch no longer resizes ($hAfter)" }
    else { Note "P-D2: FAIL - resized to $hAfter after the grab" }
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1500
    $entry = HeightsEntry 'progress'
    if ($null -ne $entry) { Note "P-D3: PASS - dragged close persisted progress = $entry (rect $hDrag px)" }
    else { Note 'P-D3: FAIL - dragged height not persisted (trap-2 ActualHeight in Closed?)' }
    Stop-App $app

    $app = Start-App @{ EQBUDDY_PROGRESS = '1' }
    $win = Find-Win $app.Id 'EQBuddy Progress'
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 400
    $hRestored = Settle $hwnd
    if ([Math]::Abs($hRestored - $hDrag) -le 10) { Note "P-E1: PASS - reopened at dragged height ($hRestored)" }
    else { Note "P-E1: FAIL - expected ~$hDrag, got $hRestored" }
    Click-Label $win 'Wealth' $hwnd; $hOwn = Settle $hwnd
    if ([Math]::Abs($hOwn - $hRestored) -le 4) { Note 'P-E2: PASS - ownership survives restart' }
    else { Note "P-E2: FAIL - resized to $hOwn" }
    Stop-App $app

    # ================= HISTORY: new-caller drag + persist =================
    $app = Start-App @{ EQBUDDY_HISTORY = 'charts' }
    $win = Find-Win $app.Id '*Session History*'
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    [W.U]::SetWindowPos($hwnd, [IntPtr](-1), 80, 80, 0, 0, 0x0001 -bor 0x0010) | Out-Null  # topmost while under test
    Start-Sleep -Milliseconds 500
    $h0 = Settle $hwnd
    Drag-BottomEdge $hwnd 70
    $h1 = Get-H $hwnd
    if ($h1 -gt $h0 + 35) { Note "H-D1: PASS - History border drag took ($h0 -> $h1)" }
    else { Note "H-D1: FAIL - History drag did not resize ($h0 -> $h1)" }
    [W.U]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 1500
    $he = HeightsEntry 'history'
    if ($null -ne $he) { Note "H-D3: PASS - persisted history = $he" } else { Note 'H-D3: FAIL - not persisted' }
    Stop-App $app

    $app = Start-App @{ EQBUDDY_HISTORY = 'charts' }
    $win = Find-Win $app.Id '*Session History*'
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    Start-Sleep -Milliseconds 500
    $h2 = Settle $hwnd
    if ([Math]::Abs($h2 - $h1) -le 10) { Note "H-E1: PASS - History reopened at dragged height ($h2)" }
    else { Note "H-E1: FAIL - expected ~$h1, got $h2" }
}
finally {
    Stop-App $app
    Write-Host '--- SUMMARY ---'
    $results | ForEach-Object { Write-Host $_ }
}
