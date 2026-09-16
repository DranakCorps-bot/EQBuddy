using System.Globalization;
using System.IO;

namespace EQBuddy.Core;

/// <summary>One map line segment, in map-space coordinates with the file's color.</summary>
public sealed record MapLine(double X1, double Y1, double X2, double Y2, byte R, byte G, byte B);

/// <summary>One labeled point of interest.</summary>
public sealed record MapPoint(double X, double Y, byte R, byte G, byte B, int Size, string Label);

/// <summary>
/// A parsed classic-EQ map file (the format Brewall packs and the game's own /map
/// use): `L x1, y1, z1, x2, y2, z2, r, g, b` segments and `P x, y, z, r, g, b,
/// size, Label` points, underscores in labels standing in for spaces.
///
/// Coordinates are MAP space. The game's /loc prints "Y, X, Z" (EQ's famous
/// axis order), and a game position plots at map (-X, -Y) — the convention two
/// decades of EQ map tools (GamParse's window, nParse) settled on.
/// </summary>
public sealed class ZoneMap
{
    public List<MapLine> Lines { get; } = [];
    public List<MapPoint> Points { get; } = [];
    public double MinX { get; private set; } = double.MaxValue;
    public double MinY { get; private set; } = double.MaxValue;
    public double MaxX { get; private set; } = double.MinValue;
    public double MaxY { get; private set; } = double.MinValue;

    public bool IsEmpty => Lines.Count == 0 && Points.Count == 0;

    /// <summary>Game /loc (locY, locX) → map-space point.</summary>
    public static (double X, double Y) FromLoc(double locY, double locX) => (-locX, -locY);

    public static ZoneMap Load(string path)
    {
        var map = new ZoneMap();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length < 2 || line[1] != ' ' && line[1] != '\t') continue;
            var parts = line[1..].Split(',', StringSplitOptions.TrimEntries);
            switch (char.ToUpperInvariant(line[0]))
            {
                case 'L' when parts.Length >= 9
                    && TryD(parts[0], out var x1) && TryD(parts[1], out var y1)
                    && TryD(parts[3], out var x2) && TryD(parts[4], out var y2)
                    && TryB(parts[6], out var lr) && TryB(parts[7], out var lg) && TryB(parts[8], out var lb):
                    map.Lines.Add(new MapLine(x1, y1, x2, y2, lr, lg, lb));
                    map.Grow(x1, y1); map.Grow(x2, y2);
                    break;
                case 'P' when parts.Length >= 8
                    && TryD(parts[0], out var px) && TryD(parts[1], out var py)
                    && TryB(parts[3], out var pr) && TryB(parts[4], out var pg) && TryB(parts[5], out var pb)
                    && int.TryParse(parts[6], out var size):
                    map.Points.Add(new MapPoint(px, py, pr, pg, pb, size,
                        string.Join(",", parts[7..]).Replace('_', ' ').Trim()));
                    map.Grow(px, py);
                    break;
            }
        }
        return map;
    }

    private void Grow(double x, double y)
    {
        MinX = Math.Min(MinX, x); MinY = Math.Min(MinY, y);
        MaxX = Math.Max(MaxX, x); MaxY = Math.Max(MaxY, y);
    }

    private static bool TryD(string s, out double v) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static bool TryB(string s, out byte v) => byte.TryParse(s, out v);
}

