using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// One meter, chosen: which rows a Damage / Healing / Pet surface shows, over how many
/// seconds, under what title, with what line beneath it — and what it says when there is
/// nothing.
///
/// The rows and the seconds travel together on purpose. They are the numerator and the
/// denominator of the rate the surface prints, and a caller that picked one without the
/// other would print a fight's damage over the session's combat time.
/// </summary>
/// <param name="Empty">The whole-surface empty explanation, or null when there are rows.
/// Non-null does NOT mean "nothing happened": a bard mid-song has regen ticks the game
/// logs no amounts for, so healing can be real and every HPS row still absent.</param>
/// <param name="FightName">The pull these rows are from, or null in Session scope. It is
/// carried separately from <paramref name="Subtext"/> — which also names it — because the
/// repaint gate needs it and must not be handed the subtext: the Pet title carries a charm
/// hold that TICKS, and a gate key with a ticking value in it is no gate at all (trap 8).</param>
public sealed record LiveMeter(
    string Title, IReadOnlyList<SourceDamage> Rows, double Seconds,
    string RateLabel, string Subtext, string? Empty, string? FightName);

/// <summary>
/// **The Live room's words and the meter selection behind them, in one place because there
/// are two hosts for every one of them.**
///
/// Damage, Healing and Pet each ship twice today: as a floating <c>BreakoutWindow</c> the
/// widget opens when it is minimized, and as a room in the Evolved shell. Those two are
/// exactly the shape trap 33 is about — *"when a value has two producers, give them one
/// builder"* — and the failure would not be a stale answer, it would be two different
/// answers, each current, differing on which rows a Fight scope means or on whether a
/// regen-only session counts as empty. The breakout is where all of this was written, and
/// it is where it stays working: this class is the extraction, not a rewrite, and
/// <c>BreakoutWindow.Update</c> now asks it rather than deciding again.
///
/// It is in <c>UI.Shared</c> for the reason every window sum in this repo is: the WPF
/// layer has no unit tests (docs/TestPlan.md §5), so a rule left inline in a window is a
/// rule nothing can check.
///
/// **Nothing here says "x ago" and nothing counts down.** The Live room paints on the
/// widget's one-second tick, and a value that ticks makes every tick a rebuild (trap 8) —
/// on a <c>SizeToContent</c> surface it also makes every tick a resize (trap 12, the #173
/// keyboard-killer). Durations are fixed shapes; ages are not printed at all.
/// </summary>
public static class LivePresentation
{
    // ---- the three meters ------------------------------------------------------

