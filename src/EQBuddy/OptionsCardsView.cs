using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The three editors on Options → Cards &amp; windows: which CARDS the widget shows and in
/// what order, which stats fill the MINI DASHBOARD, and which BREAKOUT windows may open.
///
/// Lifted out of <c>OptionsWindow</c> rather than added to it. That file is a ratchet
/// hotspot and ran out of room the moment the mini-dashboard list was written;
/// <c>MezDurationsView</c> is the worked example, and CLAUDE.md's rule is to lift a surface
/// out rather than raise the ceiling.
///
/// **The three belong together, which is why this is one class and not three.** They are
/// three views of overlapping state: ticking a breakout STARS its stat, a star is a mini
/// cell, and hiding a card does not touch either. Keeping them in one place is what makes
/// "rebuild the other list" a line of code rather than a cross-file callback — and a stale
/// list here is the "tick box that lies" this screen already had to fix once.
/// </summary>
internal sealed class OptionsCardsView
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private readonly Func<bool> _ready;
    private readonly StackPanel _cards;
    private readonly WrapPanel _miniStats;
    private readonly WrapPanel _breakouts;
    private readonly TextBlock _breakoutsBlurb;
    private readonly Func<object, object> _resource;

    public OptionsCardsView(MainWindow main, OptionsViewModel vm, Func<bool> ready,
        StackPanel cards, WrapPanel miniStats, WrapPanel breakouts, TextBlock breakoutsBlurb,
        Func<object, object> resource)
    {
        _main = main;
        _vm = vm;
        _ready = ready;
        _cards = cards;
        _miniStats = miniStats;
        _breakouts = breakouts;
        _breakoutsBlurb = breakoutsBlurb;
        _resource = resource;
    }

    public void RenderAll()
    {
        BuildCards();
        BuildMiniStats();
        BuildBreakouts();
    }

    // ---------------------------------------------------------------- cards ----

    public void BuildCards()
    {
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
                card.Hidden ? "Show card" : "Hide card (data still collected)", 3,
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

    // --------------------------------------------------------- mini dashboard ----

    /// <summary>
    /// Every mini-dashboard cell, as a tick box.
    ///
    /// Written on 2026-08-21 because the theme folds had quietly closed the only route to
    /// three of them. A stat's switch is the star on its card header; Progress, Gear &amp;
    /// Loot and Kills &amp; Drops moved five of those stars into windows, and this screen
    /// could only reach a star through the BREAKOUT box for its kind — which exists for
    /// dps, hps, pet, loot, xp and buffs. Motes, coin and kills have none, so their stars
    /// lived only inside the very windows a player was calling "too much other junk that I
    /// don't care about" (#228, daetien-lab). Same family as trap 20: the fold rehomed the
    /// writers and lost the route to them.
    ///
    /// Deliberately the SAME setting as the star, not a parallel one — ticking here lights
    /// the card's star, and the breakout list re-reads it.
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
                ToolTip = "Show this in the minimised pill. Same switch as the star on the "
                    + "card header.",
            };
            check.Checked += (_, _) => Set(key, true);
            check.Unchecked += (_, _) => Set(key, false);
            _miniStats.Children.Add(check);
        }

        void Set(string key, bool on)
        {
            if (!_ready()) return;
            _main.SetMiniStat(key, on);
            // The breakout list reads MiniStats to decide whether a window can open, so it
            // goes stale the moment a star changes — the "tick box that lies" arriving from
            // the other direction.
            BuildBreakouts();
        }
    }

    // -------------------------------------------------------------- breakouts ----

    /// <summary>
    /// One checkbox per breakout kind, and ticking one TURNS THE WINDOW ON.
    ///
    /// It used to only clear the ✕-dismissal (discussion #45), while the switch that
    /// decides whether the window ever opens was the ★ on a card — so someone who came
    /// here, found "🐾 Pet", ticked it and saw nothing had to go and ask. That question
    /// kept coming back on Reddit (David, 2026-08-20), and the answer was always "yes,
    /// but also star it somewhere else", which is a tick box that lies.
    ///
    /// Unticking is deliberately NOT symmetric: it stops the window and leaves the star
    /// alone. For every kind but Buffs that same key is also a cell in the minimised
    /// pill, and quietly removing someone's pill cell because they closed a window would
    /// be a second silent surprise in the opposite direction.
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
                ToolTip = BreakoutPresentation.StarKey(pk) is null
                    ? BreakoutPresentation.WatchNote
                    : "Opens while the widget is minimised. Ticking this also stars the "
                      + "stat, so it shows in the mini pill too.",
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
            if (!_ready()) return;
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
            // The card's own ★ is the same setting seen from the other side; if the
            // widget is open behind Options it must not go on showing the old one.
            _main.SyncStarsFromSettings();
            // Ticking a breakout STARS its stat, so the mini list is now stale.
            BuildMiniStats();
        }
    }
}
