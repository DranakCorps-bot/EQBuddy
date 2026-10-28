using System.Text.Json;
using System.Text.Json.Serialization;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-3202, discussion #1067. The hint used to say "drag its edge" on a click-through
/// window. The size is now Options → Look → Ring size, and a hand-edited
/// <c>settings.json</c> is clamped to the same range the slider uses.
/// </summary>
public class CursorRingSizeTests
{
    [Theory]
    [InlineData(46, 46)]
    [InlineData(20, 20)]
    [InlineData(160, 160)]
    [InlineData(19.9, 20)]
    [InlineData(0, 20)]
    [InlineData(-4, 20)]
    [InlineData(160.1, 160)]
    [InlineData(999, 160)]
    public void ClampKeepsADrawableDiameter(double value, double expected) =>
        Assert.Equal(expected, CursorRingSizeLimits.Clamp(value));

    [Fact]
    public void NanBecomesTheDefaultRatherThanAWindowSize() =>
        Assert.Equal(CursorRingSizeLimits.Default, CursorRingSizeLimits.Clamp(double.NaN));

    [Fact]
    public void InfinityClampsToTheEnds()
    {
        Assert.Equal(CursorRingSizeLimits.Max, CursorRingSizeLimits.Clamp(double.PositiveInfinity));
        Assert.Equal(CursorRingSizeLimits.Min, CursorRingSizeLimits.Clamp(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(46, false)]
    [InlineData(20, false)]
    [InlineData(160, false)]
    [InlineData(120, false)]
    [InlineData(4, true)]
    [InlineData(999, true)]
    public void TryNormalizeChangesOnlyAnOutOfRangeValue(double value, bool changed)
    {
        var size = value;
        Assert.Equal(changed, CursorRingSizeLimits.TryNormalize(ref size));
        Assert.Equal(CursorRingSizeLimits.Clamp(value), size);
    }

    [Fact]
    public void TryNormalizeReplacesNan()
    {
        var size = double.NaN;
        Assert.True(CursorRingSizeLimits.TryNormalize(ref size));
        Assert.Equal(CursorRingSizeLimits.Default, size);
    }

    /// <summary>The file a player hand-edits is ordinary JSON. Deserializing it must not
    /// throw, and the value Load writes back is the clamped one.</summary>
    [Theory]
    [InlineData("4", 20)]
    [InlineData("999", 160)]
    [InlineData("46", 46)]
    [InlineData("120", 120)]
    public void AHandEditedSettingsValueRoundTripsClamped(string raw, double expected)
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>(
            $$"""{"CursorRingSize": {{raw}}} """)!;
        var size = loaded.CursorRingSize;
        CursorRingSizeLimits.TryNormalize(ref size);
        loaded.CursorRingSize = size;

        Assert.Equal(expected, loaded.CursorRingSize);
        // The same allowance Save() uses: an unplaced window is NaN, and the default
        // serializer refuses that. Named literals are how the file actually round-trips.
        var opts = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        var again = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(loaded, opts), opts)!;
        Assert.Equal(expected, again.CursorRingSize);
    }

    [Fact]
    public void LoadAppliesTheClampBeforeAnythingDrawsTheRing()
    {
        var src = File.ReadAllText(Path.Combine(SettingsProseSource.Src, "EQBuddy.Core", "AppSettings.cs"));
        Assert.Contains("CursorRingSizeLimits.TryNormalize(ref ring)", src, StringComparison.Ordinal);
        Assert.Contains("settings.CursorRingSize = ring", src, StringComparison.Ordinal);
        Assert.Contains("ringAdjusted", src, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHintNamesRingSizeAndDoesNotSayDrag()
    {
        var look = SettingsProseSource.Block("SettingsLookView.cs");
        Assert.DoesNotContain("Drag its edge", look, StringComparison.Ordinal);
        var hint = SettingsProseSource.PrintedLiterals(look)
            .Single(p => p.StartsWith("A soft ring", StringComparison.Ordinal));
        Assert.Contains("Ring size", hint, StringComparison.Ordinal);
        Assert.False(SettingsProsePolicy.BelongsOnHover(hint),
            $"the hint is {SettingsProsePolicy.Words(hint)} words, over the body ceiling, so the "
            + "prose sweep will demand a hover ruling. Shorten it instead.");
        Assert.Contains("_main.Settings.CursorRingSize = _cursorRingSlider.Value", look,
            StringComparison.Ordinal);
        Assert.Contains("_main.RefreshCursorRing()", look, StringComparison.Ordinal);
        Assert.Contains("CursorRingSizeLimits.Clamp", File.ReadAllText(Path.Combine(
            SettingsProseSource.Src, "EQBuddy", "CursorRingWindow.cs")), StringComparison.Ordinal);
    }
}
