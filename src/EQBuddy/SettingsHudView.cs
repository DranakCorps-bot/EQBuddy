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
/// double-click and target-drops switches; **Pass 3 added the sixth**, the "no longer on the
/// widget" blurb below. **Not one word was rewritten**; the consts below
/// are the strings that shipped, hanging somewhere else. <see cref="SettingsProsePolicy"/> is
/// the rule that decided which, and <see cref="RecentRateBlurb"/> is the negative that keeps
/// it from meaning "hide everything": ten words is a caption, and it stayed in the body.
///
/// **Two paragraphs deliberately did NOT move, and that is the whole judgement in this pass.**
/// <see cref="PromotedStatsNote"/> and <see cref="GlancePetNote"/> answer "where did my switch
/// GO", which is a question asked by somebody SCANNING a list for a row that is not in it —
/// they have no control of their own to hover, and an ⓘ nobody knows to hover is the same
/// thing as deleting them (traps 29/34, and #233's complaint in the first place). They are
/// enumerated with that reason in `SettingsProsePolicyTests` so the next pass does not "finish
/// the job" by hiding them.
///
/// **The VOCABULARY sweep deliberately did not touch <c>OverlaySections</c>' retired list**
/// (<c>RetiredHeading</c>, <c>RetiredBlurb</c>, <c>RetiredCard.Line</c>) or the fold notes
/// beside it. That copy was signed as-is at #335 eleven hours before this lift, and Fable's SR
/// series carries an explicit "no re-opening #335 — the Retired list is consumed as-is (SR-3
/// re-hosts, never redesigns)". It says "card" and "widget" on purpose, in the words a player
/// who has just failed to find something is scanning for. It is therefore NOT on
/// <c>ShellTerminologyTests.ShellStringSources</c>, and that is a decision with a signature
/// behind it rather than an oversight — whoever lands the Settings room re-asks the question
/// with #335's author in the room.
///
/// **Pass 3 (2026-09-08) reached that list all the same, and the two are not in conflict — a
/// prose-to-hover move rewrites nothing.** The owner ran the finished Pass 1 + Pass 2 build
/// against a live profile and this tab still opened on body prose, because the Pass 2 sweep
/// reads <c>Dim("…")</c> LITERALS and these paragraphs are consts declared in UI.Shared: a
/// guard that forbids the wrong thing cannot see a missing thing (trap 34).
/// <c>RetiredBlurb</c> is now the ⓘ beside the heading, WORD FOR WORD as #335 wrote it, and
/// the rows below it are still printed because each one NAMES A DOOR (trap 59; Pass 2's third
/// exemption kind, Helm-signed #458/#459). <see cref="BuildRetiredHeading"/> holds the line
/// between the two, and `SettingsProsePass3Tests` sweeps this block BY IDENTIFIER so the next
/// paragraph to arrive through a const is ruled on rather than invisible.
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
    /// <c>hudRetired</c> is the row with the most behind it: the "no longer on the widget"
    /// companion is #335's answer to the Options gap, it is the ONLY thing on either host
    /// naming six surfaces that left the HUD, and an absent panel photographs as an
    /// unremarkable list (trap 29/34). A comparison of the two hosts is what says the room
    /// kept it.
    ///
    /// <c>hudHints</c> joins it for the same reason and is the newer half of it: since the
    /// prose pass, six explanations on this screen exist ONLY behind an ⓘ, so an ⓘ that
    /// failed to build is a paragraph that has left the product with nothing on screen —
    /// and nothing in a diff, a build or a screenshot — to say so. Counted off BUILT
    /// buttons rather than off a list of the five, which is the difference between a fact
    /// and a restatement of the source (trap 34/39).
    /// </summary>
    public string DebugFacts() => _block is null
        ? ""
        : $"hudPanels={_cards.Children.Count} " +
          $"hudRetired={_retiredRows} " +
          $"hudStats={_miniStats.Children.Count} " +
          $"hudWindows={_breakouts.Children.Count} " +
          $"hudHints={_hints}";

    private StackPanel _cards = null!;
    private WrapPanel _miniStats = null!;
    private WrapPanel _breakouts = null!;

    /// <summary>The floating-window list's explanation, which is an ⓘ rather than a line of
    /// body prose since the prose pass. It kept its rebuild-on-render behaviour with its
    /// text: <see cref="BuildBreakouts"/> re-points it at <see cref="BreakoutPresentation.Blurb"/>
    /// every time it redraws the list, the way it re-pointed the TextBlock this replaced,
    /// so a blurb that ever becomes conditional cannot go stale here.</summary>
    private Button _breakoutsHint = null!;

    private Button _restoreOrder = null!;
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

    /// <summary>
    /// Pet damage can live on the always-on row (SIGNED #422; Bevel's §3/§4 ruling,
    /// Helm-signed 2026-09-08) — **a NOTE and deliberately not a control.**
    ///
    /// A button that set <c>HudGlancePet</c> would be a second AUTHOR of the exact fact the
    /// drag already authors, which is what makes it different from
    /// <see cref="RestoreOrderLabel"/> beneath it: that one only ever CLEARS an order back to
    /// canonical, and there is no clear-to-canonical shape for a bool whose default is false.
    /// So the gesture stays the only writer (#252), and what Options owes is the sentence
    /// naming it — the same job <see cref="PromotedStatsNote"/> does for the three switches
    /// SA-1 removed, which is why it sits directly beside it.
    ///
    /// **The second sentence is the un-star answer, and it is here because the alternative is
    /// a silent no-op.** While the slot is inserted, "pet" is out of
    /// <c>MiniBarPresentation.DrawnKeys</c> whatever the ★ says — so un-starring pet at that
    /// moment changes nothing on screen, there being no cell chip to lose. Without a sentence
    /// saying the ★'s job NARROWS rather than stops, a player who unticks it expecting the
    /// number gone sees nothing happen and reasonably reads that as broken.
    /// <see cref="PromotedStatsNote"/> could not cover it: those three lost their stars
    /// outright, and pet's still means something.
    /// </summary>
    internal const string GlancePetNote =
        "Pet damage can also sit on the always-on row up top, next to DPS — drag its chip "
        + "left past DPS to put it there, or drag it back down to return it here. Its ★ still "
        + "decides whether it can show at all; once it's on the top row, the ★ only decides "
        + "whether it comes back down here if you drag it off.";

    /// <summary>The undo for #191's chip drag. It says what it RESTORES rather than what it
    /// erases, because "clear your order" describes the implementation and "back to the
    /// order EQBuddy shipped" describes the bar the player is looking at.</summary>
    internal const string RestoreOrderLabel = "Restore default order";

    internal const string RestoreOrderTip =
        "Put the minimised bar's chips back in the order EQBuddy ships with — kills, pet "
        + "damage, weapon procs, loot, motes, coin, deaths, then the buff set. Only the "
        + "chips you have starred are drawn, and your character name, DPS and XP%/hr stay "
        + "first whatever you do. Off unless you have dragged a chip somewhere else.";

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
        + "brings it back, and the list above is where you stop one opening on its own.";

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
        panel.Children.Add(Dim(PromotedStatsNote, new Thickness(0, 4, 0, 2)));
        // Beside the note above rather than in a spot of its own: this is already where the
        // screen keeps its "here is what a drag on the bar changed, and here is how you would
        // know" sentences, and pet damage's row is the one ★ in the list whose meaning
        // depends on where the chip is sitting.
        panel.Children.Add(Dim(GlancePetNote, new Thickness(0, 4, 0, 2)));
        // THE WAY BACK FROM A DRAG (#191; Bevel's face, Helm-signed 2026-09-07 ~5:58 PM CT).
        // The order is set by carrying a chip on the bar, which is a gesture with nothing on
        // screen to say it happened — so the undo lives on the one screen that LISTS these
        // stats, under the checklist that keeps listing them canonically whatever the bar is
        // doing. It is drawn ALWAYS and DISABLED when there is nothing to restore, the same
        // rule "Follow the HUD again" follows: a control that only exists once you are lost
        // is a control nobody has seen before they need it (trap 17's other half — it is
        // dimmed, not merely inert).
        _restoreOrder = new Button
        {
            Content = RestoreOrderLabel,
            ToolTip = RestoreOrderTip,
            // ActionButton rather than the link style Bevel's note suggested: `SectionLink`
            // is a full-width navigation panel with an ↗ on it (it would read as a way OUT
            // of this screen), and — the half that decides it — it carries no `IsEnabled`
            // visual at all, so a disabled restore would render exactly like a live one and
            // swallow the click (trap 17). `ActionButton` is the light weight Bevel asked
            // for AND dims itself when it has nothing to do.
            Style = (Style)_resource("ActionButton"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 2),
        };
        _restoreOrder.Click += (_, _) =>
        {
            if (!Ready) return;
            // The one write that is not a drop, and it is a CLEAR rather than a second
            // author of an order: empty means canonical, so this restores the floor by
            // deleting the player's list instead of writing a copy of the default into it
            // (a literal here would be a second table to keep in step — trap 30).
            _main.Settings.MiniBarOrder.Clear();
            _main.Settings.Save();
            BuildMiniStats();
        };
        panel.Children.Add(_restoreOrder);

        _breakoutsHint = Hint(BreakoutPresentation.Blurb);
        panel.Children.Add(HeadingRow(BreakoutPresentation.Heading, _breakoutsHint,
            new Thickness(0, 14, 0, 2)));
        _breakouts = new WrapPanel();
        panel.Children.Add(_breakouts);

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
    /// **Every string here is <c>OverlaySections</c>', and not one of them was reworded by the
    /// lift or by the prose pass.** Pass 3 moved <c>RetiredBlurb</c> onto the heading's ⓘ
    /// and left the ROWS printed — see <see cref="BuildRetiredHeading"/> for the line between
    /// the two.
    /// </summary>
    private void BuildRetired()
    {
        // Counted as DRAWN, not as `OverlaySections.Retired.Count` — a fact read off a
        // static list would be the same number from both hosts whether either of them had
        // rendered a row or not, which is a guard that cannot fail (trap 34/39).
        _retiredRows = 0;
        if (OverlaySections.Retired.Count == 0) return;

        _cards.Children.Add(_retiredRow ??= BuildRetiredHeading());

        // **THE ROWS STAY PRINTED, and that is Pass 3's whole judgement.** Each one ends
        // "Right-click EQBuddy and choose “World…”" — it NAMES A DOOR, which is the third
        // exemption kind Helm signed at #458/#459. Nothing is bound by default (trap 59), this
        // is the screen a player opens at the moment they have failed to find a card, and an ⓘ
        // nobody knows to hover is the same thing as deleting the only printed answer to "how
        // do I get it back". `SettingsProsePass3Tests` holds the row and re-checks the door
        // against the record, so the seven cuts queued behind Surface A are covered the day
        // they land rather than the day somebody remembers this comment.
        foreach (var gone in OverlaySections.Retired)
        {
            _cards.Children.Add(Meta(gone.Line, top: 2));
            _retiredRows++;
        }
    }

    /// <summary>
    /// **"No longer on the widget", and the paragraph that used to sit under it — now on an ⓘ
    /// beside it (Pass 3, 2026-09-08).**
    ///
    /// The owner ran the finished Pass 1 + Pass 2 build and this tab still opened on body
    /// prose, with every row of both passes green: the Pass 2 sweep reads <c>Dim("…")</c>
    /// LITERALS, and this block prints a const declared in UI.Shared, so the paragraph was
    /// covered by nothing (trap 34, and why Pass 3's sweep asks what is PRINTED rather than
    /// what is written here).
    ///
    /// <c>RetiredBlurb</c> converts and the rows below it do not, because they are not the same
    /// kind of sentence. The blurb says the features are intact; the rows name the way back in.
    ///
    /// **Built ONCE and re-shown.** <see cref="BuildRetired"/> runs inside
    /// <see cref="BuildCards"/>, which runs on every panel move, every hide and every palette
    /// swap — a hint constructed there would push <c>hudHints</c> up on each redraw and turn
    /// the dump's only runtime check on this pass into a number that cannot fail its floor.
    /// It would also throw: <c>_cards.Children.Clear()</c> detaches this ROW but leaves the
    /// button parented to it, and a WPF element has exactly one parent.
    /// <see cref="_breakoutsHint"/> is the same answer to the same question.
    /// </summary>
    private UIElement BuildRetiredHeading()
    {
        var heading = new TextBlock
        {
            Text = OverlaySections.RetiredHeading,
            FontSize = 12, FontWeight = FontWeights.SemiBold,
        };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        // The margin belongs to the ROW, never to the heading inside it — a heading offset
        // twelve units down would sit twelve units below its own ⓘ (DesignSystem.HintRow).
        return DesignSystem.HintRow(heading, Hint(OverlaySections.RetiredBlurb),
            new Thickness(0, 12, 0, 2));
    }

    /// <summary>The retired heading and its ⓘ, built on the first render that has rows to show
    /// and re-added on every one after it — see <see cref="BuildRetiredHeading"/>.</summary>
    private UIElement? _retiredRow;

    /// <summary>How many "no longer on the widget" rows this instance last DREW. See
    /// <see cref="DebugFacts"/>.</summary>
    private int _retiredRows;

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
        // The checklist above stays in CANONICAL order whatever the bar is doing — it is a
        // catalog of what can be starred, not a mirror of the row — so this is the only thing
        // on the screen that knows the two can differ. Asked of the resolved order rather
        // than of "is the list empty": a saved order that has been dragged back to canonical
        // by hand is nothing to restore, and a button that would do nothing is disabled
        // rather than silently swallowing the click.
        if (_restoreOrder is not null)
            _restoreOrder.IsEnabled = !MiniBarPresentation.ResolveOrder(_main.Settings)
                .SequenceEqual(MiniBarPresentation.CanonicalOrder);

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
        DesignSystem.SetHintProse(_breakoutsHint, BreakoutPresentation.Blurb);
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
