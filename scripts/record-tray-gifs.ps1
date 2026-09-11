# GIF fixture for the minimized tray (DRA-48): the real EQBuddy.exe on an isolated
# profile, driven by synthetic mouse input while ffmpeg records the screen region around
# the bar. The illustration lock (CLAUDE.md) says an illustration of our own UI is a
# capture with a recipe — shoot.ps1 is the recipe for stills, and this is the recipe for
# the landing page's interaction GIFs. Everything it shows is the shipped behavior of
# HudBarView / HudExpandBar / HudExpandWindow, reached the way a player reaches it: with
# the pointer.
#
#   pwsh -NoProfile -File scripts/record-tray-gifs.ps1              # all four GIFs
#   pwsh -NoProfile -File scripts/record-tray-gifs.ps1 -Gif tray-hover-peek
#   pwsh -NoProfile -File scripts/record-tray-gifs.ps1 -List
#
# PREREQUISITES: dotnet build EQBuddy.slnx -c Release, and ffmpeg on PATH (gdigrab +
# palettegen/paletteuse are in every standard build).
#
# THE SCREEN IS A MUTEX (trap 61): this takes the same eqbuddy-screen.lock as shoot.ps1
# and tests/EQBuddy.E2E, refuses over a foreign fixture app, stands the player's EQBuddy
# down gracefully and relaunches it in finally. Synthetic input makes the lock MORE
# load-bearing here, not less — a stray window under the cursor would be clicked.
#
# Every GIF gets its own app launch on a fresh settings.json, for trap 51's reason: a
# reorder writes MiniStats and a park writes HudPanelPark*, so a second GIF recorded over
# the first's profile is a picture of cumulative state no single GIF asked for.
[CmdletBinding()]
param(
    [string[]]$Gif = @(),
    [string]$Out = '',
    [string]$Backdrop = '#202225',
    # The landing page's palette — uniform BlueGrey per the Founder T4 look
    # (2026-09-10 ~6:45 PM CT), superseding the Turquoise these clips first shipped in.
    [string]$Theme = 'BlueGrey',
    # Seconds for the startup replay to land after the widget appears — a settle, not a
    # handshake (same caveat as shoot.ps1).
    [int]$Settle = 8,
    # Frames per second of the finished GIFs. 12 keeps a 40 ms hover transition legible
    # without the file ballooning.
    [int]$Fps = 12,
    [switch]$KeepProfile,
    # Keep the intermediate .mkv screen recordings beside the GIFs for review.
    [switch]$KeepVideo,
    [switch]$Force,
    [switch]$List
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'isolated-profile.ps1')
$repo = Split-Path $PSScriptRoot -Parent
if ($Out -eq '') { $Out = Join-Path $repo 'site/assets/media' }

# --- what we can record ------------------------------------------------------------
# Each entry is a choreography routine below. The chip loadout is the founder's landing
# set (2026-09-10): DPS, XP/hr, procs, loot, motes, coin.
$Gifs = [ordered]@{
    # Hover the loot chip -> the peek panel opens under the bar; slide to the DPS chip ->
    # the panel follows; move away -> it collapses (HudExpandBar.AwayGrace).
    'tray-hover-peek'       = 'Invoke-HoverPeek'
    # Hover -> peek, CLICK -> pinned (HudExpand: "Click = stay open"); the pointer leaves
    # and the panel stays.
    'tray-click-keep'       = 'Invoke-ClickKeep'
    # Press a chip and carry it left across the bar (HudBarReorder); the drop rewrites
    # MiniStats order.
    'tray-drag-reorder'     = 'Invoke-DragReorder'
    # Pin the peek, drag its body to park it elsewhere (OE-8), then take its left edge and
    # resize the width (HudExpandWindow's SizeWE zone).
    'tray-peek-park-resize' = 'Invoke-PeekParkResize'
}
if ($List) { $Gifs.Keys | ForEach-Object { $_ }; return }
$wanted = if ($Gif.Count -gt 0) { $Gif } else { @($Gifs.Keys) }
foreach ($name in $wanted) {
    if (-not $Gifs.Contains($name)) { throw "Unknown gif '$name'. Try -List." }
}

$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'
if (-not (Test-Path $exe)) {
    throw "EQBuddy.exe not built at $exe. Run: dotnet build EQBuddy.slnx -c Release"
}
$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) { throw 'ffmpeg not found on PATH.' }

