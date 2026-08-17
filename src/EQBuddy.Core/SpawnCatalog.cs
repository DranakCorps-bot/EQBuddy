using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EQBuddy.Core;

/// <summary>One named mob in the shipped spawn catalog.</summary>
public sealed class SpawnEntry
{
    public string Name { get; set; } = "";
    /// <summary>Other names the SAME creature's kill line may use. Legends renames
    /// classic named mobs — "the ghoul lord" is "Hoptor Thaggelum" in-game, classic
    /// name demoted to a subtitle (issue #38, confirmed from chrstahl's kill line) —
    /// and the wiki mostly still titles pages by the classic names.</summary>
    public List<string> Aliases { get; set; } = [];
    /// <summary>Documented respawn in seconds, or null when nobody has written it down —
    /// the zone's <see cref="SpawnZone.NamedDefaultSeconds"/> is the fallback, and a null
    /// there too means the timer has to come from the player.</summary>
    public double? RespawnSeconds { get; set; }
    /// <summary>True when <see cref="RespawnSeconds"/> was MEASURED (camped-on-sight log
    /// timestamps), not copied from a wiki. Trusted timers disable re-kill learning for
    /// the entry: a shorter gap against a measured clock means multiple spawn points
    /// sharing the name, not a faster respawn (David, 2026-08-04 — a learned 328s from
    /// multi-spawn Orc Taskmaster kills sat under Crushbone's measured 738s clock).</summary>
    public bool Trusted { get; set; }
    /// <summary>True when creatures of this name spawn at SEVERAL places in the zone
    /// (Crushbone's Royal Guards, trainers on Trainer Hill, Slaver Pit taskmasters —
    /// David, 2026-08-09: a trainer "dropped down" onto him while his camp's clock
    /// still ran). Sightings prove nothing for these — a same-named stranger acting
    /// is not this camp's respawn — so sighting completion AND learning stand down;
    /// their clocks are kill-driven only. Actual named/boss mobs (the default) keep
    /// the full sighting treatment.</summary>
    public bool MultiSpawn { get; set; }
    public string Variance { get; set; } = "";
    /// <summary>The placeholder NPC whose death also restarts this named's cycle, or "".
    /// Displayed as "Named — Placeholder (npc)" and matched against kill lines like the
    /// named itself: killing the PH is what a camp mostly does, and in classic spawn
    /// cycles it runs the same clock.</summary>
    public string Placeholder { get; set; } = "";
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    /// <summary>True for bosses that live in RAID INSTANCES (#109, Frankthetankk):
    /// a fresh instance spawns its own copy — daily per difficulty tier, weekly full
    /// reset — so "kill it, wait N, it respawns here" is a fiction for them. Marked at
    /// load by cross-referencing <see cref="RaidTargetCatalog"/> (the game's own
    /// achievements name the bosses; the harvested wiki numbers predate instancing).
    /// Instanced entries start no countdown and show no respawn default — unless the
    /// player types a duration themselves, which outranks this like everything else.</summary>
    public bool RaidInstanced { get; set; }
}

/// <summary>A zone's named mobs plus its zone-wide default named-respawn.</summary>
public sealed class SpawnZone
{
    public string Zone { get; set; } = "";
    /// <summary>The name the game's "You have entered X." line uses, when it differs
    /// from the wiki's title for the zone.</summary>
    public string LogZoneName { get; set; } = "";
    /// <summary>Additional names the zone-enter line might use — Legends renames zones
    /// versus classic ("The Ruins of ANCIENT Guk" vs classic "…Old Guk", issue #36), and
    /// a wrong single LogZoneName silently kills every timer in the zone. Short /who
    /// codes (guktop) belong here too.</summary>
    public List<string> LogZoneAliases { get; set; } = [];
    public double? NamedDefaultSeconds { get; set; }
    /// <summary>True when <see cref="NamedDefaultSeconds"/> is a MEASURED zone clock
    /// (see SpawnEntry.Trusted) — entries riding it don't re-kill-learn.</summary>
    public bool NamedDefaultTrusted { get; set; }
    /// <summary>True for PURE raid zones — the Planes (#109, Frankthetankk's report:
    /// "Plane of Fear / Plane of Hate and similar raid content", minis included).
    /// Kills inside an INSTANCE of such a zone start no automatic countdowns.
    /// Deliberately NOT set for dual-use dungeons that merely house a raid boss
    /// (Kedge, Nagafen's Lair, Permafrost): their tier instances demonstrably
    /// respawn — the Befallen/Crushbone clocks were MEASURED inside tier variants —
    /// and a leveling group camping there must keep its timers (2026-08-13 review:
    /// the first cut suppressed those too). Their dump-listed bosses stay covered
    /// by the per-entry <see cref="SpawnEntry.RaidInstanced"/> flag.</summary>
    public bool RaidZone { get; set; }
    public List<SpawnEntry> Named { get; set; } = [];

