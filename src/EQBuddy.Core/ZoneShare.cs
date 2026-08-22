using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Zone-knowledge share strings ("EQBZ1-…"): a zone's spawn-point archive plus its
/// respawn timers, as one paste-safe string — the community layer's phase 1
/// (David, 2026-08-13: streamlined collaboration from within the community, no
/// server, nothing leaves your machine unless you paste it somewhere yourself).
/// Learned and manually-typed timers both travel ("things I set can be shared");
/// player-added custom named travel too.
///
/// Import is preview-first and add-only for observations. Timer values pass a
/// DEVIATION GATE before they're applied automatically: a submitted timer that
/// strays far from the zone's established clock (the Befallen-is-about-4:30 test)
/// is flagged for the importer to accept deliberately or leave — tanked timers
/// can't slip in quietly, here or in the review queue upstream. Because these
/// strings arrive from strangers on Discord and GitHub, decode is capped and
/// validated before anything is materialized (review 2026-08-13).
/// </summary>
public static class ZoneShare
{
    public const string Prefix = "EQBZ1-";

    /// <summary>A timer whose value differs from the current effective one by more
    /// than this fraction is flagged rather than auto-applied.</summary>
    public const double DeviationFlagFraction = 0.40;

    /// <summary>Decompression ceiling for a pasted string. A real zone archive is a
    /// few KB; a deflate bomb inflates thousands-to-one — cut it off long before it
    /// can hurt (a 100-point zone with timers is under 50 KB of JSON).</summary>
    internal const int MaxPayloadBytes = 4 * 1024 * 1024;
    /// <summary>More points than any real zone could hold = not a real archive.</summary>
    internal const int MaxPoints = 5000;

    public sealed class Payload
    {
        public string Zone { get; set; } = "";
        public List<SpawnPointLedger.SpawnPoint> Points { get; set; } = [];
        /// <summary>Respawn seconds by mob name — learned AND manually-set values
        /// both travel; the importer's protections are the deviation gate plus never
        /// overwriting their own manual edits. Wire name kept from v1.</summary>
        public Dictionary<string, double> LearnedTimers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="Triggered">The catalog says this mob has no cycle to import a number
    /// FOR — a triggered spawn (eqlwiki's own "Triggered") or a raid-instance boss, whose
    /// respawn is the game's instance clock. Shown flagged in the preview and never
    /// applied, even with includeFlagged.
    ///
    /// Triggered entries were refused from the start; RAID-INSTANCED ones were not, and
    /// that was churn nobody could see (Fable 5, release review of v1.99.2): an import
    /// would write a duration onto Lord Nagafen that `HealSuppressedOverrides` silently
    /// removed at the next launch. Same rule, same line — the catalog says there is no
    /// cycle, so there is nothing for a stranger's number to be about.</param>
    public sealed record TimerDiff(string Name, double? CurrentSeconds, double IncomingSeconds, bool Flagged,
        bool Triggered = false);

    public sealed class Preview
    {
        public Payload Payload { get; init; } = new();
        public int NewPoints { get; init; }
        public int RefinedPoints { get; init; }
        public int NewObservations { get; init; }
        public List<TimerDiff> Timers { get; init; } = [];
        /// <summary>Rows the "I trust this source" checkbox actually governs. A REFUSED
        /// row is excluded: the catalog says that mob has no cycle, so ticking the box
        /// applies nothing for it — counting it would make the checkbox promise work it
        /// never does ("silent no-ops are broken"). Found by Fable 5 in the v1.99.3
        /// release review; the engine was right and only the screen was wrong.</summary>
        public List<TimerDiff> FlaggedTimers => Timers.Where(t => t.Flagged && !t.Triggered).ToList();

        /// <summary>Rows refused outright, whatever the player ticks — a triggered spawn
        /// or a raid-instance boss. Shown, and shown as refused rather than as risky.</summary>
        public List<TimerDiff> RefusedTimers => Timers.Where(t => t.Triggered).ToList();
    }

