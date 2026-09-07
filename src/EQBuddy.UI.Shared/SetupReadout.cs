namespace EQBuddy.UI.Shared;

/// <summary>
/// The words and the one rule behind the Evolved shell's first-run **Setup** — OE-6
/// (owner LOCK B through Helm, #355; Bevel's pre-design, #356; Fable's seat, #358).
///
/// **Setup is a second HOST of <see cref="HomeReadout.Readiness"/>, not a second
/// checklist**, and that is the whole design rather than an implementation note. Home's
/// Readiness block already answers "what has EQBuddy not been told yet" — per dump, with
/// what it feeds, when it last landed, and the ⧉ copy of the command that produces it.
/// A first-run screen that hand-rolled the same rows would be two producers of one list
/// (trap 33), drifting the day a fourth dump is added — **which happened on 2026-09-07,
/// and the only thing that moved was one row in <see cref="HomeReadout.Readiness"/> and
/// the two count literals in the tests that pin it.** Nothing in this file or in
/// <c>SetupView</c> was edited to gain OE-5's spellbook row; the prose that COUNTED the
/// rows was the one part that had to (see <see cref="Lead"/>). There are no new enums here
/// and no second switch
/// over <c>OutputfileKind</c>: this file holds the SENTENCES and the auto-launch
/// PREDICATE, and the rows themselves come from the one place that has ever built them.
///
/// It is here rather than in the modal for this repo's standing reason: the WPF layer has
/// no unit tests, so a state rule left inline in a window is one nothing can check.
/// </summary>
public static class SetupReadout
{
    /// <summary>
    /// Whether the shell should open Setup by itself.
    ///
    /// **The predicate is a fact about the DUMPS, not a new "first ever launch" flag**
    /// (Bevel, adopted by the seat). A written-once boolean with a lone reader is exactly
    /// the shape trap 20's bugs are made of, and it answers the
    /// wrong question anyway: a player who reinstalls, or who has been running EQBuddy for
    /// a week without ever typing an <c>/outputfile</c> command, is in the same state as a
    /// brand-new one. So the ask stops when the dumps satisfy it, without anybody having to
    /// clear a flag. (<c>DeadSettingTests</c> is the guard that names that class of bug.)
    ///
    /// **EVERY row never scanned, not "any"** — Setup is an onboarding screen, and a player
    /// who has run one of them has already met the mechanism. Home's Readiness block
    /// carries the remainder, in the room the shell opens on.
    ///
    /// **The optional row counts here too, and that is deliberate rather than an accident
    /// of taking the whole list.** OE-5's spellbook row is the one dump EQBuddy does not
    /// need; a player who has run only that one has still learned exactly the thing this
    /// screen teaches — where the commands go and what happens after. Special-casing it out
    /// would be this file enumerating <c>OutputfileKind</c>, which is the one thing its own
    /// summary above promises it never does.
    ///
    /// **An EMPTY list is false, and that is the case worth writing down.** No rows means
    /// no character (<see cref="HomeReadout.Readiness"/> returns nothing before EQBuddy has
    /// seen a log line), and "every row is never-scanned" is vacuously true of nothing at
    /// all. That profile has a better answer already: Home's whole-room empty, which names
    /// <c>/log on</c> and the Logs folder — the two things that have to happen BEFORE any
    /// of these commands is worth typing. Opening Setup over it would ask for step two
    /// while step one is still missing.
    /// </summary>
    /// <param name="rows">The rows <see cref="HomeReadout.Readiness"/> built — passed in
    /// rather than computed here, so there is exactly one producer of them.</param>
    /// <param name="dismissed">The player's persisted "stop doing that". Read here and
    /// written by the modal's own button, in the same change (trap 20's polarity).</param>
    public static bool ShouldAutoShow(IReadOnlyList<ReadinessRow> rows, bool dismissed) =>
        !dismissed
        && rows.Count > 0
        && rows.All(row => row.State == ReadinessState.NeverScanned);

    /// <summary>The screen's own name, and it is a job rather than a noun: the player is
    /// about to DO something rather than read about it. It carries no COUNT, which is why
    /// it needed no edit when OE-5's fourth row arrived — the rows underneath are the count,
    /// and they are built somewhere else.</summary>
    public const string Headline = "Set EQBuddy up";

    /// <summary>
    /// What this screen is for, in the inventory-dump voice: what is missing, what to do,
    /// where, and what happens next.
    ///
    /// It does not promise that EQBuddy is broken without these — it is not, and saying so
    /// would be the nag the never-scanned/healthy split exists to avoid. The commands are
    /// named as what they BUY, and the rows underneath say which is which.
    ///
    /// **It counts nothing, and that is the repair OE-5 asked for.** This sentence said
    /// "Three commands" while the rows under it were built somewhere else entirely, so the
    /// day <see cref="HomeReadout.Readiness"/> gained its fourth the screen contradicted
    /// itself — a number in prose is a second producer of a fact the list already owns
    /// (trap 33, in words rather than in code). The row that is optional says so in its own
    /// "feeds" line, which is the only place that can say it per dump.
    /// </summary>
    public const string Lead =
        "Your log tells EQBuddy what happens while you play. A few commands tell it the "
        + "rest — what you are carrying, what you finished before EQBuddy, and where you "
        + "stand. Copy one, paste it into the game's chat, and EQBuddy reads the file the "
        + "game writes without being asked again.";

    /// <summary>The one way out, and it persists — see <see cref="ReopenNote"/> for why one
    /// button rather than a "later" and a "never".</summary>
    public const string Done = "Got it";

    /// <summary>
    /// **What closing this screen actually means, said on the screen.** There is ONE close,
    /// and it persists: a "not now" beside a "never" would be two paths deciding one
    /// question, and the "not now" path is the one that turns an onboarding screen into
    /// something a player meets every launch. So the button says what it does, and this
    /// line says where the same answer lives afterwards — both of them, because Home's
    /// Readiness block is the surface that keeps asking and Settings is the way back to
    /// this one.
    /// </summary>
    public const string ReopenNote =
        "EQBuddy will not open this by itself again. Home lists whatever is still missing, "
        + "and Settings → Behavior → Setup brings this back.";

    /// <summary>The re-open entry's label on Settings' Behavior tab. **Not a fifth tab** —
    /// four is the signed count (I-11/#331) and Behavior's own territory already includes
    /// the launch tour, which is the same job for a different gap.</summary>
    public const string BehaviorLabel = "Setup…";

    /// <summary>The line beside it. It names the things rather than the file names — which
    /// are EQBuddy's problem and not the player's — and, like <see cref="Lead"/>, it does
    /// not count them.</summary>
    public const string BehaviorNote =
        "The first-run screen: the commands that tell EQBuddy what you are carrying, "
        + "what you have finished, and where you stand. It opens by itself once, on a "
        + "profile that has run none of them.";

    /// <summary>The heading over the rows, so the block is not a list with no sentence over
    /// it. It is not <see cref="HomeReadout.ReadinessHeadline"/>: that one carries a "not
    /// run yet" count, which on this screen is always every row and is therefore a
    /// scoreboard for the state the screen already assumes.</summary>
    public const string RowsHeadline = "What EQBuddy is waiting for";
}
