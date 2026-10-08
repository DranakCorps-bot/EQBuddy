using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **THE CHARACTER CARD, AS PLAIN VALUES** (DRA-1288 D1, plan <c>docs/plans/DRA-1288.md</c>).
///
/// <para>One character's facts and EQBuddy's own evidence about them, read out of the stores
/// that already hold them. It is a PROJECTION: <see cref="From"/> calls the existing readers
/// under their own locks and writes nothing — no store, no setting, no file. The words are
/// <see cref="CharacterCardPresentation"/>'s and only its; nothing in here is a sentence.</para>
///
/// <para><b>Keyed on the (name, server) pair and nothing looser.</b> The ledger and settings
/// are read under <see cref="QuestLedgerStore.KeyFor"/>, the session store under
/// <see cref="SessionSummary.Stored"/>'s never-unscoped rule, and the game's dumps through
/// <see cref="FindDump"/>, which — unlike <see cref="OutputfileAutoImport.FindLatest"/> —
/// refuses a dump whose file name carries another server (decision 6). A card about Dranak on
/// freeport never shows Dranak on erollisi's bags.</para>
///
/// <para><b>Out, by rule</b>: other players, group and raid members, chat, tells, buffs and
/// rankings. None of them is a field here, so none of them can be rendered — the values line
/// is enforced by the record's shape, and <c>CharacterCardTests</c> proves it against stores
/// those names really reached.</para>
/// </summary>
public sealed record CharacterCard(
    string Name,
    string Server,
    DateTimeOffset WrittenAt,
    string AppVersion,
    IReadOnlyList<string> Classes,
    ClassSource ClassSource,
    ResolvedLevel Level,
    IReadOnlyList<CharacterCard.ClassLevelRow> ClassLevels,
    IReadOnlyList<TradeskillStanding> Skills,
    IReadOnlyList<string> TrackedQuests,
    int CompletedQuests,
    IReadOnlyList<CharacterCard.GuideRow> Guides,
    IReadOnlyList<CharacterCard.UpgradeRow> Upgrades,
    CharacterCard.InventoryDump? Inventory,
    CharacterCard.FactionDump? Factions,
    IReadOnlyList<CharacterCard.SessionLine> Sessions,
    IReadOnlyList<CharacterCard.ZoneLine> Evidence)
{
    /// <summary>How many stored sessions the card lists (plan section 8).</summary>
    public const int RecentSessions = 5;

    /// <summary>One class's own resolved level (DRA-356's per-class memory).</summary>
    public sealed record ClassLevelRow(string Class, int Level, LevelSource Source);

    /// <summary>A guide this character has touched, counted through
    /// <see cref="GuideProgressRouter.Counts(AppSettings, QuestLedgerStore, string, Guide, GuideStores)"/>
    /// — the same producer the guide's own heading reads.</summary>
    public sealed record GuideRow(string Name, int Done, int Total);

    /// <summary>One tracked upgrade, joined to where the catalog says it drops
    /// (<see cref="GearTargets.For"/>). <paramref name="Gap"/> is set when it placed nowhere.</summary>
    public sealed record UpgradeRow(
        string Item, string Slot, string Over, DateTime TrackedAt,
        IReadOnlyList<GearTargetZone> Zones, GearTargetGap? Gap);

    /// <summary>Where an inventory row is: on the character, in a bag, or in the bank.</summary>
    public enum Place { Worn, Bags, Bank }

    /// <summary>One occupied dump row. <paramref name="Location"/> is the worn SLOT for a worn
    /// row (<see cref="InventoryFile.Entry.WornSlot"/>) and the dump's own location otherwise.</summary>
    public sealed record InventoryRow(Place Place, string Location, string Name, int Count);

    /// <summary>The newest server-matched inventory dump, with the time the game wrote it.</summary>
    public sealed record InventoryDump(DateTime WrittenAt, IReadOnlyList<InventoryRow> Rows);

    /// <summary>The newest server-matched faction dump, with the time the game wrote it.</summary>
    public sealed record FactionDump(DateTime WrittenAt, IReadOnlyList<FactionsFile.Standing> Standings);

    /// <summary>One stored session, as the session store's own row carries it.</summary>
    public sealed record SessionLine(
        DateTime StartLocal, string Zone, double ActiveSeconds, double XpPercent,
        int Kills, int Deaths, int Loot);

    /// <summary>One zone of EQBuddy's own per-zone evidence. <paramref name="XpPerHour"/> is
    /// <see cref="ZoneRoll.XpPerHour"/>'s answer, null under its own floor — unknown is never
    /// zero.</summary>
    public sealed record ZoneLine(string Zone, double Hours, double? XpPerHour);

    /// <summary>
    /// Build the card for one character. Reads only; every argument may be absent, and an
    /// absent source is an empty section, never an exception.
    /// </summary>
    /// <param name="writtenAt">The moment the card is about — the caller's clock, so a test
    /// can state it and the renderer stays a pure function of its input.</param>
    public static CharacterCard From(CharacterCardSources sources, DateTimeOffset writtenAt)
    {
        var name = sources.Name ?? "";
        var server = sources.Server ?? "";
        var key = QuestLedgerStore.KeyFor(name, server);
        var ledger = key.Length > 0 ? sources.Ledger : null;
        var settings = sources.Settings;

        // Identity: the same resolution MainWindow.ClassSourceFor makes — the dump, the log's
        // inference, the picks, the statement and the /who, fresher wins (trap 33).
        var (stated, statedAt, who) = ledger?.ClassClaimsFor(key) ?? ([], default, null);
        var (classes, classSource) = CharacterClasses.Resolve(
            ledger?.UnlockedClassesFor(key), sources.InferredClasses, ledger?.ClassesFor(key),
            stated, statedAt, who);
        var level = ledger?.ResolvedLevelFor(key, classes) ?? ResolvedLevel.Unknown;
        var classLevels = ledger?.ClassLevelsFor(key)
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new ClassLevelRow(kv.Key, kv.Value.Level, kv.Value.Source))
            .ToList() ?? [];

        var skills = Tradeskills.Standings(ledger?.SkillsFor(key) ?? []);

        var tracked = (ledger?.TrackedFor(key) ?? [])
            .OrderBy(q => q, StringComparer.OrdinalIgnoreCase)
            .ThenBy(q => q, StringComparer.Ordinal)
            .ToList();
        var completed = ledger?.CompletedFor(key).Count(kv => kv.Value > 0) ?? 0;

        return new CharacterCard(
            name, server, writtenAt, sources.AppVersion ?? "",
            classes, classSource, level, classLevels, skills,
            tracked, completed,
            GuidesFor(sources, ledger, settings, key),
            UpgradesFor(settings, key, sources.Items),
            ReadInventory(FindDump(sources.LogFolder, name, server, OutputfileKind.Inventory)),
            ReadFactions(FindDump(sources.LogFolder, name, server, OutputfileKind.Factions)),
            SessionsFor(sources, name, server, out var evidence),
            evidence);
    }

    private static List<GuideRow> GuidesFor(
        CharacterCardSources sources, QuestLedgerStore? ledger, AppSettings? settings, string key)
    {
        if (ledger is null || settings is null || sources.Guides is not { } catalog) return [];
        // The Sky and Epic rows are the BOUND character's working copy (QuestTickBinding), so
        // the counts are the guide surface's own numbers for the character the app is
        // following — which is the only character D2's writer ever renders.
        var stores = GuideStores.For(settings.SkyQuestChecklist, settings.EpicQuestChecklist);
        var rows = new List<GuideRow>();
        foreach (var id in ledger.GuidesTouchedBy(key))
        {
            if (catalog.Find(id) is not { } guide) continue;
            var counts = GuideProgressRouter.Counts(settings, ledger, key, guide, stores);
            rows.Add(new GuideRow(guide.Name.Length > 0 ? guide.Name : guide.Id, counts.Done, counts.Total));
        }
        return [.. rows
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Name, StringComparer.Ordinal)];
    }

    private static List<UpgradeRow> UpgradesFor(AppSettings? settings, string key, ItemCatalog? items)
    {
        var tracked = TrackedUpgradeStore.For(settings, key);
        if (tracked.Count == 0) return [];
        // GearTargets is the one join from a goal to a place; the card asks it rather than
        // reading DropZones itself (trap 4).
        var targets = GearTargets.For(tracked, items);
        return [.. tracked.Select(t =>
        {
            var goal = TrackedUpgradeStore.Key(t.Item);
            var zones = targets.Zones
                .Where(z => z.Item.Equals(goal, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var gap = targets.Refused
                .FirstOrDefault(r => r.Item.Equals(goal, StringComparison.OrdinalIgnoreCase))?.Why;
            return new UpgradeRow(t.Item, t.Slot, t.Over, t.TrackedAt, zones, gap);
        })];
    }

    private static List<SessionLine> SessionsFor(
        CharacterCardSources sources, string name, string server, out IReadOnlyList<ZoneLine> evidence)
    {
        evidence = [];
        if (sources.QuerySessions is not { } query) return [];
        // The never-unscoped rule: an empty half answers nothing rather than every character.
        // FINISHED sessions only, for both sections. The live session is checkpointed into the
        // store as an `Active` row every five minutes; folding it in would move "Your evidence"
        // by 0.01 h on every checkpoint, and the writer rewrites the file whenever the text
        // differs (DRA-1288 decision 3) — so the card would churn while nothing about the
        // character changed (DRA-1472 binding condition 3). The live session reaches the card
        // when it finishes.
        var finished = SessionSummary.Stored((server, name), query)
            .Where(r => r.Id != sources.ActiveSessionRowId
                        && !string.Equals(r.EndReason, SessionRepository.ActiveEndReason, StringComparison.Ordinal))
            .ToList();
        if (finished.Count == 0) return [];

        // EQBuddy's own per-zone evidence: ZoneHistory's fold over this character's rows. The
        // card reads only the session-row half (hours and experience), so no pool is joined.
        evidence = [.. ZoneHistory.Fold(finished, [])
            .Where(z => z.Zone.Length > 0 && z.Hours > 0)
            .Select(z => new ZoneLine(z.Zone, z.Hours, z.XpPerHour))];

        return [.. finished
            .OrderByDescending(r => r.StartLocal)
            .ThenByDescending(r => r.Id)
            .Take(RecentSessions)
            .Select(r => new SessionLine(
                r.StartLocal, r.PrimaryZone, r.ActiveSeconds, r.XpPercent, r.Kills, r.Deaths, r.LootCount))];
    }

    private static InventoryDump? ReadInventory(FileInfo? file)
    {
        if (file is null) return null;
        try
        {
            var rows = InventoryFile.ParseEntries(File.ReadLines(file.FullName))
                .Select(e => e.Worn
                    ? new InventoryRow(Place.Worn, e.WornSlot, e.Name, e.Count)
                    : new InventoryRow(e.InBank ? Place.Bank : Place.Bags, e.Location, e.Name, e.Count))
                // Worn, then bags, then bank; inside each, the dump's own order.
                .Select((row, i) => (row, i))
                .OrderBy(p => p.row.Place)
                .ThenBy(p => p.i)
                .Select(p => p.row)
                .ToList();
            return new InventoryDump(file.LastWriteTime, rows);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static FactionDump? ReadFactions(FileInfo? file)
    {
        if (file is null) return null;
        try { return new FactionDump(file.LastWriteTime, FactionsFile.Parse(File.ReadLines(file.FullName))); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// **The server-matched dump finder** (plan decision 6).
    ///
    /// <para><see cref="OutputfileAutoImport.FindLatest"/> matches <c>{name}_*-Kind.txt</c> with
    /// the server as a WILDCARD, so a name played on two servers hands either character the
    /// other's newest dump. The card cannot afford that: it is a file about ONE character that
    /// an AI app may read. So this keeps the same glob and the same folder, then admits only a
    /// file whose server tag (<see cref="ServerTagOf"/>) is this character's server. The Helper
    /// and the auto-import are untouched in this slice.</para>
    ///
    /// <para>Measured on the Founder's own game folder, 2026-10-08:
    /// <c>Dranak_freeport-Achievements.txt</c>, <c>Dranak_freeport-Inventory.txt</c>,
    /// <c>Dranak_freeport-WAR-Factions.txt</c>, <c>Dranak_freeport-WAR-Spellbook.txt</c>,
    /// <c>Hugzee_qeynos-Inventory.txt</c> — and his log <c>eqlog_Dranak_freeport.txt</c>, which
    /// is where the session's server comes from. The tag is the text between the first
    /// <c>_</c> after the name and the first <c>-</c>.</para>
    /// </summary>
    /// <returns>The newest matching dump, or null when there is none for this server.</returns>
    public static FileInfo? FindDump(string? logFolder, string name, string server, OutputfileKind kind)
    {
        if (string.IsNullOrWhiteSpace(server)) return null;
        if (OutputfileAutoImport.Pattern(name, kind) is not { } pattern) return null;
        if (OutputfileAutoImport.DumpFolder(logFolder) is not { } root) return null;
        try
        {
            return Directory.EnumerateFiles(root, pattern)
                .Where(f => ServerTagOf(Path.GetFileName(f), name) is { } tag
                            && tag.Equals(server, StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ThenBy(f => f.Name, StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// The server a dump's file name carries, or null when the name is not this character's
    /// dump. <b>Case-insensitive on the name, and the tag ENDS AT THE FIRST <c>-</c></b>
    /// (Reviewer note b on PR #1053): the server tag varies in case
    /// (<see cref="OutputfileAutoImport.Pattern"/>'s own note) and the faction and spellbook
    /// dumps splice a class code in after it (<c>Hateborne_neriak-ENC-Factions.txt</c>), so
    /// "everything up to the kind suffix" would read <c>neriak-ENC</c> and refuse the
    /// character's own dump.
    /// </summary>
    public static string? ServerTagOf(string? fileName, string name)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(name)) return null;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var prefix = name + "_";
        if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = stem[prefix.Length..];
        var dash = rest.IndexOf('-');
        return dash > 0 ? rest[..dash] : null;
    }
}

/// <summary>
/// Where <see cref="CharacterCard.From"/> reads. The host (D2's runtime) fills it from the
/// stores it already owns; a test fills it from fixtures. Every source is optional.
/// </summary>
/// <param name="Name">The character name as the log FILE spells it — the archiver's identity
/// (<c>SessionArchiver.Identity</c>), because the session store compares it with SQL
/// <c>=</c>.</param>
/// <param name="Server">The server as the log file spells it, the archiver's other half.</param>
/// <param name="QuerySessions"><c>SessionRepository.Query(server, character)</c>.</param>
/// <param name="LogFolder">The game's Logs folder; dumps live in its parent.</param>
/// <param name="InferredClasses">What the live log looks like
/// (<c>StatsSnapshot.InferredClasses</c>) — the one identity input no store holds.</param>
/// <param name="ActiveSessionRowId">The row the live session is checkpointed under, kept off
/// the recent-sessions list (it is not finished).</param>
public sealed record CharacterCardSources(
    string Name,
    string Server,
    QuestLedgerStore? Ledger = null,
    AppSettings? Settings = null,
    Func<string, string, List<SessionRow>>? QuerySessions = null,
    string? LogFolder = null,
    IReadOnlyList<string>? InferredClasses = null,
    ItemCatalog? Items = null,
    GuideCatalog? Guides = null,
    string AppVersion = "",
    long ActiveSessionRowId = 0);
