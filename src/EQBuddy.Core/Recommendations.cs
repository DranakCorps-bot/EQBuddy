namespace EQBuddy.Core;

/// <summary>
/// What a player can be working toward — <b>the Founder's own nine, verbatim</b>, in the
/// order he wrote them (DRA-70, 2026-09-12; the plan's §2, Helm-signed).
///
/// <para><b>Nothing is invented and nothing is dropped.</b> The list extends PRD §12
/// HOME-001's five default categories rather than replacing them: "Level faster", "Upgrade
/// gear", "Farm motes" and "Make money" are here under the Founder's wording, and
/// HOME-001's fifth — "Continue quests" — is deliberately <b>not</b> a goal, because the
/// Guide room already is that answer and a chip that opened a second copy of it would be a
/// room competing with itself.</para>
///
/// <para>The enum is the must-list's subject: <see cref="Recommendations.ShapeFor"/> has to
/// have decided about every member, and a tenth arriving with no decision fails the build
/// rather than falling through a default arm into silence (trap 34).</para>
/// </summary>
public enum HelperGoal
{
    LevelUp,
    FarmGear,
    UnlockClasses,
    UnlockRaces,
    FarmMotes,
    WorkOnFaction,
    FarmMaterials,
    MakeMoney,
    Achievements,
}

/// <summary>Whether a goal has an engine behind it today. Both values are DECISIONS — see
/// <see cref="Recommendations.ShapeFor"/>, which answers null for neither.</summary>
public enum HelperGoalShape
{
    /// <summary>An engine answers it now: a selected chip either produces recommendations
    /// or names the store it is waiting for.</summary>
    Answered,

    /// <summary>Decided, and the decision is that a later slice owns the answer. A selected
    /// chip says so in its own words and hands over the door to the room that answers the
    /// question TODAY — a chip that produced nothing and pointed nowhere would be the rail's
    /// own forbidden shape ("an affordance that opens nothing is a trap") wearing a
    /// pill.</summary>
    Deferred,
}

/// <summary>
/// Where one why-line's claim comes from — <b>HOME-003 and HOME-004 as a tag rather than as
/// a convention</b>.
///
/// <para>HOME-004 is the rule with teeth: <i>when EQBuddy has insufficient personal
/// evidence it may recommend from verified catalog information, but must identify that it
/// is an estimate rather than "your expected rate."</i> A rule like that written as prose
/// discipline lasts one author; written as a required field on every line, a line that
/// forgot to say where it came from cannot be constructed.</para>
/// </summary>
public enum Evidence
{
    /// <summary>Measured from this player's own log, dumps or bags. Carries its own scope —
    /// "across 14 of your sessions" — because a rate with no denominator is a claim nobody
    /// can argue with.</summary>
    Personal,

    /// <summary>From a catalog EQBuddy ships. Always drawn with the estimate label
    /// (<c>HelperPresentation.CatalogLabel</c>), never as a measurement of your play.</summary>
    Catalog,
}

/// <summary>
/// One reason a recommendation is being shown — <b>HOME-002's "a recommendation should
/// explain why it is being shown"</b>, as a fact rather than as a sentence.
///
/// <para><b>The words are NOT here, and that is this file's one real design decision.</b>
/// <c>UnlockGuidance</c> — the mini-recommender this one copies its manners from — words its
/// own sentences in Core, and that is right for it: it produces four sentences and the
/// surfaces that draw them decide layout and nothing else. The Helper produces a sentence
/// per goal per shape across nine goals and two hosts, and the rule it has to keep
/// (HOME-006: no line may claim a camp is safe) is a rule about VOCABULARY. A guard over
/// vocabulary can only be written where the vocabulary is, so every word the Helper says
/// lives in <c>UI.Shared/HelperPresentation</c> and is swept there; Core carries the
/// numbers.</para>
///
/// <para><see cref="WordedFact"/> is the deliberate exception and it proves the rule — a
/// sentence another producer has already measured AND phrased is passed through verbatim
/// rather than re-phrased, because two surfaces wording one arithmetic are two answers and
/// the newer copy is always the one that goes stale.</para>
/// </summary>
public abstract record WhyFact(Evidence Evidence);

/// <summary>Your observed experience rate in a zone, with the scope it rests on.</summary>
public sealed record ZoneXpRateFact(string Zone, double XpPerHour, int Sessions, double Hours)
    : WhyFact(Evidence.Personal);

/// <summary>How long your fights here actually take, and over how many kills.</summary>
public sealed record ZoneCadenceFact(string Zone, double AvgFightSeconds, int Kills)
    : WhyFact(Evidence.Personal);

