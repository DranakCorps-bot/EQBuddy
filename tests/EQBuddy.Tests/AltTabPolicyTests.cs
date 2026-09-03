using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// "Keep EQBuddy out of Alt+Tab" (Hateborne, 2026-08-25), and the two honesty rules around it.
///
/// The setting is one Windows flag with two effects, and both of them matter to a player:
/// it leaves the switcher AND it leaves the taskbar. There is no way to have one without
/// the other, so the only honest design is to say so where the choice is made.
/// </summary>
public class AltTabPolicyTests
{
    [Fact]
    public void OnlyWindowsHasAPerWindowOptOut()
    {
        Assert.Equal(OperatingSystem.IsWindows(), AltTabPolicy.Available);
    }

    /// <summary>
    /// The #169 rule, applied to a second setting: a platform that cannot honour a
    /// tick-box gets a sentence naming the reason. A box that saves a choice and does
    /// nothing is the silent no-op CLAUDE.md treats as broken — and it is worse than a
    /// missing feature, because the player believes they have turned something on.
    /// </summary>
    [Fact]
    public void ThePlatformThatCannotDoItSaysSoAndTheOneThatCanStaysQuiet()
    {
        if (AltTabPolicy.Available)
        {
            Assert.Equal("", AltTabPolicy.UnavailableNote);
        }
        else
        {
            Assert.NotEqual("", AltTabPolicy.UnavailableNote);
            // Names the reason rather than just refusing — the difference between a
            // player closing Options and a player spending an evening on it.
            Assert.Contains("switcher", AltTabPolicy.UnavailableNote);
            Assert.Contains("saved", AltTabPolicy.UnavailableNote);
        }
    }

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
