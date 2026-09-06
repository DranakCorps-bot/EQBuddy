using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Evolved shell's HOME room, as far as anything without a window can see it: the
/// session-summary fact Live will share, the four blocks' state rules, and the two
/// boundaries Bevel's Helm-signed pre-design (2026-09-05 ~5:20 AM CT) drew around it.
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
        Assert.Equal("Readiness — 3 not run yet", HomeReadout.ReadinessHeadline(
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
        Assert.Equal(3, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.Kind).Distinct().Count());
        Assert.DoesNotContain(rows, r => r.Kind == OutputfileKind.Unknown);
        Assert.All(rows, r =>
        {
            Assert.NotEmpty(r.Name);
            Assert.NotEmpty(r.Feeds);
            Assert.DoesNotContain(".txt", r.Feeds);
        });
    }

    // ---- 4. deep links, and the affordance that must not open nothing ----------

    /// <summary>
    /// **Home's deep links read the SAME `Landed` list the rail reads**, which is what stops
    /// a "Live" row appearing in a room's body before Live exists — the rail's own forbidden
    /// shape (*"an affordance that opens nothing is a trap"*) reappearing one level in, where
    /// the rail's guard cannot see it.
    /// </summary>
    [Fact]
    public void EveryLinkOpensARoomThatExists()
    {
        var links = HomeReadout.Links();
        Assert.NotEmpty(links);
        Assert.All(links, link =>
        {
            Assert.Contains(link.Page, ShellPages.Landed);
            Assert.Equal((link.Page, (string?)null), ShellPages.ParseAddress(link.Address));
            Assert.NotEmpty(link.Label);
            Assert.NotEmpty(link.Detail);
        });
    }

    /// <summary>
    /// The negative that keeps the row above from going vacuous, **rewritten by the Live PR
    /// rather than deleted by it** — which is what the assertion it replaces asked for in as
    /// many words ("meant to be DELETED by the Live PR, in the same commit that adds Live to
    /// `Landed`").
    ///
    /// Deleting it outright would have taken the guard down with the case: Home's links are
    /// filtered by <c>ShellPages.Landed</c>, and the property worth holding is not "Live is
    /// absent" but "the list tracks what has landed, in both directions". Settings carried
    /// the negative next, and its clause said the two reasons apart in advance — *"it has not
    /// landed AND Home would not link to it if it had"*.
    ///
    /// **SR-5 landed Settings, and the SECOND reason is the whole of what is left.** The row
    /// is rewritten again rather than deleted again, and the rewrite is now the more valuable
    /// of the two halves: a filter keyed on `Landed` would have started offering Settings the
    /// moment the room existed, and it did not, because it is also filtered on
    /// <see cref="ShellPages.BelowTheGap"/> — Home is about the character and Settings
    /// configures the tool. That distinction was previously untestable, because the only room
    /// below the gap had not landed.
    /// </summary>
    [Fact]
    public void LiveIsOfferedAndSettingsIsNotEvenNowThatBothHaveLanded()
    {
        Assert.Contains(ShellPage.Live, ShellPages.Landed);
        Assert.Contains(HomeReadout.Links(), link => link.Page == ShellPage.Live);

        // Landed — and still not offered, which is the assertion that could not be made
        // until it was.
        Assert.Contains(ShellPage.Settings, ShellPages.Landed);
        Assert.DoesNotContain(HomeReadout.Links(), link => link.Page == ShellPage.Settings);
    }

    /// <summary>Home does not link to itself (a no-op wearing an affordance) and does not
    /// link to Settings, which configures the tool where Home is about the character — the
    /// same reason Settings sits below the rail's own gap.</summary>
    [Fact]
    public void HomeLinksToNeitherItselfNorSettings()
    {
        Assert.DoesNotContain(HomeReadout.Links(), l => l.Page == ShellPage.Home);
        Assert.DoesNotContain(HomeReadout.Links(), l => ShellPages.BelowTheGap(l.Page));
        // Everything else that has landed IS offered, in rail order.
        Assert.Equal(
            ShellPages.RailOrder
                .Where(p => ShellPages.Landed.Contains(p)
                    && p != ShellPage.Home && !ShellPages.BelowTheGap(p))
                .ToArray(),
            HomeReadout.Links().Select(l => l.Page).ToArray());
    }

    /// <summary>A readiness row's "Open" is the same grammar, filtered the same way: it
    /// points at a landed room or it is not offered.</summary>
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