    /// <summary>
    /// Which rows this kind and scope means, and the words around them.
    ///
    /// <paramref name="kind"/> is a <see cref="BreakoutPresentation"/> constant —
    /// <c>Damage</c>, <c>Healing</c> or <c>Pet</c> — and is a STRING for the reason that
    /// file already gives: the breakout enum is a WPF type, so keying on it would put this
    /// decision back inside the layer that cannot test it. Anything else is treated as
    /// Damage, which is what the window's own switch did.
    ///
    /// <paramref name="fightScope"/> is the Fight/Session axis. Fight means the current or
    /// last pull and its own duration; Session means everything since the log was picked
    /// up, over combat time — *"the honest camp number, because medding doesn't dilute
    /// it"*, in <see cref="CombatPresentation"/>'s words.
    /// </summary>
    public static LiveMeter Meter(string kind, StatsSnapshot s, bool fightScope, DateTime now)
    {
        var f = s.LastFight;
        var (title, rows, secs, rate) = kind switch
        {
            BreakoutPresentation.Healing => (BreakoutPresentation.Title(BreakoutPresentation.Healing),
                fightScope ? f?.HealsBySpell ?? [] : s.HealsBySpell,
                fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "hps"),
            BreakoutPresentation.Pet => (BreakoutPresentation.PetTitle(s.PetName, s.CharmedSince, now),
                fightScope ? f?.PetAbilities ?? [] : s.PetAbilities,
                fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
            _ => (BreakoutPresentation.Title(BreakoutPresentation.Damage),
                fightScope ? f?.ByAbility ?? [] : s.DamageBySource,
                fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
        };

        var total = rows.Sum(r => r.Total);
        var perSecond = total / Math.Max(1, secs);
        // Hymn/regen ticks carry no amounts in the log, so they can never join the HPS
        // rows — but a bard mid-song staring at "no healing" reads it as broken (David,
        // live test 2026-08-06). Counted where healing lives; estimated when attributed.
        var regen = kind == BreakoutPresentation.Healing && s.RegenTicks > 0
            ? s.RegenEstimatedHealed > 0
                ? $" · est. ~{s.RegenEstimatedHealed:N0} regen ({s.RegenTicks} ticks)"
                : $" · {s.RegenTicks} regen ticks"
            : "";
        var subtext = (fightScope
            ? f is null ? "No fights yet"
                : $"{f.Name} · {f.DurationSeconds:0}s · {f.Outcome} · {perSecond:0.#} {rate}"
            : $"Session · {s.CombatSeconds / 60:0}m in combat · {perSecond:0.#} {rate}") + regen;

        return new LiveMeter(title, rows, secs, rate, subtext,
            rows.Count == 0 ? EmptyMeter(kind, s) : null,
            fightScope ? f?.Name : null);
    }

    /// <summary>What a meter with no rows says. The two healing cases come first because
    /// they are the ones that are not "nothing happened" — a regen tick is real healing the
    /// game logs no amount for, and saying "no healing seen yet" over a bard's song is the
    /// app calling itself broken.</summary>
    public static string EmptyMeter(string kind, StatsSnapshot s) => kind switch
    {
        BreakoutPresentation.Healing when s.RegenEstimatedHealed > 0 =>
            $"{s.RegenSpell}: est. ~{s.RegenEstimatedHealed:N0} healed over {s.RegenTicks} ticks.\n" +
            "The game logs no amounts — this is ticks × your Options\nhp/tick (or the wiki base), so it stays labeled est.",
        BreakoutPresentation.Healing when s.RegenTicks > 0 =>
            $"{s.RegenTicks} hymn/regen ticks — the game logs no amounts for these,\nso they count but can't join the HPS rows.",
        BreakoutPresentation.Healing => "No healing seen yet.",
        BreakoutPresentation.Pet => "No pet damage seen yet.",
        _ => "No damage seen yet.",
    };

    /// <summary>The repaint gate's key: the scope, the sort, the fight, its length and
    /// every row's total. Rebuilding ten bar rows a second is cheap and pointless between
    /// fights, and it throws away whatever the pointer was over.
    ///
    /// **No countdown and no age in it** — trap 8's rule, which is why the fight's duration
    /// is rounded to whole seconds rather than carried at tick resolution.</summary>
    public static string MeterSignature(string kind, bool fightScope, string sort, LiveMeter meter) =>
        $"{kind}|{fightScope}|{sort}|{meter.FightName}|{meter.Seconds:0}|" +
        string.Join(",", meter.Rows.Select(r => $"{r.Name}:{r.Total}"));

    // ---- the room's tab badges -------------------------------------------------

    /// <summary>
    /// The strip's headline numbers — what each tab is worth at a glance, so the room
    /// answers before it is opened.
    ///
    /// **Counts and rates, never countdowns** (<see cref="WorldSurface.LauncherSummary"/>'s
    /// rule, for the same two reasons: trap 8 wakes every phone and trap 12 resizes a
    /// window). A tab with nothing to say gets null rather than a zero — a strip of
    /// "0 dps · 0 hps · 0 kills" on a fresh launch is noise where an unbadged chip is an
    /// honest "not yet".
    /// </summary>
    public static IReadOnlyList<LiveTabHeader> Tabs(
        StatsSnapshot s, int killKinds, int raidsDefeated, int raidsTotal) =>
        LiveSurface.Tabs(
            damage: s.SessionDps > 0 ? $"{s.SessionDps:0.#} dps" : null,
            healing: s.Hps > 0 ? $"{s.Hps:0.#} hps"
                : s.RegenTicks > 0 ? $"{s.RegenTicks} ticks" : null,
            pet: s.PetAbilities.Count > 0 && s.PetName.Length > 0 ? s.PetName : null,
            // The fight's own name, not its length: a duration ticks while the pull is
            // live, and a tab label that changes width every second is trap 12 wearing a
            // chip. Null between fights rather than "no fight" — the tab still opens.
            timeline: s.LastFight?.Name,
            kills: killKinds > 0 ? $"{killKinds} kind{(killKinds == 1 ? "" : "s")}" : null,
            // The same "2 / 21" the Raids card's own header carried under Progress, so the
            // badge did not change meaning when the room did.
            raids: raidsTotal > 0 ? $"{raidsDefeated} / {raidsTotal}" : null);

    // ---- the session report ----------------------------------------------------

    /// <summary>
    /// The room's heading: what this sitting IS.
    ///
    /// **Live's own words, and NOT <see cref="SessionSummary.Headline"/>'s** — a signed
    /// requirement rather than a style choice (Bevel §2, Helm-signed 2026-09-05). Home's
    /// in-progress line is a REFUSAL: it says a session is running and stops, because the
    /// meters are Live's job and Home is a desk surface. Reusing it here would put Home's
    /// "EQBuddy will record it when the session ends" on the one room whose entire point is
    /// that the numbers are on screen now.
    /// </summary>
    public static string Headline(LiveSession session) => session.State switch
    {
        RecentSessionState.InProgress => session.Zone.Length > 0
            ? $"This sitting — {session.Zone}"
            : "This sitting",
        RecentSessionState.Ended => session.Zone.Length > 0
            ? $"Nothing running — last was {session.Zone}"
            : "Nothing running",
        _ => "No sitting yet",
    };

    /// <summary>
    /// The line under the heading: the session's own facts, in the order a meter reads
    /// them — how long, what died, what it cost, how hard.
    ///
    /// **The finished case is the past tense of the same line rather than a different
    /// sentence**, because the numbers are the same numbers; only their source changed
    /// (<see cref="SessionSummary.LiveOf"/> reads the stored row rather than the snapshot).
    /// A part with nothing to say is omitted rather than printed as a zero.
    /// </summary>
    public static string Detail(LiveSession session) => session.State switch
    {
        RecentSessionState.NeverPlayed =>
            "Nothing has been recorded for this character yet. The meters below fill in as "
            + "your log sees the fight.",
        _ => string.Join(" · ", Facts(session)),
    };

    /// <summary>The session's facts. Deaths appear only when there are some — a "0 deaths"
    /// on a clean session is a boast the surface does not need to make, and it is the one
    /// number whose absence is the good news.</summary>
    public static IEnumerable<string> Facts(LiveSession session)
    {
        yield return LevelHistory.FormatGap(session.Elapsed);
        yield return $"{session.Kills} kill{(session.Kills == 1 ? "" : "s")}";
        if (session.Deaths > 0)
            yield return $"{session.Deaths} death{(session.Deaths == 1 ? "" : "s")}";
        if (session.Dps > 0) yield return $"{session.Dps:0.#} dps";
        if (session is { State: RecentSessionState.Ended, EndedLocal: { } ended })
            yield return SessionSummary.Stamp(ended);
    }

    // ---- the room's empty states -----------------------------------------------

    /// <summary>
    /// **The whole-room empty, and the ONE state that gets one.**
    ///
    /// A live session with nothing in it yet is a different fact from Home's "no character
    /// is known" — Home's whole-room empty already covers that one level up, before any
    /// room is drawn — so Live's version is narrower and says what will change it. Six
    /// separate "nothing yet" tabs would be six ways of saying the one thing that matters,
    /// which is the argument <c>RoomEmptyState</c> was built on for Home.
    ///
    /// It is not the same as a single tab being empty: Damage with no rows still shows its
    /// strip, its scope toggle and its own explanation, because the player who switched to
    /// it asked about damage specifically.
    /// </summary>
    public const string EmptyHeading = "Nothing has happened this session yet";

    public const string EmptyExplanation =
        "EQBuddy is following your log and has seen no fight, no kill and no heal since it "
        + "started. Pull something and the meters fill in as the lines arrive — nothing "
        + "here needs a command or an import.";

    /// <summary>The Raids tab's own empty is the card's, not this one: it names the
    /// <c>/outputfile achievements</c> import and ships the ⧉ copy of it, which is the
    /// affordance a room-level empty would have swallowed (trap 34 — a missing control is
    /// invisible to a diff, a build and a screenshot alike). So the whole-room empty is
    /// gated on the session being genuinely untouched AND the raid ledger having nothing,
    /// and this names the second half rather than leaving it implied.</summary>
    public static bool RoomIsEmpty(LiveSession session, StatsSnapshot s, int raidsDefeated) =>
        session.State != RecentSessionState.Ended
        && session.Kills == 0 && raidsDefeated == 0
        && s.DamageDealt == 0 && s.HealingDone == 0
        && s.PetAbilities.Count == 0 && s.LastFight is null;
}