/// <summary>
/// What your own history says about dying here — <b>HOME-006's only permitted shape</b>.
///
/// <para>It is emitted ONLY when the deaths are real (<c>Deaths &gt; 0</c>). A zone you have
/// never died in produces no fact at all, because "you have not died here" is one sitting
/// away from being false and would read as the safety claim HOME-006 forbids. The absence of
/// evidence is drawn as nothing, never as reassurance.</para>
/// </summary>
public sealed record ZoneDeathsFact(string Zone, int Deaths, int Sessions)
    : WhyFact(Evidence.Personal);

/// <summary>How far along an unlock is, from the game's own achievements dump.</summary>
public sealed record UnlockScoreFact(string Subject, int Done, int Total)
    : WhyFact(Evidence.Personal);

/// <summary>Where you stand with a faction, as the faction dump reported it.</summary>
public sealed record FactionStandingFact(string Faction, int Value, int PointsToMax)
    : WhyFact(Evidence.Personal);

/// <summary>
/// EQBuddy's quest catalog knows the quest an unlock criterion names.
///
/// <para><b>The one genuinely catalog-sourced line D1 ships</b>, and it is worth saying why
/// there is only one. The Level Up engine has no generic camp catalog to fall back on — the
/// plan PARKS that until somebody asks for it — so its answers are personal-only and honest
/// about it; the faction and unlock engines read your dumps. A Task criterion matching the
/// catalog by name is the one claim in D1 that comes from a file EQBuddy ships rather than
/// from your play, so it is the one that carries the estimate label.</para>
/// </summary>
public sealed record CatalogQuestFact(string Quest) : WhyFact(Evidence.Catalog);

/// <summary>
/// A sentence another producer has already measured and phrased, passed through untouched.
///
/// <para>Every one of these today is <c>UnlockGuidance</c>'s: the faction movers, the
/// "≈N more kills at +X each" estimate, the cap note, the Sky piece count. The Helper asks
/// the same question of the same stores from a different room, so it asks the same code and
/// prints the same answer rather than growing a second wording of one arithmetic.</para>
/// </summary>
/// <param name="Text">The sentence, already complete. <c>HelperPresentation</c> draws it and
/// adds nothing to it.</param>
public sealed record WordedFact(string Text, Evidence Evidence) : WhyFact(Evidence);

/// <summary>Where a recommendation's door leads. Core decides THAT there is a door and what
/// it points at; <c>HelperPresentation</c> resolves the destination, because the
/// <c>page:room</c> address grammar belongs to <c>ShellPages</c> and Core does not know what
/// a room is.</summary>
public enum HelperDoorKind
{
    /// <summary>The World room — the map, the camps and how to get there.</summary>
    World,
    /// <summary>The Guide room's Unlocks tab.</summary>
    Unlocks,
    /// <summary>The Guide room's Plane of Sky tab.</summary>
    SkyRewards,
    /// <summary>The Guide room's quest list.</summary>
    QuestCatalog,
    /// <summary>Progress → Faction: every standing the dump reported.</summary>
    FactionStandings,
    /// <summary>The faction's own page on eqlwiki. Player-clicked — EQBuddy fetches
    /// nothing.</summary>
    WikiFaction,
    /// <summary>The Gear room: bags, wishlist, what dropped for you.</summary>
    Gear,
    /// <summary>Progress → Wealth, which is where motes and coin already live.</summary>
    Wealth,
}

/// <summary>One door under a recommendation.</summary>
/// <param name="Target">What to open, in the destination's own vocabulary: a zone name, a
/// faction name, a <c>QuestChecklistLayout.RewardKey</c>, a quest name, or empty where the
/// destination is a room rather than a thing.</param>
public sealed record HelperDoor(HelperDoorKind Kind, string Target);

/// <summary>What a recommendation is ABOUT, which is what decides how its headline reads.
/// A place, a thing you are unlocking, or a reward — never a sentence.</summary>
public enum RecommendationKind
{
    /// <summary>A place. The only kind a cross-domain join can produce, because the join key
    /// IS the zone.</summary>
    Zone,
    /// <summary>A race or class unlock, with nowhere in particular to go for it.</summary>
    Unlock,
    /// <summary>A faction, with nowhere in particular to go for it — you have never killed
    /// anything that moves it, so the wiki is the honest next step.</summary>
    Faction,
}

