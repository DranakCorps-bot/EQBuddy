using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>Watch and Loot are WPF-only so far (pre-parity backlog); Buffs arrived
/// with #120 stage 2 and is ported here.</summary>
public enum BreakoutKind { Damage, Healing, Pet, Buffs }

/// <summary>Floating fight/session bar chart for outgoing damage, healing, or pet damage.</summary>
public sealed class BreakoutWindow : Window
{
    private readonly AppSettings _settings;
    private readonly BreakoutKind _kind;
    private readonly Border _chrome;
    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = AppTheme.DimText("");
    private readonly TextBlock _empty = AppTheme.DimText("");
    private readonly StackPanel _rows = new();
    private readonly Button _fight;
    private readonly Button _session;
    private readonly Button? _copyFight;
    private bool _fightScope;
    private string _signature = "";
    private PixelPoint _savedPosition;
    private LastFightInfo? _lastFight;
    private IReadOnlyDictionary<string, (int Casts, int Resists, int Blocked)>? _resists;
    private IReadOnlyDictionary<string, string>? _blockedBy;
    private (double Opacity, Color Tint) _appliedBg = (-1, default);

    /// <summary>Raised when the user ✕-dismisses the window — the owner disables this
    /// kind persistently (re-enabled under Options → Breakout windows, discussion #45).</summary>
    public event Action<BreakoutKind>? Dismissed;

    /// <summary>Damage kind only (#102): ⧗ opens the fight timeline. Set by the owner
    /// (WPF reaches this through its MainWindow reference; here the hook is explicit).</summary>
    public Action? OpenTimeline { get; set; }

    /// <summary>Whose parse the ⧉ fight export is labeled with — the owner supplies the
    /// current character name (WPF: Main.Identity.Character).</summary>
    public Func<string>? CharacterName { get; set; }

    /// <summary>Names the buffs that blocked a spell, for the "N blocked" row tooltip.
    /// The ledger lives on MainWindow (WPF reaches it through its Main reference; here
    /// the hook is explicit, same as the two above).</summary>
    public Func<StatsSnapshot, IReadOnlyDictionary<string, string>?>? BlockedBy { get; set; }

    /// <summary>The buff-set surface (#120 stage 2). Only the Buffs kind uses it, and
    /// it needs enough of the shell that an interface beats a dozen hooks.</summary>
    public IBuffSetHost? BuffHost { get; set; }

