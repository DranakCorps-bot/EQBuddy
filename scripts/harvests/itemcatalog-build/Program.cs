// Builds src/EQBuddy.Core/Data/ItemCatalog.json.gz from the items-harvest dump.
//
// The whole point of this being C# (and not more python in items-harvest.py): the
// dump flows through THE APP'S OWN parsers — EqlWikiItemService.Parse for the
// {{Itempage}} template, ItemStatsBlock.Parse for the stats block — so the catalog
// can never disagree with what a live lookup of the same page would show.

using System.IO.Compression;
using System.Text.Json;
using EQBuddy.Core;

// --check rebuilds into memory and compares the DECOMPRESSED payload with the committed
// file's decompressed payload, writing nothing. See the comparison at the bottom for why it
// is the contents and never the .gz bytes (trap 74).
var check = args.Contains("--check");

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
var dump = Path.Combine(repo, "scripts", "harvests", "eqlwiki", "cache", "items-wikitext.jsonl");
var outPath = Path.Combine(repo, "src", "EQBuddy.Core", "Data", "ItemCatalog.json.gz");
var reportPath = Path.Combine(repo, "scripts", "harvests", "eqlwiki", "items-catalog-report.md");

if (!File.Exists(dump))
{
    // The dump is gitignored and is rebuilt by fetching ~11k pages. A clone that has never
    // run the harvest cannot check anything, and saying so beats either a crash or a green
    // run that compared nothing (trap 78 — a guard aimed at nothing is green for no reason).
    Console.Error.WriteLine(
        $"items-wikitext.jsonl is not here ({dump}). It is gitignored and is written by "
        + "items-harvest.py, which FETCHES. Nothing was built or compared.");
    return check ? 2 : 1;
}

var records = new List<ItemCatalog.Record>();
int parsed = 0, noTemplate = 0, statless = 0;
var examples = new List<string>();

foreach (var line in File.ReadLines(dump))
{
    if (line.Trim().Length == 0) continue;
    using var doc = JsonDocument.Parse(line);
    var title = doc.RootElement.GetProperty("title").GetString()!;
    var wikitext = doc.RootElement.GetProperty("wikitext").GetString()!;

    var info = EqlWikiItemService.Parse(wikitext, title);
    if (info.StatsLines.Count == 0 && info.Quests.Count == 0 && info.DropsFrom.Count == 0)
    {
        noTemplate++;
        if (examples.Count < 30) examples.Add(title);
        continue;
    }
    var stats = ItemStatsBlock.Parse(info.StatsLines);
    if (info.StatsLines.Count == 0) statless++;
    parsed++;

    records.Add(new ItemCatalog.Record
    {
        Name = info.Name,
        StatsText = info.StatsLines.Count > 0 ? string.Join("\n", info.StatsLines) : "",
        Slots = stats.Slots,
        Ac = stats.Ac, Dmg = stats.Dmg, Delay = stats.Delay, Hp = stats.Hp, Mana = stats.Mana,
        Attributes = stats.Attributes.Count > 0 ? stats.Attributes : null,
        Classes = stats.Classes.Count > 0 ? stats.Classes : null,
        Skill = stats.Skill,
        QuestFlagged = info.QuestFlagged,
        Quests = info.Quests.Count > 0 ? info.Quests : null,
        Recipes = info.Recipes.Count > 0 ? info.Recipes : null,
        DropZones = info.DropsFrom.Count > 0
            ? info.DropsFrom.Select(d => d.Zone).Where(z => z.Length > 0).Distinct().ToList()
            : null,
        // WHO, per zone — the half this build discarded until DRA-71 D6. It is an annotation
        // on DropZones and never a second copy of it: a zone appears as a key only when the
        // page named at least one creature under it, so the zone list keeps its one producer
        // (trap 4). Zones are folded case-insensitively for the same reason DropZones
        // De-duplicates — a page that heads one zone twice is one zone with both lists.
        DropMobs = DropMobs(info),
    });
}

