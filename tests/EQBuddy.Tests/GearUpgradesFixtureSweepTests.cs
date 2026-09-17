using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FOUNDER'S OWN DUMP, SWEPT AGAINST THE SHIPPED CATALOG** (DRA-149 D1, plan P1+P3).
///
/// <para>Every other test in this area builds a two-record fixture catalog and proves one rule
/// in isolation, and all of them were green while the feature answered NOTHING on the only
/// machine that matters. The failure was not in any single rule — it was in what the rules
/// compose to over the real corpus, which no fixture the size of a paragraph can show. So this
/// file asks the question the Founder asked, through the real code, over the committed
/// inventory dump and the shipped <see cref="ItemCatalog"/>.</para>
///
/// <para><b>Measured before the fix, and the number is the point:</b> 20 worn anchors, of which
/// 19 are gear, and <b>every one of the 19 returned zero</b>. The only anchor that produced
/// anything was <c>Arrow</c> — the one item in the dump with no "+N" on it. That is the shape of
/// a rule that cannot be satisfied rather than one that is being strict.</para>
///
/// <para><b>The floor is a floor and not an equality</b>, deliberately: the catalog is
/// regenerated weekly from eqlwiki, so pinning an exact count would redden this suite on churn
/// that says nothing about the sweep, and the next person would learn to re-baseline it rather
/// than read it (trap 74).</para>
/// </summary>
public class GearUpgradesFixtureSweepTests
{
    private static List<InventoryFile.Entry> Fixture() =>
        InventoryFile.ParseEntries(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "inventory", "dranak.txt")));

    /// <summary>The worn rows, resolved the way the Helper room resolves them.</summary>
    private static List<WornItem> Worn() =>
        GearUpgrades.WornFrom(Fixture(), n => ItemCatalog.Default.Find(n)?.ToStatsBlock());

    /// <summary>Gear, as opposed to ammunition — the AMMO anchor is excluded because it is the
    /// one row that ALREADY worked, and counting it would let the floor be met by the thing
    /// that was never broken.</summary>
    private static List<WornItem> GearAnchors() =>
        [.. Worn().Where(w => !string.Equals(
            GearUpgrades.NormalizeSlot(w.Slot), "AMMO", StringComparison.Ordinal))];

    private static int CandidatesFor(WornItem anchor) => GearUpgrades.Sweep(
        GearIntent.UpgradeWorn, [anchor], [], ItemCatalog.Default, [], includeQuests: false)
        .Upgrades.Count;

    /// <summary>
    /// **THE FOUNDER'S FAIL, AND THE FLOOR THAT HOLDS IT FIXED.**
    ///
    /// <para>He picked "upgrade what I wear" against this dump and the room told him the catalog
    /// had nothing for him, for all 19 things he was wearing. The sentence was true and the
    /// feature was broken.</para>
    /// </summary>
    [Fact]
    public void TheFoundersOwnDumpFindsUpgradesForAlmostEverythingHeWears()
    {
        var anchors = GearAnchors();
        Assert.Equal(19, anchors.Count);

        var answered = anchors.Where(a => CandidatesFor(a) > 0).ToList();

        // Measured at 19 of 19 the day this landed; the floor is 15 so the weekly eqlwiki
        // refresh cannot redden the suite on churn that says nothing about the sweep.
        Assert.True(answered.Count >= 15,
            $"only {answered.Count} of {anchors.Count} gear anchors found a candidate; "
            + "silent ones: " + string.Join(", ",
                anchors.Except(answered).Select(a => $"[{a.Slot}] {a.Name}")));
    }

    /// <summary>
    /// **THE CAUSE, PINNED — so a later reader cannot re-adopt the tier rule here by
    /// reasoning that it is the safer of two options.**
    ///
    /// <para>The same anchors, the same catalog, the same metric table: asked
    /// <see cref="ItemDominance.Dominates"/> they answer, and asked
    /// <see cref="ItemDominance.CanClaimUpgrade"/> they cannot — not rarely, but never, because
    /// no catalog name carries a "+N" for the tier comparison to read. This is the measurement
    /// the plan's P1 rests on, taken through the real code rather than re-implemented.</para>
    /// </summary>
    [Fact]
    public void TheTierRuleCouldNeverHaveAdmittedACatalogCandidate()
    {
        Assert.DoesNotContain(ItemCatalog.Default.All, r => ItemDominance.UpgradeTier(r.Name) > 0);

        var tiered = 0;
        var based = 0;
        foreach (var anchor in GearAnchors())
        {
            // Only the plussed anchors can show the difference — a plain worn item is treated
            // identically by both rules, which is why the dump is the right fixture: 19 of its
            // 19 gear rows carry a "+N".
            if (ItemDominance.UpgradeTier(anchor.Name) == 0) continue;

            foreach (var record in ItemCatalog.Default.All)
            {
                if (record.Slots is not { Count: > 0 }) continue;
                var stats = record.ToStatsBlock();
                if (ItemDominance.Dominates(record.Name, stats, anchor.Name, anchor.Stats, []))
                    based++;
                if (ItemDominance.CanClaimUpgrade(
                        record.Name, stats, anchor.Name, anchor.Stats, [])) tiered++;
            }
        }

        Assert.Equal(0, tiered);
        Assert.True(based > 0, "base-vs-base finds nothing either — the fixture proves nothing");
    }
}

