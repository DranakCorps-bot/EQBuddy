using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **TURNING A TRACKED GOAL INTO A PLACE** (DRA-216 D5, S13/S14).
///
/// <para>The join has three rules and every one of them is a rule about what NOT to match: the
/// zone rule refuses containment, the creature rule refuses fuzzy, and the place rule refuses
/// the promoter's non-places. Each has a committed negative out of the SHIPPED catalog rather
/// than a hand-built one, because a rule proven only against a fixture is a rule about the
/// fixture (trap 23).</para>
/// </summary>
public class GearTargetsTests
{
    private static TrackedUpgrade Goal(string item, string over = "Rusty Dagger") =>
        new(item, "PRIMARY", over, new DateTime(2026, 9, 15, 20, 0, 0, DateTimeKind.Local));

    private static ItemCatalog Catalog(params ItemCatalog.Record[] records) => new(records);

    private static ItemCatalog.Record Drop(
        string name, params (string Zone, string[] Mobs)[] where) =>
        new()
        {
            Name = name,
            Slots = ["PRIMARY"],
            DropZones = [.. where.Select(w => w.Zone)],
            DropMobs = where.Where(w => w.Mobs.Length > 0)
                .ToDictionary(w => w.Zone, w => w.Mobs.ToList()),
        };

    // ---- the zone rule --------------------------------------------------------------

