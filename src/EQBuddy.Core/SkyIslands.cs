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
    [GeneratedRegex(@"\bisles?\b\s*\.?\s*(?<n>\d+(?:\.\d+)?|[a-z]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsleRx();

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
        }
        found.Sort();
        return found;
    }

    /// <summary>The heading a group of steps sits under. Kept here so the two desktops and
    /// EQBuddy Mobile cannot spell the same island three ways (#184's rule).</summary>
    public static string Heading(double island) =>
        "Island " + island.ToString(island % 1 == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);

    /// <summary>The heading for steps that name SEVERAL islands. They are not unknown — we
    /// know all three places — so they never fall in with the trash-mob steps; and they are
    /// not on "a specific island", so by David's own wording they do not join a numbered
    /// group unless the player asks for that (see
    /// <see cref="AppSettings.SkyStepsUnderEveryIsland"/>).</summary>
    public const string SeveralHeading = "Several islands";

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
