using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// What the zone/map/travel views need from the app shell — verbatim from Avalonia's
/// <c>IZoneHost</c> (World PR 1, Fable 5's plan, finding 1: its own doc comment says its
/// member names "mirror the WPF MainWindow surface one-for-one", and WPF's reach into
/// these three windows is exactly that member set). Member names mirror the Avalonia
/// interface one-for-one; wiring is "MainWindow implements IZoneHost" — the members it
/// already grows for the spawn layer satisfy this implicitly (each was already declared
/// <c>internal</c>; this PR widens the ones this interface needs to <c>public</c>, which
/// implicit interface implementation requires).
///
/// <see cref="PlayAlertSound"/> is the one member neither lane's <c>IZoneHost</c> had
/// before this PR — <c>SpawnsView</c> needs it for the per-named bell preview
/// (finding 2), and both <c>MainWindow</c>s already carry the identical signature.
/// </summary>
public interface IZoneHost
{
    AppSettings Settings { get; }
    string CurrentZoneName { get; }
    StatsSnapshot CurrentSnapshot();
    SpawnTimers SpawnTimers { get; }
    SpawnPointLedger SpawnPoints { get; }
    SpawnCatalog SpawnCatalogData { get; }
    SpawnOverrides SpawnOverridesStore { get; }
    ZoneGraph ZoneGraph { get; }
    MobLookupResult? WikiMobResult(string name);
    void EnsureMobLookup(string name);
    void PlayAlertSound(string choiceOrPath, bool coalesce = false);
    void DropCampMarker();

    /// <summary>
    /// What this character is going after, already joined to places (DRA-216 D5, S13).
    ///
    /// <para><b>The host answers it, rather than the view asking the store</b>, for the reason
    /// every other member here is a host read: the desktop map and the phone's map must be
    /// looking at ONE answer, and two views calling <see cref="GearTargets.For"/> with their
    /// own arguments is the shape where whichever ran last wins (trap 33). The host memoizes
    /// it and hands the same object to both.</para>
    ///
    /// <para>An empty set is the ordinary state — nobody has tracked anything — and every
    /// reader draws nothing at all on it.</para>
    /// </summary>
    GearTargetSet GearTargets { get; }
}
