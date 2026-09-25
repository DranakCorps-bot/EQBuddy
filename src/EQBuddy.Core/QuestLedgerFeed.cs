namespace EQBuddy.Core;

/// <summary>What the quest ledger took in and let go since the host last looked.</summary>
/// <param name="Gained">Loot the ledger accepted as NEW, names as logged - never a
/// replayed line.</param>
/// <param name="Lost">Items the ledger saw leave (a sale, destroy, merge or hand-in), and
/// how many.</param>
public sealed record QuestLedgerDelta(
    IReadOnlyList<(string Item, int Count)> Gained, IReadOnlyList<(string Item, int Count)> Lost)
{
    public static readonly QuestLedgerDelta Empty = new([], []);

    public bool IsEmpty => Gained.Count == 0 && Lost.Count == 0;
}

/// <summary>
/// The hand-off between the ingest thread, where the ledger decides what is NEW, and the
/// UI thread, where the Sky and Epic checklists tick.
///
/// **Why the checklists key on this and not on session totals** (Hateborne, 2026-09-18):
/// they used to diff the snapshot's loot against a high-water mark held in RAM, cleared on
/// every launch, session start, character switch and review - while the log watcher
/// re-reads the whole file each time. Every launch re-offered the same loot line, and the
/// Sky auto-tick's rule 3 parked one more * on the next class's row; one Wind Rune Meda had
/// walked down six classes. The ledger's time gate is persisted, so a replayed line bounces
/// there and never reaches this.
/// </summary>
public sealed class QuestLedgerFeed
{
    private readonly object _lock = new();
    private readonly List<(string Item, int Count)> _gained = [];
    private readonly Dictionary<string, int> _lost = new(StringComparer.OrdinalIgnoreCase);

    public void Gained(string item, int count)
    {
        lock (_lock) _gained.Add((item, count));
    }

    public void Lost(string item, int count)
    {
        lock (_lock) _lost[item] = _lost.GetValueOrDefault(item) + count;
    }

    /// <summary>Take everything queued and empty the queue. Once per UI tick.</summary>
    public QuestLedgerDelta Drain()
    {
        lock (_lock)
        {
            if (_gained.Count == 0 && _lost.Count == 0) return QuestLedgerDelta.Empty;
            var delta = new QuestLedgerDelta([.. _gained], [.. _lost.Select(kv => (kv.Key, kv.Value))]);
            _gained.Clear();
            _lost.Clear();
            return delta;
        }
    }
}
