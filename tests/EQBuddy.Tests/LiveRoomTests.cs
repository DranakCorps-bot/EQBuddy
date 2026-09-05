using System.Reflection;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The LIVE room's decisions, which live in Core and `UI.Shared` for the reason every
/// window sum in this repo does: the WPF layer has no unit tests (docs/TestPlan.md §5), so
/// a rule left inline in a room is a rule nothing can check.
///
/// The rows here are not a description of the code — they are what Bevel's Live pre-design
/// and Helm's ~6:35 AM CT sign actually require, each written so it FAILS if the
/// requirement stops being met:
///
///  1. The room's tabs are one definition, and its addresses round-trip.
///  2. The Home/Live boundary holds from LIVE's side: a sibling record, one shared merge.
///  3. Live's words are its OWN — never Home's refusal sentence.
///  4. One producer for the three meters the breakout window also draws.
///  5. Nothing on this surface ticks on the clock inside a repaint gate.
/// </summary>
public class LiveRoomTests
{
    // ---- 1. one definition of the room's rooms ---------------------------------

    /// <summary>Every tab round-trips through its own key, and the address the shell hands
    /// `Navigate` parses back to it. Same assertion `ShellNavigationTests` makes for the
    /// other four surfaces, written here too because Live's keys are the ones most likely
    /// to be "improved": `damage` reads like it should be `combat`, and `raids` looks like
    /// a leftover from the room it came from — which is exactly what it is, deliberately.
    /// </summary>
    [Fact]
    public void EveryLiveTabRoundTripsThroughItsOwnKey()
    {
        foreach (var tab in Enum.GetValues<LiveTab>())
        {
            var key = LiveSurface.KeyFor(tab);
            Assert.NotEmpty(key);
            Assert.NotEmpty(LiveSurface.LabelFor(tab));
            Assert.Equal(tab, LiveSurface.TabForKey(key));
            Assert.Equal((ShellPage.Live, (string?)key),
                ShellPages.ParseAddress(ShellPages.Address(ShellPage.Live, key)));
        }
    }

    /// <summary>The old names, which have to keep landing: `combat` is what the widget's
    /// card and the phone's screen are called, `fight` is what the Combat card's ⧗ opens,
    /// and `hps`/`dps` are the MiniStats keys the breakouts are gated on. A player or a
    /// script reaching for one of those should land somewhere true.</summary>
    [Theory]
    [InlineData("combat", LiveTab.Damage)]
    [InlineData("dps", LiveTab.Damage)]
    [InlineData("DAMAGE", LiveTab.Damage)]
    [InlineData("  hps  ", LiveTab.Healing)]
    [InlineData("heals", LiveTab.Healing)]
    [InlineData("fight", LiveTab.Timeline)]
    [InlineData("creatures", LiveTab.Kills)]
    [InlineData("raids", LiveTab.Raids)]
    public void TheOldNamesForTheseSurfacesStillLand(string key, LiveTab expected) =>
        Assert.Equal(expected, LiveSurface.TabForKey(key));

    /// <summary>Unknown keys answer null rather than snapping to a default — the refusal
    /// every sibling surface makes, and the negative that keeps the row above from going
    /// vacuous (trap 39).</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("drops")]
    [InlineData("wealth")]
    [InlineData(null)]
    public void AnUnknownRoomKeyResolvesToNothing(string? key) =>
        Assert.Null(LiveSurface.TabForKey(key));

    /// <summary>**`drops` is the one in that list worth saying out loud.** The Drops tab
    /// ships from the same v1 window as the Kills tab this room takes, and it belongs to
    /// World — a Live that quietly answered to its key would be the first step of building
    /// World's half here by accident.
    ///
    /// **Since S2 that sentence has somewhere to point**, so the second half is asserted
    /// too: it is not enough that Live refuses the key, because a key nothing answers to is
    /// a room nobody can reach. `world:drops` is where it went.</summary>
    [Fact]
    public void DropsIsNotALiveRoomBecauseCampResearchIsWorlds()
    {
        Assert.Null(LiveSurface.TabForKey("drops"));
        Assert.DoesNotContain("drops", ShellPages.Rooms(ShellPage.Live).Select(r => r.Key));

        Assert.Equal(WorldTab.Drops, WorldSurface.TabForKey("drops"));
        Assert.Contains("drops", ShellPages.Rooms(ShellPage.World).Select(r => r.Key));
    }

