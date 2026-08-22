using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// What the PROGRESS THEME's launcher line and tab badges actually say.
///
/// **This file exists because 1.96.0 shipped without it and a player noticed within the
/// hour.** The Progress theme folded five cards into one launcher; the Motes card's header
/// had read "3 · 0.9/hr" on the widget face, and after the fold the rate survived only
/// inside the Wealth tab's body — two clicks away, and gone from Options entirely, so it
/// could not be put back. #219 (typical-usual-chaos): *"Where'd motes/hour go? That was
/// the most useful stat and the main reason I opened EQBuddy."*
///
/// Every gate was green when that shipped. Nothing here is subtle or racy — it is a
/// string, and the only reason no test caught it is that no test asked. So these ask.
/// </summary>
public class ProgressThemeTests
{
    /// <summary>A session that has looted motes over a measurable stretch — the shape a
    /// farmer is in when they glance at the widget.</summary>
    private static StatsSnapshot Farming(int motes = 3, double hours = 2) => new()
    {
        SessionStart = DateTime.Now.AddHours(-hours),
        LastEventTime = DateTime.Now,
        Copper = 51408,
        Loot = [new LootDetail("Mote of Infinitesimal Potential", motes, "a shadowed man")],
    };

    [Fact]
    public void The_launcher_carries_the_mote_RATE_not_just_a_count()
    {
        var line = ProgressTheme.LauncherSummary(Farming());

        // The rate, on the widget face, without opening anything. This is the assertion
        // #219 is about; a count alone answers a different question than "is this camp
        // worth staying at".
        Assert.Contains("motes/hr", line);
    }

    [Fact]
    public void The_launcher_still_leads_with_xp_and_coin()
    {
        var line = ProgressTheme.LauncherSummary(Farming());

        Assert.StartsWith("0.0% xp", line);
        Assert.Contains(StatsSnapshot.FormatCoin(51408), line);
    }

    /// <summary>The other half of the bargain: the line stays short for everyone who is
    /// not farming motes, which is what earns motes a place on it at all.</summary>
    [Fact]
    public void The_launcher_says_nothing_about_motes_when_none_have_dropped()
    {
        var line = ProgressTheme.LauncherSummary(new StatsSnapshot
        {
            SessionStart = DateTime.Now.AddHours(-1),
            LastEventTime = DateTime.Now,
            Copper = 51408,
        });

        Assert.DoesNotContain("mote", line);
    }

    /// <summary>Faction and raids badge their own TABS instead of riding the line. Pinned
    /// so the fix for #219 is not undone by someone re-adding them for symmetry: the line
    /// truncates mid-word once it is long enough, and the mote rate is what got dropped to
    /// make room last time.</summary>
    [Fact]
    public void The_launcher_leaves_review_time_facts_to_the_tabs()
    {
        var s = Farming();
        s.Faction.Add(new FactionDetail("Knights of Truth", 4, 80));

        var line = ProgressTheme.LauncherSummary(s);

        Assert.DoesNotContain("faction", line);
        Assert.DoesNotContain("raid", line);
        // …and the tab badge is where it went.
        Assert.Equal("1 faction", ProgressTheme.Faction(s));
    }

    /// <summary>The Wealth badge is COIN ONLY (Bevel, Helm-signed 2026-08-22). It used to
    /// carry the mote rate as well, from when Wealth was the tab that absorbed two cards;
    /// #227 settled that Wealth is coin and the Motes card owns the rate, and the inline
    /// card made the split visible — a chip naming a rate sat an inch above a body that
    /// refused to.
    ///
    /// The negative is the half that matters: without it this passes on a badge that has
    /// quietly grown the rate back for "consistency" with something.</summary>
    [Fact]
    public void The_wealth_badge_is_coin_only()
    {
        var badge = ProgressTheme.Wealth(Farming());

        Assert.Equal(StatsSnapshot.FormatCoin(51408), badge);
        Assert.DoesNotContain("/hr", badge);
        Assert.DoesNotContain("mote", badge, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"1 motes" is the same slip the Faction badge carried for months. It is
    /// cheap to pin and it reads as carelessness on a surface people look at constantly.
    ///
    /// Asserted against <c>MoteRate</c> itself now that the Wealth badge no longer carries
    /// it — that string still reaches players, on the Motes card's own header in both
    /// widgets, which is where #227 said the rate belongs.</summary>
    [Fact]
    public void One_mote_is_singular()
    {
        Assert.Contains("1 mote ", ProgressTheme.MoteRate(Farming(motes: 1)));
        Assert.Contains("2 motes ", ProgressTheme.MoteRate(Farming(motes: 2)));
    }

    /// <summary>
    /// The Raids glance line says what the chip CANNOT (Bevel, Helm-signed 2026-08-22).
    ///
    /// The first build printed `Raids — 2 / 21` under a chip already reading `Raids 2 / 21`.
    /// Both failure modes are pinned here: the twin (no second fraction, no second "Raids")
    /// and the empty body, which is why the line still exists at all.
    /// </summary>
    [Fact]
    public void The_raids_glance_carries_the_remainder_not_the_scoreboard()
    {
        var line = ProgressTheme.RaidsGlance(defeated: 2, total: 21);

        Assert.Equal("19 left", line);
        Assert.DoesNotContain("/", line);
        Assert.DoesNotContain("Raids", line);
        Assert.NotEqual("", line);
    }

    /// <summary>The one state that is an achievement rather than a measurement. "0 left"
    /// is a number to read; this is not.</summary>
    [Fact]
    public void A_cleared_raid_ledger_says_so()
    {
        Assert.Equal("all cleared", ProgressTheme.RaidsGlance(defeated: 21, total: 21));
        // A ledger that has somehow over-counted must not print "-2 left".
        Assert.Equal("all cleared", ProgressTheme.RaidsGlance(defeated: 23, total: 21));
    }
}
