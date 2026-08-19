using EQBuddy.Core;

namespace EQBuddy.Companion;

/// <summary>
/// Everything one projection pass reads, gathered by the host. A bundle rather than a
/// dozen parameters so a new surface adds a property here instead of rewriting every
/// call site — and so the LAZY rule is visible: each member is filled only when its
/// surface is offered AND a device is connected (see CompanionHost.Tick), which is why
/// they are all nullable or empty-by-default rather than required.
/// </summary>
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

    /// <summary>This character's raid clears, for the Progress theme's Raids tab. The
    /// LEDGER rather than a pre-built block: the projection needs to ask it per boss, and
    /// it is per character and rebuilt when the followed character changes, so a snapshot
    /// taken once would answer for the wrong one.</summary>
    public RaidKillLedger? Raids { get; init; }

    /// <summary>The live palette. Not gateable — see CompanionSnapshot.Theme.</summary>
    public CompanionThemeSection? Theme { get; init; }
}
