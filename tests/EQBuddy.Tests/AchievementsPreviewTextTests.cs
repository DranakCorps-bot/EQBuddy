using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The achievements preview's action row — #235 (LeBigNasty, 2026-08-23): *"Import
/// achievements button does not function."*
///
/// **It functioned.** His screenshot shows 502 achievements read, 76 Sky rewards
/// recognized, and Apply correctly disabled because every one was already marked. What
/// failed was the telling: the sentence explaining it sat at the top of the panel, 76 rows
/// above a greyed `Apply (0)` at the bottom of a scrolled dialog.
///
/// These pin the words, because the words ARE the fix and nothing else can see them — a
/// disabled button photographs as a disabled button, and no test reads a layout.
/// </summary>
public class AchievementsPreviewTextTests
{
    /// <summary>A count of zero is a number to work out; the disabled state should read as
    /// an answer. "Apply (0)" is what the reporter saw and called broken.</summary>
    [Fact]
    public void TheButtonSaysWhyItCannotBePressed()
    {
        Assert.Equal("Apply (3)", AchievementsPreviewText.ApplyLabel(3));
        Assert.Equal("Nothing to apply", AchievementsPreviewText.ApplyLabel(0));
        Assert.DoesNotContain("(0)", AchievementsPreviewText.ApplyLabel(0));
    }

    /// <summary>
    /// The line beside the buttons exists only when the button is disabled — when there IS
    /// something to apply the count says everything and a second sentence is noise.
    ///
    /// It repeats the top of the panel on purpose: "notifications go where the eye lands"
    /// (trap 44), and the top line is not visible from the bottom of a 76-row dialog.
    /// </summary>
    [Fact]
    public void TheLineBesideTheButtonAppearsOnlyWhenItIsDisabled()
    {
        Assert.Null(AchievementsPreviewText.WhyDisabled(3, 76));

        var why = AchievementsPreviewText.WhyDisabled(0, 76);
        Assert.NotNull(why);
        // It leads by saying the import WORKED — the reporter's conclusion was that it had
        // not, so the first clause is the one that has to answer him.
        Assert.StartsWith("Your import worked", why);
        Assert.Contains("76", why);
    }

    /// <summary>A file that matched nothing is a different state from one whose matches are
    /// all already marked, and saying "all 0 recognized rewards are already marked" would be
    /// nonsense — so it gets its own sentence rather than a plural hack.</summary>
    [Fact]
    public void NothingRecognizedIsItsOwnAnswerRatherThanAZeroCount()
    {
        var why = AchievementsPreviewText.WhyDisabled(0, 0);

        Assert.NotNull(why);
        Assert.DoesNotContain("0 recognized", why);
        Assert.Contains("matched a Sky reward", why);
    }

    /// <summary>Trap 17: say why in the tooltip, because that is where a player who does not
    /// believe the line goes next. Never empty — an empty tooltip on a dead-looking button
    /// is the original complaint with an extra step.</summary>
    [Theory]
    [InlineData(0, 76)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 76)]
    public void TheTooltipAlwaysSaysSomething(int fresh, int recognized)
    {
        var tip = AchievementsPreviewText.ApplyTooltip(fresh, recognized);

        Assert.False(string.IsNullOrWhiteSpace(tip));
        // The enabled case promises what pressing it does, and repeats the add-only rule
        // that the panel states — that is the sentence that makes it safe to press.
        if (fresh > 0) Assert.Contains("ADDS", tip);
    }

    [Fact]
    public void OneRewardReadsAsOneRatherThanAsAPlural()
    {
        Assert.Contains("1 Sky reward ", AchievementsPreviewText.ApplyTooltip(1, 1));
        Assert.Contains("2 Sky rewards", AchievementsPreviewText.ApplyTooltip(2, 2));
    }
}
