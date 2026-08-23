using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Progress card — the XP summary, what a ding just unlocked, skill-ups, and the AA
/// ledger split into "learned this session" and the whole list.
///
/// **The first of the three heavy card BODIES to move**, and it could only move once two
/// things were true. Its pure logic had already left (<see cref="LevelUnlockRows"/> in
/// UI.Shared, so the Progress breakout stopped calling into <c>MainWindow</c> to draw its
/// own list), and <see cref="EqCardRows.Fill"/> grew the per-row tooltip and click lookups
/// that only <c>MainWindow.FillList</c> had — which was the actual reason these bodies were
/// stuck, rather than anybody's oversight. A surface that has to reach back into the window
/// for its drawing routine has not really been lifted.
///
/// Its rendered shape was pinned in <c>tests/EQBuddy.E2E</c> BEFORE the move
/// (<c>dingShown</c>, <c>dingRows</c>, <c>nextShown</c>, <c>nextRows</c>, <c>aaNew</c>,
/// <c>aaAll</c>) — the WPF layer has no unit tests (docs/TestPlan.md §5), so that assertion
/// from a launched app is the only thing standing between this and a silent regression.
///
/// It owns the two MEMOS as well as the drawing, which is the point: the widget and the
/// Progress breakout both read unlocks, and the memo now lives with the surface that
/// computes it instead of being a pair of internals on a 4,400-line window.
/// </summary>
internal sealed class ProgressCardView : IWidgetCard
{
    private readonly ICardContext _context;
    private readonly AppSettings _settings;
    private readonly Func<StatsSnapshot, IReadOnlyList<string>> _classes;
    private readonly Func<int?> _storedLevel;
    private readonly LevelUnlockMemo _unlocks;
    private readonly StackPanel _panel = new();

    private readonly TextBlock _summary = DesignSystem.Text(Tok.TypeRole.BodySecondary);
    private readonly TextBlock _dingLabel = SectionLabel();
    private readonly ItemsControl _dingList = new();
    private readonly ItemsControl _skillList = new();
    /// <summary>A FIELD, not an inline label, so it can hide when there is nothing under
    /// it. It was added unconditionally until 2026-08-22 and every session with no
    /// skill-ups drew a heading with nothing beneath it — visible in
    /// docs/screenshots/theme-inline-progress.png, and in the Progress WINDOW too, since
    /// both hosts draw this same view. The ding and AA labels beside it were fields from
    /// the start; this one was the odd one out.</summary>
    private readonly TextBlock _skillLabel = SectionLabel("Skill-ups");
    private readonly TextBlock _aaNewLabel = SectionLabel("AA learned this session");
    private readonly ItemsControl _aaNewList = new();
    private readonly EqFoldLabel _aaAllLabel = new() { Section = true, Open = false };
    private readonly ItemsControl _aaAllList = new();
    private readonly TextBlock _nextLabel = SectionLabel();
    private readonly ItemsControl _nextList = new();

    /// <summary>The per-class expanders under the "At level N" heading (David's ask,
    /// 2026-08-23; Bevel's rules, Helm-signed the same day). A panel rather than a second
    /// <see cref="ItemsControl"/> because each group is a heading plus its own rows, and
    /// <see cref="_nextList"/> stays as the ONE-group case: *"one inferred class = names
    /// under the heading, no lone expander."*</summary>
    private readonly StackPanel _nextGroups = new();

    /// <summary>Which class groups are open, keyed by class name. **A field, never a
    /// setting** — Bevel, Helm-signed: *"first inferred class open, the rest collapsed
    /// (session-only, not a setting)."* A player who folds Warrior away to read Druid is
    /// making a decision about the next thirty seconds, not about how their app opens
    /// tomorrow, and <c>DeadSettingTests</c> exists because settings outlive the surfaces
    /// that wrote them.
    ///
    /// Keyed by CLASS rather than by index so the choice survives the level moving on: the
    /// groups are rebuilt every render and a positional key would hand the player's fold
    /// to whichever class sorted into that slot next (the same trap a fixture hit when it
    /// asserted on <c>Named[0]</c>).</summary>
    private readonly Dictionary<string, bool> _openGroups = new(StringComparer.OrdinalIgnoreCase);

    public string Key => "progress";

    public UIElement Body => _panel;

    // ---- the EQBUDDY_EXPAND dump's view of this card, for the E2E assertions ----
    public bool DingShown => _dingLabel.Visibility == Visibility.Visible;
    public int DingRows => _dingList.Items.Count;
    public bool NextShown => _nextLabel.Visibility == Visibility.Visible;

