using System.Text.Json;
using System.Text.Json.Serialization;

namespace EQBuddy.Companion;

/// <summary>
/// The versioned wire envelope the phone receives. Lives in the Companion project (not
/// UI.Shared) because it IS the companion's wire contract: the server serializes it, the
/// embedded page consumes it, and its shape must move with the protocol — not with
/// desktop presentation helpers.
///
/// SECTIONED by design: each surface is its own nullable section property (null =
/// not subscribed, or gated off on the desktop — nulls vanish from the JSON), so
/// per-client projection is a cheap record copy that nulls what that phone didn't
/// ask for, and a mez-only pocket phone never pays for loot payloads. New surfaces
/// are new section properties plus a <see cref="CompanionSurfaces"/> name — the
/// envelope (kind/protocol/version/identity/offered) never changes shape.
///
/// STICKY sections: <see cref="Theme"/> and the map's geometry are big and change
/// rarely, so they are withheld from a device that already holds the current stamp
/// (see <see cref="ForClient"/> and <see cref="CompanionClientState"/>) rather than
/// re-serialized every push.
///
/// Extensibility: every message carries a <see cref="Kind"/> ("snapshot" now,
/// "heartbeat" for keepalives; deltas bolt on as new kinds) and a
/// <see cref="Protocol"/> number the page checks before trusting the shape.
/// </summary>
public sealed record CompanionSnapshot
{
    /// <summary>Bumped to 2 for the Phase 2 surface set: theme, map, mez, buffs,
    /// combat, loot, progress and the three checklists. A protocol-1 page and a
    /// protocol-2 server simply don't talk — both refuse rather than half-render.
    /// Bumped to 3 when the separate Epic/Sky sections folded into the quest surface;
    /// a stale open page goes quiet until reloaded, and the reload is served by the
    /// same exe, so page and server can only disagree for that one window.</summary>
    public const int CurrentProtocol = 3;

    public string Kind { get; init; } = "snapshot";
    public int Protocol { get; init; } = CurrentProtocol;
    /// <summary>Moves when the underlying data moves (see CompanionProjection.Fingerprint);
    /// equal versions mean the phone may skip re-rendering.</summary>
    public long Version { get; init; }
    public DateTime SentAtUtc { get; init; }
    public CompanionIdentity Identity { get; init; } = new("", "", "");

    /// <summary>The surfaces the DESKTOP is willing to send (the owner's gate in
    /// Options → EQBuddy Mobile). The page builds its ⚙ Screens picker from this.</summary>
    public IReadOnlyList<string> Offered { get; init; } = [];

    /// <summary>Surfaces this client subscribed to that the desktop is NOT offering —
    /// the explicit "not offered" marker, so a gated surface reads as a told "no"
    /// on the phone rather than silence. Null (omitted) when there are none.</summary>
    public IReadOnlyList<string>? NotOffered { get; init; }

    /// <summary>The desktop's live palette. Not a surface (it can't be gated off —
    /// a phone with no colors is a broken phone); sent on connect and after a swap.</summary>
    public CompanionThemeSection? Theme { get; init; }

    // ---- sections, one nullable property per surface ----
    public CompanionMapSection? Map { get; init; }
    public CompanionSpawnSection? Spawns { get; init; }
    public CompanionTravelSection? Travel { get; init; }
    public CompanionMezSection? Mez { get; init; }
    public CompanionBuffsSection? Buffs { get; init; }
    public CompanionCombatSection? Combat { get; init; }
    public CompanionSessionSection? Session { get; init; }
    public CompanionLootSection? Loot { get; init; }
    public CompanionProgressSection? Progress { get; init; }
    public CompanionQuestsSection? Quests { get; init; }
    public CompanionChecklistSection? Gear { get; init; }

