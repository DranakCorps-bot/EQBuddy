using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// ONE FAMILY'S EDIT CHICKLET — the Place and Mute affordances for a single chip family,
/// drawn in the shape of the chicklet it stands for (Surface A / SA-4).
///
/// <code>  ◀  (timer) Spawn timers  ✓  ▶  </code>
///
/// **Why a placeholder per FAMILY rather than affordances on the live chips.** The verbs
/// are per family (B3 §3, Helm-signed), and a family with nothing running right now has no
/// live chip to hang them on — so the two families a player most wants to mute, the ones
/// that keep interrupting, would be un-editable at exactly the moment they are quiet. Edit
/// mode shows all four, always, in the stored order. That is <c>AlertWindow</c>'s
/// <c>EnterPlacement</c> shape: the tile draws itself as a placeholder while the mode is on,
/// and goes back to its live self when it is off.
///
/// **The geometry is <see cref="HudChip"/>'s**, deliberately — same corner radius, same
/// padding, same 3-unit gap — so the edit row is visibly the row being edited rather than a
/// dialog about it. The border is the ACCENT rather than the hairline, which is the one
/// difference and the thing that says the mode is on.
/// </summary>
internal static class HudEditChip
{
    /// <param name="canLeft">False at the left end of the row: the ◀ is drawn disabled
    /// rather than silently swallowing the click. <c>IsEnabled</c> alone is invisible in
    /// this app's styles (trap 17), so the ink is dimmed with it.</param>
    /// <param name="onNudge">-1 / +1, applied to the stored order.</param>
    /// <param name="onMute">Toggle this family's mute.</param>
    public static Border Build(HudChipFamily family, bool muted, bool canLeft, bool canRight,
        Action<int> onNudge, Action onMute)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Nudge("ChevronLeft",
            $"Move {HudChipRow.Label(family)} left on the row", canLeft, () => onNudge(-1)));

        var kind = DesignSystem.Icon(HudChipRow.Emblem(family),
            muted ? "DimBrush" : "TextBrush", size: Tok.IconInline);
        kind.Margin = new Thickness(Tok.SpaceXs, 0, Tok.SpaceXs, 0);
        kind.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(kind);

        var name = new TextBlock
        {
            Text = HudChipRow.Label(family),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Tok.SpaceXs, 0),
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, muted ? "DimBrush" : "TextBrush");
        row.Children.Add(name);

        // A vector, not a glyph, and a real InlineIconButton rather than a click-handled
        // Path: the drawn strokes only receive a click where they are PAINTED (#211), and
        // this is the control the whole mode exists for.
        //
        // **A TICK, NOT A BELL, and that is a correctness choice rather than a taste one.**
        // Bell/BellOff is this app's SOUND vocabulary, and this mute does not touch sound at
        // all — a muted family keeps every alert, spoken line and banner it had. A bell here
        // would be a control that says one thing and does another, with a tooltip left to
        // contradict it; the tick says presence, which is the whole of what the verb does.
        // (It also collides: the watch-fire family's own emblem IS the bell, so the two
        // vectors on that chicklet would have been the same shape — #148/#166's failure.)
        row.Children.Add(DesignSystem.InlineIconButton(
            muted ? "Close" : "Check",
            muted
                ? $"{HudChipRow.Label(family)} are muted — click to put them back on the row. "
                  + "Their sounds, spoken alerts and banners were never affected."
                : $"Mute {HudChipRow.Label(family)} — they leave the row and nothing else "
                  + "changes: sounds, spoken alerts and banners keep their own settings in "
                  + "Options → Alerts & chips.",
            (_, _) => onMute(), muted ? "WarnBrush" : "AccentBrush"));

        row.Children.Add(Nudge("ChevronRight",
            $"Move {HudChipRow.Label(family)} right on the row", canRight, () => onNudge(+1)));

        var border = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(4, 3, 4, 4),
            Margin = new Thickness(0, 0, 3, 0),
            BorderThickness = new Thickness(1),
            Tag = family,
        };
        border.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        return border;
    }

    /// <summary>
    /// "FOLLOW THE HUD AGAIN" (OE-8) — the way back from a free-drag, in the shape of the
    /// chicklets beside it.
    ///
    /// **Drawn even when nothing is parked**, dimmed and disabled with the tooltip saying so.
    /// The alternative — appear only once a window is loose — is a control whose first
    /// appearance is at the moment a player has already lost something, on the one surface
    /// that is supposed to be the answer to "what did I do to my row". <c>IsEnabled</c> alone
    /// is invisible in this app's styles, so the ink is dimmed with it (trap 17).
    ///
    /// **<c>Pin</c> as the emblem, <c>Undo</c> as the control**, and they are deliberately
    /// two different vectors: the emblem says what the STATE is (something is pinned to a
    /// spot on the screen) and the button says what the CLICK does. Two identical shapes on
    /// one chicklet is #148/#166's three-identical-boxes failure, which is also why neither
    /// borrows the mute toggle's <c>Check</c>/<c>Close</c> or the order nudges' chevrons —
    /// every one of those is already a live verb on this same row.
    /// </summary>
    /// <param name="parked">Anything is parked — the row, the under-bar panel, or both.</param>
    public static Border Unpark(bool parked, Action onUnpark)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = DesignSystem.Icon("Pin", parked ? "TextBrush" : "DimBrush",
            size: Tok.IconInline);
        icon.Margin = new Thickness(Tok.SpaceXs, 0, Tok.SpaceXs, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(icon);

        var name = new TextBlock
        {
            Text = "Follow the HUD again",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Tok.SpaceXs, 0),
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, parked ? "TextBrush" : "DimBrush");
        row.Children.Add(name);

        var button = DesignSystem.InlineIconButton("Undo",
            parked
                ? "Puts the chip row and the under-bar panel back under EQBuddy, where they "
                  + "move with it. Drag either one anywhere to park it again."
                : "Nothing is parked — the chip row and the under-bar panel are already "
                  + "following EQBuddy. Drag either one anywhere on the screen to park it, "
                  + "and this puts it back.",
            (_, _) => onUnpark(), parked ? "AccentBrush" : "DimBrush");
        button.IsEnabled = parked;
        if (!parked) button.Opacity = 0.35;
        row.Children.Add(button);

        var border = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(4, 3, 4, 4),
            Margin = new Thickness(0, 0, 3, 0),
            BorderThickness = new Thickness(1),
            Tag = "unpark",
        };
        border.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        if (!parked) border.Opacity = 0.55;
        return border;
    }

    private static Button Nudge(string icon, string tip, bool enabled, Action act)
    {
        var button = DesignSystem.InlineIconButton(icon, tip, (_, _) => act(),
            enabled ? "TextBrush" : "DimBrush");
        button.IsEnabled = enabled;
        // The style carries no disabled visual, so an end-of-row arrow would look exactly
        // like a live one and eat the click in silence (trap 17).
        if (!enabled) button.Opacity = 0.35;
        return button;
    }

    /// <summary>The one-line instruction that sits at the end of the edit row. Edit mode has
    /// no chrome of its own — it is the row, in place — so the sentence that says how to
    /// leave it has to be ON the row, or the mode is a state a player can enter and not
    /// obviously exit.</summary>
    public static Border Hint()
    {
        var text = new TextBlock
        {
            Text = "Editing the HUD row — the arrows reorder, the tick puts a kind of chip "
                 + "on or off the row. Sounds and alerts are not affected. Right-click the "
                 + "widget and choose Edit HUD… again when you are done.",
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 320,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

        var border = new Border
        {
            Child = text,
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(8, 3, 8, 4),
            Margin = new Thickness(0, 0, 3, 0),
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
        };
        return border;
    }
}
