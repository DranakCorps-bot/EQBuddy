namespace EQBuddy.Core;

/// <summary>
/// The eight professions EQ Legends gives a Mastery AA to — <b>the curated list, and the
/// enum is the must-list's subject</b> (DRA-71 D8, plan P13).
///
/// <para>The order is the AA page's own: alphabetical. It is not a ranking and nothing reads
/// it as one.</para>
/// </summary>
public enum Tradeskill
{
    Alchemy,
    Baking,
    Blacksmithing,
    Brewing,
    Fletching,
    Jewelcrafting,
    Pottery,
    Tailoring,
}

/// <summary>
/// One profession, as the wiki names it.
/// </summary>
/// <param name="Name">What the profession is CALLED, in the spelling the ability's own effect
/// text uses ("reduces the chance of failing <i>Blacksmithing</i> recipes"). That is the
/// wiki's own sentence about the skill rather than a label anybody here chose, which is what
/// lets <c>TradeskillsTests</c> assert it against the shipped AA catalog instead of against a
/// comment.</param>
/// <param name="MasteryAa">The Mastery AA that proves this profession exists — the source the
/// plan named. It is a KEY into <see cref="AaCatalog"/>, so a profession whose AA left the
/// catalog fails the build rather than going quietly stale.</param>
/// <param name="WikiPage">eqlwiki's own page title for the skill. Every one of these is a
/// title the cached item pages LINK to (<c>[[Skill Blacksmithing]]</c>, 179 item pages;
/// <c>[[Skill Pottery]]</c>, the thinnest at 18) rather than a URL assembled from the name —
/// trap 3's lesson one step earlier: a title we invented is a page nobody has seen.</param>
/// <param name="LogNames">
/// Every spelling that counts as this profession when the log says <i>"You have become better
/// at X! (N)"</i>. Matching is whole-string and case-insensitive — never a substring, because
/// "Alchemy" inside a longer skill name would be a different skill.
///
/// <para><b>The list is longer than one because the wiki itself spells three of these two
/// ways, and one of them three ways.</b> Jewelcrafting is the AA name "Jewel Craft Mastery",
/// the effect text's "Jewelcrafting", and — on the same page, inside Crafting Mastery's own
/// sentence — "Jewelcraft". A curated list that picked one and matched on it would silently
/// lose the standing of whichever spelling the game's log happens to print.</para>
///
/// <para><b>"Jewelry Making" is the one entry that is NOT from eqlwiki</b>, and it is marked
/// as such under the standing rule that other sources are allowed where the wiki is silent
/// (David, 2026-08-16): the wiki names the SKILL, and is silent on what the skill-up line
/// prints, which is a different fact. It is the classic EverQuest spelling of that skill and
/// it costs nothing to carry — an alias the log never prints simply never matches, while a
/// missing one costs a player their standing with no way to tell why.</para>
/// </param>
public sealed record TradeskillProfession(
    Tradeskill Skill,
    string Name,
    string MasteryAa,
    string WikiPage,
    IReadOnlyList<string> LogNames);

/// <summary>
/// **WHAT THIS CHARACTER'S PROFESSIONS ARE, AND WHERE THEY STAND** (DRA-71 D8, plan P13;
/// Founder smoke item 6 — *"resources: profession-first, and plan honestly on gaps"*).
///
/// <para><b>CURATED, and never auto-written.</b> It is the same rule the spawn timers, the AA
/// list and the guide catalog keep: a machine may write a file BESIDE a curated one, never
/// into it. Nothing in the weekly refresh touches this file, and nothing should — eight rows
/// that a human checked against the wiki are worth more than eighty a transformer guessed.
/// </para>
///
/// <para><b>Why the Mastery AA list is the source.</b> EQ Legends' own Alternate Advancement
/// page grants one Mastery ability per profession, and the shipped <see cref="AaCatalog"/>
/// carries all of them with their effect text. So "which professions exist" is answered by a
/// catalog EQBuddy already ships rather than by a list somebody typed: every row here names
/// its AA, and <c>TradeskillsTests</c> reads that AA's own effect sentence back out of the
/// catalog to check the spelling. A ninth profession arriving in the game would arrive in the
/// AA harvest first, and the test that counts them is where it would be noticed.</para>
///
/// <para><b>FOUR REAL SKILLS ARE DELIBERATELY NOT HERE</b>, and the omission is the plan's
/// rather than an oversight: Tinkering, Research (Spell Research), Make Poison and Fishing all
/// have eqlwiki skill pages, and the cached item pages name all four in recipe lines — but none
/// of them has a Mastery AA, which is the source this list is built from. They are named here
/// so the next person can see the edge of the curation rather than discover it. Widening the
/// list is a decision, not a fix.</para>
///
/// <para><b>There is no item→profession arithmetic behind this, and that is the delivery's
/// main finding</b> (see <c>DECISIONS.md</c>, DRA-71 D8). The coverage survey the plan asked
/// for ran first: 11,157 of the 11,197 cached item pages carry at least one
/// <c>[[Category:…]]</c> — 99.6%, across 553 distinct categories — and exactly <b>14</b> of
/// them carry a category naming a profession, across five of the eight. The field is populated and it answers a different
/// question (who can wear this, what slot, which zone, how it is obtained), so the arithmetic
/// PARKS with its reopen condition rather than shipping on a field that looks covered.</para>
///
/// <para><b>RE-TAKEN BY THE DRA-84 D3 REFRESH, AND THE REOPEN CONDITION DID NOT FIRE.</b> The
/// weekly refresh re-read the wiki — 240 more pages than the survey above was taken on — and
/// the profession count is still exactly <b>14</b>. That is the number this park was
/// conditional on, so it stays parked on re-measured evidence rather than on a year-old
/// reading. The recipes field moved with the corpus (899 pages naming one of the eight, all
/// eight distinct, up from 851) and is still the field carrying the answer the categories
/// do not.</para>
/// </summary>
public static class Tradeskills
{
    /// <summary>
    /// The generic Mastery ability, named so it can be REFUSED.
    ///
    /// <para>It is the ninth "… Mastery" in the General category and it is not a profession:
    /// its effect raises the specialization cap of professions you already have. A list built
    /// by pattern-matching "Mastery" in the AA catalog would have picked it up, which is why
    /// the refusal is a named constant with a test on it rather than an absence.</para>
    ///
    /// <para>Its effect text is also the closest thing the game's own data has to a
    /// definition of the set — it names seven of these eight in one parenthetical, and the
    /// one it leaves out is Alchemy.</para>
    /// </summary>
    public const string GenericMasteryAa = "Crafting Mastery";

