using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The Font picker's rule (discussion #1046, DRA-1048): the curated list, the
/// saved-key lookup, and the "a Windows face this PC lacks is the default, SAID" fallback.</summary>
public class AppFontChoiceTests
{
    /// <summary>Pinned: a row added to the picker is a face somebody has to screenshot in a
    /// 10-px chip first, so adding one fails here until this list is edited on purpose.</summary>
    [Fact]
    public void TheCuratedListIsTheFiveReviewedFacesInPickerOrder()
    {
        Assert.Equal(
            ["", "atkinson-hyperlegible-next", "open-sans", "verdana", "tahoma"],
            AppFontChoice.Options.Select(o => o.Key));
        Assert.Equal(
            ["Default (Segoe UI)", "Atkinson Hyperlegible Next", "Open Sans", "Verdana", "Tahoma"],
            AppFontChoice.Options.Select(o => o.Label));
        Assert.Same(AppFontChoice.Default, AppFontChoice.Options[0]);
        Assert.Equal(["atkinson-hyperlegible-next", "open-sans"],
            AppFontChoice.Options.Where(o => o.IsBundled).Select(o => o.Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("comic-sans")]
    [InlineData("Atkinson Hyperlegible Next")] // a LABEL is not a key
    public void BlankAndUnknownKeysAreTheDefault(string? key)
    {
        Assert.Same(AppFontChoice.Default, AppFontChoice.Find(key));
        var r = AppFontChoice.Resolve(key, _ => true);
        Assert.Same(AppFontChoice.Default, r.Drawn);
        Assert.False(r.FellBack);
        Assert.Null(r.Reason);
    }

    [Fact]
    public void AKeyIsMatchedWithoutRegardToCaseOrSurroundingSpace() =>
        Assert.Equal("open-sans", AppFontChoice.Find(" Open-Sans ").Key);

    [Fact]
    public void AnInstalledWindowsFaceIsDrawnAsPicked()
    {
        var r = AppFontChoice.Resolve("verdana", f => f == "Verdana");
        Assert.Equal("verdana", r.Drawn.Key);
        Assert.False(r.FellBack);
        Assert.Null(r.Reason);
    }

    /// <summary>The no-silent-no-op rule: Tahoma missing draws the default AND says so.</summary>
    [Fact]
    public void AnUninstalledWindowsFaceFallsBackToTheDefaultWithTheReason()
    {
        var r = AppFontChoice.Resolve("tahoma", _ => false);
        Assert.Equal("tahoma", r.Picked.Key);
        Assert.Same(AppFontChoice.Default, r.Drawn);
        Assert.True(r.FellBack);
        Assert.Equal("Tahoma is not installed on this PC, so EQBuddy is using Default (Segoe UI).", r.Reason);
    }

    /// <summary>A bundled face is packed in the .exe, so the PC's installed fonts are never
    /// asked about it — a player who has never installed Atkinson still gets it.</summary>
    [Fact]
    public void ABundledFaceNeverAsksWhetherThePcHasIt()
    {
        var asked = new List<string>();
        var r = AppFontChoice.Resolve("atkinson-hyperlegible-next", f => { asked.Add(f); return false; });
        Assert.Equal("atkinson-hyperlegible-next", r.Drawn.Key);
        Assert.Empty(asked);
    }

    [Fact]
    public void EveryChoiceEndsInTheDefaultChainSoIconsStillFallBack()
    {
        Assert.Equal("Segoe UI Variable Text, Segoe UI", AppFontChoice.FamilySource(AppFontChoice.Default));
        Assert.Equal("./Fonts/AtkinsonHyperlegibleNext/#Atkinson Hyperlegible Next, Segoe UI Variable Text, Segoe UI",
            AppFontChoice.FamilySource(AppFontChoice.Find("atkinson-hyperlegible-next")));
        Assert.Equal("Verdana, Segoe UI Variable Text, Segoe UI",
            AppFontChoice.FamilySource(AppFontChoice.Find("verdana")));
        foreach (var o in AppFontChoice.Options)
            Assert.EndsWith(AppFontChoice.DefaultFamilies, AppFontChoice.FamilySource(o));
    }

    /// <summary>The default chain is Theme.xaml's own value — two spellings of "the default
    /// face" would be trap 4.</summary>
    [Fact]
    public void TheDefaultChainIsThemeXamlsOwnValue()
    {
        var theme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "EQBuddy", "Theme.xaml"));
        Assert.Contains($"<FontFamily x:Key=\"AppFontFamily\">{AppFontChoice.DefaultFamilies}</FontFamily>", theme);
    }

    /// <summary>The picker writes the KEY, and an index round-trips through it.</summary>
    [Fact]
    public void TheOptionsPickerStoresTheKeyNotTheLabel()
    {
        var settings = new AppSettings();
        var vm = new OptionsViewModel(settings, () => { });
        Assert.Equal(0, vm.AppFontIndex);
        vm.AppFontIndex = 1;
        Assert.Equal("atkinson-hyperlegible-next", settings.AppFont);
        Assert.Equal(1, vm.AppFontIndex);
        vm.AppFontIndex = 0;
        Assert.Equal("", settings.AppFont);
        settings.AppFont = "nonsense";
        Assert.Equal(0, vm.AppFontIndex);
    }
}
