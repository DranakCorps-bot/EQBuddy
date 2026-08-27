using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy;

/// <summary>
/// Named-mob respawn countdowns for one zone at a time, fed by <see cref="SpawnsViewModel"/>.
///
/// Lifted out of <c>SpawnsWindow</c> for World PR 1 (docs/Themes.md theme 6): this carries
/// the whole bordered panel — chrome and content together, since the window is borderless
/// and hand-drawn and there is no clean chrome/content seam at the XAML level the way
/// Map/Travel had. <c>SpawnsWindow</c> becomes a thin host owning only what a literal OS
/// window owns: sizing, position, <c>DragMove</c>, <c>Close</c> — reached here via
/// <see cref="Window.GetWindow(DependencyObject)"/> rather than a captured reference.
///
/// Owns its own 1-second tick (unlike Map, which rides <c>MainWindow</c>'s shared
/// <c>RefreshUi</c> tick via <c>MaybeRefresh</c>) — that was already true of the window
/// this replaces, so the timer moved with the rest of the content unchanged.
/// </summary>
public partial class SpawnsView : UserControl
{
    private readonly IZoneHost _host;
    private readonly SpawnsViewModel _vm;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _tick;

    // Rebuilds are keyed on a signature of everything except the countdown text, so a
    // ticking clock updates labels in place and never yanks focus out of an edit box.
    private string _signature = "";
    private readonly List<TimerCell> _timerCells = [];
    private TextBlock _titleText = null!;
    private List<SpawnRow> _rows = [];
    private bool _syncingZone;

    /// <paramref name="initialZone"/>: the zone whose kill popped the window,
    /// so it opens showing the timer that summoned it.
    public SpawnsView(IZoneHost host, SpawnsViewModel vm, string? initialZone = null)
    {
        InitializeComponent();
        _host = host;
        _vm = vm;
        _settings = host.Settings;
        BuildStaticChrome();

        _vm.RefreshZoneList();
        foreach (var z in _vm.ZoneNames) ZoneCombo.Items.Add(z);
        FollowCheck.IsChecked = _settings.SpawnFollowZone;

        SelectZone(initialZone
            ?? (_settings.SpawnFollowZone ? _vm.CurrentZoneName : null)
            ?? FirstNonEmpty(_settings.SpawnZone, _vm.ZoneNames.FirstOrDefault() ?? ""));
        _lastFollowedZone = _vm.CurrentZoneName;

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => RefreshRows();
        _tick.Start();
        RefreshRows();
    }

    public UIElement Body => this;

    /// <summary>Exposed so the thin host's <c>UpdateHeightCaps</c> can cap the scroller —
    /// the one place the window's geometry and this view's content agree.</summary>
    internal ScrollViewer BodyScrollView => BodyScroll;

    /// <summary>Stop the independent tick — called from the host's <c>Closed</c> handler,
    /// exactly where <c>_tick.Stop()</c> ran before the lift.</summary>
    public void StopTicking() => _tick.Stop();

    /// <summary>Hide this view's own title row and close button (World PR 2) — leftover
    /// chrome from when this was a borderless standalone window. <c>WorldWindow</c>
    /// supplies both now, so drawing this view's copies too would put two title rows and
    /// two close buttons on screen at once. Collapsing the row rather than deleting the
    /// controls: the Auto row height follows, so nothing leaves a gap.</summary>
    internal void HideOwnTitleBar() => TitleBar.Visibility = Visibility.Collapsed;

    private static string FirstNonEmpty(string a, string b) => a.Length > 0 ? a : b;

    private string SelectedZone => ZoneCombo.SelectedItem as string ?? "";

    private void SelectZone(string zone)
    {
        _syncingZone = true;
        ZoneCombo.SelectedItem = _vm.ZoneNames.FirstOrDefault(z =>
            string.Equals(z, zone, StringComparison.OrdinalIgnoreCase)) ?? ZoneCombo.SelectedItem;
        _syncingZone = false;
    }

    /// <summary>The zone Follow last snapped to. Following reacts to zone CHANGES, not to
    /// every tick — so browsing another zone's list mid-camp survives until you actually
    /// zone, instead of the dropdown yanking itself back every second.</summary>
    private string? _lastFollowedZone;

