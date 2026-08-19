using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// One compact chip per believed-active mez. This is deliberately a separate movable
/// stack from spawn timers: mez wake-ups are combat-urgent and are normally parked near
/// the fight, while spawn timers are ambient camp information.
/// </summary>
public sealed class MezChipsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<DateTime, List<SpawnChip>> _clockSource;
    private readonly StackPanel _panel = new();
    private readonly List<TextBlock> _countdowns = [];
    private readonly List<(Grid Track, Border Fill)> _gauges = [];
    private List<SpawnChip> _chips = [];
    private string _signature = "";
    /// <summary>The last position we can vouch for — the spot asked for at open, or a
    /// drag. Never a read taken while the window is opening or closing (#169).</summary>
    private LastVisiblePosition _seen;
    // Fallback placements must never persist (#117): where the window was placed at
    // open and whether that was the player's saved spot.
    private bool _restoredSaved;
    private bool _openedOnce;
    private bool _userMoved;
    private ChipStackAnchor? _anchor;
    /// <summary>Tests can't drag a headless window; this is the drag signal's test seam.</summary>
    internal void MarkUserMovedForTests() => _userMoved = true;

    /// <summary>WPF's current shape: one clock-driven source for everything the
    /// fight-side stack shows — mez chips, slow chips, the Options placement preview —
    /// built by MainWindow (its FightChips), sharing this window and saved position.</summary>
    public MezChipsWindow(AppSettings settings, Func<DateTime, List<SpawnChip>> source,
        Action<double>? setChipScale = null)
    {
        _settings = settings;
        _clockSource = source;
        Title = "EQBuddy Mez Targets";
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
            _restoredSaved = ScreenGuard.OnScreen(this, _settings.MezChipsLeft,
                _settings.MezChipsTop, Width, Height);
            if (_restoredSaved)
                Position = new PixelPoint((int)_settings.MezChipsLeft, (int)_settings.MezChipsTop);
            else if (Screens.Primary is { } primary)
                Position = new PixelPoint(primary.WorkingArea.X + 40, primary.WorkingArea.Y + 120);
            // Attach AFTER placement — a grow-up stack restores its BOTTOM edge, and
            // the saved Top belongs to whatever chip count the stack had at close
            // (#122, Snagglefern).
            _anchor = ChipAnchor.Attach(this, () => _settings.MezChipsGrowUp,
                _restoredSaved && _settings.MezChipsGrowUp && !double.IsNaN(_settings.MezChipsBottom)
                    ? _settings.MezChipsBottom : null);
            _openedOnce = true;
            // The spot we ASKED for, not a read-back: the window manager applies a
            // programmatic move asynchronously, so reading Position here can hand back
            // 0,0 on X11/Wayland and that zero becomes the stack's saved home (#169).
            if (_restoredSaved)
                _seen.Observe(_settings.MezChipsLeft, _settings.MezChipsTop, visible: true);
        };
        PositionChanged += (_, _) =>
        {
            // Programmatic placement is not a choice — only persist once the player
            // has actually STARTED A DRAG (#117; coordinate deltas can't tell a drag
            // from the WM's or anchor's own writes — 2026-08-13 review).
            if (!IsVisible || !_openedOnce || !_userMoved) return;
            _seen.Observe(Position.X, Position.Y, visible: true);
            _settings.MezChipsLeft = Position.X;
            _settings.MezChipsTop = Position.Y;
        };
        Closed += (_, _) =>
        {
            var (curX, curY) = _seen.Or(_settings.MezChipsLeft, _settings.MezChipsTop);
            (_settings.MezChipsLeft, _settings.MezChipsTop) = WindowPlacement.PositionToPersist(
                _restoredSaved, _userMoved, curX, curY,
                _settings.MezChipsLeft, _settings.MezChipsTop);
            // The anchor's own bottom, never a measurement taken here: a closing window
            // reports a zero height, and "bottom" then means the top edge (#152).
            if (_anchor is { HasAnchor: true } anchor) _settings.MezChipsBottom = anchor.Bottom;
            _settings.Save();
        };
    }

    /// <summary>Same-named targets remain separate and are numbered in snapshot order.
    /// The log cannot identify which physical creature is which, but collapsing them
    /// would hide an active mez and make one break appear to clear both.</summary>
    internal static List<SpawnChip> BuildChips(IReadOnlyList<MezState> mezzes, DateTime now)
    {
        var totals = mezzes.GroupBy(mez => mez.Target, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return mezzes.Select(mez =>
        {
            var number = seen[mez.Target] = seen.GetValueOrDefault(mez.Target) + 1;
            var name = totals[mez.Target] > 1 ? $"{mez.Target} ({number})" : mez.Target;
            var remaining = mez.RemainingSeconds(now);
            var countdown = remaining is { } seconds
                ? $"{(int)seconds / 60}:{(int)seconds % 60:00}"
                : "?";
            return new SpawnChip("", name, countdown, remaining is <= 6,
                $"{mez.Spell} by {mez.Caster} · landed {mez.LandedAt:h:mm:ss tt}", "Moon")
            {
                // Elapsed share for the gauge; the mez view draws the REMAINING side
                // (a draining bar, like a buff), so 1 - this.
                Fraction = mez.ExpiresAt is { } exp && (exp - mez.LandedAt).TotalSeconds is > 0 and var dur
                    ? Math.Clamp((now - mez.LandedAt).TotalSeconds / dur, 0, 1)
                    : null,
            };
        }).ToList();
    }

    /// <summary>The main window's tick entry point: mez chips, slow chips, and the
    /// Options placement preview arrive already built (MainWindow.FightChips).</summary>
    internal void RefreshChips(DateTime now) => ApplyChips(_clockSource(now));

    private void ApplyChips(List<SpawnChip> chips)
    {
        _chips = chips;
        var signature = string.Join("\u0001", _chips.Select(chip =>
            $"{chip.Name}|{chip.IsDue}|{chip.Icon}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
            return;
        }

        for (var i = 0; i < _chips.Count && i < _countdowns.Count; i++)
        {
            _countdowns[i].Text = _chips[i].CountdownText;
            // The draining gauge ticks with the countdown, no rebuild needed.
            if (i < _gauges.Count && _gauges[i].Fill is { } fill && _chips[i].Fraction is { } frac)
                fill.Width = Math.Max(0, _gauges[i].Track.Bounds.Width * (1 - frac));
        }
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        _countdowns.Clear();
        _gauges.Clear();
        foreach (var chip in _chips)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            // The mez moon / slow hourglass as a vector, in its own column — the same
            // shape SpawnChipsWindow draws, from the same table.
            var kind = DesignSystem.Icon(chip.Icon, "TextBrush", size: DesignTokens.IconInline);
            kind.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
            row.Children.Add(kind);
            var name = new TextBlock
            {
                Text = chip.Name,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.TextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var countdown = new TextBlock
            {
                Text = chip.CountdownText,
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = chip.IsDue ? AppTheme.WarnBrush : AppTheme.AccentBrush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(countdown, 2);
            row.Children.Add(countdown);
            _countdowns.Add(countdown);

            // The mez gauge DRAINS (2026-08-11): remaining share, shrinking — a buff
            // bar for the sleep. Same track idiom as the spawn chips' filling gauge.
            var host = new StackPanel();
            host.Children.Add(row);
            if (chip.Fraction is { } frac0)
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
                    Background = chip.IsDue ? AppTheme.WarnBrush : AppTheme.AccentBrush,
                };
                track.Children.Add(fill);
                track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * (1 - frac0));
                host.Children.Add(track);
                _gauges.Add((track, fill));
            }
            else
            {
                _gauges.Add(default);
            }
            var border = new Border
            {
                Child = host,
                Background = AppTheme.BgBrush,
                BorderBrush = chip.IsDue ? AppTheme.WarnBrush : AppTheme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 4),
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = new Cursor(StandardCursorType.SizeAll),
            };
            ToolTip.SetTip(border, chip.Detail);
            border.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    _userMoved = true;   // a real drag — the one signal that persists
                    BeginMoveDrag(e);
                }
            };
            _panel.Children.Add(border);
        }
    }

    private static IBrush TrackBrush() => AppTheme.TrackBrush;
}