    public bool MatchesZoneName(string zoneName)
    {
        if (zoneName.Length == 0) return false;
        if (Eq(Zone, zoneName) || Eq(LogZoneName, zoneName)
            // "You have entered The Estate of Unrest." vs catalog "Estate of Unrest":
            // containment either way covers article and prefix differences.
            || Contains(zoneName, Zone) || Contains(Zone, zoneName)
            || (LogZoneName.Length > 0 && (Contains(zoneName, LogZoneName) || Contains(LogZoneName, zoneName))))
            return true;
        foreach (var alias in LogZoneAliases)
            if (Eq(alias, zoneName) || Contains(zoneName, alias) || Contains(alias, zoneName))
                return true;
        return false;

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        static bool Contains(string hay, string needle) =>
            needle.Length >= 4 && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// The shipped database of named-mob spawn timers (SPAWN-001), embedded so installs need
/// no extra files. Sources: eqlwiki.com (the EQ Legends community wiki, authoritative)
/// with classic-EQ references filling gaps — see the JSON's comment field. Times are
/// community lore, not game data: they ship as *defaults*, and every number is editable
/// in the Spawns window (edits live in spawn-overrides.json via <see cref="SpawnOverrides"/>,
/// never in this file, so an app update refreshing the catalog can't eat anyone's edits).
/// </summary>
public sealed class SpawnCatalog
{
    public List<SpawnZone> Zones { get; init; } = [];

    private sealed class CatalogFile
    {
        public string Comment { get; set; } = "";
        public List<SpawnZone> Zones { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static SpawnCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.SpawnCatalog.json")
            ?? throw new InvalidOperationException("SpawnCatalog.json missing from resources");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, JsonOpts);
        var catalog = new SpawnCatalog { Zones = file?.Zones ?? [] };
        MarkRaidInstanced(catalog, RaidTargetCatalog.Default);
        return catalog;
    }

    /// <summary>Flags every catalog entry whose name (or alias) the raid-target
    /// catalog knows as a raid-instance boss — the achievements dump is the
    /// authority on who lives in an instance, the spawn catalog only inherited
    /// classic open-world numbers from the wiki harvest — and every ZONE the raid
    /// catalog names (see <see cref="SpawnZone.RaidZone"/>). Separate from
    /// LoadEmbedded so tests can exercise the cross-reference with hand-built
    /// catalogs.</summary>
    public static void MarkRaidInstanced(SpawnCatalog catalog, RaidTargetCatalog raidTargets)
    {
        foreach (var zone in catalog.Zones)
        {
            // Pure raid zones only: the Planes. A dual-use dungeon housing one raid
            // boss must NOT have its whole zone gated — see the RaidZone doc.
            zone.RaidZone = zone.Zone.StartsWith("Plane of ", StringComparison.OrdinalIgnoreCase)
                && raidTargets.Zones.Any(rz => zone.MatchesZoneName(rz.Zone));
            foreach (var entry in zone.Named)
                if (raidTargets.IsRaidBoss(entry.Name)
                    || entry.Aliases.Any(raidTargets.IsRaidBoss))
                    entry.RaidInstanced = true;
        }
    }

    /// <summary>The catalog zone the given log zone name refers to, or null. EQ Legends
    /// runs instanced tier variants — "Befallen 1 (Awakened)", "Clan Crushbone 2
    /// (Adaptive)" — which resolve to their base zone (observed in eqlog_Hugzee
    /// 2026-08-02). The variant's own named differ from classic; until the catalog
    /// learns them, resolving to the base zone at least keeps Follow and manual timers
    /// working there.</summary>
    public SpawnZone? FindZone(string zoneName)
    {
        // Exact/log-name matches beat containment so "Qeynos" resolves to Qeynos, not
        // Qeynos Hills.
        var found = Zones.FirstOrDefault(z =>
                string.Equals(z.Zone, zoneName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(z.LogZoneName, zoneName, StringComparison.OrdinalIgnoreCase))
            ?? Zones.FirstOrDefault(z => z.MatchesZoneName(zoneName));
        if (found is not null) return found;

        var baseName = StripTierVariant(zoneName);
        return baseName == zoneName ? null : FindZone(baseName);
    }

    /// <summary>"Befallen 1 (Awakened)" → "Befallen"; anything without the trailing
    /// tier pattern comes back unchanged.</summary>
    public static string StripTierVariant(string zoneName) =>
        System.Text.RegularExpressions.Regex
            .Replace(zoneName, @"\s+\d+(\s*\([^)]*\))?$", "").Trim();

    /// <summary>
    /// True when a "You have entered X." zone name is an INSTANCE rather than the open
    /// world (#109, the Tranix/mini-boss cases). EQ Legends states instance-ness only in
    /// the zone name — no kill, lockout, or creation line carries it (fact verified
    /// against a community 1.4M-line log survey, 2026-08-13). The observed shapes:
    ///
    ///   "The Plane of Hate"                      → open world
    ///   "The Plane of Hate - Solo"               → base instance
    ///   "Nagafen's Lair - Group 3 (Fused)"       → tiered instance
    ///   "Najena 4 (Refined)"                     → tiered instance, no Solo/Group word
    ///
    /// Instance-ness alone does NOT suppress timers — ordinary dungeons run as tier
    /// instances and their mobs demonstrably respawn (our Befallen and Crushbone zone
    /// clocks were measured inside "4 (Refined)"-style variants). The suppression is
    /// instanced AND a raid zone (<see cref="SpawnZone.RaidZone"/>), where kills run on
    /// lockouts and an open-world clock is fiction. Unrecognized shapes fall toward
    /// "open world" — the failure mode there is today's behavior, not a
    /// silently-suppressed real timer.
    /// </summary>
    public static bool IsInstancedZoneName(string zoneName) =>
        InstanceTier.IsInstance(InstanceTier.FromZoneName(zoneName));

    /// <summary>Effective respawn seconds for an entry in a zone: the entry's own timer,
    /// else the zone default, else null (unknown — the player has to supply one).
    /// Raid-instance bosses have NO effective respawn (#109): their wiki numbers
    /// describe a world that predates instancing, and a countdown built on them is
    /// misleading rather than helpful. A player-typed override still applies — it's
    /// checked before this everywhere.</summary>
    public static double? EffectiveSeconds(SpawnZone zone, SpawnEntry entry) =>
        entry.RaidInstanced ? null : entry.RespawnSeconds ?? zone.NamedDefaultSeconds;

    /// <summary>Case-insensitive name equality that shrugs off leading articles, a
    /// trailing plural, and name punctuation: catalog names are wiki titles
    /// ("a froglok ghoul lord"), kill lines arrive normalized ("froglok ghoul lord"),
    /// placeholder notes are sometimes plural ("orc centurions") — and wikis routinely
    /// drop the EQ backtick (wiki "Skeleton Lrodd" vs the log's "Skeleton L`rodd",
    /// found by David mid-camp), which must never cost anyone a timer.</summary>
    public static bool NameMatches(string catalogName, string killedName)
    {
        if (catalogName.Length == 0 || killedName.Length == 0) return false;
        return Fold(catalogName).Equals(Fold(killedName), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Forgiving-but-bounded match for wiki typos (the Velious page spells Keljemor
    /// "Leljemor"; suggested by David): edit distance ≤1 on the folded names, ≤2 from
    /// twelve characters up, and never for names under six characters — "Red V" must
    /// not match "Red X". Callers must prefer exact matches: fuzzy is the fallback,
    /// checked only after every exact candidate has missed, so a typo'd catalog entry
    /// can't steal a kill from a correct one.
    /// </summary>
    public static bool NameMatchesFuzzy(string catalogName, string killedName)
    {
        if (NameMatches(catalogName, killedName)) return true;
        var a = Fold(catalogName).ToLowerInvariant();
        var b = Fold(killedName).ToLowerInvariant();
        if (a.Length < 6 || b.Length < 6) return false;
        // Same-length names that differ at the word's END are siblings, not typos:
        // "orc trainee" bridged onto the Orc Trainer clock, one substitution apart
        // (David, live in Crushbone 2026-08-09). EQ's rank ladders inflect the tail
        // (trainee/trainer); wiki typos live at the front or middle (Keljemor spelled
        // "Leljemor"). Length-changing edits — dropped letters, truncated log
        // captures — stay forgiven anywhere.
        if (a.Length == b.Length && (a[^1] != b[^1] || a[^2] != b[^2])) return false;
        // Names sharing every word but the LAST are siblings from a family, even when
        // the last words change length: Sol A's trash clockworks CWG Model XA/XB/XC
        // all sat within two edits of the named CWG Model EXG, so ordinary kills ran
        // (and re-ran) his clock (2026-08-16). The shared prefix eats most of the
        // name, leaving the whole distinguishing part inside the edit budget — so a
        // differing last word only stays forgiven when one is a truncation of the
        // other ("Gynok Molto" for Gynok Moltor); anything else is a different mob.
        var aCut = a.LastIndexOf(' ');
        var bCut = b.LastIndexOf(' ');
        if (aCut >= 0 && bCut >= 0 && a[..aCut].Equals(b[..bCut], StringComparison.Ordinal))
        {
            var aLast = a[(aCut + 1)..];
            var bLast = b[(bCut + 1)..];
            if (!aLast.StartsWith(bLast, StringComparison.Ordinal)
                && !bLast.StartsWith(aLast, StringComparison.Ordinal)) return false;
        }
        var budget = Math.Max(a.Length, b.Length) >= 12 ? 2 : 1;
        return WithinEditDistance(a, b, budget);
    }

    /// <summary>Two names from one mob FAMILY: every word shared but the last, which
    /// differs outright — "CWG Model XA" beside "CWG Model EXG". #181 learned this the
    /// expensive way (the siblings were bridging onto the named's clock); auto-discovery
    /// needs the same fact for the opposite reason. A serial-named trash clockwork has
    /// no article and reads as a proper name, so the article convention alone would time
    /// every one of them — but the catalog listing the family's NAMED is a statement that
    /// the rest of the family is trash.</summary>
    public static bool SharesNameFamily(string a, string b)
    {
        a = Fold(a).ToLowerInvariant();
        b = Fold(b).ToLowerInvariant();
        var aCut = a.LastIndexOf(' ');
        var bCut = b.LastIndexOf(' ');
        if (aCut < 0 || bCut < 0) return false;
        if (!a[..aCut].Equals(b[..bCut], StringComparison.Ordinal)) return false;
        return !a[(aCut + 1)..].Equals(b[(bCut + 1)..], StringComparison.Ordinal);
    }

    private static string Fold(string s)
    {
        s = s.Trim().Replace("`", "").Replace("'", "");
        foreach (var article in (string[])["a ", "an ", "the "])
            if (s.Length > article.Length && s.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            { s = s[article.Length..]; break; }
        if (s.EndsWith('s') && !s.EndsWith("ss", StringComparison.Ordinal)) s = s[..^1];
        return s;
    }

    /// <summary>Banded Levenshtein: true when edit distance ≤ <paramref name="max"/>.</summary>
    private static bool WithinEditDistance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return false;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            var rowMin = cur[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, cur[j]);
            }
            if (rowMin > max) return false;   // the whole row exceeded the budget — bail
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length] <= max;
    }
}