/// <summary>
/// One ranked answer to "what should I do next?".
/// </summary>
/// <param name="Subject">The proper noun this is about — a zone, a race, a class, a faction.
/// Game data, never prose; the headline is <c>HelperPresentation</c>'s.</param>
/// <param name="Zone">The place, when there is one. <b>The join key</b> (HOME-005): two
/// candidates from different goals naming one zone become one recommendation.</param>
/// <param name="Goals">Which of the player's selected goals this serves. More than one is
/// the cross-domain chain the PRD calls the key differentiator, and it is the first sort
/// key.</param>
/// <param name="WithheldWhy">How many why-lines the cap held back. Drawn out loud when it is
/// not zero — a surviving cap says so (trap 50).</param>
/// <param name="Weight">The engine's own ordering within its kind, 0..1. <b>A tie-break and
/// never a claim</b>: 0.7 of an unlock is not "better than" 0.6 of a zone, and nothing here
/// pretends the two are measured in the same units.</param>
public sealed record Recommendation(
    RecommendationKind Kind,
    string Subject,
    string Zone,
    IReadOnlyList<HelperGoal> Goals,
    IReadOnlyList<WhyFact> Why,
    IReadOnlyList<HelperDoor> Doors,
    int WithheldWhy,
    double Weight)
{
    /// <summary>How many of the why-lines are the player's own evidence. Reported rather than
    /// ranked on — see <see cref="HasPersonalEvidence"/> for why the SORT is a boolean.</summary>
    public int PersonalWhy => Why.Count(w => w.Evidence == Evidence.Personal);

    /// <summary>
    /// Does this answer rest on the player's own play at all? <b>The second sort key, and it
    /// is deliberately a BOOLEAN rather than <see cref="PersonalWhy"/>.</b>
    ///
    /// <para>HOME-003 says personal evidence outranks generic advice. It does not say more
    /// sentences outrank fewer, and ranking on the count makes it say that: a faction grind
    /// with four movers would outrank the fastest camp this character has ever farmed,
    /// because it had more lines. A line count is a proxy for confidence and confidence is a
    /// claim about the world (trap 64b) — so the question asked is the one HOME-003 actually
    /// poses, and the magnitude argument is left to <see cref="Weight"/>, which at least
    /// knows what it is measuring.</para>
    ///
    /// <para><b>It is a sort and never a filter.</b> A catalog-only answer is still shown; it
    /// just does not outrank one measured from your play.</para>
    /// </summary>
    public bool HasPersonalEvidence => Why.Any(w => w.Evidence == Evidence.Personal);
}

/// <summary>Why a selected goal has an engine and still produced nothing. Each maps to one
/// already-worded empty state naming what would feed it — and, where the answer is a dump,
/// the command that writes it.</summary>
public enum GoalGapReason
{
    /// <summary>No faction dump has ever been read.</summary>
    NoFactionDump,
    /// <summary>The faction dump is there and the player has not picked a faction to work
    /// on. The sub-picker is the answer, not a command.</summary>
    NoFactionPicked,
    /// <summary>No achievements dump has ever been read.</summary>
    NoAchievementsDump,
    /// <summary>The dumps are read and there is genuinely nothing left: every unlock in this
    /// half is complete, or every picked faction is maxed. A finished job is not a gap in the
    /// data and must not be worded as one.</summary>
    NothingLeftToDo,
    /// <summary>Not enough of the player's own play is stored to divide — see
    /// <see cref="ZoneHistory.MinHours"/>. The only honest answer is to say so.</summary>
    NoPlayHistory,
}

/// <summary>One selected goal that produced no recommendation, and why.</summary>
public sealed record GoalGap(HelperGoal Goal, GoalGapReason Reason);

/// <summary>
/// Everything the Helper needs, read once by its host and handed over in one object.
/// </summary>
/// <remarks>
/// One parameter object rather than eleven arguments, for the reason trap 33 names: two
/// callers with different arguments produce two current answers and whichever ran last wins.
/// The desktop room and — at D4 — the phone's projection both call
/// <see cref="Recommendations.Rank"/> with one of these, so "the phone showed something
/// else" is a question about the inputs rather than about which overload somebody picked.
/// </remarks>
/// <param name="Zones">Per-zone rollup — <see cref="ZoneHistory.Fold"/>'s own output.</param>
/// <param name="Pool">Pooled creatures — <see cref="MobHistory.Pool"/>'s own output.</param>
/// <param name="Factions">The newest faction dump, or null. Null is a real state: it is the
/// difference between "you are not maxed with anyone" and "EQBuddy has never been told".</param>
/// <param name="PickedFactions">Which factions the player chose to work on. Empty means the
/// picker has not been used — not "all of them", because 200 standings is not a
/// recommendation.</param>
/// <param name="HasAchievements">Whether the achievements dump has ever been read.</param>
public sealed record HelperInputs(
    IReadOnlyList<ZoneRoll> Zones,
    IReadOnlyList<MobSummary> Pool,
    FactionsFile.Snapshot? Factions,
    IReadOnlyList<string> PickedFactions,
    IReadOnlyList<UnlockProgress> Races,
    IReadOnlyList<UnlockProgress> Classes,
    bool HasAchievements,
    IReadOnlyList<SkyQuestChecklistItem> SkyItems,
    IReadOnlyCollection<string> SkyCompleted,
    QuestCatalog? Catalog)
{
    public static readonly HelperInputs Nothing =
        new([], [], null, [], [], [], false, [], [], null);
}

