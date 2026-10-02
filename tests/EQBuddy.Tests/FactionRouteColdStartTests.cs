using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The cold-start arm in <see cref="UnlockGuidance.Faction"/>** (DRA-747, DRA-728 D2): when
/// the player's own log holds no raiser for a faction, the Helper says what eqlwiki lists
/// instead — labelled as eqlwiki's, never as the player's.
///
/// <para><b>Every route here is a REAL shipped row</b> (plan §8): the Dark Bargainers routes
/// (Book of Turmoil Quest, Bottle of Red Wine) out of the committed
/// <c>FactionRoutes.json</c>, Hateborne's own faction dump, and the shipped quest catalog.
/// The brief's Gnoll Fang chain is not eqlwiki's and appears nowhere. Lion Delight — the
/// wiki's real Barbarian route — is the committed NEGATIVE: D1 refused it (no turn-in item in
/// the catalog), so the arm must never name it.</para>
/// </summary>
public class FactionRouteColdStartTests
{
    private static readonly FactionRoutes Routes = FactionRoutes.LoadEmbedded();
    private static readonly QuestCatalog Catalog = QuestCatalog.LoadEmbedded();

    private static FactionsFile.Snapshot Hateborne()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "factions", "hateborne.txt");
        return new FactionsFile.Snapshot(path, DateTime.Now,
            FactionsFile.Parse(File.ReadAllLines(path)));
    }

    private static InventoryFile.Snapshot Bags(params (string Item, int Count)[] rows) =>
        new("inventory.txt", DateTime.Today,
            rows.ToDictionary(r => r.Item, r => r.Count, StringComparer.OrdinalIgnoreCase));

    private static MobSummary Raiser(string faction, int delta) =>
        new("a Neriak guard", 40, 40, 12.0, 0, 0, [])
        {
            Zone = "Neriak Foreign Quarter",
            Factions = [new MobFactionHit(faction, delta, 40)],
        };

    private static UnlockGuidanceRow Cold(
        string faction, InventoryFile.Snapshot? bags = null, FactionsFile.Snapshot? dump = null) =>
        UnlockGuidance.Faction(faction, dump ?? Hateborne(), [], Routes, Catalog, bags);

    // ---- the arm fires, and says whose number it is -------------------------------------

    /// <summary>No raiser in the pool: the route is drawn as a CATALOG line that names its
    /// eqlwiki page, with the turn-ins to go said PER FACTION and never as one number.</summary>
    [Fact]
    public void WithNoRaiserTheRouteNamesItsPageAndCountsTurnInsPerFaction()
    {
        var row = Cold("Dark Bargainers");

        var route = row.RouteLines[0];
        Assert.Equal(Evidence.Catalog, route.Evidence);
        // Book of Turmoil and Bottle of Red Wine both raise Dark Bargainers +10; the tie
        // breaks on the quest name, so the order is a rule and not a dictionary accident.
        Assert.StartsWith("eqlwiki's “Book of Turmoil Quest” page lists", route.Text);
        Assert.Contains("Dark Bargainers +10", route.Text);

        // Hateborne's dump: Dark Bargainers 0 (2000 to go), Dreadguard Outer 5 (1995),
        // Dreadguard Inner 0 (2000). ceil(2000/10), ceil(1995/5), ceil(2000/5).
        var toGo = Assert.Single(row.RouteLines, l => l.Text.StartsWith("Turn-ins to max"));
        Assert.Equal(Evidence.Catalog, toGo.Evidence);
        Assert.Contains("Dark Bargainers ≈200", toGo.Text);
        Assert.Contains("Dreadguard Outer ≈399", toGo.Text);
        Assert.Contains("Dreadguard Inner ≈400", toGo.Text);

        // The route the cap held back is NAMED, not dropped (trap 50).
        var more = row.RouteLines[^1];
        Assert.Equal(Evidence.Catalog, more.Evidence);
        Assert.Contains("Bottle of Red Wine", more.Text);
    }

    /// <summary>The Bottle of Red Wine row is the table's: requirement and obtain cells are
    /// carried VERBATIM and the page is the table's own title.</summary>
    [Fact]
    public void TheTablesRequirementAndObtainCellsAreQuotedVerbatim()
    {
        var wine = Assert.Single(Routes.Routes, r => r.Quest == "Bottle of Red Wine");
        var line = UnlockGuidance.RouteLine(wine);

        Assert.StartsWith("eqlwiki's “All Positive Faction Quests” page lists the "
            + "Bottle of Red Wine turn-in: hand in 1 Red Wine per turn-in", line);
        Assert.Contains("It asks for: Apprehensive Dark Bargainers (or Stealth).", line);
        Assert.Contains("Purchased Item, Stackable Items, Stacked Turn-Ins", line);
    }

    /// <summary>A route that COSTS a faction says so with a minus sign — the costs are the
    /// first thing a grinder needs, and D1 kept them for exactly this line.</summary>
    [Fact]
    public void ACostIsSaidAsACost()
    {
        var nanrum = Assert.Single(Routes.Routes, r => r.Quest == "A Job for Nanrum");
        Assert.Contains("Broken Skull Clan −1", UnlockGuidance.RouteLine(nanrum));
    }

    /// <summary>The zone and the door are the quest catalog's: the Book of Turmoil starts in
    /// Neriak, and its door is the EXISTING General-tab kind — no new door.</summary>
    [Fact]
    public void TheZoneAndTheDoorComeFromTheQuestCatalog()
    {
        var row = Cold("Dark Bargainers");

        Assert.Equal("Neriak", row.Zone);
        var door = Assert.Single(row.RouteDoors);
        Assert.Equal(UnlockDoorKind.GeneralTabQuest, door.Kind);
        Assert.Equal("Book of Turmoil Quest", door.Target);
        // The wiki-faction door is still the row's own, unchanged.
        Assert.Equal(UnlockDoorKind.WikiFaction, row.Door!.Kind);
    }

    // ---- what the player holds --------------------------------------------------------

    /// <summary>**No dump is "never read", never "0 held".** The row raises
    /// <see cref="UnlockGuidanceRow.NeedsBags"/> and draws no held line at all.</summary>
    [Fact]
    public void AnAbsentDumpIsNeverZeroHeld()
    {
        var row = Cold("Dark Bargainers", bags: null);

        Assert.True(row.NeedsBags);
        Assert.DoesNotContain(row.RouteLines, l => l.Text.Contains("inventory dump"));
        Assert.DoesNotContain(row.RouteLines, l => l.Text.Contains(" 0 "));
    }

    [Fact]
    public void ADumpCountsTheTurnInsYouCouldHandInNow()
    {
        var row = Cold("Dark Bargainers", Bags(("Dark Elf Decapitated Head", 3)));

        Assert.False(row.NeedsBags);
        var held = Assert.Single(row.RouteLines, l => l.Text.StartsWith("Your inventory dump"));
        Assert.Equal(Evidence.Personal, held.Evidence);
        Assert.Equal(
            "Your inventory dump shows 3 Dark Elf Decapitated Head — enough for 3 turn-ins.",
            held.Text);
    }

    /// <summary>A dump that WAS read and holds none is an honest zero — a different sentence
    /// from the unread one, which says nothing at all.</summary>
    [Fact]
    public void ADumpWithNoneOfTheItemSaysSo()
    {
        var row = Cold("Dark Bargainers", Bags(("Bone Chips", 40)));

        var held = Assert.Single(row.RouteLines, l => l.Text.StartsWith("Your inventory dump"));
        Assert.Contains("0 Dark Elf Decapitated Head", held.Text);
        Assert.Contains("not enough for one turn-in yet", held.Text);
    }

    // ---- personal evidence wins the row ---------------------------------------------

    /// <summary>**With a raiser in the pool the row is UNCHANGED** — every personal field
    /// equal to the routes-off answer — plus at most one "the wiki also lists" line, with no
    /// door, no zone and no arithmetic of its own.</summary>
    [Fact]
    public void WithARaiserTheRowIsUnchangedPlusOneAlsoLine()
    {
        var pool = new[] { Raiser("Dark Bargainers", 4) };
        var off = UnlockGuidance.Faction("Dark Bargainers", Hateborne(), pool);
        var on = UnlockGuidance.Faction("Dark Bargainers", Hateborne(), pool, Routes, Catalog, null);

        Assert.Equal(off.Lines, on.Lines);
        Assert.Equal(off.Who, on.Who);
        Assert.Equal(off.Zone, on.Zone);
        Assert.Equal(off.Door, on.Door);
        var also = Assert.Single(on.RouteLines);
        Assert.Equal(Evidence.Catalog, also.Evidence);
        Assert.StartsWith("The wiki also lists 2 turn-in routes for this faction:", also.Text);
        Assert.Empty(on.RouteDoors);
        Assert.False(on.NeedsBags);
    }

    /// <summary>A caller that passes no routes — the Unlocks tab, today — gets exactly the
    /// row it always got.</summary>
    [Fact]
    public void NoRoutesPassedIsTheOldRow()
    {
        var row = UnlockGuidance.Faction("Dark Bargainers", Hateborne(), []);
        Assert.Empty(row.RouteLines);
        Assert.Empty(row.RouteDoors);
        Assert.False(row.NeedsBags);
        Assert.Equal("", row.Zone);
    }

    // ---- the other two statuses -----------------------------------------------------

    /// <summary>**Lion Delight is the committed negative.** It is eqlwiki's real Barbarian
    /// route, D1 refused it (no turn-in item in the catalog), and the arm must not resurrect
    /// it — Rogues of the White Rose's routes are the ones D1 admitted.</summary>
    [Fact]
    public void ARefusedRouteIsNeverNamed()
    {
        var row = Cold("Rogues of the White Rose");

        Assert.NotEmpty(row.RouteLines);
        Assert.DoesNotContain(row.RouteLines, l => l.Text.Contains("Lion"));
        Assert.Contains("Halas", row.Zone);
    }

    /// <summary>A faction the wiki names as raised with NO amount gets one sentence naming the
    /// quests, and no arithmetic and no bag count — there is no delta to divide by.</summary>
    [Fact]
    public void ADirectionOnlyFactionGetsOneSentenceAndNoArithmetic()
    {
        Assert.Equal(FactionRoutes.Status.DirectionOnly, Routes.StatusFor("Deepwater Knights"));
        var row = Cold("Deepwater Knights");

        var line = Assert.Single(row.RouteLines);
        Assert.Equal(Evidence.Catalog, line.Evidence);
        Assert.StartsWith("eqlwiki names ", line.Text);
        Assert.Contains("with no amount EQBuddy could read", line.Text);
        Assert.False(row.NeedsBags);
        Assert.Empty(row.RouteDoors);
    }

    [Fact]
    public void AFactionTheWikiNeverNamesDrawsNothingNew()
    {
        var row = Cold("A Faction Nobody Wrote About");
        Assert.Empty(row.RouteLines);
        Assert.False(row.NeedsBags);
    }

    // ---- through the Helper ---------------------------------------------------------

    private static HelperInputs Inputs(InventoryFile.Snapshot? bags, params MobSummary[] pool) =>
        new HelperInputs([], pool, Hateborne(), ["Dark Bargainers"], [], [], [], false,
            [], [], Catalog)
        {
            Routes = Routes,
            Bags = bags,
        };

    /// <summary>Through <see cref="Recommendations.Rank"/>: the route lines arrive as
    /// <see cref="Evidence.Catalog"/> facts (so the catalog label is appended), the door is
    /// the existing quest-catalog door, and an absent dump is the existing
    /// <see cref="GoalGapReason.NoInventoryDump"/> gap.</summary>
    [Fact]
    public void TheHelperDrawsTheRouteAsCatalogAndAsksForTheDump()
    {
        var set = Recommendations.Rank(Inputs(bags: null), [HelperGoal.WorkOnFaction]);

        var row = Assert.Single(set.Top);
        Assert.Equal("Neriak", row.Zone);
        var route = Assert.Single(row.Why.OfType<WordedFact>(), w => w.Text.StartsWith("eqlwiki's"));
        Assert.Equal(Evidence.Catalog, route.Evidence);
        Assert.EndsWith(HelperPresentation.CatalogLabel, HelperPresentation.Why(route));
        Assert.Contains(row.Doors, d => d.Kind == HelperDoorKind.QuestCatalog
                                        && d.Target == "Book of Turmoil Quest");
        Assert.Contains(set.Gaps, g => g == new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NoInventoryDump));
        Assert.Contains("what you are carrying", HelperPresentation.Gap(
            new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NoInventoryDump)));
    }

    [Fact]
    public void WithADumpTheHelperAsksForNothing()
    {
        var set = Recommendations.Rank(
            Inputs(Bags(("Dark Elf Decapitated Head", 1))), [HelperGoal.WorkOnFaction]);

        Assert.DoesNotContain(set.Gaps, g => g.Reason == GoalGapReason.NoInventoryDump);
        Assert.Contains(set.Top[0].Why.OfType<WordedFact>(),
            w => w.Evidence == Evidence.Personal && w.Text.Contains("enough for 1 turn-in."));
    }

    /// <summary>With a raiser, the Helper's faction row is the routes-off row plus the one
    /// "also" line — and no gap, because nothing asked about bags.</summary>
    [Fact]
    public void WithARaiserTheHelperRowGainsOnlyTheAlsoLine()
    {
        var raiser = Raiser("Dark Bargainers", 4);
        var off = Recommendations.Rank(
            Inputs(null, raiser) with { Routes = null }, [HelperGoal.WorkOnFaction]);
        var on = Recommendations.Rank(Inputs(null, raiser), [HelperGoal.WorkOnFaction]);

        var before = off.Top[0].Why.Select(HelperPresentation.Why).ToList();
        var after = on.Top[0].Why.Select(HelperPresentation.Why).ToList();
        Assert.Equal(before, after.Take(before.Count));
        var extra = Assert.Single(after.Skip(before.Count));
        Assert.StartsWith("The wiki also lists", extra);
        Assert.Equal(off.Top[0].Doors, on.Top[0].Doors);
        Assert.DoesNotContain(on.Gaps, g => g.Reason == GoalGapReason.NoInventoryDump);
    }

    /// <summary>**Through the REAL assembly point.** Every test above builds its own
    /// <see cref="HelperInputs"/>, so a build where <c>HelperSources.Gather</c> stopped
    /// supplying the routes or the bags would leave them all green while both surfaces lost the
    /// arm — measured: deleting the <c>Routes</c> line reddened nothing until this row.</summary>
    [Fact]
    public void TheOneAssemblyPointSuppliesTheRoutesAndTheSameDumpWornWasReadFrom()
    {
        var dump = Bags(("Red Wine", 2));
        var sources = new HelperSources(new HelperSources.Reads(
            () => [], () => [], () => [], () => [], () => dump, _ => null, () => 0));
        sources.Read(null, "Dranak", "erollisi");

        var inputs = sources.Gather(new AppSettings(), "Dranak|erollisi", new UnlockSource(),
            ResolvedLevel.Unknown, Catalog, null, [], "", []).Inputs;

        Assert.Same(FactionRoutes.Default, inputs.Routes);
        Assert.Same(dump, inputs.Bags);
    }

    /// <summary>The FarmGear sentence is untouched — the turn-in wording is a second arm, not
    /// an edit of the first.</summary>
    [Fact]
    public void TheGearGapStillTalksAboutWhatYouWear() =>
        Assert.Contains("what you are wearing", HelperPresentation.Gap(
            new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoInventoryDump)));
}
