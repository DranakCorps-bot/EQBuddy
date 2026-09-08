using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The minimized bar's contents (Gate 5c) — what each cell shows and in what order.
///
/// Both widgets carried this table by hand, identically, down to the comments. Nothing
/// tested it on either side, because testing it used to mean launching a window. It is
/// data now, so it can simply be asked.
/// </summary>
public class MiniBarPresentationTests
{
    private static StatsSnapshot Snapshot(double currentDps = 55) => new()
    {
        YourKillCount = 82,
        SessionDps = 41,
        CurrentDps = currentDps,
        Hps = 12.25,
        LootTotal = 39,
        Copper = 5_01_04_08,
        Deaths = [],
        CombatSeconds = 120,
    };

    /// <summary>A profile with these stats starred and no order of its own — the shape every
    /// assertion below is about, since <c>DrawnKeys</c> answers from settings rather than
    /// from a bare list.</summary>
    private static AppSettings Starred(params string[] stats) =>
        new() { MiniStats = [.. stats] };

    [Fact]
    public void OnlyTheStatsYouStarredAppear()
        => Assert.Equal(["kills", "loot"],
            MiniBarPresentation.DrawnKeys(Starred("kills", "loot")));

    [Fact]
    public void CellsFollowTheFixedOrderNotTheOrderYouPickedThem()
    {
        // A bar that reshuffles as you toggle stats is a bar you re-read every time. Since
        // #191 the fixed order is the CANONICAL one rather than the only one — but MiniStats
        // is still not where order comes from, which is what this says.
        Assert.Equal(["kills", "loot", "money"],
            MiniBarPresentation.DrawnKeys(Starred("money", "kills", "loot")));
    }

    [Fact]
    public void EveryCellNamesAVectorThatActuallyExists()
    {
        // The whole point of the conversion: a name that IconPaths does not know would
        // fall back to a blank shape, which on the minimized bar reads as nothing at all.
        Assert.All(MiniBarPresentation.Order,
            key => Assert.Contains(MiniBarPresentation.Cell(Snapshot(), key)!.Icon,
                IconPaths.Names));
    }

    [Fact]
    public void NoCellCarriesAGlyph()
    {
        // #148/#166: a glyph can fail to render outright under Wine, and this surface is
        // up for the whole session.
        Assert.All(MiniBarPresentation.Icons.Values,
            name => Assert.All(name, ch => Assert.True(ch < 0x2190,
                $"'{name}' is a glyph, not an IconPaths name.")));
    }

    /// <summary>"buffs" has a PLACE on the bar and no FACE in this table, and the two lists
    /// say so separately.
    ///
    /// It used to have neither: the key gated the Buffs window and drew nothing at all. OE-7
    /// gave it a chip whose face <c>HudBarView</c> builds from the buff tracker's own count —
    /// there is no buff state on a snapshot for this class to format — and #191 gave it a
    /// slot in the order, because a chip drawn outside the ordered walk is a chip nobody can
    /// drag past its neighbours.</summary>
    [Fact]
    public void BuffsHasAPlaceInTheOrderAndNoFaceInThisTable()
    {
        Assert.DoesNotContain(MiniBarPresentation.BuffsKey, MiniBarPresentation.Order);
        Assert.Null(MiniBarPresentation.Cell(Snapshot(), MiniBarPresentation.BuffsKey));
        Assert.Contains(MiniBarPresentation.BuffsKey, MiniBarPresentation.CanonicalOrder);
        Assert.Equal([MiniBarPresentation.BuffsKey],
            MiniBarPresentation.DrawnKeys(Starred(MiniBarPresentation.BuffsKey)));
    }

    /// <summary>Its canonical slot is where it has always drawn — last, after "deaths". A
    /// negative of the pair above (trap 39): every assertion there would still pass with
    /// "buffs" leading the bar on every profile that never dragged anything.</summary>
    [Fact]
    public void BuffsSitsLastInTheCanonicalOrder()
        => Assert.Equal(MiniBarPresentation.BuffsKey, MiniBarPresentation.CanonicalOrder[^1]);

    [Fact]
    public void AKeyFromALaterVersionIsSkippedRatherThanDrawnBlank()
    {
        Assert.Equal(["kills"], MiniBarPresentation.DrawnKeys(Starred("kills", "somethingNew")));
        Assert.Null(MiniBarPresentation.Cell(Snapshot(), "somethingNew"));
    }

    // ---- THE PLAYER'S ORDER (#191, TheMegaSage; owner lock 2026-09-07) ----------------

    /// <summary>The FLOOR, and it is the default: an untouched profile draws exactly the bar
    /// every release before this one drew. Empty means canonical — the same construction OE-8
    /// gave the park pair, where NaN means slaved.</summary>
    [Fact]
    public void AnEmptyOrderIsTheCanonicalOne()
    {
        Assert.Empty(new AppSettings().MiniBarOrder);
        Assert.Equal(MiniBarPresentation.CanonicalOrder,
            MiniBarPresentation.ResolveOrder(new AppSettings()));
    }

