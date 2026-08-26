# #239 acceptance: the expand/minimize toggle keeps the widget's RIGHT edge fixed, so a
# second click at the SAME cursor position lands on the toggle again (disberon's habit —
# "habitually I may just click again to minimize and instead I click start new session").
# Drives the REAL exe on an isolated profile with real mouse clicks, same family as
# drag-verify.ps1. Three assertions: right edge held across minimize; the same point
# toggles back to expanded with the edge still held; the round trip returns the window
# to exactly its original rect.
#
# First green run 2026-08-26 — and its FIRST run failed usefully: the anchor had been
# computed before UpdateMiniChips, against a mini bar 87px wide that the player never
# sees (the bar's width IS its chips), and walked the window 230px right. The harness
# found it in one run; thirteen green unit tests could not have (trap 49's lesson).
$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\david\source\EQBuddy'
$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'

$Root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-239-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$profileDir = New-Item -ItemType Directory -Force (Join-Path $Root 'profile')
$logsDir = New-Item -ItemType Directory -Force (Join-Path $Root 'game/Logs')
New-Item -ItemType Directory -Force (Join-Path $Root 'updates') | Out-Null
& (Join-Path $repo 'scripts/make-test-session.ps1') -Out $logsDir.FullName | Out-Null
$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
@{
    LogFolder = $logsDir.FullName; ShowTutorial = $false; LastSeenVersion = $version
    TruncateLogs = $false; UpdateFolder = (Join-Path $Root 'updates'); Theme = 'Midnight'
    WindowLeft = 900.0; WindowTop = 200.0
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $profileDir 'settings.json') -Encoding utf8

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -Namespace W -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
public struct RECT { public int L, T, R, B; }
'@

function Rect([IntPtr]$h) { $r = New-Object W.U+RECT; [W.U]::GetWindowRect($h, [ref]$r) | Out-Null; $r }
function Click([int]$x, [int]$y) {
    [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 150
    [W.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); [W.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 700
}
# The Nth button from the right in the top strip of the window (title bar / mini bar).
function ButtonFromRight($winEl, [int]$n) {
    $cond = New-Object Windows.Automation.PropertyCondition(
        [Windows.Automation.AutomationElement]::ControlTypeProperty,
        [Windows.Automation.ControlType]::Button)
    $btns = $winEl.FindAll([Windows.Automation.TreeScope]::Descendants, $cond)
    $wr = $winEl.Current.BoundingRectangle
    $top = @()
    foreach ($b in $btns) {
        $r = $b.Current.BoundingRectangle
        if ($r.Y -lt $wr.Y + 40 -and -not $b.Current.IsOffscreen) { $top += ,$b }
    }
    ($top | Sort-Object { -$_.Current.BoundingRectangle.X })[$n - 1]
}

$psi = New-Object Diagnostics.ProcessStartInfo $exe
$psi.UseShellExecute = $false
$psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
$psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
$proc = [Diagnostics.Process]::Start($psi)
try {
    $deadline = (Get-Date).AddSeconds(30); $win = $null
    while ((Get-Date) -lt $deadline -and -not $win) {
        Start-Sleep -Milliseconds 400
        $cond = New-Object Windows.Automation.PropertyCondition(
            [Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
        foreach ($w in [Windows.Automation.AutomationElement]::RootElement.FindAll(
            [Windows.Automation.TreeScope]::Children, $cond)) {
            if ($w.Current.Name -eq 'EQBuddy') { $win = $w; break }
        }
    }
    if (-not $win) { throw 'widget not found' }
    Start-Sleep -Seconds 3
    $hwnd = [IntPtr]$win.Current.NativeWindowHandle
    $r0 = Rect $hwnd
    Write-Host "expanded rect: L=$($r0.L) R=$($r0.R) (w $($r0.R-$r0.L))"

    # Click Minimize (second from right on the full title bar) with a REAL mouse.
    $btn = ButtonFromRight $win 2
    $b = $btn.Current.BoundingRectangle
    $cx = [int]($b.X + $b.Width / 2); $cy = [int]($b.Y + $b.Height / 2)
    Write-Host "clicking Minimize at $cx,$cy"
    Click $cx $cy
    $r1 = Rect $hwnd
    Write-Host "mini rect: L=$($r1.L) R=$($r1.R) (w $($r1.R-$r1.L))"
    if ([Math]::Abs($r1.R - $r0.R) -le 2) { Write-Host 'PASS-1: right edge held across minimize' }
    else { Write-Host "FAIL-1: right edge moved $($r0.R) -> $($r1.R)" }

    # THE HABIT: click again at the SAME point without moving the cursor. Pre-fix this
    # landed on the mini bar's body or nothing; post-fix it must land on Expand.
    Click $cx $cy
    $r2 = Rect $hwnd
    Write-Host "after second click: L=$($r2.L) R=$($r2.R) (w $($r2.R-$r2.L))"
    # "Expanded again" = wearing the ORIGINAL expanded width, not "grew a lot": with many
    # starred chips the mini bar is nearly as wide as the full window (317 vs 338 here),
    # which is also why the bug read as a habitual annoyance rather than a universal miss.
    $expandedAgain = [Math]::Abs(($r2.R - $r2.L) - ($r0.R - $r0.L)) -le 2
    if ($expandedAgain -and [Math]::Abs($r2.R - $r1.R) -le 2) {
        Write-Host 'PASS-2: the same cursor point toggled back to expanded, right edge held'
    } elseif (-not $expandedAgain) {
        Write-Host 'FAIL-2: second click at the same point did not expand (cursor missed the toggle)'
    } else {
        Write-Host "FAIL-2: expanded but right edge moved $($r1.R) -> $($r2.R)"
    }

    # And the round trip did not walk the window: expanded Left is back where it started.
    if ([Math]::Abs($r2.L - $r0.L) -le 2) { Write-Host 'PASS-3: round trip returns to the original place' }
    else { Write-Host "FAIL-3: round trip drifted L $($r0.L) -> $($r2.L)" }
}
finally {
    if (-not $proc.HasExited) { $proc.CloseMainWindow() | Out-Null; if (-not $proc.WaitForExit(8000)) { $proc.Kill() } }
}
