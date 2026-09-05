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
/// not landed yet — see <see cref="HomeReadout.Links"/>.</param>
public sealed record ReadinessRow(
    OutputfileKind Kind, string Name, string Feeds, ReadinessState State,
    DateTime? ScannedAt, string Address);

/// <summary>A Home deep link: a room's own label and one-line pitch, at its address.</summary>
public sealed record HomeLink(ShellPage Page, string Label, string Detail, string Address);

/// <summary>
/// The words and the arithmetic behind the Evolved shell's HOME room — the four blocks
/// Bevel's signed door 1 locks: **Identity · Readiness · Recent session · Deep links**.
///
/// It is here rather than in the room for this repo's standing reason: the WPF layer has
/// no unit tests, so a sentence or a state rule left inline is one nothing can check. What
/// the room keeps is the wiring.
///
/// **Each of the four blocks can be empty independently of the other three**, which is why
/// there is no single "nothing here yet" for the room. Bevel's table is explicit that a
/// blanket empty would be wrong: a player with a character and no dumps has three of four
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

    // ---- readiness --------------------------------------------------------------

    /// <summary>
    /// The three dumps Home reports on, in the order they matter to a player who is about
    /// to play: bags first (it is the one that changes every session), then the two that
    /// answer "what have I already finished".
    ///
    /// **The list is here and not in the room** so a fourth dump joins it in one place —
    /// and so the addresses can be filtered against <see cref="ShellPages.Landed"/> rather
    /// than hand-checked, which is the rail's own refusal to draw a dead affordance applied
    /// inside a room's body.
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
        ];

        ReadinessRow Row(OutputfileKind kind, string name, string feeds, ShellPage page, string room)
        {
            var at = writtenAt(kind);
            return new ReadinessRow(kind, name, feeds,
                at is null ? ReadinessState.NeverScanned : ReadinessState.Scanned, at,
                ShellPages.Landed.Contains(page) ? ShellPages.Address(page, room) : "");
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

    // ---- deep links -------------------------------------------------------------

    /// <summary>
    /// Home's deep links: every room that has actually LANDED, in rail order, minus Home
    /// itself and minus Settings.
    ///
    /// **It reads <see cref="ShellPages.Landed"/> — the same list the rail reads — and that
    /// is the point rather than a convenience.** Home ships before Live under the signed
    /// order, so a hand-written link list here would put a "Live" row in a room's body that
    /// opens nothing: the rail's own forbidden shape (*"an affordance that opens nothing is
    /// a trap"*) reappearing one level in, where the rail's guard cannot see it. Reading the
    /// list means Live's row arrives on the day Live does, in Live's own PR, and cannot
    /// arrive before.
    ///
    /// **Settings is excluded for the reason it sits below the rail's gap**: it configures
    /// the tool, and Home is about the character. Home itself is excluded because a link to
    /// the room you are standing in is a no-op wearing an affordance.
    /// </summary>
    public static IReadOnlyList<HomeLink> Links() =>
    [
        .. ShellPages.RailOrder
            .Where(page => ShellPages.Landed.Contains(page)
                && page != ShellPage.Home
                && !ShellPages.BelowTheGap(page))
            .Select(page => new HomeLink(page, ShellPages.Label(page),
                ShellPages.Describe(page), ShellPages.Address(page))),
    ];

    /// <summary>Unreachable while any room has landed — Home is only ever drawn by a shell
    /// that has rooms — and written anyway, because the block that renders it must not be
    /// able to draw a heading over nothing.</summary>
    public const string EmptyLinks = "The other rooms appear here as they are built.";
}
