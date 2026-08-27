using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>
/// Named-mob respawn countdowns for one zone. The shared view model owns matching,
/// persistence, and edits; this class only renders rows and forwards user actions.
///
/// Lifted out of <c>SpawnsWindow</c> for World PR 1 (docs/Themes.md theme 6): this
/// carries the whole bordered panel — chrome and content together, since the window
/// is borderless and hand-drawn and there is no clean chrome/content seam the way
/// Map/Travel had. <c>SpawnsWindow</c> becomes a thin host owning only what a
/// literal OS window owns: sizing, position, drag-move, close — reached here via
/// <see cref="TopLevel.GetTopLevel(Visual)"/> rather than a captured reference.
///
/// Owns its own 1-second tick, exactly as the window it replaces did.
/// </summary>
internal sealed class SpawnsView
{
    private readonly IZoneHost _host;
    private readonly SpawnsViewModel _vm;
    private readonly AppSettings _settings;
    private readonly TextBlock _title = new();
    private readonly ComboBox _zoneCombo = new() { FontSize = DesignTokens.Spec(Role.Body).Size };
    private readonly CheckBox _followCheck = new() { Content = "Follow", FontSize = DesignTokens.Spec(Role.Caption).Size };
    private readonly StackPanel _rowsPanel = new();
    private readonly ScrollViewer _bodyScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
    private readonly TextBox _addName = DarkBox("", "Add a named the catalog doesn't know");
    private readonly TextBox _addDuration = DarkBox("", "Respawn time: 22 (minutes), 90s, 12h, 3d, 6:40");
    private readonly DispatcherTimer _tick;
    private readonly List<TimerCell> _timerCells = [];
    private readonly Grid _headerRow = new();
    private Grid _titleBar = null!;
    private readonly Border _root;
    private List<SpawnRow> _rows = [];
    private string _signature = "";
    private bool _syncingZone;
    private string? _lastFollowedZone;

    /// <paramref name="initialZone"/>: the zone whose kill popped the window, so it
    /// opens showing the timer that summoned it.
    public SpawnsView(IZoneHost host, SpawnsViewModel vm, string? initialZone = null)
    {
        _host = host;
        _vm = vm;
        _settings = host.Settings;

        _vm.RefreshZoneList();
        foreach (var zone in _vm.ZoneNames) _zoneCombo.Items.Add(zone);
        _followCheck.IsChecked = _settings.SpawnFollowZone;

        _root = BuildContent();
        _root.PointerPressed += OnDrag;
        SelectZone(initialZone
            ?? (_settings.SpawnFollowZone ? _vm.CurrentZoneName : null)
            ?? FirstNonEmpty(_settings.SpawnZone, _vm.ZoneNames.FirstOrDefault() ?? ""));
        _lastFollowedZone = _vm.CurrentZoneName;

        _zoneCombo.SelectionChanged += (_, _) => OnZonePicked();
        _followCheck.IsCheckedChanged += (_, _) => OnFollowChanged();
        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => RefreshRows();
        _tick.Start();
        RefreshRows();
    }

    public Control Body => _root;

    /// <summary>Exposed so the thin host's <c>UpdateHeightLimit</c> can cap the
    /// scroller — the one place the window's geometry and this view's content agree.</summary>
    internal ScrollViewer BodyScrollView => _bodyScroll;

    /// <summary>Stop the independent tick — called from the host's <c>Closed</c>
    /// handler, exactly where <c>_tick.Stop()</c> ran before the lift.</summary>
    public void StopTicking() => _tick.Stop();

    /// <summary>Hide this view's own title row and close button (World PR 2) — leftover
    /// chrome from when this was a borderless standalone window. <c>WorldWindow</c>
    /// supplies both now, so drawing this view's copies too would put two title rows and
    /// two close buttons on screen at once.</summary>
    internal void HideOwnTitleBar() => _titleBar.IsVisible = false;

    /// <summary>Window-edge padding, from the spacing scale — the same value the WPF
    /// window's PadCard resource composes.</summary>
    private static Thickness CardPad => new(
        DesignTokens.SpaceL, DesignTokens.SpaceM, DesignTokens.SpaceL, DesignTokens.SpaceM);

