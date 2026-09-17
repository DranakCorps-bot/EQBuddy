namespace EQBuddy.Core;

/// <summary>
/// One ingredient a profession needs, and where the wiki says it drops (DRA-149 D3, plan P4).
/// </summary>
/// <param name="Skill">Which of the eight this material belongs to. One material can belong to
/// several — <c>Water Flask</c> feeds Brewing, Pottery and Blacksmithing — and it arrives once
/// per profession rather than once with a list, because the pick narrows on the profession and
/// a row that carried all of them would have to be re-filtered at every reader.</param>
/// <param name="Item">The material, as the catalog titles it.</param>
/// <param name="Recipes">The recipes the page lists it under, in the page's own order — the
/// evidence that this item belongs to this profession at all, so a surface can show WHY a
/// material is on the list rather than asking the player to take it on faith.</param>
/// <param name="Zones">Where it drops: the catalog's own <c>DropZones</c>, minus the entries
/// that are not places (<see cref="TradeskillMaterials.IsPlace"/>).</param>
/// <param name="Mobs">Per zone, the creatures the page named — the catalog's own annotation,
/// never a second copy of <paramref name="Zones"/> (trap 4).</param>
public sealed record TradeskillMaterial(
    Tradeskill Skill,
    string Item,
    IReadOnlyList<string> Recipes,
    IReadOnlyList<string> Zones,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Mobs)
{
    /// <summary>The creatures this page named in one zone, or empty. Same shape and same
    /// contract as <c>GearUpgrade.MobsIn</c>, so the Helper's who rule reads both the same
    /// way.</summary>
    public IReadOnlyList<string> MobsIn(string zone) =>
        Mobs.TryGetValue(zone, out var mobs) ? mobs : [];
}

