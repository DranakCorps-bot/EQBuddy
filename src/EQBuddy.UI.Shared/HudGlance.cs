namespace EQBuddy.UI.Shared;

/// <summary>Which number the collapsed HUD's third slot is currently showing.</summary>
public enum HudThird
{
    /// <summary>XP%/hr — the default, and what a farmer watches.</summary>
    Experience,
    /// <summary>HPS — while healing has been the weight of the last ~30 seconds.</summary>
    Healing,
}

/// <summary>Everything the glance decides from, in one value so a test can state a
/// situation rather than assemble one.</summary>
/// <param name="CharacterName">Whoever the log is naming, or null/empty before it names
/// anybody. An empty name is a normal state, not an error.</param>
/// <param name="CurrentDps">DPS of the fight that is live right now, 0 between pulls.</param>
/// <param name="SessionDps">DPS across the session's combat time — what the DPS slot
/// falls back to between pulls, exactly as the bar's own cell always has.</param>
/// <param name="Hps">Healing per combat second, session scope.</param>
/// <param name="XpPerHour">Experience percent per hour.</param>
/// <param name="RecentDamage">Damage dealt in the dominance window (~30 s).</param>
/// <param name="RecentHealing">Healing cast in the dominance window (~30 s).</param>
/// <param name="DamageSinceResume">Damage dealt in the short resume window (~5 s) — the
/// one that answers "has damage-combat returned".</param>
public readonly record struct HudGlanceInput(
    string? CharacterName,
    double CurrentDps,
    double SessionDps,
    double Hps,
    double XpPerHour,
    long RecentDamage,
    long RecentHealing,
    long DamageSinceResume);

/// <summary>The three strings the collapsed HUD draws, and which number slot three
/// currently is.</summary>
/// <param name="Third">Feed this back in as the next call's <c>current</c>.</param>
/// <param name="Name">Character name, or "" — the slot keeps its reserved width.</param>
/// <param name="Dps">Slot two, always DPS.</param>
/// <param name="ThirdText">Slot three: XP%/hr, or HPS.</param>
/// <param name="ThirdIcon">The <see cref="IconPaths"/> name slot three wears, so the icon
/// swaps with the number rather than being decided a second time by the view.</param>
public sealed record HudGlanceReadout(
    HudThird Third, string Name, string Dps, string ThirdText, string ThirdIcon);

/// <summary>
/// The collapsed HUD's three numbers — Name · DPS · XP%/hr — and the one swap they make
/// (Surface A / SA-1; the signed spec is docs/BEVEL-v2-staging-critique.md §3).
///
/// **A decision with no window in it**, which is the whole reason it lives here: the WPF
/// layer has no unit tests (docs/TestPlan.md §5), so a rule expressed in a view is a rule
/// nothing can check. Every string this returns is pinned by
/// <c>HudGlanceTests</c>.
///
/// **Every output is a FIXED SHAPE, and that is load-bearing rather than tidy** (trap
/// 12). The widget is <c>SizeToContent</c>, so a readout whose width changes IS a window
/// resize — on an always-on-top transparent window stacked over a fullscreen game, which
/// is what cost #173 (KoboldCoterie, CachyOS) its keyboard. These three numbers update on
/// a one-second timer forever, so they are precisely the case that rule was written
/// against. <see cref="PerfReadout"/> is the worked example this follows: pad to a fixed
/// length here, reserve a fixed width in the view, and a new sample then changes pixels
/// and nothing else.
///
/// **The swap is the subtle half.** Slot three shows HPS "when healing dominates for ~30
/// seconds" and goes back to XP%/hr "the moment combat-as-damage returns" — two different
/// questions, so <see cref="Core.RecentEffort"/> carries two windows and this weighs
/// them. Entering is deliberately slow (a healer who lands one heal mid-pull does not lose
/// their XP rate); leaving is deliberately instant (a healer who starts swinging wants
/// their damage number back that second). Both directions are tested.
/// </summary>
public static class HudGlance
{
    /// <summary>Width to reserve for the NAME slot, in the widget's pre-scale units.
    ///
    /// A name is player-driven and changes only when the character does, so it is not the
    /// timer-driven resize trap 12 forbids — but reserving it costs nothing and buys the
    /// one case that IS timer-adjacent: the log naming a character for the first time,
    /// seconds after launch, which would otherwise widen the HUD under the player's
    /// cursor. An empty name renders as an empty slot at this width rather than as a
    /// collapsing hole, so the two metrics beside it never move.
    ///
    /// Sized for a 16-character EverQuest name at the bar's title-section size; longer
    /// ones trim. The exact number only decides how much HUD the name costs.</summary>
    public const double NameReservedWidth = 92;

    /// <summary>Character count of every string <see cref="DpsText"/> and
    /// <see cref="ThirdText"/> return. Asserted by the tests — it is the invariant that
    /// makes the swap in slot three free of a measure change, not a decoration.</summary>
    public const int MetricFixedLength = 10;

