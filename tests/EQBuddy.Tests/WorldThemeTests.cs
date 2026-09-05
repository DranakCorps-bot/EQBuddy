using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The WORLD theme's two tab strips — the v1 one (<c>WorldWindow</c> and the widget's
/// inline card) and the Evolved shell's.
///
/// **This file exists because of what a fifth tab would have done unattended.** S2 gives the
/// shell's World room a Drops tab, and both v1 hosts map a <see cref="WorldTab"/> to a body
/// with a <c>_ =&gt; _travels.Body</c> default — so a header they cannot draw does not throw,
/// does not fail a build and does not fail a test. It ships a "Drops" chip on a window that
/// answers with the Travels list, which is trap 30's staging-list lesson (a list that stops
/// covering an enum the day the enum grows) arriving through a shared definition instead of
/// through a script.
/// </summary>
public class WorldThemeTests
{
    /// <summary>The v1 strip is the definition minus what only the shell can draw — asserted
    /// as a SUBTRACTION rather than as a number, so a sixth World tab follows automatically
    /// and nobody has to re-type a 4 here (the same property <c>ShellOnly</c> being a
    /// predicate rather than a second list buys).</summary>
    [Fact]
    public void TheV1StripIsTheDefinitionMinusWhatOnlyTheShellCanDraw()
    {
        var v1 = WorldTheme.Tabs("Crushbone", deaths: 2);

        Assert.DoesNotContain(v1, h => h.Tab == WorldTab.Drops);
        Assert.DoesNotContain(v1, h => WorldSurface.ShellOnly(h.Tab));
        Assert.Equal(
            WorldSurface.Tabs().Count(h => !WorldSurface.ShellOnly(h.Tab)),
            v1.Count);
    }

    /// <summary>The shell's strip is everything, and the two agree word for word on every
    /// tab they share — the parity-by-shared-module reason both are built here rather than
    /// in the two UIs (#210).</summary>
    [Fact]
    public void TheShellStripIsEverythingAndTheTwoAgreeOnWhatTheyShare()
    {
        var v1 = WorldTheme.Tabs("Crushbone", deaths: 2);
        var shell = WorldTheme.ShellTabs("Crushbone", deaths: 2, drops: "3 creatures");

        Assert.Equal(WorldSurface.Tabs().Count, shell.Count);
        Assert.Contains(shell, h => h.Tab == WorldTab.Drops);
        Assert.Equal("3 creatures", shell.Single(h => h.Tab == WorldTab.Drops).Value);

        foreach (var header in v1)
        {
            var same = shell.Single(h => h.Tab == header.Tab);
            Assert.Equal(header.Label, same.Label);
            Assert.Equal(header.Key, same.Key);
            Assert.Equal(header.Value, same.Value);
        }
    }

    /// <summary>Map gets the zone, Travels gets the death count, and Camps/Path carry no
    /// badge — a live timer count on a tab strip is a countdown by another name the moment a
    /// player watches it tick (trap 12/8). Drops carries whatever its VIEW says and nothing
    /// derived here, so a filtered body and its own badge cannot disagree.</summary>
    [Fact]
    public void OnlyTheTabsWithSomethingStillToSayCarryAbadge()
    {
        var shell = WorldTheme.ShellTabs("Crushbone", deaths: 2, drops: null);

        Assert.Equal("Crushbone", shell.Single(h => h.Tab == WorldTab.Map).Value);
        Assert.Equal("2 deaths", shell.Single(h => h.Tab == WorldTab.Travels).Value);
        Assert.Null(shell.Single(h => h.Tab == WorldTab.Camps).Value);
        Assert.Null(shell.Single(h => h.Tab == WorldTab.Routes).Value);
        // Null rather than "0 creatures": a zero on a fresh character reads as a failure
        // rather than as a session that has not started, which is why DropsCardView's own
        // badge is null until something drops.
        Assert.Null(shell.Single(h => h.Tab == WorldTab.Drops).Value);

        Assert.Equal("1 death", WorldTheme.ShellTabs(null, deaths: 1, drops: null)
            .Single(h => h.Tab == WorldTab.Travels).Value);
        Assert.Null(WorldTheme.Tabs(null, deaths: 0).Single(h => h.Tab == WorldTab.Map).Value);
    }

    // `GlanceLinesExistForTheThreeGlanceTabsAndNoOthers` WAS HERE and went with
    // `WorldTheme`'s glance family on 2026-09-05 (HUD subtraction cut 2). A Glance line is
    // what an INLINE CARD draws in place of a tab body it is too small for, and there is no
    // World card — the window and the shell's room both draw every tab for real. Asserting
    // the wording of a sentence nothing renders is trap 34's shape: a guard that cannot
    // fail, reading as coverage.
}