/// <summary>
/// **WHICH ITEMS A PROFESSION NEEDS, READ OFF THE CATALOG'S <c>Recipes</c> COLUMN**
/// (DRA-149 D3, plan P4 — the Founder's FAIL item 3a).
///
/// <para><b>This un-parks the DRA-71 D8 arithmetic, and the reason it can is that the park
/// measured the wrong COLUMN.</b> That survey counted <c>[[Category:…]]</c> tags and found 14
/// of 11,197 pages naming a profession — true, re-taken on the D3 refresh, and about a field
/// that answers a different question. The <c>Recipes</c> field is the one that carries the
/// answer: 1,276 records ship one, and the eight professions appear in them as HEADINGS.
/// <c>scripts/dra149-materials-survey.py</c> is the committed record of the measurement.</para>
///
/// <para><b>The record IS the ingredient, and that is the finding the slice's opening survey
/// was declared to make</b> (the plan's escalation seam). An item page's <c>recipes</c> field
/// names the recipes the item is USED IN, so a page carrying a profession heading is a
/// material for it. The exhibit, both ways, from the shipped file: <c>A Giant Blood Sac</c>
/// carries <c>["Brewing", "Legion Lager (Trivial: 36)"]</c> and drops in East Cabilis; the
/// PRODUCT, <c>Legion Lager</c>, carries no recipes and no drop zones at all. Measured over
/// the corpus: of the 905 records naming one of the eight, 165 are themselves named as a
/// recipe output somewhere (the intermediates), and 150 of those 165 drop nowhere — so they
/// leave through the zone gate below without a rule of their own. The 15 that are both an
/// output and a real drop are all genuinely farmable (<c>Low Quality Bear Skin</c>,
/// <c>Loaf of Bread</c>, the ores), so naming them is true either way and the slice did not
/// need to escalate.</para>
///
/// <para><b>It reads the SHIPPED catalog and fetches nothing</b>, like every other reader in
/// this file's neighbourhood. Nothing here is written back: the curated
/// <see cref="Tradeskills"/> list stays the only statement about which professions exist, and
/// this is a fold over data that already shipped.</para>
///
/// <para><b>The four real skills that are not professions here stay out, by the same curated
/// rule and with the same reason</b> — Spell Research, Tinkering, Make Poison and Fishing all
/// appear as recipe headings (138, 61, 22 and 8 records), and none has a Mastery AA. The
/// admission test is <see cref="Tradeskills.Match"/>, the one whole-string matcher this repo
/// already keeps, so a heading is admitted by the same code that admits a skill-up line rather
/// than by a second list that could drift from it (trap 4).</para>
/// </summary>
public static class TradeskillMaterials
{
    /// <summary>
    /// **A <c>DropZones</c> ENTRY IS NOT ALWAYS A PLACE, AND A NON-PLACE IS NOT A CAMP.**
    ///
    /// <para>Two shapes get refused. The first is the promoter garbage DRA-84 D4 measured on
    /// the gear side — <c>}}</c>, <c>:* Dread</c>, a category tag, an <c>ITEM REMOVED FROM
    /// GAME</c> note — which is filed as its own V2 card and not repaired here. On the gear
    /// side those fell out for free, because a non-place also names no creature and the who
    /// rule removed it; <b>that luck does not hold here</b>, which is why this is an explicit
    /// rule rather than an inherited side effect.</para>
    ///
    /// <para>The second is the wiki's own vague placeholders, and they are the reason the rule
    /// exists at all: 12 of the eight professions' (material, zone) pairs say
    /// <c>Various Zones</c>, and every one of them DOES name creatures — so the who rule would
    /// have kept them and the room would have offered "Various Zones" as somewhere to go. That
    /// is the Rathe class of failure with a different cause, and this repo has already named
    /// this exact vocabulary once: <c>MoteCatalogSurveyTests</c> pins
    /// <c>Various Zones</c> / <c>Unknown</c> / <c>D3+ Zones</c> as the three values that make
    /// the mote catalog arm non-existent.</para>
    /// </summary>
    public static bool IsPlace(string zone)
    {
        var trimmed = zone.Trim();
        if (trimmed.Length == 0) return false;
        foreach (var vague in Vague)
            if (trimmed.Equals(vague, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var prefix in NotAPlacePrefixes)
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return trimmed != "/";
    }

    /// <summary>The wiki's own placeholders for "we did not record where" — the same three
    /// <c>MoteCatalogSurveyTests</c> names.</summary>
    private static readonly string[] Vague = ["Various Zones", "Unknown", "D3+ Zones", "n/a", "?"];

    /// <summary>The promoter's non-place shapes, from the DRA-84 D4 survey.</summary>
    private static readonly string[] NotAPlacePrefixes =
        ["}}", ":*", "Category:", "N O T _", "NOT_", "ITEM REMOVED", "<br"];

    /// <summary>
    /// Split one page's <c>Recipes</c> list into its profession sections.
    ///
    /// <para>A HEADING is an entry that names one of the eight and carries no recipe of its
    /// own. The trivial marker is what tells the two apart — <c>"Legion Lager (Trivial: 36)"</c>
    /// is a recipe and <c>"Brewing"</c> is the heading over it — and the <c>=</c> strip is part
    /// of the reading rather than a repair beside it, because five pages spell the heading in
    /// raw wikitext (<c>== Tailoring ==</c>).</para>
    ///
    /// <para>Lines before the first heading belong to nobody and are dropped: the page put them
    /// under no profession, and assigning them to the first one that happens to follow would be
    /// inventing the association this whole reader exists to avoid.</para>
    /// </summary>
    internal static Dictionary<Tradeskill, List<string>> Sections(IReadOnlyList<string>? recipes)
    {
        var sections = new Dictionary<Tradeskill, List<string>>();
        if (recipes is null) return sections;

        Tradeskill? current = null;
        foreach (var entry in recipes)
        {
            if (HeadingFor(entry) is { } heading)
            {
                current = heading;
                if (!sections.ContainsKey(heading)) sections[heading] = [];
                continue;
            }
            if (current is { } skill) sections[skill].Add(entry.Trim());
        }
        return sections;
    }

    /// <summary>Which profession this entry is the heading for, or null. Null covers both "a
    /// recipe line" and "a heading for a skill that is not one of the eight".</summary>
    internal static Tradeskill? HeadingFor(string entry)
    {
        if (entry.Contains("(Trivial:", StringComparison.OrdinalIgnoreCase)) return null;
        return Tradeskills.Match(entry.Trim().Trim('=').Trim());
    }

    /// <summary>
    /// Every material the picked professions need, in the catalog's own order.
    /// </summary>
    /// <param name="catalog">The shipped catalog. Null answers an empty list rather than
    /// throwing — a fixture without one is a test, not an error, which is
    /// <c>HelperInputs.Items</c>' own rule.</param>
    /// <param name="picked">Which professions to read. <b>Empty means ALL EIGHT</b> — filter
    /// semantics, which is what <see cref="TradeskillPickStore"/> already carries and what the
    /// professions block above these rows already says out loud.</param>
    /// <param name="droppedOnly">Keep only materials with somewhere real to farm them. The
    /// engine wants that; a survey wants the whole list, and the difference is a parameter
    /// rather than two folds.</param>
    public static IReadOnlyList<TradeskillMaterial> From(
        ItemCatalog? catalog, IReadOnlyCollection<Tradeskill> picked, bool droppedOnly = true)
    {
        if (catalog is null) return [];
        var wanted = picked.Count > 0
            ? new HashSet<Tradeskill>(picked)
            : [.. Tradeskills.All.Select(p => p.Skill)];

        var materials = new List<TradeskillMaterial>();
        foreach (var record in catalog.All)
        {
            var sections = Sections(record.Recipes);
            if (sections.Count == 0) continue;

            var zones = (record.DropZones ?? []).Where(IsPlace).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (droppedOnly && zones.Count == 0) continue;

            var mobs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in zones)
                if (record.DropMobs is { } named && named.TryGetValue(zone, out var who) && who.Count > 0)
                    mobs[zone] = who;

            foreach (var (skill, recipes) in sections)
            {
                if (!wanted.Contains(skill)) continue;
                materials.Add(new TradeskillMaterial(skill, record.Name, recipes, zones, mobs));
            }
        }
        return materials;
    }
}
