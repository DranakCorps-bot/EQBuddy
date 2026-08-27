using Avalonia.Controls;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// A thin host for <see cref="TravelView"/> (World PR 1 — the view lift, zero product
/// change). All the content, the route logic and the wording live in the view now;
/// this window owns only what a window owns: title, sizing, position memory, zoom.
/// Still takes <see cref="IZoneHost"/> directly — unit tests (<c>ZoneWindowsRenderTests</c>)
/// construct one against a fake host with no widget at all.
/// </summary>
public sealed class TravelWindow : Window
{
    private readonly TravelView _view;

    public TravelWindow(IZoneHost host)
    {
        _view = new TravelView(host);
        Title = "Travel route";
        CanResize = false;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppTheme.BgBrush;
        Content = _view.Body;
        // Ctrl+wheel zoom, persisted per window — possible now that the shared
        // adapter exists (it didn't when this window was first ported).
        WindowZoom.Attach(this, "travel", host.Settings);
    }

    /// <summary>Re-run against the current zone — called on open and when you zone.</summary>
    public void RenderRoute() => _view.Render();

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF window's shape.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
