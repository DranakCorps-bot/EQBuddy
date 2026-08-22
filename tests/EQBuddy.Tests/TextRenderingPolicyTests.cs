using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The policy behind the 2026-08-21 CrossOver text fix. The WPF layer that
/// applies it has no unit tests, so the DECISION lives in UI.Shared and is pinned here —
/// the standing move for window bugs that are a rule rather than a pixel.</summary>
public class TextRenderingPolicyTests
{
    [Fact]
    public void WineGetsDisplay_BecauseIdealIsVisiblyBrokenThere()
    {
        Assert.Equal(TextLayoutMode.Display,
            TextRenderingPolicy.Decide(underWine: true, overrideValue: null));
    }

    /// <summary>Windows keeps WPF's own default. This fix is scoped to the environment
    /// that needs it: Display snaps to whole pixels BEFORE the widget's UI-scale
    /// LayoutTransform (trap 1), so imposing it where Ideal works correctly would trade
    /// a real defect for a new one on every surface.</summary>
    [Fact]
    public void WindowsKeepsIdeal()
    {
        Assert.Equal(TextLayoutMode.Ideal,
            TextRenderingPolicy.Decide(underWine: false, overrideValue: null));
    }

    [Theory]
    [InlineData("display", TextLayoutMode.Display)]
    [InlineData("Display", TextLayoutMode.Display)]
    [InlineData("  DISPLAY  ", TextLayoutMode.Display)]
    [InlineData("ideal", TextLayoutMode.Ideal)]
    [InlineData("IDEAL", TextLayoutMode.Ideal)]
    public void TheOverrideWinsInBothDirections(string value, TextLayoutMode expected)
    {
        // Both environments, because the escape hatch exists to overrule the default
        // and a hatch that only opens one way is half a hatch.
        Assert.Equal(expected, TextRenderingPolicy.Decide(underWine: true, value));
        Assert.Equal(expected, TextRenderingPolicy.Decide(underWine: false, value));
    }

    /// <summary>A typo must not leave the app with no policy — it falls back to what the
    /// environment would have chosen, rather than to a fixed value that would be wrong
    /// for one of them.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("crisp")]
    [InlineData(null)]
    public void AnUnrecognisedOverrideFallsBackToTheEnvironment(string? value)
    {
        Assert.Equal(TextLayoutMode.Display, TextRenderingPolicy.Decide(true, value));
        Assert.Equal(TextLayoutMode.Ideal, TextRenderingPolicy.Decide(false, value));
    }
}
