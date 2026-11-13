namespace EQBuddy.UI.Shared;

/// <summary>
/// **THE WORDS ON A WATCH RULE'S PIN AND BELL** (#1104, Flaamwing).
///
/// A rule puts two different chips on screen, and the hovers used to name only one of them.
/// The pin (<see cref="PinTip"/>) is the count that stays on the minimised bar
/// (<c>TrackedRule.Pinned</c>, drawn by <c>HudBarView</c>). The bell (<see cref="BellTip"/>)
/// is the banner and the chip that follows it on the HUD chip row for
/// <see cref="WatchFireLedger.Linger"/> (<c>TrackedRule.AlertBanner</c>). The pencil's
/// "Watch alerts" mute turns that second chip off for every rule; it is
/// <see cref="HudChipRow.Label"/>'s own name, so the hover cannot drift from the chicklet.
///
/// One producer. <c>SettingsAlertsView</c> is the rule row both Options and the shell's
/// Alerts tab build, and it reads these rather than keeping a second sentence.
/// </summary>
public static class WatchRuleToggleText
{
    /// <summary>The 📌 hover. The count on the bar, and an explicit refusal of the other
    /// chip. The previous hover named one chip on the HUD as if the pin owned every chip
    /// there, which is what a player with the pin off and the bell on read as the pin
    /// being broken.</summary>
    public const string PinTip =
        "Keep this rule's count on the minimised bar while this pin is on. "
        + "The chip that appears after a match is the bell, not this pin.";

    /// <summary>The 🔔 hover. The banner, then the lingering chip, then the one switch that
    /// takes that chip off every rule. The seconds are <see cref="WatchFireLedger.Linger"/>'s,
    /// and the family name is <see cref="HudChipRow.Label"/>'s, so neither number nor label
    /// is a second copy.</summary>
    public static string BellTip =>
        "When this rule matches, show the banner, then a chip on the HUD chip row for "
        + (int)WatchFireLedger.Linger.TotalSeconds
        + " seconds. The pencil on EQBuddy (Edit HUD) → "
        + HudChipRow.Label(HudChipFamily.WatchFire)
        + " turns that chip off for every rule.";
}
