using System.Globalization;
using System.Text.RegularExpressions;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Reading the island out of a Sky step's own prose (David, 2026-08-23, from a Reddit ask:
/// group Sky quest steps by island, sorted numerically).
///
/// The catalog has no island FIELD — the fact is written by hand into <c>Source</c>, in five
/// shapes across 223 steps. Every case below is taken from the shipped catalog rather than
/// invented, which is what makes this a parser test and not a regex test.
/// </summary>
public class SkyIslandsTests
{
    [Theory]
    [InlineData("Isle 4: Keeper of Souls", 4)]
    [InlineData("Isle 6: Bazzt Zzzt", 6)]
    [InlineData("Isle 7: sphinxes, drakes and undine spirits", 7)]
    // Spelled out, and with a dash instead of a colon — both real, both in the catalog.
    [InlineData("Isle four - griffons and pegasus", 4)]
    [InlineData("Isle two - Protector of Sky", 2)]
    [InlineData("Isle five - The Spiroc Lord", 5)]
    [InlineData("Isle six - bees", 6)]
    public void OneIslandIsReadWhicheverWayItIsWritten(string source, double expected) =>
        Assert.Equal([expected], SkyIslands.Parse(source));

    /// <summary>Sky's second stop really is called 1.5, which is why islands are doubles and
    /// why "sorted numerically" needed saying: sorted as text, 1.5 lands after 1 and before
    /// 2 by luck, and "10" would land between 1 and 2 by the same luck.</summary>
    [Fact]
    public void TheHalfIslandSurvives()
    {
        Assert.Equal([SkyIslands.HalfIsland], SkyIslands.Parse("Isle 1.5: Noble Dojorn"));
        Assert.Equal("Island 1.5", SkyIslands.Heading(SkyIslands.HalfIsland));
        Assert.Equal("Island 8", SkyIslands.Heading(8));
    }

    /// <summary>The 22 three-island steps, verbatim from the catalog. Ascending, deduplicated,
    /// and all three kept — dropping any of them would silently narrow where a player thinks
    /// an item can be found.</summary>
    [Fact]
    public void AllThreeIslandsAreKeptAndSorted() =>
        Assert.Equal([1.5, 4, 8], SkyIslands.Parse(
            "Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn"));

    /// <summary>**"No island" is a true answer, not a parse failure.** 95 of 223 steps say
    /// "Trash mobs" because Wind Runes drop anywhere on the plane. Treating that as missing
    /// data would put a confident wrong island on nearly half the checklist.</summary>
    [Theory]
    [InlineData("Trash mobs")]
    [InlineData("")]
    [InlineData(null)]
    public void NoIslandNamedMeansNoIsland(string? source) => Assert.Empty(SkyIslands.Parse(source));

    /// <summary>The negative that keeps the regex honest (trap 39). A number in prose is not
    /// an island, and neither is a word that merely follows "Isle".</summary>
    [Theory]
    [InlineData("Dropped by 2 spawns near the tower")]
    [InlineData("Isle of Dread")]
    [InlineData("6 of them patrol here")]
    public void ANumberInProseIsNotAnIsland(string source) => Assert.Empty(SkyIslands.Parse(source));

    /// <summary>**The prose ascends too, so it agrees with the heading above it** (David,
    /// 2026-08-23). The catalog writes the real three-island source as "eight … four … 1.5",
    /// which rendered directly beneath a heading reading "1.5, 4, and 8" — two orderings of
    /// one fact, one line apart.</summary>
    [Fact]
    public void MultiIslandProseIsReorderedToAscend() =>
        Assert.Equal(
            "Isle 1.5: Noble Dojorn; Isle four: Overseer of Air; Isle eight: the Hand of Veeshan",
            SkyIslands.OrderClausesByIsland(
                "Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn"));

    /// <summary>**Only whole clauses move — no word inside one is touched**, which is what
    /// makes reordering curated prose safe. Prose with nothing to reorder comes back byte for
    /// byte, and a clause naming no island keeps its place at the end rather than being sorted
    /// to an island it never claimed (OrderBy is a stable sort).</summary>
    [Theory]
    [InlineData("Isle 6: Bazzt Zzzt", "Isle 6: Bazzt Zzzt")]
    [InlineData("Trash mobs", "Trash mobs")]
    [InlineData("", "")]
    // Already ascending: returned unchanged rather than rebuilt, so spacing cannot drift.
    [InlineData("Isle 2: Protector of Sky; Isle 7: Sister of the Spire",
                "Isle 2: Protector of Sky; Isle 7: Sister of the Spire")]
    // A clause with no island of its own sinks to the end, keeping its own wording.
    [InlineData("Isle 7: Sister of the Spire; rare trash drop; Isle 2: Protector of Sky",
                "Isle 2: Protector of Sky; Isle 7: Sister of the Spire; rare trash drop")]
    public void ReorderingNeverRewritesAClause(string source, string expected) =>
        Assert.Equal(expected, SkyIslands.OrderClausesByIsland(source));