    /// <summary>A badge with nothing to say is null rather than a zero — a strip reading
    /// "0 dps · 0 hps · 0 kinds" on a fresh launch is noise where an unbadged chip is an
    /// honest "not yet". Every tab still gets its chip either way.</summary>
    [Fact]
    public void AFreshSessionBadgesNothingAndStillOffersEveryTab()
    {
        var tabs = LivePresentation.Tabs(new StatsSnapshot(), killKinds: 0,
            raidsDefeated: 0, raidsTotal: 0);

        Assert.Equal(Enum.GetValues<LiveTab>().Length, tabs.Count);
        Assert.All(tabs, t => Assert.Null(t.Value));
    }

    [Fact]
    public void TheBadgesAreTheNumbersEachTabIsAbout()
    {
        var tabs = LivePresentation.Tabs(
            new StatsSnapshot { SessionDps = 118.42, Hps = 30, PetName = "Gharuk",
                PetAbilities = [new SourceDamage("Bite", 4, 100)] },
            killKinds: 3, raidsDefeated: 2, raidsTotal: 21);

        string? Badge(LiveTab tab) => tabs.Single(t => t.Tab == tab).Value;
        Assert.Equal("118.4 dps", Badge(LiveTab.Damage));
        Assert.Equal("30 hps", Badge(LiveTab.Healing));
        Assert.Equal("Gharuk", Badge(LiveTab.Pet));
        Assert.Equal("3 kinds", Badge(LiveTab.Kills));
        // The same "2 / 21" the Raids card's own header carried under Progress, so the
        // badge did not change meaning when the room did.
        Assert.Equal("2 / 21", Badge(LiveTab.Raids));
    }

    /// <summary>A bard mid-song has healing that the game logs no amounts for. The Healing
    /// badge says so rather than reading as nothing — the same refusal the tab's own empty
    /// state makes, and for the same reason it was written (David, live test 2026-08-06).
    /// </summary>
    [Fact]
    public void RegenTicksBadgeTheHealingTabEvenWithNoHpsRows()
    {
        var tabs = LivePresentation.Tabs(new StatsSnapshot { RegenTicks = 12 },
            killKinds: 0, raidsDefeated: 0, raidsTotal: 0);

        Assert.Equal("12 ticks", tabs.Single(t => t.Tab == LiveTab.Healing).Value);
    }

    // ---- 2. the Home/Live boundary, from Live's side ---------------------------

