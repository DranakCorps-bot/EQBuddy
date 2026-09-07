using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// WHAT THE UNDER-BAR PANEL SHOWS for Watch, Loot and the buff set (OE-7).
///
/// These three arrived with the seat that gave every floating-window kind a HUD chip, and
/// they are here rather than in <c>HudExpandWindow</c> for the reason every window sum in
/// this repo is lifted: the WPF layer has no unit tests (docs/TestPlan.md §5), so a choice
/// of rows expressed only in a render method is a choice nothing can check.
///
/// <see cref="WatchPeek"/> is the one with a second consumer — the Watch FLOAT reads the
/// same builder — so the assertions here are load-bearing for two surfaces.
/// </summary>
public class HudExpandPeekTests
{
    private static TrackedRule Rule(string id, string name, bool pinned = true,
        bool enabled = true) =>
        new() { Id = id, Name = name, Pattern = name, Pinned = pinned, Enabled = enabled };

    private static TrackedRuleResult Hit(string id, string name, int qty, double perHour = 0,
        string? last = null) =>
        new(name, qty, [], perHour, perHour, null, null, last, id);

    // ---------------------------------------------------------------- watch ----

    /// <summary>Pinned rules only, biggest first, gauged against the biggest — and the
    /// unpinned rule is the assertion that matters, because "every enabled rule" is what the
    /// bar used to mean before SA-R and is what a re-derivation would drift back to.</summary>
    [Fact]
    public void WatchPeek()
    {
        var body = HudExpandPeek.Watch(
            [Rule("a", "Motes"), Rule("b", "Gems"), Rule("c", "Silk", pinned: false)],
            [Hit("a", "Motes", 3, 6), Hit("b", "Gems", 9, 18), Hit("c", "Silk", 40)]);

        Assert.Null(body.Empty);
        Assert.Equal(["Gems", "Motes"], body.Rows.Select(r => r.Name));
        Assert.Equal("9 · 18/hr", body.Rows[0].Value);
        Assert.Equal(1, body.Rows[0].Share);
        Assert.Equal(3 / 9.0, body.Rows[1].Share, 6);
        Assert.Contains("2 pinned rules", body.Subtext);
        Assert.Contains("12 total", body.Subtext);
    }

    /// <summary>A pin that has matched nothing yet still has no ROW — <c>Tracked</c> only
    /// carries rules with results — so the empty state is what a fresh session sees, and it
    /// names the one thing Options can do about it.</summary>
    [Fact]
    public void WatchPeekWithNothingPinnedSaysWhereToPin()
    {
        var body = HudExpandPeek.Watch([Rule("a", "Motes", pinned: false)], [Hit("a", "Motes", 3)]);

        Assert.NotNull(body.Empty);
        Assert.Contains("Pin a watch rule in Options", body.Empty);
        Assert.Empty(body.Rows);
        Assert.Contains("0 pinned rules", body.Subtext);
    }

    /// <summary>The last match rides the hover, which is where the float already put it.
    /// A rule that has matched nothing carries no tooltip rather than an empty one.</summary>
    [Fact]
    public void WatchPeekPutsTheLastMatchOnTheHover()
    {
        var body = HudExpandPeek.Watch([Rule("a", "Motes"), Rule("b", "Gems")],
            [Hit("a", "Motes", 3, last: "a mote of fire"), Hit("b", "Gems", 1)]);

        Assert.Equal("last: a mote of fire", body.Rows[0].Tooltip);
        Assert.Null(body.Rows[1].Tooltip);
    }

    // ----------------------------------------------------------------- loot ----

    /// <summary>A target with rows: the subtext is the float's own Target-scope line, and
    /// every row shares the flat 1.0 gauge (mixed units — observed counts and wiki rarity
    /// words are not one scale, so no proportional bar is drawn).</summary>
    [Fact]
    public void LootPeekShowsTheTargetNotTheSession()
    {
        var body = HudExpandPeek.Loot("a skeleton", " — 3 kills this session",
            [("Bone Chips", "4 this session · 40%"), ("Rusty Sword", "common")], "");

        Assert.Null(body.Empty);
        Assert.Equal(["Bone Chips", "Rusty Sword"], body.Rows.Select(r => r.Name));
        Assert.Equal("4 this session · 40%", body.Rows[0].Value);
        Assert.Equal(1, body.Rows[0].Share);
        Assert.Equal(1, body.Rows[1].Share);
        Assert.Contains("a skeleton", body.Subtext);
        Assert.Contains("3 kills this session", body.Subtext);
    }

