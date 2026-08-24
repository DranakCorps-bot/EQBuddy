using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The zone-map coverage audit (the Qeynos Hills field report, 2026-08-13: a zone
/// whose name neither the alias table nor containment can bridge is a zone with NO
/// map). Ground truth is the client's own zone table — map-file stem to the
/// "You have entered X." display name — mined via eqltools.com
/// (scripts/harvests/eqltools/layout-extract.json). Every zone EQBuddy knows
/// (ZoneGraph nodes, SpawnCatalog names) must resolve to a stem the game actually
/// ships, so a future zone addition cannot silently lose its map.
/// </summary>
public class ZoneMapCoverageTests : IDisposable
{
    /// <summary>Stem → client display name, verbatim from the client extract, plus
    /// "arena": The Arena is a ZoneGraph node the extract carries no map for, and
    /// packs that do cover it use the classic stem.</summary>
    private static readonly (string Stem, string Display)[] ClientZones =
    [
        ("airplane", "The Plane of Sky"),
        ("akanon", "Ak'Anon"),
        ("befallen", "Befallen"),
        ("beholder", "The Gorge of King Xorbb"),
        ("blackburrow", "Blackburrow"),
        ("burningwood", "The Burning Woods"),
        ("butcher", "Butcherblock Mountains"),
        ("cabeast", "Cabilis East"),
        ("cabwest", "Cabilis West"),
        ("cauldron", "Dagnor's Cauldron"),
        ("cazicthule", "Temple of Cazic-Thule"),
        ("charasis", "Howling Stones"),
        ("chardok", "Chardok"),
        ("citymist", "The City of Mist"),
        ("cobaltscar", "Cobalt Scar"),
        ("commons", "West Commonlands"),
        ("crushbone", "Clan Crushbone"),
        ("crystal", "Crystal Caverns"),
        ("dalnir", "The Crypt of Dalnir"),
        ("dreadlands", "The Dreadlands"),
        ("droga", "The Temple of Droga"),
        ("eastkarana", "The Eastern Plains of Karana"),
        ("eastwastes", "Eastern Wastes"),
        ("ecommons", "East Commonlands"),
        ("emeraldjungle", "The Emerald Jungle"),
        ("erudnext", "Erudin"),
        ("erudnint", "Erudin Palace"),
        ("erudsxing", "Erud's Crossing"),
        ("everfrost", "Everfrost Peaks"),
        ("fearplane", "The Plane of Fear"),
        ("feerrott", "The Feerrott"),
        ("felwithea", "Northern Felwithe"),
        ("felwitheb", "Southern Felwithe"),
        ("fieldofbone", "The Field of Bone"),
        ("firiona", "Firiona Vie"),
        ("freporte", "East Freeport"),
        ("freportn", "North Freeport"),
        ("freportw", "West Freeport"),
        ("frontiermtns", "Frontier Mountains"),
        ("frozenshadow", "The Tower of Frozen Shadow"),
        ("gfaydark", "The Greater Faydark"),
        ("greatdivide", "The Great Divide"),
        ("grobb", "Grobb"),
        ("growthplane", "The Plane of Growth"),
        ("gukbottom", "The Ruins of Old Guk"),
        ("guktop", "The City of Guk"),
        ("halas", "Halas"),
        ("hateplane", "The Plane of Hate"),
        ("highkeep", "High Keep"),
        ("highpass", "Highpass Hold"),
        ("hole", "The Ruins of Old Paineel"),
        ("iceclad", "The Iceclad Ocean"),
        ("innothule", "Innothule Swamp"),
        ("kael", "Kael Drakkel"),
        ("kaesora", "Kaesora"),
        ("kaladima", "South Kaladim"),
        ("kaladimb", "North Kaladim"),
        ("karnor", "Karnor's Castle"),
        ("kedge", "Kedge Keep"),
        ("kerraridge", "Kerra Isle"),
        ("kithicor", "Kithicor Forest"),
        ("kurn", "Kurn's Tower"),
        ("lakeofillomen", "Lake of Ill Omen"),
        ("lakerathe", "Lake Rathetear"),
        ("lavastorm", "The Lavastorm Mountains"),
        ("lfaydark", "The Lesser Faydark"),
        ("mischiefplane", "The Plane of Mischief"),
        ("mistmoore", "The Castle of Mistmoore"),
        ("misty", "Misty Thicket"),
        ("najena", "Najena"),
        ("necropolis", "Dragon Necropolis"),
        ("nektulos", "Nektulos Forest"),
        ("neriaka", "Neriak - Foreign Quarter"),
        ("neriakb", "Neriak - Commons"),
        ("neriakc", "Neriak - Third Gate"),
        ("newsebexp", "New Sebilis Expedition"),
        ("northkarana", "The Northern Plains of Karana"),
        ("nro", "The Northern Desert of Ro"),
        ("nurga", "The Mines of Nurga"),
        ("oasis", "The Oasis of Marr"),
        ("oggok", "Oggok"),
        ("oot", "The Ocean of Tears"),
        ("overthere", "The Overthere"),
        ("paineel", "Paineel"),
        ("paw", "The Lair of the Splitpaw"),
        ("permafrost", "Permafrost Keep"),
        ("qcat", "The Qeynos Aqueduct System"),
        ("qey2hh1", "The Western Plains of Karana"),
        ("qeynos", "South Qeynos"),
        ("qeynos2", "North Qeynos"),
        ("qeytoqrg", "Qeynos Hills"),
        ("qrg", "Surefall Glade"),
        ("rathemtn", "The Rathe Mountains"),
        ("rivervale", "Rivervale"),
        ("runnyeye", "The Liberated Citadel of Runnyeye"),
        ("sebilis", "The Ruins of Sebilis"),
        ("sirens", "Siren's Grotto"),
        ("skyfire", "The Skyfire Mountains"),
        ("skyshrine", "Skyshrine"),
        ("sleeper", "The Sleeper's Tomb"),
        ("soldunga", "Solusek's Eye"),
        ("soldungb", "Nagafen's Lair"),
        ("soltemple", "The Temple of Solusek Ro"),
        ("southkarana", "The Southern Plains of Karana"),
        ("sro", "The Southern Desert of Ro"),
        ("steamfont", "The Steamfont Mountains"),
        ("stonebrunt", "The Stonebrunt Mountains"),
        ("swampofnohope", "The Swamp of No Hope"),
        ("templeveeshan", "The Temple of Veeshan"),
        ("thurgadina", "The City of Thurgadin"),
        ("thurgadinb", "Icewell Keep"),
        ("timorous", "Timorous Deep"),
        ("tox", "Toxxulia Forest"),
        ("trakanon", "Trakanon's Teeth"),
        ("unrest", "The Estate of Unrest"),
        ("veeshan", "Veeshan's Peak"),
        ("velketor", "Velketor's Labyrinth"),
        ("wakening", "The Wakening Land"),
        ("warrens", "The Warrens"),
        ("warslikswood", "The Warsliks Woods"),
        ("westwastes", "The Western Wastes"),
        ("arena", "The Arena"),
    ];

    private readonly string _dir = Directory.CreateTempSubdirectory("eqbuddy-map-audit-").FullName;

    public ZoneMapCoverageTests()
    {
        // One folder holding every stem the game ships. Assertions demand the EXACT
        // stem, so a containment near-miss (neriakb landing on neriakc.txt) fails
        // loud instead of quietly showing the wrong zone.
        foreach (var (stem, _) in ClientZones)
            File.WriteAllText(Path.Combine(_dir, stem + ".txt"), "L 0, 0, 0, 1, 1, 0, 0, 0, 0");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void AssertResolves(string zoneName, string expectedStem, string source)
    {
        var file = ZoneMapFiles.Resolve(_dir, zoneName);
        Assert.True(file is not null,
            $"{source} '{zoneName}': no map resolved — expected {expectedStem}.txt; add an alias in ZoneMapFiles.Shortnames");
        Assert.True(string.Equals(Path.GetFileNameWithoutExtension(file), expectedStem, StringComparison.OrdinalIgnoreCase),
            $"{source} '{zoneName}': resolved {Path.GetFileName(file)}, expected {expectedStem}.txt");
    }

    [Fact]
    public void EveryClientZoneNameResolvesToItsOwnMap()
    {
        foreach (var (stem, display) in ClientZones)
        {
            AssertResolves(display, stem, "client");
            // The no-map guidance must name the same file resolution would load.
            Assert.Equal(stem, ZoneMapFiles.ExpectedShortname(display));
        }
    }

    /// <summary>
    /// ZoneGraph nodes the CLIENT ships no map for — curated, one reason per row.
    ///
    /// The test below exists so a new zone cannot SILENTLY lose its map, so an exemption
    /// has to be added deliberately and defended here rather than by loosening the rule.
    /// Same shape as `DeadSettingTests.Known` and `GameCommandsTests.SurfacesNeedingACommand`
    /// (trap 34): the list is the thing that keeps the guard honest.
    /// </summary>
    private static readonly Dictionary<string, string> ZonesWithNoClientMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Jaggedpine Forest"] =
                "A real EQ Legends zone — eqlwiki's page is tagged {{Classic Era}} and carries "
                + "its monster list, notable NPCs and unique items — but the client's own zone "
                + "table ships no map stem for it. It entered ZoneGraph in the 2026-08-24 "
                + "knowledge refresh, as a connection from Blackburrow that the wiki's Blackburrow "
                + "page had gained. Keeping it is the eqlwiki-is-the-source rule: travel routing "
                + "through it is correct, and the map window already has a no-map state to show.",

            ["Nedaria's Landing"] =
                "Arrived in the same 2026-08-24 refresh, but on WEAKER evidence than Jaggedpine "
                + "and it is worth saying so: eqlwiki has NO page of its own for it. It exists "
                + "only in the 'Adjacent Zones' line of the Jaggedpine Forest page, and the "
                + "client's zone table has neither a map nor an entry. Kept because the standing "
                + "rule is that eqlwiki is the source and we do not depart from it without "
                + "decisive evidence — 'the page it is named on may have been copied from live "
                + "EQ' is a suspicion, not evidence. **David ruled on it directly, 2026-08-24 "
                + "(asked with the question tool, because dropping it would be departing from "
                + "eqlwiki on game data): keep it, follow the wiki.** Worth knowing that "
                + "'no page of its own' is NOT the discriminator — 18 other ZoneGraph zones have "
                + "no page either (Sebilis, North Ro, Cazic Thule) and every one of them resolves "
                + "through the alias table. This is the only entry that resolves to nothing at all.",
        };

    [Fact]
    public void EveryZoneGraphZoneResolvesToAShippedStem()
    {
        var stems = ClientZones.Select(z => z.Stem).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var graph = ZoneGraph.LoadEmbedded();
        Assert.True(graph.ZoneCount > 100, "ZoneGraph failed to load");
        // Collected, not thrown on the first miss: a knowledge refresh adds zones in
        // BATCHES, and failing one at a time turns one review into N build-run cycles.
        var unmapped = graph.Zones
            .Where(z => !ZonesWithNoClientMap.ContainsKey(z))
            .Select(z => (Zone: z, Stem: ZoneMapFiles.ExpectedShortname(z)))
            .Where(z => !stems.Contains(z.Stem))
            .ToList();

        Assert.True(unmapped.Count == 0,
            "ZoneGraph names zones the game ships no map for. Add an alias in "
            + "ZoneMapFiles.Shortnames, or — if the client genuinely ships no map — a defended "
            + "row in ZonesWithNoClientMap:" + Environment.NewLine + "  "
            + string.Join(Environment.NewLine + "  ",
                unmapped.Select(u => $"{u.Zone} (expected {u.Stem}.txt)")));

        foreach (var zone in graph.Zones)
        {
            if (ZonesWithNoClientMap.ContainsKey(zone)) continue;
            AssertResolves(zone, ZoneMapFiles.ExpectedShortname(zone), "ZoneGraph");
        }
    }

    /// <summary>
    /// An exemption nobody can see is a blind spot, not an exemption. Every row above must
    /// still be BOTH in the graph and genuinely map-less — so the day a map ships for one of
    /// them, or the zone leaves the graph, this fails and the row comes out. Without this the
    /// list only ever grows, which is how a hold decays into a line people stop reading.
    /// </summary>
    [Fact]
    public void EveryNoMapExemptionIsStillNeeded()
    {
        var stems = ClientZones.Select(z => z.Stem).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var zones = ZoneGraph.LoadEmbedded().Zones.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (zone, reason) in ZonesWithNoClientMap)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{zone}: an exemption needs a reason");
            Assert.True(zones.Contains(zone),
                $"'{zone}' is exempted from the map check but is no longer in ZoneGraph — drop the row");
            Assert.False(stems.Contains(ZoneMapFiles.ExpectedShortname(zone)),
                $"'{zone}' now resolves to a shipped map — drop its ZonesWithNoClientMap row, "
                + "the exemption is hiding real coverage");
        }
    }

    [Fact]
    public void EverySpawnCatalogZoneNameResolvesToAShippedStem()
    {
        var stems = ClientZones.Select(z => z.Stem).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalog = SpawnCatalog.LoadEmbedded();
        Assert.True(catalog.Zones.Count > 100, "SpawnCatalog failed to load");
        foreach (var zone in catalog.Zones)
            // Zone, LogZoneName, and every log alias all reach the map window as
            // "the zone I'm in" — each must land on a real map.
            foreach (var name in new[] { zone.Zone, zone.LogZoneName }.Concat(zone.LogZoneAliases)
                         .Where(n => n.Length > 0))
            {
                var expected = ZoneMapFiles.ExpectedShortname(name);
                Assert.True(stems.Contains(expected),
                    $"SpawnCatalog '{name}': expected stem '{expected}' is no map file the game ships — add an alias in ZoneMapFiles.Shortnames");
                AssertResolves(name, expected, "SpawnCatalog");
            }
    }
}
