namespace EQBuddy.UI.Shared;

/// <summary>
/// What the widget's bottom-edge height grip promises, in the state it is actually in.
///
/// **The tooltip has to be true, because it is the only thing that says what the control
/// does.** David's 1.66.1 retest is why it branches at all: with every card already
/// visible, dragging DOWN was a no-op, and a control that silently does nothing reads as
/// broken. So the line said "the widget is sizing itself automatically" instead of
/// offering something that would not happen.
///
/// **That branch stopped being true on 2026-08-31**, and it would have started lying in
/// exactly the case #250 (Paineless) reported: with a theme card expanded and its body at
/// <see cref="WidgetMetrics.ThemeBodyMaxHeight"/>, dragging down now buys the open room
/// more of itself even when no card is hidden. A stale "nothing to gain here" is the same
/// defect as the silent no-op it was written to prevent, with the switch on the other side
/// — so the sentence moved out of both windows and into one tested place.
///
/// Both widgets read this. The strings are identical per lane by construction rather than
/// by two people keeping two literals level, which is how #122 and #152 reached Linux.
/// </summary>
public static class HeightGripTip
{
    /// <summary>The double-click/double-tap half, which never changes — passed in because
    /// the gesture's NAME differs by toolkit and nothing else does.</summary>
    private const string DragUp = "drag up to shorten.";

    /// <param name="listIsScrolling">The card stack is taller than its viewport, so there
    /// are cards below the fold that dragging down would reveal.</param>
    /// <param name="anExpandedBodyIsCapped">A theme card is open and its body is being held
    /// at its cap — so dragging down gives that room more of itself, whether or not any
    /// card is hidden.</param>
    /// <param name="resetGesture">"Double-click" on WPF, "Double-tap" on the touch-first
    /// lane. The only thing about this sentence that is a toolkit's business.</param>
    public static string For(bool listIsScrolling, bool anExpandedBodyIsCapped, string resetGesture)
    {
        var reset = $" {resetGesture}: back to automatic.";

        // Both true: say the two different things a drag buys, because they are different
        // wins and a player wanting one should not have to guess the other exists.
        if (listIsScrolling && anExpandedBodyIsCapped)
            return "Drag down to show more cards and to give the open card's list more room "
                   + $"(both are cut off right now); {DragUp}{reset}";

        if (listIsScrolling)
            return $"Drag down to show more cards (the list is scrolling); {DragUp}{reset}";

        // The case the old line got wrong. Every card is visible, so "more cards" would be
        // an empty promise — but the open card's list is NOT all there, and that is worth
        // naming rather than telling the player the widget has already given them
        // everything.
        if (anExpandedBodyIsCapped)
            return "Drag down to give the open card's list more room (it is cut off right now); "
                   + $"{DragUp}{reset}";

        return "The widget is sizing itself automatically — everything you've selected in "
               + "Options is shown. Drag up if you'd rather have it shorter (the list "
               + $"scrolls);{reset}";
    }
}
