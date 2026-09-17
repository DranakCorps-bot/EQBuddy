using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>One merchant the wiki named, and the zone whose page named it.</summary>
/// <param name="Zone">The wiki's own zone title — the same key <see cref="ZoneLevels"/> uses,
/// so a surface that already knows how to draw a zone name does not learn a second one.</param>
/// <param name="Line">The map-key entry, <b>verbatim</b>, with wikitext folded to what a reader
/// sees. Nothing here is re-worded or shortened. Where a vendor has a name the page gave it,
/// the name is IN this sentence — see <see cref="ZoneMerchants"/> for why it is not a field.
/// </param>
public sealed record MerchantLine(string Zone, string Line);

/// <summary>
/// **WHERE THE WIKI SAYS THE VENDORS ARE** (DRA-149 D4, plan P5 — the second half of the
/// Founder's FAIL item 3: *"OR named vendors + where they are when shopping vendors"*).
///
/// <para>D3 answered the first half — <see cref="TradeskillMaterials"/> over the item catalog's
/// <c>Recipes</c> column, for the zones and creatures a material drops from. This is the other
/// path to the same bench: an ingredient you can simply BUY, and the shop that has it. The two
/// are deliberately different surfaces over different data, because they are different plans
/// for an evening.</para>
///
/// <para><b>The data is on the ZONE pages, not the item pages.</b> An item page mentions a
/// vendor on 3 of 11,196; a zone page writes a map key under its map image, and 50 of the 118
/// committed ones name a merchant in it. <c>scripts/harvests/eqlwiki/merchants-transform.py</c>
/// transcribes them out of the COMMITTED wikitext cache — it <b>fetches nothing</b> — into
/// <c>Data/ZoneMerchants.json</c>, and <c>merchants-report.md</c> lists every line it refused.
/// </para>
///
/// <para><b>Lines are TRANSCRIBED, never summarised.</b> *"Everhot Forge - Merchants selling
/// Blunt and Sharp Weapons, ... (Bndainy Everhot), Jewelry Metal and Rare Gems, Forge
/// Outside"* is a better answer than any sentence generated from a parse of it, and unlike a
/// generated one it is checkable against the page. That is the standing eqlwiki rule and it is
/// also the only claim this class can honestly make.</para>
///
/// <para><b>The vendor's NAME is not a field, on purpose.</b> The links inside these lines are
/// not all NPCs — <c>[[Cleric]] Guild</c>, <c>[[Rogue]] Guild Members</c> and
/// <c>[[Kafia Ratsbone]]</c> sit in one list on one page — so a rule that lifted them into a
/// "vendor" field would print "Cleric" as a merchant, and a rule that tried to tell them apart
/// would be guessing about the wiki's link targets. The name stays where the page put it: in
/// the sentence.</para>
///
/// <para><b>This class does no ranking and knows nothing about a player.</b> It answers what
/// the page said, in the idiom <see cref="ZoneLevels"/> keeps — the judgement lives with the
/// reader.</para>
/// </summary>
public sealed class ZoneMerchants
{
    private readonly Dictionary<string, IReadOnlyList<string>> _byZone =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Zones whose map key names at least one merchant.</summary>
    public int ZoneCount => _byZone.Count;

    /// <summary>Admitted merchant lines across every zone.</summary>
    public int LineCount { get; }

    /// <summary>Zones whose page was read and whose map key names no merchant. Beside
    /// <see cref="ZoneCount"/> because "we read the page and it says nothing" and "we have never
    /// read a page for this zone" are different sentences — <see cref="ZoneLevels"/>'s
    /// <c>NoBand</c> rule, kept.</summary>
    public int SilentZoneCount { get; }

    public ZoneMerchants() { }

    public ZoneMerchants(IReadOnlyDictionary<string, IReadOnlyList<string>> zones,
                         IReadOnlyCollection<string> silent)
    {
        foreach (var (zone, lines) in zones)
        {
            if (lines.Count == 0) continue;
            _byZone[zone] = lines;
            _read.Add(zone);
            LineCount += lines.Count;
        }
        foreach (var zone in silent) _read.Add(zone);
        SilentZoneCount = _read.Count - _byZone.Count;
    }

