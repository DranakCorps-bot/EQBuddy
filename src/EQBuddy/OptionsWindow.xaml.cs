using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Thin WPF view over the shared OptionsViewModel (EQBuddy.UI.Shared) — all
/// mappings/mutations live there; this class builds controls, forwards input, and
/// applies the visual side effects (scale/opacity/layout) to the main window.
/// </summary>
public partial class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private bool _ready;

    public OptionsWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _vm = new OptionsViewModel(main.Settings, main.PersistSettings);
        Owner = main;
        Width = Math.Clamp(_vm.OptionsWidth, MinWidth, MaxWidth);
        // The handle only exists once the window is sourced; re-clamp on move because the
        // user may drag it to a monitor with a different size or DPI.
        WindowZoom.Attach(this, "options", _main.Settings);
        SourceInitialized += (_, _) => ClampToMonitor();
        LocationChanged += (_, _) => ClampToMonitor();

        PinChipsCheck.IsChecked = _vm.PinWatchChips;
        SelectTab(_main.Settings.OptionsTab);

        BuildHudTab();
        BuildAlertsTabs();
        BuildLookAndBehaviour();

        _ready = true;

        // CenterOwner + SizeToContent positions before the size is known and can land
        // off-screen next to an edge-docked widget — place ourselves once measured:
        // beside the widget (left if room, else right), clamped to the work area.
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            var left = _main.Left - ActualWidth - 12;
            if (left < wa.Left + 8) left = _main.Left + _main.ActualWidth + 12;
            Left = Math.Max(wa.Left + 8, Math.Min(left, wa.Right - ActualWidth - 8));
            Top = Math.Max(wa.Top + 8, Math.Min(_main.Top, wa.Bottom - ActualHeight - 8));
            Activate();
        };
    }

    // THE CARDS TAB'S THREE STRAY SWITCHES WENT WITH THEIR CONTROLS on 2026-09-05 (SR-3).
    // Target drops, double-click-a-chip and the recent-rate picker are `SettingsHudView`'s
    // now, wired inside the block that draws them, gated on the same host-ready callback
    // this window used to gate them with — so the shell's Settings room gets three working
    // switches rather than three that render and do nothing.

    /// <summary>MainWindow calls this when the breakout editor writes the same storage,
    /// so an edit made there appears here immediately too. Forwarded: the editor is
    /// SettingsAlertsView's now, and this window is one of its two hosts.</summary>
    internal void RefreshBuffSetEditor() => _alerts?.RefreshBuffSetEditor();

    // ---- the four alert blocks (SR-4) ----

    /// <summary>
    /// This window's own instance of the alert blocks — never a shared one. A WPF
    /// <c>UIElement</c> has exactly one parent, so a block borrowed from another host would
    /// be torn out of whichever painted it last, silently (trap 45).
    /// </summary>
    private SettingsAlertsView? _alerts;

    /// <summary>
    /// The v1 arrangement, rebuilt from the lifted blocks: Watch keeps its own tab (with the
    /// <c>PinWatchChips</c> row XAML still declares beneath it), and the Alerts tab stacks the
    /// shared sound header over Buffs, Spawns and Crowd.
    ///
    /// **Order and headings come from <see cref="AlertSurface"/>, not from this file** — the
    /// first spend of a definition that has been sitting unused since before the pivot, and
    /// the reason the shell's Settings room cannot end up showing a different set of tabs in
    /// a different order from the window it replaces. The badges are real counts: rules
    /// written, buff buckets assembled, timers running.
    /// </summary>
    private void BuildAlertsTabs()
    {
        _alerts = new SettingsAlertsView(_main, _vm, () => _ready, FindResource, () => this);

        WatchBlockHost.Children.Add(_alerts.Block(AlertTab.Watch));

        TabAlertsPanel.Children.Add(_alerts.Header);
        foreach (var tab in _alerts.Tabs())
        {
            // Watch is the one that has a tab of its own here — it is the biggest editor in
            // Options and it had its own tab before the lift. Nothing else is skipped.
            if (tab.Tab == AlertTab.Watch) continue;
            TabAlertsPanel.Children.Add(_alerts.Heading(tab));
            TabAlertsPanel.Children.Add(_alerts.Block(tab.Tab));
        }
    }

    // ---- the HUD block (SR-3) ----

    /// <summary>
    /// This window's own instance of the HUD block — never a shared one. A WPF
    /// <c>UIElement</c> has exactly one parent, so a block borrowed from another host would be
    /// torn out of whichever painted it last, silently (trap 45). The shell's Settings room is
    /// the second host, and it builds its own.
    /// </summary>
    private SettingsHudView? _hud;

    /// <summary>
    /// The v1 arrangement, rebuilt from the lifted block. The host is bare: every heading,
    /// blurb, tick box and picker the `cards` tab used to declare is inside
    /// <see cref="SettingsHudView"/> now, in the order players already have.
    ///
    /// **The tab LINK above keeps saying "Cards &amp; windows"** — that label is this window's,
    /// it is shipped v1 copy, and the terminology ban's scope line exempts it (Bevel I-11 §3).
    /// The shell calls the same block "HUD".
    /// </summary>
    private void BuildHudTab()
    {
        _hud = new SettingsHudView(_main, _vm, () => _ready, FindResource);
        TabCardsPanel.Children.Add(_hud.Block);
    }

    // ---- tabs (1.67.0, David: "a wall of options... needs serious reorganization") ----

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBlock { Tag: string tab }) return;
        SelectTab(tab);
        if (_ready) { _main.Settings.OptionsTab = tab; _main.Settings.Save(); }
    }

    private void SelectTab(string tab)
    {
        var panels = new (string Key, System.Windows.Controls.TextBlock Link, UIElement Panel)[]
        {
            ("look", TabLook, TabLookPanel), ("alerts", TabAlerts, TabAlertsPanel),
            ("watch", TabWatch, TabWatchPanel), ("cards", TabCards, TabCardsPanel),
            ("behavior", TabBehavior, TabBehaviorPanel),
        };
        if (panels.All(p => p.Key != tab)) tab = "look";   // stale setting → home
        foreach (var (key, link, panel) in panels)
        {
            var active = key == tab;
            panel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            link.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            link.TextDecorations = active ? TextDecorations.Underline : null;
            link.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                active ? "AccentBrush" : "DimBrush");
        }
    }

    // ---- the Look and Behavior blocks (SR-1) ----

    /// <summary>
    /// This window's own instances of the two lifted blocks — never shared ones. A WPF
    /// <c>UIElement</c> has exactly one parent, so a block borrowed from another host would be
    /// torn out of whichever painted it last, silently (trap 45). The shell's Settings room is
    /// the second host, and it builds its own pair.
    /// </summary>
    private SettingsLookView? _look;
    private SettingsBehaviorView? _behavior;

    /// <summary>
    /// The v1 arrangement, rebuilt from the lifted blocks. Look hangs in a host that also keeps
    /// ONE window-chrome sentence declared beside it (the resize grips are this window's, not a
    /// block's); Behavior's host is bare.
    ///
    /// The blocks read their own values out of the shared <see cref="OptionsViewModel"/> as they
    /// build, so nothing here assigns a control — the long construction list this window used to
    /// carry for both tabs went with them.
    /// </summary>
    private void BuildLookAndBehaviour()
    {
        // The HUD block's panel rows resolve their Foreground with FindResource at build time
        // rather than through a DynamicResource, so a palette swap has to rebuild them. That is
        // the host's business, not the block's, which is why the block asks instead of reaching.
        _look = new SettingsLookView(_main, _vm, () => _ready, FindResource,
            () => _hud?.BuildCards());
        LookBlockHost.Children.Add(_look.Block);

        _behavior = new SettingsBehaviorView(_main, _vm, () => _ready, FindResource);
        TabBehaviorPanel.Children.Add(_behavior.Block);
    }

    /// <summary>
    /// Window chrome the Behavior block cannot own: ROUTING a key press to an armed hotkey
    /// recorder. The block rebuilds its rows on every click, so the button that was clicked no
    /// longer exists and nothing inside that panel has focus when the gesture arrives — the
    /// press reaches the WINDOW. The decision stays the block's; only the route is ours.
    /// </summary>
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_behavior?.HandleRecordingKey(e) == true) { e.Handled = true; return; }
        base.OnPreviewKeyDown(e);
    }

    // A member-less doc comment about the breakout tick boxes stood here until SR-3. It had
    // been orphaned by the OptionsCardsView lift long before this one — the paragraph it
    // documents is on SettingsHudView.BuildBreakouts, which is now the only copy.

    /// <summary>Called back by MainWindow.SetTrackSpawns so closing the Spawns window
    /// (or toggling the menu) updates the box while Options sits open. Forwarded to the
    /// block that owns it.</summary>
    internal void SyncTrackSpawns(bool on) => _alerts?.SyncTrackSpawns(on);

    private void OnPinChipsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.PinWatchChips = PinChipsCheck.IsChecked == true;
    }

    // Resize state captured at drag start. Deriving each frame from the cursor's absolute
    // position rather than accumulating DragDelta avoids the feedback jitter you get when
    // the thumb moves with the window (which the left grip does).
    private double _dragCursorX, _dragLeft, _dragWidth;

    private void OnResizeStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragCursorX = CursorX();
        _dragLeft = Left;
        _dragWidth = Width;
    }

    private void OnResizeRightDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) =>
        Width = Math.Clamp(_dragWidth + (CursorX() - _dragCursorX), MinWidth, MaxWidth);

    /// <summary>Left edge: grow leftwards, keeping the right edge where it is.</summary>
    private void OnResizeLeftDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var width = Math.Clamp(_dragWidth - (CursorX() - _dragCursorX), MinWidth, MaxWidth);
        Left = _dragLeft + (_dragWidth - width);
        Width = width;
    }

    private void OnResizeCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        _vm.OptionsWidth = Width;

    /// <summary>Cursor X in device-independent units (the space Left/Width live in).</summary>
    private double CursorX()
    {
        Native.GetCursorPos(out var p);
        return p.X * DipScale().X;
    }

    private (double X, double Y) DipScale()
    {
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        return m is { } t ? (t.M11, t.M22) : (1.0, 1.0);
    }

    /// <summary>
    /// Cap the window to the work area of whichever monitor it is on. At high Windows
    /// scaling (a tester runs 300%) the full options panel is taller than the screen, so
    /// without this the bottom is simply unreachable — the ScrollViewer only helps once
    /// the window itself is bounded. Recomputed on move because monitors differ in both
    /// size and DPI.
    /// </summary>
    private void ClampToMonitor()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var monitor = Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest);
        var info = new Native.MonitorInfo { cbSize = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return;

        var scale = DipScale();
        var workHeight = (info.rcWork.bottom - info.rcWork.top) * scale.Y;
        var workWidth = (info.rcWork.right - info.rcWork.left) * scale.X;
        // Leave a little breathing room so the rounded border isn't flush to the edge.
        MaxHeight = Math.Max(MinHeight + 1, workHeight - 24);
        MaxWidth = Math.Max(MinWidth + 1, Math.Min(900, workWidth - 24));
        if (Width > MaxWidth) Width = MaxWidth;
    }

    private static class Native
    {
        public const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point { public int X, Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }

    // THE GEAR CHECKLIST IMPORT HANDLERS WENT WITH THEIR CONTROLS on 2026-09-05 (SR-2).
    // The status line, the file picker, the website link and Clear are `GearCardView`'s
    // now — the surface the import produces — and the mutation itself is
    // `GearChecklistImporter.Apply`/`.Clear` in Core, where the "a re-import keeps your
    // ticks" rule can be tested rather than asserted about a window.
    //
    // What this window no longer needs, and did: nothing. The block read
    // `_main.Settings.GearChecklist` and called two `MainWindow` methods; every one of
    // those readings is on the card, which was already holding the same settings object.

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