    private void RefreshRows()
    {
        // Follow the player: the log's zone lines drive the dropdown while Follow is on.
        if (_settings.SpawnFollowZone && _vm.CurrentZoneName is { } here && here != _lastFollowedZone)
        {
            _lastFollowedZone = here;
            if (here != SelectedZone) SelectZone(here);
        }

        var zone = SelectedZone;
        _titleText.Text = zone.Length > 0 ? $"Spawns — {zone}" : "Spawns";
        if (zone.Length == 0) return;

        var now = DateTime.Now;
        _rows = _vm.RowsFor(zone, now);
        var signature = zone + "" + string.Join("",
            _rows.Select(r => $"{r.DisplayName}|{r.HasActiveTimer}|{r.IsDue}|{r.DurationText}|{r.Alert}|{r.SoundName}|{r.IsCustom}"));
        if (signature != _signature)
        {
            // Never rebuild under someone's cursor — committing the edit refreshes anyway.
            if (RowsPanel.IsKeyboardFocusWithin) return;
            _signature = signature;
            Rebuild();
        }
        else
        {
            // Same rows, one second later: repaint the clocks in place rather than
            // rebuilding. The BAR has to move with the text or the two disagree for as
            // long as nothing else changes — which, on a 3-day raid timer, is days.
            for (var i = 0; i < _rows.Count && i < _timerCells.Count; i++)
                _timerCells[i].Update(_rows[i], now);
        }
    }

    /// <summary>The chrome that never changes: the title icon, the close icon, and the
    /// help callout. Built in code because each is a vector Path whose geometry comes
    /// from a shared table, which XAML cannot express.</summary>
    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Timer", "AccentBrush", size: 15));
        _titleText = DesignSystem.Text(Role.TitleWindow, "Spawns");
        _titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        _titleText.Ink("AccentBrush");
        TitleRow.Children.Add(_titleText);
        CloseBtn.Content = DesignSystem.Icon("Close");