var payload = JsonSerializer.SerializeToUtf8Bytes(
    new ItemCatalog.Root { Items = records },
    new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

// **THE GATE COMPARES THE THING THE CLAIM IS ABOUT** (trap 74). "This file is reproducible
// from the cache" is a claim about the catalog's CONTENTS; gzip is a container, and its bytes
// depend on which zlib built them — HarvestedGuides.json.gz went red on CI for exactly that,
// against a payload that was identical. So both the --check comparison AND the write decision
// read the decompressed bytes, which means a refresh whose data did not move leaves no binary
// diff in the PR at all.
var committed = File.Exists(outPath) ? Decompress(outPath) : null;
var identical = committed is not null && committed.AsSpan().SequenceEqual(payload);

if (check)
{
    if (committed is null)
    {
        Console.Error.WriteLine($"{outPath} is not committed — nothing to compare against.");
        return 2;
    }
    if (!identical)
    {
        Console.Error.WriteLine(
            $"ItemCatalog.json.gz is NOT reproducible from the cache: committed contents are "
            + $"{committed.Length:N0} bytes, a rebuild is {payload.Length:N0}. Re-run the "
            + "promoter and commit the result.");
        return 1;
    }
    Console.WriteLine(
        $"ItemCatalog.json.gz reproduces byte-for-byte from the cache ({payload.Length / 1024} KB "
        + $"decompressed, {parsed} items). Nothing written.");
    return 0;
}

if (identical)
{
    Console.WriteLine(
        $"ItemCatalog.json.gz contents are unchanged ({parsed} items) — left alone, so a "
        + "refresh that moved no data carries no binary diff.");
}
else
{
    using var fs = File.Create(outPath);
    using var gz = new GZipStream(fs, CompressionLevel.SmallestSize);
    gz.Write(payload);
}

// The DropMobs survey, in the report rather than in a head. **Distinct-count discipline**
// (trap 73): 5,613 items carrying ten distinct creature names between them would be a parser
// finding one template, not a catalog learning who drops things, and only a count can tell the
// two apart before anybody reads a row.
var withMobs = records.Count(r => r.DropMobs is { Count: > 0 });
var mobZones = records.Sum(r => r.DropMobs?.Count ?? 0);
var mobNames = records.SelectMany(r => r.DropMobs?.Values.SelectMany(v => v) ?? []).ToList();
var distinctMobs = mobNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();

File.WriteAllText(reportPath,
    $"# item catalog build report\n\n" +
    $"- dump entries parsed into the catalog: {parsed}\n" +
    $"- pages with no Itempage content (skipped): {noTemplate}\n" +
    $"- catalog items without a stats block (knowledge-only): {statless}\n" +
    $"- items with at least one NAMED creature (DropMobs): {withMobs}\n" +
    $"- zone entries carrying creatures: {mobZones}; creature mentions: {mobNames.Count}; " +
    $"distinct creature names: {distinctMobs}\n" +
    $"- raw payload: {payload.Length / 1024} KB; gz: {new FileInfo(outPath).Length / 1024} KB\n\n" +
    "Skipped examples:\n" + string.Join("\n", examples.Select(e => $"  - {e}")) + "\n");

Console.WriteLine($"{parsed} items -> {outPath} ({new FileInfo(outPath).Length / 1024} KB gz); " +
    $"{noTemplate} skipped (no template), {statless} statless; " +
    $"{withMobs} with named creatures ({distinctMobs:N0} distinct). Report: {reportPath}");
return 0;

static byte[] Decompress(string path)
{
    using var file = File.OpenRead(path);
    using var gz = new GZipStream(file, CompressionMode.Decompress);
    using var buffer = new MemoryStream();
    gz.CopyTo(buffer);
    return buffer.ToArray();
}

static Dictionary<string, List<string>>? DropMobs(ItemInfo info)
{
    var byZone = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    foreach (var (zone, mobs) in info.DropsFrom)
    {
        if (zone.Length == 0 || mobs.Count == 0) continue;
        if (!byZone.TryGetValue(zone, out var list)) byZone[zone] = list = [];
        foreach (var mob in mobs)
            if (mob.Length > 0 && !list.Contains(mob, StringComparer.OrdinalIgnoreCase))
                list.Add(mob);
    }
    return byZone.Count > 0 ? byZone : null;
}
