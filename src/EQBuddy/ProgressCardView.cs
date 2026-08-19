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
    private readonly TextBlock _aaNewLabel = SectionLabel("AA learned this session");
    private readonly ItemsControl _aaNewList = new();
    private readonly EqFoldLabel _aaAllLabel = new() { Section = true, Open = false };
    private readonly ItemsControl _aaAllList = new();
    private readonly TextBlock _nextLabel = SectionLabel();
    private readonly ItemsControl _nextList = new();

    public string Key => "progress";

    public UIElement Body => _panel;

    // ---- the EQBUDDY_EXPAND dump's view of this card, for the E2E assertions ----
    public bool DingShown => _dingLabel.Visibility == Visibility.Visible;
    public int DingRows => _dingList.Items.Count;
    public bool NextShown => _nextLabel.Visibility == Visibility.Visible;
    public int NextRows => _nextList.Items.Count;
    public int SkillRows => _skillList.Items.Count;
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

        _panel.Children.Add(_summary);
        _panel.Children.Add(_dingLabel);
        _panel.Children.Add(_dingList);
        _panel.Children.Add(SectionLabel("Skill-ups"));
        _panel.Children.Add(_skillList);
        _panel.Children.Add(_aaNewLabel);
        _panel.Children.Add(_aaNewList);
        _panel.Children.Add(_aaAllLabel);
        _panel.Children.Add(_aaAllList);
        _panel.Children.Add(_nextLabel);
        _panel.Children.Add(_nextList);
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
        // previewing from an unknown level would be a guess.
        var next = NextUnlockPreview(s);
        _nextLabel.Visibility = next is not null ? Visibility.Visible : Visibility.Collapsed;
        if (next is { } nx)
        {
            _nextLabel.Text = LevelUnlockText.NextLabel(
                nx.Level, nx.Unlocks.Aas.Count, nx.Unlocks.Spells.Count, _settings.ShowNextUnlocks);
            _nextList.Visibility = _settings.ShowNextUnlocks ? Visibility.Visible : Visibility.Collapsed;
            if (_settings.ShowNextUnlocks) FillUnlocks(_nextList, nx.Unlocks);
            else _nextList.Items.Clear();
        }
        else
        {
            _nextList.Visibility = Visibility.Collapsed;
            _nextList.Items.Clear();
        }

        EqCardRows.Fill(_skillList,
            s.SkillUps.Select(k => new CardRow(k.Skill, $"{k.Value} (+{k.Ups})")));

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
}