$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace RecW -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
[DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
[DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
public struct RECT { public int L, T, R, B; }
'@

function Get-RecSecondaryScreen {
    [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
}
function Get-RecOrigin {
    $sec = Get-RecSecondaryScreen
    if ($sec) { return @{ Left = [int]($sec.WorkingArea.X + 120); Top = [int]($sec.WorkingArea.Y + 120) } }
    return @{ Left = 120; Top = 120 }
}

# The same seeded profile shoot.ps1's mini-bar shots use, with the founder's chip set.
function Write-RecSettings {
    @{
        LogFolder    = $logsDir.FullName
        UpdateFolder = $updateDir.FullName
        Theme        = $Theme
        WindowLeft   = (Get-RecOrigin).Left
        WindowTop    = (Get-RecOrigin).Top
        Minimized    = $true
        ShowTutorial = $false
        SetupDismissed = $true
        LastSeenVersion = $version
        WatchPinsMigrated = $true
        WatchChipMasterRetired = $true
        TrackSpawns  = $false
        TruncateLogs = $false
        ArchiveLogs  = $false
        DefaultRulesVersion = 1
        # Every breakout OFF, exactly as the 'mini-bar' shot does it: a starred stat's
        # float would auto-show over the region this records (and it matches on title).
        # The list has to grow with BreakoutKind (trap 30).
        DisabledBreakouts = @('Damage','Healing','Pet','Watch','Loot','Buffs')
        MiniStats = @('dps','xp','procs','loot','motes','money')
    } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $profileDir 'settings.json') -Encoding UTF8
}

function Stop-RecHard([Diagnostics.Process]$proc) {
    try { & taskkill /PID $proc.Id /T /F 2>$null | Out-Null } catch {}
    try { if (-not $proc.HasExited) { $proc.Kill($true) } } catch {}
}

function Find-RecWindow([string]$titleExact, [int]$ownerPid) {
    $found = [IntPtr]::Zero
    $cb = [RecW.U+EnumProc]{ param($h, $l)
        if (-not [RecW.U]::IsWindowVisible($h)) { return $true }
        $winPid = 0u; [RecW.U]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
        if ($winPid -ne $ownerPid) { return $true }
        $n = [RecW.U]::GetWindowTextLength($h)
        if ($n -le 0) { return $true }
        $sb = New-Object Text.StringBuilder ($n + 1)
        [RecW.U]::GetWindowText($h, $sb, $sb.Capacity) | Out-Null
        if ($sb.ToString() -eq $titleExact) { $script:foundHwnd = $h; return $false }
        return $true
    }
    $script:foundHwnd = [IntPtr]::Zero
    [RecW.U]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    $script:foundHwnd
}

function Get-RecRect([IntPtr]$h) {
    $r = New-Object RecW.U+RECT
    [RecW.U]::GetWindowRect($h, [ref]$r) | Out-Null
    $r
}

# Minimize every shell room window ('EQBuddy — <room>') the fixture app has up. Matching
# the prefix rather than one exact title, and CALLING THIS REPEATEDLY through the settle,
# because the shell can raise AFTER the widget: the 2026-09-10 BlueGrey click-keep clip
# recorded the Home room behind the whole gesture when a single post-widget check ran
# before the shell existed.
function Hide-RecShells([int]$ownerPid) {
    $cb = [RecW.U+EnumProc]{ param($h, $l)
        if (-not [RecW.U]::IsWindowVisible($h) -or [RecW.U]::IsIconic($h)) { return $true }
        $winPid = 0u; [RecW.U]::GetWindowThreadProcessId($h, [ref]$winPid) | Out-Null
        if ($winPid -ne $ownerPid) { return $true }
        $n = [RecW.U]::GetWindowTextLength($h)
        if ($n -le 0) { return $true }
        $sb = New-Object Text.StringBuilder ($n + 1)
        [RecW.U]::GetWindowText($h, $sb, $sb.Capacity) | Out-Null
        if ($sb.ToString() -like 'EQBuddy — *') { [RecW.U]::ShowWindow($h, 6) | Out-Null }  # SW_MINIMIZE
        return $true
    }
    [RecW.U]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
}

# --- pointer choreography ----------------------------------------------------------
# Real cursor moves, because the behaviors under capture are hover behaviors: WPF paints
# :hover and HudExpandBar opens the peek from the REAL pointer, so nothing here may
# teleport. Eased interpolation, so the GIF reads as a hand rather than a script.
#
# Every wait PUMPS the backdrop form's messages. The choreography runs on the same
# thread that owns the form, and a plain Start-Sleep starves its queue — Windows then
# draws the blue "app starting" ring on the cursor whenever it crosses the backdrop,
# and gdigrab records the ring into the shipped clip (it did).
function Wait-Pump([int]$ms) {
    $until = (Get-Date).AddMilliseconds($ms)
    while ((Get-Date) -lt $until) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 15
    }
}
function Move-Smooth([int]$x, [int]$y, [int]$ms = 500) {
    $p = [System.Windows.Forms.Cursor]::Position
    $steps = [Math]::Max(6, [int]($ms / 16))
    for ($i = 1; $i -le $steps; $i++) {
        $t = $i / $steps
        $e = $t * $t * (3.0 - 2.0 * $t)   # smoothstep
        [RecW.U]::SetCursorPos([int]($p.X + ($x - $p.X) * $e), [int]($p.Y + ($y - $p.Y) * $e)) | Out-Null
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 15
    }
    [RecW.U]::SetCursorPos($x, $y) | Out-Null
}
function Mouse-Down { [RecW.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero) }
function Mouse-Up   { [RecW.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero) }
function Click-Here { Mouse-Down; Wait-Pump 90; Mouse-Up }

# A press that becomes a DRAG: down, eased moves (each with a mouse_event so WPF sees
# MouseMove, not just a cursor teleport), up. Same shape as drag-verify.ps1's Drag-Body —
# the app writes reorders and parks only at the end of a real pointer gesture.
function Drag-Smooth([int]$x, [int]$y, [int]$ms = 700) {
    Mouse-Down
    Wait-Pump 160
    $p = [System.Windows.Forms.Cursor]::Position
    $steps = [Math]::Max(8, [int]($ms / 25))
    for ($i = 1; $i -le $steps; $i++) {
        $t = $i / $steps
        $e = $t * $t * (3.0 - 2.0 * $t)
        [RecW.U]::SetCursorPos([int]($p.X + ($x - $p.X) * $e), [int]($p.Y + ($y - $p.Y) * $e)) | Out-Null
        [RecW.U]::mouse_event(1, 0, 0, 0, [IntPtr]::Zero)
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 25
    }
    Wait-Pump 120
    Mouse-Up
}

# Chip centers by UIA, keyed on the text each cell draws. The bar's cells are TextBlocks
# under the widget window; matching on a regex over their names avoids hardcoding pixel
# offsets that go stale with every font or padding change.
function Get-ChipPoint([int]$ownerPid, [string]$pattern) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ProcessIdProperty, $ownerPid)
    $wins = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $wins) {
        if ($w.Current.Name -ne 'EQBuddy') { continue }
        $all = $w.FindAll([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition(
                [Windows.Automation.AutomationElement]::IsOffscreenProperty, $false)))
        foreach ($el in $all) {
            $n = $el.Current.Name
            if ($n -and $n -match $pattern) {
                $b = $el.Current.BoundingRectangle
                if ($b.Width -gt 0) {
                    return @{ X = [int]($b.X + $b.Width / 2); Y = [int]($b.Y + $b.Height / 2); Name = $n }
                }
            }
        }
    }
    $null
}
function Require-Chip([int]$ownerPid, [string]$pattern, [string]$what) {
    $p = Get-ChipPoint $ownerPid $pattern
    if (-not $p) { throw "No visible bar text matching /$pattern/ ($what) — is the tray up with the founder chip set?" }
    Write-Host "  $what chip: '$($p.Name)' at $($p.X),$($p.Y)"
    $p
}

