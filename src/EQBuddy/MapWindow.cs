using System.Windows;

namespace EQBuddy;

/// <summary>
/// A thin host for <see cref="MapView"/> (World PR 1 — the view lift, zero product
/// change). All the content — canvas, spawn circles, named panel, trail, marker — lives
/// in the view now; this window owns only what a window owns: title, sizing, position,
/// ownership for its own child dialogs.
/// </summary>
public sealed class MapWindow : Window
{
    /// <summary>Brewall's EverQuest Maps — kept here too since <c>MapView.MapPackUrl</c>
    /// is internal and this constant had external callers before the lift.</summary>
    internal const string MapPackUrl = MapView.MapPackUrl;

    private readonly MapView _view;

    public MapWindow(MainWindow main)
    {
        _view = main.NewMapView();
        Title = "Zone map";
        Width = 560;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = main;
        SetResourceReference(BackgroundProperty, "BgBrush");
        Content = _view.Body;
    }

    /// <summary>Cheap follow tick from RefreshUi: reload only when the zone (or the
    /// marker) actually moved.</summary>
    public void MaybeRefresh(bool force = false) => _view.MaybeRefresh(force);

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