    /// <summary>No target at all — the ask this test guards: the peek must say a target
    /// needs to be picked, never fall back to a session summary.</summary>
    [Fact]
    public void LootPeekWithNoTargetSaysSoRatherThanShowingSession()
    {
        var body = HudExpandPeek.Loot("", "", [], "");

        Assert.NotNull(body.Empty);
        Assert.Equal(LootPresentation.NoTargetNote, body.Empty);
        Assert.Empty(body.Rows);
        Assert.Equal("No target", body.Subtext);
    }

    /// <summary>A real target, nothing known about it yet — the caller's own
    /// <c>TargetEmptyNote</c> wording rides through unchanged, not a second "no loot" line.</summary>
    [Fact]
    public void LootPeekWithATargetButNothingKnownUsesTheCallersEmptyNote()
    {
        var body = HudExpandPeek.Loot("a bat", "", [], "Looking up on eqlwiki…");

        Assert.Equal("Looking up on eqlwiki…", body.Empty);
        Assert.Empty(body.Rows);
        Assert.Equal("a bat", body.Subtext);
    }

    // ---------------------------------------------------------------- buffs ----

    private static BuffState Buff(string label, DateTime now, double? secondsLeft,
        bool estimated = false) =>
        new(label, [label], "you", now.AddMinutes(-1),
            secondsLeft is { } s ? now.AddSeconds(s) : null, estimated);

    /// <summary>
    /// SOONEST TO FADE FIRST, and the unknown LAST — the opposite of every other peek here,
    /// which orders by "biggest".
    ///
    /// The unknown is the half worth pinning. A buff whose duration was never learned has no
    /// deadline at all, and sorting it to the top of a list about time running out would be
    /// the surface asserting something it does not know — the same honesty rule
    /// <c>BuffRosterPresentation.Clock</c> spends a "?" on.
    /// </summary>
    [Fact]
    public void BuffPeekPutsTheSoonestToFadeFirstAndTheUnknownLast()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        var body = HudExpandPeek.Buffs(
            [Buff("Rune", now, 600), Buff("Haste", now, 30), Buff("Unknown Ward", now, null)],
            now);

