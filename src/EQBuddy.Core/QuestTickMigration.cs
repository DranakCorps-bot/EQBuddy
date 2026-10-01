using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// **The one-time move of the per-PROFILE Sky and Epic ticks into the per-CHARACTER quest
/// ledger** (DRA-47, Delivery 2 N3). Two halves, and they run at different times on purpose:
///
/// <list type="number">
/// <item><b><see cref="Drain"/></b>, once per profile, at launch: the tick section a
/// pre-DRA-47 <c>settings.json</c> carried is copied VERBATIM into
/// <see cref="FileName"/> beside it, and only then removed from the settings file. That file
/// is the <c>.bak</c> of the drained section the plan asked for, and it is also the source
/// the second half reads — one file, so the backup and the thing adopted from it cannot
/// disagree.</item>
/// <item><b><see cref="Adopt"/></b>, once per CHARACTER, the first time
/// <see cref="QuestTickBinding"/> binds it: the drained ticks are unioned into that
/// character's own and the character is stamped <see cref="QuestLedgerStore.QuestTicks.Adopted"/>.</item>
/// </list>
///
/// <para><b>Why every character adopts, and not just whoever is playing.</b> Before this the
/// ticks were shared by every character on the profile, so every one of them was SHOWN those
/// boxes. Adopting into the first character bound would hide them from every alt the moment
/// the player updated, with no way to tell what happened. Adopting into each one means
/// nobody's screen changes at the upgrade — a pre-migration tick is visible after, for
/// whichever character looks — and from then on they diverge, which is the point of the
/// move. The cost is that an alt keeps a box its main ticked until the player clears it,
/// which is exactly what that alt showed the day before.</para>
///
/// <para><b>Torn-write order (trap 65).</b> The sidecar is written through
/// <see cref="ProfileJson"/> first; only if that succeeds are the legacy properties nulled
/// and settings saved. A kill between the two leaves the section in BOTH files, and the next
/// launch drains it again — the merge is a union, so draining twice is the same as once.</para>
/// </summary>
public static class QuestTickMigration
{
    /// <summary>Beside <c>settings.json</c> in the profile folder.</summary>
    public const string FileName = "quest-ticks.pre-ledger.json";

