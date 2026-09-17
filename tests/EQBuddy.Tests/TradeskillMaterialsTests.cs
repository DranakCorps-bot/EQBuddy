using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **THE READER, AND THE SLICE'S OPENING SURVEY AS COMMITTED EVIDENCE** (DRA-149 D3, plan P4).
///
/// <para>Fable's P4 declared an escalation seam — *"if materials cannot be told from products
/// defensibly it STOPS AND ESCALATES with the number"*. It did not have to, and the reason is
/// measured rather than argued: the numbers below are taken against the SHIPPED catalog, so a
/// weekly refresh that moves them reddens here instead of quietly changing what a player is
/// told. <c>scripts/dra149-materials-survey.py</c> is the same measurement outside the suite.</para>
/// </summary>
public class TradeskillMaterialsTests
{
    private static ItemCatalog Catalog => ItemCatalog.Default;

    private static IReadOnlyList<TradeskillMaterial> All(bool droppedOnly = true) =>
        TradeskillMaterials.From(Catalog, [], droppedOnly);

    /// <summary>
    /// **THE PARK MEASURED THE WRONG COLUMN, AND BOTH NUMBERS ARE HERE SO NOBODY HAS TO TAKE
    /// THAT ON FAITH.**
    ///
    /// <para>DRA-71 D8 parked the arithmetic on a survey of <c>[[Category:…]]</c>: 14 pages of
    /// 11,197 name a profession, re-taken on the DRA-84 D3 refresh and still 14. Every word of
    /// that is true. The <c>Recipes</c> column answers the question the categories do not, and
    /// the gap between the two counts is what this slice is.</para>
    /// </summary>
    [Fact]
    public void TheRecipesColumnNamesOrdersOfMagnitudeMoreMaterialsThanTheCategoriesOneDid()
    {
        var materials = All(droppedOnly: false);
        var distinct = materials.Select(m => m.Item).Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.InRange(distinct.Count(), 800, 1_100);
        Assert.Equal(8, materials.Select(m => m.Skill).Distinct().Count());
    }

    /// <summary>
    /// **THE ESCALATION QUESTION, ANSWERED FROM THE SHIPPED FILE.**
    ///
    /// <para>An item page's <c>recipes</c> field names the recipes the item is USED IN, so the
    /// record IS the ingredient. The exhibit is one pair and it is decisive: the blood sac
    /// carries the Brewing heading and drops in two zones; the LAGER it brews into carries no
    /// recipes and no drop zones at all. That is why products need no rule of their own — they
    /// leave through the zone gate, which is a gate the rows have to pass anyway.</para>
    /// </summary>
    [Fact]
    public void AProductCarriesNoRecipesAndTheIngredientCarriesTheHeading()
    {
        var sac = Catalog.Find("A Giant Blood Sac");
        var lager = Catalog.Find("Legion Lager");

        Assert.NotNull(sac);
        Assert.NotNull(lager);
        Assert.Contains("Brewing", sac!.Recipes ?? []);
        Assert.NotEmpty(sac.DropZones ?? []);

        // The product. Not "absent from the catalog" — present, and silent about both things
        // this reader asks, which is the whole of the structural filter.
        Assert.Null(lager!.Recipes);
        Assert.True(lager.DropZones is null || lager.DropZones.Count == 0);

        Assert.Contains(All(), m => m.Item == "A Giant Blood Sac" && m.Skill == Tradeskill.Brewing);
        Assert.DoesNotContain(All(), m => m.Item == "Legion Lager");
    }