    private static readonly TradeskillProfession[] Curated =
    [
        new(Tradeskill.Alchemy, "Alchemy", "Alchemy Mastery", "Skill Alchemy",
            ["Alchemy"]),
        new(Tradeskill.Baking, "Baking", "Baking Mastery", "Skill Baking",
            ["Baking"]),
        new(Tradeskill.Blacksmithing, "Blacksmithing", "Blacksmithing Mastery",
            "Skill Blacksmithing", ["Blacksmithing"]),
        new(Tradeskill.Brewing, "Brewing", "Brewing Mastery", "Skill Brewing",
            ["Brewing"]),
        new(Tradeskill.Fletching, "Fletching", "Fletching Mastery", "Skill Fletching",
            ["Fletching"]),
        // Three spellings from the wiki (see TradeskillProfession.LogNames) and one from
        // classic EverQuest, marked there.
        new(Tradeskill.Jewelcrafting, "Jewelcrafting", "Jewel Craft Mastery",
            "Skill Jewelcrafting",
            ["Jewelcrafting", "Jewelcraft", "Jewel Craft", "Jewelry Making"]),
        new(Tradeskill.Pottery, "Pottery", "Pottery Mastery", "Skill Pottery",
            ["Pottery"]),
        new(Tradeskill.Tailoring, "Tailoring", "Tailoring Mastery", "Skill Tailoring",
            ["Tailoring"]),
    ];

    /// <summary>The eight, in the enum's own order — read from <see cref="Curated"/> rather
    /// than re-listed beside it (trap 30).</summary>
    public static IReadOnlyList<TradeskillProfession> All => Curated;

    /// <summary>One profession. Throws for a value outside the enum, which is unreachable
    /// from a switch the must-list walks.</summary>
    public static TradeskillProfession For(Tradeskill skill) =>
        Curated.First(p => p.Skill == skill);

    /// <summary>
    /// Which profession the log meant, or null.
    ///
    /// <para>Null is the answer for every other skill the game announces — "1H Slashing",
    /// "Channeling", "Defense" — and it is what keeps the ledger profession-sized. <b>Whole
    /// string, never a substring</b>: the skills a substring rule would confuse are real
    /// skills with real standings, and a player's Blacksmithing reading somebody else's
    /// number is worse than no reading at all.</para>
    /// </summary>
    public static Tradeskill? Match(string? logSkillName)
    {
        var name = logSkillName?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var p in Curated)
            foreach (var alias in p.LogNames)
                if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                    return p.Skill;
        return null;
    }

    /// <summary>True when the log's skill name belongs to one of the eight — the ledger's
    /// admission filter, in the idiom <c>QuestLedgerStore.TrackFilter</c> already keeps ("the
    /// file stays quest-sized instead of hoarding every rat whisker").</summary>
    public static bool IsProfessionSkill(string? logSkillName) => Match(logSkillName) is not null;

    /// <summary>
    /// **THE ONE JOIN** from what the log has reported to where a profession stands.
    ///
    /// <para>Every surface that shows a standing calls this rather than folding the ledger
    /// itself (trap 4: one fact, one producer). A profession the log has never mentioned comes
    /// back with <see cref="TradeskillStanding.Known"/> false and a zero — <b>unknown is never
    /// drawn as a zero</b>, which is the whole reason the flag exists beside the number: "you
    /// have never raised this" and "you are at 0" are different sentences, and only one of them
    /// is true of a player who has been smithing for a month on a machine EQBuddy was not
    /// watching.</para>
    ///
    /// <para>Where two aliases of one profession have both been seen, the HIGHER value wins
    /// and carries its own timestamp. A skill value the game prints is the new total, so the
    /// largest one seen is the standing; the moment travels with it so a surface can say when
    /// rather than implying "now".</para>
    /// </summary>
    public static IReadOnlyList<TradeskillStanding> Standings(
        IEnumerable<(string Skill, int Value, DateTime At)> seen)
    {
        var best = new Dictionary<Tradeskill, (int Value, DateTime At)>();
        foreach (var (skill, value, at) in seen ?? [])
        {
            if (Match(skill) is not { } which || value <= 0) continue;
            if (best.TryGetValue(which, out var have) && have.Value >= value) continue;
            best[which] = (value, at);
        }

        return [.. Curated.Select(p => best.TryGetValue(p.Skill, out var v)
            ? new TradeskillStanding(p.Skill, v.Value, v.At)
            : new TradeskillStanding(p.Skill, 0, default))];
    }
}

