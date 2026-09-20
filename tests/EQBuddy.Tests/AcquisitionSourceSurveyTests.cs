using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **WHAT THE SHIPPED CATALOGS CAN AND CANNOT SAY ABOUT WHERE GEAR COMES FROM** (DRA-219,
/// requirements S10; acceptance S25 AC 8).
///
/// <para><c>ItemCatalogWhoCoverageTests</c> is this file's model and its sibling: that one
/// measured the DROP path before D4 was allowed to withhold on it, this one measures the QUEST
/// path before D3 is allowed to withhold on that. S10.1 lists nine acquisition routes and asks
/// for all of them *"when relevant and supported by catalog data"* — so the first job is to find
/// out which of them this repo's data actually supports, and to write down, with numbers, the
/// ones it does not.</para>
///
/// <para><b>What the survey found on the shipped catalogs:</b></para>
///
/// <code>
///    6,844 wearable records                     of 11,196 shipped
///    1,884 of them name at least one quest      2,380 distinct (item, quest) offers
///    1,351 of those offers resolve              56.8% — the number the withhold rule rests on
///    1,213 distinct quest names                 676 of them in no shipped quest entry
///        5 of those 676 are not quests at all   wiki markup the item promoter read as a title
///      552 wearable pairs in the three planes   Sky 110, Hate 248, Fear 194 — every one named
///    1,820 wearable records name NO source      28 carry a recipe, 20 a merchant price
/// </code>
///
/// <para><b>Two of S10.1's routes are REFUSED BY NAME, and the refusal is measured rather than
/// asserted</b> — see <see cref="TheRecipesFieldNamesWhatAnItemIsUsedInAndIsNotACraftedSource"/>
/// and <see cref="TheMerchantValueIsWhatAVendorPaysYouAndIsNotAVendorSource"/>. S20 forbids
/// claiming a source the available data does not support, and both fields read as a source
/// exactly until somebody checks which direction they point.</para>
/// </summary>
public class AcquisitionSourceSurveyTests
{
    /// <summary>The shipped quest list, loaded once — it is ~625 KB of JSON and every row below
    /// asks the same one.</summary>
    private static readonly QuestCatalog Quests = QuestCatalog.LoadEmbedded();

    /// <summary>Every wearable record's distinct quest names, and how many of them EQBuddy's own
    /// quest list can describe — the engine's own denominator, which is the OFFER and not the
    /// item: one item offered under three quests is answered under whichever of them
    /// resolve.</summary>
    private readonly record struct QuestCoverage(int Records, int Offers, int Answered)
    {
        public double OfferShare => Offers == 0 ? 0 : Answered / (double)Offers;
    }

    private static QuestCoverage Survey()
    {
        var quests = Quests;
        int records = 0, offers = 0, answered = 0;
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;
            if (record.Quests is not { Count: > 0 } named) continue;

            var any = false;
            foreach (var quest in named
                         .Where(q => q.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                offers++;
                any = true;
                if (Describable(quests, quest)) answered++;
            }
            if (any) records++;
        }
        return new QuestCoverage(records, offers, answered);
    }

    /// <summary>The engine's own predicate, restated once here rather than at four call sites —
    /// a quest entry that answers none of who / where / when / how is a row that can print a
    /// title and stop, which is what <c>Recommendations.QuestSourceRule</c> withholds.</summary>
    private static bool Describable(QuestCatalog catalog, string quest) =>
        catalog.Quests.FirstOrDefault(q =>
                q.Name.Equals(quest, StringComparison.OrdinalIgnoreCase)) is { } entry
        && (entry.QuestGiver.Length > 0 || entry.StartZone.Length > 0
            || entry.MinLevel > 0 || entry.Items.Count > 0);

    /// <summary>The liveness check, first (trap 78). A catalog that stopped carrying quest names
    /// on its items would make every share below 0/0, and a ratio over nothing is green for no
    /// reason — and it would also make the withhold rule unreachable, which is the state a
    /// coverage floor cannot see.</summary>
    [Fact]
    public void ThereAreQuestSourcedWearablesToSurvey()
    {
        var survey = Survey();

        Assert.True(survey.Records >= 1_000,
            $"Only {survey.Records:N0} wearable records name a quest at all. The share below "
            + "measures a fraction of them, and a fraction of almost nothing says nothing.");
        Assert.True(survey.Offers >= 1_500,
            $"Only {survey.Offers:N0} distinct (item, quest) offers were found — this is the "
            + "population Recommendations.QuestSourceRule acts on.");
        Assert.True(Quests.Quests.Count >= 500,
            "The shipped quest catalog is nearly empty, so every quest offer would be withheld "
            + "and the Farm Gear room's quest half would go silent while reporting honestly.");
    }

    /// <summary>
    /// **THE FLOOR UNDER THE WITHHOLD RULE.**
    ///
    /// <para>Measured at 56.8%, which is low — and the rule ships anyway, because the 43% it
    /// removes are offers that can print a quest title and nothing else. What this floor catches
    /// is the state that would make the rule wrong rather than strict: a quest catalog that
    /// shrank, or an item promoter that started emitting titles in a spelling the quest pages do
    /// not use. Either way the room's quest half empties while every sentence in it stays
    /// true.</para>
    ///
    /// <para><b>It is deliberately far below the measurement</b>, like the DRA-84 floor beside
    /// it: a guard asserting 56.8% would redden on any honest weekly refresh, and this one has
    /// to survive the refresh in order to still be there when it matters.</para>
    /// </summary>
    [Fact]
    public void MostQuestOffersThatMatterCanBeDescribed()
    {
        var survey = Survey();

        Assert.True(survey.OfferShare >= 0.25,
            $"Only {survey.Answered:N0} of {survey.Offers:N0} (item, quest) offers "
            + $"({survey.OfferShare:P1}) resolve to a quest the shipped quest catalog can "
            + "describe. It measured 56.8% when the rule shipped, and under a quarter the "
            + "withhold default is no longer strictness — it is the Farm Gear room's quest half "
            + "emptying itself. Take the refresh that caused it to Helm rather than lowering "
            + "this number.");
    }

    /// <summary>
    /// **THE FIVE STRINGS THAT ARE NOT QUESTS** — the quest side's non-place defect (DRA-84 D4
    /// found the same shape in <c>DropZones</c>: <c>}}</c>, <c>:* Dread</c>).
    ///
    /// <para>The item promoter reads a wiki page's quest list structurally and five of its
    /// outputs are wikitext rather than a title. They are committed by name because a rule that
    /// cannot be shown to FIRE is a rule aimed at nothing (trap 78) — and because the promoter
    /// defect is filed rather than fixed here, so the day it IS fixed this row says so by going
    /// red on a string that has stopped existing.</para>
    /// </summary>
    [Theory]
    [InlineData("</ul>")]
    [InlineData("== See Also ==")]
    [InlineData("{{Screenshot Needed}}")]
    public void TheItemCatalogCarriesQuestTitlesThatAreWikiMarkup(string debris)
    {
        var carrying = ItemCatalog.Default.All
            .Where(r => r.Slots is { Count: > 0 })
            .Where(r => r.Quests is { } q
                        && q.Contains(debris, StringComparer.Ordinal))
            .ToList();

        Assert.NotEmpty(carrying);
        // …and it is refused, which is the half that matters. Nothing in the shipped quest
        // catalog answers to a closing list tag.
        Assert.False(Describable(Quests, debris),
            $"'{debris}' now resolves to a describable quest, which would put a row on the "
            + "player's screen headed by a piece of wikitext.");
    }

    /// <summary>
    /// **S25 AC 8 — the three planes are represented, and by DATA rather than by a list**
    /// (S10.2: *"do not hard-code only these three"*).
    ///
    /// <para>Each is a <c>DropZones</c> value on dozens of wearable records and each names a
    /// creature on effectively all of them, so a candidate dropping there reaches a row with
    /// something to do in it. Nothing in the gear engine mentions a plane: the zones come off the
    /// catalog, which is what makes the fourth plane arrive for free — the paired negative below
    /// is the half that proves it (trap 34).</para>
    /// </summary>
    [Theory]
    [InlineData("Plane of Sky")]
    [InlineData("Plane of Hate")]
    [InlineData("Plane of Fear")]
    public void TheThreeNamedPlanesAreRepresentedAsGearSources(string plane)
    {
        int pairs = 0, named = 0;
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;
            if (record.DropZones is not { } zones
                || !zones.Contains(plane, StringComparer.OrdinalIgnoreCase)) continue;

            pairs++;
            if (record.DropMobs is { } mobs
                && mobs.TryGetValue(plane, out var who) && who.Count > 0) named++;
        }

        Assert.True(pairs >= 50,
            $"{plane} carries only {pairs:N0} wearable drop records. S25 AC 8 names it as a "
            + "source that must be represented where the data supports it, and the data is this "
            + "count.");
        // The who rule would withhold any pair that cannot name a creature, so coverage here is
        // what decides whether the plane reaches a row at all.
        Assert.True(named >= pairs - 1,
            $"{named:N0} of {plane}'s {pairs:N0} wearable pairs name a creature. Recommendations."
            + "WhoRule withholds the rest, so this plane's representation is only as good as "
            + "this number.");
    }

    /// <summary>
    /// **AND THE PLANES ARE NOT A LIST** (S10.2, trap 34's negative half).
    ///
    /// <para>The three above are *"explicit must-cover examples, not the complete future domain
    /// list"*. The proof is that the catalog's wearable drop zones run to hundreds of places with
    /// no plane in the name — the engine buckets whatever string the catalog carries, so a zone
    /// added by a future refresh needs no code at all.</para>
    /// </summary>
    [Fact]
    public void GearSourcesAreNotLimitedToThePlanes()
    {
        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;
            foreach (var zone in record.DropZones ?? []) zones.Add(zone);
        }

        var planes = zones.Count(z => z.StartsWith("Plane of", StringComparison.OrdinalIgnoreCase));
        Assert.True(zones.Count - planes >= 100,
            $"Only {zones.Count - planes:N0} non-plane zones carry a wearable drop. If that is "
            + "genuinely true, gear acquisition HAS become a plane list and S10.2 is broken.");
    }

    /// <summary>
    /// **S10.1's CRAFTED SOURCE IS REFUSED, AND THE FIELD THAT LOOKS LIKE ONE POINTS THE OTHER
    /// WAY** (S20: do not claim a source the data does not support).
    ///
    /// <para><c>ItemCatalog.Record.Recipes</c> is the item page's *"used in"* list — the record
    /// is the INGREDIENT and the entries name the products, which is exactly the property
    /// DRA-149 D3 built Farm Materials on (*"an item page lists what it is USED IN, so the record
    /// IS the material"*). Reading it as "this item is crafted" inverts it, and would offer a
    /// player a recipe for something else entirely.</para>
    ///
    /// <para>The exhibit is committed rather than described: <c>Forged Long Sword</c> is a
    /// wearable whose whole <c>Recipes</c> list is <c>Bloodclaw Long Sword</c> — and that is
    /// itself a catalog item, so the direction is a fact rather than a reading. 28 wearable
    /// records are in exactly that state with no drop zone and no quest, and every one of them
    /// is counted as sourceless rather than offered as craftable.</para>
    /// </summary>
    [Fact]
    public void TheRecipesFieldNamesWhatAnItemIsUsedInAndIsNotACraftedSource()
    {
        var ingredient = ItemCatalog.Default.Find("Forged Long Sword");
        Assert.NotNull(ingredient);
        var product = Assert.Single(ingredient!.Recipes!);
        Assert.Equal("Bloodclaw Long Sword", product);

        // The entry names a PRODUCT the catalog also holds. That is what makes the direction
        // checkable rather than assumed.
        Assert.NotNull(ItemCatalog.Default.Find(product));
        Assert.Null(ingredient.DropZones);
        Assert.Null(ingredient.Quests);

        // And the population the refusal is about, so the day a real crafted-source field
        // arrives this number moves and somebody re-reads the decision.
        var recipeOnly = ItemCatalog.Default.All.Count(r =>
            r.Slots is { Count: > 0 } && r.Recipes is { Count: > 0 }
            && r.DropZones is not { Count: > 0 } && r.Quests is not { Count: > 0 });
        Assert.InRange(recipeOnly, 1, 200);
    }

    /// <summary>
    /// **S10.1's VENDOR SOURCE IS REFUSED, FOR THE SAME REASON ONE FIELD OVER.**
    ///
    /// <para><c>MerchantCopper</c> is eqlwiki's <c>merchant_value</c> — *"VALUE TO VENDOR"*, what
    /// a shop PAYS you, quoted at a Charisma and a faction that differ per page. It says nothing
    /// about whether anybody sells the item, and the one place this repo knows about shops
    /// (<see cref="ZoneMerchants"/>) transcribes zone-map lines that deliberately do not carry
    /// item names at all. So there is no "bought from" datum here, and a row claiming one would
    /// be S20's invented source.</para>
    ///
    /// <para>The committed half is the condition text, which is the evidence: a number that
    /// travels with *"VALUE TO VENDOR with CHA : 80"* is a quote somebody was given for
    /// selling.</para>
    /// </summary>
    [Fact]
    public void TheMerchantValueIsWhatAVendorPaysYouAndIsNotAVendorSource()
    {
        var quoted = ItemCatalog.Default.All
            .Where(r => r.MerchantCondition is { Length: > 0 })
            .ToList();

        Assert.NotEmpty(quoted);
        Assert.Contains(quoted, r =>
            r.MerchantCondition!.Contains("VENDOR", StringComparison.OrdinalIgnoreCase));

        // Every priced record also carries the number, so a surface printing one always has the
        // other — and neither of them is a claim that the item can be bought.
        Assert.All(quoted, r => Assert.NotNull(r.MerchantCopper));

        // ZoneMerchants is the only shop data this repo holds and it names zones and lines, not
        // items — which is why no join from an item to a vendor exists to be made.
        Assert.NotEmpty(ZoneMerchants.Default.Zones);
        Assert.DoesNotContain(
            ZoneMerchants.Default.Zones.SelectMany(ZoneMerchants.Default.LinesFor),
            line => line.Contains("Forged Long Sword", StringComparison.OrdinalIgnoreCase));
    }
}