/// <summary>The whole answer for one set of chips.</summary>
/// <param name="Top">The ranked recommendations, capped.</param>
/// <param name="Withheld">How many candidates the cap held back. Said out loud when it is
/// not zero — <b>HOME-002 wants three strong answers, and trap 50 wants the cap to admit
/// it</b>, because the fourth-best camp is exactly the one somebody is looking for.</param>
/// <param name="NotAnsweredYet">Selected goals whose shape is <see cref="HelperGoalShape.Deferred"/>.</param>
/// <param name="Gaps">Selected, answerable goals that produced nothing, each with the reason.</param>
public sealed record RecommendationSet(
    IReadOnlyList<Recommendation> Top,
    int Withheld,
    IReadOnlyList<HelperGoal> NotAnsweredYet,
    IReadOnlyList<GoalGap> Gaps)
{
    public static readonly RecommendationSet Empty = new([], 0, [], []);
}

/// <summary>
/// **"What should I do next?" — the Helper's engine** (PRD §12 HOME-001..006; Founder ask
/// DRA-70; Fable's plan 2026-09-12, Helm-signed).
///
/// <para><b>It is the first thing in this codebase that ranks across domains</b>, which is
/// the whole reason it went through a plan. Everything it reads already existed and every
/// one of those stores answered exactly one question: <c>ZoneHistory</c> knows where you
/// level fastest, <c>UnlockGuidance</c> knows which of your kills move a faction,
/// <c>MobHistory.Pool</c> knows what you have fought. None of them has ever been asked
/// "which of these is worth doing this evening", and the PRD's own §12 names the reason to
/// ask it: <i>"Good XP for your observed performance + a gear upgrade + a tracked quest step
/// in the same area. This cross-domain chain is a key EQBuddy differentiator."</i></para>
///
/// <para><b>THE JOIN KEY IS THE ZONE, and that is the feature.</b> A place serving two of
/// your selected goals outranks either alone, because the player's evening is spent in one
/// place and not in a list. Everything else about the ranking is a tie-break.</para>
///
/// <para><b>The manners are <c>UnlockGuidance</c>'s, copied deliberately:</b> one producer
/// per sentence, arithmetic from your own log, a door at the end of every row, and silence
/// where there is nothing true to say. A goal with no evidence draws an honest empty state
/// naming what would feed it — never a template with a guessed number in it (trap 73).</para>
///
/// <para><b>HOME-006 is a refusal and not a caveat.</b> Nothing here produces a fact that
/// could be worded as "this camp is safe". <see cref="ZoneDeathsFact"/> is the only
/// survival-adjacent shape and it exists only where the player has actually died; the
/// vocabulary guard lives on <c>HelperPresentation</c>, which is where the words are.</para>
///
/// <para><b>Nothing here measures another player.</b> Every input is this character's own
/// log, this character's own dumps and catalogs EQBuddy ships. There is no comparison, no
/// ranking against anyone, and no number that came off somebody else's screen.</para>
/// </summary>
public static class Recommendations
{
    /// <summary>
    /// How many recommendations are shown by default. <b>Three, and the PRD asked for the
    /// number out loud</b> — HOME-002: <i>"prefer three strong recommendations to thirty weak
    /// ones."</i> The cap reports what it withheld, which is trap 50's whole rule: a "top N"
    /// list that silently drops the rare row hides exactly what a player is hunting for.
    /// </summary>
    public const int DefaultCap = 3;

    /// <summary>How many why-lines one recommendation draws before it says it is holding
    /// some back. Four, because a headline plus five reasons stops being a recommendation
    /// and becomes a report — and the withheld count is carried on the record rather than
    /// dropped, for the same reason the cap above reports itself.</summary>
    public const int WhyCap = 4;

    /// <summary>How many candidates one engine offers into the join. Deliberately larger
    /// than <see cref="DefaultCap"/>: the join is what decides the winner, so an engine that
    /// only offered three would hide the fourth-best zone that happens to be the one your
    /// faction grind is also in.</summary>
    private const int PerEngineCandidates = 6;

