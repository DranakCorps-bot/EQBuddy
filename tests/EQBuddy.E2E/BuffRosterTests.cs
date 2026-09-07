namespace EQBuddy.E2E;

/// <summary>
/// The Buffs card's roster reaching the screen as a WRAPPED CHIP GRID (OE-4).
///
/// **`BuffRosterPresentationTests` proves what a chip says; this proves the shape it is
/// drawn in**, which is the half no unit test can reach and no screenshot can assert. Trap
/// 25 is the failure: a horizontal `StackPanel` measures with infinite width in the stacking
/// direction, so chips past the card's edge are CLIPPED — no ellipsis, no overflow, simply
/// not on screen — and the card photographs as a correct card with fewer buffs in it. The
/// Progress tab strip shipped exactly that, one quarter invisible on every launch.
///
/// It is also the pin the lift owed: the roster moved out of `MainWindow` into
/// `BuffsCardView` in this same change, and the WPF layer has no unit tests
/// (docs/TestPlan.md §5), so this assertion is the only thing standing between that move and
/// a silent regression.
///
/// **`EQBUDDY_EXPAND=buffs`, not the harness's default `1`**: the Buffs card is Collapsed on
/// a default profile and the review set does not open it, so under `1` the body is never
/// rendered and every number below would be a correct zero about a card nobody drew.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and two
/// at once would fight for the desktop (trap 57).
/// </summary>
[Collection("e2e")]
public sealed class BuffRosterTests
{
    private static AppHarness Widget() => new(
        settings =>
        {
            settings.TrackSpawns = false;
            // The default, stated: the roster is the WHOLE list, and expiring-only is the
            // other test below.
            settings.BuffTimersExpiringOnly = false;
        },
        new Dictionary<string, string> { ["EQBUDDY_EXPAND"] = "buffs" });

    /// <summary>
    /// Three buffs land and the card draws three chips, in a WrapPanel.
    ///
    /// THE PREDICTION, written before it ran (trap 23): `buffsActive=3` (the tracker saw
    /// them), `buffChips=3` (the card drew one each), `buffWrapped=1` (they are in a wrapping
    /// grid) and `buffRows=1` — the panel's ONE child is the chip grid, because this profile
    /// has no buff set and no suggestions, so nothing hangs below it.
    ///
    /// `AppendLogLines` writes AFTER launch: it returns once the tail has read the bytes, and
    /// the waits below are positive assertions on values that can only rise once the render
    /// has run, which is trap 62's rule about naming the moment an assertion is true at.
    /// </summary>
    [Fact]
    public void TheBuffRosterDrawsOneChipPerBuffInAWrappingGrid()
    {
        using var app = Widget();
        app.Launch();
        app.WaitForDump("buffChips", 0, "an empty roster before anything has landed");

        app.AppendLogLines(
            "Sanctari begins casting Valor.",
            "You feel valorous.",
            "Sanctari begins casting Health.",
            "You feel healthy.",
            "Sanctari begins casting Aegolism.",
            "You are filled with the power of Aegolism.");

        app.WaitForDump("buffsActive", 3, "all three landings to be tracked");
        app.WaitForDump("buffChips", 3, "one chip per active buff on the card");
        app.WaitForDump("buffWrapped", 1, "the chips to be in a WrapPanel and not a column");
        app.WaitForDump("buffRows", 1, "the chip grid to be the card's only block");
    }

    /// <summary>
    /// Expiring-only mode: the same three buffs, none of them close to fading, and the card
    /// says so instead of listing them.
    ///
    /// **The negative half, and the pair is the point** — either test alone passes with the
    /// wrong implementation. A card that ignores the setting passes the one above; a card
    /// that draws nothing passes this one. `buffsActive=3` beside `buffChips=0` is what makes
    /// "no chips because you asked to be told late" a different reading from "no chips
    /// because nothing landed" (trap 56's liveness rule, one surface over).
    ///
    /// THE PREDICTION: `buffsActive=3`, `buffChips=0`, `buffWrapped=0`, and `buffRows=1` —
    /// the one row being the line that names the three running quietly. A roster that
    /// silently drew nothing would report `buffRows=0`.
    /// </summary>
    [Fact]
    public void ExpiringOnlyKeepsTheRosterQuietAndStillSaysSomething()
    {
        using var app = new AppHarness(
            settings =>
            {
                settings.TrackSpawns = false;
                settings.BuffTimersExpiringOnly = true;
                settings.BuffWarnSeconds = 60;
            },
            new Dictionary<string, string> { ["EQBUDDY_EXPAND"] = "buffs" });
        app.Launch();

        app.AppendLogLines(
            "Sanctari begins casting Valor.",
            "You feel valorous.",
            "Sanctari begins casting Health.",
            "You feel healthy.",
            "Sanctari begins casting Aegolism.",
            "You are filled with the power of Aegolism.");

        app.WaitForDump("buffsActive", 3, "the buffs to be tracked, so an empty card is the mode");
        app.WaitForDump("buffChips", 0, "no chip for a buff nowhere near fading");
        app.WaitForDump("buffWrapped", 0, "and no chip grid at all");
        app.WaitForDump("buffRows", 1, "the card to say how many are running quietly");
    }
}
