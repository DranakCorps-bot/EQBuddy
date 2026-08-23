using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Experience room: XP and AAs, skill-ups, and what a ding just unlocked.
///
/// **Lifted out of <c>MainWindow</c> for PR A** (Fable 5, 2026-08-22) — eleven fields, a
/// builder and a ~55-line paint branch that between them were the largest single thing
/// standing between this lane and the <see cref="IWidgetCard"/> seam.
///
/// It takes its class source and level accessor as functions rather than reaching for the
/// window, matching what WPF's twin takes. That is the difference between a lift and a
/// relocation: <c>MainWindow</c> carries fewer internal members after this, not more.
/// </summary>
/// <param name="settings">Two fold states live here (<c>ShowNextUnlocks</c>,
/// <c>ShowAllAAs</c>) and this view is their only writer — which is the thing trap 20 says
/// to check when a surface moves.</param>
/// <param name="classes">Which classes to filter unlocks by: the Quest Tracker's picked
/// classes, falling back to the combat-inferred one (the Gear Locker rule, #104).</param>
/// <param name="storedLevel">The last level the log ever announced for this character,
/// persisted, so "what do I get at N" survives a restart.</param>
/// <param name="repaint">Ask the host for a repaint after a fold is toggled.</param>
internal sealed class ProgressCardView(
    AppSettings settings,
    Func<StatsSnapshot, IReadOnlyList<string>> classes,
    Func<int?> storedLevel,
    Action repaint) : IWidgetCard
{
    private readonly TextBlock _summary = AppTheme.DimText("");
    private readonly TextBlock _levelUnlocksLabel = AppTheme.Heading("");
    private readonly ItemsControl _levelUnlocksList = new();
    private readonly TextBlock _nextUnlocksLabel = AppTheme.Heading("");
    private readonly ItemsControl _nextUnlocksList = new();
    private readonly TextBlock _skillLabel = AppTheme.Heading("Skill-ups");
    private readonly ItemsControl _skillList = new();
    private readonly TextBlock _aaNewLabel = AppTheme.Heading("AA learned this session");
    private readonly ItemsControl _aaNewList = new();
    private readonly EqFoldLabel _aaAbilitiesLabel = new() { Section = true };
    private readonly ItemsControl _aaAbilityList = new();
    private StackPanel? _body;

    // The ding memo, moved with the surface it serves: LevelUnlocks.UnlocksAt walks the
    // catalogs, and this paints every tick the room is open.
    private int _dingLevelMemo = -1;
    private string _dingClassesMemo = "";
    private LevelUnlockSet _dingUnlocks = LevelUnlockSet.Empty;

    public string Key => "progress";

    public Control Body => _body ??= Build();

    /// <summary>Skill-up row count, for the <c>EQBUDDY_EXPAND</c> dump the widget writes.</summary>
    public int SkillRowCount => _skillList.Items.Count;

    private StackPanel Build()
    {
        var panel = new StackPanel();
        _summary.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        panel.Children.Add(_summary);
        // Ding, and the room answers "what did I just get?" — AAs first (labeled, not
        // guessed: the wiki doesn't say which classes they cover), then spells.
        _levelUnlocksLabel.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        _levelUnlocksLabel.IsVisible = false;
        panel.Children.Add(_levelUnlocksLabel);
        _levelUnlocksList.IsVisible = false;
        panel.Children.Add(_levelUnlocksList);
        // "What do I get at N?" without waiting for a ding — click to fold.
        _nextUnlocksLabel.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        _nextUnlocksLabel.IsVisible = false;
        _nextUnlocksLabel.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(_nextUnlocksLabel,
            "The next level that unlocks anything for your classes — click to expand or fold");
        _nextUnlocksLabel.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            settings.ShowNextUnlocks = !settings.ShowNextUnlocks;
            settings.Save();
            repaint();
        };
        panel.Children.Add(_nextUnlocksLabel);
        _nextUnlocksList.IsVisible = false;
        panel.Children.Add(_nextUnlocksList);
        // Hidden when there is nothing under it — a heading with no rows reads as a
        // surface that failed to load. Its WPF twin had the same bug and the same fix.
        _skillLabel.IsVisible = false;
        panel.Children.Add(_skillLabel);
        panel.Children.Add(_skillList);
        // Session-new AAs lead (Reddit, 2026-08-11); the full character ledger folds
        // behind the ▸ label, Pet-abilities style.
        _aaNewLabel.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        _aaNewLabel.IsVisible = false;
        panel.Children.Add(_aaNewLabel);
        _aaNewList.IsVisible = false;
        panel.Children.Add(_aaNewList);
        _aaAbilitiesLabel.Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        _aaAbilitiesLabel.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(_aaAbilitiesLabel,
            "Everything the log's history (plus the durable ledger) says this character owns — "
            + "click to expand or fold");
        _aaAbilitiesLabel.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            settings.ShowAllAAs = !settings.ShowAllAAs;
            settings.Save();
            repaint();
        };
        panel.Children.Add(_aaAbilitiesLabel);
        panel.Children.Add(_aaAbilityList);
        return panel;
    }

    public void Render(StatsSnapshot s)
    {
        // The shared builder with this build's plain-ASCII separator ("-", the file's own
        // convention under fonts Wine/Linux may lack).
        _summary.Text = ProgressText.Summary(s, " - ");
        // Ding: the AA group in its category order (labeled, not guessed — the wiki
        // doesn't say which classes they cover); the Spells grouping follows, its rows
        // marked "… spell".
        var ding = DingUnlocks(s);
        _levelUnlocksLabel.IsVisible = ding.Count > 0;
        _levelUnlocksList.IsVisible = _levelUnlocksLabel.IsVisible;
        if (ding.Count > 0 && s.LastLevel is { } dingLevel)
        {
            _levelUnlocksLabel.Text = LevelUnlockText.NewAtLevelLabel(dingLevel);
            CardParts.FillList(_levelUnlocksList, UnlockRows(ding), tooltip: UnlockTooltip(ding));
        }

        // "What do I get at N?" without waiting for a ding — the next milestone that
        // unlocks anything, anchored to the last level the log ever announced (persisted
        // per character, so it works across restarts). Hidden until a level is known:
        // previewing from an unknown level would be a guess.
        var knownLevel = s.LastLevel ?? storedLevel();
        var next = knownLevel is { } kl ? LevelUnlocks.Next(classes(s), kl) : null;
        _nextUnlocksLabel.IsVisible = next is not null;
        if (next is { } nx)
        {
            _nextUnlocksLabel.Text = LevelUnlockText.NextLabel(
                nx.Level, nx.Unlocks.Aas.Count, nx.Unlocks.Spells.Count, settings.ShowNextUnlocks);
            _nextUnlocksList.IsVisible = settings.ShowNextUnlocks;
            if (settings.ShowNextUnlocks)
                CardParts.FillList(_nextUnlocksList, UnlockRows(nx.Unlocks), tooltip: UnlockTooltip(nx.Unlocks));
        }
        else _nextUnlocksList.IsVisible = false;

        CardParts.FillList(_skillList, s.SkillUps.Select(k => (k.Skill, $"{k.Value} (+{k.Ups})")));
        _skillLabel.IsVisible = _skillList.Items.Count > 0;
        // AA display, rethought (Reddit, 2026-08-11: "is it supposed to just show newly
        // learned this session?" — yes, now it is): session-new AAs lead, the full ledger
        // folds behind a click, same idiom as Pet abilities.
        var newAas = ProgressText.SessionNewAas(s);
        _aaNewLabel.IsVisible = newAas.Count > 0;
        _aaNewList.IsVisible = _aaNewLabel.IsVisible;
        CardParts.FillList(_aaNewList, newAas.Select(a =>
                (a.Name, a.Rank > 1 ? $"rank {a.Rank}" : "")),
            tooltip: name => AaCatalog.Find(name)?.Effect);
        _aaAbilitiesLabel.IsVisible = s.AaAbilities.Count > 0;
        _aaAbilitiesLabel.Set(settings.ShowAllAAs, settings.ShowAllAAs
            ? "All AA abilities"
            : $"All AA abilities ({s.AaAbilities.Count})");
        _aaAbilityList.IsVisible = settings.ShowAllAAs;
        if (settings.ShowAllAAs)
            CardParts.FillList(_aaAbilityList, s.AaAbilities.Select(a =>
                    (a.Name, a.Rank > 1 ? $"rank {a.Rank}" : "")),
                tooltip: name => AaCatalog.Find(name)?.Effect);
    }

    private LevelUnlockSet DingUnlocks(StatsSnapshot s)
    {
        if (s.LastLevel is not { } level) return LevelUnlockSet.Empty;
        var picked = classes(s);
        var key = string.Join(",", picked);
        if (_dingLevelMemo != level || _dingClassesMemo != key)
        {
            _dingLevelMemo = level;
            _dingClassesMemo = key;
            _dingUnlocks = LevelUnlocks.UnlocksAt(picked, level);
        }
        return _dingUnlocks;
    }

    /// <summary>Unlock rows for FillList: the AA group in its category order, then the
    /// Spells grouping — same list, rows told apart by their value column.</summary>
    private static IEnumerable<(string Name, string Value)> UnlockRows(LevelUnlockSet set) =>
        set.Aas.Select(a => (a.Name, LevelUnlockText.RowValue(a)))
            .Concat(set.Spells.Select(sp => (sp.Name, LevelUnlockText.SpellRowValue(sp))));

    /// <summary>Tooltip lookup for a merged unlock list: spell rows show which classes get
    /// the spell and when (catalog facts, never invented effect text); AA rows keep the
    /// wiki effect prose. Resolved per set, since only it knows which group a name came
    /// from.</summary>
    private static Func<string, string?> UnlockTooltip(LevelUnlockSet set) =>
        name => set.Spells.Any(sp => sp.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ? LevelUnlockText.SpellTooltip(SpellLevelCatalog.Default.Find(name))
            : AaCatalog.Find(name)?.Effect;
}
