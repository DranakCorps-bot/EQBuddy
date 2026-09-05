using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The four data rooms' whole-room empty — the rule and the words, where a test can reach
/// them.** The WPF layer has no unit tests (docs/TestPlan.md §5), so a predicate left inline
/// in a room is a predicate nothing can check, and this one is not cosmetic: a room-level
/// empty COLLAPSES the tab strip and the body, so a predicate that fires while the room has
/// something in it is a capability the player can no longer reach.
///
/// The rows are written so each one FAILS if a requirement stops being met:
///
///  1. Every room's empty needs a character to be missing — the root condition, asked once.
///  2. Every GUARD clause fails on its own, including the two that are not about the log at
///     all (Gear's wishlist, World's markers, Quests' hand-ticked steps).
///  3. The four sets of words are the room's OWN and are not each other's, and every one of
///     them names the first thing to do.
/// </summary>
public class ShellRoomEmptyTests
{
    private static (string Server, string Character) Nobody => ("", "");
    private static (string Server, string Character) Somebody => ("test", "Testchar");

    // ---- 1. the root condition -------------------------------------------------

    /// <summary>
    /// **The one thing all four have in common, asserted for all four at once.** A profile
    /// EQBuddy is following has something to say in every one of these rooms, even when the
    /// current session is quiet — Progress has a level history, Gear has bags, World has a
    /// zone, Quests has a tracker — so none of them may ever collapse over a known
    /// character. This is the row that fails if somebody widens a predicate to "and the
    /// session is quiet", which is the obvious-looking generalisation and the wrong one.
    /// </summary>
    [Fact]
    public void NoRoomIsEverEmptyOnceAcharacterIsKnown()
    {
        var blank = new StatsSnapshot();

        Assert.True(ShellRoomEmpty.NoCharacterYet(Nobody));
        Assert.False(ShellRoomEmpty.NoCharacterYet(Somebody));

        Assert.True(ShellRoomEmpty.ProgressIsEmpty(Nobody, blank));
        Assert.True(ShellRoomEmpty.GearIsEmpty(Nobody, blank, wishlistCount: 0));
        Assert.True(ShellRoomEmpty.WorldIsEmpty(Nobody, blank));
        Assert.True(ShellRoomEmpty.QuestsIsEmpty(Nobody, tickedSteps: 0));

        Assert.False(ShellRoomEmpty.ProgressIsEmpty(Somebody, blank));
        Assert.False(ShellRoomEmpty.GearIsEmpty(Somebody, blank, wishlistCount: 0));
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Somebody, blank));
        Assert.False(ShellRoomEmpty.QuestsIsEmpty(Somebody, tickedSteps: 0));
    }

    /// <summary>A character with nothing but a NAME is still a character. The identity pair
    /// is <c>(Server, Character)</c> in every UI.Shared reader and <c>(Character, Server)</c>
    /// on the widget, and a tuple conversion is positional — so a room that got them the
    /// wrong way round would report "no character" for every profile whose SERVER string is
    /// empty, and collapse four rooms over it. This is that failure, in the only place a
    /// unit test can stand.</summary>
    [Fact]
    public void TheCharacterHalfIsWhatDecidesAndNotTheServerHalf()
    {
        Assert.False(ShellRoomEmpty.NoCharacterYet((Server: "", Character: "Testchar")));
        Assert.True(ShellRoomEmpty.NoCharacterYet((Server: "test", Character: "")));
    }

    // ---- 2. the guards, one failing row each -----------------------------------

    /// <summary>Progress is the one room with no character-independent source, so its guard
    /// is a list of everything its three tabs draw. Each clause on its own is enough to keep
    /// the room open: a snapshot that carries rows with no character is a state to SHOW and
    /// argue about, not one to paint over.</summary>
    [Theory]
    [MemberData(nameof(ProgressContent))]
    public void ProgressStaysOpenWheneverThereIsAnythingToChart(StatsSnapshot s) =>
        Assert.False(ShellRoomEmpty.ProgressIsEmpty(Nobody, s));

    public static TheoryData<StatsSnapshot> ProgressContent() =>
    [
        new StatsSnapshot { XpTicks = 1 },
        new StatsSnapshot { Levels = [new TimedDetail(DateTime.Now, "12")] },
        new StatsSnapshot { SkillUps = [new SkillDetail("1H Blunt", 1, 22)] },
        new StatsSnapshot { AaAbilities = [new AaAbilityInfo("Innate Run Speed", 1, DateTime.Now)] },
        new StatsSnapshot { Copper = 1 },
        // Motes are summarised out of the loot list, so the loot clause is the Wealth tab's
        // second half as well as Gear's first.
        new StatsSnapshot { Loot = [new LootDetail("a mote of water", 1, "an air elemental")] },
        new StatsSnapshot { Faction = [new FactionDetail("Guards of Qeynos", 1, 1)] },
    ];

    /// <summary>
    /// **The wishlist is a SETTINGS list and the one thing in the Gear room a player can
    /// build before they ever play.** Collapsing the room over it would take away something
    /// they typed themselves, which is the whole reason <c>GearIsEmpty</c> takes a count
    /// instead of asking about the character alone.
    /// </summary>
    [Fact]
    public void GearStaysOpenForAwishlistTypedBeforeTheFirstSession()
    {
        var blank = new StatsSnapshot();
        Assert.True(ShellRoomEmpty.GearIsEmpty(Nobody, blank, wishlistCount: 0));
        Assert.False(ShellRoomEmpty.GearIsEmpty(Nobody, blank, wishlistCount: 1));
        Assert.False(ShellRoomEmpty.GearIsEmpty(Nobody,
            new StatsSnapshot { Loot = [new LootDetail("Rusty Dagger", 1, "a rat")] },
            wishlistCount: 0));
    }

    /// <summary>
    /// **"Drop camp marker" is room chrome whose button works with no log at all**, so a
    /// profile carrying markers has something this room must keep showing. The zone clauses
    /// are the other three tabs, including Path — which looks character-independent and is
    /// not, since <c>TravelView</c> plans FROM the zone the log last saw.
    /// </summary>
    [Fact]
    public void WorldStaysOpenForAzoneAmarkerOrAdeath()
    {
        Assert.True(ShellRoomEmpty.WorldIsEmpty(Nobody, new StatsSnapshot()));
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Nobody,
            new StatsSnapshot { CurrentZone = "Lower Guk" }));
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Nobody,
            new StatsSnapshot { Zones = [new TimedDetail(DateTime.Now, "Lower Guk")] }));
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Nobody,
            new StatsSnapshot { Markers = [new MarkerDetail(DateTime.Now, "Marker 1")] }));
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Nobody,
            new StatsSnapshot { Deaths = [new TimedDetail(DateTime.Now, "a froglok knight")] }));
    }

    /// <summary>
    /// **The fifth tab had to reach the predicate, or the room would hide it.** A room-level
    /// empty collapses the strip and every tab under it, so a World predicate still written
    /// over four tabs would hide Drops on exactly the profiles the other four have nothing
    /// to say about — a surface removed by something that reads as untouched code, which is
    /// trap 34's shape.
    ///
    /// The kill with NO loot is the row that keeps this honest: the Drops tab shows
    /// creatures that dropped something, so a kill on its own is not content for it.
    /// </summary>
    [Fact]
    public void WorldStaysOpenForAcreatureThatDroppedSomething()
    {
        Assert.False(ShellRoomEmpty.WorldIsEmpty(Nobody, new StatsSnapshot
        {
            Mobs = [Mob([new MobLoot("Fine Steel Long Sword", 1, null)])],
        }));

        Assert.True(ShellRoomEmpty.WorldIsEmpty(Nobody, new StatsSnapshot { Mobs = [Mob([])] }));

        static MobSummary Mob(List<MobLoot> loot) =>
            new("a froglok tad", 3, 3, 8.0, 0.4, 0, loot);
    }

    /// <summary>
    /// **Epic and Sky steps are ticked in SETTINGS and survive a profile that has never seen
    /// a log line.** #204/#209, #210 and #212 are three separate bugs about those very lists
    /// losing the thing that showed them; a room that hid them because the identity was
    /// unknown would be a fourth in the same family.
    /// </summary>
    [Fact]
    public void QuestsStaysOpenForAstepTickedByHand()
    {
        Assert.True(ShellRoomEmpty.QuestsIsEmpty(Nobody, tickedSteps: 0));
        Assert.False(ShellRoomEmpty.QuestsIsEmpty(Nobody, tickedSteps: 1));
    }

    // ---- 3. the words ----------------------------------------------------------

    public static TheoryData<string, RoomEmptyMessage> Messages() => new()
    {
        { "progress", ShellRoomEmpty.Progress },
        { "gear", ShellRoomEmpty.Gear },
        { "world", ShellRoomEmpty.World },
        { "quests", ShellRoomEmpty.Quests },
    };

    /// <summary>
    /// Every room-level empty follows the inventory-dump voice Home set: what is missing
    /// (the heading), what to do and where (<c>/log on</c>, then the Logs folder in Options),
    /// and what happens next.
    ///
    /// **The <c>/log on</c> row is the one with teeth.** These four rooms deliberately carry
    /// no ⧉ copy of their own — the <c>/outputfile</c> commands their surfaces offer all need
    /// a character logged in, so on a profile with none they are not the next thing to do.
    /// That is only defensible if the empty NAMES the step that is, and this is what says it
    /// still does.
    /// </summary>
    [Theory]
    [MemberData(nameof(Messages))]
    public void EveryRoomEmptySaysWhatIsMissingAndWhatToDoAboutIt(string room, RoomEmptyMessage message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message.Heading), room);
        Assert.DoesNotContain("no data", message.Heading, StringComparison.OrdinalIgnoreCase);
        Assert.True(message.Explanation.Length > 80,
            $"{room}'s explanation is a label, not an answer: {message.Explanation}");
        Assert.Contains("/log on", message.Explanation);
        Assert.Contains("Options", message.Explanation);
    }

    /// <summary>
    /// **Four rooms, four sentences — and Home's is a fifth.** Bevel's ruling is that the
    /// ROOM decides position and the SURFACE keeps its own words; reusing one room's copy in
    /// another is the drift that turns six empty states into one generic panel, and reusing
    /// HOME's is worse, because Home's sentence is about not knowing who you are and these
    /// are about what a particular room is waiting for.
    ///
    /// A count rather than a hand-written list of pairs: the day a seventh room joins, this
    /// row covers it by construction.
    /// </summary>
    [Fact]
    public void NoTwoRoomsShareTheirEmptyStateCopy()
    {
        var headings = Messages().Select(row => ((RoomEmptyMessage)row[1]).Heading).ToList();
        var explanations = Messages().Select(row => ((RoomEmptyMessage)row[1]).Explanation).ToList();

        Assert.Equal(headings.Count, headings.Distinct().Count());
        Assert.Equal(explanations.Count, explanations.Distinct().Count());

        Assert.DoesNotContain(HomeReadout.EmptyIdentity, explanations);
        Assert.DoesNotContain(LivePresentation.EmptyExplanation, explanations);
        Assert.DoesNotContain(LivePresentation.EmptyHeading, headings);
    }
}
