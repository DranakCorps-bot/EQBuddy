using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Progress theme's tab definition — step 1 of docs/Themes.md's recipe. Five cards
/// (Progress, Money, Motes, Faction, Raids) become four tabs behind one launcher.
///
/// The assertions that matter are the two about KEYS. Step 5 of the recipe is "fold the
/// old settings keys, PRESERVING position and hidden state", and this theme absorbs five
/// of them — so the key it takes decides whether a player's widget keeps its layout or
/// silently rearranges itself.
/// </summary>
public class ProgressSurfaceTests
{
    [Fact]
    public void The_strip_is_always_all_four_tabs_in_a_fixed_order()
    {
        Assert.Equal(
            [ProgressTab.Experience, ProgressTab.Wealth, ProgressTab.Faction, ProgressTab.Raids],
            ProgressSurface.Tabs().Select(t => t.Tab));
    }

    /// <summary>The theme inherits the Progress card's own slot. A NEW key would append the
    /// folded card to the bottom of every player's SectionOrder and un-hide it for anyone
    /// who had Progress hidden.</summary>
    [Fact]
    public void The_theme_takes_a_card_key_it_already_absorbs()
    {
        Assert.Equal("progress", ProgressSurface.ThemeCardKey);
        Assert.Contains(ProgressSurface.ThemeCardKey, ProgressSurface.AbsorbedCardKeys);
        Assert.Equal("progress", ProgressSurface.KeyFor(ProgressTab.Experience));
    }

    /// <summary>All five cards named in the plan, and no others — the fold reads this list,
    /// so an omission here silently leaves a card behind and an extra one deletes a card
    /// this theme does not own.</summary>
    [Fact]
    public void It_absorbs_exactly_the_five_cards_the_plan_names()
    {
        Assert.Equal(["progress", "money", "motes", "faction", "raids"],
            ProgressSurface.AbsorbedCardKeys);
    }

    [Fact]
    public void Every_tab_has_a_label_and_a_lowercase_stable_key()
    {
        foreach (var tab in Enum.GetValues<ProgressTab>())
        {
            Assert.NotEmpty(ProgressSurface.LabelFor(tab));
            var key = ProgressSurface.KeyFor(tab);
            Assert.NotEmpty(key);
            Assert.Equal(key.ToLowerInvariant(), key);
        }
    }

    [Fact]
    public void Keys_are_distinct_so_a_saved_choice_cannot_be_ambiguous()
    {
        var keys = Enum.GetValues<ProgressTab>().Select(ProgressSurface.KeyFor).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Wealth merges TWO cards, so both of their old keys have to land on it —
    /// otherwise a player whose saved tab was "motes" opens the window on nothing.</summary>
    [Theory]
    [InlineData("money", ProgressTab.Wealth)]
    [InlineData("motes", ProgressTab.Wealth)]
    [InlineData("wealth", ProgressTab.Wealth)]
    [InlineData("progress", ProgressTab.Experience)]
    [InlineData("xp", ProgressTab.Experience)]
    [InlineData("faction", ProgressTab.Faction)]
    [InlineData("raids", ProgressTab.Raids)]
    [InlineData("  Motes  ", ProgressTab.Wealth)]
    public void Every_absorbed_card_key_resolves_to_the_tab_that_now_holds_it(
        string key, ProgressTab expected)
    {
        Assert.Equal(expected, ProgressSurface.TabForKey(key));
    }

    /// <summary>The strongest form of the rule above: nothing this theme swallows may
    /// resolve to null, or a saved choice lands nowhere.</summary>
    [Fact]
    public void No_absorbed_card_key_fails_to_resolve()
    {
        foreach (var key in ProgressSurface.AbsorbedCardKeys)
            Assert.NotNull(ProgressSurface.TabForKey(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("combat")]
    public void A_key_this_theme_does_not_own_is_null_rather_than_a_guess(string? key)
    {
        Assert.Null(ProgressSurface.TabForKey(key));
    }

    [Fact]
    public void Every_key_round_trips_to_its_own_tab()
    {
        foreach (var tab in Enum.GetValues<ProgressTab>())
            Assert.Equal(tab, ProgressSurface.TabForKey(ProgressSurface.KeyFor(tab)));
    }

    [Fact]
    public void A_tab_with_nothing_to_report_carries_no_value_rather_than_an_empty_string()
    {
        var tabs = ProgressSurface.Tabs(experience: "14.2% xp", wealth: "   ");

        Assert.Equal("14.2% xp", tabs.Single(t => t.Tab == ProgressTab.Experience).Value);
        Assert.Null(tabs.Single(t => t.Tab == ProgressTab.Wealth).Value);
    }

    // ---- the launcher line (recipe step 3) ----

    [Fact]
    public void The_launcher_carries_what_the_five_card_headers_carried()
    {
        Assert.Equal("14.2% xp · 5p 1g 4s 8c · 5 factions · 3 raids",
            ProgressSurface.LauncherSummary("14.2% xp", "5p 1g 4s 8c", 5, 3));
    }

    [Fact]
    public void The_launcher_singularizes()
    {
        Assert.Equal("1 faction · 1 raid", ProgressSurface.LauncherSummary(factions: 1, raidsCleared: 1));
    }

    /// <summary>Five zeros would be noise on a fresh character — which is exactly who is
    /// looking at a fresh widget.</summary>
    [Fact]
    public void The_launcher_omits_what_has_nothing_to_say()
    {
        Assert.Equal("14.2% xp", ProgressSurface.LauncherSummary(xp: "14.2% xp"));
        Assert.Equal("no progress yet", ProgressSurface.LauncherSummary());
    }
}
