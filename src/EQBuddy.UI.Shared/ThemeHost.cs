namespace EQBuddy.UI.Shared;

/// <summary>Where a theme's body is being drawn right now.</summary>
public enum ThemePlacement
{
    /// <summary>Neither: the launcher is a one-line glance and nothing else.</summary>
    Collapsed,

    /// <summary>Expanded under its card on the widget.</summary>
    Inline,

    /// <summary>In its own window, for the second monitor. The card is collapsed.</summary>
    Window,
}

/// <summary>
/// One theme's placement, as a state machine with one invariant: **exactly one owner of
/// the body.** Inline themes (David, 2026-08-21; Bevel's ruling and host rules, Helm-signed
/// 2026-08-22).
///
/// **This is not a tidiness rule, it is what keeps the app running on Linux and macOS.**
/// The two UIs own theme bodies differently: WPF builds a fresh instance per host, so it
/// COULD draw the same surface in the card and the window at once and merely look wrong.
/// Avalonia builds each body once on the widget and the window borrows it — and a control
/// has one visual parent, so the same thing THROWS. A rule that only one of the two can
/// hold the body is therefore the difference between a layout bug and a crash, which is
/// why it lives here as a tested decision rather than in each window's click handler.
///
/// Framework-free and unit-tested for the reason CLAUDE.md gives for every window sum: the
/// WPF layer has no unit tests at all (docs/TestPlan.md §5), so a transition that is only
/// expressed in a click handler is a transition nothing can check.
/// </summary>
/// <typeparam name="TTab">The theme's own tab enum — <c>ProgressTab</c> and friends. The
/// host keeps the selected tab so the card and the window hand it back and forth, and it
/// is deliberately SESSION-only: a player who expands a theme tomorrow gets the default
/// room, not wherever they happened to leave it.</typeparam>
public sealed class ThemeHost<TTab> where TTab : struct, Enum
{
    private readonly TTab _defaultTab;

    public ThemeHost(TTab defaultTab)
    {
        _defaultTab = defaultTab;
        SelectedTab = defaultTab;
    }

    public ThemePlacement Placement { get; private set; } = ThemePlacement.Collapsed;

    /// <summary>The room the next expand (or pop-out) opens on.</summary>
    public TTab SelectedTab { get; private set; }

    public bool IsInline => Placement == ThemePlacement.Inline;
    public bool IsWindowOpen => Placement == ThemePlacement.Window;

    /// <summary>True when the caller must bring the existing window forward instead of
    /// drawing anything — the answer to "they clicked the card while the window is up".</summary>
    public bool ShouldBringWindowForward { get; private set; }

    public void SelectTab(TTab tab) => SelectedTab = tab;

    /// <summary>The card's own header was clicked.
    ///
    /// From <see cref="ThemePlacement.Window"/> this does NOT expand the card: it asks the
    /// caller to bring the window forward. Drawing the body in both places is the crash
    /// above on Avalonia, and on WPF it is trap 15 — a surface visible in two hosts where
    /// only one of them owns its state.</summary>
    public void ToggleCard()
    {
        ShouldBringWindowForward = false;
        switch (Placement)
        {
            case ThemePlacement.Collapsed:
                Placement = ThemePlacement.Inline;
                break;
            case ThemePlacement.Inline:
                Placement = ThemePlacement.Collapsed;
                break;
            case ThemePlacement.Window:
                ShouldBringWindowForward = true;
                break;
        }
    }

    /// <summary>The ⧉ on an expanded card: the window takes the body and the card
    /// collapses, so the player never has two of the same surface on screen.</summary>
    public void PopOut()
    {
        Placement = ThemePlacement.Window;
        ShouldBringWindowForward = false;
    }

    /// <summary>An opener that is not the card — the ⚙ menu, a hotkey, an
    /// <c>EQBUDDY_*</c> environment variable. Same destination as <see cref="PopOut"/>,
    /// and it may name the room to land on.</summary>
    public void OpenWindow(TTab? tab = null)
    {
        if (tab is { } t) SelectedTab = t;
        Placement = ThemePlacement.Window;
        ShouldBringWindowForward = false;
    }

    /// <summary>The window closed. **Collapsed, never back to Inline** — the player closed
    /// a thing, and re-growing the widget in its place is the opposite of what they asked
    /// for. The tab they were on is kept, so the next expand opens where they left off.</summary>
    public void WindowClosed()
    {
        Placement = ThemePlacement.Collapsed;
        ShouldBringWindowForward = false;
    }

    /// <summary>Back to first-run: collapsed, on the default room. For a profile reset or
    /// a character switch, where "where they left off" is about someone else.</summary>
    public void Reset()
    {
        Placement = ThemePlacement.Collapsed;
        SelectedTab = _defaultTab;
        ShouldBringWindowForward = false;
    }
}
