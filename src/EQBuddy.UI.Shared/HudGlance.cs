namespace EQBuddy.UI.Shared;

/// <summary>Which of the CONDITIONAL metric slots the collapsed HUD's always-on row is
/// carrying right now, so the next decision can be told what the last one settled.
///
/// It is a state and not a mode: the row's unconditional members (the name, DPS, the XP
/// rate) are never in here, because nothing about them is decided. Today it holds one
/// bool; it is a record struct rather than that bool so the next conditional slot does not
/// change every caller's signature.
///
/// <para><b>It used to be an either/or</b> (<c>HudThird</c>: the third slot is the XP rate
/// OR healing), and that is precisely what DRA-72 removes — see
/// <see cref="HudGlance"/>.</para></summary>
/// <param name="Healing">The HPS slot is on the row. Fed back in as the next call's state,
/// because arriving and leaving are different tests.</param>
public readonly record struct HudGlanceState(bool Healing)
{
    /// <summary>Before the first render: the row a melee character sees forever — name,
    /// DPS, XP%/hr, and nothing conditional.</summary>
    public static HudGlanceState Start => default;
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
/// one that answers "has damage-combat returned".
///
/// <b>Nothing reads it since DRA-72, and it stays on the input on purpose.</b> It was the
/// instant-exit half of the swap, and the swap is gone: damage returning no longer takes a
/// slot away, because the XP rate it used to hand the slot back to now has a slot of its
/// own. <see cref="Core.RecentEffort"/> still measures it, the Damage surfaces still use
/// it, and removing it from this record would make the one input that explains the
/// deleted clause unavailable to the test that proves the clause is gone.</param>
/// <param name="PetDps">Your pet's damage per combat second
/// (<see cref="Core.StatsSnapshot.PetDps"/>), whether or not the slot is showing. One
/// value, two surfaces — the cell formats the same number compactly (trap 4).</param>
/// <param name="PetInserted">The player has dragged the pet chip into the always-on row
/// (<see cref="Core.AppSettings.HudGlancePet"/>). The row's MEMBERSHIP, decided by the
/// profile and never by this class.</param>
public readonly record struct HudGlanceInput(
    string? CharacterName,
    double CurrentDps,
    double SessionDps,
    double Hps,
    double XpPerHour,
    long RecentDamage,
    long RecentHealing,
    long DamageSinceResume,
    double PetDps = 0,
    bool PetInserted = false);

/// <summary>One metric slot on the always-on row: what it reads, what it wears, and how
/// much width it keeps whatever it reads.
///
/// <para><b>The KEY is the identity, and it is deliberately the key everything else on
/// this bar already uses</b> — <c>"dps"</c>, <c>"pet"</c>, <c>"hps"</c>, <c>"xp"</c>. So
/// <see cref="HudExpand.TargetForKey"/> turns a slot into its expansion chip's target, the
/// panel, the title and the ⧉, exactly as it does for a tray cell since OE-9 (one table,
/// trap 4) — and the <c>EQBUDDY_EXPAND</c> dump can name the row in the same vocabulary a
/// player's <c>MiniBarOrder</c> is written in.</para></summary>
/// <param name="Key">The slot's identity — a <see cref="HudExpand.TargetForKey"/> name.</param>
/// <param name="Text">The fixed-shape string, <see cref="HudGlance.MetricFixedLength"/>
/// characters for every value it can hold.</param>
/// <param name="Icon">The <see cref="IconPaths"/> name the slot wears, decided WITH the
/// number rather than a second time by the view.</param>
/// <param name="ReservedWidth">Width the view pins the string to, in the widget's
/// pre-scale units. A constant PER METRIC — see <see cref="HudGlance"/>.</param>
public sealed record HudGlanceSlot(string Key, string Text, string Icon, double ReservedWidth);

/// <summary>The strings the collapsed HUD draws, and which slots it draws them in.</summary>
/// <param name="State">Feed this back in as the next call's state.</param>
/// <param name="Name">Character name, or "" — the slot keeps its reserved width.</param>
/// <param name="Slots">Every metric slot, LEFT TO RIGHT, and only the ones that are on the
/// row. **A slot that is not there is absent rather than empty**: membership is the whole
/// answer, so the view draws what it is handed and asks nothing a second time (the rule
/// SIGNED #422 established for the pet slot's null, widened to every conditional
/// member).</param>
public sealed record HudGlanceReadout(
    HudGlanceState State, string Name, IReadOnlyList<HudGlanceSlot> Slots)
{
    /// <summary>The row as one space-free token — <c>"dps,hps,xp"</c> — in
    /// <see cref="MiniBarPresentation.OrderKey"/>'s shape, because a key list on this bar
    /// has exactly one spelling whether it is naming cells or slots.
    ///
    /// **This is what the DECISION says, and it is deliberately not what the dump
    /// reports.** <c>HudBarView.GlanceKey</c> builds the same token out of the keys it
    /// actually DREW, because "HudGlance would answer dps,hps,xp" and "the row drew
    /// dps,hps,xp" are different claims and only the second one is the feature (trap 42).
    /// The two agreeing is the thing worth being able to check; sharing the spelling is
    /// what makes them comparable.</summary>
    public string RowKey => MiniBarPresentation.OrderKey(Slots.Select(slot => slot.Key));

    /// <summary>Is this metric on the row at all.</summary>
    public bool Has(string key) => Slots.Any(slot => slot.Key == key);

    /// <summary>What a metric currently reads, or null when it is not on the row — the two
    /// answers a caller must be able to tell apart (an HPS slot showing "     0 hps" is a
    /// real state and is NOT the same as no HPS slot).</summary>
    public string? TextOf(string key) =>
        Slots.FirstOrDefault(slot => slot.Key == key)?.Text;
}

/// <summary>
/// The collapsed HUD's always-on numbers — Name · DPS · (pet) · (HPS) · XP%/hr (Surface A
/// / SA-1; the signed spec is docs/BEVEL-v2-staging-critique.md §3, AMENDED by DRA-72).
///
/// <para><b>DRA-72: THE THIRD SLOT NO LONGER SWAPS, IT GAINS A NEIGHBOUR.</b> SA-1 put HPS
/// and the XP rate in ONE slot that changed identity — "one swap, not a second meter" — and
/// the Founder's video is what that costs when a character does both at once: healing
/// out-weighed damage across the ~30 s window *and* a swing landed inside the ~5 s resume
/// window, so the ENTER test and the EXIT test were both true, and the slot alternated
/// between "13 hps" and "167.5%/hr" about once a second, forever. The two rules were each
/// right on their own; nothing tested them TOGETHER, because the swap's tests drive one
/// direction at a time (which is the shape to remember — a hysteresis is only as good as
/// the case where both of its tests pass).</para>
///
/// <para>So the amendment is one deleted clause, and no more than that: <b>damage
/// returning no longer removes the HPS slot</b>, because the number that clause existed to
/// hand the slot back to now has a slot of its own that nothing takes. Everything else the
/// spec signed survives verbatim — entering is still slow (healing has to have out-weighed
/// damage across the whole window, so a damage dealer who lands one heal mid-pull gains no
/// slot), and it still leaves when the window holds no healing at all, which is what
/// happens ~30 s after a healer stops. <b>An oscillation is now impossible by
/// construction</b>: no input both adds and removes the slot on alternating ticks, because
/// only one signal decides either.</para>
///
/// <para><b>Since SIGNED #422 the row has one player-INSERTED member</b>: pet DPS, at a
/// fixed insertion POINT between DPS and the metrics that follow it, present exactly while
/// <see cref="Core.AppSettings.HudGlancePet"/> says so. The owner's 2026-09-07 ~7:36 PM CT
/// lock widens this row's MEMBERSHIP, not its ORDER — and DRA-72 leaves that reasoning
/// intact for the same reason it always held: the insertion point is between DPS and
/// whatever comes next, and the arrival of an HPS slot does not move it. The XP rate is
/// still the row's LAST slot, exactly as it has been since SA-1, so no position on this row
/// has changed what it means.</para>
///
/// <para><b>A decision with no window in it</b>, which is the whole reason it lives here:
/// the WPF layer has no unit tests (docs/TestPlan.md §5), so a rule expressed in a view is
/// a rule nothing can check. Every string and every membership answer this returns is
/// pinned by <c>HudGlanceTests</c>.</para>
///
/// <para><b>Every output is a FIXED SHAPE, and that is load-bearing rather than tidy</b>
/// (trap 12). The widget is <c>SizeToContent</c>, so a readout whose width changes IS a
/// window resize — on an always-on-top transparent window stacked over a fullscreen game,
/// which is what cost #173 (KoboldCoterie, CachyOS) its keyboard. These numbers update on
/// a one-second timer forever, so they are precisely the case that rule was written
/// against. <see cref="PerfReadout"/> is the worked example this follows: pad to a fixed
/// length here, reserve a fixed width in the view, and a new sample then changes pixels
/// and nothing else.</para>
///
/// <para><b>The row GROWS by adding a slot, and never by letting a string measure
/// wider</b> — which is how "expand and shrink with the content" and trap 12 are the same
/// design rather than opposed ones. Every slot carries its own
/// <see cref="HudGlanceSlot.ReservedWidth"/>, a constant PER METRIC: since no slot changes
/// identity on a timer any more, a per-metric width is a constant of the row rather than
/// the per-sample remeasure SA-1 had to forbid, and the XP slot is the one that needed it
/// (<see cref="ExperienceReservedWidth"/>). A slot ARRIVING changes the measured width
/// once, when the player's own play changes what is being tracked — the same permission
/// the player's pet drop has always had.</para>
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
    /// collapsing hole, so the metrics beside it never move.
    ///
    /// Sized for a 16-character EverQuest name at the bar's title-section size; longer
    /// ones trim. The exact number only decides how much HUD the name costs.</summary>
    public const double NameReservedWidth = 92;

    /// <summary>Character count of every string <see cref="DpsText"/>,
    /// <see cref="PetText"/>, <see cref="HealingText"/> and <see cref="ExperienceText"/>
    /// return. Asserted by the tests — it is the invariant that makes a sample change
    /// pixels and nothing else, and it is asserted ACROSS the four rather than per string,
    /// because four slots of one shape is what makes the row measurable.</summary>
    public const int MetricFixedLength = 10;

    /// <summary>Width to reserve for a rate slot — DPS, pet DPS, HPS. One number for the
    /// three of them because they are the same string shape ("  1234 dps") down to the
    /// unit's three letters.</summary>
    public const double MetricReservedWidth = 66;

    /// <summary>Width to reserve for the XP RATE slot, which is wider than its neighbours
    /// and always was.
    ///
    /// <b>Same character count, more ink</b>: "9999.9%/hr" spends two of its ten
    /// characters on '%' and '/', which out-measure the leading spaces " 1234 dps" pads
    /// with at the same <see cref="DesignTokens.TypeRole.TitleSection"/> size. At
    /// <see cref="MetricReservedWidth"/> a four-digit rate trimmed to an ellipsis — the
    /// Founder's video reads it as the XP string "eating the gap" — and a trimmed number is
    /// a number the player cannot read.
    ///
    /// <b>A per-metric width only became legal in DRA-72</b>, and the reason is worth
    /// keeping: SA-1 gave every metric ONE width because the third slot changed identity
    /// on a timer, so a per-string width there would have been exactly the resize trap 12
    /// forbids. No slot changes identity any more, so this is a constant of the row.
    /// It is not a measured value — nothing in this project can measure text without a
    /// window — it is headroom over the longest string the slot can hold.</summary>
    public const double ExperienceReservedWidth = 76;

    /// <summary>The DPS slot's key. Every key here is a <see cref="HudExpand.TargetForKey"/>
    /// name, so one table answers for the chip, the peek, the title and the ⧉.</summary>
    public const string DpsKey = "dps";
    /// <summary>The HPS slot's key.</summary>
    public const string HpsKey = "hps";
    /// <summary>The XP-rate slot's key. <see cref="HudExpand.TargetForKey"/> reads it as
    /// <see cref="HudExpandTarget.Progress"/> — the slot's pop-out is the Progress WINDOW,
    /// which is the signed 2026-08-25 fold and not a breakout.</summary>
    public const string XpKey = "xp";

    /// <summary>Slot two's icon — the same vector the DPS cell and the Damage breakout
    /// wear, because the thing that means "damage" looks the same wherever it appears.
    /// A vector and never a glyph (#148, #166).</summary>
    public const string DpsIcon = "Swords";
    /// <summary>The XP-rate slot's icon.</summary>
    public const string ExperienceIcon = "Chart";
    /// <summary>The HPS slot's icon. It arrives and leaves WITH the number — an icon left
    /// behind by a slot that is gone is the "tick box that lies" in a smaller costume.</summary>
    public const string HealingIcon = "Heal";
    /// <summary>The inserted pet slot's icon — the SAME vector its cell, its peek panel and
    /// the Pet float wear (<c>MiniBarPresentation.Icons["pet"]</c>), because a chip that
    /// changed shape when it moved rows would read as a different stat. It is also what
    /// tells two "N dps" slots apart on one row: Swords against Paw, exactly how the HPS
    /// slot is told from them (Heal against Chart).</summary>
    public const string PetIcon = "Paw";

    /// <summary>Hover text for the name slot, including when it is empty — an empty slot
    /// with no explanation is the silent no-op rule with the switch on the other side.</summary>
    public const string EmptyNameTooltip = "Looking for a character — play for a moment";

    /// <summary>Is the HPS slot on the row, given whether it is on the row now.
    ///
    /// Asymmetric on purpose, and the asymmetry IS the hysteresis:
    /// <list type="bullet">
    /// <item>To ARRIVE, healing has to have out-weighed damage across the whole ~30 s
    /// window. One heal during a fight does not widen a farmer's HUD.</item>
    /// <item>To STAY, any healing at all in that window is enough — so a healer who is
    /// also swinging keeps the number they are healing by. <b>This is the DRA-72
    /// amendment</b>: damage in the resume window used to take the slot away on the spot,
    /// which is what made it alternate with the XP rate about once a second when both
    /// tests were true of one character.</item>
    /// <item>It LEAVES when the window holds no healing at all, which is what happens when
    /// a healer simply stops: thirty seconds later there is nothing for the slot to be
    /// about.</item>
    /// </list>
    /// </summary>
    public static bool HealingShown(bool shown, in HudGlanceInput input) =>
        shown
            ? input.RecentHealing > 0
            : input.RecentHealing > 0 && input.RecentHealing > input.RecentDamage;

    /// <summary>The name slot's text: whoever the log has named, or "" while it has named
    /// nobody. Never a placeholder sentence — the slot is a label beside a row of numbers
    /// on an overlay, and the view reserves <see cref="NameReservedWidth"/> either
    /// way.</summary>
    public static string NameText(string? characterName) =>
        characterName is { Length: > 0 } name ? name.Trim() : "";

    /// <summary>The DPS slot. The live fight's rate while one is live, the session's between
    /// pulls — the same rule the bar's own dps cell has always used, so promoting the
    /// number did not quietly redefine it.
    ///
    /// Clamped into the fixed shape rather than allowed to grow. The clamp is unreachable
    /// in EverQuest Legends; what it buys is that <see cref="MetricFixedLength"/> is an
    /// invariant with no "for plausible values" attached to it.</summary>
    public static string DpsText(in HudGlanceInput input) =>
        Metric(input.CurrentDps > 0 ? input.CurrentDps : input.SessionDps, "dps");

    /// <summary>The player-inserted slot, in the same fixed shape as its neighbours — so
    /// inserting it changes the measured width ONCE, on the player's own drop (a
    /// player-driven resize, which trap 12 permits), and every tick after that repaints in
    /// place at <see cref="MetricReservedWidth"/>.
    ///
    /// It formats <see cref="HudGlanceInput.PetDps"/> and never derives it: the sum lives on
    /// the snapshot, and the cell down in the tray formats the same number its own way
    /// (trap 4).</summary>
    public static string PetText(in HudGlanceInput input) => Metric(input.PetDps, "dps");

    /// <summary>The HPS slot's number — session healing per combat second, yours.</summary>
    public static string HealingText(in HudGlanceInput input) => Metric(input.Hps, "hps");

    /// <summary>The XP-rate slot's number.
    ///
    /// One decimal, because an XP rate below 10%/hr is the normal case at level and
    /// "0%/hr" would read as broken while the number is really 0.4. Ten characters like
    /// every other slot, in a wider box (<see cref="ExperienceReservedWidth"/>) because
    /// '%' and '/' are wider glyphs than the spaces the rates pad with.</summary>
    public static string ExperienceText(in HudGlanceInput input) =>
        $"{Math.Clamp(input.XpPerHour, 0, 9999.9),6:0.0}%/hr";

    /// <summary>"  1234 dps" — right-aligned in six columns so the digits neither jitter
    /// nor change the measured width, and exactly <see cref="MetricFixedLength"/>
    /// characters for every value.</summary>
    private static string Metric(double value, string unit) =>
        $"{Math.Clamp(value, 0, 999999),6:0} {unit}";

    /// <summary>The whole glance in one call: decide the row's MEMBERSHIP, then format
    /// every slot from the SAME input. One moment, one decision — a view that asked which
    /// slots there were and what they read separately could be handed two (trap 4).
    ///
    /// <b>The ORDER is this list's order and the view does not re-sort it</b>: DPS, the
    /// inserted pet slot, HPS, then the XP rate. The two fixed ends are what make the
    /// middle safe to grow — DPS has been slot one since SA-1 and the XP rate has been the
    /// last thing on the row since SA-1, so the pet insertion POINT (between DPS and
    /// whatever follows) and the ARRIVAL of HPS both land between two slots whose meaning
    /// never moves.</summary>
    public static HudGlanceReadout Read(HudGlanceState state, in HudGlanceInput input)
    {
        var healing = HealingShown(state.Healing, in input);
        var slots = new List<HudGlanceSlot>(4)
        {
            new(DpsKey, DpsText(in input), DpsIcon, MetricReservedWidth),
        };
        if (input.PetInserted)
            slots.Add(new(MiniBarPresentation.PetKey, PetText(in input), PetIcon,
                MetricReservedWidth));
        if (healing)
            slots.Add(new(HpsKey, HealingText(in input), HealingIcon, MetricReservedWidth));
        slots.Add(new(XpKey, ExperienceText(in input), ExperienceIcon,
            ExperienceReservedWidth));
        return new HudGlanceReadout(new HudGlanceState(healing),
            NameText(input.CharacterName), slots);
    }

    /// <summary>The glance straight off a snapshot — what the widget actually calls, so
    /// the mapping from session fields to glance inputs exists once rather than in every
    /// host that ever draws a HUD.
    ///
    /// <paramref name="petInserted"/> has no default ON PURPOSE: it is
    /// <c>AppSettings.HudGlancePet</c>, and a host that forgot it would silently draw the
    /// row this release shipped to widen. A missing argument is a build error; a defaulted
    /// one is a feature that quietly never arrives.</summary>
    public static HudGlanceReadout Read(HudGlanceState state, Core.StatsSnapshot s,
        string? characterName, bool petInserted) =>
        Read(state, new HudGlanceInput(
            characterName, s.CurrentDps, s.SessionDps, s.Hps, s.XpPerHour,
            s.Effort.DamageDone, s.Effort.HealingDone, s.Effort.DamageDoneInResumeWindow,
            s.PetDps, petInserted));
}
