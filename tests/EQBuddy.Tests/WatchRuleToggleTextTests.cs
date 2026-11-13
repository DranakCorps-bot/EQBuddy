using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **THE PIN AND THE BELL NAME DIFFERENT CHIPS** (#1104, Flaamwing).
///
/// A watch rule draws two chips. The pin is the count that stays on the minimised bar
/// (<c>TrackedRule.Pinned</c>). The bell is the banner and the chip that lingers on the HUD
/// chip row (<c>TrackedRule.AlertBanner</c>, <see cref="WatchFireLedger.Linger"/>). The pin's
/// old hover — "Show this rule as a chip on the HUD" — named the second chip as if it were
/// the first, and a player with the pin off and the bell on read the lingering chip as the
/// pin not working. Our own reply on #1104 made the same misreading.
///
/// The WPF row has no unit tests (docs/TestPlan.md §5). What a test can hold is the words,
/// and that <c>SettingsAlertsView</c> — the one row both Options and the shell build — is
/// the only caller.
/// </summary>
public sealed class WatchRuleToggleTextTests
{
    /// <summary>The pin is the count on the bar. It must not go back to claiming a chip on
    /// the HUD, which is the sentence that made the two chips look like one.</summary>
    [Fact]
    public void ThePinNamesTheCountOnTheBarAndDoesNotClaimEveryHudChip()
    {
        Assert.Contains("count", WatchRuleToggleText.PinTip, StringComparison.Ordinal);
        Assert.Contains("minimised bar", WatchRuleToggleText.PinTip, StringComparison.Ordinal);
        Assert.DoesNotContain("HUD", WatchRuleToggleText.PinTip, StringComparison.Ordinal);
        Assert.DoesNotContain("every chip", WatchRuleToggleText.PinTip, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("all chips", WatchRuleToggleText.PinTip, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The bell is the banner and the lingering chip, and it says how to turn that
    /// chip off for every rule. The seconds and the family name are the producers the row
    /// already uses, so a linger change or a rename cannot leave the hover behind.</summary>
    [Fact]
    public void TheBellNamesTheBannerAndTheLingeringChip()
    {
        var tip = WatchRuleToggleText.BellTip;
        var seconds = ((int)WatchFireLedger.Linger.TotalSeconds).ToString();

        Assert.Contains("chip", tip, StringComparison.Ordinal);
        Assert.Contains("banner", tip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(seconds, tip, StringComparison.Ordinal);
        Assert.Contains("pencil", tip, StringComparison.Ordinal);
        Assert.Contains("Edit HUD", tip, StringComparison.Ordinal);
        Assert.Contains(HudChipRow.Label(HudChipFamily.WatchFire), tip, StringComparison.Ordinal);
        Assert.Contains("every rule", tip, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The old sentences may not come back**, in this file or in any other under src.
    /// A hover that says them again is the #1104 misreading, whether or not the new
    /// constants are still sitting unused beside it.
    /// </summary>
    [Fact]
    public void TheOldSentencesAreGoneFromSrc()
    {
        var offenders = SrcFiles()
            .Where(f => File.ReadAllText(f).Contains("Show this rule as a chip on the HUD", StringComparison.Ordinal)
                     || File.ReadAllText(f).Contains("Banner alert on match", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Those hovers are the #1104 misreading. They are back in: " + string.Join(", ", offenders));
    }

    /// <summary>One producer, one caller. Options and the shell's Alerts tab both build
    /// <c>SettingsAlertsView</c>; a second file repeating the sentence is the copy the
    /// card refused.</summary>
    [Fact]
    public void SettingsAlertsViewIsTheOnlyCaller()
    {
        var callers = SrcFiles()
            .Where(f => File.ReadAllText(f).Contains("WatchRuleToggleText", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "SettingsAlertsView.cs", "WatchRuleToggleText.cs" }, callers);
        var view = File.ReadAllText(Path.Combine(SrcDir(), "EQBuddy", "SettingsAlertsView.cs"));
        Assert.Contains("WatchRuleToggleText.PinTip", view, StringComparison.Ordinal);
        Assert.Contains("WatchRuleToggleText.BellTip", view, StringComparison.Ordinal);
    }

    /// <summary>The release being prepared tells the player, and credits the reporter.
    /// 2.0.9 is the unreleased top entry (v2.0.9 is not tagged); the note lives there.</summary>
    [Fact]
    public void TheUnreleasedNotesCreditFlaamwing()
    {
        var entry = WhatsNewCatalog.Load()[0];
        Assert.Equal("2.0.9", entry.Version);
        Assert.Contains(entry.Highlights, h =>
            h.Contains("Flaamwing", StringComparison.Ordinal)
            && h.Contains("#1104", StringComparison.Ordinal)
            && h.Contains("pin", StringComparison.OrdinalIgnoreCase)
            && h.Contains("bell", StringComparison.OrdinalIgnoreCase));
    }

    private static string SrcDir() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static IEnumerable<string> SrcFiles() =>
        Directory.EnumerateFiles(SrcDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
