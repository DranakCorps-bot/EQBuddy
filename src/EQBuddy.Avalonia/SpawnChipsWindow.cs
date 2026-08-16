using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Compact ambient face of spawn tracking. Every active timer on the current server is
/// one chip; the stack moves as a unit and exists only while timers do. The full spawn
/// browser remains available independently through a double-click or the main menu. A
/// due chip remains for one minute, then Core expires it if it was not clicked sooner.
/// </summary>
public sealed class SpawnChipsWindow : Window
{
    private readonly MainWindow _main;
    private readonly SpawnsViewModel _vm;
    private readonly AppSettings _settings;
    private readonly StackPanel _panel = new();
    private readonly List<TextBlock> _countdowns = [];
    // The gauge fills live here so the per-tick refresh can advance them: rebuilds
    // only happen on a signature change (zone|name|due|icon), and a fill painted
    // only at rebuild froze at whatever fraction that moment saw — WPF's audit
    // finding 14, fixed there and in MezChipsWindow but hand-copied stale here.
    private readonly List<(Grid Track, Border Fill)> _gauges = [];
    private List<SpawnChip> _chips = [];
    private string _signature = "";
    // Fallback placements must never persist (#117): where the window was placed at
    // open and whether that was the player's saved spot.
    private bool _restoredSaved;
    private bool _openedOnce;
    private bool _userMoved;
    private ChipStackAnchor? _anchor;
    /// <summary>Tests can't drag a headless window; this is the drag signal's test seam.</summary>
    internal void MarkUserMovedForTests() => _userMoved = true;
    /// <summary>The last position we can vouch for — the spot asked for at open, or a
    /// drag. Never a read taken while the window is opening or closing (#169).</summary>
    private LastVisiblePosition _seen;

    public SpawnChipsWindow(MainWindow main, SpawnsViewModel vm, Action<double>? setChipScale = null)
    {
        _main = main;
        _vm = vm;
        _settings = main.Settings;
        Title = "EQBuddy Spawn Timers";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        Content = ChipScale.Host(_panel);
        ChipScale.Apply(this, _settings.ChipScale);
        // Ctrl+wheel drives the shared chip-scale setter (which clamps, applies to
        // every open family window, and persists on its own) — WPF's WindowZoom.Route.
        if (setChipScale is not null)
            WindowZoom.Route(this, () => _settings.ChipScale, setChipScale);
        Opened += (_, _) =>
        {
            _restoredSaved = ScreenGuard.OnScreen(this, _settings.SpawnChipsLeft,
                _settings.SpawnChipsTop, Width, Height);
            if (_restoredSaved)
                Position = new PixelPoint((int)_settings.SpawnChipsLeft, (int)_settings.SpawnChipsTop);
            else if (Screens.Primary is { } primary)
                Position = new PixelPoint(primary.WorkingArea.X + 40, primary.WorkingArea.Y + 40);
            // Attach AFTER placement — a grow-up stack restores its BOTTOM edge, and
            // the saved Top belongs to whatever chip count the stack had at close
            // (#122, Snagglefern).
            _anchor = ChipAnchor.Attach(this, () => _settings.SpawnChipsGrowUp,
                _restoredSaved && _settings.SpawnChipsGrowUp && !double.IsNaN(_settings.SpawnChipsBottom)
                    ? _settings.SpawnChipsBottom : null);
            _openedOnce = true;
            // The spot we ASKED for, not a read-back: the window manager applies a
            // programmatic move asynchronously, so reading Position here can hand back
            // 0,0 on X11/Wayland and that zero becomes the stack's saved home (#169).
            if (_restoredSaved)
                _seen.Observe(_settings.SpawnChipsLeft, _settings.SpawnChipsTop, visible: true);
        };
        // Position can be reset by the native backend while a window is closing. Capture
        // moves only while it is visibly on screen, then persist that stable snapshot.
        PositionChanged += (_, _) =>
        {
            // Programmatic placement is not a choice — only persist once the player
            // has actually STARTED A DRAG (#117; coordinate deltas can't tell a drag
            // from the WM's or anchor's own writes — 2026-08-13 review).
            if (!IsVisible || !_openedOnce || !_userMoved) return;
            _seen.Observe(Position.X, Position.Y, visible: true);
            // Keep the live settings object current too, so a newly-created stack in the
            // same session restores correctly even before Closed has flushed the file.
            _settings.SpawnChipsLeft = Position.X;
            _settings.SpawnChipsTop = Position.Y;
        };
        Closed += (_, _) =>
        {
            var (curX, curY) = _seen.Or(_settings.SpawnChipsLeft, _settings.SpawnChipsTop);
            (_settings.SpawnChipsLeft, _settings.SpawnChipsTop) = WindowPlacement.PositionToPersist(
                _restoredSaved, _userMoved, curX, curY,
                _settings.SpawnChipsLeft, _settings.SpawnChipsTop);
            // The anchor's own bottom, never a measurement taken here: a closing window
            // reports a zero height, and "bottom" then means the top edge (#152).
            if (_anchor is { HasAnchor: true } anchor) _settings.SpawnChipsBottom = anchor.Bottom;
            _settings.Save();
        };
    }