# --- recording ---------------------------------------------------------------------
# ffmpeg gdigrab over the region around the bar, -draw_mouse 1 because the cursor IS the
# story. Recorded to lossless x264, then quantized to GIF in a second pass (palettegen /
# paletteuse), which is what keeps flat Turquoise surfaces from banding.
function Start-Recording([string]$mkv, [hashtable]$region) {
    $psi = New-Object Diagnostics.ProcessStartInfo $ffmpeg.Source
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardError = $true
    $psi.Arguments = "-y -f gdigrab -framerate 24 -offset_x $($region.X) -offset_y $($region.Y) " +
        "-video_size $($region.W)x$($region.H) -draw_mouse 1 -i desktop " +
        "-c:v libx264 -qp 0 -preset ultrafast -pix_fmt yuv444p `"$mkv`""
    $p = [Diagnostics.Process]::Start($psi)
    $p.BeginErrorReadLine()   # drain, or a full stderr pipe stalls the encoder
    Wait-Pump 700             # let gdigrab open before the choreography starts
    $p
}
function Stop-Recording([Diagnostics.Process]$rec) {
    try { $rec.StandardInput.Write('q'); $rec.StandardInput.Flush() } catch {}
    if (-not $rec.WaitForExit(8000)) { Stop-RecHard $rec }
}
function Convert-ToGif([string]$mkv, [string]$gifPath) {
    $filters = "fps=$Fps,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle"
    & $ffmpeg.Source -y -v error -i $mkv -filter_complex $filters $gifPath
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg GIF conversion failed for $mkv" }
}

# --- the screen lock (trap 61) -----------------------------------------------------
$screenLockPath = Join-Path ([IO.Path]::GetTempPath()) 'eqbuddy-screen.lock'
$screenLock = $null
try {
    $screenLock = [IO.File]::Open($screenLockPath, [IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::Write, [IO.FileShare]::Read)
}
catch [IO.IOException] {
    $holder = try { (Get-Content $screenLockPath -Raw -ErrorAction Stop).Trim() } catch { '(unreadable)' }
    $msg = "Another screen job holds $screenLockPath — $holder. " +
           "Wait for it, or pass -Force if you know the holder is gone."
    if (-not $Force) { throw $msg }
    Write-Warning "$msg`n-Force given; continuing."
}
if ($screenLock) {
    $screenLock.SetLength(0)
    $stamp = [Text.Encoding]::UTF8.GetBytes("pid $PID | $(Get-Date -Format o) | $repo")
    $screenLock.Write($stamp, 0, $stamp.Length)
    $screenLock.Flush()
}

