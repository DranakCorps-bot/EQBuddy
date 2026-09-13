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

/// <summary>
/// How long your fights here actually take, and over how many kills.
/// </summary>
/// <param name="BaselineSeconds">How long your fights run across every zone EQBuddy has
/// measured, or 0 when there is nothing to compare against (DRA-71 D4, plan P7).
///
/// <para><b>The comparison is against YOURSELF, and it has to be.</b> "Is 95 seconds a long
/// fight?" has no answer in this repo — there is no mob-HP model and no con-colour model, so
/// nothing here knows what a creature should take. "Is 95 seconds long FOR THIS CHARACTER?"
/// is answerable from measurements already on disk, and it is the question the plan's
/// outcome-first reading asks. 0 draws no comparison clause at all rather than a clause
/// comparing a zone to itself (see <see cref="ThroughputBaseline.Known"/>).</para></param>
public sealed record ZoneCadenceFact(
    string Zone, double AvgFightSeconds, int Kills, double BaselineSeconds = 0)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **What this character actually PUT OUT here, against what they put out everywhere else**
/// (DRA-71 D4, plan P7; Founder smoke item 3 — *"DPS/Healing vs mob difficulty"*).
///
/// <para><b>There is no difficulty number in it, because the game gives none.</b> EQBuddy
/// has no mob-HP model and no con-colour model; the only difficulty scale the game's own data
/// states is the instance tier, which has its own fact (<see cref="ZoneTierFact"/>). So the
/// answer to "was my output good for what I was fighting" is assembled from measurements
/// rather than from a score: your damage and healing per second of combat here, your own
/// pooled figure across every zone, and — in the cadence fact beside it — how long the
/// fights ran. A player who reads all three can tell whether a camp suited their character.
/// EQBuddy does not tell them, because it would have to invent the rule it decided
/// with.</para>
///
/// <para><b>Nothing here is anybody else's number.</b> Damage and healing are the
/// self-measured values the log has always carried; <c>DamageByAttacker</c> and
/// <c>HealsByHealer</c> are who hit or healed YOU. There is no cohort, no comparison and no
/// ranking against another player, and this slice adds none.</para>
/// </summary>
/// <param name="Dps">Your damage per combat second here.</param>
/// <param name="Hps">Your healing per combat second here. Drawn only when it is above zero —
/// a clause reading "and 0.0 healing a second" is furniture on every character who does not
/// heal.</param>
/// <param name="Output">The two together, which is the figure the WEIGHT reads — see
/// <see cref="ZoneRoll.OutputPerSecond"/> for why a measure that can only see damage answers
/// the question for exactly one kind of character.</param>
/// <param name="BaselineOutput">The same figure pooled across every zone, or 0 when there is
/// nothing to compare against.</param>
/// <param name="CombatSeconds">The denominator. Named in the sentence, because a rate whose
/// scope a player cannot see is a claim nobody can argue with.</param>
/// <param name="Zones">How many zones the baseline rests on — the comparison's own scope.</param>
public sealed record ZoneThroughputFact(
    string Zone, double Dps, double Hps, double Output, double BaselineOutput,
    double CombatSeconds, int Zones)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **How much of your time here had nothing happening in it** (DRA-71 D4, plan P7).