    internal void RefreshChips(DateTime now)
    {
        _chips = _vm.Chips(now);
        var signature = string.Join("\u0001", _chips.Select(chip =>
            $"{chip.Zone}|{chip.Name}|{chip.IsDue}|{chip.Icon}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
            return;
        }

        for (var i = 0; i < _chips.Count && i < _countdowns.Count; i++)
        {
            _countdowns[i].Text = _chips[i].IsDue ? "DUE" : _chips[i].CountdownText;
            // DUE fills solid at rebuild (IsDue is in the signature); running chips
            // advance their elapsed-share fill every tick.
            if (i < _gauges.Count && _gauges[i].Fill is { } fill && !_chips[i].IsDue
                && _chips[i].Fraction is { } frac)
                fill.Width = Math.Max(0, _gauges[i].Track.Bounds.Width * frac);
        }
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        _countdowns.Clear();
        _gauges.Clear();
        foreach (var chip in _chips)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var name = new TextBlock
            {
                Text = $"{chip.Icon} {chip.Name}",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.TextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Children.Add(name);
            var countdown = new TextBlock
            {
                Text = chip.IsDue ? "DUE" : chip.CountdownText,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = chip.IsDue ? AppTheme.WarnBrush : AppTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(countdown, 1);
            row.Children.Add(countdown);
            _countdowns.Add(countdown);

            // The countdown made visual (2026-08-11): a progress track along the chip's
            // bottom edge — elapsed share fills in accent, DUE fills solid in the warn
            // red. A stack of chips reads as a stack of gauges.
            var host = new StackPanel();
            host.Children.Add(row);
            if (chip.Fraction is not null || chip.IsDue)
            {
                var track = new Grid { Height = 2.5, Margin = new Thickness(0, 3, 0, 0) };
                track.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(1.25),
                    Background = TrackBrush(),
                });
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(1.25),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 0,
                    Background = chip.IsDue ? AppTheme.BadBrush : AppTheme.AccentBrush,
                };
                track.Children.Add(fill);
                var frac = chip.IsDue ? 1.0 : chip.Fraction!.Value;
                track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * frac);
                host.Children.Add(track);
                _gauges.Add((track, fill));
            }
            else _gauges.Add(default);
            var border = new Border
            {
                Child = host,
                Tag = chip,
                Background = AppTheme.BgBrush,
                BorderBrush = chip.IsDue ? AppTheme.WarnBrush : AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 4),
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(border, chip.Detail + "\nRight-click: dismiss this timer");
            border.PointerPressed += OnChipPressed;
            _panel.Children.Add(border);
        }
    }

    private void OnChipPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: SpawnChip chip }) return;
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            DismissChip(chip);
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 2)
        {
            _main.ShowSpawnsWindow(chip.Zone);
            e.Handled = true;
            return;
        }
        if (chip.IsDue)
        {
            _vm.ClearTimer(chip.Zone, chip.Name);
            _signature = "\uFFFF";
            RefreshChips(DateTime.Now);
            e.Handled = true;
            return;
        }
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _userMoved = true;   // a real drag — the one signal that persists
            BeginMoveDrag(e);
        }
    }

    private static IBrush TrackBrush() => AppTheme.TrackBrush;

    internal void DismissChip(SpawnChip chip)
    {
        if (chip.Zone.Length == 0) return;
        _vm.ClearTimer(chip.Zone, chip.Name);
        // A sentinel is required when dismissing the last chip: its new signature is
        // the empty string, so resetting to "" would incorrectly skip the rebuild.
        _signature = "\uFFFF";
        RefreshChips(DateTime.Now);
    }
}