/// <summary>Where one profession stands for this character, from the log's own skill-up
/// lines. <see cref="Known"/> is false when EQBuddy has never seen one — see
/// <see cref="Tradeskills.Standings"/> for why that is not a zero.</summary>
public sealed record TradeskillStanding(Tradeskill Skill, int Value, DateTime At)
{
    public bool Known => Value > 0;
}

/// <summary>
/// Which professions this character wants listed — <b>one store, filter semantics</b>
/// (DRA-71 D8).
///
/// <para><b>Absent means ALL EIGHT</b>, which is <c>UnlockPickStore</c>'s rule and the
/// deliberate opposite of <c>HelperGoalStore.Factions</c> beside it. The difference is what
/// the pick is FOR: a faction pick tells an engine which of hundreds of standings to weigh,
/// so "none picked" has to mean "none" or the engine would rank everything. This pick decides
/// which of eight rows a list draws, so "none picked" means the list a player who has never
/// touched the control should see — all of them.</para>
///
/// <para>Stored per character under <see cref="AppSettings.HelperProfessions"/> in the enum's
/// own spelling, so an unknown name is skipped rather than throwing: a profession removed from
/// the list should stop mattering, not break the room for whoever had ticked it.</para>
/// </summary>
public static class TradeskillPickStore
{
    /// <summary>The professions to list — every one of them when the player has never
    /// picked.</summary>
    public static IReadOnlyList<Tradeskill> Listed(AppSettings settings, string characterKey) =>
        ListedFrom(Picked(settings, characterKey));

    /// <summary>The same rule, applied to a pick that has already been read — <b>one producer
    /// for what "absent means all eight" means</b> (trap 4, DRA-149 D4).
    ///
    /// <para>It exists because the phone never sees <c>AppSettings</c>: the projection is handed
    /// the PICKED list over the wire and has to arrive at the same eight-or-fewer rows the
    /// desktop drew. Re-deriving that there with its own <c>Count == 0</c> would be the second
    /// copy of a rule whose whole content is which of two states means everything.</para>
    /// </summary>
    public static IReadOnlyList<Tradeskill> ListedFrom(IReadOnlyCollection<Tradeskill> picked) =>
        picked.Count == 0 ? [.. Enum.GetValues<Tradeskill>()] : [.. picked];

    /// <summary>What the store actually HOLDS — empty when nothing is picked. Separate from
    /// <see cref="Listed"/> because a picker has to draw ticks: "all of them" and "eight
    /// ticks" look identical on screen and are different states, and only one of them is
    /// undoable.</summary>
    public static IReadOnlyList<Tradeskill> Picked(AppSettings settings, string characterKey)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return [];
        if (!settings.HelperProfessions.TryGetValue(characterKey, out var names)) return [];
        return [.. Enum.GetValues<Tradeskill>().Where(t =>
            names.Any(n => string.Equals(n, t.ToString(), StringComparison.OrdinalIgnoreCase)))];
    }

    /// <summary>True when this profession is one the room should draw.</summary>
    public static bool IsListed(IReadOnlyList<Tradeskill> listed, Tradeskill skill) =>
        listed.Contains(skill);

    /// <summary>Turn one profession on or off. An empty result REMOVES the key rather than
    /// storing an empty list — "never picked" and "picked nothing" are the same state here,
    /// and two spellings of one state is a distinction a later reader would eventually act
    /// on.</summary>
    public static void Toggle(AppSettings settings, string characterKey, Tradeskill skill)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return;
        var picked = Picked(settings, characterKey).ToList();
        if (!picked.Remove(skill)) picked.Add(skill);
        if (picked.Count == 0) settings.HelperProfessions.Remove(characterKey);
        else settings.HelperProfessions[characterKey] = [.. picked.Select(p => p.ToString())];
    }
}