    /// <summary>Width to reserve for each METRIC slot. Both slots get the same one: slot
    /// three swaps its string on a timer, so a per-string width would be the resize this
    /// class exists to avoid.</summary>
    public const double MetricReservedWidth = 66;

    /// <summary>Slot two's icon — the same vector the DPS cell and the Damage breakout
    /// wear, because the thing that means "damage" looks the same wherever it appears.
    /// A vector and never a glyph (#148, #166).</summary>
    public const string DpsIcon = "Swords";
    /// <summary>Slot three's icon while it is XP%/hr.</summary>
    public const string ExperienceIcon = "Chart";
    /// <summary>Slot three's icon while it is HPS. It swaps WITH the number: an icon left
    /// behind by a swap is the "tick box that lies" in a smaller costume.</summary>
    public const string HealingIcon = "Heal";

    /// <summary>Hover text for the name slot, including when it is empty — an empty slot
    /// with no explanation is the silent no-op rule with the switch on the other side.</summary>
    public const string EmptyNameTooltip = "Looking for a character — play for a moment";

    /// <summary>Which number slot three should be NEXT, given what it is now.
    ///
    /// Asymmetric on purpose, and the asymmetry IS the hysteresis:
    /// <list type="bullet">
    /// <item>From XP, healing has to have out-weighed damage across the whole ~30 s
    /// window. One heal during a fight does not take a farmer's XP rate away.</item>
    /// <item>From HPS, any damage at all in the short resume window brings XP back
    /// immediately — "the moment combat-as-damage returns", in the spec's words.</item>
    /// <item>…and so does the window emptying of healing altogether, which is what
    /// happens when a healer simply stops: thirty seconds later there is no longer
    /// anything for slot three to be about.</item>
    /// </list>
    /// </summary>
    public static HudThird NextThird(HudThird current, in HudGlanceInput input) =>
        current == HudThird.Healing
            ? (input.DamageSinceResume > 0 || input.RecentHealing <= 0
                ? HudThird.Experience : HudThird.Healing)
            : (input.RecentHealing > 0 && input.RecentHealing > input.RecentDamage
                ? HudThird.Healing : HudThird.Experience);

    /// <summary>The name slot's text: whoever the log has named, or "" while it has named
    /// nobody. Never a placeholder sentence — the slot is one of three numbers on an
    /// overlay, and the view reserves <see cref="NameReservedWidth"/> either way.</summary>
    public static string NameText(string? characterName) =>
        characterName is { Length: > 0 } name ? name.Trim() : "";

    /// <summary>Slot two. The live fight's rate while one is live, the session's between
    /// pulls — the same rule the bar's own dps cell has always used, so promoting the
    /// number did not quietly redefine it.
    ///
    /// Clamped into the fixed shape rather than allowed to grow. The clamp is unreachable
    /// in EverQuest Legends; what it buys is that <see cref="MetricFixedLength"/> is an
    /// invariant with no "for plausible values" attached to it.</summary>
    public static string DpsText(in HudGlanceInput input) =>
        Metric(input.CurrentDps > 0 ? input.CurrentDps : input.SessionDps, "dps");

    /// <summary>Slot three, for whichever number it currently is.</summary>
    public static string ThirdText(HudThird third, in HudGlanceInput input) =>
        third == HudThird.Healing
            ? Metric(input.Hps, "hps")
            // One decimal, because an XP rate below 10%/hr is the normal case at level
            // and "0%/hr" would read as broken while the number is really 0.4.
            : $"{Math.Clamp(input.XpPerHour, 0, 9999.9),6:0.0}%/hr";

    /// <summary>"  1234 dps" — right-aligned in six columns so the digits neither jitter
    /// nor change the measured width, and exactly <see cref="MetricFixedLength"/>
    /// characters for every value.</summary>
    private static string Metric(double value, string unit) =>
        $"{Math.Clamp(value, 0, 999999),6:0} {unit}";

    /// <summary>The whole glance in one call: advance the swap, then format all three
    /// slots from the SAME input. One moment, one decision — a view that asked for the
    /// third-slot mode and the third-slot text separately could be handed two
    /// (trap 4).</summary>
    public static HudGlanceReadout Next(HudThird current, in HudGlanceInput input)
    {
        var third = NextThird(current, in input);
        return new HudGlanceReadout(
            third,
            NameText(input.CharacterName),
            DpsText(in input),
            ThirdText(third, in input),
            third == HudThird.Healing ? HealingIcon : ExperienceIcon);
    }

    /// <summary>The glance straight off a snapshot — what the widget actually calls, so
    /// the mapping from session fields to glance inputs exists once rather than in every
    /// host that ever draws a HUD.</summary>
    public static HudGlanceReadout Next(HudThird current, Core.StatsSnapshot s, string? characterName) =>
        Next(current, new HudGlanceInput(
            characterName, s.CurrentDps, s.SessionDps, s.Hps, s.XpPerHour,
            s.Effort.DamageDone, s.Effort.HealingDone, s.Effort.DamageDoneInResumeWindow));
}
