using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>
/// Everything one projection pass reads, gathered by the host. A bundle rather than a
/// dozen parameters so a new surface adds a property here instead of rewriting every
/// call site — and so the LAZY rule is visible: each member is filled only when its
/// surface is offered AND a device is connected (see CompanionHost.Tick), which is why
/// they are all nullable or empty-by-default rather than required.
/// </summary>
/// <summary>The desktop's Experience state for one tick: what the ding opened, what the
/// NEXT level opens, the classes both were filtered by, and every level-up this character
/// has. A record rather than a tuple because it grew from two members to four in one
/// change, and a four-tuple of <c>(int?, LevelUnlockSet, IReadOnlyList&lt;string&gt;, …)</c>
/// is where a caller swaps two arguments and nothing complains.</summary>
/// <param name="LevelUps">Every level-up EQBuddy has seen for the character being followed
/// (#240), newest first, from the widget's own <see cref="LevelHistoryMemo"/> —
/// <see cref="LevelHistory.Rows"/>, exactly as the Experience room draws them.
///
/// **POSITIONAL on purpose.** A member added here is a compile error on the lane that
/// forgets it, which is the only guard the WPF widget has: the Avalonia twin's wiring is
/// covered by reflection (`CompanionWiringTests`) and this one has no unit tests at all.
/// `Raids` and `Progress` were added to `CompanionSources` five days after the record and a
/// port from an older mental model would have looked complete without them — the same
/// failure this shape makes impossible.</param>
public sealed record CompanionProgressState(
    int? Level,
    LevelUnlockSet Unlocks,
    IReadOnlyList<string> Classes,
    (int Level, LevelUnlockSet Unlocks)? Next,
    IReadOnlyList<LevelHistory.Row> LevelUps);

public sealed record CompanionInputs
{
    public string Character { get; init; } = "";
    public string AppVersion { get; init; } = "";
    /// <summary>The desktop gate; null = everything this build knows.</summary>
    public IReadOnlyList<string>? Offered { get; init; }

    /// <summary>The per-tick shared snapshot the desktop cards render from. The
    /// companion never builds its own — one snapshot per tick is the perf contract.</summary>
    public StatsSnapshot? Stats { get; init; }

    public IReadOnlyList<SpawnTimerState> Timers { get; init; } = [];
    public IReadOnlyList<MezState> Mezzes { get; init; } = [];
    public IReadOnlyList<(string Class, IReadOnlyList<BuffSetEntryState> Entries)> BuffSets { get; init; } = [];
    public IReadOnlyList<BuffLossEntry> BuffLosses { get; init; } = [];

    /// <summary>The zone's map picture plus its spawn points, already resolved and
    /// cached by <see cref="CompanionMapSource"/> — never re-parsed per tick.</summary>
    public CompanionMapSection? Map { get; init; }

    /// <summary>The embedded zone graph, for the Path tab's route (World PR 4) — the
    /// SAME <see cref="EQBuddy.Core.TravelPlan"/> module the desktop Path tab reads.</summary>
    public ZoneGraph? ZoneGraph { get; init; }

    /// <summary>The Path tab's picked destination, held in settings (World PR 4) —
    /// null/empty means nothing picked yet.</summary>
    public string? TravelDestination { get; init; }

    /// <summary>The checklists live in settings, not in the snapshot.</summary>
    public AppSettings? Settings { get; init; }

    /// <summary>The quest surface's per-tick state: the catalog plus this character's
    /// ledger slice (owned / tracked / hidden / completed / classes).</summary>
    public CompanionQuestRequest? Quests { get; init; }

    /// <summary>The searchable catalog index, built once and cached by the host —
    /// never rebuilt per tick, and withheld per device by its stamp.</summary>
    public CompanionQuestCatalog? QuestIndex { get; init; }

    /// <summary>Zone → hops, for the gear checklist's by-zone view. Null when the
    /// zone graph can't answer (before the first zone line).</summary>
    public Func<string, int?>? HopsFromHere { get; init; }

    /// <summary>What level unlocked, already resolved by the desktop (it owns the
    /// class list and the memoized lookup).</summary>
    public LevelUnlockSet? Unlocks { get; init; }
    public int? Level { get; init; }

    /// <summary>The next-level preview and the classes it was filtered by — the two
    /// halves EQBuddy Mobile's own next fold needs (Bevel, Helm-signed 2026-08-23:
    /// *"give phone Progress the same next fold"*).
    ///
    /// **The classes ride the wire rather than the phone asking for them**, and that is
    /// the whole point: the split is <see cref="EQBuddy.UI.Shared.LevelUnlockGroups"/>'s
    /// decision, made once desktop-side, so the page cannot group the same unlocks
    /// differently than the two windows do. It is the #210 fix applied before the bug
    /// rather than after it — the phone went on building its own cross-class ready list
    /// for two days once, and this is the same shape of list.</summary>
    public IReadOnlyList<string> UnlockClasses { get; init; } = [];

    public (int Level, LevelUnlockSet Unlocks)? NextUnlocks { get; init; }

    /// <summary>Every level-up EQBuddy has seen for this character, newest first (#240,
    /// joeymavity) — the rows the desktop's Experience room folds under "Level-ups".
    ///
    /// **Resolved desktop-side, like <see cref="Unlocks"/>**, and for a harder reason: the
    /// stored half is a SQLite read over up to a thousand snapshots
    /// (<see cref="SessionRepository.ProgressSeries"/>), scoped to the exact two strings the
    /// ARCHIVER wrote its rows under. The widget owns both the repository and that identity,
    /// and it hands over the finished list through a <see cref="LevelHistoryMemo"/> so a
    /// paired phone does not put a database probe on every tick.</summary>
    public IReadOnlyList<LevelHistory.Row> LevelUps { get; init; } = [];

    /// <summary>This character's raid clears, for the Progress theme's Raids tab. The
    /// LEDGER rather than a pre-built block: the projection needs to ask it per boss, and
    /// it is per character and rebuilt when the followed character changes, so a snapshot
    /// taken once would answer for the wrong one.</summary>
    public RaidKillLedger? Raids { get; init; }

    /// <summary>The live palette. Not gateable — see CompanionSnapshot.Theme.</summary>
    public CompanionThemeSection? Theme { get; init; }

    /// <summary>The phone's alert audio (#208): the owner's switch and the running count
    /// of alerts the PC has fired. Null on the projection's older call shapes, which is
    /// why the page treats a missing section as "stay quiet" rather than as a default.</summary>
    public CompanionAlertsSection? Alerts { get; init; }
}
