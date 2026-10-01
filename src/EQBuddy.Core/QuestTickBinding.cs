namespace EQBuddy.Core;

/// <summary>
/// **Whose boxes the Sky and Epic checklists are showing** (DRA-47, Delivery 2 N3).
///
/// <para>The ticks persist per character in <see cref="QuestLedgerStore.CharacterLedger.QuestTicks"/>.
/// The surfaces — both desktops, the phone, the achievements import, the loot auto-tick —
/// read and write <see cref="AppSettings.SkyQuestChecklist"/> and its four siblings, as they
/// always have. This class is what joins the two: it LOADS the bound character's ticks into
/// those lists when the character changes, and COMMITS them back on every
/// <see cref="AppSettings.Save"/> (through <see cref="AppSettings.Saving"/>). So a toggle that
/// ends in <c>Save()</c> — which every writer already did, because the lists used to live in
/// that file — lands in the ledger, for the right character, with no call site changed.</para>
///
/// <para><b>One writer, on purpose.</b> Nothing else calls
/// <see cref="QuestLedgerStore.SetTicks"/> in the app. A second writer to the ledger's ticks
/// would be overwritten by this class's next commit from its (older) working set: trap 4, with
/// the losing side decided by which one saved last.</para>
///
/// <para><b>UI thread.</b> Bind and Commit touch the same lists the surfaces draw; the host
/// calls both from its dispatcher, the same place the loot auto-tick already drained.</para>
/// </summary>
public sealed class QuestTickBinding
{
    private readonly AppSettings _settings;
    private readonly QuestLedgerStore _ledger;
    private readonly string _sidecarPath;

    // Ticked ids the bound character carries for rows the shipped catalog no longer has.
    // Nothing can draw them, but a commit derived from the ROWS alone would erase them, and a
    // row that comes back (a corrected catalog) should come back ticked. Carried, not shown.
    private QuestLedgerStore.QuestTicks _orphans = new();

    public QuestTickBinding(AppSettings settings, QuestLedgerStore ledger, string sidecarPath)
    {
        _settings = settings;
        _ledger = ledger;
        _sidecarPath = sidecarPath;
        _settings.Saving += Commit;
    }

    /// <summary>The host's one call at construction: drain a pre-DRA-47 profile's section to
    /// its backup (<see cref="QuestTickMigration.Drain"/>), then bind against that backup. In
    /// that order, so the first character bound can adopt what the drain just moved.</summary>
    public static QuestTickBinding Start(AppSettings settings, QuestLedgerStore ledger, string sidecarPath)
    {
        QuestTickMigration.Drain(settings, sidecarPath);
        return new QuestTickBinding(settings, ledger, sidecarPath);
    }

    /// <summary>The character whose ticks the settings lists hold; "" before any is known.</summary>
    public string BoundKey { get; private set; } = "";

    /// <summary>
    /// Show <paramref name="characterKey"/>'s ticks. A no-op for the character already bound,
    /// so the host can call it every tick. Returns true when it actually switched.
    ///
    /// <para>Order is the contract: the outgoing character is COMMITTED before anything is
    /// loaded, so a tick placed the instant before a switch is never carried onto the next
    /// character's boxes nor lost from its own.</para>
    /// </summary>
    public bool Bind(string characterKey)
    {
        characterKey ??= "";
        if (characterKey.Equals(BoundKey, StringComparison.OrdinalIgnoreCase)) return false;

        // Ticks placed while NO character was known (the seconds before the log is found)
        // have nowhere of their own to go. They go to the first character bound rather than
        // nowhere — before DRA-47 they would have been every character's.
        var carried = BoundKey.Length == 0 ? Capture() : null;
        Commit();
        BoundKey = characterKey;

        var ticks = characterKey.Length == 0 ? new QuestLedgerStore.QuestTicks() : _ledger.TicksFor(characterKey);
        var dirty = false;
        if (characterKey.Length > 0 && !ticks.Adopted)
        {
            QuestTickMigration.Adopt(ticks, QuestTickMigration.TicksOf(QuestTickMigration.Read(_sidecarPath)));
            dirty = true;
        }
        if (characterKey.Length > 0 && carried is not null && !carried.IsEmpty)
        {
            QuestTickMigration.Adopt(ticks, carried);
            dirty = true;
        }
        // A rename shipped after this character's ticks were written reaches them here — the
        // same table the profile's own migration uses (AppSettings.SkyRewardRenames).
        dirty |= AppSettings.RenameSkyRewardKeys(ticks.SkyCompleted);
        if (dirty) _ledger.SetTicks(characterKey, ticks);

        Load(ticks);
        return true;
    }

