namespace EQBuddy.Core;

/// <summary>
/// Which of the guided shapes a criterion gets. The enum exists so the must-list can be
/// written as a test (trap 34): a negative rule — "no criterion invents a quest" — cannot
/// see a MEMBER OF <see cref="UnlockNeed"/> THAT NOBODY DECIDED ABOUT, and a new dump line
/// shape arriving with no guidance would read as coverage.
/// </summary>
public enum UnlockGuidanceShape
{
    /// <summary>Nothing to guide: the row is a fact about how the unlock can happen rather
    /// than work (Derived, Bypass). These never even reach a surface —
    /// <see cref="UnlockProgress.Actionable"/> filters them — and the shape is decided
    /// anyway, because "it cannot arrive" is a claim about today's filter and not about the
    /// enum.</summary>
    None,
    /// <summary>"Get maximum faction with X": the player's own kills that moved it, the
    /// kills-to-go arithmetic, and a wiki door.</summary>
    FactionGrind,
    /// <summary>"Obtain X": the Plane of Sky checklist's own piece count, and a door to the
    /// tab that guides it.</summary>
    SkyPieces,
    /// <summary>"Complete the 'X' Task": a door to the General tab when the quest catalog
    /// knows that name, and silence when it does not.</summary>
    CatalogQuest,
}

/// <summary>Where a guidance door leads. The surface resolves the destination — a URL
/// through <c>WikiLinks.Faction</c>, a tab switch of its own — because Core does not know
/// what a tab is and a URL builder does not belong beside a checklist.</summary>
public enum UnlockDoorKind
{
    WikiFaction,
    SkyTab,
    GeneralTabQuest,
}

/// <summary>One door under an unlock row.</summary>
/// <param name="Target">What to open: a faction name, a
/// <see cref="QuestChecklistLayout.RewardKey"/>, or a quest name. The surface's only job is
/// to carry it where it goes.</param>
public sealed record UnlockDoor(UnlockDoorKind Kind, string Target, string Tip);

/// <summary>
/// The guided detail under one unlock criterion, already worded.
///
/// <para>Every field is a SENTENCE with one producer, for the reason
/// <c>QuestChecklistCard</c> gives: two surfaces that each word the same measurement are
/// two answers, and the phone's copy is the one that goes stale. A surface decides layout
/// and the order these are drawn in, and nothing else.</para>
/// </summary>
/// <param name="Movers">The player's own kills that moved this faction, raisers first,
/// capped by <see cref="UnlockGuidance.MoverCap"/> each way.</param>
/// <param name="Estimate">"≈N more kills at your observed rate", or empty. Empty is the
/// normal case and never a template: no observed raiser, or nothing left to raise.</param>
/// <param name="CapNote">Said only when the cap actually WITHHELD movers — a surviving cap
/// says so out loud (trap 50).</param>
/// <param name="Pieces">The Sky checklist's own count for an Obtain row, or empty.</param>
public sealed record UnlockGuidanceRow(
    IReadOnlyList<string> Movers,
    string Estimate,
    string CapNote,
    string Pieces,
    UnlockDoor? Door)
{
    public static readonly UnlockGuidanceRow Nothing = new([], "", "", "", null);

    /// <summary>Every sentence this row adds, in the order a surface draws them: what the
    /// checklist knows, then what your own log knows, then what it implies. Ordering lives
    /// here rather than in each renderer for the same reason the words do.</summary>
    public IReadOnlyList<string> Lines =>
    [
        .. Pieces.Length > 0 ? (string[])[Pieces] : [],
        .. Movers,
        .. CapNote.Length > 0 ? (string[])[CapNote] : [],
        .. Estimate.Length > 0 ? (string[])[Estimate] : [],
    ];

    /// <summary>Nothing to add — the row draws exactly what it drew before this feature
    /// existed. The common case, and it has to STAY the common case: a faction nobody has
    /// farmed and a reward no checklist knows are silence, not a template (trap 73).</summary>
    public bool IsEmpty => Lines.Count == 0 && Door is null;
}

