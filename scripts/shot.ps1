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
  [int]$OwnerPid = 0
)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;using System.Runtime.InteropServices;using System.Text;
public class Win {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
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
$hit = [IntPtr]::Zero
$cb = [Win+EnumProc]{ param($h, $l)
  if ([Win]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [Win]::GetWindowText($h, $sb, 256) | Out-Null
    if ($sb.ToString() -like "*$TitleLike*") {
      if ($OwnerPid -gt 0) {
        $owner = 0
        [Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
        if ($owner -ne $OwnerPid) { return $true }   # right title, wrong app
      }
      $script:hit = $h; return $false
    }
  }
  return $true
}
[Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
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
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"saved $Out  ($w x $h2)"