    /// <summary>**The grouping created a redundancy and this removes it.** A row under
    /// "Island 6" was reading "Josin Faithbringer · Isle 6: Bazzt Zzzt" — the island twice in
    /// eight words. Only the leading label goes; the mob that drops it is what the detail
    /// column was always for.</summary>
    [Theory]
    [InlineData("Isle 6: Bazzt Zzzt", "Bazzt Zzzt")]
    [InlineData("Isle four - griffons and pegasus", "griffons and pegasus")]
    [InlineData("Isle 7: 'drake/sphinx/spirit' type mobs", "'drake/sphinx/spirit' type mobs")]
    [InlineData("Isle two - Protector of Sky", "Protector of Sky")]
    public void TheIslandLabelComesOffARowThatSitsUnderIt(string source, string expected) =>
        Assert.Equal(expected, SkyIslands.WithoutIslePrefix(source));

    /// <summary>What it must NOT touch. A multi-island step keeps every word — it sits under
    /// "Several islands", so those three names are the only place a player learns where to
    /// go. Prose with no label keeps itself. And a source that is ONLY a label keeps itself,
    /// because an empty detail column reads as data that failed to load rather than as data
    /// that was already said.</summary>
    [Theory]
    [InlineData("Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn")]
    [InlineData("Trash mobs")]
    [InlineData("Isle 6")]
    [InlineData("")]
    public void NothingElseIsStripped(string source) =>
        Assert.Equal(source, SkyIslands.WithoutIslePrefix(source));

    /// <summary>Every step in the SHIPPED catalog parses to something defensible — either an
    /// island in Sky's real range, or nothing at all. This is the assertion that would catch
    /// a future catalog edit inventing a sixth spelling, which is exactly how the existing
    /// five accumulated.</summary>
    [Fact]
    public void EveryShippedSkyStepParsesToARealIslandOrToNothing()
    {
        var items = SkyQuestDefaults.Items;
        Assert.NotEmpty(items);

        foreach (var island in items.SelectMany(i => SkyIslands.Parse(i.Source)))
        {
            Assert.True(island is >= 1 and <= 8,
                $"Island {island} is not a Plane of Sky island — check the Source prose it came from.");
            // 1.5 is the only fractional one the plane has.
            Assert.True(island % 1 == 0 || island == SkyIslands.HalfIsland,
                $"Island {island} is fractional and is not the half-island.");
        }

        // And the split is the one the design was reasoned about, so a catalog edit that
        // moves it a long way shows up here rather than in a player's screenshot.
        var grouped = items.Count(i => SkyIslands.Parse(i.Source).Count == 1);
        var several = items.Count(i => SkyIslands.Parse(i.Source).Count > 1);
        var anywhere = items.Count(i => SkyIslands.Parse(i.Source).Count == 0);
        Assert.Equal(items.Length, grouped + several + anywhere);
        Assert.True(grouped > several && grouped > 0, "most located steps name exactly one island");
        Assert.True(anywhere > 0, "the trash-mob steps must stay ungrouped");
    }

    // ----- DRA-164: the sixth shape, and the defect it closes -----------------------------

    /// <summary>The GUIDE catalog's Efreeti-drop location, verbatim — one isle word governing a
    /// list of three. All 22 of those objectives carry this exact string, and the island view
    /// is the first thing that ever asked it a question.</summary>
    private const string EfreetiWhere = "Plane of Sky - Isles 1.5, 4 and 8 respectively.";

