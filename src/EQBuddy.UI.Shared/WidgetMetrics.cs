namespace EQBuddy.UI.Shared;

/// <summary>
/// The widget's screen-pixels-to-layout-units arithmetic, extracted so it can be tested
/// without a window.
///
/// The widget's content sits under a UI-scale transform. Anything measured from the
/// SCREEN — the monitor's work area, a cursor position — is in real pixels, while
/// anything assigned to a control INSIDE the transform is in pre-scale layout units.
/// The two agree only at 100%, so a mix-up is invisible until someone moves the size
/// slider. That is exactly how discussion #144 shipped: the section list was handed a
/// monitor-derived cap in screen pixels, believed it had more room than the window
/// could show, and clipped the last card with no scrollbar to reach it.
///
/// Every conversion between the two lives here, with the direction in the method name.
/// </summary>
public static class WidgetMetrics
{
    /// <summary>Never let the card list shrink below this (pre-scale units) — a list
    /// too short to hold one card is a broken window, not a small one.</summary>
    public const double MinSectionHeight = 120;

    /// <summary>Guards the divisions below. A scale of zero would produce infinity, and
    /// settings files have carried surprising values before.</summary>
    public static double SafeScale(double uiScale) => Math.Max(0.25, uiScale);

    /// <summary>
    /// The card list's MaxHeight, in the PRE-SCALE units the control expects.
    /// </summary>
    /// <param name="screenCap">The monitor-derived ceiling, in screen pixels.</param>
    /// <param name="contentHeight">The player's dragged height in pre-scale units, or
    /// NaN for automatic. Stored pre-scale deliberately, so it survives scale changes —
    /// which is also why it is compared against a pre-scale cap and not a screen one.</param>
    public static double SectionMaxHeight(double screenCap, double contentHeight, double uiScale)
    {
        var cap = screenCap / SafeScale(uiScale);
        return double.IsNaN(contentHeight)
            ? cap
            : Math.Clamp(contentHeight, MinSectionHeight, Math.Max(MinSectionHeight, cap));
    }

    /// <summary>
    /// How tall ONE expanded theme's body may get on the widget, in pre-scale units.
    ///
    /// <see cref="SectionMaxHeight"/> already caps the whole card stack, so this is not
    /// about running off the screen — it is about one theme eating the stack. Without it
    /// an expanded Faction list pushes every other card below the fold, and the widget's
    /// job is the glance across all of them.
    ///
    /// **320 rather than 280** (Bevel offered both and delegated the pick): it is the
    /// number <c>GearCardView</c> already uses for exactly this job, and a second nearby
    /// constant would be two answers to one question.
    ///
    /// **And the screenshot could not decide between them, which is worth saying out loud
    /// rather than letting the number look measured.** The Progress theme does not reach
    /// either cap: its tallest room is Experience with a ding list and every AA shown, and
    /// that is about 175 of these units — Wealth is four lines, Faction is five rows, and
    /// Raids is a Glance that draws no body at all. So PR 1 picks the constant and PR 2 is
    /// where it is actually tested, on Loot's rows and the Drops list. A cap nothing has
    /// yet hit is a guard, not a measurement.
    ///
    /// It is a CAP, not a height: a short room (Wealth's four coin lines) draws short.
    /// The scroller it implies is the host's problem, and the wheel has to reach the card
    /// stack when there is nothing here to scroll — trap 36, which shipped an Inventory
    /// tab that could only be moved by dragging the outer slider.
    ///
    /// **Since 2026-08-31 this is the FLOOR rather than the whole answer** — see
    /// <see cref="ThemeBodyCap"/>. A widget nobody has dragged still gets exactly this
    /// number, so the untouched app is pixel-identical to what it was.
    /// </summary>
    public const double ThemeBodyMaxHeight = 320;

    /// <summary>The most one expanded theme body may take, whatever the player drags —
    /// exactly 2× <see cref="ThemeBodyMaxHeight"/>, so the relationship is legible rather
    /// than a second unexplained number. Monitor safety is NOT this constant's job:
    /// <see cref="SectionMaxHeight"/> still bounds the whole stack, so the ceiling only
    /// says how much of the stack ONE card may claim.</summary>
    public const double ThemeBodyCeiling = 2 * ThemeBodyMaxHeight;

