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

/// <summary>
/// **A CATALOG ITEM THAT BEATS SOMETHING YOU ARE WEARING** (DRA-71 D6, plan P8; Founder smoke
/// items 4a/4b).
///
/// <para><b>This is the fact the "never BiS" amendment is about, so read it with
/// <see cref="GearUpgrades"/>' summary.</b> The Gear Locker compares what is in your bags and
/// still does. This one compares a shipped catalog record against the item on your character
/// — a farmable upgrade — and it is <see cref="Evidence.Catalog"/> for exactly that reason:
/// the estimate label is appended by construction, so a line about an item EQBuddy has read
/// about can never read as a measurement of your play.</para>
///
/// <para><b>It always names what it beats.</b> An upgrade with no anchor would be a claim
/// about the game ("this is the best X"), which is the line the Locker's lock draws and this
/// slice does not cross. The anchor is in the record because it is in the sentence.</para>
/// </summary>
/// <param name="GainMetric">The biggest single number that improved, in the stats block's own
/// spelling ("AC", "HP", "STR"). From <see cref="ItemDominance.Gain"/> — the same table that
/// decided dominance, so the sentence cannot name a metric the comparison did not weigh.</param>
/// <param name="Who">The creatures the wiki named for THIS zone, in the page's own order, capped
/// at <see cref="Recommendations.GearMobsPerItem"/>.
///
/// <para><b>Plural since DRA-84 D4</b> (plan P3). It used to be one string, taken with
/// <c>FirstOrDefault</c> — so a page naming six creatures answered with one of them and nothing
/// said the other five existed. 3,830 of the 10,637 (item, zone) pairs in the shipped catalog
/// name more than one.</para>
///
/// <para>Empty only where the player's own kills answered instead — the fact beside this one
/// carries it, one producer per fact (trap 4). A pair that can answer from NEITHER source is not
/// a row at all any more: see <see cref="RecommendationSet.GearWhoWithheld"/>.</para></param>
/// <param name="WhoWithheld">How many more creatures the page named that the cap held back. Said
/// out loud rather than dropped (trap 50).</param>
public sealed record GearUpgradeFact(
    string Item, string Over, string Slot, string GainMetric, double GainBy,
    IReadOnlyList<string> Who, int WhoWithheld = 0)
    : WhyFact(Evidence.Catalog);