    [Fact]
    public void TheSavedOrderIsTheOrder()
        => Assert.Equal(["money", "kills", "loot"],
            MiniBarPresentation.DrawnKeys(new AppSettings
            {
                MiniStats = ["kills", "loot", "money"],
                MiniBarOrder = ["money", "kills", "loot", "deaths", "pet", "procs", "motes", "buffs"],
            }));

    /// <summary>A key the saved order omits is APPENDED in its canonical place, never
    /// dropped. An omission is a stale file or a stat a later release added, and a chip with
    /// no way back would be a cell lost with nothing naming the loss (trap 20's shape).</summary>
    [Fact]
    public void AKeyTheSavedOrderOmitsIsAppendedRatherThanDropped()
    {
        var order = MiniBarPresentation.ResolveOrder(new AppSettings { MiniBarOrder = ["money"] });
        Assert.Equal("money", order[0]);
        Assert.Equal(MiniBarPresentation.CanonicalOrder.Count, order.Count);
        Assert.All(MiniBarPresentation.CanonicalOrder, key => Assert.Contains(key, order));
    }

    /// <summary>A hand-edited file cannot make the bar draw one chip twice, and a name from a
    /// later version is skipped rather than carried.</summary>
    [Fact]
    public void DuplicatesCollapseAndUnknownNamesAreSkipped()
    {
        var order = MiniBarPresentation.ResolveOrder(
            new AppSettings { MiniBarOrder = ["money", "money", "somethingNew", "kills"] });
        Assert.Equal(["money", "kills"], order.Take(2));
        Assert.DoesNotContain("somethingNew", order);
        Assert.Equal(order.Count, order.Distinct().Count());
    }

    /// <summary>The WRITER half, shipping in the same change as its reader — the
    /// <c>DeadSettingTests</c> posture.</summary>
    [Fact]
    public void SetOrderWritesTheKeysTheReaderReads()
    {
        var settings = new AppSettings();
        MiniBarPresentation.SetOrder(settings, ["money", "kills"]);
        Assert.Equal(["money", "kills"], settings.MiniBarOrder);
        Assert.Equal(["money", "kills"], MiniBarPresentation.ResolveOrder(settings).Take(2));
    }

    /// <summary>The dump token: comma-joined, never spaced, and "-" for a bar with no chips —
    /// the dump is space-separated key=value, so a value with a space in it would silently
    /// become two keys and a key with an empty value cannot be waited on.</summary>
    [Fact]
    public void TheOrderKeyIsOneWord()
    {
        Assert.Equal("money,kills", MiniBarPresentation.OrderKey(["money", "kills"]));
        Assert.Equal("-", MiniBarPresentation.OrderKey([]));
        Assert.DoesNotContain(' ', MiniBarPresentation.OrderKey(MiniBarPresentation.CanonicalOrder));
    }

    /// <summary>The three keys Surface A / SA-1 PROMOTED draw no cell here at all.
    ///
    /// A negative assertion on purpose (trap 39): every one of the positive tests above
    /// would still pass with "dps" quietly back in the table drawing a second, differently
    /// formatted damage number beside the HUD trio's. The fallback rule those keys used to
    /// carry moved to HudGlance and is asserted there — the current rate while a fight is
    /// live, the session rate between pulls.</summary>
    [Theory]
    [InlineData("dps")]
    [InlineData("hps")]
    [InlineData("xp")]
    public void ThePromotedHudNumbersDrawNoCellHere(string key)
    {
        Assert.DoesNotContain(key, MiniBarPresentation.Order);
        // …and no PLACE either, which is the #191 half: the trio is fixed leftmost, and its
        // third slot swaps identity mid-session, so a drag target there would change meaning
        // under the cursor.
        Assert.DoesNotContain(key, MiniBarPresentation.CanonicalOrder);
        Assert.Null(MiniBarPresentation.Cell(Snapshot(), key));
        Assert.Empty(MiniBarPresentation.DrawnKeys(Starred(key)));
        Assert.Equal("", MiniBarPresentation.Text(Snapshot(), key));
    }

    [Fact]
    public void EveryOrderedStatFormatsSomething()
    {
        // A cell that renders an icon and an empty string is worse than an absent one.
        Assert.All(MiniBarPresentation.Order,
            key => Assert.NotEqual("", MiniBarPresentation.Text(Snapshot(), key)));
    }

    // ---- PET DPS ON THE ALWAYS-ON ROW (SIGNED #422) ----------------------------------

    /// <summary>A snapshot with pet damage in it, so the assertions below are about a real
    /// number rather than about zero. 10,560 over 120 combat seconds is 88 dps.</summary>
    private static StatsSnapshot WithPet() => new()
    {
        CombatSeconds = 120,
        PetAbilities = [new SourceDamage("Pet (Gnoll Pup)", 40, 10_560)],
    };

