param(
  [Parameter(Mandatory)][string]$TitleLike,
  [Parameter(Mandatory)][string]$Out,
  [int]$Pad = 0,
  # Which process the window must belong to. Optional, and it should not be: a title is
  # not an identity. CLAUDE.md already records this costing two captures (release.ps1
  # relaunches the real app, and a title match photographed David's live profile), and
  # the Progress theme made it far easier to hit — FOUR shots now share the title
  # "EQBuddy Progress", so a previous shot's app that has not finished exiting is a
  # perfect match for the next shot's request. That produced a Faction tab filed as
  # progress-wealth.png on 2026-08-19, which looks exactly like a correct screenshot of
  # the wrong feature.
  [int]$OwnerPid = 0,
  # COMPOSITE THE PROCESS'S OPEN POPUPS ON TOP OF THE WINDOW. Opt-in, for one shape:
  # **a WPF `Popup` is its own top-level HWND**, so `PrintWindow` on the owner renders
  # everything EXCEPT the dropdown that is open over it. DRA-71 D2 found this the way the
  # illustration lock intends — `shell-helper-picker.png` came back BYTE-IDENTICAL to
  # `shell-helper.png`, a picture of a button where the prediction said nine check rows,
  # and nothing but comparing the two files would have said so.
  #
  # **The obvious fix was a screen grab, and it was tried and reverted, twice over.** The
  # first take put the always-on-top widget across the left half of the room; moving the
  # widget aside, the second take caught an unrelated application on this machine's
  # desktop. That is exactly the failure PrintWindow was chosen to prevent, arriving by
  # the door marked "just this once" — so the popup is PrintWindow'd into its own bitmap
  # and drawn onto the owner's at its own screen offset. Occlusion-proof, like everything
  # else here.
  [switch]$WithPopups
)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;using System.Text;
public class Win {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  // CharSet.Unicode, and it is not decoration. Without it this binds GetWindowTextA, which
  // round-trips the title through the machine's ANSI code page — and since E-3 five shot
  // rows match on a title carrying an em dash ("EQBuddy — Gear"). On Windows-1252 that
  // survives; on a code page that has no em dash it comes back "?" and the row can never
  // match, on that machine only. scripts/shoot.ps1's own copy of this import already says
  // CharSet.Unicode; the asymmetry between the two is the tell.
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, ref int pid);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int cb);
  // PrintWindow asks the window to render ITSELF into a DC, so whatever happens to be
  // stacked over it is irrelevant. PW_RENDERFULLCONTENT (2) is what makes it work for
  // the composited/layered windows EQBuddy uses — without that flag a layered window
  // renders blank. This is why the shoot script survives the real EQBuddy running:
  // every window here is always-on-top, so a screen grab photographs whichever one
  // happens to be in front rather than the one asked for.
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  public struct RECT { public int L, T, R, B; }
  public static RECT Frame(IntPtr h) { RECT r; DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))); return r; }
}
'@
# EVERY match is collected, and an EXACT title wins over a substring one.
#
# -OwnerPid separates two PROCESSES; it cannot separate two windows of one process, which
# is the half of trap 24 the guard never covered — and E-3 made it live. The widget's
# title is exactly "EQBuddy"; the Evolved shell's carries its room ("EQBuddy — Character
# Setup"), and
# since scripts/shoot.ps1 opens the shell on every launch the two are in the same process
# at the same time. `-like "*EQBuddy*"` matches both, and the winner would be whichever
# EnumWindows reached first — a picture of the shell filed as `widget-cards.png`, which
# looks exactly like a correct screenshot of the wrong feature.
#
# Preferring the exact match is not a new rule so much as the one the shot table already
# assumed: a Title that IS a window's whole name is naming that window, and a Title that
# is a fragment ('Quest Tracker', 'Options') is still matched as a fragment, unchanged.
$exact = [IntPtr]::Zero
$loose = [IntPtr]::Zero
$cb = [Win+EnumProc]{ param($h, $l)
  if ([Win]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [Win]::GetWindowText($h, $sb, 256) | Out-Null
    $title = $sb.ToString()
    if ($title -like "*$TitleLike*") {
      if ($OwnerPid -gt 0) {
        $owner = 0
        [Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -ne $OwnerPid) { return $true }   # right title, wrong app
      }
      if ($title -eq $TitleLike) { $script:exact = $h; return $false }
      if ($script:loose -eq [IntPtr]::Zero) { $script:loose = $h }
    }
  }
  return $true
}
[Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$hit = if ($exact -ne [IntPtr]::Zero) { $exact } else { $loose }
if ($hit -eq [IntPtr]::Zero) {
  throw "no visible window matching '$TitleLike'" +
        $(if ($OwnerPid -gt 0) { " in process $OwnerPid" } else { "" })
}
$r = [Win]::Frame($hit)
$x = $r.L - $Pad; $y = $r.T - $Pad
$w = ($r.R - $r.L) + 2 * $Pad; $h2 = ($r.B - $r.T) + 2 * $Pad
$bmp = New-Object System.Drawing.Bitmap($w, $h2)
$g = [System.Drawing.Graphics]::FromImage($bmp)
# Ask the window to draw itself. Occlusion-proof, which matters because every EQBuddy
# window is always-on-top: with a screen grab, a real running copy of the app wins over
# the fixture's and the PNG silently shows the wrong thing.
$hdc = $g.GetHdc()
$ok = [Win]::PrintWindow($hit, $hdc, 2)   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
if (-not $ok) {
    # Fall back rather than fail: some window types refuse PrintWindow, and a capture
    # that might be occluded beats no capture at all — but say so, because a silently
    # occluded shot is exactly what this is here to prevent.
    Write-Warning "PrintWindow refused; falling back to a screen grab (anything stacked over the window will be in the PNG)."
    $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
}

# ---- the open popups, drawn on top (see -WithPopups) ------------------------------------
#
# A WPF Popup lives in its OWN top-level HWND, so it is invisible to the owner's
# PrintWindow. Each one is rendered into its own bitmap the same occlusion-proof way and
# blitted at its screen offset.
#
# WHICH WINDOWS COUNT, and each clause is load-bearing: same process (never another app's
# menu), visible, an EMPTY title (every EQBuddy window a shot can ask for HAS a title —
# "EQBuddy", "EQBuddy — Helper", "Quest Tracker" — so this cannot swallow a sibling window
# and re-open trap 24 from the other side), not the owner itself, and INTERSECTING the
# region being captured. `$popups` is reported in the output line, because "a popup was
# composited" and "the popup you meant was composited" are different claims and only the
# first one a script can make.
$popups = 0
if ($WithPopups) {
    $extra = New-Object System.Collections.ArrayList
    $pcb = [Win+EnumProc]{ param($h, $l)
        if (-not [Win]::IsWindowVisible($h)) { return $true }
        if ($h -eq $hit) { return $true }
        if ($OwnerPid -gt 0) {
            $owner = 0
            [Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
            if ($owner -ne $OwnerPid) { return $true }
        }
        $sb = New-Object System.Text.StringBuilder 256
        [Win]::GetWindowText($h, $sb, 256) | Out-Null
        if ($sb.ToString().Length -gt 0) { return $true }
        $pr = [Win]::Frame($h)
        $pw = $pr.R - $pr.L; $ph = $pr.B - $pr.T
        if ($pw -le 0 -or $ph -le 0) { return $true }
        if ($pr.R -le $x -or $pr.L -ge ($x + $w) -or
            $pr.B -le $y -or $pr.T -ge ($y + $h2)) { return $true }
        [void]$extra.Add($pr)
        return $true
    }
    [Win]::EnumWindows($pcb, [IntPtr]::Zero) | Out-Null

    # Re-enumerated rather than carried, because the callback cannot hand handles back
    # through a typed ArrayList of RECTs; the second pass matches on the rect it recorded.
    $handles = New-Object System.Collections.ArrayList
    $hcb = [Win+EnumProc]{ param($h, $l)
        if (-not [Win]::IsWindowVisible($h) -or $h -eq $hit) { return $true }
        $pr = [Win]::Frame($h)
        foreach ($k in $extra) {
            if ($k.L -eq $pr.L -and $k.T -eq $pr.T -and $k.R -eq $pr.R -and $k.B -eq $pr.B) {
                [void]$handles.Add($h); break
            }
        }
        return $true
    }
    [Win]::EnumWindows($hcb, [IntPtr]::Zero) | Out-Null

    foreach ($ph in $handles) {
        $pr = [Win]::Frame($ph)
        $pw2 = $pr.R - $pr.L; $ph2 = $pr.B - $pr.T
        $pbmp = New-Object System.Drawing.Bitmap($pw2, $ph2)
        $pg = [System.Drawing.Graphics]::FromImage($pbmp)
        # **KNOWN AND ACCEPTED: the popup's TRANSLUCENT pixels composite against black, so a
        # 1px border drawn at partial alpha photographs darker here than it renders on
        # screen.** In Solarized `BorderBrush` is `#66586E75` (40%) and the hairline comes
        # out near-black, which reads like a light-theme contrast defect and is not one.
        # PrintWindow OVERWRITES the DC rather than blending into it — seeding this bitmap
        # with the owner's pixels first was tried and changed nothing, which is the evidence
        # for that sentence — so the translucent edge cannot be recovered from the capture.
        # The interior is opaque and correct, which is exactly why the artifact reads as a
        # border decision rather than a capture one. Anything INSIDE the popup is reviewable;
        # its outline is not. See trap 79.
        $pdc = $pg.GetHdc()
        $pok = [Win]::PrintWindow($ph, $pdc, 2)
        $pg.ReleaseHdc($pdc)
        $pg.Dispose()
        if ($pok) {
            $g.DrawImage($pbmp, ($pr.L - $x), ($pr.T - $y))
            $popups++
        } else {
            Write-Warning "PrintWindow refused a popup at $($pr.L),$($pr.T) — it is NOT in the PNG."
        }
        $pbmp.Dispose()
    }
    if ($popups -eq 0) {
        # Loud, because the whole reason this switch exists is that a missing popup looks
        # exactly like a correct picture of a closed control.
        Write-Warning "-WithPopups found no popup over '$TitleLike'. If the shot is ABOUT a dropdown, it is not in this PNG."
    }
}

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"saved $Out  ($w x $h2)" + $(if ($WithPopups) { "  [$popups popup(s) composited]" } else { "" })