$fixtureApps = @(Get-Process EQBuddy -ErrorAction SilentlyContinue | Where-Object {
    $p = try { $_.Path } catch { $null }
    $p -and $p -match '[\\/]bin[\\/](Release|Debug)[\\/]'
})
if ($fixtureApps.Count -gt 0 -and -not $Force) {
    throw "An EQBuddy is already running from a build output — another harness has the screen. Wait, or pass -Force."
}

# Stand the player's app down (gracefully — it finalizes the session), relaunch in finally.
$relaunch = @()
foreach ($proc in @(Get-Process EQBuddy -ErrorAction SilentlyContinue)) {
    $path = try { $proc.Path } catch { $null }
    if ($path -and $path -match '[\\/]bin[\\/](Release|Debug)[\\/]') { continue }
    if ($path) { $relaunch += $path }
    Write-Host "Standing down the running EQBuddy (pid $($proc.Id)) — it will be relaunched."
    try {
        if (-not $proc.CloseMainWindow()) { Stop-RecHard $proc }
        if (-not $proc.WaitForExit(15000)) { Stop-RecHard $proc; $proc.WaitForExit(5000) | Out-Null }
    } catch { }
}
$relaunch = @($relaunch | Select-Object -Unique)

# --- backdrop + isolated profile ----------------------------------------------------
$backdropForm = New-Object System.Windows.Forms.Form
$backdropForm.FormBorderStyle = 'None'
$backdropForm.ShowInTaskbar = $false
$backdropForm.BackColor = [System.Drawing.ColorTranslator]::FromHtml($Backdrop)
# TOPMOST, unlike shoot.ps1's backdrop — and the difference is the capture method.
# shot.ps1 uses PrintWindow, so whatever sits over the backdrop is irrelevant to the
# PNG; gdigrab records the SCREEN, so a browser above a non-topmost backdrop records
# the browser (bookmarks bar and all) behind every clip. The fixture app's own windows
# are always-on-top and created after this, so they stack above it in the topmost band.
$backdropForm.TopMost = $true
$backdropScreen = Get-RecSecondaryScreen
if ($backdropScreen) {
    $backdropForm.StartPosition = 'Manual'
    $backdropForm.Bounds = $backdropScreen.Bounds
} else {
    $backdropForm.WindowState = 'Maximized'
}
$backdropForm.Show()
$backdropForm.Refresh()

$root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-trayrec-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$profileDir = New-Item -ItemType Directory -Force (Join-Path $root 'profile')
Assert-EqIsolatedProfile $profileDir.FullName 'record-tray-gifs.ps1'
$logsDir = New-Item -ItemType Directory -Force (Join-Path $root 'game/Logs')
$updateDir = New-Item -ItemType Directory -Force (Join-Path $root 'updates')
Write-Host "Profile: $profileDir"
& (Join-Path $PSScriptRoot 'make-test-session.ps1') -Out $logsDir.FullName | Write-Host

