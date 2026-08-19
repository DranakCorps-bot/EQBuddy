using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Alerts theme's tab definition — step 1 of docs/Themes.md's recipe, and the thing
/// that stops the desktop window and EQBuddy Mobile inventing their own idea of what the
/// tabs are. <c>QuestSurfaceTests</c> is the precedent these mirror.
///
/// The assertion that matters most is <see cref="Watch_keeps_the_settings_key_the_card_already_used"/>:
/// step 5 of the recipe is "fold the old settings keys, PRESERVING position and hidden
/// state", and a fresh key for the Watch tab would silently move every existing player's
/// card and un-hide one they had hidden.
/// </summary>
public class AlertSurfaceTests
{
    [Fact]
    public void The_strip_is_always_all_four_tabs_in_a_fixed_order()
    {
        var tabs = AlertSurface.Tabs();

        Assert.Equal([AlertTab.Watch, AlertTab.Buffs, AlertTab.Spawns, AlertTab.Crowd],
            tabs.Select(t => t.Tab));
    }

    /// <summary>An empty tab keeps its place. A Spawns tab that vanishes when no timer is
    /// running is a silent no-op, and a player who has never set one up is exactly who
    /// needs to find it.</summary>
    [Fact]
    public void An_empty_tab_still_gets_its_place()
    {
        Assert.Equal(4, AlertSurface.Tabs(watch: 0).Count);
        Assert.Contains(AlertSurface.Tabs(), t => t.Tab == AlertTab.Spawns);
    }

    /// <summary>THE one. `tracked` is the key SectionOrder, HiddenSections and
    /// EQBUDDY_EXPAND have always used for the Watch card; a fresh key would reset
    /// position and hidden state for everyone who already has it placed.</summary>
    [Fact]
    public void Watch_keeps_the_settings_key_the_card_already_used()
    {
        Assert.Equal("tracked", AlertSurface.KeyFor(AlertTab.Watch));
    }

    [Fact]
    public void Every_tab_has_a_label_and_a_lowercase_stable_key()
    {
        foreach (var tab in Enum.GetValues<AlertTab>())
        {
            Assert.NotEmpty(AlertSurface.LabelFor(tab));
            var key = AlertSurface.KeyFor(tab);
            Assert.NotEmpty(key);
            Assert.Equal(key.ToLowerInvariant(), key);
        }
    }

    [Fact]
    public void Keys_are_distinct_so_a_saved_choice_cannot_be_ambiguous()
    {
        var keys = Enum.GetValues<AlertTab>().Select(AlertSurface.KeyFor).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("tracked", AlertTab.Watch)]
    [InlineData("watch", AlertTab.Watch)]     // the human name, accepted too
    [InlineData("buffs", AlertTab.Buffs)]
    [InlineData("spawns", AlertTab.Spawns)]
    [InlineData("crowd", AlertTab.Crowd)]
    [InlineData("mez", AlertTab.Crowd)]       // what the old surfaces were called
    [InlineData("charm", AlertTab.Crowd)]
    [InlineData("  Buffs  ", AlertTab.Buffs)] // trimmed and case-insensitive
    public void Keys_round_trip_including_the_names_the_old_surfaces_used(string key, AlertTab expected)
    {
        Assert.Equal(expected, AlertSurface.TabForKey(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void An_unknown_key_is_null_rather_than_a_guess(string? key)
    {
        Assert.Null(AlertSurface.TabForKey(key));
    }

    [Fact]
    public void Every_key_maps_back_to_its_own_tab()
    {
        foreach (var tab in Enum.GetValues<AlertTab>())
            Assert.Equal(tab, AlertSurface.TabForKey(AlertSurface.KeyFor(tab)));
    }

    /// <summary>Zero renders. "No watch rules yet" is a state a player can act on, and
    /// hiding the badge would make an empty tab look broken rather than empty.</summary>
    [Fact]
    public void A_zero_count_still_shows_a_badge_but_an_absent_one_does_not()
    {
        var tabs = AlertSurface.Tabs(watch: 0);

        Assert.Equal("0", tabs.Single(t => t.Tab == AlertTab.Watch).Badge);
        Assert.Null(tabs.Single(t => t.Tab == AlertTab.Buffs).Badge);
    }

    // ---- the launcher card's line (recipe step 3) ----

    [Fact]
    public void The_launcher_names_what_is_configured()
    {
        Assert.Equal("3 watch · 2 buff sets · 5 timers",
            AlertSurface.LauncherSummary(watchRules: 3, buffSets: 2, spawnTimers: 5));
    }

    [Fact]
    public void The_launcher_singularizes_one_buff_set()
    {
        Assert.Contains("1 buff set ", AlertSurface.LauncherSummary(1, 1, 1) + " ");
    }

    [Fact]
    public void The_launcher_omits_what_is_not_set_up_rather_than_printing_zeros()
    {
        Assert.Equal("4 watch", AlertSurface.LauncherSummary(4, 0, 0));
    }

    /// <summary>A fresh profile has none of this. The card must invite rather than read
    /// as broken — "silent no-ops are broken" applies to empty states too.</summary>
    [Fact]
    public void Nothing_configured_says_so_plainly()
    {
        Assert.Equal("none set up", AlertSurface.LauncherSummary(0, 0, 0));
    }
}
