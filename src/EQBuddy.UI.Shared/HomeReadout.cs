using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Whether EQBuddy knows who it is following. Two states, and the empty one is
/// the only empty state in the whole shell that a profile with NO game data of any kind
/// can reach — every other room assumes a character is already known.</summary>
public enum IdentityState { NoCharacter, Following }

/// <summary>
/// Whether one <c>/outputfile</c> dump has ever been produced.
///
/// **Two states, not three, and the missing third is deliberate.** There is no "stale"
/// here. Bevel's Home pre-design says never-scanned and healthy *"are two DIFFERENT states
/// with the same 'no problem to report' shape ... treating them the same either nags a
/// healthy player or reassures one who has never run the command"*, and it stops there —
/// it does not ask for an age past which a dump is wrong, and nobody has signed one. A
/// threshold invented here would be a policy the player never agreed to, arriving as a
/// nag; the date is reported and the reading is theirs.
/// </summary>
public enum ReadinessState
{
    /// <summary>The command has never been run. This is the one that earns a call to
    /// action, and the ⧉ copy that goes with it.</summary>
    NeverScanned,

    /// <summary>A dump exists, and Home says when it was written.</summary>
    Scanned,
}

/// <summary>One line of the Readiness block: what a dump feeds, when it last landed, and
/// where in the shell the surface that uses it lives.</summary>
/// <param name="Feeds">What stops working without it, in the player's terms — never the
/// filename, which is EQBuddy's problem and not theirs.</param>
/// <param name="Address">The <c>page:room</c> the dump's surface lives at, so the row can
/// be a deep link through the SAME <c>Navigate</c> the rail uses. Empty when the room has
/// not landed yet — filtered against <c>ShellPages.Landed</c> in
/// <see cref="HomeReadout.Readiness"/>, the same list the rail draws from — and equally
/// empty when the dump's surface is not a ROOM at all, which is the spellbook's case: buff
/// countdowns are drawn on the widget's HUD, and pointing "Open" at some room that does not
/// show them would be the dead affordance <c>ShellPages.Landed</c> exists one level up to
/// refuse.
///
/// **Since DRA-63 this is the ONLY navigation Home's body offers**, which raises what it is
/// worth rather than lowering it: the "Go to" block that used to sit under it was the other
/// reader of <c>Landed</c> inside a room, and with it gone these addresses are the whole of
/// what <c>shellHomeDeadLinks</c> is asked about.</param>
public sealed record ReadinessRow(
    OutputfileKind Kind, string Name, string Feeds, ReadinessState State,
    DateTime? ScannedAt, string Address);

/// <summary>
/// The words and the arithmetic behind the Evolved shell's HOME room — **three blocks:
/// Identity · Readiness · Recent session.**
///
/// **The fourth was "Deep links", and the Founder cut it on 2026-09-11 (DRA-63 smoke,
/// through Helm).** Bevel's signed door 1 locked four blocks on a shell whose rail had one
/// room in it; by the time every room had landed, the block was a second copy of the rail
/// sitting under the fold, naming the same six doors the rail already draws down the left
/// edge of the same window. **The room now matches the sentence it has always been
/// described by** — <c>ShellPages.Describe(Home)</c> is *"Who you are playing, what is
/// ready, and where you left off"*, three clauses, written before the room existed and
/// never amended to promise a fourth thing.
///
/// It is here rather than in the room for this repo's standing reason: the WPF layer has
/// no unit tests, so a sentence or a state rule left inline is one nothing can check. What
/// the room keeps is the wiring.
///
/// **Each of the three blocks can be empty independently of the other two**, which is why
/// there is no single "nothing here yet" for the room. Bevel's table is explicit that a
/// blanket empty would be wrong: a player with a character and no dumps has two of three
/// blocks full, and telling them the room is empty is a worse answer than telling them
/// which one thing is missing.
///
/// **Home is the first room whose most-likely-seen state is its empty one**, and on a
/// profile that has never seen a log line its Identity block is the first thing a
/// brand-new player's Evolved shell ever shows. So every empty here follows the
/// inventory-dump voice: what is missing, what to do, where, and what happens next. A
/// blank panel is a silent no-op with better manners.
/// </summary>
public static class HomeReadout
{
    // ---- identity ---------------------------------------------------------------

