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

    /// <summary>
    /// **The <c>misc</c> WIRE KEY outlives the <c>misc</c> CARD**, and that distinction is
    /// the whole content of this test since HUD subtraction cut 2 (2026-09-05).
    ///
    /// It used to assert <c>AbsorbedCardKeys</c> and <c>ThemeCardKey</c> — a fold's
    /// statement about a card — and both went with the card, because a fold may only name
    /// keys that are no longer cards (trap 55, and #252 is what leaving one cost). What
    /// stays is the address: <c>world:misc</c> is how the shell, a saved tab choice and
    /// every old habit reach the Travels room, and it must not move just because the
    /// widget stopped drawing a card of that name.
    /// </summary>
    [Fact]
    public void TheTravelsRoomKeepsTheMiscWireKeyAfterTheCardIsCut()
    {
        Assert.Equal("misc", WorldSurface.KeyFor(WorldTab.Travels));
        Assert.Equal(WorldTab.Travels, WorldSurface.TabForKey("misc"));

        // And no card in the app answers to it any more — the premise check that pairs
        // with the migration, asserted here rather than assumed.
        Assert.DoesNotContain("misc",
            EQBuddy.UI.Shared.OverlaySections.Catalog.Select(c => c.Key));
    }

    /// <summary>The room three hosts land on when nobody names one. Was
    /// <c>DefaultInlineTab</c> until 2026-09-05: there is no inline card for it to be the
    /// default OF, and `InlineModeFor` — which only a card ever asked — went with it.</summary>
    [Fact]
    public void TravelsIsTheDefaultRoom() =>
        Assert.Equal(WorldTab.Travels, WorldSurface.DefaultTab);

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
    }

    // `TheLauncherCarriesCountsNeverCountdowns` WAS HERE and went with
    // `WorldSurface.LauncherSummary` on 2026-09-05 (HUD subtraction cut 2) — the collapsed
    // World card's one line, whose only caller was `MainWindow.RefreshUi`. Keeping the
    // assertion would have pinned a sentence nothing renders, which is trap 34's shape.
    //
    // **The RULE it protected is still enforced, one file over**: `WorldThemeTests` asserts
    // that Camps and Path carry no badge, which is the same "counts, never countdowns"
    // (trap 12 resizes an always-on-top window every second; trap 8 wakes every phone).
}