    private static Button ActionButton(string text) => new()
    {
        Content = text,
        FontSize = DesignTokens.Spec(Role.Caption).Size,
        Height = DesignTokens.ControlHeight,
        Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
        Background = AppTheme.PanelBrush,
        Foreground = AppTheme.TextBrush,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(DesignTokens.RadiusControl),
        VerticalContentAlignment = VerticalAlignment.Center,
        Cursor = new Cursor(StandardCursorType.Hand),
    };

    private Border BuildContent()
    {
        _title.FontSize = DesignTokens.Spec(Role.TitleWindow).Size;
        _title.FontWeight = FontWeight.SemiBold;
        _title.Foreground = AppTheme.AccentBrush;
        var close = DesignSystem.IconButton("Close",
            "Hide — the tracker returns on the next named kill; disable tracking in Options or the main menu",
            CloseWindow);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        var header = _titleBar = new Grid { Margin = CardPad };
        header.Children.Add(_title);
        header.Children.Add(close);

        var zoneRow = new Grid { Margin = CardPad };
        zoneRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        zoneRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ToolTip.SetTip(_zoneCombo, "Which zone's named to show");
        zoneRow.Children.Add(_zoneCombo);
        _followCheck.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        ToolTip.SetTip(_followCheck, "Switch to whatever zone the log says you are in");
        Grid.SetColumn(_followCheck, 1);
        zoneRow.Children.Add(_followCheck);

        _bodyScroll.Content = _rowsPanel;
        _rowsPanel.Margin = CardPad;

        var addRow = new Grid();
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(110)));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        addRow.Children.Add(_addName);
        _addDuration.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        Grid.SetColumn(_addDuration, 1);
        addRow.Children.Add(_addDuration);
        var add = ActionButton("Add");
        add.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        add.Click += (_, _) =>
        {
            if (_vm.AddCustom(SelectedZone, _addName.Text ?? "", _addDuration.Text ?? ""))
            {
                _addName.Text = "";
                _addDuration.Text = "";
                Kick();
            }
        };
        Grid.SetColumn(add, 2);
        addRow.Children.Add(add);

        var footer = new StackPanel { Margin = CardPad };
        footer.Children.Add(AppTheme.DimText(
            "Kill a named (or its placeholder) and its countdown starts from the log. Start one by hand with the play button — type how long ago it died first (5m, 90s) or leave it empty for now. Respawn times came from community sources: if what you see in game disagrees, type over the duration. Your number wins, and it survives updates.",
            new Thickness(0, 0, 0, DesignTokens.SpaceS)));
        footer.Children.Add(addRow);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.Children.Add(header);
        Grid.SetRow(zoneRow, 1);
        layout.Children.Add(zoneRow);
        Grid.SetRow(_headerRow, 2);
        layout.Children.Add(_headerRow);
        Grid.SetRow(_bodyScroll, 3);
        layout.Children.Add(_bodyScroll);
        Grid.SetRow(footer, 4);
        layout.Children.Add(footer);
        return new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.RadiusPanel),
            Child = layout,
        };
    }

    private static string FirstNonEmpty(string first, string second) => first.Length > 0 ? first : second;
    private string SelectedZone => _zoneCombo.SelectedItem as string ?? "";

    private void SelectZone(string zone)
    {
        _syncingZone = true;
        _zoneCombo.SelectedItem = _vm.ZoneNames.FirstOrDefault(z =>
            string.Equals(z, zone, StringComparison.OrdinalIgnoreCase));
        _syncingZone = false;
    }

    internal void RefreshRows()
    {
        // Follow zone CHANGES, not every tick. This lets the user browse another zone's
        // list mid-camp without the dropdown immediately snapping back.
        if (_settings.SpawnFollowZone && _vm.CurrentZoneName is { } current
            && current != _lastFollowedZone)
        {
            _lastFollowedZone = current;
            if (current != SelectedZone) SelectZone(current);
        }
        var zone = SelectedZone;
        _title.Text = zone.Length > 0 ? $"Spawns — {zone}" : "Spawns";
        if (zone.Length == 0) return;
        _rows = _vm.RowsFor(zone, DateTime.Now);
        var signature = zone + "" + string.Join("", _rows.Select(r =>
            $"{r.DisplayName}|{r.HasActiveTimer}|{r.IsDue}|{r.DurationText}|{r.Alert}|{r.SoundName}|{r.IsCustom}"));
        if (signature != _signature)
        {
            if (_rowsPanel.GetVisualDescendants().OfType<TextBox>().Any(b => b.IsFocused)) return;
            _signature = signature;
            Rebuild();
        }
        else
            for (var i = 0; i < _rows.Count && i < _timerCells.Count; i++)
                _timerCells[i].Update(_rows[i], DateTime.Now);
    }

    private void Rebuild()
    {
        _rowsPanel.Children.Clear();
        _timerCells.Clear();
        BuildHeader();
        if (_rows.Count == 0)
        {
            _rowsPanel.Children.Add(AppTheme.DimText("No named catalogued for this zone yet - add one below."));
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
            if (row.Detail.Length > 0) ToolTip.SetTip(name, row.Detail);
            grid.Children.Add(name);

            var cell = new TimerCell();
            cell.Update(row, now);
            _timerCells.Add(cell);
            Grid.SetColumn(cell.Root, 1);
            grid.Children.Add(cell.Root);

            // FREE TEXT, deliberately (docs/DesignSystem.md §8c): SpawnDurationText parses
            // 5m, 90s, 22m and "3d 12h", and the numeric spinner the mockup drew would
            // regress week-long raid targets.
            var duration = DarkBox(row.DurationText,
                "Respawn time: 22 (minutes), 90s, 12h, 3d, 3d 12h, 6:40 — your edit persists and outranks the catalog");
            void CommitDuration()
            {
                if ((duration.Text ?? "").Trim() == row.DurationText) return;
                _vm.SetDuration(row.Zone, row.Name, duration.Text ?? "");
                Kick();
            }
            duration.LostFocus += (_, _) => CommitDuration();
            // Enter commits too, as it always has on Windows — alt-tabbing back to the
            // game without clicking elsewhere must not silently drop the typed value
            // (review catch, 2026-08-18).
            duration.KeyDown += (_, e) => { if (e.Key == Key.Enter) CommitDuration(); };
            Grid.SetColumn(duration, 2);
            grid.Children.Add(duration);

            var ago = DarkBox("", "Died how long ago? (5m, 90s) Empty = just now");
            ago.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
            Grid.SetColumn(ago, 3);
            grid.Children.Add(ago);

            // GROUPED actions: start is the one thing you do TO a camp; the rest
            // configure or undo it and sit behind a divider.
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            buttons.Children.Add(DesignSystem.IconButton("Play",
                "Start the countdown from a kill you saw yourself",
                () => { _vm.StartNow(row.Zone, row.Name, ago.Text ?? ""); Kick(); }, "AccentBrush"));
            buttons.Children.Add(new Border
            {
                Width = 1,
                Margin = new Thickness(DesignTokens.SpaceXs),
                Background = AppTheme.HairlineBrush,
            });
            // A vector bell can be COLOURED, which an emoji one could not.
            buttons.Children.Add(DesignSystem.IconButton(row.Alert ? "Bell" : "BellOff",
                "Sound when this one comes due — off by default, like watch-rule sounds (the chip shows DUE either way)",
                () => { _vm.ToggleAlert(row.Zone, row.Name); Kick(); },
                row.Alert ? "AccentBrush" : "DimBrush", row.Alert ? 1.0 : 0.55));
            buttons.Children.Add(BuildSoundPicker(row));
            if (row.HasActiveTimer)
                buttons.Children.Add(DesignSystem.IconButton("Close", "Forget this countdown",
                    () => { _vm.ClearTimer(row.Zone, row.Name); Kick(); }, "DimBrush", 0.55));
            if (row.IsCustom)
                buttons.Children.Add(DesignSystem.IconButton("Trash",
                    "Remove this named (you added it)",
                    () => { _vm.RemoveCustom(row.Zone, row.Name); Kick(); }, "DimBrush", 0.55));
            Grid.SetColumn(buttons, 4);
            grid.Children.Add(buttons);

            // A row is a card now, so a due one can be picked out of forty by its edge.
            var body = new StackPanel();
            body.Children.Add(grid);
            body.Children.Add(cell.Track);
            _rowsPanel.Children.Add(new Border
            {
                Child = body,
                Background = row.IsDue ? AppTheme.WarnWashBrush : AppTheme.PanelBrush,
                BorderBrush = row.IsDue ? AppTheme.WarnBrush : AppTheme.HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
                Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs),
                Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
            });
        }
    }

    /// <summary>The row's column widths, in one place because the HEADER has to agree
    /// with them exactly.</summary>
    /// <summary>Room for start · bell · sound · clear · delete, always reserved.</summary>
    private const double ActionLaneWidth = 132;

    private static void DefineColumns(Grid grid)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(150)));  // timer + bar
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(70)));   // respawn
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(52)));   // died
        // FIXED, not Auto: the header has no buttons, so an Auto column is zero-wide
        // there and every label lands left of the column it names — and a row grows a
        // Clear button when its timer starts, which would reflow the inputs mid-edit.
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ActionLaneWidth)));
    }

    /// <summary>Column headers — five unlabelled columns of boxes and glyphs is a puzzle
    /// the first time and a memory test after that.</summary>
    private void BuildHeader()
    {
        _headerRow.Children.Clear();
        _headerRow.ColumnDefinitions.Clear();
        DefineColumns(_headerRow);
        // A row is a CARD, so its content sits one card-padding further in than the raw
        // grid does. Without matching that, every label is offset from the column it
        // names — worse than no header, and what the first Gate 3 capture showed.
        _headerRow.Margin = new Thickness(DesignTokens.SpaceL + DesignTokens.SpaceM,
            DesignTokens.SpaceM, DesignTokens.SpaceL + DesignTokens.SpaceM, 0);
        var labels = new[] { "Named", "Next spawn", "Respawn", "Died", "" };
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i].Length == 0) continue;
            var text = DesignSystem.Text(Role.Metadata, labels[i]);
            text.Margin = new Thickness(0, 0, DesignTokens.SpaceS, DesignTokens.SpaceXs);
            Grid.SetColumn(text, i);
            _headerRow.Children.Add(text);
        }
    }

    /// <summary>One row's countdown AND its progress toward respawn, as one thing —
    /// because they are one fact, and showing only the text made "due in 4:21" and
    /// "due in 18:31" look equally urgent (the Gate 1 audit's finding about this window).
    /// The bar's star weights ARE the fraction, so the layout system resolves it in
    /// whatever units it is actually working in rather than a measured width.</summary>
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
            _text.FontWeight = FontWeight.SemiBold;
            _percent = DesignSystem.Text(Role.Metadata);
            _percent.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
            _percent.VerticalAlignment = VerticalAlignment.Bottom;

            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(_text);
            line.Children.Add(_percent);

            _filled = new ColumnDefinition(new GridLength(0, GridUnitType.Star));
            _rest = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
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

            // The bar is NOT parented here — it spans the whole row underneath every
            // column, because that is where the room is. The row places it; this still
            // owns it, since the text and the bar are one fact.
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
            _text.Foreground = AppTheme.BrushFor(view.TextColorKey);
            _percent.Text = view.Fraction is { } f && view.State is not TimerView.State.Due
                ? $"{f * 100:0}%" : "";

            _track.IsVisible = view.HasTrack;
            if (!view.HasTrack) return;
            _track.Background = AppTheme.BrushFor(view.TrackColorKey);
            var frac = view.Fraction ?? 0;
            _filled.Width = new GridLength(frac, GridUnitType.Star);
            _rest.Width = new GridLength(1 - frac, GridUnitType.Star);
            if (view.FillColorKey is { } key) _fill.Background = AppTheme.BrushFor(key);
            _fill.IsVisible = view.Fraction is not null;
        }
    }

    private static TextBox DarkBox(string text, string tip)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = DesignTokens.Spec(Role.Caption).Size,
            Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs),
            Background = AppTheme.ComboBoxBrush,
            Foreground = AppTheme.TextBrush,
            BorderBrush = AppTheme.BorderBrush,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(box, tip);
        return box;
    }

    private ComboBox BuildSoundPicker(SpawnRow row)
    {
        var picker = new ComboBox
        {
            FontSize = DesignTokens.Spec(Role.Metadata).Size,
            Width = 78,
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0),
        };
        foreach (var choice in (string[])["Default", "Off", .. AlertSoundCatalog.Names, "Custom…"])
            picker.Items.Add(choice);
        var custom = row.SoundName.Length > 0
            && !string.Equals(row.SoundName, "Off", StringComparison.OrdinalIgnoreCase)
            && !AlertSoundCatalog.Names.Contains(row.SoundName, StringComparer.OrdinalIgnoreCase);
        picker.SelectedItem = row.SoundName.Length == 0 ? "Default"
            : custom ? "Custom…"
            : picker.Items.Cast<string>().First(choice =>
                string.Equals(choice, row.SoundName, StringComparison.OrdinalIgnoreCase));
        ToolTip.SetTip(picker, custom
            ? $"Custom: {row.SoundName}"
            : "Sound for this named; Default is Alarm, and choosing a concrete sound enables its bell");

        var ready = true;
        picker.SelectionChanged += async (_, _) =>
        {
            if (!ready || picker.SelectedItem is not string choice) return;
            switch (choice)
            {
                case "Default":
                    _vm.SetSound(row.Zone, row.Name, "");
                    break;
                case "Off":
                    _vm.SetSound(row.Zone, row.Name, "Off");
                    break;
                case "Custom…":
                    var storage = TopLevel.GetTopLevel(_root)?.StorageProvider;
                    var picked = storage is null
                        ? []
                        : await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = $"Choose a sound for \"{row.Name}\"",
                            AllowMultiple = false,
                            FileTypeFilter =
                            [
                                new FilePickerFileType("Sound files")
                                {
                                    Patterns = AlertSoundFormats.Patterns,
                                },
                            ],
                        });
                    if (picked.FirstOrDefault()?.TryGetLocalPath() is not { } path)
                    {
                        ready = false;
                        picker.SelectedItem = row.SoundName.Length == 0 ? "Default"
                            : custom ? "Custom…" : row.SoundName;
                        ready = true;
                        return;
                    }
                    _vm.SetSound(row.Zone, row.Name, path);
                    _host.PlayAlertSound(path);
                    break;
                default:
                    _vm.SetSound(row.Zone, row.Name, choice);
                    _host.PlayAlertSound(choice);
                    break;
            }
            Kick();
        };
        return picker;
    }

    private void Kick()
    {
        _signature = "";
        RefreshRows();
    }

    private void OnZonePicked()
    {
        if (_syncingZone || SelectedZone.Length == 0) return;
        _settings.SpawnZone = SelectedZone;
        _settings.Save();
        Kick();
    }

    private void OnFollowChanged()
    {
        if (_syncingZone) return;
        _settings.SpawnFollowZone = _followCheck.IsChecked == true;
        _settings.Save();
        RefreshRows();
    }

    // ---- chrome ----
    //
    // BeginMoveDrag/Close are Window-only, so they resolve the actual hosting window
    // at the point of use rather than capturing one in the constructor.

    private void CloseWindow() => (TopLevel.GetTopLevel(_root) as Window)?.Close();

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        // ComboBox clicks originate from template children (Border, ContentPresenter,
        // arrow Path), not from the ComboBox itself. Checking only e.Source made those
        // presses begin a window drag and immediately dismiss the popup.
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(IsInteractiveControl))
            return;
        if (TopLevel.GetTopLevel(_root) is not Window window) return;
        if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed) window.BeginMoveDrag(e);
    }

    private static bool IsInteractiveControl(Visual visual) =>
        visual is TextBox or Button or ComboBox or CheckBox or ScrollBar;

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF view's shape.</summary>
    public string DebugFacts() =>
        $"spawnsRows={_rows.Count} spawnsZones={_zoneCombo.Items.Count} " +
        $"spawnsFollow={(_followCheck.IsChecked == true ? 1 : 0)}";
}
