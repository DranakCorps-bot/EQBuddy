# Staging a REAL character log into a capture profile — one producer for shoot.ps1 and
# record-tray-gifs.ps1 (dot-source it), the way drops-fixture-wiki.ps1 is one seed for both.
#
# Built for the launch trailer (scripts/trailer/README.md), whose owner asked for his own
# character's log rather than the Testchar fixture, 2026-09-28. The rules:
#
#   * The source is only ever READ. It is copied into the throwaway game folder; nothing is
#     written beside it, and the app under capture is still pinned to the isolated profile
#     (IsolatedLaunchPolicy / Assert-EqIsolatedProfile) exactly as for the fixture.
#   * Every stamp up to the cut moves by ONE constant, so the character's history lands
#     intact and ends "now": sessions, gaps, kills and loot keep their real spacing, and the
#     app rebuilds them on launch exactly as it would have on the day.
#   * The lines after the cut are returned, not written, as (seconds after the cut, message)
#     so a live take can append them at the pace they were played.
#   * The game's /outputfile dumps beside the source's Logs folder (<Name>_<server>-*.txt)
#     are copied beside the staged Logs folder, where the app looks for them.
#
# Latin-1 on both sides maps bytes 1:1, so everything but the stamps is byte-identical.

if (-not ('EqRealLog' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
public static class EqRealLog {
    const string Fmt = "ddd MMM dd HH:mm:ss yyyy";
    static bool TryStamp(string line, out DateTime t) {
        t = default;
        int close = line.IndexOf(']');
        if (line.Length < 3 || line[0] != '[' || close < 2) return false;
        return DateTime.TryParseExact(line.Substring(1, close - 1), Fmt,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out t);
    }
    public static List<KeyValuePair<double, string>> Stage(string src, string dst,
        DateTime cut, TimeSpan delta, int replaySeconds) {
        var replay = new List<KeyValuePair<double, string>>();
        var enc = Encoding.Latin1;
        using (var r = new StreamReader(src, enc))
        using (var w = new StreamWriter(dst, false, enc)) {
            string line; bool past = false;
            while ((line = r.ReadLine()) != null) {
                DateTime t;
                if (!TryStamp(line, out t)) { if (!past) w.WriteLine(line); continue; }
                if (t <= cut) {
                    w.WriteLine("[" + (t + delta).ToString(Fmt, CultureInfo.InvariantCulture) + "]"
                        + line.Substring(line.IndexOf(']') + 1));
                    continue;
                }
                past = true;
                double off = (t - cut).TotalSeconds;
                if (off > replaySeconds) break;
                replay.Add(new KeyValuePair<double, string>(off, line.Substring(line.IndexOf(']') + 2)));
            }
        }
        return replay;
    }
}
'@
}

# Stages $SourceLog into $LogsDir (removing any other eqlog there) and copies its dumps into
# $LogsDir's parent. Returns @{ Log = <staged path>; Replay = <list>; Key = 'name_server' }.
function Copy-EqRealLogStaged([string]$SourceLog, [string]$CutAt, [string]$LogsDir,
                              [int]$ReplaySeconds = 0) {
    if (-not (Test-Path $SourceLog)) { throw "-SourceLog not found: $SourceLog" }
    if (-not $CutAt) { throw '-SourceLog needs -CutAt (yyyy-MM-dd HH:mm:ss).' }
    $src = (Resolve-Path $SourceLog).Path
    $file = [IO.Path]::GetFileName($src)
    if ($file -notmatch '^eqlog_(?<n>[^_]+)_(?<s>[^.]+)\.txt$') {
        throw "-SourceLog must be a game log named eqlog_<Name>_<server>.txt, not '$file'."
    }
    $cut = [DateTime]::ParseExact($CutAt, 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
    Get-ChildItem $LogsDir -Filter 'eqlog_*.txt' | Remove-Item -Force
    $dst = Join-Path $LogsDir $file
    $delta = (Get-Date) - $cut
    $replay = [EqRealLog]::Stage($src, $dst, $cut, $delta, $ReplaySeconds)
    Write-Host "  staged $file to $CutAt (+$([int]$delta.TotalHours) h); $($replay.Count) line(s) after the cut"
    $who = "$($Matches.n)_$($Matches.s)"
    Get-ChildItem (Split-Path (Split-Path $src)) -Filter "$who-*.txt" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path (Split-Path $LogsDir) $_.Name) -Force
        Write-Host "  copied dump $($_.Name)"
    }
    @{ Log = $dst; Replay = $replay; Key = $who.ToLowerInvariant() }
}