    /// <summary>
    /// **The before-picture, committed, so the fix is shown to fix something** (trap 34).
    ///
    /// <para>This is the regex `SkyIslands` shipped at `29f18da9`, pasted verbatim. It reads
    /// the string above as island 1.5 ALONE — the plural-list shape captured only the number
    /// touching the word — which would have filed a three-island drop under the half-island
    /// and printed it with exactly as much confidence as a right answer. Green-only coverage
    /// could not have caught it: both parsers return the same row for this step, the same 317
    /// objectives and the same 97/95 split, and only the 103-vs-125 count tells them apart
    /// (see <c>SkyIslandPlacementSweepTests</c>).</para>
    ///
    /// <para>It is a literal rather than a call because the old shape no longer exists to call.
    /// If someone later widens `Parse` in a way that makes this test look redundant, the thing
    /// to check is the assertion below it, not this one.</para>
    /// </summary>
    [Fact]
    public void TheShippedParserReadTheEfreetiDropAsOneIsland()
    {
        var shipped = new Regex(@"\bisles?\b\s*\.?\s*(?<n>\d+(?:\.\d+)?|[a-z]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var before = shipped.Matches(EfreetiWhere)
            .Select(m => m.Groups["n"].Value)
            .Where(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(t => double.Parse(t, NumberStyles.Float, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();

        Assert.Equal([SkyIslands.HalfIsland], before);          // the bug, on the board
        Assert.Equal([1.5, 4, 8], SkyIslands.Parse(EfreetiWhere)); // and the fix
    }

    /// <summary>The list shape, in the spellings the two catalogs actually use between them.
    /// A list may be joined by commas, by "and", or by both — and the isle word may be
    /// singular or plural, because prose is prose.</summary>
    [Theory]
    [InlineData("Plane of Sky - Isles 1.5, 4 and 8 respectively.", new[] { 1.5, 4d, 8d })]
    [InlineData("Isles 1.5, 4, 8", new[] { 1.5, 4d, 8d })]
    [InlineData("Isle 4 and 8", new[] { 4d, 8d })]
    [InlineData("Isles 2 & 7", new[] { 2d, 7d })]
    // Deduplicated and sorted, exactly as the older shapes are.
    [InlineData("Isles 8, 4 and 4", new[] { 4d, 8d })]
    public void AListUnderOneIsleWordIsReadWhole(string source, double[] expected) =>
        Assert.Equal(expected, SkyIslands.Parse(source));

    /// <summary>
    /// **What the list arm must NOT swallow, and why the tail is digits only.**
    ///
    /// <para>"Isle four - griffons and pegasus" is a real catalog string. An `and` arm that
    /// accepted WORDS would read "pegasus" as a second island; "Isle 7: sphinxes, drakes and
    /// undine spirits" would collect three. The list is the one shape DRA-164 admitted and the
    /// scope lock is what keeps it one — these five rows are what a widening would redden.</para>
    /// </summary>
    [Theory]
    [InlineData("Isle four - griffons and pegasus", new[] { 4d })]
    [InlineData("Isle 7: sphinxes, drakes and undine spirits", new[] { 7d })]
    [InlineData("Isle 6: Bazzt Zzzt", new[] { 6d })]
    [InlineData("Isle 2: Protector of Sky; Isle 7: Sister of the Spire", new[] { 2d, 7d })]
    [InlineData("Isle 1.5: Noble Dojorn", new[] { 1.5 })]
    public void TheListArmNeverReadsAWordAsASecondIsland(string source, double[] expected) =>
        Assert.Equal(expected, SkyIslands.Parse(source));

    /// <summary>The committed negatives the plan named, and they include the string 95 of the
    /// 317 guided objectives carry: the wind rune's *"any isle"*. It parses to NOTHING, which
    /// is the TRUE answer — trash drops anywhere on the plane — and a parser that reached for
    /// "one of the early isles" later in the same sentence would invent a location for nearly a
    /// third of the checklist.</summary>
    [Theory]
    [InlineData("Plane of Sky - any isle; the zone page says most players farm the trash mobs "
                + "on one of the early isles.")]
    [InlineData("Dropped by 2 spawns near the tower")]
    [InlineData("Isle of Dread")]
    public void TheListArmLeavesTheOldNegativesEmpty(string source) =>
        Assert.Empty(SkyIslands.Parse(source));

    /// <summary>**The classic half of the app cannot have moved**, and this is the measurement
    /// rather than the assurance: no `Source` string in the shipped `SkyQuestDefaults` uses the
    /// list shape at all, so P3 is additive for the 223 steps the class view has always drawn.
    /// The fixture for the new shape lives in the GUIDE catalog, which is a different file
    /// written by a different hand.</summary>
    [Fact]
    public void NoClassicSkyStepUsesTheListShape()
    {
        var listShape = new Regex(@"\bisles?\b\s*\.?\s*\d+(?:\.\d+)?\s*(?:,|\band\b|&)\s*\d",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var offenders = SkyQuestDefaults.Items
            .Select(i => i.Source ?? "")
            .Where(s => listShape.IsMatch(s))
            .Distinct()
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// <see cref="SkyIslands.SetKey"/> round-trips, which is the whole contract
    /// <c>QuestChecklistRow.IslandKey</c> rests on: the island view reads the SET back out of
    /// the row rather than parsing the heading a human reads (trap 4).
    ///
    /// <para>The zero-padding is the point of the format — "10" must not sort between "1" and
    /// "2" — so the key is asserted as text as well as round-tripped, or a later "tidy-up" to
    /// a plainer format would pass this test while silently re-ordering the groups.</para>
    /// </summary>
    [Fact]
    public void TheIslandKeyRoundTripsAndStaysSortable()
    {
        Assert.Equal("04.0", SkyIslands.SetKey([4]));
        Assert.Equal("01.5|04.0|08.0", SkyIslands.SetKey([1.5, 4, 8]));
        Assert.Equal("", SkyIslands.SetKey([]));

        Assert.Equal([4], SkyIslands.FromSetKey("04.0"));
        Assert.Equal([1.5, 4, 8], SkyIslands.FromSetKey("01.5|04.0|08.0"));

        // Zero-padded keys sort the way the islands do. Unpadded, "10.0" would land between
        // "1.5" and "2.0" and the island list would read 1.5, 10, 2.
        Assert.Equal(
            ["01.5", "02.0", "10.0"],
            new[] { SkyIslands.SetKey([10]), SkyIslands.SetKey([1.5]), SkyIslands.SetKey([2]) }
                .OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>A key it cannot read exactly comes back EMPTY — the row has no island rather
    /// than a guessed one, which is this file's rule everywhere else too.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Island 4")]
    [InlineData("04.0|nonsense")]
    public void AnUnreadableKeyIsNoIslandRatherThanAGuess(string? key) =>
        Assert.Empty(SkyIslands.FromSetKey(key));
}
