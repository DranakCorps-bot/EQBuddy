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
}
