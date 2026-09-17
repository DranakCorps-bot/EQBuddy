using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>Parsed eqlwiki item page, ready for display.</summary>
public sealed class ItemInfo
{
    public string Name { get; set; } = "";
    /// <summary>The in-game item window text, line by line (MAGIC ITEM, Slot, DMG…).</summary>
    public List<string> StatsLines { get; set; } = [];
    /// <summary>Vendor value as compact coin text ("5g 8s 2c"), or "" when unlisted.</summary>
    public string MerchantValue { get; set; } = "";

    /// <summary>
    /// **WHAT THE PAGE SAID THE PRICE WAS QUOTED AT** — "with CHA : 80 and faction at
    /// Indifferently", or "" where it stated no condition (DRA-71 D7, plan P9).
    ///
    /// <para><b>It exists because the survey found the number is not a property of the
    /// item.</b> A vendor price in EQ moves with the seller's Charisma and their faction with
    /// the merchant, and 262 of the 975 cached pages carrying a <c>merchant_value</c> say so
    /// in their own heading — at a Charisma that differs per page (80, 72, 111). Until this
    /// slice, <see cref="MerchantValue"/> kept the number and threw the heading away, which
    /// turns one player's quote into a fact about the object.</para>
    ///
    /// <para>So the condition travels with the value, and any surface that prints the one
    /// prints the other. An unconditional page produces "", and an unanswered question draws
    /// nothing (trap 73) — it does not get an invented caveat either.</para>
    /// </summary>
    public string MerchantCondition { get; set; } = "";
    public List<(string Zone, List<string> Mobs)> DropsFrom { get; set; } = [];
    public List<(string Zone, string Merchant, string Loc)> SoldBy { get; set; } = [];
    public List<string> Quests { get; set; } = [];
    public List<string> Recipes { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public string WikiUrl { get; set; } = "";

    /// <summary>The page's own word for it: the in-game stats block prints "QUEST ITEM"
    /// even when an editor forgot the Quest Items category tag (Phosphorous Powder,
    /// wizen's #75 — flagged in the infobox, absent from the category). The stats
    /// block is what the game shows, so it outranks the category's bookkeeping.</summary>
    public bool QuestFlagged =>
        StatsLines.Any(l => l.Contains("QUEST ITEM", StringComparison.OrdinalIgnoreCase))
        || Categories.Any(c => c.Equals("Quest Items", StringComparison.OrdinalIgnoreCase));
}

public enum ItemLookupState { Live, Cached, StaleCache, Offline, NotFound, Catalog }

public sealed record ItemLookupResult(ItemInfo? Item, ItemLookupState State, DateTime? FetchedAt);

/// <summary>
/// Explicit, on-demand item lookups against eqlwiki.com — the Lore-Lens idea without the
/// OCR: the LOG names every looted item exactly, so a click is all the input needed.
/// One bounded fetch per request, a 7-day disk cache, and honest LIVE/CACHED/STALE
/// labels; nothing is scraped in the background (the same etiquette the spawn-catalog
/// sync uses). Page structure per the 2026-08-04 survey: {{Itempage}} template inside
/// &lt;onlyinclude&gt;, categories outside, api.php with redirects=1 resolves case and
/// name variants; in-game "+N" suffixes have no wiki pages (strip before lookup).
/// </summary>
public sealed partial class EqlWikiItemService
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);

    private readonly string _cacheDir;
    private readonly Func<string, Task<string?>> _fetch;
    private static readonly HttpClient Http = EqlWikiText.CreateClient();

    /// <param name="fetchOverride">Tests inject a fake fetcher; null = real api.php.
    /// The fetcher returns the page's raw wikitext, or null when the page is missing.</param>
    public EqlWikiItemService(string cacheDir, Func<string, Task<string?>>? fetchOverride = null)
    {
        _cacheDir = cacheDir;
        _fetch = fetchOverride ?? FetchFromApi;
    }

    /// <summary>
    /// Strips the in-game upgrade suffix ("Rusty Broad Sword +4" → base name) — the wiki has
    /// no "+N" pages (verified: 404s, no redirects) — and then asks the curated alias table
    /// how eqlwiki spells it (DRA-149 D2).
    ///
    /// <para><b>THE ONE SEAM, and the alias lands in it rather than beside it.</b> Every reader
    /// of an item name in this app already comes through here —
    /// <see cref="ItemCatalog.Find"/>, <see cref="CachedInfo"/>, <see cref="LookupAsync"/>, the
    /// item window's heading and <c>WikiLinks.Search</c> — so one table fixes the catalog
    /// lookup, the cache lookup, the fetch and the player's own wiki door at once. A second
    /// place that knew about spellings would be a second answer to "what is this item called"
    /// (trap 4), and the one that got asked would depend on which surface the player was
    /// standing on.</para>
    ///
    /// <para>Order matters: the tier suffix comes off FIRST, so the table is keyed on base
    /// names and one row covers "+2" and "+8" alike. <see cref="ItemNameAliases.Resolve"/> is
    /// whole-string and never fuzzy — an unknown name comes back unchanged, which is what lets
    /// <see cref="GearUpgrades.WornFrom"/> REPORT it as unread instead of guessing.</para>
    /// </summary>
    public static string NormalizeTitle(string inGameName) =>
        ItemNameAliases.Resolve(Regex.Replace(inGameName.Trim(), @"\s+\+\d+$", ""));

    public async Task<ItemLookupResult> LookupAsync(string inGameName)
    {
        var title = NormalizeTitle(inGameName);
        if (title.Length == 0) return new ItemLookupResult(null, ItemLookupState.NotFound, null);

        var cached = ReadCache(title);
        if (cached is { } fresh && DateTime.UtcNow - fresh.FetchedAt < CacheLifetime)
            return new ItemLookupResult(Parse(fresh.Wikitext, fresh.Title), ItemLookupState.Cached, fresh.FetchedAt);

        // Exact title first; the backtick-stripped variant is a fallback only (the wiki
        // KEEPS backticks on at least some pages, contrary to its mob-page habits).
        foreach (var candidate in Candidates(title))
        {
            string? wikitext;
            try { wikitext = await _fetch(candidate).ConfigureAwait(false); }
            catch
            {
                // Network trouble: any cache, however old, beats nothing.
                return cached is { } stale
                    ? new ItemLookupResult(Parse(stale.Wikitext, stale.Title), ItemLookupState.StaleCache, stale.FetchedAt)
                    : new ItemLookupResult(null, ItemLookupState.Offline, null);
            }
            if (wikitext is null) continue;
            WriteCache(title, candidate, wikitext);
            return new ItemLookupResult(Parse(wikitext, candidate), ItemLookupState.Live, DateTime.UtcNow);
        }
        // Not on the live wiki (renamed page, offline miss): the embedded catalog
        // still answers, honestly labeled — weekly-refresh data beats a shrug.
        return FromCatalog(title) is { } fromCatalog
            ? new ItemLookupResult(fromCatalog, ItemLookupState.Catalog, null)
            : new ItemLookupResult(null, ItemLookupState.NotFound, null);
    }

    private static IEnumerable<string> Candidates(string title)
    {
        yield return title;
        var noBacktick = title.Replace("`", "");
        if (noBacktick != title) yield return noBacktick;
    }

    // "Costs nothing" was a lie until 2026-08-10: every CachedInfo call was a
    // File.Exists + ReadAllText + JSON parse + wikitext parse — and the loot list
    // calls it PER ROW PER SECOND while expanded (perf audit finding #2: ~6ms/100
    // rows of blocking UI-thread disk I/O, worse under Defender). One memo makes
    // the second call a hash probe; WriteCache invalidates so fresh fetches show.
    private readonly Dictionary<string, ItemInfo?> _infoMemo = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _memoLock = new();

    /// <summary>Cache-only peek: synchronous, accepts any age, never fetches — for
    /// surfaces that must cost nothing (row values, first-paint tooltips). A fetched
    /// page wins; the embedded catalog answers for everything never fetched — the
    /// layer that makes the Gear Locker and hover stats instant and offline.</summary>
    public ItemInfo? CachedInfo(string inGameName)
    {
        lock (_memoLock)
            if (_infoMemo.TryGetValue(inGameName, out var memo)) return memo;
        var cached = ReadCache(NormalizeTitle(inGameName));
        var info = cached is null ? null : Parse(cached.Wikitext, cached.Title);
        info ??= FromCatalog(NormalizeTitle(inGameName));
        lock (_memoLock) _infoMemo[inGameName] = info;
        return info;
    }

    /// <summary>An <see cref="ItemInfo"/> synthesized from the embedded catalog —
    /// the same fields a live parse yields, minus the merchant/vendor details the
    /// catalog deliberately doesn't carry.</summary>
    private static ItemInfo? FromCatalog(string title)
    {
        if (ItemCatalog.Default.Find(title) is not { } rec) return null;
        return new ItemInfo
        {
            Name = rec.Name,
            StatsLines = rec.StatsText.Length > 0 ? [.. rec.StatsText.Split('\n')] : [],
            Quests = rec.Quests ?? [],
            Recipes = rec.Recipes ?? [],
            // The creatures ride along where the catalog has them (DRA-71 D6). A zone with no
            // named creature keeps the empty list it always had — the shape is unchanged and
            // an item page that never named one still says nothing.
            DropsFrom = (rec.DropZones ?? [])
                .Select(z => (z, rec.DropMobs is { } m && m.TryGetValue(z, out var mobs)
                    ? new List<string>(mobs) : new List<string>()))
                .ToList(),
            // The build tool computed QuestFlagged from the page's stats AND its
            // categories; carry the category half through so category-only quest
            // items keep their 🗺 badge (2026-08-13 review: the flag was shipped
            // in the gz but unreachable).
            Categories = rec.QuestFlagged ? ["Quest Items"] : [],
            WikiUrl = "https://eqlwiki.com/" + Uri.EscapeDataString(rec.Name.Replace(' ', '_')),
        };
    }

    // The catalog fallback made this branch always-taken for real items, so the
    // join is memoized too (2026-08-13 review): loot rows call this per row per
    // render second, and re-joining an unchanged stats block every tick was churn.
    private readonly Dictionary<string, string?> _statsTextMemo = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// **THE ONE STATS RESOLVER** every gear comparison in the app takes (DRA-71 D6).
    ///
    /// <para>The embedded catalog answers FIRST, with the build tool's own structured numbers
    /// — no text round-trip, and the catalog≡live-parse guarantee holds because the catalog
    /// was built through these same parsers. A genuinely fetched live page covers the rest.
    /// That ordering was typed out in <c>InventoryView</c> and would have been typed out again
    /// in the Helper's Farm Gear block; the two would then have been free to disagree about
    /// which layer wins, which is one item comparing differently in two rooms (trap 4).</para>
    ///
    /// <para>It never fetches. Both callers run on a render path.</para>
    /// </summary>
    public ItemStatsBlock? StatsFor(string baseName) =>
        ItemCatalog.Default.Find(baseName) is { } rec
        && (rec.Slots.Count > 0 || rec.StatsText.Length > 0)
            ? rec.ToStatsBlock()
            : CachedInfo(baseName) is { StatsLines.Count: > 0 } info
                ? ItemStatsBlock.Parse(info.StatsLines)
                : null;

    /// <summary>Cache-only stats peek for hover tooltips: synchronous, accepts any age,
    /// never fetches. A hover must cost nothing — the click path does the real lookup.</summary>
    public string? CachedStatsText(string inGameName)
    {
        lock (_memoLock)
            if (_statsTextMemo.TryGetValue(inGameName, out var memo)) return memo;
        var info = CachedInfo(inGameName);
        var text = info is { StatsLines.Count: > 0 } ? string.Join("\n", info.StatsLines) : null;
        lock (_memoLock) _statsTextMemo[inGameName] = text;
        return text;
    }

    private async Task<string?> FetchFromApi(string title)
    {
        var url = "https://eqlwiki.com/api.php?action=query&prop=revisions&rvprop=content&format=json&redirects=1&titles="
            + Uri.EscapeDataString(title);
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
        foreach (var page in pages.EnumerateObject())
        {
            if (page.Value.TryGetProperty("missing", out _)) return null;
            if (page.Value.TryGetProperty("revisions", out var revs) && revs.GetArrayLength() > 0
                && revs[0].TryGetProperty("*", out var content))
                return content.GetString();
        }
        return null;
    }

    // ---- cache ----

    private sealed record CacheEntry(string Title, string Wikitext, DateTime FetchedAt);

    private string CachePath(string title) =>
        Path.Combine(_cacheDir, string.Concat(title.Select(c =>
            char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')) + ".json");

    private CacheEntry? ReadCache(string title)
    {
        try
        {
            var path = CachePath(title);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    private void WriteCache(string title, string resolvedTitle, string wikitext)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            File.WriteAllText(CachePath(title),
                JsonSerializer.Serialize(new CacheEntry(resolvedTitle, wikitext, DateTime.UtcNow)));
            lock (_memoLock) { _infoMemo.Clear(); _statsTextMemo.Clear(); }   // fresh page — memos re-read
        }
        catch { /* cache is a convenience; lookups still work without it */ }
    }

    // ---- wikitext parsing ({{Itempage}} template, field grammar per the survey) ----

    [GeneratedRegex(@"\[\[:?(?:[^\]|]*\|)?([^\]]*)\]\]")]
    private static partial Regex WikiLinkRx();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRx();

    [GeneratedRegex(@"(\d+)\s+(Platinum|Gold|Silver|Copper)s?", RegexOptions.IgnoreCase)]
    private static partial Regex CoinWordRx();

    /// <summary>A bolded run — where the vendor-value block states what its price was quoted
    /// at (DRA-71 D7).</summary>
    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BoldRunRx();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRx();

    [GeneratedRegex(@"\{\{ItemWhereRow(.*?)\}\}", RegexOptions.Singleline)]
    private static partial Regex WhereRowRx();

    [GeneratedRegex(@"\[\[Category:([^\]]+)\]\]")]
    private static partial Regex CategoryRx();

    private static string StripLinks(string s) => WikiLinkRx().Replace(s, "$1").Trim();

    public static ItemInfo Parse(string wikitext, string title)
    {
        var info = new ItemInfo
        {
            Name = title,
            WikiUrl = "https://eqlwiki.com/" + Uri.EscapeDataString(title.Replace(' ', '_')),
        };
        foreach (Match c in CategoryRx().Matches(wikitext)) info.Categories.Add(c.Groups[1].Value.Trim());

        var fields = SplitTemplateFields(wikitext);
        if (fields.TryGetValue("itemname", out var name) && name.Trim().Length > 0)
            info.Name = name.Trim();

        if (fields.TryGetValue("statsblock", out var stats))
            // Split on <br> AND raw newlines: some pages open the block with a bare
            // flags line ("Attunable, Placeable") before the first <br>, and leaving
            // it glued to "Slot: …" hid the slot from every anchored parser (found
            // 2026-08-13 when Fine Steel Long Sword vanished from the Gear Locker).
            info.StatsLines = Regex.Split(stats, @"<br\s*/?>|\n")
                .Select(l => StripLinks(HtmlTagRx().Replace(l, "")).Trim())
                .Where(l => l.Length > 0)
                .ToList();

        if (fields.TryGetValue("merchant_value", out var value))
            (info.MerchantValue, info.MerchantCondition) = ParseMerchantValue(value);

        if (fields.TryGetValue("dropsfrom", out var drops))
            info.DropsFrom = ParseDropsFrom(drops);

        if (fields.TryGetValue("soldby", out var sold))
            foreach (Match m in WhereRowRx().Matches(sold))
            {
                // Positional row: | zone | merchant | area | (x, y) — but zone/merchant
                // are often piped links ("[[Freeport|East Freeport]]"), so the split has
                // to ignore pipes inside [[…]].
                var parts = SplitTopLevelPipes(m.Groups[1].Value);
                if (parts.Count >= 4)
                    info.SoldBy.Add((StripLinks(parts[0]), StripLinks(parts[1]), parts[3].Trim()));
            }

        if (fields.TryGetValue("relatedquests", out var quests))
            info.Quests = BulletLines(quests);
        if (fields.TryGetValue("recipes", out var recipes))
            info.Recipes = BulletLines(recipes);
        return info;
    }

    private static Dictionary<string, string> SplitTemplateFields(string wikitext) =>
        EqlWikiText.TemplateFields(wikitext, "Itempage");

    /// <summary>
    /// Two shapes in the wild: plain "5p 9g 2s 8c", or an HTML list of per-coin lines
    /// ("0 Platinums / 5 Silvers / 8 Coppers" with markup). Both normalize to compact coin
    /// text; zero denominations drop out.
    ///
    /// <para><b>The HTML shape carries a HEADING, and since DRA-71 D7 the heading comes back
    /// too</b> — "VALUE TO VENDOR with CHA : 80 and faction at Indifferently". It is the
    /// condition the price was quoted at, it differs per page, and dropping it (which this
    /// method did until now) turns one editor's quote into a fact about the item. See
    /// <see cref="ItemInfo.MerchantCondition"/>.</para>
    /// </summary>
    private static (string Value, string Condition) ParseMerchantValue(string raw)
    {
        if (!raw.Contains('<')) return (raw.Trim(), "");
        var parts = new List<string>();
        foreach (Match m in CoinWordRx().Matches(HtmlTagRx().Replace(raw, " ")))
        {
            var n = int.Parse(m.Groups[1].Value);
            if (n > 0) parts.Add(n + m.Groups[2].Value[..1].ToString().ToLowerInvariant());
        }
        return (string.Join(" ", parts), MerchantCondition(raw));
    }

    /// <summary>
    /// The page's own sentence about what the price rests on, or "".
    ///
    /// <para>The first bolded run before the coin list, tags stripped and whitespace
    /// collapsed. It is taken VERBATIM rather than re-phrased: the numbers in it (a Charisma,
    /// a faction standing) are the page's own and a tidier sentence would be a second
    /// producer of one claim. Only a run that actually names Charisma qualifies — a bolded
    /// heading that says something else is not a condition, and an unanswered question draws
    /// nothing rather than a caveat somebody invented (trap 73).</para>
    /// </summary>
    private static string MerchantCondition(string raw)
    {
        foreach (Match m in BoldRunRx().Matches(raw))
        {
            var text = WhitespaceRx().Replace(HtmlTagRx().Replace(m.Groups[1].Value, " "), " ").Trim();
            if (text.Contains("CHA", StringComparison.OrdinalIgnoreCase)) return text;
        }
        return "";
    }

    private static List<(string Zone, List<string> Mobs)> ParseDropsFrom(string raw)
    {
        var result = new List<(string, List<string>)>();
        List<string>? mobs = null;
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = StripLinks(rawLine.Trim());
            if (line.Length == 0) continue;
            if (rawLine.TrimStart().StartsWith('*'))
            {
                mobs ??= NewZone("");
                mobs.Add(line.TrimStart('*', ' '));
            }
            else
            {
                mobs = NewZone(line);
            }
        }
        return result;

        List<string> NewZone(string zone)
        {
            var list = new List<string>();
            result.Add((zone, list));
            return list;
        }
    }

    /// <summary>Splits on '|' except inside [[wiki links]]; the leading empty segment
    /// (rows begin with '|') is dropped.</summary>
    private static List<string> SplitTopLevelPipes(string s)
    {
        var parts = new List<string>();
        var depth = 0; var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (i + 1 < s.Length && s[i] == '[' && s[i + 1] == '[') { depth++; i++; }
            else if (i + 1 < s.Length && s[i] == ']' && s[i + 1] == ']') { depth--; i++; }
            else if (s[i] == '|' && depth == 0)
            {
                parts.Add(s[start..i]);
                start = i + 1;
            }
        }
        parts.Add(s[start..]);
        if (parts.Count > 0 && parts[0].Trim().Length == 0) parts.RemoveAt(0);
        return parts;
    }

    private static List<string> BulletLines(string raw) =>
        raw.Split('\n')
            .Select(l => StripLinks(l.Trim()).TrimStart('*', ':', ' ').Trim())
            .Where(l => l.Length > 0)
            .ToList();
}