    public BreakoutWindow(AppSettings settings, BreakoutKind kind)
    {
        _settings = settings;
        _kind = kind;
        _fightScope = ScopeSetting() != "session";
        Title = $"EQBuddy {kind} breakout";
        Width = 310;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        // Auto-shown on the minimize pass, not by a click — without this, the first
        // Show() activates the window and yanks keyboard focus off the game mid-fight
        // (same bug the WPF chip stacks carried; every other auto-shown overlay
        // already declares it).
        ShowActivated = false;
        CanResize = false;

        _title.FontSize = 13;
        _title.FontWeight = FontWeight.Bold;
        _title.Foreground = AppTheme.TextBrush;
        _fight = ScopeButton("Fight", true);
        _session = ScopeButton("Session", false);
        var close = AppTheme.IconButton("x",
            "Hide this window for good (its star chip stays; re-enable under Options → Breakout windows)");
        close.Click += (_, _) => { HideAndSave(); Dismissed?.Invoke(_kind); };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto") };
        header.Children.Add(_title);
        if (_kind == BreakoutKind.Damage)
        {
            // #102 (jeremycranfill): the Combat card's fight export and timeline,
            // reachable without leaving the minimized view.
            _copyFight = DesignSystem.IconButton("Copy",
                "Copy the last fight as Discord-ready text (a monospace block — the official Discord " +
                "blocks images, so the parse travels as text). Your numbers only, from your log.",
                onClick: () => _ = OnCopyFight());
            Grid.SetColumn(_copyFight, 1); header.Children.Add(_copyFight);
            var timeline = DesignSystem.IconButton("Timeline",
                "Fight timeline: the whole pull, a lane per skill — every hit, miss and resist, " +
                "plus DPS over time.",
                onClick: () => OpenTimeline?.Invoke());
            Grid.SetColumn(timeline, 2); header.Children.Add(timeline);
        }
        // No Fight/Session axis on the buff set — the axis is the class combination,
        // named in the subheader. The in-place editor is this kind's whole point
        // (#120 stage 2): configuring the set never requires Options.
        _fight.IsVisible = _session.IsVisible = _kind != BreakoutKind.Buffs;
        Grid.SetColumn(_fight, 3); header.Children.Add(_fight);
        Grid.SetColumn(_session, 4); header.Children.Add(_session);
        Grid.SetColumn(close, 5); header.Children.Add(close);
        var panel = new StackPanel();
        panel.Children.Add(header);
        panel.Children.Add(_subtitle);
        panel.Children.Add(_empty);
        panel.Children.Add(_rows);
        if (_kind == BreakoutKind.Buffs) panel.Children.Add(BuildBuffEditor());
        // Hairline chrome (2026-08-11 modernization): the accent at a whisper, same
        // treatment as the main widget's cards.
        _chrome = new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 7, 10, 9),
            Child = panel,
        };
        Content = _chrome;
        WindowZoom.Attach(this, $"breakout:{kind}", settings);
        PointerPressed += (_, e) =>
        {
            if (e.Source is not Button && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        Opened += (_, _) => RestorePosition();
        PositionChanged += (_, _) => { if (IsVisible) _savedPosition = Position; };
        Closed += (_, _) => SavePosition();
        PaintScope();
    }

    private Button ScopeButton(string text, bool fight)
    {
        var button = AppTheme.IconButton(text, $"Show {text.ToLowerInvariant()} numbers");
        button.FontSize = 11;
        button.Click += (_, _) =>
        {
            _fightScope = fight;
            SetScopeSetting(fight ? "fight" : "session");
            _signature = "";
            PaintScope();
        };
        return button;
    }

    /// <summary>#102: the Combat card's fight export without leaving the minimized
    /// view — same Discord-ready text, same clipboard.</summary>
    private async Task OnCopyFight()
    {
        if (_lastFight is not { } f || _copyFight is null) return;
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(FightExport.ToText(
                    f, CharacterName?.Invoke() ?? "", $"v{UpdateChecker.CurrentVersion}"));
            _copyFight.Content = DesignSystem.Icon("Check", "GoodBrush");
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { _copyFight.Content = DesignSystem.Icon("Copy"); t.Stop(); };
            t.Start();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    // ---- background see-through (#96, badly-developed): breakouts follow the main
    // widget's setting — only the panel fades, text stays sharp, same rule as the
    // widget. Re-checked on the shared tick so Options changes and theme switches
    // reach an already-open breakout without a rebuild.
    private void ApplyBackgroundOpacity()
    {
        var opacity = _settings.BackgroundOpacity;
        var tint = AppTheme.BgBrush.Color;
        if (_appliedBg == (opacity, tint)) return;
        _appliedBg = (opacity, tint);
        _chrome.Background = new SolidColorBrush(
            Color.FromArgb((byte)(opacity * 255), tint.R, tint.G, tint.B));
    }

    /// <summary>Refresh from the 1 s snapshot tick. Rebuilds rows only when the numbers
    /// actually changed (same signature idiom as the chip windows).</summary>
    public void Update(StatsSnapshot s)
    {
        ApplyBackgroundOpacity();
        if (_kind == BreakoutKind.Buffs) { UpdateBuffs(s); return; }
        _lastFight = s.LastFight;
        _resists = MainWindow.SpellResistLookup(s);
        _blockedBy = BlockedBy?.Invoke(s);
        var fight = s.LastFight;
        var (title, stats, seconds, rateLabel) = _kind switch
        {
            BreakoutKind.Damage => (BreakoutPresentation.Title(BreakoutPresentation.Damage), _fightScope ? fight?.ByAbility ?? [] : s.DamageBySource,
                _fightScope ? fight?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
            BreakoutKind.Healing => (BreakoutPresentation.Title(BreakoutPresentation.Healing), _fightScope ? fight?.HealsBySpell ?? [] : s.HealsBySpell,
                _fightScope ? fight?.DurationSeconds ?? 0 : s.CombatSeconds, "hps"),
            _ => (BreakoutPresentation.PetTitle(s.PetName, s.CharmedSince, DateTime.Now),
                _fightScope ? fight?.PetAbilities ?? [] : s.PetAbilities,
                _fightScope ? fight?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
        };
        _title.Text = title;
        var rate = stats.Sum(row => row.Total) / Math.Max(1, seconds);
        // Hymn/regen ticks carry no amounts in the log, so they can never join the HPS
        // rows — but a bard mid-song staring at "no healing" reads it as broken (David,
        // live test 2026-08-06). Count them where healing lives; estimate when attributed.
        var regen = _kind == BreakoutKind.Healing && s.RegenTicks > 0
            ? s.RegenEstimatedHealed > 0
                ? $" · est. ~{s.RegenEstimatedHealed:N0} regen ({s.RegenTicks} ticks)"
                : $" · {s.RegenTicks} regen ticks"
            : "";
        _subtitle.Text = (_fightScope
            ? fight is null ? "No fights yet"
                : $"{fight.Name} · {fight.DurationSeconds:0}s · {fight.Outcome} · {rate:0.#} {rateLabel}"
            : $"Session · {s.CombatSeconds / 60:0}m in combat · {rate:0.#} {rateLabel}") + regen;
        var empty = stats.Count == 0;
        _empty.IsVisible = empty;
        if (empty)
        {
            _empty.Text = _kind switch
            {
                BreakoutKind.Healing when s.RegenEstimatedHealed > 0 =>
                    $"{s.RegenSpell}: est. ~{s.RegenEstimatedHealed:N0} healed over {s.RegenTicks} ticks.\n" +
                    "The game logs no amounts — this is ticks × your Options\nhp/tick (or the wiki base), so it stays labeled est.",
                BreakoutKind.Healing when s.RegenTicks > 0 =>
                    $"{s.RegenTicks} hymn/regen ticks — the game logs no amounts for these,\nso they count but can't join the HPS rows.",
                BreakoutKind.Healing => "No healing seen yet.",
                BreakoutKind.Pet => "No pet damage seen yet.",
                _ => "No damage seen yet.",
            };
            _rows.Children.Clear();
            _signature = "";
            return;
        }
        var signature = $"{_fightScope}|{fight?.Name}|{seconds:0}|{string.Join(',', stats.Select(row => $"{row.Name}:{row.Total}"))}";
        if (signature == _signature) return;
        _signature = signature;
        // Resist % rides only the session-scope damage rows — the tallies are
        // session-wide, and stamping them on a single fight would misstate it.
        var resists = _kind == BreakoutKind.Damage && !_fightScope ? _resists : null;
        BreakdownRows.FillAbilityRowsSorted(_rows, stats, StatSort.Total, Math.Max(1, seconds),
            rateLabel, max: 10, resists: resists, blockedBy: resists is null ? null : _blockedBy);
    }

    // ---- the buff set (#120 stage 2/3, Frankthetankk) ----

    private readonly ComboBox _buffClassBox = new() { FontSize = 11, MinWidth = 104 };
    private readonly TextBox _buffAddBox = new() { FontSize = 11, Watermark = "add a buff…" };
    private readonly ListBox _buffMatches = new() { FontSize = 11, MaxHeight = 150 };
    private Popup? _buffPopup;
    private readonly List<TextBlock> _buffSetClocks = [];
    private string _buffBucketsMemo = "\0";
    private bool _lossesOpen;

    private Control BuildBuffEditor()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        ToolTip.SetTip(_buffClassBox,
            "Which bucket an added buff joins. (any class) picks always apply; a class "
            + "bucket applies only while that class is in your combination.");
        row.Children.Add(_buffClassBox);
        _buffAddBox.Margin = new Thickness(4, 0, 0, 0);
        ToolTip.SetTip(_buffAddBox,
            "Type two letters or more — buffs you were seen casting rank first.");
        _buffAddBox.TextChanged += OnBuffSearchChanged;
        Grid.SetColumn(_buffAddBox, 1);
        row.Children.Add(_buffAddBox);

        _buffMatches.Background = AppTheme.PopupBrush;
        _buffMatches.Foreground = AppTheme.TextBrush;
        _buffMatches.SelectionChanged += OnBuffMatchPicked;
        _buffPopup = new Popup
        {
            PlacementTarget = _buffAddBox,
            Placement = PlacementMode.Bottom,
            Child = new Border
            {
                Background = AppTheme.PopupBrush,
                BorderBrush = AppTheme.AccentBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = _buffMatches,
                MinWidth = 200,
            },
        };
        row.Children.Add(_buffPopup);
        return row;
    }

    private string SelectedBuffBucket => _buffClassBox.SelectedItem as string ?? BuffSetStore.AnyClass;

    /// <summary>The add-target buckets: "(any class)" plus the active combination.
    /// Parked classes (stored, not active) are the Options editor's business — this
    /// window shows the assembled set.</summary>
    private void RefreshBuffClassChoices(IReadOnlyList<string> classes)
    {
        var memo = string.Join("|", classes);
        if (memo == _buffBucketsMemo && _buffClassBox.ItemCount > 0) return;
        _buffBucketsMemo = memo;
        var keep = _buffClassBox.SelectedItem as string;
        var items = new List<string> { BuffSetStore.AnyClass };
        items.AddRange(classes);
        _buffClassBox.ItemsSource = items;
        _buffClassBox.SelectedItem = keep is not null && items.Contains(keep) ? keep : BuffSetStore.AnyClass;
    }

    private void OnBuffSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (BuffHost is not { } host || host.BuffSetKey is not { Length: > 0 } key
            || _buffPopup is null) return;
        var query = (_buffAddBox.Text ?? "").Trim();
        if (query.Length < 2) { _buffPopup.IsOpen = false; return; }
        var inBucket = BuffSetStore.SpellsFor(
            host.Settings.BuffSetsByClass.GetValueOrDefault(key), SelectedBuffBucket);
        var items = BuffSetSearch.Rank(query, host.SeenBuffCasts(), inBucket,
                BuffDurationCatalog.Default.SpellNames)
            .Select(m => new ListBoxItem
            {
                Content = m.Seen ? m.Spell + "   · seen this session" : m.Spell,
                Tag = m.Spell,
            })
            .ToList();
        if (items.Count == 0)
            items.Add(new ListBoxItem
            {
                Content = "No buff in the catalog matches — check the spelling?",
                IsEnabled = false,
            });
        _buffMatches.ItemsSource = items;
        _buffPopup.IsOpen = true;
    }

    private void OnBuffMatchPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_buffMatches.SelectedItem is not ListBoxItem { Tag: string spell }) return;
        if (_buffPopup is not null) _buffPopup.IsOpen = false;
        _buffMatches.SelectedItem = null;
        if (BuffHost is not { } host || host.BuffSetKey is not { Length: > 0 } key) return;
        BuffSetStore.Add(host.Settings.BuffSetsByClass, key, SelectedBuffBucket, spell);
        host.Settings.Save();
        _buffAddBox.Text = "";   // TextChanged with an empty box closes the popup
        host.OnBuffSetEdited();
    }

    /// <summary>An edit arrived from the other editor — repaint now, not next tick.</summary>
    public void RefreshBuffSet(StatsSnapshot s)
    {
        if (_kind != BreakoutKind.Buffs) return;
        _signature = "";
        UpdateBuffs(s);
    }

    /// <summary>The set, per class bucket, with each entry's live honesty state, the
    /// card's suggestion rows mirrored, and the lost-buff history folded at the bottom.
    /// Edits route through the host so card, Options and this window repaint at once —
    /// a change that waits for the next tick reads as a silent no-op.</summary>
    private void UpdateBuffs(StatsSnapshot s)
    {
        _title.Text = BreakoutPresentation.Title(BreakoutPresentation.Buffs);
        if (BuffHost is not { } host || host.BuffSetKey is not { Length: > 0 } key)
        {
            _subtitle.Text = "No character detected yet";
            _empty.Text = "Once today's log names your character,\nthe set unlocks here.";
            _empty.IsVisible = true;
            _rows.Children.Clear();
            _buffSetClocks.Clear();
            _buffAddBox.IsEnabled = false;
            _buffClassBox.IsEnabled = false;
            _signature = "";
            return;
        }
        _buffAddBox.IsEnabled = true;
        _buffClassBox.IsEnabled = true;

        var now = DateTime.Now;
        // The RESOLVED source, not a hardcoded "inferred" — Bevel, Helm-signed 2026-08-23:
        // passing ClassSource.Inferred always meant "a dump-sourced trio still reads as a
        // guess", which is the exact fact-vs-guess distinction these words exist to carry.
        var (classes, classSource) = host.ClassSourceFor(s);
        // The class filter, visible: the combination this set is assembled for, its
        // source named honestly — the log has no /who or loadout-change line to read.
        _subtitle.Text = host.BuffSetCharacterName + " · " + (classes.Count > 0
            // Where the classes came from, in the shared words — "(inferred)" said only
            // one of the three things this can now be, and said nothing at all when the
            // GAME had told us via an achievements dump.
            ? string.Join("/", classes.Select(QuestClassFilter.Abbrev))
                + $" ({CharacterClasses.SourceLabel(classSource)})"
            : "no classes known yet");
        ToolTip.SetTip(_subtitle,
            "Classes come from your Quest Tracker picks, falling back to the class "
            + "inferred from your combat log — EQ Legends logs announce no loadout changes, so "
            + "this is the signal EQBuddy honestly has. (any class) picks always apply. "
            + "Swap a class and the other classes' picks stay put.");
        RefreshBuffClassChoices(classes);

        var sections = host.BuffSetSectionStates(s, now);
        // Stage 3 (#120): the card's suggestion rows mirror here, and the lost-buff
        // history folds at the bottom — both live content, both in the signature.
        var suggestions = host.BuffSuggestionsFor(s, host.AssembledBuffSet(classes));
        var losses = host.BuffLosses.Snapshot();
        if (sections.All(sec => sec.Entries.Count == 0) && suggestions.Count == 0 && losses.Count == 0)
        {
            _empty.Text = "Nothing picked yet — choose a class bucket below\nand type a buff to build the set.";
            _empty.IsVisible = true;
            _rows.Children.Clear();
            _buffSetClocks.Clear();
            _signature = "";
            return;
        }
        _empty.IsVisible = false;

        // Signature covers spells and STATUSES, not countdown text — clocks update in
        // place on a match, so a ticking timer never forces a rebuild. Losses key on
        // the newest entry too: at the cap the count alone stops moving.
        var flat = sections.SelectMany(sec => sec.Entries).ToList();
        var sig = "buffs|" + _subtitle.Text + "|" + string.Join(";", sections.Select(sec =>
                sec.Class + ":" + string.Join(",", sec.Entries.Select(e => $"{e.Spell}·{e.Status}"))))
            + "|sug:" + string.Join(",", suggestions.Select(x => x.Spell + "@" + x.Class))
            + "|loss:" + losses.Count + (_lossesOpen ? "-open" : "-shut")
            + (losses.Count > 0 ? losses[0].Time.Ticks + losses[0].Spell : "");
        if (sig == _signature)
        {
            for (var i = 0; i < _buffSetClocks.Count && i < flat.Count; i++)
                _buffSetClocks[i].Text = BuffStatusText(flat[i]);
            return;
        }
        _signature = sig;
        _buffSetClocks.Clear();

        _rows.Children.Clear();
        foreach (var (cls, entries) in sections)
        {
            _rows.Children.Add(AppTheme.Heading(cls));
            if (entries.Count == 0)
            {
                // The empty section is deliberate furniture: it shows the bucket a
                // freshly swapped-in class gets, right where adding happens.
                _rows.Children.Add(new TextBlock
                {
                    Text = "nothing picked for this class yet",
                    FontSize = 11,
                    FontStyle = FontStyle.Italic,
                    Margin = new Thickness(4, 0, 0, 2),
                    Foreground = AppTheme.DimBrush,
                });
                continue;
            }
            foreach (var entry in entries) _rows.Children.Add(BuffSetRow(host, key, cls, entry));
        }
        foreach (var sug in suggestions) _rows.Children.Add(BuffSuggestionRow(host, sug));
        AddBuffLossFold(host, losses);
    }

    /// <summary>One set entry: name and clock in the stage-1 state brushes (Active =
    /// good, Expiring = accent, Missing = warn, NotSeen = dim italic), ✕ = remove
    /// from THIS class bucket only.</summary>
    private Grid BuffSetRow(IBuffSetHost host, string key, string cls, BuffSetEntryState entry)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(4, 1, 0, 1),
        };
        var (brush, italic) = entry.Status switch
        {
            BuffSetStatus.Active => (AppTheme.GoodBrush, false),
            BuffSetStatus.Expiring => (AppTheme.AccentBrush, false),
            BuffSetStatus.Missing => (AppTheme.WarnBrush, false),
            _ => (AppTheme.DimBrush, true),
        };
        var name = new TextBlock
        {
            Text = entry.Spell,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = brush,
        };
        if (italic) name.FontStyle = FontStyle.Italic;
        row.Children.Add(name);
        var clock = new TextBlock
        {
            Text = BuffStatusText(entry),
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = brush,
        };
        ToolTip.SetTip(clock, entry.Status switch
        {
            BuffSetStatus.Missing => "Seen fading this session, or its timer ran out — rebuff.",
            BuffSetStatus.Expiring => "Still up, but inside the warn window.",
            BuffSetStatus.NotSeen => "No landing line this session — it may be up from before "
                + "EQBuddy was watching; the log can't tell, so this stays its own honest state.",
            _ => "Up, counting down.",
        });
        Grid.SetColumn(clock, 1);
        row.Children.Add(clock);
        _buffSetClocks.Add(clock);
        var remove = DesignSystem.IconButton("Close", $"Remove {entry.Spell} from {cls}");
        remove.FontSize = 11;
        remove.Margin = new Thickness(4, 0, 0, 0);
        remove.Click += (_, _) =>
        {
            BuffSetStore.Remove(host.Settings.BuffSetsByClass, key, cls, entry.Spell);
            host.Settings.Save();
            host.OnBuffSetEdited();
        };
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);
        return row;
    }

    /// <summary>The card's suggestion row, mirrored (#120 stage 3): dim, ✓ add to the
    /// gaining class's bucket / ✕ dismiss for good — never auto-added.</summary>
    private static Grid BuffSuggestionRow(IBuffSetHost host, BuffSuggestion sug)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(4, 3, 0, 1),
        };
        var text = new TextBlock
        {
            Text = $"new at your level — add {sug.Spell} to {sug.Class}?",
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = AppTheme.DimBrush,
        };
        ToolTip.SetTip(text,
            "Your level-up made this buff available. The tick adds it to that class's "
            + "bucket; the cross never asks again for this character. A new RANK of a set "
            + "buff folds into the same slot and is never suggested.");
        row.Children.Add(text);
        var add = DesignSystem.IconButton("Check", $"Add {sug.Spell} to your {sug.Class} set", colorKey: "GoodBrush");
        add.FontSize = 11;
        add.Margin = new Thickness(4, 0, 0, 0);
        add.Click += (_, _) => host.AcceptBuffSuggestion(sug);
        Grid.SetColumn(add, 1);
        row.Children.Add(add);
        var dismiss = DesignSystem.IconButton("Close",
            "Dismiss — never suggest this buff for this character again");
        dismiss.FontSize = 11;
        dismiss.Margin = new Thickness(4, 0, 0, 0);
        dismiss.Click += (_, _) => host.DismissBuffSuggestion(sug);
        Grid.SetColumn(dismiss, 2);
        row.Children.Add(dismiss);
        return row;
    }

    /// <summary>The lost-buff history fold (#120 stage 3, Frankthetankk): "▸ lost this
    /// session (N)" at the bottom of the breakout — time · buff · cause per row, the
    /// AA-list fold idiom. ⧉ on the header copies the list as plain text: the
    /// requester's dev-report evidence ("Buff X was active, NPC Y cast debuff Z,
    /// Buff X was gone"), same content-copy style as the fight export.</summary>
    private void AddBuffLossFold(IBuffSetHost host, List<BuffLossEntry> losses)
    {
        if (losses.Count == 0) return;
        var head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 5, 0, 0),
        };
        // The chevron was typed into the label. A vector now, and still a chevron: it is
        // the only thing saying whether the fold is open.
        var chevron = DesignSystem.Icon(_lossesOpen ? "ChevronDown" : "ChevronRight",
            size: EQBuddy.UI.Shared.DesignTokens.IconInline);
        chevron.Cursor = new Cursor(StandardCursorType.Hand);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        chevron.Margin = new Thickness(0, 0, EQBuddy.UI.Shared.DesignTokens.SpaceXs, 0);
        head.Children.Add(chevron);
        var label = new TextBlock
        {
            Text = $"lost this session ({losses.Count})",
            FontSize = 11,
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = AppTheme.DimBrush,
        };
        ToolTip.SetTip(label,
            "Every set buff that went missing this session, newest first, with "
            + "the best cause the log names: expired (the countdown ran out; est = "
            + "the duration was still the wiki-base estimate), faded (the wear-off "
            + "line), \"lost as X landed\" (a hostile spell landed on you within "
            + "2 s before the fade), lost on death.");
        label.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _lossesOpen = !_lossesOpen;
            RefreshBuffSet(host.CurrentSnapshot());   // repaint now, not next tick
        };
        Grid.SetColumn(label, 1);
        head.Children.Add(label);
        var copy = DesignSystem.Icon("Copy", size: EQBuddy.UI.Shared.DesignTokens.IconInline);
        copy.Cursor = new Cursor(StandardCursorType.Hand);
        copy.Margin = new Thickness(EQBuddy.UI.Shared.DesignTokens.SpaceXs, 0, 0, 0);
        ToolTip.SetTip(copy,
            "Copy the list as plain text — evidence for a bug report to the game devs.");
        copy.PointerPressed += async (_, e) =>
        {
            e.Handled = true;
            try
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
                    await cb.SetTextAsync(host.BuffLosses.ExportText(host.BuffSetCharacterName));
                copy.Data = StreamGeometry.Parse(IconPaths.Path("Check"));
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (_, _) => { copy.Data = StreamGeometry.Parse(IconPaths.Path("Copy")); t.Stop(); };
                t.Start();
            }
            catch (Exception ex) { App.LogError(ex); }
        };
        Grid.SetColumn(copy, 2);
        head.Children.Add(copy);
        _rows.Children.Add(head);
        if (!_lossesOpen) return;
        foreach (var loss in losses)
        {
            var row = new TextBlock
            {
                Text = $"{loss.Time:h:mm:ss tt}  {loss.Spell} — {loss.Cause}",
                FontSize = 10.5,
                Margin = new Thickness(8, 0, 0, 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = AppTheme.DimBrush,
            };
            ToolTip.SetTip(row, $"{loss.Spell} — {loss.Cause} at {loss.Time:h:mm:ss tt}");
            _rows.Children.Add(row);
        }
    }

    private static string BuffStatusText(BuffSetEntryState entry) => entry.Status switch
    {
        BuffSetStatus.Missing => "missing",
        BuffSetStatus.NotSeen => "not seen",
        _ => entry.RemainingSeconds is { } r ? $"{(int)r / 60}:{(int)r % 60:00}" : "up",
    };

    public void HideAndSave() { SavePosition(); Hide(); }

    private void RestorePosition()
    {
        var (left, top) = PositionSetting();
        if (ScreenGuard.OnScreen(this, left, top, Width, 120)) Position = new PixelPoint((int)left, (int)top);
        else if (Screens.Primary is { } screen)
            Position = new PixelPoint(screen.WorkingArea.Right - (int)(Width * screen.Scaling) - 40,
                screen.WorkingArea.Y + 80 + 150 * (int)_kind);
        _savedPosition = Position;
    }

    private void SavePosition()
    {
        var p = _savedPosition;
        switch (_kind)
        {
            case BreakoutKind.Damage: _settings.BreakoutDamageLeft = p.X; _settings.BreakoutDamageTop = p.Y; break;
            case BreakoutKind.Healing: _settings.BreakoutHealingLeft = p.X; _settings.BreakoutHealingTop = p.Y; break;
            case BreakoutKind.Buffs: _settings.BreakoutBuffsLeft = p.X; _settings.BreakoutBuffsTop = p.Y; break;
            default: _settings.BreakoutPetLeft = p.X; _settings.BreakoutPetTop = p.Y; break;
        }
        _settings.Save();
    }

    private (double, double) PositionSetting() => _kind switch
    {
        BreakoutKind.Damage => (_settings.BreakoutDamageLeft, _settings.BreakoutDamageTop),
        BreakoutKind.Healing => (_settings.BreakoutHealingLeft, _settings.BreakoutHealingTop),
        BreakoutKind.Buffs => (_settings.BreakoutBuffsLeft, _settings.BreakoutBuffsTop),
        _ => (_settings.BreakoutPetLeft, _settings.BreakoutPetTop),
    };

    private string ScopeSetting() => _kind switch
    {
        BreakoutKind.Damage => _settings.BreakoutDamageScope,
        BreakoutKind.Healing => _settings.BreakoutHealingScope,
        _ => _settings.BreakoutPetScope,
    };

    private void SetScopeSetting(string value)
    {
        if (_kind == BreakoutKind.Damage) _settings.BreakoutDamageScope = value;
        else if (_kind == BreakoutKind.Healing) _settings.BreakoutHealingScope = value;
        else _settings.BreakoutPetScope = value;
        _settings.Save();
    }

    private void PaintScope()
    {
        _fight.Foreground = _fightScope ? AppTheme.AccentBrush : AppTheme.DimBrush;
        _session.Foreground = _fightScope ? AppTheme.DimBrush : AppTheme.AccentBrush;
    }
}