    /// <summary>The drained section, verbatim: the same property names and shapes
    /// <c>settings.json</c> carried, so a player (or a support thread) reading the backup
    /// reads what they used to have.</summary>
    public sealed class Section
    {
        public List<SkyQuestChecklistItem> SkyQuestChecklist { get; set; } = [];
        public List<string> SkyQuestCompleted { get; set; } = [];
        public List<EpicQuestChecklistItem> EpicQuestChecklist { get; set; } = [];
        public List<string> EpicQuestCompleted { get; set; } = [];
        public Dictionary<string, List<string>> EpicQuestPreCompleteAcquired { get; set; } = [];
        /// <summary>When the section left <c>settings.json</c> — the player's local clock.</summary>
        public DateTime DrainedAt { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// Move <paramref name="settings"/>' legacy tick section into <paramref name="sidecarPath"/>
    /// and out of the settings file. No-op (false) when there is nothing to drain. Throws
    /// nothing: a sidecar that cannot be written leaves the section where it is, to be tried
    /// again next launch — never nulled on a write that did not happen.
    /// </summary>
    /// <param name="save">Whether to save settings afterwards. The app passes true; a test
    /// that wants to look at the object before the write passes false.</param>
    public static bool Drain(AppSettings settings, string sidecarPath, bool save = true)
    {
        if (!settings.HasLegacyQuestTicks) return false;
        try
        {
            var incoming = new Section
            {
                SkyQuestChecklist = settings.LegacySkyQuestChecklist ?? [],
                SkyQuestCompleted = settings.LegacySkyQuestCompleted ?? [],
                EpicQuestChecklist = settings.LegacyEpicQuestChecklist ?? [],
                EpicQuestCompleted = settings.LegacyEpicQuestCompleted ?? [],
                EpicQuestPreCompleteAcquired = settings.LegacyEpicQuestPreCompleteAcquired ?? [],
                DrainedAt = DateTime.Now,
            };
            var merged = Read(sidecarPath) is { } existing ? Merge(existing, incoming) : incoming;
            ProfileJson.Write(sidecarPath, JsonSerializer.Serialize(merged, JsonOpts));
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return false;
        }

        settings.LegacySkyQuestChecklist = null;
        settings.LegacySkyQuestCompleted = null;
        settings.LegacyEpicQuestChecklist = null;
        settings.LegacyEpicQuestCompleted = null;
        settings.LegacyEpicQuestPreCompleteAcquired = null;
        if (save) settings.Save();
        return true;
    }

    /// <summary>The drained section, or null when this profile never had one (or its file
    /// is unreadable and has no backup — <see cref="ProfileJson.Read{T}"/>'s rule).</summary>
    public static Section? Read(string sidecarPath)
    {
        ProfileJson.Read<Section>(sidecarPath, JsonOpts, out var section);
        return section;
    }

    /// <summary>The drained section as ticks, in the ledger's shape. Empty when there is no
    /// section — so adopting from a profile that never had one only sets the stamp.</summary>
    public static QuestLedgerStore.QuestTicks TicksOf(Section? section)
    {
        if (section is null) return new QuestLedgerStore.QuestTicks();
        return new QuestLedgerStore.QuestTicks
        {
            SkyAcquired = [.. section.SkyQuestChecklist.Where(i => i.Acquired).Select(i => i.Id).Distinct(StringComparer.Ordinal)],
            SkyGuessed = [.. section.SkyQuestChecklist.Where(i => i.AcquiredUnassigned).Select(i => i.Id).Distinct(StringComparer.Ordinal)],
            SkyCompleted = [.. section.SkyQuestCompleted.Distinct(StringComparer.OrdinalIgnoreCase)],
            EpicAcquired = [.. section.EpicQuestChecklist.Where(i => i.Acquired).Select(i => i.Id).Distinct(StringComparer.Ordinal)],
            EpicGuessed = [.. section.EpicQuestChecklist.Where(i => i.AcquiredUnassigned).Select(i => i.Id).Distinct(StringComparer.Ordinal)],
            EpicCompleted = [.. section.EpicQuestCompleted.Distinct(StringComparer.OrdinalIgnoreCase)],
            EpicPreCompleteAcquired = new Dictionary<string, List<string>>(
                section.EpicQuestPreCompleteAcquired.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value ?? [])),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Union <paramref name="legacy"/> into <paramref name="target"/> and stamp it adopted.
    /// A union and never a replace: a character with ticks of its own (a later build's
    /// ticks, or a crash-and-retry) keeps every one of them. The per-class undo snapshot is
    /// taken from the legacy side only where the character has none — two snapshots of one
    /// class are two different "before"s, and the character's own is the later one.
    /// </summary>
    public static void Adopt(QuestLedgerStore.QuestTicks target, QuestLedgerStore.QuestTicks legacy)
    {
        Union(target.SkyAcquired, legacy.SkyAcquired, StringComparer.Ordinal);
        Union(target.SkyGuessed, legacy.SkyGuessed, StringComparer.Ordinal);
        Union(target.SkyCompleted, legacy.SkyCompleted, StringComparer.OrdinalIgnoreCase);
        Union(target.EpicAcquired, legacy.EpicAcquired, StringComparer.Ordinal);
        Union(target.EpicGuessed, legacy.EpicGuessed, StringComparer.Ordinal);
        Union(target.EpicCompleted, legacy.EpicCompleted, StringComparer.OrdinalIgnoreCase);
        foreach (var (cls, rows) in legacy.EpicPreCompleteAcquired)
            if (!target.EpicPreCompleteAcquired.ContainsKey(cls))
                target.EpicPreCompleteAcquired[cls] = [.. rows];
        target.Adopted = true;
    }

    /// <summary>Two drains of one profile (a crash between the writes, or a downgrade that
    /// wrote the section back) are unioned, never replaced — the no-lost-tick rule applied
    /// to the backup itself. A row ticked in either is ticked.</summary>
    internal static Section Merge(Section a, Section b)
    {
        var sky = a.SkyQuestChecklist.ToDictionary(i => i.Id, i => i.Clone(), StringComparer.Ordinal);
        foreach (var row in b.SkyQuestChecklist)
        {
            if (sky.TryGetValue(row.Id, out var have))
            {
                have.Acquired |= row.Acquired;
                have.AcquiredUnassigned |= row.AcquiredUnassigned;
            }
            else sky[row.Id] = row.Clone();
        }
        var epic = a.EpicQuestChecklist.ToDictionary(i => i.Id, i => i.Clone(), StringComparer.Ordinal);
        foreach (var row in b.EpicQuestChecklist)
        {
            if (epic.TryGetValue(row.Id, out var have))
            {
                have.Acquired |= row.Acquired;
                have.AcquiredUnassigned |= row.AcquiredUnassigned;
            }
            else epic[row.Id] = row.Clone();
        }
        var pre = new Dictionary<string, List<string>>(a.EpicQuestPreCompleteAcquired, StringComparer.OrdinalIgnoreCase);
        foreach (var (cls, rows) in b.EpicQuestPreCompleteAcquired) pre.TryAdd(cls, rows);

        var skyCompleted = new List<string>(a.SkyQuestCompleted);
        Union(skyCompleted, b.SkyQuestCompleted, StringComparer.OrdinalIgnoreCase);
        var epicCompleted = new List<string>(a.EpicQuestCompleted);
        Union(epicCompleted, b.EpicQuestCompleted, StringComparer.OrdinalIgnoreCase);
        return new Section
        {
            SkyQuestChecklist = [.. sky.Values],
            SkyQuestCompleted = skyCompleted,
            EpicQuestChecklist = [.. epic.Values],
            EpicQuestCompleted = epicCompleted,
            EpicQuestPreCompleteAcquired = pre,
            DrainedAt = b.DrainedAt > a.DrainedAt ? b.DrainedAt : a.DrainedAt,
        };
    }

    private static void Union(List<string> into, IEnumerable<string> add, StringComparer comparer)
    {
        foreach (var v in add)
            if (!into.Contains(v, comparer)) into.Add(v);
    }
}
