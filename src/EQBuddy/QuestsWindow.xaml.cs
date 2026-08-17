using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The standalone Quest Tracker (QUEST-*, David's spec 2026-08-07): every wiki quest
/// whose turn-in items overlap what this character owns — looted since the ledger began,
/// or read from the game's own /outputfile inventory dump (bags and bank). One card per
/// quest, most-complete first; expanding a card lists each item as have/need; the quest
/// name opens the eqlwiki walkthrough. "all quests" flips from the overlap view to the
/// whole catalog for browsing ahead.
///
/// One search box is the way in (David, 2026-08-15) — it matches rewards, turn-in items,
/// quest names, givers and zones, so "I want the Wakizashi of the Frozen Skies" is a
/// first-class question. It used to share the header with a "+ I have this" item/quantity
/// row that dominated it visually; that row is gone, and its job belongs to the dump.
/// </summary>
public partial class QuestsWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _mode = "mine";   // mine = items+pins · zone = current zone · all

    public QuestsWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        // Base width so Ctrl+wheel shrinks the WINDOW, not just its text (#186).
        WindowZoom.Attach(this, "quests", _settings, baseWidth: Width);
        EpicClassicOnlyCheck.IsChecked = _settings.EpicQuestClassicOnly;
        BuildClassChecks();
        EraCombo.Items.Add("Any era");
        foreach (var era in QuestEraLadder.Eras) EraCombo.Items.Add($"≤ {era}");
        var savedEra = Array.IndexOf(QuestEraLadder.Eras, _settings.QuestEraFilter);
        EraCombo.SelectedIndex = savedEra >= 0 ? savedEra + 1 : 0;
        foreach (var s in new[] { "any state", "open", "ready", "done" }) StateCombo.Items.Add(s);
        StateCombo.SelectedIndex = 0;
        ApplyModeVisual();
        // No ChipScale here — quests read at widget size, not chip size. That used to be
        // said as ChipScale.Apply(this, 1.0), which is not a no-op: it CLEARS the content
        // LayoutTransform, so it silently threw away the zoom WindowZoom had just restored
        // and every saved Ctrl+wheel zoom was lost on open (#186).
        var restored = ScreenGuard.OnScreen(_settings.QuestsLeft, _settings.QuestsTop, Width, 200);
        if (restored) { Left = _settings.QuestsLeft; Top = _settings.QuestsTop; }
        else
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + 80;
        }
        var (placedLeft, placedTop) = (Left, Top);
        // The cap follows the monitor the window is ACTUALLY on. Sizing against
        // SystemParameters.WorkArea caps against the PRIMARY screen, so dragging the
        // tracker to a smaller second monitor left it taller than that monitor with no
        // way to shrink it — "1/4 of the window is cut off" (#186, Kemble-Kemble). Same
        // primary-only bug class as discussion #31; SpawnsWindow already does this.
        UpdateHeightCaps();
        SourceInitialized += (_, _) => UpdateHeightCaps();
        LocationChanged += (_, _) => UpdateHeightCaps();
        // Ctrl+Z, because the undo button promises it and a promise in a tooltip is a
        // feature. Not while typing in the search box — there it means undo the text.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Z || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (Keyboard.FocusedElement is TextBox) return;
            e.Handled = true;
            OnUndo(this, new RoutedEventArgs());
        };
        Closed += (_, _) =>
        {
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.QuestsLeft, _settings.QuestsTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.QuestsLeft, _settings.QuestsTop);
            _settings.Save();
        };
        Refresh(force: true);
    }

    /// <summary>Height caps follow the monitor this window occupies, re-applied whenever
    /// it moves — a window dragged to a shorter screen must shrink to fit it (#186).</summary>
    private void UpdateHeightCaps()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;   // before the handle exists
        MaxHeight = Math.Max(220, height * 0.85);
    }

    /// <summary>Jump the window to one item's quests (the 🗺 badge in the Loot views):
    /// browse mode + the item as filter, so the quests appear even before any overlap
    /// and each carries its 📌 as the invitation to track.</summary>
    public void FilterToItem(string item)
    {
        _mode = "all";
        ApplyModeVisual();
        FilterBox.Text = item;
        Refresh(force: true);
        Activate();
    }

    /// <summary>Programmatic mode switch (screenshot hook + the 🗺 badge path).</summary>
    internal void SetMode(string mode)
    {
        _mode = mode is "zone" or "all" or "held" or "done" ? mode : "mine";
        ApplyModeVisual();
        Refresh(force: true);
    }

    // ---- top-level tabs: General · Epic 1.0 · Plane of Sky ----

    private QuestTab _tab = QuestTab.General;
    private readonly List<(Border Tile, TextBlock Label, TextBlock Badge, QuestTab Tab)> _tabTiles = [];
    /// <summary>Which single class the view is narrowed to, or null for all of yours.
    /// Session-scoped like the search box: a sticky lens reads as a broken tracker
    /// tomorrow when you have swapped classes.</summary>
    private string? _classLens;
    private readonly List<(Border Chip, TextBlock Label, string? Class)> _classChips = [];

    /// <summary>Build the strip from Core's <see cref="QuestSurface"/> so the desktop and
    /// EQBuddy Mobile cannot disagree about which tabs exist, their order or their
    /// names — the whole reason that lives in Core.</summary>
    private void BuildTabs()
    {
        TabStrip.Children.Clear();
        _tabTiles.Clear();
        foreach (var header in QuestSurface.Tabs(EpicCounts(), SkyCounts()))
        {
            // Tiles, not text (David, 2026-08-15: "I couldn't tell they were tabs at
            // first glance"). Colour comes from the live theme brushes, so a tile
            // follows whatever palette the player picked rather than hard-coding one.
            var label = new TextBlock
            {
                Text = header.Label, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            };
            var badge = new TextBlock
            {
                Text = header.Badge ?? "", FontSize = 10.5, Margin = new Thickness(7, 1, 0, 0),
                Visibility = header.Badge is null ? Visibility.Collapsed : Visibility.Visible,
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(label);
            content.Children.Add(badge);
            var tile = new Border
            {
                Child = content,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = header.Tab,
            };
            tile.MouseLeftButtonDown += OnTabClick;
            TabStrip.Children.Add(tile);
            _tabTiles.Add((tile, label, badge, header.Tab));
        }
        // Chips first, THEN the paint: ApplyTabVisual colours the chip list, so colouring
        // before rebuilding it left every freshly-built chip unstyled until the next
        // unrelated refresh — including the selected one, which is the whole signal.
        BuildClassStrip();
        ApplyTabVisual();
    }

    /// <summary>Any · one of your classes. The ⚙ popup still decides WHICH classes you
    /// have; this decides which of them you're looking at right now, which is a
    /// different question and wanted far more often.</summary>
    private void BuildClassStrip()
    {
        ClassStrip.Children.Clear();
        _classChips.Clear();
        var key = _main.QuestCharacterKey;
        var mine = _main.QuestLedger?.ClassesFor(key) ?? [];
        if (mine.Count == 0
            && _main.CurrentSnapshot().InferredClass is { Length: > 0 } inferred)
            mine = [inferred];
        // One class and no lens to offer: a strip reading "Any · BRD" chooses nothing.
        if (mine.Count < 2) { ClassStrip.Visibility = Visibility.Collapsed; return; }
        ClassStrip.Visibility = Visibility.Visible;

        Add(null, "Any");
        foreach (var cls in mine) Add(cls, QuestClassFilter.Abbrev(cls));

        void Add(string? cls, string text)
        {
            var label = new TextBlock { Text = text, FontSize = 11 };
            var chip = new Border
            {
                Child = label,
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(11, 3, 11, 3),
                Margin = new Thickness(0, 0, 5, 0),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = cls ?? "",
                ToolTip = cls is null
                    ? "Every class you play"
                    : $"Show only {cls} — quests, Epic and Plane of Sky alike",
            };
            chip.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _classLens = ((string)((FrameworkElement)s).Tag) is { Length: > 0 } picked
                    ? picked : null;
                Refresh(force: true);
            };
            ClassStrip.Children.Add(chip);
            _classChips.Add((chip, label, cls));
        }
    }

    private (int Done, int Total)? EpicCounts()
    {
        var items = _settings.EpicQuestChecklist;
        return items.Count == 0 ? null : (items.Count(i => i.Acquired), items.Count);
    }

    private (int Done, int Total)? SkyCounts()
    {
        var items = _settings.SkyQuestChecklist;
        return items.Count == 0 ? null : (items.Count(i => i.Acquired), items.Count);
    }

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (((FrameworkElement)sender).Tag is not QuestTab tab) return;
        _tab = tab;
        ApplyTabVisual();
        Refresh(force: true);
    }

    private void ApplyTabVisual()
    {
        foreach (var (tile, label, badge, tab) in _tabTiles)
        {
            var on = tab == _tab;
            // Selected: the accent fills the tile and the text inverts onto it, which is
            // what makes a tab read as a tab at a glance. Unselected still gets a filled
            // panel and a border, so the row looks like a set of controls rather than a
            // line of prose.
            tile.SetResourceReference(Border.BackgroundProperty,
                on ? "AccentBrush" : "ToggleHighlightBrush");
            tile.SetResourceReference(Border.BorderBrushProperty,
                on ? "AccentBrush" : "BorderBrush");
            label.SetResourceReference(TextBlock.ForegroundProperty, on ? "BgBrush" : "TextBrush");
            badge.SetResourceReference(TextBlock.ForegroundProperty, on ? "BgBrush" : "DimBrush");
            badge.Opacity = on ? 0.85 : 1;
        }
        foreach (var (chip, label, cls) in _classChips)
        {
            var on = cls == _classLens;
            chip.SetResourceReference(Border.BackgroundProperty,
                on ? "AccentBrush" : "ToggleHighlightBrush");
            chip.SetResourceReference(Border.BorderBrushProperty, on ? "AccentBrush" : "BorderBrush");
            label.SetResourceReference(TextBlock.ForegroundProperty, on ? "BgBrush" : "DimBrush");
        }
        // Era, state and the mode strip are catalog concepts — meaningless against a
        // fixed checklist. The CLASS picker is not: David, 2026-08-15, "we may be
        // helping a friend", so every tab must be able to reach a class you don't play.
        var catalogOnly = _tab == QuestTab.General ? Visibility.Visible : Visibility.Collapsed;
        EraCombo.Visibility = catalogOnly;
        StateCombo.Visibility = catalogOnly;
        ModeStrip.Visibility = catalogOnly;
        // The Epic tab's own lens, which followed the Epic card here when the widget
        // consolidated its quest cards (2026-08-16).
        EpicClassicOnlyCheck.Visibility = _tab == QuestTab.Epic ? Visibility.Visible : Visibility.Collapsed;
        ClassBtn.Visibility = Visibility.Visible;
        FilterRow.Visibility = Visibility.Visible;
    }

    private void OnModeClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _mode = (string)((FrameworkElement)sender).Tag;
        ApplyModeVisual();
        Refresh(force: true);
    }

    private void ApplyModeVisual()
    {
        foreach (var (tb, key) in new[]
            { (ModeMine, "mine"), (ModeZone, "zone"), (ModeHeld, "held"), (ModeDone, "done"), (ModeAll, "all") })
        {
            tb.SetResourceReference(TextBlock.ForegroundProperty, key == _mode ? "AccentBrush" : "DimBrush");
            if (key == _mode) tb.SetResourceReference(TextBlock.BackgroundProperty, "ToggleHighlightBrush");
            else tb.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    // ---- multiclass filter (Legends: up to three active classes; David 2026-08-07) ----

    private readonly List<CheckBox> _classChecks = [];

    private void BuildClassChecks()
    {
        foreach (var cls in QuestClassFilter.Classes)
        {
            var check = new CheckBox { Margin = new Thickness(0, 1, 0, 1) };
            var label = new TextBlock { Text = cls, FontSize = 12 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            check.Content = label;
            check.Checked += (_, _) => OnClassCheckChanged();
            check.Unchecked += (_, _) => OnClassCheckChanged();
            _classChecks.Add(check);
            ClassChecks.Children.Add(check);
        }
    }

    private List<string> SelectedClasses() =>
        _classChecks.Where(c => c.IsChecked == true)
            .Select(c => ((TextBlock)c.Content).Text).ToList();

    private bool _syncingClasses;

    private void OnClassCheckChanged()
    {
        if (_syncingClasses) return;
        var selected = SelectedClasses();
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is { } ledger && key.Length > 0)
            ledger.SetClasses(key, selected);
        UpdateClassButton(selected);
        Refresh(force: true);
    }

    // Capped in UI.Shared so the Avalonia window cannot disagree: an uncapped face grew
    // with the selection and pushed the mode strip off the window (#184).
    private void UpdateClassButton(List<string> selected)
    {
        ClassBtn.Content = ClassFilterLabel.For(selected);
        ClassBtn.ToolTip = selected.Count > ClassFilterLabel.MaxNamed
            ? "Showing: " + string.Join(", ", selected)
            : "Pick your class(es) — quests any of them can do stay visible";
    }

    /// <summary>Load the character's saved classes into the checkboxes (character
    /// switches included — the selection follows the ledger, not the window).</summary>
    private void SyncClassChecks(List<string> saved)
    {
        var current = SelectedClasses();
        if (current.SequenceEqual(saved, StringComparer.OrdinalIgnoreCase)) return;
        _syncingClasses = true;
        foreach (var check in _classChecks)
            check.IsChecked = saved.Contains(((TextBlock)check.Content).Text,
                StringComparer.OrdinalIgnoreCase);
        _syncingClasses = false;
        UpdateClassButton(saved);
    }

    private void OnClassBtn(object sender, RoutedEventArgs e) =>
        ClassPopup.IsOpen = !ClassPopup.IsOpen;

    /// <summary>The Epic tab's classic-era lens. Persisted, because EQBuddy Mobile's
    /// Epic tab honors the same setting — one filter, both screens.</summary>
    private void OnEpicClassicOnlyToggled(object sender, RoutedEventArgs e)
    {
        _settings.EpicQuestClassicOnly = EpicClassicOnlyCheck.IsChecked == true;
        _settings.Save();
        Refresh(force: true);
    }

    // The state filter (Reddit ask, 2026-08-11): cuts across every tab and search —
    // session-scoped on purpose, like the search box; a sticky "done" filter would
    // read as an empty tracker tomorrow.
    private string _state = "any state";

    private void OnStateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StateCombo.SelectedItem is not string s) return;
        _state = s;
        Refresh(force: true);
    }

    private void OnEraChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EraCombo.SelectedIndex < 0) return;
        _settings.QuestEraFilter = EraCombo.SelectedIndex == 0
            ? "" : QuestEraLadder.Eras[EraCombo.SelectedIndex - 1];
        _settings.Save();
        Refresh(force: true);
    }

    /// <summary>Called from MainWindow's 1 s tick while visible; cheap unless the ledger
    /// or filters actually changed (signature idiom, same as the chip windows).</summary>
    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 2) Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        var key = _main.QuestCharacterKey;
        var character = key.Length > 0 ? key.Split('_')[0] : "";
        TitleText.Text = character.Length > 0
            ? $"🗺 Quest Tracker — {char.ToUpper(character[0])}{character[1..]}"
            : "🗺 Quest Tracker";

        var owned = _main.QuestLedger?.For(key)
            ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
        var tracked = _main.QuestLedger?.TrackedFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hidden = _main.QuestLedger?.HiddenFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completed = _main.QuestLedger?.CompletedFor(key)
            ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var filter = FilterBox.Text.Trim();
        var classes = _main.QuestLedger?.ClassesFor(key) ?? [];
        SyncClassChecks(classes);
        // No classes picked? The log's own evidence pre-filters — ALWAYS labeled
        // inferred, never persisted, and one popup pick overrides it (David,
        // 2026-08-11: players swap classes, so this is a reading, not a fact).
        var inferred = "";
        if (classes.Count == 0 && _main.CurrentSnapshot().InferredClass is { Length: > 0 } inf)
        {
            inferred = inf;
            classes = [inf];
        }
        // The lens narrows to ONE of the classes you play. Everything downstream reads
        // `classes`, so narrowing it here covers the catalog, the zone view and the
        // item-driven tabs at once. A stale lens (you dropped that class) is ignored
        // rather than emptying the window.
        if (_classLens is { } lens && classes.Contains(lens, StringComparer.OrdinalIgnoreCase))
            classes = [lens];
        else if (_classLens is not null && !classes.Contains(_classLens, StringComparer.OrdinalIgnoreCase))
            _classLens = null;

        var sig = $"{key}|{filter}|{_mode}|st:{_state}|{string.Join("+", classes)}|inf:{inferred}|{_settings.QuestEraFilter}|{_main.CurrentZoneName}" +
            $"|{string.Join(";", tracked.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", hidden.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", completed.Select(kv => $"{kv.Key}:{kv.Value}"))}" +
            $"|{string.Join(",", owned.Select(kv => $"{kv.Key}:{kv.Value.Total}"))}";
        if (!force && sig == _signature) return;
        _signature = sig;

        QuestsPanel.Children.Clear();
        _rendered = 0;
        _suppressed = 0;
        BuildTabs();
        if (_tab != QuestTab.General)
        {
            RenderChecklist(_tab, filter, classes);
            return;
        }
        if (inferred.Length > 0)
        {
            var note = new TextBlock
            {
                Text = $"🎭 Filtering for {inferred} (inferred from your most-used skills — " +
                    "pick classes above to override; inference follows you if you swap)",
                FontSize = 10.5, Margin = new Thickness(2, 0, 0, 4), TextWrapping = TextWrapping.Wrap,
            };
            note.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            QuestsPanel.Children.Add(note);
        }

        var era = _settings.QuestEraFilter;
        // Era and class gate separately since 2026-08-11 (David's Crushbone session):
        // era = world availability, always honored; class = the browse lens — and
        // item-driven views (mine, held) show out-of-class quests in their own
        // section rather than hiding what your bags are literally holding pieces of.
        bool EraOk(QuestEntry q) => QuestEraLadder.Allowed(q.Era, era);
        bool ClassOnlyOk(QuestEntry q) => QuestClassFilter.MatchesAny(q.Classes, classes);
        bool ClassOk(QuestEntry q) => ClassOnlyOk(q) && EraOk(q);
        bool StateOk(QuestMatch m) => _state switch
        {
            "open" => completed.GetValueOrDefault(m.Quest.Name) == 0,
            "ready" => m.Complete && m.ItemsTotal > 0,
            "done" => completed.GetValueOrDefault(m.Quest.Name) > 0,
            _ => true,
        };
        QuestMatch Progressed(QuestEntry quest)
        {
            var progress = quest.Items
                .Select(i => new QuestItemProgress(i.Name, i.Qty,
                    owned.TryGetValue(i.Name, out var e) ? e.Total : 0)).ToList();
            return new QuestMatch(quest, progress.Count(p => p.Have > 0), progress.Count,
                progress, tracked.Contains(quest.Name));
        }
        // Every branch funnels through here, so the render cap lives here too. Building
        // a card is not free — each one lays out item rows and wires wiki tooltips — and
        // "all" hands this the entire 1,172-quest catalog. Doing that per keystroke is
        // what froze the window (David, 2026-08-15: "typing in the search box is
        // extremely slow, it sort of freezes up the window").
        void AddCard(QuestMatch m)
        {
            if (_rendered >= RenderCap) { _suppressed++; return; }
            _rendered++;
            QuestsPanel.Children.Add(
                Card(m, hidden.Contains(m.Quest.Name), completed.GetValueOrDefault(m.Quest.Name)));
        }
        void EmptyNote(string text)
        {
            var note = new TextBlock
            {
                Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 8, 0, 8),
            };
            note.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            QuestsPanel.Children.Add(note);
        }

        // A typed search reads the WHOLE catalog, whatever tab is active (David,
        // 2026-08-10: "type an item name and see quests using that; type a quest
        // name to find and track progress"). The tabs scope browsing; a search
        // scopes finding — otherwise an item search on the mine tab found nothing
        // until you already owned pieces, which is backwards.
        if (filter.Length > 0)
        {
            // A search answers with the WHOLE catalog — no class/era/state gating
            // (David's live catch, 2026-08-11: the Blue Orc Head badge found
            // "nothing" because The Falchion is Paladin and his class filter
            // wasn't). Each card states its own class and era; the reader decides.
            var found = QuestSearch.Find(_main.QuestCatalog, filter)
                .Select(Progressed)
                .OrderByDescending(m => m.Tracked)
                .ThenByDescending(m => m.Fraction)
                .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var scope = new TextBlock
            {
                Text = $"🔎 {found.Count} match{(found.Count == 1 ? "" : "es")} in the whole catalog — names, turn-in items, rewards, NPCs, zones. " +
                    "Search ignores your class/era/state filters; each card says whose it is.",
                FontSize = 11, Margin = new Thickness(2, 0, 0, 5), TextWrapping = TextWrapping.Wrap,
            };
            scope.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            QuestsPanel.Children.Add(scope);
            foreach (var m in found) AddCard(m);
            if (found.Count == 0)
                EmptyNote("Nothing matches. Searches cover quest names, turn-in items, " +
                          "rewards, quest givers, and zones — try fewer words.");
            return;
        }

        switch (_mode)
        {
            case "all":
                foreach (var m in _main.QuestCatalog.Quests
                             .Where(q => ClassOk(q))
                             .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                             .Select(Progressed)
                             .Where(StateOk))
                    AddCard(m);
                break;

            case "zone" when _main.CurrentZoneName.Length == 0:
                EmptyNote("No zone seen in the log yet — zone view fills in once " +
                          "you've zoned somewhere.");
                break;

            case "zone":
            {
                // Everything workable where you stand — including dialogue chains the
                // item parser found nothing for (David: "not everything is item driven").
                var zoneLabel = new TextBlock
                {
                    Text = $"📍 {_main.CurrentZoneName}", FontSize = 11,
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 5),
                };
                zoneLabel.SetResourceReference(TextBlock.ForegroundProperty, "WarnBrush");
                QuestsPanel.Children.Add(zoneLabel);
                var zoneQuests = _main.QuestCatalog.Quests
                    .Where(q => q.TouchesZone(_main.CurrentZoneName)
                                && MatchesFilter(q, filter) && ClassOk(q))
                    .Select(Progressed)
                    .Where(StateOk)
                    .OrderByDescending(m => m.Tracked)
                    .ThenByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var m in zoneQuests) AddCard(m);
                if (zoneQuests.Count == 0)
                    EmptyNote($"No catalogued quests touch {_main.CurrentZoneName}.");
                break;
            }

            case "held":
            {
                // What the bags could turn in right now, AND what they contribute to
                // (David, 2026-08-11, round two): fully-covered quests lead, partial
                // overlaps follow sorted by closeness — "available quests based on
                // what's in my inventory". The /outputfile command is one click to
                // copy, one paste into the game's chat.
                var snap = _main.LatestInventory(refresh: force);
                Button CopyCmd()
                {
                    var b = new Button
                    {
                        Style = (Style)FindResource("ActionButton"), FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 4, 0, 6),
                        ToolTip = "Copies the command — paste it into the game's chat and the " +
                            "game writes your inventory file; this tab reads it. Re-run any " +
                            "time your bags change.",
                    };
                    return Theming.WireCopyCommand(b, EQBuddy.UI.Shared.GameCommands.OutputfileInventory);
                }
                if (snap is null)
                {
                    EmptyNote("No inventory dump found yet. In game, run this (the game writes " +
                        "<name>_<server>-Inventory.txt beside its own folders and this tab reads " +
                        "it — EQBuddy never scans the game itself):");
                    QuestsPanel.Children.Add(CopyCmd());
                    break;
                }
                var invAge = DateTime.Now - snap.WrittenAt;
                var invLabel = new TextBlock
                {
                    Text = $"📦 {System.IO.Path.GetFileName(snap.Path)} — written " +
                        (invAge.TotalMinutes < 1 ? "just now" : invAge.TotalHours < 1
                            ? $"{(int)invAge.TotalMinutes}m ago" : $"{(int)invAge.TotalHours}h ago") +
                        " (plus everything looted since)",
                    FontSize = 11, Margin = new Thickness(2, 0, 0, 2), TextWrapping = TextWrapping.Wrap,
                };
                invLabel.SetResourceReference(TextBlock.ForegroundProperty, "WarnBrush");
                QuestsPanel.Children.Add(invLabel);
                QuestsPanel.Children.Add(CopyCmd());

                // NO class gate on the pool: your bags don't care what class a quest
                // is for (The Falchion's Blue Orc Head in a monk's bag is a farm,
                // not a mistake). In-class leads; the rest gets its own section.
                var overlapping = _main.QuestCatalog.Quests
                    .Where(q => q.Items.Count > 0 && !q.Collection && EraOk(q) && !hidden.Contains(q.Name))
                    .Select(q => new QuestMatch(q,
                        q.Items.Count(i => snap.CountOf(i.Name) > 0), q.Items.Count,
                        q.Items.Select(i => new QuestItemProgress(i.Name, i.Qty, snap.CountOf(i.Name))).ToList(),
                        tracked.Contains(q.Name)))
                    .Where(m => m.ItemsHave > 0)
                    .ToList();
                void Section(string text)
                {
                    var tb = new TextBlock { Text = text, Style = (Style)FindResource("SectionLabel") };
                    QuestsPanel.Children.Add(tb);
                }
                var mine2 = overlapping.Where(m => ClassOnlyOk(m.Quest)).ToList();
                var others = overlapping.Where(m => !ClassOnlyOk(m.Quest))
                    .OrderByDescending(m => m.Complete).ThenByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var ready = mine2.Where(m => m.Complete)
                    .OrderBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var partial = mine2.Where(m => !m.Complete)
                    .OrderByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase).ToList();
                if (ready.Count > 0)
                {
                    Section($"Ready from your bags ({ready.Count})");
                    foreach (var m in ready) AddCard(m);
                }
                if (partial.Count > 0)
                {
                    Section($"Your bags contribute ({partial.Count})");
                    foreach (var m in partial) AddCard(m);
                }
                if (others.Count > 0)
                {
                    Section($"For other classes — you hold pieces anyway ({others.Count})");
                    foreach (var m in others) AddCard(m);
                }
                if (overlapping.Count == 0)
                    EmptyNote("Nothing in your bags matches a catalogued quest's turn-ins yet.");
                break;
            }

            case "done":
            {
                // The trophy shelf — and the catch-up surface: every card carries a ✓,
                // so returning players can mark history without touching items.
                var done = completed.Where(kv => kv.Value > 0)
                    .Select(kv => (_main.QuestCatalog.Quests.FirstOrDefault(q =>
                        q.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)), kv.Value))
                    .Where(x => x.Item1 is not null && ClassOk(x.Item1!))
                    .OrderBy(x => x.Item1!.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var (q, _) in done) AddCard(Progressed(q!));
                if (done.Count == 0)
                    EmptyNote("Nothing marked completed yet. Every quest card has a ✓ — click it " +
                              "on quests you finished before EQBuddy and the tracker catches up " +
                              "(ready quests count themselves when you click their hand-in).");
                break;
            }

            default:
            {
                // "mine": item overlap + pins, minus dismissed and finished-for-good
                // (completed non-repeatables stay visible in zone/all with their ✓).
                var doneForGood = new HashSet<string>(
                    completed.Where(kv => kv.Value > 0).Select(kv => kv.Key)
                        .Where(name => _main.QuestCatalog.Quests.FirstOrDefault(q =>
                            q.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Repeatable: false }),
                    StringComparer.OrdinalIgnoreCase);
                doneForGood.UnionWith(hidden);
                var matches = QuestMatcher.Match(_main.QuestCatalog, owned, tracked, doneForGood);
                // Same rule as held (David's Crushbone session): items you LOOTED
                // outrank the class lens — out-of-class overlaps show in their own
                // section instead of vanishing.
                var eligible = matches
                    .Where(m => MatchesFilter(m.Quest, filter) && EraOk(m.Quest) && StateOk(m))
                    .ToList();
                var shown = eligible.Where(m => ClassOnlyOk(m.Quest)).ToList();
                var othersMine = eligible.Where(m => !ClassOnlyOk(m.Quest)).ToList();
                foreach (var m in shown) AddCard(m);
                if (othersMine.Count > 0)
                {
                    var lbl = new TextBlock
                    {
                        Text = $"For other classes — from your items ({othersMine.Count})",
                        Style = (Style)FindResource("SectionLabel"),
                    };
                    QuestsPanel.Children.Add(lbl);
                    foreach (var m in othersMine) AddCard(m);
                }
                if (shown.Count == 0 && othersMine.Count == 0)
                    EmptyNote(matches.Count == 0
                        ? "Nothing yet — loot a quest item (they show green in the Loot list),\n" +
                          "or ⧉ scan bags to read what you already carry.\n" +
                          "Search a reward you want by name, or try \"zone\" and \"all\" to browse."
                        : "No quest matches that search — try a reward name, an item, or an NPC.");
                break;
            }
        }

        // Never a silent cap (CLAUDE.md): say how many are hidden and how to reach them.
        if (_suppressed > 0)
        {
            var more = new TextBlock
            {
                Text = $"+{_suppressed} more — showing the first {RenderCap}. "
                     + "Keep typing to narrow it down.",
                FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 6, 0, 8),
            };
            more.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            QuestsPanel.Children.Add(more);
        }
    }

    /// <summary>The Epic and Sky tabs. Rows come straight from the same settings lists
    /// the loot auto-checkers tick and EQBuddy Mobile reads, so ticking here, on the
    /// tablet, or by looting the thing are all the same tick — this is a second VIEW,
    /// never a second copy of the data. The search box keeps working here: on a 100-row
    /// class list, "Wakizashi" beating your eyes down the page is the same value it has
    /// on the catalog tab.
    ///
    /// The rows are TICKABLE, and have to be: hand-ticking used to live on the widget's
    /// Epic and Sky cards, and when those became one launcher (2026-08-16) this became
    /// the only place on the desktop to say "I already have that". Read-only rows here
    /// would have quietly moved the job to the phone.</summary>
    private void RenderChecklist(QuestTab tab, string filter, List<string> classes)
    {
        // Grouping, ordering and the detail line come from Core so this window, the
        // Avalonia one and EQBuddy Mobile cannot disagree about what a checklist row
        // says — they already had (#184).
        var groups = tab == QuestTab.Epic
            ? QuestChecklistLayout.Epic(_settings.EpicQuestChecklist
                .Where(i => !_settings.EpicQuestClassicOnly || i.AvailableInClassic))
            : QuestChecklistLayout.Sky(_settings.SkyQuestChecklist, _settings.SkyQuestCompleted);

        var setters = tab == QuestTab.Epic
            ? _settings.EpicQuestChecklist.ToDictionary(i => i.Id, i => (Action<bool>)(done =>
            {
                i.Acquired = done;
                // The player deciding IS the resolution of an unassigned auto-tick,
                // exactly as the old card's toggle treated it.
                i.AcquiredUnassigned = false;
            }), StringComparer.Ordinal)
            : _settings.SkyQuestChecklist.ToDictionary(i => i.Id, i => (Action<bool>)(done =>
            {
                i.Acquired = done;
                i.AcquiredUnassigned = false;
            }), StringComparer.Ordinal);

        // The ⚙ picker chooses WHICH classes are in view — including ones you don't play,
        // because "we may be helping a friend" (David, 2026-08-15). The chips then narrow
        // to one of them. An empty pick means every class, never an empty window.
        var matching = groups
            .Where(g => classes.Count == 0
                || classes.Contains(g.ClassName, StringComparer.OrdinalIgnoreCase))
            .Where(g => _classLens is null
                || g.ClassName.Equals(_classLens, StringComparison.OrdinalIgnoreCase))
            .Select(g => filter.Length == 0 || Hit(g.Heading)
                ? g
                : g with { Rows = [.. g.Rows.Where(r => Hit(r.Title) || Hit(r.Detail))] })
            .Where(g => g.Rows.Count > 0)
            .ToList();

        bool Hit(string s) => s.Contains(filter, StringComparison.OrdinalIgnoreCase);

        if (matching.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = filter.Length > 0
                    ? "Nothing on this checklist matches that search."
                    : "This checklist is empty — it fills in from the wiki catalog and "
                      + "your own progress. ⧉ scan bags or import achievements to catch it up.",
                FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 8, 0, 8),
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            QuestsPanel.Children.Add(empty);
            return;
        }

        foreach (var group in matching)
        {
            // The heading opens the wiki page for the reward it names — the "way to view
            // details of sky quests" #184 asked back for. Catalog cards have carried a
            // clickable name since the tracker existed; checklist rows never did.
            var headingText = new TextBlock
            {
                Text = $"{group.Heading}   {group.Done}/{group.Total}"
                       + (group.Note is { } n ? $"  · {n}" : ""),
                FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 8, 0, 3),
                Cursor = Cursors.Hand,
                ToolTip = "Open the wiki page for this quest",
            };
            headingText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            var rewardName = group.Heading.Split('·').Last().Trim();
            headingText.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenUrl(EqlWiki.PageUrl(rewardName));
            };
            QuestsPanel.Children.Add(headingText);

            foreach (var row in group.Rows)
            {
                var text = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
                text.Inlines.Add(new System.Windows.Documents.Run(row.Title));
                if (row.Detail.Length > 0)
                {
                    // The drop location, dimmed — present on every row, because "where
                    // does this come from" is the question the row exists to answer.
                    var detail = new System.Windows.Documents.Run("   " + row.Detail);
                    detail.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                    text.Inlines.Add(detail);
                }
                if (row.Unassigned)
                {
                    // The auto-tick guessed which class earned a shared item. Say so.
                    var mark = new System.Windows.Documents.Run(" *") { FontWeight = FontWeights.Bold };
                    mark.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "WarnBrush");
                    text.Inlines.Add(mark);
                }
                text.SetResourceReference(TextBlock.ForegroundProperty,
                    row.Acquired ? "DimBrush" : "TextBrush");
                var check = new CheckBox
                {
                    Content = text,
                    IsChecked = row.Acquired,
                    Margin = new Thickness(10, 1, 0, 1),
                    ToolTip = row.Unassigned
                        ? "EQBuddy ticked this itself — several classes want this item and the "
                          + "log couldn't say which one earned it. Move the tick if it's on the "
                          + "wrong class; either way, toggling it settles the question."
                        : null,
                };
                if (!setters.TryGetValue(row.Id, out var set)) continue;
                check.Checked += (_, _) => Tick(true);
                check.Unchecked += (_, _) => Tick(false);
                QuestsPanel.Children.Add(check);

                void Tick(bool done)
                {
                    set(done);
                    _settings.Save();
                    PushUndo(row, done, set);
                    Refresh(force: true);
                }
            }
        }
    }

    // ---- undo (#184) ----

    /// <summary>Ticks this session, newest last. A tick is one click and saves at once,
    /// so without this a mis-click is unrecoverable except from memory — bjstrange lost
    /// three and had to work out what had been checked before.</summary>
    private readonly Stack<(string Label, bool Was, Action<bool> Set)> _undo = new();

    private void PushUndo(QuestChecklistRow row, bool done, Action<bool> set)
    {
        _undo.Push((row.Title, !done, set));
        UndoText.Text = $"{(done ? "Ticked" : "Cleared")} {row.Title}";
        UndoBar.Visibility = Visibility.Visible;
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (!_undo.TryPop(out var last)) { UndoBar.Visibility = Visibility.Collapsed; return; }
        last.Set(last.Was);
        _settings.Save();
        if (_undo.Count == 0) UndoBar.Visibility = Visibility.Collapsed;
        else UndoText.Text = $"Undid {last.Label}";
        Refresh(force: true);
    }

    // One search predicate, shared with the tests that guard it (QuestSearch in Core).
    private static bool MatchesFilter(QuestEntry q, string filter) => QuestSearch.Matches(q, filter);

    // ---- card building ----

    private Border Card(QuestMatch m, bool isHidden = false, int completedCount = 0)
    {
        var body = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBlock
        {
            Text = m.Quest.Name, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, Cursor = Cursors.Hand,
            ToolTip = "Open the wiki walkthrough",
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        name.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenUrl(m.Quest.Url);
        };
        Grid.SetColumn(name, 0);
        header.Children.Add(name);

        var count = new TextBlock
        {
            // Collection pages (CatalogHygiene): several quests share the page, so a
            // fraction over the union would lie — the label says what it is instead.
            Text = m.Quest.Collection ? "📚 set of quests"
                : m.ItemsTotal == 0 ? "steps"
                : m.Complete
                    ? m.Quest.Repeatable && m.ReadyCount > 1 ? $"✔ ready ×{m.ReadyCount}" : "✔ ready"
                    : $"{m.ItemsHave}/{m.ItemsTotal}",
            FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(8, 0, 0, 0),
            ToolTip = m.Quest.Collection
                ? "This wiki page documents several quests at once, so per-page progress would " +
                  "mislead — open the page for the individual quests. Your items still show below."
                : null,
        };
        count.SetResourceReference(TextBlock.ForegroundProperty,
            m.Quest.Collection ? "DimBrush"
            : m.Complete ? "GoodBrush" : m.ItemsHave > 0 ? "AccentBrush" : "DimBrush");
        // A ready card's count doubles as the "I handed it in" button: consumes one set
        // of turn-ins and bumps the done counter. Dialogue quests mark done for free.
        if (m.Complete || m.ItemsTotal == 0)
        {
            count.Cursor = Cursors.Hand;
            count.ToolTip = m.ItemsTotal == 0
                ? "Click when you finish this quest to mark it done"
                : "Click when you hand it in — consumes one set of turn-in items and counts a completion";
            count.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                var key = _main.QuestCharacterKey;
                if (_main.QuestLedger is { } ledger && key.Length > 0)
                {
                    ledger.RecordCompletion(key, m.Quest.Name, m.Quest.Items);
                    Refresh(force: true);
                }
            };
        }
        Grid.SetColumn(count, 1);
        header.Children.Add(count);

        // 📌 = "keep this quest in front of me": tracked quests sort first and stay
        // visible even with zero items — the choose-to-track affordance (David,
        // 2026-08-07: "players can choose to track quests or not, easily").
        var pin = new TextBlock
        {
            Text = "📌", FontSize = 12, Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand, Opacity = m.Tracked ? 1.0 : 0.35,
            ToolTip = m.Tracked ? "Stop tracking this quest" : "Track this quest",
        };
        pin.SetResourceReference(TextBlock.ForegroundProperty, m.Tracked ? "AccentBrush" : "DimBrush");
        pin.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var key = _main.QuestCharacterKey;
            if (_main.QuestLedger is { } ledger && key.Length > 0)
            {
                ledger.SetTracked(key, m.Quest.Name, !m.Tracked);
                Refresh(force: true);
            }
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pin, 2);
        header.Children.Add(pin);

        // ✓ = "I did this before EQBuddy" (David, 2026-08-11): catch-up marking on
        // EVERY card, consuming nothing — RecordCompletion's consume path is for
        // hand-ins happening now. Clicking again unmarks a misclick. Completed
        // non-repeatables leave "mine" and gather under the done tab.
        var doneMark = new TextBlock
        {
            Text = "✓", FontSize = 12, Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand, Opacity = completedCount > 0 ? 1.0 : 0.35,
            ToolTip = completedCount > 0
                ? $"Completed ×{completedCount} — click to unmark"
                : "Did this before EQBuddy? Mark it completed (consumes nothing; click again to undo)",
        };
        doneMark.SetResourceReference(TextBlock.ForegroundProperty,
            completedCount > 0 ? "GoodBrush" : "DimBrush");
        doneMark.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var key = _main.QuestCharacterKey;
            if (_main.QuestLedger is { } ledger && key.Length > 0)
            {
                ledger.SetCompleted(key, m.Quest.Name, completedCount == 0);
                Refresh(force: true);
            }
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(doneMark, header.ColumnDefinitions.Count - 1);
        header.Children.Add(doneMark);

        // ✕ = "not interested": drops the quest from the overlap view AND un-greens
        // loot only it wants (David, 2026-08-07: "there are definitely some I don't
        // want to track"). Hidden quests reappear dimmed under "all quests", where ✕
        // becomes the way back.
        var dismiss = new TextBlock
        {
            Text = "✕", FontSize = 11, Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand, Opacity = isHidden ? 1.0 : 0.35,
            ToolTip = isHidden
                ? "Show this quest again"
                : "Not interested — hide this quest (its items stop showing green unless another quest wants them)",
        };
        dismiss.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        dismiss.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var key = _main.QuestCharacterKey;
            if (_main.QuestLedger is { } ledger && key.Length > 0)
            {
                ledger.SetHidden(key, m.Quest.Name, !isHidden);
                Refresh(force: true);
            }
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(dismiss, 4);
        header.Children.Add(dismiss);

        // ⚑ = "this data is wrong" (David, 2026-08-11: one wrong quest drops faith in
        // everything). One click opens a prefilled report — the catalog's accuracy
        // loop runs on these, same as every parser fix ran on pasted log lines.
        var flag = new TextBlock
        {
            Text = "⚑", FontSize = 11, Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand, Opacity = 0.35,
            ToolTip = "Something wrong with this quest's data (items, giver, zone)? " +
                "Open a prefilled report — fixes usually ship the same day.",
        };
        flag.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        flag.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            var body =
                $"Quest: {m.Quest.Name}\nWiki page: {m.Quest.Url}\n" +
                $"EQBuddy shows: {m.ItemsTotal} turn-in item(s) — {string.Join(", ", m.Quest.Items.Select(i => i.Qty > 1 ? $"{i.Name} x{i.Qty}" : i.Name))}\n" +
                $"Giver: {m.Quest.QuestGiver} · Zone: {m.Quest.StartZone}\n\nWhat's wrong:\n\n\n" +
                "---\nNote: EQBuddy mirrors eqlwiki.com, so if the wiki page itself is wrong, " +
                "editing the page is the strongest fix — the catalog re-harvests it weekly. " +
                "If the page is right and EQBuddy read it wrong, this report is exactly the right place.\n";
            OpenUrl("https://github.com/DranakCorps-bot/EQBuddy/discussions/new?category=q-a" +
                "&title=" + Uri.EscapeDataString($"Quest data: {m.Quest.Name}") +
                "&body=" + Uri.EscapeDataString(body));
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(flag, 5);
        header.Children.Add(flag);
        body.Children.Add(header);

        if (m.Quest.Rewards.Count > 0)
        {
            // The payoff sits right under the title (David, 2026-08-07: "Crude Stein
            // Quest should show the Crude Stein item"), with the same hover/click as
            // loot: hover pulls the item's wiki stats live, click opens its page.
            var wrap = new WrapPanel { Margin = new Thickness(0, 1, 0, 1) };
            var label = new TextBlock
            {
                Text = "Rewards:", FontSize = 10.5, Margin = new Thickness(0, 0, 6, 0),
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            wrap.Children.Add(label);
            const int shown = 6;
            foreach (var reward in m.Quest.Rewards.Take(shown))
                wrap.Children.Add(RewardLink(reward));
            if (m.Quest.Rewards.Count > shown)
            {
                var more = new TextBlock
                {
                    Text = $"+{m.Quest.Rewards.Count - shown} more", FontSize = 10.5,
                    ToolTip = string.Join("\n", m.Quest.Rewards.Skip(shown)),
                };
                more.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
                wrap.Children.Add(more);
            }
            body.Children.Add(wrap);
        }

        // "How far is the turn-in from here" — BFS hops over the harvested zone graph,
        // path in the tooltip (David, 2026-08-07: "3 zones away, zone 1 → zone 2 →
        // zone 3"). Multi-zone quests measure to the nearest listed start zone.
        var distance = "";
        string? route = null;
        if (_main.CurrentZoneName.Length > 0 && m.Quest.StartZone.Length > 0)
        {
            var best = m.Quest.StartZone.Split(',')
                .Select(z => _main.ZoneGraph.Distance(_main.CurrentZoneName, z.Trim()))
                .Where(d => d is not null)
                .OrderBy(d => d!.Value.Hops)
                .FirstOrDefault();
            if (best is { } b)
            {
                distance = b.Hops == 0 ? " · you're here" : $" · {b.Hops} zone{(b.Hops == 1 ? "" : "s")} away";
                route = b.Hops == 0 ? null : string.Join(" → ", b.Path);
            }
        }

        // Classes go LAST: they're the longest fragment and the only one that can
        // afford to vanish into the ellipsis — "done ×2" never should.
        var meta = string.Join(" · ", new[]
        {
            m.Quest.StartZone,
            m.Quest.QuestGiver.Length > 0 ? $"from {m.Quest.QuestGiver}" : "",
            m.Quest.MinLevel > 0 ? $"lvl {m.Quest.MinLevel}+" : "",
            m.Quest.Repeatable ? "repeatable" : "",
            completedCount > 0
                ? m.Quest.Repeatable ? $"done ×{completedCount}" : "✓ done"
                : "",
        }.Where(s => s.Length > 0));
        if (meta.Length > 0 || distance.Length > 0)
        {
            var full = meta + distance
                + (m.Quest.Classes.Length > 0 ? $" · {m.Quest.Classes}" : "");
            var metaText = new TextBlock
            {
                Text = full, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 2),
                ToolTip = route is { Length: > 0 } ? $"{route}\n{full}" : full,
            };
            metaText.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            body.Children.Add(metaText);
        }

        foreach (var item in m.Items)
            body.Children.Add(ItemRow(item));
        if (m.ItemsTotal == 0)
        {
            // The item parser found no turn-ins: a dialogue/kill/exploration chain.
            var dialogue = new TextBlock
            {
                Text = "Dialogue or task chain — steps on the wiki page.",
                FontSize = 11, FontStyle = FontStyles.Italic, Margin = new Thickness(8, 0.5, 0, 0.5),
            };
            dialogue.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            body.Children.Add(dialogue);
        }

        var card = new Border
        {
            Child = body, CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 7, 10, 8), Margin = new Thickness(0, 0, 0, 6),
            BorderThickness = new Thickness(1),
            Opacity = isHidden ? 0.55 : 1.0,
        };
        card.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
        // Hairline chrome; a READY card keeps the louder green edge — state has a shape.
        card.SetResourceReference(Border.BorderBrushProperty, m.Complete ? "GoodBrush" : "HairlineBrush");
        return card;
    }

    /// <summary>One reward item: hover fetches its eqlwiki stats on the spot (the
    /// tooltip live-updates from "Looking up…", same as the Loot breakout rows), click
    /// opens the wiki page.</summary>
    private TextBlock RewardLink(string name)
    {
        var cached = _main.CachedItemStats(name);
        var link = new TextBlock
        {
            Text = name, FontSize = 10.5, Margin = new Thickness(0, 0, 10, 1),
            Cursor = Cursors.Hand,
        };
        link.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");

        var tipText = new TextBlock
        {
            Text = cached ?? "Looking up on eqlwiki…",
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        };
        var tip = new ToolTip { Content = tipText };
        link.ToolTip = tip;
        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            var text = await _main.FetchItemTooltip(name);
            tipText.Text = text ?? (cached ?? "Not on the wiki.");
        };

        link.MouseLeftButtonDown += (_, e) => e.Handled = true;
        link.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            MainWindow.OpenWikiPage(name);
        };
        return link;
    }

    private const string ItemRowHint =
        "Left-click: +1 (you have one more) · Right-click: clear your count (after a hand-in)";

    private TextBlock ItemRow(QuestItemProgress item)
    {
        var met = item.Have >= item.Need;
        var row = new TextBlock
        {
            Text = $"{(met ? "✔" : "•")} {item.Name} — {item.Have}/{item.Need}",
            FontSize = 11.5, Margin = new Thickness(8, 0.5, 0, 0.5),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Cursor = Cursors.Hand,
        };
        // Same live wiki-stats hover the Loot window has (David, 2026-08-07), with the
        // count-adjust hint riding underneath.
        var cached = _main.CachedItemStats(item.Name);
        var tipText = new TextBlock
        {
            Text = (cached ?? "Looking up on eqlwiki…") + "\n\n" + ItemRowHint,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        };
        var tip = new ToolTip { Content = tipText };
        row.ToolTip = tip;
        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            var text = await _main.FetchItemTooltip(item.Name);
            tipText.Text = (text ?? cached ?? "Not on the wiki.") + "\n\n" + ItemRowHint;
        };
        row.SetResourceReference(TextBlock.ForegroundProperty,
            met ? "GoodBrush" : item.Have > 0 ? "TextBrush" : "DimBrush");
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            AdjustManual(item.Name, +1);
        };
        row.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            ClearCount(item.Name);
        };
        return row;
    }

    private void AdjustManual(string item, int delta)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        ledger.For(key).TryGetValue(item, out var entry);
        ledger.SetManual(key, item, (entry?.Manual ?? 0) + delta);
        Refresh(force: true);
    }

    /// <summary>A hand-in happened: zero the whole count for this item. The looted count
    /// is history we can't re-earn, so it becomes a negative manual offset instead —
    /// net zero now, and future loot counts up from there.</summary>
    private void ClearCount(string item)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        ledger.For(key).TryGetValue(item, out var entry);
        if (entry is null) return;
        ledger.SetManual(key, item, -entry.Looted);
        Refresh(force: true);
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    // ---- search + inventory scan ----

    /// <summary>The one way in. "+ I have this" used to sit above this box with a
    /// button and a quantity field, and won the eye by sheer weight — David missed the
    /// search entirely because of it (2026-08-15). Declaring owned items by hand is also
    /// the worse half of that trade: /outputfile inventory reads bags AND bank exactly,
    /// with no spelling to get right, and <see cref="OnCopyInventoryCmd"/> hands over
    /// the command.</summary>
    /// <summary>How many cards a single view will build. Each card lays out its item
    /// rows and wires wiki tooltips, and "all" offers the whole 1,172-quest catalog —
    /// rendering that is seconds of UI thread, per keystroke.</summary>
    private const int RenderCap = 60;
    private int _rendered;
    private int _suppressed;

    /// <summary>Rebuild after typing STOPS, not on every character. WPF raises
    /// TextChanged per keystroke and the rebuild is synchronous, so "Wakizashi" used to
    /// mean nine full catalog rebuilds — the window appeared to hang (David, 2026-08-15).
    /// A quarter-second is below the threshold where a search feels laggy and above the
    /// gap between keystrokes for any normal typing speed.</summary>
    private static readonly TimeSpan SearchSettle = TimeSpan.FromMilliseconds(120);
    private DispatcherTimer? _searchDebounce;

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        // The hint is pure paint — never make it wait on the debounce.
        FilterHint.Visibility = FilterBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDebounce ??= Build();
        // Restarting is what makes it a debounce rather than a throttle: only a pause
        // in typing fires it.
        _searchDebounce.Stop();
        _searchDebounce.Start();

        DispatcherTimer Build()
        {
            var t = new DispatcherTimer { Interval = SearchSettle };
            t.Tick += (_, _) => { t.Stop(); Refresh(force: true); };
            return t;
        }
    }

    /// <summary>Same ⧉ contract as every other place EQBuddy reads a game command's
    /// output: we never type in your client, so the most we can do is put the exact
    /// command on your clipboard. Flashes ✓ so a silent clipboard write isn't a
    /// silent no-op.</summary>
    private void OnCopyInventoryCmd(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(GameCommands.OutputfileInventory); }
        catch (Exception ex) { CoreLog.Error(ex); return; }
        CopyInvBtn.Content = "✓ copied";
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        t.Tick += (_, _) => { CopyInvBtn.Content = "⧉ scan bags"; t.Stop(); };
        t.Start();
    }
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
