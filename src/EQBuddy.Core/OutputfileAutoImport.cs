namespace EQBuddy.Core;

/// <summary>Which dump the game just announced. Decided from the FILENAME the log prints,
/// because that is the only thing the announcement gives us.</summary>
public enum OutputfileKind
{
    /// <summary>A dump EQBuddy has no reader for. Named rather than ignored so a new
    /// <c>/outputfile</c> variant shows up as "we saw it and did nothing" instead of as
    /// silence — the game has more of these than EQBuddy reads.</summary>
    Unknown,
    Inventory,
    Achievements,
}

/// <summary>
/// The game announces every dump it writes, in the log EQBuddy already tails, and names
/// the file: <c>Outputfile Complete: Dranak_freeport-Inventory.txt</c>. This turns that
/// announcement into an import.
///
/// **Why this exists** — David, 2026-08-20, after being handed a ⧉ copy button and then
/// left to find the file himself: *"We automatically read the logs, we should
/// automatically read the other files we generate. I shouldn't have to do a bunch of menu
/// navigation and then folder searching hunting around for something that can just be
/// lifted directly."* He was right twice over. The line had been in the log the whole
/// time, unparsed. And the reader was already built — <see cref="InventoryFile.FindLatest"/>
/// has always located the dump on its own, in the game folder, with no help from anyone —
/// so the folder hunting the UI asked for was never necessary even before this.
///
/// **The failure this fixes is a seam, not a missing feature.** Every piece existed:
/// a parser that tails the log, a finder that locates dumps, an auto-check that ticks the
/// checklist, an importer that marks raid clears. Nothing connected the announcement to
/// the readers, so three surfaces told the player to do by hand what the app could do by
/// itself. Same family as trap 20 — the capability was there and the path to it was not.
///
/// Framework-free and side-effect-scoped: it touches settings and the ledger it is handed,
/// and returns an <see cref="AutoImportOutcome"/> carrying both what changed and how to
/// put it back. No UI, no file dialogs, no dependency on which widget is asking.
/// </summary>
public static class OutputfileAutoImport
{
    /// <summary>Suffix → meaning. Both names are now verified against David's own log —
    /// inventory 2026-08-20 18:47:36, and achievements 2026-08-25 12:02:04
    /// (<c>Outputfile Complete: Hateborne_neriak-Achievements.txt</c>), which is what the
    /// note here used to say nobody had seen. An unrecognised dump is
    /// <see cref="OutputfileKind.Unknown"/> rather than a guess; the game writes more of
    /// these than EQBuddy reads, and its own usage line names them all:
    /// <c>achievements | faction | guild | guildbank | guildhall | inventory |
    /// missingspells | raid | realestate | recipes | spellbook</c>.</summary>
    public static OutputfileKind KindOf(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName ?? "");
        if (name.EndsWith("-Inventory", StringComparison.OrdinalIgnoreCase)) return OutputfileKind.Inventory;
        if (name.EndsWith("-Achievements", StringComparison.OrdinalIgnoreCase)) return OutputfileKind.Achievements;
        return OutputfileKind.Unknown;
    }

    /// <summary>The game writes dumps beside its own folders — the Logs folder's PARENT.
    /// The same place <see cref="InventoryFile.FindLatest"/> looks, said once so the two
    /// cannot disagree about where a dump lives (trap 4).</summary>
    public static string? ResolvePath(string? logFolder, string fileName)
    {
        if (string.IsNullOrWhiteSpace(logFolder) || string.IsNullOrWhiteSpace(fileName)) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null) return null;
        // The log prints a bare name. Anything else is not something we were told about.
        var safe = Path.GetFileName(fileName);
        var full = Path.Combine(root, safe);
        return File.Exists(full) ? full : null;
    }

    /// <summary>Read the achievements dump and apply it: raid clears from before EQBuddy,
    /// and Sky rewards whose class-unlock achievement says they were turned in.
    ///
    /// Both are ADD-ONLY — an import never unticks something. That was already true of
    /// the manual path and is what makes doing it unprompted safe (David chose "read and
    /// apply, say what it did" on 2026-08-20). The undo is offered anyway, because a
    /// change the player did not watch happen has to be reversible to be honest.</summary>
    /// <param name="ledger">Where the dump's class list is recorded, with
    /// <paramref name="characterKey"/>. Optional only so a test can call this without a
    /// store — every SHIPPING caller passes one, and `ClassSourceWritersTests` names them,
    /// because "the data survived the move and the write path did not" is the sentence
    /// behind #204, #210 and #212.</param>
    public static AutoImportOutcome ImportAchievements(
        string path, AppSettings settings, RaidKillLedger? raids,
        QuestLedgerStore? ledger = null, string characterKey = "")
    {
        var entries = AchievementsImport.Parse(File.ReadLines(path));
        // The game's own statement about which classes this character holds. Recorded
        // before anything else, because it is the one thing here that cannot be wrong:
        // every other outcome below is a MATCH against our checklists, and this is a
        // read of what the dump plainly says.
        if (ledger is not null && characterKey.Length > 0)
            ledger.SetUnlockedClasses(characterKey, AchievementsImport.UnlockedClasses(entries));
        // The other two lists are NOT spare. The manual import shows both in its preview,
        // and an unprompted import that drops them is the same dump telling the player
        // less than the menu would have — so they are counted onto the outcome and the
        // report says so. `unmatched` is the one that costs progress: a reward the player
        // really did obtain, whose name drifted from the checklist's, is silently not
        // ticked, and only the preview ever named it.
        var (matches, unmatched, autoGranted) =
            AchievementsImport.SkyRewards(entries, settings.SkyQuestChecklist);

        // Captured BEFORE applying, and only what actually flips — an undo that restores
        // a whole snapshot would also revert ticks the player made in between.
        var skyBefore = settings.SkyQuestChecklist
            .Where(i => !i.Acquired).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var completedBefore = settings.SkyQuestCompleted.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var raidKeysBefore = raids?.AchievementCompleteKeys().ToHashSet(StringComparer.Ordinal) ?? [];
        var raidsMarked = raids?.MarkAchievements(entries) ?? 0;
        var skyMarked = AchievementsImport.Apply(matches, settings);

        var skyFlipped = settings.SkyQuestChecklist
            .Where(i => i.Acquired && skyBefore.Contains(i.Id)).Select(i => i.Id).ToList();
        var completedAdded = settings.SkyQuestCompleted
            .Where(k => !completedBefore.Contains(k)).ToList();
        var raidKeysAdded = raids is null ? []
            : raids.AchievementCompleteKeys().Where(k => !raidKeysBefore.Contains(k)).ToList();

        return new AutoImportOutcome(OutputfileKind.Achievements, Path.GetFileName(path),
            File.GetLastWriteTime(path), GearTicked: 0, RaidsMarked: raidsMarked, SkyMarked: skyMarked)
        {
            SkySkipped = autoGranted.Count,
            SkyUnrecognized = unmatched.Count,
            Undo = raidsMarked + skyMarked == 0 ? null : () =>
            {
                foreach (var item in settings.SkyQuestChecklist)
                    if (skyFlipped.Contains(item.Id)) item.Acquired = false;
                foreach (var key in completedAdded) settings.SkyQuestCompleted.Remove(key);
                raids?.UnmarkAchievements(raidKeysAdded);
                settings.Save();
            },
        };
    }

    /// <summary>The inventory dump's half: tick the gear checklist off what the character
    /// verifiably owns. The dump itself is found and parsed by the caller (the widgets
    /// already memoize it), so this is only the apply-and-remember step.</summary>
    public static AutoImportOutcome ImportInventory(InventoryFile.Snapshot dump, AppSettings settings)
    {
        var before = settings.GearChecklist
            .Where(i => !i.Acquired).Select(GearKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        GearLootAutoCheck.ApplyInventory(settings.GearChecklist, dump.Entries);
        var flipped = settings.GearChecklist
            .Where(i => i.Acquired && before.Contains(GearKey(i))).Select(GearKey).ToList();

        return new AutoImportOutcome(OutputfileKind.Inventory, Path.GetFileName(dump.Path),
            dump.WrittenAt, GearTicked: flipped.Count, RaidsMarked: 0, SkyMarked: 0)
        {
            Undo = flipped.Count == 0 ? null : () =>
            {
                var undo = flipped.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in settings.GearChecklist)
                    if (undo.Contains(GearKey(item))) item.Acquired = false;
                settings.Save();
            },
        };
    }

    private static string GearKey(GearChecklistItem item) => item.Slot + "|" + item.Item;
}

