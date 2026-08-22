namespace EQBuddy.Core;

/// <summary>
/// How much of a theme's tab is drawn when the theme is expanded IN PLACE on the widget,
/// rather than in its window (David, 2026-08-21: *"expandable sub-categories under them
/// with an option to pop out the window"*).
///
/// The widget is `SizeToContent` and sits over a running game, so "show the tab" is not a
/// question with one answer: a rate and four coin lines belong there, and a searchable
/// list of 1,200 quests does not. Bevel ruled the split per tab (Helm-signed 2026-08-22)
/// and the table lives HERE because moving a tab between the two is then one line that
/// both desktops follow — the same reason the tab lists themselves are in Core.
/// </summary>
public enum InlineMode
{
    /// <summary>The tab's real body, drawn inline under its card.</summary>
    Full,

    /// <summary>One line and a ⧉ into the window. For a tab whose body is a list with its
    /// own chrome — Bevel's host rule: *"do not shrink-wrap the full window onto a
    /// SizeToContent always-on-top panel."* A Glance tab never renders its full view, so
    /// the cost of expanding a theme can never be the cost of the window.</summary>
    Glance,
}