    /// <summary>Exact title first — the ordinary case, and the one a player sees.</summary>
    [Fact]
    public void AGoalWhoseZoneIsThisZoneIsHere()
    {
        var set = GearTargets.For(
            [Goal("Blackened Wand")],
            Catalog(Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"]))));

        var here = set.Here("Befallen");
        Assert.Equal("Blackened Wand", Assert.Single(here).Item);
        Assert.Equal(["Priest Amiaz"], here[0].Creatures);
    }

    /// <summary>The instance tier folds, which is the whole reason the identity key is asked
    /// rather than the string compared: the log names "Befallen 4 (Refined)" and the catalog
    /// names "Befallen", and they are one place.</summary>
    [Theory]
    [InlineData("Befallen 4 (Refined)")]
    [InlineData("befallen")]
    [InlineData("The Befallen")]
    public void TheZoneRuleFoldsTheInstanceTierTheCaseAndALeadingThe(string spelling)
    {
        var set = GearTargets.For(
            [Goal("Blackened Wand")],
            Catalog(Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"]))));

        Assert.Single(set.Here(spelling));
    }

    /// <summary>
    /// **THE COMMITTED NEGATIVE, AND IT IS A REAL CATALOG VALUE.** <i>Bronze Long Sword</i>'s
    /// own page names <c>Commonlands</c>, and a player standing in <c>West Commonlands</c> must
    /// get nothing for it: containment would hand them a ring in a zone the page never named.
    /// It is <see cref="ZoneLevels"/>' and <see cref="ZoneEras"/>' rule verbatim, for the same
    /// reason one step further on — a wrong band is a number a surface states as fact, and a
    /// wrong target is a dot a player walks to.
    /// </summary>
    [Theory]
    [InlineData("West Commonlands")]
    [InlineData("East Commonlands")]
    [InlineData("North Commonlands")]
    public void ContainmentIsRefusedBothWays(string standingIn)
    {
        var set = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword", ("Commonlands", ["an orc legionnaire"]))));

        Assert.Empty(set.Here(standingIn));
        // …and the reverse direction, which is the one containment gets right by accident.
        var wide = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword", (standingIn, ["an orc legionnaire"]))));
        Assert.Empty(wide.Here("Commonlands"));
    }

    // ---- the place rule -------------------------------------------------------------

    /// <summary>The promoter's non-places, refused by name through the rule
    /// <see cref="TradeskillMaterials.IsPlace"/> already owns (trap 4: not a second copy of
    /// it). All five spellings below are real values in the shipped catalog.</summary>
    [Theory]
    [InlineData("Various Zones")]
    [InlineData("Unknown")]
    [InlineData("}}")]
    [InlineData(":* Dread")]
    [InlineData("Category:Head")]
    public void ANonPlaceIsNotAZoneToGoTo(string notAPlace)
    {
        var set = GearTargets.For(
            [Goal("Ghostly Mote")], Catalog(Drop("Ghostly Mote", (notAPlace, ["a thing"]))));

        Assert.Empty(set.Zones);
        Assert.Equal(GearTargetGap.NoDropZone, Assert.Single(set.Refused).Why);
    }

    /// <summary>One place said twice is one place. The pages are transcription, and a zone
    /// listed under two spellings would otherwise draw two identical rows.</summary>
    [Fact]
    public void ATwiceNamedZoneCollapsesOnTheIdentityKey()
    {
        var set = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword",
                ("Befallen", ["an elf skeleton"]), ("The Befallen", ["a skeleton"]))));

        Assert.Equal("Befallen", Assert.Single(set.Zones).Zone);
    }

    // ---- the two refusals -----------------------------------------------------------

    /// <summary>A goal with no page at all and a goal whose page names nowhere are DIFFERENT
    /// facts, reported apart, because the player can act on them differently: one is a name to
    /// check on eqlwiki, the other is an item that simply does not drop.</summary>
    [Fact]
    public void TheTwoWaysAGoalBecomesNoPlaceAreToldApart()
    {
        var set = GearTargets.For(
            [Goal("Thing We Do Not Ship"), Goal("Quest Reward")],
            Catalog(new ItemCatalog.Record { Name = "Quest Reward", Slots = ["HEAD"] }));

        Assert.Empty(set.Zones);
        Assert.Equal(2, set.Refused.Count);
        Assert.Equal(GearTargetGap.Unreadable,
            set.Refused.Single(r => r.Item == "Thing We Do Not Ship").Why);
        Assert.Equal(GearTargetGap.NoDropZone,
            set.Refused.Single(r => r.Item == "Quest Reward").Why);
        // A set holding only refusals is NOT empty — the block still draws, because a goal
        // EQBuddy cannot place is the thing a player would otherwise assume it was working on.
        Assert.False(set.IsEmpty);
    }

    /// <summary>Nothing tracked is nothing at all: no rows, no refusals, and the flag every
    /// surface draws nothing on.</summary>
    [Fact]
    public void NothingTrackedIsAnEmptySet()
    {
        Assert.True(GearTargets.For([], ItemCatalog.Default).IsEmpty);
        Assert.True(GearTargets.For(null, ItemCatalog.Default).IsEmpty);
        // No catalog at all is every goal UNREADABLE and not a silence — a host that cannot
        // load the catalog must not look like a player who has tracked nothing.
        var blind = GearTargets.For([Goal("Blackened Wand")], null);
        Assert.False(blind.IsEmpty);
        Assert.Equal(GearTargetGap.Unreadable, Assert.Single(blind.Refused).Why);
    }

    /// <summary>The identity is the STORE's key — <c>NormalizeTitle</c>, the one seam — so a
    /// goal tracked off a "+5" row finds the base item's page. S8 is parked: nothing here
    /// invents a number for the "+5".</summary>
    [Fact]
    public void ATieredNameFindsTheBaseItemsPage()
    {
        var set = GearTargets.For(
            [Goal("Blackened Wand +5")],
            Catalog(Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"]))));

        Assert.Equal("Blackened Wand", Assert.Single(set.Zones).Item);
    }

    // ---- the creature rule ----------------------------------------------------------

    /// <summary>The fold the timers already use: a leading article, the backtick the wiki
    /// drops, a plural. The ledger's keys are normalized kill lines and the catalog's names are
    /// wiki titles, which is the pair <see cref="SpawnCatalog.NameMatches"/> was written
    /// for.</summary>
    [Theory]
    [InlineData("an elf skeleton", "elf skeleton")]
    [InlineData("Skeleton L`rodd", "Skeleton Lrodd")]
    [InlineData("orc legionnaires", "orc legionnaire")]
    public void TheCreatureRuleFoldsWhatTheTimersFold(string onThePage, string inTheLedger)
    {
        var here = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword", ("Befallen", [onThePage])))).Here("Befallen");

        var hit = Assert.Single(GearTargets.AtPoint(here, [inTheLedger]));
        Assert.Equal("Bronze Long Sword", hit.Item);
        Assert.Equal(onThePage, hit.Creature);
    }

    /// <summary>
    /// **AND IT IS NOT FUZZY, DELIBERATELY.** <c>NameMatchesFuzzy</c> exists to rescue a TIMER
    /// from a wiki typo, where a miss costs a countdown. Here a false hit is a ring on a dot
    /// that does not drop the item and a player travels to it, so the costs are not symmetric
    /// and the strict fold is the one that only ever fails by drawing nothing.
    ///
    /// <para>The exhibit is the one <see cref="SpawnCatalog"/>'s own comment gives: the fuzzy
    /// rule forgives "Gynok Molto" for "Gynok Moltor". A target layer must not.</para>
    /// </summary>
    [Theory]
    [InlineData("Gynok Moltor", "Gynok Molto")]
    [InlineData("Keljemor", "Leljemor")]
    public void ANearMissIsNotATarget(string onThePage, string inTheLedger)
    {
        // The premise, stated rather than assumed: the fuzzy rule really would have matched.
        Assert.True(SpawnCatalog.NameMatchesFuzzy(onThePage, inTheLedger));

        var here = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword", ("Befallen", [onThePage])))).Here("Befallen");

        Assert.Empty(GearTargets.AtPoint(here, [inTheLedger]));
    }

    /// <summary>Every hit, never the first: one camp can be two goals' answer, and
    /// <c>FirstOrDefault</c> is the bug DRA-84 D4 took out of the gear sweep's Who. S13.5
    /// (several known spawn points) is the same property read the other way — the caller asks
    /// once per point and there is no "the" spawn point anywhere in the module.</summary>
    [Fact]
    public void OnePointCanAnswerTwoGoals()
    {
        var here = GearTargets.For(
            [Goal("Blackened Wand"), Goal("Bronze Long Sword")],
            Catalog(
                Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"])),
                Drop("Bronze Long Sword", ("Befallen", ["Priest Amiaz", "an elf skeleton"]))))
            .Here("Befallen");

        var hits = GearTargets.AtPoint(here, ["Priest Amiaz", "an elf skeleton"]);
        Assert.Equal(3, hits.Count);
        Assert.Contains(hits, h => h.Item == "Blackened Wand" && h.Creature == "Priest Amiaz");
        Assert.Contains(hits, h => h.Item == "Bronze Long Sword" && h.Creature == "an elf skeleton");
    }

    /// <summary>A point whose mobs are all something else, and a zone with no goals at all —
    /// both draw no ring rather than a ring with nothing under it.</summary>
    [Fact]
    public void APointWithNoneOfTheCreaturesIsNotATarget()
    {
        var here = GearTargets.For(
            [Goal("Blackened Wand")],
            Catalog(Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"])))).Here("Befallen");

        Assert.Empty(GearTargets.AtPoint(here, ["a decaying skeleton", "a rat"]));
        Assert.Empty(GearTargets.AtPoint([], ["Priest Amiaz"]));
        Assert.Empty(GearTargets.AtPoint(here, []));
    }

    /// <summary>A zone the page named with nobody under it is a ROW and never a ring: the item
    /// does drop there, and there is no name for a point to be matched on. Saying only one of
    /// those halves would leave a player hunting a dot that cannot appear.</summary>
    [Fact]
    public void AZoneWithNoCreatureIsARowThatCanNeverRing()
    {
        var here = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword", ("Befallen", [])))).Here("Befallen");

        Assert.Empty(Assert.Single(here).Creatures);
        Assert.Empty(GearTargets.AtPoint(here, ["an elf skeleton"]));
        Assert.Contains("nothing here for a ring to match",
            GearTargetPresentation.GoalRow(here[0]));
    }

    // ---- elsewhere ------------------------------------------------------------------

    /// <summary>The zone the map is SHOWING is excluded, which is the point of the call: a line
    /// reading "it also drops in Befallen" while the map shows Befallen is a surface that has
    /// not read its own screen. The rest keep the page's own order — the map has no evidence
    /// with which to rank a place.</summary>
    [Fact]
    public void ElsewhereLeavesOutTheZoneYouAreLookingAt()
    {
        var set = GearTargets.For(
            [Goal("Bronze Long Sword")],
            Catalog(Drop("Bronze Long Sword",
                ("Befallen", ["an elf skeleton"]), ("Crushbone", ["Emperor Crush"]),
                ("Najena", ["a skeleton"]))));

        Assert.Equal(["Crushbone", "Najena"],
            GearTargets.Elsewhere(set, "Bronze Long Sword", "Befallen 2 (Refined)"));
        Assert.Equal(["Befallen", "Crushbone", "Najena"],
            GearTargets.Elsewhere(set, "Bronze Long Sword", ""));
        Assert.Empty(GearTargets.Elsewhere(set, "Something Else", "Befallen"));
    }

    // ---- the words ------------------------------------------------------------------

    /// <summary>Every cap SAYS how much it held back (trap 50), and the count is always the
    /// WHOLE count rather than what was left over.</summary>
    [Fact]
    public void EveryCapSaysWhatItHeldBack()
    {
        var many = new GearTargetZone("Bronze Long Sword", "Kithicor Forest",
            ["decaying healer", "skeleton infantry", "Skeleton Officer", "skeleton trooper",
             "undead cleric"]);
        var row = GearTargetPresentation.GoalRow(many);
        Assert.Contains("decaying healer", row);
        Assert.DoesNotContain("undead cleric", row);
        Assert.Contains("2 more on its page", row);

        var zones = GearTargetPresentation.Elsewhere("Bronze Long Sword",
            ["Befallen", "Blackburrow", "Crushbone", "Najena", "Permafrost"]);
        Assert.Contains("2 more zones on its page", zones);

        // Exactly at the cap it counts nothing, because there is nothing to count.
        Assert.DoesNotContain("more", GearTargetPresentation.GoalRow(
            new GearTargetZone("X", "Befallen", ["a", "b", "c"])));
    }

    /// <summary>The ring count carries its denominator, as every counted line in this repo
    /// does: "2 of your 14" is a fact somebody can argue with and "2 rings" is not. And the
    /// zero arms are two different sentences, because "no points archived here" and "none of
    /// your points is one of these" are two different things to do next.</summary>
    [Fact]
    public void TheRingCountCarriesItsDenominatorAndBothZerosAreTheirOwnSentence()
    {
        Assert.Contains("2 of your 14", GearTargetPresentation.PointsHere(2, 14));
        Assert.Contains("1 of your 14", GearTargetPresentation.PointsHere(1, 14));
        Assert.Contains("No spawn points archived here yet",
            GearTargetPresentation.PointsHere(0, 0));
        Assert.Contains("None of your 14", GearTargetPresentation.PointsHere(0, 14));
    }

    /// <summary>
    /// **THE SUBJECT OF EVERY REFUSAL IS EQBUDDY'S CATALOG AND NEVER THE GAME.** "That item
    /// drops nowhere" is a claim about EverQuest; what happened is that a name did not match a
    /// page we ship, or the page we ship lists no zone.
    /// </summary>
    [Fact]
    public void ARefusalTalksAboutOurPagesAndNotAboutTheGame()
    {
        var unreadable = GearTargetPresentation.Unreadable(["Made Up Blade"]);
        Assert.Contains("No item page shipped", unreadable);
        Assert.Contains("eqlwiki", unreadable);

        var noZone = GearTargetPresentation.NoDropZone(["Quest Reward"]);
        Assert.Contains("names no drop zone", noZone);
        Assert.Contains("as far as this map is concerned", noZone);

        // Named up to three, and the count is the whole count.
        var many = GearTargetPresentation.Unreadable(["A", "B", "C", "D", "E"]);
        Assert.Contains("2 more", many);
        Assert.Equal("", GearTargetPresentation.Unreadable([]));
        Assert.Equal("", GearTargetPresentation.NoDropZone([]));
    }

    /// <summary>The note that keeps this layer from reading as a spawn database. It is the one
    /// sentence in the block that is about EQBuddy rather than about an item, and both halves
    /// are load-bearing: the wiki named the creature, YOUR log put the dot there.</summary>
    [Fact]
    public void TheNoteRefusesToClaimEQBuddyKnowsWhereAnythingSpawns()
    {
        Assert.Contains("does not know where anything spawns",
            GearTargetPresentation.PointsNote);
        Assert.Contains("your own /loc at the kill", GearTargetPresentation.PointsNote);
        Assert.Contains("eqlwiki names the creature", GearTargetPresentation.PointsNote);
    }

    /// <summary>The circle's extra hover line names the goal AND the creature: a point that has
    /// seen five mobs would otherwise be marked without saying which one to wait for.</summary>
    [Fact]
    public void TheCircleTipNamesBothHalves()
    {
        var tip = GearTargetPresentation.CircleTip(
            [new GearTargetHit("Blackened Wand", "Priest Amiaz")]);
        Assert.Contains("Blackened Wand", tip);
        Assert.Contains("Priest Amiaz", tip);
        Assert.Equal("", GearTargetPresentation.CircleTip([]));
    }

    /// <summary>The heading names the zone, because the map's other block does and because this
    /// one is about one zone — a heading that did not say which would read as the whole tracked
    /// list, which is the Helper room's block and a different claim.</summary>
    [Fact]
    public void TheHeadingNamesTheZone()
    {
        Assert.Equal("Going after — Befallen", GearTargetPresentation.Heading("Befallen"));
        Assert.Equal("Going after", GearTargetPresentation.Heading(""));
    }

    // ---- the shipped catalog --------------------------------------------------------

    /// <summary>
    /// **THE EXHIBIT, END TO END, AGAINST THE FILES THAT SHIP** — the item page, the zone, the
    /// creature, and the fact that <see cref="SpawnCatalog"/> knows that creature as a Befallen
    /// NAMED, which is what makes S14's learned countdown join rather than the ordinary point's
    /// projection.
    /// </summary>
    [Fact]
    public void TheShippedCatalogsAgreeOnOneWholeTarget()
    {
        var set = GearTargets.For([Goal("Blackened Wand")], ItemCatalog.Default);
        var here = Assert.Single(set.Here("Befallen"));
        Assert.Equal("Priest Amiaz", Assert.Single(here.Creatures));

        var hit = Assert.Single(GearTargets.AtPoint([here], ["Priest Amiaz"]));
        Assert.Equal("Blackened Wand", hit.Item);

        // S14: the creature is a catalog named, so the circle's countdown is a real learned
        // timer rather than the zone's projection. A refresh that renames either side fails
        // here rather than silently falling back to the estimate.
        var befallen = SpawnCatalog.LoadEmbedded().FindZone("Befallen");
        Assert.NotNull(befallen);
        Assert.Contains(befallen!.Named, e => SpawnCatalog.NameMatches(e.Name, "Priest Amiaz"));
    }

    /// <summary>
    /// **THE JOIN, MEASURED ON THE SHIPPED CATALOG BEFORE ANY OF IT WAS DRAWN.**
    ///
    /// <para>Floors rather than equalities, because the weekly refresh moves these numbers and
    /// a slice should not go red for the catalog growing. What each one is FOR: the place rule
    /// really does remove rows (40 pairs in 15 spellings), most pairs really do name a
    /// creature, and only about a third of those creatures are a zone named — which is the S14
    /// split, and the reason the ordinary point's projected respawn is not a fallback nobody
    /// hits.</para>
    /// </summary>
    [Fact]
    public void TheJoinsOwnNumbersAreCommitted()
    {
        var wearable = ItemCatalog.Default.All.Where(r => r.Slots is { Count: > 0 }).ToList();
        Assert.InRange(wearable.Count, 6_000, 12_000);

        var pairs = wearable.SelectMany(r => (r.DropZones ?? []).Select(z => (r, z))).ToList();
        var places = pairs.Where(p => TradeskillMaterials.IsPlace(p.z)).ToList();
        var refusedSpellings = pairs.Select(p => p.z)
            .Where(z => !TradeskillMaterials.IsPlace(z))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // The place rule is load-bearing and small: it removes real rows, and every one of
        // them is a string a player could not have travelled to.
        Assert.InRange(pairs.Count - places.Count, 20, 200);
        Assert.InRange(refusedSpellings.Count, 5, 40);
        Assert.Contains("Various Zones", refusedSpellings);

        // Almost every place-pair names somebody, which is what makes a ring possible at all.
        var withCreature = places.Count(p =>
            p.r.DropMobs is { } m && m.TryGetValue(p.z, out var mobs) && mobs is { Count: > 0 });
        Assert.True(withCreature * 100.0 / places.Count > 90,
            $"only {withCreature} of {places.Count} place-pairs name a creature");
    }

    /// <summary>
    /// **S13.4, BOTH HALVES** — the five target kinds stay apart in the model, and this build
    /// still only ever produces the one the catalog can answer for.
    ///
    /// <para>The forbid-half alone would be vacuous (trap 34): "no target carries a kind we did
    /// not mean" passes just as well on an enum with one member, which is the collapse S13.4
    /// exists to stop. So the must-list is asserted first — all five are NAMED — and the
    /// production claim second. The day a slice starts producing a <c>QuestGiver</c>, the
    /// second assert reddens and that slice has to come back here and say so.</para>
    /// </summary>
    [Fact]
    public void OnlyTheDropKindIsProducedAndTheOtherFourAreNamedNotInvented()
    {
        // The must-list. Spelled out rather than counted, so renaming one is a failure too.
        Assert.Equal(
            new[]
            {
                GearTargetKind.MobSpawn, GearTargetKind.QuestGiver, GearTargetKind.ComponentSource,
                GearTargetKind.SpawnTimer, GearTargetKind.Route,
            },
            Enum.GetValues<GearTargetKind>());

        // And the honest state of the data: every hit this build can make is a drop.
        var here = GearTargets.For(
            [Goal("Blackened Wand"), Goal("Bronze Long Sword")],
            Catalog(
                Drop("Blackened Wand", ("Befallen", ["Priest Amiaz"])),
                Drop("Bronze Long Sword", ("Befallen", ["Priest Amiaz", "an elf skeleton"]))))
            .Here("Befallen");
        var hits = GearTargets.AtPoint(here, ["Priest Amiaz", "an elf skeleton"]);

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(GearTargetKind.MobSpawn, h.Kind));
    }

    /// <summary>
    /// **THE LAYER IS ON UNTIL A PLAYER SAYS OTHERWISE, AND "OFF" IS THE SET EVERY READER
    /// ALREADY DRAWS NOTHING ON** (D5 Planner review, finding D5-1).
    ///
    /// <para>Two small claims, and the pair is the point. A default that flipped would make the
    /// whole slice invisible to everyone who never found the chip — the same failure a
    /// default-off <c>TrackSpawns</c> would be — and <see cref="GearTargetSet.None"/> being
    /// genuinely empty is what lets the gate be one expression at one producer instead of a
    /// branch in each of the three surfaces.</para>
    ///
    /// <para>The GATE itself lives in <c>GearTargetMemo.For</c>, which is WPF-project and has no
    /// unit tests (<c>docs/TestPlan.md</c> §5) — <c>MapTargetLayerTests</c> asserts it from a
    /// launched app, which is where a reader should go to see it proven.</para>
    /// </summary>
    [Fact]
    public void TheTargetLayerDefaultsOnAndItsOffStateIsTheEmptyAnswer()
    {
        Assert.True(new AppSettings().ShowGearTargetsOnMap);

        Assert.True(GearTargetSet.None.IsEmpty);
        Assert.Empty(GearTargetSet.None.Zones);
        Assert.Empty(GearTargetSet.None.Refused);
        Assert.Empty(GearTargetSet.None.Here("Befallen"));
    }

    /// <summary>
    /// **HIDING THE LAYER IS NOT UNTRACKING, AND THIS IS WHERE THAT IS PROVEN.**
    ///
    /// <para>It is the whole reason the finding was a finding: before the toggle existed the
    /// only way to clear the layer off a busy map was to untrack the goal, which throws away the
    /// decision D4's store exists to remember. So the two live in different places and this
    /// pins it — the map dump cannot see the tracked store, and
    /// <c>MapTargetLayerTests.TheLayerSwitchedOffDrawsNoRingsAndNoBlockAndLeavesTheMapAlone</c>
    /// points here for exactly this half.</para>
    /// </summary>
    [Fact]
    public void SwitchingTheLayerOffLeavesTheTrackedListWhereItWas()
    {
        var settings = new AppSettings();
        settings.TrackedUpgrades["dranak_test"] = [Goal("Blackened Wand")];

        settings.ShowGearTargetsOnMap = false;

        var still = TrackedUpgradeStore.For(settings, "dranak_test");
        Assert.Equal("Blackened Wand", Assert.Single(still).Item);
    }
}
