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
        var tabs = WorldSurface.Tabs(map: "Crushbone", travels: "2 deaths").ToList();
        Assert.Equal(4, tabs.Count);
        Assert.Equal("Crushbone", tabs.Single(t => t.Tab == WorldTab.Map).Value);
        Assert.Null(tabs.Single(t => t.Tab == WorldTab.Camps).Value);
        Assert.Equal("2 deaths", tabs.Single(t => t.Tab == WorldTab.Travels).Value);
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
