using System.Globalization;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// Which island of the Plane of Sky a checklist step is on, read out of the step's own
/// <c>Source</c> prose.
///
/// **Why it is a parser and not a field.** A Reddit player asked for Sky steps to be grouped
/// by island (David, 2026-08-23: *"where we know a step is on a specific island, list those
/// steps by it… sorted by island numerically"*), and the obvious first question is whether
/// the catalog already carries one. It does not — but the fact is there, written by hand, in
/// five different shapes across 223 steps:
///
/// <list type="bullet">
/// <item><c>Isle 4: Keeper of Souls</c> — digits and a colon, the common form.</item>
/// <item><c>Isle four - griffons and pegasus</c> — the number spelled out, and a dash.</item>
/// <item><c>Isle 1.5: Noble Dojorn</c> — Sky's half-island really is called 1.5.</item>
/// <item><c>Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble
/// Dojorn</c> — 22 steps name THREE, because the item drops from any of them.</item>
/// <item><c>Trash mobs</c> — 95 of them, and this is not missing data: Wind Runes drop
/// anywhere on the plane, so "no island" is the true answer and the step keeps the
/// ungrouped presentation it has always had.</item>
/// </list>
///
/// **Since DRA-164 it reads a SECOND body of prose, and that is where the sixth shape came
/// from.** The island view asks the same question of the GUIDE catalog — a stage's name
/// (`Isle 6: Bazzt Zzzt`), falling back to an objective's `Where`. Those `Where` strings are
/// written by a different hand and one of them puts a list under a single isle word:
/// *"Plane of Sky - Isles 1.5, 4 and 8 respectively."* — 22 steps, see <see cref="Parse"/>.
/// The five shapes above are untouched by it, and no classic `Source` string in
/// <c>SkyQuestDefaults</c> uses the list shape at all (a committed negative says so).
///
/// **Nothing here writes back into the catalog.** Curated data is never auto-written, and a
/// parse that guessed wrong would be a wrong island printed with the same confidence as a
/// right one. When the prose does not clearly name an island this returns nothing, which the
/// layout reads as "leave it where it was".
/// </summary>
public static partial class SkyIslands
{
    /// <summary>The half-island, spelled the way the wiki spells it. Sky's second stop is
    /// genuinely numbered 1.5, which is the reason every island number here is a
    /// <c>double</c> rather than an <c>int</c> — and the reason sorting them "numerically"
    /// is a real instruction rather than an obvious one.</summary>
    public const double HalfIsland = 1.5;

