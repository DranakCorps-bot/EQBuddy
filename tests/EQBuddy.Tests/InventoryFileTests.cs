using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The /outputfile inventory parse (David's ask, 2026-08-11), against his real
/// Dranak dump: tab format, Empty rows, +N tier folding, the trailing-* marker, stack
/// counts, and container structure ("General 1-Slot2" lives inside "General 1").</summary>
public class InventoryFileTests
{
    private static string[] Fixture() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "inventory", "dranak.txt"));

    [Fact]
    public void CountsFoldTiersHonorStacksAndIgnoreEmpties()
    {
        var counts = InventoryFile.Parse(Fixture());

        // 9 flagged "Bone Chips*" + a 134 stack = 143, one key.
        Assert.Equal(143, counts["Bone Chips"]);
        // "Leather Whip +9" folds to the base name every quest page uses.
        Assert.True(counts.ContainsKey("Leather Whip"));
        Assert.False(counts.Keys.Any(k => k.Contains('+')), "no tier suffixes survive");
        Assert.False(counts.ContainsKey("Empty"));
        // Both Enchanted Fine Steel Morning Stars (primary + secondary) count.
        Assert.Equal(2, counts["Enchanted Fine Steel Morning Star"]);
    }

    /// <summary>The dump is a baseline; the log keeps it current (David, 2026-08-11):
    /// loot since the dump adds, sells subtract, and nothing goes below zero — a sale
    /// the dump never saw proves the dump stale, not a negative bag.</summary>
    [Fact]
    public void LogChangesSinceTheDumpAdjustTheCounts()
    {
        var stats = new SessionStats();
        void Line(int ss, string msg) =>
            stats.Apply(LogParser.Parse($"[Tue Aug 11 09:00:{ss:D2} 2026] {msg}")!);
        Line(10, "--You have looted a Bone Chips from a decaying skeleton's corpse.--");
        Line(11, "--You have looted a Bone Chips from a decaying skeleton's corpse.--");
        Line(12, "--You have looted a Cracked Staff from a decaying skeleton's corpse.--");

        var dumpTime = new DateTime(2026, 8, 11, 9, 0, 5);   // written between loots? no — before all
        var gained = stats.ItemsGainedSince(dumpTime);
        Assert.Equal(2, gained["Bone Chips"]);

        var snap = new InventoryFile.Snapshot("x", dumpTime,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Bone Chips"] = 141 })
            .WithChanges(gained);
        Assert.Equal(143, snap.CountOf("Bone Chips"));
        Assert.Equal(1, snap.CountOf("Cracked Staff"));

        // Only loot AFTER the dump counts.
        Assert.Empty(stats.ItemsGainedSince(new DateTime(2026, 8, 11, 9, 30, 0)));

        // An auto-sold pickup is net ZERO for the bags: it never entered them, so it
        // must neither add nor subtract. (It used to subtract — telling the overlay an
        // item left that was never counted in.)
        Line(20, "You looted a Bone Chips from a decaying skeleton's corpse and sold it for 4 copper.");
        Assert.Equal(2, stats.ItemsGainedSince(dumpTime)["Bone Chips"]);

        // Sells floor at zero rather than going negative.
        var oversold = snap.WithChanges(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["Cracked Staff"] = -5 });
        Assert.Equal(0, oversold.CountOf("Cracked Staff"));
    }

    [Fact]
    public void EntriesKeepWornSlotsAndBagStructure()
    {
        var entries = InventoryFile.ParseEntries(Fixture());

        var head = Assert.Single(entries, e => e.Location == "Head");
        Assert.Equal("Raw-Hide Skullcap +2", head.Name);
        Assert.False(head.InContainer);

        var bagged = entries.Where(e => e.ContainerSlot == "General 1" && e.InContainer).ToList();
        Assert.Contains(bagged, e => e.Name == "Bone Chips" && e.Count == 134);
        // The bag itself is a top-level entry at the same slot.
        Assert.Contains(entries, e => e is { Location: "General 1", Name: "Lightweight Bag" });
    }
}
