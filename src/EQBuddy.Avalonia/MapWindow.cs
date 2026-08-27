using Avalonia.Controls;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// A thin host for <see cref="MapView"/> (World PR 1 — the view lift, zero product
/// change). All the content — canvas, spawn circles, named panel, trail, marker — lives
/// in the view now; this window owns only what a window owns: title, sizing, background.
/// Still takes <see cref="IZoneHost"/> directly (not <c>MainWindow</c>) — unit tests
/// (<c>ZoneWindowsRenderTests</c>) construct one against a fake host with no widget at all.
/// </summary>
public sealed class MapWindow : Window
{
    /// <summary>Brewall's EverQuest Maps — kept here too since <c>MapView.MapPackUrl</c>
    /// is internal and this constant had external callers before the lift.</summary>
    internal const string MapPackUrl = MapView.MapPackUrl;

    private readonly MapView _view;

    public MapWindow(IZoneHost host)
    {
        _view = new MapView(host);
        Title = "Zone map";
        Width = 560;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppTheme.BgBrush;
        Content = _view.Body;
    }

    /// <summary>Cheap follow tick from RefreshUi: reload only when the zone (or the
    /// marker) actually moved.</summary>
    public void MaybeRefresh(bool force = false) => _view.MaybeRefresh(force);

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF window's shape.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