        Assert.Null(body.Empty);
        Assert.Equal(["Haste", "Rune", "Unknown Ward"], body.Rows.Select(r => r.Name));
        Assert.Equal("0:30", body.Rows[0].Value);
        Assert.Equal("?", body.Rows[2].Value);
        Assert.Contains("3 buffs up", body.Subtext);
    }

    /// <summary>
    /// The countdown is IN the gate, which is the deliberate exception to trap 8 the builder's
    /// own header argues for: the panel has one fixed width since OE-7, so a rebuild repaints
    /// identical geometry — and a clock left out of the gate is a clock that never moves,
    /// which on a surface whose whole job is "how long have I got" is a wrong answer rather
    /// than a saving.
    /// </summary>
    [Fact]
    public void BuffPeekSignatureMovesWithTheClock()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        var buff = Buff("Haste", now, 90);

        var first = HudExpandPeek.Buffs([buff], now);
        var later = HudExpandPeek.Buffs([buff], now.AddSeconds(10));

        Assert.NotEqual(first.Signature, later.Signature);
        Assert.Equal("1:30", first.Rows[0].Value);
        Assert.Equal("1:20", later.Rows[0].Value);
    }

    /// <summary>The gauge is what is LEFT, so a fresh buff draws full and a nearly-gone one
    /// draws empty. The direction is the one thing a screenshot cannot argue with and a
    /// sign flip would look completely plausible in a diff.</summary>
    [Fact]
    public void BuffPeekGaugeShowsWhatIsLeftNotWhatIsSpent()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Local);
        // Landed a minute ago with a minute to go: half spent, half left.
        var half = HudExpandPeek.Buffs([Buff("Haste", now, 60)], now).Rows[0];
        Assert.Equal(0.5, half.Share, 2);

        // No duration known — no elapsed share to read, so the gauge is full rather than
        // zero: an unknown must not render as "about to fade".
        var unknown = HudExpandPeek.Buffs([Buff("Ward", now, null)], now).Rows[0];
        Assert.Equal(1, unknown.Share);
    }

    [Fact]
    public void BuffPeekWithNothingUpSaysSo()
    {
        var body = HudExpandPeek.Buffs([], DateTime.Now);

        Assert.NotNull(body.Empty);
        Assert.Empty(body.Rows);
        Assert.Contains("0 buffs up", body.Subtext);
    }

    // ---------------------------------------------------------------- motes ----

    /// <summary>
    /// One row per tier, count AND per-hour each, with #154's weighting on the subtext.
    ///
    /// **The per-tier rate is apportioned from the summary's own <c>PerHour</c>, so it is the
    /// summary's denominator by construction** — the arithmetic check here is that four of a
    /// six-mote hour reads 4/hr and not 4 divided by something this file guessed.
    /// </summary>
    [Fact]
    public void MotesPeek()
    {
        var body = HudExpandPeek.Motes(Motes.Summarize(
            [new LootDetail("Mote of Minor Potential", 4, ""),
             new LootDetail("Mote of Greater Potential", 2, "")],
            TimeSpan.FromHours(1)));

        Assert.Null(body.Empty);
        Assert.Equal(["Mote of Minor Potential", "Mote of Greater Potential"],
            body.Rows.Select(r => r.Name));
        Assert.Equal("4 · 4/hr", body.Rows[0].Value);
        Assert.Equal("2 · 2/hr", body.Rows[1].Value);
        Assert.Equal(0.5, body.Rows[1].Share, 6);
        // #154: potency, not just a count — 4×1 + 2×6 = 16 in one hour.
        Assert.Contains("16 potency/hr", body.Subtext);
    }

    /// <summary>A NAMED mote is an ordinary item and stays in Loot — the ladder is the
    /// "Mote of X Potential" family and nothing else. The negative that keeps this from
    /// going vacuous (trap 39): a Crystallized Fire Mote in the same loot list must not
    /// appear, or the peek would be quietly disagreeing with the Motes card beside it.
    /// </summary>
    [Fact]
    public void MotesPeekLeavesNamedMotesInLoot()
    {
        var body = HudExpandPeek.Motes(Motes.Summarize(
            [new LootDetail("Mote of Minor Potential", 1, ""),
             new LootDetail("Crystallized Fire Mote", 9, "")],
            TimeSpan.FromHours(1)));

        Assert.Single(body.Rows);
        Assert.DoesNotContain("Crystallized Fire Mote", body.Rows.Select(r => r.Name));
    }

    [Fact]
    public void MotesPeekWithNoneSaysWhereTheyComeFrom()
    {
        var body = HudExpandPeek.Motes(Motes.Summarize([], TimeSpan.FromHours(1)));

        Assert.NotNull(body.Empty);
        Assert.Contains("as you loot them", body.Empty);
        Assert.Empty(body.Rows);
    }

    // ---------------------------------------------------------------- kills ----

    /// <summary>The list the expanded Kills card fills, verbatim — Core has already ordered
    /// it — with the session's OWN <c>KillsPerHour</c> on the subtext rather than a division
    /// performed here.</summary>
    [Fact]
    public void KillsPeek()
    {
        var body = HudExpandPeek.Kills(
            [new NameCount("a giant spider", 9), new NameCount("a skeleton", 3)],
            total: 12, perHour: 24);

        Assert.Null(body.Empty);
        Assert.Equal(["a giant spider", "a skeleton"], body.Rows.Select(r => r.Name));
        Assert.Equal("9", body.Rows[0].Value);
        Assert.Equal(3 / 9.0, body.Rows[1].Share, 6);
        Assert.Contains("12 kills", body.Subtext);
        Assert.Contains("24/hr", body.Subtext);
    }

    /// <summary>The negative (trap 39): a creature that is NOT in <c>YourKills</c> is not in
    /// the peek. "Same as the main widget" is literal — this list is your kills, not every
    /// mob the session saw die, and the two differ in a group.</summary>
    [Fact]
    public void KillsPeekShowsOnlyWhatIsInYourKills()
    {
        var body = HudExpandPeek.Kills([new NameCount("a giant spider", 2)], 2, 4);

        Assert.Single(body.Rows);
        Assert.DoesNotContain("a skeleton", body.Rows.Select(r => r.Name));
    }

    [Fact]
    public void KillsPeekWithNoneSaysSo()
    {
        var body = HudExpandPeek.Kills([], 0, 0);

        Assert.NotNull(body.Empty);
        Assert.Empty(body.Rows);
        Assert.Contains("0 kills", body.Subtext);
    }

    // ---------------------------------------------------------------- procs ----

    /// <summary>
    /// Count, rate and damage per proc — and the RATE is per combat MINUTE (#85, Kerdude),
    /// the denominator the Procs block and the mini-bar cell already use.
    ///
    /// The arithmetic is the assertion: six procs over 120 combat seconds is 3/min, not 3/hr
    /// and not 0.05/s. A peek that divided by wall-clock elapsed would flatter the weapon
    /// exactly as much as the downtime, which is the bug #85 was filed about.
    /// </summary>
    [Fact]
    public void ProcsPeekRatesPerCombatMinute()
    {
        var body = HudExpandPeek.Procs(
            [("Lifetap Strike", 6, 900), ("Frost Bite", 2, 400)], combatSeconds: 120);

        Assert.Null(body.Empty);
        Assert.Equal(["Lifetap Strike", "Frost Bite"], body.Rows.Select(r => r.Name));
        Assert.Equal("×6 · 3/min · 900 dmg", body.Rows[0].Value);
        Assert.Equal("×2 · 1/min · 400 dmg", body.Rows[1].Value);
        Assert.Equal(2 / 6.0, body.Rows[1].Share, 6);
        Assert.Contains("8 procs", body.Subtext);
        Assert.Contains("4/min", body.Subtext);
        Assert.Contains("1,300 dmg", body.Subtext);
    }

    /// <summary>**The source check Bevel's #371 asked for, as a test.**
    /// <c>StatsSnapshot.Procs</c> is <c>(Name, Count, Damage)</c> — there is no healing field
    /// on it — so what ships is the stats the app tracks, which is exactly what the Procs
    /// card shows. This asserts the peek does not invent a heal figure it cannot have; if
    /// Core ever learns healing procs, this row is what has to be revisited on purpose
    /// rather than a silent ride-along.</summary>
    [Fact]
    public void ProcsPeekReportsOnlyWhatCoreTracks()
    {
        var body = HudExpandPeek.Procs([("Lifetap Strike", 1, 100)], 60);

        Assert.DoesNotContain("heal", body.Rows[0].Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("heal", body.Subtext, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcsPeekWithNoneSaysSo()
    {
        var body = HudExpandPeek.Procs([], 60);

        Assert.NotNull(body.Empty);
        Assert.Empty(body.Rows);
    }

    // ---------------------------------------------------------------- money ----

    /// <summary>The Wealth tab's four coin facts, formatted by <c>StatsSnapshot.FormatCoin</c>
    /// so the peek and the tab cannot render one number two ways. NO gauge: this is one figure
    /// broken into its parts, and a bar under "per hour" would compare a rate to a total.
    /// </summary>
    [Fact]
    public void MoneyPeek()
    {
        var body = HudExpandPeek.Money(total: 1234, looted: 1000, vendor: 234, perHour: 2468);

        Assert.Null(body.Empty);
        Assert.Equal(["Looted", "Sold to vendors", "Total", "Per hour"],
            body.Rows.Select(r => r.Name));
        Assert.Equal(StatsSnapshot.FormatCoin(1000), body.Rows[0].Value);
        Assert.Equal(StatsSnapshot.FormatCoin(2468), body.Rows[3].Value);
        Assert.All(body.Rows, r => Assert.Equal(0, r.Share));
        Assert.Contains(StatsSnapshot.FormatCoin(1234), body.Subtext);
    }

    /// <summary>The per-item breakdown stays in the WINDOW (the negative): the peek is
    /// row-capped and the ⧉ is one click from the full Wealth tab, so a sold item's name has
    /// no business here — that is what "the float carries the detail" means for a surface
    /// whose detail is a list of everything you vendored.</summary>
    [Fact]
    public void MoneyPeekLeavesTheSoldItemBreakdownInTheWindow()
    {
        var body = HudExpandPeek.Money(500, 300, 200, 1000);

        Assert.Equal(4, body.Rows.Count);
        // Four FACTS, not a list that could grow with the session.
        Assert.All(body.Rows, r => Assert.DoesNotContain("Rusty", r.Name));
    }

    [Fact]
    public void MoneyPeekWithNoCoinSaysSo()
    {
        var body = HudExpandPeek.Money(0, 0, 0, 0);

        Assert.NotNull(body.Empty);
        Assert.Empty(body.Rows);
    }

    // --------------------------------------------------------------- deaths ----

    /// <summary>Newest first — the opposite of Core's own order for this list, and
    /// deliberately: the death you want to read is the one that just happened. The value is a
    /// WALL time rather than "3 minutes ago", so nothing in the signature ticks (trap 8).
    /// </summary>
    [Fact]
    public void DeathsPeekPutsTheNewestFirst()
    {
        var at = new DateTime(2026, 9, 7, 13, 5, 0);
        var body = HudExpandPeek.Deaths(
            [new TimedDetail(at, "a giant spider"), new TimedDetail(at.AddMinutes(20), "a skeleton")]);

        Assert.Null(body.Empty);
        Assert.Equal(["a skeleton", "a giant spider"], body.Rows.Select(r => r.Name));
        Assert.Equal("1:05 PM", body.Rows[1].Value);
        Assert.All(body.Rows, r => Assert.Equal(0, r.Share));
        Assert.Contains("2 deaths", body.Subtext);
    }

    [Fact]
    public void DeathsPeekWithNoneSaysSo()
    {
        var body = HudExpandPeek.Deaths([]);

        Assert.Equal("No deaths this session.", body.Empty);
        Assert.Empty(body.Rows);
        Assert.Contains("0 deaths", body.Subtext);
    }
}