        // The accuracy contract for this window, and the reason it is a callout rather
        // than a 65%-opacity paragraph: "our numbers came from community sources and
        // YOURS WIN" is the sentence that makes a wrong respawn time survivable, and
        // nobody was reading it.
        var callout = new Grid();
        callout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        callout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = DesignSystem.Icon("Info", "DimBrush", size: 13);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 1, DesignTokens.SpaceS, 0);
        callout.Children.Add(icon);
        var text = DesignSystem.Text(Role.Caption,
            "Kill a named (or its placeholder) and its countdown starts from the log. "
            + "Start one by hand with the play button — type how long ago it died first (5m, 90s) or "
            + "leave it empty for now. Respawn times came from community sources: if what "
            + "you see in game disagrees, type over the duration. Your number wins, and it "
            + "survives updates.");
        text.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(text, 1);
        callout.Children.Add(text);
        HelpCallout.Child = callout;
    }

    /// <summary>The row's column widths, in one place because the HEADER has to agree
    /// with them exactly — a header that drifts from its columns is worse than none.</summary>
    /// <summary>Room for start · bell · sound · clear · delete, always reserved.</summary>
    private const double ActionLaneWidth = 132;

    private static void DefineColumns(Grid grid)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });  // timer + bar
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });   // respawn
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });   // died
        // FIXED, not Auto, for two reasons. The header row has no buttons in it, so an
        // Auto column is zero-wide there and every label lands 115px left of the column
        // it names — a header worse than no header, which is what the first Gate 3
        // capture showed. And a row grows a Clear button the moment its timer starts, so
        // an Auto lane would reflow the inputs under the player's cursor mid-edit.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ActionLaneWidth) });
    }

    /// <summary>Column headers (the mockup's, adopted wholesale): five unlabelled columns
    /// of boxes and glyphs is a puzzle the first time and a memory test after that.</summary>
    private void BuildHeader()
    {
        HeaderRow.Children.Clear();
        HeaderRow.ColumnDefinitions.Clear();
        DefineColumns(HeaderRow);
        // A row is a CARD, so its content sits one card-padding further in than the raw
        // grid does. Without matching that, every label is offset from the column it
        // names — which is worse than no header, and is exactly what the first Gate 3
        // capture showed.
        HeaderRow.Margin = new Thickness(DesignTokens.SpaceL + DesignTokens.SpaceM,
            DesignTokens.SpaceM, DesignTokens.SpaceL + DesignTokens.SpaceM, 0);
        var labels = new[] { "Named", "Next spawn", "Respawn", "Died", "" };
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i].Length == 0) continue;
            var text = DesignSystem.Text(Role.Metadata, labels[i]);
            text.Margin = new Thickness(0, 0, DesignTokens.SpaceS, DesignTokens.SpaceXs);
            Grid.SetColumn(text, i);
            HeaderRow.Children.Add(text);
        }
    }

    /// <summary>
    /// One row's countdown AND its progress toward respawn, as one thing — because they
    /// are one fact and the audit's finding was that showing only the text made "due in
    /// 4:21" and "due in 18:31" look equally urgent.
    ///
    /// The bar is a two-column Grid whose star weights are the fraction, not a Width
    /// computed from ActualWidth: a measured width would be wrong on the first layout
    /// pass, wrong again after a Ctrl+wheel zoom, and wrong in a different way under the
    /// UI-scale transform (trap 1). Star weights are resolved by the layout system in
    /// whatever units it is actually working in.
    /// </summary>
    private sealed class TimerCell
    {
        public StackPanel Root { get; }
        private readonly TextBlock _text;
        private readonly TextBlock _percent;
        private readonly Border _track;
        private readonly Border _fill;
        private readonly ColumnDefinition _filled;
        private readonly ColumnDefinition _rest;

        public TimerCell()
        {
            _text = DesignSystem.Text(Role.Body);
            _text.FontWeight = FontWeights.SemiBold;
            _percent = DesignSystem.Text(Role.Metadata);
            _percent.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
            _percent.VerticalAlignment = VerticalAlignment.Bottom;

            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(_text);
            line.Children.Add(_percent);

            _filled = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
            _rest = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
            var bar = new Grid { Height = DesignTokens.StateRuleWidth };
            bar.ColumnDefinitions.Add(_filled);
            bar.ColumnDefinitions.Add(_rest);
            _fill = new Border { CornerRadius = new CornerRadius(DesignTokens.StateRuleWidth / 2) };
            bar.Children.Add(_fill);
            _track = new Border
            {
                Child = bar,
                CornerRadius = new CornerRadius(DesignTokens.StateRuleWidth / 2),
                Margin = new Thickness(0, DesignTokens.SpaceXxs, DesignTokens.SpaceS, 0),
            };

            // The bar is NOT parented here. It spans the whole row underneath every
            // column (David, 2026-08-17: "we have room between the columns") — penned
            // inside the timer column it was a 150px sliver, and a progress bar's whole
            // job is being readable without being looked at. The row places it; this
            // still owns it, because the text and the bar are one fact and must not be
            // updated from two places.
            Root = line;
            _track.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        }

        /// <summary>The progress bar, for the row to span across its full width.</summary>
        public Border Track => _track;

        public void Update(SpawnRow row, DateTime now)
        {
            var view = TimerView.For(row.DueAt, row.DurationSeconds, now,
                row.HasActiveTimer, row.Suppression);
            _text.Text = TimerView.Text(view, row.DueAt, now, row.SuppressionNote);
            _text.Ink(view.TextColorKey);

            // The percentage is the mockup's, and it earns its place on a long cycle:
            // "2d 3h" says nothing about whether that is nearly over.
            _percent.Text = view.Fraction is { } f && view.State is not TimerView.State.Due
                ? $"{f * 100:0}%" : "";

            _track.Visibility = view.HasTrack ? Visibility.Visible : Visibility.Collapsed;
            if (!view.HasTrack) return;
            _track.SetResourceReference(Border.BackgroundProperty, view.TrackColorKey);
            // Null fraction keeps the track and draws no fill: the row still has a slot
            // for progress, it just has no claim to make.
            var frac = view.Fraction ?? 0;
            _filled.Width = new GridLength(frac, GridUnitType.Star);
            _rest.Width = new GridLength(1 - frac, GridUnitType.Star);
            if (view.FillColorKey is { } key) _fill.SetResourceReference(Border.BackgroundProperty, key);
            _fill.Visibility = view.Fraction is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void Rebuild()
    {
        RowsPanel.Children.Clear();
        _timerCells.Clear();
        BuildHeader();

        if (_rows.Count == 0)
        {
            var empty = DesignSystem.Text(Role.Body,
                "No named catalogued for this zone yet — add one below.");
            empty.TextWrapping = TextWrapping.Wrap;
            empty.Margin = new Thickness(0, DesignTokens.SpaceM, 0, DesignTokens.SpaceM);
            RowsPanel.Children.Add(empty);
            return;
        }

        var now = DateTime.Now;
        foreach (var row in _rows)
        {
            var grid = new Grid();
            DefineColumns(grid);

            var name = DesignSystem.Text(Role.Body, row.DisplayName);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(0, 0, DesignTokens.SpaceS, 0);
            if (row.Detail.Length > 0) name.ToolTip = row.Detail;
            grid.Children.Add(name);

            var cell = new TimerCell();
            cell.Update(row, now);
            _timerCells.Add(cell);
            Grid.SetColumn(cell.Root, 1);
            grid.Children.Add(cell.Root);

            // FREE TEXT, deliberately (docs/DesignSystem.md §8c): SpawnDurationText parses
            // 5m, 90s, 22m and "3d 12h", and the numeric spinner the mockup drew would
            // regress week-long raid targets. The placeholder guidance IS adopted.
            var duration = DarkBox(row.DurationText,
                "Respawn time: 22 (minutes), 90s, 12h, 3d, 3d 12h, 6:40 — your edit persists and outranks the catalog");
            duration.Tag = row.Name;
            duration.LostFocus += (_, _) => CommitDuration(duration, row);
            duration.KeyDown += (_, e) => { if (e.Key == Key.Enter) CommitDuration(duration, row); };
            Grid.SetColumn(duration, 2);
            grid.Children.Add(duration);

            var ago = DarkBox("", "Died how long ago? (5m, 90s) Empty = just now");
            ago.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
            Grid.SetColumn(ago, 3);
            grid.Children.Add(ago);

            // GROUPED actions (the mockup's, and the audit's complaint that three
            // controls sat at identical size with no hierarchy). Start is the one thing
            // you do TO a camp; the rest configure or undo it, and sit behind a divider.
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
            };
            buttons.Children.Add(DesignSystem.IconButton("Play",
                "Start the countdown from a kill you saw yourself",
                (_, _) => { _vm.StartNow(row.Zone, row.Name, ago.Text); Kick(); }, "AccentBrush"));
            buttons.Children.Add(new Border
            {
                Width = 1,
                Margin = new Thickness(DesignTokens.SpaceXs, DesignTokens.SpaceXs,
                    DesignTokens.SpaceXs, DesignTokens.SpaceXs),
                Background = (System.Windows.Media.Brush)FindResource("HairlineBrush"),
            });
            // A vector bell can be COLOURED, which an emoji one could not — the old
            // toggle had to signal "on" with opacity because the glyph ignored
            // Foreground entirely.
            buttons.Children.Add(DesignSystem.IconButton(row.Alert ? "Bell" : "BellOff",
                "Sound when this one comes due — off by default, like watch-rule sounds (the chip shows DUE either way)",
                (_, _) => { _vm.ToggleAlert(row.Zone, row.Name); Kick(); },
                row.Alert ? "AccentBrush" : "DimBrush", row.Alert ? 1.0 : 0.55));
            buttons.Children.Add(BuildSoundPicker(row));
            if (row.HasActiveTimer)
                buttons.Children.Add(DesignSystem.IconButton("Close", "Forget this countdown",
                    (_, _) => { _vm.ClearTimer(row.Zone, row.Name); Kick(); }, "DimBrush", 0.55));
            if (row.IsCustom)
                buttons.Children.Add(DesignSystem.IconButton("Trash",
                    "Remove this named (you added it)",
                    (_, _) => { _vm.RemoveCustom(row.Zone, row.Name); Kick(); }, "DimBrush", 0.55));
            Grid.SetColumn(buttons, 4);
            grid.Children.Add(buttons);

            // A row is a card now, so a due one can be picked out of forty by its edge.
            // The progress bar spans the whole card beneath the columns rather than
            // sitting in the timer column, which is where the room actually is.
            var body = new StackPanel();
            body.Children.Add(grid);
            body.Children.Add(cell.Track);
            var card = new Border
            {
                Child = body,
                CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
                Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs,
                    DesignTokens.SpaceM, DesignTokens.SpaceXs),
                Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
                BorderThickness = new Thickness(1),
            };
            card.SetResourceReference(Border.BackgroundProperty,
                row.IsDue ? "WarnWashBrush" : "PanelBrush");
            card.SetResourceReference(Border.BorderBrushProperty,
                row.IsDue ? "WarnBrush" : "HairlineBrush");
            RowsPanel.Children.Add(card);
        }
    }

    /// <summary>This named's own due sound — the watch-rule scheme: Default follows the
    /// shared choice at the bottom, Off silences just this one, Custom… takes a file.
    /// Different camps with different sounds is how the ear knows which one popped.</summary>
    private ComboBox BuildSoundPicker(SpawnRow row)
    {
        var combo = new ComboBox
        {
            FontSize = DesignTokens.Spec(Role.Metadata).Size, Width = 66,
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0),
            ToolTip = "Sound for this named — Default is Alarm",
        };
        foreach (var item in (string[])["Default", "Off", .. EQBuddy.UI.Shared.AlertSoundCatalog.Names, "Custom…"])
            combo.Items.Add(item);
        var isCustomFile = row.SoundName.Length > 0
            && !string.Equals(row.SoundName, "Off", StringComparison.OrdinalIgnoreCase)
            && !EQBuddy.UI.Shared.AlertSoundCatalog.Names.Contains(row.SoundName, StringComparer.OrdinalIgnoreCase);
        combo.SelectedItem = row.SoundName.Length == 0 ? "Default"
            : isCustomFile ? "Custom…"
            : combo.Items.Cast<string>().First(i => string.Equals(i, row.SoundName, StringComparison.OrdinalIgnoreCase));
        if (isCustomFile) combo.ToolTip = $"Custom: {row.SoundName}";

        var ready = false;
        combo.SelectionChanged += (_, _) =>
        {
            if (!ready || combo.SelectedItem is not string choice) return;
            switch (choice)
            {
                case "Default":
                    _vm.SetSound(row.Zone, row.Name, "");
                    break;
                case "Off":
                    _vm.SetSound(row.Zone, row.Name, "Off");
                    break;
                case "Custom…":
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = $"Choose a sound for \"{row.Name}\"",
                        Filter = EQBuddy.UI.Shared.AlertSoundFormats.WpfFilter,
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        _vm.SetSound(row.Zone, row.Name, dlg.FileName);
                        _host.PlayAlertSound(dlg.FileName);
                    }
                    break;
                default:
                    _vm.SetSound(row.Zone, row.Name, choice);
                    _host.PlayAlertSound(choice);   // hear it as you pick it
                    break;
            }
            Kick();
        };
        ready = true;
        return combo;
    }

    private void CommitDuration(TextBox box, SpawnRow row)
    {
        var before = row.DurationText;
        if (box.Text.Trim() == before) return;
        _vm.SetDuration(row.Zone, row.Name, box.Text);
        Kick();
    }

    /// <summary>Force the next tick to rebuild even while focus sits in the panel.</summary>
    private void Kick()
    {
        _signature = "";
        Keyboard.ClearFocus();
        RefreshRows();
    }

    private static TextBox DarkBox(string text, string tooltip)
    {
        var box = new TextBox
        {
            Text = text, FontSize = DesignTokens.Spec(Role.Caption).Size, ToolTip = tooltip,
            Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs, DesignTokens.SpaceS, DesignTokens.SpaceXxs),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        // SetResourceReference so an in-place theme switch repaints rebuilt rows too.
        box.SetResourceReference(Control.BackgroundProperty, "ComboBoxBrush");
        box.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        return box;
    }

    // ---- chrome ----
    //
    // DragMove/Close are Window-only, so they resolve the actual hosting window at the
    // point of use rather than capturing one in the constructor — this view is no longer
    // a Window itself. Works identically: both fire from an event that already arrived
    // on the same window's message pump.

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox)
            Window.GetWindow(this)?.DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private void OnZonePicked(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingZone) return;
        // A null/empty selection can arrive from WPF teardown paths, not from the user —
        // 1.20.0 let one of those persist state and silently killed zone-following.
        if (SelectedZone.Length == 0) return;
        _settings.SpawnZone = SelectedZone;
        _settings.Save();
        _signature = "";
        RefreshRows();
    }

    private void OnFollowChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingZone) return;
        _settings.SpawnFollowZone = FollowCheck.IsChecked == true;
        _settings.Save();
        RefreshRows();
    }

    private void OnAddCustom(object sender, RoutedEventArgs e)
    {
        if (_vm.AddCustom(SelectedZone, AddNameBox.Text, AddDurationBox.Text))
        {
            AddNameBox.Text = "";
            AddDurationBox.Text = "";
            Kick();
        }
    }

    /// <summary>Facts for the <c>EQBUDDY_EXPAND</c> dump — the WPF layer's only test seam
    /// (docs/TestPlan.md §5). Pinned before the extraction so the move has numbers to be
    /// checked against, not a claim to be believed.</summary>
    public string DebugFacts() =>
        $"spawnsRows={_rows.Count} spawnsZones={ZoneCombo.Items.Count} " +
        $"spawnsFollow={(FollowCheck.IsChecked == true ? 1 : 0)}";
}
