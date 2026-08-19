using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using SpawnChip = EQBuddy.UI.Shared.SpawnChip;

namespace EQBuddy;

/// <summary>
/// The mez-target stack: one chip per believed-active mez, counting down to wake-up.
/// Same chicklet language as the spawn stack but a separate window with its own saved
/// position — mez chips get parked next to the fight, spawn chips are ambient. Chips
/// only drag; there is nothing to click through to and nothing to clear (breaks clear
/// themselves via the log).
/// </summary>
public partial class MezChipsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _userMoved;
    private string _signature = "";
    private readonly List<TextBlock> _countdowns = [];
    private List<SpawnChip> _chips = [];
    private readonly Func<DateTime, List<SpawnChip>> _source;

    public MezChipsWindow(AppSettings settings, Func<DateTime, List<SpawnChip>> source,
        Action<double>? setChipScale = null)
    {
        InitializeComponent();
        _settings = settings;
        _source = source;
        ChipScale.Apply(this, _settings.ChipScale);
        if (setChipScale is not null)
            WindowZoom.Route(this, () => _settings.ChipScale, setChipScale);
        var restored = ScreenGuard.OnScreen(_settings.MezChipsLeft, _settings.MezChipsTop, Width, Height);
        if (restored) { Left = _settings.MezChipsLeft; Top = _settings.MezChipsTop; }
        else { Left = SystemParameters.WorkArea.Left + 40; Top = SystemParameters.WorkArea.Top + 120; }
        // Grow-up stacks restore their BOTTOM edge — the saved Top belongs to
        // whatever chip count the stack had at close, and restoring it makes the
        // stack walk upward across reopen cycles (#122, Snagglefern).
        var anchor = ChipAnchor.Attach(this, () => _settings.MezChipsGrowUp,
            restored && _settings.MezChipsGrowUp && !double.IsNaN(_settings.MezChipsBottom)
                ? _settings.MezChipsBottom : null);
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117). The
            // stack moves ITSELF (grow-up anchor), so "moved" is the drag flag, not
            // a coordinate delta (2026-08-13 review).
            (_settings.MezChipsLeft, _settings.MezChipsTop) = WindowPlacement.PositionToPersist(
                restored, _userMoved, Left, Top,
                _settings.MezChipsLeft, _settings.MezChipsTop);
            // The bottom edge persists whenever the position we just kept is the
            // live one; a rejected fallback keeps the old bottom too.
            // The anchor, never the window: its geometry is already gone by now.
            if (_settings.MezChipsTop == Top && anchor.HasAnchor)
                _settings.MezChipsBottom = anchor.Bottom;
            _settings.Save();
        };
    }

    /// <summary>Called from MainWindow's 1 s tick while the stack is visible.</summary>
    public void RefreshChips(DateTime now)
    {
        _chips = _source(now);
        var signature = string.Join("", _chips.Select(c => $"{c.Name}|{c.IsDue}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
        }
        else
        {
            for (var i = 0; i < _chips.Count && i < _countdowns.Count; i++)
            {
                _countdowns[i].Text = _chips[i].CountdownText;
                // The draining gauge ticks with the countdown, no rebuild needed.
                if (i < _gauges.Count && _gauges[i].Fill is { } fillB && _chips[i].Fraction is { } frac)
                    fillB.Width = Math.Max(0, _gauges[i].Track.ActualWidth * (1 - frac));
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

            // The mez moon and the slow hourglass, as vectors in their own column. This
            // stack shares its chip language with SpawnChipsWindow and the two must draw
            // the mark the same way — a near-copy here is how #122 and #152 happened.
            var kind = DesignSystem.Icon(chip.Icon, "TextBrush",
                size: EQBuddy.UI.Shared.DesignTokens.IconInline);
            kind.Margin = new Thickness(0, 0, EQBuddy.UI.Shared.DesignTokens.SpaceXs, 0);
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
                Text = chip.CountdownText,
                FontSize = 11, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // Warning tint inside the last tick — the wake-up is the urgent moment.
            countdown.SetResourceReference(TextBlock.ForegroundProperty, chip.IsDue ? "WarnBrush" : "AccentBrush");
            Grid.SetColumn(countdown, 2);
            row.Children.Add(countdown);
            _countdowns.Add(countdown);

            // The mez gauge DRAINS (2026-08-11): remaining share, shrinking — a buff
            // bar for the sleep. Same track idiom as the spawn chips' filling gauge.
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition());
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.Children.Add(row);
            if (chip.Fraction is { } frac0)
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
                fill.SetResourceReference(Border.BackgroundProperty, chip.IsDue ? "WarnBrush" : "AccentBrush");
                track.Children.Add(fill);
                track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * (1 - frac0));
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
                Child = host, ToolTip = chip.Detail,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 4),
                Margin = new Thickness(0, 0, 0, 3),
                BorderThickness = new Thickness(1),
            };
            border.SetResourceReference(Border.BackgroundProperty, "BgBrush");
            border.SetResourceReference(Border.BorderBrushProperty, chip.IsDue ? "WarnBrush" : "BorderBrush");
            border.MouseLeftButtonDown += (_, _) => { _userMoved = true; DragMove(); };
            // Right-click dismisses dismissible chips (David, 2026-08-13: a slow
            // chip that isn't relevant must not squat for two minutes).
            if (chip.OnDismiss is { } dismiss)
                border.MouseRightButtonUp += (_, e) => { e.Handled = true; dismiss(); };
            ChipsPanel.Children.Add(border);
        }
    }
}