/// <summary>
/// **YOU HAVE ACTUALLY SEEN IT DROP** (DRA-71 D6).
///
/// <para>The catalog says an item comes from a zone; your own pooled kills say WHICH creature
/// gave it to you and how often. That is the <c>who · where</c> the guided rows are built on,
/// answered from the one source that cannot be stale — and it is why a zone you have farmed
/// outranks one you have only read about, through <see cref="Recommendation.HasPersonalEvidence"/>
/// rather than through a weight this slice invented.</para>
///
/// <para>The denominator is in the sentence, as every personal line's is: "2 of your 340
/// kills" is a fact somebody can argue with and "it drops sometimes" is not.</para>
/// </summary>
public sealed record GearDropSeenFact(string Item, string Mob, string Zone, int Drops, int Kills)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **WHAT THIS ZONE HAS ACTUALLY PAID YOU IN MOTES** (DRA-71 D7, plan P10; Founder smoke
/// item 5).
///
/// <para>Potency and not a count, because <see cref="Motes"/> has said since #154 that a
/// hundred Infinitesimal motes and a hundred Infinite motes are not the same hour. The count
/// rides beside it anyway: a player who is short of ONE more mote of any kind is counting
/// motes, and the two numbers together are what they can act on.</para>
///
/// <para><b>There is no catalog half of this fact and there is not going to be one until the
/// wiki gains one.</b> The shipped catalog's eleven mote records all carry a
/// <c>DropZones</c> — and every value is "Various Zones", "Unknown" or "D3+ Zones". A drop
/// zone nobody can travel to is not a drop zone, so the mote engine is personal-only and says
/// so (<c>MoteCatalogSurveyTests</c>).</para>
/// </summary>
/// <param name="VoidTouched">How many of <paramref name="Motes"/> carried no potency at all —
/// the raid-only mote whose worth is a whole item tier rather than a number of points. It is
/// named in the sentence precisely because the potency figure cannot see it.</param>
public sealed record ZoneMoteRateFact(
    string Zone, double PotencyPerHour, double MotesPerHour, int Motes, int VoidTouched,
    int Sessions, double Hours)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **WHO ACTUALLY GAVE THEM TO YOU** — the <c>who · where</c> half of a mote answer, measured
/// (DRA-71 D7).
///
/// <para>From the pool, which is keyed on the zone the kill happened in, so this cannot be
/// stale and cannot be a page's guess. The denominator is in the sentence, as every personal
/// line's is: "12 motes across your 340 kills of it" is a fact somebody can argue with.</para>
/// </summary>
public sealed record MoteSourceFact(string Mob, string Zone, int Motes, int Potency, int Kills)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **HOW OFTEN YOU KILL THINGS HERE** — the Founder's *"frequent kills"*, measured rather
/// than asserted (DRA-71 D7, plan P10).
///
/// <para>Drawn only where the cadence discount FIRED, for <see cref="ZoneDowntimeFact"/>'s
/// reason: a line that appears on every row tells a player nothing, and the primary rate is on
/// screen either way. The comparison is against this character's own pooled kill rate across
/// the zones that have paid them motes — the same "compared to WHAT? to yourself" answer D4
/// gave the throughput weight, because there is no cohort here and never will be.</para>
/// </summary>
public sealed record ZoneKillRateFact(
    string Zone, double KillsPerHour, double BaselineKillsPerHour, int Kills, int Zones)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **THE INSTANCE TIER, WEIGHED** (DRA-71 D7, plan P10) — the one thing this slice does that
/// <see cref="ZoneTierFact"/> deliberately did not.
///
/// <para>D4 REPORTED the tier and weighed it at nothing, saying the preference belonged to the
/// mote engine's own slice. This is that slice, and this is that fact: a zone whose own line
/// recorded tier 0 or 1 is marked down against the 2–4 the Founder named. <b>The assumption is
/// logged for veto</b> — that his "difficulty 2–4" means the game's instance tiers — and the
/// catalog corroborates it in one place: the bare "Mote of Potential" lists its drop zones as
/// <i>"D3+ Zones"</i>, which is the wiki tying mote quality to instance tier in its own words.
/// One string is not a model, so nothing here derives a per-tier mote value from it.</para>
///
/// <para><b>A zone with NO tier observed is untouched</b>, and that is the clause that keeps
/// this honest. Open world is most of the game and most of what a low-level character can
/// reach; marking it down would be EQBuddy telling a player their whole evening is wrong, on
/// the strength of a comparison nobody here can make ("is an open-world camp better or worse
/// than a D3 for motes" has no answer in this repo). So the preference applies only BETWEEN
/// instances the player's own zone lines recorded.</para>
/// </summary>
public sealed record ZoneTierPreferenceFact(string Zone, int Tier, int PreferredMin, int PreferredMax)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **WHAT THIS ZONE HAS ACTUALLY PAID YOU IN COIN** (DRA-71 D7, plan P9; Founder smoke
/// item 4c).
///
/// <para>The same division, the same floor and the same scope wording as
/// <see cref="ZoneXpRateFact"/> beside it — <see cref="ZoneRoll.CopperPerHour"/> has been
/// written and documented as "read by the Make Money engine, which is a later slice" since
/// D1. This is that slice.</para>
/// </summary>
public sealed record ZoneCoinRateFact(string Zone, double CopperPerHour, int Sessions, double Hours)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **SOMETHING YOU LOOT HERE THAT YOU HAVE ACTUALLY SOLD** (DRA-71 D7, plan P9).
///
/// <para><b>Both halves are measurements of this player, which is the whole reason the slice
/// ended up here.</b> The drop count and its denominator come from the pool; the price comes
/// from what a vendor actually paid THIS character, pooled from their own stored sessions
/// (<see cref="SaleHistory"/>). The plan asked for the catalog's price to be weighed instead —
/// the survey of the cached pages is why it is not: eqlwiki states its vendor value at a
/// Charisma and a faction standing that differ per page, so it is a quote somebody was given
/// rather than a property of the object.</para>
/// </summary>
/// <param name="CopperEach">What one of them has fetched you, on average.</param>
public sealed record SellableDropFact(
    string Item, string Mob, string Zone, int Drops, int Kills, long CopperEach)
    : WhyFact(Evidence.Personal);

/// <summary>
/// **WHAT THE WIKI SAYS A VENDOR PAYS** — the catalog fallback, for an item you have never
/// sold (DRA-71 D7, plan P9).
///
/// <para><see cref="Evidence.Catalog"/>, so HOME-004's estimate label arrives by construction.
/// <b>And it carries the page's own condition as a required field</b>, because the survey found
/// the number is not a property of the item: 262 of the 975 cached pages that state a value
/// head it "VALUE TO VENDOR with CHA : 80 and faction at Indifferently", at a Charisma that
/// differs per page. A price quoted at somebody else's Charisma printed without that clause is
/// a measurement of nothing wearing the clothes of a fact.</para>
///
/// <para><b>It never weighs anything.</b> The ranking reads what YOU were paid; this names an
/// item, so that a zone you have farmed without ever visiting a merchant is not silent.</para>
/// </summary>
/// <param name="Condition">The page's own sentence, verbatim, or "" where it stated none.</param>
public sealed record CatalogValueFact(string Item, long Copper, string Condition)
    : WhyFact(Evidence.Catalog);

/// <summary>
/// **AN INGREDIENT ONE OF YOUR PROFESSIONS NEEDS, AND WHAT DROPS IT HERE** (DRA-149 D3,
/// plan P4; the Founder's FAIL item 3a — *"zones/creatures where gems drop more commonly"*).
///
/// <para><see cref="Evidence.Catalog"/>, because every word of it is something EQBuddy READ:
/// the profession heading, the recipe under it and the creature list are all the item page's
/// own. What the player has actually seen drop here rides beside it as a separate
/// <see cref="GearDropSeenFact"/>, exactly as the gear rows do, and the two are never both
/// drawn for one material — the who precedence is decided at the door (trap 4).</para>
///
/// <para><b><paramref name="Recipe"/> is the page's own line and is what makes the row
/// checkable.</b> "Amber is a Jewelcrafting material" is a claim; "the page lists Amber under
/// Jewelcrafting, for <i>Golden Amber Earring (Trivial: 102)</i>" is the evidence for it, and a
/// player who disagrees knows which page to open. Empty where the heading carried no recipe
/// line under it, which draws nothing rather than a guess (trap 73).</para>
/// </summary>
/// <param name="Who">The creatures the page named here, capped at
/// <see cref="Recommendations.GearMobsPerItem"/> — the same cap and the same list the gear
/// rows use.</param>
/// <param name="WhoWithheld">How many more the page named. Said out loud (trap 50).</param>
public sealed record TradeskillMaterialFact(
    Tradeskill Skill, string Item, string Recipe, int OtherRecipes,
    IReadOnlyList<string> Who, int WhoWithheld = 0)
    : WhyFact(Evidence.Catalog);

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

    /// <summary>
    /// Settings → Alerts → Watch rules, where every watch a player has written lives
    /// (DRA-71 D8).
    ///
    /// <para><b>It is the one door in this enum with a SIDE EFFECT behind it</b> — the Helper
    /// adds the profession's skill-up rule on the way through when the player has none, which
    /// is why the room's control has two labels. The rule that makes that acceptable is the
    /// one this repo already keeps for in-game commands: a surface that names an action ships
    /// the action. "Turn on a skill-up alert" with no way to do it is the silent no-op wearing
    /// a sentence, and a door that lands the player in an empty rules list to type it
    /// themselves is the same defect with a walk attached.</para>
    /// </summary>
    WatchRules,

    /// <summary>The profession's own skill page on eqlwiki. Player-clicked, exactly like
    /// <see cref="WikiFaction"/>: EQBuddy fetches nothing, so the request policy toward the
    /// wiki is untouched.</summary>
    WikiSkill,

    /// <summary>
    /// An ITEM's page on eqlwiki, searched for by the name the game printed (DRA-149 D2).
    ///
    /// <para>The door under the unread-worn sentence, and the third player-clicked one:
    /// EQBuddy fetches nothing here either. It is a SEARCH rather than a built page title for
    /// <c>WikiLinks.Search</c>'s own reason — this door exists precisely because the name did
    /// not match a page, so a guessed URL would 404 by construction where a search finds the
    /// near-miss the player is looking for.</para>
    ///
    /// <para><b>This is the contribution shape, not a lookup</b> (CLAUDE.md: eqlwiki is the
    /// source and EQBuddy is the tool that helps it update). A player who finds the real page
    /// has found the row <see cref="ItemNameAliases"/> wants; one who finds no page has found
    /// something the wiki is missing. Both are answers EQBuddy cannot produce on its own.</para>
    /// </summary>
    WikiItem,

    /// <summary>
    /// A ZONE's own page on eqlwiki (DRA-149 D4).
    ///
    /// <para>The door under a merchant line, and the fourth player-clicked one: EQBuddy fetches
    /// nothing here either. It is the page the line was TRANSCRIBED from, which is the whole
    /// reason it belongs beside the sentence — the map key it came out of sits under a map
    /// image EQBuddy does not ship, so "where in the zone" is an answer only the page can
    /// give.</para>
    ///
    /// <para>It resolves through <c>WikiLinks.Page</c> rather than <c>WikiLinks.Search</c>: a
    /// zone title is not an item and must not go through the item-alias rule.</para>
    /// </summary>
    WikiZone,
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

    /// <summary>
    /// A quest, because the thing you are being pointed at is a hand-in rather than a camp
    /// (DRA-71 D6).
    ///
    /// <para>It exists so a gear answer whose source is a quest does not have to pretend to be
    /// a place. Quest-sourced upgrades only appear at all behind the include-quests toggle —
    /// "farm gear" and "run a quest chain" are different evenings, and the Founder asked for
    /// the toggle by name — and when they do, the row is grouped by the QUEST for the same
    /// reason a drop row is grouped by the zone: one quest handing out three of your upgrades
    /// is one thing to go and do.</para>
    /// </summary>
    Quest,
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

    /// <summary>No inventory dump has ever been read, so EQBuddy does not know what this
    /// character is wearing and has nothing to compare against (DRA-71 D6).</summary>
    NoInventoryDump,

    /// <summary>
    /// The dump is read, the sweep ran, and nothing in the shipped catalog beat what is worn.
    ///
    /// <para><b>The subject of that sentence is the CATALOG and never the game</b> — see
    /// <see cref="GearUpgrades"/>. "Nothing beats your helm" is a best-in-slot claim wearing a
    /// negative, and the one the Locker's own lock has refused since #104.</para>
    /// </summary>
    NoCatalogUpgrade,

    /// <summary>The player chose a gear intent whose engine is a later slice. Kept as a
    /// DECIDED shape even though all three intents answer since DRA-71 D7 — see
    /// <see cref="GearUpgrades.ShapeFor"/>, which still has to be able to say it, and
    /// <c>HelperMustListTests</c>, which asserts the sentence exists for whichever intent next
    /// arrives Deferred.</summary>
    GearIntentNotAnsweredYet,

    // ---- DRA-71 D7 ------------------------------------------------------------------

    /// <summary>
    /// There is enough stored play to divide, and no mote has ever dropped in it (DRA-71 D7).
    ///
    /// <para>A different state from <see cref="NoPlayHistory"/> and the difference matters: one
    /// is EQBuddy having nothing to read, the other is EQBuddy having read it and found no
    /// motes. The second has no command that fixes it and no catalog to fall back on — the
    /// shipped mote records name no real zone — so the sentence says what would fill it and
    /// nothing else.</para>
    /// </summary>
    NoMotesSeen,

    /// <summary>The stored sessions are there and none of them earned coin, so there is no
    /// rate to rank zones by (DRA-71 D7). Not a missing dump and not a missing session — a
    /// measured zero, said as one.</summary>
    NoCoinEarned,

    /// <summary>
    /// Nothing this character loots has a price EQBuddy can name (DRA-71 D7).
    ///
    /// <para>Two ways to be in it and the sentence covers both: you have never sold anything a
    /// vendor recorded, and the shipped catalog carries no vendor value for what you loot —
    /// which is every item today, because the copper the promoter now parses arrives with the
    /// next weekly refresh.</para>
    /// </summary>
    NoSellEvidence,

    // ---- DRA-84 D2 -------------------------------------------------------------------

    /// <summary>
    /// The sweep found upgrades and the band gate refused every zone they drop in (DRA-84 D2).
    ///
    /// <para><b>A distinct state from <see cref="NoCatalogUpgrade"/>, and calling it that would
    /// be a lie.</b> The catalog DOES carry something better than what this character wears —
    /// it drops in places whose creatures eqlwiki puts outside their level. The count and the
    /// bands ride <see cref="RecommendationSet.GearBandRefusals"/>; this reason is what stops a
    /// gate that emptied the list from reading as a room with nothing in it.</para>
    /// </summary>
    EveryZoneOutsideYourBand,

    // ---- DRA-84 D4 -------------------------------------------------------------------

    /// <summary>
    /// The sweep found upgrades, the band gate kept their zones, and not one of them could name
    /// a creature from either source (DRA-84 D4, plan P3).
    ///
    /// <para><b>A third distinct state, and the third one that <see cref="NoCatalogUpgrade"/>
    /// would misdescribe.</b> The catalog carries something better than what this character
    /// wears and eqlwiki's band says the place is in reach — what is missing is the WHO, which
    /// is the half the Founder failed the Rathe row for. 98.2% of the shipped catalog's
    /// wearable (item, zone) pairs name a creature, so this arm is rare by construction and
    /// KEEPS for the same reason <see cref="GearIntentNotAnsweredYet"/> does: a rule that
    /// emptied the list must be able to say it did.</para>
    /// </summary>
    NoUpgradeNamesACreature,

    // ---- DRA-149 D2 ------------------------------------------------------------------

    /// <summary>
    /// There IS an inventory dump, every worn row in it named something EQBuddy has never read
    /// about, and so there is nothing to anchor a sweep on (DRA-149 D2, plan P2).
    ///
    /// <para><b>A fourth distinct state, and <see cref="NoInventoryDump"/> would not merely
    /// misdescribe it — it would ask for the thing the player already did.</b> That sentence
    /// ends *"run the inventory command in game and this fills in"*, which for a character
    /// whose dump EQBuddy simply cannot read is a loop with no exit. What actually happened is
    /// named by <see cref="RecommendationSet.UnreadWorn"/>, item by item, beside this gap: the
    /// remedy is the wiki's spelling, not another dump.</para>
    /// </summary>
    NothingWornIsReadable,

    // ---- DRA-149 D3 ------------------------------------------------------------------

    /// <summary>
    /// The picked professions' ingredients are known and not one of them drops anywhere the
    /// catalog names a place for (DRA-149 D3, plan P4).
    ///
    /// <para><b>Fletching is the shipped exhibit and the reason this has its own sentence.</b>
    /// Its 33 materials carry ZERO drop zones between them — every one is bought, foraged or
    /// crafted — so a Fletcher who picks only that profession gets this and nothing else. It
    /// would be wrong to word that as "nothing to farm": there is plenty to farm, EQBuddy just
    /// has no page saying a creature drops it. The difference is exactly the
    /// <see cref="NoCatalogUpgrade"/> distinction one goal over — a statement about EQBuddy's
    /// own catalog rather than about the game.</para>
    /// </summary>
    NoMaterialDrops,

    /// <summary>
    /// The materials drop somewhere, and the band gate refused every one of those places
    /// (DRA-149 D3, plan P4).
    ///
    /// <para><see cref="EveryZoneOutsideYourBand"/>'s sibling, and separate for the same reason
    /// the gear one is separate from <see cref="NoCatalogUpgrade"/>: a gate that emptied the
    /// list must be able to say so in its own voice, or the room draws its whole-room empty
    /// state and tells the player EQBuddy has nothing stored. The two are not merged because
    /// their subjects differ — one is about upgrades to what you wear, this is about
    /// ingredients — and one sentence covering both would have to name neither.</para>
    /// </summary>
    EveryMaterialZoneOutsideYourBand,

    /// <summary>
    /// The materials drop in places this character can reach, and not one page names a creature
    /// in any of them (DRA-149 D3, plan P4).
    ///
    /// <para><see cref="NoUpgradeNamesACreature"/>'s sibling, and it carries the same rule for
    /// the same reason: a drop offer that cannot say what drops it is not an offer. Materials
    /// need it MORE than gear does, not less — 12 of the eight professions' (material, zone)
    /// pairs say only <c>"Various Zones"</c>, and unlike the gear side's junk those DO name
    /// creatures, so <see cref="TradeskillMaterials.IsPlace"/> refuses them before this rule
    /// ever sees them.</para>
    /// </summary>
    NoMaterialNamesACreature,
}

/// <summary>One selected goal that produced no recommendation, and why.</summary>
public sealed record GoalGap(HelperGoal Goal, GoalGapReason Reason);

/// <summary>Which arm of the Farm Gear band gate refused a zone. Two arms and no third —
/// see <see cref="Recommendations.OutgrownBy"/> and
/// <see cref="Recommendations.GearBandReachAbove"/> for why they carry different numbers.</summary>
public enum GearBandArm
{
    /// <summary>The band's TOP is <see cref="Recommendations.OutgrownBy"/> or more under the
    /// character's level. Never fires for an open-topped band — there is no top to read
    /// (<see cref="ZoneLevels.Band.Max"/>).</summary>
    TopUnder,

    /// <summary>The band's BOTTOM is <see cref="Recommendations.GearBandReachAbove"/> or more
    /// over the character's level.</summary>
    BottomOver,
}

/// <summary>
/// One zone the Farm Gear band gate refused, carrying everything its sentence quotes
/// (DRA-84 D2, plan P2).
///
/// <para><b>The wiki's own row travels with the refusal, not a paraphrase of it.</b> The
/// player is owed the two numbers and the source that produced them — *"eqlwiki lists its
/// creatures at 5–20 — you are 29"* — because a zone silently missing from a list is
/// indistinguishable from a zone that has nothing in it. <see cref="Verbatim"/> is the row as
/// the page printed it, so an open top reads as the "and above" it actually was.</para>
/// </summary>
/// <param name="Zone">The zone as the item catalog spelled it — what the row would have said.</param>
/// <param name="Min">The band's bottom.</param>
/// <param name="Max">The band's top, or null where the page stated none.</param>
/// <param name="Verbatim">The <c>Level of Monsters</c> row, word for word.</param>
/// <param name="Level">The resolved level the gate compared against, so the sentence can
/// name the number it used rather than leaving the reader to infer it.</param>
/// <param name="Arm">Which of the two rules fired.</param>
public sealed record GearBandRefusal(
    string Zone, int Min, int? Max, string Verbatim, int Level, GearBandArm Arm);

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
/// <param name="UnlockPicks">Which race and class unlocks the player chose to work on —
/// <see cref="UnlockPickStore.Picked"/>'s own answer, in the achievements dump's spelling.
/// <b>Empty means ALL of them</b>, unlike <paramref name="PickedFactions"/> beside it: the
/// unlock list is thirty rows rather than a dump's several hundred, and filter semantics are
/// what <see cref="AppSettings.UnlockPicks"/> carries the argument for. Narrowed PER SECTION
/// inside <see cref="Recommendations.Rank"/>, so picking a race never empties the class
/// half.</param>
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
    IReadOnlyList<string> UnlockPicks,
    bool HasAchievements,
    IReadOnlyList<SkyQuestChecklistItem> SkyItems,
    IReadOnlyCollection<string> SkyCompleted,
    QuestCatalog? Catalog,
    ResolvedLevel Level = default)
{
    public static readonly HelperInputs Nothing =
        new([], [], null, [], [], [], [], false, [], [], null, ResolvedLevel.Unknown);

    // ---- Farm Gear (DRA-71 D6, plan P8) -------------------------------------------------
    //
    // These five arrive as `init` properties rather than as five more positional parameters,
    // and the reason is the twelve above them: a positional record is a good shape for a
    // parameter object right up until the call sites are reading like a phone number. Every
    // existing caller — the room, the E2E fixtures, forty tests — is untouched by an `init`
    // property with a default, which is also what keeps a slice's diff about the slice.

    /// <summary>
    /// What this character is WEARING, one entry per (item, slot) —
    /// <see cref="GearUpgrades.WornFrom"/>'s own answer over the inventory dump.
    ///
    /// <para>Empty is a real state and not a hole: it is the difference between "you are
    /// wearing nothing EQBuddy can describe" and "EQBuddy has never been told", and the
    /// engine draws <see cref="GoalGapReason.NoInventoryDump"/> with the command that fixes
    /// it rather than an empty list.</para>
    /// </summary>
    public IReadOnlyList<WornItem> Worn { get; init; } = [];

    /// <summary>
    /// The worn rows EQBuddy could not read about, as the dump spells them —
    /// <see cref="WornSheet.Unread"/>, carried from the same fold that produced
    /// <see cref="Worn"/> (DRA-149 D2, plan P2).
    ///
    /// <para><b>It rides the inputs rather than being recomputed, because it is the other half
    /// of one answer.</b> The paragraph above says an empty <see cref="Worn"/> is a real state;
    /// what it could not say until now is WHICH state, and the difference is a player being
    /// asked to run a command they have already run. It is also what makes the Founder's
    /// missing bow visible: a row that vanished silently is indistinguishable from a slot with
    /// no upgrades, and no amount of gear machinery downstream can tell the player about an
    /// anchor that was never built.</para>
    /// </summary>
    public IReadOnlyList<string> UnreadWorn { get; init; } = [];

    /// <summary>The shipped item catalog. Null answers nothing rather than throwing — a
    /// fixture without one is a test, not an error.</summary>
    public ItemCatalog? Items { get; init; }

    /// <summary>This character's classes, as the item blocks spell them (PAL, RNG). Empty
    /// means unknown, and unknown filters NOTHING — hiding a real upgrade is worse than
    /// showing one the player will recognise as not theirs.</summary>
    public IReadOnlyList<string> MyClasses { get; init; } = [];

    /// <summary>Which of the Founder's three gear questions is being asked —
    /// <see cref="GearIntentStore.Intent"/>'s own answer. Single-select; see
    /// <see cref="AppSettings.HelperGearIntent"/> for why this one selection in this room is
    /// not a filter.</summary>
    public GearIntent GearIntent { get; init; } = GearUpgrades.DefaultIntent;

    /// <summary>Which worn items the player picked. <b>Empty means ALL of them</b> — filter
    /// semantics, like <see cref="UnlockPicks"/> above. Read only for
    /// <see cref="GearIntent.UpgradeWorn"/>.</summary>
    public IReadOnlyList<string> WornPicks { get; init; } = [];

    /// <summary>Whether quest-obtained items may be offered (the Founder's "± quests").</summary>
    public bool IncludeQuests { get; init; }

    /// <summary>
    /// What eqlwiki says the creatures in each zone are levelled at — <see cref="ZoneLevels"/>,
    /// read by the Farm Gear band gate (DRA-84 D2, plan P2).
    ///
    /// <para><b>Null is a real state and it stands the gate down</b>, in the same voice
    /// <see cref="Items"/> uses beside it: a fixture without bands is a test rather than an
    /// error, and an unanswered question gates nothing (trap 73). Production supplies it in
    /// exactly one place — <c>HelperSources.Gather</c>, which is the ONE assembly point both
    /// the room and the phone go through, so neither surface can be the one that forgot.</para>
    /// </summary>
    public ZoneLevels? Bands { get; init; }

    // ---- Farm Motes / Make Money (DRA-71 D7, plans P9 and P10) --------------------------

    /// <summary>What each zone has paid this character in motes —
    /// <see cref="MoteHistory.Fold"/>'s own answer. Empty is a real state: a character who has
    /// never looted a mote, and the engine says so rather than falling back on a catalog that
    /// names no real zone.</summary>
    public IReadOnlyList<MoteRoll> Motes { get; init; } = [];

    /// <summary>What a vendor has actually paid this character, per item —
    /// <see cref="SaleHistory.Fold"/>'s own answer. Empty means they have never sold anything
    /// EQBuddy saw, which is when the catalog's own estimate is allowed to speak.</summary>
    public IReadOnlyList<SaleRoll> Sales { get; init; } = [];

    // ---- Farm Materials (DRA-149 D3, plan P4) -------------------------------------------

    /// <summary>
    /// Which professions this character is raising — <see cref="TradeskillPickStore.Picked"/>'s
    /// own answer, in the curated enum's order.
    ///
    /// <para><b>Empty means ALL EIGHT</b> — filter semantics, like <see cref="UnlockPicks"/> and
    /// <see cref="WornPicks"/> and unlike <see cref="PickedFactions"/>. The list is eight rows
    /// rather than a dump's several hundred, and the professions block above these rows already
    /// says out loud that an empty pick shows everything.</para>
    ///
    /// <para>It was read by <c>HelperSources.Gather</c> and carried only to the picker until
    /// this slice; the engine is the second reader of the SAME store rather than a second
    /// producer of the pick (trap 4).</para>
    /// </summary>
    public IReadOnlyList<Tradeskill> Professions { get; init; } = [];
}