    /// <summary>The floor: with the setting off — the default — nothing about the cells
    /// changes, and pet draws exactly where it always did.</summary>
    [Fact]
    public void WithTheSettingOffThePetCellIsWhereItAlwaysWas()
    {
        Assert.False(new AppSettings().HudGlancePet);
        Assert.Equal(["kills", "pet", "loot"],
            MiniBarPresentation.DrawnKeys(Starred("kills", "pet", "loot")));
        Assert.Equal(MiniBarPresentation.CanonicalOrder,
            MiniBarPresentation.ResolveOrder(new AppSettings()));
    }

    /// <summary>**Never drawn twice.** While the slot is inserted, "pet" leaves the cells —
    /// the membership decision is this class's and not the view's, which is the only thing
    /// standing between a player and two pet-damage numbers on one bar.</summary>
    [Fact]
    public void AnInsertedPetLeavesTheCells()
    {
        var settings = new AppSettings
        {
            MiniStats = ["kills", "pet", "loot"],
            HudGlancePet = true,
        };
        Assert.Equal(["kills", "loot"], MiniBarPresentation.DrawnKeys(settings));
    }

    /// <summary>**And the ★ gets no vote while it is up there** (§2). Un-starring pet with
    /// the slot inserted changes the cells and nothing else — which is exactly why Options
    /// carries a sentence saying so: a tick with no visible effect otherwise reads as broken.
    /// A Theory over both ★ states, because either one alone would pass with the rule
    /// written the other way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheStarDoesNotDecideAnInsertedPetSlot(bool starred)
    {
        var settings = new AppSettings
        {
            MiniStats = starred ? ["kills", "pet"] : ["kills"],
            HudGlancePet = true,
        };
        Assert.Equal(["kills"], MiniBarPresentation.DrawnKeys(settings));
    }

    /// <summary>**Never lost, either.** `MiniBarOrder` keeps pet's slot while the chip is
    /// away — `ResolveOrder` is untouched by the flag — so an eject puts it back where the
    /// player left it rather than where the canonical list would. The negative is the half
    /// that matters (trap 39): the assertion above would still pass if the insert had
    /// stripped "pet" out of the saved order on its way up.</summary>
    [Fact]
    public void AnInsertedPetKeepsItsRememberedSlotForTheEject()
    {
        var settings = new AppSettings
        {
            MiniStats = ["kills", "pet", "loot"],
            MiniBarOrder = ["loot", "pet", "kills", "procs", "motes", "money", "deaths", "buffs"],
            HudGlancePet = true,
        };
        Assert.Equal(["loot", "pet", "kills"], MiniBarPresentation.ResolveOrder(settings).Take(3));
        Assert.Equal(["loot", "kills"], MiniBarPresentation.DrawnKeys(settings));

        // The eject, which is one bool: the chip comes back BETWEEN loot and kills, not on
        // the end and not in canonical position.
        settings.HudGlancePet = false;
        Assert.Equal(["loot", "pet", "kills"], MiniBarPresentation.DrawnKeys(settings));
    }

    /// <summary>The key still has a FACE while it is inserted — the moment the player drags
    /// it back down it is a cell again, so a table that had stopped being able to format it
    /// would draw a hole in the bar.</summary>
    [Fact]
    public void ThePetKeyStillFormatsWhileItIsInserted()
    {
        Assert.Contains(MiniBarPresentation.PetKey, MiniBarPresentation.Order);
        Assert.Equal("88 dps",
            MiniBarPresentation.Text(WithPet(), MiniBarPresentation.PetKey));
    }

    /// <summary>ONE source for the number (§4 / trap 4). The cell formats
    /// <c>StatsSnapshot.PetDps</c> and does not carry its own copy of the expression — the
    /// glance formats the same value into its own fixed shape, and the day one of them gains
    /// a decimal both move.</summary>
    [Fact]
    public void ThePetCellReadsTheSnapshotsOwnRate()
    {
        var s = WithPet();
        Assert.Equal(88, s.PetDps);
        Assert.Equal($"{s.PetDps:0.#} dps", MiniBarPresentation.Text(s, MiniBarPresentation.PetKey));
        // A session with no combat seconds yet is a rate of zero rather than an infinity:
        // the denominator is floored at one second.
        Assert.Equal(0, new StatsSnapshot().PetDps);
    }

    [Fact]
    public void KillsAndDeathsShareTheirIconAndThatIsDeliberate()
    {
        // They are never both a surprise: deaths is yours, kills is theirs, and the two
        // are only ever read with their number attached. Documented so a later pass does
        // not "fix" it into two shapes that mean the same thing.
        Assert.Equal(MiniBarPresentation.Icons["kills"], MiniBarPresentation.Icons["deaths"]);
    }
}
