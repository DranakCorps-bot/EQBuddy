using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Evolved shell's HOME room, as far as anything without a window can see it: the
/// session-summary fact Live shares, the three blocks' state rules, and the two boundaries
/// Bevel's Helm-signed pre-design (2026-09-05 ~5:20 AM CT) drew around it.
///
/// **Three since DRA-63** (Founder smoke 2026-09-11): the "Go to" block was cut, and the ⧉
/// catch-up copy became unconditional. §4 below carries both — what replaced the deep-link
/// assertions is in it, rather than the assertions simply leaving with the block.
///
/// The room's own wiring is asserted from a launched app (`ShellHostTests`), because the
/// WPF layer has no unit tests and a control that is absent photographs as an unremarkable
/// panel (trap 29). What is here is everything that is arithmetic or words — which is where
/// this codebase puts a window's logic on purpose.
/// </summary>
public class HomeRoomTests
{
    private static readonly (string Server, string Character) Me = ("test", "Testchar");

    private static SessionRow Row(string endReason, DateTime start, string zone = "Guk",
        double elapsed = 3600, double xp = 2.5, long copper = 1234, int loot = 4) =>
        new(1, Me.Server, Me.Character, start, start.AddSeconds(elapsed), elapsed, elapsed,
            endReason, zone, Kills: 12, xp, copper, loot, Deaths: 1, Dps: 40, "", "");

    private static StatsSnapshot Live(DateTime start, string zone = "Innothule Swamp") =>
        new() { SessionStart = start, CurrentZone = zone, YourKillCount = 6,
                Elapsed = TimeSpan.FromMinutes(20) };

    // ---- 1. the session-summary fact -------------------------------------------

    [Fact]
    public void NothingStoredAndNothingLiveIsNeverPlayed()
    {
        var session = SessionSummary.Of(Me, [], null);
        Assert.Equal(RecentSessionState.NeverPlayed, session.State);
        Assert.Equal("No sessions yet", SessionSummary.Headline(session));
        Assert.NotEmpty(SessionSummary.Detail(session));
    }

