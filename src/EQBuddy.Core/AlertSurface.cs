namespace EQBuddy.Core;

/// <summary>
/// The four alert surfaces, in the order every UI shows them — the Alerts THEME
/// (docs/Themes.md, first of the five David ruled on 2026-08-19).
///
/// **What consolidates here is the CONFIGURATION, not the alerting.** Every chip this
/// theme configures is a deadline with an action attached, which is exactly what earns
/// space on the overlay, so the chips stay where they are. What was scattered is the
/// answer to "alert me, at this volume, with this sound": the Watch card owned some of
/// it, the Buffs card some, Options a third slice, and the spawn and mez chip windows
/// their own. <see cref="UI.Shared"/>'s AlertSoundPlan already owns the DECISION; this
/// gives it one front door.
///
/// Ordering and labels live here rather than in each window for the reason
/// <see cref="QuestSurface"/> exists: "it should look and work the same on mobile" is
/// only true if there is one definition of what the tabs ARE. The Avalonia chip stacks
/// proved the alternative twice (#122, #152).
/// </summary>
public enum AlertTab
{
    /// <summary>Text and loot rules the player writes (#105, wizen).</summary>
    Watch,
    /// <summary>Buff sets, expiry warnings, the "new at level" suggestions.</summary>
    Buffs,
    /// <summary>Respawn timers and the chips they raise.</summary>
    Spawns,
    /// <summary>Mez and charm break warnings — the shortest fuse of the four.</summary>
    Crowd,
}

/// <summary>A tab as a UI should draw it. <see cref="Count"/> is how many things are
/// configured on it — rules written, sets assembled, timers tracked — and is null when
/// the tab has nothing countable rather than zero, because "0" and "not applicable" read
/// differently on a badge (the QuestTab.General lesson: a "0 / 900" badge on a catalog
/// reads as failure rather than as a library).</summary>
public sealed record AlertTabHeader(AlertTab Tab, string Label, string Key, int? Count)
{
    /// <summary>The badge, or null when there is nothing to report. Zero DOES render —
    /// "no watch rules yet" is a state the player can act on, and hiding it would make
    /// the empty tab look broken rather than empty.</summary>
    public string? Badge => Count is { } c ? c.ToString() : null;
}

/// <summary>
/// Builds the tab strip shared by the desktop Alerts window and EQBuddy Mobile. Pure:
/// takes counts, returns headers, so it cannot drift from what those tabs contain.
/// </summary>
public static class AlertSurface
{
    /// <summary>The canonical label for each tab.
    ///
    /// "Crowd control" rather than "Mez/Charm": the card it replaces was named for the
    /// two spells EQBuddy happens to track, and a player who roots or stuns is looking
    /// for the same screen. The catalog behind it is already called the CC list.</summary>
    public static string LabelFor(AlertTab tab) => tab switch
    {
        AlertTab.Watch => "Watch rules",
        AlertTab.Buffs => "Buffs",
        AlertTab.Spawns => "Spawns",
        AlertTab.Crowd => "Crowd control",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label.
    ///
    /// <c>tracked</c> for Watch is deliberate and is NOT a typo: that is the settings key
    /// the Watch card has always used (<c>SectionOrder</c>, <c>HiddenSections</c>,
    /// <c>EQBUDDY_EXPAND</c>), and reusing it is what lets the fold in step 5 of the
    /// recipe preserve a player's card position and hidden state instead of resetting
    /// them. Inventing a fresh key here would silently move everyone's card.</summary>
    public static string KeyFor(AlertTab tab) => tab switch
    {
        AlertTab.Watch => "tracked",
        AlertTab.Buffs => "buffs",
        AlertTab.Spawns => "spawns",
        AlertTab.Crowd => "crowd",
        _ => tab.ToString().ToLowerInvariant(),
    };

    public static AlertTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "tracked" or "watch" => AlertTab.Watch,
        "buffs" => AlertTab.Buffs,
        "spawns" => AlertTab.Spawns,
        "crowd" or "mez" or "charm" => AlertTab.Crowd,
        _ => null,
    };

    /// <summary>The full strip, always all four tabs in a fixed order. An empty tab still
    /// gets its place: a Spawns tab that vanishes when no timer is running is a silent
    /// no-op, and a player who has never set one up is exactly who needs to find it.</summary>
    public static IReadOnlyList<AlertTabHeader> Tabs(
        int? watch = null, int? buffs = null, int? spawns = null, int? crowd = null)
    {
        return
        [
            Header(AlertTab.Watch, watch),
            Header(AlertTab.Buffs, buffs),
            Header(AlertTab.Spawns, spawns),
            Header(AlertTab.Crowd, crowd),
        ];

        static AlertTabHeader Header(AlertTab tab, int? count) =>
            new(tab, LabelFor(tab), KeyFor(tab), count);
    }

    /// <summary>The launcher card's one-line summary — step 3 of the recipe, the line that
    /// has to justify replacing two cards with one. Modelled on the Quests card's
    /// "Epic 0/486 · Sky 0/222": name what is CONFIGURED, so the card still answers the
    /// question its predecessors answered at a glance.
    ///
    /// Deliberately not a live count of what is FIRING — that is the chips' job, on the
    /// overlay, and a launcher that flickers with the session would be a meter.</summary>
    public static string LauncherSummary(int watchRules, int buffSets, int spawnTimers)
    {
        var parts = new List<string>();
        if (watchRules > 0) parts.Add($"{watchRules} watch");
        if (buffSets > 0) parts.Add($"{buffSets} buff {(buffSets == 1 ? "set" : "sets")}");
        if (spawnTimers > 0) parts.Add($"{spawnTimers} timers");
        // Nothing configured is a real and common state — a fresh profile — and it should
        // invite rather than read as broken.
        return parts.Count > 0 ? string.Join(" · ", parts) : "none set up";
    }
}