/// <summary>The whole answer for one set of chips.</summary>
/// <param name="Top">The ranked recommendations, capped.</param>
/// <param name="Withheld">How many candidates the cap held back. Said out loud when it is
/// not zero — <b>HOME-002 wants three strong answers, and trap 50 wants the cap to admit
/// it</b>, because the fourth-best camp is exactly the one somebody is looking for.</param>
/// <param name="NotAnsweredYet">Selected goals whose shape is <see cref="HelperGoalShape.Deferred"/>.</param>
/// <param name="Gaps">Selected, answerable goals that produced nothing, each with the reason.</param>
/// <param name="GearWithheld">
/// Upgrades the gear sweep's own per-anchor cap held back (DRA-71 D6).
///
/// <para><b>It is here because it is the one cap whose count cannot ride a row.</b> Every
/// other cap in this file trims something a row already exists for and reports it in
/// <see cref="Recommendation.WithheldWhy"/>; <see cref="GearUpgrades.MaxPerAnchor"/> stops
/// before there are rows at all, and a count attached to whichever row happened to be built
/// first would be a number pointing at the wrong thing. A surviving cap says so (trap
/// 50).</para>
/// </param>
/// <param name="GearBandRefusals">
/// The zones the Farm Gear band gate refused (DRA-84 D2, plan P2).
///
/// <para><b>A refusal is a cap with a rule instead of a number, and trap 50 applies to it the
/// same way.</b> This one cannot ride a row either — the row is precisely what did not get
/// built — and a zone that vanished without a sentence is worse than a capped list, because
/// the player cannot tell it from a zone the catalog has nothing in. The list rather than a
/// count, because the sentence quotes each band.</para>
/// </param>
/// <param name="GearWhoWithheld">
/// Drop offers held back because nothing could say what drops them (DRA-84 D4, plan P3).
///
/// <para><b>Its own count, beside <paramref name="GearWithheld"/> rather than folded into
/// it.</b> That one is the per-anchor cap — a number of things EQBuddy chose not to list from a
/// list it could have listed. This one is a RULE with a different cause: the offer exists, its
/// zone is in reach, and neither the page nor the player's own kills can say who carries it.
/// Two causes summed into one sentence is a sentence that cannot explain itself, which is the
/// opposite of what trap 50 asks a surviving cap to do.</para>
///
/// <para>Counted per (item, zone) OFFER and not per item: the same item is offered under every
/// zone it drops in, and a page that names creatures in one of two zones is answered in one
/// place and withheld in the other.</para>
/// </param>
/// <param name="UnreadWorn">
/// The worn rows EQBuddy could not read about, as the dump spells them (DRA-149 D2, plan P2).
///
/// <para><b>The fourth thing the gear block holds back, and the only one that is not a
/// decision.</b> The cap, the band gate and the who rule each chose to leave something out;
/// this one is EQBuddy admitting it never had the row at all. It reports the same way for the
/// same reason (trap 50): the Founder's bow disappeared between his bags and his screen, and
/// an absence nobody counts is indistinguishable from a slot with nothing better in it.</para>
///
/// <para>The NAMES rather than a count, because the remedy is per item — the player can check
/// the spelling on eqlwiki — and because a bare "1 item" is a sentence nobody can act on. The
/// cap on how many are named is the SURFACE's (<c>HelperPresentation.UnreadWornNamed</c>); this
/// list is whole, so the count in that sentence is the real one.</para>
/// </param>
public sealed record RecommendationSet(
    IReadOnlyList<Recommendation> Top,
    int Withheld,
    IReadOnlyList<HelperGoal> NotAnsweredYet,
    IReadOnlyList<GoalGap> Gaps,
    int GearWithheld = 0,
    IReadOnlyList<GearBandRefusal>? GearBandRefusals = null,
    int GearWhoWithheld = 0,
    IReadOnlyList<string>? UnreadWorn = null,
    IReadOnlyList<GearBandRefusal>? MaterialBandRefusals = null,
    int MaterialWhoWithheld = 0)
{
    /// <summary>Never null, so no caller has to decide what an absent list means.</summary>
    public IReadOnlyList<GearBandRefusal> GearBandRefusals { get; init; }
        = GearBandRefusals ?? [];

    /// <summary>Never null, for <see cref="GearBandRefusals"/>' reason.</summary>
    public IReadOnlyList<string> UnreadWorn { get; init; } = UnreadWorn ?? [];

    /// <summary>
    /// The zones the band gate refused on the MATERIALS list (DRA-149 D3, plan P4).
    ///
    /// <para><b>Its own list beside <see cref="GearBandRefusals"/> rather than folded into
    /// it.</b> Both are the same rule run over the same catalog, and that is exactly why they
    /// must not be summed: a player reading one sentence about "zones EQBuddy has upgrades for"
    /// that silently also counted the zones its gems drop in could not act on either half. The
    /// two engines run independently — one goal can be ticked without the other — so a merged
    /// list would also be a count of a list nobody asked for.</para>
    /// </summary>
    public IReadOnlyList<GearBandRefusal> MaterialBandRefusals { get; init; }
        = MaterialBandRefusals ?? [];

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
public static partial class Recommendations
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
        // DRA-71 D6. Two of the Founder's three gear intents are answered; the third — farm
        // to sell — is Deferred one level down (GearUpgrades.ShapeFor) and says so in the
        // room, which is why the GOAL is answered while one of its questions is not.
        HelperGoal.FarmGear => HelperGoalShape.Answered,
        // DRA-71 D7. Both engines are personal-evidence-only, and honestly so: the shipped
        // catalog names no real zone for a mote, and its vendor value is a price somebody was
        // quoted rather than a property of an item (see the two engines below).
        HelperGoal.FarmMotes => HelperGoalShape.Answered,
        HelperGoal.MakeMoney => HelperGoalShape.Answered,
        // **DRA-149 D3: this row flips, and what changed is a COLUMN rather than a decision.**
        // It was Deferred from DRA-71 D8 on a survey that counted `[[Category:…]]` tags — 14 of
        // 11,197 pages naming a profession, re-taken on the DRA-84 D3 refresh and still 14. The
        // `Recipes` field is the one carrying the answer: all eight professions appear in it as
        // headings, over 1,276 records. See FarmMaterials and TradeskillMaterials.
        HelperGoal.FarmMaterials => HelperGoalShape.Answered,
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
        // **DRA-84 D2: the Founder's veto lands, and this row flips.** It was Exempt from
        // DRA-71 D6 until 2026-09-14, on a survey of the ITEM side that still stands (see the
        // comment on GearBandGate for the numbers and why they are not what changed). What
        // changed is that there is now a ZONE-side level datum to read: D1 shipped eqlwiki's
        // own `Level of Monsters` band for 87 zones, so the engine can refuse a camp whose
        // creatures sit outside this character's level instead of ranking it. The Founder
        // failed the room for offering Crushbone to a 29.
        HelperGoal.FarmGear => LevelUse.Consumes,
        // **DRA-71 D7, and the two rows are deliberately opposite.** Motes CONSUME the level
        // because the Founder asked them to by name — *"highest-level zone"* is the first of
        // his three mote criteria. Money is EXEMPT because nothing he asked for needs it and
        // both readings of what it would do are wrong; see LevelExemptReason.
        HelperGoal.FarmMotes => LevelUse.Consumes,
        HelperGoal.MakeMoney => LevelUse.Exempt,
        // **DRA-149 D3: a camp is a camp.** This engine names places out of the same catalog
        // the gear one does, so a material whose only zone is Temple of Veeshan has to be
        // refused for a level 30 for the identical reason a helm there is — and it is the SAME
        // gate rather than a second one with the same numbers typed in. The band refusal is the
        // only thing here that reads the level; nothing about which ingredients a profession
        // needs depends on it.
        HelperGoal.FarmMaterials => LevelUse.Consumes,
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
        // **FarmGear's arm LEFT this table in DRA-84 D2** and deliberately left no stub behind:
        // the pairing above requires a consuming engine to carry no reason, so a row here would
        // now be the drift that pairing exists to catch. The D6 item-side survey it used to
        // hold is NOT retired — it is the live reason the gate reads a ZONE and not an item,
        // and it is quoted with its numbers on GearBandGate.

        // **DRA-71 D7.** The Founder's money ask is "farm valuable gear to sell" and "make
        // money", and neither names his level. What a level rule WOULD do here has two
        // readings and both are wrong, which is D6's own conclusion arrived at again on
        // different evidence. Coin is a property of the CREATURE — the pool stores CoinMin and
        // CoinMax per creature and they do not move when the killer levels — so an outgrown
        // camp pays the same per kill and is killed through faster, which makes it the BETTER
        // farm rather than the worse one; and inverting the discount into a bonus is the arm
        // D4 refused plus a game rule nobody here can verify. Neither is shipped, and the
        // measured coin-per-hour already contains whatever the truth is.
        HelperGoal.MakeMoney =>
            "Coin is a property of the creature rather than of the killer — the pooled "
            + "CoinMin and CoinMax for a creature do not move when you level — so a camp you "
            + "have outgrown pays the same per kill and is killed through faster. Marking it "
            + "down would recommend against the goal the player picked, and marking it up "
            + "would be a game rule nobody here can verify. The measured coin per hour "
            + "already contains whichever is true.",
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

    /// <summary>
    /// How far OVER the character's level a zone band's BOTTOM must sit before a Farm Gear row
    /// for that zone is refused (DRA-84 D2, plan P2).
    ///
    /// <para><b>Five, and like <see cref="OutgrownBy"/> beside it this is a judgement somebody
    /// can veto rather than a measurement.</b> This repo has no XP curve, no mob-HP model and
    /// no con-colour scale, and eqlwiki publishes none; the Founder's own ceiling is level 29.
    /// What the number encodes is one claim: a camp whose weakest creature is five or more
    /// levels above you is not a camp you can farm an item out of tonight.</para>
    ///
    /// <para><b>It is deliberately smaller than <see cref="OutgrownBy"/>'s ten, and the
    /// asymmetry is the point.</b> The two arms are not opposites. Being over a band's top
    /// costs time; being under its bottom costs the attempt, and the distance at which that
    /// happens is shorter. A single number used for both would have to be wrong in one
    /// direction.</para>
    ///
    /// <para>It reads the band's BOTTOM and not its middle for the mirror of
    /// <see cref="OutgrownBy"/>'s reason: if anything in the zone is within reach, the player
    /// has a camp there, and refusing the whole zone would hide it.</para>
    /// </summary>
    public const int GearBandReachAbove = 5;

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

    // ---- motes: the Founder's three criteria, priced (DRA-71 D7, plan P10) --------------

    /// <summary>
    /// The share of your own pooled kill rate below which a mote zone's CADENCE is priced in —
    /// the Founder's *"frequent kills"*.
    ///
    /// <para>Three fifths, the same figure and the same reasoning as
    /// <see cref="ThroughputShortfall"/>: far enough under that an ordinary spread between
    /// camps does not trip it. <b>It is against this character's own pooled rate and never a
    /// number of kills per hour</b>, because "is forty kills an hour frequent?" has no answer
    /// in this repo and "is it frequent for the character who averages a hundred?" does.</para>
    ///
    /// <para>The double-count objection is the same one D4 answered out loud, and so is the
    /// answer: potency-per-hour already has cadence inside it, and this weight is about whether
    /// a rate rests on kills the player can repeat. It is a discount and never a bonus, so a
    /// busy camp is not promoted above the potency it actually paid.</para>
    /// </summary>
    public const double SlowKillShare = 0.6;

    /// <summary>What a zone under <see cref="SlowKillShare"/> of your own pooled kill rate is
    /// multiplied by. Its why-line is <see cref="ZoneKillRateFact"/>, drawn only when this
    /// fires.</summary>
    public const double SlowKillWeight = 0.8;

    /// <summary>
    /// The bottom of the instance-tier band the Founder named — *"tier 2–4"*.
    ///
    /// <para><b>The assumption is logged for veto</b> (and lives in <c>DECISIONS.md</c>): his
    /// "difficulty 2–4" is read as the game's instance tiers D0–D4, which is the only 1–5
    /// difficulty datum the game's own data carries (<see cref="InstanceTier"/>). The shipped
    /// catalog corroborates the shape of it in one place — the bare "Mote of Potential" lists
    /// its drop zones as "D3+ Zones" — and one string is not a model, so nothing here derives a
    /// per-tier mote value from it.</para>
    /// </summary>
    public const int MotePreferredTierMin = 2;

    /// <summary>The top of that band.</summary>
    public const int MotePreferredTierMax = 4;

    /// <summary>
    /// What an instance OUTSIDE <see cref="MotePreferredTierMin"/>..<see cref="MotePreferredTierMax"/>
    /// is multiplied by.
    ///
    /// <para><b>A preference expressed as a discount, because D4 refused bonus arms and this
    /// slice does not reopen that.</b> A zone whose own line recorded tier 0 or 1 is marked
    /// down; a zone in the named band is left alone rather than promoted, so the measured
    /// potency rate stays the primary term.</para>
    ///
    /// <para><b>And a zone with NO tier observed is untouched</b> — the clause that keeps this
    /// from being a verdict on the whole open world. See
    /// <see cref="ZoneTierPreferenceFact"/>.</para>
    /// </summary>
    public const double OffPreferredTierWeight = 0.8;

    // ---- money: your own coin, and your own prices (DRA-71 D7, plan P9) ------------------

    /// <summary>
    /// How many sellable drops one money row names before it stops.
    ///
    /// <para>Two, which is one fewer than <see cref="GearNamedPerRow"/> because each of these
    /// costs a whole why-line and a money row already spends one on its coin rate. Three named
    /// items would push a loaded row past <see cref="WhyCap"/> and start trimming the discount
    /// sentences the zone was marked down for — the failure D4 raised the cap over, and the one
    /// worth not repeating.</para>
    /// </summary>
    public const int SellablesPerRow = 2;

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
        var gearWithheld = 0;
        var gearWhoWithheld = 0;
        List<GearBandRefusal> gearBandRefusals = [];
        IReadOnlyList<string> unreadWorn = [];
        List<GearBandRefusal> materialBandRefusals = [];
        var materialWhoWithheld = 0;

        if (goals.Contains(HelperGoal.LevelUp)) LevelUp(inputs, candidates, gaps);
        if (goals.Contains(HelperGoal.FarmGear))
            (gearWithheld, gearBandRefusals, gearWhoWithheld, unreadWorn) =
                FarmGear(inputs, candidates, gaps);
        // DRA-71 D7. Both read the player's own play and nothing else; the catalog's half of
        // each was refused by its own survey, which is written down where the engine is.
        if (goals.Contains(HelperGoal.FarmMotes)) FarmMotes(inputs, candidates, gaps);
        if (goals.Contains(HelperGoal.MakeMoney)) MakeMoney(inputs, candidates, gaps);
        // DRA-149 D3. Its two counts are kept apart from the gear engine's above for the reason
        // every cap in this file is said out loud separately (trap 50): they are answers about a
        // different list, and one merged number would point at neither.
        if (goals.Contains(HelperGoal.FarmMaterials))
            (materialBandRefusals, materialWhoWithheld) = FarmMaterials(inputs, candidates, gaps);
        if (goals.Contains(HelperGoal.WorkOnFaction)) Faction(inputs, candidates, gaps);
        // **THE PICK NARROWS THE ENGINE, NOT THE ROOM** (DRA-71 D5, plan P11). It happens here
        // rather than in the caller so the phone gets it the day it calls Rank — porting a
        // feature TO a surface is the signal its logic never went through the shared layer —
        // and PER SECTION, so a player working on one race still gets class answers.
        if (goals.Contains(HelperGoal.UnlockClasses))
            Unlocks(inputs, HelperGoal.UnlockClasses,
                UnlockPickStore.Narrow(inputs.Classes, inputs.UnlockPicks), candidates, gaps);
        if (goals.Contains(HelperGoal.UnlockRaces))
            Unlocks(inputs, HelperGoal.UnlockRaces,
                UnlockPickStore.Narrow(inputs.Races, inputs.UnlockPicks), candidates, gaps);

        var joined = Join(candidates);
        var ordered = joined
            .OrderByDescending(r => r.Goals.Count)
            .ThenByDescending(r => r.HasPersonalEvidence)
            .ThenByDescending(r => r.Weight)
            .ThenBy(r => r.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = ordered.Take(Math.Max(0, cap)).Select(Trim).ToList();
        return new RecommendationSet(
            top, Math.Max(0, ordered.Count - top.Count), deferred, gaps, gearWithheld,
            gearBandRefusals, gearWhoWithheld, unreadWorn,
            materialBandRefusals, materialWhoWithheld);
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
                Interleave(parts),
                [.. Dedupe(parts.SelectMany(p => p.Doors))],
                parts.Sum(p => p.WithheldWhy),
                parts.Max(p => p.Weight)));
        }
        result.AddRange(candidates.Where(c => c.Zone.Length == 0));
        return result;
    }

    /// <summary>
    /// **ONE SENTENCE PER ENGINE BEFORE ANY ENGINE GETS A SECOND** — round-robin across the
    /// parts a zone was merged from (DRA-71 D7).
    ///
    /// <para><b>A launched-app row found this and nothing else could have.</b> Concatenating
    /// the parts put every engine's sentences in a block, and <see cref="WhyCap"/> trims the
    /// TAIL — so the first merged row with three engines on it (Level Up + Farm Motes + Make
    /// Money, one zone, the differentiator working exactly as designed) drew a headline reading
    /// *"Level Up · Farm Motes · Make Money"* above six sentences of which NOT ONE was about
    /// money. The cap was correct, the engines were correct, and the row lied about itself.</para>
    ///
    /// <para>Interleaving makes the cap fair by construction rather than by arithmetic: at any
    /// cap of at least one per part, every goal the headline claims has at least one sentence
    /// under it. It also reads better — a merged row now leads with one figure per goal instead
    /// of burying the second engine — and it changes NOTHING for a row with one part, which is
    /// most of them.</para>
    ///
    /// <para>The alternative was raising the cap again (D4 already went 4→6 under protest), and
    /// it is the wrong lever: three engines can put ten sentences on one zone and a row with ten
    /// sentences has stopped being a recommendation. The count withheld is still reported
    /// (trap 50).</para>
    /// </summary>
    private static List<WhyFact> Interleave(List<Recommendation> parts)
    {
        var merged = new List<WhyFact>();
        var deepest = parts.Max(p => p.Why.Count);
        for (var i = 0; i < deepest; i++)
            foreach (var part in parts)
                if (i < part.Why.Count) merged.Add(part.Why[i]);
        return merged;
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
        Outgrown(z.Zone, z.ConnedMin, z.ConnedMax, z.ConnedKills, level);

    /// <summary>
    /// The same question asked of a band that did not arrive on a <see cref="ZoneRoll"/> —
    /// <see cref="MoteRoll"/> carries its own copy of the conned figures (DRA-71 D7).
    ///
    /// <para>ONE producer of the judgement, taking the numbers rather than the record, so the
    /// mote engine cannot drift into a second reading of "have you outgrown this". The
    /// <see cref="ZoneRoll"/> overload above is a call into this one for the same reason.</para>
    /// </summary>
    private static ZoneOutgrownFact? Outgrown(
        string zone, int connedMin, int connedMax, int connedKills, ResolvedLevel level) =>
        level.Known && connedMin > 0 && connedMax >= connedMin
        && level.Level - connedMax >= OutgrownBy
            ? new ZoneOutgrownFact(zone, connedMin, connedMax, level.Level, connedKills)
            : null;

    // ---- Farm Motes: where motes have actually dropped for you (DRA-71 D7, plan P10) -----

    /// <summary>
    /// **THE FOUNDER'S THREE MOTE CRITERIA, EACH MEASURED** (smoke item 5: *"Motes:
    /// highest-level zone, frequent kills, tier 2–4"*).
    ///
    /// <para><b>Personal-only, and the survey is why.</b> The plan let this slice check whether
    /// the shipped catalog could name where motes drop. It can't: all eleven mote records carry
    /// a <c>DropZones</c> and every value is "Various Zones", "Unknown" or "D3+ Zones". A drop
    /// zone nobody can travel to is not a drop zone, so there is no catalog arm and no invented
    /// mote→zone map — a character who has never looted a mote gets
    /// <see cref="GoalGapReason.NoMotesSeen"/> and a sentence naming what would fill it (trap
    /// 73).</para>
    ///
    /// <para><b>The primary term is the measured potency rate</b> and the Founder's three
    /// criteria are priced around it as discounts, which is D4's doctrine kept rather than
    /// reopened: the level band through the SAME <see cref="Outgrown"/> judgement the
    /// experience engine uses, the kill cadence against this character's own pooled rate, and
    /// the instance tier against the band he named. There is no bonus arm, so nothing here can
    /// promote a camp above the motes it actually paid.</para>
    ///
    /// <para><b>This is the engine that made <see cref="LevelUse.Consumes"/> true a second
    /// time.</b> D3 shipped with exactly one, and its own note said the throughput goals would
    /// each own their row when they landed.</para>
    /// </summary>
    private static void FarmMotes(HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        // Three ways to have nothing, and they are three different sentences. No stored play at
        // all is the same gap the experience engine draws; stored play with no mote in it is
        // its own fact and has no command that fixes it.
        var rated = inputs.Motes.Where(m => m.HasRate).ToList();
        if (rated.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmMotes,
                inputs.Zones.Any(z => z.HasPersonalEvidence)
                    ? GoalGapReason.NoMotesSeen
                    : GoalGapReason.NoPlayHistory));
            return;
        }

        var best = rated.Max(m => m.PotencyPerHour ?? 0);
        // ONE yardstick for the engine, folded once — a property of the SET, so a per-row
        // recomputation would be the same sum computed six times (trap 4 in a loop). And TWO
        // zones at minimum, which is D4's `Known` clause kept: a baseline built from one zone
        // IS that zone, so every single-zone profile would compare exactly average and a
        // discount that could never fire would be dressed as one that had been checked.
        var killBaseline = KillRateBaseline(rated);

        foreach (var m in rated
                     .OrderByDescending(m => m.PotencyPerHour ?? 0)
                     .Take(PerEngineCandidates))
        {
            var why = new List<WhyFact>
            {
                new ZoneMoteRateFact(
                    m.Zone, m.PotencyPerHour ?? 0, m.MotesPerHour ?? 0, m.Motes, m.VoidTouched,
                    m.Sessions, m.Hours),
            };

            // **EVERY DISCOUNT THAT FIRED COMES BEFORE THE FACT THAT WEIGHS NOTHING**, which is
            // D4's own ordering rule applied to this engine's three. `WhyCap` trims the tail, so
            // whatever is emitted last is what a loaded row gives up — and the one thing a row
            // must never give up is the explanation for a mark-down it already applied. The WHO
            // line is the nice-to-have here (it explains nothing about the order), so it goes
            // last, exactly where D4 put the instance tier for the same reason.

            // The cadence sentence is drawn only where its discount FIRED — ZoneDowntimeFact's
            // rule, and for its reason: a line on every row varies with nothing and says
            // nothing, and the potency rate is on screen either way.
            var slowKills = SlowKills(m, killBaseline);
            if (slowKills)
                why.Add(new ZoneKillRateFact(
                    m.Zone, m.KillsPerHour ?? 0, killBaseline.Rate, m.Kills, killBaseline.Zones));

            var outgrown = Outgrown(m.Zone, m.ConnedMin, m.ConnedMax, m.ConnedKills, inputs.Level);
            if (outgrown is { } fact) why.Add(fact);

            // The tier, and only where the player's own line recorded one OUTSIDE the band the
            // Founder named. An in-band instance draws nothing rather than a congratulation,
            // and an open-world zone draws nothing because the comparison does not exist.
            var offTier = OffPreferredTier(m.Tier);
            if (offTier)
                why.Add(new ZoneTierPreferenceFact(
                    m.Zone, m.Tier, MotePreferredTierMin, MotePreferredTierMax));

            // WHO, from the one source that cannot be stale. Silent where the fold found
            // nobody, which it only can be if a mote arrived under a creature with no name.
            if (m.Top is { } top)
                why.Add(new MoteSourceFact(top.Mob, m.Zone, top.Motes, top.Potency, top.Kills));

            into.Add(new Recommendation(
                RecommendationKind.Zone, m.Zone, m.Zone,
                [HelperGoal.FarmMotes], why,
                [new HelperDoor(HelperDoorKind.World, m.Zone),
                 new HelperDoor(HelperDoorKind.Wealth, "")],
                0,
                (best > 0 ? Math.Clamp((m.PotencyPerHour ?? 0) / best, 0, 1) : 0)
                * (outgrown is null ? 1 : OutgrownWeight)
                * (slowKills ? SlowKillWeight : 1)
                * (offTier ? OffPreferredTierWeight : 1)));
        }
    }

    /// <summary>This character's own pooled kill rate across the zones that have paid them
    /// motes, and how many zones it rests on. Pooled — total kills over total hours — and never
    /// a mean of the rows' own rates, so a zone farmed for forty hours counts for more than one
    /// farmed for twenty minutes rather than the same (<see cref="ZoneHistory.Baseline"/>'s own
    /// rule).</summary>
    private static (double Rate, int Zones) KillRateBaseline(IReadOnlyList<MoteRoll> rated)
    {
        double hours = 0;
        var kills = 0;
        var zones = 0;
        foreach (var m in rated)
        {
            if (m.KillsPerHour is null) continue;
            hours += m.Hours;
            kills += m.Kills;
            zones++;
        }
        return zones >= 2 && hours > 0 ? (kills / hours, zones) : (0, zones);
    }

    private static bool SlowKills(MoteRoll m, (double Rate, int Zones) baseline) =>
        baseline.Rate > 0 && m.KillsPerHour is { } rate && rate < baseline.Rate * SlowKillShare;

    /// <summary>Did the player's own zone line record an instance OUTSIDE the band the Founder
    /// named? False for open world and false for an adjective this build does not know — the
    /// preference applies only between instances, because "is an open-world camp better than a
    /// D3 for motes" is a question this repo cannot answer.</summary>
    private static bool OffPreferredTier(int tier) =>
        tier >= 0 && (tier < MotePreferredTierMin || tier > MotePreferredTierMax);

    // ---- Make Money / Farm to sell: your coin, and your own prices (DRA-71 D7, plan P9) ---

    /// <summary>
    /// **WHAT YOUR OWN SESSIONS HAVE EARNED, PER PLACE** (Founder smoke item 4c).
    ///
    /// <para><see cref="ZoneRoll.CopperPerHour"/> has carried a comment since D1 saying it is
    /// "read by the Make Money engine, which is a later slice". This is that slice, and the
    /// division was already there — which is the point of having written it where the
    /// experience rate lives rather than where a recommender wanted it.</para>
    ///
    /// <para><b>The plan asked the catalog's copper to be WEIGHED, and the survey is why it is
    /// not.</b> Of the 975 cached item pages that state a <c>merchant_value</c>, 262 head it
    /// "VALUE TO VENDOR with CHA : 80 and faction at Indifferently" — at a Charisma that
    /// differs per page — and 435 more are prose, approximations or qualified figures this
    /// build refuses to read. A vendor price in EQ moves with the seller's Charisma and their
    /// faction, so the wiki's number is a quote somebody was given rather than a fact about the
    /// object, and a ranking built on it would sort zones by which of their drops happen to
    /// have a priced page. What the player was PAID has neither problem, so
    /// <see cref="SaleHistory"/> is the evidence and the catalog's number is a label on an item
    /// they have never sold. The departure is logged in <c>DECISIONS.md</c> for veto.</para>
    /// </summary>
    private static void MakeMoney(HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        var earning = inputs.Zones
            .Where(z => z.HasPersonalEvidence && z.CopperPerHour is > 0)
            .ToList();
        if (earning.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.MakeMoney,
                inputs.Zones.Any(z => z.HasPersonalEvidence)
                    ? GoalGapReason.NoCoinEarned
                    : GoalGapReason.NoPlayHistory));
            return;
        }

        var best = earning.Max(z => z.CopperPerHour ?? 0);
        foreach (var z in earning
                     .OrderByDescending(z => z.CopperPerHour ?? 0)
                     .Take(PerEngineCandidates))
        {
            var why = new List<WhyFact>
            {
                new ZoneCoinRateFact(z.Zone, z.CopperPerHour ?? 0, z.Sessions, z.Hours),
            };
            why.AddRange(Sellables(inputs, z.Zone));

            into.Add(new Recommendation(
                RecommendationKind.Zone, z.Zone, z.Zone,
                [HelperGoal.MakeMoney], why,
                [new HelperDoor(HelperDoorKind.World, z.Zone),
                 new HelperDoor(HelperDoorKind.Wealth, "")],
                0,
                best > 0 ? Math.Clamp((z.CopperPerHour ?? 0) / best, 0, 1) : 0));
        }
    }

    /// <summary>
    /// **FARM TO SELL — the third gear intent, answered** (DRA-71 D7; the Founder's 4c, and the
    /// one <see cref="GearIntent.FarmToSell"/> has pointed at since D6).
    ///
    /// <para><b>It is not the dominance sweep and it must not be.</b> "What is worth money" has
    /// no worn anchor, so routing it through <see cref="GearUpgrades.Sweep"/> would be the one
    /// thing that sweep's summary forbids — a claim about the game's items ranked against each
    /// other. Instead the anchor is the player's own loot: what you have actually pulled out of
    /// a place, priced at what a vendor actually paid you for it. Both halves are measurements
    /// of this player, so the "never BiS" lock is not even approached.</para>
    ///
    /// <para>It shares <see cref="Sellables"/> with Make Money rather than copying it — one
    /// producer of "what is worth selling here" — and differs in exactly the way the two
    /// questions differ: Make Money leads with the coin your sessions earned and this one does
    /// not mention it, because coin off a corpse is not gear you farmed to sell.</para>
    /// </summary>
    private static void FarmToSell(HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        var rows = new List<(ZoneRoll Zone, List<WhyFact> Why, long Value)>();
        foreach (var z in inputs.Zones)
        {
            var why = Sellables(inputs, z.Zone);
            if (why.Count == 0) continue;
            rows.Add((z, why, why.OfType<SellableDropFact>().Sum(f => f.CopperEach * f.Drops)));
        }

        if (rows.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoSellEvidence));
            return;
        }

        // The weight is what this place has actually put in your pocket — drops you took out of
        // it times what one of them fetched — over the best row's. A count would say a zone full
        // of worthless drops beats one with a single valuable one; this is arithmetic over two
        // measurements, which is the same standard the gear rows' count-of-upgrades keeps.
        var best = rows.Max(r => r.Value);
        foreach (var row in rows
                     .OrderByDescending(r => r.Value)
                     .ThenBy(r => r.Zone.Zone, StringComparer.OrdinalIgnoreCase)
                     .Take(PerEngineCandidates))
            into.Add(new Recommendation(
                RecommendationKind.Zone, row.Zone.Zone, row.Zone.Zone,
                [HelperGoal.FarmGear], row.Why,
                [new HelperDoor(HelperDoorKind.World, row.Zone.Zone),
                 new HelperDoor(HelperDoorKind.Wealth, "")],
                0,
                best > 0 ? Math.Clamp(row.Value / (double)best, 0, 1) : 0));
    }

    /// <summary>
    /// What one zone drops that is worth selling — <b>ONE producer, read by two engines</b>.
    ///
    /// <para>The player's own pooled kills say what dropped here and how often; their own
    /// stored sales say what one fetched. Where they have never sold one, the catalog's number
    /// speaks instead — <see cref="Evidence.Catalog"/>, so the estimate label arrives by
    /// construction, and carrying the page's own Charisma-and-faction condition, because
    /// without it the number is a quote pretending to be a property (HOME-004 with teeth).</para>
    ///
    /// <para><b>A price is answered ONCE per item</b> (trap 4): your own sale wins, and only
    /// where there is none does the catalog answer. Two sentences naming two prices for one
    /// item is the shape a reader has to reconcile, and the gear rows already refused it for
    /// the creature.</para>
    /// </summary>
    private static List<WhyFact> Sellables(HelperInputs inputs, string zone)
    {
        var byItem = new Dictionary<string, (MobSummary Mob, MobLoot Loot)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var mob in inputs.Pool)
        {
            if (!mob.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var loot in mob.Loot)
            {
                // Motes are the other engine's answer and are not sold to a vendor; naming one
                // here would be one item with two homes and a recommendation to sell the thing
                // the player is being told to farm.
                if (Motes.IsMote(loot.Item)) continue;
                var key = QuestCatalog.BaseItemName(loot.Item);
                if (!byItem.TryGetValue(key, out var best) || loot.Count > best.Loot.Count)
                    byItem[key] = (mob, loot);
            }
        }

        var priced = new List<(WhyFact Fact, long Worth, string Item)>();
        foreach (var (item, (mob, loot)) in byItem)
        {
            if (SaleHistory.CopperEachFor(inputs.Sales, item) is { } each && each > 0)
            {
                priced.Add((
                    new SellableDropFact(item, mob.Name, zone, loot.Count, mob.Kills, each),
                    each * loot.Count, item));
                continue;
            }
            // The catalog's own number, for an item you have never sold. It was EMPTY on every
            // record when DRA-71 D7 wrote this arm; the DRA-84 D3 refresh filled it on 773 of
            // the 11,196, so this now draws on real rows. It still WEIGHS nothing — see
            // MerchantCopper's own summary: a price quoted at somebody else's Charisma is not a
            // property of the item, which is a survey finding rather than a gap that closed.
            if (inputs.Items?.Find(item) is { MerchantCopper: > 0 } record)
                priced.Add((
                    new CatalogValueFact(item, record.MerchantCopper!.Value,
                        record.MerchantCondition ?? ""),
                    // Worth nothing in the ORDERING, because it is worth nothing in the ranking:
                    // a price quoted at somebody else's Charisma must not decide which of your
                    // own drops is named first.
                    0, item));
        }

        return [.. priced
            .OrderByDescending(p => p.Worth)
            .ThenBy(p => p.Item, StringComparer.OrdinalIgnoreCase)
            .Take(SellablesPerRow)
            .Select(p => p.Fact)];
    }

    // ---- Farm Gear: the catalog, anchored on what you are wearing (DRA-71 D6, plan P8) ---

    /// <summary>
    /// How many upgrades one row names before it says it is holding some back.
    ///
    /// <para>Three, which is <see cref="DefaultCap"/> one level down and for the same
    /// HOME-002 reason: a zone row is an answer to "where should I go tonight", and a place
    /// that listed eleven items would be a shopping list with a heading. The count it held
    /// back rides the row (<see cref="Recommendation.WithheldWhy"/>, trap 50), and the Gear
    /// room's own wishlist is where the full list belongs — which is why every one of these
    /// rows carries a Gear door.</para>
    /// </summary>
    public const int GearNamedPerRow = 3;

    /// <summary>
    /// How many creatures ONE item line names before it says it is holding some back (DRA-84
    /// D4, plan P3).
    ///
    /// <para>Three, and for a reason one level down from <see cref="GearNamedPerRow"/>'s: a zone
    /// row names three items, so an uncapped who clause would put a row of eighteen creature
    /// names on screen. Three is also enough to be a PLAN — one name reads as the thing to kill,
    /// three reads as the sort of thing this zone drops it from, which is what a camp actually
    /// is.</para>
    ///
    /// <para><b>The cap is load-bearing on the shipped data rather than theoretical.</b> 3,830
    /// of the 10,637 (item, zone) pairs name more than one creature and 1,384 name more than
    /// three; before this slice the row named exactly one of them and said nothing about the
    /// rest. What it holds back rides the same line (<see cref="GearUpgradeFact.WhoWithheld"/>,
    /// trap 50), and the order is the wiki page's own — nothing here ranks creatures, because
    /// nothing here has measured them.</para>
    /// </summary>
    public const int GearMobsPerItem = 3;

    /// <summary>
    /// **UPGRADE WHAT I WEAR / REPLACE WITH BETTER** — the Founder's smoke items 4a and 4b.
    ///
    /// <para><b>The engine does no comparing.</b> <see cref="GearUpgrades.Sweep"/> owns the
    /// dominance question and <see cref="ItemDominance"/> owns the metric table; this method
    /// decides what a player travels to. A drop groups by its ZONE, which is the join key the
    /// whole room is built on (HOME-005) — so a zone that feeds a gear upgrade AND pays the
    /// experience rate you picked Level Up for becomes one row, which is the differentiator
    /// working rather than a coincidence. A quest groups by the QUEST, because a hand-in is
    /// not a place, and only ever behind the include-quests toggle.</para>
    ///
    /// <para><b>The weight is a count and never a taste.</b> A row's weight is how many of
    /// your open upgrades it accounts for, over the best row's — the same measure the Gear
    /// room's own <c>GearFarmRollup</c> has ranked camps by since 1.84. "Which of two
    /// upgrades is better for your character" has no answer in this repo and none is invented;
    /// "this zone feeds four of them and that one feeds one" is arithmetic.</para>
    ///
    /// <para><b>Every catalog line is <see cref="Evidence.Catalog"/> and every observed drop
    /// is <see cref="Evidence.Personal"/></b>, so HOME-004's label arrives by construction and
    /// HOME-003's sort does the rest: a zone where you have actually SEEN the thing drop
    /// outranks one you have only read about, through
    /// <see cref="Recommendation.HasPersonalEvidence"/> rather than through a weight this
    /// slice invented.</para>
    ///
    /// <para><b>Level is EXEMPT here, with a surveyed reason</b> — see
    /// <see cref="LevelExemptReason"/>. It is the default in this delivery most worth a
    /// veto.</para>
    /// </summary>
    /// <returns>What the sweep's per-anchor cap held back, which zones the band gate refused,
    /// what the who rule withheld, and the worn rows EQBuddy could never read (DRA-149 D2 —
    /// the last one is not a decision this method made, which is exactly why it has to be
    /// carried out rather than inferred from a short list of anchors).</returns>
    private static (
        int Withheld,
        List<GearBandRefusal> Refused,
        int WhoWithheld,
        IReadOnlyList<string> UnreadWorn) FarmGear(
        HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        // A DECIDED deferral, said out loud. Every intent answers since DRA-71 D7, so this arm
        // is unreachable today and STAYS: a fourth intent arriving Deferred must say so rather
        // than return an empty list, which would read as a character with perfect gear.
        if (GearUpgrades.ShapeFor(inputs.GearIntent) != GearIntentShape.Answered)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmGear, GoalGapReason.GearIntentNotAnsweredYet));
            return (0, [], 0, []);
        }

        // **"Farm to sell" is a different question and leaves here** (DRA-71 D7, plan P9). It
        // has no worn anchor and does no comparing, so it must not enter the dominance sweep
        // below — see FarmToSell for why routing it through Sweep would be the one claim that
        // sweep's own summary forbids. It needs no inventory dump either: the anchor is what
        // you have LOOTED, not what you are wearing.
        //
        // **IT IS ALSO WHY FarmToSell STAYS LEVEL-EXEMPT** while the goal as a whole now
        // Consumes (DRA-84 D2). The band gate below refuses a CAMP, and this intent names no
        // camp: it ranks what the player has already looted, priced at what a vendor has
        // already paid them. There is no zone to look a band up for, and D7's coin reasoning
        // is untouched by anything D2 measured.
        //
        // **The unread worn rows leave with it, for the same reason** (DRA-149 D2). This intent
        // never looked at a worn row, so it has nothing to report about one — a sentence naming
        // an item the player is wearing, under a question about what to sell, would be an
        // answer to a question nobody asked.
        if (inputs.GearIntent == GearIntent.FarmToSell)
        {
            FarmToSell(inputs, into, gaps);
            return (0, [], 0, []);
        }

        // "EQBuddy has never been told what you are wearing" is a different state from
        // "nothing beats it", and only the first one has a command that fixes it.
        //
        // **AND A THIRD STATE SITS BETWEEN THEM** (DRA-149 D2, plan P2): a dump that arrived and
        // whose every worn row named something EQBuddy has never read about. Asking for the
        // inventory command there is a loop with no exit — the command has already been run and
        // running it again produces the same unreadable rows — so the gap says which state it
        // is, and the names ride out beside it either way.
        if (inputs.Worn.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmGear, inputs.UnreadWorn.Count > 0
                ? GoalGapReason.NothingWornIsReadable
                : GoalGapReason.NoInventoryDump));
            return (0, [], 0, inputs.UnreadWorn);
        }

        var sweep = GearUpgrades.Sweep(
            inputs.GearIntent, inputs.Worn, inputs.WornPicks,
            inputs.Items, inputs.MyClasses, inputs.IncludeQuests);
        if (sweep.Upgrades.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoCatalogUpgrade));
            return (sweep.Withheld, [], 0, inputs.UnreadWorn);
        }

        var byZone = new Dictionary<string, List<GearCandidate>>(StringComparer.OrdinalIgnoreCase);
        var byQuest = new Dictionary<string, List<GearCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var upgrade in sweep.Upgrades)
        {
            // An item that drops in five zones is offered under every one of them, which is
            // GearFarmRollup's own rule and its own reason: the question a row answers is "if
            // I camp here tonight, what can this place still give me", and a per-zone list
            // that hid a valid camp would make its own heading lie.
            //
            // The creature question is answered HERE, once, for the (offer, place) pair — and
            // it is only ACTED on further down, after the band gate has had its say.
            foreach (var zone in upgrade.Zones.Distinct(StringComparer.OrdinalIgnoreCase))
                Bucket(byZone, zone,
                    new GearCandidate(upgrade, WhoFor(inputs, upgrade.Item, upgrade.MobsIn(zone), zone)));
            // **A quest row is not a drop row and the who rule does not reach it.** "Who drops
            // it" has no answer for a hand-in and needs none — the quest IS the path, which is
            // the other half of acceptance item 2's "source mob(s) AND/OR quest".
            foreach (var quest in upgrade.Quests.Distinct(StringComparer.OrdinalIgnoreCase))
                Bucket(byQuest, quest, new GearCandidate(upgrade, GearWho.None));
        }

        // **THE BAND GATE, AND IT RUNS BEFORE THE YARDSTICK IS TAKEN** (DRA-84 D2, plan P2).
        // A refused zone is not a candidate, so it must not set the scale the surviving rows
        // are measured against — leaving it in `best` would let a camp this character cannot
        // farm decide how full every other row's bar looks.
        var refused = BandGate(inputs, byZone);

        // **AND THE WHO RULE RUNS AFTER IT** (DRA-84 D4, plan P3; acceptance item 2, the Rathe
        // exhibit). Both rules can remove the same row and the ORDER decides which sentence the
        // player gets, so it is chosen rather than incidental: the band refusal quotes eqlwiki's
        // own numbers and this character's level, and the who rule can only say that a page was
        // silent. Running the who rule first would have swallowed refusals D2 shipped —
        // Crushbone at level 30 would vanish as "no creature named" instead of as "eqlwiki lists
        // its creatures at 5–20". A slice must not quietly narrow what the slice before it
        // refused out loud.
        var whoWithheld = WhoRule(byZone, c => c.Who);

        // ONE yardstick for the whole engine, folded once — a property of the SET, and a
        // per-row recomputation would be the same sum computed six times (trap 4 in a loop).
        // Zones and quests share it deliberately: they are answers to one goal and ranking
        // them on two scales would make the order meaningless where they interleave.
        var best = Math.Max(
            byZone.Count > 0 ? byZone.Max(kv => kv.Value.Count) : 0,
            byQuest.Count > 0 ? byQuest.Max(kv => kv.Value.Count) : 0);

        // **A GATE THAT EMPTIED THE LIST HAS TO SAY SO IN ITS OWN VOICE.** The refusal
        // sentence alone is not enough: with no rows and no gap the room draws its
        // whole-room empty state, which says EQBuddy has nothing stored — the opposite of
        // what happened, which is that it read the catalog, found upgrades and refused every
        // place they drop. NoCatalogUpgrade would be a lie for the same reason.
        if (byZone.Count == 0 && byQuest.Count == 0)
        {
            // Two rules can empty this list and they are different states, so they get
            // different sentences. The band gate is named first because it is the louder of
            // the two and carries its own numbers; the who rule answers for the rest.
            if (refused.Count > 0)
                gaps.Add(new GoalGap(HelperGoal.FarmGear, GoalGapReason.EveryZoneOutsideYourBand));
            else if (whoWithheld > 0)
                gaps.Add(new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoUpgradeNamesACreature));
        }

        foreach (var (zone, candidates) in Ranked(byZone))
            into.Add(GearRow(RecommendationKind.Zone, zone, zone, candidates, best,
                [new HelperDoor(HelperDoorKind.World, zone), new HelperDoor(HelperDoorKind.Gear, "")]));

        foreach (var (quest, candidates) in Ranked(byQuest))
            into.Add(GearRow(RecommendationKind.Quest, quest, "", candidates, best,
                [new HelperDoor(HelperDoorKind.QuestCatalog, quest),
                 new HelperDoor(HelperDoorKind.Gear, "")]));

        return (sweep.Withheld, refused, whoWithheld, inputs.UnreadWorn);
    }

    /// <summary>Add one candidate under one key. Generic since DRA-149 D3 — the materials
    /// engine buckets by zone the same way and for the same reason.</summary>
    private static void Bucket<T>(Dictionary<string, List<T>> into, string key, T candidate)
    {
        if (!into.TryGetValue(key, out var list)) into[key] = list = [];
        list.Add(candidate);
    }

    /// <summary>The buckets a drop engine will actually draw, best first: how many candidates
    /// a place offers, then alphabetically so an equal pair is stable rather than
    /// dictionary-ordered. Capped at <see cref="PerEngineCandidates"/>.</summary>
    private static List<KeyValuePair<string, List<T>>> Ranked<T>(
        Dictionary<string, List<T>> buckets) =>
        [.. buckets
            .OrderByDescending(kv => kv.Value.Count)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(PerEngineCandidates)];

    /// <summary>
    /// One catalog upgrade, offered under one place, with the creature question already
    /// answered for it (DRA-84 D4, plan P3).
    ///
    /// <para>The pairing exists so the who is decided ONCE, at the door, and read at the row
    /// (trap 4). Before this slice the row resolved it — which was harmless while it only
    /// decided a sentence, and is not now that it also decides whether the offer exists at
    /// all: a bucket whose count disagreed with the rows drawn from it would make this
    /// engine's weight a number about a different list.</para>
    /// </summary>
    private readonly record struct GearCandidate(GearUpgrade Upgrade, GearWho Who);

    /// <summary>
    /// **A DROP OFFER THAT CANNOT SAY WHAT DROPS IT IS NOT AN OFFER** (DRA-84 D4, plan P3;
    /// Founder acceptance item 2, and the Rathe Mountains exhibit by name).
    ///
    /// <para>The Founder's Rathe row named an item and a place and stopped. Its band is 13–45,
    /// which spans most characters, so D2's level gate cannot refuse it and was never going to
    /// — what was wrong with that row is that there was nothing in it to DO. This rule is the
    /// other mechanism the plan named for the other class of failure.</para>
    ///
    /// <para><b>It prunes rather than filtering at the door, so the count stays honest.</b> The
    /// bucket count is this engine's weight AND its yardstick, so an offer that will never be
    /// drawn must not inflate the zone it cannot be drawn under; a zone left with nothing goes
    /// with them, because a heading over no items is the emptiest version of the same row.</para>
    ///
    /// <para><b>It is rare by construction on the shipped data</b> — 98.2% of the catalog's
    /// wearable (item, zone) pairs name a creature since the D3 refresh — which is what makes
    /// the rule affordable. Before that refresh it would have withheld everything, which is
    /// exactly why the plan ordered this slice after it and made the coverage survey this
    /// slice's opening move (<c>ItemCatalogWhoCoverageTests</c>).</para>
    /// </summary>
    /// <returns>How many (item, zone) offers were withheld.</returns>
    /// <remarks><b>It is generic since DRA-149 D3</b>, and nothing about the rule moved: Farm
    /// Materials asks the same question of the same catalog and had to get the same answer, so
    /// it calls this rather than growing a second copy that could later disagree about what
    /// "nobody can say what drops it" means (trap 4). The candidate type is the only thing the
    /// two engines do not share.</remarks>
    private static int WhoRule<T>(Dictionary<string, List<T>> byZone, Func<T, GearWho> who)
    {
        var withheld = 0;
        foreach (var zone in byZone.Keys.ToList())
        {
            var kept = byZone[zone].FindAll(c => who(c).Answered);
            withheld += byZone[zone].Count - kept.Count;
            if (kept.Count == 0) byZone.Remove(zone);
            else byZone[zone] = kept;
        }
        return withheld;
    }

    /// <summary>
    /// **WHO DROPS IT — ONE ANSWER, FROM ONE SOURCE** (DRA-84 D4, plan P3; trap 4).
    ///
    /// <para>The player's own pool WINS wherever it has seen the item drop here: it is
    /// measured, it carries its denominator, and it cannot be stale. Only where it has not does
    /// the wiki's own list speak, Catalog-labelled, capped at
    /// <see cref="GearMobsPerItem"/>. The two are never both drawn for one item — two
    /// sentences naming two creatures for one thing is a contradiction the reader has to
    /// resolve, and the catalog clause is empty BY CONSTRUCTION here rather than by a rule at
    /// the far end that someone could later forget.</para>
    /// </summary>
    /// <param name="Seen">The player's own kills, or null.</param>
    /// <param name="Named">The wiki's creatures for this zone, capped. Empty where
    /// <paramref name="Seen"/> answered, and empty where the page named nobody.</param>
    /// <param name="Withheld">How many more the page named.</param>
    private readonly record struct GearWho(
        GearDropSeenFact? Seen, IReadOnlyList<string> Named, int Withheld)
    {
        /// <summary>The answer for a place that is not a place — a quest row, which is asked
        /// nothing and withheld for nothing.</summary>
        public static readonly GearWho None = new(null, [], 0);

        /// <summary>Whether anything at all can say what drops this here. The withhold rule is
        /// this property and nothing else.</summary>
        public bool Answered => Seen is not null || Named.Count > 0;
    }

    /// <param name="item">The thing being offered — a catalog upgrade, or since DRA-149 D3 a
    /// tradeskill material. It takes the NAME and the page's creature list rather than a
    /// <see cref="GearUpgrade"/> for that reason: the precedence is a rule about evidence, not
    /// about gear, and a second copy of it for materials is the contradiction trap 4
    /// names.</param>
    private static GearWho WhoFor(
        HelperInputs inputs, string item, IReadOnlyList<string> named, string zone)
    {
        if (zone.Length == 0) return GearWho.None;
        if (SeenDrop(inputs.Pool, item, zone) is { } seen)
            return new GearWho(seen, [], 0);

        return named.Count == 0
            ? GearWho.None
            : new GearWho(null, [.. named.Take(GearMobsPerItem)],
                Math.Max(0, named.Count - GearMobsPerItem));
    }

    /// <summary>
    /// **THE FOUNDER'S VETO, AS A RULE THAT REMOVES ROWS** (DRA-84 D2, plan P2; acceptance
    /// item 3 — *"no zones with no viable upgrade path for my intent/level"*, Crushbone and
    /// the Rathe named as the exhibits).
    ///
    /// <para><b>It REFUSES rather than demotes, and that is a deliberate departure from the
    /// precedent beside it.</b> <see cref="OutgrownWeight"/> halves a zone and keeps it,
    /// because there the zone is the player's OWN measured evidence and a recommender that
    /// deleted their best camp would be overruling a measurement with a judgement. These rows
    /// are <see cref="Evidence.Catalog"/> — EQBuddy read about them — and their mere presence
    /// is what the Founder failed. So the two rules are opposite on purpose: protect what the
    /// player measured, remove what we only read about.</para>
    ///
    /// <para><b>It fires even where the player HAS farmed the zone.</b> Crushbone stays
    /// refused for a 29 who camped it at 12, because the question a Farm Gear row answers is
    /// where to go tonight and a personal drop from seventeen levels ago does not change the
    /// band. This is the delivery's most vetoable default and is logged as one.</para>
    ///
    /// <para><b>Both numbers are judgements</b> — <see cref="OutgrownBy"/> reused rather than
    /// re-derived, and <see cref="GearBandReachAbove"/> new. No XP curve is invented, and the
    /// band is eqlwiki's own row rather than anything this repo computed.</para>
    ///
    /// <para><b>The TOP arm stands down for an open-topped band</b>
    /// (<see cref="ZoneLevels.Band.Max"/> null — 41 of the 87 shipped bands). There is no top
    /// to be ten levels under, and reading the number before the `+` as one would be inventing
    /// the maximum Helm's option (a) refused to invent. Those zones can still be refused by the
    /// BOTTOM arm, which is what keeps a level-12 character out of Plane of Sky's `50+`.</para>
    ///
    /// <para><b>THE ITEM SIDE IS STILL NOT GATED, and the D6 survey is why</b> — kept here
    /// because it is the live reason this gate reads a ZONE. Every one of the 11,196 shipped
    /// item records was scanned for a Level or Required-Level key: FIVE print one and exactly
    /// ONE is a wearable (<c>Shroud of the Sky</c>, <c>Required Level: 46</c>); the other four
    /// are <c>Level Needed</c> on spell scrolls and a food, which is the SPELL's level and not
    /// a requirement to equip anything. Re-taken on the DRA-84 D3 refresh, so the claim is
    /// "one" rather than D6's "none" and the conclusion is unchanged: one wearable in 11,196 is
    /// not a datum an engine can gate on. Inventing a level requirement per item would be trap
    /// 73 with arithmetic instead of prose.</para>
    ///
    /// <para><b>And the P6 outgrown DISCOUNT is still not borrowed here</b>, which is the
    /// other half of that survey and is not what D2 changed. That discount is a claim about XP
    /// throughput; for gear the zone is where the ITEM is, and an outgrown camp is if anything
    /// the quicker farm. What D2 adds is not a discount — it is a refusal on eqlwiki's
    /// published band, and it is bounded by two named distances rather than applied as a
    /// slope.</para>
    ///
    /// <para>Three ways to stand down entirely, each a different silence: no resolved level
    /// (EQBuddy has not been told, and the room already offers the Character door), no
    /// <see cref="HelperInputs.Bands"/>, and no band for this zone — 31 zone pages answer
    /// nothing and an unanswered question gates nothing (trap 73).</para>
    /// </summary>
    /// <param name="byZone">Mutated in place: a refused zone is REMOVED.</param>
    /// <returns>One entry per refused zone, in the order a reader would meet them
    /// (alphabetical), each carrying the band and the level its sentence quotes.</returns>
    /// <remarks><b>Generic since DRA-149 D3, with the same constants and the same two arms.</b>
    /// Farm Materials names camps out of the same catalog, so a material whose only zone is
    /// Temple of Veeshan has to be refused for a level 30 for the identical reason a helm there
    /// is. Sharing the gate rather than the numbers is what stops the two lists from drifting
    /// apart the first time either constant is tuned.</remarks>
    private static List<GearBandRefusal> BandGate<T>(
        HelperInputs inputs, Dictionary<string, List<T>> byZone)
    {
        var refused = new List<GearBandRefusal>();
        if (!inputs.Level.Known || inputs.Bands is not { } bands) return refused;

        var level = inputs.Level.Level;
        foreach (var zone in byZone.Keys
                     .OrderBy(z => z, StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (bands.BandFor(zone) is not { } band) continue;
            if (ArmFor(band, level) is not { } arm) continue;
            byZone.Remove(zone);
            refused.Add(new GearBandRefusal(
                zone, band.Min, band.Max, band.Verbatim, level, arm));
        }
        return refused;
    }

    /// <summary>
    /// Which arm refuses this band at this level, or null for a band that is in reach.
    ///
    /// <para>Split out so the judgement is one expression a test can drive at a boundary: the
    /// numbers are inclusive thresholds (*"<see cref="OutgrownBy"/> or more under"*), and
    /// off-by-one on either of them would silently change which zones a player sees.</para>
    /// </summary>
    internal static GearBandArm? ArmFor(ZoneLevels.Band band, int level)
    {
        if (band.Max is { } max && level - max >= OutgrownBy) return GearBandArm.TopUnder;
        if (band.Min - level >= GearBandReachAbove) return GearBandArm.BottomOver;
        return null;
    }

    /// <summary>
    /// One gear row: what the place (or the quest) can give you, what it beats, and — since
    /// DRA-84 D4 — what drops it.
    ///
    /// <para><b>The creature was answered at the door</b> (<see cref="WhoFor"/>, trap 4). This
    /// method draws the answer and decides nothing about it: every candidate that reached here
    /// under a ZONE can name somebody, because one that could not was never bucketed.</para>
    /// </summary>
    private static Recommendation GearRow(
        RecommendationKind kind, string subject, string zone,
        List<GearCandidate> candidates, int best, List<HelperDoor> doors)
    {
        var named = candidates
            .OrderByDescending(c => c.Upgrade.ImprovedMetrics)
            .ThenBy(c => c.Upgrade.Item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shown = named.Take(GearNamedPerRow).ToList();

        var why = new List<WhyFact>();
        foreach (var (upgrade, who) in shown)
        {
            why.Add(new GearUpgradeFact(
                upgrade.Item, upgrade.Over, upgrade.Slot, upgrade.GainMetric, upgrade.GainBy,
                who.Named, who.Withheld));
            if (who.Seen is { } fact) why.Add(fact);
        }

        return new Recommendation(
            kind, subject, zone, [HelperGoal.FarmGear], why, doors,
            named.Count - shown.Count,
            best > 0 ? Math.Clamp(named.Count / (double)best, 0, 1) : 0);
    }

    /// <summary>
    /// Has this character actually seen the item drop here, and from what?
    ///
    /// <para>The pool is keyed on (creature, zone), so this is a scan of the creatures the
    /// player has killed in THIS zone for one they looted this item from. The busiest source
    /// wins — a creature that gave it to you four times is the answer to "who drops it" in a
    /// way one lucky pull from something else is not.</para>
    ///
    /// <para>Names are compared through <c>QuestCatalog.BaseItemName</c> on both sides: the
    /// log prints what the game called it ("+N" and all) and the catalog titles the base item,
    /// which is the same fold every other join in this repo between those two sources
    /// makes.</para>
    /// </summary>
    private static GearDropSeenFact? SeenDrop(
        IReadOnlyList<MobSummary> pool, string item, string zone)
    {
        var wanted = QuestCatalog.BaseItemName(item);
        (MobSummary Mob, MobLoot Loot)? best = null;
        foreach (var mob in pool)
        {
            if (!mob.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var loot in mob.Loot)
            {
                if (!QuestCatalog.BaseItemName(loot.Item)
                        .Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
                if (best is null || loot.Count > best.Value.Loot.Count) best = (mob, loot);
            }
        }
        return best is { } b
            ? new GearDropSeenFact(item, b.Mob.Name, zone, b.Loot.Count, b.Mob.Kills)
            : null;
    }

    // ---- Farm Materials: the drop half (DRA-149 D3, plan P4) ----------------------------

    /// <summary>
    /// How many materials one zone row names before the cap. <see cref="GearNamedPerRow"/>'s
    /// number and its reason: three named things plus the creatures under them is what fits
    /// under <see cref="WhyCap"/> without trimming the sentences a row was ranked on.
    /// </summary>
    public const int MaterialsNamedPerRow = 3;

    /// <summary>One ingredient, offered under one place, with the creature question already
    /// answered for it. <see cref="GearCandidate"/>'s shape and its reason — the who is decided
    /// at the door and read at the row, because it decides whether the offer exists at
    /// all.</summary>
    private readonly record struct MaterialCandidate(TradeskillMaterial Material, GearWho Who);

    /// <summary>
    /// **WHERE TO FARM WHAT YOUR PROFESSIONS NEED** (DRA-149 D3, plan P4; the Founder's FAIL
    /// item 3 — *"jewelcrafting … should recommend zones/creatures where gems drop more
    /// commonly"*).
    ///
    /// <para><b>It un-parks a goal that had been Deferred since DRA-71 D8, and the park was not
    /// wrong — it was about the wrong COLUMN.</b> That survey read <c>[[Category:…]]</c> and
    /// found 14 of 11,197 pages naming a profession; the <c>Recipes</c> column names all eight
    /// as headings over 1,276 records. <see cref="TradeskillMaterials"/> is the reader and
    /// <c>scripts/dra149-materials-survey.py</c> is the measurement, including the answer to
    /// the escalation question this slice was declared to ask: a page's <c>recipes</c> field
    /// lists the recipes an item is USED IN, so the record IS the ingredient, and the 150
    /// intermediates that are themselves recipe outputs drop nowhere and leave through the zone
    /// gate without a rule of their own.</para>
    ///
    /// <para><b>It reuses the Farm Gear machinery whole rather than resembling it.</b> The same
    /// <see cref="WhoFor"/> precedence (your own pooled kills WIN; the page's creature list
    /// behind them, never both — trap 4), the same <see cref="BandGate{T}"/> with the same two
    /// constants and the same arms, the same <see cref="WhoRule{T}"/> AFTER it in the same
    /// order and for the same reason: a band refusal quotes eqlwiki's numbers and this
    /// character's level, and the who rule can only say a page was silent, so running them the
    /// other way round would swallow the louder sentence. Those three are shared code, not
    /// shared prose.</para>
    ///
    /// <para><b>The weight is how many of your professions' ingredients one place feeds</b> —
    /// the gear engine's own yardstick, one noun over. A zone that drops four gems you need
    /// outranks one that drops a single ore, and the bucket count is both the weight and the
    /// scale, so a pruned offer must not inflate the zone it can no longer be drawn under
    /// (which is why the who rule prunes rather than filters at the door).</para>
    ///
    /// <para><b>There is no personal-rate arm and that is deliberate.</b> EQBuddy stores what
    /// you have looted, so it could rank gem zones by your own drop rate — but the question the
    /// Founder asked is "where do gems drop more commonly", which for a profession you are
    /// STARTING is about places you have never been. Your own kills still speak, in the one
    /// place they outrank the page: <see cref="WhoFor"/>'s first clause.</para>
    /// </summary>
    /// <returns>Which zones the band gate refused and how many offers the who rule withheld —
    /// counted separately from the gear engine's, because they are answers about a different
    /// list and a merged number would point at neither.</returns>
    private static (List<GearBandRefusal> Refused, int WhoWithheld) FarmMaterials(
        HelperInputs inputs, List<Recommendation> into, List<GoalGap> gaps)
    {
        var materials = TradeskillMaterials.From(inputs.Items, inputs.Professions);
        if (materials.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmMaterials, GoalGapReason.NoMaterialDrops));
            return ([], 0);
        }

        // An ingredient that drops in five zones is offered under every one of them —
        // GearFarmRollup's rule and its reason: the question a row answers is "if I camp here
        // tonight, what can this place give me", and a per-zone list that hid a valid camp
        // would make its own heading lie.
        var byZone = new Dictionary<string, List<MaterialCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in materials)
            foreach (var zone in material.Zones)
                Bucket(byZone, zone, new MaterialCandidate(
                    material, WhoFor(inputs, material.Item, material.MobsIn(zone), zone)));

        var refused = BandGate(inputs, byZone);
        var whoWithheld = WhoRule(byZone, c => c.Who);

        // **A RULE THAT EMPTIED THE LIST SAYS SO IN ITS OWN VOICE** — the gear engine's own
        // clause, with this engine's three reasons. Silence here would draw the room's
        // whole-room empty state, which says EQBuddy has nothing stored: the opposite of what
        // happened, which is that it read the recipes, found the ingredients and refused every
        // place they drop.
        if (byZone.Count == 0)
        {
            gaps.Add(new GoalGap(HelperGoal.FarmMaterials,
                refused.Count > 0 ? GoalGapReason.EveryMaterialZoneOutsideYourBand
                : whoWithheld > 0 ? GoalGapReason.NoMaterialNamesACreature
                : GoalGapReason.NoMaterialDrops));
            return (refused, whoWithheld);
        }

        var best = byZone.Max(kv => kv.Value.Count);
        foreach (var (zone, candidates) in Ranked(byZone))
            into.Add(MaterialRow(zone, candidates, best));

        return (refused, whoWithheld);
    }

    /// <summary>
    /// One materials row: what this place drops that your professions need, and what to kill
    /// for it.
    ///
    /// <para>The creature was answered at the door (<see cref="WhoFor"/>, trap 4), so every
    /// candidate that reached here can name somebody. Materials are named in the page's own
    /// order within a profession and then alphabetically, because there is no "better" among
    /// ingredients to rank them by — a gem is not an improvement on an ore — and inventing one
    /// would be arithmetic nobody asked for.</para>
    /// </summary>
    private static Recommendation MaterialRow(
        string zone, List<MaterialCandidate> candidates, int best)
    {
        var named = candidates
            .OrderBy(c => c.Material.Skill)
            .ThenBy(c => c.Material.Item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var shown = named.Take(MaterialsNamedPerRow).ToList();

        var why = new List<WhyFact>();
        foreach (var (material, who) in shown)
        {
            why.Add(new TradeskillMaterialFact(
                material.Skill, material.Item,
                material.Recipes.Count > 0 ? material.Recipes[0] : "",
                Math.Max(0, material.Recipes.Count - 1),
                who.Named, who.Withheld));
            if (who.Seen is { } seen) why.Add(seen);
        }

        return new Recommendation(
            RecommendationKind.Zone, zone, zone, [HelperGoal.FarmMaterials], why,
            [new HelperDoor(HelperDoorKind.World, zone), new HelperDoor(HelperDoorKind.Gear, "")],
            named.Count - shown.Count,
            best > 0 ? Math.Clamp(named.Count / (double)best, 0, 1) : 0);
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
