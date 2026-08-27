using System.Windows;

namespace EQBuddy;

/// <summary>
/// A thin host for <see cref="TravelView"/> (World PR 1 — the view lift, zero product
/// change). All the content, the route logic and the wording live in the view now; this
/// window owns only what a window owns: title, sizing, position memory, zoom.
/// </summary>
public sealed class TravelWindow : Window
{
    private readonly TravelView _view;

    public TravelWindow(MainWindow main)
    {
        _view = main.NewTravelView();
        Title = "Travel route";
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BgBrush");
        WindowZoom.Attach(this, "travel", main.Settings);
        // Resizable and REMEMBERED (David, 2026-08-25). The route is computed before the
        // window opens, so the height sampled at first render is a true one.
        WindowZoom.AllowResize(this, "travel", main.Settings);

        Content = _view.Body;
    }

    /// <summary>Re-run against the current zone — called on open and when you zone.</summary>
    public void RenderRoute() => _view.Render();

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
