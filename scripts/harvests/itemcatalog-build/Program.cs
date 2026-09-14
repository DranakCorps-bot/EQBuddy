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

// See ProfessionIn at the bottom of the file.
string[] UnmasteredSkills =
    ["Tinkering", "Research", "Spell Research", "Skill Research", "Make Poison",
     "Poison Making", "Fishing"];

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
int valueStated = 0, valueParsed = 0, valueRefused = 0, valueConditional = 0;
var distinctValues = new HashSet<long>();
// **THE CATEGORY SURVEY** (DRA-71 D8, plan P13). It runs here rather than in a throwaway
// script so the answer is re-taken on every weekly refresh — the arithmetic it gates is PARKED
// with "better wiki coverage" as its reopen condition, and a condition nobody measures is one
// nobody can satisfy. Nothing it counts is written into the catalog.
int withCategory = 0, professionCategory = 0, genericTradeskillCategory = 0, playerCrafted = 0;
int withRecipes = 0, recipesNamingAProfession = 0, recipesNamingAnUnmasteredSkill = 0;
var distinctCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var professionsNamedByCategory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var professionsNamedByRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    // The survey, per page. `info.Categories` and `info.Recipes` are both parsed by the app's
    // own item parser; the categories are DISCARDED below and DRA-71 D8 deliberately did not
    // change that — see the report's own note for what the survey found.
    if (info.Categories.Count > 0) withCategory++;
    var namedHere = false;
    foreach (var category in info.Categories)
    {
        distinctCategories.Add(category);
        if (category.Equals("Tradeskill Ingredient", StringComparison.OrdinalIgnoreCase))
            genericTradeskillCategory++;
        if (category.Equals("Player Crafted", StringComparison.OrdinalIgnoreCase))
            playerCrafted++;
        if (ProfessionIn(category) is { } named)
        {
            professionsNamedByCategory.Add(named);
            namedHere = true;
        }
    }
    if (namedHere) professionCategory++;

    if (info.Recipes.Count > 0)
    {
        withRecipes++;
        var profession = info.Recipes.Select(ProfessionIn).FirstOrDefault(n => n is not null);
        if (profession is not null)
        {
            recipesNamingAProfession++;
            professionsNamedByRecipes.Add(profession);
        }
        else if (info.Recipes.Any(line => UnmasteredSkills.Contains(line.Trim())))
        {
            recipesNamingAnUnmasteredSkill++;
        }
    }

    // The condition only means something beside a value. A caveat with nothing to qualify is
    // a sentence a surface would have to decide what to do with, and the honest answer is that
    // there is nothing to say (trap 73).
    var copper = CoinText.Parse(info.MerchantValue);
    if (info.MerchantValue.Trim().Length > 0) valueStated++;
    if (copper is not null) valueParsed++; else if (info.MerchantValue.Trim().Length > 0) valueRefused++;
    if (copper is not null && info.MerchantCondition.Length > 0) valueConditional++;
    if (copper is { } c) distinctValues.Add(c);

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
        // WHAT A VENDOR PAID — the half this build discarded until DRA-71 D7. Parsed through
        // the app's own CoinText.Parse, which refuses anything that is not plain
        // coin text: an unparseable merchant_value is ABSENT rather than guessed (trap 73).
        // The CONDITION rides with it because the wiki's price is quoted at a Charisma and a
        // faction standing that differ per page — a number stripped of that heading is one
        // editor's quote wearing the clothes of a fact about the object.
        MerchantCopper = copper,
        MerchantCondition = copper is not null && info.MerchantCondition.Length > 0
            ? info.MerchantCondition
            : null,
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

// **THE COPPER SURVEY, PRINTED IN BOTH PATHS** (DRA-71 D7, plan P9, trap 73's tell).
// It runs before the --check return so a survey can be taken WITHOUT writing anything, which
// is how this slice took its own. A distinct count is the only thing that separates "the
// catalog learned what vendors pay" from "the parser found one template" — and
// `valueConditional` is the one nobody expected to matter: it counts the values whose own page
// states a Charisma and a faction, which is what makes this number a quote rather than a fact
// about the item.
// **WHAT THE CATEGORY FIELD MEANS, NOT ONLY WHETHER IT IS THERE** (DRA-71 D8).
// "Is it populated" is the wrong question and 99.7% is the wrong answer to feel good about:
// what decides whether an item can be pointed at a profession is whether the field says WHICH
// ONE. So this reports the coverage AND the count that matters, beside the recipes field —
// which turned out to carry the answer the categories do not.
var categorySurvey =
    $"- item pages with at least one Category: {withCategory} of {parsed + noTemplate}; "
    + $"distinct categories: {distinctCategories.Count}\n"
    + $"- categories naming a PROFESSION: {professionCategory} pages, "
    + $"{professionsNamedByCategory.Count} of the eight "
    + $"({string.Join(", ", professionsNamedByCategory.OrderBy(n => n))})\n"
    + $"- generic instead: 'Tradeskill Ingredient' {genericTradeskillCategory}, "
    + $"'Player Crafted' {playerCrafted}\n"
    + $"- pages with a recipes field: {withRecipes}; naming one of the eight: "
    + $"{recipesNamingAProfession} ({professionsNamedByRecipes.Count} distinct); naming a skill "
    + $"with no Mastery AA: {recipesNamingAnUnmasteredSkill}\n";
Console.WriteLine(categorySurvey.TrimEnd());

var survey =
    $"- pages stating a merchant_value: {valueStated}; parsed to copper: {valueParsed}; "
    + $"refused as unreadable: {valueRefused}; distinct parsed values: {distinctValues.Count}\n"
    + $"- of the parsed, quoted at a stated Charisma/faction: {valueConditional}\n";
Console.WriteLine(survey.TrimEnd());

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
    survey +
    categorySurvey +
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

// Which of the curated eight a category or a recipe line names, or null. The category shape
// is "Pottery Ingredient"; the recipe shape is the bare profession name on its own bullet.
// `UnmasteredSkills` above is the survey's other half — the skills eqlwiki names in recipe
// lines that have NO Mastery AA and are therefore not in `Core/Tradeskills.cs`. They are
// counted so the survey reports the whole picture rather than the part the curation covers; a
// reopen decision wants to know they exist. Survey-only: nothing here is promoted.
static string? ProfessionIn(string text)
{
    var trimmed = (text ?? "").Trim();
    if (Tradeskills.Match(trimmed) is { } direct) return direct.ToString();
    foreach (var suffix in new[] { " Ingredient", " Ingredients", " Items" })
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && Tradeskills.Match(trimmed[..^suffix.Length]) is { } stripped)
            return stripped.ToString();
    return null;
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
