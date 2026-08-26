using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy;

/// <summary>
/// The standalone Quest Tracker (QUEST-*, David's spec 2026-08-07): every wiki quest
/// whose turn-in items overlap what this character owns — looted since the ledger began,
/// or read from the game's own /outputfile inventory dump (bags and bank). The quest name
/// opens the eqlwiki walkthrough; "all" flips from the overlap view to the whole catalog
/// for browsing ahead.
///
/// One search box is the way in (David, 2026-08-15) — it matches rewards, turn-in items,
/// quest names, givers and zones, so "I want the Wakizashi of the Frozen Skies" is a
/// first-class question.
///
/// GATE 2 of the UI/UX rework (docs/DesignSystem.md) rebuilt the presentation and NOTHING
/// else: same filters, same modes, same ledger calls, same undo, same search. What changed
/// is that a column of self-contained cards became a LIST plus a DETAIL PANE. Every card
/// carried its own rewards, meta line, item rows and five controls, so finding the one
/// quest that is ready meant reading fifty paragraphs. The list answers "which one" and
/// the pane answers "what about it", which is the order the question actually gets asked
/// in — and it is what made room for the status badge and the state rule that now carry
/// readiness at a glance.
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
        WindowZoom.AllowResize(this, "quests", _settings);
        // A drag changes how much room the body has; without this the window grows
        // and its content does not follow.
        SizeChanged += (_, _) => UpdateHeightCaps();
        _tabs = new EqSegmentedStrip(TabStrip);
        _classes = new EqSegmentedStrip(ClassStrip);
        _modes = new EqSegmentedStrip(ModeStrip);
        BuildStaticChrome();
        EpicClassicOnlyCheck.IsChecked = _settings.EpicQuestClassicOnly;
        SkyIslandRepeatCheck.IsChecked = _settings.SkyStepsUnderEveryIsland;
        BuildClassChecks();
        EraCombo.Items.Add("Any era");
        foreach (var era in QuestEraLadder.Eras) EraCombo.Items.Add($"≤ {era}");
        var savedEra = Array.IndexOf(QuestEraLadder.Eras, _settings.QuestEraFilter);
        EraCombo.SelectedIndex = savedEra >= 0 ? savedEra + 1 : 0;
        // From Core, so the combo, the checklist filter and EQBuddy Mobile cannot end up
        // offering three different vocabularies for one lens.
        foreach (var s in QuestChecklistLayout.States) StateCombo.Items.Add(s);
        StateCombo.SelectedIndex = 0;
        foreach (var s in UnlockLayout.Sections) UnlockSectionCombo.Items.Add(s);
        UnlockSectionCombo.SelectedIndex = 0;
        BuildModeStrip();
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

    /// <summary>The chrome that never changes: the title row's icon, the search
    /// placeholder, and the labels on the two buttons whose content is an icon plus a
    /// word. Built in code because every one of them is a vector Path rather than the
    /// glyph it used to be — XAML can hold a Path, but not one whose geometry comes from
    /// a shared table.</summary>
    private void BuildStaticChrome()
    {
        TitleRow.Children.Add(DesignSystem.Icon("Quest", "AccentBrush", size: 15));
        _titleText = DesignSystem.Text(Role.TitleWindow, "Quest Tracker");
        _titleText.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        _titleText.Ink("AccentBrush");
        TitleRow.Children.Add(_titleText);
        CloseBtn.Content = DesignSystem.Icon("Close");

        FilterHint.Children.Add(DesignSystem.Icon("Search", "TextBrush", size: 13));
        var hint = DesignSystem.Text(Role.Body, "Search a reward, item, quest, or NPC…");
        hint.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        FilterHint.Children.Add(hint);

        CopyInvBtn.Content = IconLabel("Copy", "scan bags");
        UndoBtn.Content = IconLabel("Undo", "undo");
    }

    private TextBlock _titleText = null!;

    /// <summary>An icon and a word, on one baseline — the shape every textual button in
    /// the migrated surfaces takes.</summary>
    private static StackPanel IconLabel(string icon, string label, string colorKey = "DimBrush")
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(DesignSystem.Icon(icon, colorKey, size: 12));
        var text = DesignSystem.Text(Role.Caption, label);
        text.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        panel.Children.Add(text);
        return panel;
    }

    /// <summary>Height caps follow the monitor this window occupies, re-applied whenever
    /// it moves — a window dragged to a shorter screen must shrink to fit it (#186).</summary>
    private void UpdateHeightCaps()
    {
        var height = MonitorMetrics.WorkAreaFor(this) is { } work
            ? work.Height
            : SystemParameters.WorkArea.Height;   // before the handle exists
        MaxHeight = Math.Max(220, height * 0.85);
        // The BODY opens at a design constant, not at a fraction of the monitor. Deriving
        // it from the screen is what made this window fill a tall display; UI.Shared owns
        // the number so all seven pop-outs cannot disagree about it.
        // The list and the pane share the window's height, so cap the SCROLLERS rather
        // than the window: without this the window grows past its cap on a long catalog
        // and the footnotes walk off the bottom of the screen.
        BodyScroll.MaxHeight = WindowSizing.BodyCap(MaxHeight, 280, FramelessResize.ManualHeight(this));
        DetailScroll.MaxHeight = BodyScroll.MaxHeight;
    }

    /// <summary>Jump the window to one item's quests (the map badge in the Loot views):
    /// browse mode + the item as filter, so the quests appear even before any overlap
    /// and each carries its pin as the invitation to track.</summary>
    public void FilterToItem(string item)
    {
        _mode = "all";
        ApplyModeVisual();
        FilterBox.Text = item;
        Refresh(force: true);
        Activate();
    }

    /// <summary>Programmatic mode switch (screenshot hook + the map badge path).</summary>
    internal void SetMode(string mode)
    {
        _mode = mode is "zone" or "all" or "held" or "done" ? mode : "mine";
        ApplyModeVisual();
        Refresh(force: true);
    }

    /// <summary>Open straight onto a tab, optionally with the state lens already set —
    /// the screenshot hook's half of the tab strip (<c>EQBUDDY_QUESTS=sky</c>, or
    /// <c>sky:ready</c>). The checklist tabs have controls the General tab does not, and a
    /// review criterion that cannot reach them is not one: the lens restored for #205/#209
    /// is a control whose whole effect is on OTHER controls, so photographing it switched
    /// off proves only that it exists.</summary>
    internal void SetTab(string tab)
    {
        var state = "";
        if (tab.Split(':') is [var name, var wanted]) { tab = name; state = wanted; }
        // Core's own key table, not a ladder repeated here: it already maps every tab,
        // so a new one is openable by its wire key the day it exists. The ladder knew
        // only sky and epic, which is why the Unlocks tab could not be opened for review
        // at all — a surface nobody can put on screen reads as reviewed anyway (trap 22).
        _tab = QuestSurface.TabForKey(tab) ?? QuestTab.General;
        if (QuestChecklistLayout.States.Contains(state))
        {
            _state = state;
            StateCombo.SelectedItem = state;
        }
        // Anything else after the colon is a SEARCH. The item-grouped result (#108) only
        // exists while a query is live, so without this hook the layout that answers "who
        // wants this drop" could not be photographed at all — trap 22, the same reason
        // the Watch card's sort strip needed staging.
        else if (state.Length > 0) FilterBox.Text = state;
        ApplyTabVisual();
        Refresh(force: true);
    }

    // ---- top-level tabs: General · Epic 1.0 · Plane of Sky ----
    //
    // The tabs, the class lens and the mode strip are three features and ONE shape, so
    // they are three instances of one primitive (EqChip / EqSegmentedStrip, gate 2b).
    // Picking a tab and picking a mode look like the same kind of act because they are.

    private QuestTab _tab = QuestTab.General;
    /// <summary>Which single class the view is narrowed to, or null for all of yours.
    /// Session-scoped like the search box: a sticky lens reads as a broken tracker
    /// tomorrow when you have swapped classes.</summary>
    private string? _classLens;
    private EqSegmentedStrip _tabs = null!;
    private EqSegmentedStrip _classes = null!;
    private EqSegmentedStrip _modes = null!;

    /// <summary>Build the strip from Core's <see cref="QuestSurface"/> so the desktop and
    /// EQBuddy Mobile cannot disagree about which tabs exist, their order or their
    /// names — the whole reason that lives in Core.</summary>
    private void BuildTabs()
    {
        _tabs.Clear();
        foreach (var header in QuestSurface.Tabs(EpicCounts(), SkyCounts(), UnlockCounts()))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Badge, onClick: () =>
            {
                _tab = tab;
                ApplyTabVisual();
                Refresh(force: true);
            });
        }
        // Chips first, THEN the paint: ApplyTabVisual colours the chip list, so colouring
        // before rebuilding it left every freshly-built chip unstyled until the next
        // unrelated refresh — including the selected one, which is the whole signal.
        BuildClassStrip();
        ApplyTabVisual();
    }

    private void BuildModeStrip()
    {
        foreach (var (key, tip) in new[]
        {
            ("mine", "Quests matching your items and pins"),
            ("zone", "Everything you can work on in the zone you're in"),
            ("held", "Quests you could turn in with what your bags already hold — from the " +
                "game's /outputfile inventory dump. In game, type /outputfile inventory, " +
                "and this tab reads the file the game writes."),
            ("done", "Quests you've marked completed — every quest can be marked done, so " +
                "returning players can check off history"),
            ("all", "The whole quest catalog"),
        })
        {
            var mode = key;
            _modes.Add(key, key, tip: tip, onClick: () =>
            {
                _mode = mode;
                ApplyModeVisual();
                Refresh(force: true);
            });
        }
        ApplyModeVisual();
    }

    /// <summary>Any · one of your classes. The class picker still decides WHICH classes
    /// you have; this decides which of them you're looking at right now, which is a
    /// different question and wanted far more often.</summary>
    private void BuildClassStrip()
    {
        _classes.Clear();
        var key = _main.QuestCharacterKey;
        // The RESOLVED list, not the picks with one inferred class behind them: the dump
        // leads, the log fills in, picks widen (`CharacterClasses`). Reading
        // `InferredClass` here was the last place this window could see one class where
        // the character has three.
        var mine = _main.ClassSourceFor(_main.CurrentSnapshot()).Classes;
        // One class and no lens to offer: a strip reading "Any · BRD" chooses nothing.
        if (mine.Count < 2) { ClassStrip.Visibility = Visibility.Collapsed; return; }
        ClassStrip.Visibility = Visibility.Visible;

        Add(null, "Any");
        foreach (var cls in mine) Add(cls, QuestClassFilter.Abbrev(cls));

        void Add(string? cls, string text) =>
            _classes.Add(text, cls ?? "",
                tip: cls is null
                    ? "Every class you play"
                    : $"Show only {cls} — quests, Epic and Plane of Sky alike",
                onClick: () => { _classLens = cls; Refresh(force: true); });
    }

    // The counting RULE is Core's (QuestSurface.CountOf) — this window, the Avalonia one
    // and the phone each had their own hand-rolled copy of the same expression, and a
    // fourth copy is how #184 happened.
    private (int Done, int Total)? EpicCounts() =>
        QuestSurface.CountOf(_settings.EpicQuestChecklist, i => i.Acquired);

    private (int Done, int Total)? SkyCounts() =>
        QuestSurface.CountOf(_settings.SkyQuestChecklist, i => i.Acquired);

    private (int Done, int Total)? UnlockCounts() =>
        QuestSurface.UnlockCounts(_main.Unlocks.Races, _main.Unlocks.Classes);

    private void ApplyTabVisual()
    {
        _tabs.Select(_tab);
        // The class strip keys on "" for Any, because a null key would make "nothing
        // selected" and "Any selected" the same answer.
        _classes.Select(_classLens ?? "");
        // Era and the mode strip are catalog concepts — meaningless against a fixed
        // checklist. The CLASS picker is not: David, 2026-08-15, "we may be helping a
        // friend", so every tab must be able to reach a class you don't play.
        var catalogOnly = _tab == QuestTab.General ? Visibility.Visible : Visibility.Collapsed;
        EraCombo.Visibility = catalogOnly;
        ModeStrip.Visibility = catalogOnly;
        // STATE is not a catalog concept, and calling it one is what #205 (bjstrange) and
        // #209 (crydeevisions-arch) reported: a checklist is the surface where "ready" and
        // "done" mean the most, because the reward you can hand in RIGHT NOW is the only
        // thing on the page with anything to do about it. The widget's Sky card had this
        // lens and it did not come across when the card became a launcher.
        // The Epic tab's own lens, which followed the Epic card here when the widget
        // consolidated its quest cards (2026-08-16).
        EpicClassicOnlyCheck.Visibility = _tab == QuestTab.Epic ? Visibility.Visible : Visibility.Collapsed;
        SkyIslandRepeatCheck.Visibility = _tab == QuestTab.Sky ? Visibility.Visible : Visibility.Collapsed;
        // Unlocks is divided by SECTION, not by class, so the class picker is replaced by
        // a section lens; the state lens is not wired here and an inert filter is worse
        // than an absent one. EVERY CONTROL BELOW IS ASSIGNED EXACTLY ONCE — an earlier
        // cut hid ClassBtn in an `if` that a later unconditional assignment overwrote,
        // which only a screenshot could catch.
        var unlocks = _tab == QuestTab.Unlocks;
        UnlockSectionCombo.Visibility = unlocks ? Visibility.Visible : Visibility.Collapsed;
        StateCombo.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        ClassBtn.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        // "scan bags" copies /outputfile inventory, which is not what this tab reads.
        CopyInvBtn.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        ClassStrip.Visibility = !unlocks && _classes.Count > 1
            ? Visibility.Visible : Visibility.Collapsed;
        FilterRow.Visibility = Visibility.Visible;
        // A checklist has nothing to select, so the pane would only ever be empty. Give
        // its width back to the rows instead.
        var catalog = _tab == QuestTab.General;
        DetailCard.Visibility = catalog ? Visibility.Visible : Visibility.Collapsed;
        DetailColumn.Width = catalog ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        MasterColumn.Width = catalog ? new GridLength(400) : new GridLength(1, GridUnitType.Star);
    }

    private void ApplyModeVisual() => _modes.Select(_mode);

    /// <summary>A faction dump just landed. Nothing to import — UnlockSource re-reads it
    /// off disk — but an OPEN Unlocks tab should fill in now rather than on the next
    /// reopen, which is the difference between the command appearing to work and appearing
    /// to do nothing.</summary>
    internal void FactionsChanged()
    {
        if (_tab == QuestTab.Unlocks) Refresh(force: true);
    }

    /// <summary>Which section of the Unlocks tab is in view. Session-scoped, like the
    /// class lens and the search box: a sticky filter reads as a broken tracker tomorrow.</summary>
    private string _unlockSection = UnlockLayout.SectionAll;

    private void OnUnlockSectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UnlockSectionCombo.SelectedItem is string s) _unlockSection = s;
        Refresh(force: true);
    }

    // ---- multiclass filter (Legends: up to three active classes; David 2026-08-07) ----

    private readonly List<CheckBox> _classChecks = [];

    private void BuildClassChecks()
    {
        foreach (var cls in QuestClassFilter.Classes)
        {
            var check = new CheckBox { Margin = new Thickness(0, 1, 0, 1) };
            check.Content = DesignSystem.Text(Role.Body, cls);
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

    /// <summary>The Sky tab's island lens. Persisted for the same reason the Epic one is:
    /// EQBuddy Mobile's Sky tab reads the same setting, so the phone and the desktop group
    /// one checklist one way (#210's rule — a surface that shows the same list differently
    /// is the drift SurfaceParityTests exists to stop).</summary>
    private void OnSkyIslandRepeatToggled(object sender, RoutedEventArgs e)
    {
        _settings.SkyStepsUnderEveryIsland = SkyIslandRepeatCheck.IsChecked == true;
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
        _titleText.Text = character.Length > 0
            ? $"Quest Tracker — {char.ToUpper(character[0])}{character[1..]}"
            : "Quest Tracker";

        var owned = _main.QuestLedger?.For(key)
            ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
        var tracked = _main.QuestLedger?.TrackedFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hidden = _main.QuestLedger?.HiddenFor(key)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Folded with the Sky checklist, because a "<Class> Sky Test: <Reward>" row on
        // THIS tab and the reward row on the Sky tab are the same fact. The ledger never
        // knew about SkyQuestCompleted, so a reward the game's own achievements dump said
        // was handed in still sat here as live work.
        var completed = SkyTestSplit.WithTurnIns(
            _main.QuestLedger?.CompletedFor(key)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            _settings.SkyQuestCompleted);
        var filter = FilterBox.Text.Trim();
        var picks = _main.QuestLedger?.ClassesFor(key) ?? [];
        SyncClassChecks(picks);
        // Nothing PICKED? The character's classes still pre-filter — from the dump if it
        // has one, from the log otherwise — always labeled with where they came from,
        // never persisted, and one popup pick overrides (David, 2026-08-11: players swap
        // classes, so this is a reading, not a fact).
        var (resolved, classSource) = _main.ClassSourceFor(_main.CurrentSnapshot());
        var classes = picks.Count > 0 ? picks : resolved.ToList();
        // WHO the character is, shown whether or not classes are picked — Bevel,
        // Helm-signed 2026-08-23: "identity stays on screen after picks. It is not the
        // filter." Hiding it the moment they tick the picker hides the game's own answer
        // exactly when they are deciding what to look at.
        var identity = string.Join(" · ", resolved);
        // The lens narrows to ONE of the classes you play. Everything downstream reads
        // `classes`, so narrowing it here covers the catalog, the zone view and the
        // item-driven tabs at once. A stale lens (you dropped that class) is ignored
        // rather than emptying the window.
        if (_classLens is { } lens && classes.Contains(lens, StringComparer.OrdinalIgnoreCase))
            classes = [lens];
        else if (_classLens is not null && !classes.Contains(_classLens, StringComparer.OrdinalIgnoreCase))
            _classLens = null;

        var sig = $"{key}|{filter}|{_mode}|st:{_state}|{string.Join("+", classes)}|id:{identity}|{_settings.QuestEraFilter}|{_main.CurrentZoneName}" +
            $"|sel:{_selected}" +
            $"|{string.Join(";", tracked.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", hidden.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", completed.Select(kv => $"{kv.Key}:{kv.Value}"))}" +
            $"|{string.Join(",", owned.Select(kv => $"{kv.Key}:{kv.Value.Total}"))}";
        if (!force && sig == _signature) return;
        _signature = sig;

        QuestsPanel.Children.Clear();
        _rows.Clear();
        _renderedCount = 0;
        _suppressed = 0;
        SummaryRow.Visibility = Visibility.Collapsed;
        BuildTabs();
        if (_tab == QuestTab.Unlocks)
        {
            DetailPane.Children.Clear();
            RenderUnlocks();
            return;
        }
        if (_tab != QuestTab.General)
        {
            DetailPane.Children.Clear();
            RenderChecklist(_tab, filter, classes);
            return;
        }
        if (identity.Length > 0)
        {
            // No verb. "pick classes above to override" told the player to override their
            // own character; the picker is a LENS over identity (#104), not a replacement.
            var note = Note($"{identity} ({CharacterClasses.SourceLabel(classSource)})", "Info");
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
        // a row is far cheaper than the card it replaces — no reward links, no item rows,
        // no wiki tooltips until a row is SELECTED — but "all" still hands this the whole
        // 1,172-quest catalog, and doing that per keystroke is what froze the window
        // (David, 2026-08-15: "typing in the search box is extremely slow").
        void AddRow(QuestMatch m)
        {
            if (_renderedCount >= RenderCap) { _suppressed++; return; }
            _renderedCount++;
            var entry = new RowEntry(m, hidden.Contains(m.Quest.Name),
                completed.GetValueOrDefault(m.Quest.Name));
            entry.Element = Row(entry);
            _rows.Add(entry);
            QuestsPanel.Children.Add(entry.Element);
        }
        void EmptyNote(string text) => QuestsPanel.Children.Add(EmptyState(text));

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
            // wasn't). Each row states its own class and era; the reader decides.
            var found = QuestSearch.Find(_main.QuestCatalog, filter)
                .Select(Progressed)
                .OrderByDescending(m => m.Tracked)
                .ThenByDescending(m => m.Fraction)
                .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            QuestsPanel.Children.Add(Note(
                $"{found.Count} match{(found.Count == 1 ? "" : "es")} in the whole catalog — names, " +
                "turn-in items, rewards, NPCs, zones. Search ignores your class/era/state filters.",
                "Search"));
            foreach (var m in found) AddRow(m);
            if (found.Count == 0)
                EmptyNote("Nothing matches. Searches cover quest names, turn-in items, " +
                          "rewards, quest givers, and zones — try fewer words.");
            FinishRender();
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
                    AddRow(m);
                break;

            case "zone" when _main.CurrentZoneName.Length == 0:
                EmptyNote("No zone seen in the log yet — zone view fills in once " +
                          "you've zoned somewhere.");
                break;

            case "zone":
            {
                // Everything workable where you stand — including dialogue chains the
                // item parser found nothing for (David: "not everything is item driven").
                QuestsPanel.Children.Add(Note(_main.CurrentZoneName, "Location", "WarnBrush"));
                var zoneQuests = _main.QuestCatalog.Quests
                    .Where(q => q.TouchesZone(_main.CurrentZoneName)
                                && MatchesFilter(q, filter) && ClassOk(q))
                    .Select(Progressed)
                    .Where(StateOk)
                    .OrderByDescending(m => m.Tracked)
                    .ThenByDescending(m => m.Fraction)
                    .ThenBy(m => m.Quest.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var m in zoneQuests) AddRow(m);
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
                        Style = (Style)FindResource("ActionButton"),
                        FontSize = DesignTokens.Spec(Role.Caption).Size,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceS),
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
                QuestsPanel.Children.Add(Note(
                    $"{System.IO.Path.GetFileName(snap.Path)} — written " +
                    (invAge.TotalMinutes < 1 ? "just now" : invAge.TotalHours < 1
                        ? $"{(int)invAge.TotalMinutes}m ago" : $"{(int)invAge.TotalHours}h ago") +
                    " (plus everything looted since)", "Bag", "WarnBrush"));
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
                void Section(string text) => QuestsPanel.Children.Add(
                    new TextBlock { Text = text, Style = (Style)FindResource("SectionLabel") });
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
                    foreach (var m in ready) AddRow(m);
                }
                if (partial.Count > 0)
                {
                    Section($"Your bags contribute ({partial.Count})");
                    foreach (var m in partial) AddRow(m);
                }
                if (others.Count > 0)
                {
                    Section($"For other classes — you hold pieces anyway ({others.Count})");
                    foreach (var m in others) AddRow(m);
                }
                if (overlapping.Count == 0)
                    EmptyNote("Nothing in your bags matches a catalogued quest's turn-ins yet.");
                break;
            }

            case "done":
            {
                // The trophy shelf — and the catch-up surface: every quest can be marked
                // done, so returning players can mark history without touching items.
                var done = completed.Where(kv => kv.Value > 0)
                    .Select(kv => (_main.QuestCatalog.Quests.FirstOrDefault(q =>
                        q.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)), kv.Value))
                    .Where(x => x.Item1 is not null && ClassOk(x.Item1!))
                    .OrderBy(x => x.Item1!.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var (q, _) in done) AddRow(Progressed(q!));
                if (done.Count == 0)
                    EmptyNote("Nothing marked completed yet. Select a quest and use \"mark as " +
                              "done\" on quests you finished before EQBuddy, and the tracker " +
                              "catches up (ready quests count themselves when you hand them in).");
                break;
            }

            default:
            {
                // "mine": item overlap + pins, minus dismissed and finished-for-good
                // (completed non-repeatables stay visible in zone/all with their mark).
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
                foreach (var m in shown) AddRow(m);
                if (othersMine.Count > 0)
                {
                    QuestsPanel.Children.Add(new TextBlock
                    {
                        Text = $"For other classes — from your items ({othersMine.Count})",
                        Style = (Style)FindResource("SectionLabel"),
                    });
                    foreach (var m in othersMine) AddRow(m);
                }
                if (shown.Count == 0 && othersMine.Count == 0)
                    EmptyNote(matches.Count == 0
                        ? "Nothing yet — loot a quest item (they show green in the Loot list), " +
                          "or scan bags to read what you already carry. Search a reward you want " +
                          "by name, or try \"zone\" and \"all\" to browse."
                        : "No quest matches that search — try a reward name, an item, or an NPC.");
                break;
            }
        }

        FinishRender();
    }

    /// <summary>The two things that can only be said once every row is in: how many of
    /// them are ready, and how many were capped away. Never a silent cap (CLAUDE.md).</summary>
    private void FinishRender()
    {
        if (_suppressed > 0)
            QuestsPanel.Children.Add(EmptyState(
                $"+{_suppressed} more — showing the first {RenderCap}. Keep typing to narrow it down."));

        var ready = _rows.Count(r => Badge(r).State == QuestPresentation.State.Ready);
        if (QuestPresentation.ReadySummary(ready) is { } summary)
        {
            SummaryRow.Children.Clear();
            SummaryRow.Children.Add(DesignSystem.Icon("Check", "GoodBrush", size: 13));
            var text = DesignSystem.Text(Role.BodySecondary, summary);
            text.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
            text.Ink("GoodBrush");
            SummaryRow.Children.Add(text);
            SummaryRow.Visibility = Visibility.Visible;
        }

        // Keep the selection if it survived the rebuild; otherwise fall to the first row,
        // so the pane is never blank beside a full list.
        Select(_rows.FirstOrDefault(r => r.Match.Quest.Name.Equals(_selected, StringComparison.OrdinalIgnoreCase))
               ?? _rows.FirstOrDefault());
    }

    /// <summary>Facts about this window for the <c>EQBUDDY_EXPAND</c> dump, asserted from
    /// tests/EQBuddy.E2E. The WPF layer has no unit tests (docs/TestPlan.md §5), so a
    /// launched app reporting its own structure is the only cover the Gate 2 rebuild can
    /// have — and the rebuild's whole claim is structural: a LIST of rows, one of them
    /// SELECTED, and a detail pane that is not empty beside it.</summary>
    internal string DebugFacts() =>
        $"questsRows={_rows.Count} " +
        $"questsSuppressed={_suppressed} " +
        $"questsSelected={(_selected.Length > 0 ? 1 : 0)} " +
        $"questsDetailBlocks={DetailPane.Children.Count} " +
        $"questsDetailShown={(DetailCard.Visibility == Visibility.Visible ? 1 : 0)} " +
        $"questsReadySummary={(SummaryRow.Visibility == Visibility.Visible ? 1 : 0)} " +
        $"questsTabs={_tabs.Count} " +
        $"questsModes={_modes.Count} " +
        // The Sky tab's ⧉ copy of /outputfile achievements. Counted off the real visual
        // tree rather than from a flag, for the same reason gearCopyCmd exists: an absent
        // control photographs as an unremarkable panel (trap 29), and a bool that nobody
        // resets goes stale without anything noticing.
        $"questsSkyCopyCmd={SkyCopyCommandsOnScreen()}";

    private int SkyCopyCommandsOnScreen() => QuestsPanel.Children.OfType<StackPanel>()
        .SelectMany(p => p.Children.OfType<Button>())
        .Count(b => b.Content is string s
            && s.Contains(GameCommands.OutputfileAchievements, StringComparison.Ordinal));

    // ---- the list ----

    /// <summary>One rendered row and everything the detail pane needs to redraw it
    /// without going back to the ledger.</summary>
    private sealed class RowEntry(QuestMatch match, bool hidden, int completedCount)
    {
        public QuestMatch Match { get; } = match;
        public bool Hidden { get; } = hidden;
        public int CompletedCount { get; } = completedCount;
        public Border Element { get; set; } = null!;
    }

    private readonly List<RowEntry> _rows = [];
    private string _selected = "";

    private static QuestPresentation.Badge Badge(RowEntry entry) =>
        QuestPresentation.BadgeFor(entry.Match, entry.CompletedCount);

    /// <summary>A compact list row: state rule · name · badge · one meta line. That is
    /// all — the rewards, the turn-in items and the five controls moved to the pane.
    /// Fifty of these can be scanned; fifty of the cards they replace could only be
    /// read.</summary>
    private Border Row(RowEntry entry)
    {
        var m = entry.Match;
        var badge = Badge(entry);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The state rule. One fact, two encodings — the rule makes the list scannable
        // without reading, the badge makes it unambiguous when read. "Open" gets no rule
        // at all: a list where every row is highlighted highlights nothing.
        var rule = new Border
        {
            Width = DesignTokens.StateRuleWidth,
            CornerRadius = new CornerRadius(DesignTokens.StateRuleWidth / 2),
            Margin = new Thickness(0, 0, DesignTokens.SpaceM, 0),
        };
        if (QuestPresentation.RuleColorKey(badge.State) is { } ruleKey)
        {
            rule.SetResourceReference(BackgroundProperty, ruleKey);
            rule.Opacity = QuestPresentation.RuleOpacity(badge.State);
        }
        Grid.SetColumn(rule, 0);
        grid.Children.Add(rule);

        var stack = new StackPanel();
        var name = DesignSystem.Text(Role.TitleSection, m.Quest.Name);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        if (m.Tracked) name.Ink("AccentBrush");
        stack.Children.Add(name);

        var meta = DesignSystem.Text(Role.Caption,
            QuestPresentation.MetaLine(m.Quest, entry.CompletedCount, Distance(m.Quest).Text));
        meta.TextTrimming = TextTrimming.CharacterEllipsis;
        if (meta.Text.Length > 0) stack.Children.Add(meta);
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0),
        };
        if (m.Tracked)
            right.Children.Add(DesignSystem.Icon("PinFilled", "AccentBrush", size: 11));
        var badgeText = DesignSystem.Text(Role.Caption, badge.Label);
        badgeText.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        badgeText.FontWeight = FontWeights.SemiBold;
        badgeText.Ink(badge.ColorKey);
        right.Children.Add(badgeText);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var row = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Opacity = entry.Hidden ? 0.55 : 1.0,
            ToolTip = entry.Hidden ? "Hidden — select it and use \"show again\" to bring it back" : null,
        };
        row.MouseLeftButtonDown += (_, e) => { e.Handled = true; Select(entry); };
        // Double-click still opens the wiki walkthrough, which is what clicking the name
        // used to do. Kept because it is muscle memory and costs nothing.
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.ClickCount >= 2) { e.Handled = true; OpenUrl(m.Quest.Url); }
        };
        return row;
    }

    /// <summary>Selected is a SURFACE change (raised, hairline turns accent), never a
    /// re-render: the pane is what changes, and repainting fifty rows to move a
    /// highlight is how a list starts to feel slow.</summary>
    private void Select(RowEntry? entry)
    {
        _selected = entry?.Match.Quest.Name ?? "";
        foreach (var row in _rows)
        {
            var on = ReferenceEquals(row, entry);
            row.Element.SetResourceReference(BackgroundProperty, on ? "RaisedBrush" : "PanelBrush");
            row.Element.SetResourceReference(BorderBrushProperty, on ? "AccentBrush" : "HairlineBrush");
        }
        BuildDetail(entry);
    }

    // ---- the detail pane ----

    private void BuildDetail(RowEntry? entry)
    {
        DetailPane.Children.Clear();
        if (entry is null)
        {
            DetailPane.Children.Add(EmptyState("Select a quest to see its rewards, turn-ins and where to go."));
            return;
        }

        var m = entry.Match;
        var badge = Badge(entry);

        // Title + the controls that act on this quest. They used to be five click-handled
        // TextBlocks on every card in the list — 300 of them on a full catalog view, none
        // of them keyboard-reachable, and all of them competing with the data.
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = DesignSystem.Text(Role.TitleWindow, m.Quest.Name);
        title.TextWrapping = TextWrapping.Wrap;
        title.Cursor = Cursors.Hand;
        title.ToolTip = "Open the wiki walkthrough";
        title.Ink("AccentBrush");
        title.MouseLeftButtonUp += (_, e) => { e.Handled = true; OpenUrl(m.Quest.Url); };
        head.Children.Add(title);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top,
        };
        // Pin = "keep this quest in front of me": tracked quests sort first and stay
        // visible even with zero items (David, 2026-08-07: "players can choose to track
        // quests or not, easily").
        actions.Children.Add(DesignSystem.IconButton(
            m.Tracked ? "PinFilled" : "Pin",
            m.Tracked ? "Stop tracking this quest" : "Track this quest",
            (_, _) => WithLedger(l => l.SetTracked(_main.QuestCharacterKey, m.Quest.Name, !m.Tracked)),
            m.Tracked ? "AccentBrush" : "DimBrush", m.Tracked ? 1.0 : 0.55));
        // Check = "I did this before EQBuddy" (David, 2026-08-11): catch-up marking,
        // consuming nothing — the turn-in button below is for hand-ins happening now.
        actions.Children.Add(DesignSystem.IconButton("Check",
            entry.CompletedCount > 0
                ? $"Completed ×{entry.CompletedCount} — click to unmark"
                : "Did this before EQBuddy? Mark it completed (consumes nothing; click again to undo)",
            (_, _) => ToggleCompleted(m.Quest.Name, entry.CompletedCount == 0),
            entry.CompletedCount > 0 ? "GoodBrush" : "DimBrush",
            entry.CompletedCount > 0 ? 1.0 : 0.55));
        // Close = "not interested": drops the quest from the overlap view AND un-greens
        // loot only it wants (David, 2026-08-07: "there are definitely some I don't want
        // to track"). Hidden quests reappear dimmed under "all", where this is the way back.
        actions.Children.Add(DesignSystem.IconButton("Close",
            entry.Hidden
                ? "Show this quest again"
                : "Not interested — hide this quest (its items stop showing green unless another quest wants them)",
            (_, _) => WithLedger(l => l.SetHidden(_main.QuestCharacterKey, m.Quest.Name, !entry.Hidden)),
            "DimBrush", entry.Hidden ? 1.0 : 0.55));
        // Flag = "this data is wrong" (David, 2026-08-11: one wrong quest drops faith in
        // everything). One click opens a prefilled report — the catalog's accuracy loop
        // runs on these, same as every parser fix ran on pasted log lines.
        actions.Children.Add(DesignSystem.IconButton("Flag",
            "Something wrong with this quest's data (items, giver, zone)? " +
            "Open a prefilled report — fixes usually ship the same day.",
            (_, _) => OpenUrl(ReportUrl(m)), "DimBrush", 0.55));
        Grid.SetColumn(actions, 1);
        head.Children.Add(actions);
        DetailPane.Children.Add(head);

        // The status line, said in words directly under the title — the badge in the list
        // is a glance, this is the answer.
        var status = IconLine(badge.State switch
        {
            QuestPresentation.State.Ready => "ready to turn in",
            QuestPresentation.State.Done => $"completed ×{entry.CompletedCount}",
            QuestPresentation.State.InProgress => $"{m.ItemsHave} of {m.ItemsTotal} turn-ins started",
            QuestPresentation.State.Steps => "dialogue or task chain — steps on the wiki page",
            QuestPresentation.State.Collection =>
                "this wiki page documents several quests at once, so per-page progress would mislead",
            _ => "nothing held yet",
        }, badge.State switch
        {
            QuestPresentation.State.Ready => "Check",
            QuestPresentation.State.Done => "Check",
            QuestPresentation.State.Collection => "Book",
            QuestPresentation.State.Steps => "Info",
            _ => "Quest",
        }, badge.ColorKey, Role.Body);
        status.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceM);
        DetailPane.Children.Add(status);

        if (m.Quest.Rewards.Count > 0) DetailPane.Children.Add(Rewards(m));
        if (m.Items.Count > 0) DetailPane.Children.Add(Objectives(m));
        DetailPane.Children.Add(Details(m, entry.CompletedCount));

        // THE primary action, and the only one on the surface: "I handed it in". It was
        // previously the progress COUNT doubling as a button, which is not an affordance
        // anyone finds — the tooltip was the only thing that said so.
        if (m.Complete || m.ItemsTotal == 0)
        {
            var handIn = new Button
            {
                Style = (Style)FindResource("EqPrimaryButton"),
                Content = IconLabel("Check",
                    m.ItemsTotal == 0 ? "Mark as done" : "Mark as turned in", "GoodBrush"),
                Margin = new Thickness(0, DesignTokens.SpaceL, 0, 0),
                ToolTip = m.ItemsTotal == 0
                    ? "Click when you finish this quest to mark it done"
                    : "Click when you hand it in — consumes one set of turn-in items and counts a completion",
            };
            handIn.Click += (_, _) => WithLedger(l =>
                l.RecordCompletion(_main.QuestCharacterKey, m.Quest.Name, m.Quest.Items));
            DetailPane.Children.Add(handIn);
        }
    }

    /// <summary>The payoff, right under the status (David, 2026-08-07: "Crude Stein Quest
    /// should show the Crude Stein item"), with the same hover/click as loot: hover pulls
    /// the item's wiki stats live, click opens its page.
    ///
    /// The silhouette beside each name comes from the item's OWN catalog record (slots and
    /// weapon skill). The mockup drew a bespoke icon per item; nothing in EQBuddy can map
    /// an item to one — the 2026-08-15 spike established that the game ships the icon
    /// sheets and nothing indexes them — so this draws what the data supports and nothing
    /// more (docs/DesignSystem.md §8a).</summary>
    private UIElement Rewards(QuestMatch m)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        panel.Children.Add(new TextBlock
        {
            Text = "Rewards", Style = (Style)FindResource("SectionLabel"),
        });
        var wrap = new WrapPanel();
        const int shown = 8;
        foreach (var reward in m.Quest.Rewards.Take(shown)) wrap.Children.Add(RewardTile(reward));
        if (m.Quest.Rewards.Count > shown)
        {
            var more = DesignSystem.Text(Role.Caption, $"+{m.Quest.Rewards.Count - shown} more");
            more.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXs, 0, 0);
            more.ToolTip = string.Join("\n", m.Quest.Rewards.Skip(shown));
            wrap.Children.Add(more);
        }
        panel.Children.Add(wrap);
        return panel;
    }

    private Border RewardTile(string name)
    {
        var record = ItemCatalog.Default.Find(name);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(DesignSystem.Icon(
            IconPaths.ForItem(record?.Slots, record?.Skill), "DimBrush", size: 14));
        var label = DesignSystem.Text(Role.BodySecondary, name);
        label.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Ink("AccentBrush");
        content.Children.Add(label);

        var tile = new Border
        {
            Child = content,
            Background = null,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs,
                DesignTokens.SpaceM, DesignTokens.SpaceXxs),
            Margin = new Thickness(0, 0, DesignTokens.SpaceXs, DesignTokens.SpaceXs),
            Cursor = Cursors.Hand,
            MaxWidth = 220,
        };
        tile.SetResourceReference(BackgroundProperty, "RaisedBrush");
        AttachItemTooltip(tile, name, null);
        tile.MouseLeftButtonDown += (_, e) => e.Handled = true;
        tile.MouseLeftButtonUp += (_, e) => { e.Handled = true; MainWindow.OpenWikiPage(name); };
        return tile;
    }

    private const string ItemRowHint =
        "Left-click: +1 (you have one more) · Right-click: clear your count (after a hand-in)";

    private UIElement Objectives(QuestMatch m)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        panel.Children.Add(new TextBlock
        {
            Text = "Turn-ins", Style = (Style)FindResource("SectionLabel"),
        });
        foreach (var item in m.Items) panel.Children.Add(ItemRow(item));
        return panel;
    }

    private Border ItemRow(QuestItemProgress item)
    {
        var met = item.Have >= item.Need;
        var record = ItemCatalog.Default.Find(item.Name);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = DesignSystem.Icon(IconPaths.ForItem(record?.Slots, record?.Skill),
            met ? "GoodBrush" : "DimBrush", size: 14);
        icon.Margin = new Thickness(0, 0, DesignTokens.SpaceM, 0);
        grid.Children.Add(icon);

        var name = DesignSystem.Text(Role.Body, item.Name);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.Ink(met ? "GoodBrush" : item.Have > 0 ? "TextBrush" : "DimBrush");
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var count = DesignSystem.Text(Role.Body, $"{item.Have} / {item.Need}");
        count.FontWeight = FontWeights.SemiBold;
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(DesignTokens.SpaceM, 0, 0, 0);
        count.Ink(met ? "GoodBrush" : item.Have > 0 ? "AccentBrush" : "DimBrush");
        Grid.SetColumn(count, 2);
        grid.Children.Add(count);

        var row = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs,
                DesignTokens.SpaceM, DesignTokens.SpaceXs),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXxs),
            Cursor = Cursors.Hand,
        };
        row.SetResourceReference(BackgroundProperty, "RaisedBrush");
        // Same live wiki-stats hover the Loot window has (David, 2026-08-07), with the
        // count-adjust hint riding underneath.
        AttachItemTooltip(row, item.Name, ItemRowHint);
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; AdjustManual(item.Name, +1); };
        row.MouseRightButtonUp += (_, e) => { e.Handled = true; ClearCount(item.Name); };
        return row;
    }

    /// <summary>The live wiki-stats tooltip, wired the same way for reward tiles and
    /// turn-in rows: it opens saying "Looking up…", then updates in place. One helper so
    /// the two cannot drift into fetching differently.</summary>
    private void AttachItemTooltip(FrameworkElement element, string itemName, string? footer)
    {
        var cached = _main.CachedItemStats(itemName);
        var suffix = footer is null ? "" : "\n\n" + footer;
        var tipText = new TextBlock
        {
            Text = (cached ?? "Looking up on eqlwiki…") + suffix,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        };
        var tip = new ToolTip { Content = tipText };
        element.ToolTip = tip;
        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            if (fetched) return;
            fetched = true;
            var text = await _main.FetchItemTooltip(itemName);
            tipText.Text = (text ?? cached ?? "Not on the wiki.") + suffix;
        };
    }

    /// <summary>Zone · giver · level · distance · class as labelled CELLS. On the card
    /// this replaces they were one ellipsized run of "·"-joined fragments, so on a narrow
    /// window the class quietly vanished and nothing said it had.</summary>
    private UIElement Details(QuestMatch m, int completedCount)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Details", Style = (Style)FindResource("SectionLabel"),
        });
        var wrap = new WrapPanel();
        var (distance, route) = Distance(m.Quest);
        foreach (var (label, value, tip) in new[]
        {
            ("Zone", m.Quest.StartZone, (string?)null),
            ("Giver", m.Quest.QuestGiver, null),
            ("Level", m.Quest.MinLevel > 0 ? $"{m.Quest.MinLevel}+" : "", null),
            ("Distance", distance, route),
            ("Class", m.Quest.Classes, null),
            ("Completed", completedCount > 0 ? $"×{completedCount}" : "", null),
            ("Repeatable", m.Quest.Repeatable ? "yes" : "", null),
        })
        {
            if (value.Length == 0) continue;
            var cell = new StackPanel();
            cell.Children.Add(DesignSystem.Text(Role.Metadata, label));
            var body = DesignSystem.Text(Role.BodySecondary, value);
            body.Ink("TextBrush");
            body.TextWrapping = TextWrapping.Wrap;
            body.MaxWidth = 150;
            cell.Children.Add(body);
            var border = new Border { Child = cell, Style = (Style)FindResource("EqDetailCell") };
            if (tip is { Length: > 0 }) border.ToolTip = tip;
            wrap.Children.Add(border);
        }
        panel.Children.Add(wrap);
        return panel;
    }

    /// <summary>"How far is the turn-in from here" — BFS hops over the harvested zone
    /// graph, path in the tooltip (David, 2026-08-07: "3 zones away, zone 1 → zone 2 →
    /// zone 3"). Multi-zone quests measure to the nearest listed start zone.</summary>
    private (string Text, string? Route) Distance(QuestEntry quest)
    {
        if (_main.CurrentZoneName.Length == 0 || quest.StartZone.Length == 0) return ("", null);
        var best = quest.StartZone.Split(',')
            .Select(z => _main.ZoneGraph.Distance(_main.CurrentZoneName, z.Trim()))
            .Where(d => d is not null)
            .OrderBy(d => d!.Value.Hops)
            .FirstOrDefault();
        return best is { } b
            ? (QuestPresentation.DistanceText(b.Hops), b.Hops == 0 ? null : string.Join(" → ", b.Path))
            : ("", null);
    }

    private static string ReportUrl(QuestMatch m)
    {
        var body =
            $"Quest: {m.Quest.Name}\nWiki page: {m.Quest.Url}\n" +
            $"EQBuddy shows: {m.ItemsTotal} turn-in item(s) — {string.Join(", ", m.Quest.Items.Select(i => i.Qty > 1 ? $"{i.Name} x{i.Qty}" : i.Name))}\n" +
            $"Giver: {m.Quest.QuestGiver} · Zone: {m.Quest.StartZone}\n\nWhat's wrong:\n\n\n" +
            "---\nNote: EQBuddy mirrors eqlwiki.com, so if the wiki page itself is wrong, " +
            "editing the page is the strongest fix — the catalog re-harvests it weekly. " +
            "If the page is right and EQBuddy read it wrong, this report is exactly the right place.\n";
        return "https://github.com/DranakCorps-bot/EQBuddy/discussions/new?category=q-a" +
            "&title=" + Uri.EscapeDataString($"Quest data: {m.Quest.Name}") +
            "&body=" + Uri.EscapeDataString(body);
    }

    private void WithLedger(Action<QuestLedgerStore> act)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        act(ledger);
        Refresh(force: true);
    }

    /// <summary>
    /// Mark or unmark a catalog quest, sending a Plane of Sky test to the Sky checklist
    /// instead of the quest ledger.
    ///
    /// The read side folds `SkyQuestCompleted` into the completed map, so writing to the
    /// ledger here would leave the merge undoing the player's un-mark on the next render
    /// — a control that visibly does nothing, which is the "silent no-ops are broken"
    /// rule with the switch on the other side. One fact, one store, both directions.
    ///
    /// It also means turning a Sky Test in HERE acquires its pieces and resolves any
    /// parked auto-tick, exactly as the Sky tab's own button does — those rules live in
    /// <see cref="SkyCompleteToggle"/> and are not re-decided here. The Sky checklist is
    /// per profile rather than per character, which is how it has always been; this makes
    /// the two tabs agree rather than introducing it.
    /// </summary>
    private void ToggleCompleted(string questName, bool done)
    {
        var rewardKey = SkyTestSplit.RewardKeyFor(questName);
        if (rewardKey.Length == 0)
        {
            WithLedger(l => l.SetCompleted(_main.QuestCharacterKey, questName, done));
            return;
        }

        if (done)
            SkyCompleteToggle.MarkTurnedIn(_settings, rewardKey,
                SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey));
        else
            SkyCompleteToggle.Reopen(_settings, rewardKey);
        _settings.Save();
        Refresh(force: true);
    }

    // ---- shared small pieces ----

    /// <summary>A leading note above the list — the search scope, the current zone, the
    /// inventory file's age. Icon plus one caption; they used to be four differently
    /// sized TextBlocks each carrying its own emoji.
    ///
    /// A GRID, not a horizontal StackPanel: a stack hands its children infinite width, so
    /// TextWrapping never fires and a long note is silently CLIPPED instead of wrapping.
    /// Caught in the first Gate 2 capture — "pick classes ab" — which is exactly what the
    /// screenshot-review criterion is for.</summary>
    private static Grid IconLine(string text, string icon, string colorKey,
        DesignTokens.TypeRole role)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var glyph = DesignSystem.Icon(icon, colorKey, size: 12);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        glyph.Margin = new Thickness(0, 1, 0, 0);
        grid.Children.Add(glyph);
        var block = DesignSystem.Text(role, text);
        block.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        block.TextWrapping = TextWrapping.Wrap;
        block.Ink(colorKey);
        Grid.SetColumn(block, 1);
        grid.Children.Add(block);
        return grid;
    }

    private static Grid Note(string text, string icon, string colorKey = "DimBrush") =>
        IconLine(text, icon, colorKey, Role.Caption);

    /// <summary>An empty state: never a blank panel. Silent no-ops are broken (CLAUDE.md),
    /// and "nothing here" without "and here is how to change that" is the same defect.</summary>
    private static TextBlock EmptyState(string text)
    {
        var block = DesignSystem.Text(Role.Body, text);
        block.TextWrapping = TextWrapping.Wrap;
        block.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceM,
            DesignTokens.SpaceS, DesignTokens.SpaceM);
        return block;
    }

    /// <summary>The Epic tab's per-class band: the class name and the "Epic complete"
    /// master check (#138 aodgizmo, restored for #210).
    ///
    /// It sits at class level and not on a section heading because epic completion IS per
    /// class — <see cref="AppSettings.EpicQuestCompleted"/> is keyed by class name, and a
    /// per-section button would promise a hand-in that does not exist. That asymmetry with
    /// the Sky turn-in is real and is why <c>QuestChecklistGroup.CompletionKey</c> is null
    /// for Epic groups: a turn-in control must not appear there by accident.</summary>
    private Border EpicClassBand(string className)
    {
        var complete = EpicCompleteToggle.IsComplete(_settings, className);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = DesignSystem.Text(Role.TitleSection, className);
        name.VerticalAlignment = VerticalAlignment.Center;
        name.Ink(complete ? "GoodBrush" : "TextBrush");
        row.Children.Add(name);

        var button = new Button
        {
            Style = (Style)FindResource("EqPrimaryButton"),
            Content = EpicCompleteToggle.ButtonLabel(complete),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = complete
                ? "Reopen this epic. Rows go back the way they were before the master "
                  + "check ticked them — your own ticks are returned, not discarded."
                : "You finished this epic: ticks every remaining step for this class. "
                  + "Reopening puts them back.",
        };
        button.Click += (_, _) => ToggleEpicComplete(className, complete);
        Grid.SetColumn(button, 1);
        row.Children.Add(button);

        var band = new Border
        {
            Child = row,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, DesignTokens.SpaceL, 0, 0),
        };
        band.SetResourceReference(BackgroundProperty, "RaisedBrush");
        return band;
    }

    private void ToggleEpicComplete(string className, bool complete)
    {
        var items = EpicCompleteToggle.ItemsFor(
            _settings.EpicQuestChecklist, className, _settings.EpicQuestClassicOnly);
        if (complete)
        {
            EpicCompleteToggle.Reopen(_settings, className);
            EpicCompleteToggle.RestoreFrom(_settings, className, items);
        }
        else
        {
            // One click flips every unchecked row, which is bulk enough to warrant the
            // one confirmation this window has (#138). Nothing to overwrite means no
            // dialog — a prompt that can only be answered one way teaches nothing.
            if (EpicCompleteToggle.ConfirmPrompt(className, items) is { } prompt
                && MessageBox.Show(this, prompt, "Epic complete",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;
            EpicCompleteToggle.MarkComplete(_settings, className, items);
        }
        _settings.Save();
        Refresh(force: true);
    }

    /// <summary>"What can I turn in right now, across every class" (#129 bjstrange,
    /// restored for #205/#209/#210) — a band above the list naming every reward whose
    /// pieces are all in hand, and the NPC who takes it.
    ///
    /// Sky only, and only while something is actually ready: this is the one question on
    /// the page that names an action, and a permanently-present band reading "nothing" is
    /// how a player learns to stop looking at it. Epic has no per-section hand-in, so
    /// there is nothing for it to say.</summary>
    private void RenderReadyBand(QuestTab tab, IReadOnlyList<QuestChecklistGroup> groups)
    {
        if (tab != QuestTab.Sky) return;
        var ready = QuestChecklistLayout.ReadyToTurnIn(groups);
        if (ready.Count == 0) return;

        var panel = new StackPanel();
        // Built here rather than through IconLabel, which leaves its text at the caption
        // default: this heading names the one actionable thing on the page and cannot be
        // the dimmest line in its own band.
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        heading.Children.Add(DesignSystem.Icon("Check", "GoodBrush", size: 12));
        var headingText = DesignSystem.Text(Role.Caption, $"Ready to turn in — {ready.Count}");
        headingText.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        headingText.FontWeight = FontWeights.SemiBold;
        headingText.Ink("GoodBrush");
        heading.Children.Add(headingText);
        panel.Children.Add(heading);

        foreach (var group in ready)
        {
            var line = DesignSystem.Text(Role.Body, "");
            line.TextWrapping = TextWrapping.Wrap;
            line.Margin = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXxs, 0, 0);
            line.Inlines.Add(new System.Windows.Documents.Run(
                $"{QuestClassFilter.Abbrev(group.ClassName)} — {group.Title}")
            { FontWeight = FontWeights.SemiBold });
            if (group.TurnInNpc is { Length: > 0 } npc)
            {
                var to = new System.Windows.Documents.Run($"   {npc}");
                to.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                line.Inlines.Add(to);
            }
            line.ToolTip = $"{group.ClassName}: all {group.Total} "
                + (group.Total == 1 ? "item" : "items") + " acquired"
                + (group.TurnInNpc is { Length: > 0 } n ? $" — turn in to {n}" : "");
            panel.Children.Add(line);
        }

        var band = new Border
        {
            Child = panel,
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM),
        };
        band.SetResourceReference(BackgroundProperty, "RaisedBrush");
        QuestsPanel.Children.Add(band);
    }

    /// <summary>Done / Ready / Partial / Total per class (#136 bjstrange, restored with
    /// the band above) — "how am I doing across all sixteen" without a scroll.
    ///
    /// Only worth drawing for more than one class: with a single class in view the list
    /// underneath already says all of this, and a summary of one line is furniture.</summary>
    private void RenderClassCounts(QuestTab tab, IReadOnlyList<QuestChecklistGroup> groups)
    {
        if (tab != QuestTab.Sky) return;
        var counts = QuestChecklistLayout.ClassCounts(groups);
        if (counts.Count < 2) return;

        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM) };
        foreach (var c in counts)
        {
            var line = DesignSystem.Text(Role.Caption, "");
            var cls = new System.Windows.Documents.Run(QuestClassFilter.Abbrev(c.ClassName) + " ")
            { FontWeight = FontWeights.SemiBold };
            cls.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "TextBrush");
            line.Inlines.Add(cls);
            Metric("D", c.Done, "GoodBrush");
            Metric("R", c.Ready, "WarnBrush");
            Metric("P", c.Partial, "AccentBrush");
            // The total, because D+R+P deliberately does NOT sum to it — a reward you
            // have not started sits in no bucket. bjstrange read three numbers that
            // didn't add up and reasonably concluded they were wrong (#136); showing
            // what they are out of turns a puzzle into a subtraction.
            var total = new System.Windows.Documents.Run($" /{c.Total}");
            total.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
            line.Inlines.Add(total);

            void Metric(string label, int count, string brushKey)
            {
                var name = new System.Windows.Documents.Run(label);
                name.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                var value = new System.Windows.Documents.Run(count.ToString() + " ")
                { FontWeight = FontWeights.SemiBold };
                value.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, brushKey);
                line.Inlines.Add(name);
                line.Inlines.Add(value);
            }

            var chip = new Border
            {
                Child = line,
                CornerRadius = new CornerRadius(DesignTokens.RadiusPill),
                Padding = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXxs,
                    DesignTokens.SpaceS, DesignTokens.SpaceXxs),
                Margin = new Thickness(0, 0, DesignTokens.SpaceXs, DesignTokens.SpaceXs),
                Cursor = Cursors.Hand,
                ToolTip = $"{c.ClassName}: {c.Done} turned in, {c.Ready} ready to turn in, "
                    + $"{c.Partial} started, of {c.Total}. Click to show only this class.",
            };
            chip.SetResourceReference(BackgroundProperty, "RaisedBrush");
            var className = c.ClassName;
            chip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                // A second click clears it, so the summary can put a class back as well
                // as take one away — a lens you can only enter is a trap.
                _classLens = _classLens is { } lens
                    && lens.Equals(className, StringComparison.OrdinalIgnoreCase)
                        ? null : className;
                ApplyTabVisual();
                Refresh(force: true);
            };
            wrap.Children.Add(chip);
        }
        QuestsPanel.Children.Add(wrap);
    }

    /// <summary>The Epic and Sky tabs. Rows come straight from the same settings lists
    /// the loot auto-checkers tick and EQBuddy Mobile reads, so ticking here, on the
    /// tablet, or by looting the thing are all the same tick — this is a second VIEW,
    /// never a second copy of the data. The search box keeps working here.
    ///
    /// The rows are TICKABLE, and have to be: hand-ticking used to live on the widget's
    /// Epic and Sky cards, and when those became one launcher (2026-08-16) this became
    /// the only place on the desktop to say "I already have that".</summary>
    /// <summary>The Sky tab's copy of the achievements auto-import report (Bevel,
    /// Helm-signed 2026-08-23). The dump feeds TWO consumers — raid clears and Sky rewards —
    /// and until now the report sat only on Raids, so *"1 Sky reward marked · 2 skipped"* was
    /// being read above a list of raid bosses by a player who may never open it. Bevel:
    /// *"a Quest-Tracker job being read on a raid-clear list… not Raids-only."*
    ///
    /// The same class, not a Sky-flavoured variant: one more host, one more line, and the
    /// rule about when an Undo is offered stays in exactly one place.</summary>
    private ImportReportView SkyImport => _skyImport ??=
        // force: true, because Refresh's signature check would see no change — an Undo
        // moves checklist TICKS, and the signature is built from the same lists it just
        // restored. A repaint that decides nothing changed is how an Undo looks broken.
        new ImportReportView(() => _main.LastAchievementsImport, () => Refresh(force: true));

    private ImportReportView? _skyImport;

    /// <summary>
    /// The Sky tab's route to its own data source.
    ///
    /// This surface is FED by the achievements dump — the import is what tells it which
    /// rewards were handed in before EQBuddy existed, and a hand-in never appears in the
    /// log — and it named no way to produce one. The command lived on the widget's menu
    /// and on the Raids card, neither of which is where a player wondering about Sky
    /// rewards is looking. `GameCommandsTests.SurfacesNeedingACommand` is the curated
    /// list this row is now on: a negative assertion cannot see an absence (trap 34), and
    /// the Gear tab fell through the same hole for as long as it existed.
    ///
    /// Above the rows, beside the import report, for the reason in trap 44 — it is read
    /// on arrival, and the widget caps its own height.
    /// </summary>
    private UIElement SkyAchievementsPrompt()
    {
        var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        wrap.Children.Add(Note(
            "Turned rewards in before EQBuddy? The game's achievements dump knows. Run this "
            + "in game and EQBuddy reads the file it writes — it never scans the game itself.",
            "Info"));
        var b = new Button
        {
            Style = (Style)FindResource("ActionButton"),
            FontSize = DesignTokens.Spec(Role.Caption).Size,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0),
            ToolTip = "Copies the command — paste it into the game's chat. The game writes "
                + "<name>_<server>-Achievements.txt beside its own folders and EQBuddy "
                + "imports it on its own; the report appears here.",
        };
        wrap.Children.Add(Theming.WireCopyCommand(b, GameCommands.OutputfileAchievements));
        return wrap;
    }

    /// <summary>
    /// Race and class unlocks (Hateborne, 2026-08-25).
    ///
    /// **Rows are read-only, and that is the design rather than a shortcut.** An unlock is
    /// the GAME's answer — it comes from the achievements dump and, for a race, from the
    /// faction dump. There is nothing for the player to tick, so there is no checkbox: a
    /// disabled one would render exactly like a live one and swallow clicks (trap 17), and
    /// a live one would invite a player to record something EQBuddy would overwrite on the
    /// next dump.
    /// </summary>
    private void RenderUnlocks()
    {
        var races = _main.Unlocks.Races;
        var classes = _main.Unlocks.Classes;
        var factions = _main.Unlocks.Factions;

        // BOTH commands, always — not only in the empty states they used to hide behind
        // (Hateborne, 2026-08-25). This tab is built from two dumps and neither is a
        // one-off: a race unlock moves every time you grind faction, so the button a
        // player needs most is the one on the POPULATED surface. That is the same rule
        // the Gear tab learned in #217, and the reason its ⧉ is not empty-state-only.
        QuestsPanel.Children.Add(UnlockCommandRow());

        if (!_main.Unlocks.HasAchievements)
        {
            QuestsPanel.Children.Add(EmptyState(
                "No achievements dump yet. Race and class unlocks are the game's own record "
                + "— EQBuddy reads the file the game writes and never scans the game itself. "
                + "Run the achievements command above and this fills in."));
            return;
        }

        // What the second dump is for, said only when something in view wants it.
        if (UnlockLayout.NeedsFactionDump(races, factions))
        {
            QuestsPanel.Children.Add(Note(
                "Race unlocks are faction work, and the log only ever sees faction CHANGES "
                + "— never where you stand. Run the faction command above and the rows "
                + "below fill in.", "Info"));
        }
        else if (factions is { } f)
        {
            QuestsPanel.Children.Add(Note(
                $"Standings as of {f.WrittenAt:d MMM HH:mm}. Re-run the faction command "
                + "after a grind to refresh them.", "Info"));
        }

        Section(UnlockLayout.RacesHeading, races);
        Section(UnlockLayout.ClassesHeading, classes);

        void Section(string heading, IReadOnlyList<UnlockProgress> unlocks)
        {
            if (unlocks.Count == 0) return;
            if (!UnlockLayout.InSection(heading, _unlockSection)) return;
            var title = DesignSystem.Text(Role.TitleSection, heading);
            title.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL, 0,
                DesignTokens.SpaceXs);
            title.Ink("AccentBrush");
            QuestsPanel.Children.Add(title);

            var groups = UnlockLayout.Groups(unlocks, factions, heading);
            for (var i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var u = unlocks[i];
                var score = u.Score is { } s ? $"   {s.Done}/{s.Total}" : "";
                var head = DesignSystem.Text(Role.Body, g.Title + score);
                head.FontWeight = FontWeights.SemiBold;
                head.TextWrapping = TextWrapping.Wrap;
                head.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceM, 0, 0);
                head.Ink(u.Complete ? "GoodBrush" : "TextBrush");
                QuestsPanel.Children.Add(head);

                // Why it is complete matters as much as that it is: a granted unlock sits
                // beside factions near zero, and a player who is not told reads the
                // tracker as broken rather than the unlock as free.
                if (UnlockLayout.Note(u) is { Length: > 0 } note)
                    QuestsPanel.Children.Add(Note(note, "Info"));

                foreach (var row in g.Rows)
                {
                    var line = new Grid { Margin = new Thickness(DesignTokens.SpaceL, 1, 0, 1) };
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    // Two columns, never a horizontal StackPanel: a stack measures with
                    // infinite width, so wrapping text beside an icon is clipped with no
                    // ellipsis to say so (trap 14).
                    var icon = DesignSystem.Icon(row.Acquired ? "Check" : "Pending",
                        row.Acquired ? "GoodBrush" : "DimBrush", size: DesignTokens.IconInline);
                    icon.VerticalAlignment = VerticalAlignment.Center;
                    icon.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
                    Grid.SetColumn(icon, 0);
                    line.Children.Add(icon);

                    var text = DesignSystem.Text(Role.Body, "");
                    text.TextWrapping = TextWrapping.Wrap;
                    text.Inlines.Add(new System.Windows.Documents.Run(row.Title));
                    if (row.Detail.Length > 0)
                    {
                        var detail = new System.Windows.Documents.Run("   " + row.Detail);
                        detail.SetResourceReference(
                            System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                        text.Inlines.Add(detail);
                    }
                    text.Ink(row.Acquired ? "DimBrush" : "TextBrush");
                    Grid.SetColumn(text, 1);
                    line.Children.Add(text);
                    QuestsPanel.Children.Add(line);
                }
            }
        }
    }

    /// <summary>
    /// The two commands this tab is built from, side by side and always on screen.
    ///
    /// The header's "scan bags" button is hidden on this tab (see ApplyTabVisual): it
    /// copies <c>/outputfile inventory</c>, which has nothing to do with race or class
    /// unlocks, and its tooltip said so in as many words. A button that works and answers
    /// a question the surface is not asking is its own kind of wrong.
    /// </summary>
    private UIElement UnlockCommandRow()
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceS) };
        row.Children.Add(CommandPrompt(GameCommands.OutputfileAchievements,
            "Copies the command. The game writes <name>_<server>-Achievements.txt beside "
            + "its own folders; EQBuddy reads it on its own and this tab fills in. "
            + "This is what says which races and classes you have unlocked."));
        row.Children.Add(CommandPrompt(GameCommands.OutputfileFaction,
            "Copies the command. The game writes <name>_<server>-<CLASS>-Factions.txt; "
            + "EQBuddy reads it the moment the game says it is written. This is what says "
            + "how far along each race's factions are — the log can only see faction "
            + "CHANGES, never where you stand."));
        return row;
    }

    /// <summary>A ⧉ copy of an in-game command, off <see cref="GameCommands"/>. Never its
    /// own literal — GameCommandsTests forbids that, and the reason is that a button and
    /// the prose beside it drifted.</summary>
    private Button CommandPrompt(string command, string tip)
    {
        var b = new Button
        {
            Style = (Style)FindResource("ActionButton"),
            FontSize = DesignTokens.Spec(Role.Caption).Size,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXs, 0, DesignTokens.SpaceS),
            ToolTip = tip,
        };
        return Theming.WireCopyCommand(b, command);
    }

    private void RenderChecklist(QuestTab tab, string filter, List<string> classes)
    {
        // ABOVE the rows and re-added on every render, because QuestsPanel is cleared
        // wholesale — trap 44: a report about something that just happened belongs where
        // the eye lands, not under a checklist the player has to scroll.
        if (tab == QuestTab.Sky)
        {
            SkyImport.Render();
            QuestsPanel.Children.Add(SkyImport.Body);
            QuestsPanel.Children.Add(SkyAchievementsPrompt());
        }
        // Grouping, ordering and the detail line come from Core so this window, the
        // Avalonia one and EQBuddy Mobile cannot disagree about what a checklist row
        // says — they already had (#184).
        var groups = tab == QuestTab.Epic
            ? QuestChecklistLayout.Epic(_settings.EpicQuestChecklist
                .Where(i => !_settings.EpicQuestClassicOnly || i.AvailableInClassic))
            : QuestChecklistLayout.Sky(_settings.SkyQuestChecklist, _settings.SkyQuestCompleted,
                _settings.SkyStepsUnderEveryIsland);

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

        // The class picker chooses WHICH classes are in view — including ones you don't
        // play, because "we may be helping a friend" (David, 2026-08-15). The chips then
        // narrow to one of them. An empty pick means every class, never an empty window.
        var inScope = groups
            .Where(g => classes.Count == 0
                || classes.Contains(g.ClassName, StringComparer.OrdinalIgnoreCase))
            .Where(g => _classLens is null
                || g.ClassName.Equals(_classLens, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // The two cross-class summaries go ABOVE the lens, deliberately: they answer
        // "what can I do right now" and "how am I doing overall", and a filter that hid
        // them would leave the player narrowing a list to find out what they were already
        // being told. Both read the class-scoped set, so the class picker still governs.
        RenderReadyBand(tab, inScope);
        RenderClassCounts(tab, inScope);

        // SEARCHING IS NOT FILTERING (#108, liminalwarmth). A query rearranges the screen
        // by ITEM and crosses every class — "who wants this drop" is unanswerable inside
        // one class's filter — so it reads `groups`, not `inScope`, and skips the state
        // lens entirely. 1.69.0 shipped it under exactly that rule and the Gate 2 rebuild
        // lost it: the box survived as a row filter INSIDE the per-class sections, which
        // is the "scrolling through each class" the ask was about. Clearing the box brings
        // the class layout back.
        if (filter.Length > 0)
        {
            RenderItemMatches(QuestChecklistLayout.SearchByItem(groups, filter), setters, tab);
            return;
        }

        var matching = QuestChecklistLayout.InState(inScope, _state).ToList();

        if (matching.Count == 0)
        {
            // NAME what emptied the list. "Nothing matches" over a checklist that is
            // merely filtered reads as a broken tracker — the failure mode #193 and #203
            // both describe from the other side.
            // (A search never reaches here — it returns above with its own empty state.)
            QuestsPanel.Children.Add(EmptyState(
                _state != QuestChecklistLayout.StateAny
                    ? $"Nothing here is “{_state}” right now — the state filter "
                      + "above is narrowing the list."
                : groups.Count > 0
                    ? "Nothing for the classes you have picked — the class picker above "
                      + "chooses which checklists this tab shows."
                : "This checklist is empty — it fills in from the wiki catalog and your own "
                  + "progress. Scan bags or import achievements to catch it up."));
            return;
        }

        var lastClass = "";
        foreach (var group in matching)
        {
            // A class whose epic is marked complete has LOCKED rows. Not decoration: the
            // master check's undo restores the snapshot it took, so a tick made while
            // complete would be silently discarded on Reopen — EpicCompleteToggle.Restore
            // says so in as many words, and it is only true if the rows cannot move.
            var locked = tab == QuestTab.Epic
                && EpicCompleteToggle.IsComplete(_settings, group.ClassName);
            // One band per CLASS on the Epic tab, carrying the master complete. Epic
            // completion is per class — never per section — so this cannot ride on a
            // group heading the way the Sky turn-in does.
            if (tab == QuestTab.Epic && !group.ClassName.Equals(lastClass, StringComparison.OrdinalIgnoreCase))
            {
                lastClass = group.ClassName;
                QuestsPanel.Children.Add(EpicClassBand(group.ClassName));
            }
            // The heading opens the wiki page for the reward it names — the "way to view
            // details of sky quests" #184 asked back for. Catalog rows have carried a
            // clickable name since the tracker existed; checklist rows never did.
            var headingText = DesignSystem.Text(Role.TitleSection,
                $"{group.Heading}   {group.Done}/{group.Total}"
                + (group.Note is { } n ? $"  · {n}" : ""));
            headingText.TextWrapping = TextWrapping.Wrap;
            headingText.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL,
                0, DesignTokens.SpaceXs);
            headingText.Cursor = Cursors.Hand;
            headingText.ToolTip = "Open the wiki page for this quest";
            headingText.Ink("AccentBrush");
            var rewardName = group.Title;
            headingText.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenUrl(EqlWiki.PageUrl(rewardName));
            };
            // "I turned this in." Restored 2026-08-18 — the widget's Sky card had this per
            // reward, and when that card became a launcher only the per-ITEM ticks came
            // across: SkyQuestCompleted kept being READ by both desktops and the phone
            // while nothing but the achievements import could WRITE it. Holding the pieces
            // and having handed them over are different states.
            if (group.CompletionKey is { } rewardKey && (group.Completed || group.ReadyToTurnIn))
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(headingText);

                var turnIn = new Button
                {
                    Style = (Style)FindResource("EqPrimaryButton"),
                    Content = SkyCompleteToggle.ButtonLabel(group.Completed),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceL, 0, 0),
                    ToolTip = group.Completed
                        ? "Reopen this reward. Your item ticks stay as they are — you know what you still hold."
                        : "You handed these in: marks the reward done and ticks its items.",
                };
                var completed = group.Completed;
                turnIn.Click += (_, _) =>
                {
                    if (completed) SkyCompleteToggle.Reopen(_settings, rewardKey);
                    else SkyCompleteToggle.MarkTurnedIn(_settings, rewardKey,
                        SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey));
                    _settings.Save();
                    Refresh(force: true);
                };
                Grid.SetColumn(turnIn, 1);
                row.Children.Add(turnIn);
                QuestsPanel.Children.Add(row);
            }
            else QuestsPanel.Children.Add(headingText);

            // Island sub-headings (David, 2026-08-23, from a Reddit ask): "a player should
            // see the work for one island together, not a flat list that jumps islands."
            // Core hands the rows over already ordered and already labelled, so this draws a
            // heading whenever the label changes and owns no grouping logic of its own —
            // which is the only reason three surfaces can agree about it (#184).
            var lastIsland = "";
            foreach (var row in group.Rows)
            {
                if (row.IslandHeading.Length > 0 && row.IslandHeading != lastIsland)
                {
                    lastIsland = row.IslandHeading;
                    var island = DesignSystem.Text(Role.Caption, row.IslandHeading);
                    island.FontWeight = FontWeights.SemiBold;
                    island.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceS,
                        0, DesignTokens.SpaceXxs);
                    island.Ink("DimBrush");
                    QuestsPanel.Children.Add(island);
                }
                var text = DesignSystem.Text(Role.Body, "");
                text.TextWrapping = TextWrapping.Wrap;
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
                text.Ink(row.Acquired ? "DimBrush" : "TextBrush");
                var check = new CheckBox
                {
                    Content = text,
                    IsChecked = row.Acquired,
                    Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
                    ToolTip = row.Unassigned
                        ? "EQBuddy ticked this itself — several classes want this item and the "
                          + "log couldn't say which one earned it. Move the tick if it's on the "
                          + "wrong class; either way, toggling it settles the question."
                        : null,
                };
                if (locked)
                {
                    check.IsEnabled = false;
                    // And LOOK disabled. The app's CheckBox style carries no disabled
                    // visual, so IsEnabled alone leaves a control that reads as live and
                    // silently ignores clicks — the "silent no-ops are broken" rule with
                    // the switch on the other side. Found by looking at the screenshot.
                    check.Opacity = 0.5;
                    check.ToolTip = $"{group.ClassName}'s epic is marked complete. "
                        + "Reopen it above to change individual steps.";
                }
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

    /// <summary>The item-grouped search result (#108): one heading per ITEM, and under it
    /// every class that wants it, each still a live tick. The arrangement is the answer —
    /// a drop three classes are queuing for is one block here and was three sections you
    /// had to scroll between before.</summary>
    private void RenderItemMatches(
        IReadOnlyList<QuestChecklistLayout.ChecklistItemMatch> matches,
        Dictionary<string, Action<bool>> setters,
        QuestTab tab)
    {
        if (matches.Count == 0)
        {
            QuestsPanel.Children.Add(EmptyState(
                "Nothing on this checklist matches that search. It looks at item names, "
                + "reward names and drop locations, across every class — so this is the "
                + "whole checklist saying no, not a filter narrowing it."));
            return;
        }

        var scope = DesignSystem.Text(Role.Caption, QuestChecklistLayout.SearchScopeNote);
        scope.TextWrapping = TextWrapping.Wrap;
        scope.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceXs, 0, 0);
        QuestsPanel.Children.Add(scope);

        foreach (var match in matches)
        {
            var heading = DesignSystem.Text(Role.TitleSection, match.Title);
            heading.TextWrapping = TextWrapping.Wrap;
            heading.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL,
                0, DesignTokens.SpaceXxs);
            heading.Cursor = Cursors.Hand;
            heading.ToolTip = "Open the wiki page for this item";
            heading.Ink("AccentBrush");
            var itemName = match.Title;
            heading.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                OpenUrl(EqlWiki.PageUrl(itemName));
            };
            QuestsPanel.Children.Add(heading);

            // The one line #108 asked for in as many words: who wants this drop.
            var wanted = match.Classes == 1
                ? $"1 class wants this · {match.Held} of {match.Total} in hand"
                : $"{match.Classes} classes want this · {match.Held} of {match.Total} in hand";
            var summary = DesignSystem.Text(Role.Caption, wanted);
            summary.Margin = new Thickness(DesignTokens.SpaceXxs, 0, 0, DesignTokens.SpaceXs);
            QuestsPanel.Children.Add(summary);

            foreach (var wanter in match.Wanters)
            {
                var text = DesignSystem.Text(Role.Body, "");
                text.TextWrapping = TextWrapping.Wrap;
                text.Inlines.Add(new System.Windows.Documents.Run(
                    wanter.ClassName + " · " + wanter.Reward));
                if (wanter.Detail.Length > 0)
                {
                    var detail = new System.Windows.Documents.Run("   " + wanter.Detail);
                    detail.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                    text.Inlines.Add(detail);
                }
                if (wanter.RewardCompleted)
                {
                    var done = new System.Windows.Documents.Run("   turned in");
                    done.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "GoodBrush");
                    text.Inlines.Add(done);
                }
                text.Ink(wanter.Acquired ? "DimBrush" : "TextBrush");

                var check = new CheckBox
                {
                    Content = text,
                    IsChecked = wanter.Acquired,
                    Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
                };
                // Same lock as the class layout: a class whose epic is marked complete has
                // rows that must not move, or the master check's undo would discard them.
                if (tab == QuestTab.Epic
                    && EpicCompleteToggle.IsComplete(_settings, wanter.ClassName))
                {
                    check.IsEnabled = false;
                    check.Opacity = 0.5;    // trap 17: IsEnabled alone is invisible
                    check.ToolTip = $"{wanter.ClassName}'s epic is marked complete. "
                        + "Clear the search and reopen it to change individual steps.";
                }
                if (!setters.TryGetValue(wanter.RowId, out var set)) continue;

                var rowId = wanter.RowId;
                var label = match.Title + " (" + wanter.ClassName + ")";
                check.Checked += (_, _) => Tick(true);
                check.Unchecked += (_, _) => Tick(false);
                QuestsPanel.Children.Add(check);

                void Tick(bool done)
                {
                    set(done);
                    _settings.Save();
                    PushUndo(new QuestChecklistRow(rowId, wanter.ClassName, label, "",
                        done, Unassigned: false), done, set);
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

    /// <summary>How many rows a single view will build. "all" offers the whole
    /// 1,172-quest catalog, and rendering that is seconds of UI thread, per keystroke.
    /// Rows are far cheaper than the cards they replace, so the cap could rise — but a
    /// list nobody can scan is not an improvement over a list that says how much it is
    /// holding back.</summary>
    private const int RenderCap = 60;
    private int _renderedCount;
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

    /// <summary>Same command-copy contract as every other place EQBuddy reads a game
    /// command's output: we never type in your client, so the most we can do is put the
    /// exact command on your clipboard. Flashes a confirmation so a silent clipboard
    /// write isn't a silent no-op.</summary>
    private void OnCopyInventoryCmd(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(GameCommands.OutputfileInventory); }
        catch (Exception ex) { CoreLog.Error(ex); return; }
        CopyInvBtn.Content = IconLabel("Check", "copied", "GoodBrush");
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        t.Tick += (_, _) => { CopyInvBtn.Content = IconLabel("Copy", "scan bags"); t.Stop(); };
        t.Start();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
