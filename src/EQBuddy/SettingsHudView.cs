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
/// **What the sweep deliberately did NOT touch, so the gap is named rather than silent:**
/// <c>OverlaySections</c>' retired list (<c>RetiredHeading</c>, <c>RetiredBlurb</c>,
/// <c>RetiredCard.Line</c>) and the fold notes beside it. That copy was signed as-is at #335
/// eleven hours before this lift, and Fable's SR series carries an explicit "no re-opening
/// #335 — the Retired list is consumed as-is (SR-3 re-hosts, never redesigns)". It says "card"
/// and "widget" on purpose, in the words a player who has just failed to find something is
/// scanning for. It is therefore NOT on <c>ShellTerminologyTests.ShellStringSources</c>, and
/// that is a decision with a signature behind it rather than an oversight — whoever lands the
/// Settings room re-asks the question with #335's author in the room.
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

    private StackPanel _cards = null!;
    private WrapPanel _miniStats = null!;
    private WrapPanel _breakouts = null!;
    private TextBlock _breakoutsBlurb = null!;
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

    /// <summary>Where three switches WENT (SA-1). Naming the destination without naming the
    /// origin is the #233 complaint; this screen is where someone looks for a switch that is
    /// gone, so it says both.</summary>
    internal const string PromotedStatsNote =
        "XP, DPS and HPS are not in this list because they are always on the HUD now — the "
        + "collapsed bar shows your name, your DPS and your XP%/hr whatever you pick here, "
        + "and the third number becomes HPS while healing is the weight of the last "
        + "half-minute. Their stars are gone; there is nothing left to switch off.";

    /// <summary>Was "Show this in the minimised pill. Same switch as the star on the card
    /// header." — "mini pill" is the sentence #326 banned by name and "card" is a ban row of
    /// its own.</summary>
    internal const string HudStatTip =
        "Show this on the HUD while EQBuddy is minimised — the same switch as the ★ on that "
        + "panel's own heading, not a second one.";

    /// <summary>Was "Double-click a HUD chip to open/close its breakout".</summary>
    internal const string DoubleClickChipsLabel =
        "Double-click a HUD chip to open or close its window";

    internal const string DoubleClickChipsBlurb =
        "With this on, double-click the 🎒 Loot, 🐾 pet or 🎯 watch chip to pop its window up "
        + "or dismiss it — and closing one with its ✕ stays quiet, since a double-click brings "
        + "it right back. The always-on XP number opens the Progress window the same way; "
        + "Damage and Healing no longer have a chip of their own, so their windows are "
        + "switched on above.";

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
    /// the HUD stats, the floating windows, then the three strays that had accumulated under
    /// them.
    ///
    /// **Nothing here is left for a host to position** (trap 15). The Gate 4 Loot breakout
    /// shipped correct, selected filter strips into a `ContentControl` XAML had declared
    /// `Visibility="Collapsed"` — invisible on every launch, and nothing in a diff, a test or a
    /// build could see it.
    /// </summary>
    private UIElement Build()
    {
        var panel = new StackPanel();

        panel.Children.Add(Heading(PanelsHeading, new Thickness(0, 0, 0, 2)));
        panel.Children.Add(Dim(PanelsBlurb, new Thickness(0, 0, 0, 2)));
        // THE GEAR CHECKLIST IMPORT BLOCK LEFT THIS TAB on 2026-09-05 (SR-2), one PR before
        // the rest of it moved into this file. Its heading, its explanation, the three
        // buttons and the status line are on the GEAR & LOOT card's Wishlist tab now, in both
        // of that surface's hosts. An import workflow is a domain action, not a setting.
        _cards = new StackPanel();
        panel.Children.Add(_cards);

        panel.Children.Add(Heading(HudStatsHeading, new Thickness(0, 14, 0, 2)));
        panel.Children.Add(Dim(HudStatsBlurb, new Thickness(0, 0, 0, 2)));
        _miniStats = new WrapPanel();
        panel.Children.Add(_miniStats);
        panel.Children.Add(Dim(PromotedStatsNote, new Thickness(0, 4, 0, 2)));

        panel.Children.Add(Heading(BreakoutPresentation.Heading, new Thickness(0, 14, 0, 2)));
        _breakoutsBlurb = Dim("", new Thickness(0, 0, 0, 2));
        panel.Children.Add(_breakoutsBlurb);
        _breakouts = new WrapPanel();
        panel.Children.Add(_breakouts);

        _doubleClickChips = Check(DoubleClickChipsLabel,
            _main.Settings.DoubleClickChipsToggleBreakouts, new Thickness(0, 10, 0, 0),
            () =>
            {
                if (!Ready) return;
                _main.Settings.DoubleClickChipsToggleBreakouts =
                    _doubleClickChips.IsChecked == true;
                _main.Settings.Save();
            });
        panel.Children.Add(_doubleClickChips);
        panel.Children.Add(Dim(DoubleClickChipsBlurb, new Thickness(0, 0, 0, 2)));

        _targetDrops = Check(TargetDropsLabel, _vm.ShowTargetDrops, new Thickness(0, 12, 0, 0),
            () => { if (Ready) _vm.ShowTargetDrops = _targetDrops.IsChecked == true; });
        panel.Children.Add(_targetDrops);
        panel.Children.Add(Dim(TargetDropsBlurb, new Thickness(20, 2, 0, 0)));

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
        BuildBreakouts();
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

        BuildRetired();
    }

    /// <summary>
    /// "No longer on the widget" — the cards that LEFT, under the list of the ones that
    /// stayed.
    ///
    /// The note above hangs a fold's old names under the card that absorbed them, and a
    /// SUBTRACTION has no such card: Quests and World did not merge into anything. Six names
    /// a player might hunt for had no row on this screen at all — recorded as a known cost
    /// when each cut shipped, ruled on by Bevel (I-11 §4) and Helm-signed 2026-09-05.
    ///
    /// It sits inside the same panel as the card rows, deliberately: this is the list a
    /// player is reading when they discover the row they came for is missing, and an answer
    /// one heading below where the question is asked is an answer they will find.
    ///
    /// **Consumed as-is by this lift, on the sign.** Every string here is
    /// <c>OverlaySections</c>' — see the note on this class about why they are not swept.
    /// </summary>
    private void BuildRetired()
    {
        if (OverlaySections.Retired.Count == 0) return;

        var heading = new TextBlock
        {
            Text = OverlaySections.RetiredHeading,
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 2),
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        _cards.Children.Add(heading);
        _cards.Children.Add(Meta(OverlaySections.RetiredBlurb, top: 0));

        foreach (var gone in OverlaySections.Retired)
            _cards.Children.Add(Meta(gone.Line, top: 2));
    }

    private static TextBlock Meta(string text, double top)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 0),
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        return block;
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
        foreach (var key in MiniBarPresentation.Order)
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
            // The floating-window list reads MiniStats to decide whether a window can open,
            // so it goes stale the moment a star changes — the "tick box that lies" arriving
            // from the other direction.
            BuildBreakouts();
        }
    }

    // -------------------------------------------------------- floating windows ----

    /// <summary>
    /// One checkbox per floating-window kind, and ticking one TURNS THE WINDOW ON.
    ///
    /// It used to only clear the ✕-dismissal (discussion #45), while the switch that
    /// decides whether the window ever opens was the ★ on a card — so someone who came
    /// here, found "🐾 Pet", ticked it and saw nothing had to go and ask. That question
    /// kept coming back on Reddit (David, 2026-08-20), and the answer was always "yes,
    /// but also star it somewhere else", which is a tick box that lies.
    ///
    /// Unticking is deliberately NOT symmetric: it stops the window and leaves the star
    /// alone. For every kind but Buffs that same key is also a cell on the minimised HUD,
    /// and quietly removing someone's HUD cell because they closed a window would be a
    /// second silent surprise in the opposite direction.
    /// </summary>
    public void BuildBreakouts()
    {
        _breakouts.Children.Clear();
        _breakoutsBlurb.Text = BreakoutPresentation.Blurb;
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            var name = kind.ToString();               // the DisabledBreakouts key
            var pk = BreakoutPresentation.Kind(name);

            // Drawn, never an emoji: this is the screen a Wine player opens to find out
            // why a window will not appear, and ⚔ ⚕ 🐾 are exactly what boxes there.
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = DesignSystem.Icon(BreakoutPresentation.Icon(pk), "DimBrush", 12);
            icon.Margin = new Thickness(0, 0, 5, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(icon);
            var label = new TextBlock
            {
                Text = BreakoutPresentation.Title(pk), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            content.Children.Add(label);

            var check = new CheckBox
            {
                IsChecked = IsOn(name),
                Margin = new Thickness(0, 2, 14, 0),
                Content = content,
                // Keyed on the KIND, from UI.Shared. It used to be "no star means this is
                // Watch" and a literal typed here — which said "mini pill", the phrase
                // Helm banned at #323(b)/#326, and which stopped being true the day SA-1
                // promoted dps and hps and left three kinds sharing that null.
                ToolTip = BreakoutPresentation.Note(pk),
            };
            check.Checked += (_, _) => Set(name, enabled: true);
            check.Unchecked += (_, _) => Set(name, enabled: false);
            _breakouts.Children.Add(check);
        }

        // Ticked means "this window may open", which needs BOTH halves to be true.
        bool IsOn(string name) =>
            !_main.Settings.DisabledBreakouts.Contains(name)
            && (BreakoutPresentation.StarKey(BreakoutPresentation.Kind(name)) is not { } star
                || _main.Settings.MiniStats.Contains(star));

        void Set(string name, bool enabled)
        {
            if (!Ready) return;
            if (enabled)
            {
                _main.Settings.DisabledBreakouts.Remove(name);
                // The half that was missing. Watch has no star to set — it opens for a
                // pinned rule, which is the player's pick to make.
                if (BreakoutPresentation.StarKey(BreakoutPresentation.Kind(name)) is { } star
                    && !_main.Settings.MiniStats.Contains(star))
                    _main.Settings.MiniStats.Add(star);
            }
            else if (!_main.Settings.DisabledBreakouts.Contains(name))
                _main.Settings.DisabledBreakouts.Add(name);
            _vm.Persist();
            // The panel's own ★ is the same setting seen from the other side; if the
            // widget is open behind Options it must not go on showing the old one.
            _main.SyncStarsFromSettings();
            // Ticking a floating window STARS its stat, so the HUD list is now stale.
            BuildMiniStats();
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