    [Fact]
    public void ARunningSessionIsReportedAsInProgressAndNotAsWhereYouLeftOff()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        var session = SessionSummary.Of(Me, [Row("Ended", now.AddDays(-1))], Live(now));
        Assert.Equal(RecentSessionState.InProgress, session.State);
        Assert.Equal("Session in progress", SessionSummary.Headline(session));
        // The zone it names is the one you are IN, not yesterday's.
        Assert.Equal("Innothule Swamp", session.Zone);
    }

    /// <summary>
    /// **THE MERGE, and it is the row with teeth.** A session that is being played is in
    /// BOTH sources — `SessionArchiver` checkpoints it into the store under
    /// `ActiveEndReason` on the tick, and the live snapshot carries it too. Read naively,
    /// the newest stored row IS the live session, and Home would describe the sitting the
    /// player is in the middle of as "where you left off" — Live's job, in the past tense,
    /// on the one surface signed not to do it.
    ///
    /// Both halves are asserted: the live row is skipped, AND the real previous session is
    /// the one that comes back. Skipping everything would also pass the first half, which
    /// is exactly the vacuous shape trap 39 is about.
    /// </summary>
    [Fact]
    public void TheLiveSessionsOwnCheckpointIsNotMistakenForThePreviousOne()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        var yesterday = now.AddDays(-1);
        SessionRow[] stored =
        [
            Row(SessionRepository.ActiveEndReason, now, zone: "Innothule Swamp"),
            Row("Ended", yesterday, zone: "Befallen"),
        ];

        // While it is running: in progress, and nothing from the store is offered as "last".
        Assert.Equal(RecentSessionState.InProgress, SessionSummary.Of(Me, stored, Live(now)).State);

        // The moment the live snapshot rolls, the checkpoint row is still sitting there
        // marked Active — and the answer must be YESTERDAY's session, not that one.
        var after = SessionSummary.Of(Me, stored, null);
        Assert.Equal(RecentSessionState.Ended, after.State);
        Assert.Equal("Befallen", after.Zone);
    }

    /// <summary>The other half of the same guard: a row whose end reason has already been
    /// rewritten (a finalize under a real reason, or a crash recovered at startup) is still
    /// the live session while the snapshot says so — matched on the start time, because the
    /// end reason has stopped being the tell.</summary>
    [Fact]
    public void AFinalisedRowIsStillTheLiveSessionWhileTheSnapshotSaysSo()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        var after = SessionSummary.Of(Me,
            [Row(SessionRepository.RecoveredEndReason, now, zone: "Innothule Swamp"),
             Row("Ended", now.AddDays(-1), zone: "Befallen")],
            new StatsSnapshot { SessionStart = now });   // not meaningful: no kills, no xp
        Assert.Equal("Befallen", after.Zone);
    }

    [Fact]
    public void TheNewestFinishedSessionIsTheOneReported()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        var session = SessionSummary.Of(Me,
            [Row("Ended", now.AddDays(-3), zone: "Oasis"),
             Row("Ended", now.AddDays(-1), zone: "Befallen"),
             Row("Ended", now.AddDays(-9), zone: "Guk")],
            null);
        Assert.Equal("Befallen", session.Zone);
        Assert.Equal("Last session — Befallen", SessionSummary.Headline(session));
    }

    /// <summary>`SessionRepository.Query` treats a blank side as "do not filter on it", so
    /// a blank identity passed through would answer with EVERY character's sessions — a
    /// "where you left off" about somebody else. Same rule, same reason, as
    /// <c>LevelHistory.Stored</c>.</summary>
    [Theory]
    [InlineData("", "Testchar")]
    [InlineData("test", "")]
    [InlineData("", "")]
    public void ABlankIdentityNeverQueriesTheStore(string server, string character)
    {
        var asked = false;
        var rows = SessionSummary.Stored((server, character),
            (_, _) => { asked = true; return []; });
        Assert.False(asked, "a blank identity must not reach the query at all");
        Assert.Empty(rows);
    }

    [Fact]
    public void ARealIdentityIsPassedThroughVerbatim()
    {
        var seen = ("", "");
        SessionSummary.Stored(Me, (server, character) => { seen = (server, character); return []; });
        Assert.Equal((Me.Server, Me.Character), seen);
    }

    // ---- 2. the Home/Live boundary, as a property of the TYPE -------------------

    /// <summary>
    /// **THE HOME/LIVE BOUNDARY, ASSERTED AGAINST THE TYPE RATHER THAN AGAINST A HABIT.**
    ///
    /// Helm's Home ruling is "no combat numbers on Home", and a room cannot render what its
    /// record does not carry — so the guard is that <see cref="RecentSession"/> has no field
    /// to reach for. That is the difference between a rule and a wish: the temptation is one
    /// property access away (`MainWindow.CurrentSnapshot()` has all of them, in the room's
    /// own file), and a reviewer noticing is not a mechanism.
    ///
    /// **With a positive half**, per trap 39: a record with NO properties at all would pass
    /// the negative forever and read as coverage.
    /// </summary>
    [Fact]
    public void TheRecentSessionRecordCarriesNoCombatNumbersToRender()
    {
        var names = typeof(RecentSession).GetProperties().Select(p => p.Name).ToList();
        foreach (var forbidden in new[] { "Dps", "Kills", "Deaths", "Damage", "Healing" })
            Assert.DoesNotContain(names, n => n.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        // And it does carry what a desk surface is FOR.
        Assert.Contains("Zone", names);
        Assert.Contains("Elapsed", names);
        Assert.Contains("EndedLocal", names);
    }

    /// <summary>The in-progress line is a sentence and not a row of numbers — the one state
    /// where the meters exist and are moving, and the one Home is signed not to draw.</summary>
    [Fact]
    public void TheInProgressLineShowsNoFigures()
    {
        var detail = SessionSummary.Detail(
            SessionSummary.Of(Me, [], Live(new DateTime(2026, 9, 5, 20, 0, 0))));
        Assert.DoesNotContain(detail, char.IsDigit);
    }

    /// <summary>A finished session's facts, in the order a desk surface reads them — and
    /// nothing that is zero, because "0 items" is a measurement of nothing.</summary>
    [Fact]
    public void AFinishedSessionsFactsSkipTheZeroes()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        var rich = SessionSummary.Of(Me, [Row("Ended", now.AddHours(-2))], null);
        var facts = SessionSummary.Facts(rich).ToList();
        Assert.Contains(facts, f => f.Contains("2.5% xp"));
        Assert.Contains(facts, f => f.Contains("4 items"));

        var bare = SessionSummary.Of(Me,
            [Row("Ended", now.AddHours(-2), xp: 0, copper: 0, loot: 0)], null);
        var bareFacts = SessionSummary.Facts(bare).ToList();
        Assert.DoesNotContain(bareFacts, f => f.Contains("xp"));
        Assert.DoesNotContain(bareFacts, f => f.Contains("item"));
        // The two that always hold: when it ended, and how long it ran.
        Assert.Equal(2, bareFacts.Count);
    }

    /// <summary>Nothing on this surface says "x ago". An age ticks, which changes measured
    /// text width on a SizeToContent window (trap 12) and would re-wake every phone on the
    /// fingerprint (trap 8) — and it would make Home's own repaint gate fire every second,
    /// which is the same defect as having no gate.</summary>
    [Fact]
    public void NothingHereIsAnAge()
    {
        var now = new DateTime(2026, 9, 5, 20, 0, 0);
        foreach (var text in new[]
                 {
                     SessionSummary.Detail(SessionSummary.Of(Me, [Row("Ended", now)], null)),
                     SessionSummary.Detail(SessionSummary.Of(Me, [], null)),
                     HomeReadout.ReadinessAnswer(new ReadinessRow(
                         OutputfileKind.Inventory, "Bags", "", ReadinessState.Scanned, now, "")),
                 })
            Assert.DoesNotContain("ago", text, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 3. identity and readiness ---------------------------------------------

    [Fact]
    public void NoCharacterIsTheOneEmptyStateWithNoGameDataAtAll()
    {
        Assert.Equal(IdentityState.NoCharacter, HomeReadout.Identity(("", "")));
        Assert.Equal("No character yet", HomeReadout.IdentityHeadline(("", "")));
        var empty = HomeReadout.IdentityDetail(("", ""), "");
        // The inventory-dump voice: what is missing, the action, where, what happens next.
        Assert.Contains("/log on", empty);
        Assert.Contains("Options", empty);
        // And nothing to be ready ABOUT until there is a character.
        Assert.Empty(HomeReadout.Readiness(("", ""), _ => DateTime.Now));
    }

    [Fact]
    public void AFollowedCharacterIsNamedWithItsServerAndZone()
    {
        Assert.Equal(IdentityState.Following, HomeReadout.Identity(Me));
        Assert.Equal("Testchar", HomeReadout.IdentityHeadline(Me));
        Assert.Equal("test · Guk", HomeReadout.IdentityDetail(Me, "Guk"));
        // A zone EQBuddy has not seen yet drops out rather than leaving a dangling separator.
        Assert.Equal("test", HomeReadout.IdentityDetail(Me, ""));
    }

    /// <summary>
    /// **Never-scanned and healthy are two states with one shape, and collapsing them is
    /// the failure Bevel named**: silence tells a player who has never run the command that
    /// everything is fine, and a nag tells a player who ran it this morning that it is not.
    /// </summary>
    [Fact]
    public void NeverScannedAndScannedAreDifferentAnswers()
    {
        var when = new DateTime(2026, 9, 4, 20, 14, 0);
        var none = HomeReadout.Readiness(Me, _ => null);
        Assert.All(none, r => Assert.Equal(ReadinessState.NeverScanned, r.State));
        Assert.All(none, r => Assert.Equal("Not run yet", HomeReadout.ReadinessAnswer(r)));

        var all = HomeReadout.Readiness(Me, _ => when);
        Assert.All(all, r => Assert.Equal(ReadinessState.Scanned, r.State));
        Assert.All(all, r => Assert.Equal("Sep 4, 8:14 PM", HomeReadout.ReadinessAnswer(r)));
    }

    /// <summary>There is no third "stale" state, and that is a decision. Nobody signed an
    /// age past which a dump is wrong, and a threshold invented here would arrive as a nag
    /// the player never agreed to.</summary>
    [Fact]
    public void AVeryOldDumpIsStillJustScanned()
    {
        var rows = HomeReadout.Readiness(Me, _ => new DateTime(2019, 1, 1));
        Assert.All(rows, r => Assert.Equal(ReadinessState.Scanned, r.State));
        Assert.Equal(2, Enum.GetValues<ReadinessState>().Length);
    }

    [Fact]
    public void TheHeadlineCountsOnlyWhatHasNotBeenRun()
    {
        Assert.Equal("Readiness", HomeReadout.ReadinessHeadline(
            HomeReadout.Readiness(Me, _ => DateTime.Now)));
        Assert.Equal("Readiness — 4 not run yet", HomeReadout.ReadinessHeadline(
            HomeReadout.Readiness(Me, _ => null)));
        Assert.Equal("Readiness — 1 not run yet", HomeReadout.ReadinessHeadline(
            HomeReadout.Readiness(Me,
                kind => kind == OutputfileKind.Factions ? null : DateTime.Now)));
    }

    /// <summary>Each row asks about a real dump kind, once, and says what the player loses
    /// without it — never the filename, which is EQBuddy's problem.</summary>
    [Fact]
    public void EveryReadinessRowNamesADumpAndWhatItFeeds()
    {
        var rows = HomeReadout.Readiness(Me, _ => null);
        Assert.Equal(4, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.Kind).Distinct().Count());
        Assert.DoesNotContain(rows, r => r.Kind == OutputfileKind.Unknown);
        Assert.All(rows, r =>
        {
            Assert.NotEmpty(r.Name);
            Assert.NotEmpty(r.Feeds);
            Assert.DoesNotContain(".txt", r.Feeds);
        });
    }

    /// <summary>
    /// **The fourth row is the OPTIONAL one, and it says so in the player's words.** The
    /// other three name something EQBuddy cannot work out any other way; the spellbook only
    /// sharpens buff countdowns that already run off landing and fade lines. A row that
    /// asked for it in the same voice as the other three would be telling a player
    /// something is missing when nothing is — which is the nag Bevel's two-states rule
    /// exists to refuse, arriving through the wording instead of through the state.
    /// </summary>
    [Fact]
    public void TheSpellbookRowSaysItIsOptionalAndThatCountdownsWorkWithoutIt()
    {
        var row = Assert.Single(HomeReadout.Readiness(Me, _ => null),
            r => r.Kind == OutputfileKind.Spellbook);
        Assert.Contains("optional", row.Feeds, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without it", row.Feeds, StringComparison.OrdinalIgnoreCase);
        // It is last: the three that name a real gap are read first.
        Assert.Equal(OutputfileKind.Spellbook, HomeReadout.Readiness(Me, _ => null)[^1].Kind);
    }

    /// <summary>
    /// **A row whose surface is not a ROOM offers no "Open", and that is the rail's own
    /// rule one level in.** Buff countdowns are drawn on the widget's HUD; there is no
    /// `page:room` that shows them, so pointing the link at some room that does not would
    /// be the dead affordance `ShellPages.Landed` exists to refuse. Asserted on a SCANNED
    /// row, because that is the only state where the link would have been drawn at all.
    /// </summary>
    [Fact]
    public void TheSpellbookRowOffersNoOpenBecauseItsSurfaceIsNotARoom()
    {
        var row = Assert.Single(HomeReadout.Readiness(Me, _ => DateTime.Now),
            r => r.Kind == OutputfileKind.Spellbook);
        Assert.Equal(ReadinessState.Scanned, row.State);
        Assert.Equal("", row.Address);
        // And the other three still have theirs — an empty address must not become the
        // way every row answers.
        Assert.All(HomeReadout.Readiness(Me, _ => DateTime.Now)
                .Where(r => r.Kind != OutputfileKind.Spellbook),
            r => Assert.NotEqual("", r.Address));
    }

    // ---- 4. the catch-up copy, and the affordance that must not open nothing ----

    /// <summary>
    /// **DRA-63 ask 1: the ⧉ catch-up is offered in BOTH states** (Founder smoke
    /// 2026-09-11 — *"always show copy/paste catch-up for Bags, Achievements, Factions,
    /// Spellbooks from Home"*). The button is WPF and only a launched app can say it exists
    /// (trap 29, `shellHomeCopyCmd`); what is assertable here is the sentence on it, which
    /// is the half that has to stay different.
    ///
    /// **Both halves, because either alone goes vacuous.** One tooltip for both states
    /// would satisfy "every row has words" while collapsing never-run and healthy into one
    /// voice — the exact thing Bevel's two-states rule forbids, arriving through the
    /// wording instead of through the state (the way the spellbook row nearly did). And a
    /// scanned row whose sentence never mentions running it again is a button with no
    /// answer to "why would I".
    /// </summary>
    [Fact]
    public void TheCatchUpCopyIsOfferedInBothStatesAndSaysADifferentThingInEach()
    {
        var never = Assert.Single(HomeReadout.Readiness(Me, _ => null),
            r => r.Kind == OutputfileKind.Inventory);
        var scanned = Assert.Single(HomeReadout.Readiness(Me, _ => DateTime.Now),
            r => r.Kind == OutputfileKind.Inventory);

        Assert.Equal(HomeReadout.CatchUpFirstRun, HomeReadout.CatchUpTooltip(never));
        Assert.Equal(HomeReadout.CatchUpAgain, HomeReadout.CatchUpTooltip(scanned));
        Assert.NotEqual(HomeReadout.CatchUpFirstRun, HomeReadout.CatchUpAgain);
        // The scanned one answers "why would I press this": it names running it AGAIN.
        Assert.Contains("again", HomeReadout.CatchUpAgain, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("again", HomeReadout.CatchUpFirstRun, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every row gets one, including the optional spellbook — the ask names all
    /// four by name. A per-kind exception would put the row a player is least likely to
    /// have run back where DRA-63 found it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryReadinessRowHasACatchUpSentenceWhateverItsState(bool scanned)
    {
        var rows = HomeReadout.Readiness(Me, _ => scanned ? DateTime.Now : null);
        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.NotEmpty(HomeReadout.CatchUpTooltip(r)));
    }

    /// <summary>
    /// **The "Go to" block is gone and does not come back** (DRA-63 ask 2 — *"sidebar
    /// already owns those doors"*). Asserted against the TYPE rather than against a habit:
    /// a room cannot draw a link list whose builder does not exist, which is the same shape
    /// as `RecentSession` having no combat field to reach for.
    ///
    /// **With a positive half**, per trap 39: a reflection that found no members at all
    /// would pass this forever and read as coverage.
    /// </summary>
    [Fact]
    public void HomeHasNoRoomLinkBlockToRebuild()
    {
        var members = typeof(HomeReadout).GetMembers().Select(m => m.Name).ToList();
        Assert.DoesNotContain("Links", members);
        Assert.DoesNotContain("EmptyLinks", members);
        Assert.Null(typeof(ShellPages).Assembly.GetType("EQBuddy.UI.Shared.HomeLink"));
        // The three blocks that DID stay, so the sweep above cannot be vacuous.
        foreach (var kept in new[] { "IdentityHeadline", "Readiness", "CatchUpTooltip" })
            Assert.Contains(kept, members);
    }

    /// <summary>A readiness row's "Open" is the one navigation left in Home's body, and it
    /// is filtered the way the block that used to sit under it was: it points at a landed
    /// room or it is not offered.</summary>
    [Fact]
    public void AReadinessRowsAddressIsAlsoFilteredThroughLanded()
    {
        foreach (var row in HomeReadout.Readiness(Me, _ => DateTime.Now))
        {
            if (row.Address.Length == 0) continue;
            var parsed = ShellPages.ParseAddress(row.Address);
            Assert.NotNull(parsed);
            Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
            // And the room half is one the surface itself knows — never a shell invention.
            Assert.Contains(parsed.Value.Room,
                ShellPages.Rooms(parsed.Value.Page).Select(r => r.Key));
        }
    }
}