# --- per-GIF choreographies ---------------------------------------------------------
# Each receives the widget pid and its bar rect; the recording is already rolling.

function Invoke-HoverPeek([int]$appPid, [RecW.U+RECT]$bar) {
    # The loot cell's UIA name is its bare count — the bag is a drawn vector, not a glyph
    # (IconPaths' rule), so the count is the only pure-number cell on the bar.
    $loot = Require-Chip $appPid '^\s*\d+\s*$' 'loot'
    $dps  = Require-Chip $appPid 'dps\s*$' 'dps'
    Wait-Pump 800
    Move-Smooth $loot.X $loot.Y 700
    Wait-Pump 2300                  # peek opens under the bar
    Move-Smooth $dps.X $dps.Y 600
    Wait-Pump 2300                  # panel follows the chip
    Move-Smooth ($bar.R + 70) ($bar.T - 40) 500     # up and away — not through the panel
    Wait-Pump 1400                  # AwayGrace elapses, peek collapses
    Move-Smooth ($bar.R + 420) ($bar.T - 44) 400    # exit the frame, so the loop closes clean
    Wait-Pump 600
}

function Invoke-ClickKeep([int]$appPid, [RecW.U+RECT]$bar) {
    # The DPS card, for a body with rows in it — the loot peek's no-target state is the
    # hover GIF's story, not this one's.
    $dps = Require-Chip $appPid 'dps\s*$' 'dps'
    Wait-Pump 800
    Move-Smooth $dps.X $dps.Y 700
    Wait-Pump 1900                  # peek (and the tooltip says why to click)
    Click-Here                                      # pin — "click to keep it open"
    Wait-Pump 900
    Move-Smooth ($bar.R + 70) ($bar.T - 40) 600     # pointer leaves…
    Wait-Pump 2200                  # …and the panel stays
    Move-Smooth ($bar.R + 420) ($bar.T - 44) 400
    Wait-Pump 600
}

function Invoke-DragReorder([int]$appPid, [RecW.U+RECT]$bar) {
    $loot = Require-Chip $appPid '^\s*\d+\s*$' 'loot'
    $dps  = Require-Chip $appPid 'dps\s*$' 'dps'
    Wait-Pump 800
    Move-Smooth $loot.X $loot.Y 700
    Wait-Pump 600
    Drag-Smooth $dps.X $dps.Y 900                   # carry the loot chip onto dps's slot
    Wait-Pump 800
    Move-Smooth ($bar.R + 70) ($bar.T - 40) 500
    Wait-Pump 1300                  # the new order, at rest
    Move-Smooth ($bar.R + 420) ($bar.T - 44) 400
    Wait-Pump 600
}

function Invoke-PeekParkResize([int]$appPid, [RecW.U+RECT]$bar) {
    $dps = Require-Chip $appPid 'dps\s*$' 'dps'
    Wait-Pump 700
    Move-Smooth $dps.X $dps.Y 700
    Wait-Pump 1500
    Click-Here                                      # pin first, so the panel survives the trip
    Wait-Pump 900
    $panel = Find-RecWindow 'EQBuddy HUD Panel' $appPid
    if ($panel -eq [IntPtr]::Zero) { throw 'HUD panel window not found after pin.' }
    $pr = Get-RecRect $panel
    $bx = [int](($pr.L + $pr.R) / 2); $by = [int]($pr.T + 14)   # top strip = body, not rows
    Move-Smooth $bx $by 400
    Drag-Smooth ($bx + 150) ($by + 70) 900          # drag the panel — it parks where dropped
    Wait-Pump 900
    $pr = Get-RecRect $panel
    Move-Smooth ($pr.R - 2) ([int](($pr.T + $pr.B) / 2)) 450    # right edge: the SizeWE zone
    Wait-Pump 400
    Drag-Smooth ($pr.R + 110) ([int](($pr.T + $pr.B) / 2)) 800  # wider
    Wait-Pump 1200
    Move-Smooth ($bar.R + 420) ($bar.T - 44) 400
    Wait-Pump 600
}

