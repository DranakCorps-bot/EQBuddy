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

    [Fact]
    public void The_wealth_badge_carries_coin_and_the_mote_rate()
    {
        var badge = ProgressTheme.Wealth(Farming());

        Assert.Contains(StatsSnapshot.FormatCoin(51408), badge);
        Assert.Contains("/hr", badge);
    }

    /// <summary>"1 motes" is the same slip the Faction badge carried for months. It is
    /// cheap to pin and it reads as carelessness on a surface people look at constantly.</summary>
    [Fact]
    public void One_mote_is_singular()
    {
        Assert.Contains("1 mote ", ProgressTheme.Wealth(Farming(motes: 1)));
        Assert.Contains("2 motes ", ProgressTheme.Wealth(Farming(motes: 2)));
    }
}