    /// <summary>This snapshot reduced to what one client subscribed to:
    /// null subscriptions = everything offered. Unknown or gated names land in
    /// <see cref="NotOffered"/>. A cheap record copy — safe per client per push.</summary>
    public CompanionSnapshot ForSubscription(IReadOnlyList<string>? surfaces)
    {
        if (surfaces is null) return this;
        var wanted = new HashSet<string>(surfaces, StringComparer.OrdinalIgnoreCase);
        var missing = surfaces
            .Where(s => !Offered.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return this with
        {
            Map = wanted.Contains(CompanionSurfaces.Map) ? Map : null,
            Spawns = wanted.Contains(CompanionSurfaces.Spawns) ? Spawns : null,
            Travel = wanted.Contains(CompanionSurfaces.Travel) ? Travel : null,
            Mez = wanted.Contains(CompanionSurfaces.Mez) ? Mez : null,
            Buffs = wanted.Contains(CompanionSurfaces.Buffs) ? Buffs : null,
            Combat = wanted.Contains(CompanionSurfaces.Combat) ? Combat : null,
            Session = wanted.Contains(CompanionSurfaces.Session) ? Session : null,
            Loot = wanted.Contains(CompanionSurfaces.Loot) ? Loot : null,
            Progress = wanted.Contains(CompanionSurfaces.Progress) ? Progress : null,
            Quests = wanted.Contains(CompanionSurfaces.Quests) ? Quests : null,
            Gear = wanted.Contains(CompanionSurfaces.Gear) ? Gear : null,
            NotOffered = missing.Count > 0 ? missing : null,
        };
    }

    /// <summary>Subscription filtering PLUS the sticky payloads this device already
    /// holds: the theme and the zone's map geometry are dropped when their stamps
    /// match what was last sent to it, and <paramref name="state"/> advances to what
    /// this message carries. Called once per client per push; the state object belongs
    /// to that one connection and nothing else reads it.</summary>
    public CompanionSnapshot ForClient(IReadOnlyList<string>? surfaces, CompanionClientState state)
    {
        var snap = ForSubscription(surfaces);

        // A sticky payload is held by the device only while its SECTION keeps arriving.
        // The page carries the withheld copy forward off the PREVIOUS payload, so a
        // payload that omitted the section left it nothing to copy from — and this memo
        // said "already sent" regardless, withholding it for the life of the connection.
        // That is David's 2026-08-21 phone: Quests stuck forever on "Waiting for the
        // quest catalog from the PC…". Two doors, both real — the ⚙ Screens picker
        // adding a surface the connect push had already spent the payload on, and the
        // desktop gating a surface off and back on under a device that never moved.
        // Forgetting here costs one re-send; not forgetting costs the surface.
        if (!state.HeldQuests) state.QuestCatalogStamp = null;
        if (!state.HeldMap) state.MapGeometryStamp = null;

        if (snap.Theme is { } theme)
        {
            if (theme.Stamp == state.ThemeStamp) snap = snap with { Theme = null };
            else state.ThemeStamp = theme.Stamp;
        }
        if (snap.Map is { } map)
        {
            if (map.GeometryStamp == state.MapGeometryStamp)
            {
                // Same zone, same picture: the device redraws from what it kept.
                if (map.Geometry is not null) snap = snap with { Map = map with { Geometry = null } };
            }
            else state.MapGeometryStamp = map.GeometryStamp;
        }
        if (snap.Quests is { Catalog: not null } quests)
        {
            // The quest catalog is the same kind of payload as map geometry — big,
            // static, pointless to repeat — and gets the same treatment.
            if (quests.CatalogStamp == state.QuestCatalogStamp)
                snap = snap with { Quests = quests with { Catalog = null } };
            else state.QuestCatalogStamp = quests.CatalogStamp;
        }

        // What this device is holding after this message — the question the stamps
        // above are only meaningful against.
        state.HeldQuests = snap.Quests is not null;
        state.HeldMap = snap.Map is not null;
        return snap;
    }

    /// <summary>One serializer config for everything that crosses the wire — camelCase,
    /// so the page reads <c>msg.spawns.timers</c> without C#-casing surprises.</summary>
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);
}

/// <summary>What one connected device already holds, so the server can stop resending
/// it. Per connection and thrown away with it — a reconnecting phone is told
/// everything again, which is exactly right after a restart on either side.</summary>
public sealed class CompanionClientState
{
    public string? ThemeStamp { get; set; }
    public string? MapGeometryStamp { get; set; }
    public string? QuestCatalogStamp { get; set; }

    /// <summary>Whether the LAST message to this device carried the section at all.
    /// A stamp only says "this device was sent it once"; the page can only still hold
    /// it if the section has been on every payload since, because that is where it
    /// carries the copy forward from. False re-ships on the next push.</summary>
    public bool HeldQuests { get; set; }
    public bool HeldMap { get; set; }
}

/// <summary>Who and where the data comes from — the page's header line.</summary>
public sealed record CompanionIdentity(string Character, string Zone, string AppVersion);
