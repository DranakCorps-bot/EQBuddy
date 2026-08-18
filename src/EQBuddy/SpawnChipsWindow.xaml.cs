using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The spawn-countdown chicklet stack (SPAWN-005, David's design): while timers run,
/// each shows as a small movable chip — a stopwatch, "Asaka L`Rei", "3:12" — and the zone list
/// only exists behind a double-click. At zero a chip flips to a warn-colored DUE for
/// one minute, then clears itself — click it away sooner if you're watching. The stack shows every
/// running timer on the server regardless of zone: a Befallen camp timer keeps counting
/// while you bank in West Commonlands, because a timer you set is a timer you care
/// about. MainWindow's shared tick drives refresh and decides visibility — the stack
/// exists exactly while timers do, staying up alongside the full zone window too
/// (dropping it while the browser was open lost the countdowns you cared about).
/// </summary>
public partial class SpawnChipsWindow : Window
{
    private readonly MainWindow _main;
    private readonly SpawnsViewModel _vm;
    private readonly AppSettings _settings;
    private bool _userMoved;
    private string _signature = "";
    private readonly List<TextBlock> _countdowns = [];
    private List<SpawnChip> _chips = [];

    public SpawnChipsWindow(MainWindow main, SpawnsViewModel vm)
    {
        InitializeComponent();
        NoActivate.Attach(this);
        _main = main;
        _vm = vm;
        _settings = main.Settings;
        ChipScale.Apply(this, _settings.ChipScale);
        WindowZoom.Route(this, () => _settings.ChipScale, main.SetChipScale);
        var restored = ScreenGuard.OnScreen(_settings.SpawnChipsLeft, _settings.SpawnChipsTop, Width, Height);
        if (restored) { Left = _settings.SpawnChipsLeft; Top = _settings.SpawnChipsTop; }
        else { Left = SystemParameters.WorkArea.Left + 40; Top = SystemParameters.WorkArea.Top + 40; }
        // Grow-up stacks restore their BOTTOM edge (#122) — see MezChipsWindow.
        var anchor = ChipAnchor.Attach(this, () => _settings.SpawnChipsGrowUp,
            restored && _settings.SpawnChipsGrowUp && !double.IsNaN(_settings.SpawnChipsBottom)
                ? _settings.SpawnChipsBottom : null);
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117). The
            // stack moves ITSELF (grow-up anchor), so "moved" is the drag flag, not
            // a coordinate delta (2026-08-13 review).
            (_settings.SpawnChipsLeft, _settings.SpawnChipsTop) = WindowPlacement.PositionToPersist(
                restored, _userMoved, Left, Top,
                _settings.SpawnChipsLeft, _settings.SpawnChipsTop);
            // The anchor, never the window: its geometry is already gone by now.
            if (_settings.SpawnChipsTop == Top && anchor.HasAnchor)
                _settings.SpawnChipsBottom = anchor.Bottom;
            _settings.Save();
        };
    }

    /// <summary>Called from MainWindow's 1 s tick while the stack is visible.</summary>
    public void RefreshChips(DateTime now)
    {
        _chips = _vm.Chips(now);
        var signature = string.Join("", _chips.Select(c => $"{c.Zone}|{c.Name}|{c.IsDue}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
        }
        else
        {
            for (var i = 0; i < _chips.Count && i < _countdowns.Count; i++)
            {
                _countdowns[i].Text = _chips[i].IsDue ? "DUE" : _chips[i].CountdownText;
                // The filling gauge ticks with the countdown, same as MezChipsWindow's
                // draining one — the fraction froze at whatever the last REBUILD saw
                // (audit finding 14: signature-stable ticks skipped it entirely).
                if (i < _gauges.Count && _gauges[i].Fill is { } fillB && _chips[i].Fraction is { } frac)
                    fillB.Width = Math.Max(0, _gauges[i].Track.ActualWidth * frac);
            }
        }
    }

    private readonly List<(Grid Track, Border Fill)> _gauges = [];

    private void Rebuild()
    {
        ChipsPanel.Children.Clear();
        _countdowns.Clear();
        _gauges.Clear();
        foreach (var chip in _chips)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // chip.Icon is an IconPaths NAME now, not a glyph pasted onto the front of
            // the name. Its own column, so a long chip name still trims against the star
            // column instead of pushing the mark off (trap 14).
            var kind = DesignSystem.Icon(chip.Icon, "TextBrush", size: DesignTokens.IconInline);
            kind.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
            row.Children.Add(kind);

            var name = new TextBlock
            {
                Text = chip.Name, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 180,
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var countdown = new TextBlock
            {
                Text = chip.IsDue ? "DUE" : chip.CountdownText,
                FontSize = 11, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            countdown.SetResourceReference(TextBlock.ForegroundProperty, chip.IsDue ? "WarnBrush" : "AccentBrush");
            Grid.SetColumn(countdown, 2);
            row.Children.Add(countdown);
            _countdowns.Add(countdown);

            // The countdown made visual (2026-08-11): a progress track along the chip's
            // bottom edge — elapsed share fills in accent, DUE fills solid in the warn
            // red. A stack of chips reads as a stack of gauges.
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition());
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.Children.Add(row);
            if (chip.Fraction is not null || chip.IsDue)
            {
                var track = new Grid { Height = 2.5, Margin = new Thickness(0, 3, 0, 0) };
                var trackBg = new Border { CornerRadius = new CornerRadius(1.25) };
                trackBg.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
                track.Children.Add(trackBg);
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(1.25),
                    HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
                };
                fill.SetResourceReference(Border.BackgroundProperty, chip.IsDue ? "BadBrush" : "AccentBrush");
                track.Children.Add(fill);
                var frac = chip.IsDue ? 1.0 : chip.Fraction!.Value;
                track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * frac);
                Grid.SetRow(track, 1);
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
                ToolTip = chip.Detail + "\nRight-click: dismiss this timer",
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 4),
                Margin = new Thickness(0, 0, 0, 3),
                BorderThickness = new Thickness(1),
                Tag = chip,
            };
            border.SetResourceReference(Border.BackgroundProperty, "BgBrush");
            border.SetResourceReference(Border.BorderBrushProperty, chip.IsDue ? "WarnBrush" : "BorderBrush");
            border.MouseLeftButtonDown += OnChipMouseDown;
            border.MouseRightButtonUp += OnChipDismiss;
            ChipsPanel.Children.Add(border);
        }
    }

    /// <summary>Right-click dismisses a chip whether DUE or still counting — a camp
    /// abandoned mid-countdown shouldn't haunt the stack until it expires (Reddit,
    /// anyhow188: only due chips could be cleared).</summary>
    private void OnChipDismiss(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: SpawnChip chip } || chip.Zone.Length == 0) return;
        _vm.ClearTimer(chip.Zone, chip.Name);
        // Sentinel, not "": dismissing the LAST chip makes the new signature the empty
        // string too, and a matching reset would skip the rebuild and leave a ghost
        // chip painted (Don's catch porting this window to Avalonia, PR #67).
        _signature = "￿";
        RefreshChips(DateTime.Now);
        e.Handled = true;
    }

    private void OnChipMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: SpawnChip chip }) return;
        if (e.ClickCount == 2)
        {
            // The full zone list, opened on the chip's zone. MainWindow's tick hides
            // the stack while the full window is up.
            _main.ShowSpawnsWindow(chip.Zone);
            e.Handled = true;
            return;
        }
        if (chip.IsDue)
        {
            // A due chip has said its piece — click acknowledges and clears the timer.
            _vm.ClearTimer(chip.Zone, chip.Name);
            _signature = "￿";   // sentinel, not "" — see OnChipDismiss
            RefreshChips(DateTime.Now);
            e.Handled = true;
            return;
        }
        _userMoved = true;
        DragMove();
    }
}