# --- the run -----------------------------------------------------------------------
New-Item -ItemType Directory -Force $Out | Out-Null
$taken = @(); $failed = @()
try {
    foreach ($name in $wanted) {
      try {
        Write-Host "`n=== $name ==="
        Write-RecSettings
        $psi = New-Object Diagnostics.ProcessStartInfo $exe
        $psi.UseShellExecute = $false
        Assert-EqIsolatedProfile $profileDir.FullName 'record-tray-gifs.ps1'
        $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
        $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
        # The shell comes up like every capture launch (shoot.ps1's standing order), then
        # is minimized out of the recorded region — the story here is the bar.
        $psi.EnvironmentVariables['EQBUDDY_SHELL'] = '1'
        $proc = [Diagnostics.Process]::Start($psi)
        $rec = $null
        try {
            $deadline = (Get-Date).AddSeconds(60)
            $widget = [IntPtr]::Zero
            while ((Get-Date) -lt $deadline -and $widget -eq [IntPtr]::Zero) {
                Start-Sleep -Milliseconds 400
                if ($proc.HasExited) { throw "$exe exited early (code $($proc.ExitCode))." }
                $widget = Find-RecWindow 'EQBuddy' $proc.Id
            }
            if ($widget -eq [IntPtr]::Zero) { throw 'Widget window never appeared.' }
            # Park the pointer off every window BEFORE the settle (shoot.ps1's rule): the
            # first frame must not already be a hover. The settle doubles as the shell
            # sweep — Hide-RecShells runs through it, not once, for the reason on it.
            $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
            [RecW.U]::SetCursorPos(($vs.Right - 1), ($vs.Bottom - 1)) | Out-Null
            $settleUntil = (Get-Date).AddSeconds($Settle)
            while ((Get-Date) -lt $settleUntil) {
                Hide-RecShells $proc.Id
                Start-Sleep -Milliseconds 250
            }
            Hide-RecShells $proc.Id

            $bar = Get-RecRect $widget
            # The recorded region: the bar with room below-left for the peek panel and
            # slack for a park drag. Even dimensions, for the encoder.
            $rg = @{
                X = $bar.L - 120
                Y = $bar.T - 56
                W = ($bar.R - $bar.L) + 460   # 120 left; 340 right, for the park + resize
                H = 470
            }
            $rg.W += $rg.W % 2; $rg.H += $rg.H % 2
            Write-Host "  bar $($bar.L),$($bar.T)-$($bar.R),$($bar.B); region $($rg.X),$($rg.Y) $($rg.W)x$($rg.H)"

            # Start from just inside the region's lower-right, so frame 1 shows a parked
            # pointer rather than one materializing at the first chip.
            [RecW.U]::SetCursorPos($rg.X + $rg.W - 30, $rg.Y + $rg.H - 30) | Out-Null
            Wait-Pump 300

            $mkv = Join-Path $root "$name.mkv"
            $rec = Start-Recording $mkv $rg
            & $Gifs[$name] $proc.Id $bar
            Stop-Recording $rec; $rec = $null

            $gifPath = Join-Path $Out "$name.gif"
            Convert-ToGif $mkv $gifPath
            if ($KeepVideo) { Copy-Item $mkv (Join-Path $Out "$name.mkv") -Force }
            $taken += $gifPath
            Write-Host "  → $gifPath ($([int]((Get-Item $gifPath).Length / 1kb)) KB)"
        }
        finally {
            if ($rec) { Stop-Recording $rec }
            if (-not $proc.HasExited) { Stop-RecHard $proc }
            if (-not $proc.WaitForExit(10000)) { Stop-RecHard $proc; $proc.WaitForExit(5000) | Out-Null }
        }
      }
      catch {
        $failed += [pscustomobject]@{ Gif = $name; Error = $_.Exception.Message }
        Write-Warning "GIF FAILED — $name : $($_.Exception.Message)"
      }
    }
}
finally {
    if ($screenLock) { $screenLock.Dispose() }
    $backdropForm.Close(); $backdropForm.Dispose()
    if ($KeepProfile) { Write-Host "`nProfile kept at $root" }
    else { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }
    foreach ($path in $relaunch) {
        if (Test-Path $path) {
            Write-Host "Relaunching $path"
            $re = New-Object Diagnostics.ProcessStartInfo $path
            $re.UseShellExecute = $false
            $re.WorkingDirectory = Split-Path $path
            Clear-EqHarnessProfileOverrides $re
            [Diagnostics.Process]::Start($re) | Out-Null
        }
    }
}

Write-Host "`n$($taken.Count) GIF(s):"
$taken | ForEach-Object { Write-Host "  $_" }
if ($failed.Count -gt 0) {
    Write-Host "`n$($failed.Count) FAILED:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $($_.Gif): $($_.Error)" -ForegroundColor Red }
    exit 1
}
