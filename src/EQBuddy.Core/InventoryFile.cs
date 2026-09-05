using System.IO;

namespace EQBuddy.Core;

/// <summary>
/// The game's `/outputfile inventory` dump (David's ask, 2026-08-11): tab-separated
/// Location / Name / ID / Count / Slots, one row per slot, "Empty" rows for the
/// gaps, written as `<Character>_<server>-Inventory.txt` beside eqgame.exe. Parsed
/// into base-name → count so the quest tracker can answer "what could I turn in
/// with what I'm already carrying" — upgrade tiers fold ("Leather Whip +9" counts
/// as Leather Whip, the name every quest and wiki page uses), a trailing `*`
/// (the dump's attuned/no-trade marker) is cosmetic, and stack counts are honored
/// (134 Bone Chips is 134 toward a 4-chip turn-in, not 1).
/// </summary>
public static class InventoryFile
{
    public sealed record Snapshot(string Path, DateTime WrittenAt, Dictionary<string, int> Counts)
    {
        public List<Entry> Entries { get; init; } = [];
        /// <summary>Net log-observed change since the dump was written (loot in,
        /// sells/destroys out) — already merged into <see cref="Counts"/>; kept
        /// separately so views can show "since this dump" honestly.</summary>
        public Dictionary<string, int> SinceDump { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public int CountOf(string itemName) =>
            Counts.TryGetValue(QuestCatalog.BaseItemName(itemName), out var n) ? n : 0;

        /// <summary>The dump plus the log since it: loot adds, sells/destroys subtract,
        /// nothing goes below zero (selling something the dump never saw proves the
        /// dump stale, not a negative bag).</summary>
        public Snapshot WithChanges(Dictionary<string, int> gained)
        {
            var merged = new Dictionary<string, int>(Counts, StringComparer.OrdinalIgnoreCase);
            foreach (var (item, n) in gained)
            {
                var next = merged.GetValueOrDefault(item) + n;
                if (next > 0) merged[item] = next;
                else merged.Remove(item);
            }
            return new Snapshot(Path, WrittenAt, merged) { Entries = Entries, SinceDump = gained };
        }
    }

    /// <summary>One occupied row, structure preserved: "General 1-Slot2" is inside the
    /// container occupying top-level "General 1"; a location without "-Slot" IS a
    /// top-level slot (worn gear, the General bag row itself, bank slots).</summary>
    public sealed record Entry(string Location, string Name, int Count)
    {
        public bool InContainer => Location.Contains("-Slot", StringComparison.Ordinal);
        public string ContainerSlot => InContainer
            ? Location[..Location.IndexOf("-Slot", StringComparison.Ordinal)] : Location;

        /// <summary>In the bank rather than on the character — including the SHARED bank,
        /// which the dump writes as "SharedBank1" and a plain "Bank" prefix test misses.
        /// Said once here because "is this taking up bag space" is the question #243 asks
        /// and <see cref="GearLocker"/> asks, and a second spelling of the rule is how the
        /// two answers drift apart: `GearLocker` ranked a `SharedBank1` row as WORN gear
        /// and labelled it `worn · SharedBank1`.</summary>
        public bool InBank =>
            ContainerSlot.StartsWith("Bank", StringComparison.OrdinalIgnoreCase)
            || ContainerSlot.StartsWith("SharedBank", StringComparison.OrdinalIgnoreCase);
    }

    public static List<Entry> ParseEntries(IEnumerable<string> lines)
    {
        var entries = new List<Entry>();
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length < 4) continue;
            var name = parts[1].Trim().TrimEnd('*').Trim();
            if (name.Length == 0 || name == "Empty" || name == "Name") continue;
            if (!int.TryParse(parts[3], out var count) || count < 1) continue;
            entries.Add(new Entry(parts[0].Trim(), name, count));
        }
        return entries;
    }

    public static Dictionary<string, int> Parse(IEnumerable<string> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in ParseEntries(lines))
        {
            var baseName = QuestCatalog.BaseItemName(e.Name);
            counts[baseName] = counts.GetValueOrDefault(baseName) + e.Count;
        }
        return counts;
    }

    /// <summary>The newest dump for the followed character in the game folder (the
    /// Logs folder's parent — where /outputfile writes), parsed. Finding it is
    /// <see cref="OutputfileAutoImport.FindLatest"/>'s job, which is the one place the
    /// root and the filename shape are decided; this adds the parse. Null when none
    /// exists yet.</summary>
    public static Snapshot? FindLatest(string? logFolder, string character)
    {
        var file = OutputfileAutoImport.FindLatest(logFolder, character, OutputfileKind.Inventory);
        if (file is null) return null;
        try
        {
            var entries = ParseEntries(File.ReadLines(file.FullName));
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                var baseName = QuestCatalog.BaseItemName(e.Name);
                counts[baseName] = counts.GetValueOrDefault(baseName) + e.Count;
            }
            return new Snapshot(file.FullName, file.LastWriteTime, counts) { Entries = entries };
        }
        catch { return null; }
    }
}
