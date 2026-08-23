using EQBuddy.Core;

namespace EQBuddy.Companion;

/// <summary>
/// One tick's quest inputs, gathered by the host's callback — a bundle for the same
/// reason <see cref="CompanionMapRequest"/> is one: the quest surface reads the
/// catalog plus the whole per-character ledger slice, and a parameter list would be
/// rewritten by every addition. Members default to empty rather than null so the
/// projection never branches on "host couldn't answer" — an empty ledger IS the
/// honest answer for a character the ledger hasn't met.
/// </summary>
public sealed record CompanionQuestRequest
{
    public QuestCatalog? Catalog { get; init; }
    public IReadOnlyDictionary<string, QuestLedgerStore.Entry> Owned { get; init; } =
        new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
    public ISet<string> Tracked { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public ISet<string> Hidden { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, int> Completed { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Classes { get; init; } = [];
    /// <summary>The heaviest class, kept for one release so an OPEN PHONE running the
    /// page it downloaded weeks ago keeps working (trap 32 — the page never re-fetches
    /// itself). New code reads <see cref="CharacterClassNames"/>.</summary>
    public string InferredClass { get; init; } = "";

    /// <summary>The character's classes and where they came from, resolved desktop-side by
    /// <see cref="CharacterClasses.Resolve"/> — so the phone cannot resolve them
    /// differently than the two windows do (#210's rule applied to a decision rather than
    /// to a list of rows).</summary>
    public IReadOnlyList<string> CharacterClassNames { get; init; } = [];

    public ClassSource ClassSource { get; init; } = ClassSource.Unknown;
}

/// <summary>
/// Builds the searchable catalog index the quest surface ships ONCE per device. The
/// catalog is immutable per process, so the host builds this once and hands the same
/// reference to every tick; <see cref="CompanionSnapshot.ForClient"/> then withholds
/// it from any device already holding the stamp — the map-geometry contract, because
/// the index is the same kind of payload: big, static, and pointless to repeat.
/// </summary>
public static class CompanionQuestIndex
{
    public static CompanionQuestCatalog Build(QuestCatalog catalog)
    {
        var allClasses = QuestClassFilter.Classes
            .Select(c => new CompanionQuestClass(c, QuestClassFilter.Abbrev(c)))
            .ToList();

        var entries = new List<CompanionQuestIndexEntry>(catalog.Quests.Count);
        foreach (var q in catalog.Quests)
        {
            // Class matching stays Core's QuestClassFilter call: the page checks
            // membership in this list rather than re-implementing the wiki's free-text
            // rules ("ALL except NEC WIZ MAG ENC") in JavaScript.
            var allowed = QuestClassFilter.Classes
                .Where(c => QuestClassFilter.Matches(q.Classes, c))
                .Select(QuestClassFilter.Abbrev)
                .ToList();
            entries.Add(new CompanionQuestIndexEntry(
                q.Name, q.Url, q.QuestGiver, q.StartZone, q.MinLevel, q.Classes,
                allowed.Count == QuestClassFilter.Classes.Length ? null : allowed,
                [.. q.Items.Select(i => new CompanionQuestNeed(i.Name, i.Qty))],
                q.Rewards, q.Era, q.Repeatable, q.Collection));
        }

        // Hashed over the serialized rows, so ANY field change re-ships the index —
        // a stamp built from counts alone would let a fixed quantity ride a stale copy.
        var stamp = CompanionHash.Of(
            System.Text.Json.JsonSerializer.Serialize(entries, CompanionSnapshot.JsonOpts));
        return new CompanionQuestCatalog(stamp, allClasses, entries);
    }
}
