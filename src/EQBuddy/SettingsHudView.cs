using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The HUD block, host-neutral** — everything Settings knows about what EQBuddy PUTS ON
/// SCREEN while you play: which panels the widget shows and in what order, which stats fill
/// the minimised HUD, which floating windows may open, whether a double-click on a chip
/// toggles one, whether the Loot panel lists the target's drops, and how long the
/// "recent rate" window is.
///
/// **Blocks, not tabs, are the unit that moves** (Fable's SR series; <see cref="SettingsAlertsView"/>
/// was the first, <see cref="SettingsLookView"/> and <see cref="SettingsBehaviorView"/> the pair
/// before this one). It builds its own controls, carries its own visibility and spacing
/// (trap 15), and knows nothing about the window it hangs in — so v1 <c>OptionsWindow</c> keeps
/// its five tabs while the Evolved shell's Settings room composes the SAME block. **Each host
/// constructs its own instance** (trap 45), and **both hosts wrap one <c>AppSettings</c>**
/// (trap 13) — the block never loads settings for itself, because a second snapshot clobbers
/// the first one wholesale (#169).
///
/// **In the shell this block renders under the tab name "HUD"; in v1 it keeps the
/// "Cards &amp; windows" tab label it shipped with.** That split is signed (Bevel I-11 §3, Helm
/// 2026-09-05): the terminology ban's own scope line exempts v1 <c>OptionsWindow</c>, and
/// renaming shipped v1 copy for no player benefit is the #228 class. **The tab name is SR-5's
/// to spell** — it lands with `SettingsSurface` and the room, and nothing here declares it.
///
/// **This block is TRANSITIONAL and must not be built as though it will stay this size.**
/// Bevel's ruling in as many words: Surface A's SA-R star-retirement empties the HUD-stat grid
/// and the floating-window list card by card, so each SA-R PR edits THIS ONE block and both
/// hosts follow. That is the whole argument for it being a shared module rather than two
/// screens, and it is also why there is no strip control, no tab enum and no extra structure in
/// here — a scaffold built for a shape that is scheduled to shrink is a scaffold with no
/// consumer (trap 43).
///
/// **The three editors belong together, which is why they are one class and not three.** They
/// are three views of overlapping state: ticking a floating window STARS its stat, a star is a
/// HUD cell, and hiding a panel does not touch either. Keeping them in one place is what makes
/// "rebuild the other list" a line of code rather than a cross-file callback — and a stale list
/// here is the "tick box that lies" this screen already had to fix once.
///
/// **The vocabulary sweep ran here** (§4 of `docs/BEVEL-v2-staging-critique.md`, Helm-signed;
/// Bevel's I-11 §5 named the hits in advance, and lifting a block IS that block's sweep). Six
/// sentences were reworded on the way through, once, for both hosts: the two headings, the
/// panel-list blurb, the HUD-stat tooltip and its blurb, the double-click label and the
/// target-drops pair. <see cref="BreakoutPresentation"/>'s three player consts were reworded at
/// their source for the same reason a shared const the block PRINTS is a string the block shows
/// (SR-1's <c>AltTabPolicy</c> precedent).
///
/// **The prose-to-hover pass ran here too (Pass 1; Bevel's faces, Helm-signed 2026-09-08).**
/// The owner's complaint is the SHAPE of the screen rather than its words: a paragraph under
/// every control turns a screen whose job is "find your switch and flip it" into an essay you
/// scroll past. Five explanations moved onto an ⓘ beside the thing they explain — the panel
/// list's, the mini dashboard's, the floating-window list's, and the two under the
/// double-click and target-drops switches. **Not one word was rewritten**; the consts below
/// are the strings that shipped, hanging somewhere else. <see cref="SettingsProsePolicy"/> is
/// the rule that decided which, and <see cref="RecentRateBlurb"/> is the negative that keeps
/// it from meaning "hide everything": ten words is a caption, and it stayed in the body.
///
/// **DRA-352 D2 took three blocks off this screen, by Founder direction on the card's own
/// screenshot (2026-09-23).** The "No longer on the widget" catalog (<c>OverlaySections</c>'
/// retired list — Helm LOCKED the drop, data and all); the two Mini-dashboard notes that
/// Pass 1 had exempted from the hover rule, with the Restore-default-order button under them
/// (<c>MiniBarOrder</c> keeps its bar-drag writer, so an order is undone by dragging); and
/// the Floating windows list. That list was the ONE writer of
/// <c>AppSettings.DisabledBreakouts</c>, so the write MOVED before the list went (traps
/// 20/26): it is the pin on each floating window's own title bar now
/// (<see cref="BreakoutHost.SetAutoOpen"/>), which is the surface a player is looking at when
/// they decide a window should or should not open by itself.
/// </summary>
internal sealed class SettingsHudView
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private readonly Func<bool> _hostReady;
    private readonly Func<object, object> _resource;

    private bool Ready => _hostReady();

    public SettingsHudView(MainWindow main, OptionsViewModel vm, Func<bool> ready,
        Func<object, object> resource)
    {
        _main = main;
        _vm = vm;
        _hostReady = ready;
        _resource = resource;
    }

    private UIElement? _block;

    /// <summary>This instance's body, built on first ask and kept — the host re-shows it
    /// rather than re-building, so nothing a player has half-changed is thrown away by a tab
    /// switch.</summary>
    public UIElement Block => _block ??= Build();

    /// <summary>
    /// This instance's facts for the <c>EQBUDDY_EXPAND</c> dump, in the block's OWN
    /// vocabulary — see <see cref="SettingsLookView.DebugFacts"/> for why the host, not the
    /// block, adds the prefix (trap 58) and why these are counted off BUILT controls.
    ///
    /// <c>hudRetired</c> and <c>hudWindows</c> left with their blocks (DRA-352 D2).
    ///
    /// <c>hudHints</c> is the row with the most behind it: since the prose pass, four
    /// explanations on this screen exist ONLY behind an ⓘ (five until D2 took the
    /// floating-window list's), so an ⓘ that failed to build is a paragraph that has left
    /// the product with nothing on screen — and nothing in a diff, a build or a screenshot —
    /// to say so. Counted off BUILT buttons rather than off a list of them, which is the
    /// difference between a fact and a restatement of the source (trap 34/39).
    /// </summary>
    public string DebugFacts() => _block is null
        ? ""
        : $"hudPanels={_cards.Children.Count} " +
          $"hudStats={_miniStats.Children.Count} " +
          $"hudHints={_hints}";

    private StackPanel _cards = null!;
    private WrapPanel _miniStats = null!;
    private CheckBox _doubleClickChips = null!;
    private CheckBox _targetDrops = null!;
    private ComboBox _recentWindow = null!;
    private bool _built;

    // ---------------------------------------------------------------- the words ----

    /// <summary>Was "Overlay cards" until 2026-09-05. `\bcards?\b` is banned in shell scope
    /// and this block has ONE string set for both hosts, so the heading says what the list
    /// does rather than what we call its rows.</summary>
    internal const string PanelsHeading = "What EQBuddy shows";

    internal const string PanelsBlurb =
        "Every panel you leave visible shows while EQBuddy is open — one with nothing yet "
        + "says so in a line and fills in as it happens.";

    /// <summary>NOT reworded, deliberately: "mini dashboard" is not on the ban list, this
    /// block adds nothing beyond what re-hosting needs, and the v1 <c>PinWatchChips</c> row
    /// that stayed behind on the Watch tab still uses the phrase.</summary>
    internal const string HudStatsHeading = "Mini dashboard";

    internal const string HudStatsBlurb =
        "Which stats show on the HUD while EQBuddy is minimised. Each of these is the same "
        + "switch as the ★ on that panel's own heading — two views of one setting, not two "
        + "settings.";

    /// <summary>Was "Show this in the minimised pill. Same switch as the star on the card
    /// header." — "mini pill" is the sentence #326 banned by name and "card" is a ban row of
    /// its own.</summary>
    internal const string HudStatTip =
        "Show this on the HUD while EQBuddy is minimised — the same switch as the ★ on that "
        + "panel's own heading, not a second one.";

    /// <summary>Was "Double-click a HUD chip to open/close its breakout".</summary>
    internal const string DoubleClickChipsLabel =
        "Double-click a HUD chip to open or close its window";

    /// <summary>Reworded by OE-7. It used to end "…closing one with its ✕ stays quiet, since
    /// a double-click brings it right back", which described the ONE case in which the ✕ was
    /// silent; the ✕ is a plain close for everybody now, and a chip answers a single click
    /// without this box being ticked at all. What survives is what this switch still buys:
    /// one gesture instead of two.</summary>
    internal const string DoubleClickChipsBlurb =
        "Every HUD chip already peeks its panel on hover and keeps it open on a click, and "
        + "↗ from there pops the floating window out. With this on, a double-click on the "
        + "chip pops that window straight up — or dismisses it — in one gesture. Closing a "
        + "floating window with its ✕ only closes it for now, whatever this says: its chip "
        + "brings it back, and the pin beside its ✕ is where you stop one opening on its own.";

    /// <summary>Was "🎯 Show target drops in the Loot card".</summary>
    internal const string TargetDropsLabel = "🎯 Show target drops in the Loot panel";

    internal const string TargetDropsBlurb =
        "While you fight, the Loot panel lists what the creature can drop (eqlwiki) with your "
        + "own observed counts this session. Hover an item for its stats; click for full info.";

    internal const string RecentRateLabel = "Recent-rate window";

    internal const string RecentRateBlurb =
        "The \"Last Xm\" figures on Combat, Kills, Money, and Progress.";

    // -------------------------------------------------------------------- build ----

    /// <summary>
    /// The arrangement players already have, rebuilt in code so a host with no XAML of its own
    /// can hang it. The order is the one <c>OptionsWindow.xaml</c> declared: the panel list,
    /// the HUD stats, then the three strays that had accumulated under them (the floating
    /// windows list sat between the two until DRA-352 D2).
    ///
    /// **Nothing here is left for a host to position** (trap 15). The Gate 4 Loot breakout
    /// shipped correct, selected filter strips into a `ContentControl` XAML had declared
    /// `Visibility="Collapsed"` — invisible on every launch, and nothing in a diff, a test or a
    /// build could see it.
    /// </summary>
    private UIElement Build()
    {
        var panel = new StackPanel();

        panel.Children.Add(HeadingHint(PanelsHeading, PanelsBlurb, new Thickness(0, 0, 0, 2)));
        // THE GEAR CHECKLIST IMPORT BLOCK LEFT THIS TAB on 2026-09-05 (SR-2), one PR before
        // the rest of it moved into this file. Its heading, its explanation, the three
        // buttons and the status line are on the GEAR & LOOT card's Wishlist tab now, in both
        // of that surface's hosts. An import workflow is a domain action, not a setting.
        _cards = new StackPanel();
        panel.Children.Add(_cards);

        panel.Children.Add(HeadingHint(HudStatsHeading, HudStatsBlurb, new Thickness(0, 14, 0, 2)));
        _miniStats = new WrapPanel();
        panel.Children.Add(_miniStats);
        // The two Mini-dashboard notes, the Restore-default-order button and the Floating
        // windows list left here on 2026-09-23 (DRA-352 D2) — see the class summary for where
        // each one's job went.

        // The two switches keep their own margins on the ROW rather than on the box: a
        // checkbox offset ten units down inside the row would sit ten units below its own ⓘ,
        // which is the one thing an explanation attached to a control must not look like.
        _doubleClickChips = Check(DoubleClickChipsLabel,
            _main.Settings.DoubleClickChipsToggleBreakouts, new Thickness(0),
            () =>
            {
                if (!Ready) return;
                _main.Settings.DoubleClickChipsToggleBreakouts =
                    _doubleClickChips.IsChecked == true;
                _main.Settings.Save();
            });
        panel.Children.Add(HintRow(_doubleClickChips, DoubleClickChipsBlurb,
            new Thickness(0, 10, 0, 2)));

        _targetDrops = Check(TargetDropsLabel, _vm.ShowTargetDrops, new Thickness(0),
            () => { if (Ready) _vm.ShowTargetDrops = _targetDrops.IsChecked == true; });
        panel.Children.Add(HintRow(_targetDrops, TargetDropsBlurb, new Thickness(0, 12, 0, 0)));

        panel.Children.Add(BuildRecentRate());
        panel.Children.Add(Dim(RecentRateBlurb, new Thickness(0, 0, 0, 0)));

        _built = true;
        RenderAll();
        return panel;
    }

    /// <summary>The one row that is a Grid rather than a stack: a label on the left, the
    /// picker pinned right, exactly as the XAML had it.</summary>
    private UIElement BuildRecentRate()
    {
        var row = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(new TextBlock { Text = RecentRateLabel, FontSize = 12 });

        _recentWindow = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Right, Width = 90, FontSize = 12,
        };
        foreach (var choice in OptionsViewModel.WindowChoices) _recentWindow.Items.Add(choice);
        _recentWindow.SelectedIndex = _vm.RecentWindowIndex;
        _recentWindow.SelectionChanged += (_, _) =>
        {
            if (Ready) _vm.RecentWindowIndex = _recentWindow.SelectedIndex;
        };
        row.Children.Add(_recentWindow);
        return row;
    }

    public void RenderAll()
    {
        BuildCards();
        BuildMiniStats();
    }

    // ---------------------------------------------------------------- panels ----

    /// <summary>
    /// The panel list, rebuilt from scratch. Public because a palette swap has to redo it: the
    /// row Foreground is resolved with <c>FindResource</c> at build time rather than through a
    /// <c>DynamicResource</c>, so <see cref="SettingsLookView"/> asks its host to call this
    /// (trap 19's neighbour — a value resolved once does not follow a theme change).
    /// </summary>
    public void BuildCards()
    {
        // A host that never asked for Block has nothing on screen to repaint. Set inside
        // Build(), before its own RenderAll(), so the first paint is not skipped by its own
        // guard — the failure that reads as "the list is empty on launch".
        if (!_built) return;
        _cards.Children.Clear();
        foreach (var card in _vm.Cards)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Since 1.66.3 every unhidden card shows (with an empty state when it has
            // nothing yet) — Options is the whole truth, no self-hiding asterisks.
            row.Children.Add(new TextBlock
            {
                Text = card.Title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)_resource(card.Hidden ? "DimBrush" : "TextBrush"),
            });

            row.Children.Add(CardButton("↑", "Move up", 1, () => { _vm.MoveCard(card.Key, -1); Apply(); }));
            row.Children.Add(CardButton("↓", "Move down", 2, () => { _vm.MoveCard(card.Key, +1); Apply(); }));
            row.Children.Add(CardButton(card.Hidden ? "🙈" : "👁",
                card.Hidden ? "Show this panel" : "Hide this panel (data still collected)", 3,
                () => { _vm.ToggleCard(card.Key); Apply(); }));
            _cards.Children.Add(row);

            // "Money · Motes · Faction · Raids are tabs in here now" — #219. A fold is
            // invisible by construction: the row that would have told you where a card
            // went is the row that was removed, and this is the screen someone opens when
            // a card is missing. Metadata weight, under the card it belongs to.
            if (card.Absorbed is { } absorbed)
            {
                var note = new TextBlock
                {
                    Text = absorbed,
                    FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 2),
                };
                note.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
                _cards.Children.Add(note);
            }
        }
    }

    private void Apply()
    {
        _main.ApplySectionLayout();
        BuildCards();
    }

    private Button CardButton(string glyph, string tip, int column, Action action)
    {
        var b = new Button
        {
            Content = glyph, ToolTip = tip, FontSize = 11,
            Style = (Style)_resource("IconButton"), Margin = new Thickness(6, 0, 0, 0),
        };
        b.Click += (_, _) => action();
        Grid.SetColumn(b, column);
        return b;
    }

    // ------------------------------------------------------------- HUD stats ----

    /// <summary>
    /// Every minimised-HUD cell, as a tick box.
    ///
    /// Written on 2026-08-21 because the theme folds had quietly closed the only route to
    /// three of them. A stat's switch is the ★ on its panel heading; Progress, Gear &amp;
    /// Loot and Kills &amp; Drops moved five of those stars into windows, and this screen
    /// could only reach a star through the floating-window box for its kind — which exists
    /// for dps, hps, pet, loot, xp and buffs. Motes, coin and kills have none, so their stars
    /// lived only inside the very windows a player was calling "too much other junk that I
    /// don't care about" (#228, daetien-lab). Same family as trap 20: the fold rehomed the
    /// writers and lost the route to them.
    ///
    /// Deliberately the SAME setting as the star, not a parallel one — ticking here lights
    /// the panel's star, and the floating-window list re-reads it.
    /// </summary>
    public void BuildMiniStats()
    {
        _miniStats.Children.Clear();
        // OptionKeys and NOT Order since DRA-81: the top row's three stats are ★s again, and
        // `Order` is a formatting table for CELLS that will never hold them. Walking `Order`
        // here is precisely how SA-1 left three switches in the profile with no screen able
        // to offer them.
        foreach (var key in MiniBarPresentation.OptionKeys)
        {
            var check = new CheckBox
            {
                IsChecked = _main.Settings.MiniStats.Contains(key),
                Margin = new Thickness(0, 4, 14, 0),
                Content = new TextBlock
                {
                    Text = MiniBarPresentation.Names.GetValueOrDefault(key, key),
                    FontSize = 12,
                },
                ToolTip = HudStatTip,
            };
            check.Checked += (_, _) => Set(key, true);
            check.Unchecked += (_, _) => Set(key, false);
            _miniStats.Children.Add(check);
        }
        void Set(string key, bool on)
        {
            if (!Ready) return;
            _main.SetMiniStat(key, on);
        }
    }

    // ----------------------------------------------------------------- chrome ----

    private static TextBlock Heading(string text, Thickness margin)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = margin,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return block;
    }

    /// <summary>
    /// A heading and the paragraph that used to sit under it, now on an ⓘ beside it.
    ///
    /// The row itself is <see cref="DesignSystem.HintRow"/> — Pass 2 lifted the WrapPanel
    /// (trap 25) out of here so the three blocks it converted could not build a second,
    /// slightly different one. What stays here is the only part that is this block's: which
    /// TextBlock the heading is.
    /// </summary>
    private UIElement HeadingHint(string heading, string prose, Thickness margin) =>
        HeadingRow(heading, Hint(prose), margin);

    /// <summary>Same, for a hint the surface holds a reference to and re-points later.</summary>
    private UIElement HeadingRow(string heading, Button hint, Thickness margin) =>
        DesignSystem.HintRow(Heading(heading, new Thickness(0)), hint, margin);

    /// <summary>A control and its explanation, in the same shape — see
    /// <see cref="HeadingHint"/> for why this wraps.</summary>
    private UIElement HintRow(FrameworkElement control, string prose, Thickness margin) =>
        DesignSystem.HintRow(control, Hint(prose), margin);

    /// <summary>The ⓘ itself, and the only place this block counts one — see
    /// <see cref="DebugFacts"/>, which reports what was BUILT rather than how many the
    /// source names.</summary>
    private Button Hint(string prose)
    {
        var hint = DesignSystem.InfoHint(prose);
        hint.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        _hints++;
        return hint;
    }

    /// <summary>How many ⓘ affordances this instance has built.</summary>
    private int _hints;

    private TextBlock Dim(string text, Thickness margin) => new()
    {
        Text = text, Style = (Style)_resource("Dim"),
        TextWrapping = TextWrapping.Wrap, Margin = margin,
    };

    private CheckBox Check(string text, bool initial, Thickness margin, Action changed)
    {
        var label = new TextBlock { Text = text, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var box = new CheckBox { Content = label, Margin = margin, IsChecked = initial };
        box.Checked += (_, _) => changed();
        box.Unchecked += (_, _) => changed();
        return box;
    }
}