    public static IdentityState Identity((string Server, string Character) identity) =>
        identity.Character.Length == 0 ? IdentityState.NoCharacter : IdentityState.Following;

    /// <summary>The Identity block's heading: the character, or the fact that there is
    /// not one.</summary>
    public static string IdentityHeadline((string Server, string Character) identity) =>
        Identity(identity) == IdentityState.NoCharacter
            ? "No character yet"
            : identity.Character;

    /// <summary>The line under it. Zone rather than level or class, because zone is the
    /// one thing that changes between sittings and is what "where you left off" means to
    /// somebody about to play.</summary>
    public static string IdentityDetail(
        (string Server, string Character) identity, string zone) =>
        Identity(identity) == IdentityState.NoCharacter
            ? EmptyIdentity
            : string.Join(" · ", new[] { identity.Server, zone }
                .Where(part => part.Length > 0));

    /// <summary>
    /// **The one empty state that can happen with zero game data**, and the one being
    /// compared — whether we asked for that or not — against the eight-page tour it is the
    /// eventual replacement for. What is missing, the action, where, and what happens next,
    /// in that order.
    ///
    /// It names <c>/log</c> in prose rather than offering a ⧉ copy of it: the log toggle is
    /// not an <c>/outputfile</c> dump EQBuddy reads back, so there is nothing for a copy
    /// button to make happen — and the other half of the answer (pointing EQBuddy at the
    /// folder) is a Settings trip that no clipboard can shorten.
    /// </summary>
    public const string EmptyIdentity =
        "EQBuddy has not seen a character log yet. Turn logging on in game with /log on, "
        + "then point EQBuddy at your Logs folder in Options. Everything else here fills "
        + "itself in from that one file.";

    // ---- class (DRA-66) ---------------------------------------------------------

    /// <summary>
    /// The class line under the character's name — what EQBuddy thinks this character IS,
    /// always saying where the reading came from, because "Warrior · Druid" is a different
    /// sentence depending on whether the game said it, the player did, or a heuristic
    /// guessed it (the same rule the quest window's identity note has carried since #104).
    ///
    /// **The list and source arrive RESOLVED** — <c>CharacterClasses.Resolve</c> through
    /// the one <c>ClassSourceFor</c> every class-aware surface reads — so this line can
    /// never disagree with the quest window or the phone about who the character is
    /// (trap 33: two callers with different arguments are two current answers).
    /// </summary>
    public static string ClassLine(IReadOnlyList<string> classes, ClassSource source) =>
        classes.Count == 0
            ? EmptyClass
            : $"{string.Join(" · ", classes)} ({CharacterClasses.SourceLabel(source)})";

    /// <summary>The class line before anything knows (signed plan D2): how the answer
    /// arrives on its own, and that the Achievements ⧉ row one block down answers it at
    /// once — pointing at the row that already ships the command, so no new command
    /// literal and no <c>GameCommandsTests</c> row.</summary>
    public const string EmptyClass =
        "Class not known yet — EQBuddy reads it from your log as you play, and the "
        + "Achievements catch-up below answers it at once.";

    /// <summary>The door into the class editor. "Set", not "correct" or "override" — the
    /// same ruling that reworded the quest picker: being told to override your own
    /// character is a strange thing for an app to say.</summary>
    public const string EditClasses = "Set class…";

    /// <summary>The door's label while the editor is open. It closes the strip; the picks
    /// themselves were saved the moment they were ticked, and a button reading "Save"
    /// over already-saved state would be claiming a job it does not do.</summary>
    public const string EditClassesDone = "Done";

    /// <summary>Over the editor's chips: what stating does, and the cap — named up front
    /// so the fourth click refusing is an announced rule rather than a silent no-op. The
    /// limit is the game's (up to three active classes), through
    /// <see cref="CharacterClasses.Max"/>.</summary>
    public const string ClassEditorNote =
        "Tick what this character actually is — up to three, the game's own limit. "
        + "While anything is ticked here, EQBuddy stops guessing.";