    public static string Export(SpawnPointLedger.ZoneArchive archive, SpawnZone? zone, SpawnOverrides overrides)
    {
        var payload = new Payload { Zone = archive.Zone, Points = archive.Points };
        if (zone is not null)
            foreach (var entry in zone.Named)
                if (overrides.Find(archive.Zone, entry.Name) is { RespawnSeconds: { } s })
                    payload.LearnedTimers[entry.Name] = s;
        // Player-added named aren't in the catalog's list but are exactly the
        // "things I set" the export promises to carry.
        foreach (var (name, o) in overrides.CustomFor(archive.Zone))
            if (o.RespawnSeconds is { } cs)
                payload.LearnedTimers[name] = cs;

        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(json);
        return Prefix + Convert.ToBase64String(buffer.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Decode and validate a pasted string, or null if it isn't a healthy
    /// EQBZ payload. Decompression is capped, point/mob shapes are sanitized (a
    /// mob-less point would crash the map's projection path), and pet names fold
    /// the same way live observation folds them.</summary>
    public static Payload? TryDecode(string shareString)
    {
        Payload? payload;
        try
        {
            var trimmed = shareString.Trim();
            if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var b64 = trimmed[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            var padded = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var buffer = new MemoryStream(Convert.FromBase64String(padded));
            using var inflate = new DeflateStream(buffer, CompressionMode.Decompress);
            using var bounded = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = inflate.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (bounded.Length + read > MaxPayloadBytes) return null;   // bomb
                bounded.Write(chunk, 0, read);
            }
            bounded.Position = 0;
            payload = JsonSerializer.Deserialize<Payload>(bounded);
        }
        catch { return null; }
        if (payload is null || payload.Zone.Length == 0) return null;
        if (payload.Points is null || payload.Points.Count > MaxPoints) return null;

        payload.Points.RemoveAll(p => p?.Mobs is not { Count: > 0 });
        foreach (var p in payload.Points)
        {
            foreach (var petName in p.Mobs.Keys
                         .Where(n => n is null
                             || !string.Equals(SpawnPointLedger.FoldPetName(n), n, StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                if (petName is null || p.Mobs[petName] is null) { p.Mobs.Remove(petName!); continue; }
                var seen = p.Mobs[petName];
                p.Mobs.Remove(petName);
                var owner = SpawnPointLedger.FoldPetName(petName);
                var into = p.Mobs.TryGetValue(owner, out var o) && o is not null
                    ? o : p.Mobs[owner] = new SpawnPointLedger.MobSeen();
                into.Kills += seen.Kills;
                if (seen.LastKill > into.LastKill) into.LastKill = seen.LastKill;
            }
            p.Mobs = p.Mobs.Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }
        payload.Points.RemoveAll(p => p.Mobs.Count == 0);
        return payload;
    }

    /// <summary>Diff a decoded payload against local knowledge. Compatibility
    /// overload below still accepts the raw string.</summary>
    public static Preview Preview_(Payload payload,
        SpawnPointLedger.ZoneArchive local, SpawnZone? zone, SpawnOverrides overrides)
    {
        int newPoints = 0, refined = 0, newObs = 0;
        foreach (var incoming in payload.Points)
        {
            var mine = SpawnPointLedger.FindCluster(local.Points, incoming.LocY, incoming.LocX);
            if (mine is null) { newPoints++; newObs += incoming.TotalKills(); }
            else
            {
                refined++;
                foreach (var (name, seen) in incoming.Mobs)
                    newObs += Math.Max(0, seen.Kills - (mine.Mobs.GetValueOrDefault(name)?.Kills ?? 0));
            }
        }

        var timers = new List<TimerDiff>();
        foreach (var (name, incoming) in payload.LearnedTimers)
        {
            var entry = zone?.Named.FirstOrDefault(e => SpawnCatalog.NameMatches(e.Name, name));
            var current = overrides.Find(payload.Zone, name)?.RespawnSeconds
                ?? (entry is not null && zone is not null ? SpawnCatalog.EffectiveSeconds(zone, entry) : null);
            // The deviation gate: no baseline = flagged (nothing to corroborate);
            // a big swing from the established clock = flagged.
            var triggered = entry is { IsTriggered: true } or { RaidInstanced: true };
            var flagged = triggered || current is not { } cur
                || Math.Abs(incoming - cur) / Math.Max(1, cur) > DeviationFlagFraction;
            timers.Add(new TimerDiff(name, current, incoming, flagged, triggered));
        }

        return new Preview
        {
            Payload = payload, NewPoints = newPoints, RefinedPoints = refined,
            NewObservations = newObs, Timers = timers,
        };
    }

    public static Preview? PreviewImport(string shareString,
        SpawnPointLedger.ZoneArchive local, SpawnZone? zone, SpawnOverrides overrides) =>
        TryDecode(shareString) is { } payload ? Preview_(payload, local, zone, overrides) : null;

    /// <summary>Merge previewed points into an archive: ADD-ONLY (counts take the
    /// max of both sides — re-importing the same string twice adds nothing).</summary>
    public static void ApplyPoints(Preview preview, SpawnPointLedger.ZoneArchive local)
    {
        foreach (var incoming in preview.Payload.Points)
        {
            var mine = SpawnPointLedger.FindCluster(local.Points, incoming.LocY, incoming.LocX);
            if (mine is null)
            {
                local.Points.Add(incoming);
                continue;
            }
            foreach (var (name, seen) in incoming.Mobs)
            {
                var ours = mine.Mobs.TryGetValue(name, out var m) ? m
                    : mine.Mobs[name] = new SpawnPointLedger.MobSeen();
                ours.Kills = Math.Max(ours.Kills, seen.Kills);
                if (seen.LastKill > ours.LastKill) ours.LastKill = seen.LastKill;
            }
            // Add-only applies to attestations too: a confirmation from either
            // side sticks; an import never un-confirms the importer's own.
            mine.Confirmed |= incoming.Confirmed;
        }
    }

    /// <summary>Apply previewed timers: unflagged ones apply as learned overrides;
    /// flagged ones only when the importer explicitly said so; the importer's own
    /// MANUAL edits are never overwritten. One store write for the whole batch.</summary>
    public static void ApplyTimers(Preview preview, SpawnZone? zone,
        SpawnOverrides overrides, bool includeFlagged)
    {
        var changed = false;
        foreach (var diff in preview.Timers)
        {
            if (diff.Triggered) continue;   // no cycle exists to import a number for
            if (diff.Flagged && !includeFlagged) continue;
            // Never overwrite the importer's own MANUAL edit — their number wins
            // over anyone's, always.
            var existing = overrides.Find(preview.Payload.Zone, diff.Name);
            if (existing is { Learned: false, RespawnSeconds: not null }) continue;
            var o = overrides.GetOrAdd(preview.Payload.Zone, diff.Name);
            o.RespawnSeconds = diff.IncomingSeconds;
            o.Learned = true;
            // Someone else's number, and now recorded as such: an import used to be
            // indistinguishable from what your own kills taught the moment it landed.
            o.Imported = true;
            // Whatever this entry had SIGHTED is not true of the incoming value, and a
            // stale flag here would exempt a stranger's number from the self-heal that
            // exists to purge re-kill noise.
            o.Sighted = false;
            // A sharer's player-added named arrives as a custom named here too —
            // without the flag, a name the catalog doesn't know never starts a timer.
            if (existing is null && zone?.Named.Any(e => SpawnCatalog.NameMatches(e.Name, diff.Name)) != true)
                o.Custom = true;
            changed = true;
        }
        if (changed) overrides.Save();
    }

    /// <summary>Apply both halves — the single-call form the tests and simple
    /// callers use. The ledger's ApplyImport splits them around its lock.</summary>
    public static void Apply(Preview preview, SpawnPointLedger.ZoneArchive local,
        SpawnZone? zone, SpawnOverrides overrides, bool includeFlagged)
    {
        ApplyPoints(preview, local);
        ApplyTimers(preview, zone, overrides, includeFlagged);
    }
}
