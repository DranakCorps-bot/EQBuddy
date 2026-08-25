using System.IO;

namespace EQBuddy.Core;

/// <summary>
/// The game's `/outputfile faction` dump: tab-separated `ID Name StandingValue
/// PointsToMax` under a header row, CRLF, written as
/// `&lt;Character&gt;_&lt;server&gt;-&lt;CLASS&gt;-Factions.txt` beside the game's own folders.
///
/// **The command is singular and the file is plural**, with the character's class code
/// spliced into the middle — `Hateborne_neriak-ENC-Factions.txt`. Both facts come from
/// David's own log rather than from documentation, which does not cover this dump at all:
///
///   usage: /outputfile [achievements | faction | guild | guildbank | guildhall |
///          inventory | missingspells | raid | realestate | recipes | spellbook ]
///   Outputfile Complete: Hateborne_neriak-ENC-Factions.txt
///
/// So anything matching on the filename matches the SUFFIX and never counts segments —
/// the same distinction that mattered in <see cref="GameWrittenLog"/> (trap 48), for the
/// same reason: a name with an extra part in the middle is still the game's own.
///
/// **`PointsToMax == 0` is "maxed"**, which is exactly the question the race-unlock
/// achievements ask ("Get maximum faction with X"). The dump also makes the ceiling
/// explicit: every row observed has `StandingValue + PointsToMax == 2000`, so a fraction
/// is `clamp(StandingValue, 0, 2000) / 2000`. That invariant is asserted rather than
/// assumed — if a future row breaks it, a test says so instead of a progress bar quietly
/// reading wrong.
/// </summary>
public static class FactionsFile
{
    /// <summary>The ceiling every observed row agrees on. Not used to DERIVE anything the
    /// dump states directly — <see cref="Standing.Maxed"/> reads PointsToMax — only to
    /// draw a fraction.</summary>
    public const int Cap = 2000;

    /// <summary>One faction's standing as the dump reported it.</summary>
    /// <param name="Id">The game's own faction id. Kept because it is the only stable
    /// handle: the NAMES drift between the dump and the achievements text, and an id
    /// cannot.</param>
    public sealed record Standing(int Id, string Name, int Value, int PointsToMax)
    {
        /// <summary>"Get maximum faction with X" is satisfied. Read from the dump's own
        /// answer rather than compared against <see cref="Cap"/>, so a re-tuned ceiling
        /// cannot make this lie.</summary>
        public bool Maxed => PointsToMax <= 0;

        /// <summary>0..1 for a progress bar. Negative standing reads as 0 rather than as
        /// a negative bar — "you are 1,861 in the wrong direction" is a number to print,
        /// not a length to draw.</summary>
        public double Fraction => Math.Clamp((double)Value / Cap, 0, 1);
    }

    public sealed record Snapshot(string Path, DateTime WrittenAt, List<Standing> Standings)
    {
        private Dictionary<string, Standing>? _byName;

        /// <summary>By name, case-insensitively. Callers that hold a name from somewhere
        /// ELSE — the achievements dump, say — must go through
        /// <see cref="FactionNames.Resolve"/> first; the two sources do not spell four of
        /// these the same.</summary>
        public Standing? this[string name]
        {
            get
            {
                _byName ??= Standings
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                return _byName.GetValueOrDefault(name);
            }
        }
    }

    /// <summary>Does this filename look like a faction dump? Suffix only — the class code
    /// sits between the character and "Factions", and a rule that counted segments would
    /// refuse a real dump forever.</summary>
    public static bool IsFactionDump(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName ?? "")
            .EndsWith("-Factions", StringComparison.OrdinalIgnoreCase);

    public static List<Standing> Parse(IEnumerable<string> lines)
    {
        var result = new List<Standing>();
        foreach (var raw in lines)
        {
            var parts = raw.TrimEnd().Split('\t');
            if (parts.Length < 4) continue;
            // The header row and any stray text simply fail to parse, which is the
            // check — no need to special-case "ID" by name.
            if (!int.TryParse(parts[0].Trim(), out var id)) continue;
            if (!int.TryParse(parts[2].Trim(), out var value)) continue;
            if (!int.TryParse(parts[3].Trim(), out var toMax)) continue;
            var name = parts[1].Trim();
            if (name.Length == 0) continue;
            result.Add(new Standing(id, name, value, toMax));
        }
        return result;
    }

    /// <summary>The newest faction dump for this character, found the same way and in the
    /// same place as <see cref="InventoryFile.FindLatest"/> — the log folder's PARENT,
    /// said in one place so the two cannot disagree about where a dump lives.</summary>
    public static Snapshot? FindLatest(string? logFolder, string character)
    {
        if (logFolder is null || character.Length == 0) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null || !Directory.Exists(root)) return null;
        try
        {
            var file = Directory.EnumerateFiles(root, $"{character}_*-Factions.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (file is null) return null;
            return new Snapshot(file.FullName, file.LastWriteTime,
                Parse(File.ReadLines(file.FullName)));
        }
        catch { return null; }
    }
}