    private static readonly Dictionary<string, double> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8,
    };

    // "Isle 4", "Isle 1.5", "Isle eight", "Island 6" — the word, then a number or a name.
    // Anchored on the word so a stray number in prose ("2 spawns") cannot become an island.
    //
    // **`rest` is the sixth shape, and it is the only one DRA-164 added** (P3, scope-locked).
    // The guide catalog writes the Efreeti drop's location as *"Plane of Sky - Isles 1.5, 4
    // and 8 respectively."* — one isle word governing a LIST — and the five shapes above
    // captured only the number touching the word, so all 22 of those steps parsed to `[1.5]`
    // and would have been filed on the half-island alone. That is the failure this file's own
    // summary calls out: a wrong island printed with the same confidence as a right one, and
    // the only one of the failures here that a reader cannot notice, because it looks like an
    // answer. `SkyIslandsTests.TheShippedParserReadTheEfreetiDropAsOneIsland` holds the
    // before-picture so this is a fix that was shown to fix something (trap 34).
    //
    // **The tail is DIGITS ONLY, deliberately.** "Isle four - griffons and pegasus" is a real
    // catalog string and an `and` arm that accepted words would read "pegasus" as an island;
    // the same goes for "Isle 7: sphinxes, drakes and undine spirits". A spelled-out number may
    // still LEAD, because that shape is already in the catalog — a spelled-out number in a
    // list is not, and inventing an admitted shape no page uses is how the next reader learns
    // the wrong rule.
    [GeneratedRegex(
        @"\bisles?\b\s*\.?\s*(?<n>\d+(?:\.\d+)?|[a-z]+)"
        + @"(?<rest>(?:\s*(?:,|\band\b|&)\s*\d+(?:\.\d+)?)*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsleRx();

    // The numbers inside a matched tail. Only ever run over `rest`, which the regex above has
    // already proved is a comma/and list of numbers and nothing else.
    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex TailNumberRx();

    /// <summary>Every island a step's <c>Source</c> names, in ascending order, without
    /// duplicates. Empty when it names none — which is a real answer, not a failure.</summary>
    public static IReadOnlyList<double> Parse(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return [];
        var found = new List<double>();
        foreach (Match m in IsleRx().Matches(source))
        {
            var token = m.Groups["n"].Value;
            double n;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var digits))
                n = digits;
            else if (Words.TryGetValue(token, out var word))
                n = word;
            else continue;   // "Isle of …" and anything else that is not a number
            if (!found.Contains(n)) found.Add(n);

            // The rest of the list this isle word governs. Empty for all five older shapes,
            // which is why they come back byte for byte.
            foreach (Match tail in TailNumberRx().Matches(m.Groups["rest"].Value))
                if (double.TryParse(tail.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var more) && !found.Contains(more))
                    found.Add(more);
        }
        found.Sort();
        return found;
    }

    /// <summary>
    /// A set of islands in one sortable, round-trippable string — the value
    /// <c>QuestChecklistRow.IslandKey</c> carries.
    ///
    /// <para>Zero-padded so "10" could never sort between "1" and "2", and ordered so the set
    /// starting on the lowest island leads. The empty set is the empty string, which is the
    /// default on every row that names no island and on every Epic row.</para>
    ///
    /// <para><b>It lives here, with its inverse, because DRA-164 gave it a second reader.</b>
    /// It was <c>QuestChecklistLayout</c>'s private tie-breaker — never shown, only ever
    /// compared — and the island view needs the SET back to decide which island headings a row
    /// belongs under. Parsing the HEADING back would be the trap 4 shape twice over: a fact
    /// stored in one place and recovered from another, and that other a string written for a
    /// human to read.</para>
    /// </summary>
    public static string SetKey(IReadOnlyList<double> islands) =>
        string.Join("|", islands.Select(i => i.ToString("00.0", CultureInfo.InvariantCulture)));

    /// <summary>The islands a <see cref="SetKey"/> was built from. Round-trips, and answers
    /// empty for "" and for anything it cannot read exactly — a key it cannot read is a row
    /// with no island rather than a guess at one.</summary>
    public static IReadOnlyList<double> FromSetKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var found = new List<double>();
        foreach (var part in key.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                return [];
            if (!found.Contains(n)) found.Add(n);
        }
        found.Sort();
        return found;
    }

    /// <summary>The heading a group of steps sits under. Kept here so the two desktops and
    /// EQBuddy Mobile cannot spell the same island three ways (#184's rule).</summary>
    public static string Heading(double island) => "Island " + Number(island);

    /// <summary>
    /// The heading for a step that names SEVERAL islands — **and it names which** (David,
    /// 2026-08-23, after seeing the first build: *"for the 'Several Islands' ones, please list
    /// which. IE: 1, 3, and 7"*).
    ///
    /// The first cut said only "Several islands", which told a player the one thing they
    /// already knew from the absence of a number and none of what they needed. A player
    /// standing on Island 4 can now see at a glance whether a step is reachable from where
    /// they are, without opening anything.
    ///
    /// It also stops being ONE bucket: two steps with different island sets get different
    /// headings and sit apart, which is the honest grouping — they were only ever together
    /// because the heading could not tell them apart.
    ///
    /// These are still not "on a specific island", so they keep their place after the
    /// numbered groups unless the player asks otherwise (see
    /// <see cref="AppSettings.SkyStepsUnderEveryIsland"/>).
    /// </summary>
    /// <remarks>
    /// **The separator is a middle dot, and the reason is worth keeping** (David, 2026-08-23).
    /// It was commas first — "Islands 1.5, 4, and 8" — and he read that back as *four*
    /// islands, "1, 5, 4, 8", because Sky's half-island puts a decimal point inside a
    /// comma-separated list. He had specified the comma format himself an hour earlier, which
    /// is the whole argument: if the person who chose it misreads it, a player who does not
    /// yet know there IS a half-island has no chance.
    ///
    /// "·" is already this app's list separator — the checklist rows beneath these headings
    /// use it, so do the raid rows — and it cannot be mistaken for part of a number.
    /// </remarks>
    public static string SeveralHeading(IReadOnlyList<double> islands) => islands.Count switch
    {
        0 => AnywhereHeading,
        1 => Heading(islands[0]),
        _ => "Islands " + string.Join(" · ", islands.Select(Number)),
    };

    /// <summary>An island's number on its own, without the word — "4", "1.5". One formatter,
    /// so a heading and a list of them cannot disagree about how 1.5 is written.</summary>
    private static string Number(double island) =>
        island.ToString(island % 1 == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);

    /// <summary>
    /// The clauses of a multi-island source, reordered so the islands ascend (David,
    /// 2026-08-23: *"in the 'Several Islands', order the islands numerically"*).
    ///
    /// The heading was already sorted — <see cref="Parse"/> sorts — but the PROSE underneath
    /// it was not, because it is hand-written and the catalog happens to say *"Isle eight: the
    /// Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn"*. So the row read
    /// 8, 4, 1.5 directly beneath a heading reading 1.5, 4, 8. Two orderings of one fact, one
    /// line apart.
    ///
    /// **Only the ORDER of whole clauses changes; not a word inside one.** Each clause is
    /// self-contained ("Isle N: which mob"), which is what makes this safe to do to curated
    /// prose — nothing is rewritten, reworded or dropped. A clause naming no island keeps its
    /// place at the end rather than being sorted to an island it never claimed.
    /// </summary>
    public static string OrderClausesByIsland(string? source)
    {
        var text = (source ?? "").Trim();
        if (text.Length == 0 || !text.Contains(';')) return text;

        var clauses = text.Split(';')
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();
        if (clauses.Count < 2) return text;

        // OrderBy is a STABLE sort in .NET, so clauses that name no island — all sorted to
        // the same sentinel — keep the order the editor wrote them in.
        var ordered = clauses
            .OrderBy(c => Parse(c) is [var first, ..] ? first : double.MaxValue)
            .ToList();
        return ordered.SequenceEqual(clauses) ? text : string.Join("; ", ordered);
    }

    /// <summary>
    /// The source prose with a leading "Isle N:" / "Isle N -" removed, for a row that is
    /// already sitting under that island's heading.
    ///
    /// **Added the same day the grouping was**, because the grouping created the redundancy:
    /// a row under "Island 6" was reading *"Josin Faithbringer · Isle 6: Bazzt Zzzt"*, saying
    /// the island twice in the space of eight words. What is left — the mob that drops it —
    /// is the half the row was always for.
    ///
    /// **Only the leading label goes, and only when there is exactly one.** A step naming
    /// three islands keeps every word: it sits under "Several islands", so the three names
    /// are the only place a player can learn where to go. Same reason nothing is stripped
    /// when the prose has no such label at all.
    /// </summary>
    public static string WithoutIslePrefix(string? source)
    {
        var text = (source ?? "").Trim();
        if (text.Length == 0 || Parse(text).Count != 1) return text;
        var m = LeadingIsleRx().Match(text);
        if (!m.Success) return text;
        var rest = text[m.Length..].Trim();
        // Never strip a row down to nothing: "Isle 6" on its own IS the whole fact, and an
        // empty detail column would read as data we failed to load.
        return rest.Length > 0 ? rest : text;
    }

    // The label at the START only, with the separator that follows it (":" or "-").
    [GeneratedRegex(@"^\s*isles?\s*\.?\s*(?:\d+(?:\.\d+)?|[a-z]+)\s*[:\-–]\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingIsleRx();

    /// <summary>The heading for steps whose source names no island at all. Worded as what it
    /// IS rather than as an absence: 95 of 223 steps are here because Wind Runes drop
    /// anywhere on the plane, and "Unknown" would call the catalog incomplete when it is
    /// telling the truth.</summary>
    public const string AnywhereHeading = "Anywhere on the plane";
}