/// <summary>
/// What a player can DO about an unlock criterion, from stores EQBuddy already has
/// (Founder ask, DRA-65: *"each Unlock needs more guided detail"*).
///
/// <para><b>It is a cross-reference and an arithmetic, never a quest.</b> The Unlocks tab
/// has always said where you stand — "1,535 / 2,000 — 465 to go" — and never how to move,
/// so the three shapes here are the three answers that already exist on disk:</para>
///
/// <list type="number">
/// <item><b>Your own kills.</b> <see cref="MobHistory.Pool"/> already folds per-creature
/// faction hits across every archived session plus the live one, because the #65 wiki pack
/// needed them. Inverted for one faction that is "which mobs move this, measured from your
/// own log" — log-only, personal, and nobody else's play is measured.</item>
/// <item><b>The Sky checklist.</b> A class unlock's "Obtain Skycleaver" names a reward group
/// the Plane of Sky tab already guides; the two have never pointed at each other.</item>
/// <item><b>The quest catalog.</b> A Task row carries a quoted quest name. On an exact match
/// it gets a door; on no match it keeps the dump's own sentence and gains nothing.</item>
/// </list>
///
/// <para><b>The tick never moves.</b> An unlock is the GAME's answer — the achievement flag,
/// or the faction dump — and every sentence here is additive. "Pieces in hand" is bag
/// evidence and "obtained" is the game's record; wording them as one fact would be trap 4
/// with two sources for one claim.</para>
///
/// <para><b>The kill-X/loot-Y objective grammar is deliberately refused</b> (Founder
/// soft-leave). An unlock is a grind or a pointer, not a checklist step, so DRA-41's
/// <i>idiom</i> carries over — one producer per sentence, drawn in one place per surface —
/// and its <i>grammar</i> does not.</para>
/// </summary>
public static class UnlockGuidance
{
    /// <summary>How many movers are shown each way. Three, and the cap SAYS so when it
    /// withheld something: a "top N" list that quietly drops the rare row is the failure
    /// trap 50 is about, and on a faction grind the fourth-best raiser is exactly the camp
    /// somebody is looking for.</summary>
    public const int MoverCap = 3;

    /// <summary>
    /// The decided shape for each kind of criterion — the MUST-LIST half of trap 34.
    ///
    /// <para>Null means "nobody has decided", which is only reachable by adding a member to
    /// <see cref="UnlockNeed"/>; <c>UnlockGuidanceMustListTests</c> fails on it. A default
    /// arm returning <see cref="UnlockGuidanceShape.None"/> would have swallowed exactly
    /// that case, and "no guidance" and "no decision" are different answers.</para>
    /// </summary>
    public static UnlockGuidanceShape? ShapeFor(UnlockNeed need) => need switch
    {
        UnlockNeed.MaxFaction => UnlockGuidanceShape.FactionGrind,
        UnlockNeed.Obtain => UnlockGuidanceShape.SkyPieces,
        UnlockNeed.Task => UnlockGuidanceShape.CatalogQuest,
        UnlockNeed.Derived => UnlockGuidanceShape.None,
        UnlockNeed.Bypass => UnlockGuidanceShape.None,
        _ => null,
    };

    /// <summary>
    /// Guidance for one criterion under one unlock.
    /// </summary>
    /// <param name="unlock">The parent, for the class an Obtain row belongs to. A granted
    /// (<see cref="UnlockProgress.Inherited"/>) unlock is NOT suppressed here: the movers
    /// are still true about the player's own kills, and the tick logic this does not touch
    /// is where inheritance matters.</param>
    /// <param name="pool">Pooled per-creature observations —
    /// <see cref="MobHistory.Pool"/>'s own output. Empty is the normal state for a player
    /// who has never fought this faction's mobs, and it draws nothing.</param>
    public static UnlockGuidanceRow Resolve(
        UnlockProgress unlock,
        UnlockCriterion criterion,
        FactionsFile.Snapshot? factions,
        IReadOnlyList<MobSummary>? pool,
        IEnumerable<SkyQuestChecklistItem>? skyItems,
        IReadOnlyCollection<string>? skyCompleted,
        QuestCatalog? catalog) =>
        ShapeFor(criterion.Need) switch
        {
            UnlockGuidanceShape.FactionGrind => Faction(criterion, factions, pool ?? []),
            UnlockGuidanceShape.SkyPieces => Sky(unlock, criterion, skyItems, skyCompleted),
            UnlockGuidanceShape.CatalogQuest => Task(criterion, catalog),
            _ => UnlockGuidanceRow.Nothing,
        };