    /// <summary>
    /// How tall one expanded theme's body may get ON A WIDGET THE PLAYER HAS RESIZED.
    ///
    /// #250 (Paineless) reached for the height grip and got nothing:
    /// <see cref="ThemeBodyMaxHeight"/> is a constant, so dragging the widget taller
    /// grew the card STACK and left every expanded body at 320. That is not a cap being
    /// too small — it is a cap that ignores the one control the player reached for
    /// (Bevel, 2026-08-28; plan Fable 5, product-signed Bevel and Helm-signed 2026-08-31).
    ///
    /// **The floor IS the default.** <paramref name="playerContentHeight"/> is
    /// <c>AppSettings.ContentHeight</c>, which is NaN until the grip is dragged —
    /// so an untouched widget answers 320 and renders exactly as it did before. Nothing
    /// here raises the cap globally; it scales only for a player who has already said how
    /// much room they want.
    ///
    /// <paramref name="otherVisibleChrome"/> is everything else the dragged height has to
    /// hold: the other visible cards' HEADERS, this card's own header and tab strip, and
    /// the margins between them. **Not the siblings' bodies** (Bevel, 2026-08-31) — a
    /// second expanded card is the player asking for two open cards, not a reason to
    /// shrink this one, and <see cref="SectionMaxHeight"/> arbitrates by scrolling the
    /// stack. Excluding this body is also what keeps the arithmetic from feeding back
    /// into its own input.
    ///
    /// **Both arguments are PRE-SCALE units** and so is the answer — everything under the
    /// widget's UI-scale transform already is, which is why this method does no dividing
    /// (trap 1). A caller that has only screen pixels has the wrong number, not a
    /// conversion to do here.
    ///
    /// **Whole units, deliberately.** The result feeds a <c>MaxHeight</c> on a
    /// <c>SizeToContent</c> always-on-top window, so a cap that wobbled by a fraction
    /// would ask the windowing system to resize — #173's cost, from trap 12. Layout
    /// changes move this number; nothing on a clock does.
    /// </summary>
    /// <param name="playerContentHeight">The dragged height of the card stack in pre-scale
    /// units, or NaN for automatic — the widget as shipped.</param>
    /// <param name="otherVisibleChrome">Measured height inside the stack that is not this
    /// body. A measurement that has not happened yet (NaN, infinity) answers the floor,
    /// because "we cannot tell yet" and "draw what you have always drawn" are the same
    /// instruction.</param>
    public static double ThemeBodyCap(double playerContentHeight, double otherVisibleChrome)
    {
        if (!double.IsFinite(playerContentHeight)) return ThemeBodyMaxHeight;
        if (!double.IsFinite(otherVisibleChrome)) return ThemeBodyMaxHeight;
        var room = playerContentHeight - Math.Max(0, otherVisibleChrome);
        return Math.Floor(Math.Clamp(room, ThemeBodyMaxHeight, ThemeBodyCeiling));
    }

    /// <summary>
    /// The <c>otherVisibleChrome</c> argument above, summed from what the card stack
    /// actually measured. **Both widgets call this** — the measuring is per toolkit, the
    /// rule about what the measurements MEAN is not, which is the only way the two lanes
    /// can be said to feed <see cref="ThemeBodyCap"/> the same inputs.
    ///
    /// One entry per VISIBLE card in the stack, each already net of any body:
    /// <list type="bullet">
    /// <item>a collapsed card is all header, so its whole extent goes in;</item>
    /// <item>an expanded SIBLING contributes its header only — a second open card is the
    /// player asking for two open cards, not a reason to shrink this one (Bevel,
    /// 2026-08-31);</item>
    /// <item>the card doing the asking contributes everything it occupies MINUS the body
    /// being capped — its header, its tab strip and its padding. Subtracting the body is
    /// also what keeps this out of its own input: the difference does not move when the
    /// cap does.</item>
    /// </list>
    ///
    /// **Widget chrome outside the stack is NOT in here, and that is a correction to the
    /// plan rather than an omission.** The height grip seeds from the card scroller's own
    /// height and assigns straight back to it, so <c>ContentHeight</c> IS the stack's
    /// viewport — the title bar, the KPI strip and the status line are already outside the
    /// number being divided up. Subtracting them again would hand every player less room
    /// than they asked for, invisibly.
    ///
    /// **A measurement that has not happened poisons the total on purpose.** One card the
    /// layout has not reached yet is NaN, and dropping it would silently under-count the
    /// chrome and over-grant the cap; propagating it makes <see cref="ThemeBodyCap"/>
    /// answer the floor, which is what the widget drew before any of this existed. A
    /// negative extent is a toolkit having a bad day, not room to spend, so it counts as
    /// zero.
    /// </summary>
    public static double ThemeBodyChrome(IEnumerable<double> visibleCardExtents)
    {
        double total = 0;
        foreach (var extent in visibleCardExtents)
        {
            if (!double.IsFinite(extent)) return double.NaN;
            total += Math.Max(0, extent);
        }
        return total;
    }

    /// <summary>A bottom-edge drag turned into a stored height. The cursor travels in
    /// screen pixels while the list it resizes lives under the transform, so the delta
    /// is divided rather than added raw.</summary>
    public static double ContentHeightFromDrag(
        double startHeight, double cursorDeltaPixels, double uiScale) =>
        Math.Max(MinSectionHeight, startHeight + cursorDeltaPixels / SafeScale(uiScale));

    /// <summary>
    /// Where the window's Left goes when the expand/minimize swap changes its width, so
    /// the RIGHT edge stays where it was (#239, disberon).
    ///
    /// The mini bar and the full title bar both put their mode toggle second from the
    /// right — the ORDER was never the bug. The bug is that the window is SizeToContent
    /// and the mode swap only changes visibility: Left stays put, the right edge travels
    /// by the width delta, and the cursor that just clicked Expand is now over Settings
    /// or Start-a-new-session. Anchoring the right edge keeps the toggle pair under the
    /// cursor in BOTH directions, which is what makes habitual toggling safe.
    ///
    /// **One unit space, caller's choice, used consistently** — WPF passes DIPs
    /// (Left and ActualWidth agree there); Avalonia must convert, because its Position is
    /// PHYSICAL pixels while Width is logical units, and mixing them is trap 1 with a
    /// different pair of units. No work-area clamp on purpose: a negative Left is
    /// legitimate on a multi-monitor desk, and clamping against the primary monitor's
    /// area would yank a secondary-monitor widget. The window was already on screen at
    /// this right edge; it still is.
    ///
    /// A width that is not yet real — zero, negative, NaN on the startup call before the
    /// first layout — answers "leave Left alone": anchoring to a measurement that never
    /// happened would move a freshly restored window.
    /// </summary>
    public static double RightAnchoredLeft(double left, double oldWidth, double newWidth)
    {
        if (double.IsNaN(left) || double.IsInfinity(left)) return left;
        if (!(oldWidth > 0) || !(newWidth > 0)) return left;
        if (double.IsInfinity(oldWidth) || double.IsInfinity(newWidth)) return left;
        return left + oldWidth - newWidth;
    }
}