    /// <summary>Every zone with at least one merchant line, alphabetized — <c>ZoneLevels</c>'s
    /// <c>BandedZones</c> one catalog along. It is what lets a guard walk the whole corpus
    /// instead of a hand-written list of zone names that stops covering it the day the wiki
    /// adds one (trap 30).</summary>
    public IReadOnlyList<string> Zones =>
        [.. _byZone.Keys.OrderBy(z => z, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether a zone page was read at all. <c>false</c> is "we know nothing about this
    /// place", which is not the same answer as an empty <see cref="LinesFor"/>.</summary>
    public bool HasRead(string zone) => _read.Contains(zone.Trim());

    /// <summary>Every merchant line the wiki wrote for one zone, in the page's own order.</summary>
    public IReadOnlyList<string> LinesFor(string zone) =>
        _byZone.TryGetValue(zone.Trim(), out var lines) ? lines : [];

    /// <summary>
    /// The merchant lines that name one profession's materials or tools, zone by zone in
    /// alphabetical order.
    ///
    /// <para>The match is <see cref="Keywords"/>, and the LINE is what a surface shows — so a
    /// player reads the wiki's own sentence and can see for themselves what is on offer. The
    /// keyword is why EQBuddy thought the line was relevant; it is not a claim about what the
    /// merchant stocks.</para>
    /// </summary>
    public IReadOnlyList<MerchantLine> For(Tradeskill skill)
    {
        var keywords = Keywords(skill);
        var hits = new List<MerchantLine>();
        foreach (var zone in _byZone.Keys.OrderBy(z => z, StringComparer.OrdinalIgnoreCase))
            foreach (var line in _byZone[zone])
                if (Names(line, keywords))
                    hits.Add(new MerchantLine(zone, line));
        return hits;
    }

    /// <summary>How many distinct zones have a line for this profession — what a face says
    /// before a player opens the list.</summary>
    public int ZonesFor(Tradeskill skill) =>
        For(skill).Select(m => m.Zone).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>
    /// **THE MATCH RULE: a keyword at the START of a word, case-insensitively.**
    ///
    /// <para>A keyword may run into the rest of the word to its right — <c>gem</c> matches
    /// "Gems" and "Gemstones", <c>arrow-making</c> matches "Arrow-making" — but may not begin
    /// inside one. That single anchor is the difference between a usable table and a useless
    /// one, and it was measured rather than assumed: with a plain substring rule
    /// <c>ore</c> matched <i>"Cooking and L<b>ore</b> Books"</i> and filed a cookbook merchant
    /// under Blacksmithing.</para>
    ///
    /// <para>It is deliberately NOT a whole-word rule either. English plurals would then need a
    /// second row per keyword (<c>gem</c>/<c>gems</c>, <c>pelt</c>/<c>pelts</c>), and a curated
    /// table whose rows are mostly inflections of each other is one nobody will keep
    /// accurate.</para>
    /// </summary>
    internal static bool Names(string line, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            var from = 0;
            while (from <= line.Length - keyword.Length)
            {
                var at = line.IndexOf(keyword, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;
                if (at == 0 || !char.IsLetter(line[at - 1])) return true;
                from = at + 1;
            }
        }
        return false;
    }

    /// <summary>
    /// **CURATED, hand-written, one row per profession — and the words are the WIKI's, not
    /// ours.**
    ///
    /// <para>Every keyword below was read out of the 359 admitted lines before it was written
    /// down, which is why the list is short and lopsided: <c>fletching</c> needs five rows
    /// because the pages spell that trade five ways ("Fletching Supplies", "Bowyer",
    /// "Bowmaking", "Arrow-making", "Arrow Fletching"), and <c>brewing</c> needs two because
    /// they mostly just say "Brewing Supplies". A list invented from what a profession
    /// <i>ought</i> to buy would have been longer and would have matched less.</para>
    ///
    /// <para><b>THREE ROWS WERE WRITTEN FROM THAT INSTINCT AND THE GUARD DELETED THEM.</b> The
    /// first draft carried <c>spices</c>, <c>metal bit</c> and <c>pelt</c> — all three are real
    /// trade supplies, and not one of them appears on any shipped line.
    /// <c>EveryCuratedKeywordMatchesSomethingInTheShippedCatalog</c> named them, which is
    /// precisely the failure that has no symptom: a dead keyword matches nothing, contributes
    /// nothing, and leaves a curated table looking longer and better-researched than it is
    /// (trap 78). A keyword added here without a line behind it fails the build.</para>
    ///
    /// <para><b>The crafting STATIONS are deliberately absent</b> — no oven, kiln, forge, loom
    /// or brew barrel. They appear on a third of these lines, they are not something a merchant
    /// sells, and matching on them would file almost every shop in Norrath under almost every
    /// profession. Where a station rides a line that matched for another reason it is still
    /// visible, because the line is shown whole.</para>
    ///
    /// <para>This is the same curation rule <see cref="Tradeskills"/> itself keeps: a machine may
    /// write a file beside a curated one, never into it. The transform publishes the lines; the
    /// join to a profession is eight rows a human checked.</para>
    /// </summary>
    public static IReadOnlyList<string> Keywords(Tradeskill skill) => skill switch
    {
        // "Alchemy Supplies", "Alchemy Items", "alchemy ingredient merchants". Medicine bags are
        // the trade's own container and the pages sell them by that name.
        Tradeskill.Alchemy => ["alchemy", "medicine bag"],
        // "Baking Supplies" and "Cooking Supplies" are the same shelf in the wiki's usage, and
        // one page says "Pastries" instead.
        Tradeskill.Baking => ["baking", "cooking", "pastries"],
        // Bare "smithy" is OUT: it is a shop NAME on three pages ("The Smithy", "Gord's
        // Smithy"), each selling weapons rather than supplies. "Smithy Hammers" is the tool.
        Tradeskill.Blacksmithing =>
            ["blacksmithing", "smithing", "ore", "smithy hammer", "sharpening stone"],
        // Grapes are the one raw ingredient these pages name; everything else says "Brewing".
        // "Alcohol" is out — it is the PRODUCT, and it is on forty lines.
        Tradeskill.Brewing => ["brewing", "grapes"],
        // Bare "arrow" is OUT: "Bows and Arrows" is a weapon rack, not a fletcher's shelf.
        Tradeskill.Fletching =>
            ["fletching", "bowyer", "bowmaking", "arrow-making", "arrow fletching"],
        // "Gems", "Gemstones", "Jewelry Supplies", "Gems for Jewelcraft".
        Tradeskill.Jewelcrafting => ["gem", "jewelry", "jewelcraft"],
        // "Pottery Supplies", "Pottery Sketches", "Clay", "Firing Sheets".
        Tradeskill.Pottery => ["pottery", "clay", "firing sheet"],
        // "Tailoring Supplies", "Sewing Kits", "Silk".
        Tradeskill.Tailoring => ["tailoring", "sewing", "silk"],
        _ => [],
    };

    public static ZoneMerchants LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.ZoneMerchants.json");
        if (stream is null) return new ZoneMerchants();
        try
        {
            var root = JsonSerializer.Deserialize<Root>(stream);
            if (root is null) return new ZoneMerchants();
            return new ZoneMerchants(
                root.Zones?.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value) ?? [],
                root.NoLines ?? []);
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return new ZoneMerchants();
        }
    }

    private sealed class Root
    {
        public string? Source { get; set; }
        public Dictionary<string, List<string>>? Zones { get; set; }
        public List<string>? NoLines { get; set; }
    }

    private static ZoneMerchants? _default;
    private static readonly object DefaultLock = new();

    /// <summary>Lazy, like every other shipped catalog: the parse happens on first use.</summary>
    public static ZoneMerchants Default
    {
        get
        {
            if (_default is { } d) return d;
            lock (DefaultLock) return _default ??= LoadEmbedded();
        }
    }
}