    /// <summary>Write the working set to the bound character's ledger entry. Nothing to do
    /// while no character is bound.</summary>
    public void Commit()
    {
        if (BoundKey.Length == 0) return;
        _ledger.SetTicks(BoundKey, Capture());
    }

    /// <summary>The working set as ticks. <c>Adopted</c> is true for any bound character:
    /// <see cref="Bind"/> adopted before loading.</summary>
    private QuestLedgerStore.QuestTicks Capture() => new()
    {
        SkyAcquired = [.. _settings.SkyQuestChecklist.Where(i => i.Acquired).Select(i => i.Id), .. _orphans.SkyAcquired],
        SkyGuessed = [.. _settings.SkyQuestChecklist.Where(i => i.AcquiredUnassigned).Select(i => i.Id), .. _orphans.SkyGuessed],
        SkyCompleted = [.. _settings.SkyQuestCompleted],
        EpicAcquired = [.. _settings.EpicQuestChecklist.Where(i => i.Acquired).Select(i => i.Id), .. _orphans.EpicAcquired],
        EpicGuessed = [.. _settings.EpicQuestChecklist.Where(i => i.AcquiredUnassigned).Select(i => i.Id), .. _orphans.EpicGuessed],
        EpicCompleted = [.. _settings.EpicQuestCompleted],
        EpicPreCompleteAcquired = new Dictionary<string, List<string>>(
            _settings.EpicQuestPreCompleteAcquired.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value)),
            StringComparer.OrdinalIgnoreCase),
        Adopted = BoundKey.Length > 0,
    };

    /// <summary>Put <paramref name="ticks"/> on the rows. IN PLACE — the same list objects
    /// stay in the settings, because a surface holding a reference to one must see the new
    /// character's state rather than a list nobody updates any more.</summary>
    private void Load(QuestLedgerStore.QuestTicks ticks)
    {
        var skyAcquired = ticks.SkyAcquired.ToHashSet(StringComparer.Ordinal);
        var skyGuessed = ticks.SkyGuessed.ToHashSet(StringComparer.Ordinal);
        foreach (var item in _settings.SkyQuestChecklist)
        {
            item.Acquired = skyAcquired.Contains(item.Id);
            item.AcquiredUnassigned = skyGuessed.Contains(item.Id);
        }
        var epicAcquired = ticks.EpicAcquired.ToHashSet(StringComparer.Ordinal);
        var epicGuessed = ticks.EpicGuessed.ToHashSet(StringComparer.Ordinal);
        foreach (var item in _settings.EpicQuestChecklist)
        {
            item.Acquired = epicAcquired.Contains(item.Id);
            item.AcquiredUnassigned = epicGuessed.Contains(item.Id);
        }
        var skyIds = _settings.SkyQuestChecklist.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var epicIds = _settings.EpicQuestChecklist.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        _orphans = new QuestLedgerStore.QuestTicks
        {
            SkyAcquired = [.. skyAcquired.Where(id => !skyIds.Contains(id))],
            SkyGuessed = [.. skyGuessed.Where(id => !skyIds.Contains(id))],
            EpicAcquired = [.. epicAcquired.Where(id => !epicIds.Contains(id))],
            EpicGuessed = [.. epicGuessed.Where(id => !epicIds.Contains(id))],
        };
        _settings.SkyQuestCompleted.Clear();
        _settings.SkyQuestCompleted.AddRange(ticks.SkyCompleted);
        _settings.EpicQuestCompleted.Clear();
        _settings.EpicQuestCompleted.AddRange(ticks.EpicCompleted);
        _settings.EpicQuestPreCompleteAcquired.Clear();
        foreach (var (cls, rows) in ticks.EpicPreCompleteAcquired)
            _settings.EpicQuestPreCompleteAcquired[cls] = [.. rows];
    }
}