    /// <summary>Every unlock row the preview is currently drawing, flat list or grouped —
    /// the number E2E has asserted since the Progress card existed, and it has to go on
    /// meaning the same thing after the split or the assertion silently stops covering
    /// anything (trap 39's shape).</summary>
    public int NextRows { get; private set; }

    /// <summary>How many per-class expanders the preview drew. 0 is the one-class case
    /// (names under the heading, no lone expander) — which is a real state, not an
    /// absence, so it needs its own fact rather than being read off
    /// <see cref="NextRows"/>.</summary>
    public int NextGroups { get; private set; }

    public int SkillRows => _skillList.Items.Count;

    /// <summary>Whether the "Skill-ups" heading is up. Pinned for the reason
    /// <c>MoneyCardView.SoldShown</c> is: a heading that stops appearing is invisible in a
    /// diff, a build and every unit test, and the WPF layer has none of its own.</summary>
    public bool SkillLabelShown => _skillLabel.Visibility == Visibility.Visible;
    public int AaNewRows => _aaNewList.Items.Count;
    public int AaAllRows => _aaAllList.Items.Count;

    /// <param name="classes">Which classes the unlock filter runs against — the Quest
    /// Tracker's picked classes, falling back to the combat-inferred one (#104). A card
    /// cannot derive this from its snapshot, so it is handed in.</param>
    /// <param name="storedLevel">The ledger's persisted level, used when the log has not
    /// announced one this session. Without it the preview would vanish on every restart.</param>
    public ProgressCardView(ICardContext context, AppSettings settings,
        Func<StatsSnapshot, IReadOnlyList<string>> classes, Func<int?> storedLevel)
    {
        _context = context;
        _settings = settings;
        _classes = classes;
        _storedLevel = storedLevel;
        _unlocks = new LevelUnlockMemo(classes, storedLevel);

        _summary.TextWrapping = TextWrapping.Wrap;
        _summary.Margin = (Thickness)Application.Current.FindResource("ListBlock");
        _dingLabel.Visibility = Visibility.Collapsed;
        _dingList.Visibility = Visibility.Collapsed;
        _dingList.Margin = (Thickness)Application.Current.FindResource("ListBlock");

        _aaNewLabel.Visibility = Visibility.Collapsed;
        _aaNewList.Visibility = Visibility.Collapsed;

        _aaAllLabel.Visibility = Visibility.Collapsed;
        _aaAllLabel.Cursor = System.Windows.Input.Cursors.Hand;
        _aaAllLabel.ToolTip = "Everything the log's history (plus the durable ledger) says "
            + "this character owns — click to expand or fold";
        _aaAllLabel.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _settings.ShowAllAAs = !_settings.ShowAllAAs;
            _settings.Save();
            Render(_context.CurrentSnapshot());
        };
        _aaAllList.Visibility = Visibility.Collapsed;