/// <summary>
/// **THE SLOT VOCABULARY THE TWO SIDES DO NOT SHARE** (DRA-149 D1, plan P3).
///
/// <para>The sweep's index is keyed on the CATALOG's spelling and looked up with the DUMP's.
/// Where they differ the candidates are unreachable — not filtered, not ranked low, but absent,
/// with nothing on any surface able to say a pile was never opened.</para>
/// </summary>
public class GearUpgradesSlotTests
{
    [Theory]
    // The three aliases, each a real divergence measured in the shipped catalog.
    [InlineData("FINGER", "FINGERS")]
    [InlineData("SHOULDER", "SHOULDERS")]
    [InlineData("SECONDAY", "SECONDARY")]
    // Trailing promoter punctuation, stripped.
    [InlineData("PRIMARY,", "PRIMARY")]
    [InlineData("BACK,", "BACK")]
    // Already right, and unchanged — normalization is idempotent on the dump's own spellings.
    [InlineData("FINGERS", "FINGERS")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("ANY SLOT", "ANY SLOT")]
    // Case and whitespace, since one side is a file and the other is a parse.
    [InlineData(" head ", "HEAD")]
    public void OneSpellingWins(string raw, string expected) =>
        Assert.Equal(expected, GearUpgrades.NormalizeSlot(raw));

    /// <summary>Debris is REFUSED rather than passed through, so "not a slot" stays a different
    /// answer from "a slot nothing is worn in".</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("EMPTY")]
    [InlineData("ORNAMENTATION:")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GarbageProducesNoKeyAtAll(string? raw) =>
        Assert.Null(GearUpgrades.NormalizeSlot(raw));

    /// <summary>
    /// **THE COUNT, AGAINST THE SHIPPED CATALOG** — the measurement P3 was written from, kept
    /// executable so the day the promoter emits a new spelling is a day this suite says so.
    ///
    /// <para>It asserts the DIRECTION rather than an exact total, for the floor's reason: the
    /// catalog is regenerated weekly and an equality here would redden on churn (trap 74).</para>
    /// </summary>
    [Fact]
    public void TheCatalogSpellsSlotsTheDumpNeverDoesAndTheyAreAllReachedOrRefused()
    {
        var aliased = 0;
        var refused = 0;
        var debris = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var record in ItemCatalog.Default.All)
            foreach (var slot in record.Slots ?? [])
            {
                var raw = slot.Trim().ToUpperInvariant();
                var normal = GearUpgrades.NormalizeSlot(slot);
                if (normal is null) { refused++; debris.Add(raw); continue; }
                if (!string.Equals(raw, normal, StringComparison.Ordinal)) aliased++;
            }

        // FINGER 209 + SHOULDER 5 + SECONDAY 3 + the two trailing-comma rows, measured.
        Assert.True(aliased >= 200,
            $"only {aliased} catalog slot entries needed normalizing — the divergence P3 "
            + "measured has gone, and this rule may no longer be earning its keep");

        // …and the debris is refused rather than indexed under a key nothing can ask for.
        Assert.True(refused > 0, "no debris found; the refusal arm decided nothing");
        Assert.Equal(["/", "EMPTY", "ORNAMENTATION:"], debris);
    }

    /// <summary>
    /// **AN "ANY SLOT" ROW GETS A POOL FROM ITS OWN PAGE** (plan P3).
    ///
    /// <para>The Founder wears a <c>Shiny Brass Shield +6</c> and a <c>Lute +1</c> in
    /// <c>Any Slot</c> — a key the catalog never emits, so both anchored against an empty pile
    /// and could never have answered. The fallback asks the item's own catalog <c>Slot:</c>
    /// line; the anchor's own slot and label are untouched (DRA-81).</para>
    /// </summary>
    [Fact]
    public void AnAnySlotAnchorFallsBackToTheCatalogsOwnSlotLine()
    {
        var worn = GearUpgrades.WornFrom(
            InventoryFile.ParseEntries(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "fixtures", "inventory", "dranak.txt"))),
            n => ItemCatalog.Default.Find(n)?.ToStatsBlock());

        var shield = Assert.Single(worn, w => w.Name.StartsWith("Shiny Brass Shield"));
        // The anchor is still the DUMP's: the fallback moved the candidate pool, not identity.
        Assert.Equal("ANY SLOT", shield.Slot);
        Assert.NotEmpty(shield.Stats.Slots);

        var sweep = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, [shield], [], ItemCatalog.Default, [], includeQuests: false);

        Assert.NotEmpty(sweep.Upgrades);
        // Every row it produced is still ABOUT the worn item, in the worn item's own slot —
        // the anchor rule that keeps this off the best-in-slot side of the line.
        Assert.All(sweep.Upgrades, u =>
        {
            Assert.Equal(shield.Name, u.Over);
            Assert.Equal("ANY SLOT", u.Slot);
        });
    }
}