    /// <summary>
    /// The decided shape for each goal — <b>the must-list half of trap 34</b>.
    ///
    /// <para>Null means nobody has decided, which is only reachable by adding a member to
    /// <see cref="HelperGoal"/>; <c>HelperMustListTests</c> fails on it. A default arm
    /// answering <see cref="HelperGoalShape.Deferred"/> would have swallowed exactly that
    /// case, and "a later slice owns this" and "nobody thought about this" are different
    /// answers that would have looked identical on screen.</para>
    ///
    /// <para>Four are answered here (D1); the other five are decided and waiting, each named
    /// in the plan's own slice table. They are not hidden from the chip strip while they
    /// wait: the Founder asked for the nine, a chip that vanished until its engine landed
    /// would make the feature look smaller than it is, and the deferred answer hands over the
    /// door to the room that answers the question today.</para>
    /// </summary>
    public static HelperGoalShape? ShapeFor(HelperGoal goal) => goal switch
    {
        HelperGoal.LevelUp => HelperGoalShape.Answered,
        HelperGoal.WorkOnFaction => HelperGoalShape.Answered,
        HelperGoal.UnlockClasses => HelperGoalShape.Answered,
        HelperGoal.UnlockRaces => HelperGoalShape.Answered,
        HelperGoal.FarmGear => HelperGoalShape.Deferred,
        HelperGoal.FarmMotes => HelperGoalShape.Deferred,
        HelperGoal.MakeMoney => HelperGoalShape.Deferred,
        HelperGoal.FarmMaterials => HelperGoalShape.Deferred,
        HelperGoal.Achievements => HelperGoalShape.Deferred,
        _ => null,
    };

    /// <summary>Every goal, in the Founder's order — the chip strip's own list, read from
    /// the enum rather than hand-written beside it (trap 30: a hand-maintained list stops
    /// covering the set the day the set grows).</summary>
    public static IReadOnlyList<HelperGoal> All => Enum.GetValues<HelperGoal>();

    /// <summary>
    /// Rank the answers for one set of selected goals.
    /// </summary>
    /// <param name="selected">The player's chips. <b>Empty means all of them</b> —
    /// HOME-001's "goals/filters rather than a permanent wall of sections" read the way a
    /// filter works everywhere else: nothing ticked is not nothing shown.</param>
    /// <param name="cap">How many to return. <see cref="DefaultCap"/> unless a caller has a
    /// reason; the count withheld comes back either way.</param>
    public static RecommendationSet Rank(
        HelperInputs inputs, IReadOnlyCollection<HelperGoal>? selected, int cap = DefaultCap)
    {
        inputs ??= HelperInputs.Nothing;
        var goals = selected is { Count: > 0 }
            ? All.Where(selected.Contains).ToList()
            : All.ToList();

        var deferred = goals.Where(g => ShapeFor(g) == HelperGoalShape.Deferred).ToList();
        var gaps = new List<GoalGap>();
        var candidates = new List<Recommendation>();

        if (goals.Contains(HelperGoal.LevelUp)) LevelUp(inputs, candidates, gaps);
        if (goals.Contains(HelperGoal.WorkOnFaction)) Faction(inputs, candidates, gaps);
        if (goals.Contains(HelperGoal.UnlockClasses))
            Unlocks(inputs, HelperGoal.UnlockClasses, inputs.Classes, candidates, gaps);
        if (goals.Contains(HelperGoal.UnlockRaces))
            Unlocks(inputs, HelperGoal.UnlockRaces, inputs.Races, candidates, gaps);

        var joined = Join(candidates);
        var ordered = joined
            .OrderByDescending(r => r.Goals.Count)
            .ThenByDescending(r => r.HasPersonalEvidence)
            .ThenByDescending(r => r.Weight)
            .ThenBy(r => r.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = ordered.Take(Math.Max(0, cap)).Select(Trim).ToList();
        return new RecommendationSet(top, Math.Max(0, ordered.Count - top.Count), deferred, gaps);
    }

    // ---- the join: one place, every goal it serves (HOME-005) --------------------------

    /// <summary>
    /// Fold candidates that name the same zone into one recommendation.
    ///
    /// <para><b>This is the differentiator, expressed as a GroupBy.</b> "Good XP for your
    /// observed performance + a faction your kills here move" is two engines answering about
    /// one place, and a list that printed them as two rows would be asking the player to
    /// notice the chain themselves. A merged row is <see cref="RecommendationKind.Zone"/>
    /// whatever its parts were: the subject a player acts on is the place they travel to,
    /// and the unlock's own name survives inside its why-line.</para>
    ///
    /// <para>Candidates with no zone stand alone. A Sky reward and a faction nobody has
    /// farmed have no place to join on, and inventing one would be a second spelling of "we
    /// do not know where".</para>
    /// </summary>
    private static List<Recommendation> Join(List<Recommendation> candidates)
    {
        var result = new List<Recommendation>();
        foreach (var group in candidates
                     .Where(c => c.Zone.Length > 0)
                     .GroupBy(c => c.Zone, StringComparer.OrdinalIgnoreCase))
        {
            var parts = group.ToList();
            if (parts.Count == 1) { result.Add(parts[0]); continue; }
            result.Add(new Recommendation(
                RecommendationKind.Zone,
                // The zone as the FIRST part spelled it. Grouped case-insensitively because
                // two sources capitalise differently; the displayed spelling is one of the
                // real ones rather than a normalisation nobody would recognise.
                parts[0].Zone,
                parts[0].Zone,
                [.. parts.SelectMany(p => p.Goals).Distinct()],
                [.. parts.SelectMany(p => p.Why)],
                [.. Dedupe(parts.SelectMany(p => p.Doors))],
                parts.Sum(p => p.WithheldWhy),
                parts.Max(p => p.Weight)));
        }
        result.AddRange(candidates.Where(c => c.Zone.Length == 0));
        return result;
    }

    private static IEnumerable<HelperDoor> Dedupe(IEnumerable<HelperDoor> doors)
    {
        var seen = new HashSet<(HelperDoorKind, string)>();
        foreach (var d in doors)
            if (seen.Add((d.Kind, d.Target.ToLowerInvariant()))) yield return d;
    }

    /// <summary>Apply <see cref="WhyCap"/>, recording what it held back. Personal lines
    /// survive first — HOME-003's sort applied one level down, so a trimmed row keeps the
    /// evidence that earned it its position.</summary>
    private static Recommendation Trim(Recommendation r)
    {
        if (r.Why.Count <= WhyCap) return r;
        var kept = r.Why
            .OrderByDescending(w => w.Evidence == Evidence.Personal)
            .Take(WhyCap)
            .ToList();
        // Back into the order the engines emitted them in: the sort above is a selection,
        // not a reordering, and a why-list that shuffled itself as it grew would read as a
        // different answer each time a session landed.
        var ordered = r.Why.Where(kept.Contains).ToList();
        return r with { Why = ordered, WithheldWhy = r.WithheldWhy + (r.Why.Count - ordered.Count) };
    }

    // ---- Level Up: your own rate, per zone ---------------------------------------------

    /// <summary>
    /// Where you have actually levelled fastest.
    ///
    /// <para><b>Personal-only, and honestly so.</b> There is no shipped camp catalog to fall
    /// back on for a player with no history — the plan PARKS one behind a reporter asking for
    /// it — so a character with nothing stored gets <see cref="GoalGapReason.NoPlayHistory"/>
    /// and a sentence saying what would fill it, rather than a level-range table EQBuddy
    /// would have had to invent (trap 73, and the "match the wiki or say nothing" rule one
    /// step further out: we have no wiki answer here either).</para>
    /// </summary>
    private static void LevelUp(HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        var zones = inputs.Zones.Where(z => z.HasPersonalEvidence).ToList();
        if (zones.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.LevelUp, GoalGapReason.NoPlayHistory));
            return;
        }

