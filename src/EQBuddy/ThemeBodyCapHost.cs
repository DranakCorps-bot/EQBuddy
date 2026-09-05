using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// How tall ONE expanded theme card's body may be on this widget, measured from the card
/// stack (#250, Paineless: <i>"cannot just expand window size"</i>).
///
/// Lifted out of <c>MainWindow</c> in the same commit that added it, for the reason the
/// hotspot ratchet exists and <c>WidgetDump</c> already demonstrates: this is a SUM over
/// controls, and CLAUDE.md's standing move for a sum is to get it out of the window. The
/// arithmetic itself is one step further out again, in <see cref="WidgetMetrics"/>, where
/// it is unit-tested without a toolkit; what is left here is the measuring, which needs
/// real controls and therefore cannot be.
///
/// It reads <c>MainWindow</c>'s internals exactly as <c>WidgetDump</c> does. If it ever
/// starts needing a DECISION rather than a measurement, that decision belongs in
/// <c>UI.Shared</c> beside the formula.
/// </summary>
internal static class ThemeBodyCapHost
{
    /// <summary>
    /// The cap for one card's body, in pre-scale units.
    ///
    /// The widget owns this rather than the card because only the widget can see the rest
    /// of the stack; the card owns the one measurement the widget cannot reach, and hands
    /// it over as <paramref name="askingCardChromeExcludingBody"/> — everything that card
    /// occupies except the body being capped.
    ///
    /// **Every other visible card contributes its HEADER only** (Bevel, 2026-08-31). A
    /// second open card is the player asking for two open cards, not a reason to shrink
    /// this one, and <c>MainWindow.ApplySectionMaxHeight</c> still arbitrates by scrolling
    /// the stack. Note the units: <c>ContentHeight</c> and every measurement here are
    /// pre-scale, because the whole stack is under the UI-scale transform — no trap-1
    /// conversion belongs in this file, and one appearing would be the bug.
    /// </summary>
    public static double CapFor(MainWindow w, Expander askingCard, double askingCardChromeExcludingBody) =>
        WidgetMetrics.ThemeBodyCap(RoomFor(w), ChromeFor(w, askingCard, askingCardChromeExcludingBody));

    /// <summary>
    /// The first of <see cref="CapFor"/>'s two inputs, on its own so the
    /// <c>EQBUDDY_EXPAND</c> dump can report what the arithmetic was actually fed.
    ///
    /// **The E2E suite needs the INPUTS, not only the answer.** `themeBodyCap` alone can
    /// only be checked against a constant, and the constant is a claim about the MONITOR:
    /// the room below is clamped to the work area, so on a 1024x768 hosted runner a
    /// 4000-unit drag yields the floor and "the body grew" is untestable there. With the
    /// inputs in the dump a test asserts the whole relationship — cap == ThemeBodyCap(room,
    /// chrome) against the control's real MaxHeight — which is exactly as strong on a
    /// short screen as on a tall one, and is the "reaches the control" claim of trap 42.
    /// </summary>
    public static double RoomFor(MainWindow w) =>
            // The height the player asked for, AS THE MONITOR GRANTED IT.
            // ContentHeight is the raw drag and the stack does not necessarily get it —
            // ApplySectionMaxHeight clamps it to the work area, hardest at a UI scale
            // above 100%, where a 900-unit drag on a 1032px work area becomes 698. Handing
            // the RAW number over would let one body claim room the stack was never given,
            // which is trap 1's family: two numbers that agree at the default and diverge
            // exactly where nobody looks. NaN still means "never dragged" and still answers
            // the floor, so it is read from the setting rather than from the clamp.
            //
            // RECOMPUTED from the same tested helper rather than read back off
            // SectionScroll.MaxHeight: reading the control would make this answer depend
            // on whether ApplySectionMaxHeight had run yet, which is a second writer
            // deciding one value by ordering (trap 33). The helper is pure, so there is no
            // order to get wrong.
            double.IsNaN(w._settings.ContentHeight)
                ? double.NaN
                : WidgetMetrics.SectionMaxHeight(
                    w._sectionAutoCap, w._settings.ContentHeight, w._settings.UiScale);

    /// <summary>The second input: everything in the stack that is not the body being
    /// capped. Split out for the same reason as <see cref="RoomFor"/> — one producer, read
    /// by the cap and by the dump, never re-derived by either.</summary>
    public static double ChromeFor(MainWindow w, Expander askingCard, double askingCardChromeExcludingBody) =>
            WidgetMetrics.ThemeBodyChrome(
                w.SectionsPanel.Children.OfType<FrameworkElement>()
                    .Where(card => card.Visibility == Visibility.Visible)
                    .Select(card => ReferenceEquals(card, askingCard)
                        ? askingCardChromeExcludingBody
                        : HeaderExtentOf(card)));

    /// <summary>What a card costs the stack when only its header is counted. A collapsed
    /// card is ALL header, so its measured height is the answer; an expanded one is
    /// measured at the header <c>ToggleButton</c> the Section template puts its Header
    /// inside.
    ///
    /// Walked up from the Header content this app owns rather than looked up by a template
    /// part NAME — a name is a string that can go stale silently. The fallback (count the
    /// whole card) is the conservative direction: it can only ever make the open body
    /// smaller, never larger than the room there actually is.</summary>
    private static double HeaderExtentOf(FrameworkElement card)
    {
        var margins = card.Margin.Top + card.Margin.Bottom;
        if (card is not Expander { IsExpanded: true, Header: FrameworkElement header })
            return card.ActualHeight + margins;
        for (DependencyObject? d = header; d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is System.Windows.Controls.Primitives.ToggleButton toggle)
                return toggle.ActualHeight + margins;
        return card.ActualHeight + margins;
    }

    /// <summary>True while any expanded theme card's body is holding back rows the player
    /// could reach by dragging the widget taller — what the height grip's tooltip asks
    /// before it promises anything (#250).</summary>
    public static bool AnyBodyIsCapped(MainWindow w) =>
        (w._progressCard?.BodyIsCapped ?? false) || (w._killsCard?.BodyIsCapped ?? false)
        // Quests is not in this list because it has no card since 2026-09-05 (HUD
        // subtraction cut 1) — there is no inline body of its to cap.
        || (w._lootCard?.BodyIsCapped ?? false)
        || (w._worldCard?.BodyIsCapped ?? false);
}
