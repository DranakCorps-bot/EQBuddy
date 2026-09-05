using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A window that appears WITHOUT the player asking for it must never take the foreground.
///
/// #207 (bjstrange): the mez chips and the spawn chips sometimes stole focus from
/// EverQuest. Not intermittent and not mysterious — those two were the only surfaces in
/// the WPF build that appear on their own and never declared `ShowActivated="False"`.
/// Their Avalonia twins already had it, so Windows was the lane behind for once.
///
/// **The rule is about WHO OPENED IT, not about window properties.** The first version of
/// this test inferred "overlay" from `Topmost` + `ShowInTaskbar="False"` and immediately
/// flagged the Feedback, Options, What's-new, Tutorial and item-info windows — every one
/// of which the player opens on purpose and several of which contain a text box. A
/// feedback form that cannot take focus is broken, not safe. So the surfaces are named
/// here rather than detected: a timer or a log line puts these on screen while someone is
/// playing, and stealing the keyboard mid-fight is the one thing they must not do.
///
/// A missing attribute is invisible in review precisely because it is an ABSENCE. Naming
/// the list makes adding to it a decision someone has to make on purpose.
/// </summary>
public class OverlayActivationTests
{
    private static string Src =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src"));

    /// <summary>The surfaces that appear on a timer or a log event — nobody clicked
    /// anything to summon these.
    ///
    /// **Seven Avalonia rows went with the platform in E-2 (2026-09-04).** The lane split
    /// was the old second reason (#122/#152: Windows pays, Linux inherits three releases
    /// later); the first reason is untouched and is the one that matters — a window that
    /// takes the foreground while the player is fighting steals their keyboard, and every
    /// row below is a surface that opens without anybody asking.</summary>
    public static TheoryData<string> UnpromptedSurfaces =>
    [
        // The one chip row, which the two chip stacks folded into (Surface A / SA-2).
        // Built in code rather than XAML, so the scan reads the .cs — the attribute it
        // checks for is ShowActivated = false either way.
        "EQBuddy/HudChipRowWindow.cs",
        "EQBuddy/AlertWindow.xaml",
        "EQBuddy/ClickThroughChip.cs",
        "EQBuddy/CursorRingWindow.cs",
        "EQBuddy/GridOverlayWindow.cs",
        // The breakouts pop on the minimize pass — the star was clicked minutes ago,
        // the show happens mid-fight.
        "EQBuddy/BreakoutWindow.xaml",
    ];

    [Theory]
    [MemberData(nameof(UnpromptedSurfaces))]
    public void AWindowNobodyAskedForNeverStealsTheForeground(string relativePath)
    {
        var path = Path.Combine(Src, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path),
            $"{relativePath} is on the unprompted-surface list and is not there any more. "
            + "If it was renamed, rename it here; if it was deleted, delete it here — a "
            + "list that silently stops matching guards nothing.");

        var text = File.ReadAllText(path);
        Assert.True(
            text.Contains("ShowActivated=\"False\"") || text.Contains("ShowActivated = false"),
            $"{relativePath} appears on a timer or a log event while the player is in "
            + "combat, but does not declare ShowActivated false — showing it takes the "
            + "keyboard away from EverQuest (#207, bjstrange). Add ShowActivated=\"False\" "
            + "(XAML) or ShowActivated = false (code-behind).");
    }
}