        var best = zones.Max(z => z.XpPerHour ?? 0);
        foreach (var z in zones
                     .OrderByDescending(z => z.XpPerHour ?? 0)
                     .Take(PerEngineCandidates))
        {
            var why = new List<WhyFact>
            {
                new ZoneXpRateFact(z.Zone, z.XpPerHour ?? 0, z.Sessions, z.Hours),
            };
            // Unknown is not zero: a zone whose pooled creatures never recorded a fight
            // length says nothing about cadence rather than claiming instant kills.
            if (z.AvgFightSeconds > 0)
                why.Add(new ZoneCadenceFact(z.Zone, z.AvgFightSeconds, z.Kills));
            // HOME-006: deaths are shown where they HAPPENED and silence otherwise. There is
            // deliberately no "and you have never died here" arm — see ZoneDeathsFact.
            if (z.Deaths > 0) why.Add(new ZoneDeathsFact(z.Zone, z.Deaths, z.Sessions));

            into.Add(new Recommendation(
                RecommendationKind.Zone, z.Zone, z.Zone,
                [HelperGoal.LevelUp], why,
                [new HelperDoor(HelperDoorKind.World, z.Zone)],
                0,
                best > 0 ? Math.Clamp((z.XpPerHour ?? 0) / best, 0, 1) : 0));
        }
    }

    // ---- Work on Faction: the grind, for any faction you picked -------------------------

    /// <summary>
    /// The faction work, generalised from the Unlocks tab's own arithmetic.
    ///
    /// <para><b>It calls <see cref="UnlockGuidance.Faction"/> and prints what comes back.</b>
    /// That method was private and took an unlock criterion until this slice; it now takes a
    /// faction NAME, so the sentence a player reads on the Unlocks tab and the sentence they
    /// read here are the same sentence from the same code. The alternative — a second
    /// phrasing of one measurement — is the failure <c>UnlockGuidance</c>'s own comment names,
    /// and the copy that goes stale is always the newer one.</para>
    ///
    /// <para>The picker is required and is not a filter over "all". A faction dump carries
    /// hundreds of standings; recommending against all of them would be thirty weak answers,
    /// which is the exact thing HOME-002 asks for the opposite of.</para>
    /// </summary>
    private static void Faction(HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        if (inputs.Factions is not { } dump)
        {
            gaps.Add(new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NoFactionDump));
            return;
        }
        if (inputs.PickedFactions.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NoFactionPicked));
            return;
        }

        var added = 0;
        foreach (var name in inputs.PickedFactions.Take(PerEngineCandidates))
        {
            var standing = FactionNames.Resolve(dump, name);
            // A maxed faction is a finished job, not a recommendation. It is not a gap
            // either — see NothingLeftToDo below, which fires only when EVERY pick is done.
            if (standing is null or { Maxed: true }) continue;

            var row = UnlockGuidance.Faction(name, dump, inputs.Pool);
            var why = new List<WhyFact>
            {
                new FactionStandingFact(standing.Name, standing.Value, standing.PointsToMax),
            };
            // Every line UnlockGuidance produced, in ITS order, tagged Personal: movers, the
            // cap note and the estimate are each measured from this player's own kills. The
            // Pieces slot is empty on a faction row by construction.
            why.AddRange(row.Lines.Select(line => new WordedFact(line, Evidence.Personal)));

            var doors = new List<HelperDoor>
            {
                new(HelperDoorKind.WikiFaction, standing.Name),
                new(HelperDoorKind.FactionStandings, ""),
            };
            if (row.Zone.Length > 0) doors.Insert(0, new HelperDoor(HelperDoorKind.World, row.Zone));

            into.Add(new Recommendation(
                // A faction you have farmed has a place; one you have not is a
                // Faction-kind row whose honest next step is the wiki.
                row.Zone.Length > 0 ? RecommendationKind.Zone : RecommendationKind.Faction,
                row.Zone.Length > 0 ? row.Zone : standing.Name,
                row.Zone,
                [HelperGoal.WorkOnFaction], why, doors, 0,
                // Closest to done first, as a tie-break inside this goal. Never compared
                // against another kind's weight as though the two were one scale.
                Math.Clamp(1 - standing.PointsToMax / (double)FactionsFile.Cap, 0, 1)));
            added++;
        }

        if (added == 0)
            gaps.Add(new GoalGap(HelperGoal.WorkOnFaction, GoalGapReason.NothingLeftToDo));
    }

    // ---- Unlock Classes / Unlock Races: the top actionable rows -------------------------

    /// <summary>
    /// The unlocks you are closest to finishing, with the guided detail DRA-65 already built.
    ///
    /// <para><b>Consumed as-is — the Helper adds no unlock arithmetic of its own.</b>
    /// <c>UnlockProgress.Score</c> already knows how far along an unlock is and already
    /// refuses to count the rows that are not work; <c>UnlockGuidance.Resolve</c> already
    /// knows what a player can DO about one criterion and already answers silence where it
    /// does not. This engine picks which of them are worth the evening and hands the rest
    /// through.</para>
    ///
    /// <para><b>The tick never moves.</b> An unlock is the game's own answer. Nothing here
    /// writes, and a recommendation about an unlock is additive to the Unlocks tab in exactly
    /// the way its own guided rows are.</para>
    /// </summary>
    private static void Unlocks(
        HelperInputs inputs, HelperGoal goal, IReadOnlyList<UnlockProgress> unlocks,
        List<Recommendation> into, List<GoalGap> gaps)
    {
        if (!inputs.HasAchievements)
        {
            gaps.Add(new GoalGap(goal, GoalGapReason.NoAchievementsDump));
            return;
        }

        // Incomplete, with actual work in it. An unlock whose only rows are Derived has a
        // null Score and nothing to recommend — the Unlocks tab says so in a sentence, and
        // repeating that here would be a recommendation to do nothing.
        var open = unlocks
            .Where(u => !u.Complete && u.Score is { Total: > 0 })
            .OrderByDescending(u => u.Score!.Value.Done / (double)u.Score!.Value.Total)
            .ThenBy(u => u.Subject, StringComparer.OrdinalIgnoreCase)
            .Take(PerEngineCandidates)
            .ToList();

        if (open.Count == 0)
        {
            gaps.Add(new GoalGap(goal, GoalGapReason.NothingLeftToDo));
            return;
        }

        foreach (var u in open)
        {
            var score = u.Score!.Value;
            var why = new List<WhyFact> { new UnlockScoreFact(u.Subject, score.Done, score.Total) };
            var doors = new List<HelperDoor> { new(HelperDoorKind.Unlocks, u.Subject) };
            var zone = "";
            var withheld = 0;

            foreach (var criterion in u.Actionable.Where(c => !c.Done || u.Inherited))
            {
                var row = UnlockGuidance.Resolve(
                    u, criterion, inputs.Factions, inputs.Pool,
                    inputs.SkyItems, inputs.SkyCompleted, inputs.Catalog);

                // The join: the first criterion that names a place gives this unlock one.
                if (zone.Length == 0 && row.Zone.Length > 0) zone = row.Zone;

                foreach (var line in row.Lines)
                {
                    // "N of M pieces in hand" is tagged Personal because its subject is your
                    // bags — the catalog's contribution is the denominator, and the sentence
                    // never claims a rate. The one genuinely catalog-sourced claim in this
                    // engine is the quest match below.
                    if (why.Count < WhyCap * 2) why.Add(new WordedFact(line, Evidence.Personal));
                    else withheld++;
                }

                if (row.Door is { } door)
                {
                    doors.Add(Map(door));
                    if (door.Kind == UnlockDoorKind.GeneralTabQuest)
                        why.Add(new CatalogQuestFact(door.Target));
                }
            }

            into.Add(new Recommendation(
                zone.Length > 0 ? RecommendationKind.Zone : RecommendationKind.Unlock,
                zone.Length > 0 ? zone : u.Subject,
                zone,
                [goal], why, [.. Dedupe(doors)], withheld,
                Math.Clamp(score.Done / (double)score.Total, 0, 1)));
        }
    }

    /// <summary>An unlock row's door, in the Helper's own vocabulary. A mapping and never a
    /// second destination: the three kinds are the three <c>UnlockGuidance</c> already
    /// produces, and a fourth arriving there fails the switch rather than silently losing a
    /// door.</summary>
    private static HelperDoor Map(UnlockDoor door) => door.Kind switch
    {
        UnlockDoorKind.WikiFaction => new HelperDoor(HelperDoorKind.WikiFaction, door.Target),
        UnlockDoorKind.SkyTab => new HelperDoor(HelperDoorKind.SkyRewards, door.Target),
        UnlockDoorKind.GeneralTabQuest => new HelperDoor(HelperDoorKind.QuestCatalog, door.Target),
        _ => new HelperDoor(HelperDoorKind.Unlocks, door.Target),
    };
}

