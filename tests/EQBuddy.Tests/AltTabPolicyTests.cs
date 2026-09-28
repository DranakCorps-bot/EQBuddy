using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// "Keep EQBuddy out of Alt+Tab" (Hateborne, 2026-08-25), and the honesty rule around it.
///
/// The setting is one Windows flag with two effects, and both of them matter to a player:
/// it leaves the switcher AND it leaves the taskbar. There is no way to have one without
/// the other, so the only honest design is to say so where the choice is made. (The
/// macOS/Linux "not available here" note and its two tests left with those lanes.)
/// </summary>
public class AltTabPolicyTests
{
    /// <summary>
    /// The cost is not a footnote. WS_EX_TOOLWINDOW removes the taskbar button in the
    /// same stroke, so a widget that is also hidden by the focus-hide settings has
    /// exactly one way back: the tray icon. Both facts have to be in the sentence, or
    /// this setting can strand someone.
    /// </summary>
    [Fact]
    public void TheWarningNamesBothTheTaskbarAndTheWayBack()
    {
        Assert.Contains("taskbar", AltTabPolicy.TaskbarWarning);
        Assert.Contains("tray icon", AltTabPolicy.TaskbarWarning);
    }

    /// <summary>
    /// The behaviour finally matches the warning (Hateborne, 2026-09-03): hiding from
    /// Alt+Tab takes the main window's taskbar button, because ShowInTaskbar=true is
    /// asserted as WS_EX_APPWINDOW and APPWINDOW overrides TOOLWINDOW for switcher
    /// membership. For a week the warning promised a cost the feature never charged —
    /// and the switcher exclusion it was the price OF never happened either.
    /// </summary>
    [Fact]
    public void HidingFromAltTabIsExactlyWhatCostsTheTaskbarButton()
    {
        Assert.False(AltTabPolicy.MainWindowShowsInTaskbar(hideFromAltTab: true));
        Assert.True(AltTabPolicy.MainWindowShowsInTaskbar(hideFromAltTab: false));
    }
}
