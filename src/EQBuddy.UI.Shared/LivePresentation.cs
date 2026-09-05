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
    /// **Counts and rates, never countdowns** (the World theme's rule — see
    /// <see cref="WorldTheme"/> on why Camps and Path carry no badge; it was written on
    /// <c>WorldSurface.LauncherSummary</c> until that card was cut on 2026-09-05, and it
    /// holds for the same two reasons: trap 8 wakes every phone and trap 12 resizes a
    /// window). A tab with nothing to say gets null rather than a zero — a strip of
    /// "0 dps · 0 hps · 0 kills" on a fresh launch is noise where an unbadged chip is an
    /// honest "not yet".
    /// </summary>
    public static IReadOnlyList<LiveTabHeader> Tabs(
        StatsSnapshot s, int killKinds, int raidsDefeated, int raidsTotal, int pulls = 0) =>
        LiveSurface.Tabs(
            damage: s.SessionDps > 0 ? $"{s.SessionDps:0.#} dps" : null,
            healing: s.Hps > 0 ? $"{s.Hps:0.#} hps"
                : s.RegenTicks > 0 ? $"{s.RegenTicks} ticks" : null,
            pet: s.PetAbilities.Count > 0 && s.PetName.Length > 0 ? s.PetName : null,
            // The fight's own name, not its length: a duration ticks while the pull is
            // live, and a tab label that changes width every second is trap 12 wearing a
            // chip. Null between fights rather than "no fight" — the tab still opens.
            timeline: s.LastFight?.Name,
            // The PEAK rather than the elapsed span, and for the rule directly above: a
            // "27m" that becomes "28m" is a width change on a clock. A peak only moves when
            // a minute actually beats the best one, and it is the number the graph's own
            // caption leads with, so the chip and the body agree.
            pace: PeakDps(s.DamageTimeline) is { } peak ? $"peak {peak:0.#} dps" : null,
            // The pull COUNT is handed in for the same reason the kill kinds and the raid
            // clears are: grouping the session's encounters is the room's work, done once
            // per paint, and a badge that regrouped them a second time would be a second
            // producer of one number (trap 33) as well as twice the work.
            encounters: pulls > 0 ? $"{pulls} pull{(pulls == 1 ? "" : "s")}" : null,
            kills: killKinds > 0 ? $"{killKinds} kind{(killKinds == 1 ? "" : "s")}" : null,
            // The same "2 / 21" the Raids card's own header carried under Progress, so the
            // badge did not change meaning when the room did.
            raids: raidsTotal > 0 ? $"{raidsDefeated} / {raidsTotal}" : null);

    // ---- Pace: the whole sitting's shape ----------------------------------------

    /// <summary>The best MINUTE the sitting has had, as a rate. Null when the timeline
    /// cannot be drawn at all, so the badge and the graph agree about whether there is
    /// anything to see — <see cref="HistoryPresentation.BuildDpsGraph"/> refuses under two
    /// points, and a chip promising a peak over a tab that draws nothing is the
    /// chip-disagrees-with-body defect #227 already cost us once.
    ///
    /// Per-minute buckets over 60, which is the same arithmetic the graph does; the two
    /// read the same list and neither is allowed to invent a different denominator.</summary>
    public static double? PeakDps(IReadOnlyList<TimelinePoint> timeline)
    {
        if (timeline.Count < 2) return null;
        var peak = timeline.Max(p => p.Damage) / 60.0;
        return peak > 0 ? peak : null;
    }

    /// <summary>The Pace tab's caption — the v1 History window's own sentence, word for
    /// word, because it is the same graph read from a live snapshot instead of a stored
    /// one. Keeping the words identical is what lets a player who knows the studio
    /// recognise this without being told.
    ///
    /// **The clock times are the session's own and do not tick**: they are the first and
    /// last point the timeline holds, which move a minute at a time rather than a second at
    /// a time, and only when a new minute has damage in it.</summary>
    public static string PaceCaption(HistoryGraph graph) =>
        $"DPS over time — peak {graph.PeakDps:0.#}/s " +
        $"({graph.Start:h:mm tt}–{graph.End:h:mm tt}, per minute)";

    public const string EmptyPace =
        "Not enough of this sitting has happened to draw a line yet. The graph plots one "
        + "point per minute of the session, so it appears once EQBuddy has seen damage in "
        + "two different minutes.";

    /// <summary>The repaint gate for the Pace graph. Point COUNT and the total, which
    /// together move exactly when the polyline would change shape and never otherwise —
    /// no elapsed time and no age in it (trap 8), so a quiet minute costs no redraw.
    /// </summary>
    public static string PaceSignature(IReadOnlyList<TimelinePoint> timeline) =>
        $"{timeline.Count}|{timeline.Sum(p => p.Damage)}";

    // ---- Encounters: every pull of this sitting ----------------------------------

    public const string EmptyEncounters =
        "No pull has finished yet. Each one lands here when the fight closes — with what "
        + "you dealt, what hit you, what healed, and a ⧉ copy of the whole encounter as "
        + "Discord-ready text. The fight you are in is on Damage until it ends.";

    /// <summary>
    /// The repaint gate for the pull list, and the one that has to be right: rebuilding
    /// this list throws away every pull a player has EXPANDED, and the room paints once a
    /// second.
    ///
    /// **Count plus the last pull's start, and neither of them ticks.**
    /// <c>StatsSnapshot.Encounters</c> is appended to when a fight CLOSES, so a finished
    /// pull's start and duration are fixed facts — the value only moves when a new pull
    /// lands, which is exactly when the list must be rebuilt. Putting a duration or an "x
    /// ago" in here would make every tick a rebuild (trap 8) and every rebuild a lost
    /// expansion.
    /// </summary>
    public static string EncountersSignature(IReadOnlyList<PullInfo> pulls) =>
        $"{pulls.Count}|{(pulls.Count > 0 ? pulls[^1].Start.Ticks : 0)}";

    /// <summary>A pull's stable identity across rebuilds — its start instant, which is
    /// unique per pull and fixed once the fight has closed. The expansion set is keyed on
    /// this rather than on the list INDEX: a new pull is appended at the end today, and an
    /// index would still be the wrong key the day the order or the grouping gap changes.
    /// </summary>
    public static string PullKey(PullInfo pull) => pull.Start.Ticks.ToString();

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