    /// <summary>The way back (the plan's own words), offered only while a statement
    /// exists: clearing it returns the line to EQBuddy's own reading (the dump if one has
    /// landed, the log otherwise). Without this row a correction would be one-way, and a
    /// player who set it wrong once would be stuck telling EQBuddy forever.</summary>
    public const string ClearStated = "Let EQBuddy work it out";

    /// <summary>What replaces the editor when the source is the achievements dump (plan
    /// D4): a sentence saying WHY there is nothing to tick, not disabled chips — trap 17,
    /// a disabled control with no visual is invisible, and even a dimmed one says nothing
    /// about why. The repair it points at is the Achievements row one block down.</summary>
    public const string DumpAnswersClass =
        "Your achievements dump answers this — run it again below if it is out of date.";

    // ---- readiness --------------------------------------------------------------

    /// <summary>
    /// The dumps Home reports on, in the order they matter to a player who is about to
    /// play: bags first (it is the one that changes every session), then the two that
    /// answer "what have I already finished", then the optional one.
    ///
    /// **The list is here and not in the room** so a fourth dump joins it in one place —
    /// and so the addresses can be filtered against <see cref="ShellPages.Landed"/> rather
    /// than hand-checked, which is the rail's own refusal to draw a dead affordance applied
    /// inside a room's body. The fourth one arrived on 2026-09-07 (OE-5) and the promise
    /// held: one row here, and both hosts of this list — Home and the first-run Setup that
    /// OE-6 builds on top of it — got it without either of them being edited.
    ///
    /// **The spellbook row is LAST and it is worded as optional**, which is a decision and
    /// not a default. The other three are things EQBuddy cannot work out any other way: a
    /// hand-in never appears in the log, a standing never appears in the log, and bags are
    /// invisible until you dump them. The spellbook only SHARPENS something that already
    /// works — buff countdowns run off landing and fade lines whether you ever run it or
    /// not — so a row that asked for it in the same voice as the other three would be
    /// telling a player something is missing when nothing is.
    /// </summary>
    public static IReadOnlyList<ReadinessRow> Readiness(
        (string Server, string Character) identity,
        Func<OutputfileKind, DateTime?> writtenAt)
    {
        if (Identity(identity) == IdentityState.NoCharacter) return [];
        return
        [
            Row(OutputfileKind.Inventory, "Bags",
                "what you are carrying — the wishlist ticks itself and quests know what "
                + "you can turn in", ShellPage.Gear, "inventory"),
            Row(OutputfileKind.Achievements, "Achievements",
                "what you finished before EQBuddy — Plane of Sky turn-ins and raid clears",
                ShellPage.Quests, "sky"),
            Row(OutputfileKind.Factions, "Factions",
                "where you stand — the log only ever sees faction changes, never a standing",
                ShellPage.Quests, "unlocks"),
            Row(OutputfileKind.Spellbook, "Spellbook",
                "optional — sharpens buff countdowns by telling EQBuddy which rank of a "
                + "spell you know. Countdowns work without it",
                null, ""),
        ];

        ReadinessRow Row(OutputfileKind kind, string name, string feeds, ShellPage? page, string room)
        {
            var at = writtenAt(kind);
            return new ReadinessRow(kind, name, feeds,
                at is null ? ReadinessState.NeverScanned : ReadinessState.Scanned, at,
                page is { } p && ShellPages.Landed.Contains(p) ? ShellPages.Address(p, room) : "");
        }
    }

    /// <summary>A readiness row's answer: the date, or the ask. **"Not run yet" is a
    /// different sentence from a date, and it is the whole reason this block exists** —
    /// silence would tell a player who has never run the command that everything is
    /// fine.</summary>
    public static string ReadinessAnswer(ReadinessRow row) =>
        row.State == ReadinessState.NeverScanned || row.ScannedAt is null
            ? "Not run yet"
            : SessionSummary.Stamp(row.ScannedAt.Value);