    /// <summary>
    /// **THE ONLY GENUINELY AMBIGUOUS CLASS IS SMALL AND IS FARMABLE ANYWAY.**
    ///
    /// <para>An intermediate — an item that is both a recipe OUTPUT somewhere and an ingredient
    /// here — is the one shape the structural filter could get wrong. Measured on the shipped
    /// catalog: a small handful of them drop in a real place, and every one is something a
    /// player genuinely kills for (the pelts, the ores, a loaf of bread). So naming them is TRUE
    /// whichever way you read them, and the slice had nothing to escalate.</para>
    ///
    /// <para>The bound is a RANGE rather than the exact count, deliberately: this is a claim
    /// about a refresh-able corpus, and a guard that reddens on ordinary churn is one the next
    /// person learns to re-run until green (trap 74).</para>
    /// </summary>
    [Fact]
    public void AnIntermediateThatAlsoDropsIsRareAndIsStillSomethingYouKillFor()
    {
        var outputs = Outputs();
        var both = All()
            .Select(m => m.Item)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(outputs.Contains)
            .ToList();

        Assert.InRange(both.Count, 1, 40);
        Assert.Contains("Low Quality Bear Skin", both);
    }

    /// <summary>Every recipe OUTPUT any page names — the corpus asked, rather than guessed at
    /// from an item's name.</summary>
    private static HashSet<string> Outputs()
    {
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in Catalog.All)
            foreach (var (_, recipes) in TradeskillMaterials.Sections(record.Recipes))
                foreach (var line in recipes)
                {
                    var cut = line.IndexOf("(Trivial:", StringComparison.OrdinalIgnoreCase);
                    var name = (cut < 0 ? line : line[..cut]).Trim();
                    if (name.Length > 0) outputs.Add(name);
                }
        return outputs;
    }

    /// <summary>
    /// **THE FOUR REAL SKILLS THAT ARE NOT PROFESSIONS HERE, AS COMMITTED NEGATIVES.**
    ///
    /// <para>Spell Research, Tinkering, Make Poison and Fishing all appear as recipe headings —
    /// this is not a theoretical exclusion, it is 229 pages the reader walks past. None has a
    /// Mastery AA, which is the source <see cref="Tradeskills"/> is curated from, and widening
    /// the list is a decision rather than a fix. The admission test is
    /// <see cref="Tradeskills.Match"/> — the SAME whole-string matcher that admits a skill-up
    /// line — so this cannot drift from the ledger's own rule (trap 4).</para>
    /// </summary>
    [Theory]
    [InlineData("Spell Research")]
    [InlineData("Tinkering")]
    [InlineData("Make Poison")]
    [InlineData("Poison Making")]
    [InlineData("Fishing")]
    [InlineData("Research")]
    [InlineData("Non-Tradeskill")]
    [InlineData("Unknown Use")]
    public void ASkillWithNoMasteryAaIsNotAHeadingThisReaderAdmits(string heading) =>
        Assert.Null(TradeskillMaterials.HeadingFor(heading));

    /// <summary>And the eight ARE admitted, including the raw-wikitext spelling five pages
    /// use and the three spellings the wiki gives Jewelcrafting.</summary>
    [Theory]
    [InlineData("Blacksmithing", Tradeskill.Blacksmithing)]
    [InlineData("Jewelcrafting", Tradeskill.Jewelcrafting)]
    [InlineData("Jewelcraft", Tradeskill.Jewelcrafting)]
    [InlineData("== Tailoring ==", Tradeskill.Tailoring)]
    [InlineData("Fletching", Tradeskill.Fletching)]
    public void TheEightAreAdmittedInEverySpellingTheWikiUses(string heading, Tradeskill expected) =>
        Assert.Equal(expected, TradeskillMaterials.HeadingFor(heading));

    /// <summary>A recipe LINE is never a heading, even when it starts with a profession's own
    /// name — the trivial marker is what tells them apart, and a recipe called
    /// "Blacksmithing Hammer" would otherwise re-open a section it belongs inside.</summary>
    [Fact]
    public void ARecipeLineIsNotAHeading()
    {
        Assert.Null(TradeskillMaterials.HeadingFor("Legion Lager (Trivial: 36)"));
        Assert.Null(TradeskillMaterials.HeadingFor("Blacksmithing (Trivial: 122)"));
    }

    /// <summary>Lines before the first heading belong to nobody. The page put them under no
    /// profession, and handing them to whichever one follows would invent exactly the
    /// association this reader exists to avoid.</summary>
    [Fact]
    public void ALineBeforeAnyHeadingIsAssignedToNobody()
    {
        var sections = TradeskillMaterials.Sections(
            ["Combine in Empty Pot of Gold with Armor of Distraction.",
             "Brewing", "Legion Lager (Trivial: 36)"]);

        Assert.Equal([Tradeskill.Brewing], sections.Keys);
        Assert.Equal(["Legion Lager (Trivial: 36)"], sections[Tradeskill.Brewing]);
    }

    /// <summary>
    /// **"VARIOUS ZONES" IS NOT A CAMP, AND IT IS THE ONE THE WHO RULE WOULD HAVE KEPT.**
    ///
    /// <para>On the gear side the promoter's non-places fell out for free: a <c>}}</c> names no
    /// creature, so the who rule removed it. That luck does not hold here — every one of the
    /// eight professions' <c>Various Zones</c> pairs DOES name creatures, so without this rule
    /// the room would offer "Various Zones" as somewhere to go. <c>MoteCatalogSurveyTests</c>
    /// pins the same three placeholders for the same reason one engine over.</para>
    /// </summary>
    [Theory]
    [InlineData("Various Zones")]
    [InlineData("various zones")]
    [InlineData("Unknown")]
    [InlineData("D3+ Zones")]
    [InlineData("}}")]
    [InlineData(":* Dread")]
    [InlineData("Category:2H Slashing")]
    [InlineData("ITEM REMOVED FROM GAME")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void ADropZoneThatIsNotAPlaceIsRefused(string zone) =>
        Assert.False(TradeskillMaterials.IsPlace(zone));

    /// <summary>And a real place is kept — the committed positive, because a refusal rule that
    /// refused everything would pass every row above (trap 78).</summary>
    [Theory]
    [InlineData("Lower Guk")]
    [InlineData("Crushbone")]
    [InlineData("Temple of Veeshan")]
    public void ARealZoneIsKept(string zone) => Assert.True(TradeskillMaterials.IsPlace(zone));

    /// <summary>
    /// No shipped material carries a non-place among its zones — the rule above, over the whole
    /// catalog rather than over the strings somebody thought of.
    ///
    /// <para><b>The forbidden list here is a LITERAL and is deliberately not
    /// <see cref="TradeskillMaterials.IsPlace"/>'s own.</b> The first version of this test
    /// asserted <c>IsPlace(zone)</c> over zones <c>IsPlace</c> had already filtered, which is
    /// true for every possible implementation including one that refuses nothing — a guard
    /// aimed at nothing, green for no reason (trap 78). It was caught by prove-failing the rule
    /// and watching this row stay green. Stating the offending values independently is what
    /// makes emptying that list redden here.</para>
    /// </summary>
    [Fact]
    public void NoShippedMaterialOffersAZoneThatIsNotAPlace()
    {
        string[] notPlaces = ["Various Zones", "Unknown", "D3+ Zones", "}}", "/", ""];

        foreach (var material in All())
            foreach (var zone in material.Zones)
            {
                Assert.DoesNotContain(zone.Trim(), notPlaces, StringComparer.OrdinalIgnoreCase);
                Assert.False(zone.TrimStart().StartsWith(":*", StringComparison.Ordinal),
                    $"{material.Item} offers \"{zone}\", which is one wiki bullet read as a zone.");
            }
    }

    /// <summary>
    /// **FLETCHING IS THE NAMED GAP, AND IT IS A REAL ZERO.**
    ///
    /// <para>33 materials, not one with a drop zone: they are bought, foraged and crafted. The
    /// plan named it as the profession that draws its own sentence rather than padding, and
    /// this is the measurement behind <see cref="GoalGapReason.NoMaterialDrops"/>. If a refresh
    /// ever gives Fletching a drop page this goes red, which is the right outcome — the
    /// sentence claiming the zero would then be wrong.</para>
    /// </summary>
    [Fact]
    public void FletchingHasMaterialsAndNoneOfThemDrops()
    {
        Assert.NotEmpty(TradeskillMaterials.From(Catalog, [Tradeskill.Fletching], droppedOnly: false));
        Assert.Empty(TradeskillMaterials.From(Catalog, [Tradeskill.Fletching]));
    }

    /// <summary>
    /// **THE FOUNDER'S OWN EXAMPLE, MEASURED** — *"tradeskills (example jewelcrafting): should
    /// recommend zones/creatures where gems drop more commonly"* (FAIL item 3).
    ///
    /// <para>Jewelcrafting's ingredients are gems, they drop in dozens of zones, and nearly all
    /// of them name creatures. This is the row that says the FAIL is answerable from data
    /// EQBuddy already ships rather than from anything fetched.</para>
    /// </summary>
    [Fact]
    public void JewelcraftingNamesGemsInManyZonesWithCreaturesUnderThem()
    {
        var gems = TradeskillMaterials.From(Catalog, [Tradeskill.Jewelcrafting]);

        Assert.InRange(gems.Count, 20, 60);
        Assert.Contains(gems, g => g.Item == "Bloodstone");

        var zones = gems.SelectMany(g => g.Zones).Distinct(StringComparer.OrdinalIgnoreCase);
        Assert.InRange(zones.Count(), 20, 200);

        // Nearly every (gem, zone) pair can say what to kill — which is the half the Founder
        // failed the gear rows for, and the reason the who rule is affordable here too.
        var pairs = gems.SelectMany(g => g.Zones.Select(z => (g, z))).ToList();
        var named = pairs.Count(p => p.g.MobsIn(p.z).Count > 0);
        Assert.True(named >= pairs.Count * 0.8,
            $"only {named} of {pairs.Count} Jewelcrafting (gem, zone) pairs name a creature.");
    }

    /// <summary>
    /// The pick is a FILTER and an empty pick is ALL EIGHT — <see cref="TradeskillPickStore"/>'s
    /// own semantics, which the professions block already states out loud. A reader that read
    /// an empty pick as "none" would empty the room for every player who has never opened the
    /// picker, which is most of them.
    /// </summary>
    [Fact]
    public void AnEmptyPickReadsAsAllEightAndANarrowPickNarrows()
    {
        var all = All();
        var one = TradeskillMaterials.From(Catalog, [Tradeskill.Jewelcrafting]);

        Assert.All(one, m => Assert.Equal(Tradeskill.Jewelcrafting, m.Skill));
        Assert.True(all.Count > one.Count);

        // **SEVEN, not eight — and the missing one is the finding rather than a bug in the
        // pick.** All eight have ingredients; Fletching's never drop, so the drop-only fold
        // has nothing under it and the engine draws that profession's own gap sentence
        // instead. Asserting 8 here would have been a guard quietly demanding a row the data
        // does not carry.
        Assert.Equal(7, all.Select(m => m.Skill).Distinct().Count());
        Assert.DoesNotContain(all, m => m.Skill == Tradeskill.Fletching);
        Assert.Equal(8, All(droppedOnly: false).Select(m => m.Skill).Distinct().Count());
    }

    /// <summary>A material feeding several professions arrives once PER profession, because the
    /// pick narrows on the profession and a row carrying a list would have to be re-filtered at
    /// every reader. Water Flask is the shipped exhibit — Brewing, Pottery and Blacksmithing
    /// all use it.</summary>
    [Fact]
    public void AMaterialSeveralProfessionsNeedArrivesOncePerProfession()
    {
        var rows = All(droppedOnly: false).Where(m => m.Item == "Water Flask").ToList();

        Assert.True(rows.Count > 1);
        Assert.Equal(rows.Count, rows.Select(r => r.Skill).Distinct().Count());
    }

    /// <summary>A null catalog answers an empty list rather than throwing —
    /// <c>HelperInputs.Items</c>' own rule: a fixture without one is a test, not an error.</summary>
    [Fact]
    public void ANullCatalogAnswersNothingRatherThanThrowing() =>
        Assert.Empty(TradeskillMaterials.From(null, []));
}
