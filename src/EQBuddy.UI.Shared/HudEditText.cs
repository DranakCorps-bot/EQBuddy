namespace EQBuddy.UI.Shared;

/// <summary>
/// **THE WORDS EDIT HUD USES TO SAY HOW TO LEAVE IT** — and the reason they are here rather
/// than inline in the WPF chicklet that draws them.
///
/// Bevel's cog/Options IA faces §C (Helm-signed 2026-09-08). The finding was a mode with an
/// asymmetric door: entering it took a right-click and a menu row, and LEAVING it took the
/// same right-click and the same row a second time — so the sentence on the row had to say
/// *"choose Edit HUD… again when you are done"*, which is a piece of copy apologising for a
/// missing control. Edit mode now has a **Done** chicklet of its own, **Esc** does the same,
/// and the pencil on the expanded widget still toggles.
///
/// **The copy is in UI.Shared because it is the only part of this a test can hold.** The WPF
/// layer has no unit tests (docs/TestPlan.md §5): the chicklet, the key handler and the
/// pencil are all E2E or nothing. What a unit test CAN do is refuse the regression by name —
/// a hint that goes back to naming the menu row as the only way out is the defect returning,
/// and <c>HudEditTextTests</c> fails on it.
/// </summary>
public static class HudEditText
{
    /// <summary>The one-line instruction at the end of the edit strip. Edit mode has no
    /// chrome of its own — it IS the row, in place — so the sentence that says how to leave
    /// has to be on the row, or the mode is a state a player can enter and not obviously
    /// exit. It names both ways out and no longer names the menu row at all.</summary>
    public const string Hint =
        "Editing the HUD row — the arrows move a kind of chip up or down the stack, the "
        + "tick puts it on or off. \"Stack grows\" turns the whole column around. Sounds "
        + "and alerts are not affected. Choose Done, or press Esc, when you are finished.";

    /// <summary>The exit chicklet's caption. One word, the same word the hint names.</summary>
    public const string DoneLabel = "Done";

    /// <summary>The exit chicklet's hover. It names the other two ways out, because a
    /// control that is the ONLY way out of a mode is a control a player has to find, and
    /// the pencil they entered by is still sitting on the widget.</summary>
    public const string DoneTip =
        "Finish editing the HUD row. Esc does the same, and so does the pencil on EQBuddy.";

    /// <summary>The expanded widget's persistent Edit control — the ≤1-click way IN, which
    /// is the other half of §C. It says what the mode does, not what the button is.</summary>
    public const string EnterTip =
        "Edit HUD — reorder the chip row, mute a whole kind of chip, or bring a row you "
        + "have dragged away back under EQBuddy. Sounds and alerts are not affected; those "
        + "stay in Options → Alerts & chips.";
}