    /// <summary>
    /// **The ⧉ copy is offered on EVERY readiness row, in BOTH states** — the Founder's
    /// DRA-63 smoke, 2026-09-11: *"always show copy/paste catch-up for Bags, Achievements,
    /// Factions, Spellbooks from Home."*
    ///
    /// It used to be an empty-state affordance only, which read as a onboarding prompt that
    /// switched itself off the moment it succeeded. **A dump is a SNAPSHOT of a thing that
    /// keeps changing**: the bags a player is carrying tonight are not the bags the file
    /// records from last week, and the surfaces downstream of it (the wishlist's own ticks,
    /// what a quest says you can turn in) are only as current as the last run. So the state
    /// the copy was hidden in — "you have run this before" — is precisely the state a player
    /// is in every time they want to run it AGAIN, and the room's answer was to make them go
    /// and find the command somewhere else.
    ///
    /// **The two states still say different things** (Bevel's two-states rule, which this
    /// does not touch): a never-run row asks, a scanned row offers. What is identical is that
    /// the command is one click away from both, which is David's 2026-08-14 rule — a surface
    /// that needs an in-game command SHIPS the command — applied to the state it had been
    /// quietly exempting.
    /// </summary>
    public static string CatchUpTooltip(ReadinessRow row) =>
        row.State == ReadinessState.NeverScanned ? CatchUpFirstRun : CatchUpAgain;

    /// <summary>The ask, for a dump that has never been produced.</summary>
    public const string CatchUpFirstRun =
        "Copies the command — paste it into the game's chat. The game writes the file beside "
        + "its own folders and EQBuddy reads it by itself.";

    /// <summary>The offer, for a dump that HAS landed. It says what running it again buys,
    /// because a button on a row that already has a date has to answer "why would I" before
    /// it answers "how".</summary>
    public const string CatchUpAgain =
        "Run it again whenever you want EQBuddy to catch up — paste the command into the "
        + "game's chat and EQBuddy reads the new file by itself. What it already knows stays "
        + "until then.";

    /// <summary>The Readiness block's own empty state, reached only when there is no
    /// character to have dumps FOR — in which case the Identity block above has already
    /// said the useful thing and this one must not say it again.</summary>
    public const string EmptyReadiness =
        "Readiness fills in once EQBuddy is following a character.";

    /// <summary>The block's heading, carrying the count that has not been run — the glance
    /// answer, so a healthy player reads one line and stops. Zero earns a plain heading
    /// rather than a tick with a number, because "0 waiting" is a scoreboard for a
    /// non-event.</summary>
    public static string ReadinessHeadline(IReadOnlyList<ReadinessRow> rows)
    {
        var waiting = rows.Count(r => r.State == ReadinessState.NeverScanned);
        return waiting == 0 ? "Readiness" : $"Readiness — {waiting} not run yet";
    }

    // ---- the block that is NOT here ---------------------------------------------

    // **THE "GO TO" BLOCK IS GONE, AND THIS IS ITS TOMBSTONE** (Founder smoke 2026-09-11,
    // DRA-63 ask 2: *"drop Go to Live/Progress/Gear/Quests/World — sidebar already owns
    // those doors"*). `Links()` built one row per landed room off `ShellPages.RailOrder`,
    // filtered by `Landed` and `BelowTheGap`; `HomeLink` and `EmptyLinks` went with it.
    //
    // **What the deletion cost, said out loud, because the guard was a good one.** Those
    // links were the shell's demonstration that a room's BODY is a second navigation
    // surface where the rail's own filter cannot see it — a hand-written row naming an
    // unlanded room compiles, renders, photographs perfectly and opens nothing. The rule
    // does not leave with the block: **Home still navigates**, through the readiness rows'
    // "Open", and those addresses are filtered through `Landed` in `Readiness()` above.
    // `shellHomeDeadLinks` was re-pointed at them in the same commit rather than deleted
    // with the block it was written for — the assertion is about the property, not about
    // the four rows that happened to be the first thing to have it.
    //
    // **Do not rebuild this block.** The rail is the door list, it is on screen in the same
    // window at the same moment, and a second copy of it under the fold is one more place
    // that has to be taught the day a seventh room lands.
}