/// <summary>
/// Finds the map FILE for a zone NAME. Map packs (Brewall and kin) name files by
/// the game's internal shortname (crushbone.txt, ecommons.txt); logs and quests
/// speak display names ("East Commonlands"). The alias table carries the classic
/// shortnames; anything unmapped falls back to normalized containment against the
/// files actually present, so a pack with unexpected names still mostly resolves.
/// </summary>
public static class ZoneMapFiles
{
    /// <summary>Display name (lowercase, "the " stripped) → map-file shortname.</summary>
    internal static readonly Dictionary<string, string> Shortnames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["east commonlands"] = "ecommons", ["west commonlands"] = "commons",
        ["east freeport"] = "freporte", ["west freeport"] = "freportw", ["north freeport"] = "freportn",
        ["north qeynos"] = "qeynos2", ["south qeynos"] = "qeynos", ["qeynos hills"] = "qeytoqrg",
        ["qeynos catacombs"] = "qcat", ["surefall glade"] = "qrg", ["blackburrow"] = "blackburrow",
        ["west karana"] = "qey2hh1", ["north karana"] = "northkarana", ["south karana"] = "southkarana",
        ["east karana"] = "eastkarana", ["highpass hold"] = "highpass", ["highkeep"] = "highkeep",
        ["kithicor forest"] = "kithicor", ["rivervale"] = "rivervale", ["misty thicket"] = "misty",
        ["runnyeye citadel"] = "runnyeye", ["gorge of king xorbb"] = "beholder",
        ["nektulos forest"] = "nektulos", ["lavastorm mountains"] = "lavastorm",
        ["najena"] = "najena", ["solusek's eye"] = "soldunga", ["nagafen's lair"] = "soldungb",
        ["temple of solusek ro"] = "soltemple", ["north ro"] = "nro", ["south ro"] = "sro",
        ["oasis of marr"] = "oasis", ["innothule swamp"] = "innothule", ["the feerrott"] = "feerrott",
        ["temple of cazic-thule"] = "cazicthule", ["grobb"] = "grobb", ["oggok"] = "oggok",
        ["rathe mountains"] = "rathemtn", ["lake rathetear"] = "lakerathe",
        ["greater faydark"] = "gfaydark", ["lesser faydark"] = "lfaydark", ["crushbone"] = "crushbone",
        ["northern felwithe"] = "felwithea", ["southern felwithe"] = "felwitheb", ["felwithe"] = "felwithea",
        ["north kaladim"] = "kaladimb", ["south kaladim"] = "kaladima", ["kaladim"] = "kaladima",
        ["ak'anon"] = "akanon", ["steamfont mountains"] = "steamfont",
        ["butcherblock mountains"] = "butcher", ["dagnor's cauldron"] = "cauldron",
        ["estate of unrest"] = "unrest", ["kedge keep"] = "kedge", ["castle mistmoore"] = "mistmoore",
        ["ocean of tears"] = "oot", ["befallen"] = "befallen", ["neriak foreign quarter"] = "neriaka",
        ["neriak commons"] = "neriakb", ["neriak third gate"] = "neriakc",
        ["everfrost peaks"] = "everfrost", ["halas"] = "halas", ["permafrost keep"] = "permafrost",
        ["erudin"] = "erudnext", ["erudin palace"] = "erudnint", ["erud's crossing"] = "erudsxing",
        ["toxxulia forest"] = "tox", ["kerra isle"] = "kerraridge", ["paineel"] = "paineel",
        ["the hole"] = "hole", ["stonebrunt mountains"] = "stonebrunt", ["the warrens"] = "warrens",
        ["infested paw"] = "paw", ["splitpaw lair"] = "paw",
        ["plane of fear"] = "fearplane", ["plane of hate"] = "hateplane", ["plane of sky"] = "airplane",
        ["city of guk"] = "guktop", ["ruins of old guk"] = "gukbottom",
        ["upper guk"] = "guktop", ["lower guk"] = "gukbottom",
        ["field of bone"] = "fieldofbone", ["warsliks woods"] = "warslikswood",
        ["cabilis west"] = "cabwest", ["cabilis east"] = "cabeast", ["cabilis"] = "cabwest",
        ["west cabilis"] = "cabwest", ["east cabilis"] = "cabeast",
        ["swamp of no hope"] = "swampofnohope", ["firiona vie"] = "firiona",
        ["lake of ill omen"] = "lakeofillomen", ["dreadlands"] = "dreadlands",
        // Normalize strips a leading "the ", so alias KEYS must arrive pre-stripped —
        // "the burning woods" could never match (found by the coverage audit).
        ["burning wood"] = "burningwood", ["burning woods"] = "burningwood",
        ["kaesora"] = "kaesora", ["ruins of sebilis"] = "sebilis", ["city of mist"] = "citymist",
        ["skyfire mountains"] = "skyfire", ["frontier mountains"] = "frontiermtns",
        ["the overthere"] = "overthere", ["emerald jungle"] = "emeraldjungle",
        ["trakanon's teeth"] = "trakanon", ["timorous deep"] = "timorous",
        ["kurn's tower"] = "kurn", ["chardok"] = "chardok", ["dalnir"] = "dalnir",
        ["howling stones"] = "charasis", ["mines of nurga"] = "nurga", ["temple of droga"] = "droga",
        ["veeshan's peak"] = "veeshan", ["iceclad ocean"] = "iceclad",
        ["tower of frozen shadow"] = "frozenshadow", ["velketor's labyrinth"] = "velketor",
        ["kael drakkel"] = "kael", ["skyshrine"] = "skyshrine", ["thurgadin"] = "thurgadina",
        ["icewell keep"] = "thurgadinb", ["eastern wastes"] = "eastwastes",
        ["cobalt scar"] = "cobaltscar", ["great divide"] = "greatdivide",
        ["wakening land"] = "wakening", ["western wastes"] = "westwastes",
        ["crystal caverns"] = "crystal", ["dragon necropolis"] = "necropolis",
        ["temple of veeshan"] = "templeveeshan", ["siren's grotto"] = "sirens",
        ["plane of mischief"] = "mischiefplane", ["plane of growth"] = "growthplane",
        // The names the game ITSELF speaks — the client's zone table (mined via
        // eqltools, scripts/harvests/eqltools/layout-extract.json) and the spawn
        // catalog's logZoneName/aliases. The wiki-style keys above don't bridge
        // these: "The Western Plains of Karana" contains no "qey2hh1", so a missing
        // alias here is a zone with NO map (the Qeynos Hills report, 2026-08-13).
        ["western plains of karana"] = "qey2hh1", ["northern plains of karana"] = "northkarana",
        ["southern plains of karana"] = "southkarana", ["eastern plains of karana"] = "eastkarana",
        ["northern desert of ro"] = "nro", ["southern desert of ro"] = "sro",
        ["neriak - foreign quarter"] = "neriaka", ["neriak - commons"] = "neriakb",
        ["neriak - third gate"] = "neriakc", ["neriak - 3rd gate"] = "neriakc",
        ["qeynos aqueduct system"] = "qcat", ["ruins of old paineel"] = "hole",
        ["city of thurgadin"] = "thurgadina", ["lair of the splitpaw"] = "paw",
        ["clan crushbone"] = "crushbone", ["castle of mistmoore"] = "mistmoore",
        ["liberated citadel of runnyeye"] = "runnyeye", ["crypt of dalnir"] = "dalnir",
        ["karnor's castle"] = "karnor", ["sleeper's tomb"] = "sleeper",
        ["new sebilis expedition"] = "newsebexp", ["old sebilis"] = "sebilis",
        ["guk"] = "guktop", ["ruins of ancient guk"] = "gukbottom",
        ["felwithe north"] = "felwithea", ["felwithe south"] = "felwitheb",
        ["kaladim north"] = "kaladimb", ["kaladim south"] = "kaladima",
        ["kelethin"] = "gfaydark", ["permafrost caverns"] = "permafrost",
    };

    /// <summary>The game's maps folder sits beside Logs; a user override wins.</summary>
    public static string? DefaultFolder(string? logFolder)
    {
        if (logFolder is null) return null;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null) return null;
        var maps = Path.Combine(root, "maps");
        return Directory.Exists(maps) ? maps : null;
    }

    /// <summary>Probe folders in order — the user's custom pack first, the game's own
    /// maps folder as the fallback — so a pack that skips a zone degrades to the
    /// game's shipped map instead of a blank window (Qeynos Hills, 2026-08-13).</summary>
    public static string? Resolve(IReadOnlyList<string> folders, string zoneName) =>
        folders.Select(f => Resolve(f, zoneName)).FirstOrDefault(f => f is not null);

    /// <summary>The map-file stem a zone SHOULD have — the alias shortname when one
    /// is carried, the squeezed display name otherwise — regardless of what exists
    /// on disk. This is what lets the window name the missing file precisely
    /// ("qeytoqrg.txt not found in …") instead of shrugging.</summary>
    public static string ExpectedShortname(string zoneName)
    {
        var key = Normalize(zoneName);
        return Shortnames.TryGetValue(key, out var shortname) ? shortname : Squeeze(key);
    }

    /// <summary>Resolve a display zone name to a map file in <paramref name="folder"/>,
    /// or null. Tries the alias shortname, then normalized-name containment against
    /// what's actually on disk (packs vary).</summary>
    public static string? Resolve(string folder, string zoneName)
    {
        if (!Directory.Exists(folder) || zoneName.Trim().Length == 0) return null;
        var key = Normalize(zoneName);
        if (Shortnames.TryGetValue(key, out var shortname))
        {
            var direct = Path.Combine(folder, shortname + ".txt");
            if (File.Exists(direct)) return direct;
        }
        var squeezed = Squeeze(key);
        return Directory.EnumerateFiles(folder, "*.txt")
            .Where(f =>
            {
                var stem = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                // Layer files ("crushbone_1.txt") resolve through their base map.
                var baseStem = stem.Split('_')[0];
                return baseStem == squeezed || squeezed.Contains(baseStem) && baseStem.Length >= 4;
            })
            .OrderBy(f => Path.GetFileNameWithoutExtension(f).Length)
            .FirstOrDefault();
    }

    /// <summary>The base map plus its numbered layer files ("_1", "_2", …) — packs
    /// split detail across layers and the picture is wrong without them.</summary>
    public static IEnumerable<string> WithLayers(string basePath)
    {
        yield return basePath;
        var dir = Path.GetDirectoryName(basePath)!;
        var stem = Path.GetFileNameWithoutExtension(basePath);
        for (var i = 1; i <= 3; i++)
        {
            var layer = Path.Combine(dir, $"{stem}_{i}.txt");
            if (File.Exists(layer)) yield return layer;
        }
    }

    /// <summary>The token that decides whether two zone SPELLINGS are the same zone:
    /// lowercased, a parenthetical dropped ("Cazic Thule (Zone)"), a trailing difficulty
    /// number dropped, a leading "the" dropped, and spaces/apostrophes/hyphens squeezed
    /// out. It is the identity half of what map lookup already did, lifted out so
    /// <see cref="ZoneLevels"/> asks the same question rather than growing a second rule
    /// for it (trap 4). Nothing here touches the filesystem — this names a zone, it does
    /// not find a file.</summary>
    public static string IdentityKey(string zone) => Squeeze(Normalize(zone));

    private static string Squeeze(string key) =>
        key.Replace(" ", "").Replace("'", "").Replace("-", "");

    private static string Normalize(string zone)
    {
        var z = zone.Trim().ToLowerInvariant();
        // Difficulty-tier suffixes ("Befallen 4 (Refined)") fold to the base zone.
        var paren = z.IndexOf('(');
        if (paren > 0) z = z[..paren];
        z = System.Text.RegularExpressions.Regex.Replace(z, @"\s+\d+\s*$", "");
        if (z.StartsWith("the ", StringComparison.Ordinal)) z = z[4..];
        return z.Trim();
    }
}