/// <summary>
/// The Helper's two per-character selections, read and written in one place.
///
/// <para><b>Writer and reader land in the same slice, on purpose</b> (trap 20): a setting
/// only READERS touch is the signature of a lost capability, and it has cost this repo three
/// player-facing bugs. Both halves are here so neither can be folded away without the other
/// going with it.</para>
///
/// <para>It writes through <see cref="AppSettings"/> itself rather than taking the
/// dictionary by reference — a helper that mutates a collection it was handed is invisible to
/// <c>DeadSettingTests</c>' scan, which is why that test's known-list has four entries whose
/// only sin is being written that way. One fewer.</para>
/// </summary>
public static class HelperGoalStore
{
    /// <summary>
    /// The goals this character has picked, or EMPTY when they have never picked any.
    ///
    /// <para>Empty is the "weigh all of them" state and not a mistake — see
    /// <see cref="AppSettings.HelperGoals"/>. An unknown stored name is skipped rather than
    /// throwing: a goal removed from the Founder's list should stop mattering, not break the
    /// room for whoever had ticked it.</para>
    /// </summary>
    public static IReadOnlyList<HelperGoal> Goals(AppSettings settings, string characterKey)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return [];
        if (!settings.HelperGoals.TryGetValue(characterKey, out var names)) return [];
        // Back through the enum's own order rather than the stored order, so the answers'
        // "serves" line reads the same way the chip strip does however the player clicked.
        return [.. Recommendations.All.Where(g =>
            names.Any(n => string.Equals(n, g.ToString(), StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>Turn one goal on or off. An empty result REMOVES the key rather than storing
    /// an empty list: "never picked" and "picked nothing" are the same state here — both mean
    /// weigh everything — and two spellings of one state is a distinction a later reader
    /// would eventually act on.</summary>
    public static void Toggle(AppSettings settings, string characterKey, HelperGoal goal)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return;
        var picked = Goals(settings, characterKey).ToList();
        if (!picked.Remove(goal)) picked.Add(goal);
        if (picked.Count == 0) settings.HelperGoals.Remove(characterKey);
        else settings.HelperGoals[characterKey] = [.. picked.Select(g => g.ToString())];
    }

    /// <summary>The factions this character is working on, in the dump's own spelling.</summary>
    public static IReadOnlyList<string> Factions(AppSettings settings, string characterKey) =>
        settings is not null && !string.IsNullOrEmpty(characterKey)
        && settings.HelperFactions.TryGetValue(characterKey, out var picked)
            ? picked
            : [];

    /// <summary>Toggle one faction. Same empty-key rule as <see cref="Toggle"/>, for the same
    /// reason.</summary>
    public static void ToggleFaction(AppSettings settings, string characterKey, string faction)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)
            || string.IsNullOrWhiteSpace(faction)) return;
        var picked = Factions(settings, characterKey).ToList();
        if (picked.RemoveAll(f => f.Equals(faction, StringComparison.OrdinalIgnoreCase)) == 0)
            picked.Add(faction);
        if (picked.Count == 0) settings.HelperFactions.Remove(characterKey);
        else settings.HelperFactions[characterKey] = picked;
    }
}
