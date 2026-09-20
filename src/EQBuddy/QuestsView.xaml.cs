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
///
/// ---
///
/// **LIFTED out of <c>QuestsWindow</c> for E-3 Phase 2 PR 3** — the Evolved shell needed
/// the CONTENT without the window, and there was no view to hand it: this was 2,481 lines
/// of window-owned rendering, which is why Bevel's pre-design called Quests *"an
/// extraction, not a redesign"* and why it got its own diff instead of riding PR 2 with
/// World and Gear. <c>SpawnsView</c> (World PR 1) is the precedent and the shape is
/// identical: the whole bordered panel comes across, chrome and content together, because
/// the window is borderless and hand-drawn and there is no clean seam at the XAML level.
/// <see cref="QuestsWindow"/> is now a thin host owning only what a literal OS window owns
/// — sizing, position, <c>DragMove</c>, <c>Close</c>, reached through
/// <see cref="Window.GetWindow(DependencyObject)"/> rather than a captured reference —
/// and <see cref="QuestsRoom"/> hosts the same view inside the shell.
///
/// **The lift moved no presentation rule and changed no wording.** The five Helm-signed
/// rules Bevel asked to be inventoried before the diff all live BELOW this line and all
/// stayed exactly where they were, because they are properties of the surface rather than
/// of the window around it: the #241 turn-in provenance sentence
/// (<see cref="Objectives"/>), the Sky bags/leftover bands (<see cref="RenderLeftoverBands"/>),
/// the session-only band folds (<see cref="_skyBandOpen"/>), the Ready-unlocked caveat
/// (<see cref="RenderReadyBand"/>) and the Alt+Tab-reachable Sky commands
/// (<see cref="SkyAchievementsPrompt"/>). Each is asserted from
/// <c>tests/EQBuddy.E2E</c> through <see cref="DebugFacts"/>, and the shell reports the
/// SAME strings under a <c>shellQuests*</c> prefix so the two hosts cannot disagree.
///
/// **What is NEW here, and it is one thing:** <see cref="SinglePane"/>. Below
/// <c>ShellLayoutPolicy.SplitRoomWidth</c> a list+detail room has to become one pane with
/// a way back, and this surface is the first consumer of an axis that has been decided and
/// unexercised since PR 1. It is off by default, so the window's arrangement is untouched.
/// </summary>
public partial class QuestsView : UserControl
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private string _signature = "";
    private DateTime _lastRefresh = DateTime.MinValue;
    private string _mode = "mine";   // mine = items+pins · zone = current zone · all
    // Snapshot of the ledger's owned dict as of the last Refresh, kept for the detail
    // pane: Select()/BuildDetail() run off a click, not a refresh, and need the raw
    // Verified/VerifiedAt fields Progressed() already collapsed to Total.
    private IReadOnlyDictionary<string, QuestLedgerStore.Entry> _owned =
        new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
    /// <summary>The classes this VIEW is about, from the one producer
    /// (<see cref="QuestClassLens.Offered"/>) and captured before the view lens narrows
    /// them to one.
    ///
    /// <para>Two readers, one stored answer, which is the point of the field rather than a
    /// second call: the leftover bands need the character's real class list, not the one
    /// class the player is currently looking at — "only other classes want this" said
    /// about a class you play, because you had it lensed out, is a false claim and the one
    /// claim band B exists to make carefully (#193's rule, one surface over) — and
    /// <see cref="BuildClassStrip"/> needs exactly the same list, because a chip for a
    /// class this render has already narrowed away is a control that does nothing
    /// (DRA-181 D4). <see cref="Refresh"/> writes it before <see cref="BuildTabs"/> runs,
    /// so the strip is never built from the render before last (trap 33).</para></summary>
    private IReadOnlyList<string> _offered = [];
    /// <summary>The Helper's answers about the subjects this catalog's steps point at
    /// (DRA-83) — this view's OWN memo, like <see cref="_unlockPool"/> beside it, because
    /// QuestsWindow and QuestsRoom each build their own view (trap 45).</summary>
    private readonly GuideHelperSource _helper;

    public QuestsView(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _settings = main.Settings;
        _tabs = new EqSegmentedStrip(TabStrip);
        _classes = new EqSegmentedStrip(ClassStrip);
        _modes = new EqSegmentedStrip(ModeStrip);
        _skyView = new EqSegmentedStrip(SkyViewStrip, compact: true);
        _unlockPool = new WikiPackPool(main.StoredMobRows);
        _helper = new GuideHelperSource(main);
        BuildStaticChrome();
        EpicClassicOnlyCheck.IsChecked = _settings.EpicQuestClassicOnly;
        SkyIslandRepeatCheck.IsChecked = _settings.SkyStepsUnderEveryIsland;
        SkyClosestCheck.IsChecked = _settings.SkyClosestToCompletion;
        BuildClassChecks();
        BuildUnlockPicker();
        EraCombo.Items.Add("Any era");
        foreach (var era in QuestEraLadder.Eras) EraCombo.Items.Add($"≤ {era}");
        var savedEra = Array.IndexOf(QuestEraLadder.Eras, _settings.QuestEraFilter);
        EraCombo.SelectedIndex = savedEra >= 0 ? savedEra + 1 : 0;
        // From Core, so the combo, the checklist filter and EQBuddy Mobile cannot end up
        // offering three different vocabularies for one lens.
        foreach (var s in QuestChecklistLayout.States) StateCombo.Items.Add(s);
        StateCombo.SelectedIndex = 0;
        BuildUnlockSectionStrip();
        BuildModeStrip();
        BuildSkyViewStrip();
        // Ctrl+Z, because the undo button promises it and a promise in a tooltip is a
        // feature. Not while typing in the search box — there it means undo the text.
        //
        // On the VIEW rather than on the window since the lift, which is the same handler
        // with a smaller reach: it fires while focus is inside this surface, and the only
        // thing the window had outside it was a title row. In the shell it is the reach
        // that is correct — Ctrl+Z pressed in the World room must not undo a quest tick.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Z || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (Keyboard.FocusedElement is TextBox) return;
            e.Handled = true;
            OnUndo(this, new RoutedEventArgs());
        };
        Refresh(force: true);
    }

    // ---- the seams the two hosts hang on ----------------------------------------
    //
    // Everything in this block exists because there are now TWO hosts: the v1 window and
    // the shell's room. Each one is a thing the WINDOW used to do inline, named on the
    // view so a host has to answer it rather than inherit a default of "nothing" — which
    // is the same argument IShellRoom.Release() is built on.

    /// <summary>The room IS the control. Nothing is handed out that this view did not
    /// build, so a host cannot end up sharing a surface with the other one — a WPF
    /// <c>UIElement</c> has exactly one parent and would be torn out of whichever host
    /// painted it last, silently (trap 45).</summary>
    public UIElement Body => this;

    /// <summary>
    /// Hide this view's own title row and close button, for a host that supplies its own
    /// chrome. <c>SpawnsView.HideOwnTitleBar</c> is the precedent, word for word: the shell
    /// draws a native title bar reading "EQBuddy — Guide" and a rail, so drawing this
    /// view's copies too would put two title rows and two close buttons on screen at once.
    /// Collapsing the ROW rather than the two controls, so the Auto row height follows and
    /// nothing leaves a gap.
    ///
    /// **What that subtracts, named rather than left to be noticed** (trap 26): the
    /// heading carries the CHARACTER — "Quest Tracker — Dranak" — which no other room's
    /// title did and which the shell's own title bar cannot say. It is not lost:
    /// <see cref="Heading"/> is the one place that string is built, and
    /// <see cref="QuestsRoom"/> draws it as a caption above the tabs. One producer, two
    /// consumers, which is the only arrangement that cannot drift.
    /// </summary>
    internal void HideOwnTitleBar() => TitleBar.Visibility = Visibility.Collapsed;

    /// <summary>The heading the title row shows, and the only place it is composed. Empty
    /// until the first <see cref="Refresh"/>.</summary>
    internal string Heading { get; private set; } = "Quest Tracker";

    /// <summary>Raised when <see cref="Heading"/> changes, so a host that draws it
    /// somewhere other than the title row stays in step without polling.</summary>
    internal event Action<string>? HeadingChanged;

    /// <summary>
    /// Cap the two scrollers, for the host that needs to: the v1 window is
    /// <c>SizeToContent="Height"</c>, so its <c>*</c> row measures to CONTENT and a long
    /// catalog would grow the window past its own cap and walk the footnotes off the
    /// bottom of the screen. The shell's room is a bounded <c>*</c> cell and needs no cap
    /// at all — which is why the default is uncapped and the WINDOW is the one that calls
    /// this, rather than the view guessing which host it is in.
    /// </summary>
    internal void CapScrollers(double cap)
    {
        BodyScroll.MaxHeight = cap;
        DetailScroll.MaxHeight = cap;
    }

    /// <summary>
    /// **Give back what this view borrowed** (trap 46, and the obligation
    /// <c>IShellRoom.Release</c> names on the interface). One thing: the search debounce,
    /// a <see cref="DispatcherTimer"/> that is started on every keystroke and stops itself
    /// on tick — so a host closed within the settle window left it queued to fire
    /// <see cref="Refresh"/> against a torn-down surface.
    ///
    /// The v1 window never did this, and it was survivable there because the timer stops
    /// itself 120 ms later and a closed window is closed once. A ROOM is built on first
    /// arrival and released when the shell closes, and the shell can be reopened — so
    /// "survivable" stops being the standard and the interface asks the question outright.
    /// Both hosts call it now.
    /// </summary>
    internal void Release() => _searchDebounce?.Stop();

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
        // The single-pane way back. Built here with the rest of the static chrome even
        // though it is collapsed on every host but a narrow room — a control assembled
        // lazily is a control whose first appearance is its first test.
        BackToList.Content = IconLabel("ChevronLeft", "all quests");
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

    /// <summary>Jump to one item's quests (the map badge in the Loot views): browse mode
    /// + the item as filter, so the quests appear even before any overlap and each carries
    /// its pin as the invitation to track. Fronting the host is the HOST's job — a view
    /// does not know whether it is in a window or a room.</summary>
    public void FilterToItem(string item)
    {
        _mode = "all";
        ApplyModeVisual();
        FilterBox.Text = item;
        Refresh(force: true);
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
        // "folded" collapses the three Sky bands — screenshot-only state with no settings
        // backing (the fold is session-only by design), so this hook is the ONLY way the
        // collapsed rendering can be photographed at all (trap 22).
        else if (state == "folded")
            foreach (var id in new[] { "skyReady", "skyLeftoverA", "skyLeftoverB" })
                _skyBandOpen[id] = false;
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
    /// <summary>Class view · Island view, on the Sky tab (DRA-164).</summary>
    private EqSegmentedStrip _skyView = null!;
    private EqSegmentedStrip _unlockSections = null!;

    /// <summary>Build the strip from Core's <see cref="QuestSurface"/> so the desktop and
    /// EQBuddy Mobile cannot disagree about which tabs exist, their order or their
    /// names — the whole reason that lives in Core.</summary>
    /// <summary>Raised when the PLAYER switches tabs here. Not raised by SetTab.</summary>
    internal event Action<QuestTab>? TabChanged;

    private void BuildTabs()
    {
        _tabs.Clear();
        foreach (var header in QuestSurface.Tabs(EpicCounts(), SkyCounts(), UnlockCounts()))
        {
            var tab = header.Tab;
            _tabs.Add(header.Label, tab, header.Badge, onClick: () =>
            {
                _tab = tab;
                // The theme host follows the player (Inline themes PR 3) — the same
                // event the other three windows raise, for the same hand-back.
                TabChanged?.Invoke(tab);
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

    /// <summary>
    /// Class view · Island view (DRA-164, the Founder's ask of 2026-09-17).
    ///
    /// <para><b>KEEP was the first word of the ask</b>, so "Class" leads and is what a player
    /// who upgrades sees. Both chips are always offered — an island view that appeared only
    /// once some condition was met would be a surface nobody could find.</para>
    ///
    /// <para>The fifth instance of the one primitive (<c>EqChip</c> / <c>EqSegmentedStrip</c>),
    /// never hand-built: the tabs, the class lens, the mode strip and the Unlocks section lens
    /// are the other four, and a two-chip control wearing its own clothes is the odd one out
    /// this rule exists to prevent.</para>
    /// </summary>
    private void BuildSkyViewStrip()
    {
        foreach (var (key, label, tip) in new[]
        {
            ("class", "Class view",
                "Grouped by your class and the reward you're working toward — the view "
                + "EQBuddy has always had, and still the default."),
            ("island", "Island view",
                "Grouped by ISLAND, across every class you've picked: everything to collect "
                + "on one island before you move to the next. Hand-ins aren't listed here — "
                + "the Ready band above still says what you can turn in."),
        })
        {
            var island = key == "island";
            _skyView.Add(label, key, tip: tip, onClick: () =>
            {
                if (_settings.SkyGroupByIsland == island) return;
                _settings.SkyGroupByIsland = island;
                _settings.Save();
                ApplySkyViewVisual();
                Refresh(force: true);
            });
        }
        ApplySkyViewVisual();
    }

    private void ApplySkyViewVisual() =>
        _skyView.Select(_settings.SkyGroupByIsland ? "island" : "class");

    /// <summary>Any · one of your classes. The class picker still decides WHICH classes
    /// you have; this decides which of them you're looking at right now, which is a
    /// different question and wanted far more often.</summary>
    private void BuildClassStrip()
    {
        _classes.Clear();
        // THE SAME LIST THE RENDER NARROWED TO (DRA-181 D4, plan P5) — read off the field
        // Refresh wrote a few lines earlier, never re-derived here.
        //
        // It used to call `ClassSourceFor(...).Classes` itself: the RESOLVED list, which is
        // the right answer to "who is this character" (the dump leads, the log fills in,
        // picks widen — `CharacterClasses`, and reading `InferredClass` here was the last
        // place this window could see one class where the character has three) and the
        // WRONG answer to "which classes is this view about". Picks narrow the render and
        // do not narrow `Resolve`, so every class the player had just deselected kept a
        // chip — and clicking one set a lens the render clears again on the same tick
        // (trap 33: two producers, and the dead control is what the second one bought).
        var mine = _offered;
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
                onClick: () => LensTo(cls));
    }

    /// <summary>What pressing a class chip DOES — lifted out of the handler above so the
    /// <c>EQBUDDY_LENSPROBE</c> rendezvous drives the chip's own path instead of a copy of it
    /// (DRA-199). Behaviour is the handler's, unchanged: a second producer of "what the chip
    /// does" is trap 4, and a probe that re-typed these two lines would go on passing on the
    /// day the real click learned a third.
    ///
    /// <para><c>null</c> is the Any chip. The repaint is <see cref="Refresh"/>'s — it reaches
    /// <see cref="ApplyTabVisual"/> through <c>BuildTabs</c>, which is what moves the
    /// selection.</para></summary>
    private void LensTo(string? cls)
    {
        _classLens = cls;
        Refresh(force: true);
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
        // The HOST, which carries both Sky lenses — the view toggle and the repeat check.
        // One assignment for the pair, because the decision is "are we on the Sky tab" and
        // that is a fact about the pair, not about either control (trap 15).
        SkyViewHost.Visibility = _tab == QuestTab.Sky ? Visibility.Visible : Visibility.Collapsed;
        // Unlocks is divided by SECTION, not by class, so the class picker is replaced by
        // a section lens; the state lens is not wired here and an inert filter is worse
        // than an absent one. EVERY CONTROL BELOW IS ASSIGNED EXACTLY ONCE — an earlier
        // cut hid ClassBtn in an `if` that a later unconditional assignment overwrote,
        // which only a screenshot could catch.
        var unlocks = _tab == QuestTab.Unlocks;
        UnlockSectionStrip.Visibility = unlocks ? Visibility.Visible : Visibility.Collapsed;
        StateCombo.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        ClassBtn.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        // The unlock pick's face takes the state combo's column on this tab and only this tab.
        // It is hidden here unconditionally and SHOWN by RefreshUnlockPicker, which is the one
        // place that knows whether there is anything to offer — an empty dropdown is the same
        // trap as an inert filter. EVERY CONTROL IN THIS METHOD IS ASSIGNED EXACTLY ONCE.
        if (!unlocks) UnlockPickBtn.Visibility = Visibility.Collapsed;
        // "scan bags" copies /outputfile inventory, which is not what this tab reads.
        CopyInvBtn.Visibility = unlocks ? Visibility.Collapsed : Visibility.Visible;
        ClassStrip.Visibility = !unlocks && _classes.Count > 1
            ? Visibility.Visible : Visibility.Collapsed;
        FilterRow.Visibility = Visibility.Visible;
        ApplyPanes();
    }

    /// <summary>
    /// **Which of the two panes is on screen, and how wide.** Three arrangements over the
    /// same two children, and the third is the only thing E-3 PR 3 added to this surface:
    ///
    ///  * **Checklist tabs** (Epic / Sky / Unlocks) — nothing to select, so the pane would
    ///    only ever be empty. Its width goes back to the rows. Unchanged since Gate 2.
    ///  * **General, wide** — the list at 400 and the pane taking the rest. Unchanged.
    ///  * **General, single pane** — one of the two at full width, with
    ///    <c>BackToList</c> as the way back. Reached only when a host says so.
    ///
    /// **A collapsed pane keeps its ROW rather than being torn out**, so a resize back is
    /// a width change and not a rebuild — the selection, the scroll position and the
    /// detail tree all survive crossing the threshold in either direction.
    /// </summary>
    private void ApplyPanes()
    {
        var catalog = _tab == QuestTab.General;
        // Leaving the catalog ends the detour. Coming back to a detail pane you opened
        // three tabs ago is a mode nobody remembers being in, and the list is the
        // navigational home of this surface — the selection itself survives, so returning
        // to it is one click rather than a re-find.
        if (!catalog) _paneDetail = false;
        // Single pane only ever applies to the catalog. On a checklist tab the list
        // ALREADY has the whole width, so "collapse to one pane" is a state it is
        // permanently in and the back button would open nothing (trap: an affordance that
        // opens nothing).
        var split = catalog && !SinglePane;
        var detail = catalog && SinglePane && _paneDetail;

        MasterPane.Visibility = detail ? Visibility.Collapsed : Visibility.Visible;
        DetailCard.Visibility = split || detail ? Visibility.Visible : Visibility.Collapsed;
        BackToList.Visibility = detail ? Visibility.Visible : Visibility.Collapsed;

        MasterColumn.Width = split ? new GridLength(400) : detail
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = split || detail
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
    }

    /// <summary>
    /// The room is too narrow to hold a list beside a detail pane —
    /// <c>ShellLayoutPolicy.RoomSinglePane</c>, decided from the window's width in
    /// <c>UI.Shared</c> where a unit test can reach it, and pushed here by the host.
    ///
    /// **The view does not compute this and must not.** The threshold is about the ROOM's
    /// share of the window after the rail, which only the host knows; a view measuring
    /// itself would be a second producer of one answer (trap 33) and the two would
    /// disagree at exactly the boundary where a resize bug lives.
    ///
    /// Default false, so the v1 window — which is 880 wide and never asks — keeps the
    /// arrangement it has always had.
    /// </summary>
    internal bool SinglePane
    {
        get => _singlePane;
        set
        {
            if (_singlePane == value) return;
            _singlePane = value;
            // Widening back to two panes ends the detour: the list is on screen again, so
            // "you are looking at one quest" stops being a mode the player has to leave.
            if (!value) _paneDetail = false;
            ApplyPanes();
        }
    }

    private bool _singlePane;

    /// <summary>True while single-pane is showing the DETAIL rather than the list. Set by
    /// a row click and cleared by <c>BackToList</c> — never by a render, which is what
    /// keeps <see cref="FinishRender"/>'s auto-select from throwing the player into a
    /// detail pane they did not ask for on every refresh.</summary>
    private bool _paneDetail;

    private void OnBackToList(object sender, RoutedEventArgs e)
    {
        _paneDetail = false;
        ApplyPanes();
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

    /// <summary>
    /// The pooled per-creature observations the Unlocks tab's faction movers read (DRA-65) —
    /// the SAME <c>MobHistory.Pool</c> the wiki packs use, through the same memo, so there is
    /// one pooler in the repo and not a second one written for this tab.
    ///
    /// <para><b>Never on a tick.</b> <c>WikiPackPool.Refresh</c> re-folds only when the live
    /// session's mob set actually moves, and this is asked only while the Unlocks tab is the
    /// one being drawn — an idle tab costs nothing, and a tab on another surface costs not
    /// even that.</para>
    /// </summary>
    private readonly WikiPackPool _unlockPool;

    /// <summary>Bumped whenever the pool actually re-folded, so the repaint gate has a short
    /// value that MOVES when the movers do. Trap 72 is the exact bug a new reader invites:
    /// the guidance lines come out of this pool and nothing else in the signature knows it
    /// exists, so a kill that changed what the tab should say would have redrawn nothing.</summary>
    private int _unlockPoolVersion;

    private void RefreshUnlockPool()
    {
        var (character, server) = _main.Identity;
        if (_unlockPool.Refresh(_main.CurrentSnapshot(), character, server, _main.ActiveSessionRowId))
            _unlockPoolVersion++;
    }

    /// <summary>Which section of the Unlocks tab is in view. Session-scoped, like the
    /// class lens and the search box: a sticky filter reads as a broken tracker tomorrow.</summary>
    private string _unlockSection = UnlockLayout.SectionAll;

    /// <summary>
    /// All · Races · Classes, from <see cref="UnlockLayout.Sections"/> — the same producer the
    /// ComboBox read and the same one <see cref="UnlockLayout.InSection"/> filters on, so this
    /// strip cannot offer a section the tab does not render.
    ///
    /// <para>The fourth instance of the one primitive (<c>EqChip</c> / <c>EqSegmentedStrip</c>)
    /// on this surface, beside the tabs, the class lens and the mode strip. DRA-65's Founder
    /// ask names a filter that already existed behind a collapsed dropdown; making it the same
    /// shape as its three neighbours is the whole change, and there is still exactly one list
    /// of section names in the repo (<c>UnlockSectionLensTests</c> asserts that).</para>
    /// </summary>
    private void BuildUnlockSectionStrip()
    {
        _unlockSections = new EqSegmentedStrip(UnlockSectionStrip);
        foreach (var name in UnlockLayout.Sections)
        {
            var section = name;
            _unlockSections.Add(name, name, tip: UnlockSectionTip(section), onClick: () =>
            {
                _unlockSection = section;
                _unlockSections.Select(section);
                Refresh(force: true);
            });
        }
        _unlockSections.Select(_unlockSection);
    }

    private static string UnlockSectionTip(string section) => section switch
    {
        UnlockLayout.RacesHeading => "Only the race unlocks — faction work, read from your faction dump",
        UnlockLayout.ClassesHeading => "Only the class unlocks — Plane of Sky rewards and tasks",
        _ => "Every unlock, races and classes together",
    };

    // ---- which unlocks you are working on (DRA-71 D5, Founder smoke item 7) -------------

    /// <summary>
    /// The pick, on the primitive, beside the section strip.
    ///
    /// <para><b>The section strip and this picker answer different questions, which is why
    /// both are on the row.</b> The strip is a VIEW — races, classes, or both — and is
    /// session-scoped, like every other lens on this window. The pick is an INTENT: "I am
    /// unlocking Iksar and Necromancer", stored per character and shared with the Helper room
    /// through <see cref="UnlockPickStore"/>. Folding one into the other would make a
    /// long-term plan something you re-enter every time you open the window, or make a glance
    /// at the other section something you pay for by editing your plan.</para>
    ///
    /// <para>The rows are rebuilt on every render of this tab rather than once at
    /// construction, because unlike the class lens's fixed sixteen they come from a dump that
    /// arrives lazily off disk and from whatever the section strip is currently showing.</para>
    /// </summary>
    private EqMultiPicker? _unlockPicker;

    /// <summary>What the face said at the last render — reported in the dump, because a face
    /// that stopped tracking the store is exactly the failure a count of rows cannot see.</summary>
    private string _unlockPickFace = "";

    private void BuildUnlockPicker()
    {
        _unlockPicker = new EqMultiPicker(key => ToggleUnlockPick((string)key), UnlockPickBtn);
        UnlockPickBtn.ToolTip = UnlockPickReadout.Tip;
        UnlockPickerHost.Children.Add(_unlockPicker.Host);
    }

    /// <summary>
    /// Re-offer the picker for what is in view, and repaint its face from the store.
    /// </summary>
    /// <param name="offered">The unlocks the SECTION LENS is showing, unnarrowed — the offer
    /// has to hold every row the pick could name here, or a player who picked something and
    /// then changed the lens would see a tick they cannot reach to undo.</param>
    private void RefreshUnlockPicker(
        IReadOnlyList<UnlockProgress> offered, IReadOnlyList<string> picked)
    {
        if (_unlockPicker is null) return;
        UnlockPickBtn.Visibility = offered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (offered.Count == 0) { _unlockPickFace = ""; return; }

        // Closest to done first, then alphabetically — the same ordering the Helper's copy of
        // this picker uses, so one store is offered one way and not two.
        var rows = offered
            .OrderBy(u => u.Complete)
            .ThenByDescending(u => u.Score is { } s && s.Total > 0 ? s.Done / (double)s.Total : -1)
            .ThenBy(u => u.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _unlockPicker.SetRows([.. rows.Select(u => new PickerRow(
            u.Subject, UnlockPickReadout.Row(u), UnlockPickStore.IsPicked(picked, u.Subject)))]);
        // The DEFAULT width budget, not the Helper's roomier one: this face shares its row
        // with the section strip and the mode strip, which is the geometry #184 was about.
        _unlockPickFace = UnlockPickReadout.Face(
            [.. rows.Where(u => UnlockPickStore.IsPicked(picked, u.Subject)).Select(u => u.Subject)],
            rows.Count, PickerFace.MaxChars);
        UnlockPickBtn.Content = _unlockPickFace;
    }

    private void ToggleUnlockPick(string subject)
    {
        var key = _main.QuestCharacterKey;
        if (key.Length == 0) return;
        UnlockPickStore.Toggle(_settings, key, subject);
        _settings.Save();
        Refresh(force: true);
    }

    // ---- multiclass filter (Legends: up to three active classes; David 2026-08-07) ----

    /// <summary>
    /// The class lens, on <see cref="EqMultiPicker"/> since DRA-71 D2.
    ///
    /// <para>It used to be a <c>Popup</c> of <c>CheckBox</c>es typed into this window's XAML,
    /// with the tick bookkeeping and the re-entrancy guard written here. It was the app's ONLY
    /// dropdown multi-select, which is why it is the primitive's first caller: a rule that says
    /// "never hand-build another one" is only true on the day the last hand-built one is gone.
    /// Nothing about the lens changed — same sixteen rows in the same order, same per-character
    /// store, same capped face — and that is the acceptance bar, not a bonus.</para>
    /// </summary>
    private EqMultiPicker? _classPicker;

    private void BuildClassChecks()
    {
        _classPicker = new EqMultiPicker(_ => OnClassCheckChanged(), ClassBtn);
        _classPicker.SetRows([.. QuestClassFilter.Classes
            .Select(cls => new PickerRow(cls, cls, Checked: false))]);
        ClassPickerHost.Children.Add(_classPicker.Host);
    }

    private List<string> SelectedClasses() =>
        _classPicker is null ? [] : [.. _classPicker.Checked.Cast<string>()];

    private void OnClassCheckChanged()
    {
        var selected = SelectedClasses();
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is { } ledger && key.Length > 0)
            ledger.SetClasses(key, selected);
        UpdateClassButton(selected);
        Refresh(force: true);
    }

    // Capped in UI.Shared so the Avalonia window cannot disagree: an uncapped face grew
    // with the selection and pushed the mode strip off the window (#184).
    private void UpdateClassButton(List<string> selected) =>
        _classPicker?.SetFace(
            ClassFilterLabel.For(selected),
            selected.Count > ClassFilterLabel.MaxNamed
                ? "Showing: " + string.Join(", ", selected)
                : "Pick your class(es) — quests any of them can do stay visible");

    /// <summary>Load the character's saved classes into the checkboxes (character
    /// switches included — the selection follows the ledger, not the window).</summary>
    private void SyncClassChecks(List<string> saved)
    {
        var current = SelectedClasses();
        if (current.SequenceEqual(saved, StringComparer.OrdinalIgnoreCase)) return;
        // SetChecked, not SetRows: a rebuild here would throw away a popup the player had
        // open, and the refresh that calls this runs on every tick.
        _classPicker?.SetChecked(key =>
            saved.Contains((string)key, StringComparer.OrdinalIgnoreCase));
        UpdateClassButton(saved);
    }

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

    /// <summary>The Sky tab's Closest to Completion lens (DRA-218). Persisted for its two
    /// siblings' reason — EQBuddy Mobile reads the same setting, so one checklist is ordered
    /// one way on both screens.</summary>
    private void OnSkyClosestToggled(object sender, RoutedEventArgs e)
    {
        _settings.SkyClosestToCompletion = SkyClosestCheck.IsChecked == true;
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

    /// <summary>Called from the v1 window's follow tick; cheap unless the ledger or
    /// filters actually changed (signature idiom, same as the chip windows). The TWO-second
    /// throttle is this window's, unchanged by the lift — it is what made a 2.5 s stillness
    /// guess unsafe (trap 56).</summary>
    public void MaybeRefresh()
    {
        if ((DateTime.Now - _lastRefresh).TotalSeconds >= 2) Refresh(force: false);
    }

    /// <summary>
    /// Paint from the widget's current snapshot, throttle or no throttle — the signature
    /// check inside <see cref="Refresh"/> is what makes it cheap when nothing has moved.
    ///
    /// **The shell's room calls this on every tick rather than <see cref="MaybeRefresh"/>,
    /// and that is a decision rather than an oversight.** The dump's whole contract is that
    /// it describes ONE moment: `PaintOneMoment` asks every open surface that is behind to
    /// paint before a single row count is read, and a 2-second throttle nested inside the
    /// shell's own 1-second one is precisely how a host ends up reporting last tick's
    /// numbers beside this tick's totals (trap 56, which cost the E2E suite four rounds).
    /// The v1 window keeps its throttle because its own tick is the only thing driving it.
    /// </summary>
    internal void PaintNow() => Refresh(force: false);

    /// <summary>The snapshot VERSION this surface last PAINTED — see
    /// <c>CreatureWindow.RenderedVersion</c> for why the dump carries it. Read by whichever
    /// host is following, which is what lets `surfacesBehind` stay an assertion rather than
    /// something a wait has to get lucky about.</summary>
    public long RenderedVersion { get; private set; } = -1;

    private void Refresh(bool force)
    {
        _lastRefresh = DateTime.Now;
        RenderedVersion = _main.CurrentSnapshot().Version;
        var key = _main.QuestCharacterKey;
        var character = key.Length > 0 ? key.Split('_')[0] : "";
        // ONE place this string is composed, and two consumers: this view's own title row
        // (the v1 window) and QuestsRoom's caption (the shell, whose native title bar says
        // "EQBuddy — Guide" and cannot name the character). Two hosts spelling one
        // heading is trap 33's shape, so neither of them spells it.
        var heading = character.Length > 0
            ? $"Quest Tracker — {char.ToUpper(character[0])}{character[1..]}"
            : "Quest Tracker";
        if (heading != Heading)
        {
            Heading = heading;
            HeadingChanged?.Invoke(heading);
        }
        _titleText.Text = heading;

        var owned = _main.QuestLedger?.For(key)
            ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase);
        _owned = owned;
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
        // THE ONE PRODUCER of "which classes this surface is about" (DRA-181 D4, plan P5).
        // This ternary used to be typed here, again in the phone's leftover bands, and a
        // THIRD time in BuildClassStrip as `resolved` alone — so picking three classes left
        // every other resolved class holding a chip that narrowed to nothing.
        _offered = QuestClassLens.Offered(picks, resolved);
        var classes = _offered.ToList();
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

        // BEFORE the signature, because the signature reads its version. The pool is the
        // Unlocks tab's own input and the only expensive one, so it is re-folded exactly when
        // that tab is drawing and never on another tab's tick.
        if (_tab == QuestTab.Unlocks) RefreshUnlockPool();

        var sig = $"{key}|{filter}|{_mode}|st:{_state}|{string.Join("+", classes)}|id:{identity}|{_settings.QuestEraFilter}|{_main.CurrentZoneName}" +
            // THE OFFERED LIST, beside the narrowed one (DRA-181 D4). `classes` above is what
            // the lens left, so with a lens ON it hides a change to the picks: deselect a
            // class you are not lensed to and every term here is unmoved while the strip has
            // a chip to drop. The desktop picker forces a refresh, so this is not about the
            // control the player just touched — the PHONE writes these picks too
            // (`CompanionActions.SetClasses`), and that writer has no way to force anything
            // here. Trap 72 on this surface, with the writer in another room for the second
            // time. Folded by CONTENT: a swap leaves a count unmoved.
            $"|off:{string.Join("+", _offered)}" +
            $"|sel:{_selected}" +
            $"|{string.Join(";", tracked.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", hidden.Order(StringComparer.OrdinalIgnoreCase))}" +
            $"|{string.Join(";", completed.Select(kv => $"{kv.Key}:{kv.Value}"))}" +
            $"|{string.Join(",", owned.Select(kv => $"{kv.Key}:{kv.Value.Total}"))}" +
            // The leftover bands are a join against the DUMP, and nothing else in this
            // signature moves when a newer one is read — so without this the bands would
            // keep answering from the dump that was current when the window opened.
            $"|inv:{_main.LatestInventory()?.WrittenAt.Ticks ?? 0}" +
            // THE CHECKLISTS' OWN TICKS. Nothing else here moves when the loot auto-tick
            // writes a box: `owned` is the quest ledger and `completed` is turn-ins, and
            // neither is where SkyLootAutoCheck / EpicLootAutoCheck put an acquired item.
            // So the store said "you have the Stone Amulet" and this tab went on drawing
            // the moment before — measured in E2E, not theorised.
            $"|ck:{ChecklistTickSignature()}" +
            // THE SKY TAB'S TWO LENSES — the Class/Island toggle (DRA-164) and the repeat
            // choice beside it. Trap 72 BY NAME, in the commit the island reader lands in:
            // nothing else in this signature moves when either is flipped, and each one
            // changes every row on the tab.
            //
            // Their own chips force a refresh, so this is not about the control the player
            // pressed — it is about the OTHER instance. QuestsWindow and QuestsRoom each
            // build their own QuestsView (trap 45) over ONE AppSettings, so switching to
            // Island view in the shell left the window drawing the class view until something
            // unrelated moved. Exactly the fold's story, one store later; the repeat flag has
            // had the same hole since it shipped and gets closed here because DRA-164 gave it
            // its second reader.
            //
            // The completion lens is the THIRD digit and joins for exactly the same reason
            // (DRA-218): it is a profile-level setting with two writers — this window and the
            // shell's Guide room — and it reorders every group and every island row on the
            // tab without touching a tick, a fold or a pick.
            $"|sky:{(_settings.SkyGroupByIsland ? 1 : 0)}{(_settings.SkyStepsUnderEveryIsland ? 1 : 0)}"
            + $"{(_settings.SkyClosestToCompletion ? 1 : 0)}" +
            // THE FOLD. Every guided group on all three tabs reads AppSettings.GuideExpanded
            // to decide whether it starts open, and nothing else in this signature moves when
            // it does. The fold control's own click forces a refresh, so this is not about
            // that button — it is about the OTHER instance: QuestsWindow and QuestsRoom build
            // their own QuestsView (trap 45), so folding a quest in the shell left the window
            // drawing it open until something unrelated moved. Trap 72, one store later.
            $"|gx:{_settings.GuideExpanded.Count}."
            + $"{string.Join(";", _settings.GuideExpanded.Order(StringComparer.OrdinalIgnoreCase)).GetHashCode(StringComparison.Ordinal):x8}"
            // THE UNLOCKS TAB'S OWN STORES, and only while that tab is the one drawing — the
            // pool fold is the one thing here that is not free, and no other tab reads any of
            // it. Trap 72, third time on this surface: the guidance lines (DRA-65) are built
            // from a faction dump, a pooled kill history and a section lens, and not one of
            // those moved anything above. `ck:` already carries the Sky ticks and turn-ins the
            // piece count reads, so those are deliberately not repeated here.
            // THE HELPER'S OWN EVIDENCE (DRA-83). A guide row carries what the Helper says
            // about the subject its step points at, and NOTHING else in this signature moves
            // when that answer does: an archived session, a fresh inventory dump or tonight's
            // kills change an XP rate or a dominance result without touching a tick, a fold or
            // a pick. Trap 72 for the fifth time on this surface, and the call is also what
            // REBUILDS the answers — the memo's own signature decides whether the engines run
            // (see GuideHelperSource), so asking is a string join and not a fold.
            //
            // Not on the Unlocks tab: it draws no guide rows, and this is the one term here
            // whose refresh reads the database.
            + (_tab == QuestTab.Unlocks ? "" : $"|hlp:{_helper.Refresh()}")
            + (_tab == QuestTab.Unlocks
                ? $"|un:{_unlockSection}|fac:{_main.Unlocks.Factions?.WrittenAt.Ticks ?? 0}"
                  + $"|ach:{(_main.Unlocks.HasAchievements ? 1 : 0)}|pool:{_unlockPoolVersion}"
                  // THE PICK (DRA-71 D5), folded by CONTENT and not by count — a swap leaves a
                  // count unmoved. It is written by the OTHER surface as often as by this one:
                  // the Helper room shares this store, and a pick made there while this window
                  // sits open on the Unlocks tab has to land here. Trap 72 on this surface for
                  // the fourth time, and the first where the writer is in another room.
                  + $"|pk:{string.Join(";", UnlockPickStore.Picked(_settings, _main.QuestCharacterKey).Order(StringComparer.OrdinalIgnoreCase))}"
                : "");
        if (!force && sig == _signature) return;
        _signature = sig;

        // Past the gate, so this counts REBUILDS and not ticks — see the field.
        _renders++;
        QuestsPanel.Children.Clear();
        _lastGuideCards.Clear();
        // What the last render DREW, cleared with the panel it drew into: a dump that kept
        // reporting an island layout after a switch back to class view would be describing a
        // screen that is no longer there (trap 38's shape — the memo must record what the last
        // message CARRIED).
        _lastIslandLayout = null;
        _lastIslandRowTitle = "";
        _lastIslandRowOwner = "";
        _lastBlockedNote = "";
        _lastFirstGroupHeading = "";
        _lastFirstGroupRemaining = -1;
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

    /// <summary>Facts about this surface for the <c>EQBUDDY_EXPAND</c> dump, asserted from
    /// tests/EQBuddy.E2E. The WPF layer has no unit tests (docs/TestPlan.md §5), so a
    /// launched app reporting its own structure is the only cover the Gate 2 rebuild can
    /// have — and the rebuild's whole claim is structural: a LIST of rows, one of them
    /// SELECTED, and a detail pane that is not empty beside it.
    ///
    /// **These keys did not change in the lift, deliberately.** The window reports them
    /// verbatim, so every assertion written against them still means what it meant; the
    /// shell asks this same method for this same string and re-keys it mechanically under
    /// <c>shellQuests*</c> (<see cref="ShellDumpFacts"/>), because the dump is one flat
    /// namespace and two hosts of one surface would otherwise write over each other —
    /// trap 4 with the two sources being two hosts (trap 58).</summary>
    internal string DebugFacts() =>
        // WHICH tab, in the SURFACE's own vocabulary (Core's QuestSurface.KeyFor) rather
        // than a name invented here. Added in E-3 PR 3 because the shell addresses this
        // room by `quests:<key>` and nothing in the dump could say where it landed — the
        // same key every other room has reported since PR 1, and its absence here was a
        // gap rather than a decision.
        $"questsTab={QuestSurface.KeyFor(_tab)} " +
        $"questsRows={_rows.Count} " +
        $"questsSuppressed={_suppressed} " +
        $"questsSelected={(_selected.Length > 0 ? 1 : 0)} " +
        $"questsDetailBlocks={DetailPane.Children.Count} " +
        $"questsDetailShown={(DetailCard.Visibility == Visibility.Visible ? 1 : 0)} " +
        // The LIST's own visibility beside the pane's. With one key you cannot tell the
        // wide arrangement (both) from single-pane-showing-detail (one), and those are the
        // two states the whole axis is about.
        $"questsListShown={(MasterPane.Visibility == Visibility.Visible ? 1 : 0)} " +
        $"questsSinglePane={(SinglePane ? 1 : 0)} " +
        // The way BACK, counted off the real control rather than from the flag that set
        // it: an absent control photographs as an unremarkable panel (trap 29), and a
        // single pane you cannot leave is the failure worth asserting.
        $"questsBackShown={(BackToList.Visibility == Visibility.Visible ? 1 : 0)} " +
        $"questsReadySummary={(SummaryRow.Visibility == Visibility.Visible ? 1 : 0)} " +
        $"questsTabs={_tabs.Count} " +
        $"questsModes={_modes.Count} " +
        // ---- the CLASS LENS strip (DRA-181 D4) ----------------------------------------
        // WHICH chips the strip is offering, folded from the real strip's KEYS rather than
        // counted: a count is unmoved by a swap (trap 72), and the Founder's fault was a
        // strip of the RIGHT SIZE holding the wrong classes as often as it was one chip too
        // many. The key is what a click sets the lens to, so this says the control is live
        // as well as present — a chip naming a class the render has narrowed away is the
        // dead control this slice removed.
        //
        // ABBREVIATED, as the chip itself reads: "Shadow Knight" carries a SPACE, and the
        // dump is space-separated `key=value` — a raw class name would silently corrupt the
        // pair after this one. "-" is "no chips at all", which is exactly the collapsed
        // strip (the one-class case returns before adding any), and it has to be a sentinel
        // because an empty value would corrupt the line the same way.
        $"questsClassStrip={ClassStripFact()} " +
        // WHICH of those chips is lit, from the SAME moment as the list above (trap 56) —
        // "the chip went away" and "the selection landed somewhere real" are two claims and
        // reading them from two dumps would be reading them from two renders. See
        // ClassLensFact for why it is off the strip and not off `_classLens`.
        $"questsClassLens={ClassLensFact()} " +
        // ---- the SKY tab's island view (DRA-164) --------------------------------------
        // The MODE as the setting holds it, and the STRIP that offers it — counted off the
        // real strip rather than from the list that built it, because the claim is that the
        // chips reached the screen and an absent control photographs as an unremarkable
        // panel (trap 29). `questsSkyViewShown` is the host's own visibility, which is what
        // says the pair is on the Sky tab and nowhere else.
        $"questsSkyIsland={(_settings.SkyGroupByIsland ? 1 : 0)} " +
        $"questsSkyRepeat={(_settings.SkyStepsUnderEveryIsland ? 1 : 0)} " +
        $"questsSkyViewChips={_skyView.Count} " +
        $"questsSkyViewShown={(SkyViewHost.Visibility == Visibility.Visible ? 1 : 0)} " +
        // ---- Closest to Completion (DRA-218) ------------------------------------------
        // THE SETTING and THE CONTROL, from one moment (trap 56) — a lens that reached the
        // store and no checkbox is trap 20's shape, and the checkbox is the only door this
        // lens has.
        $"questsSkyClosest={(_settings.SkyClosestToCompletion ? 1 : 0)} " +
        $"questsSkyClosestBox={(SkyClosestCheck.IsChecked == true ? 1 : 0)} " +
        // THE ORDER THE SCREEN IS IN, which is the whole acceptance bar and the one thing
        // no setting can assert: the first group's heading in the order it was drawn. A
        // blocked reward leading this value is the failure S23 AC 8 names.
        $"questsSkyFirstGroup={Dumped(_lastFirstGroupHeading)} " +
        $"questsSkyFirstRemaining={_lastFirstGroupRemaining} " +
        // The blocked sentence that reached the PANEL — not the one BlockedNote answered,
        // which is the store's claim rather than the screen's (trap 56).
        $"questsSkyBlockedNote={Dumped(_lastBlockedNote)} " +
        // WHAT THE LAST RENDER DREW, from ONE moment (trap 56): the setting says the player
        // asked for the island view, and these say the screen actually built one. -1 is
        // "this render drew no island layout at all", which is a different claim from "it
        // drew an empty one" and is the state a stale memo would hide (trap 38).
        $"questsIslandGroups={_lastIslandLayout?.Groups.Count ?? -1} " +
        // The first heading, which is the ORDERING claim — "the lowest island leads" is the
        // whole of "before moving to the next" and nothing else in this dump can say it.
        // SPACES BECOME UNDERSCORES because the dump is space-separated `key=value` and a
        // value containing a space would silently corrupt the pair after it, not this one.
        $"questsIslandFirst={(_lastIslandLayout?.Groups.FirstOrDefault()?.Heading ?? "-").Replace(' ', '_')} " +
        $"questsIslandRows={_lastIslandLayout?.Groups.Sum(g => g.Rows.Count) ?? -1} " +
        // The two exclusions, so a test can assert they FIRED rather than that a sentence
        // exists — the counts are what the sentences are about.
        $"questsIslandHiddenTurnIns={_lastIslandLayout?.HiddenTurnIns ?? -1} " +
        $"questsIslandHiddenRewards={_lastIslandLayout?.HiddenRewards ?? -1} " +
        // THE CLASS PREFIX, as the row that reached the panel actually carries it (Founder
        // CLARIFY 2026-09-17, plan P8). Taken from what RenderIslandView PASSED to the control
        // it added, not from the layout a moment later — "Core produced a prefixed title" and
        // "the screen drew one" are different claims (trap 56), and the second is the one the
        // Founder can see. The owner rides beside it so a test can assert the class is said
        // ONCE: the pair is the whole row, and a class in both halves is the redundancy P8
        // moved the label to avoid.
        $"questsIslandRowTitle={Dumped(_lastIslandRowTitle)} " +
        $"questsIslandRowOwner={Dumped(_lastIslandRowOwner)} " +
        // ---- the UNLOCKS tab (DRA-65) -------------------------------------------------
        // The section lens, counted off the STRIP rather than from UnlockLayout.Sections:
        // the claim is that the chips reached the screen, and a count taken from the list
        // that produced them cannot fail the way the screen can. It is also the only thing
        // that can say the ComboBox is gone — an inert control photographs as an
        // unremarkable panel (trap 29).
        $"questsUnlockSections={_unlockSections.Count} " +
        $"questsUnlockSection={_unlockSection} " +
        // WHAT THE LAST RENDER HELD, not what the stores hold now — and the difference is the
        // whole point. Both dumps are found and parsed lazily off disk (UnlockSource re-reads
        // them when their timestamps move), so "the app has a faction dump" and "the rows on
        // screen were drawn from one" are different claims, and a test that waits for the
        // first is asserting against whichever render happened to come after. That is trap 56
        // exactly: the store says so and the screen says so are two facts, and a wait needs
        // the one it is actually a precondition for.
        $"questsUnlockDrewFactions={(_unlockDrewFactions ? 1 : 0)} " +
        // The pool's own version beside the sentences it produced. It moves only when the
        // fold actually re-ran, which is what makes it the repaint gate's input (trap 72) and
        // a readable answer to "did the store move, or only the screen".
        $"questsUnlockPool={_unlockPoolVersion} " +
        // How many times this surface REBUILT (past the repaint gate) — the liveness question
        // beside every value one (trap 56). It is what lets a test wait for the panel to stop
        // redrawing on its own before claiming that its own append is what redrew it.
        $"questsRenders={_renders} " +

        // The unlock ROWS on screen, and the guided sentences under them, counted off the
        // real visual tree by the Tag each carries (trap 39). Both, from one moment: "the
        // resolver found movers" and "the movers are on the page" are different claims
        // (trap 56), and the rows are the floor under the guidance — "no guided lines" over
        // an empty tab is the vacuous pass this assertion is most likely to become.
        $"questsUnlockRows={PanelElements().OfType<Grid>().Count(g => g.Tag as string == UnlockRowTag)} " +
        $"questsUnlockGuided={PanelElements().OfType<TextBlock>().Count(t => t.Tag as string == UnlockGuideTag)} " +
        $"questsUnlockDoors={PanelElements().OfType<Button>().Count(b => b.Tag as string == UnlockDoorTag)} " +
        // ---- the pick, and what P12 did to the row (DRA-71 D5) -------------------------
        // THE STORE, THE CONTROL AND THE SCREEN, all from one Build (trap 56). `Picks` is what
        // settings.json holds for this character — the SAME string the Helper room dumps under
        // `helperUnlockPicks`, which is how an E2E proves one store rather than two agreeing
        // by luck. `PickRows` is what the popup offered and `PickFace` what the button said;
        // a pick that reached the store and no control is trap 20's shape, and a popup with
        // rows in it photographs as an ordinary button either way (trap 29).
        $"questsUnlockPicks={string.Join(",", UnlockPickStore.Picked(_settings, _main.QuestCharacterKey).Select(p => p.Replace(" ", "")))} " +
        $"questsUnlockPickRows={_unlockPicker?.RowCount ?? 0} " +
        $"questsUnlockPickFace={_unlockPickFace.Replace(" ", "")} " +
        $"questsUnlockPickShown={(UnlockPickBtn.Visibility == Visibility.Visible ? 1 : 0)} " +
        // How many unlocks the pick HELD BACK from the sections on screen. The screen's answer
        // to "did the filter fire", beside `questsUnlockRows` which is what survived it.
        $"questsUnlockHidden={_unlockHidden} " +
        // P12's two halves: the pointer lines drawn, and the rows carrying the prose that used
        // to be under them. See UnlockWhoWhereTag and _unlockHovers for why these are two keys.
        $"questsUnlockWhoWhere={PanelElements().OfType<TextBlock>().Count(t => t.Tag as string == UnlockWhoWhereTag)} " +
        $"questsUnlockHovers={_unlockHovers} " +
        // The Sky tab's ⧉ copy of /outputfile achievements. Counted off the real visual
        // tree rather than from a flag, for the same reason gearCopyCmd exists: an absent
        // control photographs as an unremarkable panel (trap 29), and a bool that nobody
        // resets goes stale without anything noticing.
        $"questsSkyCopyCmd={SkyCopyCommandsOnScreen(GameCommands.OutputfileAchievements)} " +
        // The Sky tab's own ⧉ of /outputfile inventory (Hateborne, 2026-09-03): the tab
        // is fed by two dumps and only ever named one. Counted the same way, for the
        // same trap-29 reason.
        $"questsSkyInvCopyCmd={SkyCopyCommandsOnScreen(GameCommands.OutputfileInventory)} " +
        // The three #243/#129 bands, counted off the REAL visual tree by the Tag each one
        // carries — not from a flag the render sets, which goes stale without anything
        // noticing, and not from the heading string, which is a name and not an identity
        // (trap 39). 0 is the honest answer for "no dump" and for "nothing leftover" —
        // and for a FOLDED band, whose open flag is dumped beside it so the two states
        // cannot be confused.
        $"questsSkyReady={BandRowsOnScreen("skyReady")} " +
        $"questsSkyLeftoverA={BandRowsOnScreen("skyLeftoverA")} " +
        $"questsSkyLeftoverB={BandRowsOnScreen("skyLeftoverB")} " +
        $"questsSkyReadyOpen={(BandOpen("skyReady") ? 1 : 0)} " +
        $"questsSkyLeftoverAOpen={(BandOpen("skyLeftoverA") ? 1 : 0)} " +
        $"questsSkyLeftoverBOpen={(BandOpen("skyLeftoverB") ? 1 : 0)} " +
        // The guided surface, counted off the REAL visual tree by the Tag each element
        // carries (trap 39) rather than from the projection's own return — the question
        // these answer is "did the rows reach the screen", and a count taken from the thing
        // that produced them cannot fail the way the screen can.
        // The CLASSIC checklist's own rows — every tickable box that is not a guide step.
        // The floor under the guide facts: "no guide chrome" over an empty tab is the
        // vacuous pass an unguided-class assertion is most likely to become.
        $"questsSkyRows={ClassicRowsOnScreen()} " +
        // Counted off the FOLD control, not the caption: the caption is now suppressed when
        // it would only repeat the heading (Bevel's SIGNED one-liner), so it is no longer one
        // per guided group. The fold control is — every guided group draws exactly one,
        // folded or open — which is what makes it the group's identity on screen (trap 39).
        $"questsGuideGroups={GuideElementsOnScreen<Button>(GuideFoldTag)} " +
        // The EPIC tab's own guided-group count (Delivery 3). questsGuideGroups is
        // tab-agnostic — it counts fold controls wherever they are — so an assertion that
        // never switched tabs could read the Sky number and call it an Epic pass. This is
        // zero on every other tab by construction, which is what makes it an Epic fact.
        $"questsEpicGuideGroups={(_tab == QuestTab.Epic ? GuideElementsOnScreen<Button>(GuideFoldTag) : 0)} " +
        // ...and the caption counted SEPARATELY, because "how many groups drew one" is now a
        // real question with a real answer. It is drawn only where it adds stubs or skipped,
        // so a class with neither shows six headings and NO caption lines — an assertion that
        // could not exist while the two were the same tag.
        $"questsGuideCaptions={GuideElementsOnScreen<TextBlock>(GuideCaptionTag)} " +
        $"questsGuideRows={GuideRowsOnScreen().Count()} " +
        $"questsGuideStubs={GuideStubsOnScreen()} " +
        $"questsGuideDone={GuideRowsOnScreen().Count(c => c.IsChecked == true)} " +
        $"questsGuideImprove={GuideImproveDoorsOnScreen()} " +
        // THE HELPER'S LINES, from BOTH ENDS OF ONE MOMENT (DRA-83, trap 56). The first is what
        // the producer answered — every reference in the catalog the Helper had something to say
        // about; the second is how many rows on this tab are drawing one. "The engine says so"
        // and "the screen says so" are different claims, and the gap between these two numbers
        // is the one a repaint bug lives in.
        $"questsHelperAnswered={_helper.Lines.Answered} " +
        $"questsHelperLines={GuideHelperLinesOnScreen()} " +
        // The card, counted off the real tree. questsGuideNext is the LENGTH of the next
        // step's row id and never the text: the E2E compares dumps, and an id with a space
        // in it would split the flat namespace (trap 58). Length moves when the step moves,
        // which is the whole assertion.
        $"questsGuideCards={GuideElementsOnScreen<Border>(GuideCardTag)} " +
        $"questsGuideNext={NextRowIdLengths()} " +
        $"questsGuideSkipped={GuideRowsOnScreen().Count(c => Struck(c))} " +
        // ---- the GENERAL tab's guide (DRA-46) ----------------------------------------
        // Counted off DetailPane and not QuestsPanel, which is why these are separate keys
        // rather than the ones above growing a second source. Every guide fact up to here
        // sweeps the LIST panel; the General tab's walkthrough is in the DETAIL pane, so a
        // questsGuideRows assertion on this tab would read 0 and pass for the wrong reason.
        $"questsGeneralGuide={(DetailPaneElements().Any(e => e.Tag as string == GeneralGuideTag) ? 1 : 0)} " +
        // The projection's own answer beside the screen's: "the store says this quest has a
        // guide" and "the guide reached the pane" are different claims, and one moment (trap
        // 56). questsGeneralGuide is the screen; this is the store.
        $"questsGeneralGuideId={(_generalGuide?.GuideId.Length ?? 0)} " +
        $"questsGeneralGuideStages={DetailGuideElements<TextBlock>(GeneralGuideStageTag)} " +
        $"questsGeneralGuideRows={DetailGuideRows().Count()} " +
        // THE N2 CLAIM, and the one a row count cannot make: the turn-in pieces are the item
        // rows this pane already drew, so a guided quest with four pieces shows four TAGGED
        // item rows and no fifth checkbox beside them. If a future change draws the Collect
        // steps as guide rows instead, this goes to 0 and questsGeneralGuideRows jumps.
        $"questsGeneralGuideItemRows={DetailGuideElements<Border>(GeneralGuideItemTag) + DetailGuideElements<Border>(GeneralGuideItemMetTag)} " +
        // ...and how many of them the BAGS have satisfied. The "a Collect row lights when the
        // owned count reaches Need" claim, counted off the screen — questsGeneralGuideDone
        // reads checkboxes and a piece row has none, so without this the claim has no number.
        $"questsGeneralGuidePiecesMet={DetailGuideElements<Border>(GeneralGuideItemMetTag)} " +
        $"questsGeneralGuideDone={DetailGuideRows().Count(c => c.IsChecked == true)} " +
        $"questsGeneralGuideCards={DetailGuideElements<Border>(GuideCardTag)} " +
        $"questsGeneralGuideImprove={DetailPaneElements().OfType<Button>().Count(b => b.Tag as string == GuideImproveTag)} " +
        $"questsGeneralGuideFolded={(_generalGuide is { Collapsed: true } ? 1 : 0)}";

    /// <summary>Everything in the detail pane, one level of container included — the pane's
    /// answer to <see cref="PanelElements"/>, and it has to go two levels rather than one:
    /// the guide block is a StackPanel of its own inside the pane, and a guide row with its
    /// share-back door is a Grid inside that.</summary>
    private IEnumerable<FrameworkElement> DetailPaneElements() =>
        DetailPane.Children.OfType<FrameworkElement>()
            .SelectMany(e => e is Panel p
                ? p.Children.OfType<FrameworkElement>()
                    .SelectMany(c => c is Grid g
                        ? g.Children.OfType<FrameworkElement>().Prepend(c)
                        : [c])
                    .Prepend(e)
                : [e]);

    private int DetailGuideElements<T>(string tag) where T : FrameworkElement =>
        DetailPaneElements().OfType<T>().Count(e => e.Tag as string == tag);

    private IEnumerable<CheckBox> DetailGuideRows() =>
        DetailPaneElements().OfType<CheckBox>().Where(c => c.Tag as string == GuideRowTag);

    /// <summary>
    /// The SUM of every drawn card's next-row-id length.
    ///
    /// <para>A sum rather than the first card's, because group ORDER is not stable across a
    /// tick: the Sky layout sorts by how close each reward is to done, so ticking a step can
    /// move its whole group up the page. "The first card's next step" would then change for
    /// two different reasons and the assertion could not tell them apart. A sum is
    /// order-independent and still moves when any one card moves.</para>
    ///
    /// <para>Lengths, never the ids: the dump is one flat space-separated namespace
    /// (trap 58). Read from the projection's own answer, because the card deliberately does
    /// not print the id anywhere a test could read back.</para></summary>
    private int NextRowIdLengths() => _lastGuideCards.Sum(c => c.RowId.Length);

    private static bool Struck(CheckBox row) =>
        row.Content is StackPanel p
            ? p.Children.OfType<TextBlock>().Any(t => t.TextDecorations?.Count > 0)
            : row.Content is TextBlock t2 && t2.TextDecorations?.Count > 0;

    /// <summary>
    /// Which boxes are ticked, folded to one short value for the repaint gate.
    ///
    /// <para>A COUNT would not do it: unticking one row and ticking another in the same tick
    /// leaves the count where it was, and the screen would keep the stale pair. So the fold
    /// is order-independent over the ids that are actually ticked — two different sets give
    /// two different values, and the same set always gives the same one (trap 8's rule from
    /// the other side: a fingerprint must include everything that decides the picture, and
    /// nothing that merely drifts).</para>
    ///
    /// <para>Cheap on purpose — this runs on every repaint gate, and the checklists are a few
    /// hundred rows.</para>
    /// </summary>
    private string ChecklistTickSignature()
    {
        var sky = 0;
        var skyOn = 0;
        foreach (var item in _settings.SkyQuestChecklist)
            if (item.Acquired) { sky ^= item.Id.GetHashCode(StringComparison.Ordinal); skyOn++; }

        var epic = 0;
        var epicOn = 0;
        foreach (var item in _settings.EpicQuestChecklist)
            if (item.Acquired) { epic ^= item.Id.GetHashCode(StringComparison.Ordinal); epicOn++; }

        // AND THE GUIDE LEDGER. The active-step card reads the skip list to decide which
        // step is next, and a skip lives in neither list above — so without this the player
        // would press Skip and the card would keep naming the step they just struck out.
        // Trap 72, one surface later: when you add a READER of a store, put that store in
        // what makes the surface redraw.
        var guide = 0;
        var guideOn = 0;
        if (_main.QuestLedger is { } ledger && _main.QuestCharacterKey is { Length: > 0 } key)
            foreach (var guideId in ledger.GuidesTouchedBy(key))
            {
                var progress = ledger.GuideProgressFor(key, guideId);
                foreach (var id in progress.SkippedObjectiveIds.Concat(progress.DoneObjectiveIds))
                {
                    guide ^= (guideId + "/" + id).GetHashCode(StringComparison.Ordinal);
                    guideOn++;
                }
            }

        // The turn-in stores too: a reward marked complete on the phone or by the
        // achievements import changes what this tab draws and lives in neither list above.
        return $"{skyOn}.{sky:x8}/{epicOn}.{epic:x8}"
            + $"/{_settings.SkyQuestCompleted.Count}.{_settings.EpicQuestCompleted.Count}"
            + $"/g{guideOn}.{guide:x8}";
    }

    /// <summary>
    /// The active-step card. Every string comes from the projection already worded — this
    /// method decides layout and nothing else, which is what keeps the phone's version of the
    /// same card saying the same thing.
    ///
    /// <para>Only the questions the step ANSWERS get a line. An empty "Where:" label reads as
    /// a broken card, and most steps outside Plane of Sky will answer three of the six.</para>
    /// </summary>
    private UIElement GuideCardView(
        QuestChecklistGroup group, QuestChecklistCard card,
        Dictionary<string, Action<bool>> setters, bool locked)
    {
        var body = new StackPanel();
        var border = new Border
        {
            Child = body,
            Padding = new Thickness(DesignTokens.SpaceM),
            Margin = new Thickness(DesignTokens.SpaceXxs, 0, 0, DesignTokens.SpaceS),
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Tag = GuideCardTag,
        };
        border.SetResourceReference(Border.BackgroundProperty, "CardBrush");

        // No next step: the card says which of the two finished states this is and offers no
        // verbs. A Done button with nothing to do is the silent no-op rule inverted.
        if (card.RowId.Length == 0)
        {
            body.Children.Add(Line(Role.Body, card.Instruction, "DimBrush"));
            return border;
        }

        body.Children.Add(Line(Role.Caption, GuidePresentation.NextLead, "AccentBrush"));
        var lead = Line(Role.TitleSection, card.Instruction, "TextBrush");
        lead.FontWeight = FontWeights.SemiBold;
        body.Children.Add(lead);

        if (card.StubNote.Length > 0)
        {
            // The banner REPLACES where/what — the projection already blanked them.
            var stub = Line(Role.Body, GuidePresentation.StubLead + " " + card.StubNote, "WarnBrush");
            stub.Tag = GuideStubTag;
            body.Children.Add(stub);
        }
        // Sentences, not labelled fields (David, 2026-09-09) — and the detail line only when
        // it says something the instruction above did not.
        foreach (var line in new[] { card.Directions, card.Detail })
            if (line.Length > 0) body.Children.Add(Line(Role.Body, line, "DimBrush"));

        if (card.Why.Length > 0) body.Children.Add(Line(Role.Caption, card.Why, "DimBrush"));
        if (card.BeforeLeaving.Length > 0)
            body.Children.Add(Line(Role.Caption, card.BeforeLeaving, "WarnBrush"));

        // A step the BAGS answer says so, in place of the button it does not get. Without
        // this the card offered "Done" on a step the router refuses — the single most
        // prominent control the guide has, doing nothing at all (DRA-46).
        if (card.Held.Length > 0)
            body.Children.Add(Line(Role.Body, card.Held, "AccentBrush"));

        var verbs = new WrapPanel { Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0) };
        // A class whose epic is marked complete has LOCKED rows, and the card's Done is the
        // same write through the same setter — so it locks with them, or it is a button that
        // ticks a box the master check's undo will silently discard. Its own tooltip says
        // why, because "silent no-ops are broken" has a second half: a control that looks
        // live and is not (trap 17).
        var lockNote = locked
            ? $"{group.ClassName}'s epic is marked complete. Reopen it above to change "
              + "individual steps."
            : null;
        // ...and it gets no Done verb at all. Not a disabled one: there is nothing the player
        // could do to enable it here, and the line above already says what would.
        if (card.Held.Length == 0)
            verbs.Children.Add(CardVerb(GuidePresentation.DoneLabel, "EqPrimaryButton", lockNote, () =>
            {
                if (setters.TryGetValue(card.RowId, out var set)) { set(true); Save(); }
            }, locked));
        // SKIP survives a held step — "I am not doing this" has no home in the bags and lands
        // in the guide ledger like every other skip (the router's deliberate asymmetry).
        verbs.Children.Add(CardVerb(GuidePresentation.SkipLabel, "ActionButton",
            lockNote ?? GuidePresentation.SkipTip,
            () => SkipGuideRow(group, card.RowId), locked));
        if (card.ImproveUrl.Length > 0)
        {
            var improve = DesignSystem.InlineIconButton("Pencil", GuidePresentation.ImproveTip,
                (_, _) => OpenUrl(card.ImproveUrl));
            improve.Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
            improve.Tag = GuideImproveTag;
            verbs.Children.Add(improve);
        }
        body.Children.Add(verbs);
        return border;

        TextBlock Line(Role role, string text, string ink)
        {
            var t = DesignSystem.Text(role, text);
            t.TextWrapping = TextWrapping.Wrap;
            t.Ink(ink);
            return t;
        }
    }

    private Button CardVerb(string label, string style, string? tip, Action act, bool locked = false)
    {
        var b = new Button
        {
            Style = (Style)FindResource(style),
            Content = label,
            Margin = new Thickness(0, 0, DesignTokens.SpaceS, 0),
            ToolTip = tip,
            Tag = GuideVerbTag,
        };
        if (locked)
        {
            b.IsEnabled = false;
            // …AND LOOK disabled, the same pairing the locked rows carry: the button styles
            // have no disabled visual, so IsEnabled alone leaves a control that reads as live
            // and quietly ignores the click.
            b.Opacity = 0.5;
        }
        b.Click += (_, _) => act();
        return b;
    }

    /// <summary>Strike a guide step out, through the router — never the ledger directly, which
    /// is what the one-writer source scan is looking for.</summary>
    private void SkipGuideRow(QuestChecklistGroup group, string rowId)
    {
        if (_main.QuestLedger is not { } ledger) return;
        if (GuideChecklistProjection.Resolve(GuideCatalog.Default, rowId)
            is not var (guide, objective)) return;
        var already = GuideProgressRouter.IsSkipped(
            ledger, _main.QuestCharacterKey, guide.Id, objective);
        GuideProgressRouter.SetSkipped(
            ledger, _main.QuestCharacterKey, guide.Id, objective, !already);
        Save();
    }

    private void Save()
    {
        _settings.Save();
        Refresh(force: true);
    }

    /// <summary>The cards this render drew, in order. Cleared at the top of every render
    /// beside the panel itself, so it can never report a card that is no longer on screen —
    /// a flag nobody resets is the failure the Tag-counted facts avoid, and this one is
    /// reset with the thing it describes.</summary>
    private readonly List<QuestChecklistCard> _lastGuideCards = [];

    /// <summary>
    /// The fold control for one guided quest. Its own row under the caption rather than a
    /// glyph on the heading, because the heading is already a link to the wiki page and a
    /// control that does two things on one click is how a surface gets a silent no-op.
    /// </summary>
    private UIElement FoldToggle(QuestChecklistGroup group)
    {
        var b = new Button
        {
            Style = (Style)FindResource("ActionButton"),
            Content = GuidePresentation.FoldFace(group.Collapsed),
            FontSize = DesignTokens.Spec(Role.Body).Size,
            MinWidth = DesignTokens.IconButtonSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            // Centred on the heading's own line and carrying its vertical margins, so the
            // glyph sits level with the name it opens rather than under it (David,
            // 2026-09-09).
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL,
                0, DesignTokens.SpaceXs),
            // The words the face dropped live here, where they cost no width.
            ToolTip = GuidePresentation.FoldTip(group.Collapsed),
            Tag = GuideFoldTag,
        };
        // The reward key on Sky, the guide id on Epic — from the projection, which is what
        // READS the same list to decide whether this group starts open. Spelled here as
        // `group.CompletionKey ?? ""` it returned early on every Epic group and the "+" was a
        // silent no-op (trap 20's shape: the control was there, the write path was not).
        var key = GuideChecklistProjection.FoldKey(group);
        b.Click += (_, _) =>
        {
            if (key.Length == 0) return;
            // Stored as the EXPANDED exception: guided quests start folded, so an opt-out
            // list would gain 95 entries the first time anyone scrolled, and a newly
            // authored class would arrive open.
            var removed = _settings.GuideExpanded.RemoveAll(
                k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) _settings.GuideExpanded.Add(key);
            Save();
        };
        return b;
    }

    /// <summary>The tag a guided group's caption line carries.</summary>
    private const string GuideCaptionTag = "guideCaption";
    /// <summary>The tag a guided quest's fold control carries.</summary>
    private const string GuideFoldTag = "guideFold";
    /// <summary>The tag the active-step card's border carries.</summary>
    private const string GuideCardTag = "guideCard";
    /// <summary>The tag each of the card's two verbs carries.</summary>
    private const string GuideVerbTag = "guideVerb";
    /// <summary>The tag every guide objective's row carries.</summary>
    private const string GuideRowTag = "guideRow";
    /// <summary>The tag a stub row's "Wiki incomplete —" caption carries.</summary>
    private const string GuideStubTag = "guideStub";
    /// <summary>The tag the Helper's line under a guide row carries (DRA-83). Counted into the
    /// dump beside the store's own number, because "the Helper answered" and "the row says so"
    /// are different claims (trap 56).</summary>
    private const string GuideHelperTag = "guideHelper";
    /// <summary>The tag the row-end "Improve this step" door carries.</summary>
    private const string GuideImproveTag = "guideImprove";

    /// <summary>
    /// Everything the panel draws, one level of <see cref="Grid"/> included.
    ///
    /// <para>The panel holds a mix: some things are laid straight in, and anything that
    /// shares a LINE with something else is wrapped in a Grid. Which of the two a given
    /// element is arrives with the layout and changes when the layout does — the heading's
    /// fold control moved from its own row into the heading's Grid the day the "+" went
    /// beside the name (David, 2026-09-09), and a direct-children sweep reported zero guided
    /// groups for a tab that was drawing six. <c>RowBoxesOnScreen</c> already had to sweep
    /// both; this is the same rule for every other tagged element rather than a second
    /// hand-written copy of it.</para></summary>
    private IEnumerable<FrameworkElement> PanelElements() =>
        QuestsPanel.Children.OfType<FrameworkElement>()
            .SelectMany(e => e is Grid g
                ? g.Children.OfType<FrameworkElement>().Prepend(e)
                : [e]);

    private int GuideElementsOnScreen<T>(string tag) where T : FrameworkElement =>
        PanelElements().OfType<T>().Count(e => e.Tag as string == tag);

    // A guide row is a CheckBox, and a guide row WITH its share-back door is that CheckBox
    // inside a two-column Grid — so both arrangements have to be swept or the door's
    // presence would silently halve the row count.
    private IEnumerable<CheckBox> GuideRowsOnScreen() =>
        RowBoxesOnScreen().Where(c => c.Tag as string == GuideRowTag);

    private int ClassicRowsOnScreen() =>
        RowBoxesOnScreen().Count(c => c.Tag as string != GuideRowTag);

    private IEnumerable<CheckBox> RowBoxesOnScreen() => QuestsPanel.Children
        .OfType<FrameworkElement>()
        .SelectMany(e => e is Grid g ? g.Children.OfType<CheckBox>() : [.. Loose(e)]);

    private static IEnumerable<CheckBox> Loose(FrameworkElement e) =>
        e is CheckBox c ? [c] : [];

    private int GuideImproveDoorsOnScreen() => QuestsPanel.Children.OfType<Grid>()
        .SelectMany(g => g.Children.OfType<Button>())
        .Count(b => b.Tag as string == GuideImproveTag);

    // Nested one level down: the stub caption lives inside the row's own StackPanel, which
    // is what a wrapped second line requires (trap 14).
    private int GuideStubsOnScreen() => GuideCaptionsOnScreen(GuideStubTag);

    /// <summary>How many guide rows on screen carry the Helper's line (DRA-83).</summary>
    private int GuideHelperLinesOnScreen() => GuideCaptionsOnScreen(GuideHelperTag);

    private int GuideCaptionsOnScreen(string tag) => GuideRowsOnScreen()
        .Select(c => c.Content).OfType<StackPanel>()
        .SelectMany(p => p.Children.OfType<TextBlock>())
        .Count(t => t.Tag as string == tag);

    /// <summary>
    /// A guide row's captions: the stub note, and the Helper's answer about what this step
    /// points at (DRA-83).
    ///
    /// <para>ONE builder for both surfaces that draw guide rows — the checklist tabs and the
    /// General tab's detail pane — because they had already been two copies of the stub block,
    /// and a second caption spelled twice is the drift the shared projection above them exists
    /// to prevent. A VERTICAL <see cref="StackPanel"/>: <c>TextWrapping</c> does nothing in a
    /// horizontal one (trap 14).</para>
    ///
    /// <para>A row with neither caption is handed back AS the text block, so nothing gains a
    /// panel it does not need — and the dump's caption counts stay a count of real captions
    /// rather than of wrappers.</para></summary>
    private static FrameworkElement GuideSubLines(TextBlock text, QuestChecklistRow row)
    {
        if (row.StubNote.Length == 0 && row.HelperAnswer.Length == 0) return text;

        var stack = new StackPanel();
        stack.Children.Add(text);
        // A stub step says so, in the player's words, under its own title. It stays fully
        // tickable — manual state beats weak inference, and "we could not find directions" is
        // a fact about US, not about how far the player has got.
        if (row.StubNote.Length > 0)
            stack.Children.Add(Caption(
                GuidePresentation.StubLead + " " + row.StubNote, GuideStubTag));
        // And what the player's own play says about the subject this step points at. Already
        // worded by HelperPresentation, drawn and not composed.
        if (row.HelperAnswer.Length > 0)
            stack.Children.Add(Caption(row.HelperAnswer, GuideHelperTag));
        return stack;

        static TextBlock Caption(string words, string tag)
        {
            var caption = DesignSystem.Text(Role.Caption, words);
            caption.TextWrapping = TextWrapping.Wrap;
            caption.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, 0);
            caption.Ink("DimBrush");
            caption.Tag = tag;
            return caption;
        }
    }

    /// <summary>
    /// A guide row, with the one-click share-back door at its end. Every other row is
    /// handed straight back — the door belongs to steps we authored and can be wrong about.
    ///
    /// <para>Lock 4's 1-click share-back. It is a real <see cref="DesignSystem.InlineIconButton"/>
    /// and not a handled glyph, because a vector only hit-tests where it is PAINTED and a
    /// pencil is mostly empty space (trap 16); the button widens the target to
    /// <see cref="DesignTokens.IconInlineHit"/> without redrawing the icon bigger.</para>
    ///
    /// <para>A two-column Grid, not a horizontal StackPanel: the row's title wraps, and
    /// wrapping does nothing inside a horizontal stack (trap 14).</para>
    /// </summary>
    private UIElement WithImproveDoor(CheckBox check, QuestChecklistRow row)
    {
        if (row.GuideRowKey.Length == 0) return check;
        if (GuideChecklistProjection.Resolve(GuideCatalog.Default, row.Id)
            is not var (guide, objective)) return check;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(check);

        var improve = DesignSystem.InlineIconButton("Pencil", GuidePresentation.ImproveTip,
            (_, _) => OpenUrl(GuidePresentation.ImproveUrl(guide, objective)));
        improve.VerticalAlignment = VerticalAlignment.Top;
        improve.Tag = GuideImproveTag;
        Grid.SetColumn(improve, 1);
        grid.Children.Add(improve);
        return grid;
    }

    private int BandRowsOnScreen(string tag) => QuestsPanel.Children.OfType<Border>()
        .Where(b => b.Tag as string == tag)
        .SelectMany(b => ((StackPanel)b.Child).Children.OfType<TextBlock>())
        .Count(t => t.Tag is null);   // the held-back note is tagged and is not a row

    private int SkyCopyCommandsOnScreen(string command) => QuestsPanel.Children.OfType<StackPanel>()
        .SelectMany(p => p.Children.OfType<Button>()
            .Concat(p.Children.OfType<WrapPanel>().SelectMany(w => w.Children.OfType<Button>())))
        .Count(b => b.Content is string s
            && s.Contains(command, StringComparison.Ordinal));

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
        row.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            // The CLICK is what opens the detail pane in a single-pane room, and only the
            // click: Select() is also how FinishRender restores the selection after every
            // rebuild, and a render that threw the player into a detail pane would make
            // the list unreachable while anything was ticking.
            _paneDetail = true;
            Select(entry);
            ApplyPanes();
        };
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
        // Cleared WITH the pane, not only at the top of a render: a click on a list row calls
        // Select directly, so a card added here would otherwise survive into the next
        // selection's dump and `questsGuideNext` would sum two quests' next steps. Safe to
        // clear outright because this pane is the General tab's, and the checklist tabs that
        // also fill this list return before it is ever built.
        _generalGuide = null;
        _lastGuideCards.Clear();
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

        // THE GUIDE (DRA-46). A quest the harvest or the curated catalog walks gets its
        // walkthrough here, between what it pays and where it is — the same projection the
        // Sky and Epic tabs call, keyed on the quest name rather than a reward or a class.
        //
        // Not a REPLACEMENT of the pane the way the Sky tab's guide replaces its item rows:
        // this pane draws a catalog quest, and the guide is the walkthrough it never had.
        // What the guide DOES absorb is the "Turn-ins" section — its turn-in-pieces stage is
        // those same rows, item-backed through the router (see GuideBlock).
        _generalGuide = _main.QuestLedger is { } guideLedger
            ? GuideChecklistProjection.ApplyQuest(m.Quest, GuideCatalog.Default,
                _settings, guideLedger, _main.QuestCharacterKey, helper: _helper.Lines)
            : null;

        if (_generalGuide is { } guided) DetailPane.Children.Add(GuideBlock(guided, m));
        else if (m.Items.Count > 0) DetailPane.Children.Add(Objectives(m));

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

    /// <summary>The guided group the LAST <see cref="BuildDetail"/> drew, or null. Set
    /// beside the pane it describes and cleared with it, so it can never report a guide that
    /// is no longer on screen — the same rule <see cref="_lastGuideCards"/> keeps.</summary>
    private QuestChecklistGroup? _generalGuide;

    /// <summary>The tag the General pane's guide block carries.</summary>
    private const string GeneralGuideTag = "generalGuide";
    /// <summary>The tag each stage heading inside that block carries.</summary>
    private const string GeneralGuideStageTag = "generalGuideStage";
    /// <summary>The tag every turn-in ITEM row drawn under a guide stage carries — the rows
    /// that are the guide's "Turn-in pieces" rather than a second list of them.</summary>
    private const string GeneralGuideItemTag = "generalGuideItem";
    /// <summary>...and the tag one of those carries once the bags hold enough of it. A
    /// SECOND tag rather than a bool read back off the brush: identity is a property you PUT
    /// on the object (trap 39), and a met row is what the "lights when owned reaches Need"
    /// claim is about — asserted off the screen rather than off the store that fed it.</summary>
    private const string GeneralGuideItemMetTag = "generalGuideItemMet";

    /// <summary>
    /// A catalog quest's walkthrough, on the General tab's detail pane (DRA-46, Fable §3 N2).
    ///
    /// <para><b>The one thing to understand here is what is NOT drawn.</b> A harvested guide
    /// ends on a "Turn-in pieces" stage: one <c>Collect</c> step per catalog item, then the
    /// hand-in. This pane has drawn those same items as have/need rows with a +1 door since
    /// 1.x. So the stage's Collect steps are drawn AS those item rows — same fact, same count,
    /// same door — and never as a second list of tickable boxes beside them. The router is
    /// what makes that safe rather than a coincidence: the step routed to
    /// <c>GuideProgressHome.LedgerItem</c>, refused a tick of its own, and handed back the
    /// item name on <see cref="QuestChecklistRow.LedgerItemName"/>, so this method matches on
    /// the decision instead of re-deriving it from a title (trap 4).</para>
    ///
    /// <para><b>And what the guide does NOT absorb.</b> An item the guide names no step for
    /// still gets its row, under a label that says so. A curated guide may walk a quest
    /// without enumerating its pieces, and a fold that silently drops the turn-in counts is
    /// trap 26 on the surface where the counts are the whole point.</para>
    ///
    /// <para>The reward STATS BLOCK the projection carries is deliberately not drawn here:
    /// this pane already puts the game's item window on every reward tile's hover
    /// (<see cref="AttachItemTooltip"/>), and a second copy under the guide would be two
    /// producers of one answer on one screen. The phone, which has no hover, draws it — that
    /// is the field's whole reason for existing (trap 35).</para></summary>
    private UIElement GuideBlock(QuestChecklistGroup group, QuestMatch m)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM),
            Tag = GeneralGuideTag,
        };

        // The fold control and the label on one line. NO heading: the pane's own title is
        // already this quest's name, and a second one under it would be the same string twice.
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(FoldToggle(group));
        var label = DesignSystem.Text(Role.TitleSection, GuideBlockLabel);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Margin = new Thickness(DesignTokens.SpaceXs, DesignTokens.SpaceL, 0, DesignTokens.SpaceXs);
        head.Children.Add(label);
        panel.Children.Add(head);

        if (group.GuideCaption.Length > 0)
        {
            var caption = DesignSystem.Text(Role.Caption, group.GuideCaption);
            caption.TextWrapping = TextWrapping.Wrap;
            caption.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, DesignTokens.SpaceXs);
            caption.Ink("DimBrush");
            caption.Tag = GuideCaptionTag;
            panel.Children.Add(caption);
        }

        // FOLDED: the control, the label and the caption. The turn-in rows come back under
        // their own section, because folding the WALKTHROUGH must not take the quest's
        // have/need counts with it — those are what this pane was for before guides existed.
        if (group.Collapsed)
        {
            if (m.Items.Count > 0) panel.Children.Add(Objectives(m));
            return panel;
        }

        var setters = GuideSetters(group, m.Quest);
        if (group.GuideCard is { } card)
        {
            _lastGuideCards.Add(card);
            panel.Children.Add(GuideCardView(group, card, setters, locked: false));
        }

        var byItem = m.Items.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastStage = "";

        foreach (var row in group.Rows)
        {
            if (row.IslandHeading.Length > 0 && row.IslandHeading != lastStage)
            {
                lastStage = row.IslandHeading;
                var stage = DesignSystem.Text(Role.Caption, row.IslandHeading);
                stage.FontWeight = FontWeights.SemiBold;
                stage.TextWrapping = TextWrapping.Wrap;
                stage.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceS,
                    0, DesignTokens.SpaceXxs);
                stage.Ink("DimBrush");
                stage.Tag = GeneralGuideStageTag;
                panel.Children.Add(stage);

                // The provenance sentence rides the stage that OWNS the counts, which is where
                // it has always been (#241 PR 3): where today's have-numbers came from, once,
                // not once per item.
                var owns = lastStage;
                if (group.Rows.Any(r => r.IslandHeading == owns && r.LedgerItemName.Length > 0))
                    panel.Children.Add(Note(
                        QuestPresentation.TurnInProvenanceText(m.Items, _owned, DateTime.Now), "Bag"));
            }

            // An item-backed step IS this pane's item row. Not a checkbox beside one.
            if (row.LedgerItemName.Length > 0)
            {
                if (!byItem.TryGetValue(row.LedgerItemName, out var item)) continue;
                if (!claimed.Add(row.LedgerItemName)) continue;
                var itemRow = ItemRow(item, row.StubNote);
                itemRow.Tag = item.Have >= item.Need ? GeneralGuideItemMetTag : GeneralGuideItemTag;
                panel.Children.Add(itemRow);
                continue;
            }

            panel.Children.Add(GuideRow(row, setters));
        }

        // Anything the guide never named keeps the row it always had. Under its own label
        // rather than appended silently, so a curated guide that walks a quest without
        // enumerating its pieces reads as two sections rather than as a guide that lost some.
        var unclaimed = m.Items.Where(i => !claimed.Contains(i.Name)).ToList();
        if (unclaimed.Count == 0) return panel;
        if (claimed.Count == 0)
        {
            panel.Children.Add(Objectives(m));
            return panel;
        }
        panel.Children.Add(new TextBlock
        {
            Text = UnguidedTurnInsLabel, Style = (Style)FindResource("SectionLabel"),
        });
        foreach (var item in unclaimed) panel.Children.Add(ItemRow(item));
        return panel;
    }

    /// <summary>What the guide block calls itself. "Guide", not the quest name — the pane's
    /// title is already that, and the caption under this says how far along and how honest
    /// the data is.</summary>
    private const string GuideBlockLabel = "Guide";

    /// <summary>The label over turn-in items the guide names no step for. Says the pieces are
    /// still tracked without claiming the walkthrough covers them.</summary>
    private const string UnguidedTurnInsLabel = "Other turn-ins";

    /// <summary>Every objective's write path, through the router and nowhere else — the same
    /// map <see cref="RenderChecklist"/> builds for the two checklist tabs, with this guide's
    /// own quest in the stores so the ledger-item and completion homes can answer.
    ///
    /// <para>A <c>LedgerItem</c> step is in here and the router REFUSES it, which is belt and
    /// braces rather than the reason no box moves: those rows are drawn as item rows and never
    /// get a checkbox at all.</para></summary>
    private Dictionary<string, Action<bool>> GuideSetters(QuestChecklistGroup group, QuestEntry quest)
    {
        var setters = new Dictionary<string, Action<bool>>(StringComparer.Ordinal);
        if (_main.QuestLedger is not { } ledger) return setters;
        if (GuideCatalog.Default.Find(group.GuideId) is not { } guide) return setters;

        // The SAME quest the projection read, handed down rather than looked up again: two
        // lookups of one fact is how the read side and the write side start disagreeing about
        // which item a Collect step names (trap 4).
        var stores = new GuideStores([], [], quest);
        foreach (var objective in guide.AllObjectives)
        {
            var step = objective;
            setters[GuideChecklistProjection.RowId(guide.Id, step.Id)] = done =>
                GuideProgressRouter.SetDone(_settings, ledger,
                    _main.QuestCharacterKey, guide.Id, step, stores, done);
        }
        return setters;
    }

    /// <summary>One guide objective's row on the detail pane. The checklist tabs' row, minus
    /// the island heading and the epic lock they own — same tags, so the dump counts and the
    /// share-back door work here exactly as they do there.</summary>
    private UIElement GuideRow(QuestChecklistRow row, Dictionary<string, Action<bool>> setters)
    {
        var text = DesignSystem.Text(Role.Body, "");
        text.TextWrapping = TextWrapping.Wrap;
        text.Inlines.Add(new System.Windows.Documents.Run(row.Title));
        if (row.Detail.Length > 0)
        {
            var detail = new System.Windows.Documents.Run("   " + row.Detail);
            detail.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
            text.Inlines.Add(detail);
        }
        text.Ink(row.Acquired || row.IsSkipped ? "DimBrush" : "TextBrush");
        if (row.IsSkipped) text.TextDecorations = TextDecorations.Strikethrough;

        // The stub note and the Helper's line, each as its own dim caption under the row.
        FrameworkElement content = GuideSubLines(text, row);

        var check = new CheckBox
        {
            Tag = GuideRowTag,
            Content = content,
            IsChecked = row.Acquired,
            Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
            ToolTip = row.GuideFacts.Length > 0 ? row.GuideFacts : null,
        };
        if (setters.TryGetValue(row.Id, out var set))
        {
            check.Checked += (_, _) => Tick(true);
            check.Unchecked += (_, _) => Tick(false);

            void Tick(bool done)
            {
                set(done);
                _settings.Save();
                PushUndo(row, done, set);
                Refresh(force: true);
            }
        }
        else
        {
            // No writer means no affordance. IsEnabled alone leaves a control that reads as
            // live and silently ignores clicks (trap 17), which is the "silent no-ops are
            // broken" rule with the switch on the other side.
            check.IsEnabled = false;
            check.Opacity = 0.5;
        }
        return WithImproveDoor(check, row);
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
        // One sentence, not one per item (#241 PR 3, Bevel-signed 2026-08-27): where
        // today's have-counts came from — an inventory dump, or a log tally that cannot
        // see hand-ins.
        panel.Children.Add(Note(
            QuestPresentation.TurnInProvenanceText(m.Items, _owned, DateTime.Now), "Bag"));
        foreach (var item in m.Items) panel.Children.Add(ItemRow(item));
        return panel;
    }

    /// <summary><paramref name="stubNote"/> is set only when this row is standing in for a
    /// guide's turn-in piece and OUR DATA for that step is hollow (DRA-46). It rides the
    /// hover, because the row itself is the count and a second line under it would push the
    /// pieces apart; the words are the same ones the checklist tabs draw under a stub row, so
    /// "we do not know where this drops" reads identically wherever it appears.
    ///
    /// <para>Not dropping it was the point. Folding the guide's Collect steps into these rows
    /// is what makes them one fact rather than two (trap 4) — and a fold that quietly loses
    /// what the folded thing SAID is trap 26 with the loss on the honesty side.</para></summary>
    private Border ItemRow(QuestItemProgress item, string stubNote = "")
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
        AttachItemTooltip(row, item.Name, stubNote.Length > 0
            ? GuidePresentation.StubLead + " " + stubNote + "\n\n" + ItemRowHint
            : ItemRowHint);
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
                SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey),
                _main.QuestLedger, _main.QuestCharacterKey);
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

    /// <summary>The Epic tab's per-class band: the class name and the "Mark as complete"
    /// master check (#138 aodgizmo, restored for #210; the label was "Epic complete" until
    /// the 2026-09-11 Founder smoke read it as a done-badge — <see
    /// cref="EpicCompleteToggle.ButtonLabel"/>).
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
            // Owner through GetWindow rather than `this`: a UserControl is not a Window,
            // and the owner differs by host — the v1 tracker in one case, the shell in the
            // other. Null before the view is in a tree, which the no-owner overload
            // handles rather than throwing.
            var prompt2 = EpicCompleteToggle.ConfirmPrompt(className, items);
            if (prompt2 is { } prompt)
            {
                // The caption is the LABEL OF THE BUTTON THAT OPENED IT, read from the
                // one producer rather than repeated as a literal (trap 4) — a dialog
                // titled with the old state word was the second place "Epic complete"
                // claimed to be a status the 2026-09-11 smoke found it was not.
                var caption = EpicCompleteToggle.ButtonLabel(completed: false);
                var answer = Window.GetWindow(this) is { } owner
                    ? MessageBox.Show(owner, prompt, caption,
                        MessageBoxButton.OKCancel, MessageBoxImage.Question)
                    : MessageBox.Show(prompt, caption,
                        MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK) return;
            }
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
        var open = BandOpen("skyReady");
        var unlocked = _main.QuestLedger?.UnlockedClassesFor(_main.QuestCharacterKey);

        var panel = new StackPanel();
        panel.Children.Add(BandHeading("skyReady", open,
            $"Ready to turn in — {ready.Count}", "Check", "GoodBrush"));

        if (open)
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
                // The already-unlocked caveat, worded in Core (Hateborne, 2026-09-03):
                // the row stays — the item is still worth collecting — and this says what
                // the turn-in still buys.
                if (QuestChecklistLayout.ReadyNote(group, unlocked) is { } note)
                {
                    var caveat = new System.Windows.Documents.Run($"   {note}");
                    caveat.SetResourceReference(
                        System.Windows.Documents.Run.ForegroundProperty, "DimBrush");
                    caveat.FontStyle = FontStyles.Italic;
                    line.Inlines.Add(caveat);
                }
                line.ToolTip = $"{group.ClassName}: all {group.Total} "
                    + (group.Total == 1 ? "item" : "items") + " acquired"
                    + (group.TurnInNpc is { Length: > 0 } n ? $" — turn in to {n}" : "")
                    + (QuestChecklistLayout.ReadyNote(group, unlocked) is { } tip
                        ? $" — {tip}" : "");
                panel.Children.Add(line);
            }

        var band = new Border
        {
            Child = panel,
            // The band's identity for the EQBUDDY_EXPAND dump, same rule as the
            // leftover bands below (trap 39: identity is put on the object).
            Tag = "skyReady",
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                DesignTokens.SpaceM, DesignTokens.SpaceS),
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM),
        };
        band.SetResourceReference(BackgroundProperty, "RaisedBrush");
        QuestsPanel.Children.Add(band);
    }

    /// <summary>Which Sky bands are open, keyed by band id ("skyReady", "skyLeftoverA",
    /// "skyLeftoverB"). **A field, never a setting** (the ProgressCardView precedent —
    /// Bevel, Helm-signed 2026-08-23: session-only): folding a band away to read the list
    /// under it is a decision about the next thirty seconds, not about how the app opens
    /// tomorrow, and <c>DeadSettingTests</c> exists because settings outlive the surfaces
    /// that wrote them. Default OPEN — the bands are the tab's answers, not its chrome.</summary>
    private readonly Dictionary<string, bool> _skyBandOpen = new(StringComparer.Ordinal);

    private bool BandOpen(string id) => _skyBandOpen.GetValueOrDefault(id, true);

    /// <summary>A Sky band's clickable fold heading: the band's identity icon, then an
    /// <see cref="EqFoldLabel"/> carrying the chevron and the heading-with-count — which
    /// is the whole band while it is folded (Hateborne, 2026-09-03: three uncollapsible
    /// boxes were most of the tab). SemiBold and never dimmer than the rows under it —
    /// trap 19's lesson, kept from the hand-built headings this replaces.</summary>
    private UIElement BandHeading(string id, bool open, string text, string icon, string inkKey)
    {
        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = open ? "Click to fold this box to one line" : "Click to expand",
            Background = System.Windows.Media.Brushes.Transparent,   // hit-testable across the whole row
        };
        var identity = DesignSystem.Icon(icon, inkKey, size: 12);
        identity.VerticalAlignment = VerticalAlignment.Center;
        heading.Children.Add(identity);
        var fold = new EqFoldLabel { Section = true };
        fold.Set(open, text);
        // After Section, so the band keeps its identity colour rather than the section
        // heading's dim ink.
        fold.Ink = inkKey;
        fold.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        heading.Children.Add(fold);
        heading.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _skyBandOpen[id] = !BandOpen(id);
            Refresh(force: true);
        };
        return heading;
    }

    /// <summary>#243 (tvongaza): *"cross check which sky quests you've completed and which
    /// sky quest items you no longer need as you've completed all the quests which use
    /// them. Would help with limited inventory space."*
    ///
    /// TWO bands, never one (Bevel's replace, Helm-signed 2026-09-02). They are claims of
    /// different strength and a player freeing bag space acts on them differently, so band
    /// B never appears under band A's heading — the words are the whole feature.
    ///
    /// Same shape and same rules as the Ready band above: Sky only, and ABSENT rather than
    /// empty. Absent also covers "no dump has ever been read", because <see
    /// cref="SkyLeftovers.Compute"/> answers empty for a null dump — "you hold none of it"
    /// and "you were never told" look identical in a count and only one of them is a fact.
    /// The way in is the ⧉ <c>/outputfile inventory</c> the tab already carries.</summary>
    private void RenderLeftoverBands(QuestTab tab)
    {
        if (tab != QuestTab.Sky) return;
        var leftovers = SkyLeftovers.Compute(
            _main.LatestInventory(), _settings.SkyQuestChecklist, _settings.SkyQuestCompleted,
            _offered, _main.QuestCatalog);
        if (leftovers.IsEmpty) return;

        // Band A first: it is the reporter's own sentence and the only strong claim.
        LeftoverBand(SkyLeftoverBand.NoLongerNeeded, leftovers.NoLongerNeededHeading,
            "Bag", "AccentBrush", "skyLeftoverA", leftovers.HeldBackNote);
        LeftoverBand(SkyLeftoverBand.OtherClassesWant, leftovers.OtherClassesWantHeading,
            "Group", "TextBrush", "skyLeftoverB", note: "");

        void LeftoverBand(SkyLeftoverBand band, string headingText, string icon,
            string inkKey, string tag, string note)
        {
            var rows = leftovers.RowsIn(band);
            if (rows.Count == 0) return;   // each band carries its own absence
            var open = BandOpen(tag);

            var panel = new StackPanel();
            panel.Children.Add(BandHeading(tag, open, headingText, icon, inkKey));

            if (open)
            {
                foreach (var row in rows)
                {
                    // The row's words come from Core, so this window, its Avalonia twin and
                    // the phone cannot disagree about what a leftover row says.
                    var line = DesignSystem.Text(
                        band == SkyLeftoverBand.NoLongerNeeded ? Role.Body : Role.BodySecondary,
                        row.Line);
                    line.TextWrapping = TextWrapping.Wrap;
                    line.Margin = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXxs, 0, 0);
                    line.ToolTip = row.Detail;
                    panel.Children.Add(line);
                }

                // What was deliberately left OUT, and why. An item that is simply absent from
                // the band reads as a bug in the join; naming the quest that wants it is the
                // sentence that stops someone selling it. Folded away with the rows: a
                // collapsed band is one line, literally.
                if (note.Length > 0)
                {
                    var held = DesignSystem.Text(Role.Caption, note);
                    // Tagged so the dump's row count below cannot mistake the note for a row.
                    held.Tag = tag + "-note";
                    held.TextWrapping = TextWrapping.Wrap;
                    held.Margin = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs, 0, 0);
                    held.Ink("DimBrush");
                    panel.Children.Add(held);
                }
            }

            var border = new Border
            {
                Child = panel,
                // The band's identity for the EQBUDDY_EXPAND dump, PUT on the object
                // rather than inferred from its heading text (trap 39).
                Tag = tag,
                CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
                Padding = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
                    DesignTokens.SpaceM, DesignTokens.SpaceS),
                Margin = new Thickness(0, 0, 0, DesignTokens.SpaceM),
            };
            border.SetResourceReference(BackgroundProperty, "RaisedBrush");
            QuestsPanel.Children.Add(border);
        }
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

    /// <summary>The inventory dump's report, beside the achievements one (Hateborne,
    /// 2026-09-03): the dump now proves Sky rewards turned in, and a change the player
    /// did not watch happen has to say so where they are looking. Same class, one more
    /// host — the precedent SkyImport's own comment states.</summary>
    private ImportReportView SkyInventoryImport => _skyInventoryImport ??=
        new ImportReportView(() => _main.LastInventoryImport, () => Refresh(force: true));

    private ImportReportView? _skyInventoryImport;

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
            "Turned rewards in before EQBuddy? The game's achievements dump knows — and "
            + "your bags are evidence too: owning a reward's finished item marks it turned "
            + "in, and leftover ingredients get flagged below. Run these in game and "
            + "EQBuddy reads the files the game writes — it never scans the game itself.",
            "Info"));
        // Both commands side by side, the Unlocks tab's own shape (Hateborne, 2026-09-03:
        // this tab is fed by two dumps and only ever named one of them).
        var row = new WrapPanel();
        row.Children.Add(CommandPrompt(GameCommands.OutputfileAchievements,
            "Copies the command — paste it into the game's chat. The game writes "
            + "<name>_<server>-Achievements.txt beside its own folders and EQBuddy "
            + "imports it on its own; the report appears here. This is what says which "
            + "rewards were turned in before EQBuddy existed."));
        row.Children.Add(CommandPrompt(GameCommands.OutputfileInventory,
            "Copies the command — paste it into the game's chat. The game writes "
            + "<name>_<server>-Inventory.txt beside its own folders and EQBuddy imports "
            + "it on its own; the report appears here. Owning a reward's finished item "
            + "proves its turn-in, and it is what the leftover boxes below read."));
        wrap.Children.Add(row);
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
        var allRaces = _main.Unlocks.Races;
        var allClasses = _main.Unlocks.Classes;
        var factions = _main.Unlocks.Factions;
        _unlockDrewFactions = factions is not null;

        // **THE PICK, APPLIED BEFORE ANYTHING IS BUILT** (DRA-71 D5, plan P11). It narrows the
        // LISTS and not the groups, so `UnlockLayout.Groups`' contract — one group per unlock,
        // one row per Actionable entry, IN ORDER — still pairs each drawn row with the
        // criterion behind it, which is how the guided detail resolves at all. The guidance
        // layer is untouched: it never knew which unlocks were on screen and still does not.
        var picked = UnlockPickStore.Picked(_settings, _main.QuestCharacterKey);
        // The OFFER is what the section lens shows, unnarrowed — see RefreshUnlockPicker.
        var offered = new List<UnlockProgress>();
        if (UnlockLayout.InSection(UnlockLayout.RacesHeading, _unlockSection))
            offered.AddRange(allRaces);
        if (UnlockLayout.InSection(UnlockLayout.ClassesHeading, _unlockSection))
            offered.AddRange(allClasses);
        RefreshUnlockPicker(offered, picked);

        var races = UnlockPickStore.Narrow(allRaces, picked);
        var classes = UnlockPickStore.Narrow(allClasses, picked);
        _unlockHidden = UnlockPickStore.Hidden(allRaces, picked)
                        + UnlockPickStore.Hidden(allClasses, picked);
        _unlockWhoWhere = 0;
        _unlockHovers = 0;

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

            // What the PICK is holding back in THIS section — a surviving filter says so out
            // loud (trap 50), and the player did this one, so the note names the way back.
            var hiddenHere = UnlockPickStore.Hidden(
                heading == UnlockLayout.RacesHeading ? allRaces : allClasses, picked);
            if (UnlockPickReadout.HiddenNote(hiddenHere) is { Length: > 0 } hiddenNote)
                QuestsPanel.Children.Add(Note(hiddenNote, "Info"));

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

                for (var r = 0; r < g.Rows.Count; r++)
                {
                    var row = g.Rows[r];
                    // The CRITERION behind this row, by position. UnlockLayout.Groups emits
                    // exactly one row per entry of u.Actionable, in order — its own contract,
                    // said on the method — so this is reading the list the rows were built
                    // from rather than splitting the row id back apart, which is the second
                    // source trap 4 is about (the id contains the separator it would split on).
                    var criterion = r < u.Actionable.Count ? u.Actionable[r] : null;
                    var guidance = criterion is null
                        ? UnlockGuidanceRow.Nothing
                        : UnlockGuidance.Resolve(u, criterion, factions, _unlockPool.Mobs,
                            _settings.SkyQuestChecklist, _settings.SkyQuestCompleted,
                            _main.QuestCatalog);

                    var line = new Grid
                    {
                        Margin = new Thickness(DesignTokens.SpaceL, 1, 0, 1),
                        Tag = UnlockRowTag,
                        // **A GRID WITH A NULL BACKGROUND DOES NOT HIT-TEST**, so the hover
                        // below would only appear over the ink and not over the gaps between
                        // the icon, the text and the door. Transparent is the WPF idiom for
                        // "claim the whole rectangle without painting it" — the same lesson
                        // trap 16 records for vectors, one control up.
                        Background = System.Windows.Media.Brushes.Transparent,
                    };
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    // The door's column, Auto so it takes only its own width and the wrapping
                    // text keeps the rest. Added whether or not a door lands in it: a column
                    // definition costs nothing and a conditional grid shape is a second layout.
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

                    // THE DOOR, at the end of the row it belongs to (DRA-65). A real
                    // InlineIconButton and not a handled glyph: a vector only hit-tests where
                    // it is PAINTED, and an arrow is mostly empty space (trap 16) — the button
                    // widens the target to IconInlineHit without redrawing the icon bigger.
                    if (guidance.Door is { } door)
                    {
                        var open = DesignSystem.InlineIconButton("ArrowUpRight", door.Tip,
                            (_, _) => OpenUnlockDoor(door));
                        open.VerticalAlignment = VerticalAlignment.Center;
                        open.Tag = UnlockDoorTag;
                        Grid.SetColumn(open, 2);
                        line.Children.Add(open);
                    }
                    // **THE LONGER PROSE, ON THE HOVER** (DRA-71 D5, plan P12). What your own
                    // kills did to this faction — up to six signed one-liners — is the wall
                    // the who · where line below replaces. Set only when it is not empty: an
                    // empty tooltip is a rectangle that appears and says nothing.
                    if (guidance.Hover is { Length: > 0 } hover)
                    {
                        line.ToolTip = hover;
                        _unlockHovers++;
                    }
                    QuestsPanel.Children.Add(line);

                    // **WHO · WHERE, THE GUIDE'S OWN ROW LINE** (plan P12). WHAT is the row's
                    // title above; this is the creature and the zone the guidance already
                    // decided on, drawn as a VALUE rather than read back out of a sentence.
                    // Empty for the Sky and Task shapes and for a faction nobody has farmed —
                    // an unanswered question draws nothing (trap 73), which is why this is an
                    // `if` and not a line that sometimes says "· ".
                    if (guidance.RowDetail is { Length: > 0 } whoWhere)
                    {
                        var pointer = DesignSystem.Text(Role.Caption, whoWhere);
                        pointer.TextWrapping = TextWrapping.Wrap;
                        pointer.Margin = new Thickness(
                            DesignTokens.SpaceL + DesignTokens.IconInlineHit, 0, 0,
                            DesignTokens.SpaceXxs);
                        pointer.Ink("DimBrush");
                        pointer.Tag = UnlockWhoWhereTag;
                        // The hover rides the line it explains too, so a player whose pointer
                        // landed on the sentence rather than on the row still gets it.
                        if (guidance.Hover is { Length: > 0 } h) pointer.ToolTip = h;
                        QuestsPanel.Children.Add(pointer);
                        _unlockWhoWhere++;
                    }

                    // The QUANTITIES, UNDER the row and indented past its icon: the piece count
                    // and the kills-to-go estimate. One line each, they are what a player acts
                    // on, and they are the half of the guidance a screenshot can review — a tab
                    // whose every sentence lived on a hover would be a tab nobody could
                    // photograph (trap 22). Each is one already-worded sentence out of
                    // UnlockGuidance; this loop decides layout and nothing else.
                    foreach (var sentence in guidance.RowLines)
                    {
                        var guided = DesignSystem.Text(Role.Caption, sentence);
                        guided.TextWrapping = TextWrapping.Wrap;
                        guided.Margin = new Thickness(
                            DesignTokens.SpaceL + DesignTokens.IconInlineHit, 0, 0,
                            DesignTokens.SpaceXxs);
                        guided.Ink("DimBrush");
                        guided.Tag = UnlockGuideTag;
                        QuestsPanel.Children.Add(guided);
                    }
                }
            }
        }
    }

    /// <summary>
    /// How many times this surface has REBUILT — counted past the repaint gate, so it moves
    /// when the panel is actually re-populated and not once per tick.
    ///
    /// <para>It exists because "the screen changed BECAUSE of this store" is a claim no value
    /// count can make. A test that appends a log line and watches a row count cannot tell a
    /// redraw caused by what it appended from a redraw that was coming anyway — and this
    /// surface keeps rebuilding for a beat after launch as the class inference and the ledger
    /// settle. Measured, not assumed: the DRA-65 mover assertion passed with the pool
    /// deliberately REMOVED from the signature, because it appended into that settling window,
    /// and failed the moment the append waited for this number to hold still.</para>
    ///
    /// <para>This is a stillness question on purpose, and a narrow one: it is not
    /// <c>WaitForReplayToSettle</c>'s job (whether the LOG is fully read — the app answers
    /// that outright and stillness there was wrong for four rounds). It is "is anything else
    /// about to redraw this panel", which nothing but the panel can answer.</para></summary>
    private int _renders;

    /// <summary>Did the LAST render of the Unlocks tab have a faction dump in hand? Dumped so
    /// a test can wait for the render that actually used one, rather than for the store to
    /// have one and hope the next repaint was the one it meant (trap 56).</summary>
    private bool _unlockDrewFactions;

    /// <summary>The tag one unlock criterion's row carries — the floor the guided-line count
    /// is read against, so "no guided lines" cannot pass over a tab that drew no rows at
    /// all.</summary>
    private const string UnlockRowTag = "unlockRow";

    /// <summary>The tag every guided sentence under an unlock row carries, so the
    /// <c>EQBUDDY_EXPAND</c> dump counts them off the REAL visual tree rather than off the
    /// resolver that produced them — "the store says so" and "the screen says so" are
    /// different claims (trap 56).</summary>
    private const string UnlockGuideTag = "unlockGuide";

    /// <summary>The tag a row-end unlock door carries. Counted the same way and for the same
    /// reason: an absent control photographs as an unremarkable panel (trap 29).</summary>
    private const string UnlockDoorTag = "unlockDoor";

    /// <summary>The tag the <c>who · where</c> line under an unlock row carries (DRA-71 D5).
    /// Its own tag rather than sharing <see cref="UnlockGuideTag"/>, because the two are
    /// different claims about the same feature: the POINTER reached the screen, and the
    /// QUANTITIES did. One count could not tell a row that gained a pointer and lost its
    /// estimate from a row that did neither.</summary>
    private const string UnlockWhoWhereTag = "unlockWhoWhere";

    /// <summary>How many unlock rows the last render hung the longer prose on, counted as it
    /// was SET rather than walked back off the tree — a <c>ToolTip</c> is not an element and
    /// there is nothing in the panel to find. It is the only fact that can say the movers
    /// survived P12's move off the row: they are no longer drawn, so
    /// <c>questsUnlockGuided</c> would read exactly the same whether the hover carries them or
    /// nothing at all.</summary>
    private int _unlockHovers;

    /// <summary>How many <c>who · where</c> lines the last render drew, and how many unlocks
    /// the pick held back. Both are the SCREEN's answer beside the store's — <c>pk:</c> in the
    /// repaint signature says the pick moved, and only these say the tab acted on it.</summary>
    private int _unlockWhoWhere;
    private int _unlockHidden;

    /// <summary>
    /// Where an unlock row's ↗ leads. Core decides THAT there is a door and what it points
    /// at; this is the only place that knows what a tab or a browser is.
    ///
    /// <para>The wiki arm is player-clicked, like the Drops tab's creature heading: EQBuddy
    /// asks eqlwiki for nothing here, so the request policy toward the wiki is untouched.</para>
    /// </summary>
    private void OpenUnlockDoor(UnlockDoor door)
    {
        switch (door.Kind)
        {
            case UnlockDoorKind.WikiFaction:
                MainWindow.OpenWikiUrl(WikiLinks.Faction(door.Target));
                break;
            case UnlockDoorKind.SkyTab:
                // The reward NAME out of the key: the key carries the class too
                // ("Warrior|Azure Ruby Ring") and the search box takes the words a player
                // would type. Split on the separator RewardKey itself owns, limit 2, so a
                // reward containing a pipe keeps its tail.
                OpenTabFiltered(QuestTab.Sky,
                    door.Target.Split('|', 2) is [_, var reward] ? reward : door.Target);
                break;
            case UnlockDoorKind.GeneralTabQuest:
                OpenTabFiltered(QuestTab.General, door.Target);
                break;
        }
    }

    /// <summary>
    /// Land on another tab with the search box already carrying what to look at.
    ///
    /// <para>NOT through <see cref="SetTab"/>, which takes <c>tab:state</c> as one string —
    /// that is the screenshot hook's protocol, and a quest name or reward containing a colon
    /// would silently fail its two-part split and drop the player on the General tab instead.
    /// A door that opens the wrong surface is worse than one that opens nothing.</para>
    ///
    /// <para><see cref="TabChanged"/> IS raised here, unlike from <see cref="SetTab"/>: the
    /// player clicked something, so the theme host should follow them.</para>
    /// </summary>
    private void OpenTabFiltered(QuestTab tab, string search)
    {
        _tab = tab;
        FilterBox.Text = search;
        TabChanged?.Invoke(tab);
        ApplyTabVisual();
        Refresh(force: true);
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
            SkyInventoryImport.Render();
            QuestsPanel.Children.Add(SkyInventoryImport.Body);
            QuestsPanel.Children.Add(SkyAchievementsPrompt());
        }
        // Grouping, ordering and the detail line come from Core so this window, the
        // Avalonia one and EQBuddy Mobile cannot disagree about what a checklist row
        // says — they already had (#184).
        // The rows the Epic tab is SHOWING, captured once: the classic-era lens is applied
        // here and nowhere else, and the guide projection reads this same list to decide which
        // objectives have a box on this tab (one producer of "is this row in this era").
        var epicRows = _settings.EpicQuestChecklist
            .Where(i => !_settings.EpicQuestClassicOnly || i.AvailableInClassic)
            .ToList();

        var groups = tab == QuestTab.Epic
            ? QuestChecklistLayout.Epic(epicRows)
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

        // A reward with an authored guide stops being a flat list of drops and becomes the
        // walkthrough, in reading order, under its stage names. Applied HERE — the one place
        // this window gets its groups — because EQBuddy Mobile calls the same projection from
        // the same point in CompanionProjection.BuildSky. Parity by shared module, not by
        // feature list (David, 2026-08-18). A class nobody has authored is untouched.
        if (_main.QuestLedger is { } guideLedger && tab is QuestTab.Sky or QuestTab.Epic)
        {
            // The Epic tab's cutover is by CLASS — an epic class has one quest, and the
            // sections the tab grouped by become its stage headings (Fable §2, Delivery 3).
            groups = tab == QuestTab.Epic
                ? GuideChecklistProjection.ApplyEpic(groups, epicRows, GuideCatalog.Default,
                    _settings, guideLedger, _main.QuestCharacterKey, helper: _helper.Lines)
                : GuideChecklistProjection.Apply(groups, GuideCatalog.Default,
                    _settings, guideLedger, _main.QuestCharacterKey, helper: _helper.Lines);

            // Guide rows tick through the router, which decides per objective whether the
            // fact belongs to the Sky turn-in store, to one of THIS reward's item boxes, to
            // the epic checklist row the objective was generated from, or to the guide
            // ledger. Never a second copy of a tick a checklist already owns.
            foreach (var guided in groups.Where(g => g.GuideId.Length > 0))
            {
                if (GuideCatalog.Default.Find(guided.GuideId) is not { } guide) continue;
                var rewardItems = GuideChecklistProjection.ItemsFor(_settings, guided.CompletionKey);
                List<EpicQuestChecklistItem> backingRows = tab == QuestTab.Epic ? epicRows : [];
                foreach (var objective in guide.AllObjectives)
                {
                    var step = objective;
                    setters[GuideChecklistProjection.RowId(guide.Id, step.Id)] = done =>
                        GuideProgressRouter.SetDone(_settings, guideLedger,
                            _main.QuestCharacterKey, guide.Id, step, rewardItems, backingRows, done);
                }
            }
        }

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
        // Under Ready, for the same reason Ready is above the lens: it answers a
        // cross-class question, and a filter that hid it would leave the player narrowing
        // a list to find out what they were already being told. Second because Ready names
        // an ACTION and this names a fact.
        RenderLeftoverBands(tab);
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

        // CLOSEST TO COMPLETION (DRA-218). Last of the three, and downstream of all of them:
        // it reorders whatever the picker, the class lens and the state lens left, and adds
        // and removes nothing — so the row objects, their ids and their tick setters are the
        // same ones class order would have handed to the loop below. Sky only; an Epic
        // section is a stage of one quest rather than a reward you could be closer to.
        if (tab == QuestTab.Sky && _settings.SkyClosestToCompletion)
            matching = [.. QuestChecklistLayout.ClosestToCompletion(matching)];

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

        // ISLAND VIEW (DRA-164). Downstream of the class picker, the class lens and the state
        // lens — it is the same checklist the player is already looking at, rearranged. The
        // search box above returns before this and still crosses every class, unchanged; the
        // Ready band, the leftover bands and the class counts are already on screen and stay
        // there in BOTH modes, because they answer cross-class questions the arrangement does
        // not change.
        if (tab == QuestTab.Sky && _settings.SkyGroupByIsland)
        {
            RenderIslandView(matching, setters);
            return;
        }

        var lastClass = "";
        foreach (var group in matching)
        {
            // THE ORDER, as the panel received it (DRA-218). Written before anything can
            // `continue` past it, so it is the first group the render was HANDED and not the
            // first one that happened to draw a control — the ordering claim is about the
            // list, and a fold decides how much of it appears.
            if (_lastFirstGroupHeading.Length == 0)
            {
                _lastFirstGroupHeading = group.Heading;
                _lastFirstGroupRemaining = group.Remaining;
            }
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
            // What this quest PAYS, on the heading's hover: on a folded list the item rows
            // that used to answer "what do I get" are not on screen (David, 2026-09-09).
            // The hover says what the quest PAYS and nothing else (David, 2026-09-09). It
            // used to append "Click to open the wiki page" — narrating an affordance the
            // cursor already shows, in the space the answer was supposed to occupy.
            // MONOSPACE, because what it now carries is the item's own stats block and the
            // game prints that in columns ("WIS: +9  MANA: +60"). Same choice, and the same
            // reason, as ItemInfoWindow — this is that panel's content on a hover, which is
            // what the Founder was asking for (2026-09-10: "show the reward as it does in
            // EQLWiki or when we mouse over any item in EQBuddy").
            headingText.ToolTip = group.RewardCard.Length > 0
                ? new TextBlock
                {
                    Text = group.RewardCard,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = DesignTokens.Spec(Role.Caption).Size,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = DesignTokens.TipWidth,
                }
                : group.RewardSummary.Length > 0
                    ? group.RewardSummary
                    : (object)"Open the wiki page for this quest";
            headingText.Ink("AccentBrush");
            // The PAGE, not the title: a Sky reward's title is its item page, but a guided
            // epic group's is "Epic 1.0" and an unguided one's is a section heading. Core
            // decides which (QuestChecklistGroup.HeadingPage) so this window and the phone
            // cannot send a player to two different places.
            var rewardName = group.HeadingPage;
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
            // THE HEADING LINE: the fold control, the name, and the turn-in button when there
            // is one — all on one row.
            //
            // The "+" sits BESIDE the name, not under it (David, 2026-09-09): *"I imagined
            // the + would be next to the quest name, not wasting space between each quest
            // name… similarly to how the main EQBuddy window works when you click on a card
            // and it expands below."* A folded list exists to fit a class on one screen, and
            // a control on its own row spent a line per quest doing what a leading glyph does
            // for free. Leading, because that is where a disclosure control lives — the eye
            // reads the + then the thing it opens.
            //
            // Column 0 is empty and therefore zero-wide for an UNGUIDED group (the Epic tab,
            // and any Sky reward with no guide), so those headings sit exactly where they did.
            var headingRow = new Grid();
            headingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // A second row for the caption, so it lines up under the NAME rather than under
            // the fold control. The grid answers that, not arithmetic on the control's width.
            headingRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headingRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            if (group.GuideId.Length > 0)
            {
                headingRow.Children.Add(FoldToggle(group));
                // The name moves off the panel's own left edge to sit after the control.
                headingText.Margin = new Thickness(DesignTokens.SpaceXs, DesignTokens.SpaceL,
                    0, DesignTokens.SpaceXs);
            }
            Grid.SetColumn(headingText, 1);
            headingRow.Children.Add(headingText);

            if (group.CompletionKey is { } rewardKey && (group.Completed || group.ReadyToTurnIn))
            {
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
                        SkyCompleteToggle.ItemsFor(_settings.SkyQuestChecklist, rewardKey),
                        _main.QuestLedger, _main.QuestCharacterKey);
                    _settings.Save();
                    Refresh(force: true);
                };
                Grid.SetColumn(turnIn, 2);
                headingRow.Children.Add(turnIn);
            }
            QuestsPanel.Children.Add(headingRow);

            // "Guide · 0 of 3 · 1 stub" — how far along, and how many of these steps we could
            // not fully write down. The stub count rides the same line as the progress on
            // purpose: a hollow guide must never read as a finished one (Founder lock 4a).
            // Worded in GuidePresentation so the phone says it identically.
            if (group.GuideCaption.Length > 0)
            {
                var guideCaption = DesignSystem.Text(Role.Caption, group.GuideCaption);
                guideCaption.TextWrapping = TextWrapping.Wrap;
                guideCaption.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, DesignTokens.SpaceXs);
                guideCaption.Ink("DimBrush");
                guideCaption.Tag = GuideCaptionTag;
                // Row 1, column 1: under the name, not under the "+".
                Grid.SetRow(guideCaption, 1);
                Grid.SetColumn(guideCaption, 1);
                headingRow.Children.Add(guideCaption);
            }

            // WHY THIS ONE WILL NOT FINISH (DRA-218, S23 AC 8). Above the rows and on the
            // heading, because a folded group is often the only thing on screen for a quest
            // and the count on that line is the thing being corrected: "1 left" with a
            // struck-out prerequisite behind it is the reading this sentence exists to stop.
            // Core's words, so the phone says it identically (#184).
            if (QuestChecklistLayout.BlockedNote(group) is { } blocked)
            {
                var blockedLine = DesignSystem.Text(Role.Caption, blocked);
                blockedLine.TextWrapping = TextWrapping.Wrap;
                blockedLine.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, DesignTokens.SpaceXs);
                blockedLine.Ink("DimBrush");
                QuestsPanel.Children.Add(blockedLine);
                _lastBlockedNote = blocked;
            }

            // FOLDED: the heading line — which now carries the fold control itself — its
            // counts and its caption, and nothing else. That is what lets a whole class fit
            // on one screen and be dug into one quest at a time.
            if (group.GuideId.Length > 0 && group.Collapsed) continue;

            // The active-step card, ABOVE this group's rows: the one thing on the tab that
            // says "do this next" rather than "here is everything" belongs where the eye
            // lands, not under the list it summarises (trap 44, requirements §12).
            if (group.GuideCard is { } card && group.GuideId.Length > 0)
            {
                _lastGuideCards.Add(card);
                QuestsPanel.Children.Add(GuideCardView(group, card, setters, locked));
            }

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
                if (ChecklistRowControl(row, setters, locked, group.ClassName) is { } control)
                    QuestsPanel.Children.Add(control);
            }
        }
    }

    /// <summary>
    /// The Sky checklist grouped by ISLAND — the Founder's ask of 2026-09-17: *"everything to
    /// collect on island N before moving to next"*, across every class the player picked.
    ///
    /// <para><b>It decides nothing.</b> The grouping, the ordering, the exclusions and both
    /// sentences come from <c>QuestChecklistLayout.SkyByIsland</c>, which EQBuddy Mobile calls
    /// from the same point — this method draws what Core hands it and owns no rule of its own,
    /// which is the only reason three surfaces can agree about a checklist (#184).</para>
    ///
    /// <para>No guide chrome: no per-reward cards, no fold state, no wiki-link headings. Folds
    /// are keyed on the REWARD and a reward is not what a group is here; the search-results
    /// view is the precedent for a rearrangement drawing rows and nothing else.</para>
    /// </summary>
    private void RenderIslandView(
        IReadOnlyList<QuestChecklistGroup> matching, Dictionary<string, Action<bool>> setters)
    {
        // The completion lens rides through rather than being applied above: the groups have
        // no headings on this screen to reorder, so it has to reach the ROWS inside an island
        // or it is a control that changes nothing and says nothing (DRA-218).
        var layout = QuestChecklistLayout.SkyByIsland(
            matching, _settings.SkyStepsUnderEveryIsland, _settings.SkyClosestToCompletion);
        _lastIslandLayout = layout;
        _lastIslandRowTitle = "";
        _lastIslandRowOwner = "";

        // WHAT IS NOT ON THIS SCREEN, above the rows rather than under them (trap 44 and
        // trap 50). Both sentences are Core's and are EMPTY when nothing was hidden, so this
        // draws nothing in the common case rather than a standing disclaimer nobody reads.
        foreach (var note in new[] { layout.TurnInNote, layout.HiddenNote })
        {
            if (note.Length == 0) continue;
            var line = DesignSystem.Text(Role.Caption, note);
            line.TextWrapping = TextWrapping.Wrap;
            line.Margin = new Thickness(DesignTokens.SpaceS, DesignTokens.SpaceXs,
                DesignTokens.SpaceS, 0);
            line.Ink("DimBrush");
            QuestsPanel.Children.Add(line);
        }

        if (layout.Groups.Count == 0)
        {
            // NAME what emptied it. Every Sky row this player can see is either a hand-in or
            // belongs to a reward they have already turned in — which is a real and rather
            // good state, not a broken tracker.
            QuestsPanel.Children.Add(EmptyState(
                "Nothing left to collect on any island for the classes you have picked. "
                + "Hand-ins and turned-in rewards are not listed here — switch to Class view "
                + "above to see them."));
            return;
        }

        foreach (var island in layout.Groups)
        {
            var heading = DesignSystem.Text(Role.TitleSection,
                $"{island.Heading}   {island.Done}/{island.Total}");
            heading.TextWrapping = TextWrapping.Wrap;
            heading.Margin = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceL,
                0, DesignTokens.SpaceXs);
            heading.Ink("AccentBrush");
            QuestsPanel.Children.Add(heading);

            foreach (var row in island.Rows)
            {
                // The island view never locks a row: locking is the Epic tab's master-complete
                // and there is no Epic island.
                // The class rides the TITLE and the reward rides the owner run — both of them
                // Core's strings, and which one carries the class is Core's decision too
                // (SkyIslandRow.Title). This method picks neither.
                if (ChecklistRowControl(row.Row, setters, locked: false, lockedClassName: "",
                        owner: row.Reward, title: row.Title) is { } control)
                {
                    QuestsPanel.Children.Add(control);
                    // Recorded on ADD, not on build: a row whose setter is missing returns null
                    // and never reaches the screen, and the dump has to answer for the screen.
                    if (_lastIslandRowTitle.Length == 0)
                    {
                        _lastIslandRowTitle = row.Title;
                        _lastIslandRowOwner = row.Reward;
                    }
                }
            }
        }
    }

    /// <summary>What the LAST island render drew, for the <c>EQBUDDY_EXPAND</c> dump — the
    /// screen's answer, not the store's, which is the distinction trap 56 is about.</summary>
    private QuestChecklistLayout.SkyIslandLayout? _lastIslandLayout;

    /// <summary>The blocked sentence that actually reached the panel (DRA-218), for the
    /// <c>EQBUDDY_EXPAND</c> dump. The SCREEN's answer and not the group's — trap 56's
    /// distinction, and the reason it is written where the control is added rather than
    /// where <c>BlockedNote</c> is asked. Cleared with the panel (trap 38).</summary>
    private string _lastBlockedNote = "";

    /// <summary>The first group the class-view render was handed, and how much work it has
    /// left (DRA-218). The ORDER is the acceptance bar for the completion lens and no setting
    /// can assert it; these two are what an E2E reads. "-" / -1 when the render drew no class
    /// view at all, which is a different claim from "it drew an empty one" (trap 38).</summary>
    private string _lastFirstGroupHeading = "";

    private int _lastFirstGroupRemaining = -1;

    /// <summary>The first island row that actually reached the panel, as the two strings the
    /// control was built from (plan P8). Reset on every island render and cleared by a class
    /// render, so "-" is the honest answer for "no island row is on screen" rather than the
    /// previous render's leftovers (trap 38).</summary>
    private string _lastIslandRowTitle = "";

    private string _lastIslandRowOwner = "";

    /// <summary>One dump value: "-" when absent, and spaces underscored because the dump is
    /// space-separated <c>key=value</c> and a space would silently corrupt the NEXT pair.</summary>
    private static string Dumped(string value) =>
        value.Length == 0 ? "-" : value.Replace(' ', '_');

    /// <summary>The class strip's own chips, off the STRIP and not off the list that built
    /// it — an absent control photographs as an unremarkable panel (trap 29), and the whole
    /// claim here is that the chips on screen are the classes the view is about. <c>""</c>
    /// is the Any chip's key; see the field note on why Any is keyed on empty.</summary>
    private string ClassStripFact() => Dumped(string.Join("+", _classes.Keys
        .Select(k => (string)k)
        .Select(k => k.Length == 0 ? "Any" : QuestClassFilter.Abbrev(k))));

    /// <summary>WHICH chip the strip is painting selected (DRA-199) — read off the REAL strip
    /// through <see cref="EqSegmentedStrip.Selected"/>, deliberately NOT off
    /// <c>_classLens</c>.
    ///
    /// <para><b>The field and the screen are different claims, and this slice exists because
    /// they can disagree.</b> Un-picking the class the lens is on drops that chip; the lens is
    /// supposed to fall back to Any. A fact read off <c>_classLens</c> would report the
    /// fallback even on a build that had stopped repainting the selection — measured: with
    /// <c>ApplyTabVisual</c>'s <c>Select</c> removed, the field says <c>Any</c> and the strip
    /// lights nothing, and only this reading fails. Trap 42, on the surface whose whole
    /// complaint was a chip that did not track. Abbreviated and underscore-free for
    /// <see cref="ClassStripFact"/>'s reasons — same strip, same dump line.</para>
    ///
    /// <para><c>-</c> is "no chip is lit", which covers both the collapsed strip (no chips at
    /// all) and a lens stranded on a class whose chip has gone. It is a sentinel rather than
    /// an empty value because the dump is space-separated <c>key=value</c>.</para></summary>
    private string ClassLensFact() => Dumped(_classes.Selected is string key
        ? key.Length == 0 ? "Any" : QuestClassFilter.Abbrev(key)
        : "");

    /// <summary>
    /// **THE LENS PROBE's one entry** (DRA-199, AUTHORIZED by Helm <c>d15c1369</c>) — driven
    /// only by the <c>EQBUDDY_LENSPROBE</c> rendezvous in <see cref="DebugHooks"/>, which is
    /// inert unless a scenario armed it.
    ///
    /// <para><b>Why it exists.</b> Both writers of the class lens are <c>onClick</c> handlers
    /// on controls inside this window, and nothing in <c>tests/EQBuddy.E2E</c> can put a
    /// pointer on one — the suite may not assert the screen, let alone drive it. The lens is
    /// not persisted either, so it cannot be seeded before launch. Same shape, and the same
    /// reason, as the door / pet / ★ probes.</para>
    ///
    /// <para><b>Both verbs drive a REAL writer, never a private path.</b> <c>lens</c> calls
    /// <see cref="LensTo"/> — the chip's own click body. <c>picks</c> calls
    /// <c>QuestLedgerStore.SetClasses</c>, which is the writer EQBuddy Mobile uses
    /// (<c>CompanionActions.SetClasses</c>), and it deliberately forces NO refresh: that
    /// remote writer has no way to force one either, so the repaint has to come from the
    /// <c>off:</c> term D4 put in the signature. Forcing one here would test a path the phone
    /// does not have and would hide trap 72 on this surface.</para>
    ///
    /// <para>Returns false for anything it cannot honour — an unknown verb, or picks with no
    /// character key — so a staging mistake times out naming the probe rather than reading as
    /// a feature that did not fire.</para>
    /// </summary>
    /// <param name="verb"><c>lens</c> or <c>picks</c>.</param>
    /// <param name="arg">For <c>lens</c>, a class name or <c>-</c> for the Any chip. For
    /// <c>picks</c>, <c>+</c>-separated class names, or <c>-</c> for none picked.</param>
    internal bool ProbeLens(string verb, string arg)
    {
        switch (verb)
        {
            case "lens":
                LensTo(arg == "-" ? null : arg);
                return true;
            case "picks":
                var key = _main.QuestCharacterKey;
                if (_main.QuestLedger is not { } ledger || key.Length == 0) return false;
                ledger.SetClasses(key, arg == "-"
                    ? []
                    : [.. arg.Split('+', StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries)]);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// ONE checklist row, wired to its own tick — the control both Sky arrangements draw.
    ///
    /// <para><b>Lifted out when the island view landed (DRA-164), and that is the point.</b>
    /// The island view is a rearrangement of the rows the class view already produces, so a
    /// second copy of this block would be a second producer of what a row LOOKS like and what
    /// pressing its box DOES — trap 4 with a renderer's clothes on, and the two would part
    /// company the first time one of them learned something.</para>
    ///
    /// <para>Returns <c>null</c> when nothing can set this row, which is the old <c>continue</c>
    /// kept honest: a checkbox with no setter is a control that silently ignores clicks.</para>
    /// </summary>
    /// <param name="owner">"Belt of the Four Winds", on the island view only — the REWARD,
    /// whose heading is no longer above the row. In class view that heading is on screen and
    /// repeating it would be the redundancy the six questions exist to remove.</param>
    /// <param name="title">The row title to draw when it is not <c>row.Title</c> — the island
    /// view's class-prefixed "[Cleric] Wind Rune Fana" (<c>SkyIslandRow.Title</c>, Founder
    /// CLARIFY 2026-09-17). Empty means "the row's own title", so class view is untouched by
    /// the parameter's existence rather than by a second branch through it.</param>
    private UIElement? ChecklistRowControl(
        QuestChecklistRow row, Dictionary<string, Action<bool>> setters,
        bool locked, string lockedClassName, string owner = "", string title = "")
    {
        var text = DesignSystem.Text(Role.Body, "");
        text.TextWrapping = TextWrapping.Wrap;
        text.Inlines.Add(new System.Windows.Documents.Run(
            title.Length > 0 ? title : row.Title));
        if (owner.Length > 0)
        {
            // Whose work this is, between the step and where it drops: the row reads
            // "what · who wants it · where it comes from", which is the order a player
            // scanning one island's list needs them in.
            var whose = new System.Windows.Documents.Run("   " + owner);
            whose.SetResourceReference(System.Windows.Documents.Run.ForegroundProperty, "AccentBrush");
            text.Inlines.Add(whose);
        }
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
        text.Ink(row.Acquired || row.IsSkipped ? "DimBrush" : "TextBrush");
        // "Not doing this one." Struck through and dimmed, so a skipped step reads as
        // deliberately set aside rather than as merely unfinished.
        if (row.IsSkipped) text.TextDecorations = TextDecorations.Strikethrough;

        // A stub step says so, in the player's words, under its own title. It stays
        // fully tickable — manual state beats weak inference, and "we could not find
        // directions" is a fact about US, not about how far the player has got.
        // A VERTICAL StackPanel: TextWrapping does nothing in a horizontal one (trap 14).
        FrameworkElement content = GuideSubLines(text, row);

        var check = new CheckBox
        {
            Tag = row.GuideRowKey.Length > 0 ? GuideRowTag : null,
            Content = content,
            IsChecked = row.Acquired,
            Margin = new Thickness(DesignTokens.SpaceM, 1, 0, 1),
            // The six questions live on the hover for a guide row: the row itself
            // says what to do and where, and repeating why and how inline is the
            // redundancy the six are meant to remove (David, 2026-09-09).
            ToolTip = row.Unassigned
                ? "EQBuddy ticked this itself — several classes want this item and the "
                  + "log couldn't say which one earned it. Move the tick if it's on the "
                  + "wrong class; either way, toggling it settles the question."
                : row.GuideFacts.Length > 0 ? row.GuideFacts : null,
        };
        if (locked)
        {
            check.IsEnabled = false;
            // And LOOK disabled. The app's CheckBox style carries no disabled
            // visual, so IsEnabled alone leaves a control that reads as live and
            // silently ignores clicks — the "silent no-ops are broken" rule with
            // the switch on the other side. Found by looking at the screenshot.
            check.Opacity = 0.5;
            check.ToolTip = $"{lockedClassName}'s epic is marked complete. "
                + "Reopen it above to change individual steps.";
        }
        if (!setters.TryGetValue(row.Id, out var set)) return null;
        check.Checked += (_, _) => Tick(true);
        check.Unchecked += (_, _) => Tick(false);
        return WithImproveDoor(check, row);

        void Tick(bool done)
        {
            set(done);
            _settings.Save();
            PushUndo(row, done, set);
            Refresh(force: true);
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

    /// <summary>A hand-in happened: zero the whole count for this item. The arithmetic
    /// lives in the store (it must offset Verified as well as Looted since #241);
    /// hand-rolling it here is how the right-click went silently dead on dump-verified
    /// rows once.</summary>
    private void ClearCount(string item)
    {
        var key = _main.QuestCharacterKey;
        if (_main.QuestLedger is not { } ledger || key.Length == 0) return;
        ledger.ClearCount(key, item);
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

    /// <summary>Moved from the widget's gear menu (gear-menu-slim faces §A3, DRA-25) — an
    /// import belongs on the surface its output lives on, and this checklist is that
    /// surface. <see cref="QuestChecklistView"/> still owns the dialog and the preview; this
    /// view only relocated the button that reaches it.</summary>
    private void OnImportAchievementsClick(object sender, RoutedEventArgs e) =>
        _main.OnImportAchievements(sender, e);

    /// <summary>Same relocation, same "flash a confirmation" contract
    /// <see cref="OnCopyInventoryCmd"/> already uses — a silent clipboard write reads as a
    /// silent no-op.</summary>
    private void OnCopyAchievementsClick(object sender, RoutedEventArgs e)
    {
        _main.OnCopyAchievementsCommand(sender, e);
        CopyAchievementsBtn.Content = IconLabel("Check", "copied", "GoodBrush");
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        t.Tick += (_, _) =>
        {
            CopyAchievementsBtn.Content = "Copy /outputfile achievements";
            t.Stop();
        };
        t.Start();
    }

    // ---- the borderless window's chrome, reached through the host ----
    //
    // This view carries the whole bordered panel — there is no clean chrome/content seam
    // in a hand-drawn window — so the close button and the drag handler come with it and
    // ask the HOST to act, exactly as SpawnsView does. Both are inert in a room, because
    // QuestsRoom collapses the title row (nothing to click) and the shell's own native
    // title bar is what a player drags.

    private void OnClose(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        // Only a borderless host is dragged by its body. The shell is a normal window with
        // native chrome, and calling DragMove on it from inside a room would make the whole
        // window move when a player meant to select text in a quest row.
        if (Window.GetWindow(this) is { WindowStyle: WindowStyle.None } frameless)
            frameless.DragMove();
    }
}
