using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The WORLD theme's vocabulary, pinned before any window exists — step 1 of the
/// recipe, the same way <see cref="CreatureSurfaceTests"/> pinned Kills &amp; Drops's. A
/// key renamed after a release is a saved tab choice broken after a release.</summary>
public class WorldSurfaceTests
{
    [Fact]
    public void LabelsMatchThemesDoc()
    {
        Assert.Equal("Map", WorldSurface.LabelFor(WorldTab.Map));
        Assert.Equal("Camps", WorldSurface.LabelFor(WorldTab.Camps));
        Assert.Equal("Path", WorldSurface.LabelFor(WorldTab.Routes));
        Assert.Equal("Travels", WorldSurface.LabelFor(WorldTab.Travels));
        Assert.Equal("Drops", WorldSurface.LabelFor(WorldTab.Drops));
    }

    /// <summary>The wire keys reuse the four absorbed windows'/card's own names — nothing
    /// invented, so an old habit and an old doc line both still land.</summary>
    [Fact]
    public void KeysReuseTheAbsorbedSurfacesOwnNames()
    {
        Assert.Equal("map", WorldSurface.KeyFor(WorldTab.Map));
        Assert.Equal("spawns", WorldSurface.KeyFor(WorldTab.Camps));
        Assert.Equal("travel", WorldSurface.KeyFor(WorldTab.Routes));
        Assert.Equal("misc", WorldSurface.KeyFor(WorldTab.Travels));
        // The fifth is the name DropsWindow answered to and the name CreatureSurface still
        // answers to — one surface, one word, in both lanes.
        Assert.Equal("drops", WorldSurface.KeyFor(WorldTab.Drops));
        Assert.Equal(CreatureSurface.KeyFor(CreatureTab.Drops), WorldSurface.KeyFor(WorldTab.Drops));
    }

    [Theory]
    [InlineData("map", WorldTab.Map)]
    [InlineData(" SPAWNS ", WorldTab.Camps)]
    [InlineData("camps", WorldTab.Camps)]
    [InlineData("timers", WorldTab.Camps)]
    [InlineData("travel", WorldTab.Routes)]
    [InlineData("routes", WorldTab.Routes)]
    [InlineData("misc", WorldTab.Travels)]
    [InlineData("travels", WorldTab.Travels)]
    [InlineData("deaths", WorldTab.Travels)]
    // Resolves whether or not the host asking can DRAW it — see ShellOnly. An address
    // landing somewhere true is a different question from who may show it.
    [InlineData("drops", WorldTab.Drops)]
    [InlineData(" DROPS ", WorldTab.Drops)]
    [InlineData("nonsense", null)]
    [InlineData(null, null)]
    public void EveryWordTheseSurfacesHaveBeenCalledStillResolves(string? key, WorldTab? expected) =>
        Assert.Equal(expected, WorldSurface.TabForKey(key));

    /// <summary>The theme absorbs exactly ONE card — the old Travels &amp; Deaths card —
    /// and keeps its <c>misc</c> key so there is no settings migration at all.</summary>
    [Fact]
    public void TheThemeTakesTheMiscCardSlotAndNoOther()
    {
        Assert.Equal(["misc"], WorldSurface.AbsorbedCardKeys);
        Assert.Equal("misc", WorldSurface.ThemeCardKey);
        Assert.Equal(WorldSurface.ThemeCardKey, WorldSurface.KeyFor(WorldTab.Travels));
    }

    [Fact]
    public void InlineTableIsTravelsFullEverythingElseGlance()
    {
        Assert.Equal(InlineMode.Full, WorldSurface.InlineModeFor(WorldTab.Travels));
        Assert.Equal(InlineMode.Glance, WorldSurface.InlineModeFor(WorldTab.Map));
        Assert.Equal(InlineMode.Glance, WorldSurface.InlineModeFor(WorldTab.Camps));
        Assert.Equal(InlineMode.Glance, WorldSurface.InlineModeFor(WorldTab.Routes));
        Assert.Equal(WorldTab.Travels, WorldSurface.DefaultInlineTab);
    }

    [Fact]
    public void TabsCarryTheirHeadlinesAndOmitEmptyOnes()
    {
        var tabs = WorldSurface.Tabs(map: "Crushbone", travels: "2 deaths", drops: "3 creatures").ToList();
        Assert.Equal(5, tabs.Count);
        Assert.Equal("Crushbone", tabs.Single(t => t.Tab == WorldTab.Map).Value);
        Assert.Null(tabs.Single(t => t.Tab == WorldTab.Camps).Value);
        Assert.Equal("2 deaths", tabs.Single(t => t.Tab == WorldTab.Travels).Value);
        Assert.Equal("3 creatures", tabs.Single(t => t.Tab == WorldTab.Drops).Value);
        // The definition is UNFILTERED, which is what makes `world:drops` a valid page:room
        // address with no edit to ShellPages: the shell reads this list to build the
        // grammar, so a tab the room can land on has to be in it.
        Assert.Null(WorldSurface.Tabs().Single(t => t.Tab == WorldTab.Drops).Value);
    }

    /// <summary>
    /// **The fifth tab is the shell's and the v1 lane cannot draw it** — <c>WorldWindow</c>
    /// and the inline card both map a <see cref="WorldTab"/> to a body with a
    /// <c>_ =&gt; _travels.Body</c> default, so an unfiltered header would put a "Drops"
    /// chip on a shipped window that answers with the Travels list. One predicate, read by
    /// <c>WorldTheme</c>, keeps it off both.
    ///
    /// The negative row is what stops this going vacuous (trap 39): a predicate that
    /// answered true for everything would hide the whole strip and read as coverage.
    /// </summary>
    [Fact]
    public void OnlyDropsIsTheShellsAloneAndTheOtherFourAreNot()
    {
        Assert.True(WorldSurface.ShellOnly(WorldTab.Drops));

        Assert.False(WorldSurface.ShellOnly(WorldTab.Map));
        Assert.False(WorldSurface.ShellOnly(WorldTab.Camps));
        Assert.False(WorldSurface.ShellOnly(WorldTab.Routes));
        Assert.False(WorldSurface.ShellOnly(WorldTab.Travels));

        // And the theme absorbs no new card for it — Drops was never a card at all, only a
        // menu entry, which is why this whole tab needs no settings migration.
        Assert.Equal(["misc"], WorldSurface.AbsorbedCardKeys);
    }

    /// <summary>Counts, never countdowns — a deadline belongs to the spawn-due chips, not
    /// this launcher line (trap 12/8). A part with nothing to say is omitted, not printed
    /// as a zero, which is what keeps this readable on a brand new character.</summary>
    [Fact]
    public void TheLauncherCarriesCountsNeverCountdowns()
    {
        Assert.Equal("Crushbone · 4 zones · 2 deaths · 3 timers",
            WorldSurface.LauncherSummary(zone: "Crushbone", zonesVisited: 4, deaths: 2, runningTimers: 3));
        Assert.Equal("1 zone", WorldSurface.LauncherSummary(zonesVisited: 1));
        Assert.Equal("no travels yet", WorldSurface.LauncherSummary());
    }
}