        _nextLabel.Visibility = Visibility.Collapsed;
        _nextLabel.Cursor = System.Windows.Input.Cursors.Hand;
        _nextLabel.ToolTip = "Spells and AA abilities that open up at your next milestone "
            + "level — click to expand or fold";
        _nextLabel.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _settings.ShowNextUnlocks = !_settings.ShowNextUnlocks;
            _settings.Save();
            Render(_context.CurrentSnapshot());
        };
        _nextList.Visibility = Visibility.Collapsed;
        _nextGroups.Visibility = Visibility.Collapsed;

        _panel.Children.Add(_summary);
        _panel.Children.Add(_dingLabel);
        _panel.Children.Add(_dingList);
        _panel.Children.Add(_skillLabel);
        _panel.Children.Add(_skillList);
        _panel.Children.Add(_aaNewLabel);
        _panel.Children.Add(_aaNewList);
        _panel.Children.Add(_aaAllLabel);
        _panel.Children.Add(_aaAllList);
        _panel.Children.Add(_nextLabel);
        _panel.Children.Add(_nextList);
        _panel.Children.Add(_nextGroups);
    }

    private static TextBlock SectionLabel(string text = "")
    {
        var block = DesignSystem.Text(Tok.TypeRole.Metadata, text);
        block.FontWeight = FontWeights.SemiBold;
        // SetResourceReference, not a lookup: a lookup inside a constructor can run before
        // the control is in a tree, which is trap 19 and cost two attempts to spot.
        block.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        return block;
    }

    // ---- unlock memos ----
    //
    // Memoized per (level, classes): the header cue and the Progress breakout both read
    // these on the tick, and the catalog scan's answer only moves on a ding, a ledger
    // level or a class pick (perf audit #1's rule).

    /// <summary>What the session's most recent ANNOUNCED level opened up. Empty until the
    /// log has actually said "Welcome to level N" — a ding list built from a remembered
    /// level would claim something just happened when nothing did.
    ///
    /// What opened up at this level, and what opens at the next — memoized in
    /// <see cref="LevelUnlockMemo"/>, which is shared with the Avalonia widget (it had a
    /// hand-copied twin) and with the window host that no longer owns a card view.</summary>
    public LevelUnlockSet DingUnlocks(StatsSnapshot s) => _unlocks.Ding(s);

    public (int Level, LevelUnlockSet Unlocks)? NextUnlockPreview(StatsSnapshot s) => _unlocks.Next(s);

    public void Render(StatsSnapshot s)
    {
        _summary.Text = string.Join(Environment.NewLine, ProgressPresentation.SummaryLines(s));

        // The ding's answer: what just became available at the session's latest level,
        // always shown while the level-up is on the card — same idiom as "AA learned this
        // session". AA class rows lead; Archetype rows are labeled, not guessed (the wiki
        // does not say which classes they cover); the Spells grouping follows.
        var ding = DingUnlocks(s);
        var dingVisible = ding.Count > 0 && s.LastLevel is not null;
        _dingLabel.Visibility = dingVisible ? Visibility.Visible : Visibility.Collapsed;
        _dingList.Visibility = _dingLabel.Visibility;
        if (dingVisible && s.LastLevel is { } dingLevel)
        {
            _dingLabel.Text = LevelUnlockText.NewAtLevelLabel(dingLevel);
            FillUnlocks(_dingList, ding);
        }

        // "What do I get at N?" without waiting for a ding. Hidden until a level is known:
        // previewing from an unknown level would be a guess — and hidden when no class is
        // in play, which is Bevel's rule and not a tidy-up. With no class the preview can
        // only be built from the class-agnostic AA categories, and `LevelUnlocks.Next`
        // then walks forward to whatever level has one: David's own card offered "At level
        // 39: 1 new AA ability" — an Archetype pet ability, five levels away, to a
        // character with no pet. A list that cannot be about you should not claim to be.
        var classes = _classes(s);
        var next = classes.Count > 0 ? NextUnlockPreview(s) : null;
        _nextLabel.Visibility = next is not null ? Visibility.Visible : Visibility.Collapsed;
        if (next is { } nx)
        {
            _nextLabel.Text = LevelUnlockText.NextLabel(
                nx.Level, nx.Unlocks.Aas.Count, nx.Unlocks.Spells.Count, _settings.ShowNextUnlocks);
            RenderNextBody(nx.Level, nx.Unlocks, classes);
        }
        else ClearNextBody();

        EqCardRows.Fill(_skillList,
            s.SkillUps.Select(k => new CardRow(k.Skill, $"{k.Value} (+{k.Ups})")));
        _skillLabel.Visibility = _skillList.Items.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        // AA display, rethought (Reddit, 2026-08-11: "is it supposed to just show newly
        // learned this session?" — yes): session-new AAs lead, the full ledger folds
        // behind a click, same idiom as Pet abilities.
        var newAas = ProgressText.SessionNewAas(s);
        _aaNewLabel.Visibility = newAas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _aaNewList.Visibility = _aaNewLabel.Visibility;
        EqCardRows.Fill(_aaNewList,
            newAas.Select(a => new CardRow(a.Name, a.Rank > 1 ? $"rank {a.Rank}" : "")),
            tooltip: name => AaCatalog.Find(name)?.Effect);

        _aaAllLabel.Visibility = s.AaAbilities.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _aaAllLabel.Set(_settings.ShowAllAAs, _settings.ShowAllAAs
            ? "All AA abilities"
            : $"All AA abilities ({s.AaAbilities.Count})");
        _aaAllList.Visibility = _settings.ShowAllAAs ? Visibility.Visible : Visibility.Collapsed;
        if (_settings.ShowAllAAs)
            EqCardRows.Fill(_aaAllList,
                s.AaAbilities.Select(a => new CardRow(a.Name, a.Rank > 1 ? $"rank {a.Rank}" : "")),
                tooltip: name => AaCatalog.Find(name)?.Effect);
        else _aaAllList.Items.Clear();
    }

    /// <summary>A merged unlock list: rows, hover and click all resolved against the SET
    /// they came from, because only it knows whether a name is a spell or an AA.</summary>
    private static void FillUnlocks(ItemsControl list, LevelUnlockSet set) =>
        EqCardRows.Fill(list,
            LevelUnlockRows.Rows(set).Select(r => new CardRow(r.Name, r.Value)),
            tooltip: LevelUnlockRows.Tooltip(set),
            onNameClick: MainWindow.UnlockClick(set));

    private void ClearNextBody()
    {
        _nextList.Visibility = Visibility.Collapsed;
        _nextList.Items.Clear();
        _nextGroups.Visibility = Visibility.Collapsed;
        _nextGroups.Children.Clear();
        NextRows = 0;
        NextGroups = 0;
    }

    /// <summary>
    /// The preview's body: one list, or one expander per class.
    ///
    /// **Why the split is worth chrome at all.** A Legends character is up to three
    /// classes at once (David, 2026-08-23), so a level's unlocks are naturally several
    /// lists — and "which of my classes is this for" was a question the flat list could
    /// only answer by making you read every row's value column. <see cref="LevelUnlockGroups"/>
    /// decides what is in each group and whether the split earns its chrome; this only
    /// draws it, which is why the phone and the Avalonia widget can agree without copying
    /// anything from here.
    /// </summary>
    private void RenderNextBody(int level, LevelUnlockSet set, IReadOnlyList<string> classes)
    {
        _nextList.Items.Clear();
        _nextGroups.Children.Clear();
        NextRows = 0;
        NextGroups = 0;
        if (!_settings.ShowNextUnlocks)
        {
            _nextList.Visibility = Visibility.Collapsed;
            _nextGroups.Visibility = Visibility.Collapsed;
            return;
        }

        var groups = LevelUnlockGroups.ByClass(set, classes);
        if (!LevelUnlockGroups.WorthGrouping(groups))
        {
            // One group is a heading with nothing to choose between. Names go straight
            // under the "At level N" line, exactly as they did before the split.
            _nextList.Visibility = Visibility.Visible;
            _nextGroups.Visibility = Visibility.Collapsed;
            FillUnlocks(_nextList, set);
            NextRows = _nextList.Items.Count;
            return;
        }

        _nextList.Visibility = Visibility.Collapsed;
        _nextGroups.Visibility = Visibility.Visible;
        var tooltip = LevelUnlockRows.Tooltip(set);
        var click = MainWindow.UnlockClick(set);
        var defaultOpen = LevelUnlockGroups.DefaultOpenIndex(groups);
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.IsEmpty)
            {
                // The class stays on screen and says so. Dropping it is indistinguishable
                // from that class not being one of yours, and a Warrior has no spell table
                // at ANY level — "nothing new" is the complete answer, not an absence.
                // No chevron: there is nothing behind it, and an affordance that opens
                // nothing is the trap-16 defect with the switch the other way.
                _nextGroups.Children.Add(SectionLabel(group.ClassName));
                var none = DesignSystem.Text(Tok.TypeRole.Body, LevelUnlockGroups.NothingNew(level));
                none.Margin = new Thickness(Tok.Indent, 1, 0, 1);
                none.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
                _nextGroups.Children.Add(none);
                NextGroups++;
                continue;
            }

            // The first class with something to show is open, the rest collapsed — until
            // the player says otherwise this session. `TryGetValue` rather than an up-front
            // seed, so a class that appears later (a new pick, a level that finally gives
            // the Monk something) still gets the default rather than inheriting whatever
            // the dictionary happened to hold.
            if (!_openGroups.TryGetValue(group.ClassName, out var open)) open = i == defaultOpen;

            var heading = new EqFoldLabel { Section = true };
            heading.Set(open, group.ClassName);
            heading.Cursor = System.Windows.Input.Cursors.Hand;
            heading.ToolTip = $"What {group.ClassName} gains at level {level} — "
                + "click to expand or fold";
            var name = group.ClassName;
            heading.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _openGroups[name] = !_openGroups.GetValueOrDefault(name, open);
                Render(_context.CurrentSnapshot());
            };
            _nextGroups.Children.Add(heading);
            NextGroups++;

            if (!open) continue;
            var rows = new ItemsControl();
            EqCardRows.Fill(rows,
                group.Rows.Select(r => new CardRow(r.Name, r.Value, Indent: true)),
                tooltip: tooltip, onNameClick: click);
            _nextGroups.Children.Add(rows);
            NextRows += rows.Items.Count;
        }
    }
}