/// <summary>What an auto-import did, in words a surface can print without deciding
/// anything — the widget, the Linux widget and the phone must not each invent their own
/// account of the same event.</summary>
public sealed record AutoImportOutcome(
    OutputfileKind Kind, string FileName, DateTime At,
    int GearTicked, int RaidsMarked, int SkyMarked)
{
    /// <summary>Puts back exactly what this import changed, and nothing else. Null when
    /// there is nothing to put back — which is the common case, and the reason the
    /// surface must not offer an Undo button unconditionally.</summary>
    public Action? Undo { get; init; }

    /// <summary>Sky rewards the dump flagged obtained and the #101 guard refused, because
    /// the class unlock that flagged them was granted rather than earned. NOT a failure —
    /// the guard working — but the player has to be told, or a dump full of rewards reads
    /// as "nothing new to mark" and the import looks broken (Frankthetankk, #101, asking
    /// whether the unprompted path shares the guard: it does; it just said nothing).</summary>
    public int SkySkipped { get; init; }

    /// <summary>Completed "Obtain X" criteria that matched no checklist reward — usually a
    /// name that drifted from the wiki's. This is the count that costs the player real
    /// progress, so it is the one an unprompted import must never swallow.</summary>
    public int SkyUnrecognized { get; init; }

    public int Applied => GearTicked + RaidsMarked + SkyMarked;

    /// <summary>What the import found but did not apply. Kept apart from
    /// <see cref="Applied"/> because it decides whether there is anything to SAY, not
    /// whether there is anything to UNDO — a run that only skipped still needs a report,
    /// and still must not offer an Undo button.</summary>
    public int Noted => SkySkipped + SkyUnrecognized;

    /// <summary>
    /// **The glance line: what happened, and nothing else.** It says the dump was READ even
    /// when nothing changed, because "EQBuddy did nothing" and "EQBuddy never saw your file"
    /// look identical to the player and only one of them is a bug — exactly the confusion
    /// the manual flow produced (David, 2026-08-20: he ran the command, the file appeared,
    /// and the window sat there).
    ///
    /// **The counts stay here; the REASONS moved to <see cref="Detail"/>** (Bevel, Helm-signed
    /// 2026-08-23). The first cut said all of it on the card and ran to three sentences —
    /// five lines on the 338 px widget. Bevel's ruling was not to cut a clause, because each
    /// one names a different way the import can look broken when it is not; it was that the
    /// glance is *"something happened, here's Undo"* and the why is a second job behind
    /// hover. Same shape as the 1.99.1 caption call.
    /// </summary>
    public string Summary => Kind switch
    {
        OutputfileKind.Inventory => GearTicked switch
        {
            0 => $"Read your inventory dump ({At:HH:mm}) — nothing new to tick.",
            1 => $"Read your inventory dump ({At:HH:mm}) — 1 item ticked.",
            _ => $"Read your inventory dump ({At:HH:mm}) — {GearTicked} items ticked.",
        },
        OutputfileKind.Achievements =>
            (Applied == 0
                ? $"Read your achievements dump ({At:HH:mm}) — nothing new to mark"
                : $"Read your achievements dump ({At:HH:mm}) — " + string.Join(", ",
                    new[]
                    {
                        RaidsMarked > 0 ? $"{RaidsMarked} raid clear{(RaidsMarked == 1 ? "" : "s")}" : null,
                        SkyMarked > 0 ? $"{SkyMarked} Sky reward{(SkyMarked == 1 ? "" : "s")}" : null,
                    }.Where(s => s is not null)) + " marked")
            // Counted, not explained. A player who sees "2 skipped" and wants to know why
            // hovers; a player who does not is left with one short line.
            + string.Concat(new[]
            {
                SkySkipped > 0 ? $" · {SkySkipped} skipped" : null,
                SkyUnrecognized > 0 ? $" · {SkyUnrecognized} unmatched" : null,
            }.Where(s => s is not null))
            + ".",
        _ => $"Saw {FileName} ({At:HH:mm}) — EQBuddy has no reader for that dump.",
    };

    /// <summary>
    /// **The hover half: WHY something was skipped or unmatched.** <c>null</c> when there is
    /// nothing to explain, so a surface can hang it straight on a tooltip without inventing
    /// filler for the ordinary case.
    ///
    /// Both clauses survive intact from the first cut, because both were load-bearing: each
    /// names a different way a correct import reads as a broken one. The skipped clause is
    /// the #101 guard working and staying silent; the unmatched clause is real progress not
    /// being recorded, which is the one that costs the player something.
    /// </summary>
    public string? Detail
    {
        get
        {
            if (Kind != OutputfileKind.Achievements || Noted == 0) return null;
            var parts = new List<string>();
            if (SkySkipped > 0)
                parts.Add($"{SkySkipped} reward{(SkySkipped == 1 ? " was" : "s were")} skipped: "
                    + $"the class unlock that flagged {(SkySkipped == 1 ? "it" : "them")} was "
                    + "granted at character creation rather than earned, so the game marks its "
                    + "rewards obtained without the items ever existing. Turn them in for real "
                    + "and the Sky tracker records them the normal way.");
            if (SkyUnrecognized > 0)
                parts.Add($"{SkyUnrecognized} obtained reward{(SkyUnrecognized == 1 ? "" : "s")} "
                    + $"matched nothing on the checklist — usually a name that has drifted from "
                    + $"the wiki's. Import achievements… names {(SkyUnrecognized == 1 ? "it" : "them")}, "
                    + "so nothing is lost.");
            return string.Join("\n\n", parts);
        }
    }
}