///
/// <para>The gap between a session's elapsed seconds and its ACTIVE seconds — two-minute
/// buckets that contained a meaningful event. It is a measurement and the sentence says only
/// what was measured: <b>nothing here knows WHY</b>. Medding, running back from a bind point,
/// a bank trip and waiting on a spawn are indistinguishable to it, so no surface built on it
/// may name a cause (trap 73 — silence beats a template with a guess in it), and it must
/// never be worded as a claim about what a place is like (HOME-006).</para>
/// </summary>
/// <param name="Share">0..1 of elapsed time that was not active.</param>
/// <param name="Sessions">Across how many of your stored sessions — the scope, on the same
/// "sessions here" wording the rate uses, because the attribution is by primary zone.</param>
/// <param name="Hours">The elapsed hours it rests on.</param>
public sealed record ZoneDowntimeFact(string Zone, double Share, int Sessions, double Hours)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **The instance tier your OWN zone line recorded** (DRA-71 D4, plan P7; the observation
/// P10's mote engine will later prefer on).
///
/// <para><b>The one difficulty datum the game actually states</b>, and it is
/// <see cref="Evidence.Personal"/> rather than catalog because it is not a catalog: the
/// tier is decoded from the "You have entered X." line this character's own log printed and
/// this character's own session stored as its primary zone (<see cref="ZoneRoll.ObservedTier"/>).
/// Nothing was looked up.</para>
///
/// <para><b>It is evidence and, in D4, weighs nothing.</b> The plan's tier PREFERENCE belongs
/// to the mote engine in its own slice; a ranking rule added here would be this slice
/// deciding something nobody has signed. What it does is let the row say which instance the
/// numbers above it were measured in — without which a D0 rate and a D4 rate read as one
/// place.</para>
/// </summary>
/// <param name="Tier">0..4. A zone whose adjective this build does not recognise is
/// unmistakably an instance but has no tier, and gets no fact at all rather than a guessed
/// D0 — the refusal <c>InstanceTier</c> itself makes.</param>
public sealed record ZoneTierFact(string Zone, int Tier) : WhyFact(Evidence.Personal);

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

/// <summary>
/// **The evidence here was earned at a level you have left behind** (DRA-71 D3, plan P6;
/// Founder smoke item 2).
///
/// <para>A rate is a fact about a past sitting, and the character who farmed it is not
/// always the character reading the recommendation. This fact is emitted only when the
/// player has actually conned creatures here and the TOP of that band is
/// <see cref="Recommendations.OutgrownBy"/> or more levels under their resolved level.</para>
///
/// <para><b>It is a report and not a prediction.</b> There is no XP curve behind it — this
/// repo has none, the wiki gives none, and inventing one would be trap 73's shape with
/// arithmetic instead of prose. What it says is the two numbers it measured: what conned,
/// and what you are. The WEIGHT that goes with it is a named judgement (see
/// <see cref="Recommendations.OutgrownWeight"/>), not a derived quantity.</para>
///
/// <para>HOME-006 is untouched: it makes no claim about danger in either direction, and
/// the word for the opposite of "outgrown" is never printed at all.</para>
/// </summary>
/// <param name="Kills">How many kills the conned creatures account for — the band's
/// denominator, so the sentence rests on something the player can argue with.</param>
public sealed record ZoneOutgrownFact(
    string Zone, int ConnedMin, int ConnedMax, int Level, int Kills)
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
    /// <summary>The Character room — who EQBuddy is following, and since DRA-71 D3 the one
    /// place a player can tell it what level they are. The Helper's door for the one input
    /// it cannot read from anything.</summary>
    Character,
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
/// <param name="Level">
/// The character's resolved level — <see cref="CharacterLevel.Resolve"/>'s own answer,
/// never re-derived here (DRA-71 D3, plan P5; the Founder's MUST).
///
/// <para><b><see cref="ResolvedLevel.Unknown"/> is a real input and not a hole.</b> A
/// profile that has never seen a ding and whose player has not said anything still gets
/// every answer its own play supports; what stops is the part that needs a number. The
/// engines' contract for that state is written once, in <see cref="Recommendations.Rank"/>:
/// personal-evidence ranking runs unchanged, and anything gated on a level draws nothing
/// rather than guessing one (trap 73).</para>
/// </param>
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
    QuestCatalog? Catalog,
    ResolvedLevel Level = default)
{
    public static readonly HelperInputs Nothing =
        new([], [], null, [], [], [], false, [], [], null, ResolvedLevel.Unknown);
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
/// could be worded as "this camp is safe". The survival-adjacent shapes are
/// <see cref="ZoneDeathsFact"/>, which exists only where the player has actually died, and
/// <see cref="ZoneDowntimeFact"/>, which reports a share of elapsed time and names no cause
/// for it; both are counts with their scope and neither carries an adjective. The vocabulary
/// guard lives on <c>HelperPresentation</c>, which is where the words are.</para>
///
/// <para><b>"Versus difficulty" is answered without a difficulty model, because there is
/// none</b> (DRA-71 D4, plan P7). The game states exactly one difficulty scale — the
/// instance tier on its own zone line — and this file reports it
/// (<see cref="ZoneTierFact"/>) rather than ranking on it. Everything else about "was this
/// camp a match for my character" is assembled from outcomes the log already measured:
/// output per combat second, fight length, deaths, downtime, each against this character's
/// OWN pooled figures. No mob-HP model is invented, no con-colour scale is invented, and no
/// number comes off anybody else's screen.</para>
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

    /// <summary>
    /// How many why-lines one recommendation draws before it says it is holding some back.
    ///
    /// <para><b>Four until DRA-71 D4, and SIX after it — a raise this slice was forced
    /// into.</b> The original reasoning stands: a headline plus a long list stops being a
    /// recommendation and becomes a report. But D4 gives the zone engine four new things to
    /// measure, and at four the cap was silently trimming the P6 outgrown sentence the
    /// PREVIOUS slice shipped — a zone marked down twice, drawing the explanation for one of
    /// them. Trimming a caveat to make room for a number is the worst way for a cap to
    /// behave, and it happens without any assertion in the repo noticing.</para>
    ///
    /// <para>Six is what a fully loaded zone row needs to keep every discount that FIRED
    /// beside its own evidence: the rate, the throughput, the cadence, the deaths, the
    /// downtime, and the outgrown band. The seventh — the instance tier, which weighs nothing
    /// — is emitted last precisely so it is the one the cap takes, and the row says so out
    /// loud (<c>HelperPresentation.WithheldWhy</c>). The density of six short personal
    /// sentences is a product question and a <c>BEVEL.md</c> stub asks it against this
    /// slice's shots; the number is logged in <c>DECISIONS.md</c> for veto.</para>
    /// </summary>
    public const int WhyCap = 6;

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

    // ---- the level must-list (DRA-71 D3, plan P5) ---------------------------------------

    /// <summary>Whether an engine weighs the character's resolved level. Both values are
    /// DECISIONS — see <see cref="LevelUseFor"/>, which answers null for neither.</summary>
    public enum LevelUse
    {
        /// <summary>The engine reads <see cref="HelperInputs.Level"/> and its answer can
        /// change because of it. <c>HelperMustListTests</c> proves that by running the same
        /// fixture at two levels and requiring the answers to DIFFER — a claim that was only
        /// a table row is trap 34's own shape one level up.</summary>
        Consumes,

        /// <summary>Decided, and the decision is that level does not belong in this
        /// engine's arithmetic. It owes a reason (<see cref="LevelExemptReason"/>), and the
        /// same test requires its answer to be IDENTICAL at two levels — so an exemption
        /// that stops being true fails rather than going quietly stale.</summary>
        Exempt,
    }

    /// <summary>
    /// **THE FOUNDER'S MUST, AS A TABLE THAT CANNOT GO SILENT** (smoke item 2: *"recs MUST
    /// factor it"*).
    ///
    /// <para>One row per ENGINE, and an engine is an <see cref="HelperGoalShape.Answered"/>
    /// goal — so a slice that answers a fifth goal has to decide about level in the same diff
    /// that adds it. Null means nobody decided, which is the only thing a pairing like this
    /// can catch: "level does not apply here" and "nobody thought about level here" look
    /// identical on screen, and the second one is how a MUST quietly becomes a maybe.</para>
    ///
    /// <para>Null for a <see cref="HelperGoalShape.Deferred"/> goal is the RIGHT answer and
    /// not a gap: there is no engine to decide about yet, and pre-deciding for one that does
    /// not exist would be a ruling nobody could check. <c>HelperMustListTests</c> asserts the
    /// two tables agree in both directions.</para>
    ///
    /// <para><b>Three of the four are exempt in D3, and that is a default logged for veto
    /// rather than an oversight.</b> The discount P6 builds is about a zone's THROUGHPUT —
    /// what your own kills there were worth — and only <see cref="HelperGoal.LevelUp"/> makes
    /// that claim. For the other three the zone is a POINTER: a faction only moves where its
    /// own creatures are, and an unlock criterion names a specific mob, quest or standing.
    /// Down-weighting those for being low-level would be EQBuddy recommending against the
    /// goal the player just picked. The slices that add throughput goals (Farm Gear, Farm
    /// Motes, Make Money) each own their row here when they land.</para>
    /// </summary>
    public static LevelUse? LevelUseFor(HelperGoal goal) => goal switch
    {
        HelperGoal.LevelUp => LevelUse.Consumes,
        HelperGoal.WorkOnFaction => LevelUse.Exempt,
        HelperGoal.UnlockClasses => LevelUse.Exempt,
        HelperGoal.UnlockRaces => LevelUse.Exempt,
        _ => null,
    };

    /// <summary>Why an exempt engine is exempt. Empty for one that CONSUMES, and empty for a
    /// goal with no engine — the test reads the pairing, so a reason that appeared beside a
    /// consuming engine would be as wrong as one that went missing.</summary>
    public static string LevelExemptReason(HelperGoal goal) => goal switch
    {
        HelperGoal.WorkOnFaction =>
            "A faction only moves where its own creatures are. A zone you have outgrown is "
            + "still the only place that standing changes, so discounting it would be "
            + "recommending against the goal the player picked.",
        HelperGoal.UnlockClasses or HelperGoal.UnlockRaces =>
            "An unlock criterion names a specific creature, quest or standing, and the zone "
            + "is where that thing IS rather than a rate this character could beat somewhere "
            + "else. The game decides when an unlock is done; level is not one of its terms.",
        _ => "",
    };

    /// <summary>
    /// How far under your level a zone's TOP conned creature must sit before an answer says
    /// you have outgrown it.
    ///
    /// <para>Ten, and <b>the number is a judgement rather than a measurement</b> — the same
    /// admission <see cref="ZoneHistory.MinHours"/> makes about its fifteen minutes. This
    /// repo has no XP curve, eqlwiki publishes none, and deriving one from con colours would
    /// be asserting a game rule nobody here can verify (the Founder's own ceiling is level
    /// 29). Ten levels is the distance at which a band stops overlapping anything a player
    /// would still be fighting.</para>
    ///
    /// <para>It reads the band's TOP and not its middle on purpose: if anything in the zone
    /// still cons near you, you have outgrown PART of a zone, which is not a thing a
    /// recommendation should act on.</para>
    /// </summary>
    public const int OutgrownBy = 10;

    /// <summary>
    /// What an outgrown zone's weight is multiplied by.
    ///
    /// <para><b>A halving, and never a removal.</b> The zone stays in the list, keeps its
    /// measured rate and gains a sentence saying what was measured there — because the
    /// player may have a reason to go back that EQBuddy does not know, and a recommender
    /// that deleted their own best-measured camp would be overruling evidence with a
    /// judgement. <see cref="Recommendation.Weight"/> is a tie-break inside a kind, so this
    /// re-orders and never filters.</para>
    /// </summary>
    public const double OutgrownWeight = 0.5;

    // ---- throughput: outcome evidence, as weights with sentences (DRA-71 D4, plan P7) ----

    /// <summary>
    /// **WHY THESE ARE DISCOUNTS AND NOT SCORES**, read once for all four constants below.
    ///
    /// <para>The plan's P7 asks the ranking to consume fight length, deaths, downtime and
    /// throughput. Every one of them is spent the same way: a NAMED threshold, a NAMED
    /// multiplier under 1, and a sentence saying what was measured. Three properties follow
    /// from that shape and all three are deliberate.</para>
    ///
    /// <para><b>They can only ever push a zone DOWN.</b> There is no bonus arm, so nothing
    /// here can promote a camp the player's experience rate did not already earn — the rate
    /// stays the primary term and these re-order inside it. <see cref="Recommendation.Weight"/>
    /// is a tie-break within a kind and never a filter, so a discounted zone keeps its place
    /// in the list and keeps its measured numbers.</para>
    ///
    /// <para><b>Each one fires only above a threshold, and says so when it does.</b> A
    /// continuous curve over four inputs would produce a number nobody could explain and no
    /// test could pin; a threshold is a judgement somebody can disagree with, which is the
    /// same admission <see cref="OutgrownBy"/> and <see cref="ZoneHistory.MinHours"/> make
    /// about theirs. None of them is derived from a game rule, because this repo has no
    /// XP curve, no mob-HP model and no con-colour model to derive one from.</para>
    ///
    /// <para><b>And none of them is an adjective.</b> P7's own words: throughput versus
    /// difficulty is *"never an adjective"*. The discount moves an order; the sentence beside
    /// it reports two measurements; nothing calls a place safe, easy or hard in either
    /// direction (HOME-006, swept in <c>HelperPresentationTests</c>).</para>
    ///
    /// <para><b>The double-count question, answered out loud</b>, because it is the first
    /// objection anybody should raise: experience per hour ALREADY prices cadence, deaths and
    /// downtime in, in aggregate — a camp where fights drag and you die pays less per hour
    /// and sorts lower for it. These weights are not a second helping of that. They are about
    /// whether the rate is a rate this character can repeat: a zone that paid well while you
    /// spent half the sitting recovering is a zone whose number rests on an evening that went
    /// a particular way, and the honest thing is to rank it a little under the camp that paid
    /// the same with none of that. The default worth vetoing is exactly this reading, and it
    /// is logged in <c>DECISIONS.md</c> as such.</para>
    /// </summary>
    public const double DeathsPerHourCost = 1.0;

    /// <summary>What a zone at or above <see cref="DeathsPerHourCost"/> is multiplied by. The
    /// why-line is <see cref="ZoneDeathsFact"/>, which is already the only survival-adjacent
    /// sentence the Helper has — a count with its scope and no adjective.</summary>
    public const double DeathsCostWeight = 0.8;

    /// <summary>The share of elapsed time with nothing happening in it at or above which the
    /// weight prices downtime in. Half, which is the point where the hours a rate was divided
    /// by stop describing the fighting they are attributed to.</summary>
    public const double DowntimeShareCost = 0.5;

    /// <summary>What a zone at or above <see cref="DowntimeShareCost"/> is multiplied by.</summary>
    public const double DowntimeCostWeight = 0.8;

    /// <summary>
    /// How many times your own average fight length a zone's fights must run before the
    /// weight prices the cadence in. Half again, and <b>relative to this character rather
    /// than to a number of seconds</b>: "is 95 seconds a long fight" has no answer in this
    /// repo, and "is 95 seconds long for the character who averages 41" does.
    /// </summary>
    public const double SlowFightRatio = 1.5;

    /// <summary>What a zone at or above <see cref="SlowFightRatio"/> of your own mean fight
    /// length is multiplied by. The why-line is <see cref="ZoneCadenceFact"/>, which gains
    /// the baseline clause in the same slice so the discount and its evidence arrive
    /// together.</summary>
    public const double SlowFightWeight = 0.8;

    /// <summary>
    /// The share of your own pooled damage-and-healing per combat second below which a zone's
    /// throughput counts as a shortfall. Three fifths — far enough under that a normal spread
    /// between camps does not trip it.
    ///
    /// <para>It reads <see cref="ZoneRoll.OutputPerSecond"/> and not dps alone, because a
    /// cleric's contribution is healing and a damage-only measure would discount every zone a
    /// healer did their job in.</para>
    /// </summary>
    public const double ThroughputShortfall = 0.6;

    /// <summary>What a zone under <see cref="ThroughputShortfall"/> of your own baseline is
    /// multiplied by. The why-line is <see cref="ZoneThroughputFact"/>, which reports both
    /// halves and the baseline's scope.</summary>
    public const double ThroughputShortfallWeight = 0.8;

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
    ///
    /// <para><b>THE ONE ENGINE THAT CONSUMES LEVEL IN D3</b> (plan P6;
    /// <see cref="LevelUseFor"/>). A rate is a fact about a past sitting, and the character
    /// reading it is not always the one who earned it. Where the player has conned creatures
    /// here and that band's top is <see cref="OutgrownBy"/> under their resolved level, the
    /// row keeps its measured rate, gains a sentence saying what it was measured against, and
    /// is weighted at <see cref="OutgrownWeight"/>. With no resolved level NOTHING here
    /// changes — the ranking still runs on the player's own evidence, which is the half of
    /// the unknown-level contract that matters (a recommender that fell silent because it did
    /// not know a number would be worse than one that never asked).</para>
    ///
    /// <para><b>AND THE ENGINE THAT PRICES OUTCOMES</b> (DRA-71 D4, plan P7). Beside the rate
    /// it now reports what this character actually put out here, how the fights compared with
    /// their own average, how much of the time had nothing happening in it, and which instance
    /// tier their own zone line recorded — and it weighs the first three (see
    /// <see cref="ThroughputCost"/>). Every one of those is a measurement of this player and
    /// nobody else, and none of them is drawn as an adjective.</para>
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
        // ONE producer for the yardstick, folded once for the whole engine rather than per
        // candidate: it is a property of the SET, and a per-row recomputation would be the
        // same sum computed six times with six chances to drift (trap 4's shape in a loop).
        // It is built from inputs.Zones and not from `zones` — every zone the fold measured
        // is part of what this character usually does, including the ones this engine will
        // not offer.
        var baseline = ZoneHistory.Baseline(inputs.Zones);

        foreach (var z in zones
                     .OrderByDescending(z => z.XpPerHour ?? 0)
                     .Take(PerEngineCandidates))
        {
            var why = new List<WhyFact>
            {
                new ZoneXpRateFact(z.Zone, z.XpPerHour ?? 0, z.Sessions, z.Hours),
            };

            // **The throughput line** (DRA-71 D4, plan P7). Drawn whenever the output was
            // measured, discount or no discount: smoke item 3 asked to SEE this, and a
            // number that only appears when EQBuddy is marking a zone down is a number a
            // player would learn to read as a verdict.
            if (z.OutputPerSecond is { } output)
                why.Add(new ZoneThroughputFact(
                    z.Zone, z.Dps ?? 0, z.Hps ?? 0, output,
                    baseline.Known ? baseline.OutputPerSecond : 0,
                    z.CombatSeconds, baseline.Known ? baseline.Zones : 0));

            // Unknown is not zero: a zone whose pooled creatures never recorded a fight
            // length says nothing about cadence rather than claiming instant kills. The
            // baseline rides along only when there is a second zone to have averaged with —
            // otherwise the clause would compare a zone against itself.
            if (z.AvgFightSeconds > 0)
                why.Add(new ZoneCadenceFact(z.Zone, z.AvgFightSeconds, z.Kills,
                    baseline.FightLengthKnown ? baseline.AvgFightSeconds : 0));

            // HOME-006: deaths are shown where they HAPPENED and silence otherwise. There is
            // deliberately no "and you have never died here" arm — see ZoneDeathsFact.
            if (z.Deaths > 0) why.Add(new ZoneDeathsFact(z.Zone, z.Deaths, z.Sessions));

            // Downtime, and only where there is enough of it to be a fact about the place
            // rather than about one evening. Below the threshold it is not reported at all:
            // "12% of your time here had nothing happening in it" is true of every camp and
            // would be furniture (trap 50's sibling — a line that never varies says nothing).
            var downtime = z.DowntimeShare;
            var downtimeCost = downtime >= DowntimeShareCost;
            if (downtimeCost)
                why.Add(new ZoneDowntimeFact(z.Zone, downtime ?? 0, z.Sessions, z.Hours));

            var outgrown = Outgrown(z, inputs.Level);
            if (outgrown is { } fact) why.Add(fact);

            // **LAST, AND THE ORDER IS THE DESIGN.** The tier the player's own zone line
            // recorded is the one fact on this row that weighs nothing (see ZoneTierFact), so
            // it is emitted after every fact that explains a discount — which makes it the
            // one WhyCap takes when a row is fully loaded, rather than a caveat. Tiers only:
            // an instance whose adjective this build does not know is not called D0.
            if (z.ObservedTier is >= 0 and <= 4)
                why.Add(new ZoneTierFact(z.Zone, z.ObservedTier));

            into.Add(new Recommendation(
                RecommendationKind.Zone, z.Zone, z.Zone,
                [HelperGoal.LevelUp], why,
                [new HelperDoor(HelperDoorKind.World, z.Zone)],
                0,
                (best > 0 ? Math.Clamp((z.XpPerHour ?? 0) / best, 0, 1) : 0)
                * (outgrown is null ? 1 : OutgrownWeight)
                * ThroughputCost(z, baseline, downtimeCost)));
        }
    }

    /// <summary>
    /// The four P7 discounts, multiplied together — <b>outcome evidence, priced</b> (DRA-71
    /// D4). 1.0 when none of them fires, which is every zone that was farmed without dying,
    /// without dragging and without a recovery for every pull.
    ///
    /// <para><b>Each arm is gated on its own measurement being PRESENT</b>, so a profile whose
    /// snapshots predate the throughput probe, or whose pool never recorded a fight length, is
    /// ranked exactly as it was before this slice rather than discounted for the gap in
    /// EQBuddy's own reading. An absent measurement is not a bad one — the same rule the
    /// conned band keeps (trap 73).</para>
    ///
    /// <para>They compound, and that is intended: a zone that is slow AND fatal AND spent
    /// half its hours recovering has three separate things measured about it, and collapsing
    /// them into the worst single one would throw away two of the three. The floor this can
    /// reach is <c>0.8⁴ ≈ 0.41</c>, and even multiplied by
    /// <see cref="OutgrownWeight"/> it re-orders rather than removes:
    /// <see cref="Recommendation.Weight"/> is a tie-break inside a kind, the zone keeps its
    /// place in the list, and every discount that fired has a sentence in the same row.</para>
    /// </summary>
    /// <param name="downtimeCost">Decided by the caller and passed in, because the same
    /// predicate decides whether the SENTENCE is drawn — a weight and a why-line that asked
    /// the question separately could answer it differently, which is a zone marked down in
    /// silence.</param>
    private static double ThroughputCost(ZoneRoll z, ThroughputBaseline baseline, bool downtimeCost)
    {
        var weight = 1.0;
        if (z.DeathsPerHour >= DeathsPerHourCost) weight *= DeathsCostWeight;
        if (downtimeCost) weight *= DowntimeCostWeight;
        if (baseline.FightLengthKnown && z.AvgFightSeconds > 0
            && z.AvgFightSeconds >= baseline.AvgFightSeconds * SlowFightRatio)
            weight *= SlowFightWeight;
        if (baseline.Known && z.OutputPerSecond is { } output
            && output < baseline.OutputPerSecond * ThroughputShortfall)
            weight *= ThroughputShortfallWeight;
        return weight;
    }

    /// <summary>
    /// Has this character left this zone's creatures behind? The fact when they have, null
    /// otherwise.
    ///
    /// <para><b>Three ways to answer null and each is a different silence.</b> No resolved
    /// level: EQBuddy has not been told and does not guess. No conned band: the player never
    /// looked at anything here, so there is nothing measured to compare — a zone is not
    /// outgrown because we failed to observe it. Band still within
    /// <see cref="OutgrownBy"/>: the honest answer is nothing at all, because the sentence
    /// for the opposite of outgrown would be a claim about how a place will treat you, which
    /// is HOME-006's own forbidden shape.</para>
    /// </summary>
    private static ZoneOutgrownFact? Outgrown(ZoneRoll z, ResolvedLevel level) =>
        level.Known && z.HasConnedBand && level.Level - z.ConnedMax >= OutgrownBy
            ? new ZoneOutgrownFact(z.Zone, z.ConnedMin, z.ConnedMax, level.Level, z.ConnedKills)
            : null;

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