    /// <summary>
    /// **The sibling record carries what Home's cannot, and that is the signed shape.**
    /// `HomeRoomTests` fails the build if `RecentSession` grows any of these five names;
    /// this is the other half — a `LiveSession` that lost them would have satisfied that
    /// test while quietly leaving the Live room with nothing to draw, and the obvious fix
    /// at that point is to widen the wrong record.
    /// </summary>
    [Fact]
    public void TheLiveSessionRecordCarriesTheCombatNumbersHomesRefuses()
    {
        var names = typeof(LiveSession).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToList();

        Assert.Contains("Kills", names);
        Assert.Contains("Deaths", names);
        Assert.Contains("Dps", names);
        // And the two SessionRow does not carry, which are absent on purpose: a field that
        // answers honestly for a running session and zero for a finished one is worse than
        // one that is not there. Those are per-tab facts, read off the snapshot by the tab.
        Assert.DoesNotContain(names, n => n.Contains("Damage", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Healing", StringComparison.Ordinal));
    }

    private static readonly DateTime Start = new(2026, 9, 5, 20, 0, 0);

    private static SessionRow Row(DateTime start, string endReason = "Ended", int kills = 7) =>
        new(1, "test", "Testchar", start, start.AddMinutes(40), 2400, 1800, endReason,
            "Lower Guk", kills, 12.5, 4321, 6, 1, 88.5, "", "");

    private static StatsSnapshot Live(DateTime start) =>
        new() { SessionStart = start, CurrentZone = "Lower Guk", YourKillCount = 19,
                SessionDps = 118.4, Elapsed = TimeSpan.FromMinutes(25) };

    /// <summary>
    /// **The merge is ONE answer, and this is the assertion that says so.** A sitting that
    /// is running exists in both sources at once, so read naively the newest stored row IS
    /// the live session. Home and Live must agree about which one they are describing; the
    /// fields they then report from it are their own business.
    /// </summary>
    [Theory]
    // Running: the checkpointed row is the live one, and neither record reports it twice.
    [InlineData("Active", RecentSessionState.InProgress)]
    // Finished under a real end reason, but the snapshot has not rolled — the start-time
    // match is what covers that window.
    [InlineData("Ended", RecentSessionState.InProgress)]
    public void HomeAndLiveNeverDisagreeAboutWhichSessionThisIs(
        string endReason, RecentSessionState expected)
    {
        var stored = new List<SessionRow> { Row(Start, endReason) };
        var live = Live(Start);

        Assert.Equal(expected, SessionSummary.Of(Identity, stored, live).State);
        Assert.Equal(expected, SessionSummary.LiveOf(Identity, stored, live).State);
        Assert.Equal(expected, SessionSummary.Pick(stored, live).State);
    }

    private static readonly (string Server, string Character) Identity = ("test", "Testchar");

    /// <summary>An earlier, genuinely different session is Ended for both — and Live reads
    /// its numbers from the STORED ROW rather than from the snapshot sitting beside it.
    /// Reading the live one here would report the sitting the player is not in, under a
    /// heading saying they had finished it.</summary>
    [Fact]
    public void AFinishedSessionsNumbersComeFromTheStoredRowAndNotTheSnapshot()
    {
        var stored = new List<SessionRow> { Row(Start.AddDays(-1), "Ended", kills: 7) };

        var session = SessionSummary.LiveOf(Identity, stored, live: null);

        Assert.Equal(RecentSessionState.Ended, session.State);
        Assert.Equal(7, session.Kills);
        Assert.Equal(88.5, session.Dps);
        Assert.Equal(RecentSessionState.Ended, SessionSummary.Of(Identity, stored, null).State);
    }

    /// <summary>A running session's numbers come from the snapshot, which is the only
    /// source that has them yet.</summary>
    [Fact]
    public void ARunningSessionsNumbersComeFromTheSnapshot()
    {
        var session = SessionSummary.LiveOf(Identity, [Row(Start, "Active")], Live(Start));

        Assert.Equal(RecentSessionState.InProgress, session.State);
        Assert.Equal(19, session.Kills);
        Assert.Equal(118.4, session.Dps);
        Assert.Equal("Lower Guk", session.Zone);
    }

    /// <summary>The pick names ONE source, so a caller cannot read the wrong one for the
    /// state it was handed. This is the property that makes the two builders above safe to
    /// write without repeating the merge.</summary>
    [Fact]
    public void ThePickNamesExactlyOneSource()
    {
        var running = SessionSummary.Pick([Row(Start, "Active")], Live(Start));
        Assert.NotNull(running.Live);
        Assert.Null(running.Row);

        var ended = SessionSummary.Pick([Row(Start.AddDays(-1))], live: null);
        Assert.Null(ended.Live);
        Assert.NotNull(ended.Row);

        var never = SessionSummary.Pick(null, null);
        Assert.Equal(RecentSessionState.NeverPlayed, never.State);
        Assert.Null(never.Live);
        Assert.Null(never.Row);
    }

    // ---- 3. Live's words are Live's -------------------------------------------

    /// <summary>
    /// **Live does not borrow Home's refusal sentence, and this is the row that says so.**
    /// Home's in-progress detail is *"EQBuddy will record it here when the session ends"* —
    /// a statement that the meters are somebody else's job. Live is that somebody. Reusing
    /// it here would put a refusal on the one room whose entire point is not refusing, and
    /// it is the single easiest mistake to make with a shared module sitting right there.
    /// </summary>
    [Fact]
    public void LiveNeverBorrowsHomesInProgressRefusal()
    {
        var live = SessionSummary.LiveOf(Identity, [Row(Start, "Active")], Live(Start));
        var home = SessionSummary.Of(Identity, [Row(Start, "Active")], Live(Start));

        Assert.NotEqual(SessionSummary.Headline(home), LivePresentation.Headline(live));
        Assert.NotEqual(SessionSummary.Detail(home), LivePresentation.Detail(live));
        Assert.DoesNotContain("will record it here", LivePresentation.Detail(live));
        // And it says the thing Home refuses to: the numbers.
        Assert.Contains("19 kills", LivePresentation.Detail(live));
        Assert.Contains("118.4 dps", LivePresentation.Detail(live));
        Assert.Contains("Lower Guk", LivePresentation.Headline(live));
    }

    /// <summary>Deaths appear only when there are some — a "0 deaths" on a clean session is
    /// a boast the surface does not need to make, and it is the one number whose absence is
    /// the good news.</summary>
    [Fact]
    public void ACleanSessionDoesNotBoastAboutItsZeroDeaths()
    {
        var clean = SessionSummary.LiveOf(Identity, [Row(Start, "Active")], Live(Start));
        Assert.DoesNotContain("death", LivePresentation.Detail(clean));

        var died = clean with { Deaths = 2 };
        Assert.Contains("2 deaths", LivePresentation.Detail(died));
    }

    /// <summary>Every state has words, including the one a brand-new profile meets. A room
    /// that drew nothing at all would be the same defect as a blank card.</summary>
    [Theory]
    [InlineData(RecentSessionState.NeverPlayed)]
    [InlineData(RecentSessionState.InProgress)]
    [InlineData(RecentSessionState.Ended)]
    public void EveryStateHasAHeadlineAndADetail(RecentSessionState state)
    {
        var session = new LiveSession(state, "Testchar", "test", "Lower Guk",
            Start, Start.AddMinutes(40), TimeSpan.FromMinutes(40), 7, 0, 88.5);

        Assert.NotEmpty(LivePresentation.Headline(session));
        Assert.NotEmpty(LivePresentation.Detail(session));
    }

    /// <summary>The room-level empty is the narrow one: a character IS known (Home's
    /// whole-room empty already covers the other case one level up) and nothing has
    /// happened in the sitting yet. A single kill, a single heal or a single raid clear all
    /// take it down.</summary>
    [Fact]
    public void TheRoomIsEmptyOnlyWhileNothingAtAllHasHappened()
    {
        var fresh = new LiveSession(RecentSessionState.InProgress, "Testchar", "test",
            "Lower Guk", Start, null, TimeSpan.Zero, 0, 0, 0);

        Assert.True(LivePresentation.RoomIsEmpty(fresh, new StatsSnapshot(), raidsDefeated: 0));
        Assert.False(LivePresentation.RoomIsEmpty(fresh with { Kills = 1 },
            new StatsSnapshot(), raidsDefeated: 0));
        Assert.False(LivePresentation.RoomIsEmpty(fresh, new StatsSnapshot(), raidsDefeated: 1));
        Assert.False(LivePresentation.RoomIsEmpty(fresh,
            new StatsSnapshot { HealingDone = 40 }, raidsDefeated: 0));
        Assert.False(LivePresentation.RoomIsEmpty(fresh,
            new StatsSnapshot { DamageDealt = 12 }, raidsDefeated: 0));
        // A FINISHED session is never the room-level empty: there is a report to draw, and
        // "nothing has happened this session yet" would be untrue of a session that is over.
        Assert.False(LivePresentation.RoomIsEmpty(fresh with { State = RecentSessionState.Ended },
            new StatsSnapshot(), raidsDefeated: 0));
    }

    // ---- 4. one producer for the three meters ----------------------------------

    /// <summary>A session with a pull in it, whose SESSION and FIGHT totals differ on every
    /// meter — which is what makes the scope assertions below able to fail. Fixtures whose
    /// two scopes happened to agree would pass whichever one the code picked (trap 23's
    /// lesson, in a unit test rather than a screenshot).
    ///
    /// Note the argument order: <c>SourceDamage</c> is <c>(Name, Hits, Total)</c>, and
    /// getting it backwards produces a perfectly valid snapshot describing 900 swings for
    /// 12 damage.</summary>
    private static StatsSnapshot Fighting(
        string fightName = "a froglok knight",
        List<SourceDamage>? damage = null,
        string pet = "", DateTime? charmedSince = null) => new()
    {
        CombatSeconds = 120,
        PetName = pet,
        CharmedSince = charmedSince,
        DamageBySource = damage ?? [new SourceDamage("Slash", 12, 900, 2)],
        HealsBySpell = [new SourceDamage("Light Healing", 8, 400)],
        PetAbilities = [new SourceDamage("Bite", 10, 300)],
        LastFight = new LastFightInfo(
            fightName, 30, 300, 40, 100, 10, 3.3, "Killed", false,
            ByAbility: [new SourceDamage("Slash", 4, 300, 1)],
            HealsBySpell: [new SourceDamage("Light Healing", 2, 100)],
            ByIncoming: [])
        {
            Start = Start,
            PetAbilities = [new SourceDamage("Bite", 3, 90)],
        },
    };

    /// <summary>Session scope means the session's rows over combat time; Fight scope means
    /// the pull's own rows over its own duration. The rows and the seconds travel together
    /// because they are the numerator and the denominator of the rate the surface prints —
    /// a caller that picked one without the other would print a fight's damage over the
    /// session's combat time.</summary>
    [Theory]
    [InlineData(BreakoutPresentation.Damage, false, 900, 120)]
    [InlineData(BreakoutPresentation.Damage, true, 300, 30)]
    [InlineData(BreakoutPresentation.Healing, false, 400, 120)]
    [InlineData(BreakoutPresentation.Healing, true, 100, 30)]
    [InlineData(BreakoutPresentation.Pet, false, 300, 120)]
    [InlineData(BreakoutPresentation.Pet, true, 90, 30)]
    public void EachMeterTakesItsOwnRowsOverItsOwnSeconds(
        string kind, bool fightScope, long total, double seconds)
    {
        var meter = LivePresentation.Meter(kind, Fighting(), fightScope, Start);

        Assert.Equal(total, meter.Rows.Sum(r => r.Total));
        Assert.Equal(seconds, meter.Seconds);
        Assert.Null(meter.Empty);
        Assert.NotEmpty(meter.Title);
        Assert.NotEmpty(meter.Subtext);
    }

    [Theory]
    [InlineData(BreakoutPresentation.Damage, "dps")]
    [InlineData(BreakoutPresentation.Healing, "hps")]
    [InlineData(BreakoutPresentation.Pet, "dps")]
    public void HealingIsTheOnlyMeterThatCountsPerSecondInHps(string kind, string rate) =>
        Assert.Equal(rate, LivePresentation.Meter(kind, Fighting(), false, Start).RateLabel);

    /// <summary>An empty meter says which KIND of empty it is, and the two healing cases are
    /// the ones that are not "nothing happened": a regen tick is real healing the game logs
    /// no amount for, and "No healing seen yet" over a bard's song is the app calling itself
    /// broken (David, live test 2026-08-06).</summary>
    [Fact]
    public void AnEmptyHealingMeterDistinguishesRegenFromNothing()
    {
        Assert.Equal("No healing seen yet.",
            LivePresentation.EmptyMeter(BreakoutPresentation.Healing, new StatsSnapshot()));
        Assert.Contains("hymn/regen ticks", LivePresentation.EmptyMeter(
            BreakoutPresentation.Healing, new StatsSnapshot { RegenTicks = 9 }));
        Assert.Contains("est. ~", LivePresentation.EmptyMeter(
            BreakoutPresentation.Healing,
            new StatsSnapshot { RegenTicks = 9, RegenEstimatedHealed = 450, RegenSpell = "Hymn" }));
        Assert.Equal("No pet damage seen yet.",
            LivePresentation.EmptyMeter(BreakoutPresentation.Pet, new StatsSnapshot()));
        Assert.Equal("No damage seen yet.",
            LivePresentation.EmptyMeter(BreakoutPresentation.Damage, new StatsSnapshot()));
    }

    [Fact]
    public void AMeterWithNoRowsCarriesItsEmptyLineRatherThanAnEmptySurface()
    {
        var meter = LivePresentation.Meter(
            BreakoutPresentation.Damage, new StatsSnapshot(), fightScope: false, Start);

        Assert.Empty(meter.Rows);
        Assert.Equal("No damage seen yet.", meter.Empty);
    }

    // ---- 5. nothing in a repaint gate moves on the clock ----------------------

    /// <summary>
    /// **The repaint gate's key carries no clock, and this is the row that proves it.**
    /// Trap 8's rule: a fingerprint with a countdown or an age in it wakes on every tick,
    /// which is the same defect as having no gate at all. The temptation here is real and
    /// specific — the Pet meter's TITLE carries a charm hold that counts up, so folding the
    /// title or the subtext into the key would have looked like the tidy thing to do.
    /// </summary>
    [Fact]
    public void TheRepaintKeyDoesNotMoveWhenOnlyTheClockDoes()
    {
        var s = Fighting(pet: "Gharuk", charmedSince: Start.AddMinutes(-3));

        var first = LivePresentation.Meter(BreakoutPresentation.Pet, s, false, Start);
        var later = LivePresentation.Meter(BreakoutPresentation.Pet, s, false, Start.AddMinutes(7));

        // The title DID move — that is what makes this test worth having.
        Assert.NotEqual(first.Title, later.Title);
        Assert.Equal(
            LivePresentation.MeterSignature(BreakoutPresentation.Pet, false, "Total", first),
            LivePresentation.MeterSignature(BreakoutPresentation.Pet, false, "Total", later));
    }

    /// <summary>And it moves when anything a reader would notice does: the scope, the sort,
    /// a row's total, or a different pull. The negative that stops the row above being
    /// satisfied by a constant (trap 39).</summary>
    [Fact]
    public void TheRepaintKeyMovesWhenTheRowsOrTheScopeOrTheSortDo()
    {
        var s = Fighting();
        var meter = LivePresentation.Meter(BreakoutPresentation.Damage, s, false, Start);
        var baseline = LivePresentation.MeterSignature(BreakoutPresentation.Damage, false, "Total", meter);

        Assert.NotEqual(baseline,
            LivePresentation.MeterSignature(BreakoutPresentation.Damage, false, "Hits", meter));
        Assert.NotEqual(baseline, LivePresentation.MeterSignature(
            BreakoutPresentation.Damage, true, "Total",
            LivePresentation.Meter(BreakoutPresentation.Damage, s, true, Start)));

        var hotter = Fighting(damage: [new SourceDamage("Slash", 15, 1200, 3)]);
        Assert.NotEqual(baseline, LivePresentation.MeterSignature(
            BreakoutPresentation.Damage, false, "Total",
            LivePresentation.Meter(BreakoutPresentation.Damage, hotter, false, Start)));
    }

    /// <summary>A NEW pull with the same totals and the same length is still a different
    /// fight. The fight's name is in the key for exactly this case, which is why the meter
    /// carries it separately from the subtext that also names it.</summary>
    [Fact]
    public void TwoDifferentPullsWithIdenticalNumbersStillRepaint()
    {
        var first = LivePresentation.Meter(BreakoutPresentation.Damage, Fighting(), true, Start);
        var second = LivePresentation.Meter(BreakoutPresentation.Damage,
            Fighting("a froglok shaman"), true, Start);

        Assert.NotEqual(
            LivePresentation.MeterSignature(BreakoutPresentation.Damage, true, "Total", first),
            LivePresentation.MeterSignature(BreakoutPresentation.Damage, true, "Total", second));
    }

    /// <summary>The tab badges carry counts and rates, never a countdown — the same rule,
    /// on the strip. The timeline's badge is the fight's NAME rather than its length for
    /// this reason: a duration ticks while the pull is live, and a chip whose label changes
    /// width every second is trap 12 wearing a chip.</summary>
    [Fact]
    public void NoTabBadgeCountsDown()
    {
        var s = Fighting();
        var first = LivePresentation.Tabs(s, 3, 2, 21);
        var later = LivePresentation.Tabs(s, 3, 2, 21);

        Assert.Equal(first.Select(t => t.Value), later.Select(t => t.Value));
        Assert.Equal("a froglok knight", first.Single(t => t.Tab == LiveTab.Timeline).Value);
    }

    // ---- and one thing this room deliberately does NOT do ---------------------

    /// <summary>
    /// **Live folds nothing, so it names no absorbed card key** — and that absence is a
    /// decision worth an assertion rather than a comment.
    ///
    /// Every other surface in this family describes a fold that already happened on the
    /// widget, with an `AbsorbedCardKeys` list naming the cards that disappeared. Live's
    /// five sources all still ship, unchanged, in the same release that builds this room:
    /// subtracting one is gated per item on a HUD chip and a screenshot. A fold list here
    /// would be trap 55 with nothing on the other side of it — a migration naming keys that
    /// are still live cards, re-running on every launch.
    /// </summary>
    [Fact]
    public void LiveAbsorbsNoCardBecauseNothingHasBeenSubtractedFromTheWidget()
    {
        var absorbing = typeof(LiveSurface).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name)
            .Concat(typeof(LiveSurface).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Select(p => p.Name))
            .Where(n => n.Contains("Absorb", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("CardKey", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(absorbing);
        // And the five sources' own card keys are still in the widget's catalog, which is
        // the fact the sentence above actually depends on.
        foreach (var key in new[] { "combat", "healing", "kills" })
            Assert.Contains(key, OverlaySections.Catalog.Select(c => c.Key));
    }
}
