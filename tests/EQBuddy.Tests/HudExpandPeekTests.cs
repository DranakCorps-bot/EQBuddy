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

    [Fact]
    public void LootPeek()
    {
        var body = HudExpandPeek.Loot(
            [new LootDetail("Bone Chips", 4, "a skeleton"), new LootDetail("Rusty Sword", 1, "")],
            lootTotal: 5);

        Assert.Null(body.Empty);
        Assert.Equal(["Bone Chips", "Rusty Sword"], body.Rows.Select(r => r.Name));
        Assert.Equal("4", body.Rows[0].Value);
        Assert.Equal("last from: a skeleton", body.Rows[0].Tooltip);
        // No source recorded is no tooltip, not "last from: ".
        Assert.Null(body.Rows[1].Tooltip);
        Assert.Contains("5 items", body.Subtext);
        Assert.Contains("2 kinds", body.Subtext);
    }

    [Fact]
    public void LootPeekWithNoDropsSaysSo()
    {
        var body = HudExpandPeek.Loot([], 0);

        Assert.NotNull(body.Empty);
        Assert.Empty(body.Rows);
        Assert.Contains("0 items", body.Subtext);
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
}