    // ---- MaxFaction: the player's own kills, and a door to the wiki ------------------

    private static UnlockGuidanceRow Faction(
        UnlockCriterion criterion, FactionsFile.Snapshot? factions, IReadOnlyList<MobSummary> pool)
    {
        // The door is unconditional, and that is the point: it is the one answer that does
        // not depend on having farmed anything, and it costs eqlwiki nothing until the
        // player clicks it. No fetch, no harvested prose — a name and a link.
        var door = new UnlockDoor(UnlockDoorKind.WikiFaction, criterion.Subject, WikiFactionTip);

        var standing = FactionNames.Resolve(factions, criterion.Subject);
        // Every (mob, zone) in the pool whose own faction ledger names this faction. The
        // names come from three different files — the log, the faction dump and the
        // achievements text — so the fold that decides "same faction" is FactionNames'
        // and not a fourth copy of it here.
        var hits = pool
            .SelectMany(m => m.Factions.Select(f => (Mob: m, Hit: f)))
            .Where(x => FactionNames.Same(x.Hit.Faction, criterion.Subject)
                        || (standing is { } s && FactionNames.Same(x.Hit.Faction, s.Name)))
            .ToList();

        var raisers = hits.Where(x => x.Hit.Delta > 0)
            .OrderByDescending(x => x.Hit.Delta).ThenByDescending(x => x.Hit.Hits)
            .ThenBy(x => x.Mob.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var costs = hits.Where(x => x.Hit.Delta < 0)
            .OrderBy(x => x.Hit.Delta).ThenByDescending(x => x.Hit.Hits)
            .ThenBy(x => x.Mob.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var movers = new List<string>();
        foreach (var x in raisers.Take(MoverCap)) movers.Add(MoverLine(x.Mob, x.Hit));
        foreach (var x in costs.Take(MoverCap)) movers.Add(MoverLine(x.Mob, x.Hit));

        var withheld = Math.Max(0, raisers.Count - MoverCap) + Math.Max(0, costs.Count - MoverCap);
        var cap = withheld > 0
            ? $"{withheld} more {(withheld == 1 ? "creature" : "creatures")} in your log "
              + $"{(withheld == 1 ? "moves" : "move")} this faction — the {MoverCap} biggest "
              + "each way are shown."
            : "";

        // The arithmetic, and only where there is something to divide. A maxed standing has
        // nothing left to estimate, a missing dump has no distance to go, and a faction with
        // no observed raiser has no rate — all three are silence rather than a sentence
        // with a guessed number in it.
        var estimate = "";
        if (standing is { Maxed: false, PointsToMax: > 0 } s2 && raisers.FirstOrDefault() is { Hit: not null } best)
        {
            var kills = (int)Math.Ceiling((double)s2.PointsToMax / best.Hit.Delta);
            estimate = $"≈{kills:N0} more {(kills == 1 ? "kill" : "kills")} of {Named(best.Mob)} "
                + $"at +{best.Hit.Delta} each — an estimate from your own log, not a target.";
        }

        return new UnlockGuidanceRow(movers, estimate, cap, "", door);
    }

    /// <summary>One mover, signed. A raiser and a cost are the same measurement read in two
    /// directions, so they are one sentence shape with one word different — suppressing the
    /// costs would hide the thing a faction grinder most needs to stop doing.</summary>
    private static string MoverLine(MobSummary mob, MobFactionHit hit)
    {
        // "Seen on N of your kills", not "N kills": <see cref="MobFactionHit.Hits"/> counts the
        // kills that PRODUCED a faction line, which is not the same number as the kills. A mob
        // killed forty times while the faction sat at the cap has forty kills and no hits, and
        // saying "40 kills" beside a per-kill delta would be a claim the log never made.
        var seen = $"seen on {hit.Hits:N0} of your kills";
        return hit.Delta > 0
            ? $"Your kills of {Named(mob)} moved it +{hit.Delta} each — {seen}."
            : $"Your kills of {Named(mob)} cost you {-hit.Delta} each — {seen}.";
    }

    /// <summary>The creature and where you killed it. The zone is carried because the pool is
    /// keyed on it — "an ice giant" in two zones is two mobs — and because a camp is the
    /// actionable half of a mover.</summary>
    private static string Named(MobSummary mob) =>
        mob.Zone is { Length: > 0 } zone ? $"{mob.Name} in {zone}" : mob.Name;

    // ---- Obtain: the Sky checklist's own count -----------------------------------------

    private static UnlockGuidanceRow Sky(
        UnlockProgress unlock, UnlockCriterion criterion,
        IEnumerable<SkyQuestChecklistItem>? skyItems, IReadOnlyCollection<string>? skyCompleted)
    {
        // The reward group as the Sky tab itself groups it: (class, reward). A class unlock
        // names its own class, so this is a lookup and never a guess.
        var rows = (skyItems ?? [])
            .Where(i => i.ClassName.Equals(unlock.Subject, StringComparison.OrdinalIgnoreCase)
                        && i.Reward.Equals(criterion.Subject, StringComparison.OrdinalIgnoreCase))
            .ToList();
        // A reward the checklist does not know is a row that gains NOTHING — byte-identical
        // to what it drew before. There is no piece count to report and no tab to open onto.
        if (rows.Count == 0) return UnlockGuidanceRow.Nothing;

        var key = QuestChecklistLayout.RewardKey(unlock.Subject, criterion.Subject);
        var have = rows.Count(i => i.Acquired);
        // "In hand" is the BAGS, and it is a different claim from the achievement's
        // "obtained" — which is why the two are never joined into one number (trap 4). The
        // turn-in clause is the Sky tab's own store, said as the Sky tab's answer.
        var turnedIn = skyCompleted is not null && skyCompleted.Contains(key, StringComparer.OrdinalIgnoreCase);
        var pieces = $"{have} of {rows.Count} pieces in hand"
            + (turnedIn ? " · marked turned in on the Plane of Sky tab." : " — the Plane of Sky tab has the guide.");

        return new UnlockGuidanceRow([], "", "", pieces,
            new UnlockDoor(UnlockDoorKind.SkyTab, key, SkyTabTip));
    }

    // ---- Task: the quoted quest name, matched against the catalog ----------------------

    /// <summary>The quest name a Task row quotes — <c>Complete the 'Aid the Kerrans of Kerra
    /// Isle' Task.</c> — or empty when the sentence carries no quoted name.
    ///
    /// <para>First quote to LAST quote, not first to next: a quest name may contain an
    /// apostrophe, and the closing quote is the last one on the line either way.</para></summary>
    public static string QuotedQuestName(string text)
    {
        var open = (text ?? "").IndexOf('\'');
        var close = (text ?? "").LastIndexOf('\'');
        return open >= 0 && close > open + 1 ? text![(open + 1)..close].Trim() : "";
    }

    private static UnlockGuidanceRow Task(UnlockCriterion criterion, QuestCatalog? catalog)
    {
        var name = QuotedQuestName(criterion.Text);
        if (name.Length == 0 || catalog is null) return UnlockGuidanceRow.Nothing;
        // EXACT, deliberately. A Task is a modern server task and the catalog is the classic
        // wiki's quests; 'Aid the Kerrans of Kerra Isle' matches nothing there today, and a
        // fuzzy match would open the wrong page with complete confidence. Silence is the
        // honest answer and the row keeps the dump's own sentence.
        var match = catalog.Quests.FirstOrDefault(q =>
            q.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? UnlockGuidanceRow.Nothing
            : new UnlockGuidanceRow([], "", "", "",
                new UnlockDoor(UnlockDoorKind.GeneralTabQuest, match.Name, GeneralTabTip));
    }

    // ---- the doors' words, in the one place that owns them ------------------------------

    public const string WikiFactionTip =
        "Open this faction's page on eqlwiki — where to raise it is the wiki's answer, and "
        + "you open the page yourself. EQBuddy never fetches it for you.";

    public const string SkyTabTip =
        "Open the Plane of Sky tab on this reward — its pieces, where they drop and who "
        + "takes the turn-in.";

    public const string GeneralTabTip =
        "Open this quest on the Quests tab — the catalog knows it by this name.";
}
